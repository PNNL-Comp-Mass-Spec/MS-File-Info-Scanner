using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using ExtensionMethods;
using MSFileInfoScannerInterfaces;

// Scans a series of MS data files (or data folders) and extracts the acquisition start and end times, 
// number of spectra, and the total size of the Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
//
// Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar/QTrap .WIFF files, 
// Masslynx .Raw folders, Bruker 1 folders, Bruker XMass analysis.baf files, and .UIMF files (IMS)
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started October 11, 2003

namespace MSFileInfoScanner
{
    public class clsMSFileInfoScanner : iMSFileInfoScanner
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public clsMSFileInfoScanner()
        {

            mFileIntegrityChecker = new clsFileIntegrityChecker();
            mFileIntegrityChecker.ErrorCaught += mFileIntegrityChecker_ErrorCaught;
            mFileIntegrityChecker.FileIntegrityFailure += mFileIntegrityChecker_FileIntegrityFailure;

            mMSFileInfoDataCache = new clsMSFileInfoDataCache();
            mMSFileInfoDataCache.ErrorEvent += mMSFileInfoDataCache_ErrorEvent;
            mMSFileInfoDataCache.StatusEvent += mMSFileInfoDataCache_StatusEvent;

            mErrorCode = eMSFileScannerErrorCodes.NoError;

            mIgnoreErrorsWhenRecursing = false;

            mUseCacheFiles = false;

            mLogMessagesToFile = false;
            mLogFilePath = string.Empty;
            mLogFolderPath = string.Empty;

            mReprocessExistingFiles = false;
            mReprocessIfCachedSizeIsZero = false;
            mRecheckFileIntegrityForExistingFolders = false;

            mCreateDatasetInfoFile = false;

            mUpdateDatasetStatsTextFile = false;
            mDatasetStatsTextFileName = clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME;

            mSaveTICAndBPIPlots = false;
            mSaveLCMS2DPlots = false;
            mCheckCentroidingStatus = false;

            mLCMS2DPlotOptions = new clsLCMSDataPlotterOptions();
            mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;

            mScanStart = 0;
            mScanEnd = 0;
            mShowDebugInfo = false;

            mComputeOverallQualityScores = false;

            mCopyFileLocalOnReadError = false;

            mCheckFileIntegrity = false;

            mDSInfoConnectionString = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
            mDSInfoDBPostingEnabled = false;
            mDSInfoStoredProcedure = "UpdateDatasetFileInfoXML";
            mDSInfoDatasetIDOverride = 0;

            mFileIntegrityDetailsFilePath = Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileTypeConstants.FileIntegrityDetails));
            mFileIntegrityErrorsFilePath = Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileTypeConstants.FileIntegrityErrors));

            mMSFileInfoDataCache.InitializeVariables();

            var oneHourAgo = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0));
            mLastWriteTimeFileIntegrityDetails = oneHourAgo;
            mLastWriteTimeFileIntegrityFailure = oneHourAgo;
            mLastCheckForAbortProcessingFile = oneHourAgo;

        }

        #region "Constants and Enums"

        public const string DEFAULT_ACQUISITION_TIME_FILENAME_TXT = "DatasetTimeFile.txt";

        public const string DEFAULT_ACQUISITION_TIME_FILENAME_XML = "DatasetTimeFile.xml";
        public const string DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_TXT = "FolderIntegrityInfo.txt";

        public const string DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_XML = "FolderIntegrityInfo.xml";
        public const string DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT = "FileIntegrityDetails.txt";

        public const string DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_XML = "FileIntegrityDetails.xml";
        public const string DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT = "FileIntegrityErrors.txt";

        public const string DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_XML = "FileIntegrityErrors.xml";
        public const string ABORT_PROCESSING_FILENAME = "AbortProcessing.txt";

        public const string XML_SECTION_MSFILESCANNER_SETTINGS = "MSFileInfoScannerSettings";
        private const int FILE_MODIFICATION_WINDOW_MINUTES = 60;
        private const int MAX_FILE_READ_ACCESS_ATTEMPTS = 2;
        public const bool USE_XML_OUTPUT_FILE = false;

        private const bool SKIP_FILES_IN_ERROR = true;
        //Public Enum iMSFileInfoScanner.eMSFileScannerErrorCodes
        //	NoError = 0
        //	InvalidInputFilePath = 1
        //	InvalidOutputFolderPath = 2
        //	ParameterFileNotFound = 4
        //	FilePathError = 8

        //	ParameterFileReadError = 16
        //	UnknownFileExtension = 32
        //	InputFileAccessError = 64
        //	InputFileReadError = 128
        //	OutputFileWriteError = 256
        //	FileIntegrityCheckError = 512

        //	DatabasePostingError = 1024

        //	UnspecifiedError = -1
        //End Enum

        //Public Enum iMSFileInfoScanner.eMSFileProcessingStateConstants
        //	NotProcessed = 0
        //	SkippedSinceFoundInCache = 1
        //	FailedProcessing = 2
        //	ProcessedSuccessfully = 3
        //End Enum

        //Public Enum eDataFileTypeConstants
        //	MSFileInfo = 0
        //	FolderIntegrityInfo = 1
        //	FileIntegrityDetails = 2
        //	FileIntegrityErrors = 3
        //End Enum

        //'Private Enum eMSFileTypeConstants
        //'    FinniganRawFile = 0
        //'    BrukerOneFolder = 1
        //'    AgilentIonTrapDFolder = 2
        //'    MicromassRawFolder = 3
        //'    AgilentOrQStarWiffFile = 4
        //'End Enum

        private enum eMessageTypeConstants
        {
            Normal = 0,
            ErrorMsg = 1,
            Warning = 2
        }

        #endregion

        #region "Classwide Variables"

        private eMSFileScannerErrorCodes mErrorCode;

        private bool mAbortProcessing;

        // If the following is false, data is not loaded/saved from/to the DatasetTimeFile.txt or the FolderIntegrityInfo.txt file
        private bool mUseCacheFiles;

        private string mFileIntegrityDetailsFilePath;

        private string mFileIntegrityErrorsFilePath;

        private bool mIgnoreErrorsWhenRecursing;
        private bool mReprocessExistingFiles;

        private bool mReprocessIfCachedSizeIsZero;

        private bool mRecheckFileIntegrityForExistingFolders;
        private bool mSaveTICAndBPIPlots;
        private bool mSaveLCMS2DPlots;

        private bool mCheckCentroidingStatus;
        private bool mComputeOverallQualityScores;
        private bool mCreateDatasetInfoFile;

        private bool mCreateScanStatsFile;
        private bool mUpdateDatasetStatsTextFile;

        private string mDatasetStatsTextFileName;

        private bool mCopyFileLocalOnReadError;

        private bool mCheckFileIntegrity;
        private string mDSInfoConnectionString;
        private bool mDSInfoDBPostingEnabled;
        private string mDSInfoStoredProcedure;

        private int mDSInfoDatasetIDOverride;
        private readonly clsLCMSDataPlotterOptions mLCMS2DPlotOptions;

        private int mLCMS2DOverviewPlotDivisor;
        private int mScanStart;
        private int mScanEnd;

        private bool mShowDebugInfo;
        private bool mLogMessagesToFile;
        private string mLogFilePath;

        private StreamWriter mLogFile;

        // This variable is updated in ProcessMSFileOrFolder
        private string mOutputFolderPath;

        // If blank, then mOutputFolderPath will be used; if mOutputFolderPath is also blank, then the log is created in the same folder as the executing assembly
        private string mLogFolderPath;

        private string mDatasetInfoXML = "";
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

        public override bool AbortProcessing {
            get { return mAbortProcessing; }
            set { mAbortProcessing = value; }
        }

        public override string AcquisitionTimeFilename {
            get { return GetDataFileFilename(eDataFileTypeConstants.MSFileInfo); }
            set { SetDataFileFilename(value, eDataFileTypeConstants.MSFileInfo); }
        }

        public override bool CheckCentroidingStatus {
            get { return mCheckCentroidingStatus; }
            set { mCheckCentroidingStatus = value; }
        }

        /// <summary>
        /// When true, then checks the integrity of every file in every folder processed
        /// </summary>
        public override bool CheckFileIntegrity {
            get { return mCheckFileIntegrity; }
            set {
                mCheckFileIntegrity = value;
                if (mCheckFileIntegrity) {
                    // Make sure Cache Files are enabled
                    UseCacheFiles = true;
                }
            }
        }

        public override bool ComputeOverallQualityScores {
            get { return mComputeOverallQualityScores; }
            set { mComputeOverallQualityScores = value; }
        }

        /// <summary>
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public override string DatasetInfoXML {
            get { return mDatasetInfoXML; }
        }

        public override string GetDataFileFilename(eDataFileTypeConstants eDataFileType)
        {
            switch (eDataFileType) {
                case eDataFileTypeConstants.MSFileInfo:
                    return mMSFileInfoDataCache.AcquisitionTimeFilePath;
                case eDataFileTypeConstants.FolderIntegrityInfo:
                    return mMSFileInfoDataCache.FolderIntegrityInfoFilePath;
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
            switch (eDataFileType) {
                case eDataFileTypeConstants.MSFileInfo:
                    mMSFileInfoDataCache.AcquisitionTimeFilePath = filePath;
                    break;
                case eDataFileTypeConstants.FolderIntegrityInfo:
                    mMSFileInfoDataCache.FolderIntegrityInfoFilePath = filePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    mFileIntegrityDetailsFilePath = filePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    mFileIntegrityErrorsFilePath = filePath;
                    break;
                default:
                    // Unknown file type
                    throw new ArgumentOutOfRangeException("eDataFileType");                
            }
        }

        public static string DefaultAcquisitionTimeFilename {
            get { return DefaultDataFileName(eDataFileTypeConstants.MSFileInfo); }
        }

        public static string DefaultDataFileName(eDataFileTypeConstants eDataFileType)
        {
            if (USE_XML_OUTPUT_FILE) {
                switch (eDataFileType) {
                    case eDataFileTypeConstants.MSFileInfo:
                        return DEFAULT_ACQUISITION_TIME_FILENAME_XML;
                    case eDataFileTypeConstants.FolderIntegrityInfo:
                        return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_XML;
                    case eDataFileTypeConstants.FileIntegrityDetails:
                        return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_XML;
                    case eDataFileTypeConstants.FileIntegrityErrors:
                        return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_XML;
                    default:
                        return "UnknownFileType.xml";
                }
            }
        
            switch (eDataFileType) {
                case eDataFileTypeConstants.MSFileInfo:
                    return DEFAULT_ACQUISITION_TIME_FILENAME_TXT;
                case eDataFileTypeConstants.FolderIntegrityInfo:
                    return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_TXT;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT;
                default:
                    return "UnknownFileType.txt";
            }
        }

        /// <summary>
        /// When True, then computes an Sha1 hash on every file
        /// </summary>
        public override bool ComputeFileHashes {
            get {
                if ((mFileIntegrityChecker != null)) {
                    return mFileIntegrityChecker.ComputeFileHashes;
                } else {
                    return false;
                }
            }
            set {
                if ((mFileIntegrityChecker != null)) {
                    mFileIntegrityChecker.ComputeFileHashes = value;
                }
            }
        }

        /// <summary>
        /// If True, then copies .Raw files to the local drive if unable to read the file over the network
        /// </summary>
        public override bool CopyFileLocalOnReadError {
            get { return mCopyFileLocalOnReadError; }
            set { mCopyFileLocalOnReadError = value; }
        }

        /// <summary>
        /// If True, then will create the _DatasetInfo.xml file
        /// </summary>   
        public override bool CreateDatasetInfoFile {
            get { return mCreateDatasetInfoFile; }
            set { mCreateDatasetInfoFile = value; }
        }

        /// <summary>
        /// If True, then will create the _ScanStats.txt file
        /// </summary>
        public override bool CreateScanStatsFile {
            get { return mCreateScanStatsFile; }
            set { mCreateScanStatsFile = value; }
        }

        /// <summary>
        /// DatasetID value to use instead of trying to lookup the value in DMS (and instead of using 0)
        /// </summary>
        public override int DatasetIDOverride {
            get { return mDSInfoDatasetIDOverride; }
            set { mDSInfoDatasetIDOverride = value; }
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

        public override string DSInfoConnectionString {
            get { return mDSInfoConnectionString; }
            set { mDSInfoConnectionString = value; }
        }

        public override bool DSInfoDBPostingEnabled {
            get { return mDSInfoDBPostingEnabled; }
            set { mDSInfoDBPostingEnabled = value; }
        }

        public override string DSInfoStoredProcedure {
            get { return mDSInfoStoredProcedure; }
            set { mDSInfoStoredProcedure = value; }
        }

        public override eMSFileScannerErrorCodes ErrorCode {
            get { return mErrorCode; }
        }

        public override bool IgnoreErrorsWhenRecursing {
            get { return mIgnoreErrorsWhenRecursing; }
            set { mIgnoreErrorsWhenRecursing = value; }
        }

        public override float LCMS2DPlotMZResolution {
            get { return mLCMS2DPlotOptions.MZResolution; }
            set { mLCMS2DPlotOptions.MZResolution = value; }
        }

        public override int LCMS2DPlotMaxPointsToPlot {
            get { return mLCMS2DPlotOptions.MaxPointsToPlot; }
            set { mLCMS2DPlotOptions.MaxPointsToPlot = value; }
        }

        public override int LCMS2DOverviewPlotDivisor {
            get { return mLCMS2DOverviewPlotDivisor; }
            set { mLCMS2DOverviewPlotDivisor = value; }
        }

        public override int LCMS2DPlotMinPointsPerSpectrum {
            get { return mLCMS2DPlotOptions.MinPointsPerSpectrum; }
            set { mLCMS2DPlotOptions.MinPointsPerSpectrum = value; }
        }

        public override float LCMS2DPlotMinIntensity {
            get { return mLCMS2DPlotOptions.MinIntensity; }
            set { mLCMS2DPlotOptions.MinIntensity = value; }
        }


        public override bool LogMessagesToFile {
            get { return mLogMessagesToFile; }
            set { mLogMessagesToFile = value; }
        }

        public override string LogFilePath {
            get { return mLogFilePath; }
            set { mLogFilePath = value; }
        }

        public override string LogFolderPath {
            get { return mLogFolderPath; }
            set { mLogFolderPath = value; }
        }

        public override int MaximumTextFileLinesToCheck {
            get {
                if ((mFileIntegrityChecker != null)) {
                    return mFileIntegrityChecker.MaximumTextFileLinesToCheck;
                } else {
                    return 0;
                }
            }
            set {
                if ((mFileIntegrityChecker != null)) {
                    mFileIntegrityChecker.MaximumTextFileLinesToCheck = value;
                }
            }
        }

        public override int MaximumXMLElementNodesToCheck {
            get {
                if ((mFileIntegrityChecker != null)) {
                    return mFileIntegrityChecker.MaximumTextFileLinesToCheck;
                } else {
                    return 0;
                }
            }
            set {
                if ((mFileIntegrityChecker != null)) {
                    mFileIntegrityChecker.MaximumTextFileLinesToCheck = value;
                }
            }
        }

        public override bool RecheckFileIntegrityForExistingFolders {
            get { return mRecheckFileIntegrityForExistingFolders; }
            set { mRecheckFileIntegrityForExistingFolders = value; }
        }

        public override bool ReprocessExistingFiles {
            get { return mReprocessExistingFiles; }
            set { mReprocessExistingFiles = value; }
        }

        public override bool ReprocessIfCachedSizeIsZero {
            get { return mReprocessIfCachedSizeIsZero; }
            set { mReprocessIfCachedSizeIsZero = value; }
        }

        /// <summary>
        /// If True, then saves TIC and BPI plots as PNG files
        /// </summary>
        public override bool SaveTICAndBPIPlots {
            get { return mSaveTICAndBPIPlots; }
            set { mSaveTICAndBPIPlots = value; }
        }

        public override bool ShowDebugInfo {
            get { return mShowDebugInfo; }
            set { mShowDebugInfo = value; }
        }

        /// <summary>
        /// If True, then saves a 2D plot of m/z vs. Intensity (requires reading every data point in the data file, which will slow down the processing)
        /// </summary>
        /// <value></value>
        public override bool SaveLCMS2DPlots {
            get { return mSaveLCMS2DPlots; }
            set { mSaveLCMS2DPlots = value; }
        }

        /// <summary>
        /// When ScanStart is > 0, then will start processing at the specified scan number
        /// </summary>
        public override int ScanStart {
            get { return mScanStart; }
            set { mScanStart = value; }
        }

        /// <summary>
        /// When ScanEnd is > 0, then will stop processing at the specified scan number
        /// </summary>
        public override int ScanEnd {
            get { return mScanEnd; }
            set { mScanEnd = value; }
        }

        /// <summary>
        /// Set to True to print out a series of 2D plots, each using a different color scheme
        /// </summary>
        public bool TestLCMSGradientColorSchemes {
            get { return mLCMS2DPlotOptions.TestGradientColorSchemes; }
            set { mLCMS2DPlotOptions.TestGradientColorSchemes = value; }
        }

        public override bool UpdateDatasetStatsTextFile {
            get { return mUpdateDatasetStatsTextFile; }
            set { mUpdateDatasetStatsTextFile = value; }
        }

        /// <summary>
        /// If True, then saves/loads data from/to the cache files (DatasetTimeFile.txt and FolderIntegrityInfo.txt)
        /// If you simply want to create TIC and BPI files, and/or the _DatasetInfo.xml file for a single dataset, then set this to False
        /// </summary>
        public override bool UseCacheFiles {
            get { return mUseCacheFiles; }
            set { mUseCacheFiles = value; }
        }

        public override bool ZipFileCheckAllData {
            get {
                if ((mFileIntegrityChecker != null)) {
                    return mFileIntegrityChecker.ZipFileCheckAllData;
                } else {
                    return false;
                }
            }
            set {
                if ((mFileIntegrityChecker != null)) {
                    mFileIntegrityChecker.ZipFileCheckAllData = value;
                }
            }
        }

        #endregion

        private void AutosaveCachedResults()
        {
            if (mUseCacheFiles) {
                mMSFileInfoDataCache.AutosaveCachedResults();
            }

        }

        private void CheckForAbortProcessingFile()
        {

            try {
                if (DateTime.UtcNow.Subtract(mLastCheckForAbortProcessingFile).TotalSeconds < 15) {
                    return;
                }

                mLastCheckForAbortProcessingFile = DateTime.UtcNow;

                if (File.Exists(ABORT_PROCESSING_FILENAME)) {
                    mAbortProcessing = true;
                    try {
                        if (File.Exists(ABORT_PROCESSING_FILENAME + ".done")) {
                            File.Delete(ABORT_PROCESSING_FILENAME + ".done");
                        }
                        File.Move(ABORT_PROCESSING_FILENAME, ABORT_PROCESSING_FILENAME + ".done");
                    } catch (Exception) {
                        // Ignore errors here
                    }
                }
            } catch (Exception) {
                // Ignore errors here
            }
        }


        private void CheckIntegrityOfFilesInFolder(string folderPath, bool forceRecheck, List<string> processedFileList)
        {
            var folderID = 0;

            try {
                if (mFileIntegrityDetailsWriter == null) {
                    OpenFileIntegrityDetailsFile();
                }

                var diFolderInfo = new DirectoryInfo(folderPath);
                var fileCount = diFolderInfo.GetFiles().Length;

                if (fileCount <= 0)
                {
                    return;
                }

                var checkFolder = true;
                if (mUseCacheFiles && !forceRecheck)
                {
                    DataRow objRow;
                    if (mMSFileInfoDataCache.CachedFolderIntegrityInfoContainsFolder(diFolderInfo.FullName, out folderID, out objRow))
                    {
                        var cachedFileCount = (int)objRow[clsMSFileInfoDataCache.COL_NAME_FILE_COUNT];
                        var cachedCountFailIntegrity = (int)objRow[clsMSFileInfoDataCache.COL_NAME_COUNT_FAIL_INTEGRITY];

                        if (cachedFileCount == fileCount && cachedCountFailIntegrity == 0) {
                            // Folder contains the same number of files as last time, and no files failed the integrity check last time
                            // Do not recheck the folder
                            checkFolder = false;
                        }
                    }
                }

                if (!checkFolder)
                {
                    return;
                }

                clsFileIntegrityChecker.udtFolderStatsType udtFolderStats;
                List<clsFileIntegrityChecker.udtFileStatsType> udtFileStats;

                mFileIntegrityChecker.CheckIntegrityOfFilesInFolder(folderPath, out udtFolderStats, out udtFileStats, processedFileList);

                if (mUseCacheFiles) {
                    if (!mMSFileInfoDataCache.UpdateCachedFolderIntegrityInfo(udtFolderStats, out folderID)) {
                        folderID = -1;
                    }
                }

                WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, folderID, udtFileStats);
            } catch (Exception ex) {
                HandleException("Error calling mFileIntegrityChecker", ex);
            }

        }

        public static string GetAppFolderPath()
        {
            // Could use Application.StartupPath, but .GetExecutingAssembly is better
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public override string[] GetKnownFileExtensions()
        {
            return GetKnownFileExtensionsList().ToArray();
        }

        public List<string> GetKnownFileExtensionsList()
        {
            var lstExtensionsToParse = new List<string>
            {
                clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION.ToUpper(),
                clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION.ToUpper(),
                clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION.ToUpper(),
                clsUIMFInfoScanner.UIMF_FILE_EXTENSION.ToUpper(),
                clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION.ToUpper()
            };


            return lstExtensionsToParse;
        }

        public override string[] GetKnownFolderExtensions()
        {
            return GetKnownFolderExtensionsList().ToArray();
        }

        public List<string> GetKnownFolderExtensionsList()
        {
            var lstExtensionsToParse = new List<string>
            {
                clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION.ToUpper(),
                clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION.ToUpper()
            };

            return lstExtensionsToParse;
        }


        public override string GetErrorMessage()
        {
            // Returns String.Empty if no error

            string errorMessage;

            switch (mErrorCode) {
                case eMSFileScannerErrorCodes.NoError:
                    errorMessage = string.Empty;
                    break;
                case eMSFileScannerErrorCodes.InvalidInputFilePath:
                    errorMessage = "Invalid input file path";
                    break;
                case eMSFileScannerErrorCodes.InvalidOutputFolderPath:
                    errorMessage = "Invalid output folder path";
                    break;
                case eMSFileScannerErrorCodes.ParameterFileNotFound:
                    errorMessage = "Parameter file not found";
                    break;
                case eMSFileScannerErrorCodes.FilePathError:
                    errorMessage = "General file path error";

                    break;
                case eMSFileScannerErrorCodes.ParameterFileReadError:
                    errorMessage = "Parameter file read error";
                    break;
                case eMSFileScannerErrorCodes.UnknownFileExtension:
                    errorMessage = "Unknown file extension";
                    break;
                case eMSFileScannerErrorCodes.InputFileReadError:
                    errorMessage = "Input file read error";
                    break;
                case eMSFileScannerErrorCodes.InputFileAccessError:
                    errorMessage = "Input file access error";
                    break;
                case eMSFileScannerErrorCodes.OutputFileWriteError:
                    errorMessage = "Error writing output file";
                    break;
                case eMSFileScannerErrorCodes.FileIntegrityCheckError:
                    errorMessage = "Error checking file integrity";
                    break;
                case eMSFileScannerErrorCodes.DatabasePostingError:
                    errorMessage = "Database posting error";

                    break;
                case eMSFileScannerErrorCodes.UnspecifiedError:
                    errorMessage = "Unspecified localized error";

                    break;
                default:
                    // This shouldn't happen
                    errorMessage = "Unknown error state";
                    break;
            }

            return errorMessage;

        }

        private bool GetFileOrFolderInfo(string fileOrFolderPath, out bool isFolder, out FileSystemInfo fileOrFolderInfo)
        {

            isFolder = false;

            // See if fileOrFolderPath points to a valid file
            var fiFileInfo = new FileInfo(fileOrFolderPath);

            if (fiFileInfo.Exists) {
                fileOrFolderInfo = fiFileInfo;
                return true;
            }

            // See if fileOrFolderPath points to a folder
            var diFolderInfo = new DirectoryInfo(fileOrFolderPath);
            if (diFolderInfo.Exists) {
                fileOrFolderInfo = diFolderInfo;
                isFolder = true;
                return true;
            }                       

            fileOrFolderInfo = new FileInfo(fileOrFolderPath);
            return false;
        }

        private void HandleException(string baseMessage, Exception ex)
        {
            if (string.IsNullOrEmpty(baseMessage)) {
                baseMessage = "Error";
            }

            // Note that ReportError() will call LogMessage()
            ReportError(baseMessage, ex);

        }

        private void LoadCachedResults(bool forceLoad)
        {
            if (mUseCacheFiles) {
                mMSFileInfoDataCache.LoadCachedResults(forceLoad);
            }
        }

        private void LogMessage(string message, eMessageTypeConstants eMessageType = eMessageTypeConstants.Normal)
        {
            // Note that ProcessMSFileOrFolder() will update mOutputFolderPath, which is used here if mLogFolderPath is blank

            if (mLogFile == null && mLogMessagesToFile) {
                try {
                    if (string.IsNullOrEmpty(mLogFilePath)) {
                        // Auto-name the log file
                        mLogFilePath = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        mLogFilePath += "_log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                    }

                    try {
                        if (mLogFolderPath == null)
                            mLogFolderPath = string.Empty;

                        if (mLogFolderPath.Length == 0) {
                            // Log folder is undefined; use mOutputFolderPath if it is defined
                            if (!string.IsNullOrEmpty(mOutputFolderPath)) {
                                mLogFolderPath = string.Copy(mOutputFolderPath);
                            }
                        }

                        if (mLogFolderPath.Length > 0) {
                            // Create the log folder if it doesn't exist
                            if (!Directory.Exists(mLogFolderPath)) {
                                Directory.CreateDirectory(mLogFolderPath);
                            }
                        }
                    } catch (Exception) {
                        mLogFolderPath = string.Empty;
                    }

                    if (mLogFolderPath.Length > 0) {
                        mLogFilePath = Path.Combine(mLogFolderPath, mLogFilePath);
                    }

                    var openingExistingFile = File.Exists(mLogFilePath);

                    mLogFile = new StreamWriter(new FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        {
                            AutoFlush = true
                        };

                    if (!openingExistingFile) {
                        mLogFile.WriteLine("Date" + '\t' + "Type" + '\t' + "Message");
                    }

                } catch (Exception) {
                    // Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                    mLogMessagesToFile = false;
                }

            }

            if (mLogFile != null) {
                string messageType;
                switch (eMessageType) {
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

            var objSettingsFile = new XmlSettingsFileAccessor();


            try {
                if (string.IsNullOrEmpty(parameterFilePath)) {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(parameterFilePath)) {
                    // See if parameterFilePath points to a file in the same directory as the application
                    parameterFilePath = Path.Combine(GetAppFolderPath(), Path.GetFileName(parameterFilePath));
                    if (!File.Exists(parameterFilePath)) {
                        ReportError("Parameter file not found: " + parameterFilePath);
                        SetErrorCode(eMSFileScannerErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                // Pass False to .LoadSettings() here to turn off case sensitive matching
                if (objSettingsFile.LoadSettings(parameterFilePath, false)) {

                    if (!objSettingsFile.SectionPresent(XML_SECTION_MSFILESCANNER_SETTINGS)) {
                        // MS File Scanner section not found; that's ok
                        ReportWarning("Parameter file " + parameterFilePath + " does not have section \"" + XML_SECTION_MSFILESCANNER_SETTINGS + "\"");
                    } else {
                        DSInfoConnectionString = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoConnectionString", DSInfoConnectionString);
                        DSInfoDBPostingEnabled = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoDBPostingEnabled", DSInfoDBPostingEnabled);
                        DSInfoStoredProcedure = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoStoredProcedure", DSInfoStoredProcedure);

                        LogMessagesToFile = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogMessagesToFile", LogMessagesToFile);
                        LogFilePath = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFilePath", LogFilePath);
                        LogFolderPath = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFolderPath", LogFolderPath);

                        UseCacheFiles = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UseCacheFiles", UseCacheFiles);
                        ReprocessExistingFiles = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessExistingFiles", ReprocessExistingFiles);
                        ReprocessIfCachedSizeIsZero = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessIfCachedSizeIsZero", ReprocessIfCachedSizeIsZero);

                        CopyFileLocalOnReadError = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CopyFileLocalOnReadError", CopyFileLocalOnReadError);

                        SaveTICAndBPIPlots = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveTICAndBPIPlots", SaveTICAndBPIPlots);
                        SaveLCMS2DPlots = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveLCMS2DPlots", SaveLCMS2DPlots);
                        CheckCentroidingStatus = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckCentroidingStatus", CheckCentroidingStatus);

                        LCMS2DPlotMZResolution = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMZResolution", LCMS2DPlotMZResolution);
                        LCMS2DPlotMinPointsPerSpectrum = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinPointsPerSpectrum", LCMS2DPlotMinPointsPerSpectrum);

                        LCMS2DPlotMaxPointsToPlot = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMaxPointsToPlot", LCMS2DPlotMaxPointsToPlot);
                        LCMS2DPlotMinIntensity = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinIntensity", LCMS2DPlotMinIntensity);

                        LCMS2DOverviewPlotDivisor = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DOverviewPlotDivisor", LCMS2DOverviewPlotDivisor);

                        ScanStart = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanStart", ScanStart);
                        ScanEnd = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanEnd", ScanEnd);

                        ComputeOverallQualityScores = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeOverallQualityScores", ComputeOverallQualityScores);
                        CreateDatasetInfoFile = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateDatasetInfoFile", CreateDatasetInfoFile);
                        CreateScanStatsFile = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateScanStatsFile", CreateScanStatsFile);

                        UpdateDatasetStatsTextFile = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UpdateDatasetStatsTextFile", UpdateDatasetStatsTextFile);
                        DatasetStatsTextFileName = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DatasetStatsTextFileName", DatasetStatsTextFileName);

                        CheckFileIntegrity = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckFileIntegrity", CheckFileIntegrity);
                        RecheckFileIntegrityForExistingFolders = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "RecheckFileIntegrityForExistingFolders", RecheckFileIntegrityForExistingFolders);

                        MaximumTextFileLinesToCheck = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumTextFileLinesToCheck", MaximumTextFileLinesToCheck);
                        MaximumXMLElementNodesToCheck = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumXMLElementNodesToCheck", MaximumXMLElementNodesToCheck);
                        ComputeFileHashes = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeFileHashes", ComputeFileHashes);
                        ZipFileCheckAllData = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ZipFileCheckAllData", ZipFileCheckAllData);

                        IgnoreErrorsWhenRecursing = objSettingsFile.GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "IgnoreErrorsWhenRecursing", IgnoreErrorsWhenRecursing);

                    }

                } else {
                    ReportError("Error calling objSettingsFile.LoadSettings for " + parameterFilePath);
                    return false;
                }

            } catch (Exception ex) {
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

        private void OpenFileIntegrityOutputFile(eDataFileTypeConstants eDataFileType, ref string strFilePath, ref StreamWriter objStreamWriter)
        {
            var openedExistingFile = false;
            FileStream fsFileStream = null;

            var strDefaultFileName = DefaultDataFileName(eDataFileType);
            ValidateDataFilePath(ref strFilePath, eDataFileType);

            try {
                if (File.Exists(strFilePath)) {
                    openedExistingFile = true;
                }
                fsFileStream = new FileStream(strFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);

            } catch (Exception ex) {
                HandleException("Error opening/creating " + strFilePath + "; will try " + strDefaultFileName, ex);

                try {
                    if (File.Exists(strDefaultFileName)) {
                        openedExistingFile = true;
                    }

                    fsFileStream = new FileStream(strDefaultFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                } catch (Exception ex2) {
                    HandleException("Error opening/creating " + strDefaultFileName, ex2);
                }
            }

            try {
                if ((fsFileStream != null)) {
                    objStreamWriter = new StreamWriter(fsFileStream);

                    if (!openedExistingFile) {
                        objStreamWriter.WriteLine(mMSFileInfoDataCache.ConstructHeaderLine(eDataFileType));
                    }
                }
            } catch (Exception ex) {
                if (fsFileStream == null)
                {
                    HandleException("Error opening/creating the StreamWriter for " + Path.GetFileName(strFilePath), ex);
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
            return PostDatasetInfoToDB(mDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string dsInfoXML)
        {
            return PostDatasetInfoToDB(dsInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure);
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
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string dsInfoXML, string connectionString, string storedProcedureName)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool success;

            try {
                ReportMessage("  Posting DatasetInfo XML to the database");

                // We need to remove the encoding line from dsInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var startIndex = dsInfoXML.IndexOf("?>", StringComparison.Ordinal);
                
                string dsInfoXMLClean;
                if (startIndex > 0)
                {
                    dsInfoXMLClean = dsInfoXML.Substring(startIndex + 2).Trim();
                } else {
                    dsInfoXMLClean = dsInfoXML;
                }

                // Call the stored procedure using connection string connectionString

                if (string.IsNullOrEmpty(connectionString)) {
                    ReportError("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName)) {
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

                var executeSP = new PRISM.DataBase.clsExecuteDatabaseSP(connectionString);

                executeSP.DBErrorEvent += mExecuteSP_DBErrorEvent;

                var result = executeSP.ExecuteSP(command, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK) {
                    // No errors
                    success = true;
                } else {
                    ReportError("Error calling stored procedure, return code = " + result);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }

                // Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
                // Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
                // PostDatasetInfoToDB(32, dsInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

            } catch (Exception ex) {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
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
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="datasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoUseDatasetID(int datasetID, string dsInfoXML, string connectionString, string storedProcedureName)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool success;

            try {
                if (datasetID == 0 && mDSInfoDatasetIDOverride > 0) {
                    datasetID = mDSInfoDatasetIDOverride;
                }

                ReportMessage("  Posting DatasetInfo XML to the database (using Dataset ID " + datasetID + ")");

                // We need to remove the encoding line from strDatasetInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var startIndex = dsInfoXML.IndexOf("?>", StringComparison.Ordinal);
                string dsInfoXMLClean;
                if (startIndex > 0) {
                    dsInfoXMLClean = dsInfoXML.Substring(startIndex + 2).Trim();
                } else {
                    dsInfoXMLClean = dsInfoXML;
                }

                // Call stored procedure strStoredProcedure using connection string strConnectionString

                if (string.IsNullOrEmpty(connectionString)) {
                    ReportError("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(storedProcedureName)) {
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

                var executeSP = new PRISM.DataBase.clsExecuteDatabaseSP(connectionString);

                var result = executeSP.ExecuteSP(command, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (result == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                } else {
                    ReportError("Error calling stored procedure, return code = " + result);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    success = false;
                }

            } catch (Exception ex) {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                success = false;
            }

            return success;
        }

        private bool ProcessMSDataset(string inputFileOrFolderPath, iMSFileInfoProcessor scanner, string datasetName, string outputFolderPath)
        {

            var datasetFileInfo = new clsDatasetFileInfo();
            if (!string.IsNullOrWhiteSpace(datasetName))
                datasetFileInfo.DatasetName = datasetName;

            bool success;

            // Open the MS datafile (or data folder), read the creation date, and update the status file

            var retryCount = 0;
            do {
                // Set the processing options
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, mSaveTICAndBPIPlots);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots, mSaveLCMS2DPlots);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus, mCheckCentroidingStatus);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, mComputeOverallQualityScores);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, mCreateDatasetInfoFile);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile, mCreateScanStatsFile);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError, mCopyFileLocalOnReadError);
                scanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile, mUpdateDatasetStatsTextFile);

                scanner.DatasetStatsTextFileName = mDatasetStatsTextFileName;

                scanner.LCMS2DPlotOptions = mLCMS2DPlotOptions;
                scanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;

                scanner.ScanStart = mScanStart;
                scanner.ScanEnd = mScanEnd;
                scanner.ShowDebugInfo = mShowDebugInfo;

                scanner.DatasetID = mDSInfoDatasetIDOverride;

                // Process the data file
                success = scanner.ProcessDataFile(inputFileOrFolderPath, datasetFileInfo);

                if (!success) {
                    retryCount += 1;

                    if (retryCount < MAX_FILE_READ_ACCESS_ATTEMPTS) {
                        // Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time

                        if (DateTime.Now.Subtract(datasetFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES || DateTime.Now.Subtract(datasetFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES) {
                            // Sleep for 10 seconds then try again
                            SleepNow(10);
                        } else {
                            retryCount = MAX_FILE_READ_ACCESS_ATTEMPTS;
                        }
                    }
                }
            } while (!success & retryCount < MAX_FILE_READ_ACCESS_ATTEMPTS);

            if (!success & retryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS) {
                if (!string.IsNullOrWhiteSpace(datasetFileInfo.DatasetName)) {
                    // Make an entry anyway; probably a corrupted file
                    success = true;
                }
            }


            if (success) {
                success = scanner.CreateOutputFiles(inputFileOrFolderPath, outputFolderPath);
                if (!success) {
                    SetErrorCode(eMSFileScannerErrorCodes.OutputFileWriteError);
                }

                // Cache the Dataset Info XML
                mDatasetInfoXML = scanner.GetDatasetInfoXML();

                if (mUseCacheFiles) {
                    // Update the results database
                    success = mMSFileInfoDataCache.UpdateCachedMSFileInfo(datasetFileInfo);

                    // Possibly auto-save the cached results
                    AutosaveCachedResults();
                }

                if (mDSInfoDBPostingEnabled) {
                    success = PostDatasetInfoToDB(mDatasetInfoXML);
                    if (!success) {
                        SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    }
                } else {
                    success = true;
                }

            } else {
                if (SKIP_FILES_IN_ERROR) {
                    success = true;
                }
            }

            return success;

        }

        // Main processing function
        public override bool ProcessMSFileOrFolder(string inputFileOrFolderPath, string outputFolderPath)
        {

            eMSFileProcessingStateConstants eMSFileProcessingState;

            return ProcessMSFileOrFolder(inputFileOrFolderPath, outputFolderPath, true, out eMSFileProcessingState);
        }

        public override bool ProcessMSFileOrFolder(
            string inputFileOrFolderPath,
            string outputFolderPath, 
            bool resetErrorCode, 
            out eMSFileProcessingStateConstants eMSFileProcessingState)
        {

            // Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
            // See function ProcessMSFilesAndRecurseFolders for more details
            // This function returns True if it processed a file (or the dataset was processed previously)
            // When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
            // If the file type is unknown, or if an error occurs, then it returns false
            // eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

            var success = false;

            if (resetErrorCode) {
                SetErrorCode(eMSFileScannerErrorCodes.NoError);
            }

            eMSFileProcessingState = eMSFileProcessingStateConstants.NotProcessed;

            if (string.IsNullOrEmpty(outputFolderPath))
            {
                // Define outputFolderPath based on the program file path
                outputFolderPath = GetAppFolderPath();
            }

            // Update mOutputFolderPath
            mOutputFolderPath = string.Copy(outputFolderPath);

            mDatasetInfoXML = string.Empty;

            LoadCachedResults(false);

            try {
                if (string.IsNullOrEmpty(inputFileOrFolderPath)) {
                    ReportError("Input file name is empty");
                } else {
                    try {
                        if (Path.GetFileName(inputFileOrFolderPath).Length == 0) {
                            ReportMessage("Parsing " + Path.GetDirectoryName(inputFileOrFolderPath));
                        } else {
                            ReportMessage("Parsing " + Path.GetFileName(inputFileOrFolderPath));
                        }
                    } catch (Exception ex) {
                        HandleException("Error parsing " + inputFileOrFolderPath, ex);
                    }

                    // Determine whether inputFileOrFolderPath points to a file or a folder

                    bool isFolder;
                    FileSystemInfo objFileSystemInfo;
                    if (!GetFileOrFolderInfo(inputFileOrFolderPath, out isFolder, out objFileSystemInfo)) {
                        ReportError("File or folder not found: " + objFileSystemInfo.FullName);
                        if (SKIP_FILES_IN_ERROR) {
                            return true;
                        } else {
                            SetErrorCode(eMSFileScannerErrorCodes.FilePathError);
                            return false;
                        }
                    }

                    var knownMSDataType = false;

                    // Only continue if it's a known type
                    if (isFolder) {
                        if (objFileSystemInfo.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME) {
                            // Bruker 1 folder
                            mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                            knownMSDataType = true;
                        } else {
                            if (inputFileOrFolderPath.EndsWith('\\')) {
                                inputFileOrFolderPath = inputFileOrFolderPath.TrimEnd('\\');
                            }

                            switch (Path.GetExtension(inputFileOrFolderPath).ToUpper()) {
                                case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION:
                                    // Agilent .D folder or Bruker .D folder

                                    if (Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME).Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SER_FILE_NAME).Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_FID_FILE_NAME).Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_idx").Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_xtr").Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME).Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME).Length > 0) 
                                    {
                                        mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();

                                    } else if (Directory.GetFiles(inputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_MS_DATA_FILE).Length > 0 || 
                                        Directory.GetFiles(inputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_ACQ_METHOD_FILE).Length > 0 ||
                                        Directory.GetFiles(inputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_GC_INI_FILE).Length > 0) 
                                    {
                                        mMSInfoScanner = new clsAgilentGCDFolderInfoScanner();

                                    } else if (Directory.GetDirectories(inputFileOrFolderPath, clsAgilentTOFDFolderInfoScanner.AGILENT_ACQDATA_FOLDER_NAME).Length > 0) 
                                    {
                                        mMSInfoScanner = new clsAgilentTOFDFolderInfoScanner();

                                    } else {
                                        mMSInfoScanner = new clsAgilentIonTrapDFolderInfoScanner();
                                    }

                                    knownMSDataType = true;
                                    break;
                                case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION:
                                    // Micromass .Raw folder
                                    mMSInfoScanner = new clsMicromassRawFolderInfoScanner();
                                    knownMSDataType = true;
                                    break;
                                default:
                                    // Unknown folder extension (or no extension)
                                    // See if the folder contains 1 or more 0_R*.zip files
                                    if (Directory.GetFiles(inputFileOrFolderPath, clsZippedImagingFilesScanner.ZIPPED_IMAGING_FILE_SEARCH_SPEC).Length > 0) {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }
                    } else {
                        if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            knownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_ANALYSIS_YEP_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            // If the folder also contains file BRUKER_EXTENSION_BAF_FILE_NAME then this is a Bruker XMass folder
                            var parentFolder = Path.GetDirectoryName(objFileSystemInfo.FullName);
                            if (!string.IsNullOrEmpty(parentFolder))
                            {
                                var pathCheck = Path.Combine(parentFolder, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME);
                                if (File.Exists(pathCheck))
                                {
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    knownMSDataType = true;
                                }
                            }
                        }


                        if (!knownMSDataType) {
                            // Examine the extension on inputFileOrFolderPath
                            switch (objFileSystemInfo.Extension.ToUpper()) {
                                case clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION:
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

                                    if (objFileSystemInfo.FullName.ToUpper().EndsWith(clsDeconToolsIsosInfoScanner.DECONTOOLS_ISOS_FILE_SUFFIX)) {
                                        mMSInfoScanner = new clsDeconToolsIsosInfoScanner();
                                        knownMSDataType = true;
                                    }

                                    break;
                                default:
                                    // Unknown file extension; check for a zipped folder 
                                    if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(objFileSystemInfo.Name)) {
                                        // Bruker s001.zip file
                                        mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                                        knownMSDataType = true;
                                    } else if (clsZippedImagingFilesScanner.IsZippedImagingFile(objFileSystemInfo.Name)) {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        knownMSDataType = true;
                                    }
                                    break;
                            }
                        }

                    }

                    if (!knownMSDataType) {
                        ReportError("Unknown file type: " + Path.GetFileName(inputFileOrFolderPath));
                        SetErrorCode(eMSFileScannerErrorCodes.UnknownFileExtension);
                        return false;
                    }

                    // Attach the events
                    RegisterEvents(mMSInfoScanner);

                    var datasetName = mMSInfoScanner.GetDatasetNameViaPath(objFileSystemInfo.FullName);

                    if (mUseCacheFiles && !mReprocessExistingFiles)
                    {
                        // See if the datasetName in inputFileOrFolderPath is already present in mCachedResults
                        // If it is present, then don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

                        DataRow objRow;
                        if (datasetName.Length > 0 && mMSFileInfoDataCache.CachedMSInfoContainsDataset(datasetName, out objRow)) {
                            if (mReprocessIfCachedSizeIsZero) {
                                long lngCachedSizeBytes;
                                try {
                                    lngCachedSizeBytes = (long)objRow[clsMSFileInfoDataCache.COL_NAME_FILE_SIZE_BYTES];
                                } catch (Exception) {
                                    lngCachedSizeBytes = 1;
                                }

                                if (lngCachedSizeBytes > 0) {
                                    // File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                    ReportMessage("  Skipping " + Path.GetFileName(inputFileOrFolderPath) + " since already in cached results");
                                    eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                    return true;
                                }
                            } else {
                                // File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                                ReportMessage("  Skipping " + Path.GetFileName(inputFileOrFolderPath) + " since already in cached results");
                                eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                return true;
                            }
                        }
                    }

                    // Process the data file or folder
                    success = ProcessMSDataset(inputFileOrFolderPath, mMSInfoScanner, datasetName, outputFolderPath);
                    if (success) {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully;
                    } else {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing;
                    }

                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFileOrFolder", ex);
                success = false;
            } finally {
                mMSInfoScanner = null;
            }

            return success;

        }

        public override bool ProcessMSFileOrFolderWildcard(string inputFileOrFolderPath, string outputFolderPath, bool resetErrorCode)
        {
            // Returns True if success, False if failure

            var processedFileList = new List<string>();

            mAbortProcessing = false;
            var success = true;
            try {
                // Possibly reset the error code
                if (resetErrorCode) {
                    SetErrorCode(eMSFileScannerErrorCodes.NoError);
                }

                // See if inputFilePath contains a wildcard
                eMSFileProcessingStateConstants eMSFileProcessingState;
                if ((inputFileOrFolderPath != null) && (inputFileOrFolderPath.IndexOf('*') >= 0 | inputFileOrFolderPath.IndexOf('?') >= 0)) {
                    // Obtain a list of the matching files and folders

                    // Copy the path into cleanPath and replace any * or ? characters with _
                    var cleanPath = inputFileOrFolderPath.Replace("*", "_").Replace("?", "_");

                    var fiFileInfo = new FileInfo(cleanPath);
                    string inputFolderPath;
                    if (fiFileInfo.Directory != null && fiFileInfo.Directory.Exists) {
                        inputFolderPath = fiFileInfo.DirectoryName;
                    } else {
                        // Use the current working directory
                        inputFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    if (string.IsNullOrEmpty(inputFolderPath))
                        inputFolderPath = ".";

                    var diFolderInfo = new DirectoryInfo(inputFolderPath);

                    // Remove any directory information from inputFileOrFolderPath
                    inputFileOrFolderPath = Path.GetFileName(inputFileOrFolderPath);

                    var matchCount = 0;

                    foreach (var fiFileMatch in diFolderInfo.GetFiles(inputFileOrFolderPath)) {
                        success = ProcessMSFileOrFolder(fiFileMatch.FullName, outputFolderPath, resetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing) {
                            processedFileList.Add(fiFileMatch.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (mAbortProcessing)
                            break;

                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount += 1;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (mAbortProcessing)
                        return false;


                    foreach (var diFolderMatch in diFolderInfo.GetDirectories(inputFileOrFolderPath)) {
                        success = ProcessMSFileOrFolder(diFolderMatch.FullName, outputFolderPath, resetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                        {
                            processedFileList.Add(diFolderMatch.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (mAbortProcessing)
                            break;

                        if (!success && !SKIP_FILES_IN_ERROR)
                            break;

                        matchCount += 1;

                        if (matchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (mAbortProcessing)
                        return false;

                    if (mCheckFileIntegrity) {
                        CheckIntegrityOfFilesInFolder(diFolderInfo.FullName, mRecheckFileIntegrityForExistingFolders, processedFileList);
                    }

                    if (matchCount == 0) {
                        if (mErrorCode == eMSFileScannerErrorCodes.NoError) {
                            ReportWarning("No match was found for the input file path:" + inputFileOrFolderPath);
                        }
                    } else {
                        Console.WriteLine();
                    }
                } else {
                    success = ProcessMSFileOrFolder(inputFileOrFolderPath, outputFolderPath, resetErrorCode, out eMSFileProcessingState);
                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFileOrFolderWildcard", ex);
                success = false;
            } finally {
                if ((mFileIntegrityDetailsWriter != null)) {
                    mFileIntegrityDetailsWriter.Close();
                }
                if ((mFileIntegrityErrorsWriter != null)) {
                    mFileIntegrityErrorsWriter.Close();
                }
            }

            return success;

        }

        /// <summary>
        /// Calls ProcessMSFileOrFolder for all files in inputFilePathOrFolder and below having a known extension
        ///  Known extensions are:
        ///   .Raw for Finnigan files
        ///   .Wiff for Agilent TOF files and for Q-Star files
        ///   .Baf for Bruker XMASS folders (contains file analysis.baf, and hopefully files scan.xml and Log.txt)
        /// For each folder that does not have any files matching a known extension, will then look for special folder names:
        ///   Folders matching *.Raw for Micromass data
        ///   Folders matching *.D for Agilent Ion Trap data
        ///   A folder named 1 for Bruker FTICR-MS data
        /// </summary>
        /// <param name="inputFilePathOrFolder">Path to the input file or folder; can contain a wildcard (* or ?)</param>
        /// <param name="outputFolderPath">Folder to write any results files to</param>
        /// <param name="recurseFoldersMaxLevels">Maximum folder depth to process; Set to 0 to process all folders</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override bool ProcessMSFilesAndRecurseFolders(string inputFilePathOrFolder, string outputFolderPath, int recurseFoldersMaxLevels)
        {
            bool success;

            // Examine strInputFilePathOrFolder to see if it contains a filename; if not, assume it points to a folder
            // First, see if it contains a * or ?
            try {
                string inputFolderPath;
                if ((inputFilePathOrFolder != null) && (inputFilePathOrFolder.IndexOf('*') >= 0 | inputFilePathOrFolder.IndexOf('?') >= 0)) {
                    // Copy the path into strCleanPath and replace any * or ? characters with _
                    var cleanPath = inputFilePathOrFolder.Replace("*", "_").Replace("?", "_");

                    var fiFileInfo = new FileInfo(cleanPath);
                    if (Path.IsPathRooted(cleanPath)) {
                        if (fiFileInfo.Directory != null && !fiFileInfo.Directory.Exists) {
                            ReportError("Folder not found: " + fiFileInfo.DirectoryName);
                            SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                            return false;
                        }
                    }

                    if (fiFileInfo.Directory != null && fiFileInfo.Directory.Exists) {
                        inputFolderPath = fiFileInfo.DirectoryName;
                    } else {
                        // Folder not found; use the current working directory
                        inputFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from strInputFilePath
                    inputFilePathOrFolder = Path.GetFileName(inputFilePathOrFolder);

                } else
                {
                    if (string.IsNullOrEmpty(inputFilePathOrFolder))
                        inputFilePathOrFolder = ".";

                    var diFolderInfo = new DirectoryInfo(inputFilePathOrFolder);
                    if (diFolderInfo.Exists) {
                        inputFolderPath = diFolderInfo.FullName;
                        inputFilePathOrFolder = "*";
                    } else {
                        if (diFolderInfo.Parent != null && diFolderInfo.Parent.Exists) {
                            inputFolderPath = diFolderInfo.Parent.FullName;
                            inputFilePathOrFolder = Path.GetFileName(inputFilePathOrFolder);
                        } else {
                            // Unable to determine the input folder path
                            inputFolderPath = string.Empty;
                        }
                    }
                }


                if (!string.IsNullOrEmpty(inputFolderPath)) {
                    // Initialize some parameters
                    mAbortProcessing = false;
                    var fileProcessCount = 0;
                    var fileProcessFailCount = 0;

                    LoadCachedResults(false);

                    // Call RecurseFoldersWork
                    success = RecurseFoldersWork(inputFolderPath, inputFilePathOrFolder, outputFolderPath, ref fileProcessCount, ref fileProcessFailCount, 1, recurseFoldersMaxLevels);

                } else {
                    SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                    return false;
                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFilesAndRecurseFolders", ex);
                success = false;
            } finally {
                if ((mFileIntegrityDetailsWriter != null)) {
                    mFileIntegrityDetailsWriter.Close();
                }
                if ((mFileIntegrityErrorsWriter != null)) {
                    mFileIntegrityErrorsWriter.Close();
                }
            }

            return success;

        }

        private bool RecurseFoldersWork(
            string inputFolderPath, 
            string fileNameMatch, 
            string outputFolderPath, 
            ref int fileProcessCount, 
            ref int fileProcessFailCount, 
            int recursionLevel, 
            int recurseFoldersMaxLevels)
        {

            const int MAX_ACCESS_ATTEMPTS = 2;

            // If recurseFoldersMaxLevels is <=0 then we recurse infinitely

            DirectoryInfo diInputFolder = null;

            List<string> fileExtensionsToParse;
            List<string> folderExtensionsToParse;
            bool processAllFileExtensions;

            bool success;
            var fileProcessed = false;

            eMSFileProcessingStateConstants eMSFileProcessingState;

            var processedFileList = new List<string>();

            var retryCount = 0;
            do {
                try {
                    diInputFolder = new DirectoryInfo(inputFolderPath);
                    break;
                } catch (Exception ex) {
                    // Input folder path error
                    HandleException("Error populating diInputFolderInfo for " + inputFolderPath, ex);
                    if (!ex.Message.Contains("no longer available")) {
                        return false;
                    }
                }

                retryCount += 1;
                if (retryCount >= MAX_ACCESS_ATTEMPTS) {
                    return false;
                }
                // Wait 1 second, then try again
                SleepNow(1);
            } while (retryCount < MAX_ACCESS_ATTEMPTS);

            if (diInputFolder == null)
            {
                ReportError("Unable to instantiate a directory info object for " + inputFolderPath);
                return false;
            }

            try {
                // Construct and validate the list of file and folder extensions to parse
                fileExtensionsToParse = GetKnownFileExtensionsList();
                folderExtensionsToParse = GetKnownFolderExtensionsList();

                // Validate the extensions, including assuring that they are all capital letters
                processAllFileExtensions = ValidateExtensions(fileExtensionsToParse);
                ValidateExtensions(folderExtensionsToParse);
            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork", ex);
                return false;
            }

            try {
                Console.WriteLine("Examining " + inputFolderPath);

                // Process any matching files in this folder
                success = true;
                var processedZippedSFolder = false;

                foreach (var fiFileMatch in diInputFolder.GetFiles(fileNameMatch)) {
                    retryCount = 0;
                    do {

                        try {
                            fileProcessed = false;
                            foreach (var fileExtension in fileExtensionsToParse) {
                                if (!processAllFileExtensions && fiFileMatch.Extension.ToUpper() != fileExtension)
                                {
                                    continue;
                                }

                                fileProcessed = true;
                                success = ProcessMSFileOrFolder(fiFileMatch.FullName, outputFolderPath, true, out eMSFileProcessingState);

                                if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                {
                                    processedFileList.Add(fiFileMatch.FullName);
                                }

                                // Successfully processed a folder; exit the for loop
                                break;
                            }

                            if (mAbortProcessing)
                                break;

                            if (!fileProcessed && !processedZippedSFolder) {
                                // Check for other valid files
                                if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(fiFileMatch.Name)) {
                                    // Only process this file if there is not a subfolder named "1" present"
                                    if (diInputFolder.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1) {
                                        fileProcessed = true;
                                        processedZippedSFolder = true;
                                        success = ProcessMSFileOrFolder(fiFileMatch.FullName, outputFolderPath, true, out eMSFileProcessingState);

                                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                        {
                                            processedFileList.Add(fiFileMatch.FullName);
                                        }

                                    }
                                }
                            }

                            // Exit the Do loop
                            break;

                        } catch (Exception ex) {
                            // Error parsing file
                            HandleException("Error in RecurseFoldersWork at For Each ioFileMatch in " + inputFolderPath, ex);
                            if (!ex.Message.Contains("no longer available")) {
                                return false;
                            }
                        }

                        if (mAbortProcessing)
                            break;

                        retryCount += 1;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS) {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);

                    } while (retryCount < MAX_ACCESS_ATTEMPTS);

                    if (fileProcessed) {
                        if (success) {
                            fileProcessCount += 1;
                        } else {
                            fileProcessFailCount += 1;
                            success = true;
                        }
                    }

                    CheckForAbortProcessingFile();
                    if (mAbortProcessing)
                        break;
                }

                if (mCheckFileIntegrity & !mAbortProcessing) {
                    CheckIntegrityOfFilesInFolder(diInputFolder.FullName, mRecheckFileIntegrityForExistingFolders, processedFileList);
                }

            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork Examining files in " + inputFolderPath, ex);
                return false;
            }

            if (mAbortProcessing)
            {
                return success;
            }

            // Check the subfolders for those with known extensions

            try {
                var subFoldersProcessed = 0;
                var subFolderNamesProcessed = new SortedSet<string>();

                foreach (var diSubfolder in diInputFolder.GetDirectories(fileNameMatch)) {
                    retryCount = 0;
                    do {
                        try {
                            // Check whether the folder name is BRUKER_ONE_FOLDER = "1"
                            if (diSubfolder.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME) {
                                success = ProcessMSFileOrFolder(diSubfolder.FullName, outputFolderPath, true, out eMSFileProcessingState);
                                if (!success) {
                                    fileProcessFailCount += 1;
                                    success = true;
                                } else {
                                    fileProcessCount += 1;
                                }
                                subFoldersProcessed += 1;
                                subFolderNamesProcessed.Add(diSubfolder.Name);
                            } else {
                                // See if the subfolder has an extension matching strFolderExtensionsToParse()
                                // If it does, process it using ProcessMSFileOrFolder and do not recurse into it
                                foreach (var folderExtension in folderExtensionsToParse) {
                                    if (diSubfolder.Extension.ToUpper() != folderExtension)
                                    {
                                        continue;
                                    }

                                    success = ProcessMSFileOrFolder(diSubfolder.FullName, outputFolderPath, true, out eMSFileProcessingState);
                                    if (!success) {
                                        fileProcessFailCount += 1;
                                        success = true;
                                    } else {
                                        fileProcessCount += 1;
                                    }
                                    subFoldersProcessed += 1;
                                    subFolderNamesProcessed.Add(diSubfolder.Name);

                                    // Successfully processed a folder; exit the for loop
                                    break;
                                }

                            }

                            // Exit the Do loop
                            break;

                        } catch (Exception ex) {
                            // Error parsing folder
                            HandleException("Error in RecurseFoldersWork at For Each diSubfolder(A) in " + inputFolderPath, ex);
                            if (!ex.Message.Contains("no longer available")) {
                                return false;
                            }
                        }

                        if (mAbortProcessing)
                            break;

                        retryCount += 1;
                        if (retryCount >= MAX_ACCESS_ATTEMPTS) {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);
                    } while (retryCount < MAX_ACCESS_ATTEMPTS);

                    if (mAbortProcessing)
                        break;

                }

                // If recurseFoldersMaxLevels is <=0 then we recurse infinitely
                //  otherwise, compare intRecursionLevel to recurseFoldersMaxLevels
                if (recurseFoldersMaxLevels <= 0 || recursionLevel <= recurseFoldersMaxLevels) {
                    // Call this function for each of the subfolders of diInputFolder
                    // However, do not step into folders listed in subFolderNamesProcessed

                    foreach (var diSubfolder in diInputFolder.GetDirectories()) {
                        retryCount = 0;
                        do {
                            try {
                                if (subFoldersProcessed == 0 || !subFolderNamesProcessed.Contains(diSubfolder.Name))
                                {
                                    success = RecurseFoldersWork(diSubfolder.FullName, fileNameMatch, outputFolderPath, ref fileProcessCount, ref fileProcessFailCount, recursionLevel + 1, recurseFoldersMaxLevels);
                                }

                                if (!success & !mIgnoreErrorsWhenRecursing) {
                                    break; 
                                }

                                CheckForAbortProcessingFile();

                                break;

                            } catch (Exception ex) {
                                // Error parsing file
                                HandleException("Error in RecurseFoldersWork at For Each diSubfolder(B) in " + inputFolderPath, ex);
                                if (!ex.Message.Contains("no longer available")) {
                                    return false;
                                }
                            }

                            if (mAbortProcessing)
                                break;


                            retryCount += 1;
                            if (retryCount >= MAX_ACCESS_ATTEMPTS) {
                                return false;
                            } else {
                                // Wait 1 second, then try again
                                SleepNow(1);
                            }

                        } while (retryCount < MAX_ACCESS_ATTEMPTS);

                        if (!success & !mIgnoreErrorsWhenRecursing)
                        {
                            break;
                        }

                        if (mAbortProcessing)
                            break;
                    }
                }


            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork examining subfolders in " + inputFolderPath, ex);
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
            if (mUseCacheFiles) {
                return mMSFileInfoDataCache.SaveCachedResults(clearCachedData);
            } else {
                return true;
            }
        }

        public override bool SaveParameterFileSettings(string parameterFilePath)
        {

            var objSettingsFile = new XmlSettingsFileAccessor();


            try {
                if (string.IsNullOrEmpty(parameterFilePath)) {
                    // No parameter file specified; unable to save
                    return false;
                }

                // Pass True to .LoadSettings() here so that newly made Xml files will have the correct capitalization
                if (objSettingsFile.LoadSettings(parameterFilePath, true)) {

                    // General settings
                    // objSettingsFile.SetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ConnectionString", Me.DatabaseConnectionString)

                    objSettingsFile.SaveSettings();

                }

            } catch (Exception ex) {
                HandleException("Error in SaveParameterFileSettings", ex);
                return false;
            }

            return true;

        }

        private void SetErrorCode(eMSFileScannerErrorCodes eNewErrorCode, bool leaveExistingErrorCodeUnchanged = false)
        {
            if (leaveExistingErrorCodeUnchanged && mErrorCode != eMSFileScannerErrorCodes.NoError) {
                // An error code is already defined; do not change it
            } else {
                mErrorCode = eNewErrorCode;
            }

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
            if (ex == null || errorMessage.EndsWith(ex.Message))
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

            if (allowLogToFile) {
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

        public static bool ValidateDataFilePath(ref string filePath, eDataFileTypeConstants eDataFileType)
        {
            if (string.IsNullOrEmpty(filePath)) {
                filePath = Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileType));
            }

            return ValidateDataFilePathCheckDir(filePath);

        }

        private static bool ValidateDataFilePathCheckDir(string filePath)
        {
            bool validFile;

            try {
                var fiFileInfo = new FileInfo(filePath);

                if (!fiFileInfo.Exists) {
                    // Make sure the folder exists
                    if (fiFileInfo.Directory != null && !fiFileInfo.Directory.Exists) {
                        fiFileInfo.Directory.Create();
                    }
                }
                validFile = true;

            } catch (Exception) {
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
            // Returns True if one of the entries in strExtensions = "*" or ".*"

            var processAllExtensions = false;

            for (var extensionIndex = 0; extensionIndex < extensions.Count; extensionIndex++)
            {
                if (extensions[extensionIndex] == null) {
                    extensions[extensionIndex] = string.Empty;
                } else {
                    if (!extensions[extensionIndex].StartsWith(".")) {
                        extensions[extensionIndex] = "." + extensions[extensionIndex];
                    }

                    if (extensions[extensionIndex] == ".*") {
                        processAllExtensions = true;
                        break;
                    }
                    extensions[extensionIndex] = extensions[extensionIndex].ToUpper();
                }
            }

            return processAllExtensions;
        }

        private void WriteFileIntegrityDetails(
            TextWriter srOutFile, 
            int folderID, 
            IEnumerable<clsFileIntegrityChecker.udtFileStatsType> udtFileStats)
        {

            if (srOutFile == null)
                return;

            foreach (var item in udtFileStats) {
                // Note: HH:mm:ss corresponds to time in 24 hour format
                srOutFile.WriteLine(
                    folderID.ToString() + '\t' + 
                    item.FileName + '\t' + 
                    item.SizeBytes + '\t' + 
                    item.ModificationDate.ToString("yyyy-MM-dd HH:mm:ss") + '\t' + 
                    item.FailIntegrity + '\t' + 
                    item.FileHash + '\t' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            if (DateTime.UtcNow.Subtract(mLastWriteTimeFileIntegrityDetails).TotalMinutes > 1)
            {
                srOutFile.Flush();
                mLastWriteTimeFileIntegrityDetails = DateTime.UtcNow;
            }

        }

        private void WriteFileIntegrityFailure(StreamWriter srOutFile, string filePath, string message)
        {

            if (srOutFile == null)
                return;

            // Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(filePath + '\t' + message + '\t' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            if (DateTime.UtcNow.Subtract(mLastWriteTimeFileIntegrityFailure).TotalMinutes > 1)
            {
                srOutFile.Flush();
                mLastWriteTimeFileIntegrityFailure = DateTime.UtcNow;
            }

        }

        private void mFileIntegrityChecker_ErrorCaught(string message)
        {
            ShowErrorMessage("Error caught in FileIntegrityChecker: " + message);
        }

        private void mFileIntegrityChecker_FileIntegrityFailure(string filePath, string message)
        {
            if (mFileIntegrityErrorsWriter == null) {
                OpenFileIntegrityErrorsFile();
            }

            WriteFileIntegrityFailure(mFileIntegrityErrorsWriter, filePath, message);
        }

        #region "Events"

        public sealed override event MessageEventEventHandler MessageEvent;
        public sealed override event ErrorEventEventHandler ErrorEvent;

        private void mMSInfoScanner_ErrorEvent(string message)
        {
            ShowErrorMessage(message);
        }

        private void mMSInfoScannerMessageEvent(string message)
        {
            ShowMessage(message, eMessageTypeConstants.Normal);
        }

        private void mMSFileInfoDataCache_ErrorEvent(string message)
        {
            ShowErrorMessage(message);
        }

        private void mMSFileInfoDataCache_StatusEvent(string message)
        {
            ShowMessage(message);
        }

        private void mExecuteSP_DBErrorEvent(string message)
        {
            ShowErrorMessage(message);
        }

    }
}
