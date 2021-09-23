using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Plotting;
using MSFileInfoScanner.Readers;
using MSFileInfoScannerInterfaces;
using PRISM;
using PRISM.FileProcessor;
using PRISMDatabaseUtils;

// ReSharper disable UnusedMember.Global

namespace MSFileInfoScanner
{
    /// <summary>
    /// <para>
    /// This program scans a series of MS data files (or data directories) and extracts the acquisition start and end times,
    /// number of spectra, and the total size of the Results are saved to MSFileInfoScanner.DefaultAcquisitionTimeFilename
    /// </para>
    /// <para>
    /// Supported file types are Thermo .RAW files, Agilent Ion Trap (.D directories), Agilent or QStar/QTrap .WIFF files,
    /// MassLynx .Raw directories, Bruker 1 directories, Bruker XMass analysis.baf files, and .UIMF files (IMS)
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started in 2005
    /// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    /// </para>
    /// </remarks>
    public sealed class MSFileInfoScanner : iMSFileInfoScanner
    {
        // Ignore Spelling: Bruker, centroiding, idx, Micromass, OxyPlot, Shimadzu, username, utf, yyyy-MM-dd, hh:mm:ss tt, xtr

        public const string PROGRAM_DATE = "September 23, 2021";

        /// <summary>
        /// Constructor
        /// </summary>
        public MSFileInfoScanner() : this(new InfoScannerOptions())
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MSFileInfoScanner(InfoScannerOptions options)
        {
            Options = options;

            mFileIntegrityChecker = new FileIntegrityChecker(options);
            RegisterEvents(mFileIntegrityChecker);
            mFileIntegrityChecker.FileIntegrityFailure += FileIntegrityChecker_FileIntegrityFailure;

            mMSFileInfoDataCache = new MSFileInfoDataCache();
            RegisterEvents(mMSFileInfoDataCache);

            SetErrorCode(MSFileScannerErrorCodes.NoError);

            DatasetInfoXML = string.Empty;

            mLogFilePath = string.Empty;
            mLogDirectoryPath = string.Empty;

            LCMS2DPlotOptions = new LCMSDataPlotterOptions(Options);

            mFileIntegrityDetailsFilePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(DataFileTypeConstants.FileIntegrityDetails));
            mFileIntegrityErrorsFilePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(DataFileTypeConstants.FileIntegrityErrors));

            mMSFileInfoDataCache.InitializeVariables();

            var oneHourAgo = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0));
            mLastWriteTimeFileIntegrityDetails = oneHourAgo;
            mLastWriteTimeFileIntegrityFailure = oneHourAgo;
            mLastCheckForAbortProcessingFile = oneHourAgo;
        }

        public const string DEFAULT_ACQUISITION_TIME_FILENAME_TXT = "DatasetTimeFile.txt";

        public const string DEFAULT_DIRECTORY_INTEGRITY_INFO_FILENAME_TXT = "DirectoryIntegrityInfo.txt";

        public const string DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT = "FileIntegrityDetails.txt";

        public const string DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT = "FileIntegrityErrors.txt";

        public const string ABORT_PROCESSING_FILENAME = "AbortProcessing.txt";

        public const string XML_SECTION_MSFILESCANNER_SETTINGS = "MSFileInfoScannerSettings";

        private const int FILE_MODIFICATION_WINDOW_MINUTES = 60;

        private const int MAX_FILE_READ_ACCESS_ATTEMPTS = 2;

        private const bool SKIP_FILES_IN_ERROR = true;

        private enum MessageTypeConstants
        {
            Normal = 0,
            ErrorMsg = 1,
            Warning = 2,
            Debug = 3
        }

        private string mFileIntegrityDetailsFilePath;

        private string mFileIntegrityErrorsFilePath;

        private string mLastNotifiedLogFilePath = string.Empty;

        /// <summary>
        /// Log file path
        /// </summary>
        /// <remarks>
        /// This will be defined using Options.LogFilePath
        /// If blank, the file will be auto-named based on the current date
        /// </remarks>
        private string mLogFilePath;

        private StreamWriter mLogFile;

        // This variable is updated in ProcessMSFileOrDirectory
        private string mOutputDirectoryPath;

        /// <summary>
        /// Log directory path
        /// </summary>
        /// <remarks>
        /// This will be defined using Options.LogDirectoryPath
        /// If blank, mOutputDirectoryPath will be used
        /// If mOutputDirectoryPath is also blank, the log file is created in the same directory as the executing assembly
        /// </remarks>
        private string mLogDirectoryPath;

        private readonly FileIntegrityChecker mFileIntegrityChecker;

        private StreamWriter mFileIntegrityDetailsWriter;

        private StreamWriter mFileIntegrityErrorsWriter;

        private MSFileInfoProcessorBaseClass mMSInfoScanner;

        private readonly MSFileInfoDataCache mMSFileInfoDataCache;

        private DateTime mLastWriteTimeFileIntegrityDetails;
        private DateTime mLastWriteTimeFileIntegrityFailure;
        private DateTime mLastCheckForAbortProcessingFile;

        public override bool AbortProcessing { get; set; }

        public override string AcquisitionTimeFilename
        {
            get => GetDataFileFilename(DataFileTypeConstants.MSFileInfo);
            set => SetDataFileFilename(value, DataFileTypeConstants.MSFileInfo);
        }

        /// <summary>
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public override string DatasetInfoXML { get; protected set; }

        public string GetDataFileFilename(DataFileTypeConstants dataFileType)
        {
            return dataFileType switch
            {
                DataFileTypeConstants.MSFileInfo => mMSFileInfoDataCache.AcquisitionTimeFilePath,
                DataFileTypeConstants.DirectoryIntegrityInfo => mMSFileInfoDataCache.DirectoryIntegrityInfoFilePath,
                DataFileTypeConstants.FileIntegrityDetails => mFileIntegrityDetailsFilePath,
                DataFileTypeConstants.FileIntegrityErrors => mFileIntegrityErrorsFilePath,
                _ => string.Empty
            };
        }

        public void SetDataFileFilename(string filePath, DataFileTypeConstants dataFileType)
        {
            switch (dataFileType)
            {
                case DataFileTypeConstants.MSFileInfo:
                    mMSFileInfoDataCache.AcquisitionTimeFilePath = filePath;
                    break;
                case DataFileTypeConstants.DirectoryIntegrityInfo:
                    mMSFileInfoDataCache.DirectoryIntegrityInfoFilePath = filePath;
                    break;
                case DataFileTypeConstants.FileIntegrityDetails:
                    mFileIntegrityDetailsFilePath = filePath;
                    break;
                case DataFileTypeConstants.FileIntegrityErrors:
                    mFileIntegrityErrorsFilePath = filePath;
                    break;
                default:
                    // Unknown file type
                    throw new ArgumentOutOfRangeException(nameof(dataFileType));
            }
        }

        public static string DefaultAcquisitionTimeFilename => DefaultDataFileName(DataFileTypeConstants.MSFileInfo);

        public static string DefaultDataFileName(DataFileTypeConstants dataFileType)
        {
            return dataFileType switch
            {
                DataFileTypeConstants.MSFileInfo => DEFAULT_ACQUISITION_TIME_FILENAME_TXT,
                DataFileTypeConstants.DirectoryIntegrityInfo => DEFAULT_DIRECTORY_INTEGRITY_INFO_FILENAME_TXT,
                DataFileTypeConstants.FileIntegrityDetails => DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT,
                DataFileTypeConstants.FileIntegrityErrors => DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT,
                _ => "UnknownFileType.txt"
            };
        }

        /// <summary>
        /// Processing error code
        /// </summary>
        public override MSFileScannerErrorCodes ErrorCode { get; protected set; }

        /// <summary>
        /// 2D Plotting options
        /// </summary>
        public override LCMSDataPlotterOptions LCMS2DPlotOptions { get; protected set; }

        /// <summary>
        /// MS2MzMin validation error or warning Message
        /// </summary>
        public override string MS2MzMinValidationMessage { get; protected set; }

        /// <summary>
        /// Processing options
        /// </summary>
        public override InfoScannerOptions Options { get; protected set; }

        private void AutoSaveCachedResults()
        {
            if (Options.UseCacheFiles)
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

                var datasetDirectory = GetDirectoryInfo(directoryPath);

                var fileCount = datasetDirectory.GetFiles().Length;

                if (fileCount <= 0)
                {
                    return;
                }

                var checkDirectory = true;
                if (Options.UseCacheFiles && !forceRecheck)
                {
                    if (mMSFileInfoDataCache.CachedDirectoryIntegrityInfoContainsDirectory(datasetDirectory.FullName, out directoryID, out var dataRow))
                    {
                        var cachedFileCount = (int)dataRow[MSFileInfoDataCache.COL_NAME_FILE_COUNT];
                        var cachedCountFailIntegrity = (int)dataRow[MSFileInfoDataCache.COL_NAME_COUNT_FAIL_INTEGRITY];

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

                mFileIntegrityChecker.CheckIntegrityOfFilesInDirectory(directoryPath, out var directoryStats, out var fileStats, processedFileList);

                if (Options.UseCacheFiles)
                {
                    if (!mMSFileInfoDataCache.UpdateCachedDirectoryIntegrityInfo(directoryStats, out directoryID))
                    {
                        directoryID = -1;
                    }
                }

                WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, directoryID, fileStats);
            }
            catch (Exception ex)
            {
                HandleException("Error calling mFileIntegrityChecker", ex);
            }
        }

        /// <summary>
        /// Get the appData directory for this program
        /// For example: C:\Users\username\AppData\Roaming\MSFileInfoScanner
        /// </summary>
        /// <param name="appName"></param>
        public static string GetAppDataDirectoryPath(string appName = "")
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().GetName().Name);
            }

            return ProcessFilesOrDirectoriesBase.GetAppDataDirectoryPath(appName);
        }

        public static string GetAppDirectoryPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Obtain a DirectoryInfo object for the given path
        /// </summary>
        /// <remarks>If the path length is over 210 and not on Linux, converts the path to a Win32 long path</remarks>
        /// <param name="directoryPath"></param>
        public static DirectoryInfo GetDirectoryInfo(string directoryPath)
        {
            return directoryPath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD - 50 && !SystemInfo.IsLinux
                ? new DirectoryInfo(NativeIOFileTools.GetWin32LongPath(directoryPath))
                : new DirectoryInfo(directoryPath);
        }

        /// <summary>
        /// Obtain a FileInfo object for the given path
        /// </summary>
        /// <remarks>If the path length is over 260 and not on Linux, converts the path to a Win32 long path</remarks>
        /// <param name="filePath"></param>
        public static FileInfo GetFileInfo(string filePath)
        {
            return filePath.Length >= NativeIOFileTools.FILE_PATH_LENGTH_THRESHOLD && !SystemInfo.IsLinux
                ? new FileInfo(NativeIOFileTools.GetWin32LongPath(filePath))
                : new FileInfo(filePath);
        }

        public override string[] GetKnownDirectoryExtensions()
        {
            return GetKnownDirectoryExtensionsList().ToArray();
        }

        public List<string> GetKnownDirectoryExtensionsList()
        {
            var extensionsToParse = new List<string>
            {
                AgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION.ToUpper(),
                WatersRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION.ToUpper()
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
                ThermoRawFileInfoScanner.THERMO_RAW_FILE_EXTENSION.ToUpper(),
                AgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION.ToUpper(),
                BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION.ToUpper(),
                BrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION.ToUpper(),
                BrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION.ToUpper(),
                UIMFInfoScanner.UIMF_FILE_EXTENSION.ToUpper(),
                DeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION.ToUpper()
            };

            return extensionsToParse;
        }

        /// <summary>
        /// Get the error message, or an empty string if no error
        /// </summary>
        public override string GetErrorMessage()
        {
            switch (ErrorCode)
            {
                case MSFileScannerErrorCodes.NoError:
                    return string.Empty;
                case MSFileScannerErrorCodes.InvalidInputFilePath:
                    return "Invalid input file path";
                case MSFileScannerErrorCodes.InvalidOutputDirectoryPath:
                    return "Invalid output directory path";
                case MSFileScannerErrorCodes.ParameterFileNotFound:
                    return "Parameter file not found";
                case MSFileScannerErrorCodes.FilePathError:
                    return "General file path error";
                case MSFileScannerErrorCodes.ParameterFileReadError:
                    return "Parameter file read error";
                case MSFileScannerErrorCodes.UnknownFileExtension:
                    return "Unknown file extension";
                case MSFileScannerErrorCodes.InputFileReadError:
                    return "Input file read error";
                case MSFileScannerErrorCodes.InputFileAccessError:
                    return "Input file access error";
                case MSFileScannerErrorCodes.OutputFileWriteError:
                    return "Error writing output file";
                case MSFileScannerErrorCodes.FileIntegrityCheckError:
                    return "Error checking file integrity";
                case MSFileScannerErrorCodes.DatabasePostingError:
                    return "Database posting error";
                case MSFileScannerErrorCodes.MS2MzMinValidationError:

                    // Over 10% of the MS/MS spectra have a minimum m/z value larger than the required minimum
                    var errorMsg = string.Format("Over {0}% of the MS/MS spectra have a minimum m/z value larger than the required minimum; " +
                                                 "reporter ion peaks likely could not be detected", MSFileInfoProcessorBaseClass.MAX_PERCENT_MS2MZMIN_ALLOWED_FAILED);

                    if (!string.IsNullOrWhiteSpace(MS2MzMinValidationMessage))
                    {
                        return errorMsg + "; " + MS2MzMinValidationMessage;
                    }
                    else
                    {
                        return errorMsg;
                    }
                case MSFileScannerErrorCodes.MS2MzMinValidationWarning:
                    const string warningMsg = "Some of the MS/MS spectra have a minimum m/z value larger than the required minimum; " +
                                              "reporter ion peaks likely could not be detected";

                    if (!string.IsNullOrWhiteSpace(MS2MzMinValidationMessage))
                    {
                        return warningMsg + "; " + MS2MzMinValidationMessage;
                    }
                    else
                    {
                        return warningMsg;
                    }

                case MSFileScannerErrorCodes.UnspecifiedError:
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
            var datasetFile = GetFileInfo(fileOrDirectoryPath);

            if (datasetFile.Exists)
            {
                fileOrDirectoryInfo = datasetFile;
                return true;
            }

            // See if fileOrDirectoryPath points to a directory
            var datasetDirectory = GetDirectoryInfo(fileOrDirectoryPath);
            if (datasetDirectory.Exists)
            {
                fileOrDirectoryInfo = datasetDirectory;
                isDirectory = true;
                return true;
            }

            fileOrDirectoryInfo = GetFileInfo(fileOrDirectoryPath);
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
            if (Options.UseCacheFiles)
            {
                mMSFileInfoDataCache.LoadCachedResults(forceLoad);
            }
        }

        private void LogMessage(string message, MessageTypeConstants messageType = MessageTypeConstants.Normal)
        {
            // Note that ProcessMSFileOrDirectory() will update mOutputDirectoryPath, which is used here if mLogDirectoryPath is blank

            if (mLogFile == null && Options.LogMessagesToFile)
            {
                try
                {
                    if (string.IsNullOrEmpty(Options.LogFilePath))
                    {
                        // Auto-name the log file
                        mLogFilePath = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
                        mLogFilePath += "_log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                    }
                    else
                    {
                        mLogFilePath = Options.LogFilePath;
                    }

                    try
                    {
                        mLogDirectoryPath = string.IsNullOrEmpty(Options.LogDirectoryPath) ? string.Empty : Options.LogDirectoryPath;

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

                    if (!mLastNotifiedLogFilePath.Equals(mLogFilePath))
                    {
                        string logFilePathToShow;
                        if (Options.ShowDebugInfo)
                        {
                            var logFileInfo = GetFileInfo(mLogFilePath);
                            logFilePathToShow = logFileInfo.FullName;
                        }
                        else
                        {
                            logFilePathToShow = mLogFilePath;
                        }

                        if (openingExistingFile)
                        {
                            OnStatusEvent("Appending to log file " + logFilePathToShow);
                        }
                        else
                        {
                            OnStatusEvent("Creating log file " + logFilePathToShow);
                        }

                        Console.WriteLine();
                        mLastNotifiedLogFilePath = mLogFilePath;
                    }

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
                    // Error creating the log file; clear the log file path so that we don't repeatedly try to create it
                    Options.LogFilePath = string.Empty;
                }
            }

            if (mLogFile == null)
                return;

            var messageTypeName = messageType switch
            {
                MessageTypeConstants.Debug => "Debug",
                MessageTypeConstants.Normal => "Normal",
                MessageTypeConstants.ErrorMsg => "Error",
                MessageTypeConstants.Warning => "Warning",
                _ => "Unknown"
            };

            mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + '\t' + messageTypeName + '\t' + message);
        }

        /// <summary>
        /// Read settings from a Key=Value parameter file
        /// </summary>
        /// <param name="parameterFilePath"></param>
        public override bool LoadParameterFileSettings(string parameterFilePath)
        {
            try
            {
                if (!File.Exists(parameterFilePath))
                {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(GetAppDirectoryPath(), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath))
                    {
                        ReportError("Parameter file not found: " + parameterFilePath);
                        SetErrorCode(MSFileScannerErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                var cachedInputFilePath = Options.InputDataFilePath;
                var cachedOutputDirectoryPath = Options.OutputDirectoryPath;

                var appVersion = ProcessFilesOrDirectoriesBase.GetAppVersion(PROGRAM_DATE);

                var args = new List<string>
                {
                    "-ParamFile",
                    parameterFilePath
                };

                var parser = new CommandLineParser<InfoScannerOptions>(Options, appVersion);
                var result = parser.ParseArgs(args.ToArray());

                // Note that result.ParsedResults and Options point to the same instance of InfoScannerOptions

                if (!result.Success)
                {
                    ReportWarning(string.Format(
                        "Error loading options from parameter file {0} using the CommandLineParser class",
                        parameterFilePath));

                    if (result.ParseErrors.Count == 0)
                    {
                        ReportWarning("Unspecified error");
                         return false;
                    }

                    foreach (var item in result.ParseErrors)
                    {
                        ReportWarning(item.Message);
                    }

                    return false;
                }

                if (string.IsNullOrWhiteSpace(Options.InputDataFilePath) && !string.IsNullOrWhiteSpace(cachedInputFilePath))
                {
                    Options.InputDataFilePath = cachedInputFilePath;
                }

                if (string.IsNullOrWhiteSpace(Options.OutputDirectoryPath) && !string.IsNullOrWhiteSpace(cachedOutputDirectoryPath))
                {
                    Options.OutputDirectoryPath = cachedOutputDirectoryPath;
                }

                Options.ParameterFilePath = parameterFilePath;
                return true;
            }
            catch (Exception ex)
            {
                HandleException("Error in LoadParameterFileSettings", ex);
                return false;
            }
        }

        private void OpenFileIntegrityDetailsFile()
        {
            OpenFileIntegrityOutputFile(DataFileTypeConstants.FileIntegrityDetails, ref mFileIntegrityDetailsFilePath, ref mFileIntegrityDetailsWriter);
        }

        private void OpenFileIntegrityErrorsFile()
        {
            OpenFileIntegrityOutputFile(DataFileTypeConstants.FileIntegrityErrors, ref mFileIntegrityErrorsFilePath, ref mFileIntegrityErrorsWriter);
        }

        private void OpenFileIntegrityOutputFile(DataFileTypeConstants dataFileType, ref string filePath, ref StreamWriter writer)
        {
            var openedExistingFile = false;
            FileStream fsFileStream = null;

            var defaultFileName = DefaultDataFileName(dataFileType);
            ValidateDataFilePath(ref filePath, dataFileType);

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
                        writer.WriteLine(mMSFileInfoDataCache.ConstructHeaderLine(dataFileType));
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
        /// <param name="datasetName">Dataset name</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetName)
        {
            return PostDatasetInfoToDB(datasetName, DatasetInfoXML, Options.DatabaseConnectionString, Options.DSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetName, string datasetInfoXML)
        {
            return PostDatasetInfoToDB(datasetName, datasetInfoXML, Options.DatabaseConnectionString, Options.DSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetName, string connectionString, string storedProcedureName)
        {
            return PostDatasetInfoToDB(datasetName, DatasetInfoXML, connectionString, storedProcedureName);
        }

        /// <summary>
        /// Post the dataset info in datasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string datasetName, string datasetInfoXML, string connectionString, string storedProcedureName)
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
                    SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName))
                {
                    storedProcedureName = "UpdateDatasetFileInfoXML";
                }

                var applicationName = "MSFileInfoScanner_" + datasetName;

                var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, applicationName);

                var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse);
                RegisterEvents(dbTools);

                var cmd = dbTools.CreateCommand(storedProcedureName, CommandType.StoredProcedure);

                var returnParam = dbTools.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);

                dbTools.AddParameter(cmd, "@DatasetInfoXML", SqlType.XML).Value = dsInfoXMLClean;

                var result = dbTools.ExecuteSP(cmd, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == DbUtilsConstants.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                }
                else
                {
                    ReportError("Error calling stored procedure, return code = " + returnParam.Value.CastDBVal<string>());
                    SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }

                // Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
                // Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
                // PostDatasetInfoToDB(32, datasetInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

            }
            catch (Exception ex)
            {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
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
                if (datasetID == 0 && Options.DatasetID > 0)
                {
                    datasetID = Options.DatasetID;
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
                    SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName))
                {
                    storedProcedureName = "CacheDatasetInfoXML";
                }

                var applicationName = "MSFileInfoScanner_DatasetID_" + datasetID;

                var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, applicationName);

                var dbTools = DbToolsFactory.GetDBTools(connectionStringToUse);
                RegisterEvents(dbTools);

                var cmd = dbTools.CreateCommand(storedProcedureName, CommandType.StoredProcedure);

                var returnParam = dbTools.AddParameter(cmd, "@Return", SqlType.Int, ParameterDirection.ReturnValue);
                dbTools.AddParameter(cmd, "@DatasetID", SqlType.Int).Value = datasetID;
                dbTools.AddParameter(cmd, "@DatasetInfoXML", SqlType.XML).Value = dsInfoXMLClean;

                var result = dbTools.ExecuteSP(cmd, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == DbUtilsConstants.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                }
                else
                {
                    ReportError("Error calling stored procedure, return code = " + returnParam.Value.CastDBVal<string>());
                    SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }
            }
            catch (Exception ex)
            {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                success = false;
            }

            return success;
        }

        private bool ProcessMSDataset(string inputFileOrDirectoryPath, MSFileInfoProcessorBaseClass scanner, string datasetName, string outputDirectoryPath)
        {
            var datasetFileInfo = new DatasetFileInfo();
            if (!string.IsNullOrWhiteSpace(datasetName))
                datasetFileInfo.DatasetName = datasetName;

            if (Options.PlotWithPython)
            {
                LCMS2DPlotOptions.PlotWithPython = true;

                if (Options.SaveTICAndBPIPlots || Options.SaveLCMS2DPlots)
                {
                    // Make sure that Python exists
                    if (!PythonPlotContainer.PythonInstalled)
                    {
                        ReportError("Could not find the python executable");
                        var debugMsg = "Paths searched:";
                        foreach (var item in PythonPlotContainer.PythonPathsToCheck())
                        {
                            debugMsg += "\n  " + item;
                        }
                        OnDebugEvent(debugMsg);

                        SetErrorCode(MSFileScannerErrorCodes.OutputFileWriteError);
                        return false;
                    }
                }
            }
            else if (SystemInfo.IsLinux)
            {
                OnWarningEvent("Plotting with OxyPlot is not supported on Linux; " +
                               "you should set PlotWithPython=True in the parameter file or use -PythonPlot at the command line");
            }

            if (Options.ShowDebugInfo)
            {
                LCMS2DPlotOptions.DeleteTempFiles = false;
            }

            var retryCount = 0;
            bool success;

            while (true)
            {
                // Process the data file
                success = scanner.ProcessDataFile(inputFileOrDirectoryPath, datasetFileInfo);

                if (success)
                    break;

                retryCount++;

                if (retryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS)
                    break;

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

            if (!success && retryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS)
            {
                if (!string.IsNullOrWhiteSpace(datasetFileInfo.DatasetName))
                {
                    // Make an entry anyway; probably a corrupted file
                    success = true;
                }
                else
                {
                    SetErrorCode(scanner.ErrorCode, true);
                }
            }

            if (success)
            {
                success = scanner.CreateOutputFiles(inputFileOrDirectoryPath, outputDirectoryPath);
                if (!success)
                {
                    SetErrorCode(MSFileScannerErrorCodes.OutputFileWriteError);
                }

                // Cache the Dataset Info XML
                DatasetInfoXML = scanner.GetDatasetInfoXML();

                if (Options.UseCacheFiles)
                {
                    // Update the results database
                    mMSFileInfoDataCache.UpdateCachedMSFileInfo(datasetFileInfo);

                    // Possibly auto-save the cached results
                    AutoSaveCachedResults();
                }

                if (Options.PostResultsToDMS)
                {
                    var dbSuccess = PostDatasetInfoToDB(datasetFileInfo.DatasetName, DatasetInfoXML);
                    if (!dbSuccess)
                    {
                        SetErrorCode(MSFileScannerErrorCodes.DatabasePostingError);
                        success = false;
                    }
                }

                if (scanner.MS2MzMinValidationError)
                {
                    MS2MzMinValidationMessage = scanner.MS2MzMinValidationMessage;
                    SetErrorCode(MSFileScannerErrorCodes.MS2MzMinValidationError);
                    success = false;
                }
                else if (scanner.MS2MzMinValidationWarning)
                {
                    MS2MzMinValidationMessage = scanner.MS2MzMinValidationMessage;
                    SetErrorCode(MSFileScannerErrorCodes.MS2MzMinValidationWarning);
                }

                if (datasetFileInfo.ScanCount == 0 && ErrorCode == MSFileScannerErrorCodes.NoError)
                {
                    if (scanner is GenericFileInfoScanner)
                    {
                        OnWarningEvent("Spectra data and acquisition details were not loaded since the file was processed with the generic scanner: " + inputFileOrDirectoryPath);
                    }
                    else
                    {
                        OnWarningEvent("Dataset has no spectra: " + inputFileOrDirectoryPath);
                        SetErrorCode(MSFileScannerErrorCodes.DatasetHasNoSpectra);
                    }
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
        /// <param name="msFileProcessingState"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessMSFileOrDirectory(
            string inputFileOrDirectoryPath,
            string outputDirectoryPath,
            bool resetErrorCode,
            out MSFileProcessingStateConstants msFileProcessingState)
        {
            // Note: inputFileOrDirectoryPath must be a known MS data file or MS data directory
            // See function ProcessMSFilesAndRecurseDirectories for more details
            // This function returns True if it processed a file (or the dataset was processed previously)
            // When SKIP_FILES_IN_ERROR = True, it also returns True if the file type was a known type but the processing failed
            // If the file type is unknown, or if an error occurs, it returns false
            // msFileProcessingState will be updated based on whether the file is processed, skipped, etc.

            var success = false;

            if (resetErrorCode)
            {
                SetErrorCode(MSFileScannerErrorCodes.NoError);
                MS2MzMinValidationMessage = string.Empty;
            }

            msFileProcessingState = MSFileProcessingStateConstants.NotProcessed;

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
                        HandleException("Error extracting the name from " + inputFileOrDirectoryPath, ex);
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
                            SetErrorCode(MSFileScannerErrorCodes.FilePathError);
                            return false;
                        }
                    }

                    var knownMSDataType = false;

                    // Only continue if it's a known type
                    if (isDirectory)
                    {
                        if (fileOrDirectoryInfo.Name == BrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME)
                        {
                            // Bruker 1 directory
                            mMSInfoScanner = new BrukerOneFolderInfoScanner(Options, LCMS2DPlotOptions);
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
                                case AgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION:
                                    // Agilent .D directory or Bruker .D directory

                                    var datasetDirectory = GetDirectoryInfo(inputFileOrDirectoryPath);

                                    if (PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME).Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_TDF_FILE_NAME).Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_SER_FILE_NAME).Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_FID_FILE_NAME).Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_idx").Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_xtr").Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME).Count > 0 ||
                                        PathUtils.FindFilesWildcard(datasetDirectory, BrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME).Count > 0)
                                    {
                                        mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    }
                                    else if (PathUtils.FindFilesWildcard(datasetDirectory, AgilentGCDFolderInfoScanner.AGILENT_MS_DATA_FILE).Count > 0 ||
                                             PathUtils.FindFilesWildcard(datasetDirectory, AgilentGCDFolderInfoScanner.AGILENT_ACQ_METHOD_FILE).Count > 0 ||
                                             PathUtils.FindFilesWildcard(datasetDirectory, AgilentGCDFolderInfoScanner.AGILENT_GC_INI_FILE).Count > 0)
                                    {
                                        mMSInfoScanner = new AgilentGCDFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    }
                                    else if (PathUtils.FindDirectoriesWildcard(datasetDirectory, AgilentTOFDFolderInfoScanner.AGILENT_ACQDATA_FOLDER_NAME).Count > 0)
                                    {
                                        mMSInfoScanner = new AgilentTOFDFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    }
                                    else
                                    {
                                        mMSInfoScanner = new AgilentIonTrapDFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    }

                                    knownMSDataType = true;
                                    break;

                                case WatersRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION:
                                    // Micromass .Raw directory
                                    mMSInfoScanner = new WatersRawFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                default:
                                    // Unknown directory extension (or no extension)
                                    // See if the directory contains one or more 0_R*.zip files
                                    if (PathUtils.FindFilesWildcard(GetDirectoryInfo(inputFileOrDirectoryPath), ZippedImagingFilesScanner.ZIPPED_IMAGING_FILE_SEARCH_SPEC).Count > 0)
                                    {
                                        mMSInfoScanner = new ZippedImagingFilesScanner(Options, LCMS2DPlotOptions);
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (string.Equals(fileOrDirectoryInfo.Name, BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                            knownMSDataType = true;
                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, BrukerXmassFolderInfoScanner.BRUKER_TDF_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                            knownMSDataType = true;
                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, BrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                            knownMSDataType = true;
                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, BrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                            knownMSDataType = true;
                        }
                        else if (string.Equals(fileOrDirectoryInfo.Extension, ".qgd", StringComparison.OrdinalIgnoreCase))
                        {
                            // Shimadzu GC file
                            // Use the generic scanner to read the file size, modification date, and compute the SHA-1 hash
                            mMSInfoScanner = new GenericFileInfoScanner(Options, LCMS2DPlotOptions);
                            knownMSDataType = true;
                        }
                        else if (string.Equals(fileOrDirectoryInfo.Name, BrukerXmassFolderInfoScanner.BRUKER_ANALYSIS_YEP_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            // If the directory also contains file BRUKER_EXTENSION_BAF_FILE_NAME then this is a Bruker XMass directory
                            var parentDirectory = Path.GetDirectoryName(fileOrDirectoryInfo.FullName);
                            if (!string.IsNullOrEmpty(parentDirectory))
                            {
                                var pathCheck = Path.Combine(parentDirectory, BrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME);
                                if (File.Exists(pathCheck))
                                {
                                    mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                }
                            }
                        }

                        if (!knownMSDataType)
                        {
                            // Examine the extension on inputFileOrDirectoryPath
                            switch (fileOrDirectoryInfo.Extension.ToUpper())
                            {
                                case ThermoRawFileInfoScanner.THERMO_RAW_FILE_EXTENSION:
                                    // Thermo .raw file
                                    mMSInfoScanner = new ThermoRawFileInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case AgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION:
                                    mMSInfoScanner = new AgilentTOFOrQStarWiffFileInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case BrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION:
                                    mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case BrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION:
                                    mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case BrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION:
                                    mMSInfoScanner = new BrukerXmassFolderInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case MzMLFileInfoScanner.MZML_FILE_EXTENSION:
                                    mMSInfoScanner = new MzMLFileInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case UIMFInfoScanner.UIMF_FILE_EXTENSION:
                                    mMSInfoScanner = new UIMFInfoScanner(Options, LCMS2DPlotOptions);
                                    knownMSDataType = true;
                                    break;

                                case DeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION:

                                    if (fileOrDirectoryInfo.FullName.EndsWith(DeconToolsIsosInfoScanner.DECONTOOLS_ISOS_FILE_SUFFIX, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        mMSInfoScanner = new DeconToolsIsosInfoScanner(Options, LCMS2DPlotOptions);
                                        knownMSDataType = true;
                                    }
                                    break;

                                default:
                                    // Unknown file extension; check for a zipped directory
                                    if (BrukerOneFolderInfoScanner.IsZippedSFolder(fileOrDirectoryInfo.Name))
                                    {
                                        // Bruker s001.zip file
                                        mMSInfoScanner = new BrukerOneFolderInfoScanner(Options, LCMS2DPlotOptions);
                                        knownMSDataType = true;
                                    }
                                    else if (ZippedImagingFilesScanner.IsZippedImagingFile(fileOrDirectoryInfo.Name))
                                    {
                                        mMSInfoScanner = new ZippedImagingFilesScanner(Options, LCMS2DPlotOptions);
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }
                    }

                    if (!knownMSDataType)
                    {
                        ReportError("Unknown file type: " + Path.GetFileName(inputFileOrDirectoryPath));
                        SetErrorCode(MSFileScannerErrorCodes.UnknownFileExtension);
                        return false;
                    }

                    // Attach the events
                    RegisterEvents(mMSInfoScanner);

                    var datasetName = mMSInfoScanner.GetDatasetNameViaPath(fileOrDirectoryInfo.FullName);

                    if (Options.UseCacheFiles && !Options.ReprocessExistingFiles)
                    {
                        // See if the datasetName in inputFileOrDirectoryPath is already present in mCachedResults
                        // If it is present, don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

                        if (datasetName.Length > 0 && mMSFileInfoDataCache.CachedMSInfoContainsDataset(datasetName, out var dataRow))
                        {
                            if (Options.ReprocessIfCachedSizeIsZero)
                            {
                                long cachedSizeBytes;
                                try
                                {
                                    cachedSizeBytes = (long)dataRow[MSFileInfoDataCache.COL_NAME_FILE_SIZE_BYTES];
                                }
                                catch (Exception)
                                {
                                    cachedSizeBytes = 1;
                                }

                                if (cachedSizeBytes > 0)
                                {
                                    // File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                    ReportMessage("  Skipping " + Path.GetFileName(inputFileOrDirectoryPath) + " since already in cached results");
                                    msFileProcessingState = MSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                    return true;
                                }
                            }
                            else
                            {
                                // File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                                ReportMessage("  Skipping " + Path.GetFileName(inputFileOrDirectoryPath) + " since already in cached results");
                                msFileProcessingState = MSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                return true;
                            }
                        }
                    }

                    // Process the data file or directory
                    success = ProcessMSDataset(inputFileOrDirectoryPath, mMSInfoScanner, datasetName, outputDirectoryPath);
                    if (success)
                    {
                        msFileProcessingState = MSFileProcessingStateConstants.ProcessedSuccessfully;
                    }
                    else
                    {
                        msFileProcessingState = MSFileProcessingStateConstants.FailedProcessing;
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
                    SetErrorCode(MSFileScannerErrorCodes.NoError);
                }

                // See if inputFilePath contains a wildcard
                MSFileProcessingStateConstants msFileProcessingState;
                if (inputFileOrDirectoryPath != null &&
                    (inputFileOrDirectoryPath.IndexOf('*') >= 0 || inputFileOrDirectoryPath.IndexOf('?') >= 0))
                {
                    // Obtain a list of the matching files and directories

                    // Copy the path into cleanPath and replace any * or ? characters with _
                    var cleanPath = inputFileOrDirectoryPath.Replace("*", "_").Replace("?", "_");

                    var datasetFile = GetFileInfo(cleanPath);
                    string inputDirectoryPath;

                    if (datasetFile.Directory?.Exists == true)
                    {
                        inputDirectoryPath = datasetFile.DirectoryName;
                    }
                    else
                    {
                        // Use the current working directory
                        inputDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }

                    if (string.IsNullOrEmpty(inputDirectoryPath))
                        inputDirectoryPath = ".";

                    var datasetDirectory = GetDirectoryInfo(inputDirectoryPath);

                    // Remove any directory information from inputFileOrDirectoryPath
                    var fileOrDirectoryMatchSpec = Path.GetFileName(inputFileOrDirectoryPath);

                    var matchCount = 0;

                    // Force this to true to avoid squashing newly-created index.html files
                    Options.UseDatasetNameForHtmlPlotsFile = true;

                    foreach (var fileItem in PathUtils.FindFilesWildcard(datasetDirectory, fileOrDirectoryMatchSpec))
                    {
                        success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, resetErrorCode, out msFileProcessingState);

                        if (msFileProcessingState == MSFileProcessingStateConstants.ProcessedSuccessfully ||
                            msFileProcessingState == MSFileProcessingStateConstants.FailedProcessing)
                        {
                            processedFileList.Add(fileItem.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (AbortProcessing)
                            break;

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount++;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (AbortProcessing)
                        return false;

                    foreach (var directoryItem in PathUtils.FindDirectoriesWildcard(datasetDirectory, fileOrDirectoryMatchSpec))
                    {
                        success = ProcessMSFileOrDirectory(directoryItem.FullName, outputDirectoryPath, resetErrorCode, out msFileProcessingState);

                        if (msFileProcessingState == MSFileProcessingStateConstants.ProcessedSuccessfully || msFileProcessingState == MSFileProcessingStateConstants.FailedProcessing)
                        {
                            processedFileList.Add(directoryItem.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (AbortProcessing)
                            break;

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount++;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (AbortProcessing)
                        return false;

                    if (Options.CheckFileIntegrity)
                    {
                        CheckIntegrityOfFilesInDirectory(datasetDirectory.FullName, Options.ReprocessExistingFiles, processedFileList);
                    }

                    if (matchCount == 0)
                    {
                        if (ErrorCode == MSFileScannerErrorCodes.NoError)
                        {
                            ReportWarning("No match was found for the input file path:" + fileOrDirectoryMatchSpec);
                        }
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }
                else
                {
                    // inputFileOrDirectoryPath does not have a * or ? wildcard

                    success = ProcessMSFileOrDirectory(inputFileOrDirectoryPath, outputDirectoryPath, resetErrorCode, out msFileProcessingState);
                    if (!success && ErrorCode == MSFileScannerErrorCodes.NoError)
                    {
                        SetErrorCode(MSFileScannerErrorCodes.UnspecifiedError);
                    }

                    if (AbortProcessing)
                        return false;

                    if (Options.CheckFileIntegrity && inputFileOrDirectoryPath != null)
                    {
                        string directoryPath;

                        var candidateDirectory = GetDirectoryInfo(inputFileOrDirectoryPath);
                        if (candidateDirectory.Exists)
                        {
                            directoryPath = candidateDirectory.FullName;
                        }
                        else
                        {
                            var dataFile = GetFileInfo(inputFileOrDirectoryPath);
                            directoryPath = dataFile.Directory?.FullName;
                        }

                        if (directoryPath == null)
                        {
                            ReportError("Unable to determine the parent directory of " + inputFileOrDirectoryPath);
                        }
                        else
                        {
                            CheckIntegrityOfFilesInDirectory(directoryPath, Options.ReprocessExistingFiles, processedFileList);
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
        /// Calls ProcessMSFileOrDirectory for all files in inputFilePathOrDirectory and below having a known extension
        ///  Known extensions are:
        ///   .Raw for Thermo files
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

                    var datasetFile = GetFileInfo(cleanPath);
                    if (Path.IsPathRooted(cleanPath))
                    {
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (datasetFile.Directory?.Exists == false)
                        {
                            ReportError("Directory not found: " + datasetFile.DirectoryName);
                            SetErrorCode(MSFileScannerErrorCodes.InvalidInputFilePath);
                            return false;
                        }
                    }

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (datasetFile.Directory?.Exists == true)
                    {
                        inputDirectoryPath = datasetFile.DirectoryName;
                    }
                    else
                    {
                        // Directory not found; use the current working directory
                        inputDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from inputFilePathOrDirectory
                    inputFilePathOrDirectory = Path.GetFileName(inputFilePathOrDirectory);
                }
                else
                {
                    if (string.IsNullOrEmpty(inputFilePathOrDirectory))
                        inputFilePathOrDirectory = ".";

                    var datasetDirectory = GetDirectoryInfo(inputFilePathOrDirectory);
                    if (datasetDirectory.Exists)
                    {
                        inputDirectoryPath = datasetDirectory.FullName;
                        inputFilePathOrDirectory = "*";
                    }
                    else
                    {
                        if (datasetDirectory.Parent?.Exists == true)
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

                    // Force this to true to avoid squashing newly-created index.html files
                    Options.UseDatasetNameForHtmlPlotsFile = true;

                    // Call RecurseDirectoriesWork
                    success = RecurseDirectoriesWork(inputDirectoryPath, inputFilePathOrDirectory, outputDirectoryPath, ref fileProcessCount, ref fileProcessFailCount, 1, maxLevelsToRecurse);
                }
                else
                {
                    SetErrorCode(MSFileScannerErrorCodes.InvalidInputFilePath);
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

            // If maxLevelsToRecurse is <=0, we recurse infinitely

            DirectoryInfo inputDirectory;

            List<string> fileExtensionsToParse;
            List<string> directoryExtensionsToParse;
            bool processAllFileExtensions;

            bool success;
            var fileProcessed = false;

            MSFileProcessingStateConstants msFileProcessingState;

            var processedFileList = new List<string>();

            var retryCount = 0;
            while (true)
            {
                try
                {
                    inputDirectory = GetDirectoryInfo(inputDirectoryPath);
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

                retryCount++;
                if (retryCount >= MAX_ACCESS_ATTEMPTS)
                {
                    return false;
                }

                // Wait 1 second, then try again
                SleepNow(1);
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

                foreach (var fileItem in PathUtils.FindFilesWildcard(inputDirectory, fileNameMatch))
                {
                    retryCount = 0;
                    while (true)
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
                                success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, true, out msFileProcessingState);

                                if (msFileProcessingState == MSFileProcessingStateConstants.ProcessedSuccessfully || msFileProcessingState == MSFileProcessingStateConstants.FailedProcessing)
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
                                if (BrukerOneFolderInfoScanner.IsZippedSFolder(fileItem.Name))
                                {
                                    // Only process this file if there is not a subdirectory named "1" present"
                                    if (PathUtils.FindDirectoriesWildcard(inputDirectory, BrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Count == 0)
                                    {
                                        fileProcessed = true;
                                        processedZippedSFolder = true;
                                        success = ProcessMSFileOrDirectory(fileItem.FullName, outputDirectoryPath, true, out msFileProcessingState);

                                        if (msFileProcessingState == MSFileProcessingStateConstants.ProcessedSuccessfully || msFileProcessingState == MSFileProcessingStateConstants.FailedProcessing)
                                        {
                                            processedFileList.Add(fileItem.FullName);
                                        }
                                    }
                                }
                            }

                            // Exit the while loop
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

                        retryCount++;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS)
                        {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);
                    }

                    if (fileProcessed)
                    {
                        if (success)
                        {
                            fileProcessCount++;
                        }
                        else
                        {
                            fileProcessFailCount++;
                            success = true;
                        }
                    }

                    CheckForAbortProcessingFile();
                    if (AbortProcessing)
                        break;
                }

                if (Options.CheckFileIntegrity && !AbortProcessing)
                {
                    CheckIntegrityOfFilesInDirectory(inputDirectory.FullName, Options.ReprocessExistingFiles, processedFileList);
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

                foreach (var subdirectory in PathUtils.FindDirectoriesWildcard(inputDirectory, fileNameMatch))
                {
                    retryCount = 0;
                    while (true)
                    {
                        try
                        {
                            // Check whether the directory name is BRUKER_ONE_FOLDER = "1"
                            if (subdirectory.Name == BrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME)
                            {
                                success = ProcessMSFileOrDirectory(subdirectory.FullName, outputDirectoryPath, true, out msFileProcessingState);
                                if (!success)
                                {
                                    fileProcessFailCount++;
                                    success = true;
                                }
                                else
                                {
                                    fileProcessCount++;
                                }
                                subdirectoriesProcessed++;
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

                                    success = ProcessMSFileOrDirectory(subdirectory.FullName, outputDirectoryPath, true, out msFileProcessingState);
                                    if (!success)
                                    {
                                        fileProcessFailCount++;
                                        success = true;
                                    }
                                    else
                                    {
                                        fileProcessCount++;
                                    }
                                    subdirectoriesProcessed++;
                                    subdirectoryNamesProcessed.Add(subdirectory.Name);

                                    // Successfully processed a directory; exit the for loop
                                    break;
                                }
                            }

                            // Exit the while loop
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

                        retryCount++;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS)
                        {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);
                    }

                    if (AbortProcessing)
                        break;
                }

                // If maxLevelsToRecurse is <=0 then we recurse infinitely
                //  otherwise, compare recursionLevel to maxLevelsToRecurse
                if (maxLevelsToRecurse <= 0 || recursionLevel <= maxLevelsToRecurse)
                {
                    // Call this function for each of the subdirectories of inputDirectory
                    // However, do not step into directories listed in subdirectoryNamesProcessed

                    foreach (var subdirectory in PathUtils.FindDirectoriesWildcard(inputDirectory, "*"))
                    {
                        retryCount = 0;
                        while (true)
                        {
                            try
                            {
                                if (subdirectoriesProcessed == 0 || !subdirectoryNamesProcessed.Contains(subdirectory.Name))
                                {
                                    success = RecurseDirectoriesWork(subdirectory.FullName, fileNameMatch, outputDirectoryPath, ref fileProcessCount, ref fileProcessFailCount, recursionLevel + 1, maxLevelsToRecurse);
                                }

                                if (!success && !Options.IgnoreErrorsWhenRecursing)
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

                            retryCount++;
                            if (retryCount >= MAX_ACCESS_ATTEMPTS)
                            {
                                return false;
                            }

                            // Wait 1 second, then try again
                            SleepNow(1);
                        }

                        if (!success && !Options.IgnoreErrorsWhenRecursing)
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
            if (Options.UseCacheFiles)
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
                    // settingsFile.SetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ConnectionString", this.DatabaseConnectionString)

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

        private void SetErrorCode(MSFileScannerErrorCodes newErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && ErrorCode != MSFileScannerErrorCodes.NoError)
            {
                // An error code is already defined; do not change it
                return;
            }

            ErrorCode = newErrorCode;
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

            LogMessage(formattedError, MessageTypeConstants.ErrorMsg);
        }

        /// <summary>
        /// Report a status message and optionally write to the log file
        /// </summary>
        /// <param name="message"></param>
        /// <param name="allowLogToFile"></param>
        /// <param name="messageType"></param>
        private void ReportMessage(
            string message,
            bool allowLogToFile = true,
            MessageTypeConstants messageType = MessageTypeConstants.Normal)
        {
            if (messageType == MessageTypeConstants.Debug)
                OnDebugEvent(message);
            if (messageType == MessageTypeConstants.Warning)
                OnWarningEvent(message);
            else
                OnStatusEvent(message);

            if (allowLogToFile)
            {
                LogMessage(message, messageType);
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
                LogMessage(message, MessageTypeConstants.Warning);
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
            if (Options.UseCacheFiles)
            {
                Console.WriteLine("CacheFiles are enabled");
                if (Options.ReprocessExistingFiles)
                    Console.WriteLine("Will reprocess files that are already defined in the acquisition time file");
                else if (Options.ReprocessIfCachedSizeIsZero)
                    Console.WriteLine("Will reprocess files if their cached size is 0 bytes");

                Console.WriteLine();
            }

            if (Options.PlotWithPython)
                Console.WriteLine("Plot generator:     Python");
            else
                Console.WriteLine("Plot generator:     OxyPlot");

            Console.WriteLine("SaveTICAndBPIPlots: {0}", TrueFalseToEnabledDisabled(Options.SaveTICAndBPIPlots));
            Console.WriteLine("SaveLCMS2DPlots:    {0}", TrueFalseToEnabledDisabled(Options.SaveLCMS2DPlots));
            if (Options.SaveLCMS2DPlots)
            {
                Console.WriteLine("   MaxPointsToPlot:     {0:N0}", LCMS2DPlotOptions.MaxPointsToPlot);
                Console.WriteLine("   OverviewPlotDivisor: {0}", LCMS2DPlotOptions.OverviewPlotDivisor);
            }
            Console.WriteLine();

            Console.WriteLine("CheckCentroidingStatus:         {0}", TrueFalseToEnabledDisabled(Options.CheckCentroidingStatus));
            Console.WriteLine("Compute Overall Quality Scores: {0}", TrueFalseToEnabledDisabled(Options.ComputeOverallQualityScores));
            Console.WriteLine("Create dataset info XML file:   {0}", TrueFalseToEnabledDisabled(Options.CreateDatasetInfoFile));
            Console.WriteLine("Create scan stats files:        {0}", TrueFalseToEnabledDisabled(Options.CreateScanStatsFile));
            Console.WriteLine("MS2MzMin:                       {0:N0}", Options.MS2MzMin);
            Console.WriteLine("SHA-1 hashing:                  {0}", TrueFalseToEnabledDisabled(!Options.DisableInstrumentHash));
            if (Options.ScanStart > 0 || Options.ScanEnd > 0)
            {
                Console.WriteLine("Start Scan:                     {0}", Options.ScanStart);
                Console.WriteLine("End Scan:                       {0}", Options.ScanEnd);
            }

            Console.WriteLine("Update dataset stats text file: {0}", TrueFalseToEnabledDisabled(Options.UpdateDatasetStatsTextFile));
            if (Options.UpdateDatasetStatsTextFile)
                Console.WriteLine("   Dataset stats file name: {0}", Options.DatasetStatsTextFileName);

            if (Options.CheckFileIntegrity)
            {
                Console.WriteLine();
                Console.WriteLine("Check integrity of all known file types: enabled");
                Console.WriteLine("   Maximum text file lines to check: {0}", Options.MaximumTextFileLinesToCheck);
                Console.WriteLine("   Compute the SHA-1 has of every file: {0}", TrueFalseToEnabledDisabled(Options.ComputeFileHashes));
                Console.WriteLine("   Check data inside .zip files: {0}", TrueFalseToEnabledDisabled(Options.ZipFileCheckAllData));
            }

            Console.WriteLine();
        }

        private string TrueFalseToEnabledDisabled(bool option)
        {
            return option ? "Enabled" : "Disabled";
        }

        public static bool ValidateDataFilePath(ref string filePath, DataFileTypeConstants dataFileType)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(GetAppDirectoryPath(), DefaultDataFileName(dataFileType));
            }

            return ValidateDataFilePathCheckDir(filePath);
        }

        private static bool ValidateDataFilePathCheckDir(string filePath)
        {
            bool validFile;

            try
            {
                var datasetFile = GetFileInfo(filePath);

                if (!datasetFile.Exists)
                {
                    // Make sure the directory exists
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (datasetFile.Directory?.Exists == false)
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
            Thread.Sleep(sleepTimeSeconds * 10);
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
            IEnumerable<FileIntegrityChecker.FileStatsType> fileStats)
        {
            if (writer == null)
                return;

            foreach (var item in fileStats)
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
    }
}
