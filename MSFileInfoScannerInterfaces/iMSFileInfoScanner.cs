using PRISM;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace MSFileInfoScannerInterfaces
{
    // ReSharper disable once InconsistentNaming
    public abstract class iMSFileInfoScanner : EventNotifier
    {
        // Ignore Spelling: lcms

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
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public virtual string DatasetInfoXML { get; protected set; }

        public virtual MSFileScannerErrorCodes ErrorCode { get; protected set; }

        /// <summary>
        /// 2D Plotting options
        /// </summary>
        public virtual LCMSDataPlotterOptions LCMS2DPlotOptions { get; protected set; }

        /// <summary>
        /// MS2MzMin validation error or warning message
        /// </summary>
        public virtual string MS2MzMinValidationMessage { get; protected set; }

        /// <summary>
        /// Processing options
        /// </summary>
        public virtual InfoScannerOptions Options { get; protected set; }

        public abstract string[] GetKnownDirectoryExtensions();

        public abstract string[] GetKnownFileExtensions();

        public abstract string GetErrorMessage();

        public abstract bool LoadParameterFileSettings(string parameterFilePath);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetName);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="datasetInfoXML">Database info XML</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetName, string datasetInfoXML);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetName, string connectionString, string storedProcedureName);

        /// <summary>
        /// Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="dsInfoXML">Database info XML</param>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="storedProcedureName">Stored procedure</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetName, string dsInfoXML, string connectionString, string storedProcedureName);

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
