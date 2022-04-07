using System;
using PRISM;

// ReSharper disable UnusedMember.Global

namespace MSFileInfoScannerInterfaces
{
    public class InfoScannerOptions
    {
        // Ignore Spelling: ArgExistsProperty, centroided, centroiding, conf, csv, deisotoped, Html, OxyPlot

        /// <summary>
        /// Default dataset stats text file name
        /// </summary>
        public const string DEFAULT_DATASET_STATS_FILENAME = "MSFileInfo_DatasetStats.txt";

        /// <summary>
        /// Default maximum number of lines to check in text files
        /// </summary>
        public const int DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK = 500;

        /// <summary>
        /// Default maximum number of XML nodes to examine in XML files
        /// </summary>
        public const int DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK = 500;

        /// <summary>
        /// Default m/z threshold for iTRAQ labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_ITRAQ = 113;

        /// <summary>
        /// String version of the default m/z threshold for iTRAQ labeled samples
        /// </summary>
        public const string MINIMUM_MZ_THRESHOLD_ITRAQ_STRING = "113";

        /// <summary>
        /// Default m/z threshold for TMT labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_TMT = 126;

        /// <summary>
        /// String version of the default m/z threshold for TMT labeled samples
        /// </summary>
        public const string MINIMUM_MZ_THRESHOLD_TMT_STRING = "126";

        /// <summary>
        /// Input file path
        /// </summary>
        /// <remarks>This path can contain wildcard characters, e.g. C:\*.raw</remarks>
        [Option("InputFilePath", "InputDataFilePath", "I",
            ArgPosition = 1, Required = true, HelpShowsDefault = false, IsInputFilePath = true,
            HelpText = "The name of a file or directory to process; the path can contain the wildcard character *\n" +
                       "Either define this at the command line using /I or in a parameter file")]
        public string InputDataFilePath { get; set; }

        /// <summary>
        /// Output directory path
        /// </summary>
        [Option("OutputDirectoryPath", "OutputDirectory", "O", HelpShowsDefault = false,
            HelpText = "Output directory name (or full path)\n" +
                       "If omitted, the output files will be created in the program directory")]
        public string OutputDirectoryPath { get; set; }

        /// <summary>
        /// Key=Value parameter file path
        /// </summary>
        /// <remarks>
        /// This property is intended for use when using MSFileInfoScanner.dll along with a parameter file
        /// For MSFileInfoScanner.exe, specify the parameter file using /Conf or /P
        /// </remarks>
        public string ParameterFilePath { get; set; }

        /// <summary>
        /// When true, recurse subdirectories
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if MaxLevelsToRecurse is defined in the parameter file or if /S is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool RecurseDirectories { get; set; }

        /// <summary>
        /// Process files in subdirectories
        /// </summary>
        [Option("MaxLevelsToRecurse", "S", ArgExistsProperty = nameof(RecurseDirectories),
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, process all valid files in the input directory and subdirectories\n" +
                       "Include a number after /S (like /S:2) to limit the level of subdirectories to examine (0 means to recurse infinitely)\n" +
                       "The equivalent notation in a parameter file is MaxLevelsToRecurse=2")]
        public int MaxLevelsToRecurse { get; set; }

        [Option("IgnoreErrorsWhenRecursing", "IE",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "Ignore errors when recursing")]
        public bool IgnoreErrorsWhenRecursing { get; set; }

        /// <summary>
        /// When true, create a log file
        /// </summary>
        /// <remarks>
        /// This is auto-set to true if /L or /LogDir is used at the command line, or if
        /// LogfilePath or LogDirectory have a name defined in a parameter file
        /// </remarks>
        public bool LogMessagesToFile { get; set; }

        [Option("LogFilePath", "LogFile", "Log", "L",
            ArgExistsProperty = nameof(LogMessagesToFile), HelpShowsDefault = false,
            HelpText = "Log file path.\n" +
                       "Use /L at the command line to log messages to a file whose name is auto-defined using the current date, " +
                       "or use /L:LogFileName.txt to specify the name.\n" +
                       "In a Key=Value parameter file, define a file name or path to enable logging to a file.")]
        public string LogFilePath { get; set; }

        [Option("LogDirectoryPath", "LogDirectory", "LogDir",
            ArgExistsProperty = nameof(LogMessagesToFile), HelpShowsDefault = false,
            HelpText = "The directory where the log file should be written")]
        public string LogDirectoryPath { get; set; }

        /// <summary>
        /// m/z resolution when centroiding data for LC/MS 2D plots
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("LCMSPlotMzResolution", "MzResolution",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "m/z resolution when centroiding data for LC/MS 2D plots")]
        public float LCMSPlotMzResolution { get; set; } = LCMSDataPlotterOptions.DEFAULT_MZ_RESOLUTION;

        /// <summary>
        /// Minimum points per spectrum for inclusion on LC/MS 2D plots
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("LCMSPlotMinPointsPerSpectrum", "MinPointsPerSpectrum",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Minimum points per spectrum for inclusion on LC/MS 2D plots")]
        public int LCMSPlotMinPointsPerSpectrum { get; set; } = LCMSDataPlotterOptions.DEFAULT_MIN_POINTS_PER_SPECTRUM;

        /// <summary>
        /// Maximum charge state to display when plotting deisotoped data (from a DeconTools _isos.csv file)
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("MaxChargeToPlot", "MaxCharge",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Maximum charge state to display when plotting deisotoped data (from a DeconTools _isos.csv file)")]
        public int LCMSPlotMaxChargeState { get; set; } = LCMSDataPlotterOptions.DEFAULT_MAX_CHARGE_STATE;

        /// <summary>
        /// Maximum points to plot on each LC/MS 2D plot
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("LCMSPlotMaxPointsToPlot", "LC", "2D", Min = 10,
            ArgExistsProperty = nameof(SaveLCMS2DPlots),
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Create 2D LC/MS plots (this process could take several minutes for each dataset), using the top N points\n" +
                       "To plot the top 20000 points, use /LC:20000 or define the value in a configuration file specified with /Conf")]
        public int LCMSPlotMaxPointsToPlot { get; set; } = LCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT;

        /// <summary>
        /// Minimum intensity to require for each mass spectrum data point when adding data to LC/MS 2D plots
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("LCMSPlotMinIntensity", "MinIntensity",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Minimum intensity to require for each mass spectrum data point when adding data to LC/MS 2D plots")]
        public float LCMSPlotMinIntensity { get; set; }

        /// <summary>
        /// Maximum monoisotopic mass for deisotoped LC/MS plots
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is only used when PlottingDeisotopedData is true,
        /// which will be the case if the source data is a DeconTools _isos.csv file
        /// </para>
        /// <para>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </para>
        /// </remarks>
        [Option("LCMSPlotMaxMonoMass", "MaxMonoMass",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Maximum monoisotopic mass for the y-axis of deisotoped LC/MS plots\n" +
                       "This is only used when the input data is a DeconTools _isos.csv file")]
        public double LCMSPlotMaxMonoMass { get; set; } = LCMSDataPlotterOptions.DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT;

        /// <summary>
        /// The divisor to use when creating the overview 2D LC/MS plots
        /// If 0, do not create overview plots
        /// </summary>
        /// <remarks>
        /// This value cannot be updated via the Options class after MSFileInfoScanner is instantiated
        /// Use MSFileInfoProcessorBaseClass.LCMS2DPlotOptions instead
        /// </remarks>
        [Option("LCMSOverviewPlotDivisor", "LCDiv",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "The divisor to use when creating the overview 2D LC/MS plots\n" +
                       "The max points to plot value (LCMSPlotMaxPointsToPlot) is divided by the overview plot divisor to compute the number of points to include on the overview plot\n" +
                       "Use /LCDiv:0 to disable creation of the overview plots (or comment out the LCMSOverviewPlotDivisor line in a parameter file)")]
        public int LCMSOverviewPlotDivisor { get; set; } = LCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;

        [Option("SaveTICAndBPIPlots", "TIC",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "By default, the MS File Info Scanner creates TIC and BPI plots\n" +
                       "Use /TIC:False to disable saving TIC and BPI plots (or use SaveTICAndBPIPlots=False in a parameter file)\n" +
                       "When this is false, device specific plots will also be disabled")]
        public bool SaveTICAndBPIPlots { get; set; }

        [Option("CreateGradientPlots", "LCGrad",
            HelpShowsDefault = false, Hidden = true,
            HelpText = "Save a series of 2D LC plots, each using a different color scheme\n" +
                       "The default color scheme is OxyPalettes.Jet")]
        public bool TestLCMSGradientColorSchemes { get; set; }

        [Option("UseDatasetNameForHtmlPlotsFile", "CustomHtmlPlotsFile",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "The HTML file for viewing plots is named index.html by default;\n" +
                       "set this to True to name the file based on the input file name\n" +
                       "This is auto-set to true if the input file spec has a wildcard")]
        public bool UseDatasetNameForHtmlPlotsFile { get; set; }

        [Option("DatasetID",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "Define the dataset's DatasetID value (where # is an integer);\n" +
                       "this is only appropriate if processing a single dataset",
            Min = 0)]
        public int DatasetID { get; set; }

        [Option("CreateDatasetInfoFile", "DI", HelpShowsDefault = false,
            HelpText = "If supplied, create a dataset info XML file for each dataset")]
        public bool CreateDatasetInfoFile { get; set; }

        [Option("CreateScanStatsFile", "SS", HelpShowsDefault = false,
            HelpText = "If supplied, create files _ScanStats.txt and _ScanStatsEx.txt for each dataset")]
        public bool CreateScanStatsFile { get; set; }

        [Option("ComputeQualityScores", "QS",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, compute an overall quality score for the data in each datasets")]
        public bool ComputeOverallQualityScores { get; set; }

        [Option("CheckCentroidingStatus", "CC",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, check spectral data for whether it is centroided or profile")]
        public bool CheckCentroidingStatus { get; set; }

        [Option("CopyFileLocalOnReadError", "CopyLocalOnError",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "When true, if an error is encountered while reading the file, copy it to the local drive and try again")]
        public bool CopyFileLocalOnReadError { get; set; }

        /// <summary>
        /// When true, create 2D LC/MS plots
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if LCMS2DMaxPointsToPlot is defined in the parameter file or if /LC is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool SaveLCMS2DPlots { get; set; }

        /// <summary>
        /// Minimum m/z value that all MS/MS spectra should have
        /// Can be a numeric value, or the text iTRAQ or TMT
        /// </summary>
        [Option("MS2MzMin", HelpShowsDefault = false,
            HelpText = "If supplied, specifies a minimum m/z value that all MS/MS spectra should have\n" +
                       "Will report an error if any MS/MS spectra have minimum m/z value larger than the threshold\n" +
                       "Useful for validating instrument files where the sample is iTRAQ or TMT labeled " +
                       "and it is important to detect the reporter ions in the MS/MS spectra" +
                       "\n  - select the default iTRAQ m/z (" + MINIMUM_MZ_THRESHOLD_ITRAQ_STRING + ") using /MS2MzMin:iTRAQ" +
                       "\n  - select the default TMT m/z (" + MINIMUM_MZ_THRESHOLD_TMT_STRING + ") using /MS2MzMin:TMT" +
                       "\n  - specify a m/z value using /MS2MzMin:110")]
        public string MS2MzMinString { get; set; }

        /// <summary>
        /// Minimum m/z value that all MS/MS spectra should have
        /// </summary>
        /// <remarks>Auto-defined when Validate is called based on MS2MzMinString</remarks>
        public float MS2MzMin { get; set; }

        [Option("DisableInstrumentHash", "NoHash",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, disables creating a SHA-1 hash for the primary instrument data file(s)")]
        public bool DisableInstrumentHash { get; set; }

        /// <summary>
        /// When true, update (or create) a tab-delimited text file with overview stats for each dataset
        /// </summary>
        /// <remarks>
        /// This will be auto-set to true if DatasetStatsTextFileName is defined in the parameter file or if /DST is used at the command line
        /// This functionality is enabled by the ArgExistsProperty option
        /// </remarks>
        public bool UpdateDatasetStatsTextFile { get; set; }

        [Option("DatasetStatsTextFileName", "DST", ArgExistsProperty = nameof(UpdateDatasetStatsTextFile),
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, update (or create) a tab-delimited text file with overview stats for the dataset\n" +
                       "If /DI is used (or CreateDatasetInfoFile=True), will include detailed scan counts; otherwise, will just have the dataset name, " +
                       "acquisition date, and (if available) sample name and comment\n" +
                       "By default, the file is named " + DEFAULT_DATASET_STATS_FILENAME + "; " +
                       "to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt")]
        public string DatasetStatsTextFileName { get; set; }

        [Option("ScanStart", "Start",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "Use to limit the scan range to process; this is useful for files where the first few scans are corrupt\n" +
                       "For example, to start processing at scan 10, use /ScanStart:10\n" +
                       "The equivalent notation in a parameter file is ScanStart=10",
            Min = 0)]
        public int ScanStart { get; set; }

        [Option("ScanEnd", "End", "ScanStop", "Stop",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "Use to limit the scan range to process; this is useful for processing just part of a file for speed purposes\n" +
                       "For example, to end processing at scan 1000, use /ScanEnd:1000",
            Min = 0)]
        public int ScanEnd { get; set; }

        [Option("ShowDebugInfo", "Debug",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, display debug information at the console, " +
                       "including showing the scan number prior to reading each scan's data\n" +
                       "Also, when /Debug is enabled, temporary files for creating plots with Python will not be deleted")]
        public bool ShowDebugInfo { get; set; }

        private bool mCheckFileIntegrity;

        [Option("CheckFileIntegrity", "CheckIntegrity", "C",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "Use to perform an integrity check on all known file types; " +
                       "this process will open known file types and verify that they contain the expected data\n" +
                       "This option is only used if you specify an Input Directory and use a wildcard; " +
                       "you will typically also want to use /S when using /C")]
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
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Maximum number of lines to process when checking text or csv files", Min = 1)]
        public int MaximumTextFileLinesToCheck { get; set; } = DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;

        /// <summary>
        /// Maximum number of XML nodes to examine when checking XML files
        /// </summary>
        /// <remarks>This value cannot be updated after class MSFileInfoScanner is instantiated</remarks>
        [Option("MaximumXMLElementNodesToCheck", "MaxNodes",
            HelpShowsDefault = true, SecondaryArg = true,
            HelpText = "Maximum number of XML nodes to examine when checking XML files", Min = 1)]
        public int MaximumXMLElementNodesToCheck { get; set; } = DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK;

        /// <summary>
        /// Compute file hashes when CheckFileIntegrity is true
        /// </summary>
        [Option("ComputeFileHashes", "H",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, compute SHA-1 file hashes when verifying file integrity")]
        public bool ComputeFileHashes { get; set; }

        /// <summary>
        /// When CheckFileIntegrity is true, also validate zip files (quick check)
        /// </summary>
        [Option("ZipFileCheckAllData", "QZ",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, run a quick zip-file validation test when verifying file integrity\n" +
                       "(the test does not check all data in the .Zip file)")]
        public bool ZipFileCheckAllData { get; set; }

        /// <summary>
        /// When enabled, save/load information from the acquisition time file (cache file)
        /// </summary>
        [Option("UseCacheFiles", "CF",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, save/load information from the acquisition time file (cache file)\n" +
                       "This option is auto-enabled if you use /C")]
        public bool UseCacheFiles { get; set; }

        /// <summary>
        /// Reprocess existing files
        /// </summary>
        [Option("ReprocessExistingFiles", "R",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, reprocess files that are already defined in the acquisition time file")]
        public bool ReprocessExistingFiles { get; set; }

        /// <summary>
        /// When true, reprocess files that are already defined in the acquisition time file only if their cached size is 0
        /// </summary>
        [Option("ReprocessIfCachedSizeIsZero", "Z",
            HelpShowsDefault = false, SecondaryArg = true,
            HelpText = "If supplied, reprocess files that are already defined in the acquisition time file " +
                       "only if their cached size is 0 bytes")]
        public bool ReprocessIfCachedSizeIsZero { get; set; }

        /// <summary>
        /// When true, store the dataset info in the DMS database
        /// </summary>
        [Option("PostResultsToDMS", "PostToDMS",
            HelpShowsDefault = false, Hidden = true,
            HelpText = "If supplied, store the dataset info in the DMS database\n" +
                       "To customize the server name and/or stored procedure to use for posting, " +
                       "use an XML parameter file with settings DSInfoConnectionString, " +
                       "DSInfoDBPostingEnabled, and DSInfoStoredProcedure")]
        public bool PostResultsToDMS { get; set; }

        [Option("DatabaseConnectionString", "ConnectionString",
            HelpShowsDefault = true, Hidden = true,
            HelpText = "Connection string for storing dataset info in DMS")]
        public string DatabaseConnectionString { get; set; } = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

        [Option("DSInfoStoredProcedure",
            HelpShowsDefault = true, Hidden = true,
            HelpText = "Procedure to call to store dataset info in DMS")]
        public string DSInfoStoredProcedure { get; set; } = "UpdateDatasetFileInfoXML";

        [Option("PythonPlot", "PlotWithPython", "Python", HelpShowsDefault = false,
            HelpText = "If supplied, create plots with Python script MSFileInfoScanner_Plotter.py instead of OxyPlot")]
        public bool PlotWithPython { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public InfoScannerOptions()
        {
            InputDataFilePath = string.Empty;
            OutputDirectoryPath = string.Empty;
            ParameterFilePath = string.Empty;
            LogFilePath = string.Empty;

            RecurseDirectories = false;

            // If maxLevelsToRecurse is <= 0, we recurse infinitely
            MaxLevelsToRecurse = 0;
            IgnoreErrorsWhenRecursing = false;

            ReprocessExistingFiles = false;
            ReprocessIfCachedSizeIsZero = false;
            UseCacheFiles = false;

            SaveTICAndBPIPlots = true;
            SaveLCMS2DPlots = false;
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

            PostResultsToDMS = false;
            PlotWithPython = false;
        }

        /// <summary>
        /// Validate the options
        /// </summary>
        /// <remarks>This method is called from Program.cs</remarks>
        /// <returns>True if all options are valid</returns>
        // ReSharper disable once UnusedMember.Global
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InputDataFilePath))
            {
                ConsoleMsgUtils.ShowError($"ERROR: Input path must be provided and non-empty; \"{InputDataFilePath}\" was provided");
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
                ConsoleMsgUtils.ShowError(
                    $"ERROR: When ScanStart and ScanEnd are both >0, ScanStart ({ScanStart}) cannot be greater than ScanEnd ({ScanEnd})!");
                return false;
            }

            return true;
        }
    }
}
