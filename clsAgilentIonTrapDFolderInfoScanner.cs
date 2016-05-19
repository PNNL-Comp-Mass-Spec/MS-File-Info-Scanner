using System;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//
// Last modified September 17, 2005

namespace MSFileInfoScanner
{
    public class clsAgilentIonTrapDFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string AGILENT_ION_TRAP_D_EXTENSION = ".D";
        private const string AGILENT_YEP_FILE = "Analysis.yep";
        private const string AGILENT_RUN_LOG_FILE = "RUN.LOG";

        private const string AGILENT_ANALYSIS_CDF_FILE = "Analysis.cdf";
        private const string RUN_LOG_FILE_METHOD_LINE_START = "Method";
        private const string RUN_LOG_FILE_INSTRUMENT_RUNNING = "Instrument running sample";

        private const string RUN_LOG_INSTRUMENT_RUN_COMPLETED = "Instrument run completed";
        private bool ExtractMethodLineDate(string strLineIn, ref DateTime dtDate)
        {

            string[] strSplitLine[ = null;
            bool blnSuccess = false;

            blnSuccess = false;
            try {
                strSplitLine[ = strLineIn.Trim.Split(' ');
                if (strSplitLine[.Length >= 2) {
                    dtDate = System.DateTime.Parse(strSplitLine[strSplitLine[.Length - 1) + " " + strSplitLine[strSplitLine[.Length - 2));
                    blnSuccess = true;
                }
            } catch (Exception ex) {
                // Ignore errors
            }

            return blnSuccess;
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

        private TimeSpan SecondsToTimeSpan(double dblSeconds)
        {

            TimeSpan dtTimeSpan = default(TimeSpan);

            try {
                dtTimeSpan = new TimeSpan(0, 0, Convert.ToInt32(dblSeconds));
            } catch (Exception ex) {
                dtTimeSpan = new TimeSpan(0, 0, 0);
            }

            return dtTimeSpan;

        }

        private bool ParseRunLogFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            string strLineIn = null;
            string strMostRecentMethodLine = string.Empty;

            int intCharLoc = 0;
            DateTime dtMethodDate = default(DateTime);

            bool blnProcessedFirstMethodLine = false;
            bool blnEndDateFound = false;
            bool blnSuccess = false;

            try {
                // Try to open the Run.Log file
                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, AGILENT_RUN_LOG_FILE))) {

                    blnProcessedFirstMethodLine = false;
                    blnEndDateFound = false;
                    while (!srInFile.EndOfStream) {
                        strLineIn = srInFile.ReadLine();

                        if ((strLineIn != null)) {
                            if (strLineIn.StartsWith(RUN_LOG_FILE_METHOD_LINE_START)) {
                                strMostRecentMethodLine = string.Copy(strLineIn);

                                // Method line found
                                // See if the line contains a key phrase
                                intCharLoc = strLineIn.IndexOf(RUN_LOG_FILE_INSTRUMENT_RUNNING);
                                if (intCharLoc > 0) {
                                    if (ExtractMethodLineDate(strLineIn, ref dtMethodDate)) {
                                        datasetFileInfo.AcqTimeStart = dtMethodDate;
                                    }
                                    blnProcessedFirstMethodLine = true;
                                } else {
                                    intCharLoc = strLineIn.IndexOf(RUN_LOG_INSTRUMENT_RUN_COMPLETED);
                                    if (intCharLoc > 0) {
                                        if (ExtractMethodLineDate(strLineIn, ref dtMethodDate)) {
                                            datasetFileInfo.AcqTimeEnd = dtMethodDate;
                                            blnEndDateFound = true;
                                        }
                                    }
                                }

                                // If this is the first method line, then parse out the date and store in .AcqTimeStart
                                if (!blnProcessedFirstMethodLine) {
                                    if (ExtractMethodLineDate(strLineIn, ref dtMethodDate)) {
                                        datasetFileInfo.AcqTimeStart = dtMethodDate;
                                    }
                                }
                            }
                        }
                    }
                }

                if (blnProcessedFirstMethodLine & !blnEndDateFound) {
                    // Use the last time in the file as the .AcqTimeEnd value
                    if (ExtractMethodLineDate(strMostRecentMethodLine, ref dtMethodDate)) {
                        datasetFileInfo.AcqTimeEnd = dtMethodDate;
                    }
                }

                blnSuccess = blnProcessedFirstMethodLine;

            } catch (Exception ex) {
                // Run.log file not found
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseAnalysisCDFFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            NetCDFReader.clsMSNetCdf objNETCDFReader = null;

            int intScanCount = 0;
            int intScanNumber = 0;
            double dblScanTotalIntensity = 0;
            double dblScanTime = 0;
            double dblMassMin = 0;
            double dblMassMax = 0;

            bool blnSuccess = false;

            try {
                objNETCDFReader = new NetCDFReader.clsMSNetCdf();
                blnSuccess = objNETCDFReader.OpenMSCdfFile(Path.Combine(strFolderPath, AGILENT_ANALYSIS_CDF_FILE));
                if (blnSuccess) {
                    intScanCount = objNETCDFReader.GetScanCount();

                    if (intScanCount > 0) {
                        // Lookup the scan time of the final scan
                        if (objNETCDFReader.GetScanInfo(intScanCount - 1, intScanNumber, dblScanTotalIntensity, dblScanTime, dblMassMin, dblMassMax)) {
                            var _with1 = datasetFileInfo;
                            // Add 1 to intScanNumber since the scan number is off by one in the CDF file
                            _with1.ScanCount = intScanNumber + 1;
                            _with1.AcqTimeEnd = datasetFileInfo.AcqTimeStart.Add(SecondsToTimeSpan(dblScanTime));
                        }
                    } else {
                        datasetFileInfo.ScanCount = 0;
                    }
                }
            } catch (Exception ex) {
                blnSuccess = false;
            } finally {
                if ((objNETCDFReader != null)) {
                    objNETCDFReader.CloseMSCdfFile();
                }
            }

            return blnSuccess;

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            bool blnSuccess = false;
            DirectoryInfo ioFolderInfo = default(DirectoryInfo);
            FileInfo ioFileInfo = default(FileInfo);

            try {
                blnSuccess = false;
                ioFolderInfo = new DirectoryInfo(strDataFilePath);

                var _with2 = datasetFileInfo;
                _with2.FileSystemCreationTime = ioFolderInfo.CreationTime;
                _with2.FileSystemModificationTime = ioFolderInfo.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                _with2.AcqTimeStart = _with2.FileSystemModificationTime;
                _with2.AcqTimeEnd = _with2.FileSystemModificationTime;

                _with2.DatasetName = GetDatasetNameViaPath(ioFolderInfo.Name);
                _with2.FileExtension = ioFolderInfo.Extension;

                // Look for the Analysis.yep file
                // Use its modification time to get an initial estimate for the acquisition time
                // Assign the .Yep file's size to .FileSizeBytes
                ioFileInfo = new FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_YEP_FILE));
                if (ioFileInfo.Exists) {
                    _with2.FileSizeBytes = ioFileInfo.Length;
                    _with2.AcqTimeStart = ioFileInfo.LastWriteTime;
                    _with2.AcqTimeEnd = ioFileInfo.LastWriteTime;
                    blnSuccess = true;
                } else {
                    // Analysis.yep not found; look for Run.log
                    ioFileInfo = new FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_RUN_LOG_FILE));
                    if (ioFileInfo.Exists) {
                        _with2.AcqTimeStart = ioFileInfo.LastWriteTime;
                        _with2.AcqTimeEnd = ioFileInfo.LastWriteTime;
                        blnSuccess = true;

                        // Sum up the sizes of all of the files in this folder
                        _with2.FileSizeBytes = 0;
                        foreach ( ioFileInfo in ioFolderInfo.GetFiles()) {
                            _with2.FileSizeBytes += ioFileInfo.Length;
                        }
                    }
                }

                _with2.ScanCount = 0;

                if (blnSuccess) {
                    try {
                        // Parse the Run Log file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                        blnSuccess = ParseRunLogFile(strDataFilePath, ref datasetFileInfo);

                        // Parse the Analysis.cdf file to determine the scan count and to further refine .AcqTimeStart
                        blnSuccess = ParseAnalysisCDFFile(strDataFilePath, ref datasetFileInfo);
                    } catch (Exception ex) {
                        // Error parsing the Run Log file or the Analysis.cdf file; do not abort

                    }

                    blnSuccess = true;
                }

            } catch (Exception ex) {
                blnSuccess = false;
            }

            return blnSuccess;
        }

    }
}
