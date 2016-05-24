using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PNNLOmics.Utilities;
using ThermoRawFileReaderDLL.FinniganFileIO;
using SpectraTypeClassifier;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//
// Last modified May 11, 2015

namespace MSFileInfoScanner
{
    public class clsFinniganRawFileInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps
        public const string FINNIGAN_RAW_FILE_EXTENSION = ".RAW";

        private readonly Regex mIsCentroid;
        private readonly Regex mIsProfileM;

        /// <summary>
        /// Constructor
        /// </summary>
        public clsFinniganRawFileInfoScanner()
        {
            mIsCentroid = new Regex("([FI]TMS [+-] c .+)|([FI]TMS {[^ ]+} +[+-] c .+)|(^ *[+-] c .+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mIsProfileM = new Regex("([FI]TMS [+-] p .+)|([FI]TMS {[^ ]+} +[+-] p .+)|(^ *[+-] p .+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// This function is used to determine one or more overall quality scores
        /// </summary>
        /// <param name="objXcaliburAccessor"></param>
        /// <param name="datasetFileInfo"></param>
        /// <remarks></remarks>

        protected void ComputeQualityScores(XRawFileIO objXcaliburAccessor, clsDatasetFileInfo datasetFileInfo)
        {
            float sngOverallScore;

            double dblOverallAvgIntensitySum = 0;
            var intOverallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0) {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all of the data using objXcaliburAccessor
                const int intMSLevelFilter = 1;
                sngOverallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(intMSLevelFilter);

            } else {
                var intScanCount = objXcaliburAccessor.GetNumScans();
                int intScanStart;
                int intScanEnd;
                GetStartAndEndScans(intScanCount, out intScanStart, out intScanEnd);

                for (var intScanNumber = intScanStart; intScanNumber <= intScanEnd; intScanNumber++)
                {
                    // This function returns the number of points in dblMassIntensityPairs()
                    double[,] dblMassIntensityPairs;
                    var intReturnCode = objXcaliburAccessor.GetScanData2D(intScanNumber, out dblMassIntensityPairs);


                    if (intReturnCode <= 0)
                    {
                        continue;
                    }

                    if ((dblMassIntensityPairs == null) || dblMassIntensityPairs.GetLength(1) <= 0)
                    {
                        continue;
                    }

                    // Keep track of the quality scores and then store one or more overall quality scores in datasetFileInfo.OverallQualityScore
                    // For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                    double dblIntensitySum = 0;
                    for (var intIonIndex = 0; intIonIndex <= dblMassIntensityPairs.GetUpperBound(1); intIonIndex++) {
                        dblIntensitySum += dblMassIntensityPairs[1, intIonIndex];
                    }

                    dblOverallAvgIntensitySum += dblIntensitySum / dblMassIntensityPairs.GetLength(1);

                    intOverallAvgCount += 1;
                }

                if (intOverallAvgCount > 0) {
                    sngOverallScore = Convert.ToSingle(dblOverallAvgIntensitySum / intOverallAvgCount);
                } else {
                    sngOverallScore = 0;
                }

            }

            datasetFileInfo.OverallQualityScore = sngOverallScore;

        }
        
        protected clsSpectrumTypeClassifier.eCentroidStatusConstants GetCentroidStatus(int intScanNumber, FinniganFileReaderBaseClass.udtScanHeaderInfoType udtScanHeaderInfoType)
        {
           

            if (udtScanHeaderInfoType.IsCentroidScan) {
                if (mIsProfileM.IsMatch(udtScanHeaderInfoType.FilterText)) {
                    ShowMessage("Warning: Scan " + intScanNumber + " appears to be profile mode data, yet XRawFileIO reported it to be centroid");
                }

                return clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid;
            }

            if (mIsCentroid.IsMatch(udtScanHeaderInfoType.FilterText)) {
                ShowMessage("Warning: Scan " + intScanNumber + " appears to be centroided data, yet XRawFileIO reported it to be profile");
            }

            return clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile;
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="strDataFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            try {
                // The dataset name is simply the file name without .Raw
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            } catch (Exception) {
                return string.Empty;
            }
        }


        protected void LoadScanDetails(XRawFileIO objXcaliburAccessor)
        {
            var udtScanHeaderInfo = new FinniganFileReaderBaseClass.udtScanHeaderInfoType();

            int intScanStart;
            int intScanEnd;

            Console.Write("  Loading scan details");

            if (mSaveTICAndBPI) {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (mSaveLCMS2DPlots) {
                InitializeLCMS2DPlot();
            }

            var dtLastProgressTime = DateTime.UtcNow;

            var intScanCount = objXcaliburAccessor.GetNumScans();
            GetStartAndEndScans(intScanCount, out intScanStart, out intScanEnd);

            for (var intScanNumber = intScanStart; intScanNumber <= intScanEnd; intScanNumber++) {

                try {
                    if (mShowDebugInfo) {
                        Console.WriteLine(" ... scan " + intScanNumber);
                    }

                    var blnSuccess = objXcaliburAccessor.GetScanInfo(intScanNumber, out udtScanHeaderInfo);

                    if (blnSuccess) {
                        if (mSaveTICAndBPI) {
                            mTICandBPIPlot.AddData(
                                intScanNumber, 
                                udtScanHeaderInfo.MSLevel, 
                                Convert.ToSingle(udtScanHeaderInfo.RetentionTime), 
                                udtScanHeaderInfo.BasePeakIntensity, 
                                udtScanHeaderInfo.TotalIonCurrent);
                        }

                        var objScanStatsEntry = new clsScanStatsEntry
                        {
                            ScanNumber = intScanNumber,
                            ScanType = udtScanHeaderInfo.MSLevel,
                            ScanTypeName = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(udtScanHeaderInfo.FilterText),
                            ScanFilterText = XRawFileIO.MakeGenericFinniganScanFilter(udtScanHeaderInfo.FilterText),
                            ElutionTime = udtScanHeaderInfo.RetentionTime.ToString("0.0000"),
                            TotalIonIntensity = StringUtilities.ValueToString(udtScanHeaderInfo.TotalIonCurrent, 5),
                            BasePeakIntensity = StringUtilities.ValueToString(udtScanHeaderInfo.BasePeakIntensity, 5),
                            BasePeakMZ = StringUtilities.DblToString(udtScanHeaderInfo.BasePeakMZ, 4),
                            BasePeakSignalToNoiseRatio = "0",
                            IonCount = udtScanHeaderInfo.NumPeaks,
                            IonCountRaw = udtScanHeaderInfo.NumPeaks
                        };


                        // Store the ScanEvent values in .ExtendedScanInfo
                        StoreExtendedScanInfo(ref objScanStatsEntry.ExtendedScanInfo, udtScanHeaderInfo.ScanEventNames, udtScanHeaderInfo.ScanEventValues);

                        // Store the collision mode and the scan filter text
                        objScanStatsEntry.ExtendedScanInfo.CollisionMode = udtScanHeaderInfo.CollisionMode;
                        objScanStatsEntry.ExtendedScanInfo.ScanFilterText = udtScanHeaderInfo.FilterText;

                        mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                    }
                } catch (Exception ex) {
                    ReportError("Error loading header info for scan " + intScanNumber + ": " + ex.Message);
                }


                try {
                    if (mSaveLCMS2DPlots | mCheckCentroidingStatus) {
                        // Also need to load the raw data

                        double[,] dblMassIntensityPairs;

                        // Load the ions for this scan
                        var intIonCount = objXcaliburAccessor.GetScanData2D(intScanNumber, out dblMassIntensityPairs);

                        if (intIonCount > 0) {
                            if (mSaveLCMS2DPlots) {
                                mLCMS2DPlot.AddScan2D(intScanNumber, udtScanHeaderInfo.MSLevel, Convert.ToSingle(udtScanHeaderInfo.RetentionTime), intIonCount, dblMassIntensityPairs);
                            }

                            if (mCheckCentroidingStatus) {
                                var mzCount = dblMassIntensityPairs.GetLength(1);

                                var lstMZs = new List<double>(mzCount);

                                for (var i = 0; i <= mzCount - 1; i++) {
                                    lstMZs.Add(dblMassIntensityPairs[0, i]);
                                }

                                var centroidingStatus = GetCentroidStatus(intScanNumber, udtScanHeaderInfo);

                                mDatasetStatsSummarizer.ClassifySpectrum(lstMZs, udtScanHeaderInfo.MSLevel, centroidingStatus);
                            }
                        }

                    }

                } catch (Exception ex) {
                    ReportError("Error loading m/z and intensity values for scan " + intScanNumber + ": " + ex.Message);
                }

                ShowProgress(intScanNumber, intScanCount, ref dtLastProgressTime);

            }

            Console.WriteLine();

        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="strDataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            var strDataFilePathLocal = string.Empty;

            // Obtain the full path to the file
            var fiRawFile = new FileInfo(strDataFilePath);

            if (!fiRawFile.Exists) {
                ShowMessage(".Raw file not found: " + strDataFilePath);
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // intDatasetID = LookupDatasetID(strDatasetName)
            var intDatasetID = DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = fiRawFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = fiRawFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = intDatasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(fiRawFile.Name);
            datasetFileInfo.FileExtension = fiRawFile.Extension;
            datasetFileInfo.FileSizeBytes = fiRawFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            var blnDeleteLocalFile = false;
            var blnReadError = false;

            // Use Xraw to read the .Raw file
            // If reading from a SAMBA-mounted network share, and if the current user has 
            //  Read privileges but not Read&Execute privileges, then we will need to copy the file locally
            var objXcaliburAccessor = new XRawFileIO();

            // Open a handle to the data file
            if (!objXcaliburAccessor.OpenRawFile(fiRawFile.FullName)) {
                // File open failed
                ReportError("Call to .OpenRawFile failed for: " + fiRawFile.FullName);
                blnReadError = true;


                if (!string.Equals(clsMSFileInfoScanner.GetAppFolderPath().Substring(0, 2), fiRawFile.FullName.Substring(0, 2), StringComparison.InvariantCultureIgnoreCase)) {
                    if (mCopyFileLocalOnReadError) {
                        // Copy the file locally and try again

                        try {
                            strDataFilePathLocal = Path.Combine(clsMSFileInfoScanner.GetAppFolderPath(), Path.GetFileName(strDataFilePath));


                            if (!string.Equals(strDataFilePathLocal, strDataFilePath, StringComparison.InvariantCultureIgnoreCase)) {
                                ShowMessage("Copying file " + Path.GetFileName(strDataFilePath) + " to the working folder");
                                File.Copy(strDataFilePath, strDataFilePathLocal, true);

                                strDataFilePath = string.Copy(strDataFilePathLocal);
                                blnDeleteLocalFile = true;

                                // Update fiRawFile then try to re-open
                                fiRawFile = new FileInfo(strDataFilePath);

                                if (!objXcaliburAccessor.OpenRawFile(fiRawFile.FullName)) {
                                    // File open failed
                                    ReportError("Call to .OpenRawFile failed for: " + fiRawFile.FullName);
                                    blnReadError = true;
                                } else {
                                    blnReadError = false;
                                }
                            }
                        } catch (Exception) {
                            blnReadError = true;
                        }
                    }

                }

            }

            if (!blnReadError) {
                // Read the file info
                try {
                    datasetFileInfo.AcqTimeStart = objXcaliburAccessor.FileInfo.CreationDate;
                } catch (Exception) {
                    // Read error
                    blnReadError = true;
                }

                if (!blnReadError) {
                    try {
                        // Look up the end scan time then compute .AcqTimeEnd
                        var intScanEnd = objXcaliburAccessor.FileInfo.ScanEnd;
                        FinniganFileReaderBaseClass.udtScanHeaderInfoType udtScanHeaderInfo;
                        objXcaliburAccessor.GetScanInfo(intScanEnd, out udtScanHeaderInfo);

                        datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(udtScanHeaderInfo.RetentionTime);
                        datasetFileInfo.ScanCount = objXcaliburAccessor.GetNumScans();
                    } catch (Exception) {
                        // Error; use default values
                        var _with5 = datasetFileInfo;
                        _with5.AcqTimeEnd = _with5.AcqTimeStart;
                        _with5.ScanCount = 0;
                    }

                    if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile || mSaveLCMS2DPlots || mCheckCentroidingStatus) {
                        // Load data from each scan
                        // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                        LoadScanDetails(objXcaliburAccessor);
                    }

                    if (mComputeOverallQualityScores) {
                        // Note that this call will also create the TICs and BPIs
                        ComputeQualityScores(objXcaliburAccessor, datasetFileInfo);
                    }
                }
            }


            var _with6 = mDatasetStatsSummarizer.SampleInfo;
            _with6.SampleName = objXcaliburAccessor.FileInfo.SampleName;
            _with6.Comment1 = objXcaliburAccessor.FileInfo.Comment1;
            _with6.Comment2 = objXcaliburAccessor.FileInfo.Comment2;

            if (!string.IsNullOrEmpty(objXcaliburAccessor.FileInfo.SampleComment)) {
                if (string.IsNullOrEmpty(_with6.Comment1)) {
                    _with6.Comment1 = objXcaliburAccessor.FileInfo.SampleComment;
                } else {
                    if (string.IsNullOrEmpty(_with6.Comment2)) {
                        _with6.Comment2 = objXcaliburAccessor.FileInfo.SampleComment;
                    } else {
                        // Append the sample comment to comment 2
                        _with6.Comment2 += "; " + objXcaliburAccessor.FileInfo.SampleComment;
                    }
                }
            }


            // Close the handle to the data file
            objXcaliburAccessor.CloseRawFile();

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            UpdateDatasetFileStats(fiRawFile, intDatasetID);

            // Copy over the updated filetime info from datasetFileInfo to mDatasetFileInfo
            var _with7 = mDatasetStatsSummarizer.DatasetFileInfo;
            _with7.FileSystemCreationTime = datasetFileInfo.FileSystemCreationTime;
            _with7.FileSystemModificationTime = datasetFileInfo.FileSystemModificationTime;
            _with7.DatasetID = datasetFileInfo.DatasetID;
            _with7.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            _with7.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            _with7.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            _with7.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            _with7.ScanCount = datasetFileInfo.ScanCount;
            _with7.FileSizeBytes = datasetFileInfo.FileSizeBytes;

            // Delete the local copy of the data file
            if (blnDeleteLocalFile) {
                try {
                    File.Delete(strDataFilePathLocal);
                } catch (Exception) {
                    // Deletion failed
                    ReportError("Deletion failed for: " + Path.GetFileName(strDataFilePathLocal));
                }
            }

            return !blnReadError;

        }


        protected void StoreExtendedScanInfo(ref clsScanStatsEntry.udtExtendedStatsInfoType udtExtendedScanInfo, string strEntryName, string strEntryValue)
        {
            if (strEntryValue == null) {
                strEntryValue = string.Empty;
            }

            //'Dim strEntryNames(0) As String
            //'Dim strEntryValues(0) As String

            //'strEntryNames(0) = String.Copy(strEntryName)
            //'strEntryValues(0) = String.Copy(strEntryValue)

            //'StoreExtendedScanInfo(htExtendedScanInfo, strEntryNames, strEntryValues)

            // This command is equivalent to the above series of commands
            // It converts strEntryName to an array and strEntryValue to a separate array and passes those arrays to StoreExtendedScanInfo()
            StoreExtendedScanInfo(ref udtExtendedScanInfo, new[] { strEntryName }, new[] { strEntryValue });

        }


        protected void StoreExtendedScanInfo(ref clsScanStatsEntry.udtExtendedStatsInfoType udtExtendedScanInfo, string[] strEntryNames, string[] strEntryValues)
        {
            var cTrimChars = new[] {
                ':',
                ' '
            };

            try {
                if (strEntryNames == null || strEntryValues == null)
                {
                    return;
                }

                for (var intIndex = 0; intIndex <= strEntryNames.Length - 1; intIndex++) {
                    if (strEntryNames[intIndex] == null || strEntryNames[intIndex].Trim().Length == 0) {
                        // Empty entry name; do not add
                        continue;
                    }

                    // We're only storing certain entries from strEntryNames
                    var entryNameLCase = strEntryNames[intIndex].ToLower().TrimEnd(cTrimChars);

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_ION_INJECTION_TIME,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.IonInjectionTime = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_SCAN_SEGMENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.ScanSegment = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_SCAN_EVENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.ScanEvent = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_CHARGE_STATE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.ChargeState = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_MONOISOTOPIC_MZ,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.MonoisotopicMZ = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_COLLISION_MODE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.CollisionMode = strEntryValues[intIndex];
                        break;
                    }

                    if (string.Equals(entryNameLCase, clsScanStatsEntry.SCANSTATS_COL_SCAN_FILTER_TEXT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        udtExtendedScanInfo.ScanFilterText = strEntryValues[intIndex];
                        break;
                    }
                }
            } catch (Exception) {
                // Ignore any errors here
            }

        }       

    }
}
