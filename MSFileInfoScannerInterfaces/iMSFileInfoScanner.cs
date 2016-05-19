using System;

[assembly: CLSCompliant(true)]
namespace MSFileInfoScannerInterfaces
{
    public abstract class iMSFileInfoScanner
    {
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

        public enum eMSFileProcessingStateConstants
        {
            NotProcessed = 0,
            SkippedSinceFoundInCache = 1,
            FailedProcessing = 2,
            ProcessedSuccessfully = 3
        }

        public enum eDataFileTypeConstants
        {
            MSFileInfo = 0,
            FolderIntegrityInfo = 1,
            FileIntegrityDetails = 2,
            FileIntegrityErrors = 3
        }

        public abstract event MessageEventEventHandler MessageEvent;
        public delegate void MessageEventEventHandler(string Message);

        public abstract event ErrorEventEventHandler ErrorEvent;
        public delegate void ErrorEventEventHandler(string Message);

        public abstract bool AbortProcessing { get; set; }
        public abstract string AcquisitionTimeFilename { get; set; }
        public abstract bool CheckFileIntegrity { get; set; }
        public abstract bool ComputeOverallQualityScores { get; set; }
        public abstract string DatasetInfoXML { get; }
        public abstract string GetDataFileFilename(eDataFileTypeConstants eDataFileType);
        public abstract void SetDataFileFilename(string strFilePath, eDataFileTypeConstants eDataFileType);
        public abstract bool CheckCentroidingStatus { get; set; }
        public abstract bool ComputeFileHashes { get; set; }
        public abstract bool CopyFileLocalOnReadError { get; set; }
        public abstract bool CreateDatasetInfoFile { get; set; }
        public abstract bool CreateScanStatsFile { get; set; }
        public abstract int DatasetIDOverride { get; set; }
        public abstract string DatasetStatsTextFileName { get; set; }
        public abstract string DSInfoConnectionString { get; set; }
        public abstract bool DSInfoDBPostingEnabled { get; set; }
        public abstract string DSInfoStoredProcedure { get; set; }
        public abstract eMSFileScannerErrorCodes ErrorCode { get; }
        public abstract bool IgnoreErrorsWhenRecursing { get; set; }
        public abstract float LCMS2DPlotMZResolution { get; set; }
        public abstract int LCMS2DPlotMaxPointsToPlot { get; set; }
        public abstract int LCMS2DOverviewPlotDivisor { get; set; }
        public abstract int LCMS2DPlotMinPointsPerSpectrum { get; set; }
        public abstract float LCMS2DPlotMinIntensity { get; set; }
        public abstract bool LogMessagesToFile { get; set; }
        public abstract string LogFilePath { get; set; }
        public abstract string LogFolderPath { get; set; }
        public abstract int MaximumTextFileLinesToCheck { get; set; }
        public abstract int MaximumXMLElementNodesToCheck { get; set; }
        public abstract bool RecheckFileIntegrityForExistingFolders { get; set; }
        public abstract bool ReprocessExistingFiles { get; set; }
        public abstract bool ReprocessIfCachedSizeIsZero { get; set; }
        public abstract bool SaveTICAndBPIPlots { get; set; }
        public abstract bool SaveLCMS2DPlots { get; set; }
        public abstract int ScanStart { get; set; }
        public abstract int ScanEnd { get; set; }
        public abstract bool ShowDebugInfo { get; set; }
        public abstract bool UpdateDatasetStatsTextFile { get; set; }
        public abstract bool UseCacheFiles { get; set; }
        public abstract bool ZipFileCheckAllData { get; set; }
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
                                                   ref eMSFileProcessingStateConstants eMSFileProcessingState);

        public abstract bool ProcessMSFileOrFolderWildcard(string strInputFileOrFolderPath, string strOutputFolderPath, bool blnResetErrorCode);

        public abstract bool ProcessMSFilesAndRecurseFolders(string strInputFilePathOrFolder, string strOutputFolderPath,
                                                             int intRecurseFoldersMaxLevels);

        public abstract bool SaveCachedResults();
        public abstract bool SaveCachedResults(bool blnClearCachedData);
        public abstract bool SaveParameterFileSettings(string strParameterFilePath);
    }
}
