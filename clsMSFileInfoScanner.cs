using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using ExtensionMethods;
using MSFileInfoScannerInterfaces;

// Scans a series of MS data files (or data folders) and extracts the acquisition start and end times, 
// number of spectra, and the total size of the   Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
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

        public override void SetDataFileFilename(string strFilePath, eDataFileTypeConstants eDataFileType)
        {
            switch (eDataFileType) {
                case eDataFileTypeConstants.MSFileInfo:
                    mMSFileInfoDataCache.AcquisitionTimeFilePath = strFilePath;
                    break;
                case eDataFileTypeConstants.FolderIntegrityInfo:
                    mMSFileInfoDataCache.FolderIntegrityInfoFilePath = strFilePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityDetails:
                    mFileIntegrityDetailsFilePath = strFilePath;
                    break;
                case eDataFileTypeConstants.FileIntegrityErrors:
                    mFileIntegrityErrorsFilePath = strFilePath;
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


        private void CheckIntegrityOfFilesInFolder(string strFolderPath, bool blnForceRecheck, List<string> strProcessedFileList)
        {
            var intFolderID = 0;

            try {
                if (mFileIntegrityDetailsWriter == null) {
                    OpenFileIntegrityDetailsFile();
                }

                var diFolderInfo = new DirectoryInfo(strFolderPath);
                var intFileCount = diFolderInfo.GetFiles().Length;

                if (intFileCount > 0) {
                    var blnCheckFolder = true;
                    if (mUseCacheFiles && !blnForceRecheck)
                    {
                        DataRow objRow;
                        if (mMSFileInfoDataCache.CachedFolderIntegrityInfoContainsFolder(diFolderInfo.FullName, out intFolderID, out objRow))
                        {
                            var intCachedFileCount = (int)objRow[clsMSFileInfoDataCache.COL_NAME_FILE_COUNT];
                            var intCachedCountFailIntegrity = (int)objRow[clsMSFileInfoDataCache.COL_NAME_COUNT_FAIL_INTEGRITY];

                            if (intCachedFileCount == intFileCount && intCachedCountFailIntegrity == 0) {
                                // Folder contains the same number of files as last time, and no files failed the integrity check last time
                                // Do not recheck the folder
                                blnCheckFolder = false;
                            }
                        }
                    }

                    if (blnCheckFolder)
                    {
                        clsFileIntegrityChecker.udtFolderStatsType udtFolderStats;
                        List<clsFileIntegrityChecker.udtFileStatsType> udtFileStats;

                        mFileIntegrityChecker.CheckIntegrityOfFilesInFolder(strFolderPath, out udtFolderStats, out udtFileStats, strProcessedFileList);

                        if (mUseCacheFiles) {
                            if (!mMSFileInfoDataCache.UpdateCachedFolderIntegrityInfo(udtFolderStats, out intFolderID)) {
                                intFolderID = -1;
                            }
                        }

                        WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, intFolderID, udtFileStats);

                    }
                }

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

            string strErrorMessage;

            switch (mErrorCode) {
                case eMSFileScannerErrorCodes.NoError:
                    strErrorMessage = string.Empty;
                    break;
                case eMSFileScannerErrorCodes.InvalidInputFilePath:
                    strErrorMessage = "Invalid input file path";
                    break;
                case eMSFileScannerErrorCodes.InvalidOutputFolderPath:
                    strErrorMessage = "Invalid output folder path";
                    break;
                case eMSFileScannerErrorCodes.ParameterFileNotFound:
                    strErrorMessage = "Parameter file not found";
                    break;
                case eMSFileScannerErrorCodes.FilePathError:
                    strErrorMessage = "General file path error";

                    break;
                case eMSFileScannerErrorCodes.ParameterFileReadError:
                    strErrorMessage = "Parameter file read error";
                    break;
                case eMSFileScannerErrorCodes.UnknownFileExtension:
                    strErrorMessage = "Unknown file extension";
                    break;
                case eMSFileScannerErrorCodes.InputFileReadError:
                    strErrorMessage = "Input file read error";
                    break;
                case eMSFileScannerErrorCodes.InputFileAccessError:
                    strErrorMessage = "Input file access error";
                    break;
                case eMSFileScannerErrorCodes.OutputFileWriteError:
                    strErrorMessage = "Error writing output file";
                    break;
                case eMSFileScannerErrorCodes.FileIntegrityCheckError:
                    strErrorMessage = "Error checking file integrity";
                    break;
                case eMSFileScannerErrorCodes.DatabasePostingError:
                    strErrorMessage = "Database posting error";

                    break;
                case eMSFileScannerErrorCodes.UnspecifiedError:
                    strErrorMessage = "Unspecified localized error";

                    break;
                default:
                    // This shouldn't happen
                    strErrorMessage = "Unknown error state";
                    break;
            }

            return strErrorMessage;

        }

        private bool GetFileOrFolderInfo(string strFileOrFolderPath, ref bool blnIsFolder, ref FileSystemInfo objFileSystemInfo)
        {

            bool blnExists;

            // See if strFileOrFolderPath points to a valid file
            var fiFileInfo = new FileInfo(strFileOrFolderPath);

            if (fiFileInfo.Exists) {
                objFileSystemInfo = fiFileInfo;
                blnExists = true;
                blnIsFolder = false;
            } else {
                // See if strFileOrFolderPath points to a folder
                var diFolderInfo = new DirectoryInfo(strFileOrFolderPath);
                if (diFolderInfo.Exists) {
                    objFileSystemInfo = diFolderInfo;
                    blnExists = true;
                    blnIsFolder = true;
                } else {
                    blnExists = false;
                }
            }

            if (!blnExists) {
                objFileSystemInfo = new FileInfo(strFileOrFolderPath);
            }

            return blnExists;

        }

        private void HandleException(string strBaseMessage, Exception ex)
        {
            if (string.IsNullOrEmpty(strBaseMessage)) {
                strBaseMessage = "Error";
            }

            // Note that ShowErrorMessage() will call LogMessage()
            ShowErrorMessage(strBaseMessage + ": " + ex.Message, true);

        }

        private void LoadCachedResults(bool blnForceLoad)
        {
            if (mUseCacheFiles) {
                mMSFileInfoDataCache.LoadCachedResults(blnForceLoad);
            }
        }

        private void LogMessage(string strMessage)
        {
            LogMessage(strMessage, eMessageTypeConstants.Normal);
        }

        private void LogMessage(string strMessage, eMessageTypeConstants eMessageType)
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

                    var blnOpeningExistingFile = File.Exists(mLogFilePath);

                    mLogFile = new StreamWriter(new FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        {
                            AutoFlush = true
                        };

                    if (!blnOpeningExistingFile) {
                        mLogFile.WriteLine("Date" + '\t' + "Type" + '\t' + "Message");
                    }

                } catch (Exception) {
                    // Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
                    mLogMessagesToFile = false;
                }

            }

            if ((mLogFile != null)) {
                string strMessageType;
                switch (eMessageType) {
                    case eMessageTypeConstants.Normal:
                        strMessageType = "Normal";
                        break;
                    case eMessageTypeConstants.ErrorMsg:
                        strMessageType = "Error";
                        break;
                    case eMessageTypeConstants.Warning:
                        strMessageType = "Warning";
                        break;
                    default:
                        strMessageType = "Unknown";
                        break;
                }

                mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") + '\t' + strMessageType + '\t' + strMessage);
            }

        }

        public override bool LoadParameterFileSettings(string strParameterFilePath)
        {

            var objSettingsFile = new XmlSettingsFileAccessor();


            try {
                if (string.IsNullOrEmpty(strParameterFilePath)) {
                    // No parameter file specified; nothing to load
                    return true;
                }

                if (!File.Exists(strParameterFilePath)) {
                    // See if strParameterFilePath points to a file in the same directory as the application
                    strParameterFilePath = Path.Combine(GetAppFolderPath(), Path.GetFileName(strParameterFilePath));
                    if (!File.Exists(strParameterFilePath)) {
                        ShowErrorMessage("Parameter file not found: " + strParameterFilePath);
                        SetErrorCode(eMSFileScannerErrorCodes.ParameterFileNotFound);
                        return false;
                    }
                }

                // Pass False to .LoadSettings() here to turn off case sensitive matching
                if (objSettingsFile.LoadSettings(strParameterFilePath, false)) {

                    if (!objSettingsFile.SectionPresent(XML_SECTION_MSFILESCANNER_SETTINGS)) {
                        // MS File Scanner section not found; that's ok
                        ShowMessage("Warning: Parameter file " + strParameterFilePath + " does not have section \"" + XML_SECTION_MSFILESCANNER_SETTINGS + "\"", eMessageTypeConstants.Warning);
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
                    ShowErrorMessage("Error calling objSettingsFile.LoadSettings for " + strParameterFilePath);
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
            var blnOpenedExistingFile = false;
            FileStream fsFileStream = null;

            var strDefaultFileName = DefaultDataFileName(eDataFileType);
            ValidateDataFilePath(ref strFilePath, eDataFileType);

            try {
                if (File.Exists(strFilePath)) {
                    blnOpenedExistingFile = true;
                }
                fsFileStream = new FileStream(strFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);

            } catch (Exception ex) {
                HandleException("Error opening/creating " + strFilePath + "; will try " + strDefaultFileName, ex);

                try {
                    if (File.Exists(strDefaultFileName)) {
                        blnOpenedExistingFile = true;
                    }

                    fsFileStream = new FileStream(strDefaultFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                } catch (Exception ex2) {
                    HandleException("Error opening/creating " + strDefaultFileName, ex2);
                }
            }

            try {
                if ((fsFileStream != null)) {
                    objStreamWriter = new StreamWriter(fsFileStream);

                    if (!blnOpenedExistingFile) {
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
        /// <param name="strDatasetInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string strDatasetInfoXML)
        {
            return PostDatasetInfoToDB(strDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure);
        }

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="strConnectionString">Database connection string</param>
        /// <param name="strStoredProcedure">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string strConnectionString, string strStoredProcedure)
        {
            return PostDatasetInfoToDB(DatasetInfoXML, strConnectionString, strStoredProcedure);
        }

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="strDatasetInfoXML">Database info XML</param>
        /// <param name="strConnectionString">Database connection string</param>
        /// <param name="strStoredProcedure">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoToDB(string strDatasetInfoXML, string strConnectionString, string strStoredProcedure)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool blnSuccess;

            try {
                ShowMessage("  Posting DatasetInfo XML to the database");

                // We need to remove the encoding line from strDatasetInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var intStartIndex = strDatasetInfoXML.IndexOf("?>", StringComparison.Ordinal);
                string strDSInfoXMLClean;
                if (intStartIndex > 0) {
                    strDSInfoXMLClean = strDatasetInfoXML.Substring(intStartIndex + 2).Trim();
                } else {
                    strDSInfoXMLClean = strDatasetInfoXML;
                }

                // Call stored procedure strStoredProcedure using connection string strConnectionString

                if (string.IsNullOrEmpty(strConnectionString)) {
                    ShowErrorMessage("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(strStoredProcedure)) {
                    strStoredProcedure = "UpdateDatasetFileInfoXML";
                }

                var objCommand = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = strStoredProcedure
                };

                objCommand.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
                objCommand.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

                objCommand.Parameters.Add(new SqlParameter("@DatasetInfoXML", SqlDbType.Xml));
                objCommand.Parameters["@DatasetInfoXML"].Direction = ParameterDirection.Input;
                objCommand.Parameters["@DatasetInfoXML"].Value = strDSInfoXMLClean;

                var executeSP = new PRISM.DataBase.clsExecuteDatabaseSP(strConnectionString);

                executeSP.DBErrorEvent += mExecuteSP_DBErrorEvent;

                var intResult = executeSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (intResult == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK) {
                    // No errors
                    blnSuccess = true;
                } else {
                    ShowErrorMessage("Error calling stored procedure, return code = " + intResult);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    blnSuccess = false;
                }

                // Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
                // Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
                // PostDatasetInfoToDB(32, strDatasetInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

            } catch (Exception ex) {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                blnSuccess = false;
            }

            return blnSuccess;
        }

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="intDatasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="strConnectionString">Database connection string</param>
        /// <param name="strStoredProcedure">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoUseDatasetID(int intDatasetID, string strConnectionString, string strStoredProcedure)
        {

            return PostDatasetInfoUseDatasetID(intDatasetID, DatasetInfoXML, strConnectionString, strStoredProcedure);
        }


        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="intDatasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="strDatasetInfoXML">Database info XML</param>
        /// <param name="strConnectionString">Database connection string</param>
        /// <param name="strStoredProcedure">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public override bool PostDatasetInfoUseDatasetID(int intDatasetID, string strDatasetInfoXML, string strConnectionString, string strStoredProcedure)
        {

            const int MAX_RETRY_COUNT = 3;
            const int SEC_BETWEEN_RETRIES = 20;

            bool blnSuccess;

            try {
                if (intDatasetID == 0 && mDSInfoDatasetIDOverride > 0) {
                    intDatasetID = mDSInfoDatasetIDOverride;
                }

                ShowMessage("  Posting DatasetInfo XML to the database (using Dataset ID " + intDatasetID + ")");

                // We need to remove the encoding line from strDatasetInfoXML before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

                var intStartIndex = strDatasetInfoXML.IndexOf("?>", StringComparison.Ordinal);
                string strDSInfoXMLClean;
                if (intStartIndex > 0) {
                    strDSInfoXMLClean = strDatasetInfoXML.Substring(intStartIndex + 2).Trim();
                } else {
                    strDSInfoXMLClean = strDatasetInfoXML;
                }

                // Call stored procedure strStoredProcedure using connection string strConnectionString

                if (string.IsNullOrEmpty(strConnectionString)) {
                    ShowErrorMessage("Connection string not defined; unable to post the dataset info to the database");
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    return false;
                }

                if (string.IsNullOrEmpty(strStoredProcedure)) {
                    strStoredProcedure = "CacheDatasetInfoXML";
                }

                var objCommand = new SqlCommand
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandText = strStoredProcedure
                };

                objCommand.Parameters.Add(new SqlParameter("@Return", SqlDbType.Int));
                objCommand.Parameters["@Return"].Direction = ParameterDirection.ReturnValue;

                objCommand.Parameters.Add(new SqlParameter("@DatasetID", SqlDbType.Int));
                objCommand.Parameters["@DatasetID"].Direction = ParameterDirection.Input;
                objCommand.Parameters["@DatasetID"].Value = intDatasetID;

                objCommand.Parameters.Add(new SqlParameter("@DatasetInfoXML", SqlDbType.Xml));
                objCommand.Parameters["@DatasetInfoXML"].Direction = ParameterDirection.Input;
                objCommand.Parameters["@DatasetInfoXML"].Value = strDSInfoXMLClean;

                var executeSP = new PRISM.DataBase.clsExecuteDatabaseSP(strConnectionString);

                var intResult = executeSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES);

                if (intResult == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK) {
                    // No errors
                    blnSuccess = true;
                } else {
                    ShowErrorMessage("Error calling stored procedure, return code = " + intResult);
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    blnSuccess = false;
                }

            } catch (Exception ex) {
                HandleException("Error calling stored procedure", ex);
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                blnSuccess = false;
            }

            return blnSuccess;
        }

        private bool ProcessMSDataset(string strInputFileOrFolderPath, iMSFileInfoProcessor objMSInfoScanner, string strDatasetName, string strOutputFolderPath)
        {

            var datasetFileInfo = new clsDatasetFileInfo();

            bool blnSuccess;

            // Open the MS datafile (or data folder), read the creation date, and update the status file

            var intRetryCount = 0;
            do {
                // Set the processing options
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, mSaveTICAndBPIPlots);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots, mSaveLCMS2DPlots);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus, mCheckCentroidingStatus);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, mComputeOverallQualityScores);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, mCreateDatasetInfoFile);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile, mCreateScanStatsFile);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError, mCopyFileLocalOnReadError);
                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile, mUpdateDatasetStatsTextFile);

                objMSInfoScanner.DatasetStatsTextFileName = mDatasetStatsTextFileName;

                objMSInfoScanner.LCMS2DPlotOptions = mLCMS2DPlotOptions;
                objMSInfoScanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;

                objMSInfoScanner.ScanStart = mScanStart;
                objMSInfoScanner.ScanEnd = mScanEnd;
                objMSInfoScanner.ShowDebugInfo = mShowDebugInfo;

                objMSInfoScanner.DatasetID = mDSInfoDatasetIDOverride;

                // Process the data file
                blnSuccess = objMSInfoScanner.ProcessDataFile(strInputFileOrFolderPath, datasetFileInfo);

                if (!blnSuccess) {
                    intRetryCount += 1;

                    if (intRetryCount < MAX_FILE_READ_ACCESS_ATTEMPTS) {
                        // Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time

                        if (DateTime.Now.Subtract(datasetFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES || DateTime.Now.Subtract(datasetFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES) {
                            // Sleep for 10 seconds then try again
                            SleepNow(10);
                        } else {
                            intRetryCount = MAX_FILE_READ_ACCESS_ATTEMPTS;
                        }
                    }
                }
            } while (!blnSuccess & intRetryCount < MAX_FILE_READ_ACCESS_ATTEMPTS);

            if (!blnSuccess & intRetryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS) {
                if (!string.IsNullOrWhiteSpace(datasetFileInfo.DatasetName)) {
                    // Make an entry anyway; probably a corrupted file
                    blnSuccess = true;
                }
            }


            if (blnSuccess) {
                blnSuccess = objMSInfoScanner.CreateOutputFiles(strInputFileOrFolderPath, strOutputFolderPath);
                if (!blnSuccess) {
                    SetErrorCode(eMSFileScannerErrorCodes.OutputFileWriteError);
                }

                // Cache the Dataset Info XML
                mDatasetInfoXML = objMSInfoScanner.GetDatasetInfoXML();

                if (mUseCacheFiles) {
                    // Update the results database
                    blnSuccess = mMSFileInfoDataCache.UpdateCachedMSFileInfo(datasetFileInfo);

                    // Possibly auto-save the cached results
                    AutosaveCachedResults();
                }

                if (mDSInfoDBPostingEnabled) {
                    blnSuccess = PostDatasetInfoToDB(mDatasetInfoXML);
                    if (!blnSuccess) {
                        SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError);
                    }
                } else {
                    blnSuccess = true;
                }

            } else {
                if (SKIP_FILES_IN_ERROR) {
                    blnSuccess = true;
                }
            }

            return blnSuccess;

        }

        // Main processing function
        public override bool ProcessMSFileOrFolder(string strInputFileOrFolderPath, string strOutputFolderPath)
        {

            eMSFileProcessingStateConstants eMSFileProcessingState;

            return ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, true, out eMSFileProcessingState);
        }

        public override bool ProcessMSFileOrFolder(
            string strInputFileOrFolderPath, 
            string strOutputFolderPath, 
            bool blnResetErrorCode, 
            out eMSFileProcessingStateConstants eMSFileProcessingState)
        {

            // Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
            // See function ProcessMSFilesAndRecurseFolders for more details
            // This function returns True if it processed a file (or the dataset was processed previously)
            // When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
            // If the file type is unknown, or if an error occurs, then it returns false
            // eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

            var blnSuccess = false;
            var blnIsFolder = false;

            FileSystemInfo objFileSystemInfo = null;

            if (blnResetErrorCode) {
                SetErrorCode(eMSFileScannerErrorCodes.NoError);
            }

            eMSFileProcessingState = eMSFileProcessingStateConstants.NotProcessed;

            if (string.IsNullOrEmpty(strOutputFolderPath)) {
                // Define strOutputFolderPath based on the program file path
                strOutputFolderPath = GetAppFolderPath();
            }

            // Update mOutputFolderPath
            mOutputFolderPath = string.Copy(strOutputFolderPath);

            mDatasetInfoXML = string.Empty;

            LoadCachedResults(false);

            try {
                if (string.IsNullOrEmpty(strInputFileOrFolderPath)) {
                    ShowErrorMessage("Input file name is empty");
                } else {
                    try {
                        if (Path.GetFileName(strInputFileOrFolderPath).Length == 0) {
                            ShowMessage("Parsing " + Path.GetDirectoryName(strInputFileOrFolderPath));
                        } else {
                            ShowMessage("Parsing " + Path.GetFileName(strInputFileOrFolderPath));
                        }
                    } catch (Exception ex) {
                        HandleException("Error parsing " + strInputFileOrFolderPath, ex);
                    }

                    // Determine whether strInputFileOrFolderPath points to a file or a folder

                    if (!GetFileOrFolderInfo(strInputFileOrFolderPath, ref blnIsFolder, ref objFileSystemInfo)) {
                        ShowErrorMessage("File or folder not found: " + objFileSystemInfo.FullName);
                        if (SKIP_FILES_IN_ERROR) {
                            return true;
                        } else {
                            SetErrorCode(eMSFileScannerErrorCodes.FilePathError);
                            return false;
                        }
                    }

                    var blnKnownMSDataType = false;

                    // Only continue if it's a known type
                    if (blnIsFolder) {
                        if (objFileSystemInfo.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME) {
                            // Bruker 1 folder
                            mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                            blnKnownMSDataType = true;
                        } else {
                            if (strInputFileOrFolderPath.EndsWith('\\')) {
                                strInputFileOrFolderPath = strInputFileOrFolderPath.TrimEnd('\\');
                            }

                            switch (Path.GetExtension(strInputFileOrFolderPath).ToUpper()) {
                                case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION:
                                    // Agilent .D folder or Bruker .D folder

                                    if (Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SER_FILE_NAME).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_FID_FILE_NAME).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_idx").Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME + "_xtr").Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME).Length > 0) {
                                        mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();

                                    } else if (Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_MS_DATA_FILE).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_ACQ_METHOD_FILE).Length > 0 || Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_GC_INI_FILE).Length > 0) {
                                        mMSInfoScanner = new clsAgilentGCDFolderInfoScanner();

                                    } else if (Directory.GetDirectories(strInputFileOrFolderPath, clsAgilentTOFDFolderInfoScanner.AGILENT_ACQDATA_FOLDER_NAME).Length > 0) {
                                        mMSInfoScanner = new clsAgilentTOFDFolderInfoScanner();

                                    } else {
                                        mMSInfoScanner = new clsAgilentIonTrapDFolderInfoScanner();
                                    }

                                    blnKnownMSDataType = true;
                                    break;
                                case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION:
                                    // Micromass .Raw folder
                                    mMSInfoScanner = new clsMicromassRawFolderInfoScanner();
                                    blnKnownMSDataType = true;
                                    break;
                                default:
                                    // Unknown folder extension (or no extension)
                                    // See if the folder contains 1 or more 0_R*.zip files
                                    if (Directory.GetFiles(strInputFileOrFolderPath, clsZippedImagingFilesScanner.ZIPPED_IMAGING_FILE_SEARCH_SPEC).Length > 0) {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        blnKnownMSDataType = true;
                                    }
                                    break;
                            }
                        }
                    } else {
                        if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            blnKnownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            blnKnownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase)) {
                            mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                            blnKnownMSDataType = true;

                        } else if (string.Equals(objFileSystemInfo.Name, clsBrukerXmassFolderInfoScanner.BRUKER_ANALYSIS_YEP_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            // If the folder also contains file BRUKER_EXTENSION_BAF_FILE_NAME then this is a Bruker XMass folder
                            var parentFolder = Path.GetDirectoryName(objFileSystemInfo.FullName);
                            if (!string.IsNullOrEmpty(parentFolder))
                            {
                                var strPathCheck = Path.Combine(parentFolder,
                                                                clsBrukerXmassFolderInfoScanner
                                                                    .BRUKER_EXTENSION_BAF_FILE_NAME);
                                if (File.Exists(strPathCheck))
                                {
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    blnKnownMSDataType = true;
                                }
                            }
                        }


                        if (!blnKnownMSDataType) {
                            // Examine the extension on strInputFileOrFolderPath
                            switch (objFileSystemInfo.Extension.ToUpper()) {
                                case clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION:
                                    mMSInfoScanner = new clsFinniganRawFileInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION:
                                    mMSInfoScanner = new clsAgilentTOFOrQStarWiffFileInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION:
                                    mMSInfoScanner = new clsBrukerXmassFolderInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsUIMFInfoScanner.UIMF_FILE_EXTENSION:
                                    mMSInfoScanner = new clsUIMFInfoScanner();
                                    blnKnownMSDataType = true;

                                    break;
                                case clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION:

                                    if (objFileSystemInfo.FullName.ToUpper().EndsWith(clsDeconToolsIsosInfoScanner.DECONTOOLS_ISOS_FILE_SUFFIX)) {
                                        mMSInfoScanner = new clsDeconToolsIsosInfoScanner();
                                        blnKnownMSDataType = true;
                                    }

                                    break;
                                default:
                                    // Unknown file extension; check for a zipped folder 
                                    if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(objFileSystemInfo.Name)) {
                                        // Bruker s001.zip file
                                        mMSInfoScanner = new clsBrukerOneFolderInfoScanner();
                                        blnKnownMSDataType = true;
                                    } else if (clsZippedImagingFilesScanner.IsZippedImagingFile(objFileSystemInfo.Name)) {
                                        mMSInfoScanner = new clsZippedImagingFilesScanner();
                                        blnKnownMSDataType = true;
                                    }
                                    break;
                            }
                        }

                    }

                    if (!blnKnownMSDataType) {
                        ShowErrorMessage("Unknown file type: " + Path.GetFileName(strInputFileOrFolderPath));
                        SetErrorCode(eMSFileScannerErrorCodes.UnknownFileExtension);
                        return false;
                    }

                    // Attach the events
                    mMSInfoScanner.ErrorEvent += mMSInfoScanner_ErrorEvent;
                    mMSInfoScanner.MessageEvent += mMSInfoScannerMessageEvent;

                    var strDatasetName = mMSInfoScanner.GetDatasetNameViaPath(objFileSystemInfo.FullName);

                    if (mUseCacheFiles && !mReprocessExistingFiles)
                    {
                        // See if the strDatasetName in strInputFileOrFolderPath is already present in mCachedResults
                        // If it is present, then don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

                        DataRow objRow;
                        if (strDatasetName.Length > 0 && mMSFileInfoDataCache.CachedMSInfoContainsDataset(strDatasetName, out objRow)) {
                            if (mReprocessIfCachedSizeIsZero) {
                                long lngCachedSizeBytes;
                                try {
                                    lngCachedSizeBytes = (long)objRow[clsMSFileInfoDataCache.COL_NAME_FILE_SIZE_BYTES];
                                } catch (Exception) {
                                    lngCachedSizeBytes = 1;
                                }

                                if (lngCachedSizeBytes > 0) {
                                    // File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                    ShowMessage("  Skipping " + Path.GetFileName(strInputFileOrFolderPath) + " since already in cached results");
                                    eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                    return true;
                                }
                            } else {
                                // File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                                ShowMessage("  Skipping " + Path.GetFileName(strInputFileOrFolderPath) + " since already in cached results");
                                eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache;
                                return true;
                            }
                        }
                    }

                    // Process the data file or folder
                    blnSuccess = ProcessMSDataset(strInputFileOrFolderPath, mMSInfoScanner, strDatasetName, strOutputFolderPath);
                    if (blnSuccess) {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully;
                    } else {
                        eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing;
                    }

                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFileOrFolder", ex);
                blnSuccess = false;
            } finally {
                mMSInfoScanner = null;
            }

            return blnSuccess;

        }

        public override bool ProcessMSFileOrFolderWildcard(string strInputFileOrFolderPath, string strOutputFolderPath, bool blnResetErrorCode)
        {
            // Returns True if success, False if failure

            var strProcessedFileList = new List<string>();

            mAbortProcessing = false;
            var blnSuccess = true;
            try {
                // Possibly reset the error code
                if (blnResetErrorCode) {
                    SetErrorCode(eMSFileScannerErrorCodes.NoError);
                }

                // See if strInputFilePath contains a wildcard
                eMSFileProcessingStateConstants eMSFileProcessingState;
                if ((strInputFileOrFolderPath != null) && (strInputFileOrFolderPath.IndexOf('*') >= 0 | strInputFileOrFolderPath.IndexOf('?') >= 0)) {
                    // Obtain a list of the matching files and folders

                    // Copy the path into strCleanPath and replace any * or ? characters with _
                    var strCleanPath = strInputFileOrFolderPath.Replace("*", "_");
                    strCleanPath = strCleanPath.Replace("?", "_");

                    var fiFileInfo = new FileInfo(strCleanPath);
                    string strInputFolderPath;
                    if (fiFileInfo.Directory != null && fiFileInfo.Directory.Exists) {
                        strInputFolderPath = fiFileInfo.DirectoryName;
                    } else {
                        // Use the current working directory
                        strInputFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    if (string.IsNullOrEmpty(strInputFolderPath))
                        strInputFolderPath = ".";

                    var diFolderInfo = new DirectoryInfo(strInputFolderPath);

                    // Remove any directory information from strInputFileOrFolderPath
                    strInputFileOrFolderPath = Path.GetFileName(strInputFileOrFolderPath);

                    var intMatchCount = 0;

                    foreach (var fiFileMatch in diFolderInfo.GetFiles(strInputFileOrFolderPath)) {
                        blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, blnResetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing) {
                            strProcessedFileList.Add(fiFileMatch.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (mAbortProcessing)
                            break;

                        if (!blnSuccess && !SKIP_FILES_IN_ERROR)
                            break;

                        intMatchCount += 1;

                        if (intMatchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (mAbortProcessing)
                        return false;


                    foreach (var diFolderMatch in diFolderInfo.GetDirectories(strInputFileOrFolderPath)) {
                        blnSuccess = ProcessMSFileOrFolder(diFolderMatch.FullName, strOutputFolderPath, blnResetErrorCode, out eMSFileProcessingState);

                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                        {
                            strProcessedFileList.Add(diFolderMatch.FullName);
                        }

                        CheckForAbortProcessingFile();
                        if (mAbortProcessing)
                            break;

                        if (!blnSuccess && !SKIP_FILES_IN_ERROR)
                            break;

                        intMatchCount += 1;

                        if (intMatchCount % 100 == 0)
                            Console.Write(".");
                    }

                    if (mAbortProcessing)
                        return false;

                    if (mCheckFileIntegrity) {
                        CheckIntegrityOfFilesInFolder(diFolderInfo.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList);
                    }

                    if (intMatchCount == 0) {
                        if (mErrorCode == eMSFileScannerErrorCodes.NoError) {
                            ShowMessage("No match was found for the input file path:" + strInputFileOrFolderPath, eMessageTypeConstants.Warning);
                        }
                    } else {
                        Console.WriteLine();
                    }
                } else {
                    blnSuccess = ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, blnResetErrorCode, out eMSFileProcessingState);
                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFileOrFolderWildcard", ex);
                blnSuccess = false;
            } finally {
                if ((mFileIntegrityDetailsWriter != null)) {
                    mFileIntegrityDetailsWriter.Close();
                }
                if ((mFileIntegrityErrorsWriter != null)) {
                    mFileIntegrityErrorsWriter.Close();
                }
            }

            return blnSuccess;

        }

        /// <summary>
        /// Calls ProcessMSFileOrFolder for all files in strInputFilePathOrFolder and below having a known extension
        ///  Known extensions are:
        ///   .Raw for Finnigan files
        ///   .Wiff for Agilent TOF files and for Q-Star files
        ///   .Baf for Bruker XMASS folders (contains file analysis.baf, and hopefully files scan.xml and Log.txt)
        /// For each folder that does not have any files matching a known extension, will then look for special folder names:
        ///   Folders matching *.Raw for Micromass data
        ///   Folders matching *.D for Agilent Ion Trap data
        ///   A folder named 1 for Bruker FTICR-MS data
        /// </summary>
        /// <param name="strInputFilePathOrFolder">Path to the input file or folder; can contain a wildcard (* or ?)</param>
        /// <param name="strOutputFolderPath">Folder to write any results files to</param>
        /// <param name="intRecurseFoldersMaxLevels">Maximum folder depth to process; Set to 0 to process all folders</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override bool ProcessMSFilesAndRecurseFolders(string strInputFilePathOrFolder, string strOutputFolderPath, int intRecurseFoldersMaxLevels)
        {
            bool blnSuccess;

            // Examine strInputFilePathOrFolder to see if it contains a filename; if not, assume it points to a folder
            // First, see if it contains a * or ?
            try {
                string strInputFolderPath;
                if ((strInputFilePathOrFolder != null) && (strInputFilePathOrFolder.IndexOf('*') >= 0 | strInputFilePathOrFolder.IndexOf('?') >= 0)) {
                    // Copy the path into strCleanPath and replace any * or ? characters with _
                    var strCleanPath = strInputFilePathOrFolder.Replace("*", "_");
                    strCleanPath = strCleanPath.Replace("?", "_");

                    var fiFileInfo = new FileInfo(strCleanPath);
                    if (Path.IsPathRooted(strCleanPath)) {
                        if (fiFileInfo.Directory != null && !fiFileInfo.Directory.Exists) {
                            ShowErrorMessage("Folder not found: " + fiFileInfo.DirectoryName);
                            SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                            return false;
                        }
                    }

                    if (fiFileInfo.Directory != null && fiFileInfo.Directory.Exists) {
                        strInputFolderPath = fiFileInfo.DirectoryName;
                    } else {
                        // Folder not found; use the current working directory
                        strInputFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    }

                    // Remove any directory information from strInputFilePath
                    strInputFilePathOrFolder = Path.GetFileName(strInputFilePathOrFolder);

                } else
                {
                    if (string.IsNullOrEmpty(strInputFilePathOrFolder))
                        strInputFilePathOrFolder = ".";

                    var diFolderInfo = new DirectoryInfo(strInputFilePathOrFolder);
                    if (diFolderInfo.Exists) {
                        strInputFolderPath = diFolderInfo.FullName;
                        strInputFilePathOrFolder = "*";
                    } else {
                        if (diFolderInfo.Parent != null && diFolderInfo.Parent.Exists) {
                            strInputFolderPath = diFolderInfo.Parent.FullName;
                            strInputFilePathOrFolder = Path.GetFileName(strInputFilePathOrFolder);
                        } else {
                            // Unable to determine the input folder path
                            strInputFolderPath = string.Empty;
                        }
                    }
                }


                if (!string.IsNullOrEmpty(strInputFolderPath)) {
                    // Initialize some parameters
                    mAbortProcessing = false;
                    var intFileProcessCount = 0;
                    var intFileProcessFailCount = 0;

                    LoadCachedResults(false);

                    // Call RecurseFoldersWork
                    blnSuccess = RecurseFoldersWork(strInputFolderPath, strInputFilePathOrFolder, strOutputFolderPath, ref intFileProcessCount, ref intFileProcessFailCount, 1, intRecurseFoldersMaxLevels);

                } else {
                    SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath);
                    return false;
                }

            } catch (Exception ex) {
                HandleException("Error in ProcessMSFilesAndRecurseFolders", ex);
                blnSuccess = false;
            } finally {
                if ((mFileIntegrityDetailsWriter != null)) {
                    mFileIntegrityDetailsWriter.Close();
                }
                if ((mFileIntegrityErrorsWriter != null)) {
                    mFileIntegrityErrorsWriter.Close();
                }
            }

            return blnSuccess;

        }

        private bool RecurseFoldersWork(string strInputFolderPath, string strFileNameMatch, string strOutputFolderPath, ref int intFileProcessCount, ref int intFileProcessFailCount, int intRecursionLevel, int intRecurseFoldersMaxLevels)
        {

            const int MAX_ACCESS_ATTEMPTS = 2;

            // If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

            DirectoryInfo diInputFolder = null;

            List<string> strFileExtensionsToParse;
            List<string> strFolderExtensionsToParse;
            bool blnProcessAllFileExtensions;

            bool blnSuccess;
            var blnFileProcessed = false;

            eMSFileProcessingStateConstants eMSFileProcessingState;

            var strProcessedFileList = new List<string>();

            var intRetryCount = 0;
            do {
                try {
                    diInputFolder = new DirectoryInfo(strInputFolderPath);
                    break;
                } catch (Exception ex) {
                    // Input folder path error
                    HandleException("Error populating diInputFolderInfo for " + strInputFolderPath, ex);
                    if (!ex.Message.Contains("no longer available")) {
                        return false;
                    }
                }

                intRetryCount += 1;
                if (intRetryCount >= MAX_ACCESS_ATTEMPTS) {
                    return false;
                }
                // Wait 1 second, then try again
                SleepNow(1);
            } while (intRetryCount < MAX_ACCESS_ATTEMPTS);

            if (diInputFolder == null)
            {
                 ShowErrorMessage("Unable to instantiate a directory info object for " + strInputFolderPath);
                return false;
            }

            try {
                // Construct and validate the list of file and folder extensions to parse
                strFileExtensionsToParse = GetKnownFileExtensionsList();
                strFolderExtensionsToParse = GetKnownFolderExtensionsList();

                // Validate the extensions, including assuring that they are all capital letters
                blnProcessAllFileExtensions = ValidateExtensions(strFileExtensionsToParse);
                ValidateExtensions(strFolderExtensionsToParse);
            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork", ex);
                return false;
            }

            try {
                Console.WriteLine("Examining " + strInputFolderPath);

                // Process any matching files in this folder
                blnSuccess = true;
                var blnProcessedZippedSFolder = false;

                foreach (var fiFileMatch in diInputFolder.GetFiles(strFileNameMatch)) {
                    intRetryCount = 0;
                    do {

                        try {
                            blnFileProcessed = false;
                            foreach (var fileExtension in strFileExtensionsToParse) {
                                if (!blnProcessAllFileExtensions && fiFileMatch.Extension.ToUpper() != fileExtension)
                                {
                                    continue;
                                }

                                blnFileProcessed = true;
                                blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, true, out eMSFileProcessingState);

                                if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                {
                                    strProcessedFileList.Add(fiFileMatch.FullName);
                                }

                                // Successfully processed a folder; exit the for loop
                                break;
                            }

                            if (mAbortProcessing)
                                break;

                            if (!blnFileProcessed && !blnProcessedZippedSFolder) {
                                // Check for other valid files
                                if (clsBrukerOneFolderInfoScanner.IsZippedSFolder(fiFileMatch.Name)) {
                                    // Only process this file if there is not a subfolder named "1" present"
                                    if (diInputFolder.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1) {
                                        blnFileProcessed = true;
                                        blnProcessedZippedSFolder = true;
                                        blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, true, out eMSFileProcessingState);

                                        if (eMSFileProcessingState == eMSFileProcessingStateConstants.ProcessedSuccessfully || eMSFileProcessingState == eMSFileProcessingStateConstants.FailedProcessing)
                                        {
                                            strProcessedFileList.Add(fiFileMatch.FullName);
                                        }

                                    }
                                }
                            }

                            // Exit the Do loop
                            break;

                        } catch (Exception ex) {
                            // Error parsing file
                            HandleException("Error in RecurseFoldersWork at For Each ioFileMatch in " + strInputFolderPath, ex);
                            if (!ex.Message.Contains("no longer available")) {
                                return false;
                            }
                        }

                        if (mAbortProcessing)
                            break;

                        intRetryCount += 1;
                        if (intRetryCount >= MAX_ACCESS_ATTEMPTS) {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);

                    } while (intRetryCount < MAX_ACCESS_ATTEMPTS);

                    if (blnFileProcessed) {
                        if (blnSuccess) {
                            intFileProcessCount += 1;
                        } else {
                            intFileProcessFailCount += 1;
                            blnSuccess = true;
                        }
                    }

                    CheckForAbortProcessingFile();
                    if (mAbortProcessing)
                        break;
                }

                if (mCheckFileIntegrity & !mAbortProcessing) {
                    CheckIntegrityOfFilesInFolder(diInputFolder.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList);
                }

            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork Examining files in " + strInputFolderPath, ex);
                return false;
            }

            if (mAbortProcessing)
            {
                return blnSuccess;
            }

            // Check the subfolders for those with known extensions

            try {
                var intSubFoldersProcessed = 0;
                var htSubFoldersProcessed = new Hashtable();

                foreach (var diSubfolder in diInputFolder.GetDirectories(strFileNameMatch)) {
                    intRetryCount = 0;
                    do {
                        try {
                            // Check whether the folder name is BRUKER_ONE_FOLDER = "1"
                            if (diSubfolder.Name == clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME) {
                                blnSuccess = ProcessMSFileOrFolder(diSubfolder.FullName, strOutputFolderPath, true, out eMSFileProcessingState);
                                if (!blnSuccess) {
                                    intFileProcessFailCount += 1;
                                    blnSuccess = true;
                                } else {
                                    intFileProcessCount += 1;
                                }
                                intSubFoldersProcessed += 1;
                                htSubFoldersProcessed.Add(diSubfolder.Name, 1);
                            } else {
                                // See if the subfolder has an extension matching strFolderExtensionsToParse()
                                // If it does, process it using ProcessMSFileOrFolder and do not recurse into it
                                foreach (var folderExtension in strFolderExtensionsToParse) {
                                    if (diSubfolder.Extension.ToUpper() != folderExtension)
                                    {
                                        continue;
                                    }

                                    blnSuccess = ProcessMSFileOrFolder(diSubfolder.FullName, strOutputFolderPath, true, out eMSFileProcessingState);
                                    if (!blnSuccess) {
                                        intFileProcessFailCount += 1;
                                        blnSuccess = true;
                                    } else {
                                        intFileProcessCount += 1;
                                    }
                                    intSubFoldersProcessed += 1;
                                    htSubFoldersProcessed.Add(diSubfolder.Name, 1);

                                    // Successfully processed a folder; exit the for loop
                                    break;
                                }

                            }

                            // Exit the Do loop
                            break;

                        } catch (Exception ex) {
                            // Error parsing folder
                            HandleException("Error in RecurseFoldersWork at For Each diSubfolder(A) in " + strInputFolderPath, ex);
                            if (!ex.Message.Contains("no longer available")) {
                                return false;
                            }
                        }

                        if (mAbortProcessing)
                            break;

                        intRetryCount += 1;
                        if (intRetryCount >= MAX_ACCESS_ATTEMPTS) {
                            return false;
                        }

                        // Wait 1 second, then try again
                        SleepNow(1);
                    } while (intRetryCount < MAX_ACCESS_ATTEMPTS);

                    if (mAbortProcessing)
                        break;

                }

                // If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
                //  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
                if (intRecurseFoldersMaxLevels <= 0 || intRecursionLevel <= intRecurseFoldersMaxLevels) {
                    // Call this function for each of the subfolders of diInputFolder
                    // However, do not step into folders listed in htSubFoldersProcessed

                    foreach (var diSubfolder in diInputFolder.GetDirectories()) {
                        intRetryCount = 0;
                        do {
                            try {
                                if (intSubFoldersProcessed == 0 || !htSubFoldersProcessed.Contains(diSubfolder.Name)) {
                                    blnSuccess = RecurseFoldersWork(diSubfolder.FullName, strFileNameMatch, strOutputFolderPath, ref intFileProcessCount, ref intFileProcessFailCount, intRecursionLevel + 1, intRecurseFoldersMaxLevels);
                                }

                                if (!blnSuccess & !mIgnoreErrorsWhenRecursing) {
                                    break; 
                                }

                                CheckForAbortProcessingFile();

                                break;

                            } catch (Exception ex) {
                                // Error parsing file
                                HandleException("Error in RecurseFoldersWork at For Each diSubfolder(B) in " + strInputFolderPath, ex);
                                if (!ex.Message.Contains("no longer available")) {
                                    return false;
                                }
                            }

                            if (mAbortProcessing)
                                break;


                            intRetryCount += 1;
                            if (intRetryCount >= MAX_ACCESS_ATTEMPTS) {
                                return false;
                            } else {
                                // Wait 1 second, then try again
                                SleepNow(1);
                            }

                        } while (intRetryCount < MAX_ACCESS_ATTEMPTS);

                        if (!blnSuccess & !mIgnoreErrorsWhenRecursing)
                        {
                            break;
                        }

                        if (mAbortProcessing)
                            break;
                    }
                }


            } catch (Exception ex) {
                HandleException("Error in RecurseFoldersWork examining subfolders in " + strInputFolderPath, ex);
                return false;
            }

            return blnSuccess;

        }

        public override bool SaveCachedResults()
        {
            return SaveCachedResults(true);
        }

        public override bool SaveCachedResults(bool blnClearCachedData)
        {
            if (mUseCacheFiles) {
                return mMSFileInfoDataCache.SaveCachedResults(blnClearCachedData);
            } else {
                return true;
            }
        }

        public override bool SaveParameterFileSettings(string strParameterFilePath)
        {

            var objSettingsFile = new XmlSettingsFileAccessor();


            try {
                if (string.IsNullOrEmpty(strParameterFilePath)) {
                    // No parameter file specified; unable to save
                    return false;
                }

                // Pass True to .LoadSettings() here so that newly made Xml files will have the correct capitalization
                if (objSettingsFile.LoadSettings(strParameterFilePath, true)) {

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

        private void SetErrorCode(eMSFileScannerErrorCodes eNewErrorCode, bool blnLeaveExistingErrorCodeUnchanged = false)
        {
            if (blnLeaveExistingErrorCodeUnchanged && mErrorCode != eMSFileScannerErrorCodes.NoError) {
                // An error code is already defined; do not change it
            } else {
                mErrorCode = eNewErrorCode;
            }

        }

        private void ShowErrorMessage(string strMessage)
        {
            ShowErrorMessage(strMessage, true);
        }

        private void ShowErrorMessage(string strMessage, bool blnAllowLogToFile)
        {
            var strSeparator = "------------------------------------------------------------------------------";

            Console.WriteLine();
            Console.WriteLine(strSeparator);
            Console.WriteLine(strMessage);
            Console.WriteLine(strSeparator);
            Console.WriteLine();

            if (ErrorEvent != null) {
                ErrorEvent(strMessage);
            }

            if (blnAllowLogToFile) {
                LogMessage(strMessage, eMessageTypeConstants.ErrorMsg);
            }

        }

        private void ShowMessage(string strMessage)
        {
            ShowMessage(strMessage, true, false, eMessageTypeConstants.Normal);
        }

        private void ShowMessage(string strMessage, eMessageTypeConstants eMessageType)
        {
            ShowMessage(strMessage, true, false, eMessageType);
        }

        private void ShowMessage(string strMessage, bool blnAllowLogToFile)
        {
            ShowMessage(strMessage, blnAllowLogToFile, false, eMessageTypeConstants.Normal);
        }


        private void ShowMessage(string strMessage, bool blnAllowLogToFile, bool blnPrecedeWithNewline, eMessageTypeConstants eMessageType)
        {
            if (blnPrecedeWithNewline) {
                Console.WriteLine();
            }
            Console.WriteLine(strMessage);

            if (MessageEvent != null) {
                MessageEvent(strMessage);
            }

            if (blnAllowLogToFile) {
                LogMessage(strMessage, eMessageType);
            }

        }

        public static bool ValidateDataFilePath(ref string strFilePath, eDataFileTypeConstants eDataFileType)
        {
            if (string.IsNullOrEmpty(strFilePath)) {
                strFilePath = Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileType));
            }

            return ValidateDataFilePathCheckDir(strFilePath);

        }

        private static bool ValidateDataFilePathCheckDir(string strFilePath)
        {
            bool blnValidFile;

            try {
                var fiFileInfo = new FileInfo(strFilePath);

                if (!fiFileInfo.Exists) {
                    // Make sure the folder exists
                    if (fiFileInfo.Directory != null && !fiFileInfo.Directory.Exists) {
                        fiFileInfo.Directory.Create();
                    }
                }
                blnValidFile = true;

            } catch (Exception) {
                // Ignore errors, but set blnValidFile to false
                blnValidFile = false;
            }

            return blnValidFile;
        }

        private void SleepNow(int sleepTimeSeconds)
        {
            System.Threading.Thread.Sleep(sleepTimeSeconds * 10);
        }

        private bool ValidateExtensions(IList<string> strExtensions)
        {
            // Returns True if one of the entries in strExtensions = "*" or ".*"

            var blnProcessAllExtensions = false;

            for (var extensionIndex = 0; extensionIndex < strExtensions.Count; extensionIndex++)
            {
                if (strExtensions[extensionIndex] == null) {
                    strExtensions[extensionIndex] = string.Empty;
                } else {
                    if (!strExtensions[extensionIndex].StartsWith(".")) {
                        strExtensions[extensionIndex] = "." + strExtensions[extensionIndex];
                    }

                    if (strExtensions[extensionIndex] == ".*") {
                        blnProcessAllExtensions = true;
                        break;
                    }
                    strExtensions[extensionIndex] = strExtensions[extensionIndex].ToUpper();
                }
            }

            return blnProcessAllExtensions;
        }

        private void WriteFileIntegrityDetails(
            TextWriter srOutFile, 
            int intFolderID, 
            IEnumerable<clsFileIntegrityChecker.udtFileStatsType> udtFileStats)
        {

            if (srOutFile == null)
                return;

            foreach (var item in udtFileStats) {
                // Note: HH:mm:ss corresponds to time in 24 hour format
                srOutFile.WriteLine(
                    intFolderID.ToString() + '\t' + 
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

        private void WriteFileIntegrityFailure(ref StreamWriter srOutFile, string strFilePath, string strMessage)
        {

            if (srOutFile == null)
                return;

            // Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(strFilePath + '\t' + strMessage + '\t' + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            if (DateTime.UtcNow.Subtract(mLastWriteTimeFileIntegrityFailure).TotalMinutes > 1)
            {
                srOutFile.Flush();
                mLastWriteTimeFileIntegrityFailure = DateTime.UtcNow;
            }

        }

        private void mFileIntegrityChecker_ErrorCaught(string strMessage)
        {
            ShowErrorMessage("Error caught in FileIntegrityChecker: " + strMessage);
        }

        private void mFileIntegrityChecker_FileIntegrityFailure(string strFilePath, string strMessage)
        {
            if (mFileIntegrityErrorsWriter == null) {
                OpenFileIntegrityErrorsFile();
            }

            WriteFileIntegrityFailure(ref mFileIntegrityErrorsWriter, strFilePath, strMessage);
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

        #endregion

    }
}
