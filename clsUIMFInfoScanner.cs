using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using PRISM;
using ThermoFisher.CommonCore.Data.Business;
using UIMFLibrary;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)

namespace MSFileInfoScanner
{
    public class clsUIMFInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps
        public const string UIMF_FILE_EXTENSION = ".UIMF";

        /// <summary>
        /// This function is used to determine one or more overall quality scores
        /// </summary>
        /// <param name="uimfReader"></param>
        /// <param name="datasetFileInfo"></param>
        /// <param name="dctMasterFrameList"></param>
        /// <param name="masterFrameNumList"></param>
        private void ComputeQualityScores(
            DataReader uimfReader,
            DatasetFileInfo datasetFileInfo,
            IReadOnlyDictionary<int, UIMFData.FrameType> dctMasterFrameList,
            IReadOnlyList<int> masterFrameNumList)
        {
            float overallScore;

            double overallAvgIntensitySum = 0;
            var overallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0)
            {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all of the data using uimfReader
                const int msLevelFilter = 1;
                overallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(msLevelFilter);

            }
            else
            {
                var globalParams = uimfReader.GetGlobalParams();

                var globalMaxBins = globalParams.Bins;

                var mzList = new double[globalMaxBins + 1];
                var intensityList = new int[globalMaxBins + 1];

                // Call .GetStartAndEndScans to get the start and end Frames
                GetStartAndEndScans(globalParams.NumFrames, out var frameStart, out var frameEnd);

                for (var masterFrameNumIndex = 0; masterFrameNumIndex <= masterFrameNumList.Count - 1; masterFrameNumIndex++)
                {
                    var frameNumber = masterFrameNumList[masterFrameNumIndex];
                    if (!dctMasterFrameList.TryGetValue(frameNumber, out var eFrameType))
                    {
                        OnWarningEvent(string.Format(
                                           "Frametype {0} not found in dictionary dctMasterFrameList; ignoring frame {1} in ComputeQualityScores",
                                           eFrameType, frameNumber));

                        continue;
                    }

                    // Check whether the frame number is within the desired range
                    if (frameNumber < frameStart || frameNumber > frameEnd)
                    {
                        continue;
                    }

                    FrameParams frameParams;
                    try
                    {
                        frameParams = uimfReader.GetFrameParams(frameNumber);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Exception obtaining frame parameters for frame " + frameNumber + "; will skip this frame");
                        frameParams = null;
                    }

                    if (frameParams == null)
                    {
                        continue;
                    }

                    // We have to clear the m/z and intensity arrays before calling GetSpectrum

                    Array.Clear(mzList, 0, mzList.Length);
                    Array.Clear(intensityList, 0, intensityList.Length);

                    // Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                    // Scans likely range from 0 to frameParams.Scans-1, but we'll use frameParams.Scans just to be safe
                    var ionCount = uimfReader.GetSpectrum(frameNumber, frameNumber, eFrameType, 0, frameParams.Scans, out mzList, out intensityList);

                    if (ionCount <= 0)
                    {
                        continue;
                    }

                    // The m/z and intensity arrays might contain entries with m/z values of 0;
                    // need to copy the data in place to get the data in the correct format.

                    if (ionCount > mzList.Length)
                    {
                        ionCount = mzList.Length;
                    }

                    var targetIndex = 0;
                    for (var ionIndex = 0; ionIndex <= ionCount - 1; ionIndex++)
                    {
                        if (mzList[ionIndex] > 0)
                        {
                            if (targetIndex != ionIndex)
                            {
                                mzList[targetIndex] = mzList[ionIndex];
                                intensityList[targetIndex] = intensityList[ionIndex];
                            }
                            targetIndex += 1;
                        }
                    }

                    ionCount = targetIndex;

                    if (ionCount > 0)
                    {
                        // ToDo: Analyze ionMZ and ionIntensity to compute a quality scores
                        // Keep track of the quality scores and then store one or more overall quality scores in datasetFileInfo.OverallQualityScore
                        // For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                        double intensitySum = 0;
                        for (var ionIndex = 0; ionIndex <= ionCount - 1; ionIndex++)
                        {
                            intensitySum += intensityList[ionIndex];
                        }

                        overallAvgIntensitySum += intensitySum / ionCount;

                        overallAvgCount += 1;

                    }

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

        private void ConstructTICAndBPI(
            DataReader uimfReader,
            int frameStart,
            int frameEnd,
            out SortedDictionary<int, double> dctTIC,
            out SortedDictionary<int, double> dctBPI)
        {
            try
            {
                // Obtain the TIC and BPI for each MS frame

                Console.WriteLine("  Loading TIC values");
                dctTIC = uimfReader.GetTICByFrame(frameStart, frameEnd, 0, 0);

                Console.WriteLine("  Loading BPI values");
                dctBPI = uimfReader.GetBPIByFrame(frameStart, frameEnd, 0, 0);

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error obtaining TIC and BPI for overall dataset: " + ex.Message);
                dctTIC = new SortedDictionary<int, double>();
                dctBPI = new SortedDictionary<int, double>();
            }
        }

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            // The dataset name is simply the file name without .UIMF
            try
            {
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void LoadFrameDetails(
            DataReader uimfReader,
            IReadOnlyDictionary<int, UIMFData.FrameType> dctMasterFrameList,
            IReadOnlyList<int> masterFrameNumList)
        {
            const int BAD_TIC_OR_BPI = int.MinValue;

            // The StartTime value for each frame is the number of minutes since 12:00 am
            // If acquiring data from 11:59 pm through 12:00 am, the StartTime will reset to zero

            clsTICandBPIPlotter pressurePlot;

            if (mSaveTICAndBPI)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
                mTICAndBPIPlot.BPIXAxisLabel = "Frame number";
                mTICAndBPIPlot.TICXAxisLabel = "Frame number";

                if (mInstrumentSpecificPlots.Count == 0)
                {
                    AddInstrumentSpecificPlot("Drift tube Pressure");
                }

                // Track drift tube pressures using mTIC in pressurePlot
                pressurePlot = mInstrumentSpecificPlots.First();
                pressurePlot.DeviceType = Device.Analog;

                pressurePlot.BPIXAxisLabel = "Frame number";
                pressurePlot.TICXAxisLabel = "Frame number";

                pressurePlot.TICYAxisLabel = "Pressure";
                pressurePlot.TICYAxisExponentialNotation = false;

                pressurePlot.TICPlotAbbrev = "Pressure";
                pressurePlot.TICAutoMinMaxY = true;
                pressurePlot.RemoveZeroesFromEnds = true;
            }
            else
            {
                pressurePlot = new clsTICandBPIPlotter();
            }

            if (mSaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            var lastProgressTime = DateTime.UtcNow;

            // Note that this starts at 2 seconds, but is extended after each progress message is shown (maxing out at 30 seconds)
            var progressThresholdSeconds = 2;

            var globalParams = uimfReader.GetGlobalParams();

            var globalMaxBins = globalParams.Bins;

            var mzList = new double[globalMaxBins + 1];
            var intensityList = new int[globalMaxBins + 1];
            var ionsIntensity = new double[globalMaxBins + 1];

            // Call .GetStartAndEndScans to get the start and end Frames
            GetStartAndEndScans(globalParams.NumFrames, out var frameStart, out var frameEnd);

            // Construct the TIC and BPI (of all frames)
            ConstructTICAndBPI(uimfReader, frameStart, frameEnd, out var dctTIC, out var dctBPI);

            Console.Write("  Loading frame details");

            // Initialize the frame StartTime variables
            double frameStartTimeInitial = -1;
            double frameStartTimeAddon = 0;

            double frameStartTimePrevious = -1;
            double frameStartTimeCurrent = 0;

            for (var masterFrameNumIndex = 0; masterFrameNumIndex <= masterFrameNumList.Count - 1; masterFrameNumIndex++)
            {
                var frameNumber = masterFrameNumList[masterFrameNumIndex];

                if (!dctMasterFrameList.TryGetValue(frameNumber, out var eFrameType))
                {
                    OnWarningEvent(string.Format(
                        "Frametype {0} not found in dictionary dctMasterFrameList; ignoring frame {1} in LoadFrameDetails",
                        eFrameType, frameNumber));

                    continue;
                }

                // Check whether the frame number is within the desired range
                if (frameNumber < frameStart || frameNumber > frameEnd)
                {
                    continue;
                }

                try
                {
                    FrameParams frameParams;

                    try
                    {
                        frameParams = uimfReader.GetFrameParams(frameNumber);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Exception obtaining frame parameters for frame " + frameNumber + "; will skip this frame");
                        frameParams = null;
                    }

                    if (frameParams == null || eFrameType == UIMFData.FrameType.Calibration)
                    {
                        continue;
                    }

                    var nonZeroPointsInFrame = uimfReader.GetCountPerFrame(frameNumber);

                    int msLevel;
                    if (frameParams.FrameType == UIMFData.FrameType.MS2)
                    {
                        msLevel = 2;
                    }
                    else
                    {
                        msLevel = 1;
                    }

                    // Read the frame StartTime
                    // This will be zero in older .UIMF files, or in files converted from Agilent .D directories
                    // In newer files, it is the number of minutes since 12:00 am
                    frameStartTimeCurrent = frameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);
                    if (masterFrameNumIndex == 0 || frameStartTimeInitial < -0.9)
                    {
                        frameStartTimeInitial = frameStartTimeCurrent;
                    }

                    if (frameStartTimePrevious > 1400 && frameStartTimePrevious > frameStartTimeCurrent)
                    {
                        // We likely rolled over midnight; bump up frameStartTimeAddon by 1440 minutes
                        frameStartTimeAddon += 60 * 24;
                    }

                    // Compute the elution time (in minutes) of this frame
                    var elutionTime = frameStartTimeCurrent + frameStartTimeAddon - frameStartTimeInitial;

                    if (!dctBPI.TryGetValue(frameNumber, out var bpi))
                    {
                        bpi = BAD_TIC_OR_BPI;
                    }

                    if (!dctTIC.TryGetValue(frameNumber, out var tic))
                    {
                        tic = BAD_TIC_OR_BPI;
                    }

                    if (mSaveTICAndBPI)
                    {
                        if (bpi > BAD_TIC_OR_BPI && tic > BAD_TIC_OR_BPI)
                        {
                            mTICAndBPIPlot.AddData(frameNumber, msLevel, (float)elutionTime, bpi, tic);
                        }

                        var pressure = frameParams.GetValueDouble(FrameParamKeyType.PressureBack);
                        if (Math.Abs(pressure) < float.Epsilon)
                            pressure = frameParams.GetValueDouble(FrameParamKeyType.RearIonFunnelPressure);
                        if (Math.Abs(pressure) < float.Epsilon)
                            pressure = frameParams.GetValueDouble(FrameParamKeyType.IonFunnelTrapPressure);
                        if (Math.Abs(pressure) < float.Epsilon)
                            pressure = frameParams.GetValueDouble(FrameParamKeyType.PressureFront);

                        pressurePlot.AddDataTICOnly(frameNumber, msLevel, (float)elutionTime, pressure);
                    }

                    var scanStatsEntry = new ScanStatsEntry
                    {
                        ScanNumber = frameNumber,
                        ScanType = msLevel
                    };

                    if (msLevel <= 1)
                    {
                        scanStatsEntry.ScanTypeName = "HMS";
                    }
                    else
                    {
                        scanStatsEntry.ScanTypeName = "HMSn";
                    }

                    scanStatsEntry.ScanFilterText = "IMS";
                    scanStatsEntry.ExtendedScanInfo.ScanFilterText = scanStatsEntry.ScanFilterText;

                    scanStatsEntry.ElutionTime = elutionTime.ToString("0.0###");

                    if (tic > BAD_TIC_OR_BPI)
                    {
                        scanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(tic, 5);
                    }
                    else
                    {
                        scanStatsEntry.TotalIonIntensity = "0";
                    }

                    if (bpi > BAD_TIC_OR_BPI)
                    {
                        scanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(bpi, 5);
                    }
                    else
                    {
                        scanStatsEntry.BasePeakIntensity = "0";
                    }

                    scanStatsEntry.BasePeakMZ = "0";

                    // Base peak signal to noise ratio
                    scanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                    scanStatsEntry.IonCount = nonZeroPointsInFrame;
                    scanStatsEntry.IonCountRaw = nonZeroPointsInFrame;

                    // For UIMF files, we only store one entry per frame
                    // Use the median drift time as the representative drift time

                    var medianScanNum = frameParams.Scans / 2;

                    var driftTimeMsec = uimfReader.GetDriftTime(frameNumber, medianScanNum, true);
                    scanStatsEntry.DriftTimeMsec = driftTimeMsec.ToString("0.0###");

                    mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);

                    if (mSaveLCMS2DPlots || mCheckCentroidingStatus)
                    {
                        try
                        {
                            // Also need to load the raw data

                            // We have to clear the m/z and intensity arrays before calling GetSpectrum

                            Array.Clear(mzList, 0, mzList.Length);
                            Array.Clear(intensityList, 0, intensityList.Length);

                            // Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame

                            // In UIMF files from IMS04,                            if Frame_Parameters.Scans = 360, Frame_Scans will have scans 0 through 359
                            // In UIMF files from IMS08, prior to December 1, 2014, if Frame_Parameters.Scans = 374, Frame_Scans will have scans 0 through 373
                            // in UIMF files from IMS08, after December 1, 2014     if Frame_Parameters.Scans = 374, Frame_Scans will have scans 1 through 374

                            var ionCount = uimfReader.GetSpectrum(frameNumber, frameNumber, eFrameType, 0, frameParams.Scans, out mzList, out intensityList);

                            if (ionCount > 0)
                            {
                                // The m/z and intensity arrays might contain entries with m/z values of 0;
                                // need to copy the data in place to get the data in the correct format.
                                // In addition, we'll copy the intensity values from intensityList() into ionsIntensity()

                                if (ionCount > mzList.Length)
                                {
                                    ionCount = mzList.Length;
                                }

                                if (ionsIntensity.Length < ionCount)
                                {
                                    Array.Resize(ref ionsIntensity, ionCount);
                                }

                                var targetIndex = 0;
                                for (var ionIndex = 0; ionIndex <= ionCount - 1; ionIndex++)
                                {
                                    if (mzList[ionIndex] > 0)
                                    {
                                        mzList[targetIndex] = mzList[ionIndex];
                                        ionsIntensity[targetIndex] = intensityList[ionIndex];
                                        targetIndex += 1;
                                    }
                                }

                                ionCount = targetIndex;

                                if (ionCount > 0)
                                {
                                    if (ionsIntensity.Length > ionCount)
                                    {
                                        Array.Resize(ref ionsIntensity, ionCount);
                                    }

                                    if (mSaveLCMS2DPlots)
                                    {
                                        mLCMS2DPlot.AddScan(frameNumber, msLevel, (float)elutionTime, ionCount, mzList, ionsIntensity);
                                    }

                                    if (mCheckCentroidingStatus)
                                    {
                                        mDatasetStatsSummarizer.ClassifySpectrum(ionCount, mzList, msLevel, "Frame " + frameNumber);
                                    }
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            OnWarningEvent("Error loading m/z and intensity values for frame " + frameNumber + ": " + ex.Message);
                        }
                    }

                }
                catch (Exception ex)
                {
                    OnWarningEvent("Error loading header info for frame " + frameNumber + ": " + ex.Message);
                }

                frameStartTimePrevious = frameStartTimeCurrent;

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < progressThresholdSeconds)
                    continue;

                lastProgressTime = DateTime.UtcNow;
                if (progressThresholdSeconds < 30)
                    progressThresholdSeconds += 2;

                var percentComplete = masterFrameNumIndex / (float)masterFrameNumList.Count * 100;
                OnProgressUpdate(string.Format("Frames processed: {0:N0}", masterFrameNumIndex), percentComplete);

            }

            Console.WriteLine();
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {

            ResetResults();

            DataReader uimfReader = null;
            GlobalParams globalParams = null;

            var masterFrameNumList = new int[1];

            // Obtain the full path to the file
            var uimfFile = new FileInfo(dataFilePath);

            if (!uimfFile.Exists)
            {
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // datasetID = LookupDatasetID(datasetName)
            var datasetID = 0;

            datasetFileInfo.FileSystemCreationTime = uimfFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = uimfFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = datasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(uimfFile.Name);
            datasetFileInfo.FileExtension = uimfFile.Extension;
            datasetFileInfo.FileSizeBytes = uimfFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            var readError = false;
            var inaccurateStartTime = false;

            try
            {
                // Use the UIMFLibrary to read the .UIMF file
                uimfReader = new DataReader(uimfFile.FullName);
            }
            catch (Exception ex)
            {
                // File open failed
                OnErrorEvent("Call to .OpenUIMF failed for " + uimfFile.Name + ": " + ex.Message, ex);
                readError = true;
            }

            if (!readError)
            {
                try
                {
                    // First obtain the global parameters
                    globalParams = uimfReader.GetGlobalParams();
                }
                catch (Exception)
                {
                    // Read error
                    readError = true;
                }
            }

            if (!readError)
            {
                // Read the file info

                var dctMasterFrameList = new Dictionary<int, UIMFData.FrameType>();

                try
                {
                    // Construct a master list of frame numbers and frame types
                    dctMasterFrameList = uimfReader.GetMasterFrameList();

                    if (dctMasterFrameList.Count > 0)
                    {
                        // Copy the frame numbers into an array so that we can assure it's sorted
                        masterFrameNumList = new int[dctMasterFrameList.Keys.Count];
                        dctMasterFrameList.Keys.CopyTo(masterFrameNumList, 0);

                        Array.Sort(masterFrameNumList);
                    }

                    // ReSharper disable CommentTypo

                    // Extract the acquisition time information
                    // The Global_Parameters table records the start time of the entire dataset in field DateStarted
                    // The Frame_Parameters table records the start time of reach frame in field StartTime

                    // The DateStarted column in the Global_Parameters table should be represented by one of these values
                    //   A text-based date, like "5/2/2011 4:26:59 PM"; example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse.uimf
                    //   A text-based date (no time info), like "Thursday, January 13, 2011"; example: QC_Shew_11_01_pt5_c2_030311_earth_4ms_0001
                    //   A tick-based date, like 129272890050787740 (number of ticks since January 1, 1601); example: BATs_TS_01_c4_Eagle_10-02-06_0000

                    // The StartTime column in the Frame_Parameters table should be represented by one of these values
                    //   Integer between 0 and 1440 representing number of minutes since midnight (can loop from 1439.9 to 0); example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse.uimf
                    //   Integer between 0 and 60 representing number of minutes since past the current hour (can loop from 59.9 to 0); example: BATs_TS_01_c4_Eagle_10-02-06_0000.uimf
                    //   A tick-based date, like 634305349108974800 (number of ticks since January 1, 0001); example: QC_Shew_11_01_pt5_c2_030311_earth_4ms_0001
                    //   A negative number representing number of minutes from the start of the run in UTC time to the start of the current frame, in local time; example: Sarc_P08_G03_0747_7Dec11_Cheetah_11-09-05.uimf
                    //      Examine values: Frame 1 has StartTime = -479.993 and Frame 1177 has StartTime = -417.509
                    //   A positive integer representing number of minutes since the start of the run
                    //      Theoretically, this will be the case for IMS_TOF_4 acquired after 12/14/2011

                    // ReSharper restore CommentTypo

                    double runTimeMinutes = 0;

                    // First examine globalParams.DateStarted
                    try
                    {

                        var validStartTime = false;
                        var reportedDateStartedText = globalParams.GetValueString(GlobalParamKeyType.DateStarted);

                        if (!DateTime.TryParse(reportedDateStartedText, out var reportedDateStarted))
                        {
                            // Invalid date; log a message
                            OnWarningEvent(".UIMF file has an invalid DateStarted value in table Global_Parameters: " + reportedDateStartedText + "; " +
                                "will use the time the datafile was last modified");
                            inaccurateStartTime = true;
                        }
                        else
                        {
                            if (reportedDateStarted.Year < 450)
                            {
                                // Some .UIMF files have DateStarted values represented by huge integers, e.g. 127805472000000000 or 129145004045937500; example: BATs_TS_01_c4_Eagle_10-02-06_0000
                                // These numbers are the number of ticks since 1 January 1601 (where each tick is 100 ns)
                                // This value is returned by function GetSystemTimeAsFileTime (see http://en.wikipedia.org/wiki/System_time)

                                // When SQLite parses these numbers, it converts them to years around 0410
                                // To get the correct year, simply add 1600

                                reportedDateStarted = reportedDateStarted.AddYears(1600);
                                validStartTime = true;

                            }
                            else if (reportedDateStarted.Year < 2000 || reportedDateStarted.Year > DateTime.Now.Year + 1)
                            {
                                // Invalid date; log a message
                                OnWarningEvent(".UIMF file has an invalid DateStarted value in table Global_Parameters: " + reportedDateStartedText + "; " +
                                    "will use the time the datafile was last modified");
                                inaccurateStartTime = true;

                            }
                            else
                            {
                                validStartTime = true;
                            }
                        }

                        if (validStartTime)
                        {
                            datasetFileInfo.AcqTimeStart = reportedDateStarted;

                            // Update the end time to match the start time; we'll update it below using the start/end times obtained from the frame parameters
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        }

                    }
                    catch (Exception ex2)
                    {
                        OnWarningEvent("Exception extracting the DateStarted date from table Global_Parameters in the .UIMF file: " + ex2.Message);
                    }

                    // NumFrames is the total number of frames in the file (for all frame types)
                    datasetFileInfo.ScanCount = globalParams.NumFrames;
                    if (masterFrameNumList.Length > datasetFileInfo.ScanCount)
                    {
                        datasetFileInfo.ScanCount = masterFrameNumList.Length;
                    }

                    if (masterFrameNumList.Length > 0)
                    {
                        // Ideally, we would lookup the acquisition time of the first frame and the last frame, then subtract the two times to determine the run time
                        // However, given the odd values that can be present in the StartTime field, we need to construct a full list of start times and then parse it

                        // Get the start time of the first frame
                        // See above for the various numbers that could be stored in the StartTime column
                        var frameParams = uimfReader.GetFrameParams(masterFrameNumList[0]);
                        var startTime = frameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);

                        // Get the start time of the last frame
                        // If the reported start time is zero, step back until a non-zero start time is reported

                        var frameIndex = masterFrameNumList.Length - 1;
                        double endTime = 0;
                        while (frameIndex >= 0)
                        {
                            frameParams = uimfReader.GetFrameParams(masterFrameNumList[frameIndex]);
                            endTime = frameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);

                            if (Math.Abs(endTime) > float.Epsilon)
                                break;

                            frameIndex -= 1;
                        };

                        // Check whether the StartTime and EndTime values are based on ticks
                        if (startTime >= 1E+17 && endTime > 1E+17)
                        {
                            // StartTime and EndTime were stored as the number of ticks (where each tick is 100 ns)
                            // Tick start date is either 1 January 1601 or 1 January 0001

                            var runTime = DateTime.MinValue.AddTicks((long)(endTime - startTime));

                            runTimeMinutes = runTime.Subtract(DateTime.MinValue).TotalMinutes;

                            // In some .UIMF files, the DateStarted column in Global_Parameters is simply the date, and not a specific time of day
                            // If that's the case, update datasetFileInfo.AcqTimeStart to be based on runTimeMinutes
                            if (datasetFileInfo.AcqTimeStart.Date == datasetFileInfo.AcqTimeStart)
                            {
                                var reportedDateStarted = DateTime.MinValue.AddTicks((long)startTime);

                                if (reportedDateStarted.Year < 500)
                                {
                                    reportedDateStarted = reportedDateStarted.AddYears(1600);
                                }

                                if (reportedDateStarted.Year >= 2000 && reportedDateStarted.Year <= DateTime.Now.Year + 1)
                                {
                                    // Date looks valid
                                    if (inaccurateStartTime)
                                    {
                                        datasetFileInfo.AcqTimeStart = reportedDateStarted;
                                        datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                                    }
                                    else
                                    {
                                        // How does it compare to datasetFileInfo.AcqTimeStart?
                                        if (reportedDateStarted.Subtract(datasetFileInfo.AcqTimeStart).TotalHours < 24)
                                        {
                                            // Update the date
                                            datasetFileInfo.AcqTimeStart = reportedDateStarted;
                                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Ideally, we'd just compute RunTime like this: runTimeMinutes = endTime - startTime
                            // But, given the idiosyncrasies that can occur, we need to construct a full list of start times

                            var startTimes = new List<double>();
                            double endTimeAddon = 0;

                            for (var index = 0; index <= masterFrameNumList.Length - 1; index++)
                            {
                                frameParams = uimfReader.GetFrameParams(masterFrameNumList[index]);
                                startTimes.Add(frameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes));
                            }

                            // ReSharper disable once CommentTypo
                            // Some datasets erroneously have zeroes stored in the .UIMF file for the StartTime of the last two frames; example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse
                            // Check for this and remove them

                            var frameCountRemoved = 0;
                            while (Math.Abs(startTimes[startTimes.Count - 1]) < float.Epsilon)
                            {
                                startTimes.RemoveAt(startTimes.Count - 1);
                                frameCountRemoved += 1;
                                if (startTimes.Count == 0)
                                    break;
                            }

                            if (frameCountRemoved > 0)
                            {
                                if (startTimes.Count > 2)
                                {
                                    // Compute the amount of time (in minutes) to addon to the total run time
                                    // We're computing the time between two frames, and multiplying that by frameCountRemoved
                                    endTimeAddon += frameCountRemoved * (startTimes[startTimes.Count - 1] - startTimes[startTimes.Count - 2]);
                                }
                            }

                            // Now check for the StartTime changing to a smaller number from one frame to the next
                            // This could happen if the StartTime changed from 1439 to 0 as the system clock hits midnight
                            // Or if the StartTime changes from 59.9 to 0 as the system clock hits the top of a new hour
                            for (var index = 1; index <= startTimes.Count - 1; index++)
                            {
                                if (startTimes[index] < startTimes[index - 1])
                                {
                                    if (startTimes[index - 1] > 1439)
                                    {
                                        endTimeAddon += 1440;
                                    }
                                    else if (startTimes[index - 1] > 59.7)
                                    {
                                        endTimeAddon += 60;
                                    }
                                }
                            }

                            if (startTimes.Count > 0)
                            {
                                // Compute the runtime
                                // Luckily, even if startTime is -479.993 and endTime is -417.509, this works out to a positive, accurate runtime
                                endTime = startTimes[startTimes.Count - 1];
                                runTimeMinutes = endTime + endTimeAddon - startTime;
                            }

                        }

                    }
                    else
                    {
                        runTimeMinutes = 0;
                    }

                    if (runTimeMinutes > 0)
                    {
                        if (runTimeMinutes > 24000)
                        {
                            OnWarningEvent("Invalid runtime computed using the StartTime value from the first and last frames: " + runTimeMinutes);
                        }
                        else
                        {
                            if (inaccurateStartTime)
                            {
                                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-runTimeMinutes);
                            }
                            else
                            {
                                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(runTimeMinutes);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    OnWarningEvent("Exception extracting acquisition time information: " + ex.Message);
                }

                if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile || mSaveLCMS2DPlots)
                {
                    // Load data from each frame
                    // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                    LoadFrameDetails(uimfReader, dctMasterFrameList, masterFrameNumList);
                }

                if (mComputeOverallQualityScores)
                {
                    // Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(uimfReader, datasetFileInfo, dctMasterFrameList, masterFrameNumList);
                }
            }

            // Close the handle to the data file
            uimfReader?.Dispose();

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the .UIMF file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(uimfFile, datasetID);

            // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

            PostProcessTasks();

            return !readError;
        }

    }
}

