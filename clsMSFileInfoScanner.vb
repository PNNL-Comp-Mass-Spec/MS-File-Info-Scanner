Option Strict On

' Scans a series of MS data files (or data folders) and extracts the acquisition start and end times, 
' number of spectra, and the total size of the data.  Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
'
' Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar .WIFF files, 
' Masslynx .Raw folders, Bruker 1 folders, and Bruker XMass analysis.baf files
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started October 11, 2003

Public Class clsMSFileInfoScanner

    Public Sub New()
        mFileDate = "May 5, 2010"

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

    Public Enum eMSFileScannerErrorCodes
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

    Public Enum eMSFileProcessingStateConstants
        NotProcessed = 0
        SkippedSinceFoundInCache = 1
        FailedProcessing = 2
        ProcessedSuccessfully = 3
    End Enum

    Public Enum eDataFileTypeConstants
        MSFileInfo = 0
        FolderIntegrityInfo = 1
        FileIntegrityDetails = 2
        FileIntegrityErrors = 3
    End Enum

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
    Private mErrorCode As eMSFileScannerErrorCodes

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

    Private mComputeOverallQualityScores As Boolean
    Private mCreateDatasetInfoFile As Boolean

    Private mCheckFileIntegrity As Boolean

    Private mDSInfoConnectionString As String
    Private mDSInfoDBPostingEnabled As Boolean
    Private mDSInfoStoredProcedure As String

    Private mLCMS2DPlotOptions As clsLCMSDataPlotter.clsOptions
    Private mLCMS2DOverviewPlotDivisor As Integer

    Protected mLogMessagesToFile As Boolean
    Protected mLogFilePath As String
    Protected mLogFile As System.IO.StreamWriter

    ' This variable is updated in ProcessMSFileOrFolder
    Protected mOutputFolderPath As String
    Protected mLogFolderPath As String          ' If blank, then mOutputFolderPath will be used; if mOutputFolderPath is also blank, then the log is created in the same folder as the executing assembly

    Protected mDatasetInfoXML As String = ""

    Private WithEvents mFileIntegrityChecker As clsFileIntegrityChecker
    Private mFileIntegrityDetailsWriter As System.IO.StreamWriter
    Private mFileIntegrityErrorsWriter As System.IO.StreamWriter

    Private WithEvents mMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor

    Private WithEvents mMSFileInfoDataCache As clsMSFileInfoDataCache

    Private WithEvents mExecuteSP As clsExecuteDatabaseSP

    Public Event MessageEvent(ByVal Message As String)
    Public Event ErrorEvent(ByVal Message As String)

#End Region

#Region "Processing Options and Interface Functions"

    Public Property AbortProcessing() As Boolean
        Get
            Return mAbortProcessing
        End Get
        Set(ByVal value As Boolean)
            mAbortProcessing = value
        End Set
    End Property

    Public Property AcquisitionTimeFilename() As String
        Get
            Return GetDataFileFilename(eDataFileTypeConstants.MSFileInfo)
        End Get
        Set(ByVal value As String)
            SetDataFileFilename(value, eDataFileTypeConstants.MSFileInfo)
        End Set
    End Property

    ''' <summary>
    ''' When true, then checks the integrity of every file in every folder processed
    ''' </summary>
    Public Property CheckFileIntegrity() As Boolean
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

    Public Property ComputeOverallQualityScores() As Boolean
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
    Public ReadOnly Property DatasetInfoXML() As String
        Get
            Return mDatasetInfoXML
        End Get
    End Property

    Public Function GetDataFileFilename(ByVal eDataFileType As eDataFileTypeConstants) As String
        Select Case eDataFileType
            Case eDataFileTypeConstants.MSFileInfo
                Return mMSFileInfoDataCache.AcquisitionTimeFilePath
            Case eDataFileTypeConstants.FolderIntegrityInfo
                Return mMSFileInfoDataCache.FolderIntegrityInfoFilePath
            Case eDataFileTypeConstants.FileIntegrityDetails
                Return mFileIntegrityDetailsFilePath
            Case eDataFileTypeConstants.FileIntegrityErrors
                Return mFileIntegrityErrorsFilePath
            Case Else
                Return String.Empty
        End Select
    End Function

    Public Sub SetDataFileFilename(ByVal strFilePath As String, ByVal eDataFileType As eDataFileTypeConstants)
        Select Case eDataFileType
            Case eDataFileTypeConstants.MSFileInfo
                mMSFileInfoDataCache.AcquisitionTimeFilePath = strFilePath
            Case eDataFileTypeConstants.FolderIntegrityInfo
                mMSFileInfoDataCache.FolderIntegrityInfoFilePath = strFilePath
            Case eDataFileTypeConstants.FileIntegrityDetails
                mFileIntegrityDetailsFilePath = strFilePath
            Case eDataFileTypeConstants.FileIntegrityErrors
                mFileIntegrityErrorsFilePath = strFilePath
            Case Else
                ' Unknown file type
        End Select
    End Sub

    Public Shared ReadOnly Property DefaultAcquisitionTimeFilename() As String
        Get
            Return DefaultDataFileName(eDataFileTypeConstants.MSFileInfo)
        End Get
    End Property

    Public Shared ReadOnly Property DefaultDataFileName(ByVal eDataFileType As eDataFileTypeConstants) As String
        Get
            If USE_XML_OUTPUT_FILE Then
                Select Case eDataFileType
                    Case eDataFileTypeConstants.MSFileInfo
                        Return DEFAULT_ACQUISITION_TIME_FILENAME_XML
                    Case eDataFileTypeConstants.FolderIntegrityInfo
                        Return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_XML
                    Case eDataFileTypeConstants.FileIntegrityDetails
                        Return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_XML
                    Case eDataFileTypeConstants.FileIntegrityErrors
                        Return DEFAULT_FILE_INTEGRITY_ERRORS_FILENAME_XML
                    Case Else
                        Return "UnknownFileType.xml"
                End Select
            Else
                Select Case eDataFileType
                    Case eDataFileTypeConstants.MSFileInfo
                        Return DEFAULT_ACQUISITION_TIME_FILENAME_TXT
                    Case eDataFileTypeConstants.FolderIntegrityInfo
                        Return DEFAULT_FOLDER_INTEGRITY_INFO_FILENAME_TXT
                    Case eDataFileTypeConstants.FileIntegrityDetails
                        Return DEFAULT_FILE_INTEGRITY_DETAILS_FILENAME_TXT
                    Case eDataFileTypeConstants.FileIntegrityErrors
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
    Public Property ComputeFileHashes() As Boolean
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
    ''' If True, then will create the _DatasetInfo.xml file
    ''' </summary>   
    Public Property CreateDatasetInfoFile() As Boolean
        Get
            Return mCreateDatasetInfoFile
        End Get
        Set(ByVal value As Boolean)
            mCreateDatasetInfoFile = value
        End Set
    End Property

    Public Property DSInfoConnectionString() As String
        Get
            Return mDSInfoConnectionString
        End Get
        Set(ByVal value As String)
            mDSInfoConnectionString = value
        End Set
    End Property

    Public Property DSInfoDBPostingEnabled() As Boolean
        Get
            Return mDSInfoDBPostingEnabled
        End Get
        Set(ByVal value As Boolean)
            mDSInfoDBPostingEnabled = value
        End Set
    End Property

    Public Property DSInfoStoredProcedure() As String
        Get
            Return mDSInfoStoredProcedure
        End Get
        Set(ByVal value As String)
            mDSInfoStoredProcedure = value
        End Set
    End Property

    Public ReadOnly Property ErrorCode() As eMSFileScannerErrorCodes
        Get
            Return mErrorCode
        End Get
    End Property

    Public Property IgnoreErrorsWhenRecursing() As Boolean
        Get
            Return mIgnoreErrorsWhenRecursing
        End Get
        Set(ByVal value As Boolean)
            mIgnoreErrorsWhenRecursing = value
        End Set
    End Property

    Public Property LCMS2DPlotMZResolution() As Single
        Get
            Return mLCMS2DPlotOptions.MZResolution
        End Get
        Set(ByVal value As Single)
            mLCMS2DPlotOptions.MZResolution = value
        End Set
    End Property

    Public Property LCMS2DPlotMaxPointsToPlot() As Integer
        Get
            Return mLCMS2DPlotOptions.MaxPointsToPlot
        End Get
        Set(ByVal value As Integer)
            mLCMS2DPlotOptions.MaxPointsToPlot = value
        End Set
    End Property

    Public Property LCMS2DOverviewPlotDivisor() As Integer
        Get
            Return mLCMS2DOverviewPlotDivisor
        End Get
        Set(ByVal value As Integer)
            mLCMS2DOverviewPlotDivisor = value
        End Set
    End Property

    Public Property LCMS2DPlotMinPointsPerSpectrum() As Integer
        Get
            Return mLCMS2DPlotOptions.MinPointsPerSpectrum
        End Get
        Set(ByVal value As Integer)
            mLCMS2DPlotOptions.MinPointsPerSpectrum = value
        End Set
    End Property

    Public Property LCMS2DPlotMinIntensity() As Single
        Get
            Return mLCMS2DPlotOptions.MinIntensity
        End Get
        Set(ByVal value As Single)
            mLCMS2DPlotOptions.MinIntensity = value
        End Set
    End Property


    Public Property LogMessagesToFile() As Boolean
        Get
            Return mLogMessagesToFile
        End Get
        Set(ByVal value As Boolean)
            mLogMessagesToFile = value
        End Set
    End Property

    Public Property LogFilePath() As String
        Get
            Return mLogFilePath
        End Get
        Set(ByVal value As String)
            mLogFilePath = value
        End Set
    End Property

    Public Property LogFolderPath() As String
        Get
            Return mLogFolderPath
        End Get
        Set(ByVal value As String)
            mLogFolderPath = value
        End Set
    End Property

    Public Property MaximumTextFileLinesToCheck() As Integer
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

    Public Property MaximumXMLElementNodesToCheck() As Integer
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

    Public Property RecheckFileIntegrityForExistingFolders() As Boolean
        Get
            Return mRecheckFileIntegrityForExistingFolders
        End Get
        Set(ByVal value As Boolean)
            mRecheckFileIntegrityForExistingFolders = value
        End Set
    End Property

    Public Property ReprocessExistingFiles() As Boolean
        Get
            Return mReprocessExistingFiles
        End Get
        Set(ByVal value As Boolean)
            mReprocessExistingFiles = value
        End Set
    End Property

    Public Property ReprocessIfCachedSizeIsZero() As Boolean
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
    Public Property SaveTICAndBPIPlots() As Boolean
        Get
            Return mSaveTICAndBPIPlots
        End Get
        Set(ByVal value As Boolean)
            mSaveTICAndBPIPlots = value
        End Set
    End Property

    ''' <summary>
    ''' If True, then saves a 2D plot of m/z vs. Intensity (requires reading every data point in the data file, which will slow down the processing)
    ''' </summary>
    ''' <value></value>
    Public Property SaveLCMS2DPlots() As Boolean
        Get
            Return mSaveLCMS2DPlots
        End Get
        Set(ByVal value As Boolean)
            mSaveLCMS2DPlots = value
        End Set
    End Property

    ''' <summary>
    ''' If True, then saves/loads data from/to the cache files (DatasetTimeFile.txt and FolderIntegrityInfo.txt)
    ''' If you simply want to create TIC and BPI files, and/or the _DatasetInfo.xml file for a single dataset, then set this to False
    ''' </summary>
    Public Property UseCacheFiles() As Boolean
        Get
            Return mUseCacheFiles
        End Get
        Set(ByVal value As Boolean)
            mUseCacheFiles = value
        End Set
    End Property

    Public Property ZipFileCheckAllData() As Boolean
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
            If System.DateTime.Now.Subtract(dtLastCheckTime).TotalSeconds < 15 Then
                Exit Sub
            End If

            dtLastCheckTime = System.DateTime.Now

            If System.IO.File.Exists(ABORT_PROCESSING_FILENAME) Then
                mAbortProcessing = True
                Try
                    If System.IO.File.Exists(ABORT_PROCESSING_FILENAME & ".done") Then
                        System.IO.File.Delete(ABORT_PROCESSING_FILENAME & ".done")
                    End If
                    System.IO.File.Move(ABORT_PROCESSING_FILENAME, ABORT_PROCESSING_FILENAME & ".done")
                Catch ex As System.Exception
                    ' Ignore errors here
                End Try
            End If
        Catch ex As System.Exception
            ' Ignore errors here
        End Try
    End Sub

    Private Sub CheckIntegrityOfFilesInFolder(ByVal strFolderPath As String, _
                                              ByVal blnForceRecheck As Boolean, _
                                              ByRef strProcessedFileList() As String)

        Dim ioFolderInfo As System.IO.DirectoryInfo
        Dim intFileCount As Integer

        Dim objRow As System.Data.DataRow

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

            ioFolderInfo = New System.IO.DirectoryInfo(strFolderPath)
            intFileCount = ioFolderInfo.GetFiles.Length

            If intFileCount > 0 Then
                blnCheckFolder = True
                If mUseCacheFiles AndAlso Not blnForceRecheck Then
                    If mMSFileInfoDataCache.CachedFolderIntegrityInfoContainsFolder(ioFolderInfo.FullName, intFolderID, objRow) Then
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
                    udtFolderStats = clsFileIntegrityChecker.GetNewFolderStats(ioFolderInfo.FullName)
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

        Catch ex As System.Exception
            HandleException("Error calling mFileIntegrityChecker", ex)
        End Try

    End Sub

    Public Shared Function GetAppFolderPath() As String
        ' Could use Application.StartupPath, but .GetExecutingAssembly is better
        Return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    End Function

    Public Function GetKnownFileExtensions() As String()
        Dim strExtensionsToParse(2) As String

        strExtensionsToParse(0) = clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION
        strExtensionsToParse(1) = clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION
        strExtensionsToParse(2) = clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION

        Return strExtensionsToParse
    End Function

    Public Function GetKnownFolderExtensions() As String()
        Dim strExtensionsToParse(1) As String

        strExtensionsToParse(0) = clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION
        strExtensionsToParse(1) = clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION

        Return strExtensionsToParse
    End Function

    Public Function GetErrorMessage() As String
        ' Returns String.Empty if no error

        Dim strErrorMessage As String

        Select Case mErrorCode
            Case eMSFileScannerErrorCodes.NoError
                strErrorMessage = String.Empty
            Case eMSFileScannerErrorCodes.InvalidInputFilePath
                strErrorMessage = "Invalid input file path"
            Case eMSFileScannerErrorCodes.InvalidOutputFolderPath
                strErrorMessage = "Invalid output folder path"
            Case eMSFileScannerErrorCodes.ParameterFileNotFound
                strErrorMessage = "Parameter file not found"
            Case eMSFileScannerErrorCodes.FilePathError
                strErrorMessage = "General file path error"

            Case eMSFileScannerErrorCodes.ParameterFileReadError
                strErrorMessage = "Parameter file read error"
            Case eMSFileScannerErrorCodes.UnknownFileExtension
                strErrorMessage = "Unknown file extension"
            Case eMSFileScannerErrorCodes.InputFileReadError
                strErrorMessage = "Input file read error"
            Case eMSFileScannerErrorCodes.InputFileAccessError
                strErrorMessage = "Input file access error"
            Case eMSFileScannerErrorCodes.OutputFileWriteError
                strErrorMessage = "Error writing output file"
            Case eMSFileScannerErrorCodes.FileIntegrityCheckError
                strErrorMessage = "Error checking file integrity"
            Case eMSFileScannerErrorCodes.DatabasePostingError
                strErrorMessage = "Database posting error"

            Case eMSFileScannerErrorCodes.UnspecifiedError
                strErrorMessage = "Unspecified localized error"

            Case Else
                ' This shouldn't happen
                strErrorMessage = "Unknown error state"
        End Select

        Return strErrorMessage

    End Function

    Private Function GetFileOrFolderInfo(ByVal strFileOrFolderPath As String, ByRef blnIsFolder As Boolean, ByRef objFileSystemInfo As System.IO.FileSystemInfo) As Boolean

        Dim ioFileInfo As System.IO.FileInfo
        Dim ioFolderInfo As System.IO.DirectoryInfo
        Dim blnExists As Boolean

        ' See if strFileOrFolderPath points to a valid file
        ioFileInfo = New System.IO.FileInfo(strFileOrFolderPath)

        If ioFileInfo.Exists() Then
            objFileSystemInfo = ioFileInfo
            blnExists = True
            blnIsFolder = False
        Else
            ' See if strFileOrFolderPath points to a folder
            ioFileInfo = Nothing
            ioFolderInfo = New System.IO.DirectoryInfo(strFileOrFolderPath)
            If ioFolderInfo.Exists Then
                objFileSystemInfo = ioFolderInfo
                blnExists = True
                blnIsFolder = True
            Else
                blnExists = False
            End If
        End If

        Return blnExists

    End Function

    Protected Sub HandleException(ByVal strBaseMessage As String, ByVal ex As System.Exception)
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
                    mLogFilePath = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    mLogFilePath &= "_log_" & System.DateTime.Now.ToString("yyyy-MM-dd") & ".txt"
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
                        If Not System.IO.Directory.Exists(mLogFolderPath) Then
                            System.IO.Directory.CreateDirectory(mLogFolderPath)
                        End If
                    End If
                Catch ex As System.Exception
                    mLogFolderPath = String.Empty
                End Try

                If mLogFolderPath.Length > 0 Then
                    mLogFilePath = System.IO.Path.Combine(mLogFolderPath, mLogFilePath)
                End If

                blnOpeningExistingFile = System.IO.File.Exists(mLogFilePath)

                mLogFile = New System.IO.StreamWriter(New System.IO.FileStream(mLogFilePath, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read))
                mLogFile.AutoFlush = True

                If Not blnOpeningExistingFile Then
                    mLogFile.WriteLine("Date" & ControlChars.Tab & _
                                       "Type" & ControlChars.Tab & _
                                       "Message")
                End If

            Catch ex As System.Exception
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

            mLogFile.WriteLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & _
                               strMessageType & ControlChars.Tab & _
                               strMessage)
        End If

    End Sub

    Private Sub InitializeLocalVariables()
        mErrorCode = eMSFileScannerErrorCodes.NoError

        mIgnoreErrorsWhenRecursing = False

        mUseCacheFiles = False

        mLogMessagesToFile = False
        mLogFilePath = String.Empty
        mLogFolderPath = String.Empty

        mReprocessExistingFiles = False
        mReprocessIfCachedSizeIsZero = False
        mRecheckFileIntegrityForExistingFolders = False

        mSaveTICAndBPIPlots = False
        mSaveLCMS2DPlots = False

        mLCMS2DPlotOptions = New clsLCMSDataPlotter.clsOptions
        mLCMS2DOverviewPlotDivisor = clsMSFileInfoProcessorBaseClass.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR

        mComputeOverallQualityScores = False
        mCreateDatasetInfoFile = False

        mCheckFileIntegrity = False

        mDSInfoConnectionString = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;"
        mDSInfoDBPostingEnabled = False
        mDSInfoStoredProcedure = "UpdateDatasetFileInfoXML"

        mFileIntegrityDetailsFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(eDataFileTypeConstants.FileIntegrityDetails))
        mFileIntegrityErrorsFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(eDataFileTypeConstants.FileIntegrityErrors))

        mMSFileInfoDataCache.InitializeVariables()

    End Sub

    Public Function LoadParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

        Dim objSettingsFile As New XmlSettingsFileAccessor

        Try

            If strParameterFilePath Is Nothing OrElse strParameterFilePath.Length = 0 Then
                ' No parameter file specified; nothing to load
                Return True
            End If

            If Not System.IO.File.Exists(strParameterFilePath) Then
                ' See if strParameterFilePath points to a file in the same directory as the application
                strParameterFilePath = System.IO.Path.Combine(GetAppFolderPath(), System.IO.Path.GetFileName(strParameterFilePath))
                If Not System.IO.File.Exists(strParameterFilePath) Then
                    ShowErrorMessage("Parameter file not found: " & strParameterFilePath)
                    SetErrorCode(eMSFileScannerErrorCodes.ParameterFileNotFound)
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

                        Me.SaveTICAndBPIPlots = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveTICAndBPIPlots", Me.SaveTICAndBPIPlots)
                        Me.SaveLCMS2DPlots = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "SaveLCMS2DPlots", Me.SaveLCMS2DPlots)

                        Me.LCMS2DPlotMZResolution = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMZResolution", Me.LCMS2DPlotMZResolution)
                        Me.LCMS2DPlotMinPointsPerSpectrum = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinPointsPerSpectrum", Me.LCMS2DPlotMinPointsPerSpectrum)

                        Me.LCMS2DPlotMaxPointsToPlot = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMaxPointsToPlot", Me.LCMS2DPlotMaxPointsToPlot)
                        Me.LCMS2DPlotMinIntensity = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DPlotMinIntensity", Me.LCMS2DPlotMinIntensity)

                        Me.LCMS2DOverviewPlotDivisor = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "LCMS2DOverviewPlotDivisor", Me.LCMS2DOverviewPlotDivisor)

                        Me.ComputeOverallQualityScores = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "ComputeOverallQualityScores", Me.ComputeOverallQualityScores)
                        Me.CreateDatasetInfoFile = .GetParam(XML_SECTION_MSFILESCANNER_SETTINGS, "CreateDatasetInfoFile", Me.CreateDatasetInfoFile)

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

        Catch ex As System.Exception
            HandleException("Error in LoadParameterFileSettings", ex)
            Return False
        End Try

        Return True

    End Function

    'Private Sub LogErrors(ByVal strSource As String, ByVal strMessage As String, ByVal ex As System.Exception, Optional ByVal blnAllowInformUser As Boolean = True, Optional ByVal blnAllowThrowingException As Boolean = True, Optional ByVal blnLogLocalOnly As Boolean = True, Optional ByVal eNewErrorCode As eMSFileScannerErrorCodes = eMSFileScannerErrorCodes.NoError)
    '    Dim strMessageWithoutCRLF As String
    '    Dim fsErrorLogFile As System.IO.StreamWriter

    '    mStatusMessage = String.Copy(strMessage)

    '    strMessageWithoutCRLF = mStatusMessage.Replace(ControlChars.NewLine, "; ")

    '    If ex Is Nothing Then
    '        ex = New System.Exception("Error")
    '    Else
    '        If Not ex.Message Is Nothing AndAlso ex.Message.Length > 0 Then
    '            strMessageWithoutCRLF &= "; " & ex.Message
    '        End If
    '    End If

    '    ShowErrorMessage(strSource & ": " & strMessageWithoutCRLF)

    '    Try
    '        fsErrorLogFile = New System.IO.StreamWriter("MSFileInfoScanner_Errors.txt", True)
    '        fsErrorLogFile.WriteLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & strSource & ControlChars.Tab & strMessageWithoutCRLF)
    '    Catch ex2 As System.Exception
    '        ' Ignore errors here
    '    Finally
    '        If Not fsErrorLogFile Is Nothing Then
    '            fsErrorLogFile.Close()
    '        End If
    '    End Try

    '    If Not eNewErrorCode = eMSFileScannerErrorCodes.NoError Then
    '        SetErrorCode(eNewErrorCode, True)
    '    End If

    '    If Me.ShowMessages AndAlso blnAllowInformUser Then
    '        System.Windows.Forms.MessageBox.Show(mStatusMessage & ControlChars.NewLine & ex.Message, "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
    '    ElseIf blnAllowThrowingException Then
    '        Throw New System.Exception(mStatusMessage, ex)
    '    End If
    'End Sub

    Protected Sub OpenFileIntegrityDetailsFile()
        OpenFileIntegrityOutputFile(eDataFileTypeConstants.FileIntegrityDetails, mFileIntegrityDetailsFilePath, mFileIntegrityDetailsWriter)
    End Sub

    Protected Sub OpenFileIntegrityErrorsFile()
        OpenFileIntegrityOutputFile(eDataFileTypeConstants.FileIntegrityErrors, mFileIntegrityErrorsFilePath, mFileIntegrityErrorsWriter)
    End Sub

    Protected Sub OpenFileIntegrityOutputFile(ByVal eDataFileType As eDataFileTypeConstants, ByRef strFilePath As String, ByRef objStreamWriter As System.IO.StreamWriter)
        Dim blnOpenedExistingFile As Boolean
        Dim fsFileStream As System.IO.FileStream
        Dim strDefaultFileName As String

        strDefaultFileName = DefaultDataFileName(eDataFileType)
        ValidateDataFilePath(strFilePath, eDataFileType)

        Try
            If System.IO.File.Exists(strFilePath) Then
                blnOpenedExistingFile = True
            End If
            fsFileStream = New System.IO.FileStream(strFilePath, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read)

        Catch ex As System.Exception
            HandleException("Error opening/creating " & strFilePath & "; will try " & strDefaultFileName, ex)

            Try
                If System.IO.File.Exists(strDefaultFileName) Then
                    blnOpenedExistingFile = True
                End If

                fsFileStream = New System.IO.FileStream(strDefaultFileName, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read)
            Catch ex2 As System.Exception
                HandleException("Error opening/creating " & strDefaultFileName, ex2)
            End Try
        End Try

        Try
            If Not fsFileStream Is Nothing Then
                objStreamWriter = New System.IO.StreamWriter(fsFileStream)

                If Not blnOpenedExistingFile Then
                    objStreamWriter.WriteLine(mMSFileInfoDataCache.ConstructHeaderLine(eDataFileType))
                End If
            End If
        Catch ex As System.Exception
            HandleException("Error opening/creating the StreamWriter for " & fsFileStream.Name, ex)
        End Try

    End Sub

    ''' <summary>
    ''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
    ''' </summary>
    ''' <returns>True if success; false if failure</returns>
    Public Function PostDatasetInfoToDB() As Boolean
        Return PostDatasetInfoToDB(mDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure)
    End Function

    ''' <summary>
    ''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
    ''' </summary>
    ''' <param name="strDatasetInfoXML">Database info XML</param>
    ''' <returns>True if success; false if failure</returns>
    Public Function PostDatasetInfoToDB(ByVal strDatasetInfoXML As String) As Boolean
        Return PostDatasetInfoToDB(strDatasetInfoXML, mDSInfoConnectionString, mDSInfoStoredProcedure)
    End Function

    ''' <summary>
    ''' Post the most recently determine dataset into XML to the database, using the specified connection string and stored procedure
    ''' </summary>
    ''' <param name="strConnectionString">Database connection string</param>
    ''' <param name="strStoredProcedure">Stored procedure</param>
    ''' <returns>True if success; false if failure</returns>
    Public Function PostDatasetInfoToDB(ByVal strConnectionString As String, _
                                        ByVal strStoredProcedure As String) As Boolean

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
                                        ByVal strStoredProcedure As String) As Boolean

        Const MAX_RETRY_COUNT As Integer = 3
        Const SEC_BETWEEN_RETRIES As Integer = 20

        Dim intStartIndex As Integer
        Dim intResult As Integer

        Dim strDSInfoXMLClean As String

        Dim objCommand As System.Data.SqlClient.SqlCommand

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
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
                Return False
            End If

            If strStoredProcedure Is Nothing OrElse strStoredProcedure.Length = 0 Then
                strStoredProcedure = "UpdateDatasetFileInfoXML"
            End If

            objCommand = New System.Data.SqlClient.SqlCommand()

            With objCommand
                .CommandType = CommandType.StoredProcedure
                .CommandText = strStoredProcedure

                .Parameters.Add(New SqlClient.SqlParameter("@Return", SqlDbType.Int))
                .Parameters.Item("@Return").Direction = ParameterDirection.ReturnValue

                .Parameters.Add(New SqlClient.SqlParameter("@DatasetInfoXML", SqlDbType.Xml))
                .Parameters.Item("@DatasetInfoXML").Direction = ParameterDirection.Input
                .Parameters.Item("@DatasetInfoXML").Value = strDSInfoXMLClean
            End With

            mExecuteSP = New clsExecuteDatabaseSP(strConnectionString)

            intResult = mExecuteSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES)

            If intResult = clsExecuteDatabaseSP.RET_VAL_OK Then
                ' No errors
                blnSuccess = True
            Else
                ShowErrorMessage("Error calling stored procedure, return code = " & intResult)
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
                blnSuccess = False
            End If

            ' Uncomment this to test calling PostDatasetInfoToDB with a DatasetID value
            ' Note that dataset Shew119-01_17july02_earth_0402-10_4-20 is DatasetID 6787
            ' PostDatasetInfoToDB(32, strDatasetInfoXML, "Data Source=gigasax;Initial Catalog=DMS_Capture_T3;Integrated Security=SSPI;", "CacheDatasetInfoXML")

        Catch ex As System.Exception
            HandleException("Error calling stored procedure", ex)
            SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
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
                                                ByVal strStoredProcedure As String) As Boolean

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
                                                ByVal strStoredProcedure As String) As Boolean

        Const MAX_RETRY_COUNT As Integer = 3
        Const SEC_BETWEEN_RETRIES As Integer = 20

        Dim intStartIndex As Integer
        Dim intResult As Integer

        Dim strDSInfoXMLClean As String

        Dim objCommand As System.Data.SqlClient.SqlCommand

        Dim blnSuccess As Boolean

        Try
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
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
                Return False
            End If

            If strStoredProcedure Is Nothing OrElse strStoredProcedure.Length = 0 Then
                strStoredProcedure = "CacheDatasetInfoXML"
            End If

            objCommand = New System.Data.SqlClient.SqlCommand()

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

            mExecuteSP = New clsExecuteDatabaseSP(strConnectionString)

            intResult = mExecuteSP.ExecuteSP(objCommand, MAX_RETRY_COUNT, SEC_BETWEEN_RETRIES)

            If intResult = clsExecuteDatabaseSP.RET_VAL_OK Then
                ' No errors
                blnSuccess = True
            Else
                ShowErrorMessage("Error calling stored procedure, return code = " & intResult)
                SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
                blnSuccess = False
            End If

        Catch ex As System.Exception
            HandleException("Error calling stored procedure", ex)
            SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
            blnSuccess = False
        Finally
            mExecuteSP = Nothing
        End Try

        Return blnSuccess
    End Function

    Private Function ProcessMSDataset( _
            ByVal strInputFileOrFolderPath As String, _
            ByRef objMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor, _
            ByVal strDatasetName As String, _
            ByVal strOutputFolderPath As String) As Boolean

        Dim udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType
        Dim intRetryCount As Integer

        Dim blnSuccess As Boolean

        ' Open the MS datafile (or data folder), read the creation date, and update the status file

        intRetryCount = 0
        Do
            ' Set the processing options
            objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, mSaveTICAndBPIPlots)
            objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots, mSaveLCMS2DPlots)
            objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, mComputeOverallQualityScores)
            objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, mCreateDatasetInfoFile)

            objMSInfoScanner.LCMS2DPlotOptions = mLCMS2DPlotOptions
            objMSInfoScanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor

            ' Process the data file
            blnSuccess = objMSInfoScanner.ProcessDatafile(strInputFileOrFolderPath, udtFileInfo)

            If Not blnSuccess Then
                intRetryCount += 1

                If intRetryCount < MAX_FILE_READ_ACCESS_ATTEMPTS Then
                    ' Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time
                    If System.DateTime.Now.Subtract(udtFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES OrElse _
                       System.DateTime.Now.Subtract(udtFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES Then

                        ' Sleep for 10 seconds then try again
                        System.Threading.Thread.Sleep(10000)
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
                SetErrorCode(eMSFileScannerErrorCodes.OutputFileWriteError)
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
                    SetErrorCode(eMSFileScannerErrorCodes.DatabasePostingError)
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
    Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, _
                                          ByVal strOutputFolderPath As String) As Boolean

        Dim eMSFileProcessingState As eMSFileProcessingStateConstants

        Return ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, True, eMSFileProcessingState)
    End Function

    Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, _
                                          ByVal strOutputFolderPath As String, _
                                          ByVal blnResetErrorCode As Boolean, _
                                          ByRef eMSFileProcessingState As eMSFileProcessingStateConstants) As Boolean

        ' Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
        ' See function ProcessMSFilesAndRecurseFolders for more details
        ' This function returns True if it processed a file (or the dataset was processed previously)
        ' When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
        ' If the file type is unknown, or if an error occurs, then it returns false
        ' eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

        Dim blnSuccess As Boolean
        Dim blnIsFolder As Boolean
        Dim blnKnownMSDataType As Boolean

        Dim objFileSystemInfo As System.IO.FileSystemInfo

        Dim objRow As System.Data.DataRow
        Dim lngCachedSizeBytes As Long

        If blnResetErrorCode Then
            SetErrorCode(eMSFileScannerErrorCodes.NoError)
        End If

        eMSFileProcessingState = eMSFileProcessingStateConstants.NotProcessed

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
                ShowMessage(" Parsing " & System.IO.Path.GetFileName(strInputFileOrFolderPath))

                ' Determine whether strInputFileOrFolderPath points to a file or a folder

                If Not GetFileOrFolderInfo(strInputFileOrFolderPath, blnIsFolder, objFileSystemInfo) Then
                    ShowErrorMessage("File or folder not found: " & strInputFileOrFolderPath)
                    If SKIP_FILES_IN_ERROR Then
                        Return True
                    Else
                        SetErrorCode(eMSFileScannerErrorCodes.FilePathError)
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
                        Select Case System.IO.Path.GetExtension(strInputFileOrFolderPath).ToUpper
                            Case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION
                                ' Agilent .D folder
                                mMSInfoScanner = New clsAgilentIonTrapDFolderInfoScanner
                                blnKnownMSDataType = True
                            Case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION
                                ' Micromass .Raw folder
                                mMSInfoScanner = New clsMicromassRawFolderInfoScanner
                                blnKnownMSDataType = True
                            Case Else
                                ' Unknown folder extension
                        End Select
                    End If
                Else
                    ' Examine the extension on strInputFileOrFolderPath
                    Select Case objFileSystemInfo.Extension.ToUpper
                        Case clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION
                            mMSInfoScanner = New clsFinniganRawFileInfoScanner
                            blnKnownMSDataType = True
                        Case clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION
                            mMSInfoScanner = New clsAgilentTOFOrQStarWiffFileInfoScanner
                            blnKnownMSDataType = True
                        Case clsBrukerXmassFolderInfoScanner.BRUKER_BAF_FILE_EXTENSION
                            mMSInfoScanner = New clsBrukerXmassFolderInfoScanner
                            blnKnownMSDataType = True
                        Case Else
                            ' Unknown file extension; check for a zipped folder 
                            If clsBrukerOneFolderInfoScanner.IsZippedSFolder(objFileSystemInfo.Name) Then
                                ' Bruker s001.zip file
                                mMSInfoScanner = New clsBrukerOneFolderInfoScanner
                                blnKnownMSDataType = True
                            End If
                    End Select
                End If

                If Not blnKnownMSDataType Then
                    ShowErrorMessage("Unknown file type: " & System.IO.Path.GetFileName(strInputFileOrFolderPath))
                    SetErrorCode(eMSFileScannerErrorCodes.UnknownFileExtension)
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
                            Catch ex2 As System.Exception
                                lngCachedSizeBytes = 1
                            End Try

                            If lngCachedSizeBytes > 0 Then
                                ' File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                ShowMessage("  Skipping " & System.IO.Path.GetFileName(strInputFileOrFolderPath) & " since already in cached results")
                                eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache
                                Return True
                            End If
                        Else
                            ' File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                            ShowMessage("  Skipping " & System.IO.Path.GetFileName(strInputFileOrFolderPath) & " since already in cached results")
                            eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache
                            Return True
                        End If
                    End If
                End If

                ' Process the data file or folder
                blnSuccess = ProcessMSDataset(strInputFileOrFolderPath, mMSInfoScanner, strDatasetName, strOutputFolderPath)
                If blnSuccess Then
                    eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully
                Else
                    eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing
                End If

            End If

        Catch ex As System.Exception
            HandleException("Error in ProcessMSFileOrFolder", ex)
            blnSuccess = False
        Finally
            mMSInfoScanner = Nothing
        End Try

        Return blnSuccess

    End Function

    Public Function ProcessMSFileOrFolderWildcard(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean) As Boolean
        ' Returns True if success, False if failure

        Dim blnSuccess As Boolean
        Dim intMatchCount As Integer

        Dim strCleanPath As String
        Dim strInputFolderPath As String

        Dim ioFileMatch As System.IO.FileInfo
        Dim ioFolderMatch As System.IO.DirectoryInfo

        Dim ioFileInfo As System.IO.FileInfo
        Dim ioFolderInfo As System.IO.DirectoryInfo

        Dim eMSFileProcessingState As eMSFileProcessingStateConstants

        Dim intProcessedFileListCount As Integer
        Dim strProcessedFileList As String()

        intProcessedFileListCount = 0
        ReDim strProcessedFileList(4)

        mAbortProcessing = False
        blnSuccess = True
        Try
            ' Possibly reset the error code
            If blnResetErrorCode Then
                SetErrorCode(eMSFileScannerErrorCodes.NoError)
            End If

            ' See if strInputFilePath contains a wildcard
            If Not strInputFileOrFolderPath Is Nothing AndAlso (strInputFileOrFolderPath.IndexOf("*") >= 0 Or strInputFileOrFolderPath.IndexOf("?") >= 0) Then
                ' Obtain a list of the matching files and folders

                ' Copy the path into strCleanPath and replace any * or ? characters with _
                strCleanPath = strInputFileOrFolderPath.Replace("*", "_")
                strCleanPath = strCleanPath.Replace("?", "_")

                ioFileInfo = New System.IO.FileInfo(strCleanPath)
                If ioFileInfo.Directory.Exists Then
                    strInputFolderPath = ioFileInfo.DirectoryName
                Else
                    ' Use the current working directory
                    strInputFolderPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ioFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)

                ' Remove any directory information from strInputFileOrFolderPath
                strInputFileOrFolderPath = System.IO.Path.GetFileName(strInputFileOrFolderPath)

                intMatchCount = 0
                For Each ioFileMatch In ioFolderInfo.GetFiles(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)

                    If eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                       eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing Then
                        AddToStringList(strProcessedFileList, ioFileMatch.FullName, intProcessedFileListCount)
                    End If

                    CheckForAbortProcessingFile()
                    If mAbortProcessing Then Exit For

                    If Not blnSuccess And Not SKIP_FILES_IN_ERROR Then Exit For

                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next ioFileMatch
                If mAbortProcessing Then Return False

                For Each ioFolderMatch In ioFolderInfo.GetDirectories(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(ioFolderMatch.FullName, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)

                    If eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                       eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing Then
                        AddToStringList(strProcessedFileList, ioFolderMatch.FullName, intProcessedFileListCount)
                    End If

                    CheckForAbortProcessingFile()
                    If mAbortProcessing Then Exit For
                    If Not blnSuccess And Not SKIP_FILES_IN_ERROR Then Exit For

                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next ioFolderMatch
                If mAbortProcessing Then Return False

                If mCheckFileIntegrity Then
                    ReDim Preserve strProcessedFileList(intProcessedFileListCount - 1)
                    CheckIntegrityOfFilesInFolder(ioFolderInfo.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList)
                End If

                If intMatchCount = 0 Then
                    If mErrorCode = eMSFileScannerErrorCodes.NoError Then
                        ShowMessage("No match was found for the input file path:" & strInputFileOrFolderPath, eMessageTypeConstants.Warning)
                    End If
                Else
                    Console.WriteLine()
                End If
            Else
                blnSuccess = ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)
            End If

        Catch ex As System.Exception
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
                                                    ByVal intRecurseFoldersMaxLevels As Integer) As Boolean

        Dim strCleanPath As String
        Dim strInputFolderPath As String

        Dim ioFileInfo As System.IO.FileInfo
        Dim ioFolderInfo As System.IO.DirectoryInfo

        Dim blnSuccess As Boolean
        Dim intFileProcessCount, intFileProcessFailCount As Integer

        ' Examine strInputFilePathOrFolder to see if it contains a filename; if not, assume it points to a folder
        ' First, see if it contains a * or ?
        Try
            If Not strInputFilePathOrFolder Is Nothing AndAlso (strInputFilePathOrFolder.IndexOf("*") >= 0 Or strInputFilePathOrFolder.IndexOf("?") >= 0) Then
                ' Copy the path into strCleanPath and replace any * or ? characters with _
                strCleanPath = strInputFilePathOrFolder.Replace("*", "_")
                strCleanPath = strCleanPath.Replace("?", "_")

                ioFileInfo = New System.IO.FileInfo(strCleanPath)
                If System.IO.Path.IsPathRooted(strCleanPath) Then
                    If Not ioFileInfo.Directory.Exists Then
                        ShowErrorMessage("Folder not found: " & ioFileInfo.DirectoryName)
                        SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath)
                        Return False
                    End If
                End If

                If ioFileInfo.Directory.Exists Then
                    strInputFolderPath = ioFileInfo.DirectoryName
                Else
                    ' Folder not found; use the current working directory
                    strInputFolderPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ' Remove any directory information from strInputFilePath
                strInputFilePathOrFolder = System.IO.Path.GetFileName(strInputFilePathOrFolder)

            Else
                ioFolderInfo = New System.IO.DirectoryInfo(strInputFilePathOrFolder)
                If ioFolderInfo.Exists Then
                    strInputFolderPath = ioFolderInfo.FullName
                    strInputFilePathOrFolder = "*"
                Else
                    If ioFolderInfo.Parent.Exists Then
                        strInputFolderPath = ioFolderInfo.Parent.FullName
                        strInputFilePathOrFolder = System.IO.Path.GetFileName(strInputFilePathOrFolder)
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
                SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath)
                Return False
            End If

        Catch ex As System.Exception
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

        Dim ioInputFolderInfo As System.IO.DirectoryInfo
        Dim ioSubFolderInfo As System.IO.DirectoryInfo

        Dim ioFileMatch As System.IO.FileInfo

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

        Dim eMSFileProcessingState As eMSFileProcessingStateConstants

        Dim intProcessedFileListCount As Integer
        Dim strProcessedFileList() As String

        intProcessedFileListCount = 0
        ReDim strProcessedFileList(4)

        intRetryCount = 0
        Do
            Try
                ioInputFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)
                Exit Do
            Catch ex As System.Exception
                ' Input folder path error
                HandleException("Error populating ioInputFolderInfo for " & strInputFolderPath, ex)
                If Not ex.Message.Contains("no longer available") Then
                    Return False
                End If
            End Try

            intRetryCount += 1
            If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
                Return False
            Else
                ' Wait 1 second, then try again
                System.Threading.Thread.Sleep(1000)
            End If

        Loop While intRetryCount < MAX_ACCESS_ATTEMPTS


        Try
            ' Construct and validate the list of file and folder extensions to parse
            strFileExtensionsToParse = GetKnownFileExtensions()
            strFolderExtensionsToParse = GetKnownFolderExtensions()

            blnProcessAllFileExtensions = ValidateExtensions(strFileExtensionsToParse)
            ValidateExtensions(strFolderExtensionsToParse)
        Catch ex As System.Exception
            HandleException("Error in RecurseFoldersWork", ex)
            Return False
        End Try

        Try
            Console.WriteLine("Examining " & strInputFolderPath)

            ' Process any matching files in this folder
            blnSuccess = True
            blnProcessedZippedSFolder = False
            For Each ioFileMatch In ioInputFolderInfo.GetFiles(strFileNameMatch)

                intRetryCount = 0
                Do
                    Try

                        blnFileProcessed = False
                        For intExtensionIndex = 0 To strFileExtensionsToParse.Length - 1
                            If blnProcessAllFileExtensions OrElse ioFileMatch.Extension.ToUpper = strFileExtensionsToParse(intExtensionIndex) Then
                                blnFileProcessed = True
                                blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, True, eMSFileProcessingState)

                                If eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                                   eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing Then
                                    AddToStringList(strProcessedFileList, ioFileMatch.FullName, intProcessedFileListCount)
                                End If

                                Exit For
                            End If
                        Next intExtensionIndex
                        If mAbortProcessing Then Exit For

                        If Not blnFileProcessed AndAlso Not blnProcessedZippedSFolder Then
                            ' Check for other valid files
                            If clsBrukerOneFolderInfoScanner.IsZippedSFolder(ioFileMatch.Name) Then
                                ' Only process this file if there is not a subfolder named "1" present"
                                If ioInputFolderInfo.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1 Then
                                    blnFileProcessed = True
                                    blnProcessedZippedSFolder = True
                                    blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, True, eMSFileProcessingState)

                                    If eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully OrElse _
                                       eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing Then
                                        AddToStringList(strProcessedFileList, ioFileMatch.FullName, intProcessedFileListCount)
                                    End If

                                End If
                            End If
                        End If
                        Exit Do

                    Catch ex As System.Exception
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
                        System.Threading.Thread.Sleep(1000)
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
            Next ioFileMatch

            If mCheckFileIntegrity And Not mAbortProcessing Then
                ReDim Preserve strProcessedFileList(intProcessedFileListCount - 1)
                CheckIntegrityOfFilesInFolder(ioInputFolderInfo.FullName, mRecheckFileIntegrityForExistingFolders, strProcessedFileList)
            End If

        Catch ex As System.Exception
            HandleException("Error in RecurseFoldersWork Examining files in " & strInputFolderPath, ex)
            Return False
        End Try

        If Not mAbortProcessing Then
            ' Check the subfolders for those with known extensions

            Try

                intSubFoldersProcessed = 0
                htSubFoldersProcessed = New Hashtable
                For Each ioSubFolderInfo In ioInputFolderInfo.GetDirectories(strFileNameMatch)

                    intRetryCount = 0
                    Do
                        Try
                            ' Check whether the folder name is BRUKER_ONE_FOLDER = "1"
                            If ioSubFolderInfo.Name = clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME Then
                                blnSuccess = ProcessMSFileOrFolder(ioSubFolderInfo.FullName, strOutputFolderPath, True, eMSFileProcessingState)
                                If Not blnSuccess Then
                                    intFileProcessFailCount += 1
                                    blnSuccess = True
                                Else
                                    intFileProcessCount += 1
                                End If
                                intSubFoldersProcessed += 1
                                htSubFoldersProcessed.Add(ioSubFolderInfo.Name, 1)
                            Else
                                ' See if the subfolder has an extension matching strFolderExtensionsToParse()
                                ' If it does, process it using ProcessMSFileOrFolder and do not recurse into it
                                For intExtensionIndex = 0 To strFolderExtensionsToParse.Length - 1
                                    If ioSubFolderInfo.Extension.ToUpper = strFolderExtensionsToParse(intExtensionIndex) Then
                                        blnSuccess = ProcessMSFileOrFolder(ioSubFolderInfo.FullName, strOutputFolderPath, True, eMSFileProcessingState)
                                        If Not blnSuccess Then
                                            intFileProcessFailCount += 1
                                            blnSuccess = True
                                        Else
                                            intFileProcessCount += 1
                                        End If
                                        intSubFoldersProcessed += 1
                                        htSubFoldersProcessed.Add(ioSubFolderInfo.Name, 1)
                                        Exit For
                                    End If
                                Next intExtensionIndex
                                If mAbortProcessing Then Exit For

                            End If

                            Exit Do

                        Catch ex As System.Exception
                            ' Error parsing folder
                            HandleException("Error in RecurseFoldersWork at For Each ioSubFolderInfo(A) in " & strInputFolderPath, ex)
                            If Not ex.Message.Contains("no longer available") Then
                                Return False
                            End If
                        End Try

                        intRetryCount += 1
                        If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
                            Return False
                        Else
                            ' Wait 1 second, then try again
                            System.Threading.Thread.Sleep(1000)
                        End If

                    Loop While intRetryCount < MAX_ACCESS_ATTEMPTS

                    If mAbortProcessing Then Exit For

                Next ioSubFolderInfo

                ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
                '  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
                If intRecurseFoldersMaxLevels <= 0 OrElse intRecursionLevel <= intRecurseFoldersMaxLevels Then
                    ' Call this function for each of the subfolders of ioInputFolderInfo
                    ' However, do not step into folders listed in htSubFoldersProcessed
                    For Each ioSubFolderInfo In ioInputFolderInfo.GetDirectories()

                        intRetryCount = 0
                        Do
                            Try
                                If intSubFoldersProcessed = 0 OrElse Not htSubFoldersProcessed.Contains(ioSubFolderInfo.Name) Then
                                    blnSuccess = RecurseFoldersWork(ioSubFolderInfo.FullName, strFileNameMatch, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, intRecursionLevel + 1, intRecurseFoldersMaxLevels)
                                End If
                                If Not blnSuccess And Not mIgnoreErrorsWhenRecursing Then
                                    Exit For
                                End If

                                CheckForAbortProcessingFile()
                                If mAbortProcessing Then Exit For

                                Exit Do

                            Catch ex As System.Exception
                                ' Error parsing file
                                HandleException("Error in RecurseFoldersWork at For Each ioSubFolderInfo(B) in " & strInputFolderPath, ex)
                                If Not ex.Message.Contains("no longer available") Then
                                    Return False
                                End If
                            End Try

                            intRetryCount += 1
                            If intRetryCount >= MAX_ACCESS_ATTEMPTS Then
                                Return False
                            Else
                                ' Wait 1 second, then try again
                                System.Threading.Thread.Sleep(1000)
                            End If

                        Loop While intRetryCount < MAX_ACCESS_ATTEMPTS

                        If mAbortProcessing Then Exit For
                    Next ioSubFolderInfo
                End If


            Catch ex As System.Exception
                HandleException("Error in RecurseFoldersWork examining subfolders in " & strInputFolderPath, ex)
                Return False
            End Try

        End If

        Return blnSuccess

    End Function

    Public Function SaveCachedResults() As Boolean
        Return Me.SaveCachedResults(True)
    End Function

    Public Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean
        If mUseCacheFiles Then
            Return mMSFileInfoDataCache.SaveCachedResults(blnClearCachedData)
        Else
            Return True
        End If
    End Function

    Public Function SaveParameterFileSettings(ByVal strParameterFilePath As String) As Boolean

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

        Catch ex As System.Exception
            HandleException("Error in SaveParameterFileSettings", ex)
            Return False
        Finally
            objSettingsFile = Nothing
        End Try

        Return True

    End Function

    Private Sub SetErrorCode(ByVal eNewErrorCode As eMSFileScannerErrorCodes)
        SetErrorCode(eNewErrorCode, False)
    End Sub

    Private Sub SetErrorCode(ByVal eNewErrorCode As eMSFileScannerErrorCodes, ByVal blnLeaveExistingErrorCodeUnchanged As Boolean)

        If blnLeaveExistingErrorCodeUnchanged AndAlso mErrorCode <> eMSFileScannerErrorCodes.NoError Then
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

    Public Shared Function ValidateDataFilePath(ByRef strFilePath As String, ByVal eDataFileType As eDataFileTypeConstants) As Boolean
        If strFilePath Is Nothing OrElse strFilePath.Length = 0 Then
            strFilePath = System.IO.Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileType))
        End If

        ValidateDataFilePathCheckDir(strFilePath)
    End Function

    Private Shared Function ValidateDataFilePathCheckDir(ByVal strFilePath As String) As Boolean

        Dim ioFileInfo As System.IO.FileInfo
        Dim blnValidFile As Boolean

        Try
            ioFileInfo = New System.IO.FileInfo(strFilePath)

            If Not ioFileInfo.Exists Then
                ' Make sure the folder exists
                If Not ioFileInfo.Directory.Exists Then
                    ioFileInfo.Directory.Create()
                End If
            End If
            blnValidFile = True

        Catch ex As System.Exception
            ' Ignore errors, but set blnValidFile to false
            blnValidFile = False
        End Try

        Return blnValidFile
    End Function

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

    Private Sub WriteFileIntegrityDetails(ByRef srOutFile As System.IO.StreamWriter, ByVal intFolderID As Integer, ByVal udtFileStats() As clsFileIntegrityChecker.udtFileStatsType)
        Static dtLastWriteTime As DateTime

        Dim intIndex As Integer
        Dim dtTimeStamp As DateTime = System.DateTime.Now

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

        If System.DateTime.Now.Subtract(dtLastWriteTime).TotalMinutes > 1 Then
            srOutFile.Flush()
            dtLastWriteTime = System.DateTime.Now
        End If

    End Sub

    Private Sub WriteFileIntegrityFailure(ByRef srOutFile As System.IO.StreamWriter, ByVal strFilePath As String, ByVal strMessage As String)
        Static dtLastWriteTime As DateTime

        If srOutFile Is Nothing Then Exit Sub

        ' Note: HH:mm:ss corresponds to time in 24 hour format
        srOutFile.WriteLine(strFilePath & ControlChars.Tab & _
                            strMessage & ControlChars.Tab & _
                            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))

        If System.DateTime.Now.Subtract(dtLastWriteTime).TotalMinutes > 1 Then
            srOutFile.Flush()
            dtLastWriteTime = System.DateTime.Now
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
    ''    Catch ex as System.Exception
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
