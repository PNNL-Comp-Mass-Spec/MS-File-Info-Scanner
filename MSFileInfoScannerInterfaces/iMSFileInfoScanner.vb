<Assembly: CLSCompliant(True)> 
Public Interface iMSFileInfoScanner

#Region "Constants and Enums"

	Enum eMSFileScannerErrorCodes
		NoError = 0
		InvalidInputFilePath = 1
		InvalidOutputFolderPath = 2
		ParameterFileNotFound = 4
		FilePathError = 8

		ParameterFileReadError = 16
		UnknownFileExtension = 32
		InputFileAccessError = 64
		InputFileReadError = 128
		OutputFileWriteError = 256
		FileIntegrityCheckError = 512

		DatabasePostingError = 1024

		UnspecifiedError = -1
	End Enum

	Enum eMSFileProcessingStateConstants
		NotProcessed = 0
		SkippedSinceFoundInCache = 1
		FailedProcessing = 2
		ProcessedSuccessfully = 3
	End Enum

	Enum eDataFileTypeConstants
		MSFileInfo = 0
		FolderIntegrityInfo = 1
		FileIntegrityDetails = 2
		FileIntegrityErrors = 3
	End Enum

#End Region

	Event MessageEvent(ByVal Message As String)
	Event ErrorEvent(ByVal Message As String)

	Property AbortProcessing() As Boolean
	Property AcquisitionTimeFilename() As String
	Property CheckFileIntegrity() As Boolean
	Property ComputeOverallQualityScores() As Boolean
	ReadOnly Property DatasetInfoXML() As String

	Function GetDataFileFilename(ByVal eDataFileType As eDataFileTypeConstants) As String
	Sub SetDataFileFilename(ByVal strFilePath As String, ByVal eDataFileType As eDataFileTypeConstants)

	Property ComputeFileHashes() As Boolean
	Property CopyFileLocalOnReadError() As Boolean
	Property CreateDatasetInfoFile() As Boolean
	Property CreateScanStatsFile() As Boolean
	Property DatasetStatsTextFileName() As String
	Property DSInfoConnectionString() As String
	Property DSInfoDBPostingEnabled() As Boolean
	Property DSInfoStoredProcedure() As String
	ReadOnly Property ErrorCode() As eMSFileScannerErrorCodes
	Property IgnoreErrorsWhenRecursing() As Boolean
	Property LCMS2DPlotMZResolution() As Single
	Property LCMS2DPlotMaxPointsToPlot() As Integer
	Property LCMS2DOverviewPlotDivisor() As Integer
	Property LCMS2DPlotMinPointsPerSpectrum() As Integer
	Property LCMS2DPlotMinIntensity() As Single
	Property LogMessagesToFile() As Boolean
	Property LogFilePath() As String
	Property LogFolderPath() As String
	Property MaximumTextFileLinesToCheck() As Integer
	Property MaximumXMLElementNodesToCheck() As Integer
	Property RecheckFileIntegrityForExistingFolders() As Boolean
	Property ReprocessExistingFiles() As Boolean
	Property ReprocessIfCachedSizeIsZero() As Boolean
	Property SaveTICAndBPIPlots() As Boolean
	Property SaveLCMS2DPlots() As Boolean
	Property ScanStart() As Integer
	Property ScanEnd() As Integer
	Property UpdateDatasetStatsTextFile() As Boolean
	Property UseCacheFiles() As Boolean
	Property ZipFileCheckAllData() As Boolean

	Function GetKnownFileExtensions() As String()
	Function GetKnownFolderExtensions() As String()
	Function GetErrorMessage() As String
	Function LoadParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

	Function PostDatasetInfoToDB() As Boolean
	Function PostDatasetInfoToDB(ByVal strDatasetInfoXML As String) As Boolean
	Function PostDatasetInfoToDB(ByVal strConnectionString As String, ByVal strStoredProcedure As String) As Boolean
	Function PostDatasetInfoToDB(ByVal strDatasetInfoXML As String, ByVal strConnectionString As String, ByVal strStoredProcedure As String) As Boolean

	Function PostDatasetInfoUseDatasetID(ByVal intDatasetID As Integer, ByVal strConnectionString As String, ByVal strStoredProcedure As String) As Boolean
	Function PostDatasetInfoUseDatasetID(ByVal intDatasetID As Integer, ByVal strDatasetInfoXML As String, ByVal strConnectionString As String, ByVal strStoredProcedure As String) As Boolean

	Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String) As Boolean
	Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean, ByRef eMSFileProcessingState As eMSFileProcessingStateConstants) As Boolean

	Function ProcessMSFileOrFolderWildcard(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean) As Boolean
	Function ProcessMSFilesAndRecurseFolders(ByVal strInputFilePathOrFolder As String, ByVal strOutputFolderPath As String, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean

	Function SaveCachedResults() As Boolean
	Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean

	Function SaveParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

End Interface