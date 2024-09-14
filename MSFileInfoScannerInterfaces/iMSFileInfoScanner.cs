using PRISM;
using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// MS File Info Scanner interface
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public abstract class iMSFileInfoScanner : EventNotifier
    {
        // Ignore Spelling: lcms

        /// <summary>
        /// MSFileInfoScanner error codes
        /// </summary>
        public enum MSFileScannerErrorCodes
        {
            /// <summary>
            /// No error
            /// </summary>
            NoError = 0,

            /// <summary>
            /// Invalid input file path
            /// </summary>
            InvalidInputFilePath = 1,

            /// <summary>
            /// Invalid output directory path
            /// </summary>
            InvalidOutputDirectoryPath = 2,

            /// <summary>
            /// Parameter file not found
            /// </summary>
            ParameterFileNotFound = 3,

            /// <summary>
            /// File path error
            /// </summary>
            FilePathError = 4,

            /// <summary>
            /// Parameter file read error
            /// </summary>
            ParameterFileReadError = 5,

            /// <summary>
            /// Unknown file extension
            /// </summary>
            UnknownFileExtension = 6,

            /// <summary>
            /// Input file access error
            /// </summary>
            InputFileAccessError = 7,

            /// <summary>
            /// Input file read error
            /// </summary>
            InputFileReadError = 8,

            /// <summary>
            /// Output file write error
            /// </summary>
            OutputFileWriteError = 9,

            /// <summary>
            /// File integrity check error
            /// </summary>
            FileIntegrityCheckError = 10,

            /// <summary>
            /// Database posting error
            /// </summary>
            DatabasePostingError = 11,

            /// <summary>
            /// MS2 m/z minimum validation error
            /// </summary>
            MS2MzMinValidationError = 12,

            /// <summary>
            /// MS2 m/z minimum validation warning
            /// </summary>
            MS2MzMinValidationWarning = 13,

            /// <summary>
            /// Thermo .raw file reader error
            /// </summary>
            ThermoRawFileReaderError = 14,

            /// <summary>
            /// Dataset has not spectra
            /// </summary>
            DatasetHasNoSpectra = 15,

            /// <summary>
            /// Unspecified error
            /// </summary>
            UnspecifiedError = -1
        }

        /// <summary>
        /// Processing state constants
        /// </summary>
        public enum MSFileProcessingStateConstants
        {
            /// <summary>
            /// Not processed
            /// </summary>
            NotProcessed = 0,

            /// <summary>
            /// Skipped since found in cache
            /// </summary>
            SkippedSinceFoundInCache = 1,

            /// <summary>
            /// Failed processing
            /// </summary>
            FailedProcessing = 2,

            /// <summary>
            /// Successfully processed
            /// </summary>
            ProcessedSuccessfully = 3
        }

        /// <summary>
        /// Data file type constants
        /// </summary>
        public enum DataFileTypeConstants
        {
            /// <summary>
            /// MS file info
            /// </summary>
            MSFileInfo = 0,

            /// <summary>
            /// Directory integrity info
            /// </summary>
            DirectoryIntegrityInfo = 1,

            /// <summary>
            /// File integrity details
            /// </summary>
            FileIntegrityDetails = 2,

            /// <summary>
            /// File integrity errors
            /// </summary>
            FileIntegrityErrors = 3
        }

        /// <summary>
        /// The calling process can set this to true to abort processing
        /// </summary>
        public virtual bool AbortProcessing { get; set; }

        /// <summary>
        /// Acquisition time file name
        /// </summary>
        public virtual string AcquisitionTimeFilename { get; set; }

        /// <summary>
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public virtual string DatasetInfoXML { get; protected set; }

        /// <summary>
        /// Error code
        /// </summary>
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

        /// <summary>
        /// Obtain the list of known directory extensions (as an array)
        /// </summary>
        /// <returns>List of directory extensions</returns>
        public abstract string[] GetKnownDirectoryExtensions();

        /// <summary>
        /// Obtain the list of known file extensions (as an array)
        /// </summary>
        /// <returns>List of file extensions</returns>
        public abstract string[] GetKnownFileExtensions();

        /// <summary>
        /// Get the error message, or an empty string if no error
        /// </summary>
        /// <returns>Error message, or empty string</returns>
        public abstract string GetErrorMessage();

        /// <summary>
        /// Read settings from a Key=Value parameter file
        /// </summary>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if successful, false if an error</returns>
        public abstract bool LoadParameterFileSettings(string parameterFilePath);

        /// <summary>
        /// Post the most recently determined dataset into XML to the database
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <returns>True if success; false if failure</returns>
        public abstract bool PostDatasetInfoToDB(string datasetName);

        /// <summary>
        /// Post the most recently determine dataset into XML to the database, using the given dataset name
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
        /// <param name="inputFileOrDirectoryPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFileOrDirectory(string inputFileOrDirectoryPath, string outputDirectoryPath);

        /// <summary>
        /// Main processing function with input / output paths, error code reset flag, and processing state
        /// </summary>
        /// <param name="inputFileOrDirectoryPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
        /// <param name="msFileProcessingState">MS file processing state</param>
        /// <returns>True if success, False if an error</returns>
        public abstract bool ProcessMSFileOrDirectory(string inputFileOrDirectoryPath, string outputDirectoryPath, bool resetErrorCode,
                                                      out MSFileProcessingStateConstants msFileProcessingState);

        /// <summary>
        /// Calls ProcessMSFileOrDirectory for all files in inputFileOrDirectoryPath and below having a known extension
        /// </summary>
        /// <param name="inputFileOrDirectoryPath">Path to the input file or directory; can contain a wildcard (* or ?)</param>
        /// <param name="outputDirectoryPath">Directory to write any results files to</param>
        /// <param name="resetErrorCode">If true, reset the error code</param>
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

        /// <summary>
        /// Save cached results now
        /// </summary>
        /// <returns>True if successful, false if an error</returns>
        public abstract bool SaveCachedResults();

        /// <summary>
        /// Save cached results now
        /// </summary>
        /// <param name="clearCachedData">When true, clear cached data</param>
        /// <returns>True if successful, false if an error</returns>
        public abstract bool SaveCachedResults(bool clearCachedData);

        /// <summary>
        /// Save parameter file settings
        /// </summary>
        /// <param name="parameterFilePath">Parameter file path</param>
        /// <returns>True if successful, false if an error</returns>
        public abstract bool SaveParameterFileSettings(string parameterFilePath);
    }
}
