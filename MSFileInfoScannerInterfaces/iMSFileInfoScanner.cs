
using PRISM;

namespace MSFileInfoScannerInterfaces
{
    public abstract class iMSFileInfoScanner : clsEventNotifier
    {
        #region "Enums"

        /// <summary>
        /// MSFileInfoScanner error codes
        /// </summary>
        public enum eMSFileScannerErrorCodes
        {
            NoError = 0,
            InvalidInputFilePath = 1,
            InvalidOutputFolderPath = 2,
            ParameterFileNotFound = 4,
            FilePathError = 8,

            ParameterFileReadError = 16,
            UnknownFileExtension = 32,
            InputFileAccessError = 64,
            InputFileReadError = 128,
            OutputFileWriteError = 256,
            FileIntegrityCheckError = 512,

            DatabasePostingError = 1024,

            UnspecifiedError = -1
        }

        /// <summary>
        /// Processing state constants
        /// </summary>
        public enum eMSFileProcessingStateConstants
        {
            NotProcessed = 0,
            SkippedSinceFoundInCache = 1,
            FailedProcessing = 2,
            ProcessedSuccessfully = 3
        }

        /// <summary>
        /// Data file type constants
        /// </summary>
        public enum eDataFileTypeConstants
        {
            MSFileInfo = 0,
            FolderIntegrityInfo = 1,
            FileIntegrityDetails = 2,
            FileIntegrityErrors = 3
        }

        #endregion

        #region "Properties"

        public virtual bool AbortProcessing { get; set; }

        public virtual string AcquisitionTimeFilename { get; set; }

        public virtual bool CheckFileIntegrity { get; set; }

        public virtual bool ComputeOverallQualityScores { get; set; }

        /// <summary>
        /// Returns the dataset info, formatted as XML
        /// </summary>
        public virtual string DatasetInfoXML { get; set;  }

        public abstract string GetDataFileFilename(eDataFileTypeConstants eDataFileType);

        public abstract void SetDataFileFilename(string strFilePath, eDataFileTypeConstants eDataFileType);

        public virtual bool CheckCentroidingStatus { get; set; }

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

        public virtual eMSFileScannerErrorCodes ErrorCode { get; set; }

        public virtual bool IgnoreErrorsWhenRecursing { get; set; }

        public virtual float LCMS2DPlotMZResolution { get; set; }

        public virtual int LCMS2DPlotMaxPointsToPlot { get; set; }

        public virtual int LCMS2DOverviewPlotDivisor { get; set; }

        public virtual int LCMS2DPlotMinPointsPerSpectrum { get; set; }

        public virtual float LCMS2DPlotMinIntensity { get; set; }

        public virtual bool LogMessagesToFile { get; set; }

        public virtual string LogFilePath { get; set; }

        public virtual string LogFolderPath { get; set; }

        public virtual int MaximumTextFileLinesToCheck { get; set; }

        public virtual int MaximumXMLElementNodesToCheck { get; set; }

        /// <summary>
        /// When true, generate plots using Python
        /// </summary>
        public virtual bool PlotWithPython { get; set; }

        public virtual bool RecheckFileIntegrityForExistingFolders { get; set; }

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
        /// If True, saves/loads data from/to the cache files (DatasetTimeFile.txt and FolderIntegrityInfo.txt)
        /// If you simply want to create TIC and BPI files, and/or the _DatasetInfo.xml file for a single dataset, set this to False
        /// </summary>
        /// <remarks>If this is false, data is not loaded/saved from/to the DatasetTimeFile.txt or the FolderIntegrityInfo.txt file</remarks>
        public virtual bool UseCacheFiles { get; set; }

        public virtual bool ZipFileCheckAllData { get; set; }

        #endregion

        #region "Methods"
        public abstract string[] GetKnownFileExtensions();
        public abstract string[] GetKnownFolderExtensions();
        public abstract string GetErrorMessage();
        public abstract bool LoadParameterFileSettings(string strParameterFilePath);
        public abstract bool PostDatasetInfoToDB();
        public abstract bool PostDatasetInfoToDB(string strDatasetInfoXML);
        public abstract bool PostDatasetInfoToDB(string strConnectionString, string strStoredProcedure);
        public abstract bool PostDatasetInfoToDB(string strDatasetInfoXML, string strConnectionString, string strStoredProcedure);
        public abstract bool PostDatasetInfoUseDatasetID(int intDatasetID, string strConnectionString, string strStoredProcedure);

        public abstract bool PostDatasetInfoUseDatasetID(int intDatasetID, string strDatasetInfoXML, string strConnectionString,
                                                         string strStoredProcedure);

        public abstract bool ProcessMSFileOrFolder(string strInputFileOrFolderPath, string strOutputFolderPath);

        public abstract bool ProcessMSFileOrFolder(string strInputFileOrFolderPath, string strOutputFolderPath, bool blnResetErrorCode,
                                                   out eMSFileProcessingStateConstants eMSFileProcessingState);

        public abstract bool ProcessMSFileOrFolderWildcard(string strInputFileOrFolderPath, string strOutputFolderPath, bool blnResetErrorCode);

        public abstract bool ProcessMSFilesAndRecurseFolders(string strInputFilePathOrFolder, string strOutputFolderPath,
                                                             int intRecurseFoldersMaxLevels);

        public abstract bool SaveCachedResults();
        public abstract bool SaveCachedResults(bool blnClearCachedData);
        public abstract bool SaveParameterFileSettings(string strParameterFilePath);

        #endregion

    }
}
