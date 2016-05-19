using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PNNLOmics.Utilities;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
//
// Last modified January 17, 2013

namespace MSFileInfoScanner
{
    public class clsAgilentGCDFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string AGILENT_DATA_FOLDER_D_EXTENSION = ".D";
        public const string AGILENT_MS_DATA_FILE = "DATA.MS";
        public const string AGILENT_ACQ_METHOD_FILE = "acqmeth.txt";

        public const string AGILENT_GC_INI_FILE = "GC.ini";
        private const string ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE = "Equilibration Time";
        private const string ACQ_METHOD_FILE_RUN_TIME_LINE = "Run Time";

        private const string ACQ_METHOD_FILE_POST_RUN_LINE = "(Post Run)";

        private readonly Regex mExtractTime;
	
        private class clsLineMatchSearchInfo
        {
            public readonly bool MatchLineStart;

            public bool Matched;
            public clsLineMatchSearchInfo(bool bMatchLineStart)
            {
                MatchLineStart = bMatchLineStart;
                Matched = false;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public clsAgilentGCDFolderInfoScanner()
        {
            mExtractTime = new Regex("([0-9.]+) min",
                                     RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            // The dataset name is simply the folder name without .D
            try {
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            } catch (Exception ex) {
                return string.Empty;
            }
        }
	
        private bool ExtractRunTime(string strText, out double dblRunTimeMinutes)
        {

            var reMatch = mExtractTime.Match(strText);

            if (reMatch.Success) {
                if (double.TryParse(reMatch.Groups[1].Value, out dblRunTimeMinutes)) {
                    return true;
                }
            }

            dblRunTimeMinutes = 0;
            return false;

        }

        private bool ParseAcqMethodFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            double dblTotalRuntime = 0;

            var blnRunTimeFound = false;
            bool blnSuccess;

            try {
                // Open the acqmeth.txt file
                var strFilePath = Path.Combine(strFolderPath, AGILENT_ACQ_METHOD_FILE);
                if (!File.Exists(strFilePath)) {
                    return false;
                }

                // Populate a dictionary var with the text strings for finding lines with runtime information
                // Note that "Post Run" occurs twice in the file, so we use clsLineMatchSearchInfo.Matched to track whether or not the text has been matched
                var dctRunTimeText = new Dictionary<string, clsLineMatchSearchInfo>
                {
                    {ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE, new clsLineMatchSearchInfo(true)},
                    {ACQ_METHOD_FILE_RUN_TIME_LINE, new clsLineMatchSearchInfo(true)}
                };

                // We could also add in the "Post Run" time for determining total acquisition time, but we don't do this, to stay consistent with run times reported by the MS file
                // dctRunTimeText.Add(ACQ_METHOD_FILE_POST_RUN_LINE, New clsLineMatchSearchInfo(False))

                using (var srInFile = new StreamReader(strFilePath)) {

                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if (string.IsNullOrWhiteSpace(strLineIn))
                        {
                            continue;
                        }
                        
                        foreach (var strKey in dctRunTimeText.Keys) {
                            if (dctRunTimeText[strKey].Matched)
                            {
                                continue;
                            }

                            bool blnMatchSuccess;
                            if (dctRunTimeText[strKey].MatchLineStart) {
                                blnMatchSuccess = strLineIn.StartsWith(strKey);
                            } else {
                                blnMatchSuccess = strLineIn.Contains(strKey);
                            }

                            if (!blnMatchSuccess)
                            {
                                continue;
                            }

                            double dblRunTime;
                            if (!ExtractRunTime(strLineIn, out dblRunTime))
                            {
                                continue;
                            }

                            dctRunTimeText[strKey].Matched = true;
                            dblTotalRuntime += dblRunTime;
                            blnRunTimeFound = true;
                            break; // TODO: might not be correct. Was : Exit For
                        }
                    }
                }

                blnSuccess = blnRunTimeFound;

            } catch (Exception ex) {
                // Exception reading file
                ReportError("Exception reading " + AGILENT_ACQ_METHOD_FILE + ": " + ex.Message);
                blnSuccess = false;
            }

            if (blnSuccess) {
                // Update the acquisition start time
                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblTotalRuntime);
            }

            return blnSuccess;

        }

        private bool ParseGCIniFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            double dblTotalRuntime = 0;

            var blnSuccess = false;

            try {
                // Open the GC.ini file
                var strFilePath = Path.Combine(strFolderPath, AGILENT_GC_INI_FILE);
                if (!File.Exists(strFilePath)) {
                    return false;
                }

                using (var srInFile = new StreamReader(strFilePath)) {

                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if (string.IsNullOrWhiteSpace(strLineIn))
                        {
                            continue;
                        }

                        if (!strLineIn.StartsWith("gc.runlength"))
                        {
                            continue;
                        }
                        
                        // Runtime is the value after the equals sign
                        var strSplitLine = strLineIn.Split('=');
                        if (strSplitLine.Length <= 1)
                        {
                            continue;
                        }

                        if (double.TryParse(strSplitLine[1], out dblTotalRuntime)) {
                            blnSuccess = true;
                        }
                    }
                }

            } catch (Exception ex) {
                // Exception reading file
                ReportError("Exception reading " + AGILENT_GC_INI_FILE + ": " + ex.Message);
                blnSuccess = false;
            }

            if (blnSuccess) {
                // Update the acquisition start time
                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblTotalRuntime);
            }

            return blnSuccess;

        }

        protected bool ProcessChemstationMSDataFile(string strDatafilePath, clsDatasetFileInfo datasetFileInfo)
        {
            bool blnSuccess;
            var intCurrentIndex = 0;

            try {
                using (var oReader = new ChemstationMSFileReader.clsChemstationDataMSFileReader(strDatafilePath)) {

                    datasetFileInfo.AcqTimeStart = oReader.Header.AcqDate;
                    datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(oReader.Header.RetentionTimeMinutesEnd);

                    datasetFileInfo.ScanCount = oReader.Header.SpectraCount;

                    for (var intSpectrumIndex = 0; intSpectrumIndex <= datasetFileInfo.ScanCount - 1; intSpectrumIndex++) {
                        intCurrentIndex = intSpectrumIndex;

                        ChemstationMSFileReader.clsSpectralRecord oSpectrum = null;
                        List<float> lstMZs = null;
                        List<Int32> lstIntensities = null;
                        const int intMSLevel = 1;

                        bool blnValidSpectrum;
                        try {
                            oReader.GetSpectrum(intSpectrumIndex, ref oSpectrum);
                            lstMZs = oSpectrum.Mzs;
                            lstIntensities = oSpectrum.Intensities;
                            blnValidSpectrum = true;
                        } catch (Exception ex) {
                            ReportError("Exception obtaining data from the MS file for spectrum index " + intCurrentIndex + ": " + ex.Message);
                            blnValidSpectrum = false;
                        }



                        if (blnValidSpectrum) {
                            var objScanStatsEntry = new clsScanStatsEntry
                            {
                                ScanNumber = intSpectrumIndex + 1,
                                ScanType = intMSLevel,
                                ScanTypeName = "GC-MS",
                                ScanFilterText = "",
                                ElutionTime = oSpectrum.RetentionTimeMinutes.ToString("0.0000"),
                                TotalIonIntensity = StringUtilities.ValueToString(oSpectrum.TIC, 1),
                                BasePeakIntensity = StringUtilities.ValueToString(oSpectrum.BasePeakAbundance, 1),
                                BasePeakMZ = StringUtilities.ValueToString(oSpectrum.BasePeakMZ, 5),
                                BasePeakSignalToNoiseRatio = "0",
                                IonCount = lstMZs.Count
                            };

                            objScanStatsEntry.IonCountRaw = objScanStatsEntry.IonCount;

                            mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                            if (mSaveTICAndBPI) {
                                mTICandBPIPlot.AddData(objScanStatsEntry.ScanNumber, intMSLevel, oSpectrum.RetentionTimeMinutes, oSpectrum.BasePeakAbundance, oSpectrum.TIC);

                                if (lstMZs.Count > 0) {
                                    double[] dblIonsMZ = null;
                                    double[] dblIonsIntensity = null;
                                    dblIonsMZ = new double[lstMZs.Count];
                                    dblIonsIntensity = new double[lstMZs.Count];

                                    for (var intIndex = 0; intIndex <= lstMZs.Count - 1; intIndex++) {
                                        dblIonsMZ[intIndex] = lstMZs[intIndex];
                                        dblIonsIntensity[intIndex] = lstIntensities[intIndex];
                                    }

                                    mLCMS2DPlot.AddScan(objScanStatsEntry.ScanNumber, intMSLevel, oSpectrum.RetentionTimeMinutes, dblIonsMZ.Length, dblIonsMZ, dblIonsIntensity);
                                }

                            }

                            if (mCheckCentroidingStatus) {
                                var lstMzDoubles = new List<double>(lstMZs.Count);
                                lstMzDoubles.AddRange(lstMZs.Select(ion => (double)ion));
                                mDatasetStatsSummarizer.ClassifySpectrum(lstMzDoubles, intMSLevel);
                            }

                        }

                    }

                }

                blnSuccess = true;

            } catch (Exception ex) {
                // Exception reading file
                ReportError("Exception reading data from the MS file at spectrum index " + intCurrentIndex + ": " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            var blnSuccess = false;
            var blnAcqTimeDetermined = false;

            try {
                var ioFolderInfo = new DirectoryInfo(strDataFilePath);
                var strMSDataFilePath = Path.Combine(ioFolderInfo.FullName, AGILENT_MS_DATA_FILE);

                datasetFileInfo.FileSystemCreationTime = ioFolderInfo.CreationTime;
                datasetFileInfo.FileSystemModificationTime = ioFolderInfo.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(ioFolderInfo.Name);
                datasetFileInfo.FileExtension = ioFolderInfo.Extension;
                datasetFileInfo.FileSizeBytes = 0;

                // Look for the MS file
                // Use its modification time to get an initial estimate for the acquisition end time
                // Assign the .MS file's size to .FileSizeBytes
                var fiMSDatafile = new FileInfo(strMSDataFilePath);
                if (fiMSDatafile.Exists) {
                    datasetFileInfo.FileSizeBytes = fiMSDatafile.Length;
                    datasetFileInfo.AcqTimeStart = fiMSDatafile.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = fiMSDatafile.LastWriteTime;

                    // Read the file info from the file system
                    UpdateDatasetFileStats(fiMSDatafile, datasetFileInfo.DatasetID);

                    blnSuccess = true;
                }

                datasetFileInfo.ScanCount = 0;

                if (blnSuccess) {
                    // Read the detailed data from the MS file
                    blnSuccess = ProcessChemstationMSDataFile(strMSDataFilePath, datasetFileInfo);

                    if (blnSuccess) {
                        blnAcqTimeDetermined = true;
                    }

                }

                if (!blnSuccess) {
                    // MS file not found (or problems parsing); use acqmeth.txt and/or GC.ini

                    // The timestamp of the acqmeth.txt file or GC.ini file is more accurate than the GC.ini file, so we'll use that
                    var fiMethodFile = new FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_ACQ_METHOD_FILE));
                    if (!fiMethodFile.Exists) {
                        fiMethodFile = new FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_GC_INI_FILE));
                    }


                    if (fiMethodFile.Exists) {

                        // Update the AcqTimes only if the LastWriteTime of the acqmeth.txt or GC.ini file is within the next 60 minutes of .AcqTimeEnd
                        if (!blnSuccess || fiMethodFile.LastWriteTime.Subtract(datasetFileInfo.AcqTimeEnd).TotalMinutes < 60) {
                            datasetFileInfo.AcqTimeStart = fiMethodFile.LastWriteTime;
                            datasetFileInfo.AcqTimeEnd = fiMethodFile.LastWriteTime;
                            blnSuccess = true;
                        }

                        if (datasetFileInfo.FileSizeBytes == 0) {
                            // File size was not determined from the MS file
                            // Instead, sum up the sizes of all of the files in this folder
                            foreach (var item in ioFolderInfo.GetFiles()) {
                                datasetFileInfo.FileSizeBytes += item.Length;
                            }

                            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                        }

                    }
                }


                if (!blnAcqTimeDetermined) {
                    try {
                        // Parse the acqmeth.txt file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                        blnSuccess = ParseAcqMethodFile(strDataFilePath, datasetFileInfo);

                        if (!blnSuccess) {
                            // Try to extract Runtime from the GC.ini file
                            blnSuccess = ParseGCIniFile(strDataFilePath, datasetFileInfo);
                        }

                    } catch (Exception ex) {
                        // Error parsing the acqmeth.txt file or GC.in file; do not abort
                    }

                    // We set blnSuccess to true, even if either of the above functions fail
                    blnSuccess = true;
                }


                if (blnSuccess) {
                    // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);

                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                }


            } catch (Exception ex) {
                ReportError("Exception parsing GC .D folder: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;
        }

    }
}
