using System;
using System.IO;
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
            mTICandBPIPlot = new clsTICandBPIPlotter();
            mInstrumentSpecificPlots = new clsTICandBPIPlotter();

            mDatasetStatsSummarizer = new clsDatasetStatsSummarizer();
            RegisterEvents(mDatasetStatsSummarizer);

            mLCMS2DPlot = new clsLCMSDataPlotter();
            RegisterEvents(mLCMS2DPlot);

            mLCMS2DPlotOverview = new clsLCMSDataPlotter();
            RegisterEvents(mLCMS2DPlotOverview);

            InitializeLocalVariables();
        }

        #region "Constants"

        #endregion

        #region "Member variables"
        protected bool mSaveTICAndBPI;
        protected bool mSaveLCMS2DPlots;

        protected bool mCheckCentroidingStatus;
        protected bool mComputeOverallQualityScores;

        // When True, then creates an XML file with dataset info
        protected bool mCreateDatasetInfoFile;

        // When True, then creates a _ScanStats.txt file
        protected bool mCreateScanStatsFile;

        private int mLCMS2DOverviewPlotDivisor;

        // When True, then adds a new row to a tab-delimited text file that has dataset stats
        private bool mUpdateDatasetStatsTextFile;

        private string mDatasetStatsTextFileName;
        private int mScanStart;
        private int mScanEnd;

        protected bool mShowDebugInfo;

        private int mDatasetID;

        protected bool mCopyFileLocalOnReadError;
        protected readonly clsTICandBPIPlotter mTICandBPIPlot;

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

        public override clsLCMSDataPlotterOptions LCMS2DPlotOptions
        {
            get => mLCMS2DPlot.Options;
            set
            {
                mLCMS2DPlot.Options = value;
                mLCMS2DPlotOverview.Options = value.Clone();
            }
        }

        public override int LCMS2DOverviewPlotDivisor
        {
            get => mLCMS2DOverviewPlotDivisor;
            set => mLCMS2DOverviewPlotDivisor = value;
        }

        public override int ScanStart
        {
            get => mScanStart;
            set => mScanStart = value;
        }

        public override bool ShowDebugInfo
        {
            get => mShowDebugInfo;
            set => mShowDebugInfo = value;
        }

        /// <summary>
        /// When ScanEnd is > 0, then will stop processing at the specified scan number
        /// </summary>
        public override int ScanEnd
        {
            get => mScanEnd;
            set => mScanEnd = value;
        }

        #endregion

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
            }

            throw new Exception("Unrecognized option, " + eOption);
        }

        public override void SetOption(ProcessingOptions eOption, bool blnValue)
        {
            switch (eOption) {
                case ProcessingOptions.CreateTICAndBPI:
                    mSaveTICAndBPI = blnValue;
                    break;
                case ProcessingOptions.ComputeOverallQualityScores:
                    mComputeOverallQualityScores = blnValue;
                    break;
                case ProcessingOptions.CreateDatasetInfoFile:
                    mCreateDatasetInfoFile = blnValue;
                    break;
                case ProcessingOptions.CreateLCMS2DPlots:
                    mSaveLCMS2DPlots = blnValue;
                    break;
                case ProcessingOptions.CopyFileLocalOnReadError:
                    mCopyFileLocalOnReadError = blnValue;
                    break;
                case ProcessingOptions.UpdateDatasetStatsTextFile:
                    mUpdateDatasetStatsTextFile = blnValue;
                    break;
                case ProcessingOptions.CreateScanStatsFile:
                    mCreateScanStatsFile = blnValue;
                    break;
                case ProcessingOptions.CheckCentroidingStatus:
                    mCheckCentroidingStatus = blnValue;
                    break;
                default:
                    throw new Exception("Unrecognized option, " + eOption);
            }

        }

        private bool CreateDatasetInfoFile(string inputFileName, string outputFolderPath)
        {

            bool blnSuccess;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var datasetInfoFilePath = Path.Combine(outputFolderPath, datasetName);
                datasetInfoFilePath += clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                blnSuccess = mDatasetStatsSummarizer.CreateDatasetInfoFile(datasetName, datasetInfoFilePath);

                if (!blnSuccess)
                {
                    OnErrorEvent("Error calling objDatasetStatsSummarizer.CreateDatasetInfoFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating dataset info file: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool CreateDatasetScanStatsFile(string inputFileName, string outputFolderPath)
        {

            bool blnSuccess;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var scanStatsFilePath = Path.Combine(outputFolderPath, datasetName) + "_ScanStats.txt";

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                blnSuccess = mDatasetStatsSummarizer.CreateScanStatsFile(datasetName, scanStatsFilePath);

                if (!blnSuccess)
                {
                    OnErrorEvent("Error calling objDatasetStatsSummarizer.CreateScanStatsFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating dataset ScanStats file: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputFolderPath)
        {

            return UpdateDatasetStatsTextFile(inputFileName, outputFolderPath, clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME);

        }

        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputFolderPath, string datasetStatsFilename)
        {

            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);

                var datasetStatsFilePath = Path.Combine(outputFolderPath, datasetStatsFilename);

                success = mDatasetStatsSummarizer.UpdateDatasetStatsTextFile(datasetName, datasetStatsFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling objDatasetStatsSummarizer.UpdateDatasetStatsTextFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error updating the dataset stats text file: " + ex.Message, ex);
                success = false;
            }

            return success;

        }

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
        /// <param name="scanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, scanCount)</param>
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
        /// <param name="scanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, scanCount)</param>
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
            mShowDebugInfo = false;

            mDatasetID = 0;

            mCopyFileLocalOnReadError = false;

        }

        protected void InitializeTICAndBPI()
        {
            // Initialize TIC, BPI, and m/z vs. time arrays
            mTICandBPIPlot.Reset();
            mInstrumentSpecificPlots.Reset();
        }

        protected void InitializeLCMS2DPlot()
        {
            // Initialize var that tracks m/z vs. time
            mLCMS2DPlot.Reset();
            mLCMS2DPlotOverview.Reset();
        }

        protected void ShowProgress(int scanNumber, int scanCount, ref DateTime dtLastProgressTime, int modulusValue = 100, int detailedUpdateIntervalSeconds = 30)
        {
            if (modulusValue < 1)
                modulusValue = 10;
            if (detailedUpdateIntervalSeconds < 5)
                detailedUpdateIntervalSeconds = 15;

            if (scanNumber % modulusValue != 0)
            {
                return;
            }

            if (!mShowDebugInfo)
            {
                Console.Write(".");
            }

            if (scanCount <= 0)
            {
                return;
            }

            var sngProgress = scanNumber / (float)scanCount * 100;

            if (!(DateTime.UtcNow.Subtract(dtLastProgressTime).TotalSeconds > detailedUpdateIntervalSeconds))
            {
                return;
            }

            dtLastProgressTime = DateTime.UtcNow;
            var percentComplete = sngProgress.ToString("0.0") + "% ";

            if (mShowDebugInfo)
            {
                Console.WriteLine(percentComplete);
            }
            else
            {
                Console.WriteLine();
                Console.Write(percentComplete);
            }
        }

        protected bool UpdateDatasetFileStats(FileInfo fiFileInfo, int datasetID)
        {

            try
            {
                if (!fiFileInfo.Exists)
                    return false;

                // Record the file size and Dataset ID
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = fiFileInfo.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = fiFileInfo.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(fiFileInfo.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = fiFileInfo.Extension;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = fiFileInfo.Length;

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

            }
            catch (Exception)
            {
                return false;
            }

            return true;

        }

        protected bool UpdateDatasetFileStats(DirectoryInfo diFolderInfo, int datasetID)
        {

            try
            {
                if (!diFolderInfo.Exists)
                    return false;

                // Record the file size and Dataset ID
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = diFolderInfo.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = diFolderInfo.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(diFolderInfo.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = diFolderInfo.Extension;

                foreach (var fiFileInfo in diFolderInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes += fiFileInfo.Length;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

            }
            catch (Exception)
            {
                return false;
            }

            return true;

        }

        private bool CreateOverview2DPlots(string datasetName, string outputFolderPath, int lcms2DOverviewPlotDivisor)
        {

            return CreateOverview2DPlots(datasetName, outputFolderPath, lcms2DOverviewPlotDivisor, string.Empty);

        }

        private bool CreateOverview2DPlots(string datasetName, string outputFolderPath, int lcms2DOverviewPlotDivisor, string scanModeSuffixAddon)
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
                var objScan = mLCMS2DPlot.GetCachedScanByIndex(index);

                mLCMS2DPlotOverview.AddScanSkipFilters(objScan);
            }

            // Write out the Overview 2D plot of m/z vs. intensity
            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
            var blnSuccess = mLCMS2DPlotOverview.Save2DPlots(datasetName, outputFolderPath, "HighAbu_", scanModeSuffixAddon);

            return blnSuccess;

        }

        public override bool CreateOutputFiles(string inputFileName, string outputFolderPath)
        {
            bool successOverall;

            try
            {
                var strDatasetName = GetDatasetNameViaPath(inputFileName);
                successOverall = true;
                var blnCreateQCPlotHtmlFile = false;

                if (outputFolderPath == null)
                    outputFolderPath = string.Empty;

                DirectoryInfo diFolderInfo;
                if (outputFolderPath.Length > 0) {
                    // Make sure the output folder exists
                    diFolderInfo = new DirectoryInfo(outputFolderPath);

                    if (!diFolderInfo.Exists) {
                        diFolderInfo.Create();
                    }
                } else {
                    diFolderInfo = new DirectoryInfo(".");
                }

                bool blnSuccess;
                if (mSaveTICAndBPI)
                {
                    // Write out the TIC and BPI plots
                    blnSuccess = mTICandBPIPlot.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }

                    // Write out any instrument-specific plots
                    blnSuccess = mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }

                    blnCreateQCPlotHtmlFile = true;
                }

                if (mSaveLCMS2DPlots)
                {
                    // Write out the 2D plot of m/z vs. intensity
                    // Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
                    blnSuccess = mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }
                    else
                    {
                        if (mLCMS2DOverviewPlotDivisor > 0)
                        {
                            // Also save the Overview 2D Plots
                            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
                            blnSuccess = CreateOverview2DPlots(strDatasetName, outputFolderPath, mLCMS2DOverviewPlotDivisor);
                            if (!blnSuccess)
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

                            mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName, "", "_zoom");
                            if (mLCMS2DOverviewPlotDivisor > 0)
                            {
                                CreateOverview2DPlots(strDatasetName, outputFolderPath, mLCMS2DOverviewPlotDivisor, "_zoom");
                            }
                        }
                    }
                    blnCreateQCPlotHtmlFile = true;
                }

                if (mCreateDatasetInfoFile)
                {
                    // Create the _DatasetInfo.xml file
                    blnSuccess = CreateDatasetInfoFile(inputFileName, diFolderInfo.FullName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }
                    blnCreateQCPlotHtmlFile = true;
                }

                if (mCreateScanStatsFile)
                {
                    // Create the _ScanStats.txt file
                    blnSuccess = CreateDatasetScanStatsFile(inputFileName, diFolderInfo.FullName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }
                }

                if (mUpdateDatasetStatsTextFile)
                {
                    // Add a new row to the MSFileInfo_DatasetStats.txt file
                    blnSuccess = UpdateDatasetStatsTextFile(inputFileName, diFolderInfo.FullName, mDatasetStatsTextFileName);
                    if (!blnSuccess)
                    {
                        successOverall = false;
                    }
                }

                if (blnCreateQCPlotHtmlFile)
                {
                    blnSuccess = CreateQCPlotHTMLFile(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess)
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

        private bool CreateQCPlotHTMLFile(string datasetName, string outputFolderPath)
        {
            try
            {
                // Obtain the dataset summary stats (they will be auto-computed if not up to date)
                var objSummaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats();

                var strHTMLFilePath = Path.Combine(outputFolderPath, "index.html");

                using (var swOutFile = new StreamWriter(new FileStream(strHTMLFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {

                    swOutFile.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 3.2//EN\">");
                    swOutFile.WriteLine("<html>");
                    swOutFile.WriteLine("<head>");
                    swOutFile.WriteLine("  <title>" + datasetName + "</title>");
                    swOutFile.WriteLine("</head>");
                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("<body>");
                    swOutFile.WriteLine("  <h2>" + datasetName + "</h2>");
                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("  <table>");

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
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
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
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    file1 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    file2 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);
                    if (file1.Length > 0 || file2.Length > 0)
                    {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">BPI</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    file1 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC);
                    file2 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    var file3 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);

                    if (file1.Length > 0 || file2.Length > 0 || file3.Length > 0)
                    {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">Addnl Plots</td>");
                        if (file1.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
                        else
                            swOutFile.WriteLine("      <td></td>");
                        if (file2.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
                        else
                            swOutFile.WriteLine("      <td></td>");
                        if (file3.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(file3, 250) + "</td>");
                        else
                            swOutFile.WriteLine("      <td></td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    swOutFile.WriteLine("    <tr>");
                    swOutFile.WriteLine("      <td valign=\"middle\">TIC</td>");
                    swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC), 250) + "</td>");
                    swOutFile.WriteLine("      <td valign=\"middle\">");

                    GenerateQCScanTypeSummaryHTML(swOutFile, objSummaryStats, "        ");

                    swOutFile.WriteLine("      </td>");
                    swOutFile.WriteLine("    </tr>");

                    swOutFile.WriteLine("    <tr>");
                    swOutFile.WriteLine("      <td>&nbsp;</td>");
                    swOutFile.WriteLine("      <td align=\"center\">DMS <a href=\"http://dms2.pnl.gov/dataset/show/" + datasetName + "\">Dataset Detail Report</a></td>");

                    var dsnfoFileName = datasetName + clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;
                    if (mCreateDatasetInfoFile || File.Exists(Path.Combine(outputFolderPath, dsnfoFileName)))
                    {
                        swOutFile.WriteLine("      <td align=\"center\"><a href=\"" + dsnfoFileName + "\">Dataset Info XML file</a></td>");
                    }
                    else
                    {
                        swOutFile.WriteLine("      <td>&nbsp;</td>");
                    }

                    swOutFile.WriteLine("    </tr>");

                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("  </table>");
                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("</body>");
                    swOutFile.WriteLine("</html>");
                    swOutFile.WriteLine("");

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

        private void GenerateQCScanTypeSummaryHTML(TextWriter swOutFile, clsDatasetSummaryStats objDatasetSummaryStats, string indent)
        {
            if (indent == null)
                indent = string.Empty;

            swOutFile.WriteLine(indent + @"<table border=""1"">");
            swOutFile.WriteLine(indent + "  <tr><th>Scan Type</th><th>Scan Count</th><th>Scan Filter Text</th></tr>");

            foreach (var scanTypeEntry in objDatasetSummaryStats.objScanTypeStats)
            {
                var scanType = scanTypeEntry.Key;
                var indexMatch = scanType.IndexOf(clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR, StringComparison.Ordinal);

                string scanFilterText;
                if (indexMatch >= 0)
                {
                    scanFilterText = scanType.Substring(indexMatch + clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR.Length);
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

                swOutFile.WriteLine(indent + "  <tr><td>" + scanType + "</td>" + "<td align=\"center\">" + scanCount + "</td>" + "<td>" + scanFilterText + "</td></tr>");

            }

            swOutFile.WriteLine(indent + "</table>");

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

    }
}

