using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using System.Xml;
using PNNLOmics.Utilities;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsBrukerXmassFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        public const string BRUKER_BAF_FILE_NAME = "analysis.baf";
        public const string BRUKER_SER_FILE_NAME = "ser";
        public const string BRUKER_FID_FILE_NAME = "fid";
        public const string BRUKER_EXTENSION_BAF_FILE_NAME = "extension.baf";
        public const string BRUKER_ANALYSIS_YEP_FILE_NAME = "analysis.yep";

        public const string BRUKER_SQLITE_INDEX_FILE_NAME = "Storage.mcf_idx";
        // Note: The extension must be in all caps
        public const string BRUKER_BAF_FILE_EXTENSION = ".BAF";
        public const string BRUKER_MCF_FILE_EXTENSION = ".MCF";

        public const string BRUKER_SQLITE_INDEX_EXTENSION = ".MCF_IDX";
        private const string BRUKER_SCANINFO_XML_FILE = "scan.xml";
        private const string BRUKER_XMASS_LOG_FILE = "log.txt";

        private const string BRUKER_AUTOMS_FILE = "AutoMS.txt";

        private struct udtMCFScanInfoType
        {
            public double ScanMode;
            public int MSLevel;
            public double RT;
            public double BPI;
            public double TIC;
            public DateTime AcqTime;

            // Only used with MALDI imaging
            public string SpotNumber;
        }

        private enum eMcfMetadataFields
        {
            ScanMode = 0,
            MSLevel = 1,
            RT = 2,
            BPI = 3,
            TIC = 4,
            AcqTime = 5,
            SpotNumber = 6
        }

        private void AddDatasetScan(
            int intScanNumber,
            int intMSLevel,
            float sngElutionTime,
            double dblBPI,
            double dblTIC,
            string strScanTypeName,
            ref double dblMaxRunTimeMinutes)
        {
            if (mSaveTICAndBPI && intScanNumber > 0)
            {
                mTICandBPIPlot.AddData(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC);
            }

            var objScanStatsEntry = new clsScanStatsEntry
            {
                ScanNumber = intScanNumber,
                ScanType = intMSLevel,
                ScanTypeName = strScanTypeName,
                ScanFilterText = "",
                ElutionTime = sngElutionTime.ToString("0.0000"),
                TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5),
                BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5),
                BasePeakMZ = "0",
                BasePeakSignalToNoiseRatio = "0",
                IonCount = 0,
                IonCountRaw = 0
            };


            double dblElutionTime;
            if (double.TryParse(objScanStatsEntry.ElutionTime, out dblElutionTime))
            {
                if (dblElutionTime > dblMaxRunTimeMinutes)
                {
                    dblMaxRunTimeMinutes = dblElutionTime;
                }
            }

            mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

        }

        /// <summary>
        /// Looks for a .m folder then looks for apexAcquisition.method or submethods.xml in that folder
        /// Uses the file modification time as the run start time
        /// Also looks for the .hdx file in the dataset folder and examine its modification time
        /// </summary>
        /// <param name="diDatasetFolder"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if a valid file is found; otherwise false</returns>
        /// <remarks></remarks>
        private bool DetermineAcqStartTime(DirectoryInfo diDatasetFolder, clsDatasetFileInfo datasetFileInfo)
        {

            var blnSuccess = false;

            try
            {
                // Look for the method folder (folder name should end in .m)
                var diSubFolders = diDatasetFolder.GetDirectories("*.m").ToList();

                if (diSubFolders.Count == 0)
                {
                    // Match not found
                    // Look for any XMass folders
                    diSubFolders = diDatasetFolder.GetDirectories("XMass*").ToList();
                }

                if (diSubFolders.Count > 0)
                {
                    // Look for the apexAcquisition.method in each matching subfolder
                    // We have historically used that file's modification time as the acquisition start time for the dataset
                    // However, we've found that on the 12T a series of datasets will all use the same method file and thus the modification time is not necessarily appropriate

                    // Note that the submethods.xml file sometimes gets modified after the run starts, so it should not be used to determine run start time

                    foreach (var diSubFolder in diSubFolders)
                    {
                        foreach (var fiFile in diSubFolder.GetFiles("apexAcquisition.method"))
                        {
                            datasetFileInfo.AcqTimeStart = fiFile.LastWriteTime;
                            blnSuccess = true;
                            break;
                        }
                        if (blnSuccess)
                            break;
                    }

                    if (!blnSuccess)
                    {
                        // apexAcquisition.method not found; try submethods.xml instead
                        foreach (var diSubFolder in diSubFolders)
                        {
                            foreach (var fiFile in diSubFolder.GetFiles("submethods.xml"))
                            {
                                datasetFileInfo.AcqTimeStart = fiFile.LastWriteTime;
                                blnSuccess = true;
                                break;
                            }
                            if (blnSuccess)
                                break;
                        }
                    }

                }

                // Also look for the .hdx file
                // Its file modification time typically also matches the run start time

                foreach (var fiFile in diDatasetFolder.GetFiles("*.hdx"))
                {
                    if (!blnSuccess || fiFile.LastWriteTime < datasetFileInfo.AcqTimeStart)
                    {
                        datasetFileInfo.AcqTimeStart = fiFile.LastWriteTime;
                    }

                    blnSuccess = true;
                    break;
                }

                // Make sure AcqTimeEnd and AcqTimeStart match
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error finding XMass method folder: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private FileInfo FindBrukerSettingsFile(DirectoryInfo diDotDFolder)
        {

            var dotMethodFiles = diDotDFolder.GetFiles("*.method", SearchOption.AllDirectories);

            if (dotMethodFiles.Length == 0)
            {
                return null;
            }

            var acquistionMethodFiles = (from methodFile in dotMethodFiles where methodFile.Name.ToLower().EndsWith("apexacquisition.method") select methodFile).ToList();

            if (acquistionMethodFiles.Count == 0)
            {
                return null;
            }

            if (acquistionMethodFiles.Count == 1)
            {
                return acquistionMethodFiles.First();
            }

            OnErrorEvent("Multiple 'apexAcquisition.method' files were found in the .D folder; not sure which to use");
            return null;

        }

        private FileInfo FindBrukerAcqusFile(DirectoryInfo diDotDFolder)
        {

            var acqusFiles = diDotDFolder.GetFiles("acqus", SearchOption.AllDirectories);

            if (acqusFiles.Length == 0)
            {
                return null;
            }

            if (acqusFiles.Length == 1)
            {
                return acqusFiles.First();
            }

            // Often the Bruker file structures contain multiple Acqus files. I will select 
            // the one that is in the same folder as the 'ser' file and if that isn't present,
            // the same folder as the 'fid' file. Otherwise, throw errors


            foreach (var acquFile in acqusFiles)
            {
                if (acquFile.Directory != null && acquFile.Directory.Name.Equals(diDotDFolder.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return acquFile;
                }
            }

            OnErrorEvent("Multiple 'acqus' files were found in the .D folder; not sure which one to use");
            return null;

        }

        private bool GetMetaDataFieldAndTable(eMcfMetadataFields eMcfMetadataField, out string strField, out string strTable)
        {

            switch (eMcfMetadataField)
            {
                case eMcfMetadataFields.ScanMode:
                    strField = "pScanMode";
                    strTable = "MetaDataInt";

                    break;
                case eMcfMetadataFields.MSLevel:
                    strField = "pMSLevel";
                    strTable = "MetaDataInt";

                    break;
                case eMcfMetadataFields.RT:
                    strField = "pRT";
                    strTable = "MetaDataDouble";

                    break;
                case eMcfMetadataFields.BPI:
                    strField = "pIntMax";
                    strTable = "MetaDataDouble";

                    break;
                case eMcfMetadataFields.TIC:
                    strField = "pTic";
                    strTable = "MetaDataDouble";

                    break;
                case eMcfMetadataFields.AcqTime:
                    strField = "pDateTime";
                    strTable = "MetaDataString";

                    break;
                case eMcfMetadataFields.SpotNumber:
                    strField = "pSpotNo";
                    strTable = "MetaDataString";

                    break;
                default:
                    // Unknown field
                    strField = string.Empty;
                    strTable = string.Empty;
                    return false;
            }

            return true;
        }

        private bool ParseAutoMSFile(DirectoryInfo diDatasetFolder)
        {

            try
            {
                var strAutoMSFilePath = Path.Combine(diDatasetFolder.FullName, BRUKER_AUTOMS_FILE);
                var fiFileInfo = new FileInfo(strAutoMSFilePath);

                if (!fiFileInfo.Exists)
                {
                    return false;
                }

                using (var srReader = new StreamReader(new FileStream(fiFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {

                    while (!srReader.EndOfStream)
                    {
                        var strLineIn = srReader.ReadLine();

                        if (string.IsNullOrEmpty(strLineIn))
                        {
                            continue;
                        }

                        var strSplitLine = strLineIn.Split('\t');

                        if (strSplitLine.Length < 2)
                        {
                            continue;
                        }

                        int intScanNumber;
                        if (!int.TryParse(strSplitLine[0], out intScanNumber))
                        {
                            continue;
                        }

                        // First column contains a number
                        // See if the second column is a known scan type

                        var intMSLevel = 0;

                        string strScanTypeName;
                        switch (strSplitLine[1])
                        {
                            case "MS":
                                strScanTypeName = "HMS";
                                intMSLevel = 1;
                                break;
                            case "MSMS":
                                strScanTypeName = "HMSn";
                                intMSLevel = 2;
                                break;
                            default:
                                strScanTypeName = string.Empty;
                                break;
                        }

                        mDatasetStatsSummarizer.UpdateDatasetScanType(intScanNumber, intMSLevel, strScanTypeName);
                    }

                }

                return true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error finding AutoMS.txt file: " + ex.Message, ex);
                return false;
            }

        }

        private bool ParseBAFFile(FileInfo fiBAFFileInfo, clsDatasetFileInfo datasetFileInfo)
        {

            bool blnSuccess;

            // Override strDataFilePath here, if needed
            var blnOverride = false;
            if (blnOverride)
            {
                var strNewDataFilePath = "c:\\temp\\analysis.baf";
                fiBAFFileInfo = new FileInfo(strNewDataFilePath);
            }

            mDatasetStatsSummarizer.ClearCachedData();
            mLCMS2DPlot.Options.UseObservedMinScan = false;

            try
            {
                if (fiBAFFileInfo.Length > 1024 * 1024 * 1024)
                {
                    OnWarningEvent("analysis.baf file is over 1 GB; ProteoWizard typically cannot handle .baf files this large");

                    // Look for a ser file
                    if (fiBAFFileInfo.Directory != null && File.Exists(Path.Combine(fiBAFFileInfo.Directory.FullName, "ser")))
                    {
                        OnStatusEvent("Will parse the ser file instead");
                        return false;
                    }

                    OnWarningEvent("Ser file not found; trying ProteoWizard anyway");
                }

                // Open the analysis.baf (or extension.baf) file using the ProteoWizardWrapper
                OnDebugEvent("Determining acquisition info using Proteowizard");

                var objPWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(fiBAFFileInfo.FullName);

                try
                {
                    var dtRunStartTime = Convert.ToDateTime(objPWiz.RunStartTime);

                    // Update AcqTimeEnd if possible
                    // Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
                    dtRunStartTime = dtRunStartTime.ToUniversalTime();
                    if (dtRunStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(dtRunStartTime).TotalDays < 1)
                        {
                            datasetFileInfo.AcqTimeStart = dtRunStartTime;
                        }
                    }

                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the Proteowizard Data Parser class
                var pWizParser = new clsProteowizardDataParser(objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot, mLCMS2DPlot,
                                                            mSaveLCMS2DPlots, mSaveTICAndBPI, mCheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                RegisterEvents(pWizParser);

                // Note that SRM .Wiff files will only have chromatograms, and no spectra

                var blnTICStored = false;
                var blnSRMDataCached = false;
                double dblRuntimeMinutes = 0;

                if (objPWiz.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out blnTICStored, out blnSRMDataCached, out dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);

                    datasetFileInfo.ScanCount = objPWiz.ChromatogramCount;
                }


                if (objPWiz.SpectrumCount > 0 & !blnSRMDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    pWizParser.StoreMSSpectraInfo(datasetFileInfo, blnTICStored, ref dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);

                    datasetFileInfo.ScanCount = objPWiz.SpectrumCount;
                }

                objPWiz.Dispose();
                PRISM.Processes.clsProgRunner.GarbageCollectNow();

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error using ProteoWizard reader: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseMcfIndexFiles(DirectoryInfo diDatasetFolder, clsDatasetFileInfo datasetFileInfo)
        {


            try
            {
                var lstMetadataNameToID = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
                var lstScanData = new Dictionary<string, udtMCFScanInfoType>();

                if (mSaveTICAndBPI)
                {
                    // Initialize the TIC and BPI arrays
                    InitializeTICAndBPI();
                }

                if (mSaveLCMS2DPlots)
                {
                    InitializeLCMS2DPlot();
                }

                var strMetadataFile = Path.Combine(diDatasetFolder.FullName, BRUKER_SQLITE_INDEX_FILE_NAME);
                var fiFileInfo = new FileInfo(strMetadataFile);

                if (!fiFileInfo.Exists)
                {
                    // Storage.mcf_idx not found
                    OnWarningEvent("Note: " + BRUKER_SQLITE_INDEX_FILE_NAME + " file does not exist");
                    return false;
                }

                var strConnectionString = "Data Source = " + fiFileInfo.FullName + "; Version=3; DateTimeFormat=Ticks;";

                // Open the Storage.mcf_idx file to lookup the metadata name to ID mapping
                using (var cnDB = new SQLiteConnection(strConnectionString, true))
                {
                    cnDB.Open();

                    var cmd = new SQLiteCommand(cnDB)
                    {
                        CommandText = "SELECT metadataId, permanentName, displayName FROM MetadataId"
                    };


                    using (var drReader = cmd.ExecuteReader())
                    {

                        while (drReader.Read())
                        {
                            var intMetadataId = ReadDbInt(drReader, "metadataId");
                            var strMetadataName = ReadDbString(drReader, "permanentName");
                            // var strMetadataDescription = ReadDbString(drReader, "displayName");

                            if (intMetadataId > 0)
                            {
                                lstMetadataNameToID.Add(strMetadataName, intMetadataId);
                                // lstMetadataNameToDescription.Add(strMetadataName, strMetadataDescription);
                            }
                        }
                    }

                    cnDB.Close();
                }

                var fiFiles = diDatasetFolder.GetFiles("*_1.mcf_idx").ToList();

                if (fiFiles.Count == 0)
                {
                    // Storage.mcf_idx not found
                    OnWarningEvent("Note: " + BRUKER_SQLITE_INDEX_FILE_NAME + " file was found but _1.mcf_idx file does not exist");
                    return false;
                }

                strConnectionString = "Data Source = " + fiFiles.First().FullName + "; Version=3; DateTimeFormat=Ticks;";

                // Open the .mcf file to read the scan info
                using (var cnDB = new SQLiteConnection(strConnectionString, true))
                {
                    cnDB.Open();

                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.AcqTime);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.ScanMode);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.MSLevel);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.RT);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.BPI);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.TIC);
                    ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.SpotNumber);

                    cnDB.Close();
                }


                // Parse each entry in lstScanData
                // Copy the values to a generic list so that we can sort them
                var oScanDataSorted = new udtMCFScanInfoType[lstScanData.Count];
                lstScanData.Values.CopyTo(oScanDataSorted, 0);

                var oScanDataSortComparer = new clsScanDataSortComparer();
                Array.Sort(oScanDataSorted, oScanDataSortComparer);

                var dtAcqTimeStart = DateTime.MaxValue;
                var dtAcqTimeEnd = DateTime.MinValue;

                var intScanCount = 0;
                double dblMaxRunTimeMinutes = 0;

                for (var intIndex = 0; intIndex <= oScanDataSorted.Length - 1; intIndex++)
                {
                    intScanCount += 1;
                    var intScanNumber = intScanCount;

                    if (oScanDataSorted[intIndex].AcqTime < dtAcqTimeStart)
                    {
                        if (oScanDataSorted[intIndex].AcqTime > DateTime.MinValue)
                        {
                            dtAcqTimeStart = oScanDataSorted[intIndex].AcqTime;
                        }
                    }

                    if (oScanDataSorted[intIndex].AcqTime > dtAcqTimeEnd)
                    {
                        if (oScanDataSorted[intIndex].AcqTime < DateTime.MaxValue)
                        {
                            dtAcqTimeEnd = oScanDataSorted[intIndex].AcqTime;
                        }
                    }

                    if (oScanDataSorted[intIndex].MSLevel == 0)
                        oScanDataSorted[intIndex].MSLevel = 1;
                    var sngElutionTime = (float)(oScanDataSorted[intIndex].RT / 60.0);
                    string strScanTypeName;

                    if (string.IsNullOrEmpty(oScanDataSorted[intIndex].SpotNumber))
                    {
                        strScanTypeName = "HMS";
                    }
                    else
                    {
                        strScanTypeName = "MALDI-HMS";
                    }

                    AddDatasetScan(
                        intScanNumber, oScanDataSorted[intIndex].MSLevel, sngElutionTime,
                        oScanDataSorted[intIndex].BPI, oScanDataSorted[intIndex].TIC,
                        strScanTypeName, ref dblMaxRunTimeMinutes);

                }

                if (intScanCount > 0)
                {
                    datasetFileInfo.ScanCount = intScanCount;

                    if (dblMaxRunTimeMinutes > 0)
                    {
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblMaxRunTimeMinutes);
                    }

                    if (dtAcqTimeStart > DateTime.MinValue && dtAcqTimeEnd < DateTime.MaxValue)
                    {
                        // Update the acquisition times if they are within 7 days of datasetFileInfo.AcqTimeEnd
                        if (Math.Abs(datasetFileInfo.AcqTimeEnd.Subtract(dtAcqTimeEnd).TotalDays) <= 7)
                        {
                            datasetFileInfo.AcqTimeStart = dtAcqTimeStart;
                            datasetFileInfo.AcqTimeEnd = dtAcqTimeEnd;
                        }

                    }

                    return true;
                }

            }
            catch (Exception ex)
            {
                // Error parsing Storage.mcf_idx file
                OnErrorEvent("Error parsing " + BRUKER_SQLITE_INDEX_FILE_NAME + " file: " + ex.Message, ex);
                return false;
            }

            return false;

        }

        private bool ParseScanXMLFile(
            DirectoryInfo diDatasetFolder,
            clsDatasetFileInfo datasetFileInfo,
            out Dictionary<int, float> scanElutionTimeMap)
        {

            scanElutionTimeMap = new Dictionary<int, float>();


            try
            {
                if (mSaveTICAndBPI)
                {
                    // Initialize the TIC and BPI arrays
                    InitializeTICAndBPI();
                }

                var strScanXMLFilePath = Path.Combine(diDatasetFolder.FullName, BRUKER_SCANINFO_XML_FILE);
                var fiFileInfo = new FileInfo(strScanXMLFilePath);

                if (!fiFileInfo.Exists)
                {
                    return false;
                }

                var intScanCount = 0;
                double dblMaxRunTimeMinutes = 0;
                var validFile = false;

                using (var srReader = new XmlTextReader(new FileStream(fiFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {

                    var blnSkipRead = false;
                    var blnInScanNode = false;

                    var intScanNumber = 0;
                    float sngElutionTime = 0;
                    double dblTIC = 0;
                    double dblBPI = 0;
                    var intMSLevel = 0;

                    while (!srReader.EOF)
                    {
                        if (blnSkipRead)
                        {
                            blnSkipRead = false;
                        }
                        else
                        {
                            srReader.Read();
                        }

                        switch (srReader.NodeType)
                        {
                            case XmlNodeType.Element:
                                if (blnInScanNode)
                                {
                                    switch (srReader.Name)
                                    {
                                        case "count":
                                            intScanNumber = srReader.ReadElementContentAsInt();
                                            blnSkipRead = true;
                                            break;
                                        case "minutes":
                                            sngElutionTime = srReader.ReadElementContentAsFloat();
                                            blnSkipRead = true;
                                            break;
                                        case "tic":
                                            dblTIC = srReader.ReadElementContentAsFloat();
                                            blnSkipRead = true;
                                            break;
                                        case "maxpeak":
                                            dblBPI = srReader.ReadElementContentAsFloat();
                                            blnSkipRead = true;
                                            break;
                                        default:
                                            break;
                                        // Ignore it
                                    }
                                }
                                else
                                {
                                    if (srReader.Name == "scanlist")
                                    {
                                        validFile = true;
                                    }
                                    else if (srReader.Name == "scan")
                                    {
                                        blnInScanNode = true;
                                        intScanNumber = 0;
                                        sngElutionTime = 0;
                                        dblTIC = 0;
                                        dblBPI = 0;
                                        intMSLevel = 1;

                                        intScanCount += 1;
                                    }
                                }
                                break;
                            case XmlNodeType.EndElement:
                                if (srReader.Name == "scan")
                                {
                                    blnInScanNode = false;

                                    scanElutionTimeMap.Add(intScanNumber, sngElutionTime);
                                    AddDatasetScan(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC, "HMS", ref dblMaxRunTimeMinutes);

                                }
                                break;
                        }

                    }

                }

                if (intScanCount > 0)
                {
                    datasetFileInfo.ScanCount = intScanCount;

                    if (dblMaxRunTimeMinutes > 0)
                    {
                        if (Math.Abs(datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes) < dblMaxRunTimeMinutes)
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(dblMaxRunTimeMinutes);
                        }
                        else
                        {
                            datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblMaxRunTimeMinutes);
                        }
                    }

                    return true;
                }

                if (validFile)
                {
                    // The XML file is valid, but no scans were listed; must be a bad dataset
                    // Return true because there is no point in opening this dataset with ProteoWizard
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Error parsing the Scan.xml file
                OnErrorEvent("Error parsing " + BRUKER_SCANINFO_XML_FILE + " file: " + ex.Message, ex);
                return false;
            }

            return false;

        }

        private DirectoryInfo GetDatasetFolder(string strDataFilePath)
        {

            // First see if strFileOrFolderPath points to a valid file
            var fiFileInfo = new FileInfo(strDataFilePath);

            if (fiFileInfo.Exists)
            {
                // User specified a file; assume the parent folder of this file is the dataset folder
                return fiFileInfo.Directory;
            }
            
            // Assume this is the path to the dataset folder
            return new DirectoryInfo(strDataFilePath);
        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            var strDatasetName = string.Empty;

            try
            {
                // The dataset name for a Bruker Xmass folder is the name of the parent directory
                // However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
                var diDatasetFolder = GetDatasetFolder(strDataFilePath);
                strDatasetName = diDatasetFolder.Name;

                if (strDatasetName.ToLower().EndsWith(".d"))
                {
                    strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2);
                }

            }
            catch (Exception)
            {
                // Ignore errors
            }

            return strDatasetName;

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the XMass files in the dataset folder)

            try
            {
                // Determine whether strDataFilePath points to a file or a folder

                var diDatasetFolder = GetDatasetFolder(strDataFilePath);

                // Validate that we have selected a valid folder
                if (!diDatasetFolder.Exists)
                {
                    OnErrorEvent("File/folder not found: " + strDataFilePath);
                    return false;
                }

                // In case we cannot find a .BAF file, update the .AcqTime values to the folder creation date
                // We have to assign a date, so we'll assign the date for the BAF file
                datasetFileInfo.AcqTimeStart = diDatasetFolder.CreationTime;
                datasetFileInfo.AcqTimeEnd = diDatasetFolder.CreationTime;

                // Look for the analysis.baf file in diFolderInfo
                // Use its modification time as the AcqTime start and End values
                // If we cannot find the anslysis.baf file, then look for a ser file or a fid file

                var lstInstrumentDataFiles = new List<string> {
                    BRUKER_BAF_FILE_NAME,
                    BRUKER_SER_FILE_NAME,
                    BRUKER_FID_FILE_NAME,
                    BRUKER_EXTENSION_BAF_FILE_NAME
                };

                var fiFiles = new List<FileInfo>();

                foreach (var instrumentDataFile in lstInstrumentDataFiles)
                {
                    fiFiles = diDatasetFolder.GetFiles(instrumentDataFile).ToList();
                    if (fiFiles.Count > 0)
                    {
                        break;
                    }
                }

                if (fiFiles.Count == 0)
                {
                    //.baf files not found; look for any .mcf files
                    fiFiles = diDatasetFolder.GetFiles("*" + BRUKER_MCF_FILE_EXTENSION).ToList();

                    if (fiFiles.Count > 0)
                    {
                        // Find the largest .mcf file (not .mcf_idx file)
                        FileInfo fiLargestMCF = null;

                        foreach (var fiMCFFile in fiFiles)
                        {
                            if (fiMCFFile.Extension.ToUpper() == BRUKER_MCF_FILE_EXTENSION)
                            {
                                if (fiLargestMCF == null)
                                {
                                    fiLargestMCF = fiMCFFile;
                                }
                                else if (fiMCFFile.Length > fiLargestMCF.Length)
                                {
                                    fiLargestMCF = fiMCFFile;
                                }
                            }
                        }

                        if (fiLargestMCF == null)
                        {
                            // Didn't actually find a .MCF file; clear fiFiles
                            fiFiles.Clear();
                        }
                        else
                        {
                            fiFiles.Clear();
                            fiFiles.Add(fiLargestMCF);
                        }
                    }
                }

                if (fiFiles.Count == 0)
                {
                    OnErrorEvent(
                        string.Join(" or ", lstInstrumentDataFiles) + " or " + 
                        BRUKER_MCF_FILE_EXTENSION + " or " + 
                        BRUKER_SQLITE_INDEX_EXTENSION + " file not found in " + diDatasetFolder.FullName);
                    return false;
                }

                var fiFileInfo = fiFiles.First();

                // Read the file info from the file system
                // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
                UpdateDatasetFileStats(fiFileInfo, datasetFileInfo.DatasetID);

                // Update the dataset name and file extension
                datasetFileInfo.DatasetName = GetDatasetNameViaPath(diDatasetFolder.FullName);
                datasetFileInfo.FileExtension = string.Empty;
                datasetFileInfo.FileSizeBytes = fiFileInfo.Length;

                // Find the apexAcquisition.method or submethods.xml file in the XMASS_Method.m subfolder to determine .AcqTimeStart
                // This function updates datasetFileInfo.AcqTimeEnd and datasetFileInfo.AcqTimeStart to have the same time
                DetermineAcqStartTime(diDatasetFolder, datasetFileInfo);

                // Update the acquisition end time using the write time of the .baf file
                if (fiFileInfo.LastWriteTime > datasetFileInfo.AcqTimeEnd)
                {
                    datasetFileInfo.AcqTimeEnd = fiFileInfo.LastWriteTime;

                    if (datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes > 60)
                    {
                        // Update the start time to match the end time to prevent accidentally reporting an inaccurately long acquisition length
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                    }
                }

                // Look for the Storage.mcf_idx file and the corresponding .mcf_idx file
                // If they exist, we can extract information from them using SqLite
                var success = ParseMcfIndexFiles(diDatasetFolder, datasetFileInfo);

                if (!success)
                {
                    Dictionary<int, float> scanElutionTimeMap;

                    // Parse the scan.xml file (if it exists) to determine the number of spectra acquired
                    // We can also obtain TIC and elution time values from this file
                    // However, it does not track whether a scan is MS or MSn
                    // If the scans.xml file contains runtime entries (e.g. <minutes>100.0456</minutes>) then .AcqTimeEnd is updated using .AcqTimeStart + RunTimeMinutes
                    success = ParseScanXMLFile(diDatasetFolder, datasetFileInfo, out scanElutionTimeMap);

                    var bafFileParsed = false;

                    if (!success)
                    {
                        // Use ProteoWizard to extract the scan counts and acquisition time information
                        // If mSaveLCMS2DPlots = True, this method will also read the m/z and intensity values from each scan so that we can make 2D plots
                        bafFileParsed = ParseBAFFile(fiFileInfo, datasetFileInfo);
                    }

                    if (mSaveTICAndBPI & mTICandBPIPlot.CountBPI + mTICandBPIPlot.CountTIC == 0 || mSaveLCMS2DPlots & mLCMS2DPlot.ScanCountCached == 0)
                    {
                        // If a ser or fid file exists, we can read the data from it to create the TIC and BPI plots, plus also the 2D plot

                        var serOrFidParsed = ParseSerOrFidFile(fiFileInfo.Directory, scanElutionTimeMap);

                        if (!serOrFidParsed & !bafFileParsed)
                        {
                            // Look for an analysis.baf file
                            bafFileParsed = ParseBAFFile(fiFileInfo, datasetFileInfo);
                        }

                    }
                }

                // Parse the AutoMS.txt file (if it exists) to determine which scans are MS and which are MS/MS
                ParseAutoMSFile(diDatasetFolder);

                // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetFileInfo
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception processing BAF data: " + ex.Message, ex);
                return false;
            }

        }

        private bool ParseSerOrFidFile(DirectoryInfo diDotDFolder, IReadOnlyDictionary<int, float> scanElutionTimeMap)
        {


            try
            {
                var fiSerOrFidFile = new FileInfo(Path.Combine(diDotDFolder.FullName, "ser"));

                if (!fiSerOrFidFile.Exists)
                {
                    fiSerOrFidFile = new FileInfo(Path.Combine(diDotDFolder.FullName, "fid"));
                    if (!fiSerOrFidFile.Exists)
                        return false;
                }


                // Look for the apexAcquisition.method
                var fiSettingsFile = FindBrukerSettingsFile(diDotDFolder);

                if (fiSettingsFile == null)
                {
                    // Not found; look for an acqus file
                    var fiAcqusFile = FindBrukerAcqusFile(diDotDFolder);

                    if (fiAcqusFile == null)
                    {
                        // Not found; cannot parse the ser file
                        return false;
                    }

                    fiSettingsFile = fiAcqusFile;
                }

                var needToSaveTICAndBPI = (mSaveTICAndBPI && mTICandBPIPlot.CountBPI + mTICandBPIPlot.CountTIC == 0);
                var dtLastProgressTime = DateTime.UtcNow;

                var serReader = new BrukerDataReader.DataReader(fiSerOrFidFile.FullName, fiSettingsFile.FullName);

                var scanCount = serReader.GetNumMSScans();

                // BrukerDataReader.DataReader treats scan 0 as the first scan

                for (var scanNumber = 0; scanNumber <= scanCount - 1; scanNumber++)
                {
                    float[] mzValues;
                    float[] intensities;

                    try
                    {
                        serReader.GetMassSpectrum(scanNumber, out mzValues, out intensities);

                    }
                    catch (Exception ex)
                    {
                        if (scanNumber >= scanCount - 1)
                        {
                            if (scanNumber == 0)
                            {
                                // Silently ignore this
                                continue;
                            }
                            // Treat this as a warning
                            OnWarningEvent("Unable to retrieve scan " + scanNumber + " using the BrukerDataReader: " + ex.Message);
                        }
                        else
                        {
                            // Treat this as an error
                            OnErrorEvent("Error retrieving scan " + scanNumber + " using the BrukerDataReader: " + ex.Message);
                        }

                        // Ignore this scan
                        continue;
                    }

                    const int msLevel = 1;
                    float elutionTime;
                    if (!scanElutionTimeMap.TryGetValue(scanNumber, out elutionTime))
                    {
                        elutionTime = scanNumber / 60f;
                    }

                    if (needToSaveTICAndBPI)
                    {
                        double basePeakIntensity = 0;
                        double totalIonCurrent = 0;

                        if (intensities.Length > 0)
                        {
                            basePeakIntensity = intensities.Max();
                            totalIonCurrent = intensities.Sum();
                        }

                        mTICandBPIPlot.AddData(scanNumber, msLevel, elutionTime, basePeakIntensity, totalIonCurrent);
                    }

                    if (mzValues.Length > 0)
                    {
                        if (mSaveLCMS2DPlots)
                        {
                            var dblMassIntensityPairs = new double[2, mzValues.Length + 1];

                            for (var i = 0; i <= mzValues.Length - 1; i++)
                            {
                                dblMassIntensityPairs[0, i] = mzValues[i];
                                dblMassIntensityPairs[1, i] = intensities[i];
                            }

                            mLCMS2DPlot.AddScan2D(scanNumber, msLevel, elutionTime, mzValues.Length, dblMassIntensityPairs);
                        }

                    }

                    ShowProgress(scanNumber, scanCount, ref dtLastProgressTime, 2);
                }

                return true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception processing Bruker ser or fid file: " + ex.Message, ex);
                return false;
            }

        }

        private bool ReadAndStoreMcfIndexData(
            SQLiteConnection cnDB,
            IReadOnlyDictionary<string, int> lstMetadataNameToID,
            IDictionary<string, udtMCFScanInfoType> lstScanData,
            eMcfMetadataFields eMcfMetadataField)
        {

            var cmd = new SQLiteCommand(cnDB);

            string strTable;
            string strField;

            int intMetadataId;

            if (!GetMetaDataFieldAndTable(eMcfMetadataField, out strField, out strTable))
            {
                return false;
            }


            if (lstMetadataNameToID.TryGetValue(strField, out intMetadataId))
            {
                cmd.CommandText = "SELECT GuidA, MetaDataId, Value FROM " + strTable + " WHERE MetaDataId = " + intMetadataId;

                using (var drReader = cmd.ExecuteReader())
                {

                    while (drReader.Read())
                    {
                        var strGuid = ReadDbString(drReader, "GuidA");
                        var strValue = ReadDbString(drReader, "Value");

                        udtMCFScanInfoType udtScanInfo;
                        bool blnNewEntry;
                        if (lstScanData.TryGetValue(strGuid, out udtScanInfo))
                        {
                            blnNewEntry = false;
                        }
                        else
                        {
                            udtScanInfo = new udtMCFScanInfoType();
                            blnNewEntry = true;
                        }

                        UpdateScanInfo(eMcfMetadataField, strValue, ref udtScanInfo);

                        if (blnNewEntry)
                        {
                            lstScanData.Add(strGuid, udtScanInfo);
                        }
                        else
                        {
                            lstScanData[strGuid] = udtScanInfo;
                        }

                    }
                }

            }

            return true;

        }

        private string ReadDbString(SQLiteDataReader drReader, string strColumnName)
        {
            return ReadDbString(drReader, strColumnName, strValueIfNotFound: string.Empty);
        }

        private string ReadDbString(SQLiteDataReader drReader, string strColumnName, string strValueIfNotFound)
        {
            string strValue;

            try
            {
                strValue = drReader[strColumnName].ToString();
            }
            catch (Exception)
            {
                strValue = strValueIfNotFound;
            }

            return strValue;
        }

        private int ReadDbInt(SQLiteDataReader drReader, string strColumnName)
        {
            try
            {
                var strValue = drReader[strColumnName].ToString();
                if (!string.IsNullOrEmpty(strValue))
                {
                    int intValue;
                    if (int.TryParse(strValue, out intValue))
                    {
                        return intValue;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            return 0;

        }


        private void UpdateScanInfo(eMcfMetadataFields eMcfMetadataField, string strValue, ref udtMCFScanInfoType udtScanInfo)
        {
            int intValue;
            double dblValue;

            switch (eMcfMetadataField)
            {
                case eMcfMetadataFields.ScanMode:
                    if (int.TryParse(strValue, out intValue))
                    {
                        udtScanInfo.ScanMode = intValue;
                    }

                    break;
                case eMcfMetadataFields.MSLevel:
                    if (int.TryParse(strValue, out intValue))
                    {
                        udtScanInfo.MSLevel = intValue;
                    }

                    break;
                case eMcfMetadataFields.RT:
                    if (double.TryParse(strValue, out dblValue))
                    {
                        udtScanInfo.RT = dblValue;
                    }

                    break;
                case eMcfMetadataFields.BPI:
                    if (double.TryParse(strValue, out dblValue))
                    {
                        udtScanInfo.BPI = dblValue;
                    }

                    break;
                case eMcfMetadataFields.TIC:
                    if (double.TryParse(strValue, out dblValue))
                    {
                        udtScanInfo.TIC = dblValue;
                    }

                    break;
                case eMcfMetadataFields.AcqTime:
                    DateTime dtValue;
                    if (DateTime.TryParse(strValue, out dtValue))
                    {
                        udtScanInfo.AcqTime = dtValue;
                    }

                    break;
                case eMcfMetadataFields.SpotNumber:
                    udtScanInfo.SpotNumber = strValue;
                    break;
                default:
                    break;
                // Unknown field
            }

        }

        private class clsScanDataSortComparer : IComparer<udtMCFScanInfoType>
        {

            public int Compare(udtMCFScanInfoType x, udtMCFScanInfoType y)
            {

                if (x.RT < y.RT)
                {
                    return -1;
                }
                else if (x.RT > y.RT)
                {
                    return 1;
                }
                else
                {
                    if (x.AcqTime < y.AcqTime)
                    {
                        return -1;
                    }
                    else if (x.AcqTime > y.AcqTime)
                    {
                        return 1;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(x.SpotNumber) || string.IsNullOrEmpty(y.SpotNumber))
                        {
                            return 0;
                        }
                        else
                        {
                            return string.CompareOrdinal(x.SpotNumber, y.SpotNumber);
                        }

                    }
                }

            }
        }

    }
}

