using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    /// -------------------------------------------------------------------------------
    /// This program scans a series of MS data files (or data directories) and extracts the acquisition start and end times,
    /// number of spectra, and the total size of the Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
    ///
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started in 2005
    /// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
    ///
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    /// -------------------------------------------------------------------------------
    static class modMain
    {

        public const string PROGRAM_DATE = "September 9, 2019";

        // This path can contain wildcard characters, e.g. C:\*.raw
        private static string mInputDataFilePath;

        // Optional
        private static string mOutputDirectoryName;

        // Optional
        private static string mParameterFilePath;

        private static string mLogFilePath;
        private static bool mRecurseDirectories;
        private static int mMaxLevelsToRecurse;

        private static bool mIgnoreErrorsWhenRecursing;
        private static bool mReprocessingExistingFiles;
        private static bool mReprocessIfCachedSizeIsZero;

        private static bool mUseCacheFiles;
        private static bool mSaveTICAndBPIPlots;
        private static bool mSaveLCMS2DPlots;
        private static int mLCMS2DMaxPointsToPlot;
        private static int mLCMS2DOverviewPlotDivisor;

        private static bool mTestLCMSGradientColorSchemes;

        private static bool mCheckCentroidingStatus;
        private static float mMS2MzMin;
        private static bool mDisableInstrumentHash;

        private static int mScanStart;
        private static int mScanEnd;

        private static bool mShowDebugInfo;

        private static int mDatasetID;
        private static bool mComputeOverallQualityScores;
        private static bool mCreateDatasetInfoFile;

        private static bool mCreateScanStatsFile;
        private static bool mUpdateDatasetStatsTextFile;

        private static string mDatasetStatsTextFileName;
        private static bool mCheckFileIntegrity;
        private static int mMaximumTextFileLinesToCheck;
        private static bool mComputeFileHashes;

        private static bool mZipFileCheckAllData;

        private static bool mPostResultsToDMS;

        private static bool mPlotWithPython;

        private static DateTime mLastProgressTime;

        /// <summary>
        /// Main function
        /// </summary>
        /// <returns>0 if no error, error code if an error</returns>
        /// <remarks>The STAThread attribute is required for OxyPlot functionality</remarks>
        [STAThread]
        public static int Main()
        {
            var commandLineParser = new clsParseCommandLine();

            mInputDataFilePath = string.Empty;
            mOutputDirectoryName = string.Empty;
            mParameterFilePath = string.Empty;
            mLogFilePath = string.Empty;

            mRecurseDirectories = false;
            mMaxLevelsToRecurse = 2;
            mIgnoreErrorsWhenRecursing = false;

            mReprocessingExistingFiles = false;
            mReprocessIfCachedSizeIsZero = false;
            mUseCacheFiles = false;

            mSaveTICAndBPIPlots = true;
            mSaveLCMS2DPlots = false;
            mLCMS2DMaxPointsToPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT;
            mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;
            mTestLCMSGradientColorSchemes = false;

            mCheckCentroidingStatus = false;
            mMS2MzMin = 0;
            mDisableInstrumentHash = false;

            mScanStart = 0;
            mScanEnd = 0;
            mShowDebugInfo = false;

            mComputeOverallQualityScores = false;
            mCreateDatasetInfoFile = false;
            mCreateScanStatsFile = false;

            mUpdateDatasetStatsTextFile = false;
            mDatasetStatsTextFileName = string.Empty;

            mCheckFileIntegrity = false;
            mComputeFileHashes = false;
            mZipFileCheckAllData = true;

            mMaximumTextFileLinesToCheck = clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;

            mPostResultsToDMS = false;
            mPlotWithPython = false;

            mLastProgressTime = DateTime.UtcNow;

            try
            {
                var proceed = false;
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        proceed = true;
                }

                if (mInputDataFilePath == null)
                    mInputDataFilePath = string.Empty;


                if (!proceed ||
                    commandLineParser.NeedToShowHelp ||
                    commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 ||
                    mInputDataFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }

                var scanner = new clsMSFileInfoScanner();

                scanner.DebugEvent += MSFileScanner_DebugEvent;
                scanner.ErrorEvent += MSFileScanner_ErrorEvent;
                scanner.WarningEvent += MSFileScanner_WarningEvent;
                scanner.StatusEvent += MSFileScanner_MessageEvent;
                scanner.ProgressUpdate += MSFileScanner_ProgressUpdate;

                if (mCheckFileIntegrity)
                    mUseCacheFiles = true;

                // Note: These values will be overridden if /P was used and they are defined in the parameter file

                scanner.UseCacheFiles = mUseCacheFiles;
                scanner.ReprocessExistingFiles = mReprocessingExistingFiles;
                scanner.ReprocessIfCachedSizeIsZero = mReprocessIfCachedSizeIsZero;

                scanner.PlotWithPython = mPlotWithPython;
                scanner.SaveTICAndBPIPlots = mSaveTICAndBPIPlots;
                scanner.SaveLCMS2DPlots = mSaveLCMS2DPlots;
                scanner.LCMS2DPlotMaxPointsToPlot = mLCMS2DMaxPointsToPlot;
                scanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;
                scanner.TestLCMSGradientColorSchemes = mTestLCMSGradientColorSchemes;

                scanner.CheckCentroidingStatus = mCheckCentroidingStatus;
                scanner.MS2MzMin = mMS2MzMin;
                scanner.DisableInstrumentHash = mDisableInstrumentHash;

                scanner.ScanStart = mScanStart;
                scanner.ScanEnd = mScanEnd;
                scanner.ShowDebugInfo = mShowDebugInfo;

                scanner.ComputeOverallQualityScores = mComputeOverallQualityScores;
                scanner.CreateDatasetInfoFile = mCreateDatasetInfoFile;
                scanner.CreateScanStatsFile = mCreateScanStatsFile;

                scanner.UpdateDatasetStatsTextFile = mUpdateDatasetStatsTextFile;
                scanner.DatasetStatsTextFileName = mDatasetStatsTextFileName;

                scanner.CheckFileIntegrity = mCheckFileIntegrity;
                scanner.RecheckFileIntegrityForExistingDirectories = mReprocessingExistingFiles;

                scanner.MaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck;
                scanner.ComputeFileHashes = mComputeFileHashes;
                scanner.ZipFileCheckAllData = mZipFileCheckAllData;

                scanner.IgnoreErrorsWhenRecursing = mIgnoreErrorsWhenRecursing;

                if (mLogFilePath.Length > 0)
                {
                    scanner.LogMessagesToFile = true;
                    scanner.LogFilePath = mLogFilePath;
                }

                scanner.DatasetIDOverride = mDatasetID;
                scanner.DSInfoDBPostingEnabled = mPostResultsToDMS;

                if (!string.IsNullOrEmpty(mParameterFilePath))
                {
                    scanner.LoadParameterFileSettings(mParameterFilePath);
                }

                scanner.ShowCurrentProcessingOptions();

                bool processingError;

                int returnCode;
                if (mRecurseDirectories)
                {
                    if (scanner.ProcessMSFilesAndRecurseDirectories(mInputDataFilePath, mOutputDirectoryName, mMaxLevelsToRecurse))
                    {
                        returnCode = 0;
                        processingError = false;
                    }
                    else
                    {
                        returnCode = (int)scanner.ErrorCode;
                        processingError = true;
                    }
                }
                else
                {
                    if (scanner.ProcessMSFileOrDirectoryWildcard(mInputDataFilePath, mOutputDirectoryName, true))
                    {
                        returnCode = 0;
                        processingError = false;
                    }
                    else
                    {
                        returnCode = (int)scanner.ErrorCode;
                        processingError = true;
                    }
                }

                if (processingError)
                {
                    if (returnCode != 0)
                    {
                        ShowErrorMessage("Error while processing: " + scanner.GetErrorMessage());
                    }
                    else
                    {
                        ShowErrorMessage("Unknown error while processing (ProcessMSFileOrDirectoryWildcard returned false but the ErrorCode is 0)");
                    }

                    System.Threading.Thread.Sleep(1500);
                }
                else if (scanner.ErrorCode == iMSFileInfoScanner.eMSFileScannerErrorCodes.MS2MzMinValidationWarning)
                {
                    ConsoleMsgUtils.ShowWarning("MS2MzMin validation warning: " + scanner.MS2MzMinValidationMessage);
                }

                scanner.SaveCachedResults();

                return returnCode;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main", ex);
                return -1;
            }

        }

        private static string GetAppVersion()
        {
            return PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.GetAppVersion(PROGRAM_DATE);
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine parser)
        {
            // Returns True if no problems; otherwise, returns false

            var validParameters = new List<string> {
                "I",
                "O",
                "P",
                "S",
                "IE",
                "L",
                "C",
                "CheckIntegrity",
                "M",
                "H",
                "QZ",
                "NoTIC",
                "LC",
                "2D",
                "LCDiv",
                "LCGrad",
                "CC",
                "MS2MzMin",
                "NoHash",
                "QS",
                "ScanStart",
                "ScanEnd",
                "Start",
                "End",
                "DatasetID",
                "DI",
                "DST",
                "SS",
                "CF",
                "R",
                "Z",
                "PostToDMS",
                "Debug",
                "Python",
                "PythonPlot"
            };

            try
            {
                // Make sure no invalid parameters are present
                if (parser.InvalidParametersPresent(validParameters))
                {
                    var invalidArgs = (from item in parser.InvalidParameters(validParameters) select "/" + item).ToList();
                    ConsoleMsgUtils.ShowErrors("Invalid command line parameters", invalidArgs);
                    return false;
                }

                // Query parser to see if various parameters are present
                if (parser.RetrieveValueForParameter("I", out var value))
                {
                    mInputDataFilePath = value;
                }
                else if (parser.NonSwitchParameterCount > 0)
                {
                    // Treat the first non-switch parameter as the input file
                    mInputDataFilePath = parser.RetrieveNonSwitchParameter(0);
                }

                if (parser.RetrieveValueForParameter("O", out value))
                    mOutputDirectoryName = value;
                if (parser.RetrieveValueForParameter("P", out value))
                    mParameterFilePath = value;

                if (parser.RetrieveValueForParameter("S", out value))
                {
                    mRecurseDirectories = true;
                    if (int.TryParse(value, out var maxLevels))
                    {
                        mMaxLevelsToRecurse = maxLevels;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid max depth value for /S: " + value);
                    }
                }
                if (parser.RetrieveValueForParameter("IE", out value))
                    mIgnoreErrorsWhenRecursing = true;

                if (parser.RetrieveValueForParameter("L", out value))
                    mLogFilePath = value;

                if (parser.IsParameterPresent("C") || parser.IsParameterPresent("CheckIntegrity"))
                    mCheckFileIntegrity = true;

                if (parser.RetrieveValueForParameter("M", out value))
                {
                    if (int.TryParse(value, out var maxLines))
                    {
                        mMaximumTextFileLinesToCheck = maxLines;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid max file lines value for /M: " + value);
                    }
                }

                if (parser.IsParameterPresent("H"))
                    mComputeFileHashes = true;
                if (parser.IsParameterPresent("QZ"))
                    mZipFileCheckAllData = false;

                if (parser.IsParameterPresent("NoTIC"))
                    mSaveTICAndBPIPlots = false;

                var lcPlotsEnabled = parser.RetrieveValueForParameter("LC", out var maxValidPoints);

                if (!lcPlotsEnabled)
                {
                    lcPlotsEnabled = parser.RetrieveValueForParameter("2D", out maxValidPoints);
                }

                if (lcPlotsEnabled)
                {
                    mSaveLCMS2DPlots = true;
                    if (!string.IsNullOrWhiteSpace(maxValidPoints))
                    {
                        if (int.TryParse(maxValidPoints, out var maxPoints))
                        {
                            mLCMS2DMaxPointsToPlot = maxPoints;
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowWarning("Ignoring invalid max points value for /LC: " + maxValidPoints);
                        }
                    }
                }

                if (parser.RetrieveValueForParameter("LCDiv", out value))
                {
                    if (int.TryParse(value, out var plotDivisor))
                    {
                        mLCMS2DOverviewPlotDivisor = plotDivisor;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid divisor value for /LCDiv: " + value);
                    }
                }

                if (parser.IsParameterPresent("LCGrad"))
                    mTestLCMSGradientColorSchemes = true;

                if (parser.IsParameterPresent("CC"))
                    mCheckCentroidingStatus = true;

                if (parser.RetrieveValueForParameter("MS2MzMin", out value))
                {
                    if (value.StartsWith("itraq", StringComparison.OrdinalIgnoreCase))
                    {
                        mMS2MzMin = clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_ITRAQ;
                    }
                    else if (value.StartsWith("tmt", StringComparison.OrdinalIgnoreCase))
                    {
                        mMS2MzMin = clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_TMT;
                    }
                    else
                    {

                        if (float.TryParse(value, out var mzMin))
                        {
                            mMS2MzMin = mzMin;
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowWarning("Ignoring invalid m/z value for /MS2MzMin: " + value);
                        }
                    }
                }

                if (parser.IsParameterPresent("NoHash"))
                    mDisableInstrumentHash = false;

                if (parser.RetrieveValueForParameter("ScanStart", out value))
                {
                    if (int.TryParse(value, out var scanStart))
                    {
                        mScanStart = scanStart;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid scan number for /ScanStart: " + value);
                    }
                }
                else
                {
                    if (parser.RetrieveValueForParameter("Start", out value))
                    {
                        if (int.TryParse(value, out var scanStart))
                        {
                            mScanStart = scanStart;
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowWarning("Ignoring invalid scan number for /Start: " + value);
                        }
                    }
                }

                if (parser.RetrieveValueForParameter("ScanEnd", out value))
                {
                    if (int.TryParse(value, out var scanEnd))
                    {
                        mScanEnd = scanEnd;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid scan number for /ScanEnd: " + value);
                    }
                }
                else
                {
                    if (parser.RetrieveValueForParameter("End", out value))
                    {
                        if (int.TryParse(value, out var scanEnd))
                        {
                            mScanEnd = scanEnd;
                        }
                        else
                        {
                            ConsoleMsgUtils.ShowWarning("Ignoring invalid scan number for /End: " + value);
                        }
                    }
                }

                if (parser.IsParameterPresent("Debug"))
                    mShowDebugInfo = true;

                if (parser.IsParameterPresent("QS"))
                    mComputeOverallQualityScores = true;

                if (parser.RetrieveValueForParameter("DatasetID", out value))
                {
                    if (!int.TryParse(value, out mDatasetID))
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid dataset ID for /DatasetID: " + value);
                    }
                }

                if (parser.IsParameterPresent("DI"))
                    mCreateDatasetInfoFile = true;

                if (parser.IsParameterPresent("SS"))
                    mCreateScanStatsFile = true;

                if (parser.RetrieveValueForParameter("DST", out value))
                {
                    mUpdateDatasetStatsTextFile = true;
                    if (!string.IsNullOrEmpty(value))
                    {
                        mDatasetStatsTextFileName = value;
                    }
                }

                if (parser.IsParameterPresent("CF"))
                    mUseCacheFiles = true;

                if (parser.IsParameterPresent("R"))
                    mReprocessingExistingFiles = true;

                if (parser.IsParameterPresent("Z"))
                    mReprocessIfCachedSizeIsZero = true;

                if (parser.IsParameterPresent("PostToDMS"))
                    mPostResultsToDMS = true;

                if (parser.IsParameterPresent("PythonPlot") || parser.IsParameterPresent("Python"))
                    mPlotWithPython = true;

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters", ex);
                return false;
            }

        }

        private static string CollapseList(List<string> itemList)
        {
            return string.Join(", ", itemList);
        }

        private static void ShowErrorMessage(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowProgramHelp()
        {
            try
            {
                var scanner = new clsMSFileInfoScanner();
                var exePath = PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.GetAppPath();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "This program will scan a series of MS data files (or data directories) and " +
                                      "extract the acquisition start and end times, number of spectra, and the " +
                                      "total size of the data, saving the values in the file " +
                                      clsMSFileInfoScanner.DefaultAcquisitionTimeFilename + ". " +
                                      "Supported file types are Thermo .RAW files, Agilent Ion Trap (.D directories), " +
                                      "Agilent or QStar/QTrap .WIFF files, MassLynx .Raw directories, Bruker 1 directories, " +
                                      "Bruker XMass analysis.baf files, .UIMF files (IMS), " +
                                      "zipped Bruker imaging datasets (with 0_R*.zip files), and " +
                                      "DeconTools _isos.csv files"));

                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + Path.GetFileName(exePath));
                Console.WriteLine(" /I:InputFileNameOrDirectoryPath [/O:OutputDirectoryName]");
                Console.WriteLine(" [/P:ParamFilePath] [/S[:MaxLevel]] [/IE] [/L:LogFilePath]");
                Console.WriteLine(" [/LC[:MaxPointsToPlot]] [/NoTIC] [/LCGrad]");
                Console.WriteLine(" [/DI] [/SS] [/QS] [/CC]");
                Console.WriteLine(" [/MS2MzMin:MzValue] [/NoHash]");
                Console.WriteLine(" [/DST:DatasetStatsFileName]");
                Console.WriteLine(" [/ScanStart:0] [/ScanEnd:0] [/Debug]");
                Console.WriteLine(" [/C] [/M:nnn] [/H] [/QZ]");
                Console.WriteLine(" [/CF] [/R] [/Z]");
                Console.WriteLine(" [/PostToDMS] [/PythonPlot]");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /I to specify the name of a file or directory to scan; the path can contain the wildcard character *"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "The output directory name is optional.  If omitted, the output files will be created in the program directory."));
                Console.WriteLine();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "The param file switch is optional. " +
                                      "If supplied, it should point to a valid XML parameter file. " +
                                      "If omitted, defaults are used."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /S to process all valid files in the input directory and subdirectories. " +
                                      "Include a number after /S (like /S:2) to limit the level of subdirectories to examine. " +
                                      "Use /IE to ignore errors when recursing."));
                Console.WriteLine("Use /L to specify the file path for logging messages.");
                Console.WriteLine();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /LC or /2D to create 2D LCMS plots (this process could take several minutes for each dataset). " +
                                      "By default, plots the top " + clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT + " points." +
                                      "To plot the top 20000 points, use /LC:20000."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /LCDiv to specify the divisor to use when creating the overview 2D LCMS plots. " +
                                      "By default, uses /LCDiv:" + clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR + "; " +
                                      "use /LCDiv:0 to disable creation of the overview plots."));
                Console.WriteLine("Use /NoTIC to not save TIC and BPI plots.");
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /LCGrad to save a series of 2D LC plots, each using a different color scheme. " +
                                      "The default color scheme is OxyPalettes.Jet"));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /DatasetID:# to define the dataset's DatasetID value (where # is an integer); " +
                                      "only appropriate if processing a single dataset"));
                Console.WriteLine("Use /DI to create a dataset info XML file for each dataset.");
                Console.WriteLine();
                Console.WriteLine("Use /SS to create a _ScanStats.txt  file for each dataset.");
                Console.WriteLine("Use /QS to compute an overall quality score for the data in each datasets.");
                Console.WriteLine("Use /CC to check spectral data for whether it is centroided or profile");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /MS2MzMin to specify a minimum m/z value that all MS/MS spectra should have. " +
                                      "Will report an error if any MS/MS spectra have minimum m/z value larger than the threshold. " +
                                      "Useful for validating instrument files where the sample is iTRAQ or TMT labeled " +
                                      "and it is important to detect the reporter ions in the MS/MS spectra"));
                Console.WriteLine("  - select the default iTRAQ m/z ({0}) using /MS2MzMin:iTRAQ", clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_ITRAQ);
                Console.WriteLine("  - select the default TMT m/z ({0}) using /MS2MzMin:TMT", clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_TMT);
                Console.WriteLine("  - specify a m/z value using /MS2MzMin:110");
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "A SHA-1 hash is computed for the primary instrument data file(s). " +
                                      "Use /NoHash to disable this"));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /DST to update (or create) a tab-delimited text file with overview stats for the dataset. " +
                                      "If /DI is used, will include detailed scan counts; otherwise, will just have the dataset name, " +
                                      "acquisition date, and (if available) sample name and comment. " +
                                      "By default, the file is named " + DatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME + "; " +
                                      "to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt"));
                Console.WriteLine();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /ScanStart and /ScanEnd to limit the scan range to process; " +
                                      "useful for files where the first few scans are corrupt. " +
                                    "For example, to start processing at scan 10, use /ScanStart:10"));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /Debug to display debug information at the console, " +
                                      "including showing the scan number prior to reading each scan's data. " +
                                      "Also, when /Debug is enabled, temporary files for creating plots with Python will not be deleted."));
                Console.WriteLine();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /C to perform an integrity check on all known file types; " +
                                      "this process will open known file types and verify that they contain the expected data. " +
                                      "This option is only used if you specify an Input Directory and use a wildcard; " +
                                      "you will typically also want to use /S when using /C."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /M to define the maximum number of lines to process when checking text or csv files; " +
                                      "default is /M:" + clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK));
                Console.WriteLine();

                Console.WriteLine("Use /H to compute SHA-1 file hashes when verifying file integrity.");
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /QZ to run a quick zip-file validation test when verifying file integrity " +
                                      "(the test does not check all data in the .Zip file)."));
                Console.WriteLine();

                Console.WriteLine(ConsoleMsgUtils.WrapParagraph
                                      ("Use /CF to save/load information from the acquisition time file (cache file). " +
                                       "This option is auto-enabled if you use /C."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /R to reprocess files that are already defined in the acquisition time file."));
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /Z to reprocess files that are already defined in the acquisition time file " +
                                      "only if their cached size is 0 bytes."));
                Console.WriteLine();
                Console.WriteLine(ConsoleMsgUtils.WrapParagraph(
                                      "Use /PostToDMS to store the dataset info in the DMS database. " +
                                      "To customize the server name and/or stored procedure to use for posting, " +
                                      "use an XML parameter file with settings DSInfoConnectionString, " +
                                      "DSInfoDBPostingEnabled, and DSInfoStoredProcedure"));
                Console.WriteLine();
                Console.WriteLine("Use /PythonPlot to create plots with Python instead of OxyPlot");
                Console.WriteLine();
                Console.WriteLine("Known file extensions: " + CollapseList(scanner.GetKnownFileExtensionsList()));
                Console.WriteLine("Known directory extensions: " + CollapseList(scanner.GetKnownDirectoryExtensionsList()));
                Console.WriteLine();

                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message);
            }

        }

        private static void MSFileScanner_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void MSFileScanner_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowErrorCustom(message, ex, false);
        }

        private static void MSFileScanner_MessageEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void MSFileScanner_ProgressUpdate(string progressMessage, float percentComplete)
        {
            if (DateTime.UtcNow.Subtract(mLastProgressTime).TotalSeconds < 5)
                return;

            Console.WriteLine();
            mLastProgressTime = DateTime.UtcNow;
            MSFileScanner_DebugEvent(percentComplete.ToString("0.0") + "%, " + progressMessage);
        }

        private static void MSFileScanner_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

    }
}
