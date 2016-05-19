using System;
using System.Collections.Generic;
using System.IO;
using PNNLOmics.Utilities;
using UIMFLibrary;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//
// Last modified August 27, 2013

namespace MSFileInfoScanner
{
    public class clsUIMFInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string UIMF_FILE_EXTENSION = ".UIMF";

        private void ComputeQualityScores(ref DataReader objUIMFReader, clsDatasetFileInfo datasetFileInfo, ref Dictionary<int, DataReader.FrameType> dctMasterFrameList, ref int[] intMasterFrameNumList)
        {
            // This function is used to determine one or more overall quality scores

            FrameParams objFrameParams = default(FrameParams);
            GlobalParams objGlobalParams = default(GlobalParams);

            int intFrameStart = 0;
            int intFrameEnd = 0;

            int intGlobalMaxBins = 0;

            double[] dblMZList = null;
            int[] intIntensityList = null;

            float sngOverallScore = 0;

            double dblIntensitySum = 0;
            double dblOverallAvgIntensitySum = 0;
            int intOverallAvgCount = 0;

            dblOverallAvgIntensitySum = 0;
            intOverallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0) {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all of the data using objUIMFReader
                const int intMSLevelFilter = 1;
                sngOverallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(intMSLevelFilter);

            } else {
                objGlobalParams = objUIMFReader.GetGlobalParams();

                intGlobalMaxBins = objGlobalParams.Bins;


                dblMZList = new double[intGlobalMaxBins + 1];
                intIntensityList = new int[intGlobalMaxBins + 1];

                // Call .GetStartAndEndScans to get the start and end Frames
                base.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd);


                for (int intMasterFrameNumIndex = 0; intMasterFrameNumIndex <= intMasterFrameNumList.Length - 1; intMasterFrameNumIndex++) {
                    int intFrameNumber = 0;
                    DataReader.FrameType eFrameType = default(DataReader.FrameType);
                    intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex);
                    eFrameType = dctMasterFrameList(intFrameNumber);


                    // Check whether the frame number is within the desired range
                    if (intFrameNumber < intFrameStart || intFrameNumber > intFrameEnd) {
                        continue;
                    }

                    try {
                        objFrameParams = objUIMFReader.GetFrameParams(intFrameNumber);
                    } catch (Exception ex) {
                        Console.WriteLine("Exception obtaining frame parameters for frame " + intFrameNumber + "; will skip this frame");
                        objFrameParams = null;
                    }

                    if (objFrameParams == null) {
                        continue;
                    }

                    int intIonCount = 0;
                    int intTargetIndex = 0;

                    // We have to clear the m/z and intensity arrays before calling GetSpectrum

                    Array.Clear(dblMZList, 0, dblMZList.Length);
                    Array.Clear(intIntensityList, 0, intIntensityList.Length);

                    // Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                    // Scans likely range from 0 to objFrameParams.Scans-1, but we'll use objFrameParams.Scans just to be safe
                    intIonCount = objUIMFReader.GetSpectrum(intFrameNumber, intFrameNumber, eFrameType, 0, objFrameParams.Scans, dblMZList, intIntensityList);

                    if (intIonCount <= 0) {
                        continue;
                    }

                    // The m/z and intensity arrays might contain entries with m/z values of 0; 
                    // need to copy the data in place to get the data in the correct format.

                    if (intIonCount > dblMZList.Length) {
                        intIonCount = dblMZList.Length;
                    }

                    intTargetIndex = 0;
                    for (int intIonIndex = 0; intIonIndex <= intIonCount - 1; intIonIndex++) {
                        if (dblMZList(intIonIndex) > 0) {
                            if (intTargetIndex != intIonIndex) {
                                dblMZList(intTargetIndex) = dblMZList(intIonIndex);
                                intIntensityList(intTargetIndex) = intIntensityList(intIonIndex);
                            }
                            intTargetIndex += 1;
                        }
                    }

                    intIonCount = intTargetIndex;


                    if (intIonCount > 0) {
                        // ToDo: Analyze dblIonMZ and dblIonIntensity to compute a quality scores
                        // Keep track of the quality scores and then store one or more overall quality scores in datasetFileInfo.OverallQualityScore
                        // For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                        dblIntensitySum = 0;
                        for (int intIonIndex = 0; intIonIndex <= intIonCount - 1; intIonIndex++) {
                            dblIntensitySum += intIntensityList(intIonIndex);
                        }

                        dblOverallAvgIntensitySum += dblIntensitySum / intIonCount;

                        intOverallAvgCount += 1;

                    }

                }

                if (intOverallAvgCount > 0) {
                    sngOverallScore = Convert.ToSingle(dblOverallAvgIntensitySum / intOverallAvgCount);
                } else {
                    sngOverallScore = 0;
                }

            }

            datasetFileInfo.OverallQualityScore = sngOverallScore;

        }


        private void ConstructTICandBPI(ref DataReader objUIMFReader, int intFrameStart, int intFrameEnd, ref Dictionary<int, double> dctTIC, ref Dictionary<int, double> dctBPI)
        {
            try {
                // Obtain the TIC and BPI for each MS frame

                Console.WriteLine("  Loading TIC values");
                dctTIC = objUIMFReader.GetTICByFrame(intFrameStart, intFrameEnd, 0, 0);

                Console.WriteLine("  Loading BPI values");
                dctBPI = objUIMFReader.GetBPIByFrame(intFrameStart, intFrameEnd, 0, 0);

            } catch (Exception ex) {
                ReportError("Error obtaining TIC and BPI for overall dataset: " + ex.Message);
            }

        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            // The dataset name is simply the file name without .UIMF
            try {
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            } catch (Exception ex) {
                return string.Empty;
            }
        }


        private void LoadFrameDetails(ref DataReader objUIMFReader, ref Dictionary<int, DataReader.FrameType> dctMasterFrameList, ref int[] intMasterFrameNumList)
        {
            const int BAD_TIC_OR_BPI = int.MinValue;

            DateTime dtLastProgressTime = default(DateTime);

            Dictionary<int, double> dctTIC = new Dictionary<int, double>();
            Dictionary<int, double> dctBPI = new Dictionary<int, double>();

            int intFrameStart = 0;
            int intFrameEnd = 0;

            // The StartTime value for each frame is the number of minutes since 12:00 am
            // If acquiring data from 11:59 pm through 12:00 am, then the StartTime will reset to zero
            double dblFrameStartTimeInitial = 0;
            double dblFrameStartTimeAddon = 0;

            double dblFrameStartTimePrevious = 0;
            double dblFrameStartTimeCurrent = 0;

            double dblElutionTime = 0;
            int intNonZeroPointsInFrame = 0;

            int intGlobalMaxBins = 0;

            double[] dblMZList = null;
            int[] intIntensityList = null;
            double[] dblIonsIntensity = null;

            double dblPressure = 0;

            if (mSaveTICAndBPI) {
                // Initialize the TIC and BPI arrays
                base.InitializeTICAndBPI();
                mTICandBPIPlot.BPIXAxisLabel = "Frame number";
                mTICandBPIPlot.TICXAxisLabel = "Frame number";

                mInstrumentSpecificPlots.BPIXAxisLabel = "Frame number";
                mInstrumentSpecificPlots.TICXAxisLabel = "Frame number";

                mInstrumentSpecificPlots.TICYAxisLabel = "Pressure";
                mInstrumentSpecificPlots.TICYAxisExponentialNotation = false;

                mInstrumentSpecificPlots.TICPlotAbbrev = "Pressure";
                mInstrumentSpecificPlots.TICAutoMinMaxY = true;
                mInstrumentSpecificPlots.RemoveZeroesFromEnds = true;
            }

            if (mSaveLCMS2DPlots) {
                base.InitializeLCMS2DPlot();
            }

            dtLastProgressTime = DateTime.UtcNow;

            var objGlobalParams = objUIMFReader.GetGlobalParams();

            intGlobalMaxBins = objGlobalParams.Bins;

            dblMZList = new double[intGlobalMaxBins + 1];
            intIntensityList = new int[intGlobalMaxBins + 1];
            dblIonsIntensity = new double[intGlobalMaxBins + 1];

            // Call .GetStartAndEndScans to get the start and end Frames
            base.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd);

            // Construct the TIC and BPI (of all frames)
            ConstructTICandBPI(ref objUIMFReader, intFrameStart, intFrameEnd, ref dctTIC, ref dctBPI);

            Console.Write("  Loading frame details");

            // Initialize the frame starttime variables
            dblFrameStartTimeInitial = -1;
            dblFrameStartTimeAddon = 0;

            dblFrameStartTimePrevious = -1;
            dblFrameStartTimeCurrent = 0;


            for (int intMasterFrameNumIndex = 0; intMasterFrameNumIndex <= intMasterFrameNumList.Length - 1; intMasterFrameNumIndex++) {
                int intFrameNumber = 0;
                DataReader.FrameType eFrameType = default(DataReader.FrameType);
                intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex);
                eFrameType = dctMasterFrameList(intFrameNumber);
                var intMSLevel = 1;

                // Check whether the frame number is within the desired range
                if (intFrameNumber < intFrameStart || intFrameNumber > intFrameEnd) {
                    continue;
                }


                try {
                    FrameParams objFrameParams = default(FrameParams);

                    try {
                        objFrameParams = objUIMFReader.GetFrameParams(intFrameNumber);
                    } catch (Exception ex) {
                        Console.WriteLine("Exception obtaining frame parameters for frame " + intFrameNumber + "; will skip this frame");
                        objFrameParams = null;
                    }

                    if (objFrameParams == null || eFrameType == DataReader.FrameType.Calibration) {
                        continue;
                    }

                    intNonZeroPointsInFrame = objUIMFReader.GetCountPerFrame(intFrameNumber);

                    if (objFrameParams.FrameType == DataReader.FrameType.MS2) {
                        intMSLevel = 2;
                    } else {
                        intMSLevel = 1;
                    }

                    // Read the frame StartTime
                    // This will be zero in older .UIMF files, or in files converted from Agilent .D folders
                    // In newer files, it is the number of minutes since 12:00 am
                    dblFrameStartTimeCurrent = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);
                    if (intMasterFrameNumIndex == 0 || dblFrameStartTimeInitial < -0.9) {
                        dblFrameStartTimeInitial = dblFrameStartTimeCurrent;
                    }

                    if (dblFrameStartTimePrevious > 1400 && dblFrameStartTimePrevious > dblFrameStartTimeCurrent) {
                        // We likely rolled over midnight; bump up dblFrameStartTimeAddon by 1440 minutes
                        dblFrameStartTimeAddon += 60 * 24;
                    }

                    // Compute the elution time (in minutes) of this frame                    
                    dblElutionTime = dblFrameStartTimeCurrent + dblFrameStartTimeAddon - dblFrameStartTimeInitial;

                    double dblTIC = 0;
                    double dblBPI = 0;

                    if (!dctBPI.TryGetValue(intFrameNumber, dblBPI)) {
                        dblBPI = BAD_TIC_OR_BPI;
                    }

                    if (!dctTIC.TryGetValue(intFrameNumber, dblTIC)) {
                        dblTIC = BAD_TIC_OR_BPI;
                    }


                    if (mSaveTICAndBPI) {
                        if (dblTIC > BAD_TIC_OR_BPI && dblTIC > BAD_TIC_OR_BPI) {
                            mTICandBPIPlot.AddData(intFrameNumber, intMSLevel, Convert.ToSingle(dblElutionTime), dblBPI, dblTIC);
                        }

                        dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.PressureBack);
                        if (Math.Abs(dblPressure) < float.Epsilon)
                            dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.RearIonFunnelPressure);
                        if (Math.Abs(dblPressure) < float.Epsilon)
                            dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.IonFunnelTrapPressure);
                        if (Math.Abs(dblPressure) < float.Epsilon)
                            dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.PressureFront);

                        mInstrumentSpecificPlots.AddDataTICOnly(intFrameNumber, intMSLevel, Convert.ToSingle(dblElutionTime), dblPressure);
                    }


                    DSSummarizer.clsScanStatsEntry objScanStatsEntry = new DSSummarizer.clsScanStatsEntry();

                    objScanStatsEntry.ScanNumber = intFrameNumber;
                    objScanStatsEntry.ScanType = intMSLevel;

                    if (intMSLevel <= 1) {
                        objScanStatsEntry.ScanTypeName = "HMS";
                    } else {
                        objScanStatsEntry.ScanTypeName = "HMSn";
                    }

                    objScanStatsEntry.ScanFilterText = "";

                    objScanStatsEntry.ElutionTime = dblElutionTime.ToString("0.0000");
                    if (dblTIC > BAD_TIC_OR_BPI) {
                        objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5);
                    } else {
                        objScanStatsEntry.TotalIonIntensity = "0";
                    }

                    if (dblBPI > BAD_TIC_OR_BPI) {
                        objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5);
                    } else {
                        objScanStatsEntry.BasePeakIntensity = "0";
                    }

                    objScanStatsEntry.BasePeakMZ = "0";

                    // Base peak signal to noise ratio
                    objScanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                    objScanStatsEntry.IonCount = intNonZeroPointsInFrame;
                    objScanStatsEntry.IonCountRaw = intNonZeroPointsInFrame;

                    mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);


                    if (mSaveLCMS2DPlots | mCheckCentroidingStatus) {
                        try {
                            // Also need to load the raw data

                            int intIonCount = 0;
                            int intTargetIndex = 0;

                            // We have to clear the m/z and intensity arrays before calling GetSpectrum

                            Array.Clear(dblMZList, 0, dblMZList.Length);
                            Array.Clear(intIntensityList, 0, intIntensityList.Length);

                            // Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame

                            // In UIMF files from IMS04, if Frame_Parameters.Scans = 360 then Frame_Scans will have scans 0 through 359
                            // In UIMF files from IMS08, prior to December 1, 2014, if Frame_Parameters.Scans = 374 then Frame_Scans will have scans 0 through 373
                            // in UIMF files from IMS08, after December 1, 2014     if Frame_Parameters.Scans = 374 then Frame_Scans will have scans 1 through 374

                            intIonCount = objUIMFReader.GetSpectrum(intFrameNumber, intFrameNumber, eFrameType, 0, objFrameParams.Scans, dblMZList, intIntensityList);

                            if (intIonCount > 0) {
                                // The m/z and intensity arrays might contain entries with m/z values of 0; 
                                // need to copy the data in place to get the data in the correct format.
                                // In addition, we'll copy the intensity values from intIntensityList() into dblIonsIntensity()

                                if (intIonCount > dblMZList.Length) {
                                    intIonCount = dblMZList.Length;
                                }

                                if (dblIonsIntensity.Length < intIonCount) {
                                    Array.Resize(ref dblIonsIntensity, intIonCount);
                                }

                                intTargetIndex = 0;
                                for (int intIonIndex = 0; intIonIndex <= intIonCount - 1; intIonIndex++) {
                                    if (dblMZList(intIonIndex) > 0) {
                                        dblMZList(intTargetIndex) = dblMZList(intIonIndex);
                                        dblIonsIntensity(intTargetIndex) = intIntensityList(intIonIndex);
                                        intTargetIndex += 1;
                                    }
                                }

                                intIonCount = intTargetIndex;

                                if (intIonCount > 0) {
                                    if (dblIonsIntensity.Length > intIonCount) {
                                        Array.Resize(ref dblIonsIntensity, intIonCount);
                                    }

                                    if (mSaveLCMS2DPlots) {
                                        mLCMS2DPlot.AddScan(intFrameNumber, intMSLevel, Convert.ToSingle(dblElutionTime), intIonCount, dblMZList, dblIonsIntensity);
                                    }

                                    if (mCheckCentroidingStatus) {
                                        mDatasetStatsSummarizer.ClassifySpectrum(intIonCount, dblMZList, intMSLevel);
                                    }
                                }
                            }

                        } catch (Exception ex) {
                            ReportError("Error loading m/z and intensity values for frame " + intFrameNumber + ": " + ex.Message);
                        }
                    }

                } catch (Exception ex) {
                    ReportError("Error loading header info for frame " + intFrameNumber + ": " + ex.Message);
                }

                ShowProgress(intMasterFrameNumIndex, intMasterFrameNumList.Length, dtLastProgressTime);

                dblFrameStartTimePrevious = dblFrameStartTimeCurrent;

            }

            Console.WriteLine();

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            DataReader objUIMFReader = null;
            GlobalParams objGlobalParams = null;
            FrameParams objFrameParams = default(FrameParams);

            int intDatasetID = 0;
            int intIndex = 0;

            bool blnReadError = false;
            bool blnInaccurateStartTime = false;

            int[] intMasterFrameNumList = null;
            intMasterFrameNumList = new int[1];

            // Obtain the full path to the file
            var fiFileInfo = new FileInfo(strDataFilePath);

            if (!fiFileInfo.Exists) {
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // intDatasetID = LookupDatasetID(strDatasetName)
            intDatasetID = 0;

            datasetFileInfo.FileSystemCreationTime = fiFileInfo.CreationTime;
            datasetFileInfo.FileSystemModificationTime = fiFileInfo.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = intDatasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(fiFileInfo.Name);
            datasetFileInfo.FileExtension = fiFileInfo.Extension;
            datasetFileInfo.FileSizeBytes = fiFileInfo.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            blnReadError = false;
            blnInaccurateStartTime = false;

            try {
                // Use the UIMFLibrary to read the .UIMF file
                objUIMFReader = new DataReader(fiFileInfo.FullName);
            } catch (Exception ex) {
                // File open failed
                ReportError("Call to .OpenUIMF failed for " + fiFileInfo.Name + ": " + ex.Message);
                blnReadError = true;
            }

            if (!blnReadError) {
                try {
                    // First obtain the global parameters
                    objGlobalParams = objUIMFReader.GetGlobalParams();
                } catch (Exception ex) {
                    // Read error
                    blnReadError = true;
                }
            }

            if (!blnReadError) {
                // Read the file info

                Dictionary<int, DataReader.FrameType> dctMasterFrameList = default(Dictionary<int, DataReader.FrameType>);
                dctMasterFrameList = new Dictionary<int, DataReader.FrameType>();


                try {
                    // Construct a master list of frame numbers and frame types
                    dctMasterFrameList = objUIMFReader.GetMasterFrameList();


                    if (dctMasterFrameList.Count > 0) {
                        // Copy the frame numbers into an array so that we can assure it's sorted
                        intMasterFrameNumList = new int[dctMasterFrameList.Keys.Count];
                        dctMasterFrameList.Keys.CopyTo(intMasterFrameNumList, 0);

                        Array.Sort(intMasterFrameNumList);
                    }

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

                    double dblStartTime = 0;
                    double dblEndTime = 0;
                    double dblRunTime = 0;

                    // First examine objGlobalParams.DateStarted
                    try {
                        string strReportedDateStarted = null;
                        DateTime dtReportedDateStarted = default(DateTime);

                        bool blnValidStartTime = false;

                        blnValidStartTime = false;
                        strReportedDateStarted = objGlobalParams.GetValue(GlobalParamKeyType.DateStarted);

                        if (!DateTime.TryParse(strReportedDateStarted, dtReportedDateStarted)) {
                            // Invalid date; log a message
                            ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " + strReportedDateStarted + "; will use the time the datafile was last modified");
                            blnInaccurateStartTime = true;
                        } else {
                            if (dtReportedDateStarted.Year < 450) {
                                // Some .UIMF files have DateStarted values represented by huge integers, e.g. 127805472000000000 or 129145004045937500; example: BATs_TS_01_c4_Eagle_10-02-06_0000
                                // These numbers are the number of ticks since 1 January 1601 (where each tick is 100 ns)
                                // This value is returned by function GetSystemTimeAsFileTime (see http://en.wikipedia.org/wiki/System_time)

                                // When SQLite parses these numbers, it converts them to years around 0410
                                // To get the correct year, simply add 1600

                                dtReportedDateStarted = dtReportedDateStarted.AddYears(1600);
                                blnValidStartTime = true;

                            } else if (dtReportedDateStarted.Year < 2000 | dtReportedDateStarted.Year > DateTime.Now.Year + 1) {
                                // Invalid date; log a message
                                ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " + dtReportedDateStarted.ToString + "; will use the time the datafile was last modified");
                                blnInaccurateStartTime = true;

                            } else {
                                blnValidStartTime = true;
                            }
                        }


                        if (blnValidStartTime) {
                            datasetFileInfo.AcqTimeStart = dtReportedDateStarted;

                            // Update the end time to match the start time; we'll update it below using the start/end times obtained from the frame parameters
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        }

                    } catch (Exception ex2) {
                        ShowMessage("Exception extracting the DateStarted date from table Global_Parameters in the .UIMF file: " + ex2.Message);
                    }

                    // NumFrames is the total number of frames in the file (for all frame types)
                    datasetFileInfo.ScanCount = objGlobalParams.NumFrames;
                    if (intMasterFrameNumList.Length > datasetFileInfo.ScanCount) {
                        datasetFileInfo.ScanCount = intMasterFrameNumList.Length;
                    }


                    if (intMasterFrameNumList.Length > 0) {
                        // Ideally, we would lookup the acquisition time of the first frame and the last frame, then subtract the two times to determine the run time
                        // However, given the odd values that can be present in the StartTime field, we need to construct a full list of start times and then parse it

                        // Get the start time of the first frame
                        // See above for the various numbers that could be stored in the StartTime column
                        objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList(0));
                        dblStartTime = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);

                        // Get the start time of the last frame
                        // If the reported start time is zero, then step back until a non-zero start time is reported

                        intIndex = intMasterFrameNumList.Length - 1;
                        do {
                            objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList[intIndex]);
                            dblEndTime = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes);

                            if (Math.Abs(dblEndTime) < float.Epsilon) {
                                intIndex -= 1;
                            }
                        } while (Math.Abs(dblEndTime) < float.Epsilon && intIndex >= 0);

                        // Check whether the StartTime and EndTime values are based on ticks
                        if (dblStartTime >= 1E+17 & dblEndTime > 1E+17) {
                            // StartTime and Endtime were stored as the number of ticks (where each tick is 100 ns)
                            // Tick start date is either 1 January 1601 or 1 January 0001

                            DateTime dtRunTime = default(DateTime);
                            dtRunTime = DateTime.MinValue.AddTicks(Convert.ToInt64(dblEndTime - dblStartTime));

                            dblRunTime = dtRunTime.Subtract(DateTime.MinValue).TotalMinutes;

                            // In some .UIMF files, the DateStarted column in Global_Parameters is simply the date, and not a specific time of day
                            // If that's the case, then update datasetFileInfo.AcqTimeStart to be based on dblRunTime
                            if (datasetFileInfo.AcqTimeStart.Date == datasetFileInfo.AcqTimeStart) {
                                DateTime dtReportedDateStarted = default(DateTime);
                                dtReportedDateStarted = DateTime.MinValue.AddTicks(Convert.ToInt64(dblStartTime));

                                if (dtReportedDateStarted.Year < 500) {
                                    dtReportedDateStarted = dtReportedDateStarted.AddYears(1600);
                                }

                                if (dtReportedDateStarted.Year >= 2000 & dtReportedDateStarted.Year <= DateTime.Now.Year + 1) {
                                    // Date looks valid
                                    if (blnInaccurateStartTime) {
                                        datasetFileInfo.AcqTimeStart = dtReportedDateStarted;
                                        datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                                    } else {
                                        // How does it compare to datasetFileInfo.AcqTimeStart?
                                        if (dtReportedDateStarted.Subtract(datasetFileInfo.AcqTimeStart).TotalHours < 24) {
                                            // Update the date
                                            datasetFileInfo.AcqTimeStart = dtReportedDateStarted;
                                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                                        }
                                    }
                                }
                            }
                        } else {
                            // Ideally, we'd just compute RunTime like this: dblRunTime = dblEndTime - dblStartTime
                            // But, given the idiosyncracies that can occur, we need to construct a full list of start times

                            List<double> lstStartTimes = new List<double>();
                            double dblEndTimeAddon = 0;

                            for (intIndex = 0; intIndex <= intMasterFrameNumList.Length - 1; intIndex++) {
                                objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList[intIndex]);
                                lstStartTimes.Add(objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes));
                            }

                            // Some datasets erroneously have zeroes stored in the .UIMF file for the StartTime of the last two frames; example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse
                            // Check for this and remove them
                            int intFrameCountRemoved = 0;
                            while ((Math.Abs(lstStartTimes(lstStartTimes.Count - 1)) < float.Epsilon)) {
                                lstStartTimes.RemoveAt(lstStartTimes.Count - 1);
                                intFrameCountRemoved += 1;
                                if (lstStartTimes.Count == 0)
                                    break; // TODO: might not be correct. Was : Exit Do
                            }

                            if (intFrameCountRemoved > 0) {
                                if (lstStartTimes.Count > 2) {
                                    // Compute the amount of time (in minutes) to addon to the total run time
                                    // We're computing the time between two frames, and multiplying that by intFrameCountRemoved
                                    dblEndTimeAddon += intFrameCountRemoved * (lstStartTimes(lstStartTimes.Count - 1) - lstStartTimes(lstStartTimes.Count - 2));
                                }
                            }

                            // Now check for the StartTime changing to a smaller number from one frame to the next
                            // This could happen if the StartTime changed from 1439 to 0 as the system clock hits midnight
                            // Or if the StartTime changes from 59.9 to 0 as the system clock hits the top of a new hour
                            for (intIndex = 1; intIndex <= lstStartTimes.Count - 1; intIndex++) {
                                if (lstStartTimes[intIndex] < lstStartTimes(intIndex - 1)) {
                                    if (lstStartTimes(intIndex - 1) > 1439) {
                                        dblEndTimeAddon += 1440;
                                    } else if (lstStartTimes(intIndex - 1) > 59.7) {
                                        dblEndTimeAddon += 60;
                                    }
                                }
                            }

                            if (lstStartTimes.Count > 0) {
                                // Compute the runtime
                                // Luckily, even if dblStartTime is -479.993 and dblEntTime is -417.509, this works out to a positive, accurate runtime
                                dblEndTime = lstStartTimes(lstStartTimes.Count - 1);
                                dblRunTime = dblEndTime + dblEndTimeAddon - dblStartTime;
                            }

                        }

                    } else {
                        dblRunTime = 0;
                    }

                    if (dblRunTime > 0) {
                        if (dblRunTime > 24000) {
                            ShowMessage("Invalid runtime computed using the StartTime value from the first and last frames: " + dblRunTime);
                        } else {
                            if (blnInaccurateStartTime) {
                                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblRunTime);
                            } else {
                                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(dblRunTime);
                            }
                        }
                    }

                } catch (Exception ex) {
                    ShowMessage("Exception extracting acquisition time information: " + ex.Message);
                }

                if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile || mSaveLCMS2DPlots) {
                    // Load data from each frame
                    // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                    LoadFrameDetails(ref objUIMFReader, ref dctMasterFrameList, ref intMasterFrameNumList);
                }

                if (mComputeOverallQualityScores) {
                    // Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(ref objUIMFReader, ref datasetFileInfo, ref dctMasterFrameList, ref intMasterFrameNumList);
                }
            }

            if ((objUIMFReader != null)) {
                // Close the handle to the data file
                objUIMFReader.Dispose();
            }


            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            UpdateDatasetFileStats(fiFileInfo, intDatasetID);

            // Copy over the updated filetime info from datasetFileInfo to mDatasetFileInfo
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = datasetFileInfo.FileSystemCreationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = datasetFileInfo.FileSystemModificationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

            return !blnReadError;

        }

    }
}

