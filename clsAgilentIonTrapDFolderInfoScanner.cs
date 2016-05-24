using System;
using System.IO;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//

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

        private bool ExtractMethodLineDate(string strLineIn, out DateTime dtDate)
        {

            var blnSuccess = false;
            dtDate = DateTime.MinValue;

            try
            {
                var strSplitLine = strLineIn.Trim().Split(' ');
                if (strSplitLine.Length >= 2)
                {
                    blnSuccess = DateTime.TryParse(strSplitLine[strSplitLine.Length - 1] + " " + strSplitLine[strSplitLine.Length - 2], out dtDate);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }

            return blnSuccess;
        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            // The dataset name is simply the folder name without .D
            try
            {
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private TimeSpan SecondsToTimeSpan(double dblSeconds)
        {

            TimeSpan dtTimeSpan;

            try
            {
                dtTimeSpan = new TimeSpan(0, 0, (int)dblSeconds);
            }
            catch (Exception)
            {
                dtTimeSpan = new TimeSpan(0, 0, 0);
            }

            return dtTimeSpan;

        }

        private bool ParseRunLogFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            var strMostRecentMethodLine = string.Empty;

            bool blnSuccess;

            try
            {
                // Try to open the Run.Log file
                bool blnProcessedFirstMethodLine;
                bool blnEndDateFound;
                DateTime dtMethodDate;

                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, AGILENT_RUN_LOG_FILE)))
                {

                    blnProcessedFirstMethodLine = false;
                    blnEndDateFound = false;
                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if (string.IsNullOrEmpty(strLineIn))
                        {
                            continue;
                        }

                        if (!strLineIn.StartsWith(RUN_LOG_FILE_METHOD_LINE_START))
                        {
                            continue;
                        }

                        strMostRecentMethodLine = string.Copy(strLineIn);

                        // Method line found
                        // See if the line contains a key phrase
                        var intCharLoc = strLineIn.IndexOf(RUN_LOG_FILE_INSTRUMENT_RUNNING, StringComparison.Ordinal);
                        if (intCharLoc > 0)
                        {
                            if (ExtractMethodLineDate(strLineIn, out dtMethodDate))
                            {
                                datasetFileInfo.AcqTimeStart = dtMethodDate;
                            }
                            blnProcessedFirstMethodLine = true;
                        }
                        else
                        {
                            intCharLoc = strLineIn.IndexOf(RUN_LOG_INSTRUMENT_RUN_COMPLETED, StringComparison.Ordinal);
                            if (intCharLoc > 0)
                            {
                                if (ExtractMethodLineDate(strLineIn, out dtMethodDate))
                                {
                                    datasetFileInfo.AcqTimeEnd = dtMethodDate;
                                    blnEndDateFound = true;
                                }
                            }
                        }

                        // If this is the first method line, then parse out the date and store in .AcqTimeStart
                        if (!blnProcessedFirstMethodLine)
                        {
                            if (ExtractMethodLineDate(strLineIn, out dtMethodDate))
                            {
                                datasetFileInfo.AcqTimeStart = dtMethodDate;
                            }
                        }
                    }
                }

                if (blnProcessedFirstMethodLine & !blnEndDateFound)
                {
                    // Use the last time in the file as the .AcqTimeEnd value
                    if (ExtractMethodLineDate(strMostRecentMethodLine, out dtMethodDate))
                    {
                        datasetFileInfo.AcqTimeEnd = dtMethodDate;
                    }
                }

                blnSuccess = blnProcessedFirstMethodLine;

            }
            catch (Exception)
            {
                // Run.log file not found
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseAnalysisCDFFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            NetCDFReader.clsMSNetCdf objNETCDFReader = null;

            bool blnSuccess;

            try
            {
                objNETCDFReader = new NetCDFReader.clsMSNetCdf();
                blnSuccess = objNETCDFReader.OpenMSCdfFile(Path.Combine(strFolderPath, AGILENT_ANALYSIS_CDF_FILE));
                if (blnSuccess)
                {
                    var intScanCount = objNETCDFReader.GetScanCount();

                    if (intScanCount > 0)
                    {
                        // Lookup the scan time of the final scan

                        var intScanNumber = 0;
                        double dblScanTotalIntensity = 0;
                        double dblScanTime = 0;
                        double dblMassMin = 0;
                        double dblMassMax = 0;

                        if (objNETCDFReader.GetScanInfo(intScanCount - 1, ref intScanNumber, ref dblScanTotalIntensity, ref dblScanTime, ref dblMassMin, ref dblMassMax))
                        {
                            // Add 1 to intScanNumber since the scan number is off by one in the CDF file
                            datasetFileInfo.ScanCount = intScanNumber + 1;
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.Add(SecondsToTimeSpan(dblScanTime));
                        }
                    }
                    else
                    {
                        datasetFileInfo.ScanCount = 0;
                    }
                }
            }
            catch (Exception)
            {
                blnSuccess = false;
            }
            finally
            {
                if ((objNETCDFReader != null))
                {
                    objNETCDFReader.CloseMSCdfFile();
                }
            }

            return blnSuccess;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strDataFilePath">Dataset folder ptah</param>
        /// <param name="datasetFileInfo"></param>
        /// <returns></returns>
        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            var blnSuccess = false;

            try {
                var diFolder = new DirectoryInfo(strDataFilePath);

                datasetFileInfo.FileSystemCreationTime = diFolder.CreationTime;
                datasetFileInfo.FileSystemModificationTime = diFolder.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(diFolder.Name);
                datasetFileInfo.FileExtension = diFolder.Extension;

                // Look for the Analysis.yep file
                // Use its modification time to get an initial estimate for the acquisition time
                // Assign the .Yep file's size to .FileSizeBytes
                var fiYepFile = new FileInfo(Path.Combine(diFolder.FullName, AGILENT_YEP_FILE));
                if (fiYepFile.Exists)
                {
                    datasetFileInfo.FileSizeBytes = fiYepFile.Length;
                    datasetFileInfo.AcqTimeStart = fiYepFile.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = fiYepFile.LastWriteTime;
                    blnSuccess = true;
                } else {
                    // Analysis.yep not found; look for Run.log
                    var fiRunLog = new FileInfo(Path.Combine(diFolder.FullName, AGILENT_RUN_LOG_FILE));
                    if (fiRunLog.Exists)
                    {
                        datasetFileInfo.AcqTimeStart = fiRunLog.LastWriteTime;
                        datasetFileInfo.AcqTimeEnd = fiRunLog.LastWriteTime;
                        blnSuccess = true;

                        // Sum up the sizes of all of the files in this folder
                        datasetFileInfo.FileSizeBytes = 0;
                        foreach (var datasetFile in diFolder.GetFiles()) {
                            datasetFileInfo.FileSizeBytes += datasetFile.Length;
                        }
                    }
                }

                datasetFileInfo.ScanCount = 0;

                if (blnSuccess) {
                    try {
                        // Parse the Run Log file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                        blnSuccess = ParseRunLogFile(strDataFilePath, datasetFileInfo);

                        // Parse the Analysis.cdf file to determine the scan count and to further refine .AcqTimeStart
                        blnSuccess = ParseAnalysisCDFFile(strDataFilePath, datasetFileInfo);
                    } catch (Exception) {
                        // Error parsing the Run Log file or the Analysis.cdf file; do not abort

                    }

                    blnSuccess = true;
                }

            } catch (Exception) {
                blnSuccess = false;
            }

            return blnSuccess;
        }

    }
}
