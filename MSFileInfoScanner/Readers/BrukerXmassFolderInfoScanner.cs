﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Bruker XMass folder info scanner
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// </remarks>
    public class BrukerXmassFolderInfoScanner : ProteoWizardScanner
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: acq, AcqTime, acqus, baf, Bruker, fid, GuidA, idx, lcms, maxpeak, mcf, scanlist, ser, SQLite, tdf, tsf, Xmass

        // ReSharper restore CommentTypo

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string BRUKER_BAF_FILE_NAME = "analysis.baf";
        public const string BRUKER_TDF_FILE_NAME = "analysis.tdf";
        public const string BRUKER_TDF_BIN_FILE_NAME = "analysis.tdf_bin";
        public const string BRUKER_TSF_FILE_NAME = "analysis.tsf";
        public const string BRUKER_TSF_BIN_FILE_NAME = "analysis.tsf_bin";
        public const string BRUKER_SER_FILE_NAME = "ser";
        public const string BRUKER_FID_FILE_NAME = "fid";
        public const string BRUKER_EXTENSION_BAF_FILE_NAME = "extension.baf";
        public const string BRUKER_ANALYSIS_YEP_FILE_NAME = "analysis.yep";

        public const string BRUKER_SQLITE_INDEX_FILE_NAME = "Storage.mcf_idx";

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Bruker BAF file extension
        /// </summary>
        /// <remarks>The extension must be in all caps</remarks>
        public const string BRUKER_BAF_FILE_EXTENSION = ".BAF";

        /// <summary>
        /// Bruker MCF file extension
        /// </summary>
        /// <remarks>The extension must be in all caps</remarks>
        public const string BRUKER_MCF_FILE_EXTENSION = ".MCF";

        /// <summary>
        /// Bruker MCF index file extension
        /// </summary>
        /// <remarks>The extension must be in all caps</remarks>
        public const string BRUKER_SQLITE_INDEX_EXTENSION = ".MCF_IDX";

        private const string BRUKER_SCANINFO_XML_FILE = "scan.xml";

        // ReSharper disable once IdentifierTypo
        private const string BRUKER_AUTOMS_FILE = "AutoMS.txt";

        private struct MCFScanInfoType
        {
            // ReSharper disable once NotAccessedField.Local
            public double ScanMode;
            public int MSLevel;
            public double RT;
            public double BPI;
            public double TIC;
            public DateTime AcqTime;

            // Only used with MALDI imaging
            public string SpotNumber;
        }

        private enum McfMetadataFields
        {
            ScanMode = 0,
            MSLevel = 1,
            RT = 2,
            BPI = 3,
            TIC = 4,
            AcqTime = 5,
            SpotNumber = 6
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Options</param>
        /// <param name="lcms2DPlotOptions">Plotting options</param>
        public BrukerXmassFolderInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        { }

        private void AddDatasetScan(
            int scanNumber,
            int msLevel,
            float elutionTime,
            double bpi,
            double tic,
            string scanTypeName,
            ref double maxRunTimeMinutes,
            bool ticAndBpiPlotDataAlreadyDefined = false)
        {
            if (Options.SaveTICAndBPIPlots && scanNumber > 0 && !ticAndBpiPlotDataAlreadyDefined)
            {
                mTICAndBPIPlot.AddData(scanNumber, msLevel, elutionTime, bpi, tic);
            }

            var scanStatsEntry = new ScanStatsEntry
            {
                ScanNumber = scanNumber,
                ScanType = msLevel,
                ScanTypeName = scanTypeName,
                ScanFilterText = string.Empty,
                ElutionTime = elutionTime.ToString("0.0000###"),
                TotalIonIntensity = StringUtilities.ValueToString(tic, 5),
                BasePeakIntensity = StringUtilities.ValueToString(bpi, 5),
                BasePeakMZ = "0",
                BasePeakSignalToNoiseRatio = "0",
                IonCount = 0,
                IonCountRaw = 0
            };

            if (elutionTime > maxRunTimeMinutes)
            {
                maxRunTimeMinutes = elutionTime;
            }

            mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);
        }

        // ReSharper disable once GrammarMistakeInComment

        /// <summary>
        /// Looks for a .m directory then looks for apexAcquisition.method or submethods.xml in that directory
        /// Uses the file modification time as the run start time
        /// Also looks for the .hdx file in the dataset directory and examine its modification time
        /// </summary>
        /// <param name="datasetDirectory">Dataset directory</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <returns>True if a valid file is found; otherwise false</returns>
        private void DetermineAcqStartTime(DirectoryInfo datasetDirectory, DatasetFileInfo datasetFileInfo)
        {
            var success = false;

            try
            {
                // Look for the method directory (directory name should end in .m)

                var methodDirectories = PathUtils.FindDirectoriesWildcard(datasetDirectory, "*.m");

                var subDirectories = methodDirectories.Count > 0
                    ? methodDirectories
                    : PathUtils.FindDirectoriesWildcard(datasetDirectory, "XMass*");

                if (subDirectories.Count > 0)
                {
                    // Look for the apexAcquisition.method in each matching subdirectory
                    // We have historically used that file's modification time as the acquisition start time for the dataset
                    // However, we've found that on the 12T a series of datasets will all use the same method file and thus the modification time is not necessarily appropriate

                    // Note that the submethods.xml file sometimes gets modified after the run starts, so it should not be used to determine run start time

                    foreach (var subdirectory in subDirectories)
                    {
                        foreach (var methodFile in PathUtils.FindFilesWildcard(subdirectory, "apexAcquisition.method"))
                        {
                            datasetFileInfo.AcqTimeStart = methodFile.LastWriteTime;
                            success = true;
                            break;
                        }

                        if (success)
                            break;
                    }

                    if (!success)
                    {
                        // apexAcquisition.method not found; try submethods.xml instead
                        foreach (var subdirectory in subDirectories)
                        {
                            foreach (var methodFile in PathUtils.FindFilesWildcard(subdirectory, "submethods.xml"))
                            {
                                datasetFileInfo.AcqTimeStart = methodFile.LastWriteTime;
                                success = true;
                                break;
                            }

                            if (success)
                                break;
                        }
                    }
                }

                // Also look for the .hdx file
                // Its file modification time typically also matches the run start time

                foreach (var hdxFile in PathUtils.FindFilesWildcard(datasetDirectory, "*.hdx"))
                {
                    if (!success || hdxFile.LastWriteTime < datasetFileInfo.AcqTimeStart)
                    {
                        datasetFileInfo.AcqTimeStart = hdxFile.LastWriteTime;
                    }
                    break;
                }

                // Make sure AcqTimeEnd and AcqTimeStart match
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error finding XMass method directory: {0}", ex.Message), ex);
            }
        }

        private FileInfo FindBrukerSettingsFile(DirectoryInfo dotDFolder)
        {
            var dotMethodFiles = PathUtils.FindFilesWildcard(dotDFolder, "*.method", true);

            if (dotMethodFiles.Count == 0)
            {
                return null;
            }

            var acquisitionMethodFiles = (from methodFile in dotMethodFiles where methodFile.Name.EndsWith("apexacquisition.method", StringComparison.OrdinalIgnoreCase) select methodFile).ToList();

            if (acquisitionMethodFiles.Count == 0)
            {
                return null;
            }

            if (acquisitionMethodFiles.Count == 1)
            {
                return acquisitionMethodFiles.First();
            }

            OnErrorEvent("Multiple 'apexAcquisition.method' files were found in the .D directory; not sure which to use");
            return null;
        }

        private FileInfo FindBrukerAcqusFile(DirectoryInfo dotDFolder)
        {
            var acqusFiles = PathUtils.FindFilesWildcard(dotDFolder, "acqus", true);

            if (acqusFiles.Count == 0)
            {
                return null;
            }

            if (acqusFiles.Count == 1)
            {
                return acqusFiles.First();
            }

            // Often the Bruker file structures contain multiple Acqus files. I will select
            // the one that is in the same directory as the 'ser' file and if that isn't present,
            // the same directory as the 'fid' file. Otherwise, throw errors

            foreach (var acquFile in acqusFiles)
            {
                if (acquFile.Directory?.Name.Equals(dotDFolder.Name, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return acquFile;
                }
            }

            OnErrorEvent("Multiple 'acqus' files were found in the .D directory; not sure which one to use");
            return null;
        }

        private bool GetMetaDataFieldAndTable(McfMetadataFields mcfMetadataField, out string fieldName, out string tableName)
        {
            switch (mcfMetadataField)
            {
                case McfMetadataFields.ScanMode:
                    fieldName = "pScanMode";
                    tableName = "MetaDataInt";
                    break;

                case McfMetadataFields.MSLevel:
                    fieldName = "pMSLevel";
                    tableName = "MetaDataInt";
                    break;

                case McfMetadataFields.RT:
                    fieldName = "pRT";
                    tableName = "MetaDataDouble";
                    break;

                case McfMetadataFields.BPI:
                    fieldName = "pIntMax";
                    tableName = "MetaDataDouble";
                    break;

                case McfMetadataFields.TIC:
                    fieldName = "pTic";
                    tableName = "MetaDataDouble";
                    break;

                case McfMetadataFields.AcqTime:
                    fieldName = "pDateTime";
                    tableName = "MetaDataString";
                    break;

                case McfMetadataFields.SpotNumber:
                    fieldName = "pSpotNo";
                    tableName = "MetaDataString";
                    break;

                default:
                    // Unknown field
                    fieldName = string.Empty;
                    tableName = string.Empty;
                    return false;
            }

            return true;
        }

        private void ParseAutoMSFile(FileSystemInfo datasetDirectory)
        {
            try
            {
                var autoMSFilePath = Path.Combine(datasetDirectory.FullName, BRUKER_AUTOMS_FILE);
                var autoMSFile = MSFileInfoScanner.GetFileInfo(autoMSFilePath);

                if (!autoMSFile.Exists)
                {
                    return;
                }

                using var reader = new StreamReader(new FileStream(autoMSFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    var splitLine = dataLine.Split('\t');

                    if (splitLine.Length < 2)
                    {
                        continue;
                    }

                    if (!int.TryParse(splitLine[0], out var scanNumber))
                    {
                        continue;
                    }

                    // First column contains a number
                    // See if the second column is a known scan type

                    var msLevel = 0;

                    string scanTypeName;

                    switch (splitLine[1])
                    {
                        case "MS":
                            scanTypeName = "HMS";
                            msLevel = 1;
                            break;
                        case "MSMS":
                            scanTypeName = "HMSn";
                            msLevel = 2;
                            break;
                        default:
                            scanTypeName = string.Empty;
                            break;
                    }

                    mDatasetStatsSummarizer.UpdateDatasetScanType(scanNumber, msLevel, scanTypeName);
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error finding AutoMS.txt file: {0}", ex.Message), ex);
            }
        }

        /// <summary>
        /// Read data from the analysis.baf or analysis.tdf file using ProteoWizard
        /// </summary>
        /// <param name="datasetFileOrDirectory">Dataset file or directory</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <param name="bafFileChecked">Output: true if the file exists, and we tried to open it (will still be true if the file is corrupt)</param>
        /// <returns>True if success, false if an error</returns>
        private bool ParseBAFFile(FileSystemInfo datasetFileOrDirectory, DatasetFileInfo datasetFileInfo, out bool bafFileChecked)
        {
            // Override dataFilePath here, if needed
            const bool manualOverride = false;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (manualOverride)
                // ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162 // Unreachable code detected
            {
                // ReSharper disable once StringLiteralTypo
                const string newDataFilePath = @"c:\temp\analysis.baf";
                datasetFileOrDirectory = MSFileInfoScanner.GetFileInfo(newDataFilePath);
            }
#pragma warning restore CS0162 // Unreachable code detected
            // ReSharper restore HeuristicUnreachableCode

            mLCMS2DPlot.Options.UseObservedMinScan = false;
            bafFileChecked = datasetFileOrDirectory.Exists;

            try
            {
                // ReSharper disable once MergeIntoPattern
                if (datasetFileOrDirectory is FileInfo bafFileInfo && bafFileInfo.Length > 1024 * 1024 * 1024)
                {
                    OnWarningEvent("{0} file is over 1 GB; ProteoWizard typically cannot handle .baf files this large", bafFileInfo.Name);

                    // Look for a ser file
                    if (bafFileInfo.Directory != null && File.Exists(Path.Combine(bafFileInfo.Directory.FullName, "ser")))
                    {
                        OnStatusEvent("Will parse the ser file instead");
                        return false;
                    }

                    OnWarningEvent("ser file not found; trying ProteoWizard anyway");
                }

                // Open the analysis.baf (or analysis.tdf or extension.baf) file using the ProteoWizardWrapper
                OnDebugEvent("Determining acquisition info using ProteoWizard");

                // ReSharper disable CommentTypo

                // This call will create a SQLite file in the .D directory and will display this status message:
                // [INFO ] bdal.io.baf2sql: Generating new SQLite cache in analysis directory: C:\CTM_WorkDir\DatasetName.d\analysis.sqlite

                // ReSharper restore CommentTypo

                return ProcessWithProteoWizard(datasetFileOrDirectory, datasetFileInfo, true);
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error using ProteoWizard reader: {0}", ex.Message), ex);

                // Note that the following exception is thrown for a corrupt .D directory
                // Error using ProteoWizard reader: unknown compressor id: 6bb2e64a-27a0-4575-a66a-4e312c8b9ad7
                if (ex.Message.IndexOf("6bb2e64a-27a0-4575-a66a-4e312c8b9ad7", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    // Most likely a corrupt analysis.baf file or a corrupt analysis.tdf file
                    OnWarningEvent("Most likely a corrupt {0} file", datasetFileOrDirectory.Name);
                }

                return false;
            }
        }

        private bool ParseMcfIndexFiles(DirectoryInfo datasetDirectory, DatasetFileInfo datasetFileInfo)
        {
            try
            {
                var metadataNameToID = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var scanData = new Dictionary<string, MCFScanInfoType>();

                var metadataFilePath = Path.Combine(datasetDirectory.FullName, BRUKER_SQLITE_INDEX_FILE_NAME);
                var metadataFile = MSFileInfoScanner.GetFileInfo(metadataFilePath);

                if (!metadataFile.Exists)
                {
                    // Storage.mcf_idx not found
                    OnWarningEvent("Note: {0} file does not exist", BRUKER_SQLITE_INDEX_FILE_NAME);
                    return false;
                }

                var connectionString = "Data Source = " + metadataFile.FullName + "; Version=3; DateTimeFormat=Ticks;";

                // Open the Storage.mcf_idx file to look up the metadata name to ID mapping
                using (var connection = new SQLiteConnection(connectionString, true))
                {
                    connection.Open();

                    var cmd = new SQLiteCommand(connection)
                    {
                        CommandText = "SELECT metadataId, permanentName, displayName FROM MetadataId"
                    };

                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var metadataId = ReadDbInt(reader, "metadataId");
                        var metadataName = ReadDbString(reader, "permanentName");
                        // var metadataDescription = ReadDbString(reader, "displayName");

                        if (metadataId > 0)
                        {
                            metadataNameToID.Add(metadataName, metadataId);
                            // metadataNameToDescription.Add(metadataName, metadataDescription);
                        }
                    }
                }

                var mcfIndexFiles = PathUtils.FindFilesWildcard(datasetDirectory, "*_1.mcf_idx");

                if (mcfIndexFiles.Count == 0)
                {
                    // Storage.mcf_idx not found
                    OnWarningEvent("Note: {0} file was found but _1.mcf_idx file does not exist", BRUKER_SQLITE_INDEX_FILE_NAME);
                    return false;
                }

                connectionString = "Data Source = " + mcfIndexFiles.First().FullName + "; Version=3; DateTimeFormat=Ticks;";

                // Open the .mcf file to read the scan info
                using (var connection = new SQLiteConnection(connectionString, true))
                {
                    connection.Open();

                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.AcqTime);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.ScanMode);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.MSLevel);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.RT);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.BPI);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.TIC);
                    ReadAndStoreMcfIndexData(connection, metadataNameToID, scanData, McfMetadataFields.SpotNumber);
                }

                // Parse each entry in scanData
                // Copy the values to a generic list so that we can sort them
                var scanDataSorted = new MCFScanInfoType[scanData.Count];
                scanData.Values.CopyTo(scanDataSorted, 0);

                var scanDataComparer = new ScanDataSortComparer();
                Array.Sort(scanDataSorted, scanDataComparer);

                var acqTimeStart = DateTime.MaxValue;
                var acqTimeEnd = DateTime.MinValue;

                var scanCount = 0;
                double maxRunTimeMinutes = 0;

                for (var index = 0; index < scanDataSorted.Length; index++)
                {
                    scanCount++;
                    var scanNumber = scanCount;

                    if (scanDataSorted[index].AcqTime < acqTimeStart)
                    {
                        if (scanDataSorted[index].AcqTime > DateTime.MinValue)
                        {
                            acqTimeStart = scanDataSorted[index].AcqTime;
                        }
                    }

                    if (scanDataSorted[index].AcqTime > acqTimeEnd)
                    {
                        if (scanDataSorted[index].AcqTime < DateTime.MaxValue)
                        {
                            acqTimeEnd = scanDataSorted[index].AcqTime;
                        }
                    }

                    if (scanDataSorted[index].MSLevel == 0)
                        scanDataSorted[index].MSLevel = 1;

                    var elutionTime = (float)(scanDataSorted[index].RT / 60.0);

                    string scanTypeName;

                    if (string.IsNullOrEmpty(scanDataSorted[index].SpotNumber))
                    {
                        scanTypeName = "HMS";
                    }
                    else
                    {
                        scanTypeName = "MALDI-HMS";
                    }

                    AddDatasetScan(
                        scanNumber, scanDataSorted[index].MSLevel, elutionTime,
                        scanDataSorted[index].BPI, scanDataSorted[index].TIC,
                        scanTypeName, ref maxRunTimeMinutes);
                }

                if (scanCount > 0)
                {
                    datasetFileInfo.ScanCount = scanCount;

                    if (maxRunTimeMinutes > 0)
                    {
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-maxRunTimeMinutes);
                    }

                    if (acqTimeStart > DateTime.MinValue && acqTimeEnd < DateTime.MaxValue)
                    {
                        // Update the acquisition times if they are within 7 days of datasetFileInfo.AcqTimeEnd
                        if (Math.Abs(datasetFileInfo.AcqTimeEnd.Subtract(acqTimeEnd).TotalDays) <= 7)
                        {
                            datasetFileInfo.AcqTimeStart = acqTimeStart;
                            datasetFileInfo.AcqTimeEnd = acqTimeEnd;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Error parsing Storage.mcf_idx file
                OnErrorEvent(string.Format("Error parsing {0} file: {1}", BRUKER_SQLITE_INDEX_FILE_NAME, ex.Message), ex);
                return false;
            }

            return false;
        }

        private bool ParseScanXMLFile(
            FileSystemInfo datasetDirectory,
            DatasetFileInfo datasetFileInfo,
            out Dictionary<int, float> scanElutionTimeMap,
            out int duplicateScanCount)
        {
            scanElutionTimeMap = new Dictionary<int, float>();
            duplicateScanCount = 0;

            try
            {
                var scanXMLFilePath = Path.Combine(datasetDirectory.FullName, BRUKER_SCANINFO_XML_FILE);
                var scanXMLFile = MSFileInfoScanner.GetFileInfo(scanXMLFilePath);

                if (!scanXMLFile.Exists)
                {
                    return false;
                }

                var scanCount = 0;
                double maxRunTimeMinutes = 0;
                var validFile = false;

                using var reader = new XmlTextReader(new FileStream(scanXMLFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));

                var skipRead = false;
                var inScanNode = false;

                var scanNumber = 0;
                float elutionTime = 0;
                double tic = 0;
                double bpi = 0;
                var msLevel = 0;

                while (!reader.EOF)
                {
                    if (skipRead)
                    {
                        skipRead = false;
                    }
                    else
                    {
                        reader.Read();
                    }

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (inScanNode)
                            {
                                switch (reader.Name)
                                {
                                    case "count":
                                        scanNumber = reader.ReadElementContentAsInt();
                                        skipRead = true;
                                        break;

                                    case "minutes":
                                        elutionTime = reader.ReadElementContentAsFloat();
                                        skipRead = true;
                                        break;

                                    case "tic":
                                        tic = reader.ReadElementContentAsFloat();
                                        skipRead = true;
                                        break;

                                    // ReSharper disable once StringLiteralTypo
                                    case "maxpeak":
                                        bpi = reader.ReadElementContentAsFloat();
                                        skipRead = true;
                                        break;

                                    // Ignore others
                                    // default:
                                    //    break;
                                }
                            }
                            else
                            {
                                if (reader.Name == "scanlist")
                                {
                                    validFile = true;
                                }
                                else if (reader.Name == "scan")
                                {
                                    inScanNode = true;
                                    scanNumber = 0;
                                    elutionTime = 0;
                                    tic = 0;
                                    bpi = 0;
                                    msLevel = 1;

                                    scanCount++;
                                }
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.Name == "scan")
                            {
                                inScanNode = false;

                                if (scanElutionTimeMap.ContainsKey(scanNumber))
                                {
                                    OnWarningEvent("Skipping duplicate scan number: {0}", scanNumber);
                                    duplicateScanCount++;
                                }
                                else
                                {
                                    scanElutionTimeMap.Add(scanNumber, elutionTime);
                                    AddDatasetScan(scanNumber, msLevel, elutionTime, bpi, tic, "HMS", ref maxRunTimeMinutes);
                                }
                            }
                            break;
                    }
                }

                if (scanCount > 0)
                {
                    datasetFileInfo.ScanCount = scanCount;

                    if (maxRunTimeMinutes > 0)
                    {
                        if (Math.Abs(datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes) < maxRunTimeMinutes)
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(maxRunTimeMinutes);
                        }
                        else
                        {
                            datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-maxRunTimeMinutes);
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
                // Error parsing Scan.xml file
                OnErrorEvent(string.Format("Error parsing {0} file: {1}", BRUKER_SCANINFO_XML_FILE, ex.Message), ex);
                return false;
            }

            return false;
        }

        private DirectoryInfo GetDatasetFolder(string dataFilePath)
        {
            // First see if dataFilePath points to a valid file
            var datasetFile = MSFileInfoScanner.GetFileInfo(dataFilePath);

            if (datasetFile.Exists)
            {
                // User specified a file; assume the parent directory of this file is the dataset directory
                return datasetFile.Directory;
            }

            // Assume this is the path to the dataset directory
            return MSFileInfoScanner.GetDirectoryInfo(dataFilePath);
        }

        /// <summary>
        /// Extract the dataset name from the file path
        /// </summary>
        /// <param name="dataFilePath">Data file path</param>
        /// <returns>Dataset name</returns>
        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            var datasetName = string.Empty;

            try
            {
                // The dataset name for a Bruker Xmass directory is the name of the parent directory
                // However, dataFilePath could be a file or a directory path, so use GetDatasetFolder to get the dataset directory
                var datasetDirectory = GetDatasetFolder(dataFilePath);
                datasetName = datasetDirectory.Name;

                if (datasetName.EndsWith(".d", StringComparison.OrdinalIgnoreCase))
                {
                    datasetName = datasetName.Substring(0, datasetName.Length - 2);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }

            return datasetName;
        }

        /// <summary>
        /// Process a Bruker Xmass directory, specified by dataFilePath
        /// </summary>
        /// <param name="dataFilePath">Either the dataset directory containing the XMass files, or any of the XMass files in the dataset directory</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            try
            {
                // Determine whether dataFilePath points to a file or a directory

                var datasetDirectory = GetDatasetFolder(dataFilePath);

                // Validate that we have selected a valid directory
                if (!datasetDirectory.Exists)
                {
                    OnErrorEvent("File/directory not found: {0}", dataFilePath);
                    return false;
                }

                // In case we cannot find a .BAF file, update the .AcqTime values to the directory creation date
                // We have to assign a date, so we'll assign the date for the BAF file
                datasetFileInfo.AcqTimeStart = datasetDirectory.CreationTime;
                datasetFileInfo.AcqTimeEnd = datasetDirectory.CreationTime;

                mDatasetStatsSummarizer.ClearCachedData();

                // Look for the analysis.baf, analysis.tdf, or analysis.tsf file in datasetDirectory
                // Use its modification time as the AcqTime start and End values
                // If we cannot find those files, look for a ser file or a fid file

                var instrumentDataFiles = new List<string> {
                    BRUKER_BAF_FILE_NAME,
                    BRUKER_TDF_FILE_NAME,
                    BRUKER_TDF_BIN_FILE_NAME,
                    BRUKER_TSF_FILE_NAME,
                    BRUKER_TSF_BIN_FILE_NAME,
                    BRUKER_SER_FILE_NAME,
                    BRUKER_FID_FILE_NAME,
                    BRUKER_EXTENSION_BAF_FILE_NAME
                };

                // This tracks the first instrument file matched
                var matchedFiles = new List<FileInfo>();

                // This tracks all instrument files matched
                var instrumentFilesToAdd = new List<FileInfo>();

                foreach (var instrumentDataFile in instrumentDataFiles)
                {
                    var candidateFiles = PathUtils.FindFilesWildcard(datasetDirectory, instrumentDataFile);

                    if (candidateFiles.Count == 0)
                        continue;

                    if (matchedFiles.Count == 0)
                    {
                        matchedFiles.AddRange(candidateFiles);
                    }

                    instrumentFilesToAdd.AddRange(candidateFiles);
                }

                // Also look for .mcf files, since they should be included in the list of files used to compute dataset size
                var mcfFilesWithExtras = PathUtils.FindFilesWildcard(datasetDirectory, "*" + BRUKER_MCF_FILE_EXTENSION);

                // The "*.MCF" sent to FindFilesWildcard matches both .mcf and .mcf_idx files
                // Filter the list to only include the .mcf files
                var mcfFiles = (from item in mcfFilesWithExtras
                                where item.Extension.Equals(BRUKER_MCF_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase)
                                select item).ToList();

                instrumentFilesToAdd.AddRange(mcfFiles);

                if (matchedFiles.Count == 0 && mcfFiles.Count > 0)
                {
                    // .baf files not found, but .mcf files were found

                    // Find the largest .mcf file (not .mcf_idx file)
                    var largestMcf = (from item in mcfFiles
                                      orderby item.Length descending
                                      select item).First();

                    matchedFiles.Add(largestMcf);
                }

                if (matchedFiles.Count == 0)
                {
                    OnErrorEvent("{0} or {1} or {2} file not found in {3}",
                        string.Join(" or ", instrumentDataFiles),
                        BRUKER_MCF_FILE_EXTENSION,
                        BRUKER_SQLITE_INDEX_EXTENSION,
                        datasetDirectory.FullName);

                    return false;
                }

                var primaryInstrumentFile = matchedFiles.First();

                // Read the file info from the file system
                // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all the necessary steps are taken)
                // This will also compute the SHA-1 hash of the primary instrument file and add it to mDatasetStatsSummarizer.DatasetFileInfo
                UpdateDatasetFileStats(primaryInstrumentFile, datasetFileInfo.DatasetID, out var primaryFileAdded);

                // Update the dataset name and file extension
                datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetDirectory.FullName);
                datasetFileInfo.FileExtension = string.Empty;

                // To only use the "primary" file for dataset size:
                //datasetFileInfo.FileSizeBytes = primaryInstrumentFile.Length;
                // To only use the "added" instrument files for dataset size:
                //datasetFileInfo.FileSizeBytes = instrumentFilesToAdd.Sum(x => x.Length);
                // To use all dataset files for dataset size:
                datasetFileInfo.FileSizeBytes = PathUtils.FindFilesWildcard(datasetDirectory, "*", true).Sum(x => x.Length);

                // Find the apexAcquisition.method or submethods.xml file in the XMASS_Method.m subdirectory to determine .AcqTimeStart
                // This method updates datasetFileInfo.AcqTimeEnd and datasetFileInfo.AcqTimeStart to have the same time
                DetermineAcqStartTime(datasetDirectory, datasetFileInfo);

                // Update the acquisition end time using the write time of the .baf file
                if (primaryInstrumentFile.LastWriteTime > datasetFileInfo.AcqTimeEnd)
                {
                    datasetFileInfo.AcqTimeEnd = primaryInstrumentFile.LastWriteTime;

                    if (datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes > 60)
                    {
                        // Update the start time to match the end time to prevent accidentally reporting an inaccurately long acquisition length
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                    }
                }

                // Look for the Storage.mcf_idx file and the corresponding .mcf_idx file
                // If they exist, we can extract information from them using SqLite
                var success = ParseMcfIndexFiles(datasetDirectory, datasetFileInfo);

                if (!success)
                {
                    // Parse the scan.xml file (if it exists) to determine the number of spectra acquired
                    // We can also obtain TIC and elution time values from this file
                    // However, it does not track whether a scan is MS or MSn
                    // If the scans.xml file contains runtime entries (e.g. <minutes>100.0456</minutes>) then .AcqTimeEnd is updated using .AcqTimeStart + RunTimeMinutes
                    success = ParseScanXMLFile(datasetDirectory, datasetFileInfo, out var scanElutionTimeMap, out var duplicateScanCount);

                    var bafFileChecked = false;

                    if (!success)
                    {
                        // Use ProteoWizard to extract the scan counts and acquisition time information
                        // If Options.SaveLCMS2DPlots= True, this method will also read the m/z and intensity values from each scan so that we can make 2D plots
                        ParseBAFFile(primaryInstrumentFile, datasetFileInfo, out bafFileChecked);
                    }

                    if (datasetFileInfo.ScanCount == 0 ||
                        Options.SaveTICAndBPIPlots && mTICAndBPIPlot.CountBPI + mTICAndBPIPlot.CountTIC == 0 ||
                        Options.SaveLCMS2DPlots && mLCMS2DPlot.ScanCountCached == 0)
                    {
                        // If a ser or fid file exists, we can read the data from it to create the TIC and BPI plots, plus also the 2D plot

                        var serOrFidParsed = ParseSerOrFidFile(primaryInstrumentFile.Directory, scanElutionTimeMap, datasetFileInfo, duplicateScanCount);

                        if (!serOrFidParsed && !bafFileChecked)
                        {
                            // Look for an analysis.baf or analysis.tdf file
                            var successWithPrimaryFile = ParseBAFFile(primaryInstrumentFile, datasetFileInfo, out _);

                            if (!successWithPrimaryFile)
                            {
                                ParseBAFFile(primaryInstrumentFile.Directory, datasetFileInfo, out _);
                            }
                        }
                    }
                }

                // Parse the AutoMS.txt file (if it exists) to determine which scans are MS and which are MS/MS
                ParseAutoMSFile(datasetDirectory);

                mInstrumentSpecificPlots.Clear();
                mDatasetStatsSummarizer.ExtractedIonStats.Clear();

                // Read data from chromatography-data.sqlite that is not TIC or BPC for plotting
                ParseChromatographyTraces(datasetDirectory);

                if (instrumentFilesToAdd.Count == 0 && !primaryFileAdded)
                {
                    // Add the largest file in instrument directory
                    AddLargestInstrumentFile(datasetDirectory);
                }
                else
                {
                    // Add the files in instrumentFilesToAdd
                    foreach (var fileToAdd in instrumentFilesToAdd)
                    {
                        if (fileToAdd.FullName.Equals(primaryInstrumentFile.FullName))
                            continue;

                        if (Options.DisableInstrumentHash)
                        {
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(fileToAdd);
                        }
                        else
                        {
                            OnStatusEvent("Computing SHA-1 hash for file {0}", fileToAdd.Name);
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(fileToAdd);
                        }
                    }
                }

                // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;

                PostProcessTasks();
                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Exception processing BAF data: {0}", ex.Message), ex);
                PostProcessTasks();
                return false;
            }
        }

        private bool ParseSerOrFidFile(
            DirectoryInfo dotDFolder,
            IReadOnlyDictionary<int, float> scanElutionTimeMap,
            DatasetFileInfo datasetFileInfo,
            int duplicateScanCount)
        {
            try
            {
                var serOrFidFile = MSFileInfoScanner.GetFileInfo(Path.Combine(dotDFolder.FullName, "ser"));

                if (!serOrFidFile.Exists)
                {
                    serOrFidFile = MSFileInfoScanner.GetFileInfo(Path.Combine(dotDFolder.FullName, "fid"));

                    if (!serOrFidFile.Exists)
                        return false;
                }

                // Look for the apexAcquisition.method
                var settingsFile = FindBrukerSettingsFile(dotDFolder);

                if (settingsFile == null)
                {
                    // Not found; look for an acqus file
                    var acqusFile = FindBrukerAcqusFile(dotDFolder);

                    if (acqusFile == null)
                    {
                        // Not found; cannot parse the ser file
                        return false;
                    }

                    settingsFile = acqusFile;
                }

                var ticAndBpiPlotDataAlreadyDefined = mTICAndBPIPlot.CountBPI + mTICAndBPIPlot.CountTIC > 0;

                var lastProgressTime = DateTime.UtcNow;

                // Note that this starts at 2 seconds, but is extended after each progress message is shown (maxing out at 30 seconds)
                var progressThresholdSeconds = 2;

                var serReader = new BrukerDataReader.DataReader(serOrFidFile.FullName, settingsFile.FullName);

                var scanCount = serReader.GetNumMSScans();

                if (datasetFileInfo.ScanCount == 0)
                {
                    datasetFileInfo.ScanCount = scanCount;
                }
                else if (datasetFileInfo.ScanCount != scanCount)
                {
                    var mismatchMessage = string.Format(
                        "Scan count from the {0} file differs from the scan count determined via ProteoWizard: {1} vs. {2}",
                        serOrFidFile.Name, scanCount, datasetFileInfo.ScanCount);

                    // Scan count mismatch between the scan.xml file and ProteoWizard
                    if (datasetFileInfo.ScanCount == scanCount + duplicateScanCount)
                    {
                        // Scan count mismatch can be attributed to duplicate scans; report this as a debug event

                        // ReSharper disable once CommentTypo
                        // See, for example, scan.xml for dataset 20190319_WK_SRFA_0pt1m_000002

                        var scanText = duplicateScanCount == 1 ? "scan" : "scans";
                        OnDebugEvent("{0}; the mismatch can be attributed to the {1} duplicate {2}", mismatchMessage, duplicateScanCount, scanText);
                    }
                    else
                    {
                        OnWarningEvent(mismatchMessage);
                    }
                }

                // BrukerDataReader.DataReader treats scan 0 as the first scan

                var scansProcessed = 0;
                double maxRunTimeMinutes = 0;

                for (var scanIndex = 0; scanIndex < scanCount; scanIndex++)
                {
                    float[] mzList;
                    float[] intensityList;

                    var scanNumber = scanIndex + 1;

                    try
                    {
                        serReader.GetMassSpectrum(scanIndex, out mzList, out intensityList);
                    }
                    catch (Exception ex)
                    {
                        if (scanIndex >= scanCount - 1)
                        {
                            if (scanIndex == 0)
                            {
                                // Silently ignore this
                                continue;
                            }

                            // Treat this as a warning
                            OnWarningEvent("Unable to retrieve scan {0} using the BrukerDataReader: {1}", scanNumber, ex.Message);
                        }
                        else
                        {
                            // Treat this as an error
                            OnErrorEvent("Error retrieving scan {0} using the BrukerDataReader: {1}", scanNumber, ex.Message);
                        }

                        // Ignore this scan
                        continue;
                    }

                    const int msLevel = 1;

                    if (!scanElutionTimeMap.TryGetValue(scanNumber, out var elutionTime))
                    {
                        // We're assigning an arbitrary elution time here, assuming one scan per second
                        elutionTime = scanNumber / 60f;
                    }

                    double basePeakIntensity = 0;
                    double totalIonCurrent = 0;

                    if (intensityList.Length > 0)
                    {
                        basePeakIntensity = intensityList.Max();
                        totalIonCurrent = intensityList.Sum();
                    }

                    AddDatasetScan(
                        scanNumber, msLevel, elutionTime,
                        basePeakIntensity, totalIonCurrent,
                        "HMS", ref maxRunTimeMinutes,
                        ticAndBpiPlotDataAlreadyDefined);

                    if (mzList.Length > 0 && Options.SaveLCMS2DPlots)
                    {
                        mLCMS2DPlot.AddScan2D(scanNumber, msLevel, elutionTime, mzList, intensityList);
                    }

                    scansProcessed++;

                    if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < progressThresholdSeconds)
                        continue;

                    lastProgressTime = DateTime.UtcNow;

                    if (progressThresholdSeconds < 30)
                        progressThresholdSeconds += 2;

                    var percentComplete = scansProcessed / (float)scanCount * 100;
                    OnProgressUpdate(string.Format("Spectra processed: {0:N0}", scansProcessed), percentComplete);
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Exception processing Bruker ser or fid file: {0}", ex.Message), ex);
                return false;
            }
        }

        /// <summary>
        /// Parse chromatography traces
        /// </summary>
        /// <param name="datasetDirectory">Dataset directory</param>
        /// <returns>True if successful, false if an error</returns>
        public bool ParseChromatographyTraces(DirectoryInfo datasetDirectory)
        {
            var eicMzMatcher = new Regex(@"([0-9]+\.[0-9]+)", RegexOptions.Compiled);

            if (!ParseChromatographyTraceDefinitions(datasetDirectory, out var traceDefinitions))
            {
                return false;
            }

            var sqlitePath = Path.Combine(datasetDirectory.FullName, "chromatography-data.sqlite");
            if (!File.Exists(sqlitePath))
            {
                return false;
            }

            using var connection = new SQLiteConnection($"Data Source={sqlitePath}; Version=3; Read Only=True");
            connection.Open();
            var sources = ParseChromatographyTraceSources(connection);

            var traces = ParseChromatographyTraceChunks(connection);

            connection.Close();

            var unitsMatcher = new Regex(@"\[(?<Units>[^]]+)\]", RegexOptions.Compiled);

            foreach (var source in sources)
            {
                if (source.Description.StartsWith("TIC", StringComparison.OrdinalIgnoreCase) ||
                    source.Description.StartsWith("BPC", StringComparison.OrdinalIgnoreCase) ||
                    source.Description.StartsWith("Capillary", StringComparison.OrdinalIgnoreCase))
                {
                    // We either already have these plots, or don't care about them.
                    continue;
                }

                var hasTrace = traces.TryGetValue(source.Id, out var trace);
                var definition = traceDefinitions.Find(x => x.Title?.Equals(source.Description, StringComparison.OrdinalIgnoreCase) ?? false);

                if (hasTrace)
                {
                    if (source.Description.StartsWith("EIC", StringComparison.OrdinalIgnoreCase))
                    {
                        // EIC traces are recorded (as maximum/median intensity), but not plotted
                        var mzMatch = eicMzMatcher.Match(source.Description);
                        if (!mzMatch.Success || trace.Count == 0)
                        {
                            // Matching the m/z to extract it didn't work, or there's no data for statistics.
                            continue;
                        }

                        var mz = double.Parse(mzMatch.Value);
                        var maxIntensity = trace.Max(x => x.Value);
                        var medianIntensity = MathNet.Numerics.Statistics.Statistics.Median(trace.Select(x => x.Value));
                        var stats = new ExtractedIonStats(mz, maxIntensity, medianIntensity);

                        mDatasetStatsSummarizer.ExtractedIonStats.Add(stats);

                        // Not plotting this, so skip to the next trace
                        continue;
                    }

                    if (Options.SaveTICAndBPIPlots)
                    {
                        var addedPlot = AddInstrumentSpecificPlot(source.Description);
                        addedPlot.TICXAxisLabel = "Time (minutes)";
                        addedPlot.TICYAxisLabel = definition.Unit;
                        addedPlot.TICXAxisIsTimeMinutes = true;

                        var max = trace.Max(x => x.Value);
                        addedPlot.TICYAxisExponentialNotation = max > 10000;
                        addedPlot.TICAutoMinMaxY = true;

                        if (string.IsNullOrWhiteSpace(addedPlot.TICYAxisLabel) ||
                            addedPlot.TICYAxisLabel.StartsWith("Unnamed", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = unitsMatcher.Match(source.Description);

                            addedPlot.TICYAxisLabel = match.Success ? match.Groups["Units"].Value : "Value";
                        }

                        // Replace a colon with a comma
                        // Replace " - []" with an empty string
                        // Replace square brackets with parentheses
                        addedPlot.TICPlotAbbrev = source.Description.Replace(":", ", ").Replace(" - []", "").Replace('[', '(').Replace(']', ')').Replace("  ", " ");

                        for (var i = 0; i < trace.Count; i++)
                        {
                            addedPlot.AddDataTICOnly(i + 1, 1, (float)(trace[i].Time / 60), trace[i].Value);
                        }
                    }
                }
            }

            return true;
        }

        private bool ParseChromatographyTraceDefinitions(DirectoryInfo datasetDirectory, out List<ChromatographyTraceDefinition> traceDefinitions)
        {
            traceDefinitions = new List<ChromatographyTraceDefinition>(20);

            var methodsDir = datasetDirectory.GetDirectories("*.m");
            if (methodsDir.Length == 0)
            {
                return false;
            }

            var apexAcqFilePath = Path.Combine(methodsDir[0].FullName, "apexAcquisition.method");

            // ReSharper disable IdentifierTypo

            var microTOFQImpacTemAcqFilePath = Path.Combine(methodsDir[0].FullName, "microTOFQImpacTemAcquisition.method");

            // ReSharper restore IdentifierTypo

            if (File.Exists(apexAcqFilePath))
            {
                return ParseChromatographyTraceDefinitionsApexAcq(apexAcqFilePath, out traceDefinitions);
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (File.Exists(microTOFQImpacTemAcqFilePath))
            {
                return ParseChromatographyTraceDefinitionsMicroTOFQImpacTemAcq(microTOFQImpacTemAcqFilePath, out traceDefinitions);
            }

            return false;
        }

        private bool ParseChromatographyTraceDefinitionsApexAcq(string filePath, out List<ChromatographyTraceDefinition> traceDefinitions)
        {
            traceDefinitions = new List<ChromatographyTraceDefinition>(20);

            try
            {
                using var reader = new XmlTextReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EOF)
                {
                    reader.Read();

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:

                            // ReSharper disable once StringLiteralTypo
                            if (reader.Name.Equals("tracedata", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    var title = reader.GetAttribute("title");
                                    var unit = reader.GetAttribute("unit");
                                    var color = reader.GetAttribute("color_rgb");

                                    traceDefinitions.Add(new ChromatographyTraceDefinition(title, unit, color));
                                }
                                catch
                                {
                                    // Ignore errors here
                                }
                            }
                            break;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent(string.Format("Exception reading {0}: {1}", "apexAcquisition.method", ex.Message), ex);
                return false;
            }
        }

        /// <summary>
        /// Extract the chromatography trace definitions for a Bruker microTOF Q ImpacT instrument
        /// </summary>
        /// <param name="filePath">microTOFQImpacTemAcquisition.method file</param>
        /// <param name="traceDefinitions">Output: list of chromatography trace definitions</param>
        /// <returns>True if successful, false if no chromatograms or an error</returns>
        private bool ParseChromatographyTraceDefinitionsMicroTOFQImpacTemAcq(string filePath, out List<ChromatographyTraceDefinition> traceDefinitions)
        {
            traceDefinitions = new List<ChromatographyTraceDefinition>(20);

            // ReSharper disable StringLiteralTypo
            const string traceShowXPath = "//timetable/segment/para_vec_int[@permname='Internal_ChromatogramTraceShow']/entry_int";
            const string traceColorXPath = "//timetable/segment/para_vec_int[@permname='Internal_ChromatogramTraceColor']/entry_int";
            const string traceDefinitionXPath = "//timetable/segment/para_vec_string[@permname='Internal_ChromatogramTraceDefinition']/entry_string";
            // ReSharper restore StringLiteralTypo

            try
            {
                var document = new XmlDocument();
                using var reader = new XmlTextReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                document.Load(reader);
                var nsManager = new XmlNamespaceManager(document.NameTable);
                var traceShow = document.SelectNodes(traceShowXPath, nsManager);
                var traceColor = document.SelectNodes(traceColorXPath, nsManager);
                var traceDefinition = document.SelectNodes(traceDefinitionXPath, nsManager);

                if (traceShow == null || traceColor == null || traceDefinition == null)
                {
                    // no data matched?
                    return false;
                }

                if (traceDefinition.Count != traceColor.Count || traceDefinition.Count != traceColor.Count)
                {
                    // mismatched data counts
                    return false;
                }

                if (traceDefinition.Count == 0)
                {
                    // no data
                    return false;
                }

                for (var i = 0; i < traceDefinition.Count; i++)
                {
                    // Not actually using values from traceShow for now, since if it is '1', it is in chromatogram-data.sqlite, but if it is '0', it is not included, and we automatically ignore it.
                    // var show = traceShow[i].Attributes["value"].Value;

                    var xmlAttributeCollection = traceColor[i].Attributes;

                    if (xmlAttributeCollection == null)
                        continue;

                    // integer value, when converted to hex it is the color in BGR format (because of byte ordering, maybe?)
                    var color = xmlAttributeCollection["value"].Value;

                    var attributeCollection = traceDefinition[i].Attributes;

                    if (attributeCollection == null)
                        continue;

                    var definition = attributeCollection["value"].Value;

                    var colorInt = int.Parse(color);

                    // Value looks like 'nBGR' (byte-by-byte).
                    // Convert to an array of bytes, reverse, convert back to integer (now RGBn), then shift right 8 bits to remove the 'n' value (now [0x00|0xFF]RGB) and mask with 0x00FFFFFF to drop a possible leading 'FF' since we don't have 'A' values
                    var rgbColor = BitConverter.ToInt32(BitConverter.GetBytes(colorInt).Reverse().ToArray(), 0) >> 8 & 0x00FFFFFF;

                    traceDefinitions.Add(new ChromatographyTraceDefinition(definition, "Intensity", $"#{rgbColor:x6}"));
                }

                return true;
            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent(string.Format("Exception reading {0}: {1}", "microTOFQImpacTemAcquisition.method", ex.Message), ex);
                return false;
            }
        }

        private List<ChromatographyTraceSource> ParseChromatographyTraceSources(SQLiteConnection sqliteDb)
        {
            var traceSources = new List<ChromatographyTraceSource>(20);
            try
            {
                var command = new SQLiteCommand("SELECT Id, Description, TimeOffset FROM TraceSources", sqliteDb);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var description = reader.GetString(1);
                    var timeOffset = reader.GetDouble(2);

                    traceSources.Add(new ChromatographyTraceSource(id, description, timeOffset));
                }
            }
            catch
            {
                return traceSources;
            }

            return traceSources;
        }

        private Dictionary<int, List<ChromatographyTraceValue>> ParseChromatographyTraceChunks(SQLiteConnection sqliteDb)
        {
            const int floatSize = sizeof(float);
            const int doubleSize = sizeof(double);

            var traceLists = new Dictionary<int, List<ChromatographyTraceValue>>(20);
            try
            {
                var command = new SQLiteCommand("SELECT Trace, Times, Intensities FROM TraceChunks", sqliteDb);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var times = reader.GetValue(1) as byte[];
                    var intensities = reader.GetValue(2) as byte[];

                    if (!traceLists.TryGetValue(id, out var traceValues))
                    {
                        traceValues = new List<ChromatographyTraceValue>(15);
                        traceLists.Add(id, traceValues);
                    }

                    if (times == null || intensities == null)
                    {
                        //OnErrorEvent("Error casting DB data to byte arrays: chromatography-data.sqlite:TraceChunks");
                        continue;
                    }

                    var valueCount = times.Length / doubleSize;

                    if (times.Length / doubleSize != intensities.Length / floatSize)
                    {
                        //OnErrorEvent("Array size mismatch: chromatography-data.sqlite");
                        continue;
                    }

                    for (var i = 0; i < valueCount; i++)
                    {
                        var time = BitConverter.ToDouble(times, doubleSize * i);
                        var intensity = BitConverter.ToSingle(intensities, floatSize * i);

                        traceValues.Add(new ChromatographyTraceValue(time, intensity));
                    }
                }
            }
            catch
            {
                return traceLists;
            }

            return traceLists;
        }

        private void ReadAndStoreMcfIndexData(
            SQLiteConnection connection,
            IReadOnlyDictionary<string, int> metadataNameToID,
            IDictionary<string, MCFScanInfoType> scanData,
            McfMetadataFields mcfMetadataField)
        {
            var cmd = new SQLiteCommand(connection);

            if (!GetMetaDataFieldAndTable(mcfMetadataField, out var fieldName, out var tableName))
            {
                return;
            }

            if (!metadataNameToID.TryGetValue(fieldName, out var metadataId))
                return;

            cmd.CommandText = "SELECT GuidA, MetaDataId, Value FROM " + tableName + " WHERE MetaDataId = " + metadataId;

            using var drReader = cmd.ExecuteReader();

            while (drReader.Read())
            {
                var scanGuid = ReadDbString(drReader, "GuidA");
                var metaDataValue = ReadDbString(drReader, "Value");

                bool newEntry;

                if (scanData.TryGetValue(scanGuid, out var scanInfo))
                {
                    newEntry = false;
                }
                else
                {
                    scanInfo = new MCFScanInfoType();
                    newEntry = true;
                }

                UpdateScanInfo(mcfMetadataField, metaDataValue, ref scanInfo);

                if (newEntry)
                {
                    scanData.Add(scanGuid, scanInfo);
                }
                else
                {
                    scanData[scanGuid] = scanInfo;
                }
            }
        }

        private string ReadDbString(IDataRecord drReader, string columnName)
        {
            return ReadDbString(drReader, columnName, valueIfNotFound: string.Empty);
        }

        private string ReadDbString(IDataRecord drReader, string columnName, string valueIfNotFound)
        {
            string dataValue;

            try
            {
                dataValue = drReader[columnName].ToString();
            }
            catch (Exception)
            {
                dataValue = valueIfNotFound;
            }

            return dataValue;
        }

        private int ReadDbInt(IDataRecord drReader, string columnName)
        {
            try
            {
                var dataValue = drReader[columnName].ToString();

                if (!string.IsNullOrEmpty(dataValue))
                {
                    if (int.TryParse(dataValue, out var numericValue))
                    {
                        return numericValue;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }

            return 0;
        }

        private void UpdateScanInfo(McfMetadataFields mcfMetadataField, string dataValue, ref MCFScanInfoType scanInfo)
        {
            switch (mcfMetadataField)
            {
                case McfMetadataFields.ScanMode:
                    if (int.TryParse(dataValue, out var scanMode))
                    {
                        scanInfo.ScanMode = scanMode;
                    }
                    break;

                case McfMetadataFields.MSLevel:
                    if (int.TryParse(dataValue, out var msLevel))
                    {
                        scanInfo.MSLevel = msLevel;
                    }
                    break;

                case McfMetadataFields.RT:
                    if (double.TryParse(dataValue, out var retentionTime))
                    {
                        scanInfo.RT = retentionTime;
                    }
                    break;

                case McfMetadataFields.BPI:
                    if (double.TryParse(dataValue, out var bpi))
                    {
                        scanInfo.BPI = bpi;
                    }
                    break;

                case McfMetadataFields.TIC:
                    if (double.TryParse(dataValue, out var tic))
                    {
                        scanInfo.TIC = tic;
                    }
                    break;

                case McfMetadataFields.AcqTime:
                    if (DateTime.TryParse(dataValue, out var acqTime))
                    {
                        scanInfo.AcqTime = acqTime;
                    }
                    break;

                case McfMetadataFields.SpotNumber:
                    scanInfo.SpotNumber = dataValue;
                    break;
            }
        }

        private class ScanDataSortComparer : IComparer<MCFScanInfoType>
        {
            public int Compare(MCFScanInfoType x, MCFScanInfoType y)
            {
                if (x.RT < y.RT)
                {
                    return -1;
                }

                if (x.RT > y.RT)
                {
                    return 1;
                }

                if (x.AcqTime < y.AcqTime)
                {
                    return -1;
                }

                if (x.AcqTime > y.AcqTime)
                {
                    return 1;
                }

                if (string.IsNullOrEmpty(x.SpotNumber) || string.IsNullOrEmpty(y.SpotNumber))
                {
                    return 0;
                }

                return string.CompareOrdinal(x.SpotNumber, y.SpotNumber);
            }
        }

        private readonly struct ChromatographyTraceDefinition
        {
            public string Title { get; }
            public string Unit { get; }
            private string Color { get; }

            // ReSharper disable once ConvertToPrimaryConstructor
            public ChromatographyTraceDefinition(string title, string unit, string color)
            {
                Title = title;
                Unit = unit;
                Color = color;
            }

            public override string ToString()
            {
                return $"Trace: '{Title}' ({Unit}, {Color})";
            }
        }

        private readonly struct ChromatographyTraceSource
        {
            public int Id { get; }
            public string Description { get; }
            private double TimeOffset { get; }

            // ReSharper disable once ConvertToPrimaryConstructor
            public ChromatographyTraceSource(int id, string description, double timeOffset)
            {
                Id = id;
                Description = description;
                TimeOffset = timeOffset;
            }

            public override string ToString()
            {
                return $"TraceSource {Id}: {Description} with offset {TimeOffset}";
            }
        }

        private readonly struct ChromatographyTraceValue
        {
            public double Time { get; }
            public float Value { get; }

            // ReSharper disable once ConvertToPrimaryConstructor
            public ChromatographyTraceValue(double time, float value)
            {
                Time = time;
                Value = value;
            }

            public override string ToString()
            {
                return $"{Time:F3}: {Value:F3}";
            }
        }
    }
}
