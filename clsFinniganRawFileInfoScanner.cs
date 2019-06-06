using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;
using SpectraTypeClassifier;
using ThermoRawFileReader;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005

namespace MSFileInfoScanner
{
    public class clsFinniganRawFileInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps
        public const string THERMO_RAW_FILE_EXTENSION = ".RAW";

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
        /// <param name="xcaliburAccessor"></param>
        /// <param name="datasetFileInfo"></param>
        /// <remarks></remarks>
        private void ComputeQualityScores(XRawFileIO xcaliburAccessor, DatasetFileInfo datasetFileInfo)
        {
            float overallScore;

            double overallAvgIntensitySum = 0;
            var overallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0)
            {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all of the data using xcaliburAccessor
                const int msLevelFilter = 1;
                overallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(msLevelFilter);

            }
            else
            {
                var scanCount = xcaliburAccessor.GetNumScans();
                GetStartAndEndScans(scanCount, out var scanStart, out var scanEnd);

                for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
                {
                    // This function returns the number of points in massIntensityPairs()
                    var returnCode = xcaliburAccessor.GetScanData2D(scanNumber, out var massIntensityPairs);

                    if (returnCode <= 0)
                    {
                        continue;
                    }

                    if (massIntensityPairs == null || massIntensityPairs.GetLength(1) <= 0)
                    {
                        continue;
                    }

                    // Keep track of the quality scores and then store one or more overall quality scores in datasetFileInfo.OverallQualityScore
                    // For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                    double intensitySum = 0;
                    for (var ionIndex = 0; ionIndex <= massIntensityPairs.GetUpperBound(1); ionIndex++)
                    {
                        intensitySum += massIntensityPairs[1, ionIndex];
                    }

                    overallAvgIntensitySum += intensitySum / massIntensityPairs.GetLength(1);

                    overallAvgCount += 1;
                }

                if (overallAvgCount > 0)
                {
                    overallScore = (float)(overallAvgIntensitySum / overallAvgCount);
                }
                else
                {
                    overallScore = 0;
                }

            }

            datasetFileInfo.OverallQualityScore = overallScore;

        }

        private clsSpectrumTypeClassifier.eCentroidStatusConstants GetCentroidStatus(
            int scanNumber,
            clsScanInfo scanInfo)
        {

            if (scanInfo.IsCentroided)
            {
                if (mIsProfileM.IsMatch(scanInfo.FilterText))
                {
                    OnWarningEvent("Warning: Scan " + scanNumber + " appears to be profile mode data, yet XRawFileIO reported it to be centroid");
                }

                return clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid;
            }

            if (mIsCentroid.IsMatch(scanInfo.FilterText))
            {
                OnWarningEvent("Warning: Scan " + scanNumber + " appears to be centroided data, yet XRawFileIO reported it to be profile");
            }

            return clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile;
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            try
            {
                // The dataset name is simply the file name without .Raw
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void LoadScanDetails(XRawFileIO xcaliburAccessor)
        {
            OnStatusEvent("  Loading scan details");

            if (mSaveTICAndBPI)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (mSaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            var lastProgressTime = DateTime.UtcNow;

            // Note that this starts at 2 seconds, but is extended after each progress message is shown (maxing out at 30 seconds)
            var progressThresholdSeconds = 2;

            var scanCount = xcaliburAccessor.GetNumScans();

            GetStartAndEndScans(scanCount, out var scanStart, out var scanEnd);

            var scansProcessed = 0;
            var totalScansToProcess = scanEnd - scanStart + 1;

            for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
            {

                clsScanInfo scanInfo;

                try
                {

                    var success = xcaliburAccessor.GetScanInfo(scanNumber, out scanInfo);

                    if (success)
                    {
                        if (mSaveTICAndBPI)
                        {
                            mTICAndBPIPlot.AddData(
                                scanNumber,
                                scanInfo.MSLevel,
                                (float)scanInfo.RetentionTime,
                                scanInfo.BasePeakIntensity,
                                scanInfo.TotalIonCurrent);
                        }

                        var scanStatsEntry = new ScanStatsEntry
                        {
                            ScanNumber = scanNumber,
                            ScanType = scanInfo.MSLevel,
                            ScanTypeName = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(scanInfo.FilterText),
                            ScanFilterText = XRawFileIO.MakeGenericFinniganScanFilter(scanInfo.FilterText),
                            ElutionTime = scanInfo.RetentionTime.ToString("0.0###"),
                            TotalIonIntensity = StringUtilities.ValueToString(scanInfo.TotalIonCurrent, 5),
                            BasePeakIntensity = StringUtilities.ValueToString(scanInfo.BasePeakIntensity, 5),
                            BasePeakMZ = scanInfo.BasePeakMZ.ToString("0.0###"),
                            BasePeakSignalToNoiseRatio = "0",
                            IonCount = scanInfo.NumPeaks,
                            IonCountRaw = scanInfo.NumPeaks,
                            MzMin = scanInfo.LowMass,
                            MzMax = scanInfo.HighMass
                        };

                        // Store the ScanEvent values in .ExtendedScanInfo
                        StoreExtendedScanInfo(scanStatsEntry.ExtendedScanInfo, scanInfo.ScanEvents);

                        // Store the collision mode and the scan filter text
                        scanStatsEntry.ExtendedScanInfo.CollisionMode = scanInfo.CollisionMode;
                        scanStatsEntry.ExtendedScanInfo.ScanFilterText = scanInfo.FilterText;

                        mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);

                    }
                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error loading header info for scan " + scanNumber + ": " + ex.Message);
                    continue;
                }

                try
                {
                    if (mSaveLCMS2DPlots || mCheckCentroidingStatus)
                    {
                        // Also need to load the raw data

                        // Load the ions for this scan
                        var ionCount = xcaliburAccessor.GetScanData2D(scanNumber, out var massIntensityPairs);

                        if (ionCount > 0)
                        {
                            if (mSaveLCMS2DPlots)
                            {
                                mLCMS2DPlot.AddScan2D(scanNumber, scanInfo.MSLevel, (float)scanInfo.RetentionTime, ionCount, massIntensityPairs);
                            }

                            if (mCheckCentroidingStatus)
                            {
                                var mzCount = massIntensityPairs.GetLength(1);

                                var mzList = new List<double>(mzCount);

                                for (var i = 0; i <= mzCount - 1; i++)
                                {
                                    mzList.Add(massIntensityPairs[0, i]);
                                }

                                var centroidingStatus = GetCentroidStatus(scanNumber, scanInfo);

                                mDatasetStatsSummarizer.ClassifySpectrum(mzList, scanInfo.MSLevel, centroidingStatus, "Scan " + scanNumber);
                            }
                        }

                    }

                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error loading m/z and intensity values for scan " + scanNumber + ": " + ex.Message);
                }

                scansProcessed++;

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < progressThresholdSeconds)
                    continue;

                lastProgressTime = DateTime.UtcNow;
                if (progressThresholdSeconds < 30)
                    progressThresholdSeconds += 2;

                var percentComplete = scansProcessed / (float)totalScansToProcess * 100;
                OnProgressUpdate(string.Format("Scans processed: {0:N0}", scansProcessed), percentComplete);

            }

            Console.WriteLine();

        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error or if the file has no scans</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            var dataFilePathLocal = string.Empty;

            // Obtain the full path to the file
            var rawFile = new FileInfo(dataFilePath);

            if (!rawFile.Exists)
            {
                OnErrorEvent(".Raw file not found: " + dataFilePath);
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // datasetID = LookupDatasetID(datasetName)
            var datasetID = DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = rawFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = rawFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = datasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(rawFile.Name);
            datasetFileInfo.FileExtension = rawFile.Extension;
            datasetFileInfo.FileSizeBytes = rawFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            var deleteLocalFile = false;
            var readError = false;

            // Note: as of June 2018 this only works if you disable "Prefer 32-bit" when compiling as AnyCPU

            // Use XRaw to read the .Raw file
            // If reading from a SAMBA-mounted network share, and if the current user has
            //  Read privileges but not Read&Execute privileges, we will need to copy the file locally

            var readerOptions = new ThermoReaderOptions {
                LoadMSMethodInfo = true,
                LoadMSTuneInfo = true
            };

            var xcaliburAccessor = new XRawFileIO(readerOptions);

            RegisterEvents(xcaliburAccessor);

            // Open a handle to the data file
            if (!xcaliburAccessor.OpenRawFile(rawFile.FullName))
            {
                // File open failed
                OnErrorEvent("Call to .OpenRawFile failed for: " + rawFile.FullName);
                ErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.ThermoRawFileReaderError;
                readError = true;

                if (!string.Equals(clsMSFileInfoScanner.GetAppDirectoryPath().Substring(0, 2), rawFile.FullName.Substring(0, 2), StringComparison.InvariantCultureIgnoreCase))
                {
                    if (mCopyFileLocalOnReadError)
                    {
                        // Copy the file locally and try again

                        try
                        {
                            dataFilePathLocal = Path.Combine(clsMSFileInfoScanner.GetAppDirectoryPath(), Path.GetFileName(dataFilePath));

                            if (!string.Equals(dataFilePathLocal, dataFilePath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                OnDebugEvent("Copying file " + Path.GetFileName(dataFilePath) + " to the working directory");
                                File.Copy(dataFilePath, dataFilePathLocal, true);

                                dataFilePath = string.Copy(dataFilePathLocal);
                                deleteLocalFile = true;

                                // Update rawFile then try to re-open
                                rawFile = new FileInfo(dataFilePath);

                                if (!xcaliburAccessor.OpenRawFile(rawFile.FullName))
                                {
                                    // File open failed
                                    OnErrorEvent("Call to .OpenRawFile failed for: " + rawFile.FullName);
                                    ErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.ThermoRawFileReaderError;
                                    readError = true;
                                }
                                else
                                {
                                    readError = false;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            readError = true;
                        }
                    }

                }

            }

            if (!readError)
            {
                // Read the file info
                try
                {
                    datasetFileInfo.AcqTimeStart = xcaliburAccessor.FileInfo.CreationDate;
                }
                catch (Exception)
                {
                    // Read error
                    readError = true;
                }

                if (!readError)
                {
                    try
                    {
                        datasetFileInfo.ScanCount = xcaliburAccessor.GetNumScans();

                        if (datasetFileInfo.ScanCount > 0)
                        {
                            // Look up the end scan time then compute .AcqTimeEnd
                            var scanEnd = xcaliburAccessor.FileInfo.ScanEnd;
                            xcaliburAccessor.GetScanInfo(scanEnd, out clsScanInfo scanInfo);

                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(scanInfo.RetentionTime);
                        }
                        else
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        }

                    }
                    catch (Exception)
                    {
                        // Error; use default values
                        datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        datasetFileInfo.ScanCount = 0;
                    }

                    if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile ||
                        mSaveLCMS2DPlots || mCheckCentroidingStatus || MS2MzMin > 0)
                    {
                        // Load data from each scan
                        // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                        LoadScanDetails(xcaliburAccessor);
                    }


                    if (mComputeOverallQualityScores)
                    {
                        // Note that this call will also create the TICs and BPIs
                        ComputeQualityScores(xcaliburAccessor, datasetFileInfo);
                    }

                    if (MS2MzMin > 0 && datasetFileInfo.ScanCount > 0)
                    {
                        // Verify that all of the MS2 spectra have m/z values below the required minimum
                        // Useful for validating that reporter ions can be detected
                        ValidateMS2MzMin();
                    }
                }
            }

            mDatasetStatsSummarizer.SampleInfo.SampleName = xcaliburAccessor.FileInfo.SampleName;
            mDatasetStatsSummarizer.SampleInfo.Comment1 = xcaliburAccessor.FileInfo.Comment1;
            mDatasetStatsSummarizer.SampleInfo.Comment2 = xcaliburAccessor.FileInfo.Comment2;

            if (!string.IsNullOrEmpty(xcaliburAccessor.FileInfo.SampleComment))
            {
                if (string.IsNullOrEmpty(mDatasetStatsSummarizer.SampleInfo.Comment1))
                {
                    mDatasetStatsSummarizer.SampleInfo.Comment1 = xcaliburAccessor.FileInfo.SampleComment;
                }
                else
                {
                    if (string.IsNullOrEmpty(mDatasetStatsSummarizer.SampleInfo.Comment2))
                    {
                        mDatasetStatsSummarizer.SampleInfo.Comment2 = xcaliburAccessor.FileInfo.SampleComment;
                    }
                    else
                    {
                        // Append the sample comment to comment 2
                        mDatasetStatsSummarizer.SampleInfo.Comment2 += "; " + xcaliburAccessor.FileInfo.SampleComment;
                    }
                }
            }

            // Close the handle to the data file
            xcaliburAccessor.CloseRawFile();

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the .Raw file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(rawFile, datasetID);

            // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

            // Delete the local copy of the data file
            if (deleteLocalFile)
            {
                try
                {
                    File.Delete(dataFilePathLocal);
                }
                catch (Exception)
                {
                    // Deletion failed
                    OnErrorEvent("Deletion failed for: " + Path.GetFileName(dataFilePathLocal));
                }
            }

            PostProcessTasks();

            return !readError;

        }

        private void StoreExtendedScanInfo(
            ExtendedStatsInfo extendedScanInfo,
            IReadOnlyCollection<KeyValuePair<string, string>> scanEvents)
        {
            var cTrimChars = new[] {
                ':',
                ' '
            };

            try
            {
                if (scanEvents == null || scanEvents.Count == 0)
                {
                    return;
                }

                foreach (var scanEvent in scanEvents)
                {
                    if (string.IsNullOrWhiteSpace(scanEvent.Key))
                    {
                        // Empty entry name; do not add
                        continue;
                    }

                    // We're only storing certain scan events
                    var entryNameLCase = scanEvent.Key.ToLower().TrimEnd(cTrimChars);

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_ION_INJECTION_TIME,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.IonInjectionTime = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_SEGMENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanSegment = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_EVENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanEvent = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_CHARGE_STATE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ChargeState = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_MONOISOTOPIC_MZ,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.MonoisotopicMZ = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_COLLISION_MODE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.CollisionMode = scanEvent.Value;
                        break;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_FILTER_TEXT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanFilterText = scanEvent.Value;
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore any errors here
            }

        }

    }
}
