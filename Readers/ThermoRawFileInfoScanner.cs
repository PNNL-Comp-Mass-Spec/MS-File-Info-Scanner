using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Plotting;
using MSFileInfoScannerInterfaces;
using PRISM;
using SpectraTypeClassifier;
using ThermoFisher.CommonCore.Data.Business;
using ThermoRawFileReader;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Thermo .raw file info scanner
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005
    /// </remarks>
    public class ThermoRawFileInfoScanner : MSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: centroided, xcalibur

        // Note: The extension must be in all caps
        public const string THERMO_RAW_FILE_EXTENSION = ".RAW";

        private readonly Regex mIsCentroid;
        private readonly Regex mIsProfileM;

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public ThermoRawFileInfoScanner() : this(new InfoScannerOptions(), new LCMSDataPlotterOptions())
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        public ThermoRawFileInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        {
            mIsCentroid = new Regex("([FI]TMS [+-] c .+)|([FI]TMS {[^ ]+} +[+-] c .+)|(^ *[+-] c .+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mIsProfileM = new Regex("([FI]TMS [+-] p .+)|([FI]TMS {[^ ]+} +[+-] p .+)|(^ *[+-] p .+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private void AddThermoDevices(
            XRawFileIO xcaliburAccessor,
            DatasetFileInfo datasetFileInfo,
            ICollection<Device> deviceMatchList,
            ICollection<Device> deviceSkipList)
        {
            // ReSharper disable once ForEachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var device in xcaliburAccessor.FileInfo.Devices)
            {
                if (deviceMatchList.Count > 0 && !deviceMatchList.Contains(device.Key))
                    continue;

                if (deviceSkipList.Count > 0 && deviceSkipList.Contains(device.Key))
                    continue;

                for (var deviceNumber = 1; deviceNumber <= device.Value; deviceNumber++)
                {
                    var deviceInfo = xcaliburAccessor.GetDeviceInfo(device.Key, deviceNumber);
                    datasetFileInfo.DeviceList.Add(deviceInfo);
                }
            }
        }

        /// <summary>
        /// This method is used to determine one or more overall quality scores
        /// </summary>
        /// <param name="xcaliburAccessor"></param>
        /// <param name="datasetFileInfo"></param>
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
                    // This method returns the number of points in massIntensityPairs()
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

                    overallAvgCount++;
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

        private SpectrumTypeClassifier.CentroidStatusConstants GetCentroidStatus(
            int scanNumber,
            clsScanInfo scanInfo)
        {
            if (scanInfo.IsCentroided)
            {
                if (mIsProfileM.IsMatch(scanInfo.FilterText))
                {
                    OnWarningEvent("Warning: Scan {0} appears to be profile mode data, yet XRawFileIO reported it to be centroid", scanNumber);
                }

                return SpectrumTypeClassifier.CentroidStatusConstants.Centroid;
            }

            if (mIsCentroid.IsMatch(scanInfo.FilterText))
            {
                OnWarningEvent("Warning: Scan {0} appears to be centroided data, yet XRawFileIO reported it to be profile", scanNumber);
            }

            return SpectrumTypeClassifier.CentroidStatusConstants.Profile;
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath"></param>
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

            if (Options.SaveTICAndBPIPlots)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (Options.SaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            var lastProgressTime = DateTime.UtcNow;
            var startTime = DateTime.UtcNow;

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
                        if (Options.SaveTICAndBPIPlots)
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
                            ScanTypeName = XRawFileIO.GetScanTypeNameFromThermoScanFilterText(scanInfo.FilterText),
                            ScanFilterText = XRawFileIO.MakeGenericThermoScanFilter(scanInfo.FilterText),
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
                    OnErrorEvent("Error loading header info for scan {0}: {1}", scanNumber, ex.Message);
                    continue;
                }

                try
                {
                    if (Options.SaveLCMS2DPlots || Options.CheckCentroidingStatus)
                    {
                        // Also need to load the raw data

                        // Load the ions for this scan
                        var ionCount = xcaliburAccessor.GetScanData2D(scanNumber, out var massIntensityPairs);

                        if (ionCount > 0)
                        {
                            if (Options.SaveLCMS2DPlots)
                            {
                                mLCMS2DPlot.AddScan2D(scanNumber, scanInfo.MSLevel, (float)scanInfo.RetentionTime, ionCount, massIntensityPairs);
                            }

                            if (Options.CheckCentroidingStatus)
                            {
                                var mzCount = massIntensityPairs.GetLength(1);

                                var mzList = new List<double>(mzCount);

                                for (var i = 0; i < mzCount; i++)
                                {
                                    if (massIntensityPairs[0, i] < LCMSDataPlotter.TINY_MZ_THRESHOLD ||
                                        massIntensityPairs[0, i] >= LCMSDataPlotter.HUGE_MZ_THRESHOLD)
                                    {
                                        // The m/z value is too small or too large, indicating a corrupt scan; skip this m/z value
                                        continue;
                                    }
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
                    OnErrorEvent("Error loading m/z and intensity values for scan {0}: {1}", scanNumber, ex.Message);
                }

                scansProcessed++;

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < progressThresholdSeconds)
                    continue;

                lastProgressTime = DateTime.UtcNow;
                if (progressThresholdSeconds < 30)
                    progressThresholdSeconds += 2;

                var percentComplete = scansProcessed / (float)totalScansToProcess * 100;

                var elapsedSeconds = DateTime.UtcNow.Subtract(startTime).Seconds;

                int scansPerMinute;
                if (elapsedSeconds > 2)
                    scansPerMinute = (int)Math.Round(scansProcessed / (double)elapsedSeconds, 0);
                else
                    scansPerMinute = 0;

                OnProgressUpdate(string.Format("Spectra processed: {0:N0} ({1:N0} scans/second)", scansProcessed, scansPerMinute), percentComplete);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error or if the file has no scans</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            var dataFilePathLocal = string.Empty;

            // Obtain the full path to the file
            var rawFile = MSFileInfoScanner.GetFileInfo(dataFilePath);

            if (!rawFile.Exists)
            {
                OnErrorEvent(".Raw file not found: {0}", dataFilePath);
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // datasetID = LookupDatasetID(datasetName)
            var datasetID = Options.DatasetID;

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

            var readerOptions = new ThermoReaderOptions
            {
                LoadMSMethodInfo = true,
                LoadMSTuneInfo = true
            };

            var xcaliburAccessor = new XRawFileIO(readerOptions);

            RegisterEvents(xcaliburAccessor);

            // Open a handle to the data file
            if (!xcaliburAccessor.OpenRawFile(rawFile.FullName))
            {
                // File open failed
                OnErrorEvent("Call to .OpenRawFile failed for: {0}", rawFile.FullName);
                ErrorCode = iMSFileInfoScanner.MSFileScannerErrorCodes.ThermoRawFileReaderError;
                readError = true;

                if (!string.Equals(MSFileInfoScanner.GetAppDirectoryPath().Substring(0, 2), rawFile.FullName.Substring(0, 2), StringComparison.InvariantCultureIgnoreCase))
                {
                    if (Options.CopyFileLocalOnReadError)
                    {
                        // Copy the file locally and try again

                        try
                        {
                            dataFilePathLocal = Path.Combine(MSFileInfoScanner.GetAppDirectoryPath(), Path.GetFileName(dataFilePath));

                            if (!string.Equals(dataFilePathLocal, dataFilePath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                OnDebugEvent("Copying file {0} to the working directory", Path.GetFileName(dataFilePath));

                                File.Copy(dataFilePath, dataFilePathLocal, true);

                                dataFilePath = string.Copy(dataFilePathLocal);
                                deleteLocalFile = true;

                                // Update rawFile then try to re-open
                                rawFile = MSFileInfoScanner.GetFileInfo(dataFilePath);

                                if (!xcaliburAccessor.OpenRawFile(rawFile.FullName))
                                {
                                    // File open failed
                                    OnErrorEvent("Call to .OpenRawFile failed for: {0}", rawFile.FullName);
                                    ErrorCode = iMSFileInfoScanner.MSFileScannerErrorCodes.ThermoRawFileReaderError;
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
                            xcaliburAccessor.GetScanInfo(scanEnd, out var scanInfo);

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

                    if (Options.SaveTICAndBPIPlots || Options.CreateDatasetInfoFile || Options.CreateScanStatsFile ||
                        Options.SaveLCMS2DPlots || Options.CheckCentroidingStatus || Options.MS2MzMin > 0)
                    {
                        // Load data from each scan
                        // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                        LoadScanDetails(xcaliburAccessor);
                    }

                    if (Options.ComputeOverallQualityScores)
                    {
                        // Note that this call will also create the TICs and BPIs
                        ComputeQualityScores(xcaliburAccessor, datasetFileInfo);
                    }

                    if (Options.MS2MzMin > 0 && datasetFileInfo.ScanCount > 0)
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

            // Add the devices
            var deviceFilterList = new SortedSet<Device> {
                Device.MS,
                Device.MSAnalog
            };

            // First add the MS devices
            AddThermoDevices(xcaliburAccessor, datasetFileInfo, deviceFilterList, new SortedSet<Device>());

            // Now add any non-mass spec devices
            var deviceSkipList = new SortedSet<Device> {
                Device.MS,
                Device.MSAnalog,
                Device.None             // Skip devices of type "None"; for example, see dataset 20200122_rmi049_SPZ_01_FAIMS_m40_CD
            };

            AddThermoDevices(xcaliburAccessor, datasetFileInfo, new SortedSet<Device>(), deviceSkipList);

            if (Options.SaveTICAndBPIPlots && datasetFileInfo.DeviceList.Count > 0)
            {
                mInstrumentSpecificPlots.Clear();

                foreach (var device in datasetFileInfo.DeviceList)
                {
                    if (device.DeviceType == Device.MS || device.DeviceType == Device.MSAnalog)
                        continue;

                    // Note: This method calls GetChromatogramData2D
                    var chromatogramData = xcaliburAccessor.GetChromatogramData(device.DeviceType, device.DeviceNumber);

                    if (chromatogramData.Count == 0)
                        continue;

                    var devicePlot = AddInstrumentSpecificPlot(device.DeviceDescription);

                    devicePlot.TICXAxisLabel = string.IsNullOrWhiteSpace(device.AxisLabelX) ? "Scan number" : device.AxisLabelX;
                    devicePlot.TICYAxisLabel = device.YAxisLabelWithUnits;

                    devicePlot.TICYAxisExponentialNotation = false;

                    devicePlot.DeviceType = device.DeviceType;
                    devicePlot.TICPlotAbbrev = string.Format("{0}{1}", device.DeviceType.ToString(), device.DeviceNumber);
                    devicePlot.TICAutoMinMaxY = true;
                    devicePlot.RemoveZeroesFromEnds = false;

                    float acqLengthMinutes;
                    if (datasetFileInfo.AcqTimeEnd > datasetFileInfo.AcqTimeStart)
                        acqLengthMinutes = (float)datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes;
                    else
                        acqLengthMinutes = chromatogramData.Count;

                    var dataCount = chromatogramData.Count;

                    foreach (var dataPoint in chromatogramData)
                    {
                        var scanNumber = dataPoint.Key;
                        float scanTimeMinutes;
                        if (acqLengthMinutes > 0)
                            scanTimeMinutes = scanNumber / (float)dataCount * acqLengthMinutes;
                        else
                            scanTimeMinutes = scanNumber;

                        devicePlot.AddDataTICOnly(scanNumber, 1, scanTimeMinutes, dataPoint.Value);
                    }
                }
            }

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
                    OnErrorEvent("Deletion failed for: {0}", Path.GetFileName(dataFilePathLocal));
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
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_SEGMENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanSegment = scanEvent.Value;
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_EVENT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanEvent = scanEvent.Value;
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_CHARGE_STATE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ChargeState = scanEvent.Value;
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_MONOISOTOPIC_MZ,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.MonoisotopicMZ = scanEvent.Value;
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_COLLISION_MODE,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.CollisionMode = scanEvent.Value;
                        continue;
                    }

                    if (string.Equals(entryNameLCase, ScanStatsEntry.SCAN_STATS_COL_SCAN_FILTER_TEXT,
                                      StringComparison.InvariantCultureIgnoreCase))
                    {
                        extendedScanInfo.ScanFilterText = scanEvent.Value;
                        continue;
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
