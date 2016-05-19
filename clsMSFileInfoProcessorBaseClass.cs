using System;
using System.IO;
using MSFileInfoScannerInterfaces;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2007
//
// Last modified May 11, 2015

namespace MSFileInfoScanner
{
    public abstract class clsMSFileInfoProcessorBaseClass : iMSFileInfoProcessor
    {

        /// <summary>
        /// Constructor
        /// </summary>
        protected clsMSFileInfoProcessorBaseClass()
        {
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

        protected int mLCMS2DOverviewPlotDivisor;
        // When True, then adds a new row to a tab-delimited text file that has dataset stats
        protected bool mUpdateDatasetStatsTextFile;

        protected string mDatasetStatsTextFileName;
        protected int mScanStart;
        protected int mScanEnd;

        protected bool mShowDebugInfo;

        protected int mDatasetID;

        protected bool mCopyFileLocalOnReadError;
        protected clsTICandBPIPlotter mTICandBPIPlot;

        protected clsTICandBPIPlotter mInstrumentSpecificPlots;
        private clsLCMSDataPlotter withEventsField_mLCMS2DPlot;
        protected clsLCMSDataPlotter mLCMS2DPlot {
            get { return withEventsField_mLCMS2DPlot; }
            set {
                if (withEventsField_mLCMS2DPlot != null) {
                    withEventsField_mLCMS2DPlot.ErrorEvent -= mLCMS2DPlot_ErrorEvent;
                }
                withEventsField_mLCMS2DPlot = value;
                if (withEventsField_mLCMS2DPlot != null) {
                    withEventsField_mLCMS2DPlot.ErrorEvent += mLCMS2DPlot_ErrorEvent;
                }
            }
        }
        private clsLCMSDataPlotter withEventsField_mLCMS2DPlotOverview;
        protected clsLCMSDataPlotter mLCMS2DPlotOverview {
            get { return withEventsField_mLCMS2DPlotOverview; }
            set {
                if (withEventsField_mLCMS2DPlotOverview != null) {
                    withEventsField_mLCMS2DPlotOverview.ErrorEvent -= mLCMS2DPlotOverview_ErrorEvent;
                }
                withEventsField_mLCMS2DPlotOverview = value;
                if (withEventsField_mLCMS2DPlotOverview != null) {
                    withEventsField_mLCMS2DPlotOverview.ErrorEvent += mLCMS2DPlotOverview_ErrorEvent;
                }
            }

        }
        private clsDatasetStatsSummarizer withEventsField_mDatasetStatsSummarizer;
        protected clsDatasetStatsSummarizer mDatasetStatsSummarizer {
            get { return withEventsField_mDatasetStatsSummarizer; }
            set {
                if (withEventsField_mDatasetStatsSummarizer != null) {
                    withEventsField_mDatasetStatsSummarizer.ErrorEvent -= mDatasetStatsSummarizer_ErrorEvent;
                }
                withEventsField_mDatasetStatsSummarizer = value;
                if (withEventsField_mDatasetStatsSummarizer != null) {
                    withEventsField_mDatasetStatsSummarizer.ErrorEvent += mDatasetStatsSummarizer_ErrorEvent;
                }
            }

        }
        public sealed override event ErrorEventEventHandler ErrorEvent;
        public sealed override event MessageEventEventHandler MessageEvent;

        //Public Event ProgressUpdate( Progress As Single)
        #endregion

        #region "Properties"

        /// <summary>
        /// This property allows the parent class to define the DatasetID value
        /// </summary>
        public override int DatasetID {
            get { return mDatasetID; }
            set { mDatasetID = value; }
        }

        public override string DatasetStatsTextFileName {
            get { return mDatasetStatsTextFileName; }
            set {
                if (string.IsNullOrEmpty(value)) {
                    // Do not update mDatasetStatsTextFileName
                } else {
                    mDatasetStatsTextFileName = value;
                }
            }
        }

        public override clsLCMSDataPlotterOptions LCMS2DPlotOptions {
            get { return mLCMS2DPlot.Options; }
            set {
                mLCMS2DPlot.Options = value;
                mLCMS2DPlotOverview.Options = value.Clone();
            }
        }

        public override int LCMS2DOverviewPlotDivisor {
            get { return mLCMS2DOverviewPlotDivisor; }
            set { mLCMS2DOverviewPlotDivisor = value; }
        }

        public override int ScanStart {
            get { return mScanStart; }
            set { mScanStart = value; }
        }

        public override bool ShowDebugInfo {
            get { return mShowDebugInfo; }
            set { mShowDebugInfo = value; }
        }

        /// <summary>
        /// When ScanEnd is > 0, then will stop processing at the specified scan number
        /// </summary>
        public override int ScanEnd {
            get { return mScanEnd; }
            set { mScanEnd = value; }
        }

        #endregion

        public override bool GetOption(ProcessingOptions eOption)
        {
            switch (eOption) {
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

        protected bool CreateDatasetInfoFile(string strInputFileName, string strOutputFolderPath)
        {

            bool blnSuccess;

            try {
                var strDatasetName = GetDatasetNameViaPath(strInputFileName);
                var strDatasetInfoFilePath = Path.Combine(strOutputFolderPath, strDatasetName);
                strDatasetInfoFilePath += clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0) {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                blnSuccess = mDatasetStatsSummarizer.CreateDatasetInfoFile(strDatasetName, strDatasetInfoFilePath);

                if (!blnSuccess) {
                    ReportError("Error calling objDatasetStatsSummarizer.CreateDatasetInfoFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            } catch (Exception ex) {
                ReportError("Error creating dataset info file: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool CreateDatasetScanStatsFile(string strInputFileName, string strOutputFolderPath)
        {

            bool blnSuccess;

            try {
                var strDatasetName = GetDatasetNameViaPath(strInputFileName);
                var strScanStatsFilePath = Path.Combine(strOutputFolderPath, strDatasetName) + "_ScanStats.txt";

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0) {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                blnSuccess = mDatasetStatsSummarizer.CreateScanStatsFile(strDatasetName, strScanStatsFilePath);

                if (!blnSuccess) {
                    ReportError("Error calling objDatasetStatsSummarizer.CreateScanStatsFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            } catch (Exception ex) {
                ReportError("Error creating dataset ScanStats file: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool UpdateDatasetStatsTextFile(string strInputFileName, string strOutputFolderPath)
        {

            return UpdateDatasetStatsTextFile(strInputFileName, strOutputFolderPath, clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME);

        }

        public bool UpdateDatasetStatsTextFile(string strInputFileName, string strOutputFolderPath, string strDatasetStatsFilename)
        {

            bool blnSuccess;

            try {
                var strDatasetName = GetDatasetNameViaPath(strInputFileName);

                var strDatasetStatsFilePath = Path.Combine(strOutputFolderPath, strDatasetStatsFilename);

                blnSuccess = mDatasetStatsSummarizer.UpdateDatasetStatsTextFile(strDatasetName, strDatasetStatsFilePath);

                if (!blnSuccess) {
                    ReportError("Error calling objDatasetStatsSummarizer.UpdateDatasetStatsTextFile: " + mDatasetStatsSummarizer.ErrorMessage);
                }

            } catch (Exception ex) {
                ReportError("Error updating the dataset stats text file: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public override string GetDatasetInfoXML()
        {

            try {
                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && mDatasetID > 0) {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID;
                }

                return mDatasetStatsSummarizer.CreateDatasetInfoXML();

            } catch (Exception ex) {
                ReportError("Error getting dataset info XML: " + ex.Message);
            }

            return string.Empty;

        }

        /// <summary>
        /// Returns the range of scan numbers to process
        /// </summary>
        /// <param name="intScanCount">Number of scans in the file</param>
        /// <param name="intScanStart">1 if mScanStart is zero; otherwise mScanStart</param>
        /// <param name="intScanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, intScanCount)</param>
        /// <remarks></remarks>
        protected void GetStartAndEndScans(int intScanCount, out int intScanStart, 	out int intScanEnd)
        {
            GetStartAndEndScans(intScanCount, 1, out intScanStart, out intScanEnd);
        }

        /// <summary>
        /// Returns the range of scan numbers to process
        /// </summary>
        /// <param name="intScanCount">Number of scans in the file</param>
        /// <param name="intScanNumFirst">The first scan number in the file</param>
        /// <param name="intScanStart">1 if mScanStart is zero; otherwise mScanStart</param>
        /// <param name="intScanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, intScanCount)</param>
        /// <remarks></remarks>
        protected void GetStartAndEndScans(int intScanCount, int intScanNumFirst, out int intScanStart, out int intScanEnd)
        {
            if (mScanStart > 0) {
                intScanStart = mScanStart;
            } else {
                intScanStart = 1;
            }

            if (mScanEnd > 0 && mScanEnd < intScanCount) {
                intScanEnd = mScanEnd;
            } else {
                intScanEnd = intScanCount;
            }

        }


        protected void InitializeLocalVariables()
        {
            mTICandBPIPlot = new clsTICandBPIPlotter();
            mInstrumentSpecificPlots = new clsTICandBPIPlotter();

            mLCMS2DPlot = new clsLCMSDataPlotter();
            mLCMS2DPlotOverview = new clsLCMSDataPlotter();

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

            mDatasetStatsSummarizer = new clsDatasetStatsSummarizer();

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

        protected void ReportError(string strMessage)
        {
            if (ErrorEvent != null) {
                ErrorEvent(strMessage);
            }
        }

        protected void ShowMessage(string strMessage)
        {
            if (MessageEvent != null) {
                MessageEvent(strMessage);
            }
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

            if (!mShowDebugInfo) {
                Console.Write(".");
            }

            if (scanCount <= 0)
            {
                return;
            }

            var sngProgress = Convert.ToSingle(scanNumber / scanCount * 100);

            if (!(DateTime.UtcNow.Subtract(dtLastProgressTime).TotalSeconds > detailedUpdateIntervalSeconds))
            {
                return;
            }

            dtLastProgressTime = DateTime.UtcNow;
            var strPercentComplete = sngProgress.ToString("0.0") + "% ";

            if (mShowDebugInfo) {
                Console.WriteLine(strPercentComplete);
            } else {
                Console.WriteLine();
                Console.Write(strPercentComplete);
            }
        }

        protected bool UpdateDatasetFileStats(FileInfo fiFileInfo, int intDatasetID)
        {

            try {
                if (!fiFileInfo.Exists)
                    return false;

                // Record the file size and Dataset ID
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = fiFileInfo.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = fiFileInfo.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = intDatasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(fiFileInfo.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = fiFileInfo.Extension;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = fiFileInfo.Length;

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

            } catch (Exception) {
                return false;
            }

            return true;

        }

        protected bool UpdateDatasetFileStats(DirectoryInfo diFolderInfo, int intDatasetID)
        {

            try {
                if (!diFolderInfo.Exists)
                    return false;

                // Record the file size and Dataset ID
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = diFolderInfo.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = diFolderInfo.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = intDatasetID;
                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = Path.GetFileNameWithoutExtension(diFolderInfo.Name);
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = diFolderInfo.Extension;

                foreach (var fiFileInfo in diFolderInfo.GetFiles("*", SearchOption.AllDirectories)) {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes += fiFileInfo.Length;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

            } catch (Exception) {
                return false;
            }

            return true;

        }

        protected bool CreateOverview2DPlots(string strDatasetName, string strOutputFolderPath, int intLCMS2DOverviewPlotDivisor)
        {

            return CreateOverview2DPlots(strDatasetName, strOutputFolderPath, intLCMS2DOverviewPlotDivisor, string.Empty);

        }

        protected bool CreateOverview2DPlots(string strDatasetName, string strOutputFolderPath, int intLCMS2DOverviewPlotDivisor, string strScanModeSuffixAddon)
        {
            if (intLCMS2DOverviewPlotDivisor <= 1) {
                // Nothing to do; just return True
                return true;
            }

            mLCMS2DPlotOverview.Reset();

            mLCMS2DPlotOverview.Options = mLCMS2DPlot.Options.Clone();

            // Set MaxPointsToPlot in mLCMS2DPlotOverview to be intLCMS2DOverviewPlotDivisor times smaller 
            // than the MaxPointsToPlot value in mLCMS2DPlot
            mLCMS2DPlotOverview.Options.MaxPointsToPlot = Convert.ToInt32(Math.Round(mLCMS2DPlot.Options.MaxPointsToPlot / (double)intLCMS2DOverviewPlotDivisor, 0));

            // Copy the data from mLCMS2DPlot to mLCMS2DPlotOverview
            // mLCMS2DPlotOverview will auto-filter the data to track, at most, mLCMS2DPlotOverview.Options.MaxPointsToPlot points
            for (var intIndex = 0; intIndex <= mLCMS2DPlot.ScanCountCached - 1; intIndex++)
            {
                var objScan = mLCMS2DPlot.GetCachedScanByIndex(intIndex);

                mLCMS2DPlotOverview.AddScanSkipFilters(objScan);
            }

            // Write out the Overview 2D plot of m/z vs. intensity
            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
            var blnSuccess = mLCMS2DPlotOverview.Save2DPlots(strDatasetName, strOutputFolderPath, "HighAbu_", strScanModeSuffixAddon);

            return blnSuccess;

        }

        public override bool CreateOutputFiles(string strInputFileName, string strOutputFolderPath)
        {
            bool blnSuccessOverall;

            try {
                var strDatasetName = GetDatasetNameViaPath(strInputFileName);
                blnSuccessOverall = true;
                var blnCreateQCPlotHtmlFile = false;

                if (strOutputFolderPath == null)
                    strOutputFolderPath = string.Empty;

                DirectoryInfo diFolderInfo;
                if (strOutputFolderPath.Length > 0) {
                    // Make sure the output folder exists
                    diFolderInfo = new DirectoryInfo(strOutputFolderPath);

                    if (!diFolderInfo.Exists) {
                        diFolderInfo.Create();
                    }
                } else {
                    diFolderInfo = new DirectoryInfo(".");
                }

                bool blnSuccess;
                if (mSaveTICAndBPI) {
                    // Write out the TIC and BPI plots
                    string strErrorMessage;
                    blnSuccess = mTICandBPIPlot.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName, out strErrorMessage);
                    if (!blnSuccess) {
                        ReportError("Error calling mTICandBPIPlot.SaveTICAndBPIPlotFiles: " + strErrorMessage);
                        blnSuccessOverall = false;
                    }

                    // Write out any instrument-specific plots
                    blnSuccess = mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName, out strErrorMessage);
                    if (!blnSuccess) {
                        ReportError("Error calling mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles: " + strErrorMessage);
                        blnSuccessOverall = false;
                    }

                    blnCreateQCPlotHtmlFile = true;
                }

                if (mSaveLCMS2DPlots) {
                    // Write out the 2D plot of m/z vs. intensity
                    // Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
                    blnSuccess = mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess) {
                        blnSuccessOverall = false;
                    } else {
                        if (mLCMS2DOverviewPlotDivisor > 0) {
                            // Also save the Overview 2D Plots
                            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
                            blnSuccess = CreateOverview2DPlots(strDatasetName, strOutputFolderPath, mLCMS2DOverviewPlotDivisor);
                            if (!blnSuccess) {
                                blnSuccessOverall = false;
                            }
                        } else {
                            mLCMS2DPlotOverview.ClearRecentFileInfo();
                        }

                        if (blnSuccessOverall && mLCMS2DPlot.Options.PlottingDeisotopedData) {
                            // Create two more plots 2D plots, but this with a smaller maximum m/z
                            mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;
                            mLCMS2DPlotOverview.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;

                            mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName, "", "_zoom");
                            if (mLCMS2DOverviewPlotDivisor > 0) {
                                CreateOverview2DPlots(strDatasetName, strOutputFolderPath, mLCMS2DOverviewPlotDivisor, "_zoom");
                            }
                        }
                    }
                    blnCreateQCPlotHtmlFile = true;
                }

                if (mCreateDatasetInfoFile) {
                    // Create the _DatasetInfo.xml file
                    blnSuccess = CreateDatasetInfoFile(strInputFileName, diFolderInfo.FullName);
                    if (!blnSuccess) {
                        blnSuccessOverall = false;
                    }
                    blnCreateQCPlotHtmlFile = true;
                }

                if (mCreateScanStatsFile) {
                    // Create the _ScanStats.txt file
                    blnSuccess = CreateDatasetScanStatsFile(strInputFileName, diFolderInfo.FullName);
                    if (!blnSuccess) {
                        blnSuccessOverall = false;
                    }
                }

                if (mUpdateDatasetStatsTextFile) {
                    // Add a new row to the MSFileInfo_DatasetStats.txt file
                    blnSuccess = UpdateDatasetStatsTextFile(strInputFileName, diFolderInfo.FullName, mDatasetStatsTextFileName);
                    if (!blnSuccess) {
                        blnSuccessOverall = false;
                    }
                }

                if (blnCreateQCPlotHtmlFile) {
                    blnSuccess = CreateQCPlotHTMLFile(strDatasetName, diFolderInfo.FullName);
                    if (!blnSuccess) {
                        blnSuccessOverall = false;
                    }
                }

            } catch (Exception ex) {
                ReportError("Error creating output files: " + ex.Message);
                blnSuccessOverall = false;
            }

            return blnSuccessOverall;

        }

        protected bool CreateQCPlotHTMLFile(string strDatasetName, string strOutputFolderPath)
        {
            try {
                // Obtain the dataset summary stats (they will be auto-computed if not up to date)
                var objSummaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats();

                var strHTMLFilePath = Path.Combine(strOutputFolderPath, "index.html");

                using (var swOutFile = new StreamWriter(new FileStream(strHTMLFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))) {

                    swOutFile.WriteLine("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 3.2//EN\">");
                    swOutFile.WriteLine("<html>");
                    swOutFile.WriteLine("<head>");
                    swOutFile.WriteLine("  <title>" + strDatasetName + "</title>");
                    swOutFile.WriteLine("</head>");
                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("<body>");
                    swOutFile.WriteLine("  <h2>" + strDatasetName + "</h2>");
                    swOutFile.WriteLine("");
                    swOutFile.WriteLine("  <table>");

                    // First the plots with the top 50,000 points
                    var strFile1 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS);

                    string strFile2;
                    if (mLCMS2DPlotOverview.Options.PlottingDeisotopedData) {
                        strFile2 = strFile1.Replace("_zoom.png", ".png");
                    } else {
                        strFile2 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn);
                    }

                    var strTop = IntToEngineeringNotation(mLCMS2DPlotOverview.Options.MaxPointsToPlot);

                    if (strFile1.Length > 0 || strFile2.Length > 0) {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + strTop + ")</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    // Now the plots with the top 500,000 points
                    strFile1 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS);

                    if (mLCMS2DPlotOverview.Options.PlottingDeisotopedData) {
                        strFile2 = strFile1.Replace("_zoom.png", ".png");
                    } else {
                        strFile2 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn);
                    }

                    strTop = IntToEngineeringNotation(mLCMS2DPlot.Options.MaxPointsToPlot);

                    if (strFile1.Length > 0 || strFile2.Length > 0) {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + strTop + ")</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    strFile1 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    strFile2 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);
                    if (strFile1.Length > 0 || strFile2.Length > 0) {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">BPI</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile1, 250) + "</td>");
                        swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile2, 250) + "</td>");
                        swOutFile.WriteLine("    </tr>");
                        swOutFile.WriteLine("");
                    }

                    strFile1 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC);
                    strFile2 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS);
                    var strFile3 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn);

                    if (strFile1.Length > 0 || strFile2.Length > 0 || strFile3.Length > 0) {
                        swOutFile.WriteLine("    <tr>");
                        swOutFile.WriteLine("      <td valign=\"middle\">Addnl Plots</td>");
                        if (strFile1.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile1, 250) + "</td>");
                        else
                            swOutFile.WriteLine("      <td></td>");
                        if (strFile2.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile2, 250) + "</td>");
                        else
                            swOutFile.WriteLine("      <td></td>");
                        if (strFile3.Length > 0)
                            swOutFile.WriteLine("      <td>" + GenerateQCFigureHTML(strFile3, 250) + "</td>");
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
                    swOutFile.WriteLine("      <td align=\"center\">DMS <a href=\"http://dms2.pnl.gov/dataset/show/" + strDatasetName + "\">Dataset Detail Report</a></td>");

                    var strDSInfoFileName = strDatasetName + clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;
                    if (mCreateDatasetInfoFile || File.Exists(Path.Combine(strOutputFolderPath, strDSInfoFileName))) {
                        swOutFile.WriteLine("      <td align=\"center\"><a href=\"" + strDSInfoFileName + "\">Dataset Info XML file</a></td>");
                    } else {
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

            } catch (Exception ex) {
                ReportError("Error creating QC plot HTML file: " + ex.Message);
                return false;
            }

        }

        private string GenerateQCFigureHTML(string strFilename, int intWidthPixels)
        {
            if (string.IsNullOrEmpty(strFilename)) {
                return "&nbsp;";
            }

            return "<a href=\"" + strFilename + "\">" + "<img src=\"" + strFilename + "\" width=\"" + intWidthPixels + "\" border=\"0\"></a>";
        }


        private void GenerateQCScanTypeSummaryHTML(StreamWriter swOutFile, clsDatasetSummaryStats objDatasetSummaryStats, string strIndent)
        {
            if (strIndent == null)
                strIndent = string.Empty;

            swOutFile.WriteLine(strIndent + "<table border=\"1\">");
            swOutFile.WriteLine(strIndent + "  <tr><th>Scan Type</th><th>Scan Count</th><th>Scan Filter Text</th></tr>");


            foreach (var scanTypeEntry in objDatasetSummaryStats.objScanTypeStats) {                
                var strScanType = scanTypeEntry.Key;
                var intIndexMatch = strScanType.IndexOf(clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR, StringComparison.Ordinal);

                string strScanFilterText;
                if (intIndexMatch >= 0) {
                    strScanFilterText = strScanType.Substring(intIndexMatch + clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR.Length);
                    if (intIndexMatch > 0) {
                        strScanType = strScanType.Substring(0, intIndexMatch);
                    } else {
                        strScanType = string.Empty;
                    }
                } else {
                    strScanFilterText = string.Empty;
                }
                var intScanCount = scanTypeEntry.Value;

                swOutFile.WriteLine(strIndent + "  <tr><td>" + strScanType + "</td>" + "<td align=\"center\">" + intScanCount + "</td>" + "<td>" + strScanFilterText + "</td></tr>");

            }

            swOutFile.WriteLine(strIndent + "</table>");

        }

        /// <summary>
        /// Converts an integer to engineering notation
        /// For example, 50000 will be returned as 50K
        /// </summary>
        /// <param name="intValue"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        protected string IntToEngineeringNotation(int intValue)
        {

            if (intValue < 1000) {
                return intValue.ToString();
            } else if (intValue < 1000000.0) {
                return Convert.ToInt32(Math.Round(intValue / 1000.0, 0)) + "K";
            } else {
                return Convert.ToInt32(Math.Round(intValue / 1000.0 / 1000, 0)) + "M";
            }

        }

        private void mLCMS2DPlot_ErrorEvent(string Message)
        {
            ReportError("Error in LCMS2DPlot: " + Message);
        }

        private void mLCMS2DPlotOverview_ErrorEvent(string Message)
        {
            ReportError("Error in LCMS2DPlotOverview: " + Message);
        }

        private void mDatasetStatsSummarizer_ErrorEvent(string errorMessage)
        {
            ReportError(errorMessage);
        }
    }
}

