using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using MSFileInfoScanner.DatasetStats;
using PRISM;
using pwiz.ProteowizardWrapper;

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsProteoWizardDataParser : EventNotifier
    {
        private const int PROGRESS_START = 0;
        private const int PROGRESS_SCAN_TIMES_LOADED = 10;

        private readonly MSDataFileReader mPWiz;

        private readonly DatasetStatsSummarizer mDatasetStatsSummarizer;
        private readonly clsTICandBPIPlotter mTICAndBPIPlot;

        private readonly clsLCMSDataPlotter mLCMS2DPlot;
        private readonly bool mSaveLCMS2DPlots;
        private readonly bool mSaveTICAndBPI;

        private readonly bool mCheckCentroidingStatus;

        private readonly Regex mGetQ1MZ;
        private readonly Regex mGetQ3MZ;

        private CancellationTokenSource mCancellationToken;
        private DateTime mGetScanTimesStartTime;
        private int mGetScanTimesMaxWaitTimeSeconds;
        private bool mGetScanTimesAutoAborted;

        private DateTime mLastScanLoadingDebugProgressTime;
        private DateTime mLastScanLoadingStatusProgressTime;
        private bool mReportedTotalSpectraToExamine;

        private bool mWarnedAccessViolationException;

        public bool HighResMS1 { get; set; }

        public bool HighResMS2 { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pWiz"></param>
        /// <param name="datasetStatsSummarizer"></param>
        /// <param name="ticAndBPIPlot"></param>
        /// <param name="lcms2DPlot"></param>
        /// <param name="saveLCMS2DPlots"></param>
        /// <param name="saveTICAndBPI"></param>
        /// <param name="checkCentroidingStatus"></param>
        public clsProteoWizardDataParser(
            MSDataFileReader pWiz,
            DatasetStatsSummarizer datasetStatsSummarizer,
            clsTICandBPIPlotter ticAndBPIPlot,
            clsLCMSDataPlotter lcms2DPlot,
            bool saveLCMS2DPlots,
            bool saveTICAndBPI,
            bool checkCentroidingStatus)
        {
            mPWiz = pWiz;
            mDatasetStatsSummarizer = datasetStatsSummarizer;
            mTICAndBPIPlot = ticAndBPIPlot;
            mLCMS2DPlot = lcms2DPlot;

            mSaveLCMS2DPlots = saveLCMS2DPlots;
            mSaveTICAndBPI = saveTICAndBPI;
            mCheckCentroidingStatus = checkCentroidingStatus;

            mGetQ1MZ = new Regex("Q[0-9]=([0-9.]+)", RegexOptions.Compiled);

            mGetQ3MZ = new Regex("Q1=[0-9.]+ Q3=([0-9.]+)", RegexOptions.Compiled);

            mWarnedAccessViolationException = false;
        }

        private bool ExtractQ1MZ(string chromatogramID, out double mz)
        {
            return ExtractQMZ(mGetQ1MZ, chromatogramID, out mz);
        }

        private bool ExtractQ3MZ(string chromatogramID, out double mz)
        {
            return ExtractQMZ(mGetQ3MZ, chromatogramID, out mz);
        }

        private bool ExtractQMZ(Regex reGetMZ, string chromatogramID, out double mz)
        {
            var reMatch = reGetMZ.Match(chromatogramID);
            if (reMatch.Success)
            {
                if (double.TryParse(reMatch.Groups[1].Value, out mz))
                {
                    return true;
                }
            }

            mz = 0;
            return false;
        }

        private int FindNearestInList(List<float> items, float valToFind)
        {
            var indexMatch = items.BinarySearch(valToFind);
            if (indexMatch >= 0)
            {
                // Exact match found
            }
            else
            {
                // Find the nearest match
                indexMatch ^= -1;
                if (indexMatch == items.Count)
                {
                    indexMatch--;
                }

                if (indexMatch > 0)
                {
                    // Possibly decrement indexMatch
                    if (Math.Abs(items[indexMatch - 1] - valToFind) < Math.Abs(items[indexMatch] - valToFind))
                    {
                        indexMatch--;
                    }
                }

                if (indexMatch < items.Count)
                {
                    // Possible increment indexMatch
                    if (Math.Abs(items[indexMatch + 1] - valToFind) < Math.Abs(items[indexMatch] - valToFind))
                    {
                        indexMatch++;
                    }
                }

                if (indexMatch < 0)
                {
                    indexMatch = 0;
                }
                else if (indexMatch == items.Count)
                {
                    indexMatch = items.Count - 1;
                }

            }

            return indexMatch;
        }

        private int GetSpectrumCountWithRetry(MSDataFileReader pWiz, int maxAttempts = 3)
        {
            var attemptCount = 0;
            while (attemptCount < maxAttempts)
            {
                try
                {
                    attemptCount++;
                    var spectrumCount = pWiz.SpectrumCount;

                    return spectrumCount;
                }
                catch (Exception ex)
                {
                    OnDebugEvent(string.Format("Attempt {0} to retrieve .SpectrumCount failed: {1}", attemptCount, ex.Message));
                    Thread.Sleep(50 + attemptCount * 250);
                }
            }

            return 0;
        }

        private void MonitorScanTimeLoadingProgress(int scansLoaded, int totalScans)
        {

            if (DateTime.UtcNow.Subtract(mLastScanLoadingDebugProgressTime).TotalSeconds < 30)
                return;

            // If the call to mPWiz.GetScanTimesAndMsLevels() takes too long,
            // abort the process and instead get the ScanTimes and MSLevels via mPWiz.GetSpectrum()
            if (mGetScanTimesMaxWaitTimeSeconds > 0 &&
                DateTime.UtcNow.Subtract(mGetScanTimesStartTime).TotalSeconds >= mGetScanTimesMaxWaitTimeSeconds)
            {
                mGetScanTimesAutoAborted = true;
                mCancellationToken.Cancel();
            }

            mLastScanLoadingDebugProgressTime = DateTime.UtcNow;

            if (totalScans == 0)
                return;

            if (!mReportedTotalSpectraToExamine)
            {
                OnStatusEvent(string.Format(" ... {0:N0} total spectra to examine", totalScans));
                mReportedTotalSpectraToExamine = true;
            }

            var percentComplete = scansLoaded / (float)totalScans * 100;

            if (DateTime.UtcNow.Subtract(mLastScanLoadingStatusProgressTime).TotalMinutes > 5)
            {
                OnStatusEvent(string.Format("Obtaining scan times and MSLevels, examined {0:N0} / {1:N0} spectra", scansLoaded, totalScans));
                mLastScanLoadingStatusProgressTime = DateTime.UtcNow;
                return;
            }

            var percentCompleteOverall = clsMSFileInfoProcessorBaseClass.ComputeIncrementalProgress(
                PROGRESS_START,
                PROGRESS_SCAN_TIMES_LOADED,
                percentComplete);

            OnProgressUpdate(string.Format("Spectra examined: {0:N0}", scansLoaded), percentCompleteOverall);
        }

        public void PossiblyUpdateAcqTimeStart(DatasetFileInfo datasetFileInfo, double runtimeMinutes)
        {
            if (runtimeMinutes > 0)
            {
                var acqTimeStartAlt = datasetFileInfo.AcqTimeEnd.AddMinutes(-runtimeMinutes);

                if (acqTimeStartAlt < datasetFileInfo.AcqTimeStart && datasetFileInfo.AcqTimeStart.Subtract(acqTimeStartAlt).TotalDays < 1)
                {
                    datasetFileInfo.AcqTimeStart = acqTimeStartAlt;
                }
            }
        }

        private void ProcessSRM(
            string chromatogramID,
            float[] scanTimes,
            float[] intensities,
            List<float> ticScanTimes,
            IReadOnlyList<int> ticScanNumbers,
            ref double runtimeMinutes,
            IDictionary<int, Dictionary<double, double>> dct2DDataParent,
            IDictionary<int, Dictionary<double, double>> dct2DDataProduct,
            IDictionary<int, float> dct2DDataScanTimes)
        {

            // Attempt to parse out the product m/z
            var parentMZFound = ExtractQ1MZ(chromatogramID, out var parentMZ);
            var productMZFound = ExtractQ3MZ(chromatogramID, out var productMZ);
            var spectrumCount = scanTimes.Length;

            for (var index = 0; index <= spectrumCount - 1; index++)
            {
                // Find the ScanNumber in the TIC nearest to scanTimes[index]
                var indexMatch = FindNearestInList(ticScanTimes, scanTimes[index]);
                var scanNumber = ticScanNumbers[indexMatch];

                // Bump up runtimeMinutes if necessary
                if (scanTimes[index] > runtimeMinutes)
                {
                    runtimeMinutes = scanTimes[index];
                }

                var scanStatsEntry = new ScanStatsEntry
                {
                    ScanNumber = scanNumber,
                    ScanType = 1,
                    ScanTypeName = "SRM",
                    ScanFilterText = StripExtraFromChromatogramID(chromatogramID),
                    ElutionTime = scanTimes[index].ToString("0.0###"),
                    TotalIonIntensity = intensities[index].ToString("0.0"),
                    BasePeakIntensity = intensities[index].ToString("0.0")
                };

                if (parentMZFound)
                {
                    scanStatsEntry.BasePeakMZ = parentMZ.ToString("0.0###");
                }
                else if (productMZFound)
                {
                    scanStatsEntry.BasePeakMZ = productMZ.ToString("0.0###");
                }
                else
                {
                    scanStatsEntry.BasePeakMZ = "0";
                }

                // Base peak signal to noise ratio
                scanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                scanStatsEntry.IonCount = 1;
                scanStatsEntry.IonCountRaw = 1;

                mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);

                if (mSaveLCMS2DPlots && intensities[index] > 0)
                {
                    // Store the m/z and intensity values in dct2DDataParent and dct2DDataProduct

                    if (parentMZFound)
                    {
                        Store2DPlotDataPoint(dct2DDataParent, scanNumber, parentMZ, intensities[index]);
                    }

                    if (productMZFound)
                    {
                        Store2DPlotDataPoint(dct2DDataProduct, scanNumber, productMZ, intensities[index]);
                    }

                    if (!dct2DDataScanTimes.ContainsKey(scanNumber))
                    {
                        dct2DDataScanTimes[scanNumber] = scanTimes[index];
                    }

                }

            }

        }

        private void ProcessTIC(
            IReadOnlyList<float> scanTimes,
            IReadOnlyList<float> intensities,
            List<float> ticScanTimes,
            List<int> ticScanNumbers,
            ref double runtimeMinutes,
            bool storeInTICAndBPIPlot)
        {
            for (var scanIndex = 0; scanIndex <= scanTimes.Count - 1; scanIndex++)
            {
                ticScanTimes.Add(scanTimes[scanIndex]);

                var scanNumber = scanIndex + 1;
                ticScanNumbers.Add(scanNumber);

                // Bump up runtimeMinutes if necessary
                if (scanTimes[scanIndex] > runtimeMinutes)
                {
                    runtimeMinutes = scanTimes[scanIndex];
                }

                if (storeInTICAndBPIPlot)
                {
                    // Use this TIC chromatogram for this dataset since there are no normal Mass Spectra
                    mTICAndBPIPlot.AddDataTICOnly(scanNumber, 1, scanTimes[scanIndex], intensities[scanIndex]);
                }

            }

            // Make sure ticScanTimes is sorted
            var needToSort = false;
            for (var index = 1; index <= ticScanTimes.Count - 1; index++)
            {
                if (ticScanTimes[index] < ticScanTimes[index - 1])
                {
                    needToSort = true;
                    break;
                }
            }

            if (needToSort)
            {
                var ticScanTimesArray = new float[ticScanTimes.Count];
                var ticScanNumbersArray = new int[ticScanTimes.Count];

                ticScanTimes.CopyTo(ticScanTimesArray);
                ticScanNumbers.CopyTo(ticScanNumbersArray);

                Array.Sort(ticScanTimesArray, ticScanNumbersArray);

                ticScanTimes.Clear();
                ticScanNumbers.Clear();

                for (var index = 0; index <= ticScanTimesArray.Length - 1; index++)
                {
                    ticScanTimes.Add(ticScanTimesArray[index]);
                    ticScanNumbers.Add(ticScanNumbersArray[index]);
                }

            }

        }

        private bool ShowPeriodicMessageNow(int currentCount)
        {
            return
                currentCount < 25 ||
                currentCount < 100 && currentCount % 10 == 0 ||
                currentCount < 1000 && currentCount % 100 == 0 ||
                currentCount < 10000 && currentCount % 1000 == 0 ||
                currentCount < 100000 && currentCount % 10000 == 0 ||
                currentCount % 100000 == 0;
        }

        [HandleProcessCorruptedStateExceptions]
        public void StoreChromatogramInfo(DatasetFileInfo datasetFileInfo, out bool ticStored, out bool srmDataCached, out double runtimeMinutes)
        {
            var ticScanTimes = new List<float>();
            var ticScanNumbers = new List<int>();

            // This dictionary tracks the m/z and intensity values for parent (Q1) ions of each scan
            // Key is ScanNumber; Value is a dictionary holding m/z and intensity values for that scan
            var dct2DDataParent = new Dictionary<int, Dictionary<double, double>>();

            // This dictionary tracks the m/z and intensity values for product (Q3) ions of each scan
            var dct2DDataProduct = new Dictionary<int, Dictionary<double, double>>();

            // This dictionary tracks the scan times for each scan number tracked by dct2DDataParent and/or dct2DDataProduct
            var dct2DDataScanTimes = new Dictionary<int, float>();

            // Note that even for a small .Wiff file (1.5 MB), obtaining the first chromatogram will take some time (20 to 60 seconds)
            // The chromatogram at index 0 should be the TIC
            // The chromatogram at index >=1 will be each SRM

            runtimeMinutes = 0;
            ticStored = false;
            srmDataCached = false;

            for (var chromatogramIndex = 0; chromatogramIndex <= mPWiz.ChromatogramCount - 1; chromatogramIndex++)
            {
                try
                {
                    if (chromatogramIndex == 0)
                    {
                        OnStatusEvent("Obtaining chromatograms (this could take as long as 60 seconds)");
                    }

                    mPWiz.GetChromatogram(chromatogramIndex, out var chromatogramID, out var scanTimes, out var intensities);

                    if (chromatogramID == null)
                        chromatogramID = string.Empty;

                    var cvParams = mPWiz.GetChromatogramCVParams(chromatogramIndex);

                    if (MSDataFileReader.TryGetCVParam(cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, out _))
                    {
                        // This chromatogram is the TIC

                        var spectrumCount = GetSpectrumCountWithRetry(mPWiz);

                        var storeInTICAndBPIPlot = (mSaveTICAndBPI && spectrumCount == 0);

                        ProcessTIC(scanTimes, intensities, ticScanTimes, ticScanNumbers, ref runtimeMinutes, storeInTICAndBPIPlot);

                        ticStored = storeInTICAndBPIPlot;

                        // This is FrameCount for Bruker timsTOF datasets
                        datasetFileInfo.ScanCount = scanTimes.Length;
                    }

                    if (MSDataFileReader.TryGetCVParam(cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, out _))
                    {
                        // This chromatogram is an SRM scan

                        ProcessSRM(chromatogramID, scanTimes, intensities, ticScanTimes, ticScanNumbers, ref runtimeMinutes, dct2DDataParent,
                                   dct2DDataProduct, dct2DDataScanTimes);

                        srmDataCached = true;
                    }

                }
                catch (AccessViolationException)
                {
                    // Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
                    if (!mWarnedAccessViolationException)
                    {
                        OnWarningEvent("Error loading chromatogram data with ProteoWizard: Attempted to read or write protected memory. " +
                                       "The instrument data file is likely corrupt.");
                        mWarnedAccessViolationException = true;
                    }
                    mDatasetStatsSummarizer.CreateEmptyScanStatsFiles = false;
                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error processing chromatogram " + chromatogramIndex + " with ProteoWizard: " + ex.Message, ex);
                }

            }

            if (!mSaveLCMS2DPlots)
            {
                return;
            }

            if (dct2DDataParent.Count <= 0 && dct2DDataProduct.Count <= 0)
            {
                return;
            }

            // Now that all of the chromatograms have been processed, transfer data from dct2DDataParent and dct2DDataProduct into mLCMS2DPlot
            mLCMS2DPlot.Options.MS1PlotTitle = "Q1 m/z";
            mLCMS2DPlot.Options.MS2PlotTitle = "Q3 m/z";

            Store2DPlotData(dct2DDataScanTimes, dct2DDataParent, dct2DDataProduct);
        }

        /// <summary>
        /// Read the spectra from the data file
        /// </summary>
        /// <param name="ticStored">The calling method should set this to true if the TIC was already stored</param>
        /// <param name="runtimeMinutes">Maximum acquisition time (updated by this method)</param>
        /// <param name="skipExistingScans">When true, skip scans already defined in mDatasetStatsSummarizer</param>
        /// <param name="skipScansWithNoIons">When true, skip scans that have no ions</param>
        /// <param name="maxScansToTrackInDetail">
        /// Maximum number of scans to store in mDatasetStatsSummarizer; limit to 1 million to reduce memory usage.
        /// If less than zero, store all scans
        /// </param>
        /// <param name="maxScansForTicAndBpi">
        /// Maximum number of scans to store in mTICAndBPIPlot; limit to 2 million to reduce memory usage.
        /// If less than zero, store all scans
        /// </param>
        /// <returns>True if at least 50% of the spectra were successfully read</returns>
        public bool StoreMSSpectraInfo(
            bool ticStored,
            ref double runtimeMinutes,
            bool skipExistingScans,
            bool skipScansWithNoIons,
            int maxScansToTrackInDetail,
            int maxScansForTicAndBpi)
        {
            var parserInfo = new ProteoWizardParserInfo(runtimeMinutes)
            {
                TicStored = ticStored,
                SkipExistingScans = skipExistingScans,
                SkipScansWithNoIons = skipScansWithNoIons,
                MaxScansToTrackInDetail = maxScansToTrackInDetail,
                MaxScansForTicAndBpi = maxScansForTicAndBpi
            };

            return StoreMSSpectraInfo(parserInfo);
        }

        /// <summary>
        /// Read the spectra from the data file
        /// </summary>
        /// <param name="parserInfo">ProteoWizard parser tracking variables</param>
        /// <returns>True if at least 50% of the spectra were successfully read</returns>
        [HandleProcessCorruptedStateExceptions]
        public bool StoreMSSpectraInfo(ProteoWizardParserInfo parserInfo)
        {
            parserInfo.ScanCountSuccess = 0;
            parserInfo.ScanCountError = 0;

            try
            {
                Console.WriteLine();
                OnStatusEvent("Obtaining scan times and MSLevels (this could take several minutes)");
                mLastScanLoadingDebugProgressTime = DateTime.UtcNow;
                mLastScanLoadingStatusProgressTime = DateTime.UtcNow;
                mReportedTotalSpectraToExamine = false;

                mCancellationToken = new CancellationTokenSource();

                var scanTimes = new double[0];
                var msLevels = new byte[0];

                mGetScanTimesStartTime = DateTime.UtcNow;
                mGetScanTimesMaxWaitTimeSeconds = 90;
                mGetScanTimesAutoAborted = false;

                parserInfo.MinScanIndexWithoutScanTimes = int.MaxValue;

                var attemptNumber = 1;
                while (true)
                {
                    var useAlternateMethod = (attemptNumber > 1);

                    try
                    {

                        mPWiz.GetScanTimesAndMsLevels(mCancellationToken.Token,
                            out scanTimes, out msLevels, MonitorScanTimeLoadingProgress, useAlternateMethod);

                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // mCancellationToken.Cancel was called in MonitorScanTimeLoadingProgress

                        // Determine the scan index where GetScanTimesAndMsLevels exited the for loop
                        for (var scanIndex = 0; scanIndex <= scanTimes.Length - 1; scanIndex++)
                        {
                            if (msLevels[scanIndex] > 0) continue;

                            parserInfo.MinScanIndexWithoutScanTimes = scanIndex;
                            break;
                        }

                        if (!mGetScanTimesAutoAborted && parserInfo.MinScanIndexWithoutScanTimes < int.MaxValue)
                        {
                            // Manually aborted; shrink the arrays to reflect the amount of data that was actually loaded
                            Array.Resize(ref scanTimes, parserInfo.MinScanIndexWithoutScanTimes);
                            Array.Resize(ref msLevels, parserInfo.MinScanIndexWithoutScanTimes);
                        }

                        break;
                    }
                    catch (Exception ex)
                    {
                        var baseMessage = "Exception calling mPWiz.GetScanTimesAndMsLevels";
                        var alternateMethodFlag = useAlternateMethod ? " (useAlternateMethod = true)" : string.Empty;

                        OnWarningEvent(string.Format("{0}{1}: {2}", baseMessage, alternateMethodFlag, ex.Message));
                        attemptNumber++;

                        if (attemptNumber > 2)
                            throw new Exception(baseMessage, ex);
                    }
                }

                StoreMSSpectraInfoForScans(scanTimes, msLevels, parserInfo);

                var scanCountTotal = parserInfo.ScanCountSuccess + parserInfo.ScanCountError;
                if (scanCountTotal == 0)
                    return false;

                // Return True if at least 50% of the spectra were successfully read
                return parserInfo.ScanCountSuccess >= scanCountTotal / 2.0;

            }
            catch (AccessViolationException)
            {
                // Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
                if (!mWarnedAccessViolationException)
                {
                    OnWarningEvent("Error reading instrument data with ProteoWizard: Attempted to read or write protected memory. " +
                                   "The instrument data file is likely corrupt.");
                    mWarnedAccessViolationException = true;
                }
                mDatasetStatsSummarizer.CreateEmptyScanStatsFiles = false;
                return false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error reading instrument data with ProteoWizard: " + ex.Message, ex);
                return false;
            }

        }

        /// <summary>
        /// Read the spectra from the data file
        /// </summary>
        /// <param name="scanTimes">Array of scan times</param>
        /// <param name="msLevels">List of msLevels</param>
        /// <param name="parserInfo">ProteoWizard parser tracking variables</param>
        private void StoreMSSpectraInfoForScans(
            double[] scanTimes,
            IList<byte> msLevels,
            ProteoWizardParserInfo parserInfo)
        {
            var spectrumCount = scanTimes.Length;

            parserInfo.ResetCounts();

            // The scan times returned by .GetScanTimesAndMsLevels() are the acquisition time in seconds from the start of the analysis
            // Convert these to minutes
            for (var scanIndex = 0; scanIndex <= spectrumCount - 1; scanIndex++)
            {
                if (scanIndex >= parserInfo.MinScanIndexWithoutScanTimes)
                    break;

                var scanTimeMinutes = scanTimes[scanIndex] / 60.0;
                scanTimes[scanIndex] = scanTimeMinutes;
            }

            Console.WriteLine();
            OnStatusEvent("Reading spectra");
            var lastDebugProgressTime = DateTime.UtcNow;
            var lastStatusProgressTime = DateTime.UtcNow;

            for (var scanIndex = 0; scanIndex <= spectrumCount - 1; scanIndex++)
            {
                var scanNumber = scanIndex + 1;

                StoreSingleSpectrum(scanTimes, msLevels, parserInfo, scanIndex);

                if (DateTime.UtcNow.Subtract(lastStatusProgressTime).TotalMinutes > 5)
                {
                    OnStatusEvent(string.Format("Reading spectra, loaded {0:N0} / {1:N0} spectra; " +
                                                "{2:N0} HMS spectra; {3:N0} HMSn spectra; " +
                                                "{4:N0} MS spectra; {5:N0} MSn spectra; " +
                                                "max elution time is {6:F2} minutes",
                        scanNumber, spectrumCount,
                        parserInfo.ScanCountHMS, parserInfo.ScanCountHMSn,
                        parserInfo.ScanCountMS, parserInfo.ScanCountMSn,
                        parserInfo.RuntimeMinutes));

                    lastStatusProgressTime = DateTime.UtcNow;
                    lastDebugProgressTime = DateTime.UtcNow;
                    continue;
                }

                if (DateTime.UtcNow.Subtract(lastDebugProgressTime).TotalSeconds < 15)
                    continue;

                lastDebugProgressTime = DateTime.UtcNow;
                var percentComplete = scanNumber / (float)spectrumCount * 100;

                var percentCompleteOverall = clsMSFileInfoProcessorBaseClass.ComputeIncrementalProgress(
                    PROGRESS_SCAN_TIMES_LOADED,
                    clsMSFileInfoProcessorBaseClass.PROGRESS_SPECTRA_LOADED,
                    percentComplete);

                OnProgressUpdate(string.Format("Spectra processed: {0:N0}", scanNumber), percentCompleteOverall);
            }


            mDatasetStatsSummarizer.StoreScanTypeTotals(
                parserInfo.ScanCountHMS, parserInfo.ScanCountHMSn,
                parserInfo.ScanCountMS, parserInfo.ScanCountMSn,
                parserInfo.RuntimeMinutes);

        }

        private void StoreSingleSpectrum(
            double[] scanTimes,
            IList<byte> msLevels,
            ProteoWizardParserInfo parserInfo,
            int scanIndex)
        {

            var scanNumber = scanIndex + 1;

            try
            {
                var computeTIC = true;
                var computeBPI = true;

                // Obtain the raw mass spectrum
                var msDataSpectrum = mPWiz.GetSpectrum(scanIndex);

                if (scanIndex >= parserInfo.MinScanIndexWithoutScanTimes)
                {
                    // msDataSpectrum.RetentionTime is already in minutes
                    scanTimes[scanIndex] = msDataSpectrum.RetentionTime ?? 0;

                    if (msDataSpectrum.Level >= byte.MinValue && msDataSpectrum.Level <= byte.MaxValue)
                    {
                        msLevels[scanIndex] = (byte)msDataSpectrum.Level;
                    }
                }

                var scanStatsEntry = new ScanStatsEntry
                {
                    ScanNumber = scanNumber,
                    ScanType = msDataSpectrum.Level
                };

                if (msLevels[scanIndex] > 1)
                {
                    if (HighResMS2)
                    {
                        scanStatsEntry.ScanTypeName = "HMSn";
                        parserInfo.ScanCountHMSn++;
                    }
                    else
                    {
                        scanStatsEntry.ScanTypeName = "MSn";
                        parserInfo.ScanCountMSn++;
                    }
                }
                else
                {
                    if (HighResMS1)
                    {
                        scanStatsEntry.ScanTypeName = "HMS";
                        parserInfo.ScanCountHMS++;
                    }
                    else
                    {
                        scanStatsEntry.ScanTypeName = "MS";
                        parserInfo.ScanCountMS++;
                    }
                }

                var driftTimeMsec = msDataSpectrum.DriftTimeMsec ?? 0;

                scanStatsEntry.ScanFilterText = driftTimeMsec > 0 ? "IMS" : string.Empty;
                scanStatsEntry.ExtendedScanInfo.ScanFilterText = scanStatsEntry.ScanFilterText;

                scanStatsEntry.DriftTimeMsec = driftTimeMsec.ToString("0.0###");

                scanStatsEntry.ElutionTime = scanTimes[scanIndex].ToString("0.0###");

                // Bump up runtimeMinutes if necessary
                if (scanTimes[scanIndex] > parserInfo.RuntimeMinutes)
                {
                    parserInfo.RuntimeMinutes = scanTimes[scanIndex];
                }

                var spectrum = mPWiz.GetSpectrumObject(scanIndex);

                if (MSDataFileReader.TryGetCVParamDouble(spectrum.cvParams, pwiz.CLI.cv.CVID.MS_total_ion_current, out var tic))
                {
                    // For timsTOF data, this is the TIC of the entire frame
                    scanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(tic, 5);
                    computeTIC = false;
                }

                if (MSDataFileReader.TryGetCVParamDouble(spectrum.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_intensity, out var bpi))
                {
                    // For timsTOF data, this is the BPI of the entire frame
                    // Additionally, for timsTOF data, MS_base_peak_m_z is not defined
                    scanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(bpi, 5);

                    if (MSDataFileReader.TryGetCVParamDouble(spectrum.scanList.scans[0].cvParams, pwiz.CLI.cv.CVID.MS_base_peak_m_z,
                        out var basePeakMzFromCvParams))
                    {
                        scanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(basePeakMzFromCvParams, 5);
                        computeBPI = false;
                    }
                }

                if (string.IsNullOrEmpty(scanStatsEntry.ScanFilterText))
                {
                    if (spectrum.scanList?.scans.Count > 0)
                    {
                        // Bruker timsTOF datasets will have CVParam "inverse reduced ion mobility" for IMS spectra; check for this
                        foreach (var scanItem in spectrum.scanList.scans)
                        {
                            if (MSDataFileReader.TryGetCVParam(scanItem.cvParams, pwiz.CLI.cv.CVID.MS_inverse_reduced_ion_mobility, out _))
                            {
                                scanStatsEntry.ScanFilterText = "IMS";
                                break;
                            }
                        }
                    }
                }

                // Base peak signal to noise ratio
                scanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                scanStatsEntry.IonCount = msDataSpectrum.Mzs.Length;
                scanStatsEntry.IonCountRaw = scanStatsEntry.IonCount;

                if ((computeBPI || computeTIC) && scanStatsEntry.IonCount > 0)
                {
                    // Step through the raw data to compute the BPI and TIC

                    var mzList = msDataSpectrum.Mzs;
                    var intensities = msDataSpectrum.Intensities;

                    tic = 0;
                    bpi = 0;
                    double basePeakMZ = 0;

                    for (var index = 0; index <= mzList.Length - 1; index++)
                    {
                        tic += intensities[index];
                        if (intensities[index] > bpi)
                        {
                            bpi = intensities[index];
                            basePeakMZ = mzList[index];
                        }
                    }

                    scanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(tic, 5);
                    scanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(bpi, 5);
                    scanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(basePeakMZ, 5);
                }

                var addScan = !parserInfo.SkipExistingScans || parserInfo.SkipExistingScans && !mDatasetStatsSummarizer.HasScanNumber(scanNumber);

                if (addScan)
                {
                    if (parserInfo.SkipScansWithNoIons && scanStatsEntry.IonCount == 0)
                    {
                        parserInfo.SkippedEmptyScans++;

                        if (ShowPeriodicMessageNow(parserInfo.SkippedEmptyScans))
                        {
                            OnDebugEvent(string.Format("Skipping scan {0:N0} since no ions; {1:N0} total skipped scans", scanNumber, parserInfo.SkippedEmptyScans));
                        }
                    }
                    else
                    {
                        if (parserInfo.MaxScansToTrackInDetail < 0 || parserInfo.ScansStored < parserInfo.MaxScansToTrackInDetail)
                        {
                            mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);
                            parserInfo.ScansStored++;
                        }
                    }
                }

                if (mSaveTICAndBPI && !parserInfo.TicStored &&
                    (parserInfo.MaxScansForTicAndBpi < 0 || parserInfo.TicAndBpiScansStored < parserInfo.MaxScansForTicAndBpi))
                {
                    mTICAndBPIPlot.AddData(scanStatsEntry.ScanNumber, msLevels[scanIndex], (float)scanTimes[scanIndex], bpi, tic);
                    parserInfo.TicAndBpiScansStored++;
                }

                if (mSaveLCMS2DPlots && addScan)
                {
                    mLCMS2DPlot.AddScan(scanStatsEntry.ScanNumber, msLevels[scanIndex], (float)scanTimes[scanIndex], msDataSpectrum.Mzs.Length,
                        msDataSpectrum.Mzs, msDataSpectrum.Intensities);
                }

                if (mCheckCentroidingStatus)
                {
                    mDatasetStatsSummarizer.ClassifySpectrum(msDataSpectrum.Mzs, msLevels[scanIndex], "Scan " + scanStatsEntry.ScanNumber);
                }

                parserInfo.ScanCountSuccess++;
            }
            catch (Exception ex)
            {
                parserInfo.ScanCountError++;

                if (ShowPeriodicMessageNow(parserInfo.ScanCountError))
                {
                    OnWarningEvent(string.Format("Error loading header info for scan {0}: {1}", scanNumber, ex.Message));
                    if (parserInfo.ScanCountSuccess > 0)
                    {
                        var statusMessage = string.Format("{0} / {1} scans loaded successfully",
                            parserInfo.ScanCountSuccess,
                            parserInfo.ScanCountSuccess + parserInfo.ScanCountError);

                        ConsoleMsgUtils.ShowDebugCustom(statusMessage, emptyLinesBeforeMessage: 0);
                    }
                }
            }
        }

        private void Store2DPlotData(
            IReadOnlyDictionary<int, float> dct2DDataScanTimes,
            Dictionary<int, Dictionary<double, double>> dct2DDataParent,
            Dictionary<int, Dictionary<double, double>> dct2DDataProduct)
        {
            // This variable keeps track of the length of the largest Dictionary(Of Double, Double) var in dct2DData
            var max2DDataCount = 1;

            var scanNumMin2D = int.MaxValue;
            var scanNumMax2D = 0;

            // Determine the min/max scan numbers in dct2DDataParent
            // Also determine max2DDataCount

            UpdateDataRanges(dct2DDataParent, ref max2DDataCount, ref scanNumMin2D, ref scanNumMax2D);
            UpdateDataRanges(dct2DDataProduct, ref max2DDataCount, ref scanNumMin2D, ref scanNumMax2D);

            Store2DPlotDataWork(dct2DDataParent, dct2DDataScanTimes, 1, max2DDataCount, scanNumMin2D, scanNumMax2D);
            Store2DPlotDataWork(dct2DDataProduct, dct2DDataScanTimes, 2, max2DDataCount, scanNumMin2D, scanNumMax2D);

        }

        private void Store2DPlotDataPoint(IDictionary<int, Dictionary<double, double>> dct2DData, int scanNumber, double mz, double intensity)
        {

            if (dct2DData.TryGetValue(scanNumber, out var obj2DMzAndIntensity))
            {
                if (obj2DMzAndIntensity.TryGetValue(mz, out var currentIntensity))
                {
                    // Bump up the stored intensity at productMZ
                    obj2DMzAndIntensity[mz] = currentIntensity + intensity;
                }
                else
                {
                    obj2DMzAndIntensity.Add(mz, intensity);
                }
            }
            else
            {
                obj2DMzAndIntensity = new Dictionary<double, double> { { mz, intensity } };
            }

            // Store the data for this scan
            dct2DData[scanNumber] = obj2DMzAndIntensity;

        }

        private void Store2DPlotDataWork(
            Dictionary<int, Dictionary<double, double>> dct2DData,
            IReadOnlyDictionary<int, float> dct2DDataScanTimes,
            int msLevel,
            int max2DDataCount,
            int scanNumMin2D,
            int scanNumMax2D)
        {
            var mzList = new double[max2DDataCount];
            var intensityList = new double[max2DDataCount];

            if (dct2DData == null)
            {
                return;
            }

            using (var dct2DEnum = dct2DData.GetEnumerator())
            {
                while (dct2DEnum.MoveNext())
                {
                    var int2DPlotScanNum = dct2DEnum.Current.Key;

                    var obj2DMzAndIntensity = dct2DEnum.Current.Value;

                    obj2DMzAndIntensity.Keys.CopyTo(mzList, 0);
                    obj2DMzAndIntensity.Values.CopyTo(intensityList, 0);

                    // Make sure the data is sorted
                    Array.Sort(mzList, intensityList, 0, obj2DMzAndIntensity.Count);

                    // Store the data
                    mLCMS2DPlot.AddScan(dct2DEnum.Current.Key, msLevel, dct2DDataScanTimes[int2DPlotScanNum], obj2DMzAndIntensity.Count, mzList,
                                        intensityList);
                }
            }

            if (scanNumMin2D / (double)scanNumMax2D > 0.5)
            {
                // Zoom in the 2D plot to prevent all of the the data from being scrunched to the right
                mLCMS2DPlot.Options.UseObservedMinScan = true;
            }

        }

        private string StripExtraFromChromatogramID(string chromatogramIdText)
        {

            // If text looks like:
            // SRM SIC Q1=506.6 Q3=132.1 sample=1 period=1 experiment=1 transition=0

            // then remove text from sample= on

            var charIndex = chromatogramIdText.IndexOf("sample=", StringComparison.InvariantCultureIgnoreCase);
            if (charIndex <= 0)
                return chromatogramIdText;

            return chromatogramIdText.Substring(0, charIndex).TrimEnd();

        }

        private void UpdateDataRanges(
            Dictionary<int, Dictionary<double, double>> dct2DData,
            ref int max2DDataCount,
            ref int scanNumMin2D,
            ref int scanNumMax2D)
        {
            if (dct2DData == null)
                return;

            using (var dct2DEnum = dct2DData.GetEnumerator())
            {
                while (dct2DEnum.MoveNext())
                {
                    var int2DPlotScanNum = dct2DEnum.Current.Key;

                    if (dct2DEnum.Current.Value.Count > max2DDataCount)
                    {
                        max2DDataCount = dct2DEnum.Current.Value.Count;
                    }

                    if (int2DPlotScanNum < scanNumMin2D)
                    {
                        scanNumMin2D = int2DPlotScanNum;
                    }

                    if (int2DPlotScanNum > scanNumMax2D)
                    {
                        scanNumMax2D = int2DPlotScanNum;
                    }
                }
            }
        }

    }
}

