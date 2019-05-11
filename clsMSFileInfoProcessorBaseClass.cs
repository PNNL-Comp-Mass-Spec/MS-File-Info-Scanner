using System;
using System.IO;
using System.Linq;
using System.Text;
using MSFileInfoScannerInterfaces;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2007
//

namespace MSFileInfoScanner
{
    public abstract class clsMSFileInfoProcessorBaseClass : iMSFileInfoProcessor
    {

        /// <summary>
        /// Constructor
        /// </summary>
        protected clsMSFileInfoProcessorBaseClass()
        {
            mTICAndBPIPlot = new clsTICandBPIPlotter("TICAndBPIPlot", true);
            RegisterEvents(mTICAndBPIPlot);

            mInstrumentSpecificPlots = new clsTICandBPIPlotter("InstrumentSpecificPlots", true);
            RegisterEvents(mInstrumentSpecificPlots);

            mDatasetStatsSummarizer = new clsDatasetStatsSummarizer();
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

        protected readonly clsTICandBPIPlotter mTICAndBPIPlot;

        protected readonly clsTICandBPIPlotter mInstrumentSpecificPlots;

        protected readonly clsLCMSDataPlotter mLCMS2DPlot;

        private readonly clsLCMSDataPlotter mLCMS2DPlotOverview;

        protected readonly clsDatasetStatsSummarizer mDatasetStatsSummarizer;

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
        protected bool HideEmptyHtmlSections { get; set; }

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

        private bool CreateDatasetInfoFile(string inputFileName, string outputDirectoryPath)
        {

            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var datasetInfoFilePath = Path.Combine(outputDirectoryPath, datasetName);
                datasetInfoFilePath += clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

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

                success = mDatasetStatsSummarizer.CreateScanStatsFile(datasetName, scanStatsFilePath);

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
            mDatasetStatsTextFileName = clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME;

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

            HideEmptyHtmlSections = false;
        }

        protected void InitializeTICAndBPI()
        {
            // Initialize TIC, BPI, and m/z vs. time arrays
            mTICAndBPIPlot.Reset();
            mInstrumentSpecificPlots.Reset();
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
                var createQCPlotHtmlFile = false;

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

                bool success;
                if (mSaveTICAndBPI)
                {
                    // Write out the TIC and BPI plots
                    success = mTICAndBPIPlot.SaveTICAndBPIPlotFiles(datasetName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }

                    // Write out any instrument-specific plots
                    success = mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles(datasetName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }

                    createQCPlotHtmlFile = true;
                }

                if (mSaveLCMS2DPlots)
                {
                    // Write out the 2D plot of m/z vs. intensity
                    // Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
                    success = mLCMS2DPlot.Save2DPlots(datasetName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                    else
                    {
                        if (mLCMS2DOverviewPlotDivisor > 0)
                        {
                            // Also save the Overview 2D Plots
                            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
                            success = CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DOverviewPlotDivisor);
                            if (!success)
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

                            mLCMS2DPlot.Save2DPlots(datasetName, outputDirectory.FullName, "", "_zoom");
                            if (mLCMS2DOverviewPlotDivisor > 0)
                            {
                                CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DOverviewPlotDivisor, "_zoom");
                            }
                        }
                    }
                    createQCPlotHtmlFile = true;
                }

                if (mCreateDatasetInfoFile)
                {
                    // Create the _DatasetInfo.xml file
                    success = CreateDatasetInfoFile(inputFileName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                    createQCPlotHtmlFile = true;
                }

                if (mCreateScanStatsFile)
                {
                    // Create the _ScanStats.txt file
                    success = CreateDatasetScanStatsFile(inputFileName, outputDirectory.FullName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                }

                if (mUpdateDatasetStatsTextFile)
                {
                    // Add a new row to the MSFileInfo_DatasetStats.txt file
                    success = UpdateDatasetStatsTextFile(inputFileName, outputDirectory.FullName, mDatasetStatsTextFileName);
                    if (!success)
                    {
                        successOverall = false;
                    }
                }

                if (createQCPlotHtmlFile)
                {
                    success = CreateQCPlotHTMLFile(datasetName, outputDirectory.FullName);
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

                    // ReSharper disable once StringLiteralTypo
                    writer.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 3.2//EN\">");
                    writer.WriteLine("<html>");
                    writer.WriteLine("<head>");
                    writer.WriteLine("  <title>" + datasetName + "</title>");
                    writer.WriteLine("</head>");
                    writer.WriteLine("");
                    writer.WriteLine("<body>");
                    writer.WriteLine("  <h2>" + datasetName + "</h2>");
                    writer.WriteLine("");
                    writer.WriteLine("  <table>");

                    // First the plots with the top 50,000 points
                    var file1 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS);

                    string file2;
                    if (mLCMS2DPlotOverview.Options.PlottingDeisotopedData)
                    {
                        file2 = file1.Replace("_zoom.png", ".png");
                    }
                    else
                    {
                        file2 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn);
                    }

                    var top = IntToEngineeringNotation(mLCMS2DPlotOverview.Options.MaxPointsToPlot);

                    if (file1.Length > 0 || file2.Length > 0)
                    {
                        writer.WriteLine("    <tr>");
                        writer.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        writer.WriteLine("    </tr>");
                        writer.WriteLine("");
                    }

                    // Now the plots with the top 500,000 points
                    file1 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS);

                    if (mLCMS2DPlotOverview.Options.PlottingDeisotopedData)
                    {
                        file2 = file1.Replace("_zoom.png", ".png");
                    }
                    else
                    {
                        file2 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn);
                    }

                    top = IntToEngineeringNotation(mLCMS2DPlot.Options.MaxPointsToPlot);

                    if (file1.Length > 0 || file2.Length > 0)
                    {
                        writer.WriteLine("    <tr>");
                        writer.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        writer.WriteLine("    </tr>");
                        writer.WriteLine("");
                    }

                    file1 = mTICAndBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    file2 = mTICAndBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);
                    if (file1.Length > 0 || file2.Length > 0)
                    {
                        writer.WriteLine("    <tr>");
                        writer.WriteLine("      <td valign=\"middle\">BPI</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        writer.WriteLine("    </tr>");
                        writer.WriteLine("");
                    }

                    file1 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC);
                    file2 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    var file3 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);

                    if (file1.Length > 0 || file2.Length > 0 || file3.Length > 0)
                    {
                        writer.WriteLine("    <tr>");
                        writer.WriteLine("      <td valign=\"middle\">Addnl Plots</td>");
                        if (file1.Length > 0)
                            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        else
                            writer.WriteLine("      <td></td>");
                        if (file2.Length > 0)
                            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        else
                            writer.WriteLine("      <td></td>");
                        if (file3.Length > 0)
                            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file3, 250) + "</td>");
                        else
                            writer.WriteLine("      <td></td>");
                        writer.WriteLine("    </tr>");
                        writer.WriteLine("");
                    }

                    writer.WriteLine("    <tr>");

                    if (HideEmptyHtmlSections && summaryStats.ScanTypeStats.Count == 0)
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

                    writer.WriteLine("    <tr>");
                    writer.WriteLine("      <td>&nbsp;</td>");
                    writer.WriteLine("      <td align=\"center\">DMS <a href=\"http://dms2.pnl.gov/dataset/show/" + datasetName + "\">Dataset Detail Report</a></td>");

                    var datasetInfoFileName = datasetName + clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;
                    if (mCreateDatasetInfoFile || File.Exists(Path.Combine(outputDirectoryPath, datasetInfoFileName)))
                    {
                        writer.WriteLine("      <td align=\"center\"><a href=\"" + datasetInfoFileName + "\">Dataset Info XML file</a></td>");
                    }
                    else
                    {
                        writer.WriteLine("      <td>&nbsp;</td>");
                    }

                    writer.WriteLine("    </tr>");

                    writer.WriteLine("");
                    writer.WriteLine("  </table>");
                    writer.WriteLine("");
                    writer.WriteLine("</body>");
                    writer.WriteLine("</html>");
                    writer.WriteLine("");

                }

                return true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating QC plot HTML file: " + ex.Message, ex);
                return false;
            }

        }

        private string GenerateQCFigureHTML(string filename, int widthPixels)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return "&nbsp;";
            }

            return string.Format("<a href=\"{0}\"><img src=\"{0}\" width=\"{1}\" border=\"0\"></a>", filename, widthPixels);
        }

        private void GenerateQCScanTypeSummaryHTML(TextWriter writer, clsDatasetSummaryStats datasetSummaryStats, string indent)
        {
            if (indent == null)
                indent = string.Empty;

            writer.WriteLine(indent + @"<table border=""1"">");
            writer.WriteLine(indent + "  <tr><th>Scan Type</th><th>Scan Count</th><th>Scan Filter Text</th></tr>");

            foreach (var scanTypeEntry in datasetSummaryStats.ScanTypeStats)
            {
                var scanType = scanTypeEntry.Key;
                var indexMatch = scanType.IndexOf(clsDatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR, StringComparison.Ordinal);

                string scanFilterText;
                if (indexMatch >= 0)
                {
                    scanFilterText = scanType.Substring(indexMatch + clsDatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR.Length);
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

        protected void PostProcessTasks()
        {
            ShowInstrumentFiles();
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
            if (mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles.Count <= 0) return;

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

        protected bool UpdateDatasetFileStats(FileInfo instrumentFile, int datasetID)
        {
            return UpdateDatasetFileStats(instrumentFile, datasetID, out _);
        }

        protected bool UpdateDatasetFileStats(FileInfo instrumentFile, int datasetID, out bool fileAdded)
        {

            fileAdded = false;
            try
            {
                if (!instrumentFile.Exists)
                    return false;

                // Record the file size and Dataset ID
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

        protected bool UpdateDatasetFileStats(DirectoryInfo outputDirectory, int datasetID)
        {

            try
            {
                if (!outputDirectory.Exists)
                    return false;

                // Record the file size and Dataset ID
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = outputDirectory.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = outputDirectory.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(outputDirectory.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = outputDirectory.Extension;

                foreach (var outputFile in outputDirectory.GetFiles("*", SearchOption.AllDirectories))
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes += outputFile.Length;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

            }
            catch (Exception)
            {
                return false;
            }

            return true;

        }

        protected void UpdateDatasetStatsSummarizerUsingDatasetFileInfo(clsDatasetFileInfo datasetFileInfo)
        {
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = datasetFileInfo.FileSystemCreationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = datasetFileInfo.FileSystemModificationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
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
            return UpdateDatasetStatsTextFile(inputFileName, outputDirectoryPath, clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME);
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
                OnErrorEvent("Error updating the dataset stats text file: " + ex.Message, ex);
                success = false;
            }

            return success;

        }

        private void UpdatePlotWithPython()
        {
            mTICAndBPIPlot.PlotWithPython = mPlotWithPython;
            mInstrumentSpecificPlots.PlotWithPython = mPlotWithPython;

            if (mPlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
            {
                // This code shouldn't be reached; this setting should have already propagated
                mLCMS2DPlot.Options.PlotWithPython = mPlotWithPython;
                mLCMS2DPlotOverview.Options.PlotWithPython = mPlotWithPython;
            }

            mTICAndBPIPlot.DeleteTempFiles = !ShowDebugInfo;
            mInstrumentSpecificPlots.DeleteTempFiles = !ShowDebugInfo;

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

