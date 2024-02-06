using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Plotting;
using MSFileInfoScannerInterfaces;
using PRISM;
using pwiz.ProteowizardWrapper;
using ThermoFisher.CommonCore.Data.Business;
using ThermoRawFileReader;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Base class for MS file info scanners
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2007
    /// </remarks>
    public abstract class MSFileInfoProcessorBaseClass : EventNotifier
    {
        // Ignore Spelling: AcqTime, Abu, Addnl, Bruker, href, html

        public const int PROGRESS_SPECTRA_LOADED = 90;
        public const int PROGRESS_SAVED_TIC_AND_BPI_PLOT = 92;
        public const int PROGRESS_SAVED_2D_PLOTS = 99;

        public const int MAX_SCANS_TO_TRACK_IN_DETAIL = 750000;
        public const int MAX_SCANS_FOR_TIC_AND_BPI = 1000000;

        /// <summary>
        /// Constructor
        /// </summary>
        protected MSFileInfoProcessorBaseClass(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions)
        {
            mTICAndBPIPlot = new TICandBPIPlotter("TICAndBPIPlot", false);
            RegisterEvents(mTICAndBPIPlot);

            mInstrumentSpecificPlots = new List<TICandBPIPlotter>();

            mDatasetStatsSummarizer = new DatasetStatsSummarizer();
            RegisterEvents(mDatasetStatsSummarizer);

            mLCMS2DPlot = new LCMSDataPlotter();
            RegisterEvents(mLCMS2DPlot);

            mLCMS2DPlotOverview = new LCMSDataPlotter();
            RegisterEvents(mLCMS2DPlotOverview);

            Options = options;

            mLCMS2DPlot.Options = lcms2DPlotOptions;
            mLCMS2DPlotOverview.Options = lcms2DPlotOptions.Clone();

            InitializeLocalVariables();
        }

        /// <summary>
        /// Used for checking if over 10% of the spectra failed MS2MzMin validation
        /// </summary>
        // ReSharper disable once IdentifierTypo
        public const int MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED = 10;

        /// <summary>
        /// This variable tracks TIC and BPI data (vs. scan)
        /// </summary>
        protected readonly TICandBPIPlotter mTICAndBPIPlot;

        /// <summary>
        /// This variable tracks UIMF pressure vs. frame (using mTIC)
        /// It also tracks data associated with other devices tracked by .raw files (e.g. LC pressure vs. scan)
        /// </summary>
        protected readonly List<TICandBPIPlotter> mInstrumentSpecificPlots;

        protected readonly LCMSDataPlotter mLCMS2DPlot;

        private readonly LCMSDataPlotter mLCMS2DPlotOverview;

        protected readonly DatasetStatsSummarizer mDatasetStatsSummarizer;

        /// <summary>
        /// Processing error code
        /// </summary>
        public iMSFileInfoScanner.MSFileScannerErrorCodes ErrorCode { get; protected set; }

        /// <summary>
        /// When true, do not include the Scan Type table in the QC Plot HTML file
        /// </summary>
        protected bool HideEmptyHTMLSections { get; set; }

        /// <summary>
        /// LC/MS 2D plot options
        /// </summary>
        public LCMSDataPlotterOptions LCMS2DPlotOptions => mLCMS2DPlot.Options;

        /// <summary>
        /// This will be True if the dataset has too many MS/MS spectra
        /// where the minimum m/z value is larger than MS2MzMin
        /// </summary>
        public bool MS2MzMinValidationError { get; set; }

        /// <summary>
        /// This will be True if the dataset has some MS/MS spectra
        /// where the minimum m/z value is larger than MS2MzMin
        /// (no more than 10% of the spectra)
        /// </summary>
        public bool MS2MzMinValidationWarning { get; set; }

        /// <summary>
        /// MS2MzMin validation error or warning message
        /// </summary>
        public string MS2MzMinValidationMessage { get; set; }

        /// <summary>
        /// Processing Options
        /// </summary>
        public InfoScannerOptions Options { get; }

        /// <summary>
        /// Add a new TICAndBPIPlotter instance to mInstrumentSpecificPlots
        /// </summary>
        /// <param name="dataSource">Data source</param>
        protected TICandBPIPlotter AddInstrumentSpecificPlot(string dataSource)
        {
            var plotContainer = new TICandBPIPlotter(dataSource, true);
            RegisterEvents(plotContainer);
            mInstrumentSpecificPlots.Add(plotContainer);

            return plotContainer;
        }

        /// <summary>
        /// Find the largest file in the instrument directory
        /// Compute its SHA-1 hash then add to mDatasetStatsSummarizer.DatasetFileInfo
        /// </summary>
        /// <param name="instrumentDirectory">Instrument directory path</param>
        protected void AddLargestInstrumentFile(DirectoryInfo instrumentDirectory)
        {
            // List of files by size, sorted descending
            var filesBySize = (from item in instrumentDirectory.GetFiles() orderby item.Length descending select item).ToList();

            if (filesBySize.Count == 0)
                return;

            if (Options.DisableInstrumentHash)
            {
                mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(filesBySize[0]);
            }
            else
            {
                mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(filesBySize[0]);
            }
        }

        /// <summary>
        /// Computes the incremental progress that has been made beyond currentTaskProgressAtStart, based on the subtask progress and the next overall progress level
        /// </summary>
        /// <param name="currentTaskProgressAtStart">Progress at the start of the current subtask (value between 0 and 100)</param>
        /// <param name="currentTaskProgressAtEnd">Progress at the start of the current subtask (value between 0 and 100)</param>
        /// <param name="subTaskProgress">Progress of the current subtask (value between 0 and 100)</param>
        /// <returns>Overall progress (value between 0 and 100)</returns>
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

            return (float)(currentTaskProgressAtStart + subTaskProgress / 100.0 * (currentTaskProgressAtEnd - currentTaskProgressAtStart));
        }

        private bool CreateDatasetInfoFile(string inputFileName, string outputDirectoryPath)
        {
            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var datasetInfoFilePath = Path.Combine(outputDirectoryPath, datasetName);
                datasetInfoFilePath += DatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && Options.DatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = Options.DatasetID;
                }

                success = mDatasetStatsSummarizer.CreateDatasetInfoFile(datasetName, datasetInfoFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling DatasetStatsSummarizer.CreateDatasetInfoFile: {0}", mDatasetStatsSummarizer.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error creating dataset info file: {0}", ex.Message), ex);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="inputFileName">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <returns>True if success; False if failure</returns>
        public bool CreateDatasetScanStatsFile(string inputFileName, string outputDirectoryPath)
        {
            bool success;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);
                var scanStatsFilePath = Path.Combine(outputDirectoryPath, datasetName) + "_ScanStats.txt";

                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && Options.DatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = Options.DatasetID;
                }

                success = mDatasetStatsSummarizer.CreateScanStatsFile(scanStatsFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling DatasetStatsSummarizer.CreateScanStatsFile: {0}", mDatasetStatsSummarizer.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error creating dataset ScanStats file: {0}", ex.Message), ex);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Get the dataset info as XML
        /// </summary>
        public string GetDatasetInfoXML()
        {
            try
            {
                if (mDatasetStatsSummarizer.DatasetFileInfo.DatasetID == 0 && Options.DatasetID > 0)
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = Options.DatasetID;
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
        private void GetStartAndEndScans(int scanCount, int scanNumFirst, out int scanStart, out int scanEnd)
        {
            if (Options.ScanStart > 0)
            {
                scanStart = Options.ScanStart;
            }
            else
            {
                scanStart = scanNumFirst;
            }

            if (Options.ScanEnd > 0 && Options.ScanEnd < scanCount)
            {
                scanEnd = Options.ScanEnd;
            }
            else
            {
                scanEnd = scanCount;
            }
        }

        private void InitializeLocalVariables()
        {
            MS2MzMinValidationError = false;
            MS2MzMinValidationWarning = false;
            MS2MzMinValidationMessage = string.Empty;

            ErrorCode = iMSFileInfoScanner.MSFileScannerErrorCodes.NoError;

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

        private bool CreateOverview2DPlots(string datasetName, string outputDirectoryPath, int overviewPlotDivisor)
        {
            return CreateOverview2DPlots(datasetName, outputDirectoryPath, overviewPlotDivisor, string.Empty);
        }

        private bool CreateOverview2DPlots(string datasetName, string outputDirectoryPath, int overviewPlotDivisor, string scanModeSuffixAddon)
        {
            if (overviewPlotDivisor <= 1)
            {
                // Nothing to do; just return True
                return true;
            }

            mLCMS2DPlotOverview.Reset();

            mLCMS2DPlotOverview.Options = mLCMS2DPlot.Options.Clone();

            // Set MaxPointsToPlot in mLCMS2DPlotOverview to be lcms2DOverviewPlotDivisor times smaller
            // than the MaxPointsToPlot value in mLCMS2DPlot
            mLCMS2DPlotOverview.Options.MaxPointsToPlot = (int)Math.Round(mLCMS2DPlot.Options.MaxPointsToPlot / (double)overviewPlotDivisor, 0);

            // Copy the data from mLCMS2DPlot to mLCMS2DPlotOverview,
            // which will auto-filter the data to track, at most, mLCMS2DPlotOverview.Options.MaxPointsToPlot points
            for (var index = 0; index < mLCMS2DPlot.ScanCountCached; index++)
            {
                var scan = mLCMS2DPlot.GetCachedScanByIndex(index);

                mLCMS2DPlotOverview.AddScanSkipFilters(scan);
            }

            // Write out the Overview 2D plot of m/z vs. intensity
            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
            return mLCMS2DPlotOverview.Save2DPlots(datasetName, outputDirectoryPath, "HighAbu_", scanModeSuffixAddon);
        }

        /// <summary>
        /// Create the output files
        /// </summary>
        /// <param name="inputFileName">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        public bool CreateOutputFiles(string inputFileName, string outputDirectoryPath)
        {
            bool successOverall;

            try
            {
                var datasetName = GetDatasetNameViaPath(inputFileName);

                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    OnWarningEvent("Dataset name returned by GetDatasetNameViaPath is an empty string");
                }

                successOverall = true;
                var createQCPlotHTMLFile = false;

                outputDirectoryPath ??= string.Empty;

                DirectoryInfo outputDirectory;

                if (outputDirectoryPath.Length > 0)
                {
                    // Make sure the output directory exists
                    outputDirectory = MSFileInfoScanner.GetDirectoryInfo(outputDirectoryPath);

                    if (!outputDirectory.Exists)
                    {
                        outputDirectory.Create();
                    }
                }
                else
                {
                    outputDirectory = new DirectoryInfo(".");
                }

                OnDebugEvent("Output directory path: {0}", outputDirectory.FullName);

                UpdatePlotWithPython();

                if (Options.PlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
                {
                    OnWarningEvent("Updating PlotWithPython to True in mLCMS2DPlot; this is typically set by ProcessMSDataset");
                    mLCMS2DPlot.Options.PlotWithPython = true;
                }

                if (mLCMS2DPlot.Options.DeleteTempFiles && Options.ShowDebugInfo)
                {
                    OnWarningEvent("Updating DeleteTempFiles to False in mLCMS2DPlot; this is typically set by ProcessMSDataset");
                    mLCMS2DPlot.Options.DeleteTempFiles = false;
                }

                if (Options.SaveTICAndBPIPlots || Options.SaveLCMS2DPlots)
                {
                    OnProgressUpdate("Saving plots", PROGRESS_SPECTRA_LOADED);
                }

                if (Options.SaveTICAndBPIPlots)
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

                if (Options.SaveLCMS2DPlots)
                {
                    // Determine the number of times we'll be calling Save2DPlots or CreateOverview2DPlots
                    var lcMSPlotStepsTotal = 1;

                    if (mLCMS2DPlot.Options.OverviewPlotDivisor > 0)
                    {
                        lcMSPlotStepsTotal++;
                    }

                    if (mLCMS2DPlot.Options.PlottingDeisotopedData)
                    {
                        lcMSPlotStepsTotal++;

                        if (mLCMS2DPlot.Options.OverviewPlotDivisor > 0)
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
                        if (mLCMS2DPlot.Options.OverviewPlotDivisor > 0)
                        {
                            // Also save the Overview 2D Plots
                            // Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
                            var success4 = CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DPlot.Options.OverviewPlotDivisor);
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
                            // Create two more 2D plots, but this time with a smaller maximum mass
                            mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot = LCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;
                            mLCMS2DPlotOverview.Options.MaxMonoMassForDeisotopedPlot = LCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT;

                            mLCMS2DPlot.Save2DPlots(datasetName, outputDirectory.FullName, string.Empty, "_zoom");
                            lcMSPlotStepsComplete++;
                            ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);

                            if (mLCMS2DPlot.Options.OverviewPlotDivisor > 0)
                            {
                                CreateOverview2DPlots(datasetName, outputDirectoryPath, mLCMS2DPlot.Options.OverviewPlotDivisor, "_zoom");
                                lcMSPlotStepsComplete++;
                                ReportProgressSaving2DPlots(lcMSPlotStepsComplete, lcMSPlotStepsTotal);
                            }
                        }
                    }
                    createQCPlotHTMLFile = true;

                    OnProgressUpdate("2D plots saved", PROGRESS_SAVED_2D_PLOTS);
                }

                if (Options.CreateDatasetInfoFile)
                {
                    // Create the _DatasetInfo.xml file
                    var success = CreateDatasetInfoFile(inputFileName, outputDirectory.FullName);

                    if (!success)
                    {
                        successOverall = false;
                    }
                    createQCPlotHTMLFile = true;
                }

                if (Options.CreateScanStatsFile)
                {
                    // Create the _ScanStats.txt file
                    var success = CreateDatasetScanStatsFile(inputFileName, outputDirectory.FullName);

                    if (!success)
                    {
                        successOverall = false;
                    }
                }

                if (Options.UpdateDatasetStatsTextFile)
                {
                    // Append a new row to the MSFileInfo_DatasetStats.txt file
                    var success = UpdateDatasetStatsTextFile(inputFileName, outputDirectory.FullName, Options.DatasetStatsTextFileName);

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
                OnErrorEvent(string.Format("Error creating output files: {0}", ex.Message), ex);
                successOverall = false;
            }

            return successOverall;
        }

        private bool CreateQCPlotHTMLFile(string datasetName, string outputDirectoryPath)
        {
            try
            {
                string htmlFileName;

                if (Options.UseDatasetNameForHtmlPlotsFile && datasetName.Length > 0)
                {
                    htmlFileName = datasetName + ".html";
                }
                else
                {
                    htmlFileName = "index.html";
                }

                const bool includePrecursorMZ = true;

                // Obtain the dataset summary stats (they will be auto-computed if not up to date)
                var datasetSummaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats(includePrecursorMZ);

                var htmlFilePath = Path.Combine(outputDirectoryPath, htmlFileName);

                OnDebugEvent("Creating file {0}", htmlFilePath);

                using var writer = new StreamWriter(new FileStream(htmlFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

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
                AppendTICAndSummaryStats(writer, datasetSummaryStats);

                // Append dataset info
                AppendDatasetInfo(writer, datasetName, outputDirectoryPath);

                // Append device info
                AppendDeviceInfo(writer);

                // Add </table> and HTML footers
                AppendHTMLFooter(writer);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error creating QC plot HTML file: {0}", ex.Message), ex);
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
            writer.WriteLine("  <style>");
            writer.WriteLine("    table.DataTable {");
            writer.WriteLine("      margin: 10px 5px 5px 5px;");
            writer.WriteLine("      border: 1px solid black;");
            writer.WriteLine("      border-collapse: collapse;");
            writer.WriteLine("    }");
            writer.WriteLine("    ");
            writer.WriteLine("    th.DataHead {");
            writer.WriteLine("      border: 1px solid black;");
            writer.WriteLine("      padding: 2px 4px 2px 2px; ");
            writer.WriteLine("      text-align: left;");
            writer.WriteLine("    }");
            writer.WriteLine("    ");
            writer.WriteLine("    td.DataCell {");
            writer.WriteLine("      border: 1px solid black;");
            writer.WriteLine("      padding: 2px 4px 2px 4px;");
            writer.WriteLine("    }");
            writer.WriteLine("        ");
            writer.WriteLine("    td.DataCentered {");
            writer.WriteLine("      border: 1px solid black;");
            writer.WriteLine("      padding: 2px 4px 2px 4px;");
            writer.WriteLine("      text-align: center;");
            writer.WriteLine("    }");
            writer.WriteLine("  </style>");
            writer.WriteLine("</head>");
            writer.WriteLine();
            writer.WriteLine("<body>");
            writer.WriteLine("  <h2>" + datasetName + "</h2>");
            writer.WriteLine();
            writer.WriteLine("  <table>");
        }

        private void AppendLCMS2DPlots(TextWriter writer, LCMSDataPlotter lcmsPlotter)
        {
            var file1 = lcmsPlotter.GetRecentFileInfo(LCMSDataPlotter.OutputFileTypes.LCMS);

            string file2;

            if (lcmsPlotter.Options.PlottingDeisotopedData)
            {
                file2 = file1.Replace("_zoom.png", ".png");
            }
            else
            {
                file2 = lcmsPlotter.GetRecentFileInfo(LCMSDataPlotter.OutputFileTypes.LCMSMSn);
            }

            var top = IntToEngineeringNotation(lcmsPlotter.Options.MaxPointsToPlot);

            if (file1.Length == 0 && file2.Length == 0)
                return;

            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td valign=\"middle\">LCMS<br>(Top " + top + ")</td>");
            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
            writer.WriteLine("    </tr>");
            writer.WriteLine();
        }

        private void AppendBPIPlots(TextWriter writer)
        {
            var file1 = mTICAndBPIPlot.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.BPIMS);
            var file2 = mTICAndBPIPlot.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.BPIMSn);

            if (file1.Length == 0 && file2.Length == 0)
                return;

            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td valign=\"middle\">BPI</td>");
            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file1, 250) + "</td>");
            writer.WriteLine("      <td>" + GenerateQCFigureHTML(file2, 250) + "</td>");
            writer.WriteLine("    </tr>");
            writer.WriteLine();
        }

        private void AppendAdditionalPlots(TextWriter writer)
        {
            // This dictionary tracks the QC plots, by device type
            var plotsByDeviceType = new Dictionary<Device, List<string>>();

            // Generate the HTML to display the plots and store in plotsByDeviceType
            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                var deviceType = plotContainer.DeviceType;

                var file1 = plotContainer.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.TIC);
                var file2 = plotContainer.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.BPIMS);
                var file3 = plotContainer.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.BPIMSn);

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

        private void AppendTICAndSummaryStats(TextWriter writer, DatasetSummaryStats datasetSummaryStats)
        {
            writer.WriteLine("    <tr>");

            if (HideEmptyHTMLSections && datasetSummaryStats.ScanTypeStats.Count == 0)
            {
                writer.WriteLine("      <td>&nbsp;</td><td style='width: 200px'>&nbsp;</td><td style='width: 200px'>&nbsp;</td>");
            }
            else
            {
                var qcFigureHTML = GenerateQCFigureHTML(mTICAndBPIPlot.GetRecentFileInfo(TICandBPIPlotter.OutputFileTypes.TIC), 250);

                // Use valign=top when ScanTypeStats has more than 3 scan types
                var verticalAlignment = datasetSummaryStats.ScanTypeStats.Count > 3 ? "top" : "middle";

                // When the vertical alignment is "top", place 3 line breaks before "TIC"
                writer.WriteLine("      <td valign=\"{0}\">{1}TIC</td>",
                    verticalAlignment,
                    verticalAlignment == "top" ? "<br><br><br>" : string.Empty);

                writer.WriteLine("      <td valign=\"{0}\">" + qcFigureHTML + "</td>", verticalAlignment);
                writer.WriteLine("      <td valign=\"{0}\">", verticalAlignment);

                GenerateQCScanTypeSummaryHTML(writer, datasetSummaryStats, "        ");

                writer.WriteLine("      </td>");
            }

            writer.WriteLine("    </tr>");
        }

        private void AppendDatasetInfo(TextWriter writer, string datasetName, string outputDirectoryPath)
        {
            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td>&nbsp;</td>");
            writer.WriteLine("      <td align=\"center\">DMS <a href=\"https://dms2.pnl.gov/dataset/show/" + datasetName + "\">Dataset Detail Report</a></td>");

            var datasetInfoFileName = datasetName + DatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX;

            if (Options.CreateDatasetInfoFile || File.Exists(Path.Combine(outputDirectoryPath, datasetInfoFileName)))
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

            var tableHtml = GetDeviceTableHTML(deviceList);

            writer.WriteLine("    <tr>");
            writer.WriteLine("      <td>&nbsp;</td>");
            writer.WriteLine("      <td align=\"center\" colspan=2>");

            foreach (var item in tableHtml)
            {
                writer.WriteLine("        " + item);
            }

            writer.WriteLine("      </td>");
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
            indent ??= string.Empty;

            writer.WriteLine(indent + @"<table class=""DataTable"">");
            writer.WriteLine(indent + @"  <tr><th class=""DataHead"">Scan Type</th><th class=""DataHead"">Scan Count</th><th class=""DataHead"">Scan Filter Text</th><th class=""DataHead"">Isolation Window (m/z)</th></tr>");

            foreach (var scanTypeEntry in datasetSummaryStats.ScanTypeStats)
            {
                var indexMatch = scanTypeEntry.Key.IndexOf(DatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR, StringComparison.Ordinal);

                string scanFilterText;

                string scanType;

                if (indexMatch >= 0)
                {
                    scanFilterText = scanTypeEntry.Key.Substring(indexMatch + DatasetStatsSummarizer.SCAN_TYPE_STATS_SEP_CHAR.Length);

                    if (indexMatch > 0)
                    {
                        scanType = scanTypeEntry.Key.Substring(0, indexMatch);
                    }
                    else
                    {
                        scanType = string.Empty;
                    }
                }
                else
                {
                    scanType = scanTypeEntry.Key;
                    scanFilterText = string.Empty;
                }

                var scanCount = scanTypeEntry.Value;

                var windowWidths = DatasetStatsSummarizer.GetDelimitedWindowWidthList(scanTypeEntry.Key, datasetSummaryStats.ScanTypeWindowWidths);

                writer.WriteLine(indent + "  " +
                                 @"<tr><td class=""DataCell"">" + scanType + "</td>" +
                                 @"<td class=""DataCentered"">" + scanCount + "</td>" +
                                 @"<td class=""DataCell"">" + scanFilterText + "</td>" +
                                 @"<td class=""DataCentered"">" + windowWidths + "</td></tr>");
            }

            writer.WriteLine(indent + "</table>");
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath">Data file path</param>
        public abstract string GetDatasetNameViaPath(string dataFilePath);

        private IEnumerable<string> GetDeviceTableHTML(IEnumerable<DeviceInfo> deviceList)
        {
            var deviceTypeRow = new StringBuilder();
            var deviceNameRow = new StringBuilder();
            var deviceModelRow = new StringBuilder();
            var deviceSerialRow = new StringBuilder();
            var deviceSwVersionRow = new StringBuilder();

            deviceTypeRow.Append(@"<th class=""DataHead"">Device Type:</th>");
            deviceNameRow.Append(@"<th class=""DataHead"">Name:</th>");
            deviceModelRow.Append(@"<th class=""DataHead"">Model:</th>");
            deviceSerialRow.Append(@"<th class=""DataHead"">Serial:</th>");
            deviceSwVersionRow.Append(@"<th class=""DataHead"">Software Version:</th>");

            // In Thermo files, the same device might be listed more than once in deviceList, e.g. if an LC is tracking pressure from two different locations in the pump
            // This SortedSet is used to avoid displaying the same device twice
            var devicesDisplayed = new SortedSet<string>();

            const string tdFormatter = @"<td class=""DataCell"">{0}</td>";

            foreach (var device in deviceList)
            {
                var deviceKey = string.Format("{0}_{1}_{2}", device.InstrumentName, device.Model, device.SerialNumber);

                if (devicesDisplayed.Contains(deviceKey))
                    continue;

                devicesDisplayed.Add(deviceKey);

                deviceTypeRow.AppendFormat(tdFormatter, device.DeviceDescription);
                deviceNameRow.AppendFormat(tdFormatter, device.InstrumentName);
                deviceModelRow.AppendFormat(tdFormatter, device.Model);
                deviceSerialRow.AppendFormat(tdFormatter, device.SerialNumber);
                deviceSwVersionRow.AppendFormat(tdFormatter, device.SoftwareVersion);
            }

            // Padding is: top right bottom left

            var html = new List<string>
            {
                @"<table class=""DataTable"">",
                string.Format("  <tr>{0}</tr>", deviceTypeRow),
                string.Format("  <tr>{0}</tr>", deviceNameRow),
                string.Format("  <tr>{0}</tr>", deviceModelRow),
                string.Format("  <tr>{0}</tr>", deviceSerialRow),
                string.Format("  <tr>{0}</tr>", deviceSwVersionRow),
                "</table>"
            };

            return html;
        }

        [CLSCompliant(false)]
        protected DateTime GetRunStartTime(MSDataFileReader msDataFileReader)
        {
            var runStartTime = Convert.ToDateTime(msDataFileReader.RunStartTime);

            // On Bruker instruments, RunStartTime is not the actual time of day that the data was acquired
            // Possibly update the time, based on the instrument model

            var instrumentConfig = msDataFileReader.GetInstrumentConfigInfoList();

            var convertToLocalTime = instrumentConfig.Any(item => item.Model.StartsWith("Bruker"));

            return convertToLocalTime ? runStartTime.ToUniversalTime() : runStartTime;
        }

        /// <summary>
        /// Converts an integer to engineering notation
        /// For example, 50000 will be returned as 50K
        /// </summary>
        /// <param name="value">Value to format</param>
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

                var msDataFileReader = new MSDataFileReader(datasetFileOrDirectory.FullName);

                try
                {
                    var runStartTime = GetRunStartTime(msDataFileReader);

                    // Possibly update AcqTimeStart
                    if (runStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                        {
                            if (datasetFileInfo.AcqTimeStart == DateTime.MinValue ||
                                Math.Abs(datasetFileInfo.AcqTimeStart.Subtract(runStartTime).TotalSeconds) > 0)
                            {
                                UpdateAcqStartAndEndTimes(datasetFileInfo, msDataFileReader, runStartTime);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the ProteoWizard Data Parser class
                var pWizParser = new ProteoWizardDataParser(msDataFileReader, mDatasetStatsSummarizer, mTICAndBPIPlot,
                                                            mLCMS2DPlot, Options.SaveLCMS2DPlots, Options.SaveTICAndBPIPlots,
                                                            Options.CheckCentroidingStatus)
                {
                    HighResMS1 = highResMS1,
                    HighResMS2 = highResMS2
                };

                RegisterEvents(pWizParser);

                var ticStored = false;
                var srmDataCached = false;
                double runtimeMinutes = 0;

                // Note that SRM .Wiff files will only have chromatograms, and no spectra
                if (msDataFileReader.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                if (msDataFileReader.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    var skipExistingScans = (msDataFileReader.ChromatogramCount > 0);
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans, skipScansWithNoIons,
                                                  maxScansToTrackInDetail: MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                msDataFileReader.Dispose();
                AppUtils.GarbageCollectNow();
            }
            catch (AccessViolationException)
            {
                // Attempted to read or write protected memory. This is often an indication that other memory is corrupt.
                OnWarningEvent(
                    "Error reading instrument data with ProteoWizard: Attempted to read or write protected memory. " +
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

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath">Data file path</param>
        /// <param name="datasetFileInfo">Dataset file info</param>
        /// <returns>True if success, False if an error or if the file has no scans</returns>
        public abstract bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo);

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
            if (mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles.Count == 0)
                return;

            var fileInfo = new StringBuilder();

            if (mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles.Count == 1)
                fileInfo.AppendLine("Primary instrument file");
            else
                fileInfo.AppendLine("Primary instrument files");

            var maxLength = 0;

            foreach (var instrumentFile in mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles)
            {
                if (instrumentFile.Key.Length > maxLength)
                    maxLength = instrumentFile.Key.Length;
            }

            foreach (var instrumentFile in mDatasetStatsSummarizer.DatasetFileInfo.InstrumentFiles)
            {
                fileInfo.AppendFormat("  {0}  {1}  {2,14:N0} bytes",
                    instrumentFile.Value.Hash, instrumentFile.Key.PadRight(maxLength), instrumentFile.Value.Length).AppendLine();
            }

            OnDebugEvent(fileInfo.ToString());
        }

        [CLSCompliant(false)]
        protected void UpdateAcqStartAndEndTimes(DatasetFileInfo datasetFileInfo, MSDataFileReader msDataFileReader, DateTime runStartTime)
        {
            datasetFileInfo.AcqTimeStart = runStartTime;

            try
            {
                var lastSpectrumIndex = msDataFileReader.SpectrumCount - 1;
                var validMetadata = msDataFileReader.GetScanMetadata(lastSpectrumIndex, out var scanStartTime, out _, out _, out _, out _);

                if (validMetadata)
                {
                    datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(scanStartTime);
                }
            }
            catch (Exception ex)
            {
                OnWarningEvent("Error determining the elution time of the final spectrum in the file: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Store the creation time and last write time of the instrument file in mDatasetStatsSummarizer.DatasetFileInfo
        /// Initialize the Acquisition start/end times using to the last write time
        /// Computes the SHA-1 hash of the instrument file
        /// </summary>
        /// <param name="instrumentFile">Instrument file path</param>
        /// <param name="datasetID">Dataset ID</param>
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
        /// <param name="instrumentFile">Instrument file path</param>
        /// <param name="datasetID">Dataset ID</param>
        /// <param name="fileAdded">Output: true if the file was added</param>
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

                if (Options.DisableInstrumentHash)
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

                foreach (var outputFile in PathUtils.FindFilesWildcard(datasetDirectory, "*", true))
                {
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes += outputFile.Length;
                }

                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

                foreach (var dataFile in primaryDataFiles)
                {
                    if (!dataFile.Exists)
                        continue;

                    if (Options.DisableInstrumentHash)
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
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and this.DatasetFileInfo)
        /// </summary>
        /// <param name="inputFileName">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <returns>True if success; False if failure</returns>
        // ReSharper disable once UnusedMember.Global
        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputDirectoryPath)
        {
            return UpdateDatasetStatsTextFile(inputFileName, outputDirectoryPath, InfoScannerOptions.DEFAULT_DATASET_STATS_FILENAME);
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and this.DatasetFileInfo)
        /// The dataset stats summarizer does not check for duplicate entries; it simply appends a new line
        /// </summary>
        /// <param name="inputFileName">Input file path</param>
        /// <param name="outputDirectoryPath">Output directory path</param>
        /// <param name="datasetStatsFilename">Dataset stats file name</param>
        /// <returns>True if success; False if failure</returns>
        public bool UpdateDatasetStatsTextFile(string inputFileName, string outputDirectoryPath, string datasetStatsFilename)
        {
            bool success;

            try
            {
                if (string.IsNullOrWhiteSpace(datasetStatsFilename))
                {
                    datasetStatsFilename = InfoScannerOptions.DEFAULT_DATASET_STATS_FILENAME;
                }

                var datasetName = GetDatasetNameViaPath(inputFileName);

                var datasetStatsFilePath = Path.Combine(outputDirectoryPath, datasetStatsFilename);

                success = mDatasetStatsSummarizer.UpdateDatasetStatsTextFile(datasetName, datasetStatsFilePath);

                if (!success)
                {
                    OnErrorEvent("Error calling datasetStatsSummarizer.UpdateDatasetStatsTextFile: {0}", mDatasetStatsSummarizer.ErrorMessage);
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
            mTICAndBPIPlot.PlotWithPython = Options.PlotWithPython;

            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                plotContainer.PlotWithPython = Options.PlotWithPython;
            }

            if (Options.PlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
            {
                // This code shouldn't be reached; this setting should have already propagated
                mLCMS2DPlot.Options.PlotWithPython = Options.PlotWithPython;
                mLCMS2DPlotOverview.Options.PlotWithPython = Options.PlotWithPython;
            }

            mTICAndBPIPlot.DeleteTempFiles = !Options.ShowDebugInfo;

            foreach (var plotContainer in mInstrumentSpecificPlots)
            {
                plotContainer.DeleteTempFiles = !Options.ShowDebugInfo;
            }

            if (Options.PlotWithPython && !mLCMS2DPlot.Options.PlotWithPython)
            {
                // This code shouldn't be reached; this setting should have already propagated
                mLCMS2DPlot.Options.DeleteTempFiles = !Options.ShowDebugInfo;
                mLCMS2DPlotOverview.Options.DeleteTempFiles = !Options.ShowDebugInfo;
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
            var validData = mDatasetStatsSummarizer.ValidateMS2MzMin(Options.MS2MzMin, out var errorOrWarningMsg, MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED);

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
