using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using MSFileInfoScannerInterfaces;
using PRISM;

// -------------------------------------------------------------------------------
// This program scans a series of MS data files (or data directories) and extracts the acquisition start and end times,
// number of spectra, and the total size of the Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
//
// Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D directories), Agilent or QStar/QTrap .WIFF files,
// MassLynx .Raw directories, Bruker 1 directories, Bruker XMass analysis.baf files, and .UIMF files (IMS)
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Program started in 2005
// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
// -------------------------------------------------------------------------------

namespace MSFileInfoScanner
{
    public sealed class clsMSFileInfoScanner : iMSFileInfoScanner
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public clsMSFileInfoScanner()
        {

            mFileIntegrityChecker = new clsFileIntegrityChecker();
            RegisterEvents(mFileIntegrityChecker);
            mFileIntegrityChecker.FileIntegrityFailure += FileIntegrityChecker_FileIntegrityFailure;

            mMSFileInfoDataCache = new clsMSFileInfoDataCache();
            RegisterEvents(mMSFileInfoDataCache);

            SetErrorCode(eMSFileScannerErrorCodes.NoError);

            IgnoreErrorsWhenRecursing = false;

            DatasetInfoXML = string.Empty;
            UseCacheFiles = false;

            LogMessagesToFile = false;
            mLogFilePath = string.Empty;
            mLogDirectoryPath = string.Empty;

            ReprocessExistingFiles = false;
            ReprocessIfCachedSizeIsZero = false;
            RecheckFileIntegrityForExistingDirectories = false;

            CreateDatasetInfoFile = false;

            UpdateDatasetStatsTextFile = false;
            mDatasetStatsTextFileName = clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME;

            SaveTICAndBPIPlots = false;
            SaveLCMS2DPlots = false;
            CheckCentroidingStatus = false;

            mLCMS2DPlotOptions = new clsLCMSDataPlotterOptions();
            mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;

            ScanStart = 0;
            ScanEnd = 0;
            ShowDebugInfo = false;

            ComputeOverallQualityScores = false;

            CopyFileLocalOnReadError = false;

            mCheckFileIntegrity = false;

            DSInfoConnectionString = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
            DSInfoDBPostingEnabled = false;
            DSInfoStoredProcedure = "UpdateDatasetFileInfoXML";
            DatasetIDOverride = 0;

            mFileIntegrityDetailsFilePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(eDataFileTypeConstants.FileIntegrityDetails));
            mFileIntegrityErrorsFilePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(eDataFileTypeConstants.FileIntegrityErrors));

            mMSFileInfoDataCache.InitializeVariables();

            var oneHourAgo = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0));
            mLastWriteTimeFileIntegrityDetails = oneHourAgo;
            mLastWriteTimeFileIntegrityFailure = oneHourAgo;
            mLastCheckForAbortProcessingFile = oneHourAgo;

        }

        #region "Constants and Enums"

        public const string DEFAULT_ACQUISITION_TIME_FILENAME_TXT = "DatasetTimeFile.txt";

        public const string DEFAULT_DIRECTORY_INTEGRITY_INFO_FILENAME_TXT = "DirectoryIntegrityInfo.txt";

        public const string DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT = "FileIntegrityDetails.txt";

        public const string DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT = "FileIntegrityErrors.txt";

        public const string ABORT_PROCESSING_FILENAME = "AbortProcessing.txt";

        public const string XML_SECTION_MSFILESCANNER_SETTINGS = "MSFileInfoScannerSettings";

        private const int FILE_MODIFICATION_WINDOW_MINUTES = 60;

        private const int MAX_FILE_READ_ACCESS_ATTEMPTS = 2;

        /// <summary>
        /// Default m/z threshold for iTRAQ labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_ITRAQ = 113;

        /// <summary>
        /// Default m/z threshold for TMT labeled samples
        /// </summary>
        /// <remarks>All MS/MS spectra should have a scan range that starts below this value</remarks>
        public const int MINIMUM_MZ_THRESHOLD_TMT = 126;

        private const bool SKIP_FILES_IN_ERROR = true;

        private enum eMessageTypeConstants
        {
            Normal = 0,
            ErrorMsg = 1,
            Warning = 2,
            Debug = 3
        }

        #endregion

        #region "Classwide Variables"

        private string mFileIntegrityDetailsFilePath;

        private string mFileIntegrityErrorsFilePath;

        private string mDatasetStatsTextFileName;
        private bool mCheckFileIntegrity;

        private readonly clsLCMSDataPlotterOptions mLCMS2DPlotOptions;

        private int mLCMS2DOverviewPlotDivisor;

        private string mLogFilePath;

        private StreamWriter mLogFile;

        // This variable is updated in ProcessMSFileOrDirectory
        private string mOutputDirectoryPath;

        // If blank, mOutputDirectoryPath will be used; if mOutputDirectoryPath is also blank, the log is created in the same directory as the executing assembly
        private string mLogDirectoryPath;

        private readonly clsFileIntegrityChecker mFileIntegrityChecker;

        private StreamWriter mFileIntegrityDetailsWriter;

        private StreamWriter mFileIntegrityErrorsWriter;

        private iMSFileInfoProcessor mMSInfoScanner;

        private readonly clsMSFileInfoDataCache mMSFileInfoDataCache;

        private DateTime mLastWriteTimeFileIntegrityDetails;
        private DateTime mLastWriteTimeFileIntegrityFailure;
        private DateTime mLastCheckForAbortProcessingFile;

        #endregion

        #region "Processing Options and Interface Functions"
        public override string AcquisitionTimeFilename
        {
            get => GetDataFileFilename(eDataFileTypeConstants.MSFileInfo);
            set => SetDataFileFilename(value, eDataFileTypeConstants.MSFileInfo);
        }

        /// <summary>
        /// When true, checks the integrity of every file in every directory processed
        /// </summary>
        public override bool CheckFileIntegrity
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

        public override string GetDataFileFilename(eDataFileTypeConstants eDataFileType)
        {
            switch (eDataFileType)
            {
                case eDataFileTypeConstants.MSFileInfo:
                    return mMSFileInfoDataCache.AcquisitionTimeFilePath;
                case eDataFileTypeConstants.DirectoryIntegrityInfo:
                    return mMSFileInfoDataCache.DirectoryIntegrityInfoFilePath;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    return mFileIntegrityDetailsFilePath;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    return mFileIntegrityErrorsFilePath;
                default:
                    return string.Empty;
            }
        }

        public override void SetDataFileFilename(string filePath, eDataFileTypeConstants eDataFileType)
        {
            switch (eDataFileType)
            {
                case eDataFileTypeConstants.MSFileInfo:
                    mMSFileInfoDataCache.AcquisitionTimeFilePath = filePath;
                    break;
                case eDataFileTypeConstants.DirectoryIntegrityInfo:
                    mMSFileInfoDataCache.DirectoryIntegrityInfoFilePath = filePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    mFileIntegrityDetailsFilePath = filePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    mFileIntegrityErrorsFilePath = filePath;
                    break;
                default:
                    // Unknown file type
                    throw new ArgumentOutOfRangeException(nameof(eDataFileType));
            }
        }

        public static string DefaultAcquisitionTimeFilename => DefaultDataFileName(eDataFileTypeConstants.MSFileInfo);

        public static string DefaultDataFileName(eDataFileTypeConstants eDataFileType)
        {

            switch (eDataFileType)
            {
                case eDataFileTypeConstants.MSFileInfo:
                    return DEFAULT_ACQUISITION_TIME_FILENAME_TXT;
                case eDataFileTypeConstants.DirectoryIntegrityInfo:
                    return DEFAULT_DIRECTORY_INTEGRITY_INFO_FILENAME_TXT;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT;
                default:
                    return "UnknownFileType.txt";
            }
        }

        /// <summary>
        /// When True, computes a SHA-1 hash on every file using mFileIntegrityChecker
        /// </summary>
        /// <remarks>
        /// Note, when this is false, the program computes the SHA-1 hash of the primary dataset file (or files),
        /// unless DisableInstrumentHash is true
        /// </remarks>
        public override bool ComputeFileHashes
        {
            get
            {
                if (mFileIntegrityChecker != null)
                {
                    return mFileIntegrityChecker.ComputeFileHashes;
                }

                return false;
            }
            set
            {
                if (mFileIntegrityChecker != null)
                {
                    mFileIntegrityChecker.ComputeFileHashes = value;
                }
            }
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

        /// <summary>
        /// By default, will compute the SHA-1 hash of only the primary dataset file
        /// Disable by setting this to true
        /// </summary>
        public bool DisableInstrumentHash { get; set; }

        public override float LCMS2DPlotMZResolution
        {
            get => mLCMS2DPlotOptions.MZResolution;
            set => mLCMS2DPlotOptions.MZResolution = value;
        }

        public override int LCMS2DPlotMaxPointsToPlot
        {
            get => mLCMS2DPlotOptions.MaxPointsToPlot;
            set => mLCMS2DPlotOptions.MaxPointsToPlot = value;
        }

        public override int LCMS2DOverviewPlotDivisor
        {
            get => mLCMS2DOverviewPlotDivisor;
            set => mLCMS2DOverviewPlotDivisor = value;
        }

        public override int LCMS2DPlotMinPointsPerSpectrum
        {
            get => mLCMS2DPlotOptions.MinPointsPerSpectrum;
            set => mLCMS2DPlotOptions.MinPointsPerSpectrum = value;
        }

        public override float LCMS2DPlotMinIntensity
        {
            get => mLCMS2DPlotOptions.MinIntensity;
            set => mLCMS2DPlotOptions.MinIntensity = value;
        }

        public override int MaximumTextFileLinesToCheck
        {
            get
            {
                if (mFileIntegrityChecker != null)
                {
                    return mFileIntegrityChecker.MaximumTextFileLinesToCheck;
                }

                return 0;
            }
            set
            {
                if (mFileIntegrityChecker != null)
                {
                    mFileIntegrityChecker.MaximumTextFileLinesToCheck = value;
                }
            }
        }

        public override int MaximumXMLElementNodesToCheck
        {
            get
            {
                if (mFileIntegrityChecker != null)
                {
                    return mFileIntegrityChecker.MaximumTextFileLinesToCheck;
                }

                return 0;
            }
            set
            {
                if (mFileIntegrityChecker != null)
                {
                    mFileIntegrityChecker.MaximumTextFileLinesToCheck = value;
                }
            }
        }

        /// <summary>
        /// Minimum m/z value that MS/mS spectra should have
        /// </summary>
        /// <remarks>
        /// Useful for validating instrument files where the sample is iTRAQ or TMT labeled
        /// and it is important to detect the reporter ions in the MS/MS spectra
        /// </remarks>
        public override float MS2MzMin { get; set; }

        /// <summary>
        /// MS2MzMin validation error or warning Message
        /// </summary>
        public override string MS2MzMinValidationMessage { get; set; }

        /// <summary>
        /// Set to True to print out a series of 2D plots, each using a different color scheme
        /// </summary>
        public bool TestLCMSGradientColorSchemes
        {
            get => mLCMS2DPlotOptions.TestGradientColorSchemes;
            set => mLCMS2DPlotOptions.TestGradientColorSchemes = value;
        }

        public override bool ZipFileCheckAllData
        {
            get
            {
                if (mFileIntegrityChecker != null)
                {
                    return mFileIntegrityChecker.ZipFileCheckAllData;
                }

                return false;
            }
            set
            {
                if (mFileIntegrityChecker != null)
                {
                    mFileIntegrityChecker.ZipFileCheckAllData = value;
                }
            }
        }

        #endregion

        private void AutoSaveCachedResults()
        {
            if (UseCacheFiles)
            {
                mMSFileInfoDataCache.AutosaveCachedResults();
            }

        }

        private void CheckForAbortProcessingFile()
        {

            try
            {
                if (DateTime.UtcNow.Subtract(mLastCheckForAbortProcessingFile).TotalSeconds < 15)
                {
                    return;
                }

                mLastCheckForAbortProcessingFile = DateTime.UtcNow;

                if (File.Exists(ABORT_PROCESSING_FILENAME))
                {
                    AbortProcessing = true;
                    try
                    {
                        if (File.Exists(ABORT_PROCESSING_FILENAME + ".done"))
                        {
                            File.Delete(ABORT_PROCESSING_FILENAME + ".done");
                        }
                        File.Move(ABORT_PROCESSING_FILENAME, ABORT_PROCESSING_FILENAME + ".done");
                    }
                    catch (Exception)
                    {
                        // Ignore errors here
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        private void CheckIntegrityOfFilesInDirectory(string directoryPath, bool forceRecheck, List<string> processedFileList)
        {
            var directoryID = 0;

            try
            {
                if (mFileIntegrityDetailsWriter == null)
                {
                    OpenFileIntegrityDetailsFile();
                }

                var datasetDirectory = new DirectoryInfo(directoryPath);
                var fileCount = datasetDirectory.GetFiles().Length;

                if (fileCount <= 0)
                {
                    return;
                }

                var checkDirectory = true;
                if (UseCacheFiles && !forceRecheck)
                {
                    if (mMSFileInfoDataCache.CachedDirectoryIntegrityInfoContainsDirectory(datasetDirectory.FullName, out directoryID, out var dataRow))
                    {
                        var cachedFileCount = (int)dataRow[clsMSFileInfoDataCache.COL_NAME_FILE_COUNT];
                        var cachedCountFailIntegrity = (int)dataRow[clsMSFileInfoDataCache.COL_NAME_COUNT_FAIL_INTEGRITY];

                        if (cachedFileCount == fileCount && cachedCountFailIntegrity == 0)
                        {
                            // Directory contains the same number of files as last time, and no files failed the integrity check last time
                            // Do not recheck the directory
                            checkDirectory = false;
                        }
                    }
                }

                if (!checkDirectory)
                {
                    return;
                }

                mFileIntegrityChecker.CheckIntegrityOfFilesInDirectory(directoryPath, out var udtDirectoryStats, out var udtFileStats, processedFileList);

                if (UseCacheFiles)
                {
                    if (!mMSFileInfoDataCache.UpdateCachedDirectoryIntegrityInfo(udtDirectoryStats, out directoryID))
                    {
                        directoryID = -1;
                    }
                }

                WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, directoryID, udtFileStats);
            }
            catch (Exception ex)
            {
                HandleException("Error calling mFileIntegrityChecker", ex);
            }

        }

        /// <summary>
        /// Get the the appData directory for this program
        /// For example: C:\Users\username\AppData\Roaming\MSFileInfoScanner
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        public static string GetAppDataDirectoryPath(string appName = "")
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
            }

            try
            {
                var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

                if (string.IsNullOrWhiteSpace(appName))
                    return appDataDirectory;

                var appDirectory = new DirectoryInfo(Path.Combine(appDataDirectory, appName));

                if (!appDirectory.Exists)
                {
                    appDirectory.Create();
                }

                return appDirectory.FullName;

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in GetAppDataDirectoryPath", ex);
                return Path.GetTempPath();
            }

        }

        public static string GetAppDirectoryPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public override string[] GetKnownDirectoryExtensions()
        {
            return GetKnownDirectoryExtensionsList().ToArray();
        }

        public List<string> GetKnownDirectoryExtensionsList()
        {
            var extensionsToParse = new List<string>
            {
                clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION.ToUpper(),
                clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION.ToUpper()
            };

            return extensionsToParse;
        }

        public override string[] GetKnownFileExtensions()
        {
            return GetKnownFileExtensionsList().ToArray();
        }

        public List<string> GetKnownFileExtensionsList()
        {
            var extensionsToParse = new List<string>
            {
                clsFinniganRawFileInfoScanner.THERMO_RAW_FILE_EXTENSION.ToUpper(),
                clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION.ToUpper(),
                clsUIMFInfoScanner.UIMF_FILE_EXTENSION.ToUpper(),
                clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION.ToUpper()
            };

            return extensionsToParse;
        }

        [Obsolete("Use GetKnownDirectoryExtensions")]
        public override string[] GetKnownFolderExtensions()
        {
            return GetKnownDirectoryExtensionsList().ToArray();
        }

        [Obsolete("Use GetKnownDirectoryExtensionsList")]
        public List<string> GetKnownFolderExtensionsList()
        {
            return GetKnownDirectoryExtensionsList();
        }

        /// <summary>
        /// Get the error message, or an empty string if no error
        /// </summary>
        /// <returns></returns>
        public override string GetErrorMessage()
        {

            switch (ErrorCode)
            {
                case eMSFileScannerErrorCodes.NoError:
                    return string.Empty;
                case eMSFileScannerErrorCodes.InvalidInputFilePath:
                    return "Invalid input file path";
                case eMSFileScannerErrorCodes.InvalidOutputDirectoryPath:
                    return "Invalid output directory path";
                case eMSFileScannerErrorCodes.ParameterFileNotFound:
                    return "Parameter file not found";
                case eMSFileScannerErrorCodes.FilePathError:
                    return "General file path error";
                case eMSFileScannerErrorCodes.ParameterFileReadError:
                    return "Parameter file read error";
                case eMSFileScannerErrorCodes.UnknownFileExtension:
                    return "Unknown file extension";
                case eMSFileScannerErrorCodes.InputFileReadError:
                    return "Input file read error";
                case eMSFileScannerErrorCodes.InputFileAccessError:
                    return "Input file access error";
                case eMSFileScannerErrorCodes.OutputFileWriteError:
                    return "Error writing output file";
                case eMSFileScannerErrorCodes.FileIntegrityCheckError:
                    return "Error checking file integrity";
                case eMSFileScannerErrorCodes.DatabasePostingError:
                    return "Database posting error";
                case eMSFileScannerErrorCodes.MS2MzMinValidationError:

                    // "Over 10% of the MS/MS spectra have a minimum m/z value larger than the required minimum
                    var errorMsg = string.Format("Over {0}% of the MS/MS spectra have a minimum m/z value larger than the required minimum; " +
                                                 "reporter ion peaks likely could not be detected", clsMSFileInfoProcessorBaseClass.MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED);

                    if (!string.IsNullOrWhiteSpace(MS2MzMinValidationMessage))
                    {
                        return errorMsg + "; " + MS2MzMinValidationMessage;
                    }
                    else
                    {
                        return errorMsg;
                    }
                case eMSFileScannerErrorCodes.MS2MzMinValidationWarning:
                    var warningMsg = "Some of the MS/MS spectra have a minimum m/z value larger than the required minimum; " +
                                     "reporter ion peaks likely could not be detected";

                    if (!string.IsNullOrWhiteSpace(MS2MzMinValidationMessage))
                    {
                        return warningMsg + "; " + MS2MzMinValidationMessage;
                    }
                    else
                    {
                        return warningMsg;
                    }

                case eMSFileScannerErrorCodes.UnspecifiedError:
                    return "Unspecified localized error";
                default:
                    // This shouldn't happen
                    return "Unknown error state";

            }


        }

        private bool GetFileOrDirectoryInfo(string fileOrDirectoryPath, out bool isDirectory, out FileSystemInfo fileOrDirectoryInfo)
        {

            isDirectory = false;

            // See if fileOrDirectoryPath points to a valid file
            var datasetFile = new FileInfo(fileOrDirectoryPath);

            if (datasetFile.Exists)
            {
                fileOrDirectoryInfo = datasetFile;
                return true;
            }

            // See if fileOrDirectoryPath points to a directory
            var datasetDirectory = new DirectoryInfo(fileOrDirectoryPath);
            if (datasetDirectory.Exists)
            {
                fileOrDirectoryInfo = datasetDirectory;
                isDirectory = true;
                return true;
            }

            fileOrDirectoryInfo = new FileInfo(fileOrDirectoryPath);
            return false;
        }

        private void HandleException(string baseMessage, Exception ex)
        {
            if (string.IsNullOrEmpty(baseMessage))
            {
                baseMessage = "Error";
            }

            // Note that ReportError() will call LogMessage()
            ReportError(baseMessage, ex);

        }

        private void LoadCachedResults(bool forceLoad)
        {
            if (UseCacheFiles)
            {
                mMSFileInfoDataCache.LoadCachedResults(forceLoad);
            }
        }

        private void LogMessage(string message, eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal)
        {
            // Note that ProcessMSFileOrDirectory() will update mOutputDirectoryPath, which is used here if mLogDirectoryPath is blank

            if (mLogFile == null && LogMessagesToFile)
            {
                try
                {
                    if (string.IsNullOrEmpty(mLogFilePath))
                    {
                        // Auto-name the log file
                        mLogFilePath = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        mLogFilePath += "_log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                    }

                    try
                    {
                        if (mLogDirectoryPath == null)
                            mLogDirectoryPath = string.Empty;

                        if (mLogDirectoryPath.Length == 0)
                        {
                            // Log directory is undefined; use mOutputDirectoryPath if it is defined
                            if (!string.IsNullOrEmpty(mOutputDirectoryPath))
                            {
                                mLogDirectoryPath = string.Copy(mOutputDirectoryPath);
                            }
                        }

                        if (mLogDirectoryPath.Length > 0)
                        {
                            // Create the log directory if it doesn't exist
                            if (!Directory.Exists(mLogDirectoryPath))
                            {
                                Directory.CreateDirectory(mLogDirectoryPath);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        mLogDirectoryPath = string.Empty;
                    }

                    if (mLogDirectoryPath.Length > 0)
                    {
                        mLogFilePath = Path.Combine(mLogDirectoryPath, mLogFilePath);
                    }

                    var openingExistingFile = File.Exists(mLogFilePath);

                    mLogFile = new StreamWriter(new FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        AutoFlush = true
                    };

                    if (!openingExistingFile)
                    {
                        mLogFile.WriteLine("Date" + '\t' + "Type" + '\t' + "Message");
                    }

                }
                catch (Exception)
                {
                    // Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                    LogMessagesToFile = false;
                }

            }

            if (mLogFile != null)
            {
                string messageType;
                switch (eMessageType)
                {
                    case eMessageTypeConstants.Debug:
                        messageType = "Debug";
                        break;
                    case eMessageTypeConstants.Normal:
                        messageType = "Normal";
                        break;
                    case eMessageTypeConstants.ErrorMsg:
                        messageType = "Error";
                        break;
                    case eMessageTypeConstants.Warning:
                        messageType = "Warning";
                        break;
                    default:
                        messageType = "Unknown";
                        break;
                }

                mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + '\t' + messageType + '\t' + message);
            }

        }

        public override bool LoadParameterFileSettings(string parameterFilePath)
        {

            var settingsFile = new XmlSettingsFileAccessor();

            try
            {
                if (string.IsNullOrEmpty(parameterFilePath))
                {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath))
                    {
                        ReportError("Parameter file not found: " + parameterFilePath);
                        SetErrorCode(eMSFileScannerErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                // Pass False to .LoadSettings() here to turn off case sensitive matching
                if (settingsFile.LoadSettings(parameterFilePath, false))
                {

                    if (!settingsFile.SectionPresent(XML_SECTION_MSFILESCANNER_SETTINGS))
                    {
                        // MS File Scanner section not found; that's ok
                        ReportWarning("Parameter file " + parameterFilePath + " does not have section \"" + XML_SECTION_MSFILESCANNER_SETTINGS + "\"");
                    }
                    else
                    {
                        DSInfoConnectionString = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoConnectionString", DSInfoConnectionString);
                        DSInfoDBPostingEnabled = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoDBPostingEnabled", DSInfoDBPostingEnabled);
                        DSInfoStoredProcedure = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoStoredProcedure", DSInfoStoredProcedure);

                        LogMessagesToFile = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogMessagesToFile", LogMessagesToFile);
                        LogFilePath = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFilePath", LogFilePath);

                        var legacyLogDir = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFolderPath", string.Empty);
                        if (!string.IsNullOrEmpty(legacyLogDir))
                        {
                            LogDirectoryPath = legacyLogDir;
                            OnWarningEvent("Update the parameter file to switch from LogFolderPath to LogDirectoryPath");
                        }

                        LogDirectoryPath = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogDirectoryPath", LogDirectoryPath);

                        UseCacheFiles = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UseCacheFiles", UseCacheFiles);
                        ReprocessExistingFiles = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessExistingFiles", ReprocessExistingFiles);
                        ReprocessIfCachedSizeIsZero = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessIfCachedSizeIsZero", ReprocessIfCachedSizeIsZero);

                        CopyFileLocalOnReadError = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CopyFileLocalOnReadError", CopyFileLocalOnReadError);

                        SaveTICAndBPIPlots = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveTICAndBPIPlots", SaveTICAndBPIPlots);
                        SaveLCMS2DPlots = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveLCMS2DPlots", SaveLCMS2DPlots);
                        CheckCentroidingStatus = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckCentroidingStatus", CheckCentroidingStatus);

                        MS2MzMin = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MS2MzMin", MS2MzMin);
                        DisableInstrumentHash = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DisableInstrumentHash", DisableInstrumentHash);

                        LCMS2DPlotMZResolution = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMZResolution", LCMS2DPlotMZResolution);
                        LCMS2DPlotMinPointsPerSpectrum = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinPointsPerSpectrum", LCMS2DPlotMinPointsPerSpectrum);

                        LCMS2DPlotMaxPointsToPlot = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMaxPointsToPlot", LCMS2DPlotMaxPointsToPlot);
                        LCMS2DPlotMinIntensity = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinIntensity", LCMS2DPlotMinIntensity);

                        LCMS2DOverviewPlotDivisor = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DOverviewPlotDivisor", LCMS2DOverviewPlotDivisor);

                        ScanStart = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanStart", ScanStart);
                        ScanEnd = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanEnd", ScanEnd);

                        ComputeOverallQualityScores = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeOverallQualityScores", ComputeOverallQualityScores);
                        CreateDatasetInfoFile = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateDatasetInfoFile", CreateDatasetInfoFile);
                        CreateScanStatsFile = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateScanStatsFile", CreateScanStatsFile);

                        UpdateDatasetStatsTextFile = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UpdateDatasetStatsTextFile", UpdateDatasetStatsTextFile);
                        DatasetStatsTextFileName = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DatasetStatsTextFileName", DatasetStatsTextFileName);

                        CheckFileIntegrity = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckFileIntegrity", CheckFileIntegrity);
                        RecheckFileIntegrityForExistingDirectories = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "RecheckFileIntegrityForExistingDirectories", RecheckFileIntegrityForExistingDirectories);

                        MaximumTextFileLinesToCheck = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumTextFileLinesToCheck", MaximumTextFileLinesToCheck);
                        MaximumXMLElementNodesToCheck = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumXMLElementNodesToCheck", MaximumXMLElementNodesToCheck);
                        ComputeFileHashes = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeFileHashes", ComputeFileHashes);
                        ZipFileCheckAllData = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ZipFileCheckAllData", ZipFileCheckAllData);

                        IgnoreErrorsWhenRecursing = settingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "IgnoreErrorsWhenRecursing", IgnoreErrorsWhenRecursing);

                    }

                }
                else
                {
                    ReportError("Error calling settingsFile.LoadSettings for " + parameterFilePath);
                    return false;
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }

            return true;

        }

        private void OpenFileIntegrityDetailsFile()
        {
            OpenFileIntegrityOutputFile(eDataFileTypeConstants.FileIntegrityDetails, ref mFileIntegrityDetailsFilePath, ref mFileIntegrityDetailsWriter);
        }

        private void OpenFileIntegrityErrorsFile()
        {
            OpenFileIntegrityOutputFile(eDataFileTypeConstants.FileIntegrityErrors, ref mFileIntegrityErrorsFilePath, ref mFileIntegrityErrorsWriter);
        }

        private void OpenFileIntegrityOutputFile(eDataFileTypeConstants eDataFileType, ref string filePath, ref StreamWriter writer)
        {
            var openedExistingFile = false;
            FileStream fsFileStream = null;

            var defaultFileName = DefaultDataFileName(eDataFileType);
            ValidateDataFilePath(ref filePath, eDataFileType);

            try
            {
                if (File.Exists(filePath))
                {
                    openedExistingFile = true;
                }
                fsFileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);

            }
            catch (Exception ex)
            {
                HandleException("Error opening/creating " + filePath + "; will try " + defaultFileName, ex);

                try
                {
                    if (File.Exists(defaultFileName))
                    {
                        openedExistingFile = true;
                    }

                    fsFileStream = new FileStream(defaultFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                }
                catch (Exception ex2)
                {
                    HandleException("Error opening/creating " + defaultFileName, ex2);
                }
            }

            try
            {
                if (fsFileStream != null)
                {
                    writer = new StreamWriter(fsFileStream);

                    if (!openedExistingFile)
                    {
                        writer.WriteLine(mMSFileInfoDataCache.ConstructHeaderLine(eDataFileType));
                    }
                }
            }
            catch (Exception ex)
            {
                if (fsFileStream == null)
                {
                    HandleException("Error opening/creating the StreamWriter for " + Path.GetFileName(filePath), ex);
                }
                else
                {
                    HandleException("Error opening/creating the StreamWriter for " + fsFileStream.Name, ex);
                }
            }

        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB()
        {
            return PostDatasetInfoToDB(DatasetInfoXML, DSInfoConnectionString, DSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetInfoXML)
        {
            return PostDatasetInfoToDB(datasetInfoXML, DSInfoConnectionString, DSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string connectionString, string storedProcedureName)
        {
            return PostDatasetInfoToDB(DatasetInfoXML, connectionString, storedProcedureName);
        }

        /// <summary>
        /// Post the dataset info in datasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetInfoXML, string connectionString, string storedProcedureName)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool success;

            try
            {
                ReportMessage("  Posting DatasetInfo XML to the database");

                // We need to remove the encoding line from datasetInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var startIndex = datasetInfoXML.IndexOf("?>", StringComparison.Ordinal);

                string dsInfoXMLClean;
                if (startIndex > 0)
                {
                    dsInfoXMLClean = datasetInfoXML.Substring(startIndex + 2).Trim();
                }
                else
                {
                    dsInfoXMLClean = datasetInfoXML;
                }

                // Call the stored procedure using connection string connectionString

                if (string.IsNullOrEmpty(connectionString))
                {
                    ReportError("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName))
                {
                    storedProcedureName = "UpdateDatasetFileInfoXML";
                }

                var command = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = storedProcedureName
                };

                command.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
                command.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

                command.Parameters.Add(new SqlParameter("@DatasetInfoXML", SqlDbType.Xml));
                command.Parameters["@DatasetInfoXML"].Direction = ParameterDirection.Input;
                command.Parameters["@DatasetInfoXML"].Value = dsInfoXMLClean;

                var executeSP = new ExecuteDatabaseSP(connectionString);

                executeSP.ErrorEvent += ExecuteSP_DBErrorEvent;

                var result = executeSP.ExecuteSP(command, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == ExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                }
                else
                {
                    ReportError("Error calling stored procedure, return code = " + result);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }

                // Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
                // Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
                // PostDatasetInfoToDB(32, datasetInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

            }
            catch (Exception ex)
            {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Post the dataset info in datasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="datasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoUseDatasetID(int datasetID, string connectionString, string storedProcedureName)
        {

            return PostDatasetInfoUseDatasetID(datasetID, DatasetInfoXML, connectionString, storedProcedureName);
        }

        /// <summary>
        /// Post the dataset info in datasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="datasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoUseDatasetID(int datasetID, string datasetInfoXML, string connectionString, string storedProcedureName)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool success;

            try
            {
                if (datasetID == 0 && DatasetIDOverride > 0)
                {
                    datasetID = DatasetIDOverride;
                }

                ReportMessage("  Posting DatasetInfo XML to the database (using Dataset ID " + datasetID + ")");

                // We need to remove the encoding line from datasetInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var startIndex = datasetInfoXML.IndexOf("?>", StringComparison.Ordinal);
                string dsInfoXMLClean;
                if (startIndex > 0)
                {
                    dsInfoXMLClean = datasetInfoXML.Substring(startIndex + 2).Trim();
                }
                else
                {
                    dsInfoXMLClean = datasetInfoXML;
                }

                // Call stored procedure storedProcedure using connection string connectionString

                if (string.IsNullOrEmpty(connectionString))
                {
                    ReportError("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName))
                {
                    storedProcedureName = "CacheDatasetInfoXML";
                }

                var command = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = storedProcedureName
                };

                command.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
                command.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

                command.Parameters.Add(new SqlParameter("@DatasetID", SqlDbType.Int));
                command.Parameters["@DatasetID"].Direction = ParameterDirection.Input;
                command.Parameters["@DatasetID"].Value = datasetID;

                command.Parameters.Add(new SqlParameter("@DatasetInfoXML", SqlDbType.Xml));
                command.Parameters["@DatasetInfoXML"].Direction = ParameterDirection.Input;
                command.Parameters["@DatasetInfoXML"].Value = dsInfoXMLClean;

                var executeSP = new ExecuteDatabaseSP(connectionString);

                var result = executeSP.ExecuteSP(command, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == ExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                }
                else
                {
                    ReportError("Error calling stored procedure, return code = " + result);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }

            }
            catch (Exception ex)
            {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                success = false;
            }

            return success;
        }

        private bool ProcessMSDataset(string inputFileOrDirectoryPath, iMSFileInfoProcessor scanner, string datasetName, string outputDirectoryPath)
        {

            var datasetFileInfo = new clsDatasetFileInfo();
            if (!string.IsNullOrWhiteSpace(datasetName))
                datasetFileInfo.DatasetName = datasetName;

            // Set the processing options
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, SaveTICAndBPIPlots);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots, SaveLCMS2DPlots);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus, CheckCentroidingStatus);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, ComputeOverallQualityScores);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, CreateDatasetInfoFile);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile, CreateScanStatsFile);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError, CopyFileLocalOnReadError);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.PlotWithPython, PlotWithPython);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile, UpdateDatasetStatsTextFile);
            scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.DisableInstrumentHash, DisableInstrumentHash);

            scanner.DatasetStatsTextFileName = mDatasetStatsTextFileName;

            if (PlotWithPython)
            {
                mLCMS2DPlotOptions.PlotWithPython = true;

                if (SaveTICAndBPIPlots || SaveLCMS2DPlots)
                {
                    // Make sure that Python exists
                    if (!clsPythonPlotContainer.PythonInstalled)
                    {
                        ReportError("Could not find the python executable");
                        var debugMsg = "Paths searched:";
                        foreach (var item in clsPythonPlotContainer.PythonPathsToCheck())
                        {
                            debugMsg += "\n  " + item;
                        }
                        OnDebugEvent(debugMsg);

                        SetErrorCode(eMSFileScannerErrorCodes.OutputFileWriteError);
                        return false;
                    }
                }
            }

            if (ShowDebugInfo)
                mLCMS2DPlotOptions.DeleteTempFiles = false;

            scanner.LCMS2DPlotOptions = mLCMS2DPlotOptions;
            scanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;

            scanner.ScanStart = ScanStart;
            scanner.ScanEnd = ScanEnd;
            scanner.MS2MzMin = MS2MzMin;
            scanner.ShowDebugInfo = ShowDebugInfo;

            scanner.DatasetID = DatasetIDOverride;

            var retryCount = 0;
            bool success;

            do
            {

                // Process the data file
                success = scanner.ProcessDataFile(inputFileOrDirectoryPath, datasetFileInfo);

                if (!success)
                {
                    retryCount += 1;

                    if (retryCount < MAX_FILE_READ_ACCESS_ATTEMPTS)
                    {
                        // Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time

                        if (DateTime.Now.Subtract(datasetFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES || DateTime.Now.Subtract(datasetFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES)
                        {
                            // Sleep for 10 seconds then try again
                            SleepNow(10);
                        }
                        else
                        {
                            retryCount = MAX_FILE_READ_ACCESS_ATTEMPTS;
                        }
                    }
                }
            } while (!success && retryCount < MAX_FILE_READ_ACCESS_ATTEMPTS);

            if (!success && retryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS)
            {
                if (!string.IsNullOrWhiteSpace(datasetFileInfo.DatasetName))
                {
                    // Make an entry anyway; probably a corrupted file
                    success = true;
                }
            }

            if (success)
            {

                success = scanner.CreateOutputFiles(inputFileOrDirectoryPath, outputDirectoryPath);
                if (!success)
                {
                    SetErrorCode(eMSFileScannerErrorCodes.OutputFileWriteError);
                }

                // Cache the Dataset Info XML
                DatasetInfoXML = scanner.GetDatasetInfoXML();

                if (UseCacheFiles)
                {
                    // Update the results database
                    mMSFileInfoDataCache.UpdateCachedMSFileInfo(datasetFileInfo);

                    // Possibly auto-save the cached results
                    AutoSaveCachedResults();
                }

                if (DSInfoDBPostingEnabled)
                {
                    var dbSuccess = PostDatasetInfoToDB(DatasetInfoXML);
                    if (!dbSuccess)
                    {
                        SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                        success = false;
                    }
                }

                if (scanner.MS2MzMinValidationError)
                {
                    MS2MzMinValidationMessage = scanner.MS2MzMinValidationMessage;
                    SetErrorCode(eMSFileScannerErrorCodes.MS2MzMinValidationError);
                    success = false;
                }
                else if (scanner.MS2MzMinValidationWarning)
                {
                    MS2MzMinValidationMessage = scanner.MS2MzMinValidationMessage;
                    SetErrorCode(eMSFileScannerErrorCodes.MS2MzMinValidationWarning);
                }

                if (datasetFileInfo.ScanCount == 0 && ErrorCode == eMSFileScannerErrorCodes.NoError)
                {
                    OnWarningEvent( "Dataset has no spectra: " + inputFileOrDirectoryPath);
                    SetErrorCode(eMSFileScannerErrorCodes.DatasetHasNoSpectra);
                }

            }
            else
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (SKIP_FILES_IN_ERROR)
                {
                    success = true;
                }
            }

            return success;

        }

        /// <summary>
        /// Main processing function with input / output paths, error code reset flag, and processing state
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessMSFileOrDirectory(string inputFileOrDirectoryPath, string outputDirectoryPath)
        {
            return ProcessMSFileOrDirectory(inputFileOrDirectoryPath, outputDirectoryPath, true, out _);
        }

        /// <summary>
        /// Main processing function with input / output paths, error code reset flag, and processing state
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="resetErrorCode"></param>
        /// <param name="eMSFileProcessingState"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessMSFileOrDirectory(
            string inputFileOrDirectoryPath,
            string outputDirectoryPath,
            bool resetErrorCode,
            out eMSFileProcessingStateConstants eMSFileProcessingState)
        {

            // Note: inputFileOrDirectoryPath must be a known MS data file or MS data directory
            // See function ProcessMSFilesAndRecurseDirectories for more details
            // This function returns True if it processed a file (or the dataset was processed previously)
            // When SKIP_FILES_IN_ERROR = True, it also returns True if the file type was a known type but the processing failed
            // If the file type is unknown, or if an error occurs, it returns false
            // eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

            var success = false;

            if (resetErrorCode)
            {
                SetErrorCode(eMSFileScannerErrorCodes.NoError);
                MS2MzMinValidationMessage = string.Empty;
            }

            eMSFileProcessingState = eMSFileProcessingStateConstants.NotProcessed;

            if (string.IsNullOrEmpty(outputDirectoryPath))
            {
                // Define outputDirectoryPath based on the program file path
                outputDirectoryPath = GetAppDirectoryPath();
            }

            // Update mOutputDirectoryPath
            mOutputDirectoryPath = string.Copy(outputDirectoryPath);

            DatasetInfoXML = string.Empty;

            LoadCachedResults(false);

            try
            {
                if (string.IsNullOrEmpty(inputFileOrDirectoryPath))
                {
                    ReportError("Input file name is empty");
                }
                else
                {
                    try
                    {
                        if (Path.GetFileName(inputFileOrDirectoryPath).Length == 0)
                        {
                            ReportMessage("Parsing " + Path.GetDirectoryName(inputFileOrDirectoryPath));
                        }
                        else
                        {
                            ReportMessage("Parsing " + Path.GetFileName(inputFileOrDirectoryPath));
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleException("Error parsing " + inputFileOrDirectoryPath, ex);
                    }

                    // Determine whether inputFileOrDirectoryPath points to a file or a directory

                    if (!GetFileOrDirectoryInfo(inputFileOrDirectoryPath, out var isDirectory, out var fileOrDirectoryInfo))
                    {
                        ReportError("File or directory not found: " + fileOrDirectoryInfo.FullName);

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (SKIP_FILES_IN_ERROR)
                        {
                            return true;
                        }
                        else
                        // ReSharper disable once HeuristicUnreachableCode
                        {
                            SetErrorCode(eMSFileScannerErrorCodes.FilePathError);
                            return false;
                        }
                    }

                    var knownMSDataType = false;

                    // Only continue if it's a known type
                    if (isDirectory)
                    {
                        if (fileOrDirectoryInfo.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME)
                        {
                            // Bruker 1 directory
                            mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                            knownMSDataType = true;
                        }
                        else
                        {
                            if (inputFileOrDirectoryPath.EndsWith(@"\"))
                            {
                                inputFileOrDirectoryPath = inputFileOrDirectoryPath.TrimEnd('\\');
                            }

                            switch (Path.GetExtension(inputFileOrDirectoryPath).ToUpper())
                            {
                                case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION:
                                    // Agilent .D directory or Bruker .D directory

                                    if (Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME).Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_SER_FILE_NAME).Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_FID_FILE_NAME).Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_idx").Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_xtr").Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME).Length > 0 ||
                                        Directory.GetFiles(inputFileOrDirectoryPath, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME).Length > 0)
                                    {
                                        mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();

                                    }
                                    else if (Directory.GetFiles(inputFileOrDirectoryPath, clsAgilentGCDFolderInfoScanner.AGILENT_MS_DATA_FILE).Length > 0 ||
                                      Directory.GetFiles(inputFileOrDirectoryPath, clsAgilentGCDFolderInfoScanner.AGILENT_ACQ_METHOD_FILE).Length > 0 ||
                                      Directory.GetFiles(inputFileOrDirectoryPath, clsAgilentGCDFolderInfoScanner.AGILENT_GC_INI_FILE).Length > 0)
                                    {
                                        mMSInfoScanner = new clsAgilentGCDFolderInfoScanner();

                                    }
                                    else if (Directory.GetDirectories(inputFileOrDirectoryPath, clsAgilentTOFDFolderInfoScanner.AGILENT_ACQDATA_FOLDER_NAME).Length > 0)
                                    {
                                        mMSInfoScanner = new clsAgilentTOFDFolderInfoScanner();

                                    }
                                    else
                                    {
                                        mMSInfoScanner = new clsAgilentIonTrapDFolderInfoScanner();
                                    }

                                    knownMSDataType = true;
                                    break;
                                case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION:
                                    // Micromass .Raw directory
                                    mMSInfoScanner = new clsMicromassRawFolderInfoScanner();
                                    knownMSDataType = true;
                                    break;
                                default:
                                    // Unknown directory extension (or no extension)
                                    // See if the directory contains one or more 0_R*.zip files
                                    if (Directory.GetFiles(inputFileOrDirectoryPath, clsZippedImagingFilesScanner.ZIPPED_IMAGING_FILE_SEARCH_SPEC).Length > 0)
                                    {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (string.Equals(fileOrDirectoryInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_ANALYSIS_YEP_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            // If the directory also contains file BRUKER_EXTENSION_BAF_FILE_NAME then this is a Bruker XMass directory
                            var parentDirectory = Path.GetDirectoryName(fileOrDirectoryInfo.FullName);
                            if (!string.IsNullOrEmpty(parentDirectory))
                            {
                                var pathCheck = Path.Combine(parentDirectory, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME);
                                if (File.Exists(pathCheck))
                                {
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    knownMSDataType = true;
                                }
                            }
                        }

                        if (!knownMSDataType)
                        {
                            // Examine the extension on inputFileOrDirectoryPath
                            switch (fileOrDirectoryInfo.Extension.ToUpper())
                            {
                                case clsFinniganRawFileInfoScanner.THERMO_RAW_FILE_EXTENSION:
                                    // Thermo .raw file
                                    mMSInfoScanner = new clsFinniganRawFileInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION:
                                    mMSInfoScanner = new clsAgilentTOFOrQStarWiffFileInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsUIMFInfoScanner.UIMF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsUIMFInfoScanner();
                                    knownMSDataType = true;
                                    break;

                                case clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION:

                                    if (fileOrDirectoryInfo.FullName.EndsWith(clsDeconToolsIsosInfoScanner.DECONTOOLS_ISOS_FILE_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        mMSInfoScanner = new clsDeconToolsIsosInfoScanner();
                                        knownMSDataType = true;
                                    }
                                    break;

                                default:
                                    // Unknown file extension; check for a zipped directory
                                    if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(fileOrDirectoryInfo.Name))
                                    {
                                        // Bruker s001.zip file
                                        mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                                        knownMSDataType = true;
                                    }
                                    else if (clsZippedImagingFilesScanner.IsZippedImagingFile(fileOrDirectoryInfo.Name))
                                    {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }

                    }

                    if (!knownMSDataType)
                    {
                        ReportError("Unknown file type: " + Path.GetFileName(inputFileOrDirectoryPath));
                        SetErrorCode(eMSFileScannerErrorCodes.UnknownFileExtension);
                        return false;
                    }

                    // Attach the events
                    RegisterEvents(mMSInfoScanner);

                    var datasetName = mMSInfoScanner.GetDatasetNameViaPath(fileOrDirectoryInfo.FullName);

                    if (UseCacheFiles && !ReprocessExistingFiles)
                    {
                        // See if the datasetName in inputFileOrDirectoryPath is already present in mCachedResults
                        // If it is present, don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

                        if (datasetName.Length > 0 && mMSFileInfoDataCache.CachedMSInfoContainsDataset(datasetName, out var dataRow))
                        {
                            if (ReprocessIfCachedSizeIsZero)
                            {
                                long cachedSizeBytes;
                                try
                                {
                                    cachedSizeBytes = (long)dataRow[clsMSFileInfoDataCache.COL_NAME_FILE_SIZE_BYTES];
                                }
                                catch (Exception)
                                {
                                    cachedSizeBytes = 1;
                                }

                                if (cachedSizeBytes > 0)
                                {
                                    // File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                    ReportMessage("  Skipping " + Path.GetFileName(inputFileOrDirectoryPath) + " since already in cached results");
                                    eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                    return true;
                                }
                            }
                            else
                            {
                                // File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                                ReportMessage("  Skipping " + Path.GetFileName(inputFileOrDirectoryPath) + " since already in cached results");
                                eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                return true;
                            }
                        }
                    }

                    // Process the data file or directory
                    success = ProcessMSDataset(inputFileOrDirectoryPath, mMSInfoScanner, datasetName, outputDirectoryPath);
                    if (success)
                    {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully;
                    }
                    else
                    {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing;
                    }

                }

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessMSFileOrDirectory", ex);
                success = false;
            }
            finally
            {
                mMSInfoScanner = null;
            }

            return success;

        }

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFileOrDirectoryPath and below having a known extension
        /// </summary>
        /// <param name="inputFileOrDirectoryPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="resetErrorCode"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessMSFileOrDirectoryWildcard(string inputFileOrDirectoryPath, string outputDirectoryPath, bool resetErrorCode)
        {

            var processedFileList = new List<string>();

            AbortProcessing = false;
            var success = true;
            try
            {
                // Possibly reset the error code
                if (resetErrorCode)
                {
                    SetErrorCode(eMSFileScannerErrorCodes.NoError);
                }

                // See if inputFilePath contains a wildcard
                eMSFileProcessingStateConstants eMSFileProcessingState;
                if (inputFileOrDirectoryPath != null &&
                    (inputFileOrDirectoryPath.IndexOf('*') >= 0 || inputFileOrDirectoryPath.IndexOf('?') >= 0))
                {
                    // Obtain a list of the matching files and directories

                    // Copy the path into cleanPath and replace any * or ? characters with _
                    var cleanPath = inputFileOrDirectoryPath.Replace("*", "_").Replace("?", "_");

                    var datasetFile = new FileInfo(cleanPath);
                    string inputDirectoryPath;

                    if (datasetFile.Directory != null && datasetFile.Directory.Exists)
                    {
                        inputDirectoryPath = datasetFile.DirectoryName;
                    }
                    else
                    {
                        // Use the current working directory
                        inputDirectoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    if (string.IsNullOrEmpty(inputDirectoryPath))
                        inputDirectoryPath = ".";

                    var datasetDirectory = new DirectoryInfo(inputDirectoryPath);

                    // Remove any directory information from inputFileOrDirectoryPath
                    inputFileOrDirectoryPath = Path.GetFileName(inputFileOrDirectoryPath);

                    var matchCount = 0;

                    foreach (var fileItem in datasetDirectory.GetFiles(inputFileOrDirectoryPath))
                    {
                        success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, resetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully ||
                            eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                        {
                            processedFileList.Add(fileItem.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (AbortProcessing)
                            break;

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount += 1;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (AbortProcessing)
                        return false;

                    foreach (var directoryItem in datasetDirectory.GetDirectories(inputFileOrDirectoryPath))
                    {
                        success = ProcessMSFileOrDirectory(directoryItem.FullName, outputDirectoryPath, resetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                        {
                            processedFileList.Add(directoryItem.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (AbortProcessing)
                            break;

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount += 1;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (AbortProcessing)
                        return false;

                    if (mCheckFileIntegrity)
                    {
                        CheckIntegrityOfFilesInDirectory(datasetDirectory.FullName, RecheckFileIntegrityForExistingDirectories, processedFileList);
                    }

                    if (matchCount == 0)
                    {
                        if (ErrorCode == eMSFileScannerErrorCodes.NoError)
                        {
                            ReportWarning("No match was found for the input file path:" + inputFileOrDirectoryPath);
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }
                else
                {
                    success = ProcessMSFileOrDirectory(inputFileOrDirectoryPath, outputDirectoryPath, resetErrorCode, out eMSFileProcessingState);
                    if (!success && ErrorCode == eMSFileScannerErrorCodes.NoError)
                    {
                        SetErrorCode(eMSFileScannerErrorCodes.UnspecifiedError);
                    }

                    if (AbortProcessing)
                        return false;

                    if (mCheckFileIntegrity && inputFileOrDirectoryPath != null)
                    {
                        string directoryPath;

                        var candidateDirectory = new DirectoryInfo(inputFileOrDirectoryPath);
                        if (candidateDirectory.Exists)
                        {
                            directoryPath = candidateDirectory.FullName;
                        }
                        else
                        {
                            var dataFile = new FileInfo(inputFileOrDirectoryPath);
                            directoryPath = dataFile.Directory?.FullName;
                        }

                        if (directoryPath == null)
                        {
                            ReportError("Unable to determine the parent directory of " + inputFileOrDirectoryPath);
                        }
                        else
                        {
                            CheckIntegrityOfFilesInDirectory(directoryPath, RecheckFileIntegrityForExistingDirectories, processedFileList);
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessMSFileOrDirectoryWildcard", ex);
                success = false;
            }
            finally
            {
                mFileIntegrityDetailsWriter?.Close();
                mFileIntegrityErrorsWriter?.Close();
            }

            return success;
        }


        /// <summary>
        /// Main processing function, with input file / directory path, plus output directory path
        /// </summary>
        /// <param name="inputFileOrFolderPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if success, False if an error</returns>
        [Obsolete("Use ProcessMSFileOrDirectory")]
        public override bool ProcessMSFileOrFolder(string inputFileOrFolderPath, string outputDirectoryPath)
        {
            return ProcessMSFileOrDirectory(inputFileOrFolderPath, outputDirectoryPath);
        }

        /// <summary>
        /// Main processing function with input / output paths, error code reset flag, and processing state
        /// </summary>
        /// <param name="inputFileOrFolderPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="resetErrorCode"></param>
        /// <param name="eMSFileProcessingState"></param>
        /// <returns>True if success, False if an error</returns>
        [Obsolete("Use ProcessMSFileOrDirectory")]
        public override bool ProcessMSFileOrFolder(
            string inputFileOrFolderPath,
            string outputDirectoryPath,
            bool resetErrorCode,
            out eMSFileProcessingStateConstants eMSFileProcessingState)
        {
            return ProcessMSFileOrDirectory(inputFileOrFolderPath, outputDirectoryPath, resetErrorCode, out eMSFileProcessingState);
        }

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFilePathOrDirectory and below having a known extension
        ///  Known extensions are:
        ///   .Raw for Finnigan files
        ///   .Wiff for Agilent TOF files and for Q-Star files
        ///   .Baf for Bruker XMASS directories (contains file analysis.baf, and hopefully files scan.xml and Log.txt)
        /// For each directory that does not have any files matching a known extension, will then look for special directory names:
        ///   Directories matching *.Raw for Micromass data
        ///   Directories matching *.D for Agilent Ion Trap data
        ///   A directory named 1 for Bruker FTICR-MS data
        /// </summary>
        /// <param name="inputFilePathOrDirectory">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="maxLevelsToRecurse">Maximum depth to recurse; Set to 0 to process all directories</param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessMSFilesAndRecurseDirectories(string inputFilePathOrDirectory, string outputDirectoryPath, int maxLevelsToRecurse)
        {
            bool success;

            // Examine inputFilePathOrDirectory to see if it contains a filename; if not, assume it points to a directory
            // First, see if it contains a * or ?
            try
            {
                string inputDirectoryPath;
                if (inputFilePathOrDirectory != null &&
                    (inputFilePathOrDirectory.IndexOf('*') >= 0 || inputFilePathOrDirectory.IndexOf('?') >= 0))
                {
                    // Copy the path into cleanPath and replace any * or ? characters with _
                    var cleanPath = inputFilePathOrDirectory.Replace("*", "_").Replace("?", "_");

                    var datasetFile = new FileInfo(cleanPath);
                    if (Path.IsPathRooted(cleanPath))
                    {
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (datasetFile.Directory != null && !datasetFile.Directory.Exists)
                        {
                            ReportError("Directory not found: " + datasetFile.DirectoryName);
                            SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                            return false;
                        }
                    }

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (datasetFile.Directory != null && datasetFile.Directory.Exists)
                    {
                        inputDirectoryPath = datasetFile.DirectoryName;
                    }
                    else
                    {
                        // Directory not found; use the current working directory
                        inputDirectoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from inputFilePathOrDirectory
                    inputFilePathOrDirectory = Path.GetFileName(inputFilePathOrDirectory);

                }
                else
                {
                    if (string.IsNullOrEmpty(inputFilePathOrDirectory))
                        inputFilePathOrDirectory = ".";

                    var datasetDirectory = new DirectoryInfo(inputFilePathOrDirectory);
                    if (datasetDirectory.Exists)
                    {
                        inputDirectoryPath = datasetDirectory.FullName;
                        inputFilePathOrDirectory = "*";
                    }
                    else
                    {
                        if (datasetDirectory.Parent != null && datasetDirectory.Parent.Exists)
                        {
                            inputDirectoryPath = datasetDirectory.Parent.FullName;
                            inputFilePathOrDirectory = Path.GetFileName(inputFilePathOrDirectory);
                        }
                        else
                        {
                            // Unable to determine the input directory path
                            inputDirectoryPath = string.Empty;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(inputDirectoryPath))
                {
                    // Initialize some parameters
                    AbortProcessing = false;
                    var fileProcessCount = 0;
                    var fileProcessFailCount = 0;

                    LoadCachedResults(false);

                    // Call RecurseDirectoriesWork
                    success = RecurseDirectoriesWork(inputDirectoryPath, inputFilePathOrDirectory, outputDirectoryPath, ref fileProcessCount, ref fileProcessFailCount, 1, maxLevelsToRecurse);

                }
                else
                {
                    SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                    return false;
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in ProcessMSFilesAndRecurseDirectories", ex);
                success = false;
            }
            finally
            {
                mFileIntegrityDetailsWriter?.Close();
                mFileIntegrityErrorsWriter?.Close();
            }

            return success;

        }

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFileOrFolderPath and below having a known extension
        /// </summary>
        /// <param name="inputFileOrFolderPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Folder to write any results files to</param>
        /// <param name="resetErrorCode"></param>
        /// <returns>True if success, False if an error</returns>
        [Obsolete("Use ProcessMSFileOrDirectoryWildcard")]
        public override bool ProcessMSFileOrFolderWildcard(string inputFileOrFolderPath, string outputDirectoryPath, bool resetErrorCode)
        {
            return ProcessMSFileOrDirectoryWildcard(inputFileOrFolderPath, outputDirectoryPath, resetErrorCode);
        }

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFilePathOrFolder and below having a known extension
        ///  Known extensions are:
        ///   .Raw for Finnigan files
        ///   .Wiff for Agilent TOF files and for Q-Star files
        ///   .Baf for Bruker XMASS directories (contains file analysis.baf, and hopefully files scan.xml and Log.txt)
        /// For each directory that does not have any files matching a known extension, will then look for special directory names:
        ///   Folders matching *.Raw for Micromass data
        ///   Folders matching *.D for Agilent Ion Trap data
        ///   A directory named 1 for Bruker FTICR-MS data
        /// </summary>
        /// <param name="inputFilePathOrFolder">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Folder to write any results files to</param>
        /// <param name="recurseFoldersMaxLevels">Maximum directory depth to process; Set to 0 to process all directories</param>
        /// <returns>True if success, False if an error</returns>
        [Obsolete("Use ProcessMSFilesAndRecurseDirectories")]
        public override bool ProcessMSFilesAndRecurseFolders(string inputFilePathOrFolder, string outputDirectoryPath, int recurseFoldersMaxLevels)
        {
            return ProcessMSFilesAndRecurseDirectories(inputFilePathOrFolder, outputDirectoryPath, recurseFoldersMaxLevels);
        }

        private bool RecurseDirectoriesWork(
            string inputDirectoryPath,
            string fileNameMatch,
            string outputDirectoryPath,
            ref int fileProcessCount,
            ref int fileProcessFailCount,
            int recursionLevel,
            int maxLevelsToRecurse)
        {

            const int MAX_ACCESS_ATTEMPTS = 2;

            // If maxLevelsToRecurse is <=0 then we recurse infinitely

            DirectoryInfo inputDirectory = null;

            List<string> fileExtensionsToParse;
            List<string> directoryExtensionsToParse;
            bool processAllFileExtensions;

            bool success;
            var fileProcessed = false;

            eMSFileProcessingStateConstants eMSFileProcessingState;

            var processedFileList = new List<string>();

            var retryCount = 0;
            do
            {
                try
                {
                    inputDirectory = new DirectoryInfo(inputDirectoryPath);
                    break;
                }
                catch (Exception ex)
                {
                    // Input directory path error
                    HandleException("Error populating DirectoryInfo inputDirectory for " + inputDirectoryPath, ex);
                    if (!ex.Message.Contains("no longer available"))
                    {
                        return false;
                    }
                }

                retryCount += 1;
                if (retryCount >= MAX_ACCESS_ATTEMPTS)
                {
                    return false;
                }
                // Wait 1 second, then try again
                SleepNow(1);
            } while (retryCount < MAX_ACCESS_ATTEMPTS);

            if (inputDirectory == null)
            {
                ReportError("Unable to instantiate a directory info object for " + inputDirectoryPath);
                return false;
            }

            try
            {
                // Construct and validate the list of file and directory extensions to parse
                fileExtensionsToParse = GetKnownFileExtensionsList();
                directoryExtensionsToParse = GetKnownDirectoryExtensionsList();

                // Validate the extensions, including assuring that they are all capital letters
                processAllFileExtensions = ValidateExtensions(fileExtensionsToParse);
                ValidateExtensions(directoryExtensionsToParse);
            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseDirectoriesWork", ex);
                return false;
            }

            try
            {
                Console.WriteLine("Examining " + inputDirectoryPath);

                // Process any matching files in this directory
                success = true;
                var processedZippedSFolder = false;

                foreach (var fileItem in inputDirectory.GetFiles(fileNameMatch))
                {
                    retryCount = 0;
                    do
                    {

                        try
                        {
                            fileProcessed = false;
                            foreach (var fileExtension in fileExtensionsToParse)
                            {
                                if (!processAllFileExtensions && fileItem.Extension.ToUpper() != fileExtension)
                                {
                                    continue;
                                }

                                fileProcessed = true;
                                success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, true, out eMSFileProcessingState);

                                if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                {
                                    processedFileList.Add(fileItem.FullName);
                                }

                                // Successfully processed a directory; exit the for loop
                                break;
                            }

                            if (AbortProcessing)
                                break;

                            if (!fileProcessed && !processedZippedSFolder)
                            {
                                // Check for other valid files
                                if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(fileItem.Name))
                                {
                                    // Only process this file if there is not a subdirectory named "1" present"
                                    if (inputDirectory.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1)
                                    {
                                        fileProcessed = true;
                                        processedZippedSFolder = true;
                                        success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, true, out eMSFileProcessingState);

                                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                        {
                                            processedFileList.Add(fileItem.FullName);
                                        }

                                    }
                                }
                            }

                            // Exit the Do loop
                            break;

                        }
                        catch (Exception ex)
                        {
                            // Error parsing file
                            HandleException("Error in RecurseDirectoriesWork at For Each fileItem in " + inputDirectoryPath, ex);
                            if (!ex.Message.Contains("no longer available"))
                            {
                                return false;
                            }
                        }

                        if (AbortProcessing)
                            break;

                        retryCount += 1;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS)
                        {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);

                    } while (retryCount < MAX_ACCESS_ATTEMPTS);

                    if (fileProcessed)
                    {
                        if (success)
                        {
                            fileProcessCount += 1;
                        }
                        else
                        {
                            fileProcessFailCount += 1;
                            success = true;
                        }
                    }

                    CheckForAbortProcessingFile();
                    if (AbortProcessing)
                        break;
                }

                if (mCheckFileIntegrity && !AbortProcessing)
                {
                    CheckIntegrityOfFilesInDirectory(inputDirectory.FullName, RecheckFileIntegrityForExistingDirectories, processedFileList);
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseDirectoriesWork Examining files in " + inputDirectoryPath, ex);
                return false;
            }

            if (AbortProcessing)
            {
                return success;
            }

            // Check the subdirectories for those with known extensions

            try
            {
                var subdirectoriesProcessed = 0;
                var subdirectoryNamesProcessed = new SortedSet<string>();

                foreach (var subdirectory in inputDirectory.GetDirectories(fileNameMatch))
                {
                    retryCount = 0;
                    do
                    {
                        try
                        {
                            // Check whether the directory name is BRUKER_ONE_FOLDER = "1"
                            if (subdirectory.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME)
                            {
                                success = ProcessMSFileOrDirectory(subdirectory.FullName, outputDirectoryPath, true, out eMSFileProcessingState);
                                if (!success)
                                {
                                    fileProcessFailCount += 1;
                                    success = true;
                                }
                                else
                                {
                                    fileProcessCount += 1;
                                }
                                subdirectoriesProcessed += 1;
                                subdirectoryNamesProcessed.Add(subdirectory.Name);
                            }
                            else
                            {
                                // See if the subdirectory has an extension matching directoryExtensionsToParse()
                                // If it does, process it using ProcessMSFileOrDirectory and do not recurse into it
                                foreach (var directoryExtension in directoryExtensionsToParse)
                                {
                                    if (subdirectory.Extension.ToUpper() != directoryExtension)
                                    {
                                        continue;
                                    }

                                    success = ProcessMSFileOrDirectory(subdirectory.FullName, outputDirectoryPath, true, out eMSFileProcessingState);
                                    if (!success)
                                    {
                                        fileProcessFailCount += 1;
                                        success = true;
                                    }
                                    else
                                    {
                                        fileProcessCount += 1;
                                    }
                                    subdirectoriesProcessed += 1;
                                    subdirectoryNamesProcessed.Add(subdirectory.Name);

                                    // Successfully processed a directory; exit the for loop
                                    break;
                                }

                            }

                            // Exit the Do loop
                            break;

                        }
                        catch (Exception ex)
                        {
                            // Error parsing directory
                            HandleException("Error in RecurseDirectoriesWork at For Each subdirectory(A) in " + inputDirectoryPath, ex);
                            if (!ex.Message.Contains("no longer available"))
                            {
                                return false;
                            }
                        }

                        if (AbortProcessing)
                            break;

                        retryCount += 1;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS)
                        {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);
                    } while (retryCount < MAX_ACCESS_ATTEMPTS);

                    if (AbortProcessing)
                        break;

                }

                // If maxLevelsToRecurse is <=0 then we recurse infinitely
                //  otherwise, compare recursionLevel to maxLevelsToRecurse
                if (maxLevelsToRecurse <= 0 || recursionLevel <= maxLevelsToRecurse)
                {
                    // Call this function for each of the subdirectories of inputDirectory
                    // However, do not step into directories listed in subdirectoryNamesProcessed

                    foreach (var subdirectory in inputDirectory.GetDirectories())
                    {
                        retryCount = 0;
                        do
                        {
                            try
                            {
                                if (subdirectoriesProcessed == 0 || !subdirectoryNamesProcessed.Contains(subdirectory.Name))
                                {
                                    success = RecurseDirectoriesWork(subdirectory.FullName, fileNameMatch, outputDirectoryPath, ref fileProcessCount, ref fileProcessFailCount, recursionLevel + 1, maxLevelsToRecurse);
                                }

                                if (!success && !IgnoreErrorsWhenRecursing)
                                {
                                    break;
                                }

                                CheckForAbortProcessingFile();

                                break;

                            }
                            catch (Exception ex)
                            {
                                // Error parsing file
                                HandleException("Error in RecurseDirectoriesWork at For Each subdirectory(B) in " + inputDirectoryPath, ex);
                                if (!ex.Message.Contains("no longer available"))
                                {
                                    return false;
                                }
                            }

                            if (AbortProcessing)
                                break;

                            retryCount += 1;
                            if (retryCount >= MAX_ACCESS_ATTEMPTS)
                            {
                                return false;
                            }

                            // Wait 1 second, then try again
                            SleepNow(1);
                        } while (retryCount < MAX_ACCESS_ATTEMPTS);

                        if (!success && !IgnoreErrorsWhenRecursing)
                        {
                            break;
                        }

                        if (AbortProcessing)
                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                HandleException("Error in RecurseDirectoriesWork examining subdirectories in " + inputDirectoryPath, ex);
                return false;
            }

            return success;

        }

        public override bool SaveCachedResults()
        {
            return SaveCachedResults(true);
        }

        public override bool SaveCachedResults(bool clearCachedData)
        {
            if (UseCacheFiles)
            {
                return mMSFileInfoDataCache.SaveCachedResults(clearCachedData);
            }

            return true;
        }

        public override bool SaveParameterFileSettings(string parameterFilePath)
        {

            var settingsFile = new XmlSettingsFileAccessor();

            try
            {
                if (string.IsNullOrEmpty(parameterFilePath))
                {
                    // No parameter file specified; unable to save
                    return false;
                }

                // Pass True to .LoadSettings() here so that newly made Xml files will have the correct capitalization
                if (settingsFile.LoadSettings(parameterFilePath, true))
                {

                    // General settings
                    // settingsFile.SetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ConnectionString", Me.DatabaseConnectionString)

                    settingsFile.SaveSettings();

                }

            }
            catch (Exception ex)
            {
                HandleException("Error in SaveParameterFileSettings", ex);
                return false;
            }

            return true;

        }

        private void SetErrorCode(eMSFileScannerErrorCodes eNewErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && ErrorCode != eMSFileScannerErrorCodes.NoError)
            {
                // An error code is already defined; do not change it
                return;
            }

            ErrorCode = eNewErrorCode;

        }

        /// <summary>
        /// Raise event ErrorEvent and call LogMessage
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="ex"></param>
        /// <remarks>The calling thread needs to monitor this event and display it at the console</remarks>
        private void ReportError(string errorMessage, Exception ex = null)
        {

            OnErrorEvent(errorMessage, ex);

            string formattedError;
            if (ex == null || errorMessage.EndsWith(ex.Message, StringComparison.InvariantCultureIgnoreCase))
            {
                formattedError = errorMessage;
            }
            else
            {
                formattedError = errorMessage + ": " + ex.Message;
            }

            LogMessage(formattedError, eMessageTypeConstants.ErrorMsg);

        }

        /// <summary>
        /// Report a status message and optionally write to the log file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        /// <param name="eMessageType"></param>
        private void ReportMessage(
            string message,
            bool allowLogToFile = true,
            eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal)
        {

            if (eMessageType == eMessageTypeConstants.Debug)
                OnDebugEvent(message);
            if (eMessageType == eMessageTypeConstants.Warning)
                OnWarningEvent(message);
            else
                OnStatusEvent(message);

            if (allowLogToFile)
            {
                LogMessage(message, eMessageType);
            }

        }

        /// <summary>
        /// Report a warning message and optionally write to the log file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        private void ReportWarning(string message, bool allowLogToFile = true)
        {
            OnWarningEvent(message);

            if (allowLogToFile)
            {
                LogMessage(message, eMessageTypeConstants.Warning);
            }

        }

        /// <summary>
        /// Display the current processing options at the console
        /// </summary>
        /// <remarks>Used by MSFileInfoScanner.exe</remarks>
        // ReSharper disable once UnusedMember.Global
        public void ShowCurrentProcessingOptions()
        {
            Console.WriteLine("Processing options");
            Console.WriteLine();
            if (UseCacheFiles)
            {
                Console.WriteLine("CacheFiles are enabled");
                if (ReprocessExistingFiles)
                    Console.WriteLine("Will reprocess files that are already defined in the acquisition time file");
                else if (ReprocessIfCachedSizeIsZero)
                    Console.WriteLine("Will reprocess files if their cached size is 0 bytes");

                Console.WriteLine();
            }

            if (PlotWithPython)
                Console.WriteLine("Plot generator:     Python");
            else
                Console.WriteLine("Plot generator:     OxyPlot");

            Console.WriteLine("SaveTICAndBPIPlots: {0}", TrueFalseToEnabledDisabled(SaveTICAndBPIPlots));
            Console.WriteLine("SaveLCMS2DPlots:    {0}", TrueFalseToEnabledDisabled(SaveLCMS2DPlots));
            if (SaveLCMS2DPlots)
            {
                Console.WriteLine("   MaxPointsToPlot:     {0:N0}", LCMS2DPlotMaxPointsToPlot);
                Console.WriteLine("   OverviewPlotDivisor: {0}", LCMS2DOverviewPlotDivisor);
            }
            Console.WriteLine();

            Console.WriteLine("CheckCentroidingStatus:         {0}", TrueFalseToEnabledDisabled(CheckCentroidingStatus));
            Console.WriteLine("Compute Overall Quality Scores: {0}", TrueFalseToEnabledDisabled(ComputeOverallQualityScores));
            Console.WriteLine("Create dataset info XML file:   {0}", TrueFalseToEnabledDisabled(CreateDatasetInfoFile));
            Console.WriteLine("Create scan stats file:         {0}", TrueFalseToEnabledDisabled(CreateScanStatsFile));
            Console.WriteLine("MS2MzMin:                       {0:N0}", MS2MzMin);
            Console.WriteLine("SHA-1 hashing:                  {0}", TrueFalseToEnabledDisabled(!DisableInstrumentHash));
            if (ScanStart > 0 || ScanEnd > 0)
            {
                Console.WriteLine("Start Scan:                     {0}", ScanStart);
                Console.WriteLine("End Scan:                       {0}", ScanEnd);
            }

            Console.WriteLine("Update dataset stats text file: {0}", TrueFalseToEnabledDisabled(UpdateDatasetStatsTextFile));
            if (UpdateDatasetStatsTextFile)
                Console.WriteLine("   Dataset stats file name: {0}", DatasetStatsTextFileName);

            if (CheckFileIntegrity)
            {
                Console.WriteLine();
                Console.WriteLine("Check integrity of all known file types: enabled");
                Console.WriteLine("   Maximum text file lines to check: {0}", MaximumTextFileLinesToCheck);
                Console.WriteLine("   Compute the SHA-1 has of every file: {0}", TrueFalseToEnabledDisabled(ComputeFileHashes));
                Console.WriteLine("   Check data inside .zip files: {0}", TrueFalseToEnabledDisabled(ZipFileCheckAllData));
            }

            Console.WriteLine();
        }

        private string TrueFalseToEnabledDisabled(bool option)
        {
            return option ? "Enabled" : "Disabled";
        }

        public static bool ValidateDataFilePath(ref string filePath, eDataFileTypeConstants eDataFileType)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(eDataFileType));
            }

            return ValidateDataFilePathCheckDir(filePath);

        }

        private static bool ValidateDataFilePathCheckDir(string filePath)
        {
            bool validFile;

            try
            {
                var datasetFile = new FileInfo(filePath);

                if (!datasetFile.Exists)
                {
                    // Make sure the directory exists
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (datasetFile.Directory != null && !datasetFile.Directory.Exists)
                    {
                        datasetFile.Directory.Create();
                    }
                }
                validFile = true;

            }
            catch (Exception)
            {
                // Ignore errors, but set validFile to false
                validFile = false;
            }

            return validFile;
        }

        private void SleepNow(int sleepTimeSeconds)
        {
            System.Threading.Thread.Sleep(sleepTimeSeconds * 10);
        }

        private bool ValidateExtensions(IList<string> extensions)
        {
            // Returns True if one of the entries in extensions = "*" or ".*"

            var processAllExtensions = false;

            for (var extensionIndex = 0; extensionIndex < extensions.Count; extensionIndex++)
            {
                if (extensions[extensionIndex] == null)
                {
                    extensions[extensionIndex] = string.Empty;
                }
                else
                {
                    if (!extensions[extensionIndex].StartsWith("."))
                    {
                        extensions[extensionIndex] = "." + extensions[extensionIndex];
                    }

                    if (extensions[extensionIndex] == ".*")
                    {
                        processAllExtensions = true;
                        break;
                    }
                    extensions[extensionIndex] = extensions[extensionIndex].ToUpper();
                }
            }

            return processAllExtensions;
        }

        private void WriteFileIntegrityDetails(
            TextWriter writer,
            int directoryID,
            IEnumerable<clsFileIntegrityChecker.udtFileStatsType> udtFileStats)
        {

            if (writer == null)
                return;

            foreach (var item in udtFileStats)
            {
                // Note: HH:mm:ss corresponds to time in 24 hour format
                writer.WriteLine(
                    directoryID.ToString() + '\t' +
                    item.FileName + '\t' +
                    item.SizeBytes + '\t' +
                    item.ModificationDate.ToString("yyyy-MM-dd HH:mm:ss") + '\t' +
                    item.FailIntegrity + '\t' +
                    item.FileHash + '\t' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            if (DateTime.UtcNow.Subtract(mLastWriteTimeFileIntegrityDetails).TotalMinutes > 1)
            {
                writer.Flush();
                mLastWriteTimeFileIntegrityDetails = DateTime.UtcNow;
            }

        }

        private void WriteFileIntegrityFailure(TextWriter writer, string filePath, string message)
        {

            if (writer == null)
                return;

            // Note: HH:mm:ss corresponds to time in 24 hour format
            writer.WriteLine(filePath + '\t' + message + '\t' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            if (DateTime.UtcNow.Subtract(mLastWriteTimeFileIntegrityFailure).TotalMinutes > 1)
            {
                writer.Flush();
                mLastWriteTimeFileIntegrityFailure = DateTime.UtcNow;
            }

        }

        private void FileIntegrityChecker_FileIntegrityFailure(string filePath, string message)
        {
            if (mFileIntegrityErrorsWriter == null)
            {
                OpenFileIntegrityErrorsFile();
            }

            WriteFileIntegrityFailure(mFileIntegrityErrorsWriter, filePath, message);
        }

        private void ExecuteSP_DBErrorEvent(string message, Exception ex)
        {
            ReportError(message);
        }

    }
}
