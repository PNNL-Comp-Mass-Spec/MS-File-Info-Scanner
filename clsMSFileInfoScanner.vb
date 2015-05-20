Option Strict On

' Scans a series of MS data files (or data folders) and extracts the acquisition start and end times, 
' number of spectra, and the total size of the   Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
'
' Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar/QTrap .WIFF files, 
' Masslynx .Raw folders, Bruker 1 folders, Bruker XMass analysis.baf files, and .UIMF files (IMS)
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started October 11, 2003
Imports MSFileInfoScannerInterfaces
Imports System.Data

Public Class clsMSFileInfoScanner
	Implements iMSFileInfoScanner

	Public Sub New()
        mFileDate = "May 20, 2015"

		mFileIntegrityChecker = New clsFileIntegrityChecker
		mMSFileInfoDataCache = New clsMSFileInfoDataCache

		InitializeLocalVariables()
	End Sub

#Region "Constants and Enums"
	Public Const DEFAULT_ACQUISITION_TIME_FILENAME_TXT As String = "DatasetTimeFile.txt"
	Public Const DEFAULT_ACQUISITION_TIME_FILENAME_XML As String = "DatasetTimeFile.xml"

	Public Const DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_TXT As String = "FolderIntegrityInfo.txt"
	Public Const DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_XML As String = "FolderIntegrityInfo.xml"

	Public Const DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT As String = "FileIntegrityDetails.txt"
	Public Const DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_XML As String = "FileIntegrityDetails.xml"

	Public Const DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT As String = "FileIntegrityErrors.txt"
	Public Const DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_XML As String = "FileIntegrityErrors.xml"

	Public Const ABORT_PROCESSING_FILENAME As String = "AbortProcessing.txt"
	Public Const XML_SECTION_MSFILESCANNER_SETTINGS As String = "MSFileInfoScannerSettings"

	Private Const FILE_MODIFICATION_WINDOW_MINUTES As Integer = 60
	Private Const MAX_FILE_READ_ACCESS_ATTEMPTS As Integer = 2
	Public Const USE_XML_OUTPUT_FILE As Boolean = False
	Private Const SKIP_FILES_IN_ERROR As Boolean = True

	'Public Enum iMSFileInfoScanner.eMSFileScannerErrorCodes
	'	NoError = 0
	'	InvalidInputFilePath = 1
	'	InvalidOutputFolderPath = 2
	'	ParameterFileNotFound = 4
	'	FilePathError = 8

	'	ParameterFileReadError = 16
	'	UnknownFileExtension = 32
	'	InputFileAccessError = 64
	'	InputFileReadError = 128
	'	OutputFileWriteError = 256
	'	FileIntegrityCheckError = 512

	'	DatabasePostingError = 1024

	'	UnspecifiedError = -1
	'End Enum

	'Public Enum iMSFileInfoScanner.eMSFileProcessingStateConstants
	'	NotProcessed = 0
	'	SkippedSinceFoundInCache = 1
	'	FailedProcessing = 2
	'	ProcessedSuccessfully = 3
	'End Enum

	'Public Enum eDataFileTypeConstants
	'	MSFileInfo = 0
	'	FolderIntegrityInfo = 1
	'	FileIntegrityDetails = 2
	'	FileIntegrityErrors = 3
	'End Enum

	''Private Enum eMSFileTypeConstants
	''    FinniganRawFile = 0
	''    BrukerOneFolder = 1
	''    AgilentIonTrapDFolder = 2
	''    MicromassRawFolder = 3
	''    AgilentOrQStarWiffFile = 4
	''End Enum

	Protected Enum eMessageTypeConstants
		Normal = 0
		ErrorMsg = 1
		Warning = 2
	End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

	Private mFileDate As String
	Private mErrorCode As iMSFileInfoScanner.eMSFileScannerErrorCodes

	Private mAbortProcessing As Boolean

	' If the following is false, then data is not loaded/saved from/to the DatasetTimeFile.txt or the FolderIntegrityInfo.txt file
	Private mUseCacheFiles As Boolean

	Private mFileIntegrityDetailsFilePath As String
	Private mFileIntegrityErrorsFilePath As String

	Private mIgnoreErrorsWhenRecursing As Boolean

	Private mReprocessExistingFiles As Boolean
	Private mReprocessIfCachedSizeIsZero As Boolean

	Private mRecheckFileIntegrityForExistingFolders As Boolean

	Private mSaveTICAndBPIPlots As Boolean
	Private mSaveLCMS2DPlots As Boolean
	Private mCheckCentroidingStatus As Boolean

	Private mComputeOverallQualityScores As Boolean
	Private mCreateDatasetInfoFile As Boolean
	Private mCreateScanStatsFile As Boolean

	Private mUpdateDatasetStatsTextFile As Boolean
	Private mDatasetStatsTextFileName As String

	Private mCopyFileLocalOnReadError As Boolean

	Private mCheckFileIntegrity As Boolean

	Private mDSInfoConnectionString As String
	Private mDSInfoDBPostingEnabled As Boolean
	Private mDSInfoStoredProcedure As String
	Private mDSInfoDatasetIDOverride As Integer

    Private mLCMS2DPlotOptions As clsLCMSDataPlotterOptions
	Private mLCMS2DOverviewPlotDivisor As Integer

	Private mScanStart As Integer
	Private mScanEnd As Integer
	Private mShowDebugInfo As Boolean

	Protected mLogMessagesToFile As Boolean
	Protected mLogFilePath As String
	Protected mLogFile As StreamWriter

	' This variable is updated in ProcessMSFileOrFolder
	Protected mOutputFolderPath As String
	Protected mLogFolderPath As String			' If blank, then mOutputFolderPath will be used; if mOutputFolderPath is also blank, then the log is created in the same folder as the executing assembly

	Protected mDatasetInfoXML As String = ""

	Private WithEvents mFileIntegrityChecker As clsFileIntegrityChecker
	Private mFileIntegrityDetailsWriter As StreamWriter
	Private mFileIntegrityErrorsWriter As StreamWriter

	Private WithEvents mMSInfoScanner As iMSFileInfoProcessor

	Private WithEvents mMSFileInfoDataCache As clsMSFileInfoDataCache

	Private WithEvents mExecuteSP As PRISM.DataBase.clsExecuteDatabaseSP

	Public Event MessageEvent(ByVal Message As String) Implements iMSFileInfoScanner.MessageEvent
	Public Event ErrorEvent(ByVal Message As String) Implements iMSFileInfoScanner.ErrorEvent

#End Region

#Region "Processing Options and Interface Functions"

	Public Property AbortProcessing() As Boolean Implements iMSFileInfoScanner.AbortProcessing
		Get
			Return mAbortProcessing
		End Get
		Set(ByVal value As Boolean)
			mAbortProcessing = value
		End Set
	End Property

	Public Property AcquisitionTimeFilename() As String Implements iMSFileInfoScanner.AcquisitionTimeFilename
		Get
			Return GetDataFileFilename(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo)
		End Get
		Set(ByVal value As String)
			SetDataFileFilename(value, iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo)
		End Set
	End Property

	Public Property CheckCentroidingStatus As Boolean Implements iMSFileInfoScanner.CheckCentroidingStatus
		Get
			Return mCheckCentroidingStatus
		End Get
		Set(value As Boolean)
			mCheckCentroidingStatus = value
		End Set
	End Property

	''' <summary>
	''' When true, then checks the integrity of every file in every folder processed
	''' </summary>
	Public Property CheckFileIntegrity() As Boolean Implements iMSFileInfoScanner.CheckFileIntegrity
		Get
			Return mCheckFileIntegrity
		End Get
		Set(ByVal value As Boolean)
			mCheckFileIntegrity = value
			If mCheckFileIntegrity Then
				' Make sure Cache Files are enabled
				Me.UseCacheFiles = True
			End If
		End Set
	End Property

	Public Property ComputeOverallQualityScores() As Boolean Implements iMSFileInfoScanner.ComputeOverallQualityScores
		Get
			Return mComputeOverallQualityScores
		End Get
		Set(ByVal value As Boolean)
			mComputeOverallQualityScores = value
		End Set
	End Property

	''' <summary>
	''' Returns the dataset info, formatted as XML
	''' </summary>
	Public ReadOnly Property DatasetInfoXML() As String Implements iMSFileInfoScanner.DatasetInfoXML
		Get
			Return mDatasetInfoXML
		End Get
	End Property

	Public Function GetDataFileFilename(ByVal eDataFileType As iMSFileInfoScanner.eDataFileTypeConstants) As String Implements iMSFileInfoScanner.GetDataFileFilename
		Select Case eDataFileType
			Case iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo
				Return mMSFileInfoDataCache.AcquisitionTimeFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo
				Return mMSFileInfoDataCache.FolderIntegrityInfoFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails
				Return mFileIntegrityDetailsFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors
				Return mFileIntegrityErrorsFilePath
			Case Else
				Return String.Empty
		End Select
	End Function

	Public Sub SetDataFileFilename(ByVal strFilePath As String, ByVal eDataFileType As iMSFileInfoScanner.eDataFileTypeConstants) Implements iMSFileInfoScanner.SetDataFileFilename
		Select Case eDataFileType
			Case iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo
				mMSFileInfoDataCache.AcquisitionTimeFilePath = strFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo
				mMSFileInfoDataCache.FolderIntegrityInfoFilePath = strFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails
				mFileIntegrityDetailsFilePath = strFilePath
			Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors
				mFileIntegrityErrorsFilePath = strFilePath
			Case Else
				' Unknown file type
		End Select
	End Sub

	Public Shared ReadOnly Property DefaultAcquisitionTimeFilename() As String
		Get
			Return DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo)
		End Get
	End Property

	Public Shared ReadOnly Property DefaultDataFileName(ByVal eDataFileType As iMSFileInfoScanner.eDataFileTypeConstants) As String
		Get
			If USE_XML_OUTPUT_FILE Then
				Select Case eDataFileType
					Case iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo
						Return DEFAULT_ACQUISITION_TIME_FILENAME_XML
					Case iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo
						Return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_XML
					Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails
						Return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_XML
					Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors
						Return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_XML
					Case Else
						Return "UnknownFileType.xml"
				End Select
			Else
				Select Case eDataFileType
					Case iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo
						Return DEFAULT_ACQUISITION_TIME_FILENAME_TXT
					Case iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo
						Return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_TXT
					Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails
						Return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT
					Case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors
						Return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_TXT
					Case Else
						Return "UnknownFileType.txt"
				End Select
			End If
		End Get
	End Property

	''' <summary>
	''' When True, then computes an Sha1 hash on every file
	''' </summary>
	Public Property ComputeFileHashes() As Boolean Implements iMSFileInfoScanner.ComputeFileHashes
		Get
			If Not mFileIntegrityChecker Is Nothing Then
				Return mFileIntegrityChecker.ComputeFileHashes
			Else
				Return False
			End If
		End Get
		Set(ByVal value As Boolean)
			If Not mFileIntegrityChecker Is Nothing Then
				mFileIntegrityChecker.ComputeFileHashes = value
			End If
		End Set
	End Property

	''' <summary>
	''' If True, then copies .Raw files to the local drive if unable to read the file over the network
	''' </summary>
	Public Property CopyFileLocalOnReadError() As Boolean Implements iMSFileInfoScanner.CopyFileLocalOnReadError
		Get
			Return mCopyFileLocalOnReadError
		End Get
		Set(ByVal value As Boolean)
			mCopyFileLocalOnReadError = value
		End Set
	End Property

	''' <summary>
	''' If True, then will create the _DatasetInfo.xml file
	''' </summary>   
	Public Property CreateDatasetInfoFile() As Boolean Implements iMSFileInfoScanner.CreateDatasetInfoFile
		Get
			Return mCreateDatasetInfoFile
		End Get
		Set(ByVal value As Boolean)
			mCreateDatasetInfoFile = value
		End Set
	End Property

	''' <summary>
	''' If True, then will create the _ScanStats.txt file
	''' </summary>
	Public Property CreateScanStatsFile() As Boolean Implements iMSFileInfoScanner.CreateScanStatsFile
		Get
			Return mCreateScanStatsFile
		End Get
		Set(value As Boolean)
			mCreateScanStatsFile = value
		End Set
	End Property

	''' <summary>
	''' DatasetID value to use instead of trying to lookup the value in DMS (and instead of using 0)
	''' </summary>
	Public Property DatasetIDOverride() As Integer Implements iMSFileInfoScanner.DatasetIDOverride
		Get
			Return mDSInfoDatasetIDOverride
		End Get
		Set(value As Integer)
			mDSInfoDatasetIDOverride = value
		End Set
	End Property

	Public Property DatasetStatsTextFileName() As String Implements iMSFileInfoScanner.DatasetStatsTextFileName
		Get
			Return mDatasetStatsTextFileName
		End Get
		Set(ByVal value As String)
			If String.IsNullOrEmpty(value) Then
				' Do not update mDatasetStatsTextFileName
			Else
				mDatasetStatsTextFileName = value
			End If
		End Set
	End Property

	Public Property DSInfoConnectionString() As String Implements iMSFileInfoScanner.DSInfoConnectionString
		Get
			Return mDSInfoConnectionString
		End Get
		Set(ByVal value As String)
			mDSInfoConnectionString = value
		End Set
	End Property

	Public Property DSInfoDBPostingEnabled() As Boolean Implements iMSFileInfoScanner.DSInfoDBPostingEnabled
		Get
			Return mDSInfoDBPostingEnabled
		End Get
		Set(ByVal value As Boolean)
			mDSInfoDBPostingEnabled = value
		End Set
	End Property

	Public Property DSInfoStoredProcedure() As String Implements iMSFileInfoScanner.DSInfoStoredProcedure
		Get
			Return mDSInfoStoredProcedure
		End Get
		Set(ByVal value As String)
			mDSInfoStoredProcedure = value
		End Set
	End Property

	Public ReadOnly Property ErrorCode() As iMSFileInfoScanner.eMSFileScannerErrorCodes Implements iMSFileInfoScanner.ErrorCode
		Get
			Return mErrorCode
		End Get
	End Property

	Public Property IgnoreErrorsWhenRecursing() As Boolean Implements iMSFileInfoScanner.IgnoreErrorsWhenRecursing
		Get
			Return mIgnoreErrorsWhenRecursing
		End Get
		Set(ByVal value As Boolean)
			mIgnoreErrorsWhenRecursing = value
		End Set
	End Property

	Public Property LCMS2DPlotMZResolution() As Single Implements iMSFileInfoScanner.LCMS2DPlotMZResolution
		Get
			Return mLCMS2DPlotOptions.MZResolution
		End Get
		Set(ByVal value As Single)
			mLCMS2DPlotOptions.MZResolution = value
		End Set
	End Property

	Public Property LCMS2DPlotMaxPointsToPlot() As Integer Implements iMSFileInfoScanner.LCMS2DPlotMaxPointsToPlot
		Get
			Return mLCMS2DPlotOptions.MaxPointsToPlot
		End Get
		Set(ByVal value As Integer)
			mLCMS2DPlotOptions.MaxPointsToPlot = value
		End Set
	End Property

	Public Property LCMS2DOverviewPlotDivisor() As Integer Implements iMSFileInfoScanner.LCMS2DOverviewPlotDivisor
		Get
			Return mLCMS2DOverviewPlotDivisor
		End Get
		Set(ByVal value As Integer)
			mLCMS2DOverviewPlotDivisor = value
		End Set
	End Property

	Public Property LCMS2DPlotMinPointsPerSpectrum() As Integer Implements iMSFileInfoScanner.LCMS2DPlotMinPointsPerSpectrum
		Get
			Return mLCMS2DPlotOptions.MinPointsPerSpectrum
		End Get
		Set(ByVal value As Integer)
			mLCMS2DPlotOptions.MinPointsPerSpectrum = value
		End Set
	End Property

	Public Property LCMS2DPlotMinIntensity() As Single Implements iMSFileInfoScanner.LCMS2DPlotMinIntensity
		Get
			Return mLCMS2DPlotOptions.MinIntensity
		End Get
		Set(ByVal value As Single)
			mLCMS2DPlotOptions.MinIntensity = value
		End Set
	End Property


	Public Property LogMessagesToFile() As Boolean Implements iMSFileInfoScanner.LogMessagesToFile
		Get
			Return mLogMessagesToFile
		End Get
		Set(ByVal value As Boolean)
			mLogMessagesToFile = value
		End Set
	End Property

	Public Property LogFilePath() As String Implements iMSFileInfoScanner.LogFilePath
		Get
			Return mLogFilePath
		End Get
		Set(ByVal value As String)
			mLogFilePath = value
		End Set
	End Property

	Public Property LogFolderPath() As String Implements iMSFileInfoScanner.LogFolderPath
		Get
			Return mLogFolderPath
		End Get
		Set(ByVal value As String)
			mLogFolderPath = value
		End Set
	End Property

	Public Property MaximumTextFileLinesToCheck() As Integer Implements iMSFileInfoScanner.MaximumTextFileLinesToCheck
		Get
			If Not mFileIntegrityChecker Is Nothing Then
				Return mFileIntegrityChecker.MaximumTextFileLinesToCheck
			Else
				Return 0
			End If
		End Get
		Set(ByVal value As Integer)
			If Not mFileIntegrityChecker Is Nothing Then
				mFileIntegrityChecker.MaximumTextFileLinesToCheck = value
			End If
		End Set
	End Property

	Public Property MaximumXMLElementNodesToCheck() As Integer Implements iMSFileInfoScanner.MaximumXMLElementNodesToCheck
		Get
			If Not mFileIntegrityChecker Is Nothing Then
				Return mFileIntegrityChecker.MaximumTextFileLinesToCheck
			Else
				Return 0
			End If
		End Get
		Set(ByVal value As Integer)
			If Not mFileIntegrityChecker Is Nothing Then
				mFileIntegrityChecker.MaximumTextFileLinesToCheck = value
			End If
		End Set
	End Property

	Public Property RecheckFileIntegrityForExistingFolders() As Boolean Implements iMSFileInfoScanner.RecheckFileIntegrityForExistingFolders
		Get
			Return mRecheckFileIntegrityForExistingFolders
		End Get
		Set(ByVal value As Boolean)
			mRecheckFileIntegrityForExistingFolders = value
		End Set
	End Property

	Public Property ReprocessExistingFiles() As Boolean Implements iMSFileInfoScanner.ReprocessExistingFiles
		Get
			Return mReprocessExistingFiles
		End Get
		Set(ByVal value As Boolean)
			mReprocessExistingFiles = value
		End Set
	End Property

	Public Property ReprocessIfCachedSizeIsZero() As Boolean Implements iMSFileInfoScanner.ReprocessIfCachedSizeIsZero
		Get
			Return mReprocessIfCachedSizeIsZero
		End Get
		Set(ByVal value As Boolean)
			mReprocessIfCachedSizeIsZero = value
		End Set
	End Property

	''' <summary>
	''' If True, then saves TIC and BPI plots as PNG files
	''' </summary>
	Public Property SaveTICAndBPIPlots() As Boolean Implements iMSFileInfoScanner.SaveTICAndBPIPlots
		Get
			Return mSaveTICAndBPIPlots
		End Get
		Set(ByVal value As Boolean)
			mSaveTICAndBPIPlots = value
		End Set
	End Property

	Public Property ShowDebugInfo As Boolean Implements iMSFileInfoScanner.ShowDebugInfo
		Get
			Return mShowDebugInfo
		End Get
		Set(value As Boolean)
			mShowDebugInfo = value
		End Set
	End Property

	''' <summary>
	''' If True, then saves a 2D plot of m/z vs. Intensity (requires reading every data point in the data file, which will slow down the processing)
	''' </summary>
	''' <value></value>
	Public Property SaveLCMS2DPlots() As Boolean Implements iMSFileInfoScanner.SaveLCMS2DPlots
		Get
			Return mSaveLCMS2DPlots
		End Get
		Set(ByVal value As Boolean)
			mSaveLCMS2DPlots = value
		End Set
	End Property

	''' <summary>
	''' When ScanStart is > 0, then will start processing at the specified scan number
	''' </summary>
	Public Property ScanStart() As Integer Implements iMSFileInfoScanner.ScanStart
		Get
			Return mScanStart
		End Get
		Set(ByVal value As Integer)
			mScanStart = value
		End Set
	End Property

	''' <summary>
	''' When ScanEnd is > 0, then will stop processing at the specified scan number
	''' </summary>
	Public Property ScanEnd() As Integer Implements iMSFileInfoScanner.ScanEnd
		Get
			Return mScanEnd
		End Get
		Set(ByVal value As Integer)
			mScanEnd = value
		End Set
	End Property

	Public Property UpdateDatasetStatsTextFile() As Boolean Implements iMSFileInfoScanner.UpdateDatasetStatsTextFile
		Get
			Return mUpdateDatasetStatsTextFile
		End Get
		Set(ByVal value As Boolean)
			mUpdateDatasetStatsTextFile = value
		End Set
	End Property

	''' <summary>
	''' If True, then saves/loads data from/to the cache files (DatasetTimeFile.txt and FolderIntegrityInfo.txt)
	''' If you simply want to create TIC and BPI files, and/or the _DatasetInfo.xml file for a single dataset, then set this to False
	''' </summary>
	Public Property UseCacheFiles() As Boolean Implements iMSFileInfoScanner.UseCacheFiles
		Get
			Return mUseCacheFiles
		End Get
		Set(ByVal value As Boolean)
			mUseCacheFiles = value
		End Set
	End Property

	Public Property ZipFileCheckAllData() As Boolean Implements iMSFileInfoScanner.ZipFileCheckAllData
		Get
			If Not mFileIntegrityChecker Is Nothing Then
				Return mFileIntegrityChecker.ZipFileCheckAllData
			Else
				Return False
			End If
		End Get
		Set(ByVal value As Boolean)
			If Not mFileIntegrityChecker Is Nothing Then
				mFileIntegrityChecker.ZipFileCheckAllData = value
			End If
		End Set
	End Property

#End Region

	Private Sub AddToStringList(ByRef strList() As String, ByVal strNewEntry As String, ByRef intListCount As Integer)
		If strList Is Nothing Then
			intListCount = 0
			ReDim strList(4)
		End If

		If intListCount >= strList.Length Then
			ReDim Preserve strList(strList.Length * 2 - 1)
		End If

		strList(intListCount) = strNewEntry
		intListCount += 1

	End Sub

	Private Sub AutosaveCachedResults()

		If mUseCacheFiles Then
			mMSFileInfoDataCache.AutosaveCachedResults()
		End If

	End Sub

	Private Sub CheckForAbortProcessingFile()
		Static dtLastCheckTime As DateTime

		Try
			If DateTime.UtcNow.Subtract(dtLastCheckTime).TotalSeconds < 15 Then
				Exit Sub
			End If

			dtLastCheckTime = DateTime.UtcNow

			If File.Exists(ABORT_PROCESSING_FILENAME) Then
				mAbortProcessing = True
				Try
					If File.Exists(ABORT_PROCESSING_FILENAME & ".done") Then
						File.Delete(ABORT_PROCESSING_FILENAME & ".done")
					End If
					File.Move(ABORT_PROCESSING_FILENAME, ABORT_PROCESSING_FILENAME & ".done")
				Catch ex As Exception
					' Ignore errors here
				End Try
			End If
		Catch ex As Exception
			' Ignore errors here
		End Try
	End Sub

	Private Sub CheckIntegrityOfFilesInFolder(ByVal strFolderPath As String, _
	   ByVal blnForceRecheck As Boolean, _
	   ByRef strProcessedFileList() As String)

		Dim intFileCount As Integer

		Dim objRow As DataRow = Nothing

		Dim intFolderID As Integer
		Dim intCachedFileCount As Integer
		Dim intCachedCountFailIntegrity As Integer

		Dim blnCheckFolder As Boolean

		Dim blnAllFilesAreValid As Boolean = True

		Dim udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType
		Dim udtFileStats() As clsFileIntegrityChecker.udtFileStatsType

		Try
			If mFileIntegrityDetailsWriter Is Nothing Then
				OpenFileIntegrityDetailsFile()
			End If

			Dim diFolderInfo = New DirectoryInfo(strFolderPath)
			intFileCount = diFolderInfo.GetFiles.Length

			If intFileCount > 0 Then
				blnCheckFolder = True
				If mUseCacheFiles AndAlso Not blnForceRecheck Then
					If mMSFileInfoDataCache.CachedFolderIntegrityInfoContainsFolder(diFolderInfo.FullName, intFolderID, objRow) Then
						intCachedFileCount = CInt(objRow(clsMSFileInfoDataCache.COL_NAME_FILE_COUNT))
						intCachedCountFailIntegrity = CInt(objRow(clsMSFileInfoDataCache.COL_NAME_COUNT_FAIL_INTEGRITY))

						If intCachedFileCount = intFileCount AndAlso intCachedCountFailIntegrity = 0 Then
							' Folder contains the same number of files as last time, and no files failed the integrity check last time
							' Do not recheck the folder
							blnCheckFolder = False
						End If
					End If
				End If

				If blnCheckFolder Then
					udtFolderStats = clsFileIntegrityChecker.GetNewFolderStats(diFolderInfo.FullName)
					ReDim udtFileStats(intFileCount - 1)

					blnAllFilesAreValid = mFileIntegrityChecker.CheckIntegrityOfFilesInFolder(strFolderPath, udtFolderStats, udtFileStats, strProcessedFileList)

					If mUseCacheFiles Then
						If Not mMSFileInfoDataCache.UpdateCachedFolderIntegrityInfo(udtFolderStats, intFolderID) Then
							intFolderID = -1
						End If
					End If

					WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, intFolderID, udtFileStats)

				End If
			End If

		Catch ex As Exception
			HandleException("Error calling mFileIntegrityChecker", ex)
		End Try

	End Sub

	Public Shared Function GetAppFolderPath() As String
		' Could use Application.StartupPath, but .GetExecutingAssembly is better
		Return Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
	End Function

	Public Function GetKnownFileExtensions() As String() Implements iMSFileInfoScanner.GetKnownFileExtensions
		Return GetKnownFileExtensionsList.ToArray()
	End Function

    Public Function GetKnownFileExtensionsList() As List(Of String)
        Dim lstExtensionsToParse As List(Of String) = New List(Of String)

        lstExtensionsToParse.Add(clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION)
        lstExtensionsToParse.Add(clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION)
        lstExtensionsToParse.Add(clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION)
        lstExtensionsToParse.Add(clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION)
        lstExtensionsToParse.Add(clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION)
        lstExtensionsToParse.Add(clsUIMFInfoScanner.UIMF_FILE_EXTENSION)
        lstExtensionsToParse.Add(clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION)

        Return lstExtensionsToParse
    End Function

    Public Function GetKnownFolderExtensions() As String() Implements iMSFileInfoScanner.GetKnownFolderExtensions
        Return GetKnownFolderExtensionsList.ToArray()
    End Function

    Public Function GetKnownFolderExtensionsList() As List(Of String)
        Dim lstExtensionsToParse As List(Of String) = New List(Of String)

        lstExtensionsToParse.Add(clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION)
        lstExtensionsToParse.Add(clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION)

        Return lstExtensionsToParse
    End Function


	Public Function GetErrorMessage() As String Implements iMSFileInfoScanner.GetErrorMessage
		' Returns String.Empty if no error

		Dim strErrorMessage As String

		Select Case mErrorCode
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError
				strErrorMessage = String.Empty
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.InvalidInputFilePath
				strErrorMessage = "Invalid input file path"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.InvalidOutputFolderPath
				strErrorMessage = "Invalid output folder path"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.ParameterFileNotFound
				strErrorMessage = "Parameter file not found"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.FilePathError
				strErrorMessage = "General file path error"

			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.ParameterFileReadError
				strErrorMessage = "Parameter file read error"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.UnknownFileExtension
				strErrorMessage = "Unknown file extension"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.InputFileReadError
				strErrorMessage = "Input file read error"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.InputFileAccessError
				strErrorMessage = "Input file access error"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.OutputFileWriteError
				strErrorMessage = "Error writing output file"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.FileIntegrityCheckError
				strErrorMessage = "Error checking file integrity"
			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError
				strErrorMessage = "Database posting error"

			Case iMSFileInfoScanner.eMSFileScannerErrorCodes.UnspecifiedError
				strErrorMessage = "Unspecified localized error"

			Case Else
				' This shouldn't happen
				strErrorMessage = "Unknown error state"
		End Select

		Return strErrorMessage

	End Function

	Private Function GetFileOrFolderInfo(ByVal strFileOrFolderPath As String, ByRef blnIsFolder As Boolean, ByRef objFileSystemInfo As FileSystemInfo) As Boolean

		Dim blnExists As Boolean

		' See if strFileOrFolderPath points to a valid file
		Dim fiFileInfo = New FileInfo(strFileOrFolderPath)

		If fiFileInfo.Exists() Then
			objFileSystemInfo = fiFileInfo
			blnExists = True
			blnIsFolder = False
		Else
			' See if strFileOrFolderPath points to a folder
			Dim diFolderInfo = New DirectoryInfo(strFileOrFolderPath)
			If diFolderInfo.Exists Then
				objFileSystemInfo = diFolderInfo
				blnExists = True
				blnIsFolder = True
			Else
				blnExists = False
			End If
		End If

		Return blnExists

	End Function

	Protected Sub HandleException(ByVal strBaseMessage As String, ByVal ex As Exception)
		If strBaseMessage Is Nothing OrElse strBaseMessage.Length = 0 Then
			strBaseMessage = "Error"
		End If

		' Note that ShowErrorMessage() will call LogMessage()
		ShowErrorMessage(strBaseMessage & ": " & ex.Message, True)

	End Sub

	Private Sub LoadCachedResults(ByVal blnForceLoad As Boolean)
		If mUseCacheFiles Then
			mMSFileInfoDataCache.LoadCachedResults(blnForceLoad)
		End If
	End Sub

	Protected Sub LogMessage(ByVal strMessage As String)
		LogMessage(strMessage, eMessageTypeConstants.Normal)
	End Sub

	Protected Sub LogMessage(ByVal strMessage As String, ByVal eMessageType As eMessageTypeConstants)
		' Note that ProcessMSFileOrFolder() will update mOutputFolderPath, which is used here if mLogFolderPath is blank

		Dim strMessageType As String
		Dim blnOpeningExistingFile As Boolean = False

		If mLogFile Is Nothing AndAlso mLogMessagesToFile Then
			Try
				If mLogFilePath Is Nothing OrElse mLogFilePath.Length = 0 Then
					' Auto-name the log file
					mLogFilePath = Path.GetFileNameWithoutExtension(Reflection.Assembly.GetExecutingAssembly().Location)
					mLogFilePath &= "_log_" & DateTime.Now.ToString("yyyy-MM-dd") & ".txt"
				End If

				Try
					If mLogFolderPath Is Nothing Then mLogFolderPath = String.Empty

					If mLogFolderPath.Length = 0 Then
						' Log folder is undefined; use mOutputFolderPath if it is defined
						If Not mOutputFolderPath Is Nothing AndAlso mOutputFolderPath.Length > 0 Then
							mLogFolderPath = String.Copy(mOutputFolderPath)
						End If
					End If

					If mLogFolderPath.Length > 0 Then
						' Create the log folder if it doesn't exist
						If Not Directory.Exists(mLogFolderPath) Then
							Directory.CreateDirectory(mLogFolderPath)
						End If
					End If
				Catch ex As Exception
					mLogFolderPath = String.Empty
				End Try

				If mLogFolderPath.Length > 0 Then
					mLogFilePath = Path.Combine(mLogFolderPath, mLogFilePath)
				End If

				blnOpeningExistingFile = File.Exists(mLogFilePath)

				mLogFile = New StreamWriter(New FileStream(mLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
				mLogFile.AutoFlush = True

				If Not blnOpeningExistingFile Then
					mLogFile.WriteLine("Date" & ControlChars.Tab & _
					 "Type" & ControlChars.Tab & _
					 "Message")
				End If

			Catch ex As Exception
				' Error creating the log file; set mLogMessagesToFile to false so we don't repeatedly try to create it
				mLogMessagesToFile = False
			End Try

		End If

		If Not mLogFile Is Nothing Then
			Select Case eMessageType
				Case eMessageTypeConstants.Normal
					strMessageType = "Normal"
				Case eMessageTypeConstants.ErrorMsg
					strMessageType = "Error"
				Case eMessageTypeConstants.Warning
					strMessageType = "Warning"
				Case Else
					strMessageType = "Unknown"
			End Select

			mLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & _
			 strMessageType & ControlChars.Tab & _
			 strMessage)
		End If

	End Sub

	Private Sub InitializeLocalVariables()
		mErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError

		mIgnoreErrorsWhenRecursing = False

		mUseCacheFiles = False

		mLogMessagesToFile = False
		mLogFilePath = String.Empty
		mLogFolderPath = String.Empty

		mReprocessExistingFiles = False
		mReprocessIfCachedSizeIsZero = False
		mRecheckFileIntegrityForExistingFolders = False

		mCreateDatasetInfoFile = False

		mUpdateDatasetStatsTextFile = False
		mDatasetStatsTextFileName = DSSummarizer.clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME

		mSaveTICAndBPIPlots = False
		mSaveLCMS2DPlots = False
		mCheckCentroidingStatus = False

        mLCMS2DPlotOptions = New clsLCMSDataPlotterOptions
        mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR

		mScanStart = 0
		mScanEnd = 0
		mShowDebugInfo = False

		mComputeOverallQualityScores = False

		mCopyFileLocalOnReadError = False

		mCheckFileIntegrity = False

		mDSInfoConnectionString = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;"
		mDSInfoDBPostingEnabled = False
		mDSInfoStoredProcedure = "UpdateDatasetFileInfoXML"
		mDSInfoDatasetIDOverride = 0

		mFileIntegrityDetailsFilePath = Path.Combine(GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails))
		mFileIntegrityErrorsFilePath = Path.Combine(GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors))

		mMSFileInfoDataCache.InitializeVariables()

	End Sub

	Public Function LoadParameterFileSettings(ByVal strParameterFilePath As String) As Boolean Implements iMSFileInfoScanner.LoadParameterFileSettings

		Dim objSettingsFile As New XmlSettingsFileAccessor

		Try

			If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
				' No parameter file specified; nothing to load
				Return True
			End If

			If Not File.Exists(strParameterFilePath) Then
				' See if strParameterFilePath points to a file in the same directory as the application
				strParameterFilePath = Path.Combine(GetAppFolderPath(), Path.GetFileName(strParameterFilePath))
				If Not File.Exists(strParameterFilePath) Then
					ShowErrorMessage("Parameter file not found: " & strParameterFilePath)
					SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.ParameterFileNotFound)
					Return False
				End If
			End If

			' Pass False to .LoadSettings() here to turn off case sensitive matching
			If objSettingsFile.LoadSettings(strParameterFilePath, False) Then
				With objSettingsFile

					If Not .SectionPresent(XML_SECTION_MSFILESCANNER_SETTINGS) Then
						' MS File Scanner section not found; that's ok
						ShowMessage("Warning: Parameter file " & strParameterFilePath & " does not have section """ & XML_SECTION_MSFILESCANNER_SETTINGS & """", eMessageTypeConstants.Warning)
					Else
						Me.DSInfoConnectionString = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoConnectionString", Me.DSInfoConnectionString)
						Me.DSInfoDBPostingEnabled = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoDBPostingEnabled", Me.DSInfoDBPostingEnabled)
						Me.DSInfoStoredProcedure = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DSInfoStoredProcedure", Me.DSInfoStoredProcedure)

						Me.LogMessagesToFile = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogMessagesToFile", Me.LogMessagesToFile)
						Me.LogFilePath = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFilePath", Me.LogFilePath)
						Me.LogFolderPath = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LogFolderPath", Me.LogFolderPath)

						Me.UseCacheFiles = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UseCacheFiles", Me.UseCacheFiles)
						Me.ReprocessExistingFiles = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessExistingFiles", Me.ReprocessExistingFiles)
						Me.ReprocessIfCachedSizeIsZero = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ReprocessIfCachedSizeIsZero", Me.ReprocessIfCachedSizeIsZero)

						Me.CopyFileLocalOnReadError = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CopyFileLocalOnReadError", Me.CopyFileLocalOnReadError)

						Me.SaveTICAndBPIPlots = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveTICAndBPIPlots", Me.SaveTICAndBPIPlots)
						Me.SaveLCMS2DPlots = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveLCMS2DPlots", Me.SaveLCMS2DPlots)
						Me.CheckCentroidingStatus = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckCentroidingStatus", Me.CheckCentroidingStatus)

						Me.LCMS2DPlotMZResolution = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMZResolution", Me.LCMS2DPlotMZResolution)
						Me.LCMS2DPlotMinPointsPerSpectrum = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinPointsPerSpectrum", Me.LCMS2DPlotMinPointsPerSpectrum)

						Me.LCMS2DPlotMaxPointsToPlot = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMaxPointsToPlot", Me.LCMS2DPlotMaxPointsToPlot)
						Me.LCMS2DPlotMinIntensity = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinIntensity", Me.LCMS2DPlotMinIntensity)

						Me.LCMS2DOverviewPlotDivisor = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DOverviewPlotDivisor", Me.LCMS2DOverviewPlotDivisor)

						Me.ScanStart = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanStart", Me.ScanStart)
						Me.ScanEnd = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ScanEnd", Me.ScanEnd)

						Me.ComputeOverallQualityScores = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeOverallQualityScores", Me.ComputeOverallQualityScores)
						Me.CreateDatasetInfoFile = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateDatasetInfoFile", Me.CreateDatasetInfoFile)
						Me.CreateScanStatsFile = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateScanStatsFile", Me.CreateScanStatsFile)

						Me.UpdateDatasetStatsTextFile = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "UpdateDatasetStatsTextFile", Me.UpdateDatasetStatsTextFile)
						Me.DatasetStatsTextFileName = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "DatasetStatsTextFileName", Me.DatasetStatsTextFileName)

						Me.CheckFileIntegrity = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CheckFileIntegrity", Me.CheckFileIntegrity)
						Me.RecheckFileIntegrityForExistingFolders = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "RecheckFileIntegrityForExistingFolders", Me.RecheckFileIntegrityForExistingFolders)

						Me.MaximumTextFileLinesToCheck = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumTextFileLinesToCheck", Me.MaximumTextFileLinesToCheck)
						Me.MaximumXMLElementNodesToCheck = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "MaximumXMLElementNodesToCheck", Me.MaximumXMLElementNodesToCheck)
						Me.ComputeFileHashes = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeFileHashes", Me.ComputeFileHashes)
						Me.ZipFileCheckAllData = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ZipFileCheckAllData", Me.ZipFileCheckAllData)

						Me.IgnoreErrorsWhenRecursing = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "IgnoreErrorsWhenRecursing", Me.IgnoreErrorsWhenRecursing)

					End If

				End With
			Else
				ShowErrorMessage("Error calling objSettingsFile.LoadSettings for " & strParameterFilePath)
				Return False
			End If

		Catch ex As Exception
			HandleException("Error in LoadParameterFileSettings", ex)
			Return False
		End Try

		Return True

	End Function

	'Private Sub LogErrors(ByVal strSource As String, ByVal strMessage As String, ByVal ex As Exception, Optional ByVal blnAllowInformUser As Boolean = True, Optional ByVal blnAllowThrowingException As Boolean = True, Optional ByVal blnLogLocalOnly As Boolean = True, Optional ByVal eNewErrorCode As iMSFileInfoScanner.eMSFileScannerErrorCodes = iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError)
	'    Dim strMessageWithoutCRLF As String
	'    Dim fsErrorLogFile As StreamWriter

	'    mStatusMessage = String.Copy(strMessage)

	'    strMessageWithoutCRLF = mStatusMessage.Replace(ControlChars.NewLine, "; ")

	'    If ex Is Nothing Then
	'        ex = New Exception("Error")
	'    Else
	'        If Not ex.Message Is Nothing AndAlso ex.Message.Length > 0 Then
	'            strMessageWithoutCRLF &= "; " & ex.Message
	'        End If
	'    End If

	'    ShowErrorMessage(strSource & ": " & strMessageWithoutCRLF)

	'    Try
	'        fsErrorLogFile = New StreamWriter("MSFileInfoScanner_Errors.txt", True)
	'        fsErrorLogFile.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & strSource & ControlChars.Tab & strMessageWithoutCRLF)
	'    Catch ex2 As Exception
	'        ' Ignore errors here
	'    Finally
	'        If Not fsErrorLogFile Is Nothing Then
	'            fsErrorLogFile.Close()
	'        End If
	'    End Try

	'    If Not eNewErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError Then
	'        SetErrorCode(eNewErrorCode, True)
	'    End If

	'    If Me.ShowMessages AndAlso blnAllowInformUser Then
	'        Windows.Forms.MessageBox.Show(mStatusMessage & ControlChars.NewLine & ex.Message, "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
	'    ElseIf blnAllowThrowingException Then
	'        Throw New Exception(mStatusMessage, ex)
	'    End If
	'End Sub

	Protected Sub OpenFileIntegrityDetailsFile()
		OpenFileIntegrityOutputFile(iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails, mFileIntegrityDetailsFilePath, mFileIntegrityDetailsWriter)
	End Sub

	Protected Sub OpenFileIntegrityErrorsFile()
		OpenFileIntegrityOutputFile(iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors, mFileIntegrityErrorsFilePath, mFileIntegrityErrorsWriter)
	End Sub

	Protected Sub OpenFileIntegrityOutputFile(ByVal eDataFileType As iMSFileInfoScanner.eDataFileTypeConstants, ByRef strFilePath As String, ByRef objStreamWriter As StreamWriter)
		Dim blnOpenedExistingFile As Boolean
		Dim fsFileStream As FileStream = Nothing
		Dim strDefaultFileName As String

		strDefaultFileName = DefaultDataFileName(eDataFileType)
		ValidateDataFilePath(strFilePath, eDataFileType)

		Try
			If File.Exists(strFilePath) Then
				blnOpenedExistingFile = True
			End If
			fsFileStream = New FileStream(strFilePath, FileMode.Append, FileAccess.Write, FileShare.Read)

		Catch ex As Exception
			HandleException("Error opening/creating " & strFilePath & "; will try " & strDefaultFileName, ex)

			Try
				If File.Exists(strDefaultFileName) Then
					blnOpenedExistingFile = True
				End If

				fsFileStream = New FileStream(strDefaultFileName, FileMode.Append, FileAccess.Write, FileShare.Read)
			Catch ex2 As Exception
				HandleException("Error opening/creating " & strDefaultFileName, ex2)
			End Try
		End Try

		Try
			If Not fsFileStream Is Nothing Then
				objStreamWriter = New StreamWriter(fsFileStream)

				If Not blnOpenedExistingFile Then
					objStreamWriter.WriteLine(mMSFileInfoDataCache.ConstructHeaderLine(eDataFileType))
				End If
			End If
		Catch ex As Exception
			HandleException("Error opening/creating the StreamWriter for " & fsFileStream.Name, ex)
		End Try

	End Sub

	''' <summary>
	''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
	''' </summary>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoToDB() As Boolean Implements iMSFileInfoScanner.PostDatasetInfoToDB
		Return PostDatasetInfoToDB(mDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure)
	End Function

	''' <summary>
	''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
	''' </summary>
	''' <param name="strDatasetInfoXML">Database info XML</param>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoToDB(ByVal strDatasetInfoXML As String) As Boolean Implements iMSFileInfoScanner.PostDatasetInfoToDB
		Return PostDatasetInfoToDB(strDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure)
	End Function

	''' <summary>
	''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
	''' </summary>
	''' <param name="strConnectionString">Database connection string</param>
	''' <param name="strStoredProcedure">Stored procedure</param>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoToDB(ByVal strConnectionString As String, ByVal strStoredProcedure As String) As Boolean Implements iMSFileInfoScanner.PostDatasetInfoToDB
		Return PostDatasetInfoToDB(Me.DatasetInfoXML, strConnectionString, strStoredProcedure)
	End Function

	''' <summary>
	''' Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
	''' </summary>
	''' <param name="strDatasetInfoXML">Database info XML</param>
	''' <param name="strConnectionString">Database connection string</param>
	''' <param name="strStoredProcedure">Stored procedure</param>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoToDB(ByVal strDatasetInfoXML As String, _
	 ByVal strConnectionString As String, _
	 ByVal strStoredProcedure As String) As Boolean Implements iMSFileInfoScanner.PostDatasetInfoToDB

		Const MAX_RETRY_COUNT As Integer = 3
		Const SEC_BETWEEN_RETRIES As Integer = 20

		Dim intStartIndex As Integer
		Dim intResult As Integer

		Dim strDSInfoXMLClean As String

		Dim objCommand As SqlClient.SqlCommand

		Dim blnSuccess As Boolean

		Try
			ShowMessage("  Posting DatasetInfo XML to the database")

			' We need to remove the encoding line from strDatasetInfoXML before posting to the DB
			' This line will look like this:
			'   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

			intStartIndex = strDatasetInfoXML.IndexOf("?>")
			If intStartIndex > 0 Then
				strDSInfoXMLClean = strDatasetInfoXML.Substring(intStartIndex + 2).Trim
			Else
				strDSInfoXMLClean = strDatasetInfoXML
			End If

			' Call stored procedure strStoredProcedure using connection string strConnectionString

			If strConnectionString Is Nothing OrElse strConnectionString.Length = 0 Then
				ShowErrorMessage("Connection string not defined; unable to post the dataset info to the database")
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
				Return False
			End If

			If strStoredProcedure Is Nothing OrElse strStoredProcedure.Length = 0 Then
				strStoredProcedure = "UpdateDatasetFileInfoXML"
			End If

			objCommand = New SqlClient.SqlCommand()

			With objCommand
				.CommandType = CommandType.StoredProcedure
				.CommandText = strStoredProcedure

				.Parameters.Add(New SqlClient.SqlParameter("@Return", SqlDbType.Int))
				.Parameters.Item("@Return").Direction = ParameterDirection.ReturnValue

				.Parameters.Add(New SqlClient.SqlParameter("@DatasetInfoXML", SqlDbType.Xml))
				.Parameters.Item("@DatasetInfoXML").Direction = ParameterDirection.Input
				.Parameters.Item("@DatasetInfoXML").Value = strDSInfoXMLClean
			End With

			mExecuteSP = New PRISM.DataBase.clsExecuteDatabaseSP(strConnectionString)

			intResult = mExecuteSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES)

			If intResult = PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK Then
				' No errors
				blnSuccess = True
			Else
				ShowErrorMessage("Error calling stored procedure, return code = " & intResult)
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
				blnSuccess = False
			End If

			' Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
			' Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
			' PostDatasetInfoToDB(32, strDatasetInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

		Catch ex As Exception
			HandleException("Error calling stored procedure", ex)
			SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
			blnSuccess = False
		Finally
			mExecuteSP = Nothing
		End Try

		Return blnSuccess
	End Function

	''' <summary>
	''' Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
	''' This version assumes the stored procedure takes DatasetID as the first parameter
	''' </summary>
	''' <param name="intDatasetID">Dataset ID to send to the stored procedure</param>
	''' <param name="strConnectionString">Database connection string</param>
	''' <param name="strStoredProcedure">Stored procedure</param>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoUseDatasetID(ByVal intDatasetID As Integer, _
	  ByVal strConnectionString As String, _
	  ByVal strStoredProcedure As String) As Boolean Implements iMSFileInfoScanner.PostDatasetInfoUseDatasetID

		Return PostDatasetInfoUseDatasetID(intDatasetID, Me.DatasetInfoXML, strConnectionString, strStoredProcedure)
	End Function


	''' <summary>
	''' Post the dataset info in strDatasetInfoXML to the database, using the specified connection string and stored procedure
	''' This version assumes the stored procedure takes DatasetID as the first parameter
	''' </summary>
	''' <param name="intDatasetID">Dataset ID to send to the stored procedure</param>
	''' <param name="strDatasetInfoXML">Database info XML</param>
	''' <param name="strConnectionString">Database connection string</param>
	''' <param name="strStoredProcedure">Stored procedure</param>
	''' <returns>True if success; false if failure</returns>
	Public Function PostDatasetInfoUseDatasetID(ByVal intDatasetID As Integer, _
	  ByVal strDatasetInfoXML As String, _
	  ByVal strConnectionString As String, _
	  ByVal strStoredProcedure As String) As Boolean Implements iMSFileInfoScanner.PostDatasetInfoUseDatasetID

		Const MAX_RETRY_COUNT As Integer = 3
		Const SEC_BETWEEN_RETRIES As Integer = 20

		Dim intStartIndex As Integer
		Dim intResult As Integer

		Dim strDSInfoXMLClean As String

		Dim objCommand As SqlClient.SqlCommand

		Dim blnSuccess As Boolean

		Try
			If intDatasetID = 0 AndAlso mDSInfoDatasetIDOverride > 0 Then
				intDatasetID = mDSInfoDatasetIDOverride
			End If

			ShowMessage("  Posting DatasetInfo XML to the database (using Dataset ID " & intDatasetID.ToString & ")")

			' We need to remove the encoding line from strDatasetInfoXML before posting to the DB
			' This line will look like this:
			'   <?xml version="1.0" encoding="utf-16" standalone="yes"?>

			intStartIndex = strDatasetInfoXML.IndexOf("?>")
			If intStartIndex > 0 Then
				strDSInfoXMLClean = strDatasetInfoXML.Substring(intStartIndex + 2).Trim
			Else
				strDSInfoXMLClean = strDatasetInfoXML
			End If

			' Call stored procedure strStoredProcedure using connection string strConnectionString

			If strConnectionString Is Nothing OrElse strConnectionString.Length = 0 Then
				ShowErrorMessage("Connection string not defined; unable to post the dataset info to the database")
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
				Return False
			End If

			If strStoredProcedure Is Nothing OrElse strStoredProcedure.Length = 0 Then
				strStoredProcedure = "CacheDatasetInfoXML"
			End If

			objCommand = New SqlClient.SqlCommand()

			With objCommand
				.CommandType = CommandType.StoredProcedure
				.CommandText = strStoredProcedure

				.Parameters.Add(New SqlClient.SqlParameter("@Return", SqlDbType.Int))
				.Parameters.Item("@Return").Direction = ParameterDirection.ReturnValue

				.Parameters.Add(New SqlClient.SqlParameter("@DatasetID", SqlDbType.Int))
				.Parameters.Item("@DatasetID").Direction = ParameterDirection.Input
				.Parameters.Item("@DatasetID").Value = intDatasetID

				.Parameters.Add(New SqlClient.SqlParameter("@DatasetInfoXML", SqlDbType.Xml))
				.Parameters.Item("@DatasetInfoXML").Direction = ParameterDirection.Input
				.Parameters.Item("@DatasetInfoXML").Value = strDSInfoXMLClean
			End With

			mExecuteSP = New PRISM.DataBase.clsExecuteDatabaseSP(strConnectionString)

			intResult = mExecuteSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES)

			If intResult = PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK Then
				' No errors
				blnSuccess = True
			Else
				ShowErrorMessage("Error calling stored procedure, return code = " & intResult)
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
				blnSuccess = False
			End If

		Catch ex As Exception
			HandleException("Error calling stored procedure", ex)
			SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
			blnSuccess = False
		Finally
			mExecuteSP = Nothing
		End Try

		Return blnSuccess
	End Function

	Private Function ProcessMSDataset( _
	  ByVal strInputFileOrFolderPath As String, _
	  ByRef objMSInfoScanner As iMSFileInfoProcessor, _
	  ByVal strDatasetName As String, _
	  ByVal strOutputFolderPath As String) As Boolean

		Dim udtFileInfo As iMSFileInfoProcessor.udtFileInfoType = New iMSFileInfoProcessor.udtFileInfoType
		Dim intRetryCount As Integer

		Dim blnSuccess As Boolean

		' Open the MS datafile (or data folder), read the creation date, and update the status file

		intRetryCount = 0
		Do
			' Set the processing options
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, mSaveTICAndBPIPlots)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots, mSaveLCMS2DPlots)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus, mCheckCentroidingStatus)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, mComputeOverallQualityScores)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, mCreateDatasetInfoFile)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile, mCreateScanStatsFile)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError, mCopyFileLocalOnReadError)
			objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile, mUpdateDatasetStatsTextFile)

			objMSInfoScanner.DatasetStatsTextFileName = mDatasetStatsTextFileName

			objMSInfoScanner.LCMS2DPlotOptions = mLCMS2DPlotOptions
			objMSInfoScanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor

			objMSInfoScanner.ScanStart = mScanStart
			objMSInfoScanner.ScanEnd = mScanEnd
			objMSInfoScanner.ShowDebugInfo = mShowDebugInfo

			objMSInfoScanner.DatasetID = mDSInfoDatasetIDOverride

			' Process the data file
			blnSuccess = objMSInfoScanner.ProcessDataFile(strInputFileOrFolderPath, udtFileInfo)

			If Not blnSuccess Then
				intRetryCount += 1

				If intRetryCount < MAX_FILE_READ_ACCESS_ATTEMPTS Then
					' Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time
					If DateTime.Now.Subtract(udtFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES OrElse _
					   DateTime.Now.Subtract(udtFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES Then

						' Sleep for 10 seconds then try again
                        SleepNow(10)
					Else
						intRetryCount = MAX_FILE_READ_ACCESS_ATTEMPTS
					End If
				End If
			End If
		Loop While Not blnSuccess And intRetryCount < MAX_FILE_READ_ACCESS_ATTEMPTS

		If Not blnSuccess And intRetryCount >= MAX_FILE_READ_ACCESS_ATTEMPTS Then
			If udtFileInfo.DatasetName.Length > 0 Then
				' Make an entry anyway; probably a corrupted file
				blnSuccess = True
			End If
		End If

		If blnSuccess Then

            blnSuccess = objMSInfoScanner.CreateOutputFiles(strInputFileOrFolderPath, strOutputFolderPath)
			If Not blnSuccess Then
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.OutputFileWriteError)
			End If

			' Cache the Dataset Info XML
			mDatasetInfoXML = objMSInfoScanner.GetDatasetInfoXML()

			If mUseCacheFiles Then
				' Update the results database
				blnSuccess = mMSFileInfoDataCache.UpdateCachedMSFileInfo(udtFileInfo)

				' Possibly auto-save the cached results
				AutosaveCachedResults()
			End If

			If mDSInfoDBPostingEnabled Then
				blnSuccess = PostDatasetInfoToDB(mDatasetInfoXML)
				If Not blnSuccess Then
					SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.DatabasePostingError)
				End If
			Else
				blnSuccess = True
			End If

		Else
			If SKIP_FILES_IN_ERROR Then
				blnSuccess = True
			End If
		End If

		Return blnSuccess

	End Function

	' Main processing function
	Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String) As Boolean Implements iMSFileInfoScanner.ProcessMSFileOrFolder

		Dim eMSFileProcessingState As iMSFileInfoScanner.eMSFileProcessingStateConstants

		Return ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, True, eMSFileProcessingState)
	End Function

	Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String,
	  ByVal strOutputFolderPath As String, _
	  ByVal blnResetErrorCode As Boolean, _
	  ByRef eMSFileProcessingState As iMSFileInfoScanner.eMSFileProcessingStateConstants) As Boolean Implements iMSFileInfoScanner.ProcessMSFileOrFolder

		' Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
		' See function ProcessMSFilesAndRecurseFolders for more details
		' This function returns True if it processed a file (or the dataset was processed previously)
		' When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
		' If the file type is unknown, or if an error occurs, then it returns false
		' eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

		Dim blnSuccess As Boolean
		Dim blnIsFolder As Boolean
		Dim blnKnownMSDataType As Boolean

		Dim objFileSystemInfo As FileSystemInfo = Nothing

		Dim objRow As DataRow = Nothing
		Dim lngCachedSizeBytes As Long

		If blnResetErrorCode Then
			SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError)
		End If

		eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.NotProcessed

		If strOutputFolderPath Is Nothing OrElse strOutputFolderPath.Length = 0 Then
			' Define strOutputFolderPath based on the program file path
			strOutputFolderPath = GetAppFolderPath()
		End If

		' Update mOutputFolderPath
		mOutputFolderPath = String.Copy(strOutputFolderPath)

		mDatasetInfoXML = String.Empty

		LoadCachedResults(False)

		Try
			If strInputFileOrFolderPath Is Nothing OrElse strInputFileOrFolderPath.Length = 0 Then
				ShowErrorMessage("Input file name is empty")
			Else
				Try
					If Path.GetFileName(strInputFileOrFolderPath).Length = 0 Then
						ShowMessage(" Parsing " & Path.GetDirectoryName(strInputFileOrFolderPath))
					Else
						ShowMessage(" Parsing " & Path.GetFileName(strInputFileOrFolderPath))
					End If
				Catch ex As Exception
					ShowMessage(" Parsing " & strInputFileOrFolderPath)
				End Try

				' Determine whether strInputFileOrFolderPath points to a file or a folder

				If Not GetFileOrFolderInfo(strInputFileOrFolderPath, blnIsFolder, objFileSystemInfo) Then
					ShowErrorMessage("File or folder not found: " & strInputFileOrFolderPath)
					If SKIP_FILES_IN_ERROR Then
						Return True
					Else
						SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.FilePathError)
						Return False
					End If
				End If

				blnKnownMSDataType = False

				' Only continue if it's a known type
				If blnIsFolder Then
					If objFileSystemInfo.Name = clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME Then
						' Bruker 1 folder
						mMSInfoScanner = New clsBrukerOneFolderInfoScanner
						blnKnownMSDataType = True
					Else
						If strInputFileOrFolderPath.EndsWith("\"c) Then
							strInputFileOrFolderPath = strInputFileOrFolderPath.TrimEnd("\"c)
						End If

						Select Case Path.GetExtension(strInputFileOrFolderPath).ToUpper()
							Case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION
								' Agilent .D folder or Bruker .D folder

								If Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME).Length > 0 OrElse
								   Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SER_FILE_NAME).Length > 0 OrElse
								   Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_FID_FILE_NAME).Length > 0 OrElse
								   Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME).Length > 0 OrElse
								   Directory.GetFiles(strInputFileOrFolderPath, clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME).Length > 0 Then
									mMSInfoScanner = New clsBrukerXmassFolderInfoScanner

								ElseIf Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_MS_DATA_FILE).Length > 0 OrElse
								  Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_ACQ_METHOD_FILE).Length > 0 OrElse
								  Directory.GetFiles(strInputFileOrFolderPath, clsAgilentGCDFolderInfoScanner.AGILENT_GC_INI_FILE).Length > 0 Then
									mMSInfoScanner = New clsAgilentGCDFolderInfoScanner

								ElseIf Directory.GetDirectories(strInputFileOrFolderPath, clsAgilentTOFDFolderInfoScanner.AGILENT_ACQDATA_FOLDER_NAME).Length > 0 Then
									mMSInfoScanner = New clsAgilentTOFDFolderInfoScanner

								Else
									mMSInfoScanner = New clsAgilentIonTrapDFolderInfoScanner
								End If

								blnKnownMSDataType = True
							Case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION
								' Micromass .Raw folder
								mMSInfoScanner = New clsMicromassRawFolderInfoScanner
								blnKnownMSDataType = True
							Case Else
								' Unknown folder extension (or no extension)
								' See if the folder contains 1 or more 0_R*.zip files
								If Directory.GetFiles(strInputFileOrFolderPath, clsZippedImagingFilesScanner.ZIPPED_IMAGING_FILE_SEARCH_SPEC).Length > 0 Then
									mMSInfoScanner = New clsZippedImagingFilesScanner
									blnKnownMSDataType = True
								End If
						End Select
					End If
				Else
					If objFileSystemInfo.Name.ToLower() = clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_NAME.ToLower() Then
						mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
						blnKnownMSDataType = True

					ElseIf objFileSystemInfo.Name.ToLower() = clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME.ToLower() Then
						mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
						blnKnownMSDataType = True

					ElseIf objFileSystemInfo.Name.ToLower() = clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_FILE_NAME.ToLower() Then
						mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
						blnKnownMSDataType = True

					ElseIf objFileSystemInfo.Name.ToLower() = clsBrukerXmassFolderInfoScanner.BRUKER_ANALYSIS_YEP_FILE_NAME.ToLower() Then
						' If the folder also contains file BRUKER_EXTENSION_BAF_FILE_NAME then this is a Bruker XMass folder
						Dim strPathCheck As String

						strPathCheck = Path.Combine(Path.GetDirectoryName(objFileSystemInfo.FullName), clsBrukerXmassFolderInfoScanner.BRUKER_EXTENSION_BAF_FILE_NAME)
						If File.Exists(strPathCheck) Then
							mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
							blnKnownMSDataType = True
						End If
					End If

					If Not blnKnownMSDataType Then

						' Examine the extension on strInputFileOrFolderPath
						Select Case objFileSystemInfo.Extension.ToUpper()
							Case clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION
								mMSInfoScanner = New clsFinniganRawFileInfoScanner
								blnKnownMSDataType = True

							Case clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION
								mMSInfoScanner = New clsAgilentTOFOrQStarWiffFileInfoScanner
								blnKnownMSDataType = True

							Case clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION
								mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
								blnKnownMSDataType = True

							Case clsBrukerXmassFolderInfoScanner.BRUKER_MCF_FILE_EXTENSION
								mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
								blnKnownMSDataType = True

							Case clsBrukerXmassFolderInfoScanner.BRUKER_SQLITE_INDEX_EXTENSION
								mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
								blnKnownMSDataType = True

							Case clsUIMFInfoScanner.UIMF_FILE_EXTENSION
								mMSInfoScanner = New clsUIMFInfoScanner
								blnKnownMSDataType = True

							Case clsDeconToolsIsosInfoScanner.DECONTOOLS_CSV_FILE_EXTENSION

								If objFileSystemInfo.FullName.ToUpper().EndsWith(clsDeconToolsIsosInfoScanner.DECONTOOLS_ISOS_FILE_SUFFIX) Then
									mMSInfoScanner = New clsDeconToolsIsosInfoScanner
									blnKnownMSDataType = True
								End If

							Case Else
								' Unknown file extension; check for a zipped folder 
								If clsBrukerOneFolderInfoScanner.IsZippedSFolder(objFileSystemInfo.Name) Then
									' Bruker s001.zip file
									mMSInfoScanner = New clsBrukerOneFolderInfoScanner
									blnKnownMSDataType = True
								ElseIf clsZippedImagingFilesScanner.IsZippedImagingFile(objFileSystemInfo.Name) Then
									mMSInfoScanner = New clsZippedImagingFilesScanner
									blnKnownMSDataType = True
								End If
						End Select
					End If

				End If

				If Not blnKnownMSDataType Then
					ShowErrorMessage("Unknown file type: " & Path.GetFileName(strInputFileOrFolderPath))
					SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.UnknownFileExtension)
					Return False
				End If

				Dim strDatasetName As String
				strDatasetName = mMSInfoScanner.GetDatasetNameViaPath(objFileSystemInfo.FullName)

				If mUseCacheFiles AndAlso Not mReprocessExistingFiles Then
					' See if the strDatasetName in strInputFileOrFolderPath is already present in mCachedResults
					' If it is present, then don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

					If strDatasetName.Length > 0 AndAlso mMSFileInfoDataCache.CachedMSInfoContainsDataset(strDatasetName, objRow) Then
						If mReprocessIfCachedSizeIsZero Then
							Try
								lngCachedSizeBytes = CLng(objRow.Item(clsMSFileInfoDataCache.COL_NAME_FILE_SIZE_BYTES))
							Catch ex2 As Exception
								lngCachedSizeBytes = 1
							End Try

							If lngCachedSizeBytes > 0 Then
								' File is present in mCachedResults, and its size is > 0, so we won't re-process it
								ShowMessage("  Skipping " & Path.GetFileName(strInputFileOrFolderPath) & " since already in cached results")
								eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.SkippedSinceFoundInCache
								Return True
							End If
						Else
							' File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
							ShowMessage("  Skipping " & Path.GetFileName(strInputFileOrFolderPath) & " since already in cached results")
							eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.SkippedSinceFoundInCache
							Return True
						End If
					End If
				End If

				' Process the data file or folder
				blnSuccess = ProcessMSDataset(strInputFileOrFolderPath, mMSInfoScanner, strDatasetName, strOutputFolderPath)
				If blnSuccess Then
					eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.ProcessedSuccessfully
				Else
					eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.FailedProcessing
				End If

			End If

		Catch ex As Exception
			HandleException("Error in ProcessMSFileOrFolder", ex)
			blnSuccess = False
		Finally
			mMSInfoScanner = Nothing
		End Try

		Return blnSuccess

	End Function

	Public Function ProcessMSFileOrFolderWildcard(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean) As Boolean Implements iMSFileInfoScanner.ProcessMSFileOrFolderWildcard
		' Returns True if success, False if failure

		Dim blnSuccess As Boolean
		Dim intMatchCount As Integer

		Dim strCleanPath As String
		Dim strInputFolderPath As String

		Dim eMSFileProcessingState As iMSFileInfoScanner.eMSFileProcessingStateConstants

		Dim intProcessedFileListCount As Integer
		Dim strProcessedFileList As String()

		intProcessedFileListCount = 0
		ReDim strProcessedFileList(4)

		mAbortProcessing = False
		blnSuccess = True
		Try
			' Possibly reset the error code
			If blnResetErrorCode Then
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError)
			End If

			' See if strInputFilePath contains a wildcard
            If Not strInputFileOrFolderPath Is Nothing AndAlso (strInputFileOrFolderPath.IndexOf("*"c) >= 0 Or strInputFileOrFolderPath.IndexOf("?"c) >= 0) Then
                ' Obtain a list of the matching files and folders

                ' Copy the path into strCleanPath and replace any * or ? characters with _
                strCleanPath = strInputFileOrFolderPath.Replace("*", "_")
                strCleanPath = strCleanPath.Replace("?", "_")

                Dim fiFileInfo = New FileInfo(strCleanPath)
                If fiFileInfo.Directory.Exists Then
                    strInputFolderPath = fiFileInfo.DirectoryName
                Else
                    ' Use the current working directory
                    strInputFolderPath = Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                Dim diFolderInfo = New DirectoryInfo(strInputFolderPath)

                ' Remove any directory information from strInputFileOrFolderPath
                strInputFileOrFolderPath = Path.GetFileName(strInputFileOrFolderPath)

                intMatchCount = 0
                For Each fiFileMatch In diFolderInfo.GetFiles(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)

                    If eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                       eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.FailedProcessing Then
                        AddToStringList(strProcessedFileList, fiFileMatch.FullName, intProcessedFileListCount)
                    End If

                    CheckForAbortProcessingFile()
                    If mAbortProcessing Then Exit For

                    If Not blnSuccess And Not SKIP_FILES_IN_ERROR Then Exit For

                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next

                If mAbortProcessing Then Return False

                For Each diFolderMatch In diFolderInfo.GetDirectories(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(diFolderMatch.FullName, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)

                    If eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                       eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.FailedProcessing Then
                        AddToStringList(strProcessedFileList, diFolderMatch.FullName, intProcessedFileListCount)
                    End If

                    CheckForAbortProcessingFile()
                    If mAbortProcessing Then Exit For
                    If Not blnSuccess And Not SKIP_FILES_IN_ERROR Then Exit For

                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next

                If mAbortProcessing Then Return False

                If mCheckFileIntegrity Then
                    ReDim Preserve strProcessedFileList(intProcessedFileListCount - 1)
                    CheckIntegrityOfFilesInFolder(diFolderInfo.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList)
                End If

                If intMatchCount = 0 Then
                    If mErrorCode = iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError Then
                        ShowMessage("No match was found for the input file path:" & strInputFileOrFolderPath, eMessageTypeConstants.Warning)
                    End If
                Else
                    Console.WriteLine()
                End If
            Else
                blnSuccess = ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)
            End If

		Catch ex As Exception
			HandleException("Error in ProcessMSFileOrFolderWildcard", ex)
			blnSuccess = False
		Finally
			If Not mFileIntegrityDetailsWriter Is Nothing Then
				mFileIntegrityDetailsWriter.Close()
			End If
			If Not mFileIntegrityErrorsWriter Is Nothing Then
				mFileIntegrityErrorsWriter.Close()
			End If
		End Try

		Return blnSuccess

	End Function

	''' <summary>
	''' Calls ProcessMSFileOrFolder for all files in strInputFilePathOrFolder and below having a known extension
	'''  Known extensions are:
	'''   .Raw for Finnigan files
	'''   .Wiff for Agilent TOF files and for Q-Star files
	'''   .Baf for Bruker XMASS folders (contains file analysis.baf, and hopefully files scan.xml and Log.txt)
	''' For each folder that does not have any files matching a known extension, will then look for special folder names:
	'''   Folders matching *.Raw for Micromass data
	'''   Folders matching *.D for Agilent Ion Trap data
	'''   A folder named 1 for Bruker FTICR-MS data
	''' </summary>
	''' <param name="strInputFilePathOrFolder">Path to the input file or folder; can contain a wildcard (* or ?)</param>
	''' <param name="strOutputFolderPath">Folder to write any results files to</param>
	''' <param name="intRecurseFoldersMaxLevels">Maximum folder depth to process; Set to 0 to process all folders</param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Function ProcessMSFilesAndRecurseFolders(ByVal strInputFilePathOrFolder As String, _
	  ByVal strOutputFolderPath As String, _
	  ByVal intRecurseFoldersMaxLevels As Integer) As Boolean Implements iMSFileInfoScanner.ProcessMSFilesAndRecurseFolders

		Dim strCleanPath As String
		Dim strInputFolderPath As String

		Dim blnSuccess As Boolean
		Dim intFileProcessCount, intFileProcessFailCount As Integer

		' Examine strInputFilePathOrFolder to see if it contains a filename; if not, assume it points to a folder
		' First, see if it contains a * or ?
		Try
			If Not strInputFilePathOrFolder Is Nothing AndAlso (strInputFilePathOrFolder.IndexOf("*") >= 0 Or strInputFilePathOrFolder.IndexOf("?") >= 0) Then
				' Copy the path into strCleanPath and replace any * or ? characters with _
				strCleanPath = strInputFilePathOrFolder.Replace("*", "_")
				strCleanPath = strCleanPath.Replace("?", "_")

				Dim fiFileInfo = New FileInfo(strCleanPath)
				If Path.IsPathRooted(strCleanPath) Then
					If Not fiFileInfo.Directory.Exists Then
						ShowErrorMessage("Folder not found: " & fiFileInfo.DirectoryName)
						SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.InvalidInputFilePath)
						Return False
					End If
				End If

				If fiFileInfo.Directory.Exists Then
					strInputFolderPath = fiFileInfo.DirectoryName
				Else
					' Folder not found; use the current working directory
					strInputFolderPath = Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
				End If

				' Remove any directory information from strInputFilePath
				strInputFilePathOrFolder = Path.GetFileName(strInputFilePathOrFolder)

			Else
				Dim diFolderInfo = New DirectoryInfo(strInputFilePathOrFolder)
				If diFolderInfo.Exists Then
					strInputFolderPath = diFolderInfo.FullName
					strInputFilePathOrFolder = "*"
				Else
					If diFolderInfo.Parent.Exists Then
						strInputFolderPath = diFolderInfo.Parent.FullName
						strInputFilePathOrFolder = Path.GetFileName(strInputFilePathOrFolder)
					Else
						' Unable to determine the input folder path
						strInputFolderPath = String.Empty
					End If
				End If
			End If

			If Not strInputFolderPath Is Nothing AndAlso strInputFolderPath.Length > 0 Then

				' Initialize some parameters
				mAbortProcessing = False
				intFileProcessCount = 0
				intFileProcessFailCount = 0

				LoadCachedResults(False)

				' Call RecurseFoldersWork
				blnSuccess = RecurseFoldersWork(strInputFolderPath, strInputFilePathOrFolder, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, 1, intRecurseFoldersMaxLevels)

			Else
				SetErrorCode(iMSFileInfoScanner.eMSFileScannerErrorCodes.InvalidInputFilePath)
				Return False
			End If

		Catch ex As Exception
			HandleException("Error in ProcessMSFilesAndRecurseFolders", ex)
			blnSuccess = False
		Finally
			If Not mFileIntegrityDetailsWriter Is Nothing Then
				mFileIntegrityDetailsWriter.Close()
			End If
			If Not mFileIntegrityErrorsWriter Is Nothing Then
				mFileIntegrityErrorsWriter.Close()
			End If
		End Try

		Return blnSuccess

	End Function

	Private Function RecurseFoldersWork(ByVal strInputFolderPath As String, _
	   ByVal strFileNameMatch As String, _
	   ByVal strOutputFolderPath As String, _
	   ByRef intFileProcessCount As Integer, _
	   ByRef intFileProcessFailCount As Integer, _
	   ByVal intRecursionLevel As Integer, _
	   ByVal intRecurseFoldersMaxLevels As Integer) As Boolean

		Const MAX_ACCESS_ATTEMPTS As Integer = 2

		' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

		Dim diInputFolder As DirectoryInfo = Nothing
		Dim diSubfolder As DirectoryInfo

		Dim strFileExtensionsToParse() As String
		Dim strFolderExtensionsToParse() As String
		Dim blnProcessAllFileExtensions As Boolean

		Dim intRetryCount As Integer
		Dim intExtensionIndex As Integer

		Dim blnSuccess As Boolean
		Dim blnFileProcessed As Boolean
		Dim blnProcessedZippedSFolder As Boolean

		Dim intSubFoldersProcessed As Integer
		Dim htSubFoldersProcessed As Hashtable

		Dim eMSFileProcessingState As iMSFileInfoScanner.eMSFileProcessingStateConstants

		Dim intProcessedFileListCount As Integer
		Dim strProcessedFileList() As String

		intProcessedFileListCount = 0
		ReDim strProcessedFileList(4)

		intRetryCount = 0
		Do
			Try
				diInputFolder = New DirectoryInfo(strInputFolderPath)
				Exit Do
			Catch ex As Exception
				' Input folder path error
				HandleException("Error populating diInputFolderInfo for " & strInputFolderPath, ex)
				If Not ex.Message.Contains("no longer available") Then
					Return False
				End If
			End Try

			intRetryCount += 1
			If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
				Return False
			Else
				' Wait 1 second, then try again
                SleepNow(1)
			End If

		Loop While intRetryCount < MAX_ACCESS_ATTEMPTS


		Try
			' Construct and validate the list of file and folder extensions to parse
			strFileExtensionsToParse = GetKnownFileExtensions()
			strFolderExtensionsToParse = GetKnownFolderExtensions()

			blnProcessAllFileExtensions = ValidateExtensions(strFileExtensionsToParse)
			ValidateExtensions(strFolderExtensionsToParse)
		Catch ex As Exception
			HandleException("Error in RecurseFoldersWork", ex)
			Return False
		End Try

		Try
			Console.WriteLine("Examining " & strInputFolderPath)

			' Process any matching files in this folder
			blnSuccess = True
			blnProcessedZippedSFolder = False
			For Each fiFileMatch In diInputFolder.GetFiles(strFileNameMatch)

				intRetryCount = 0
				Do
					Try

						blnFileProcessed = False
						For intExtensionIndex = 0 To strFileExtensionsToParse.Length - 1
							If blnProcessAllFileExtensions OrElse fiFileMatch.Extension.ToUpper = strFileExtensionsToParse(intExtensionIndex) Then
								blnFileProcessed = True
								blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, True, eMSFileProcessingState)

								If eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
								   eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.FailedProcessing Then
									AddToStringList(strProcessedFileList, fiFileMatch.FullName, intProcessedFileListCount)
								End If

								Exit For
							End If
						Next intExtensionIndex
						If mAbortProcessing Then Exit For

						If Not blnFileProcessed AndAlso Not blnProcessedZippedSFolder Then
							' Check for other valid files
							If clsBrukerOneFolderInfoScanner.IsZippedSFolder(fiFileMatch.Name) Then
								' Only process this file if there is not a subfolder named "1" present"
								If diInputFolder.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1 Then
									blnFileProcessed = True
									blnProcessedZippedSFolder = True
									blnSuccess = ProcessMSFileOrFolder(fiFileMatch.FullName, strOutputFolderPath, True, eMSFileProcessingState)

									If eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
									   eMSFileProcessingState = iMSFileInfoScanner.eMSFileProcessingStateConstants.FailedProcessing Then
										AddToStringList(strProcessedFileList, fiFileMatch.FullName, intProcessedFileListCount)
									End If

								End If
							End If
						End If
						Exit Do

					Catch ex As Exception
						' Error parsing file
						HandleException("Error in RecurseFoldersWork at For Each ioFileMatch in " & strInputFolderPath, ex)
						If Not ex.Message.Contains("no longer available") Then
							Return False
						End If
					End Try

					intRetryCount += 1
					If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
						Return False
					Else
						' Wait 1 second, then try again
                        SleepNow(1)
					End If

				Loop While intRetryCount < MAX_ACCESS_ATTEMPTS

				If blnFileProcessed Then
					If blnSuccess Then
						intFileProcessCount += 1
					Else
						intFileProcessFailCount += 1
						blnSuccess = True
					End If
				End If

				CheckForAbortProcessingFile()
				If mAbortProcessing Then Exit For
			Next

			If mCheckFileIntegrity And Not mAbortProcessing Then
				ReDim Preserve strProcessedFileList(intProcessedFileListCount - 1)
				CheckIntegrityOfFilesInFolder(diInputFolder.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList)
			End If

		Catch ex As Exception
			HandleException("Error in RecurseFoldersWork Examining files in " & strInputFolderPath, ex)
			Return False
		End Try

		If Not mAbortProcessing Then
			' Check the subfolders for those with known extensions

			Try

				intSubFoldersProcessed = 0
				htSubFoldersProcessed = New Hashtable
				For Each diSubfolder In diInputFolder.GetDirectories(strFileNameMatch)

					intRetryCount = 0
					Do
						Try
							' Check whether the folder name is BRUKER_ONE_FOLDER = "1"
							If diSubfolder.Name = clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME Then
								blnSuccess = ProcessMSFileOrFolder(diSubfolder.FullName, strOutputFolderPath, True, eMSFileProcessingState)
								If Not blnSuccess Then
									intFileProcessFailCount += 1
									blnSuccess = True
								Else
									intFileProcessCount += 1
								End If
								intSubFoldersProcessed += 1
								htSubFoldersProcessed.Add(diSubfolder.Name, 1)
							Else
								' See if the subfolder has an extension matching strFolderExtensionsToParse()
								' If it does, process it using ProcessMSFileOrFolder and do not recurse into it
								For intExtensionIndex = 0 To strFolderExtensionsToParse.Length - 1
									If diSubfolder.Extension.ToUpper = strFolderExtensionsToParse(intExtensionIndex) Then
										blnSuccess = ProcessMSFileOrFolder(diSubfolder.FullName, strOutputFolderPath, True, eMSFileProcessingState)
										If Not blnSuccess Then
											intFileProcessFailCount += 1
											blnSuccess = True
										Else
											intFileProcessCount += 1
										End If
										intSubFoldersProcessed += 1
										htSubFoldersProcessed.Add(diSubfolder.Name, 1)
										Exit For
									End If
								Next intExtensionIndex
								If mAbortProcessing Then Exit For

							End If

							Exit Do

						Catch ex As Exception
							' Error parsing folder
							HandleException("Error in RecurseFoldersWork at For Each diSubfolder(A) in " & strInputFolderPath, ex)
							If Not ex.Message.Contains("no longer available") Then
								Return False
							End If
						End Try

						intRetryCount += 1
						If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
							Return False
						Else
							' Wait 1 second, then try again
                            SleepNow(1)
						End If

					Loop While intRetryCount < MAX_ACCESS_ATTEMPTS

					If mAbortProcessing Then Exit For

				Next diSubfolder

				' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
				'  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
				If intRecurseFoldersMaxLevels <= 0 OrElse intRecursionLevel <= intRecurseFoldersMaxLevels Then
					' Call this function for each of the subfolders of diInputFolder
					' However, do not step into folders listed in htSubFoldersProcessed
					For Each diSubfolder In diInputFolder.GetDirectories()

						intRetryCount = 0
						Do
							Try
								If intSubFoldersProcessed = 0 OrElse Not htSubFoldersProcessed.Contains(diSubfolder.Name) Then
									blnSuccess = RecurseFoldersWork(diSubfolder.FullName, strFileNameMatch, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, intRecursionLevel + 1, intRecurseFoldersMaxLevels)
								End If
								If Not blnSuccess And Not mIgnoreErrorsWhenRecursing Then
									Exit For
								End If

								CheckForAbortProcessingFile()
								If mAbortProcessing Then Exit For

								Exit Do

							Catch ex As Exception
								' Error parsing file
								HandleException("Error in RecurseFoldersWork at For Each diSubfolder(B) in " & strInputFolderPath, ex)
								If Not ex.Message.Contains("no longer available") Then
									Return False
								End If
							End Try

							intRetryCount += 1
							If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
								Return False
							Else
								' Wait 1 second, then try again
                                SleepNow(1)
							End If

						Loop While intRetryCount < MAX_ACCESS_ATTEMPTS

						If mAbortProcessing Then Exit For
					Next diSubfolder
				End If


			Catch ex As Exception
				HandleException("Error in RecurseFoldersWork examining subfolders in " & strInputFolderPath, ex)
				Return False
			End Try

		End If

		Return blnSuccess

	End Function

	Public Function SaveCachedResults() As Boolean Implements iMSFileInfoScanner.SaveCachedResults
		Return Me.SaveCachedResults(True)
	End Function

	Public Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean Implements iMSFileInfoScanner.SaveCachedResults
		If mUseCacheFiles Then
			Return mMSFileInfoDataCache.SaveCachedResults(blnClearCachedData)
		Else
			Return True
		End If
	End Function

	Public Function SaveParameterFileSettings(ByVal strParameterFilePath As String) As Boolean Implements iMSFileInfoScanner.SaveParameterFileSettings

		Dim objSettingsFile As New XmlSettingsFileAccessor

		Try

			If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
				' No parameter file specified; unable to save
				Return False
			End If

			' Pass True to .LoadSettings() here so that newly made Xml files will have the correct capitalization
			If objSettingsFile.LoadSettings(strParameterFilePath, True) Then
				With objSettingsFile

					' General settings
					' .SetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ConnectionString", Me.DatabaseConnectionString)
				End With

				objSettingsFile.SaveSettings()

			End If

		Catch ex As Exception
			HandleException("Error in SaveParameterFileSettings", ex)
			Return False
		Finally
			objSettingsFile = Nothing
		End Try

		Return True

	End Function

	Private Sub SetErrorCode(ByVal eNewErrorCode As iMSFileInfoScanner.eMSFileScannerErrorCodes)
		SetErrorCode(eNewErrorCode, False)
	End Sub

	Private Sub SetErrorCode(ByVal eNewErrorCode As iMSFileInfoScanner.eMSFileScannerErrorCodes, ByVal blnLeaveExistingErrorCodeUnchanged As Boolean)

		If blnLeaveExistingErrorCodeUnchanged AndAlso mErrorCode <> iMSFileInfoScanner.eMSFileScannerErrorCodes.NoError Then
			' An error code is already defined; do not change it
		Else
			mErrorCode = eNewErrorCode
		End If

	End Sub

	Protected Sub ShowErrorMessage(ByVal strMessage As String)
		ShowErrorMessage(strMessage, True)
	End Sub

	Protected Sub ShowErrorMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean)
		Dim strSeparator As String = "------------------------------------------------------------------------------"

		Console.WriteLine()
		Console.WriteLine(strSeparator)
		Console.WriteLine(strMessage)
		Console.WriteLine(strSeparator)
		Console.WriteLine()

		RaiseEvent ErrorEvent(strMessage)

		If blnAllowLogToFile Then
			LogMessage(strMessage, eMessageTypeConstants.ErrorMsg)
		End If

	End Sub

	Protected Sub ShowMessage(ByVal strMessage As String)
		ShowMessage(strMessage, True, False, eMessageTypeConstants.Normal)
	End Sub

	Protected Sub ShowMessage(ByVal strMessage As String, ByVal eMessageType As eMessageTypeConstants)
		ShowMessage(strMessage, True, False, eMessageType)
	End Sub

	Protected Sub ShowMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean)
		ShowMessage(strMessage, blnAllowLogToFile, False, eMessageTypeConstants.Normal)
	End Sub

	Protected Sub ShowMessage(ByVal strMessage As String, ByVal blnAllowLogToFile As Boolean, ByVal blnPrecedeWithNewline As Boolean, ByVal eMessageType As eMessageTypeConstants)

		If blnPrecedeWithNewline Then
			Console.WriteLine()
		End If
		Console.WriteLine(strMessage)

		RaiseEvent MessageEvent(strMessage)

		If blnAllowLogToFile Then
			LogMessage(strMessage, eMessageType)
		End If

	End Sub

	Public Shared Function ValidateDataFilePath(ByRef strFilePath As String, ByVal eDataFileType As iMSFileInfoScanner.eDataFileTypeConstants) As Boolean
		If strFilePath Is Nothing OrElse strFilePath.Length = 0 Then
			strFilePath = Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileType))
		End If

		Return ValidateDataFilePathCheckDir(strFilePath)

	End Function

	Private Shared Function ValidateDataFilePathCheckDir(ByVal strFilePath As String) As Boolean

		Dim blnValidFile As Boolean

		Try
			Dim fiFileInfo = New FileInfo(strFilePath)

			If Not fiFileInfo.Exists Then
				' Make sure the folder exists
				If Not fiFileInfo.Directory.Exists Then
					fiFileInfo.Directory.Create()
				End If
			End If
			blnValidFile = True

		Catch ex As Exception
			' Ignore errors, but set blnValidFile to false
			blnValidFile = False
		End Try

		Return blnValidFile
	End Function

    Protected Sub SleepNow(sleepTimeSeconds As Integer)
        System.Threading.Thread.Sleep(sleepTimeSeconds * 10)
    End Sub

	Private Function ValidateExtensions(ByRef strExtensions() As String) As Boolean
		' Returns True if one of the entries in strExtensions = "*" or ".*"

		Dim intExtensionIndex As Integer
		Dim blnProcessAllExtensions As Boolean = False

		For intExtensionIndex = 0 To strExtensions.Length - 1
			If strExtensions(intExtensionIndex) Is Nothing Then
				strExtensions(intExtensionIndex) = String.Empty
			Else
				If Not strExtensions(intExtensionIndex).StartsWith(".") Then
					strExtensions(intExtensionIndex) = "." & strExtensions(intExtensionIndex)
				End If

				If strExtensions(intExtensionIndex) = ".*" Then
					blnProcessAllExtensions = True
					Exit For
				Else
					strExtensions(intExtensionIndex) = strExtensions(intExtensionIndex).ToUpper
				End If
			End If
		Next intExtensionIndex

		Return blnProcessAllExtensions
	End Function

	Private Sub WriteFileIntegrityDetails(ByRef srOutFile As StreamWriter, ByVal intFolderID As Integer, ByVal udtFileStats() As clsFileIntegrityChecker.udtFileStatsType)
		Static dtLastWriteTime As DateTime

		Dim intIndex As Integer
		Dim dtTimeStamp As DateTime = DateTime.Now

		If srOutFile Is Nothing Then Exit Sub

		For intIndex = 0 To udtFileStats.Length - 1
			With udtFileStats(intIndex)
				' Note: HH:mm:ss corresponds to time in 24 hour format
				srOutFile.WriteLine(intFolderID.ToString & ControlChars.Tab & _
				  .FileName & ControlChars.Tab & _
				  .SizeBytes.ToString & ControlChars.Tab & _
				  .ModificationDate.ToString("yyyy-MM-dd HH:mm:ss") & ControlChars.Tab & _
				  .FailIntegrity & ControlChars.Tab & _
				  .FileHash & ControlChars.Tab & _
				  dtTimeStamp.ToString("yyyy-MM-dd HH:mm:ss"))
			End With
		Next intIndex

		If DateTime.UtcNow.Subtract(dtLastWriteTime).TotalMinutes > 1 Then
			srOutFile.Flush()
			dtLastWriteTime = DateTime.UtcNow
		End If

	End Sub

	Private Sub WriteFileIntegrityFailure(ByRef srOutFile As StreamWriter, ByVal strFilePath As String, ByVal strMessage As String)
		Static dtLastWriteTime As DateTime

		If srOutFile Is Nothing Then Exit Sub

		' Note: HH:mm:ss corresponds to time in 24 hour format
		srOutFile.WriteLine(strFilePath & ControlChars.Tab & _
		  strMessage & ControlChars.Tab & _
		  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))

		If DateTime.UtcNow.Subtract(dtLastWriteTime).TotalMinutes > 1 Then
			srOutFile.Flush()
			dtLastWriteTime = DateTime.UtcNow
		End If

	End Sub

	''Private Function ValidateXRawAccessor() As Boolean

	''    Static blnValidated As Boolean
	''    Static blnValidationSaved As Boolean

	''    If blnValidated Then
	''        Return blnValidationSaved
	''    End If

	''    Try
	''        Dim objXRawAccess As New FinniganFileIO.XRawFileIO

	''        blnValidationSaved = objXRawAccess.CheckFunctionality()
	''    Catch ex as Exception
	''        blnValidationSaved = False
	''    End Try

	''    Return blnValidationSaved

	''End Function

	Private Sub mFileIntegrityChecker_ErrorCaught(ByVal strMessage As String) Handles mFileIntegrityChecker.ErrorCaught
		ShowErrorMessage("Error caught in FileIntegrityChecker: " & strMessage)
	End Sub

	Private Sub mFileIntegrityChecker_FileIntegrityFailure(ByVal strFilePath As String, ByVal strMessage As String) Handles mFileIntegrityChecker.FileIntegrityFailure
		If mFileIntegrityErrorsWriter Is Nothing Then
			OpenFileIntegrityErrorsFile()
		End If

		WriteFileIntegrityFailure(mFileIntegrityErrorsWriter, strFilePath, strMessage)
	End Sub

	Private Sub mMSInfoScanner_ErrorEvent(ByVal Message As String) Handles mMSInfoScanner.ErrorEvent
		ShowErrorMessage(Message)
	End Sub

	Private Sub mMSInfoScannerMessageEvent(ByVal Message As String) Handles mMSInfoScanner.MessageEvent
		ShowMessage(Message, eMessageTypeConstants.Normal)
	End Sub

	Private Sub mMSFileInfoDataCache_ErrorEvent(ByVal Message As String) Handles mMSFileInfoDataCache.ErrorEvent
		ShowErrorMessage(Message)
	End Sub

	Private Sub mMSFileInfoDataCache_StatusEvent(ByVal Message As String) Handles mMSFileInfoDataCache.StatusEvent
		ShowMessage(Message)
	End Sub

	Private Sub mExecuteSP_DBErrorEvent(ByVal Message As String) Handles mExecuteSP.DBErrorEvent
		ShowErrorMessage(Message)
	End Sub

End Class
