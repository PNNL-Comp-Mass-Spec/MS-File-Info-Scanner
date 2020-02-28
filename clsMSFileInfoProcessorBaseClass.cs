using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;
using ThermoFisher.CommonCore.Data.Business;
using ThermoRawFileReader;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2007

namespace MSFileInfoScanner
{
    public abstract class clsMSFileInfoProcessorBaseClass : iMSFileInfoProcessor
    {
        public const int PROGRESS_SPECTRA_LOADED = 90;
        public const int PROGRESS_SAVED_TIC_AND_BPI_PLOT = 92;
        public const int PROGRESS_SAVED_2D_PLOTS = 99;

        protected const int MAX_SCANS_TO_TRACK_IN_DETAIL = 750000;
        protected const int MAX_SCANS_FOR_TIC_AND_BPI = 1000000;

        /// <summary>
        /// Constructor
        /// </summary>
        protected clsMSFileInfoProcessorBaseClass()
        {
            mTICAndBPIPlot = new clsTICandBPIPlotter("TICAndBPIPlot", true);
            RegisterEvents(mTICAndBPIPlot);

            mInstrumentSpecificPlots = new List<clsTICandBPIPlotter>();

            mDatasetStatsSummarizer = new DatasetStatsSummarizer();
            RegisterEvents(mDatasetStatsSummarizer);

            mLCMS2DPlot = new clsLCMSDataPlotter();
            RegisterEvents(mLCMS2DPlot);

            mLCMS2DPlotOverview = new clsLCMSDataPlotter();
            RegisterEvents(mLCMS2DPlotOverview);

            InitializeLocalVariables();
        }

        #region "Constants"

        /// <summary>
        /// Used for checking if over 10% of the spectra failed MS2MzMin validation
        /// </summary>
        // ReSharper disable once IdentifierTypo
        public const int MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED = 10;

        #endregion

        #region "Member variables"

        protected bool mSaveTICAndBPI;
        protected bool mSaveLCMS2DPlots;

        protected bool mCheckCentroidingStatus;
        protected bool mComputeOverallQualityScores;

        /// <summary>
        /// When True, creates an XML file with dataset info
        /// </summary>
        protected bool mCreateDatasetInfoFile;

        /// <summary>
        /// When True, creates a _ScanStats.txt file
        /// </summary>
        protected bool mCreateScanStatsFile;

        private int mLCMS2DOverviewPlotDivisor;

        /// <summary>
        /// When True, adds a new row to a tab-delimited text file that has dataset stats
        /// </summary>
        private bool mUpdateDatasetStatsTextFile;

        private string mDatasetStatsTextFileName;
        private int mScanStart;
        private int mScanEnd;

        private float mMS2MzMin;

        protected bool mShowDebugInfo;

        private int mDatasetID;

        protected bool mDisableInstrumentHash;

        protected bool mCopyFileLocalOnReadError;

        private bool mPlotWithPython;

        /// <summary>
        /// This variable tracks TIC and BPI data (vs. scan)
        /// </summary>
        protected readonly clsTICandBPIPlotter mTICAndBPIPlot;

        /// <summary>
        /// This variable tracks UIMF pressure vs. frame (using mTIC)
        /// It also tracks data associated with other devices tracked by .raw files (e.g. LC pressure vs. scan)
        /// </summary>
        protected readonly List<clsTICandBPIPlotter> mInstrumentSpecificPlots;

        protected readonly clsLCMSDataPlotter mLCMS2DPlot;

        private readonly clsLCMSDataPlotter mLCMS2DPlotOverview;

        protected readonly DatasetStatsSummarizer mDatasetStatsSummarizer;

        #endregion

        #region "Properties"

        /// <summary>
        /// This property allows the parent class to define the DatasetID value
        /// </summary>
        public override int DatasetID
        {
            get => mDatasetID;
            set => mDatasetID = value;
        }

        /// <summary>
        /// Dataset stats file name
        /// </summary>
        public override string DatasetStatsTextFileName
        {
            get => mDatasetStatsTextFileName;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    // Do not update mDatasetStatsTextFileName
                }
                else
                {
                    mDatasetStatsTextFileName = value;
                }
            }
        }

        /// <summary>
        /// When true, do not include the Scan Type table in the QC Plot HTML file
        /// </summary>
        protected bool HideEmptyHTMLSections { get; set; }

        /// <summary>
        /// LC/MS 2D plot options
        /// </summary>
        public override clsLCMSDataPlotterOptions LCMS2DPlotOptions
        {
            get => mLCMS2DPlot.Options;
            set
            {
                mLCMS2DPlot.Options = value;
                mLCMS2DPlotOverview.Options = value.Clone();
            }
        }

        /// <summary>
        /// LC/MS 2D overview plot divisor
        /// </summary>
        public override int LCMS2DOverviewPlotDivisor
        {
            get => mLCMS2DOverviewPlotDivisor;
            set => mLCMS2DOverviewPlotDivisor = value;
        }

        /// <summary>
        /// Minimum m/z value that MS/mS spectra should have
        /// </summary>
        /// <remarks>
        /// Useful for validating instrument files where the sample is iTRAQ or TMT labelled
        /// and it is important to detect the reporter ions in the MS/MS spectra
        /// </remarks>
        public override float MS2MzMin
        {
            get => mMS2MzMin;
            set => mMS2MzMin = value;
        }

        /// <summary>
        /// This will be True if the dataset has too many MS/MS spectra
        /// where the minimum m/z value is larger than MS2MzMin
        /// </summary>
        public override bool MS2MzMinValidationError { get; set; }

        /// <summary>
        /// This will be True if the dataset has some MS/MS spectra
        /// where the minimum m/z value is larger than MS2MzMin
        /// (no more than 10% of the spectra)
        /// </summary>
        public override bool MS2MzMinValidationWarning { get; set; }

        /// <summary>
        /// MS2MzMin validation error or warning message
        /// </summary>
        public override string MS2MzMinValidationMessage { get; set; }

        /// <summary>
        /// First scan to process
        /// </summary>
        public override int ScanStart
        {
            get => mScanStart;
            set => mScanStart = value;
        }

        /// <summary>
        /// When ScanEnd is > 0, then will stop processing at the specified scan number
        /// </summary>
        public override int ScanEnd
        {
            get => mScanEnd;
            set => mScanEnd = value;
        }

        /// <summary>
        /// Set to True to show debug info
        /// </summary>
        public override bool ShowDebugInfo
        {
            get => mShowDebugInfo;
            set => mShowDebugInfo = value;
        }

        #endregion

        /// <summary>
        /// Get a processing option
        /// </summary>
        /// <param name="eOption"></param>
        /// <returns></returns>
        public override bool GetOption(ProcessingOptions eOption)
        {
            switch (eOption)
            {
                case ProcessingOptions.CreateTICAndBPI:
                    return mSaveTICAndBPI;
                case ProcessingOptions.ComputeOverallQualityScores:
                    return mComputeOverallQualityScores;
                case ProcessingOptions.CreateDatasetInfoFile:
                    return mCreateDatasetInfoFile;
                case ProcessingOptions.CreateLCMS2DPlots:
                    return mSaveLCMS2DPlots;
                case ProcessingOptions.CopyFileLocalOnReadError:
                    return mCopyFileLocalOnReadError;
                case ProcessingOptions.UpdateDatasetStatsTextFile:
                    return mUpdateDatasetStatsTextFile;
                case ProcessingOptions.CreateScanStatsFile:
                    return mCreateScanStatsFile;
                case ProcessingOptions.CheckCentroidingStatus:
                    return mCheckCentroidingStatus;
                case ProcessingOptions.PlotWithPython:
                    return mPlotWithPython;
                case ProcessingOptions.DisableInstrumentHash:
                    return mDisableInstrumentHash;
            }

            throw new Exception("Unrecognized option, " + eOption);
        }

        /// <summary>
        /// Set a processing option
        /// </summary>
        /// <param name="eOption"></param>
        /// <param name="value"></param>
        public override void SetOption(ProcessingOptions eOption, bool value)
        {
            switch (eOption)
            {
                case ProcessingOptions.CreateTICAndBPI:
                    mSaveTICAndBPI = value;
                    break;
                case ProcessingOptions.ComputeOverallQualityScores:
                    mComputeOverallQualityScores = value;
                    break;
                case ProcessingOptions.CreateDatasetInfoFile:
                    mCreateDatasetInfoFile = value;
                    break;
                case ProcessingOptions.CreateLCMS2DPlots:
                    mSaveLCMS2DPlots = value;
                    break;
                case ProcessingOptions.CopyFileLocalOnReadError:
                    mCopyFileLocalOnReadError = value;
                    break;
                case ProcessingOptions.UpdateDatasetStatsTextFile:
                    mUpdateDatasetStatsTextFile = value;
                    break;
                case ProcessingOptions.CreateScanStatsFile:
                    mCreateScanStatsFile = value;
                    break;
                case ProcessingOptions.CheckCentroidingStatus:
                    mCheckCentroidingStatus = value;
                    break;
                case ProcessingOptions.PlotWithPython:
                    mPlotWithPython = value;
                    break;
                case ProcessingOptions.DisableInstrumentHash:
                    mDisableInstrumentHash = value;
                    break;
                default:
                    throw new Exception("Unrecognized option, " + eOption);
            }

        }

        /// <summary>
        /// Add a new clsTICAndBPIPlotter instance to mInstrumentSpecificPlots
        /// </summary>
        /// <param name="dataSource"></param>
        protected clsTICandBPIPlotter AddInstrumentSpecificPlot(string dataSource)
        {
            var plotContainer = new clsTICandBPIPlotter(dataSource, true);
            RegisterEvents(plotContainer);
            mInstrumentSpecificPlots.Add(plotContainer);

            return plotContainer;
        }

        /// <summary>
        /// Find the largest file in the instrument directory
        /// Compute its SHA-1 hash then add to mDatasetStatsSummarizer.DatasetFileInfo
        /// </summary>
        /// <param name="instrumentDirectory"></param>
        protected void AddLargestInstrumentFile(DirectoryInfo instrumentDirectory)
        {
            var filesBySize = (from item in instrumentDirectory.GetFiles("*") orderby item.Length select item).ToList();

            if (filesBySize.Count > 0)
            {
                if (mDisableInstrumentHash)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(filesBySize.Last());
                }
                else
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(filesBySize.Last());
                }
            }
        }

        /// <summary>
        /// Computes the incremental progress that has been made beyond currentTaskProgressAtStart, based on the subtask progress and the next overall progress level
        /// </summary>
        /// <param name="currentTaskProgressAtStart">Progress at the start of the current subtask (value between 0 and 100)</param>
        /// <param name="currentTaskProgressAtEnd">Progress at the start of the current subtask (value between 0 and 100)</param>
        /// <param name="subTaskProgress">Progress of the current subtask (value between 0 and 100)</param>
        /// <returns>Overall progress (value between 0 and 100)</returns>
        /// <remarks></remarks>
        public static float ComputeIncrementalProgress(float currentTaskProgressAtStart, float currentTaskProgressAtEnd, float subTaskProgress)
        {
            if (subTaskProgress < 0)
            {
                return currentTaskProgressAtStart;
            }

            if (subTaskProgress >= 100)
            {
                return currentTaskProgressAtEnd;
            }

            return (float)(currentTaskProgressAtStart + (subTaskProgress / 100.0) * (currentTaskProgressAtEnd - currentTaskProgressAtStart));
        }

        private bool CreateDatasetInfoFile(string inputFileName, string outputDirectoryPath)
        {

            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var datasetInfoFilePath = Path.Combine(outputDirectoryPath, datasetName);
                datasetInfoFilePath += DatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                success = mDatasetStatsSummarizer.CreateDatasetInfoFile(datasetName, datasetInfoFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling DatasetStatsSummarizer.CreateDatasetInfoFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating dataset info file: " + ex.Message, ex);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="inputFileName">Input file name</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateDatasetScanStatsFile(string inputFileName, string outputDirectoryPath)
        {

            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var scanStatsFilePath = Path.Combine(outputDirectoryPath, datasetName) + "_ScanStats.txt";

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                success = mDatasetStatsSummarizer.CreateScanStatsFile(scanStatsFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling DatasetStatsSummarizer.CreateScanStatsFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating dataset ScanStats file: " + ex.Message, ex);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Get the dataset info as XML
        /// </summary>
        /// <returns></returns>
        public override string GetDatasetInfoXML()
        {

            try
            {
                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                return mDatasetStatsSummarizer.CreateDatasetInfoXML();

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error getting dataset info XML", ex);
            }

            return string.Empty;

        }

        /// <summary>
        /// Returns the range of scan numbers to process
        /// </summary>
        /// <param name="scanCount">Number of scans in the file</param>
        /// <param name="scanStart">1 if mScanStart is zero; otherwise mScanStart</param>
        /// <param name="scanEnd">scanCount if mScanEnd is zero; otherwise Min(mScanEnd, scanCount)</param>
        /// <remarks></remarks>
        protected void GetStartAndEndScans(int scanCount, out int scanStart, out int scanEnd)
        {
            GetStartAndEndScans(scanCount, 1, out scanStart, out scanEnd);
        }

        /// <summary>
        /// Returns the range of scan numbers to process
        /// </summary>
        /// <param name="scanCount">Number of scans in the file</param>
        /// <param name="scanNumFirst">The first scan number in the file (typically 1)</param>
        /// <param name="scanStart">1 if mScanStart is zero; otherwise mScanStart</param>
        /// <param name="scanEnd">scanCount if mScanEnd is zero; otherwise Min(mScanEnd, scanCount)</param>
        /// <remarks></remarks>
        private void GetStartAndEndScans(int scanCount, int scanNumFirst, out int scanStart, out int scanEnd)
        {
            if (mScanStart > 0)
            {
                scanStart = mScanStart;
            }
            else
            {
                scanStart = scanNumFirst;
            }

            if (mScanEnd > 0 && mScanEnd < scanCount)
            {
                scanEnd = mScanEnd;
            }
            else
            {
                scanEnd = scanCount;
            }

        }

        private void InitializeLocalVariables()
        {

            mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;

            mSaveTICAndBPI = false;
            mSaveLCMS2DPlots = false;
            mCheckCentroidingStatus = false;

            mComputeOverallQualityScores = false;

            mCreateDatasetInfoFile = false;
            mCreateScanStatsFile = false;

            mUpdateDatasetStatsTextFile = false;
            mDatasetStatsTextFileName = DatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME;

            mScanStart = 0;
            mScanEnd = 0;
            mMS2MzMin = 0;

            mShowDebugInfo = false;

            mDatasetID = 0;
            mDisableInstrumentHash = false;

            mCopyFileLocalOnReadError = false;

            MS2MzMinValidationError = false;
            MS2MzMinValidationWarning = false;
            MS2MzMinValidationMessage = string.Empty;

            ErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError;

            HideEmptyHTMLSections = false;
        }

        protected void InitializeTICAndBPI()
        {
            // Initialize TIC, BPI, and m/z vs. time arrays
            mTICAndBPIPlot.Reset();
            mTICAndBPIPlot.DeviceType = Device.MS;

            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                plotContainer.Reset();
            }
        }

        protected void InitializeLCMS2DPlot()
        {
            // Initialize var that tracks m/z vs. time
            mLCMS2DPlot.Reset();
            mLCMS2DPlotOverview.Reset();
        }

        private bool CreateOverview2DPlots(string datasetName, string outputDirectoryPath, int lcms2DOverviewPlotDivisor)
        {
            return CreateOverview2DPlots(datasetName, outputDirectoryPath, lcms2DOverviewPlotDivisor, string.Empty);
        }

        private bool CreateOverview2DPlots(string datasetName, string outputDirectoryPath, int lcms2DOverviewPlotDivisor, string scanModeSuffixAddon)
        {
            if (lcms2DOverviewPlotDivisor <= 1)
            {
                // Nothing to do; just return True
                return true;
            }

            mLCMS2DPlotOverview.Reset();

            mLCMS2DPlotOverview.Options = mLCMS2DPlot.Options.Clone();

            // Set MaxPointsToPlot in mLCMS2DPlotOverview to be lcms2DOverviewPlotDivisor times smaller
            // than the MaxPointsToPlot value in mLCMS2DPlot
            mLCMS2DPlotOverview.Options.MaxPointsToPlot = (int)Math.Round(mLCMS2DPlot.Options.MaxPointsToPlot / (double)lcms2DOverviewPlotDivisor, 0);

            // Copy the data from mLCMS2DPlot to mLCMS2DPlotOverview
            // mLCMS2DPlotOverview will auto-filter the data to track, at most, mLCMS2DPlotOverview.Options.MaxPointsToPlot points
            for (var index = 0; index <= mLCMS2DPlot.ScanCountCached - 1; index++)
            {
                var scan = mLCMS2DPlot.GetCachedScanByIndex(index);

                mLCMS2DPlotOverview.AddScanSkipFilters(scan);
            }

            // Write out the Overview 2D plot of m/z vs. intensity
            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
            var success = mLCMS2DPlotOverview.Save2DPlots(datasetName, outputDirectoryPath, "HighAbu_", scanModeSuffixAddon);

            return success;
        }

        /// <summary>
        /// Create the output files
        /// </summary>
        /// <param name="inputFileName"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns></returns>
        public override bool CreateOutputFiles(string inputFileName, string outputDirectoryPath)
        {
            bool successOverall;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                successOverall = true;
                var createQCPlotHTMLFile = false;

                if (outputDirectoryPath == null)
                    outputDirectoryPath = string.Empty;

                DirectoryInfo outputDirectory;
                if (outputDirectoryPath.Length > 0)
                {
                    // Make sure the output directory exists
                    outputDirectory = new DirectoryInfo(outputDirectoryPath);

                    if (!outputDirectory.Exists)
                    {
                        outputDirectory.Create();
                    }
                }
                else
                {
                    outputDirectory = new DirectoryInfo(".");
                }

                OnDebugEvent("Output directory path: " + outputDirectory.FullName);

                UpdatePlotWithPython();

                if (mPlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
                {
                    OnWarningEvent("Updating PlotWithPython to True in mLCMS2DPlot; this is typically set by ProcessMSDataset");
                    mLCMS2DPlot.Options.PlotWithPython = true;
                }

                if (mLCMS2DPlot.Options.DeleteTempFiles && ShowDebugInfo)
                {
                    OnWarningEvent("Updating DeleteTempFiles to False in mLCMS2DPlot; this is typically set by ProcessMSDataset");
                    mLCMS2DPlot.Options.DeleteTempFiles = false;
                }

                if (mSaveTICAndBPI || mSaveLCMS2DPlots)
                {
                    OnProgressUpdate("Saving plots", PROGRESS_SPECTRA_LOADED);
                }

                if (mSaveTICAndBPI)
                {
                    // Write out the TIC and BPI plots
                    var success = mTICAndBPIPlot.SaveTICAndBPIPlotFiles(datasetName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }

                    foreach (var plotContainer in mInstrumentSpecificPlots)
                    {
                        // Write out any instrument-specific plots
                        var success2 = plotContainer.SaveTICAndBPIPlotFiles(datasetName, outputDirectory.FullName);
                        if (!success2)
                        {
                            successOverall = false;
                        }
                    }

                    createQCPlotHTMLFile = true;

                    OnProgressUpdate("TIC and BPI plots saved", PROGRESS_SAVED_TIC_AND_BPI_PLOT);
                }

                if (mSaveLCMS2DPlots)
                {
                    // Determine the number of times we'll be calling Save2DPlots or CreateOverview2DPlots
                    var lcMSPlotStepsTotal = 1;

                    if (mLCMS2DOverviewPlotDivisor > 0)
                    {
                        lcMSPlotStepsTotal++;
                    }

                    if (mLCMS2DPlot.Options.PlottingDeisotopedData)
                    {
                        lcMSPlotStepsTotal++;
                        if (mLCMS2DOverviewPlotDivisor > 0)
                        {
                            lcMSPlotStepsTotal++;
                        }
                    }

                    // Write out the 2D plot of m/z vs. intensity
                    // Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
                    var success3 = mLCMS2DPlot.Save2DPlots(datasetName, outputDirectory.FullName);
                    var lcMSPlotStepsComplete = 1;
                    ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);

                    if (!success3)
                    {
                        successOverall = false;
                    }
                    else
                    {
                        if (mLCMS2DOverviewPlotDivisor > 0)
                        {
                            // Also save the Overview 2D Plots
                            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
                            var success4 = CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DOverviewPlotDivisor);
                            lcMSPlotStepsComplete++;
                            ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);

                            if (!success4)
                            {
                                successOverall = false;
                            }
                        }
                        else
                        {
                            mLCMS2DPlotOverview.ClearRecentFileInfo();
                        }

                        if (successOverall && mLCMS2DPlot.Options.PlottingDeisotopedData)
                        {
                            // Create two more plots 2D plots, but this with a smaller maximum m/z
                            mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;
                            mLCMS2DPlotOverview.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;

                            mLCMS2DPlot.Save2DPlots(datasetName, outputDirectory.FullName, string.Empty, "_zoom");
                            lcMSPlotStepsComplete++;
                            ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);

                            if (mLCMS2DOverviewPlotDivisor > 0)
                            {
                                CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DOverviewPlotDivisor, "_zoom");
                                lcMSPlotStepsComplete++;
                                ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);
                            }
                        }
                    }
                    createQCPlotHTMLFile = true;

                    OnProgressUpdate("2D plots saved", PROGRESS_SAVED_2D_PLOTS);
                }

                if (mCreateDatasetInfoFile)
                {
                    // Create the _DatasetInfo.xml file
                    var success = CreateDatasetInfoFile(inputFileName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                    createQCPlotHTMLFile = true;
                }

                if (mCreateScanStatsFile)
                {
                    // Create the _ScanStats.txt file
                    var success = CreateDatasetScanStatsFile(inputFileName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                }

                if (mUpdateDatasetStatsTextFile)
                {
                    // Add a new row to the MSFileInfo_DatasetStats.txt file
                    var success = UpdateDatasetStatsTextFile(inputFileName, outputDirectory.FullName, mDatasetStatsTextFileName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                }

                if (createQCPlotHTMLFile)
                {
                    var success = CreateQCPlotHTMLFile(datasetName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating output files: " + ex.Message, ex);
                successOverall = false;
            }

            return successOverall;
        }

        private bool CreateQCPlotHTMLFile(string datasetName, string outputDirectoryPath)
        {
            try
            {
                // Obtain the dataset summary stats (they will be auto-computed if not up to date)
                var summaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats();

                var htmlFilePath = Path.Combine(outputDirectoryPath, "index.html");

                using (var writer = new StreamWriter(new FileStream(htmlFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    // Add HTML headers and <table>
                    AppendHTMLHeader(writer, datasetName);

                    // First add the plots with the top 50,000 points
                    AppendLCMS2DPlots(writer, mLCMS2DPlotOverview);

                    // Now add the plots with the top 500,000 points
                    AppendLCMS2DPlots(writer, mLCMS2DPlot);

                    // Add the BPI plots
                    AppendBPIPlots(writer);

                    // Add instrument-specific plots, if defined
                    AppendAdditionalPlots(writer);

                    // Add the TIC
                    AppendTICAndSummaryStats(writer, summaryStats);

                    // Append dataset info
                    AppendDatasetInfo(writer, datasetName, outputDirectoryPath);

                    // Append device info
                    AppendDeviceInfo(writer);

                    // Add </table> and HTML footers
                    AppendHTMLFooter(writer);
                }

                return true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating QC plot HTML file: " + ex.Message, ex);
                return false;
            }

        }

        private void AppendHTMLHeader(TextWriter writer, string datasetName)
        {
            // ReSharper disable once StringLiteralTypo
            writer.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 3.2//EN\">");
            writer.WriteLine("<html>");
            writer.WriteLine("<head>");
            writer.WriteLine("  <title>" + datasetName + "</title>");
            writer.WriteLine("</head>");
            writer.WriteLine();
            writer.WriteLine("<body>");
            writer.WriteLine("  <h2>" + datasetName + "</h2>");
            writer.WriteLine();
            writer.WriteLine("  <table>");
        }

        private void AppendLCMS2DPlots(TextWriter writer, clsLCMSDataPlotter lcmsPlotter)
        {
            var file1 = lcmsPlotter.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS);

            string file2;
            if (lcmsPlotter.Options.PlottingDeisotopedData)
            {
                file2 = file1.Replace("_zoom.png", ".png");
            }
            else
            {
                file2 = lcmsPlotter.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn);
            }

            var top = IntToEngineeringNotation(lcmsPlotter.Options.MaxPointsToPlot);

            if (file1.Length > 0 || file2.Length > 0)
            {
                writer.WriteLine("    <tr>");
                writer.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
                writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                writer.WriteLine("    </tr>");
                writer.WriteLine();
            }

        }

        private void AppendBPIPlots(TextWriter writer)
        {

            var file1 = mTICAndBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
            var file2 = mTICAndBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);
            if (file1.Length > 0 || file2.Length > 0)
            {
                writer.WriteLine("    <tr>");
                writer.WriteLine("      <td valign=\"middle\">BPI</td>");
                writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                writer.WriteLine("    </tr>");
                writer.WriteLine();
            }

        }

        private void AppendAdditionalPlots(TextWriter writer)
        {
            // This dictionary tracks the QC plots, by device type
            var plotsByDeviceType = new Dictionary<Device, List<string>>();

            // Generate the HTML to display the plots and store in plotsByDeviceType
            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                var deviceType = plotContainer.DeviceType;

                var file1 = plotContainer.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC);
                var file2 = plotContainer.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                var file3 = plotContainer.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);

                if (file1.Length == 0 && file2.Length == 0 && file3.Length == 0)
                {
                    continue;
                }

                if (!plotsByDeviceType.TryGetValue(deviceType, out var plotsForDevice))
                {
                    plotsForDevice = new List<string>();
                    plotsByDeviceType.Add(deviceType, plotsForDevice);
                }

                if (file1.Length > 0)
                {
                    plotsForDevice.Add(GenerateQCFigureHTML(file1, 250));
                }

                if (file2.Length > 0)
                {
                    plotsForDevice.Add(GenerateQCFigureHTML(file2, 250));
                }

                if (file3.Length > 0)
                {
                    plotsForDevice.Add(GenerateQCFigureHTML(file3, 250));
                }

            }

            // Append rows and columns to the table for HTML in plotsByDeviceType
            foreach (var plotsForDevice in plotsByDeviceType.Values)
            {
                writer.WriteLine("    <tr>");
                writer.WriteLine("      <td valign=\"middle\">Addnl Plots</td>");

                var columnsToAdd = Math.Max(3, plotsForDevice.Count);

                for (var i = 0; i < columnsToAdd; i++)
                {
                    if (i < plotsForDevice.Count)
                        writer.WriteLine("      <td>" + plotsForDevice[i] + "</td>");
                    else
                        writer.WriteLine("      <td></td>");
                }

                writer.WriteLine("    </tr>");
                writer.WriteLine();
            }
        }

        private void AppendTICAndSummaryStats(TextWriter writer, DatasetSummaryStats summaryStats)
        {

            writer.WriteLine("    <tr>");

            if (HideEmptyHTMLSections && summaryStats.ScanTypeStats.Count == 0)
            {
                writer.WriteLine("      <td>&nbsp;</td><td style='width: 200px'>&nbsp;</td><td style='width: 200px'>&nbsp;</td>");
            }
            else
            {
                var qcFigureHTML = GenerateQCFigureHTML(mTICAndBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC), 250);

                writer.WriteLine("      <td valign=\"middle\">TIC</td>");
                writer.WriteLine("      <td>" + qcFigureHTML + "</td>");
                writer.WriteLine("      <td valign=\"middle\">");

                GenerateQCScanTypeSummaryHTML(writer, summaryStats, "        ");

                writer.WriteLine("      </td>");
            }

            writer.WriteLine("    </tr>");
        }

        private void AppendDatasetInfo(TextWriter writer, string datasetName, string outputDirectoryPath)
        {
            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td>&nbsp;</td>");
            writer.WriteLine("      <td align=\"center\">DMS <a href=\"http://dms2.pnl.gov/dataset/show/" + datasetName + "\">Dataset Detail Report</a></td>");

            var datasetInfoFileName = datasetName + DatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;
            if (mCreateDatasetInfoFile || File.Exists(Path.Combine(outputDirectoryPath, datasetInfoFileName)))
            {
                writer.WriteLine("      <td align=\"center\"><a href=\"" + datasetInfoFileName + "\">Dataset Info XML file</a></td>");
            }
            else
            {
                writer.WriteLine("      <td>&nbsp;</td>");
            }

            writer.WriteLine("    </tr>");
        }

        private void AppendDeviceInfo(TextWriter writer)
        {
            var deviceList = mDatasetStatsSummarizer.DatasetFileInfo.DeviceList;

            if (deviceList.Count == 0)
                return;

            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td>&nbsp;</td>");
            writer.WriteLine("      <td align=\"left\">" + GetDeviceTableHtml(deviceList.First()) + "</td>");

            // Display additional devices, if defined

            if (deviceList.Count > 1)
            {
                // In Thermo files, the same device might be listed more than once in deviceList, e.g. if an LC is tracking pressure from two different locations in the pump
                // This SortedSet is used to avoid displaying the same device twice
                var devicesDisplayed = new SortedSet<string>();

                writer.WriteLine("      <td align=\"left\">");

                foreach (var device in deviceList.Skip(1))
                {
                    var deviceKey = string.Format("{0}_{1}_{2}", device.InstrumentName, device.Model, device.SerialNumber);

                    if (devicesDisplayed.Contains(deviceKey))
                        continue;

                    devicesDisplayed.Add(deviceKey);
                    writer.WriteLine(GetDeviceTableHtml(device));
                }

                writer.WriteLine("</td>");
            }
            else
            {
                writer.WriteLine("      <td>&nbsp;</td>");
            }

            writer.WriteLine("    </tr>");
        }

        private void AppendHTMLFooter(TextWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine("  </table>");
            writer.WriteLine();
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
            writer.WriteLine();

        }

        private string GenerateQCFigureHTML(string filename, int widthPixels)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return "&nbsp;";
            }

            return string.Format("<a href=\"{0}\"><img src=\"{0}\" width=\"{1}\" border=\"0\"></a>", filename, widthPixels);
        }

        private void GenerateQCScanTypeSummaryHTML(TextWriter writer, DatasetSummaryStats datasetSummaryStats, string indent)
        {
            if (indent == null)
                indent = string.Empty;

            writer.WriteLine(indent + @"<table border=""1"">");
            writer.WriteLine(indent + "  <tr><th>Scan Type</th><th>Scan Count</th><th>Scan Filter Text</th></tr>");

            foreach (var scanTypeEntry in datasetSummaryStats.ScanTypeStats)
            {
                var scanType = scanTypeEntry.Key;
                var indexMatch = scanType.IndexOf(DatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR, StringComparison.Ordinal);

                string scanFilterText;
                if (indexMatch >= 0)
                {
                    scanFilterText = scanType.Substring(indexMatch + DatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR.Length);
                    if (indexMatch > 0)
                    {
                        scanType = scanType.Substring(0, indexMatch);
                    }
                    else
                    {
                        scanType = string.Empty;
                    }
                }
                else
                {
                    scanFilterText = string.Empty;
                }
                var scanCount = scanTypeEntry.Value;

                writer.WriteLine(indent + "  <tr><td>" + scanType + "</td>" + "<td align=\"center\">" + scanCount + "</td>" + "<td>" + scanFilterText + "</td></tr>");

            }

            writer.WriteLine(indent + "</table>");

        }

        private string GetDeviceTableHtml(DeviceInfo device)
        {

            var html = string.Format(
                "<table border=0>" +
                " <tr><td>Device Type:</td><td>{0}</td></tr>" +
                " <tr><td>Name:</td><td>{1}</td></tr>" +
                " <tr><td>Model:</td><td>{2}</td></tr>" +
                " <tr><td>Serial:</td><td>{3}</td></tr>" +
                " <tr><td>Software Version:</td><td>{4}</td></tr>" +
                "</table>",
                device.DeviceDescription,
                device.InstrumentName,
                device.Model,
                device.SerialNumber,
                device.SoftwareVersion);

            return html;
        }

        /// <summary>
        /// Converts an integer to engineering notation
        /// For example, 50000 will be returned as 50K
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private string IntToEngineeringNotation(int value)
        {
            if (value < 1000)
            {
                return value.ToString();
            }

            if (value < 1000000.0)
            {
                return (int)Math.Round(value / 1000.0, 0) + "K";
            }

            return (int)Math.Round(value / 1000.0 / 1000, 0) + "M";
        }

        [HandleProcessCorruptedStateExceptions]
        protected void LoadScanDataWithProteoWizard(
            FileSystemInfo datasetFileOrDirectory,
            DatasetFileInfo datasetFileInfo,
            bool skipScansWithNoIons,
            bool highResMS1 = true,
            bool highResMS2 = true)
        {

            try
            {
                // Open the instrument data using the ProteoWizardWrapper

                var pWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(datasetFileOrDirectory.FullName);

                try
                {
                    var runStartTime = Convert.ToDateTime(pWiz.RunStartTime);

                    // Update AcqTimeEnd if possible
                    // Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
                    runStartTime = runStartTime.ToUniversalTime();
                    if (runStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                        {
                            if (datasetFileInfo.AcqTimeStart == DateTime.MinValue ||
                                Math.Abs(datasetFileInfo.AcqTimeStart.Subtract(runStartTime).TotalSeconds) > 0)
                            {
                                datasetFileInfo.AcqTimeStart = runStartTime;
                            }

                        }
                    }

                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the ProteoWizard Data Parser class
                var pWizParser = new clsProteoWizardDataParser(pWiz, mDatasetStatsSummarizer, mTICAndBPIPlot,
                                                               mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI,
                                                               mCheckCentroidingStatus)
                {
                    HighResMS1 = highResMS1,
                    HighResMS2 = highResMS2
                };

                RegisterEvents(pWizParser);

                var ticStored = false;
                var srmDataCached = false;
                double runtimeMinutes = 0;
                // Note that SRM .Wiff files will only have chromatograms, and no spectra

                if (pWiz.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                if (pWiz.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    var skipExistingScans = (pWiz.ChromatogramCount > 0);
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans, skipScansWithNoIons,
                                                  maxScansToTrackInDetail: MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                pWiz.Dispose();
                ProgRunner.GarbageCollectNow();

            }
            catch (AccessViolationException)
            {
                // Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
                OnWarningEvent("Error reading instrument data with ProteoWizard: Attempted to read or write protected memory. " +
                               "The instrument data file is likely corrupt.");
                mDatasetStatsSummarizer.CreateEmptyScanStatsFiles = false;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error using ProteoWizard reader", ex);
            }
        }

        protected void PostProcessTasks()
        {
            ShowInstrumentFiles();
        }

        private void ReportProgressSaving2DPlots(int lcMSPlotStepsComplete, int lcMSPlotStepsTotal)
        {
            OnProgressUpdate("Saving 2D plots ", ComputeIncrementalProgress(
                                 PROGRESS_SAVED_TIC_AND_BPI_PLOT,
                                 PROGRESS_SAVED_2D_PLOTS,
                                 lcMSPlotStepsComplete / (float)lcMSPlotStepsTotal * 100));
        }

        protected void ResetResults()
        {
            MS2MzMinValidationError = false;
            MS2MzMinValidationWarning = false;
            MS2MzMinValidationMessage = string.Empty;
        }

        /// <summary>
        /// Display the instrument file names and stats at the console or via OnDebugEvent
        /// </summary>
        protected void ShowInstrumentFiles()
        {
            if (mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles.Count <= 0)
                return;

            var fileInfo = new StringBuilder();

            if (mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles.Count == 1)
                fileInfo.AppendLine("Primary instrument file");
            else
                fileInfo.AppendLine("Primary instrument files");

            foreach (var instrumentFile in mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles)
            {
                fileInfo.AppendLine(string.Format("  {0}  {1,-30}  {2,12:N0} bytes",
                                                  instrumentFile.Value.Hash, instrumentFile.Key, instrumentFile.Value.Length));
            }

            OnDebugEvent(fileInfo.ToString());
        }

        /// <summary>
        /// Store the creation time and last write time of the instrument file in mDatasetStatsSummarizer.DatasetFileInfo
        /// Initialize the Acquisition start/end times using to the last write time
        /// Computes the SHA-1 hash of the instrument file
        /// </summary>
        /// <param name="instrumentFile"></param>
        /// <param name="datasetID"></param>
        /// <returns>True if success, false if an error</returns>
        protected bool UpdateDatasetFileStats(FileInfo instrumentFile, int datasetID)
        {
            return UpdateDatasetFileStats(instrumentFile, datasetID, out _);
        }

        /// <summary>
        /// Store the creation time and last write time of the instrument file in mDatasetStatsSummarizer.DatasetFileInfo
        /// Initialize the Acquisition start/end times using to the last write time
        /// Computes the SHA-1 hash of the instrument file
        /// </summary>
        /// <param name="instrumentFile"></param>
        /// <param name="datasetID"></param>
        /// <param name="fileAdded"></param>
        /// <returns>True if success, false if an error</returns>
        protected bool UpdateDatasetFileStats(FileInfo instrumentFile, int datasetID, out bool fileAdded)
        {
            fileAdded = false;

            try
            {
                if (!instrumentFile.Exists)
                    return false;

                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = instrumentFile.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = instrumentFile.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(instrumentFile.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = instrumentFile.Extension;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = instrumentFile.Length;

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

                if (mDisableInstrumentHash)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(instrumentFile);
                }
                else
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(instrumentFile);
                }
                fileAdded = true;

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        /// <summary>
        /// Store the creation time and last write time of the first primary data file (or of the dataset directory) in mDatasetStatsSummarizer.DatasetFileInfo
        /// Initialize the Acquisition start/end times using to the last write time
        /// Computes the SHA-1 hash of the primary data file
        /// </summary>
        /// <param name="datasetDirectory">Dataset directory</param>
        /// <param name="primaryDataFile">Primary data file (not required to exist on disk)</param>
        /// <param name="datasetID">Dataset ID</param>
        /// <returns>True if success, false if an error</returns>
        protected bool UpdateDatasetFileStats(DirectoryInfo datasetDirectory, FileInfo primaryDataFile, int datasetID)
        {
            var primaryDataFiles = new List<FileInfo>();
            if (primaryDataFile != null)
            {
                primaryDataFiles.Add(primaryDataFile);
            }

            return UpdateDatasetFileStats(datasetDirectory, primaryDataFiles, datasetID);
        }

        /// <summary>
        /// Store the creation time and last write time of the first primary data file (or of the dataset directory) in mDatasetStatsSummarizer.DatasetFileInfo
        /// Initialize the Acquisition start/end times using to the last write time
        /// Computes the SHA-1 hash of the files in primaryDataFiles
        /// </summary>
        /// <param name="datasetDirectory">Dataset directory</param>
        /// <param name="primaryDataFiles">Primary data files (not required to exist on disk)</param>
        /// <param name="datasetID">Dataset ID</param>
        /// <returns>True if success, false if an error</returns>
        protected bool UpdateDatasetFileStats(DirectoryInfo datasetDirectory, List<FileInfo> primaryDataFiles, int datasetID)
        {
            try
            {
                if (!datasetDirectory.Exists)
                    return false;

                if (primaryDataFiles.Count > 0 && primaryDataFiles[0].Exists)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = primaryDataFiles[0].CreationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = primaryDataFiles[0].LastWriteTime;
                }
                else
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = datasetDirectory.CreationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = datasetDirectory.LastWriteTime;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(datasetDirectory.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = datasetDirectory.Extension;

                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = 0;
                foreach (var outputFile in datasetDirectory.GetFiles("*", SearchOption.AllDirectories))
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes += outputFile.Length;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

                foreach (var dataFile in primaryDataFiles)
                {
                    if (!dataFile.Exists)
                        continue;

                    if (mDisableInstrumentHash)
                    {
                        mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(dataFile);
                    }
                    else
                    {
                        // Compute the SHA-1 hash for the first file
                        mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(dataFile);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        protected void UpdateDatasetStatsSummarizerUsingDatasetFileInfo(DatasetFileInfo datasetFileInfo, bool copyFileSystemTimes = true)
        {
            if (copyFileSystemTimes)
            {
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = datasetFileInfo.FileSystemCreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = datasetFileInfo.FileSystemModificationTime;
            }

            mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

            mDatasetStatsSummarizer.DatasetFileInfo.DeviceList.AddRange(datasetFileInfo.DeviceList);

        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and Me.DatasetFileInfo)
        /// </summary>
        /// <param name="inputFileName">Input file name</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        // ReSharper disable once UnusedMember.Global
        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputDirectoryPath)
        {
            return UpdateDatasetStatsTextFile(inputFileName, outputDirectoryPath, DatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME);
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and Me.DatasetFileInfo)
        /// </summary>
        /// <param name="inputFileName">Input file name</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="datasetStatsFilename">Dataset stats file name</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputDirectoryPath, string datasetStatsFilename)
        {

            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);

                var datasetStatsFilePath = Path.Combine(outputDirectoryPath, datasetStatsFilename);

                success = mDatasetStatsSummarizer.UpdateDatasetStatsTextFile(datasetName, datasetStatsFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling datasetStatsSummarizer.UpdateDatasetStatsTextFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error updating the dataset stats text file", ex);
                success = false;
            }

            return success;

        }

        private void UpdatePlotWithPython()
        {
            mTICAndBPIPlot.PlotWithPython = mPlotWithPython;

            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                plotContainer.PlotWithPython = mPlotWithPython;
            }

            if (mPlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
            {
                // This code shouldn't be reached; this setting should have already propagated
                mLCMS2DPlot.Options.PlotWithPython = mPlotWithPython;
                mLCMS2DPlotOverview.Options.PlotWithPython = mPlotWithPython;
            }

            mTICAndBPIPlot.DeleteTempFiles = !ShowDebugInfo;

            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                plotContainer.DeleteTempFiles = !ShowDebugInfo;
            }

            if (mPlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
            {
                // This code shouldn't be reached; this setting should have already propagated
                mLCMS2DPlot.Options.DeleteTempFiles = !ShowDebugInfo;
                mLCMS2DPlotOverview.Options.DeleteTempFiles = !ShowDebugInfo;
            }

        }

        /// <summary>
        /// Examine the minimum m/z value in MS2 spectra
        /// Keep track of the number of spectra where the minimum m/z value is greater than MS2MzMin
        /// Raise an error if at least 10% of the spectra have a minimum m/z higher than the threshold
        /// Log a warning if some spectra, but fewer than 10% of the total, have a minimum higher than the threshold
        /// </summary>
        /// <returns>True if valid data, false if at least 10% of the spectra has a minimum m/z higher than the threshold</returns>
        protected bool ValidateMS2MzMin()
        {

            var validData = mDatasetStatsSummarizer.ValidateMS2MzMin(MS2MzMin, out var errorOrWarningMsg, MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED);

            if (validData && string.IsNullOrWhiteSpace(errorOrWarningMsg))
                return true;

            if (validData)
            {
                OnWarningEvent(errorOrWarningMsg);
                MS2MzMinValidationWarning = true;
                MS2MzMinValidationMessage = errorOrWarningMsg;
            }
            else
            {
                OnErrorEvent(errorOrWarningMsg);
                MS2MzMinValidationError = true;
                MS2MzMinValidationMessage = errorOrWarningMsg;
            }

            return validData;
        }

    }
}

