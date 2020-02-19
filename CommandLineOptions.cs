using System;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    internal class CommandLineOptions
    {
        // This path can contain wildcard characters, e.g. C:\*.raw
        [Option("I", ArgPosition = 1, Required = true, HelpShowsDefault = false, HelpText = "The name of a file or directory to scan; the path can contain the wildcard character *")]
        public string InputDataFilePath { get; set; }

        [Option("O", HelpShowsDefault = false, HelpText = "Output directory name.  If omitted, the output files will be created in the program directory.")]
        public string OutputDirectoryName { get; set; }

        [Option("P", HelpShowsDefault = false, HelpText = "Param file path. If supplied, it should point to a valid XML parameter file. If omitted, defaults are used.")]
        public string ParameterFilePath { get; set; }

        public bool RecurseDirectories { get; set; }

        [Option("S", ArgExistsProperty = nameof(RecurseDirectories), HelpShowsDefault = false, HelpText = "If supplied, process all valid files in the input directory and subdirectories. " +
                                                                                                          "Include a number after /S (like /S:2) to limit the level of subdirectories to examine.")]
        public int MaxLevelsToRecurse { get; set; }

        [Option("IE", HelpShowsDefault = false, HelpText = "Ignore errors when recursing.")]
        public bool IgnoreErrorsWhenRecursing { get; set; }

        [Option("L", HelpShowsDefault = false, HelpText = "File path for logging messages.")]
        public string LogFilePath { get; set; }

        [Option("LC", "2D", ArgExistsProperty = nameof(SaveLCMS2DPlots), HelpText = "Create 2D LCMS plots (this process could take several minutes for each dataset), using the top N points." +
                                                                                    "To plot the top 20000 points, use /LC:20000.")]
        public int LCMS2DMaxPointsToPlot { get; set; }

        [Option("LCDiv", HelpText = "The divisor to use when creating the overview 2D LCMS plots. Use /LCDiv:0 to disable creation of the overview plots.")]
        public int LCMS2DOverviewPlotDivisor { get; set; }

        [Option("NoTIC", HelpShowsDefault = false, HelpText = "Disable saving TIC and BPI plots")]
        public bool SaveTICAndBPIPlots { get; set; }

        [Option("LCGrad", HelpShowsDefault = false, HelpText = "Save a series of 2D LC plots, each using a different color scheme. " +
                                                                      "The default color scheme is OxyPalettes.Jet")]
        public bool TestLCMSGradientColorSchemes { get; set; }

        [Option("DatasetID", HelpShowsDefault = false, HelpText = "Define the dataset's DatasetID value (where # is an integer); " +
                                                                         "only appropriate if processing a single dataset", Min = 1)]
        public int DatasetID { get; set; }

        [Option("DI", HelpShowsDefault = false, HelpText = "If supplied, create a dataset info XML file for each dataset.")]
        public bool CreateDatasetInfoFile { get; set; }

        [Option("SS", HelpShowsDefault = false, HelpText = "If supplied, create a _ScanStats.txt  file for each dataset.")]
        public bool CreateScanStatsFile { get; set; }

        [Option("QS", HelpShowsDefault = false, HelpText = "If supplied, compute an overall quality score for the data in each datasets.")]
        public bool ComputeOverallQualityScores { get; set; }

        [Option("CC", HelpShowsDefault = false, HelpText = "If supplied, check spectral data for whether it is centroided or profile")]
        public bool CheckCentroidingStatus { get; set; }

        public bool SaveLCMS2DPlots { get; set; }

        [Option("MS2MzMin", HelpShowsDefault = false, HelpText = "If supplied, specifies a minimum m/z value that all MS/MS spectra should have. " +
                                                                 "Will report an error if any MS/MS spectra have minimum m/z value larger than the threshold. " +
                                                                 "Useful for validating instrument files where the sample is iTRAQ or TMT labeled " +
                                                                 "and it is important to detect the reporter ions in the MS/MS spectra. " +
                                                                 "\n  - select the default iTRAQ m/z (" + clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_ITRAQ_STRING + ") using /MS2MzMin:iTRAQ" +
                                                                 "\n  - select the default TMT m/z (" + clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_TMT_STRING + ") using /MS2MzMin:TMT" +
                                                                 "\n  - specify a m/z value using /MS2MzMin:110")]
        public string MS2MzMinString { get; set; }

        public float MS2MzMin { get; set; }

        [Option("NoHash", HelpShowsDefault = false, HelpText = "If supplied, disables creating a SHA-1 hash for the primary instrument data file(s).")]
        public bool DisableInstrumentHash { get; set; }

        public bool UpdateDatasetStatsTextFile { get; set; }

        [Option("DST", ArgExistsProperty = nameof(UpdateDatasetStatsTextFile), HelpShowsDefault = false, HelpText = "If supplied, update (or create) a tab-delimited text file with overview stats for the dataset. " +
                                                                                                                    "If /DI is used, will include detailed scan counts; otherwise, will just have the dataset name, " +
                                                                                                                    "acquisition date, and (if available) sample name and comment. " +
                                                                                                                    "By default, the file is named " + DatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME + "; " +
                                                                                                                    "to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt")]
        public string DatasetStatsTextFileName { get; set; }

        [Option("ScanStart", "Start", HelpShowsDefault = false, HelpText = "Use to limit the scan range to process; " +
                                                                           "useful for files where the first few scans are corrupt. " +
                                                                           "For example, to start processing at scan 10, use /ScanStart:10", Min = 0)]
        public int ScanStart { get; set; }

        [Option("ScanEnd", "End", HelpShowsDefault = false, HelpText = "Use to limit the scan range to process; " +
                                                                       "useful for files where the first few scans are corrupt. " +
                                                                       "For example, to start processing at scan 10, use /ScanStart:10", Min = 0)]
        public int ScanEnd { get; set; }

        [Option("Debug", HelpShowsDefault = false, HelpText = "If supplied, display debug information at the console, " +
                                                              "including showing the scan number prior to reading each scan's data. " +
                                                              "Also, when /Debug is enabled, temporary files for creating plots with Python will not be deleted.")]
        public bool ShowDebugInfo { get; set; }

        [Option("C", "CheckIntegrity", HelpShowsDefault = false, HelpText = "Use to perform an integrity check on all known file types; " +
                                                                            "this process will open known file types and verify that they contain the expected data. " +
                                                                            "This option is only used if you specify an Input Directory and use a wildcard; " +
                                                                            "you will typically also want to use /S when using /C.")]
        public bool CheckFileIntegrity { get; set; }

        [Option("M", HelpText = "Use to define the maximum number of lines to process when checking text or csv files.", Min = 1)]
        public int MaximumTextFileLinesToCheck { get; set; }

        [Option("H", HelpShowsDefault = false, HelpText = "If supplied, compute SHA-1 file hashes when verifying file integrity.")]
        public bool ComputeFileHashes { get; set; }

        [Option("QZ", HelpShowsDefault = false, HelpText = "If supplied, run a quick zip-file validation test when verifying file integrity " +
                                                           "(the test does not check all data in the .Zip file).")]
        public bool ZipFileCheckAllData { get; set; }

        [Option("CF", HelpShowsDefault = false, HelpText = "If supplied, save/load information from the acquisition time file (cache file). " +
                                                           "This option is auto-enabled if you use /C.")]
        public bool UseCacheFiles { get; set; }

        [Option("R", HelpShowsDefault = false, HelpText = "If supplied, reprocess files that are already defined in the acquisition time file.")]
        public bool ReprocessingExistingFiles { get; set; }

        [Option("Z", HelpShowsDefault = false, HelpText = "If supplied, reprocess files that are already defined in the acquisition time file " +
                                                          "only if their cached size is 0 bytes.")]
        public bool ReprocessIfCachedSizeIsZero { get; set; }

        [Option("PostToDMS", HelpShowsDefault = false, HelpText = "If supplied, store the dataset info in the DMS database. " +
                                                                  "To customize the server name and/or stored procedure to use for posting, " +
                                                                  "use an XML parameter file with settings DSInfoConnectionString, " +
                                                                  "DSInfoDBPostingEnabled, and DSInfoStoredProcedure")]
        public bool PostResultsToDMS { get; set; }

        [Option("PythonPlot", "Python", HelpShowsDefault = false, HelpText = "If supplied, create plots with Python instead of OxyPlot")]
        public bool PlotWithPython { get; set; }

        public CommandLineOptions()
        {
            InputDataFilePath = string.Empty;
            OutputDirectoryName = string.Empty;
            ParameterFilePath = string.Empty;
            LogFilePath = string.Empty;

            RecurseDirectories = false;
            MaxLevelsToRecurse = 2;
            IgnoreErrorsWhenRecursing = false;

            ReprocessingExistingFiles = false;
            ReprocessIfCachedSizeIsZero = false;
            UseCacheFiles = false;

            SaveTICAndBPIPlots = true;
            SaveLCMS2DPlots = false;
            LCMS2DMaxPointsToPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT;
            LCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;
            TestLCMSGradientColorSchemes = false;

            CheckCentroidingStatus = false;
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

            MaximumTextFileLinesToCheck = clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;

            PostResultsToDMS = false;
            PlotWithPython = false;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InputDataFilePath))
            {
                ConsoleMsgUtils.ShowError($"ERROR: Input path must be provided and non-empty; \"{InputDataFilePath}\" was provided.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MS2MzMinString))
            {
                if (MS2MzMinString.StartsWith("itraq", StringComparison.OrdinalIgnoreCase))
                {
                    MS2MzMin = clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_ITRAQ;
                }
                else if (MS2MzMinString.StartsWith("tmt", StringComparison.OrdinalIgnoreCase))
                {
                    MS2MzMin = clsMSFileInfoScanner.MINIMUM_MZ_THRESHOLD_TMT;
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

        public void CopyToScanner(clsMSFileInfoScanner scanner)
        {
            // Note: These values will be overridden if /P was used and they are defined in the parameter file

            scanner.UseCacheFiles = UseCacheFiles;
            scanner.ReprocessExistingFiles = ReprocessingExistingFiles;
            scanner.ReprocessIfCachedSizeIsZero = ReprocessIfCachedSizeIsZero;

            scanner.PlotWithPython = PlotWithPython;
            scanner.SaveTICAndBPIPlots = SaveTICAndBPIPlots;
            scanner.SaveLCMS2DPlots = SaveLCMS2DPlots;
            scanner.LCMS2DPlotMaxPointsToPlot = LCMS2DMaxPointsToPlot;
            scanner.LCMS2DOverviewPlotDivisor = LCMS2DOverviewPlotDivisor;
            scanner.TestLCMSGradientColorSchemes = TestLCMSGradientColorSchemes;

            scanner.CheckCentroidingStatus = CheckCentroidingStatus;
            scanner.MS2MzMin = MS2MzMin;
            scanner.DisableInstrumentHash = DisableInstrumentHash;

            scanner.ScanStart = ScanStart;
            scanner.ScanEnd = ScanEnd;
            scanner.ShowDebugInfo = ShowDebugInfo;

            scanner.ComputeOverallQualityScores = ComputeOverallQualityScores;
            scanner.CreateDatasetInfoFile = CreateDatasetInfoFile;
            scanner.CreateScanStatsFile = CreateScanStatsFile;

            scanner.UpdateDatasetStatsTextFile = UpdateDatasetStatsTextFile;
            scanner.DatasetStatsTextFileName = DatasetStatsTextFileName;

            scanner.CheckFileIntegrity = CheckFileIntegrity;
            scanner.RecheckFileIntegrityForExistingDirectories = ReprocessingExistingFiles;

            scanner.MaximumTextFileLinesToCheck = MaximumTextFileLinesToCheck;
            scanner.ComputeFileHashes = ComputeFileHashes;
            scanner.ZipFileCheckAllData = ZipFileCheckAllData;

            scanner.IgnoreErrorsWhenRecursing = IgnoreErrorsWhenRecursing;

            if (LogFilePath.Length > 0)
            {
                scanner.LogMessagesToFile = true;
                scanner.LogFilePath = LogFilePath;
            }

            scanner.DatasetIDOverride = DatasetID;
            scanner.DSInfoDBPostingEnabled = PostResultsToDMS;

            if (!string.IsNullOrEmpty(ParameterFilePath))
            {
                scanner.LoadParameterFileSettings(ParameterFilePath);
            }
        }
    }
}
