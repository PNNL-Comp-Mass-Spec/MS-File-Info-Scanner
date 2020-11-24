using PRISM;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace MSFileInfoScannerInterfaces
{
    // ReSharper disable once InconsistentNaming
    public abstract class iMSFileInfoScanner : EventNotifier
    {
        /// <summary>
        /// MSFileInfoScanner error codes
        /// </summary>
        public enum MSFileScannerErrorCodes
        {
            NoError = 0,
            InvalidInputFilePath = 1,
            InvalidOutputDirectoryPath = 2,
            ParameterFileNotFound = 3,
            FilePathError = 4,

            ParameterFileReadError = 5,
            UnknownFileExtension = 6,
            InputFileAccessError = 7,
            InputFileReadError = 8,
            OutputFileWriteError = 9,
            FileIntegrityCheckError = 10,

            DatabasePostingError = 11,
            MS2MzMinValidationError = 12,
            MS2MzMinValidationWarning = 13,

            ThermoRawFileReaderError = 14,
            DatasetHasNoSpectra = 15,

            UnspecifiedError = -1
        }

        /// <summary>
        /// Processing state constants
        /// </summary>
        public enum MSFileProcessingStateConstants
        {
            NotProcessed = 0,
            SkippedSinceFoundInCache = 1,
            FailedProcessing = 2,
            ProcessedSuccessfully = 3
        }

        /// <summary>
        /// Data file type constants
        /// </summary>
        public enum DataFileTypeConstants
        {
            MSFileInfo = 0,
            DirectoryIntegrityInfo = 1,
            FileIntegrityDetails = 2,
            FileIntegrityErrors = 3
        }

        public virtual bool AbortProcessing { get; set; }

        public virtual string AcquisitionTimeFilename { get; set; }

        /// <summary>
        /// When true, checks the integrity of every file in every directory processed
        /// </summary>
        public virtual bool CheckFileIntegrity { get; set; }

        public virtual bool ComputeOverallQualityScores { get; set; }

        /// <summary>
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public virtual string DatasetInfoXML { get; protected set; }

        public virtual bool CheckCentroidingStatus { get; set; }

        /// <summary>
        /// When True, computes a SHA-1 hash on every file using mFileIntegrityChecker
        /// </summary>
        /// <remarks>
        /// Note, when this is false, the program computes the SHA-1 hash of the primary dataset file (or files),
        /// unless DisableInstrumentHash is true
        /// </remarks>
        public virtual bool ComputeFileHashes { get; set; }

        /// <summary>
        /// If True, copies .Raw files to the local drive if unable to read the file over the network
        /// </summary>
        public virtual bool CopyFileLocalOnReadError { get; set; }

        /// <summary>
        /// If True, will create the _DatasetInfo.xml file
        /// </summary>
        public virtual bool CreateDatasetInfoFile { get; set; }

        /// <summary>
        /// If True, will create the _ScanStats.txt file
        /// </summary>
        public virtual bool CreateScanStatsFile { get; set; }

        /// <summary>
        /// DatasetID value to use instead of trying to lookup the value in DMS (and instead of using 0)
        /// </summary>
        public virtual int DatasetIDOverride { get; set; }

        public virtual string DatasetStatsTextFileName { get; set; }

        public virtual string DSInfoConnectionString { get; set; }

        public virtual bool DSInfoDBPostingEnabled { get; set; }

        public virtual string DSInfoStoredProcedure { get; set; }

        public virtual MSFileScannerErrorCodes ErrorCode { get; protected set; }

        public virtual bool IgnoreErrorsWhenRecursing { get; set; }

        public virtual float LCMS2DPlotMZResolution { get; set; }

        public virtual int LCMS2DPlotMaxPointsToPlot { get; set; }

        public virtual int LCMS2DOverviewPlotDivisor { get; set; }

        public virtual int LCMS2DPlotMinPointsPerSpectrum { get; set; }

        public virtual float LCMS2DPlotMinIntensity { get; set; }

        public virtual bool LogMessagesToFile { get; set; }

        public virtual string LogDirectoryPath { get; set; }

        public virtual string LogFilePath { get; set; }

        public virtual int MaximumTextFileLinesToCheck { get; set; }

        public virtual int MaximumXMLElementNodesToCheck { get; set; }

        /// <summary>
        /// Minimum m/z value that MS/mS spectra should have
        /// </summary>
        /// <remarks>
        /// Useful for validating instrument files where the sample is iTRAQ or TMT labeled
        /// and it is important to detect the reporter ions in the MS/MS spectra
        /// </remarks>
        public virtual float MS2MzMin { get; set; }

        /// <summary>
        /// MS2MzMin validation error or warning message
        /// </summary>
        public virtual string MS2MzMinValidationMessage { get; protected set; }

        /// <summary>
        /// Processing options
        /// </summary>
        public virtual InfoScannerOptions Options { get; protected set; }

        /// <summary>
        /// When true, generate plots using Python
        /// </summary>
        public virtual bool PlotWithPython { get; set; }

        public virtual bool RecheckFileIntegrityForExistingDirectories { get; set; }

        public virtual bool ReprocessExistingFiles { get; set; }

        public virtual bool ReprocessIfCachedSizeIsZero { get; set; }

        /// <summary>
        /// If True, saves TIC and BPI plots as PNG files
        /// </summary>
        public virtual bool SaveTICAndBPIPlots { get; set; }

        /// <summary>
        /// If True, saves a 2D plot of m/z vs. Intensity (requires reading every data point in the data file, which will slow down the processing)
        /// </summary>
        /// <value></value>
        public virtual bool SaveLCMS2DPlots { get; set; }

        /// <summary>
        /// When ScanStart is > 0, will start processing at the specified scan number
        /// </summary>
        public virtual int ScanStart { get; set; }

        /// <summary>
        /// When ScanEnd is > 0, will stop processing at the specified scan number
        /// </summary>
        public virtual int ScanEnd { get; set; }

        public virtual bool ShowDebugInfo { get; set; }

        public virtual bool UpdateDatasetStatsTextFile { get; set; }

        /// <summary>
        /// If True, saves/loads data from/to the cache files (DatasetTimeFile.txt and DirectoryIntegrityInfo.txt)
        /// If you simply want to create TIC and BPI files, and/or the _DatasetInfo.xml file for a single dataset, set this to False
        /// </summary>
        /// <remarks>If this is false, data is not loaded/saved from/to the DatasetTimeFile.txt or the DirectoryIntegrityInfo.txt file</remarks>
        public virtual bool UseCacheFiles { get; set; }

        public virtual bool ZipFileCheckAllData { get; set; }
        public abstract string[] GetKnownDirectoryExtensions();

        public abstract string[] GetKnownFileExtensions();

        public abstract string GetErrorMessage();

        public abstract bool LoadParameterFileSettings(string parameterFilePath);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB();

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetInfoXML);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string connectionString, string storedProcedureName);

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string dsInfoXML, string connectionString, string storedProcedureName);

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="datasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoUseDatasetID(int datasetID, string connectionString, string storedProcedureName);

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// This version assumes the stored procedure takes DatasetID as the first parameter
        /// </summary>
        /// <param name="datasetID">Dataset ID to send to the stored procedure</param>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoUseDatasetID(int datasetID, string dsInfoXML, string connectionString, string storedProcedureName);

        /// <summary>
        /// Main processing function, with input file / directory path, plus output directory path
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFileOrDirectory(string inputFileOrDirectoryPath, string outputDirectoryPath);

        /// <summary>
        /// Main processing function with input / output paths, error code reset flag, and processing state
        /// </summary>
        /// <param name="inputFileOrDirectoryPath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <param name="resetErrorCode"></param>
        /// <param name="msFileProcessingState"></param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFileOrDirectory(string inputFileOrDirectoryPath, string outputDirectoryPath, bool resetErrorCode,
                                                      out MSFileProcessingStateConstants msFileProcessingState);

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFileOrDirectoryPath and below having a known extension
        /// </summary>
        /// <param name="inputFileOrDirectoryPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="resetErrorCode"></param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFileOrDirectoryWildcard(string inputFileOrDirectoryPath, string outputDirectoryPath, bool resetErrorCode);

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFilePathOrDirectory and below having a known extension
        /// </summary>
        /// <param name="inputFilePathOrDirectory">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="maxLevelsToRecurse">Maximum depth to recurse; Set to 0 to process all directories</param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFilesAndRecurseDirectories(string inputFilePathOrDirectory, string outputDirectoryPath, int maxLevelsToRecurse);

        public abstract bool SaveCachedResults();

        public abstract bool SaveCachedResults(bool clearCachedData);

        public abstract bool SaveParameterFileSettings(string parameterFilePath);
    }
}
