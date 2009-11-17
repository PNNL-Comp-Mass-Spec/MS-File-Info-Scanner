Option Strict On

' Scans a series of MS data files (or data folders) and extracts the acquisition start and end times, 
' number of spectra, and the total size of the data.  Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
'
' Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar .WIFF files, 
' Masslynx .Raw folders, and Bruker 1 folders
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started October 11, 2003

Public Class clsMSFileScanner

    Public Sub New()
        mFileDate = modMain.PROGRAM_DATE

        mFileIntegrityChecker = New clsFileIntegrityChecker

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
    Public Const XML_SECTION_MSFILESCANNER_SETTINGS As String = "MSFileScanner"

    Private Const FILE_MODIFICATION_WINDOW_MINUTES As Integer = 60
    Private Const MAX_FILE_READ_MAX_ACCESS_ATTEMPTS As Integer = 2
    Private Const USE_XML_OUTPUT_FILE As Boolean = False
    Private Const SKIP_FILES_IN_ERROR As Boolean = True

    Private Const MS_FILEINFO_DATATABLE As String = "MSFileInfoTable"
    Private Const COL_NAME_DATASET_ID As String = "DatasetID"
    Private Const COL_NAME_DATASET_NAME As String = "DatasetName"
    Private Const COL_NAME_FILE_EXTENSION As String = "FileExtension"
    Private Const COL_NAME_ACQ_TIME_START As String = "AcqTimeStart"
    Private Const COL_NAME_ACQ_TIME_END As String = "AcqTimeEnd"
    Private Const COL_NAME_SCAN_COUNT As String = "ScanCount"
    Private Const COL_NAME_FILE_SIZE_BYTES As String = "FileSizeBytes"
    Private Const COL_NAME_INFO_LAST_MODIFIED As String = "InfoLastModified"
    Private Const COL_NAME_FILE_MODIFICATION_DATE As String = "FileModificationDate"

    Private Const FOLDER_INTEGRITY_INFO_DATATABLE As String = "FolderIntegrityInfoTable"
    Private Const COL_NAME_FOLDER_ID As String = "FolderID"
    Private Const COL_NAME_FOLDER_PATH As String = "FolderPath"
    Private Const COL_NAME_FILE_COUNT As String = "FileCount"
    Private Const COL_NAME_COUNT_FAIL_INTEGRITY As String = "FileCountFailedIntegrity"

    Private Const COL_NAME_FILE_NAME As String = "FileName"
    Private Const COL_NAME_FAILED_INTEGRITY_CHECK As String = "FailedIntegrityCheck"
    Private Const COL_NAME_SHA1_HASH As String = "Sha1Hash"


    Private Const MINIMUM_DATETIME As DateTime = #1/1/1900#     ' Equivalent to DateTime.MinValue

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

        UnspecifiedError = -1
    End Enum

    Public Enum eDataFileTypeConstants
        MSFileInfo = 0
        FolderIntegrityInfo = 1
        FileIntegrityDetails = 2
        FileIntegrityErrors = 3
    End Enum

    Public Enum eMSFileInfoResultsFileColumns
        DatasetID = 0
        DatasetName = 1
        FileExtension = 2
        AcqTimeStart = 3
        AcqTimeEnd = 4
        ScanCount = 5
        FileSizeBytes = 6
        InfoLastModified = 7
        FileModificationDate = 8
    End Enum

    Public Enum eFolderIntegrityInfoFileColumns
        FolderID = 0
        FolderPath = 1
        FileCount = 2
        FileCountFailedIntegrity = 3
        InfoLastModified = 4
    End Enum

    Public Enum eFileIntegrityDetailsFileColumns
        FolderID = 0
        FileName = 1
        FileSizeBytes = 2
        FileModified = 3
        FailedIntegrityCheck = 4
        Sha1Hash = 5
        InfoLastModified = 6
    End Enum

    Public Enum eMSFileProcessingStateConstants
        NotProcessed = 0
        SkippedSinceFoundInCache = 1
        FailedProcessing = 2
        ProcessedSuccessfully = 3
    End Enum

    Private Enum eCachedResultsStateConstants
        NotInitialized = 0
        InitializedButUnmodified = 1
        Modified = 2
    End Enum

    ''Private Enum eMSFileTypeConstants
    ''    FinniganRawFile = 0
    ''    BrukerOneFolder = 1
    ''    AgilentIonTrapDFolder = 2
    ''    MicromassRawFolder = 3
    ''    AgilentOrQStarWiffFile = 4
    ''End Enum

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

    Private mFileDate As String
    Private mErrorCode As eMSFileScannerErrorCodes
    Private mShowMessages As Boolean

    Private mStatusMessage As String
    Private mAbortProcessing As Boolean

    Private mAcquisitionTimeFilePath As String
    Private mFolderIntegrityInfoFilePath As String
    Private mFileIntegrityDetailsFilePath As String
    Private mFileIntegrityErrorsFilePath As String

    Private mDataFileSepChar As Char
    Private mIgnoreErrorsWhenRecursing As Boolean

    Private mReprocessExistingFiles As Boolean
    Private mReprocessIfCachedSizeIsZero As Boolean

    Private mRecheckFileIntegrityForExistingFolders As Boolean

    Private mSaveTICAndBPIPlots As Boolean
    Private mComputeOverallQualityScores As Boolean

    Private mCheckFileIntegrity As Boolean

    Private mCachedResultsAutoSaveIntervalMinutes As Integer
    Private mCachedMSInfoResultsLastSaveTime As DateTime
    Private mCachedFolderIntegrityInfoLastSaveTime As DateTime

    Private mMSFileInfoDataset As System.Data.DataSet
    Private mMSFileInfoCachedResultsState As eCachedResultsStateConstants

    Private mFolderIntegrityInfoDataset As System.Data.DataSet
    Private mFolderIntegrityInfoResultsState As eCachedResultsStateConstants
    Private mMaximumFolderIntegrityInfoFolderID As Integer = 0

    Private WithEvents mFileIntegrityChecker As clsFileIntegrityChecker
    Private mFileIntegrityDetailsWriter As System.IO.StreamWriter
    Private mFileIntegrityErrorsWriter As System.IO.StreamWriter
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

    Public Property AcquisitionTimeFileSepChar() As Char
        Get
            Return mDataFileSepChar
        End Get
        Set(ByVal value As Char)
            mDataFileSepChar = value
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

    Public Function GetDataFileFilename(ByVal eDataFileType As eDataFileTypeConstants) As String
        Select Case eDataFileType
            Case eDataFileTypeConstants.MSFileInfo
                Return mAcquisitionTimeFilePath
            Case eDataFileTypeConstants.FolderIntegrityInfo
                Return mFolderIntegrityInfoFilePath
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
                mAcquisitionTimeFilePath = strFilePath
            Case eDataFileTypeConstants.FolderIntegrityInfo
                mFolderIntegrityInfoFilePath = strFilePath
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

    Public Property SaveTICAndBPIPlots() As Boolean
        Get
            Return mSaveTICAndBPIPlots
        End Get
        Set(ByVal value As Boolean)
            mSaveTICAndBPIPlots = value
        End Set
    End Property

    Public Property ShowMessages() As Boolean
        Get
            Return mShowMessages
        End Get
        Set(ByVal value As Boolean)
            mShowMessages = value
        End Set
    End Property

    Public ReadOnly Property StatusMessage() As String
        Get
            Return mStatusMessage
        End Get
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

    Private Function AssureMinimumDate(ByVal dtDate As DateTime, ByVal dtMinimumDate As DateTime) As DateTime
        ' Assures that dtDate is >= dtMinimumDate

        If dtDate < dtMinimumDate Then
            Return dtMinimumDate
        Else
            Return dtDate
        End If

    End Function

    Private Sub AutosaveCachedResults()

        If mCachedResultsAutoSaveIntervalMinutes > 0 Then
            If mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified Then
                If System.DateTime.Now.Subtract(mCachedMSInfoResultsLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes Then
                    ' Auto save the cached results
                    SaveCachedMSInfoResults(False)
                End If
            End If

            If mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified Then
                If System.DateTime.Now.Subtract(mCachedFolderIntegrityInfoLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes Then
                    ' Auto save the cached results
                    SaveCachedFolderIntegrityInfoResults(False)
                End If
            End If
        End If

    End Sub

    Private Function CachedMSInfoContainsDataset(ByVal strDatasetName As String) As Boolean
        Return CachedMSInfoContainsDataset(strDatasetName, Nothing)
    End Function

    Private Function CachedMSInfoContainsDataset(ByVal strDatasetName As String, ByRef objRowMatch As System.Data.DataRow) As Boolean
        Return DatasetTableContainsPrimaryKeyValue(mMSFileInfoDataset, MS_FILEINFO_DATATABLE, strDatasetName, objRowMatch)
    End Function


    Private Function CachedFolderIntegrityInfoContainsFolder(ByVal strFolderPath As String, ByRef intFolderID As Integer) As Boolean
        Return CachedFolderIntegrityInfoContainsFolder(strFolderPath, intFolderID, Nothing)
    End Function

    Private Function CachedFolderIntegrityInfoContainsFolder(ByVal strFolderPath As String, ByRef intFolderID As Integer, ByRef objRowMatch As System.Data.DataRow) As Boolean
        If DatasetTableContainsPrimaryKeyValue(mFolderIntegrityInfoDataset, FOLDER_INTEGRITY_INFO_DATATABLE, strFolderPath, objRowMatch) Then
            intFolderID = CInt(objRowMatch(COL_NAME_FOLDER_ID))
            Return True
        Else
            Return False
        End If
    End Function

    Private Function DatasetTableContainsPrimaryKeyValue(ByRef dsDataset As System.Data.DataSet, ByVal strTableName As String, ByVal strValueToFind As String) As Boolean
        Return DatasetTableContainsPrimaryKeyValue(dsDataset, strTableName, strValueToFind, Nothing)
    End Function

    Private Function DatasetTableContainsPrimaryKeyValue(ByRef dsDataset As System.Data.DataSet, ByVal strTableName As String, ByVal strValueToFind As String, ByRef objRowMatch As System.Data.DataRow) As Boolean

        Try
            If dsDataset Is Nothing OrElse dsDataset.Tables(strTableName).Rows.Count = 0 Then
                objRowMatch = Nothing
                Return False
            End If

            ' Look for strValueToFind in dsDataset
            Try
                objRowMatch = dsDataset.Tables(strTableName).Rows.Find(strValueToFind)

                If objRowMatch Is Nothing Then
                    Return False
                Else
                    Return True
                End If
            Catch ex As System.Exception
                Return False
            End Try

        Catch ex As System.Exception
            Return False
        End Try

    End Function

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

    Private Sub CheckIntegrityOfFilesInFolder(ByVal strFolderPath As String, ByVal blnForceRecheck As Boolean, ByRef strProcessedFileList() As String)

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
                If Not blnForceRecheck Then
                    If CachedFolderIntegrityInfoContainsFolder(ioFolderInfo.FullName, intFolderID, objRow) Then
                        intCachedFileCount = CInt(objRow(COL_NAME_FILE_COUNT))
                        intCachedCountFailIntegrity = CInt(objRow(COL_NAME_COUNT_FAIL_INTEGRITY))

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

                    If Not UpdateCachedFolderIntegrityInfo(udtFolderStats, intFolderID) Then
                        intFolderID = -1
                    End If

                    WriteFileIntegrityDetails(mFileIntegrityDetailsWriter, intFolderID, udtFileStats)

                End If
            End If

        Catch ex As System.Exception
            LogErrors("CheckIntegrityOfFilesInFolder", "Error calling mFileIntegrityChecker", ex, True, False, True, eMSFileScannerErrorCodes.FileIntegrityCheckError)
        End Try

    End Sub

    Private Sub ClearCachedMSInfoResults()
        mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Clear()
        mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized
    End Sub

    Private Sub ClearCachedFolderIntegrityInfoResults()
        mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Clear()
        mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized
        mMaximumFolderIntegrityInfoFolderID = 0
    End Sub

    Private Function ConstructHeaderLine(ByVal eDataFileType As eDataFileTypeConstants) As String
        Select Case eDataFileType
            Case eDataFileTypeConstants.MSFileInfo
                ' Note: The order of the output should match eMSFileInfoResultsFileColumns
                Return COL_NAME_DATASET_ID & mDataFileSepChar & _
                        COL_NAME_DATASET_NAME & mDataFileSepChar & _
                        COL_NAME_FILE_EXTENSION & mDataFileSepChar & _
                        COL_NAME_ACQ_TIME_START & mDataFileSepChar & _
                        COL_NAME_ACQ_TIME_END & mDataFileSepChar & _
                        COL_NAME_SCAN_COUNT & mDataFileSepChar & _
                        COL_NAME_FILE_SIZE_BYTES & mDataFileSepChar & _
                        COL_NAME_INFO_LAST_MODIFIED & mDataFileSepChar & _
                        COL_NAME_FILE_MODIFICATION_DATE

            Case eDataFileTypeConstants.FolderIntegrityInfo
                ' Note: The order of the output should match eFolderIntegrityInfoFileColumns
                Return COL_NAME_FOLDER_ID & mDataFileSepChar & _
                        COL_NAME_FOLDER_PATH & mDataFileSepChar & _
                        COL_NAME_FILE_COUNT & mDataFileSepChar & _
                        COL_NAME_COUNT_FAIL_INTEGRITY & mDataFileSepChar & _
                        COL_NAME_INFO_LAST_MODIFIED

            Case eDataFileTypeConstants.FileIntegrityDetails
                ' Note: The order of the output should match eFileIntegrityDetailsFileColumns
                Return COL_NAME_FOLDER_ID & mDataFileSepChar & _
                        COL_NAME_FILE_NAME & mDataFileSepChar & _
                        COL_NAME_FILE_SIZE_BYTES & mDataFileSepChar & _
                        COL_NAME_FILE_MODIFICATION_DATE & mDataFileSepChar & _
                        COL_NAME_FAILED_INTEGRITY_CHECK & mDataFileSepChar & _
                        COL_NAME_SHA1_HASH & mDataFileSepChar & _
                        COL_NAME_INFO_LAST_MODIFIED

            Case eDataFileTypeConstants.FileIntegrityErrors
                Return "File_Path" & mDataFileSepChar & "Error_Message" & mDataFileSepChar & COL_NAME_INFO_LAST_MODIFIED
            Case Else
                Return "Unknown_File_Type"
        End Select
    End Function

    Public Shared Function GetAppFolderPath() As String
        ' Could use Application.StartupPath, but .GetExecutingAssembly is better
        Return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    End Function

    Public Function GetKnownFileExtensions() As String()
        Dim strExtensionsToParse(1) As String

        strExtensionsToParse(0) = clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION
        strExtensionsToParse(1) = clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION

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

    Private Sub LoadCachedResults()
        LoadCachedMSFileInfoResults()
        LoadCachedFolderIntegrityInfoResults()
    End Sub

    Private Sub LoadCachedFolderIntegrityInfoResults()

        Dim fsInFile As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim strLineIn As String
        Dim strSplitLine() As String
        Dim strSepChars() As Char

        Dim intFolderID As Integer
        Dim strFolderPath As String
        Dim udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType
        Dim dtInfoLastModified As DateTime

        Dim objNewRow As System.Data.DataRow

        strSepChars = New Char() {mDataFileSepChar}

        ' Clear the Folder Integrity Info Table
        ClearCachedFolderIntegrityInfoResults()

        ValidateDataFilePath(mFolderIntegrityInfoFilePath, eDataFileTypeConstants.FolderIntegrityInfo)

        If System.IO.File.Exists(mFolderIntegrityInfoFilePath) Then
            ' Read the entries from mFolderIntegrityInfoFilePath, populating mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE)

            If USE_XML_OUTPUT_FILE Then
                fsInFile = New System.IO.FileStream(mFolderIntegrityInfoFilePath, IO.FileMode.OpenOrCreate, IO.FileAccess.ReadWrite, IO.FileShare.Read)
                mFolderIntegrityInfoDataset.ReadXml(fsInFile)
                fsInFile.Close()
            Else
                srInFile = New System.IO.StreamReader(mFolderIntegrityInfoFilePath)
                Do While srInFile.Peek() >= 0
                    strLineIn = srInFile.ReadLine()

                    If Not strLineIn Is Nothing Then
                        strSplitLine = strLineIn.Split(strSepChars)

                        If strSplitLine.Length >= 5 Then
                            strFolderPath = strSplitLine(eFolderIntegrityInfoFileColumns.FolderPath)

                            If IsNumber(strSplitLine(eFolderIntegrityInfoFileColumns.FolderID)) Then
                                If Not CachedFolderIntegrityInfoContainsFolder(strFolderPath, intFolderID) Then
                                    Try
                                        objNewRow = mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).NewRow()

                                        With udtFolderStats
                                            intFolderID = CType(strSplitLine(eFolderIntegrityInfoFileColumns.FolderID), Integer)
                                            .FolderPath = strFolderPath
                                            .FileCount = CType(strSplitLine(eFolderIntegrityInfoFileColumns.FileCount), Integer)
                                            .FileCountFailIntegrity = CType(strSplitLine(eFolderIntegrityInfoFileColumns.FileCountFailedIntegrity), Integer)
                                            dtInfoLastModified = CType(strSplitLine(eFolderIntegrityInfoFileColumns.InfoLastModified), DateTime)
                                        End With

                                        PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objNewRow, dtInfoLastModified)
                                        mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows.Add(objNewRow)

                                    Catch ex As System.Exception
                                        ' Do not add this entry
                                    End Try
                                End If
                            End If

                        End If
                    End If
                Loop
                srInFile.Close()

            End If
        End If

        mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified

    End Sub

    Private Sub LoadCachedMSFileInfoResults()

        Dim fsInFile As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim strLineIn As String
        Dim strSplitLine() As String
        Dim strSepChars() As Char

        Dim strDatasetName As String
        Dim udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType
        Dim dtInfoLastModified As DateTime

        Dim objNewRow As System.Data.DataRow

        strSepChars = New Char() {mDataFileSepChar}

        ' Clear the MS Info Table
        ClearCachedMSInfoResults()

        ValidateDataFilePath(mAcquisitionTimeFilePath, eDataFileTypeConstants.MSFileInfo)

        If System.IO.File.Exists(mAcquisitionTimeFilePath) Then
            ' Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

            If USE_XML_OUTPUT_FILE Then
                fsInFile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.OpenOrCreate, IO.FileAccess.ReadWrite, IO.FileShare.Read)
                mMSFileInfoDataset.ReadXml(fsInFile)
                fsInFile.Close()
            Else
                srInFile = New System.IO.StreamReader(mAcquisitionTimeFilePath)
                Do While srInFile.Peek() >= 0
                    strLineIn = srInFile.ReadLine()

                    If Not strLineIn Is Nothing Then
                        strSplitLine = strLineIn.Split(strSepChars)

                        If strSplitLine.Length >= 8 Then
                            strDatasetName = strSplitLine(eMSFileInfoResultsFileColumns.DatasetName)

                            If IsNumber(strSplitLine(eMSFileInfoResultsFileColumns.DatasetID)) Then
                                If Not CachedMSInfoContainsDataset(strDatasetName) Then
                                    Try
                                        objNewRow = mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).NewRow()

                                        With udtFileInfo
                                            .DatasetID = CType(strSplitLine(eMSFileInfoResultsFileColumns.DatasetID), Integer)
                                            .DatasetName = String.Copy(strDatasetName)
                                            .FileExtension = String.Copy(strSplitLine(eMSFileInfoResultsFileColumns.FileExtension))
                                            .AcqTimeStart = CType(strSplitLine(eMSFileInfoResultsFileColumns.AcqTimeStart), DateTime)
                                            .AcqTimeEnd = CType(strSplitLine(eMSFileInfoResultsFileColumns.AcqTimeEnd), DateTime)
                                            .ScanCount = CType(strSplitLine(eMSFileInfoResultsFileColumns.ScanCount), Integer)
                                            .FileSizeBytes = CType(strSplitLine(eMSFileInfoResultsFileColumns.FileSizeBytes), Long)
                                            dtInfoLastModified = CType(strSplitLine(eMSFileInfoResultsFileColumns.InfoLastModified), DateTime)

                                            If strSplitLine.Length >= 9 Then
                                                .FileSystemModificationTime = CType(strSplitLine(eMSFileInfoResultsFileColumns.FileModificationDate), DateTime)
                                            End If
                                        End With

                                        PopulateMSInfoDataRow(udtFileInfo, objNewRow, dtInfoLastModified)
                                        mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows.Add(objNewRow)

                                    Catch ex As System.Exception
                                        ' Do not add this entry
                                    End Try
                                End If
                            End If

                        End If
                    End If
                Loop
                srInFile.Close()

            End If
        End If

        mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified

    End Sub

    Private Sub InitializeDatasets()

        Dim dtDefaultDate As DateTime = System.DateTime.Now()

        ' Make the MSFileInfo datatable
        Dim dtMSFileInfo As System.Data.DataTable = New System.Data.DataTable(MS_FILEINFO_DATATABLE)

        ' Add the columns to the datatable
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtMSFileInfo, COL_NAME_DATASET_ID)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(dtMSFileInfo, COL_NAME_DATASET_NAME)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(dtMSFileInfo, COL_NAME_FILE_EXTENSION)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_ACQ_TIME_START, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_ACQ_TIME_END, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtMSFileInfo, COL_NAME_SCAN_COUNT)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnLongToTable(dtMSFileInfo, COL_NAME_FILE_SIZE_BYTES)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_INFO_LAST_MODIFIED, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_FILE_MODIFICATION_DATE, dtDefaultDate)

        ' Use the dataset name as the primary key since we won't always know Dataset_ID
        With dtMSFileInfo
            Dim MSInfoPrimaryKeyColumn As System.Data.DataColumn() = New System.Data.DataColumn() {.Columns(COL_NAME_DATASET_NAME)}
            .PrimaryKey = MSInfoPrimaryKeyColumn
        End With


        ' Make the Folder Integrity Info datatable
        Dim dtFolderIntegrityInfo As System.Data.DataTable = New System.Data.DataTable(FOLDER_INTEGRITY_INFO_DATATABLE)

        ' Add the columns to the datatable
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtFolderIntegrityInfo, COL_NAME_FOLDER_ID)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(dtFolderIntegrityInfo, COL_NAME_FOLDER_PATH)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtFolderIntegrityInfo, COL_NAME_FILE_COUNT)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtFolderIntegrityInfo, COL_NAME_COUNT_FAIL_INTEGRITY)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtFolderIntegrityInfo, COL_NAME_INFO_LAST_MODIFIED, dtDefaultDate)

        ' Use the folder path as the primary key
        With dtFolderIntegrityInfo
            Dim FolderInfoPrimaryKeyColumn As System.Data.DataColumn() = New System.Data.DataColumn() {.Columns(COL_NAME_FOLDER_PATH)}
            .PrimaryKey = FolderInfoPrimaryKeyColumn
        End With

        ' Instantiate the datasets
        mMSFileInfoDataset = New System.Data.DataSet("MSFileInfoDataset")
        mFolderIntegrityInfoDataset = New System.Data.DataSet("FolderIntegrityInfoDataset")

        ' Add the new DataTable to each DataSet
        mMSFileInfoDataset.Tables.Add(dtMSFileInfo)
        mFolderIntegrityInfoDataset.Tables.Add(dtFolderIntegrityInfo)

        mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized
        mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized
    End Sub

    Private Sub InitializeLocalVariables()
        mErrorCode = eMSFileScannerErrorCodes.NoError

        mStatusMessage = String.Empty
        mDataFileSepChar = ControlChars.Tab
        mIgnoreErrorsWhenRecursing = False

        mCachedResultsAutoSaveIntervalMinutes = 5
        mCachedMSInfoResultsLastSaveTime = System.DateTime.Now()
        mCachedFolderIntegrityInfoLastSaveTime = System.DateTime.Now()

        mReprocessExistingFiles = False
        mReprocessIfCachedSizeIsZero = False
        mRecheckFileIntegrityForExistingFolders = False

        mSaveTICAndBPIPlots = False
        mComputeOverallQualityScores = False

        mCheckFileIntegrity = False

        mAcquisitionTimeFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileScanner.DefaultDataFileName(eDataFileTypeConstants.MSFileInfo))
        mFolderIntegrityInfoFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileScanner.DefaultDataFileName(eDataFileTypeConstants.FolderIntegrityInfo))
        mFileIntegrityDetailsFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileScanner.DefaultDataFileName(eDataFileTypeConstants.FileIntegrityDetails))
        mFileIntegrityErrorsFilePath = System.IO.Path.Combine(GetAppFolderPath(), clsMSFileScanner.DefaultDataFileName(eDataFileTypeConstants.FileIntegrityErrors))

        ValidateDataFilePath(mAcquisitionTimeFilePath, eDataFileTypeConstants.MSFileInfo)

        InitializeDatasets()

    End Sub

    Public Shared Function IsNumber(ByVal strValue As String) As Boolean
        Dim objFormatProvider As New System.Globalization.NumberFormatInfo
        Try
            Return Double.TryParse(strValue, Globalization.NumberStyles.Any, objFormatProvider, 0)
        Catch ex As System.Exception
            Return False
        End Try
    End Function

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
                    LogErrors("LoadParameterFileSettings", "Parameter file not found: " & strParameterFilePath, Nothing, True, False, False)
                    SetErrorCode(eMSFileScannerErrorCodes.ParameterFileNotFound)
                    Return False
                End If
            End If

            ' Pass False to .LoadSettings() here to turn off case sensitive matching
            If objSettingsFile.LoadSettings(strParameterFilePath, False) Then
                With objSettingsFile

                    If Not .SectionPresent(XML_SECTION_MSFILESCANNER_SETTINGS) Then
                        ' MS File Scanner section not found; that's ok
                    Else
                        'Me.DatabaseConnectionString = .GetParam(XML_SECTION_DATABASE_SETTINGS, "ConnectionString", Me.DatabaseConnectionString)
                    End If

                End With
            Else
                LogErrors("LoadParameterFileSettings", "Error calling objSettingsFile.LoadSettings for " & strParameterFilePath, Nothing, True, False, True, eMSFileScannerErrorCodes.ParameterFileReadError)
                Return False
            End If

        Catch ex As System.Exception
            LogErrors("LoadParameterFileSettings", "Error in LoadParameterFileSettings", ex, True, False, True, eMSFileScannerErrorCodes.ParameterFileReadError)
            Return False
        End Try

        Return True

    End Function

    Private Sub LogErrors(ByVal strSource As String, ByVal strMessage As String, ByVal ex As System.Exception, Optional ByVal blnAllowInformUser As Boolean = True, Optional ByVal blnAllowThrowingException As Boolean = True, Optional ByVal blnLogLocalOnly As Boolean = True, Optional ByVal eNewErrorCode As eMSFileScannerErrorCodes = eMSFileScannerErrorCodes.NoError)
        Dim strMessageWithoutCRLF As String
        Dim fsErrorLogFile As System.IO.StreamWriter

        mStatusMessage = String.Copy(strMessage)

        strMessageWithoutCRLF = mStatusMessage.Replace(ControlChars.NewLine, "; ")

        If ex Is Nothing Then
            ex = New System.Exception("Error")
        Else
            If Not ex.Message Is Nothing AndAlso ex.Message.Length > 0 Then
                strMessageWithoutCRLF &= "; " & ex.Message
            End If
        End If

        Console.WriteLine(Now.ToLongTimeString & "; " & strSource & ": " & strMessageWithoutCRLF)

        Try
            fsErrorLogFile = New System.IO.StreamWriter("MSFileInfoScanner_Errors.txt", True)
            fsErrorLogFile.WriteLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt") & ControlChars.Tab & strSource & ControlChars.Tab & strMessageWithoutCRLF)
        Catch ex2 As System.Exception
            ' Ignore errors here
        Finally
            If Not fsErrorLogFile Is Nothing Then
                fsErrorLogFile.Close()
            End If
        End Try

        If Not eNewErrorCode = eMSFileScannerErrorCodes.NoError Then
            SetErrorCode(eNewErrorCode, True)
        End If

        If Me.ShowMessages AndAlso blnAllowInformUser Then
            System.Windows.Forms.MessageBox.Show(mStatusMessage & ControlChars.NewLine & ex.Message, "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
        ElseIf blnAllowThrowingException Then
            Throw New System.Exception(mStatusMessage, ex)
        End If
    End Sub

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
            LogErrors("OpenFileIntegrityFile", "Error opening/creating " & strFilePath & "; will try " & strDefaultFileName, ex, True, True, True, eMSFileScannerErrorCodes.FileIntegrityCheckError)

            Try
                If System.IO.File.Exists(strDefaultFileName) Then
                    blnOpenedExistingFile = True
                End If

                fsFileStream = New System.IO.FileStream(strDefaultFileName, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.Read)
            Catch ex2 As System.Exception
                LogErrors("OpenFileIntegrityFile", "Error opening/creating " & strDefaultFileName, ex, True, True, True, eMSFileScannerErrorCodes.FileIntegrityCheckError)
            End Try
        End Try

        Try
            If Not fsFileStream Is Nothing Then
                objStreamWriter = New System.IO.StreamWriter(fsFileStream)

                If Not blnOpenedExistingFile Then
                    objStreamWriter.WriteLine(ConstructHeaderLine(eDataFileType))
                End If
            End If
        Catch ex As System.Exception
            LogErrors("OpenFileIntegrityFile", "Error opening/creating the StreamWriter for " & fsFileStream.Name, ex, True, True, True, eMSFileScannerErrorCodes.FileIntegrityCheckError)
        End Try

    End Sub

    Private Sub PopulateMSInfoDataRow(ByRef udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow)
        PopulateMSInfoDataRow(udtFileInfo, objRow, System.DateTime.Now())
    End Sub

    Private Sub PopulateMSInfoDataRow(ByRef udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow, ByVal dtInfoLastModified As DateTime)

        ' ToDo: Update udtFileInfo to include some overall quality scores

        With objRow
            .Item(COL_NAME_DATASET_ID) = udtFileInfo.DatasetID
            .Item(COL_NAME_DATASET_NAME) = udtFileInfo.DatasetName
            .Item(COL_NAME_FILE_EXTENSION) = udtFileInfo.FileExtension
            .Item(COL_NAME_ACQ_TIME_START) = AssureMinimumDate(udtFileInfo.AcqTimeStart, MINIMUM_DATETIME)
            .Item(COL_NAME_ACQ_TIME_END) = AssureMinimumDate(udtFileInfo.AcqTimeEnd, MINIMUM_DATETIME)
            .Item(COL_NAME_SCAN_COUNT) = udtFileInfo.ScanCount
            .Item(COL_NAME_FILE_SIZE_BYTES) = udtFileInfo.FileSizeBytes
            .Item(COL_NAME_INFO_LAST_MODIFIED) = AssureMinimumDate(dtInfoLastModified, MINIMUM_DATETIME)
            .Item(COL_NAME_FILE_MODIFICATION_DATE) = AssureMinimumDate(udtFileInfo.FileSystemModificationTime, MINIMUM_DATETIME)
            '.Item(COL_NAME_QUALITY_SCORE) = udtFileInfo.OverallQualityScore
        End With
    End Sub

    Private Sub PopulateFolderIntegrityInfoDataRow(ByVal intFolderID As Integer, ByRef udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType, ByRef objRow As System.Data.DataRow)
        PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow, System.DateTime.Now())
    End Sub

    Private Sub PopulateFolderIntegrityInfoDataRow(ByVal intFolderID As Integer, ByRef udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType, ByRef objRow As System.Data.DataRow, ByVal dtInfoLastModified As DateTime)

        With objRow
            .Item(COL_NAME_FOLDER_ID) = intFolderID
            .Item(COL_NAME_FOLDER_PATH) = udtFolderStats.FolderPath
            .Item(COL_NAME_FILE_COUNT) = udtFolderStats.FileCount
            .Item(COL_NAME_COUNT_FAIL_INTEGRITY) = udtFolderStats.FileCountFailIntegrity
            .Item(COL_NAME_INFO_LAST_MODIFIED) = AssureMinimumDate(dtInfoLastModified, MINIMUM_DATETIME)
        End With

        If intFolderID > mMaximumFolderIntegrityInfoFolderID Then
            mMaximumFolderIntegrityInfoFolderID = intFolderID
        End If
    End Sub

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
            objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, mComputeOverallQualityScores)

            ' Process the data file
            blnSuccess = objMSInfoScanner.ProcessDatafile(strInputFileOrFolderPath, udtFileInfo)

            If Not blnSuccess Then
                intRetryCount += 1

                If intRetryCount < MAX_FILE_READ_MAX_ACCESS_ATTEMPTS Then
                    ' Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time
                    If System.DateTime.Now.Subtract(udtFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES OrElse _
                       System.DateTime.Now.Subtract(udtFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES Then

                        ' Sleep for 10 seconds then try again
                        System.Threading.Thread.Sleep(10000)
                    Else
                        intRetryCount = MAX_FILE_READ_MAX_ACCESS_ATTEMPTS
                    End If
                End If
            End If
        Loop While Not blnSuccess And intRetryCount < MAX_FILE_READ_MAX_ACCESS_ATTEMPTS

        If Not blnSuccess And intRetryCount >= MAX_FILE_READ_MAX_ACCESS_ATTEMPTS Then
            If udtFileInfo.DatasetName.Length > 0 Then
                ' Make an entry anyway; probably a corrupted file
                blnSuccess = True
            End If
        End If

        If blnSuccess Then
            If mSaveTICAndBPIPlots Then
                ' Write out the TIC and BPI plots
                SaveTICAndBPIPlotFiles(objMSInfoScanner, strDatasetName, strOutputFolderPath)
            End If

            ' Update the results database
            blnSuccess = UpdateCachedMSFileInfo(udtFileInfo)

            ' Possibly auto-save the cached results
            AutosaveCachedResults()
        Else
            If SKIP_FILES_IN_ERROR Then
                blnSuccess = True
            End If
        End If

        Return blnSuccess

    End Function

    ' Main processing function
    Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean, ByRef eMSFileProcessingState As eMSFileProcessingStateConstants) As Boolean
        ' Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
        ' See function ProcessMSFilesAndRecurseFolders for more details
        ' This function returns True if it processed a file (or the dataset was processed previously)
        ' When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
        ' If the file type is unknown, or if an error occurs, then it returns false
        ' eMSFileProcessingState will be updated based on whether the file is processed, skipped, etc.

        Dim blnSuccess As Boolean
        Dim objMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor

        Dim blnIsFolder As Boolean
        Dim objFileSystemInfo As System.IO.FileSystemInfo

        Dim objRow As System.Data.DataRow
        Dim lngCachedSizeBytes As Long

        If blnResetErrorCode Then
            SetErrorCode(eMSFileScannerErrorCodes.NoError)
        End If

        mStatusMessage = String.Empty
        eMSFileProcessingState = eMSFileProcessingStateConstants.NotProcessed

        If strOutputFolderPath Is Nothing OrElse strOutputFolderPath.Length = 0 Then
            ' Define strOutputFolderPath based on the program file path
            strOutputFolderPath = GetAppFolderPath()
        End If

        If mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized Then
            LoadCachedResults()
        End If

        Try
            If strInputFileOrFolderPath Is Nothing OrElse strInputFileOrFolderPath.Length = 0 Then
                LogErrors("ProcessMSFileOrFolder", "Input file name is empty", Nothing, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
            Else
                mStatusMessage = " Parsing " & System.IO.Path.GetFileName(strInputFileOrFolderPath)
                Console.WriteLine(mStatusMessage)

                ' Determine whether strInputFileOrFolderPath points to a file or a folder

                If Not GetFileOrFolderInfo(strInputFileOrFolderPath, blnIsFolder, objFileSystemInfo) Then
                    LogErrors("ProcessMSFileOrFolder", "File or folder not found: " & strInputFileOrFolderPath, Nothing, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
                    If SKIP_FILES_IN_ERROR Then
                        Return True
                    Else
                        Return False
                    End If
                End If

                ' Only continue if it's a known type
                If blnIsFolder Then
                    If objFileSystemInfo.Name = clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME Then
                        ' Bruker 1 folder
                        objMSInfoScanner = New clsBrukerOneFolderInfoScanner
                    Else
                        Select Case System.IO.Path.GetExtension(strInputFileOrFolderPath).ToUpper
                            Case clsAgilentIonTrapDFolderInfoScanner.AGILENT_ION_TRAP_D_EXTENSION
                                ' Agilent .D folder
                                objMSInfoScanner = New clsAgilentIonTrapDFolderInfoScanner
                            Case clsMicromassRawFolderInfoScanner.MICROMASS_RAW_FOLDER_EXTENSION
                                ' Micromass .Raw folder
                                objMSInfoScanner = New clsMicromassRawFolderInfoScanner
                            Case Else
                                ' Unknown folder extension
                        End Select
                    End If
                Else
                    ' Examine the extension on strInputFileOrFolderPath
                    Select Case objFileSystemInfo.Extension.ToUpper
                        Case clsFinniganRawFileInfoScanner.FINNIGAN_RAW_FILE_EXTENSION
                            objMSInfoScanner = New clsFinniganRawFileInfoScanner
                        Case clsAgilentTOFOrQStarWiffFileInfoScanner.AGILENT_TOF_OR_QSTAR_FILE_EXTENSION
                            objMSInfoScanner = New clsAgilentTOFOrQStarWiffFileInfoScanner
                        Case Else
                            ' Unknown file extension; check for a zipped folder 
                            If clsBrukerOneFolderInfoScanner.IsZippedSFolder(objFileSystemInfo.Name) Then
                                ' Bruker s001.zip file
                                objMSInfoScanner = New clsBrukerOneFolderInfoScanner
                            End If
                    End Select
                End If

                If objMSInfoScanner Is Nothing Then
                    LogErrors("ProcessMSFileOrFolder", "Unknown file type: " & System.IO.Path.GetFileName(strInputFileOrFolderPath), Nothing, False, False, True, eMSFileScannerErrorCodes.UnknownFileExtension)
                    Return False
                End If

                Dim strDatasetName As String
                strDatasetName = objMSInfoScanner.GetDatasetNameViaPath(objFileSystemInfo.FullName)

                If Not mReprocessExistingFiles Then
                    ' See if the strDatasetName in strInputFileOrFolderPath is already present in mCachedResults
                    ' If it is present, then don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)

                    If strDatasetName.Length > 0 AndAlso CachedMSInfoContainsDataset(strDatasetName, objRow) Then
                        If mReprocessIfCachedSizeIsZero Then
                            Try
                                lngCachedSizeBytes = CLng(objRow.Item(COL_NAME_FILE_SIZE_BYTES))
                            Catch ex2 As System.Exception
                                lngCachedSizeBytes = 1
                            End Try

                            If lngCachedSizeBytes > 0 Then
                                ' File is present in mCachedResults, and its size is > 0, so we won't re-process it
                                eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache
                                Return True
                            End If
                        Else
                            ' File is present in mCachedResults, and mReprocessIfCachedSizeIsZero=False, so we won't re-process it
                            eMSFileProcessingState = eMSFileProcessingStateConstants.SkippedSinceFoundInCache
                            Return True
                        End If
                    End If
                End If

                ' Process the data file or folder
                blnSuccess = ProcessMSDataset(strInputFileOrFolderPath, objMSInfoScanner, strDatasetName, strOutputFolderPath)
                If blnSuccess Then
                    eMSFileProcessingState = eMSFileProcessingStateConstants.ProcessedSuccessfully
                Else
                    eMSFileProcessingState = eMSFileProcessingStateConstants.FailedProcessing
                End If

            End If

        Catch ex As System.Exception
            blnSuccess = False
            LogErrors("ProcessMSFileOrFolder", "Error in ProcessMSFileOrFolder", ex, True, False, False, eMSFileScannerErrorCodes.UnspecifiedError)
        Finally
            ' Could place Finally code here
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

        Dim strMessage As String

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

                If intMatchCount = 0 And Me.ShowMessages Then
                    If mErrorCode = eMSFileScannerErrorCodes.NoError Then
                        strMessage = "No match was found for the input file path:" & ControlChars.NewLine & strInputFileOrFolderPath
                        If Me.ShowMessages Then
                            System.Windows.Forms.MessageBox.Show(strMessage, "File not found", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
                        Else
                            Console.WriteLine(strMessage)
                        End If
                    End If
                Else
                    Console.WriteLine()
                End If
            Else
                blnSuccess = ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, blnResetErrorCode, eMSFileProcessingState)
            End If

        Catch ex As System.Exception
            If Me.ShowMessages Then
                strMessage = "Error in ProcessMSFileOrFolderWildcard: " & ControlChars.NewLine & ex.Message
                System.Windows.Forms.MessageBox.Show(strMessage, "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
            Else
                Throw New System.Exception("Error in ProcessMSFileOrFolderWildcard", ex)
            End If
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

    Public Function ProcessMSFilesAndRecurseFolders(ByVal strInputFilePathOrFolder As String, ByVal strOutputFolderPath As String, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
        ' Calls ProcessFile for all files in strInputFilePathOrFolder and below having a known extension
        ' Known extensions are:
        '  .Raw for Finnigan files
        '  .Wiff for Agilent TOF files and for Q-Star files
        ' 
        ' Furthermore, for each folder that does not have a file matching a known extension,
        '  it then looks for special folder names:
        '  Folders matching *.Raw for Micromass data
        '  Folders matching *.D for Agilent Ion Trap data
        '  A folder named 1 for Bruker FTICR-MS data

        ' If strInputFilePathOrFolder contains a filename with a wildcard (* or ?), then that information will be 
        '  used to filter the files that are processed
        ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

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
                If ioFileInfo.Directory.Exists Then
                    strInputFolderPath = ioFileInfo.DirectoryName
                Else
                    ' Use the current working directory
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

                If mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized Then
                    LoadCachedResults()
                End If

                ' Call RecurseFoldersWork
                blnSuccess = RecurseFoldersWork(strInputFolderPath, strInputFilePathOrFolder, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, 1, intRecurseFoldersMaxLevels)

            Else
                SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath)
                Return False
            End If

        Catch ex As System.Exception
            LogErrors("ProcessMSFilesAndRecurseFolders", ex.Message, ex, False, False, True, eMSFileScannerErrorCodes.InputFileReadError)
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

    Private Function RecurseFoldersWork(ByVal strInputFolderPath As String, ByVal strFileNameMatch As String, ByVal strOutputFolderPath As String, ByRef intFileProcessCount As Integer, ByRef intFileProcessFailCount As Integer, ByVal intRecursionLevel As Integer, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
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
                LogErrors("RecurseFoldersWork", "Populate ioInputFolderInfo for" & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
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
            LogErrors("RecurseFoldersWork", ex.Message, ex, False, False, True, eMSFileScannerErrorCodes.UnspecifiedError)
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
                        LogErrors("RecurseFoldersWork", "For Each ioFileMatch in " & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
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
            LogErrors("RecurseFoldersWork", "Examining files in " & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
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
                            LogErrors("RecurseFoldersWork", "For Each ioSubFolderInfo(A) in " & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
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
                                LogErrors("RecurseFoldersWork", "For Each ioSubFolderInfo(B) in " & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
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
                LogErrors("RecurseFoldersWork", "Examining subfolders in " & strInputFolderPath, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
                Return False
            End Try

        End If

        Return blnSuccess

    End Function

    Public Function SaveCachedResults() As Boolean
        Return SaveCachedResults(True)
    End Function

    Private Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean
        Dim blnSuccess1 As Boolean
        Dim blnSuccess2 As Boolean

        blnSuccess1 = SaveCachedMSInfoResults(blnClearCachedData)
        blnSuccess2 = SaveCachedFolderIntegrityInfoResults(blnClearCachedData)

        Return blnSuccess1 And blnSuccess2
    End Function

    Private Function SaveCachedFolderIntegrityInfoResults(ByVal blnClearCachedData As Boolean) As Boolean

        Dim fsOutfile As System.IO.FileStream
        Dim srOutFile As System.IO.StreamWriter

        Dim objRow As System.Data.DataRow
        Dim blnSuccess As Boolean

        If Not mFolderIntegrityInfoDataset Is Nothing AndAlso _
           mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows.Count > 0 AndAlso _
           mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified Then

            Try
                ' Write all of mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE) to the results file
                If USE_XML_OUTPUT_FILE Then
                    fsOutfile = New System.IO.FileStream(mFolderIntegrityInfoFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    mFolderIntegrityInfoDataset.WriteXml(fsOutfile)
                    fsOutfile.Close()
                Else
                    fsOutfile = New System.IO.FileStream(mFolderIntegrityInfoFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    srOutFile = New System.IO.StreamWriter(fsOutfile)

                    srOutFile.WriteLine(ConstructHeaderLine(eDataFileTypeConstants.FolderIntegrityInfo))

                    For Each objRow In mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows
                        WriteFolderIntegrityInfoDataLine(srOutFile, objRow)
                    Next objRow

                    srOutFile.Close()
                End If

                mCachedFolderIntegrityInfoLastSaveTime = System.DateTime.Now()

                If blnClearCachedData Then
                    ' Clear the data table
                    ClearCachedFolderIntegrityInfoResults()
                Else
                    mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified
                End If

                blnSuccess = True

            Catch ex As System.Exception
                blnSuccess = False
                LogErrors("SaveCachedFolderIntegrityInfoResults", "Error in SaveCachedFolderIntegrityInfoResults", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
            Finally
                If USE_XML_OUTPUT_FILE Then
                    fsOutfile = Nothing
                Else
                    fsOutfile = Nothing
                    srOutFile = Nothing
                End If
            End Try
        End If

        Return blnSuccess

    End Function


    Private Function SaveCachedMSInfoResults(ByVal blnClearCachedData As Boolean) As Boolean

        Dim fsOutfile As System.IO.FileStream
        Dim srOutFile As System.IO.StreamWriter

        Dim objRow As System.Data.DataRow
        Dim blnSuccess As Boolean

        If Not mMSFileInfoDataset Is Nothing AndAlso _
           mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows.Count > 0 AndAlso _
           mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified Then

            Try
                ' Write all of mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE) to the results file
                If USE_XML_OUTPUT_FILE Then
                    fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    mMSFileInfoDataset.WriteXml(fsOutfile)
                    fsOutfile.Close()
                Else
                    fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    srOutFile = New System.IO.StreamWriter(fsOutfile)

                    srOutFile.WriteLine(ConstructHeaderLine(eDataFileTypeConstants.MSFileInfo))

                    For Each objRow In mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows
                        WriteMSInfoDataLine(srOutFile, objRow)
                    Next objRow

                    srOutFile.Close()
                End If

                mCachedMSInfoResultsLastSaveTime = System.DateTime.Now()

                If blnClearCachedData Then
                    ' Clear the data table
                    ClearCachedMSInfoResults()
                Else
                    mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified
                End If

                blnSuccess = True

            Catch ex As System.Exception
                blnSuccess = False
                LogErrors("SaveCachedMSInfoResults", "Error in SaveCachedMSInfoResults", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
            Finally
                If USE_XML_OUTPUT_FILE Then
                    fsOutfile = Nothing
                Else
                    fsOutfile = Nothing
                    srOutFile = Nothing
                End If
            End Try
        End If

        Return blnSuccess

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
            LogErrors("SaveParameterFileSettings", "Error in SaveParameterFileSettings", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
            Return False
        Finally
            objSettingsFile = Nothing
        End Try

        Return True

    End Function

    Private Function InitializeGraphPane(ByRef objData As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType, ByVal strTitle As String, ByVal intMSLevelFilter As Integer) As ZedGraph.GraphPane
        Dim myPane As New ZedGraph.GraphPane

        Dim intDataCount As Integer
        Dim dblXVals() As Double
        Dim dblYVals() As Double

        Dim intIndex As Integer

        With objData
            intDataCount = 0
            ReDim dblXVals(.ScanCount - 1)
            ReDim dblYVals(.ScanCount - 1)

            For intIndex = 0 To .ScanCount - 1
                If intMSLevelFilter = 0 OrElse _
                   .ScanMSLevel(intIndex) = intMSLevelFilter OrElse _
                   intMSLevelFilter = 2 And .ScanMSLevel(intIndex) >= 2 Then
                    dblXVals(intDataCount) = .ScanNum(intIndex)
                    dblYVals(intDataCount) = .ScanIntensity(intIndex)
                    intDataCount += 1
                End If
            Next intIndex

            If intDataCount <> dblXVals.Length Then
                ReDim Preserve dblXVals(intDataCount - 1)
                ReDim Preserve dblYVals(intDataCount - 1)
            End If
        End With

        ' Set the titles and axis labels
        myPane.Title.Text = String.Copy(strTitle)
        myPane.XAxis.Title.Text = "Scan Number"
        myPane.YAxis.Title.Text = "Intensity"

        ' Generate a black curve with no symbols
        Dim myCurve As ZedGraph.LineItem
        myPane.CurveList.Clear()

        If intDataCount > 0 Then
            myCurve = myPane.AddCurve(strTitle, dblXVals, dblYVals, System.Drawing.Color.Black, ZedGraph.SymbolType.None)
        End If

        ' Show the x axis grid
        myPane.XAxis.MajorGrid.IsVisible = True

        '' Make the Y axis scale black
        'myPane.YAxis.Scale.FontSpec.FontColor = Color.Red
        'myPane.YAxis.Title.FontSpec.FontColor = Color.Red

        '' Align the Y axis labels so they are flush to the axis
        'myPane.YAxis.Scale.Align = AlignP.Inside

        ' Fill the axis background with a gradient
        myPane.Chart.Fill = New ZedGraph.Fill(System.Drawing.Color.White, System.Drawing.Color.LightGray, 45.0F)

        ' Hide the legend
        myPane.Legend.IsVisible = False

        ' Force a plot update
        myPane.AxisChange()

        Return myPane

    End Function

    Private Function SaveTICAndBPIPlotFiles(ByRef objMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor, ByVal strDatasetName As String, ByVal strOutputFolderPath As String) As Boolean
        Dim myPane As ZedGraph.GraphPane
        Dim strPNGFilePath As String
        Dim blnSuccess As Boolean

        Try
            myPane = InitializeGraphPane(objMSInfoScanner.BPI, "BPI - MS Spectra", 1)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_BPI_MS.png")
                myPane.GetImage(800, 400, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
            End If

            myPane = InitializeGraphPane(objMSInfoScanner.BPI, "BPI - MS2 Spectra", 2)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_BPI_MSn.png")
                myPane.GetImage(800, 400, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
            End If

            myPane = InitializeGraphPane(objMSInfoScanner.TIC, "TIC - All Spectra", 0)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_TIC.png")
                myPane.GetImage(800, 400, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
            End If

            blnSuccess = True
        Catch ex As System.Exception
            LogErrors("SaveTICAndBPIPlotFiles", ex.Message, Nothing, False, False, True, eMSFileScannerErrorCodes.UnknownFileExtension)
            blnSuccess = False
        End Try

        Return blnSuccess

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

    Private Function UpdateCachedMSFileInfo(ByVal udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Update the entry for this dataset in mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

        Dim objRow As System.Data.DataRow

        Dim blnSuccess As Boolean

        Try
            ' Examine the data in memory and add or update the data for strDataset
            If CachedMSInfoContainsDataset(udtFileInfo.DatasetName, objRow) Then
                ' Item already present; update it
                Try
                    PopulateMSInfoDataRow(udtFileInfo, objRow)
                Catch ex As System.Exception
                    ' Ignore errors updating the entry
                End Try
            Else
                ' Item not present; add it
                objRow = mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).NewRow
                PopulateMSInfoDataRow(udtFileInfo, objRow)
                mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows.Add(objRow)
            End If

            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified

            blnSuccess = True
        Catch ex As System.Exception
            blnSuccess = False
            LogErrors("ProcessFile", "Error in UpdateCachedMSFileInfo", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
        End Try

        Return blnSuccess

    End Function


    Private Function UpdateCachedFolderIntegrityInfo(ByVal udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType, ByRef intFolderID As Integer) As Boolean
        ' Update the entry for this dataset in mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE)

        Dim objRow As System.Data.DataRow

        Dim blnSuccess As Boolean

        Try
            If mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized Then
                ' Coding error; this shouldn't be the case
                Console.WriteLine("mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized in UpdateCachedFolderIntegrityInfo; unable to continue")
                End
            End If

            intFolderID = -1

            ' Examine the data in memory and add or update the data for strDataset
            If CachedFolderIntegrityInfoContainsFolder(udtFolderStats.FolderPath, intFolderID, objRow) Then
                ' Item already present; update it
                Try
                    PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow)
                Catch ex As System.Exception
                    ' Ignore errors updating the entry
                End Try
            Else
                ' Item not present; add it

                ' Auto-assign the next available FolderID value
                intFolderID = mMaximumFolderIntegrityInfoFolderID + 1

                objRow = mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).NewRow
                PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow)
                mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows.Add(objRow)
            End If

            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified

            blnSuccess = True
        Catch ex As System.Exception
            blnSuccess = False
            LogErrors("ProcessFile", "Error in UpdateCachedFolderIntegrityInfo", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
        End Try

        Return blnSuccess

    End Function

    Private Function ValidateDataFilePath(ByRef strFilePath As String, ByVal eDataFileType As eDataFileTypeConstants) As Boolean
        If strFilePath Is Nothing OrElse strFilePath.Length = 0 Then
            strFilePath = System.IO.Path.Combine(GetAppFolderPath(), DefaultDataFileName(eDataFileType))
        End If

        ValidateDataTableFilePath(strFilePath)
    End Function

    Private Function ValidateDataTableFilePath(ByVal strFilePath As String) As Boolean

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

    Private Sub WriteMSInfoDataLine(ByRef srOutFile As System.IO.StreamWriter, ByRef objRow As System.Data.DataRow)
        With objRow
            ' Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(.Item(COL_NAME_DATASET_ID).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_DATASET_NAME).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_FILE_EXTENSION).ToString & mDataFileSepChar & _
                                CType(.Item(COL_NAME_ACQ_TIME_START), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & mDataFileSepChar & _
                                CType(.Item(COL_NAME_ACQ_TIME_END), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & mDataFileSepChar & _
                                .Item(COL_NAME_SCAN_COUNT).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_FILE_SIZE_BYTES).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_INFO_LAST_MODIFIED).ToString & mDataFileSepChar & _
                                CType(.Item(COL_NAME_FILE_MODIFICATION_DATE), DateTime).ToString("yyyy-MM-dd HH:mm:ss"))

        End With
    End Sub

    Private Sub WriteFolderIntegrityInfoDataLine(ByRef srOutFile As System.IO.StreamWriter, ByRef objRow As System.Data.DataRow)

        With objRow
            srOutFile.WriteLine(.Item(COL_NAME_FOLDER_ID).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_FOLDER_PATH).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_FILE_COUNT).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_COUNT_FAIL_INTEGRITY).ToString & mDataFileSepChar & _
                                .Item(COL_NAME_INFO_LAST_MODIFIED).ToString)
        End With
    End Sub

    Private Sub WriteFileIntegrityDetails(ByRef srOutFile As System.IO.StreamWriter, ByVal intFolderID As Integer, ByVal udtFileStats() As clsFileIntegrityChecker.udtFileStatsType)
        Static dtLastWriteTime As DateTime

        Dim intIndex As Integer
        Dim dtTimeStamp As DateTime = System.DateTime.Now

        If srOutFile Is Nothing Then Exit Sub

        For intIndex = 0 To udtFileStats.Length - 1
            With udtFileStats(intIndex)
                ' Note: HH:mm:ss corresponds to time in 24 hour format
                srOutFile.WriteLine(intFolderID.ToString & mDataFileSepChar & _
                                    .FileName & mDataFileSepChar & _
                                    .SizeBytes.ToString & mDataFileSepChar & _
                                    .ModificationDate.ToString("yyyy-MM-dd HH:mm:ss") & mDataFileSepChar & _
                                    .FailIntegrity & mDataFileSepChar & _
                                    .FileHash & mDataFileSepChar & _
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
        srOutFile.WriteLine(strFilePath & mDataFileSepChar & _
                            strMessage & mDataFileSepChar & _
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

    Protected Overrides Sub Finalize()
        Me.SaveCachedResults()
        MyBase.Finalize()
    End Sub

    Private Sub mFileIntegrityChecker_ErrorCaught(ByVal strMessage As String) Handles mFileIntegrityChecker.ErrorCaught
        Me.LogErrors("FileIntegrityChecker", strMessage, Nothing, True, False, True, eMSFileScannerErrorCodes.FileIntegrityCheckError)
    End Sub

    Private Sub mFileIntegrityChecker_FileIntegrityFailure(ByVal strFilePath As String, ByVal strMessage As String) Handles mFileIntegrityChecker.FileIntegrityFailure
        If mFileIntegrityErrorsWriter Is Nothing Then
            OpenFileIntegrityErrorsFile()
        End If

        WriteFileIntegrityFailure(mFileIntegrityErrorsWriter, strFilePath, strMessage)
    End Sub
End Class
