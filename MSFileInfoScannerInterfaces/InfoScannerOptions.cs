﻿using System;
using PRISM;
// ReSharper disable UnusedMember.Global

namespace MSFileInfoScannerInterfaces
{
    public class InfoScannerOptions
    {
        // Ignore Spelling: ArgExistsProperty, centroiding, csv, OxyPlot, Html

        public const string DEFAULT_DATASET_STATS_FILENAME = "MSFileInfo_DatasetStats.txt";

        public const int DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK = 500;

        /// <summary>
        /// Default m/z threshold for iTRAQ labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_ITRAQ = 113;
        public const string MINIMUM_MZ_THRESHOLD_ITRAQ_STRING = "113";

        /// <summary>
        /// Default m/z threshold for TMT labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_TMT = 126;
        public const string MINIMUM_MZ_THRESHOLD_TMT_STRING = "126";

        /// <summary>
        /// Input file path
        /// </summary>
        /// <remarks>This path can contain wildcard characters, e.g. C:\*.raw</remarks>
        [Option("InputDataFilePath", "I", ArgPosition = 1, Required = true, HelpShowsDefault = false,
            HelpText = "The name of a file or directory to scan; the path can contain the wildcard character *")]
        public string InputDataFilePath { get; set; }

        [Option("OutputDirectory", "O", HelpShowsDefault = false,
            HelpText = "Output directory name.  If omitted, the output files will be created in the program directory.")]
        public string OutputDirectoryPath { get; set; }

        [Option("ParameterFile", "P", HelpShowsDefault = false,
            HelpText = "XML parameter file path. If supplied, it should point to a valid XML parameter file.\n" +
                       "Most options can alternatively be set using a Key=Value parameter file, which can be seen with /CreateParamFile")]
        public string ParameterFilePath { get; set; }

        /// <summary>
        /// When true, recurse subdirectories
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if MaxLevelsToRecurse is defined in the parameter file or if /S is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool RecurseDirectories { get; set; }

        [Option("MaxLevelsToRecurse", "S", ArgExistsProperty = nameof(RecurseDirectories), HelpShowsDefault = false,
            HelpText = "If supplied, process all valid files in the input directory and subdirectories.\n" +
                       "Include a number after /S (like /S:2) to limit the level of subdirectories to examine (0 means to recurse infinitely).")]
        public int MaxLevelsToRecurse { get; set; }

        [Option("IgnoreErrorsWhenRecursing", "IE", HelpShowsDefault = false,
            HelpText = "Ignore errors when recursing.")]
        public bool IgnoreErrorsWhenRecursing { get; set; }

        private string mLogFilePath;

        [Option("LogFile", "L", HelpShowsDefault = false,
            HelpText = "File path for logging messages.")]
        public string LogFilePath
        {
            get => mLogFilePath;
            set
            {
                mLogFilePath = value;
                LogMessagesToFile = !string.IsNullOrWhiteSpace(mLogFilePath);
            }
        }

        [Option("LogDirectoryPath", HelpShowsDefault = false,
            HelpText = "Directory to create log files")]
        public string LogDirectoryPath { get; set; }

        public bool LogMessagesToFile { get; private set; }

        /// <summary>
        /// Maximum points to plot on each LC/MS 2D plot
        /// </summary>
        /// <remarks>This value cannot be updated after class MSFileInfoScanner is instantiated</remarks>
        [Option("LCMS2DMaxPointsToPlot", "LC", "2D", ArgExistsProperty = nameof(SaveLCMS2DPlots),
            HelpText = "Create 2D LCMS plots (this process could take several minutes for each dataset), using the top N points.\n" +
                       "To plot the top 20000 points, use /LC:20000.")]
        public int LCMS2DMaxPointsToPlot { get; set; }

        /// <summary>
        /// The divisor to use when creating the overview 2D LCMS plots
        /// </summary>
        /// <remarks>This value cannot be updated after class MSFileInfoScanner is instantiated</remarks>
        [Option("LCMSPlotDivisor", "LCDiv",
            HelpText = "The divisor to use when creating the overview 2D LCMS plots.\n" +
                       "Use /LCDiv:0 to disable creation of the overview plots.")]
        public int LCMS2DOverviewPlotDivisor { get; set; }

        [Option("SaveTICAndBPIPlots", "TIC", HelpShowsDefault = false,
            HelpText = "Use /TIC:False to disable saving TIC and BPI plots; also disables any device specific plots")]
        public bool SaveTICAndBPIPlots { get; set; }

        [Option("CreateGradientPlots", "LCGrad", HelpShowsDefault = false,
            HelpText = "Save a series of 2D LC plots, each using a different color scheme.\n" +
                       "The default color scheme is OxyPalettes.Jet")]
        public bool TestLCMSGradientColorSchemes { get; set; }

        [Option("UseDatasetNameForHtmlPlotsFile", "CustomHtmlPlotsFile", HelpShowsDefault = false,
            HelpText = "The HTML file for viewing plots is named index.html by default;\n" +
                       "set this to True to name the file based on the input file name\n" +
                       "This is auto-set to true if the input file spec has a wildcard")]
        public bool UseDatasetNameForHtmlPlotsFile { get; set; }

        [Option("DatasetID", HelpShowsDefault = false,
            HelpText = "Define the dataset's DatasetID value (where # is an integer); " +
                       "only appropriate if processing a single dataset",
            Min = 0)]
        public int DatasetID { get; set; }

        [Option("CreateDatasetInfoFile", "DI", HelpShowsDefault = false,
            HelpText = "If supplied, create a dataset info XML file for each dataset.")]
        public bool CreateDatasetInfoFile { get; set; }

        [Option("CreateScanStatsFile", "SS", HelpShowsDefault = false,
            HelpText = "If supplied, create a _ScanStats.txt  file for each dataset.")]
        public bool CreateScanStatsFile { get; set; }

        [Option("ComputeQualityScores", "QS", HelpShowsDefault = false,
            HelpText = "If supplied, compute an overall quality score for the data in each datasets.")]
        public bool ComputeOverallQualityScores { get; set; }

        [Option("CheckCentroidingStatus", "CC", HelpShowsDefault = false,
            HelpText = "If supplied, check spectral data for whether it is centroided or profile")]
        public bool CheckCentroidingStatus { get; set; }

        [Option("CopyFileLocalOnReadError", "CopyLocalOnError", HelpShowsDefault = false,
            HelpText = "When true, if an error is encountered while reading the file, copy it to the local drive and try again")]
        public bool CopyFileLocalOnReadError { get; set; }

        /// <summary>
        /// When true, create 2D LCMS plots
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if LCMS2DMaxPointsToPlot is defined in the parameter file or if /LC is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool SaveLCMS2DPlots { get; set; }

        [Option("MS2MzMin", HelpShowsDefault = false,
            HelpText = "If supplied, specifies a minimum m/z value that all MS/MS spectra should have.\n" +
                       "Will report an error if any MS/MS spectra have minimum m/z value larger than the threshold.\n" +
                       "Useful for validating instrument files where the sample is iTRAQ or TMT labeled " +
                       "and it is important to detect the reporter ions in the MS/MS spectra. " +
                       "\n  - select the default iTRAQ m/z (" + MINIMUM_MZ_THRESHOLD_ITRAQ_STRING + ") using /MS2MzMin:iTRAQ" +
                       "\n  - select the default TMT m/z (" + MINIMUM_MZ_THRESHOLD_TMT_STRING + ") using /MS2MzMin:TMT" +
                       "\n  - specify a m/z value using /MS2MzMin:110")]
        public string MS2MzMinString { get; set; }

        public float MS2MzMin { get; set; }

        [Option("DisableInstrumentHash", "NoHash", HelpShowsDefault = false,
            HelpText = "If supplied, disables creating a SHA-1 hash for the primary instrument data file(s).")]
        public bool DisableInstrumentHash { get; set; }

        /// <summary>
        /// When true, update (or create) a tab-delimited text file with overview stats for each dataset
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if DatasetStatsTextFileName is defined in the parameter file or if /DST is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool UpdateDatasetStatsTextFile { get; set; }

        [Option("DatasetStatsTextFileName", "DST", ArgExistsProperty = nameof(UpdateDatasetStatsTextFile), HelpShowsDefault = false,
            HelpText = "If supplied, update (or create) a tab-delimited text file with overview stats for the dataset.\n" +
                       "If /DI is used (or CreateDatasetInfoFile=True), will include detailed scan counts; otherwise, will just have the dataset name, " +
                       "acquisition date, and (if available) sample name and comment.\n" +
                       "By default, the file is named " + DEFAULT_DATASET_STATS_FILENAME + "; " +
                       "to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt")]
        public string DatasetStatsTextFileName { get; set; }

        [Option("ScanStart", "Start", HelpShowsDefault = false,
            HelpText = "Use to limit the scan range to process; useful for files where the first few scans are corrupt.\n" +
                       "For example, to start processing at scan 10, use /ScanStart:10",
            Min = 0)]
        public int ScanStart { get; set; }

        [Option("ScanEnd", "End", HelpShowsDefault = false,
            HelpText = "Use to limit the scan range to process; useful for files where the first few scans are corrupt.\n" +
                       "For example, to start processing at scan 10, use /ScanStart:10",
            Min = 0)]
        public int ScanEnd { get; set; }

        [Option("ShowDebugInfo", "Debug", HelpShowsDefault = false,
            HelpText = "If supplied, display debug information at the console, " +
                       "including showing the scan number prior to reading each scan's data.\n" +
                       "Also, when /Debug is enabled, temporary files for creating plots with Python will not be deleted.")]
        public bool ShowDebugInfo { get; set; }

        private bool mCheckFileIntegrity;

        [Option("CheckFileIntegrity", "CheckIntegrity", "C", HelpShowsDefault = false,
            HelpText = "Use to perform an integrity check on all known file types; " +
                       "this process will open known file types and verify that they contain the expected data.\n" +
                       "This option is only used if you specify an Input Directory and use a wildcard; " +
                       "you will typically also want to use /S when using /C.")]
        public bool CheckFileIntegrity
        {
            get => mCheckFileIntegrity;
            set
            {
                mCheckFileIntegrity = value;
                if (mCheckFileIntegrity)
                {
                    // Make sure Cache Files are enabled
                    UseCacheFiles = true;
                }
            }
        }

        /// <summary>
        /// Maximum number of lines to process when checking text or csv files
        /// </summary>
        /// <remarks>This value cannot be updated after class MSFileInfoScanner is instantiated</remarks>
        [Option("MaximumTextFileLinesToCheck", "M",
            HelpText = "Use to define the maximum number of lines to process when checking text or csv files.", Min = 1)]
        public int MaximumTextFileLinesToCheck { get; set; }

        /// <summary>
        /// Maximum number of XML nodes to examine
        /// </summary>
        /// <remarks>This value cannot be updated after class MSFileInfoScanner is instantiated</remarks>
        public int MaximumXMLElementNodesToCheck { get; set; }

        [Option("ComputeFileHashes", "H", HelpShowsDefault = false,
            HelpText = "If supplied, compute SHA-1 file hashes when verifying file integrity.")]
        public bool ComputeFileHashes { get; set; }

        [Option("ZipFileCheckAllData", "QZ", HelpShowsDefault = false,
            HelpText = "If supplied, run a quick zip-file validation test when verifying file integrity " +
                       "(the test does not check all data in the .Zip file).")]
        public bool ZipFileCheckAllData { get; set; }

        [Option("UseCacheFiles", "CF", HelpShowsDefault = false,
            HelpText = "If supplied, save/load information from the acquisition time file (cache file).\n" +
                       "This option is auto-enabled if you use /C.")]
        public bool UseCacheFiles { get; set; }

        [Option("ReprocessExistingFiles", "R", HelpShowsDefault = false,
            HelpText = "If supplied, reprocess files that are already defined in the acquisition time file.")]
        public bool ReprocessExistingFiles { get; set; }

        [Option("ReprocessIfCachedSizeIsZero", "Z", HelpShowsDefault = false,
            HelpText = "If supplied, reprocess files that are already defined in the acquisition time file " +
                        "only if their cached size is 0 bytes.")]
        public bool ReprocessIfCachedSizeIsZero { get; set; }

        [Option("PostResultsToDMS", "PostToDMS", HelpShowsDefault = false,
            HelpText = "If supplied, store the dataset info in the DMS database.\n" +
                       "To customize the server name and/or stored procedure to use for posting, " +
                       "use an XML parameter file with settings DSInfoConnectionString, " +
                       "DSInfoDBPostingEnabled, and DSInfoStoredProcedure")]
        public bool PostResultsToDMS { get; set; }

        [Option("DatabaseConnectionString", "ConnectionString", HelpShowsDefault = true,
            HelpText = "Connection string for storing dataset info in DMS")]
        public string DatabaseConnectionString { get; set; } = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

        [Option("DSInfoStoredProcedure",HelpShowsDefault = true,
            HelpText = "Procedure to call to store dataset info in DMS")]
        public string DSInfoStoredProcedure { get; set; } = "UpdateDatasetFileInfoXML";

        [Option("PythonPlot", "Python", HelpShowsDefault = false,
            HelpText = "If supplied, create plots with Python instead of OxyPlot")]
        public bool PlotWithPython { get; set; }

        public InfoScannerOptions()
        {
            InputDataFilePath = string.Empty;
            OutputDirectoryPath = string.Empty;
            ParameterFilePath = string.Empty;
            mLogFilePath = string.Empty;

            RecurseDirectories = false;

            // If maxLevelsToRecurse is <=0, we recurse infinitely
            MaxLevelsToRecurse = 0;
            IgnoreErrorsWhenRecursing = false;

            ReprocessExistingFiles = false;
            ReprocessIfCachedSizeIsZero = false;
            UseCacheFiles = false;

            SaveTICAndBPIPlots = true;
            SaveLCMS2DPlots = false;
            LCMS2DMaxPointsToPlot = LCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT;
            LCMS2DOverviewPlotDivisor = LCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;
            TestLCMSGradientColorSchemes = false;

            UseDatasetNameForHtmlPlotsFile = false;

            CheckCentroidingStatus = false;
            CopyFileLocalOnReadError = false;

            MS2MzMin = 0;
            DisableInstrumentHash = false;

            ScanStart = 0;
            ScanEnd = 0;
            ShowDebugInfo = false;

            ComputeOverallQualityScores = false;
            CreateDatasetInfoFile = false;
            CreateScanStatsFile = false;

            UpdateDatasetStatsTextFile = false;
            DatasetStatsTextFileName = string.Empty;

            CheckFileIntegrity = false;
            ComputeFileHashes = false;
            ZipFileCheckAllData = true;

            MaximumTextFileLinesToCheck = DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;

            PostResultsToDMS = false;
            PlotWithPython = false;
        }

        /// <summary>
        /// Validate the options
        /// </summary>
        /// <returns>True if all options are valid</returns>
        /// <remarks>This method is called from Program.cs</remarks>
        // ReSharper disable once UnusedMember.Global
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InputDataFilePath))
            {
                ConsoleMsgUtils.ShowError($"ERROR: Input path must be provided and non-empty; \"{InputDataFilePath}\" was provided.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MS2MzMinString))
            {
                if (MS2MzMinString.StartsWith("iTRAQ", StringComparison.OrdinalIgnoreCase))
                {
                    MS2MzMin = MINIMUM_MZ_THRESHOLD_ITRAQ;
                }
                else if (MS2MzMinString.StartsWith("TMT", StringComparison.OrdinalIgnoreCase))
                {
                    MS2MzMin = MINIMUM_MZ_THRESHOLD_TMT;
                }
                else
                {
                    if (float.TryParse(MS2MzMinString, out var mzMin))
                    {
                        MS2MzMin = mzMin;
                    }
                    else
                    {
                        ConsoleMsgUtils.ShowWarning("Ignoring invalid m/z value for /MS2MzMin: " + MS2MzMinString);
                    }
                }
            }

            if (CheckFileIntegrity)
            {
                UseCacheFiles = true;
            }

            if (ScanStart > ScanEnd && ScanEnd != 0)
            {
                ConsoleMsgUtils.ShowError($"ERROR: When ScanStart and ScanEnd are both >0, ScanStart ({ScanStart}) cannot be greater than ScanEnd ({ScanEnd})!");
                return false;
            }

            return true;
        }
    }
}