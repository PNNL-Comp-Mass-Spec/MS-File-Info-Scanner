Option Strict On

' This class will read an MS/MS data file from either a Finnigan LCQ (.Raw file)
'   or Agilent Ion Trap (.MGF and .CDF files) and create selected ion chromatograms
'   for each of the parent ion masses chosen for fragmentation
' It will create several output files, including a BPI for the survey scan,
'   a BPI for the fragmentation scans, an XML file containing the SIC data
'   for each parent ion, and a "flat file" ready for import into the database
'   containing summaries of the SIC data statistics
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
' Started October 11, 2003

Public Class clsMSFileScanner

    Public Sub New()
        mFileDate = modMain.PROGRAM_DATE
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    Public Const DEFAULT_ACQUISITION_TIME_FILENAME_TXT As String = "DatasetTimeFile.txt"
    Public Const DEFAULT_ACQUISITION_TIME_FILENAME_XML As String = "DatasetTimeFile.xml"

    Public Const XML_SECTION_MSFILESCANNER_SETTINGS As String = "MSFileScanner"

    Private Const FILE_MODIFICATION_WINDOW_MINUTES As Integer = 60
    Private Const MAX_FILE_READ_RETRY_COUNT As Integer = 2
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
    Private Const COL_NAME_LAST_MODIFIED As String = "InfoLastModified"
    Private Const COL_NAME_FILE_MODIFICATION_DATE As String = "FileModificationDate"

    Private Const MINIMUM_DATETIME As DateTime = #1/1/1900#

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
        UnspecifiedError = -1
    End Enum

    Public Enum eResultsFileColumns
        DatasetID = 0
        DatasetName = 1
        FileExtension = 2
        AcqTimeStart = 3
        AcqTimeEnd = 4
        ScanCount = 5
        FileSizeBytes = 6
        LastModified = 7
        FileModificationDate = 8
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
    Private mAcquisitionTimeFileSepChar As Char

    Private mReprocessExistingFiles As Boolean
    Private mReprocessIfCachedSizeIsZero As Boolean

    Private mCachedResultsAutoSaveIntervalMinutes As Integer
    Private mCachedResultsLastSaveTime As DateTime

    Private mCachedResultsState As eCachedResultsStateConstants
    Private mMSFileInfoDataset As System.Data.DataSet

#End Region

#Region "Processing Options and Interface Functions"

    Public Property AbortProcessing() As Boolean
        Get
            Return mAbortProcessing
        End Get
        Set(ByVal Value As Boolean)
            mAbortProcessing = Value
        End Set
    End Property

    Public Property AcquisitionTimeFilename() As String
        Get
            Return mAcquisitionTimeFilePath
        End Get
        Set(ByVal Value As String)
            mAcquisitionTimeFilePath = Value
        End Set
    End Property

    Public Property AcquisitionTimeFileSepChar() As Char
        Get
            Return mAcquisitionTimeFileSepChar
        End Get
        Set(ByVal Value As Char)
            mAcquisitionTimeFileSepChar = Value
        End Set
    End Property

    Public Shared ReadOnly Property DefaultAcquisitionTimeFilename() As String
        Get
            If USE_XML_OUTPUT_FILE Then
                Return DEFAULT_ACQUISITION_TIME_FILENAME_XML
            Else
                Return DEFAULT_ACQUISITION_TIME_FILENAME_TXT
            End If
        End Get
    End Property

    Public ReadOnly Property ErrorCode() As eMSFileScannerErrorCodes
        Get
            Return mErrorCode
        End Get
    End Property

    Public Property ReprocessExistingFiles() As Boolean
        Get
            Return mReprocessExistingFiles
        End Get
        Set(ByVal Value As Boolean)
            mReprocessExistingFiles = Value
        End Set
    End Property

    Public Property ReprocessIfCachedSizeIsZero() As Boolean
        Get
            Return mReprocessIfCachedSizeIsZero
        End Get
        Set(ByVal Value As Boolean)
            mReprocessIfCachedSizeIsZero = Value
        End Set
    End Property

    Public Property ShowMessages() As Boolean
        Get
            Return mShowMessages
        End Get
        Set(ByVal Value As Boolean)
            mShowMessages = Value
        End Set
    End Property

    Public ReadOnly Property StatusMessage() As String
        Get
            Return mStatusMessage
        End Get
    End Property
#End Region

    Private Function AssureMinimumDate(ByVal dtDate As DateTime, ByVal dtMinimumDate As DateTime) As DateTime
        ' Assures that dtDate is >= dtMinimumDate

        If dtDate < dtMinimumDate Then
            Return dtMinimumDate
        Else
            Return dtDate
        End If

    End Function

    Private Sub AutosaveCachedResults()

        If mCachedResultsAutoSaveIntervalMinutes > 0 AndAlso mCachedResultsState = eCachedResultsStateConstants.Modified Then
            If System.DateTime.Now.Subtract(mCachedResultsLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes Then
                ' Auto save the cached results
                SaveCachedResults(False)
            End If
        End If
    End Sub

    Private Function CachedResultsContainsDataset(ByVal strDatasetName As String) As Boolean
        Return CachedResultsContainsDataset(strDatasetName, Nothing)
    End Function

    Private Function CachedResultsContainsDataset(ByVal strDatasetName As String, ByRef objRowMatch As System.Data.DataRow) As Boolean

        Dim objCurrentIndex As Object

        If mMSFileInfoDataset Is Nothing OrElse mMSFileInfoDataset.Tables(0).Rows.Count = 0 Then
            objRowMatch = Nothing
            Return False
        End If

        ' Look for strDatasetName in mMSFileInfoDataset
        Try

            objRowMatch = mMSFileInfoDataset.Tables(0).Rows.Find(strDatasetName)

            If objRowMatch Is Nothing Then
                Return False
            Else
                Return True
            End If
        Catch ex As System.Exception
            Return False
        End Try

    End Function

    Private Sub ClearCachedResults()
        mMSFileInfoDataset.Tables(0).Clear()
        mCachedResultsState = eCachedResultsStateConstants.NotInitialized
    End Sub

    Private Function ConstructHeaderLine() As String
        ' Note: The order of the output should match eResultsFileColumns
        Return COL_NAME_DATASET_ID & mAcquisitionTimeFileSepChar & _
                COL_NAME_DATASET_NAME & mAcquisitionTimeFileSepChar & _
                COL_NAME_FILE_EXTENSION & mAcquisitionTimeFileSepChar & _
                COL_NAME_ACQ_TIME_START & mAcquisitionTimeFileSepChar & _
                COL_NAME_ACQ_TIME_END & mAcquisitionTimeFileSepChar & _
                COL_NAME_SCAN_COUNT & mAcquisitionTimeFileSepChar & _
                COL_NAME_FILE_SIZE_BYTES & mAcquisitionTimeFileSepChar & _
                COL_NAME_LAST_MODIFIED & mAcquisitionTimeFileSepChar & _
                COL_NAME_FILE_MODIFICATION_DATE
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

        Dim fsInFile As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim strLineIn As String
        Dim strSplitLine() As String
        Dim strSepChars() As Char

        Dim strDatasetName As String
        Dim udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType
        Dim dtLastModified As DateTime


        Dim objNewRow As System.Data.DataRow

        strSepChars = New Char() {mAcquisitionTimeFileSepChar}

        ' Clear the MS Info Table
        ClearCachedResults()

        ValidateAcquisitionTimeFilePath()

        If System.IO.File.Exists(mAcquisitionTimeFilePath) Then
            ' Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(0)

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
                            strDatasetName = strSplitLine(eResultsFileColumns.DatasetName)

                            If SharedVBNetRoutines.VBNetRoutines.IsNumber(strSplitLine(eResultsFileColumns.DatasetID)) Then
                                If Not CachedResultsContainsDataset(strDatasetName) Then
                                    Try
                                        objNewRow = mMSFileInfoDataset.Tables(0).NewRow()

                                        With udtFileInfo
                                            .DatasetID = CType(strSplitLine(eResultsFileColumns.DatasetID), Integer)
                                            .DatasetName = String.Copy(strDatasetName)
                                            .FileExtension = String.Copy(strSplitLine(eResultsFileColumns.FileExtension))
                                            .AcqTimeStart = CType(strSplitLine(eResultsFileColumns.AcqTimeStart), DateTime)
                                            .AcqTimeEnd = CType(strSplitLine(eResultsFileColumns.AcqTimeEnd), DateTime)
                                            .ScanCount = CType(strSplitLine(eResultsFileColumns.ScanCount), Integer)
                                            .FileSizeBytes = CType(strSplitLine(eResultsFileColumns.FileSizeBytes), Long)
                                            dtLastModified = CType(strSplitLine(eResultsFileColumns.LastModified), DateTime)

                                            If strSplitLine.Length >= 9 Then
                                                .FileSystemModificationTime = CType(strSplitLine(eResultsFileColumns.FileModificationDate), DateTime)
                                            End If
                                        End With

                                        PopulateDataRowWithInfo(udtFileInfo, objNewRow, dtLastModified)
                                        mMSFileInfoDataset.Tables(0).Rows.Add(objNewRow)

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

        mCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified

    End Sub

    Private Sub InitializeDatasets()

        Dim dtDefaultDate As DateTime = System.DateTime.Now()

        ' Make the Peak Matching Thresholds datatable
        Dim dtMSFileInfo As System.Data.DataTable = New System.Data.DataTable(MS_FILEINFO_DATATABLE)

        ' Add the columns to the datatable
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtMSFileInfo, COL_NAME_DATASET_ID)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(dtMSFileInfo, COL_NAME_DATASET_NAME)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(dtMSFileInfo, COL_NAME_FILE_EXTENSION)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_ACQ_TIME_START, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_ACQ_TIME_END, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(dtMSFileInfo, COL_NAME_SCAN_COUNT)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnLongToTable(dtMSFileInfo, COL_NAME_FILE_SIZE_BYTES)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_LAST_MODIFIED, dtDefaultDate)
        SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(dtMSFileInfo, COL_NAME_FILE_MODIFICATION_DATE, dtDefaultDate)

        ' Use the dataset name as the primary key since we won't always know Dataset_ID
        With dtMSFileInfo
            Dim PrimaryKeyColumn As System.Data.DataColumn() = New System.Data.DataColumn() {.Columns(COL_NAME_DATASET_NAME)}
            .PrimaryKey = PrimaryKeyColumn
        End With

        ' Instantiate the dataset
        mMSFileInfoDataset = New System.Data.DataSet(MS_FILEINFO_DATATABLE)

        ' Add the new System.Data.DataTable to the DataSet.
        mMSFileInfoDataset.Tables.Add(dtMSFileInfo)

        mCachedResultsState = eCachedResultsStateConstants.NotInitialized
    End Sub

    Private Sub InitializeLocalVariables()
        mErrorCode = eMSFileScannerErrorCodes.NoError

        mStatusMessage = String.Empty
        ValidateAcquisitionTimeFilePath()
        mAcquisitionTimeFileSepChar = ControlChars.Tab

        mCachedResultsAutoSaveIntervalMinutes = 5
        mCachedResultsLastSaveTime = System.DateTime.Now()

        mReprocessExistingFiles = False
        mReprocessIfCachedSizeIsZero = False

        InitializeDatasets()

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

        mStatusMessage = String.Copy(strMessage)

        strMessageWithoutCRLF = mStatusMessage.Replace(ControlChars.NewLine, "; ")

        If ex Is Nothing Then
            ex = New System.Exception("Error")
        Else
            If Not ex.Message Is Nothing AndAlso ex.Message.Length > 0 Then
                strMessageWithoutCRLF &= "; " & ex.Message
            End If
        End If

        Console.WriteLine(Now.ToLongTimeString & "; " & strMessageWithoutCRLF, strSource)

        'If Not mErrorLogger Is Nothing Then
        '    mErrorLogger.PostError(mStatusMessage.Replace(ControlChars.NewLine, "; "), ex, blnLogLocalOnly)
        'End If

        If Not eNewErrorCode = eMSFileScannerErrorCodes.NoError Then
            SetErrorCode(eNewErrorCode, True)
        End If

        If mShowMessages AndAlso blnAllowInformUser Then
            Windows.Forms.MessageBox.Show(mStatusMessage & ControlChars.NewLine & ex.Message, "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
        ElseIf blnAllowThrowingException Then
            Throw New System.Exception(mStatusMessage, ex)
        End If
    End Sub

    Private Sub PopulateDataRowWithInfo(ByRef udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow)
        PopulateDataRowWithInfo(udtFileInfo, objRow, System.DateTime.Now())
    End Sub

    Private Sub PopulateDataRowWithInfo(ByRef udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow, ByVal dtLastModified As DateTime)
        With objRow
            .Item(COL_NAME_DATASET_ID) = udtFileInfo.DatasetID
            .Item(COL_NAME_DATASET_NAME) = udtFileInfo.DatasetName
            .Item(COL_NAME_FILE_EXTENSION) = udtFileInfo.FileExtension
            .Item(COL_NAME_ACQ_TIME_START) = AssureMinimumDate(udtFileInfo.AcqTimeStart, MINIMUM_DATETIME)
            .Item(COL_NAME_ACQ_TIME_END) = AssureMinimumDate(udtFileInfo.AcqTimeEnd, MINIMUM_DATETIME)
            .Item(COL_NAME_SCAN_COUNT) = udtFileInfo.ScanCount
            .Item(COL_NAME_FILE_SIZE_BYTES) = udtFileInfo.FileSizeBytes
            .Item(COL_NAME_LAST_MODIFIED) = AssureMinimumDate(dtLastModified, MINIMUM_DATETIME)
            .Item(COL_NAME_FILE_MODIFICATION_DATE) = AssureMinimumDate(udtFileInfo.FileSystemModificationTime, MINIMUM_DATETIME)
        End With
    End Sub

    Private Function ProcessMSDataset(ByVal strInputFileOrFolderPath As String, ByRef objMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor) As Boolean

        Dim udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType
        Dim intRetryCount As Integer

        Dim blnSuccess As Boolean

        ' Open the MS datafile (or datafolder), read the creation date, and update the status file

        intRetryCount = 0
        Do
            blnSuccess = objMSInfoScanner.ProcessDatafile(strInputFileOrFolderPath, udtFileInfo)

            If Not blnSuccess Then
                intRetryCount += 1

                If intRetryCount < MAX_FILE_READ_RETRY_COUNT Then
                    ' Retry if the file modification or creation time is within FILE_MODIFICATION_WINDOW_MINUTES minutes of the current time
                    If System.DateTime.Now.Subtract(udtFileInfo.FileSystemCreationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES OrElse _
                       System.DateTime.Now.Subtract(udtFileInfo.FileSystemModificationTime).TotalMinutes < FILE_MODIFICATION_WINDOW_MINUTES Then

                        ' Sleep for 10 seconds then try again
                        System.Threading.Thread.Sleep(10000)
                    Else
                        intRetryCount = MAX_FILE_READ_RETRY_COUNT
                    End If
                End If
            End If
        Loop While Not blnSuccess And intRetryCount < MAX_FILE_READ_RETRY_COUNT

        If Not blnSuccess And intRetryCount >= MAX_FILE_READ_RETRY_COUNT Then
            If udtFileInfo.DatasetName.Length > 0 Then
                ' Make an entry anyway; probably a corrupted file
                blnSuccess = True
            End If
        End If

        If blnSuccess Then
            ' Update the results database
            blnSuccess = UpdateCachedFileInfo(udtFileInfo)

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
    Public Function ProcessMSFileOrFolder(ByVal strInputFileOrFolderPath As String, ByVal strOutputFolderPath As String, ByVal blnResetErrorCode As Boolean) As Boolean
        ' Note: strInputFileOrFolderPath must be a known MS data file or MS data folder
        ' See function ProcessMSFilesAndRecurseFolders for more details
        ' This function returns True if it processed a file (or the dataset was processed previously)
        ' When SKIP_FILES_IN_ERROR = True, then it also returns True if the file type was a known type but the processing failed
        ' If the file type is unknown, or if an error occurs, then it returns false

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

        If strOutputFolderPath Is Nothing OrElse strOutputFolderPath.Length = 0 Then
            ' Define strOutputFolderPath based on the program file path
            strOutputFolderPath = GetAppFolderPath()
        End If

        If mCachedResultsState = eCachedResultsStateConstants.NotInitialized Then
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
                    LogErrors("ProcessMSFileOrFolder", "Unknown file type", Nothing, False, False, True, eMSFileScannerErrorCodes.UnknownFileExtension)
                    Return False
                End If

                If Not mReprocessExistingFiles Then
                    ' See if the strDatasetName in strInputFileOrFolderPath is already present in mCachedResults
                    ' If it is present, then don't process it (unless mReprocessIfCachedSizeIsZero = True and it's size is 0)
                    Dim strDatasetName As String
                    strDatasetName = objMSInfoScanner.GetDatasetNameViaPath(objFileSystemInfo.FullName)
                    If strDatasetName.Length > 0 AndAlso CachedResultsContainsDataset(strDatasetName, objRow) Then
                        If mReprocessIfCachedSizeIsZero Then
                            Try
                                lngCachedSizeBytes = CLng(objRow.Item(COL_NAME_FILE_SIZE_BYTES))
                            Catch ex2 As System.Exception
                                lngCachedSizeBytes = 1
                            End Try

                            If lngCachedSizeBytes > 0 Then
                                Return True
                            End If
                        Else
                            Return True
                        End If
                    End If
                End If

                ' Process the data file or folder
                blnSuccess = ProcessMSDataset(strInputFileOrFolderPath, objMSInfoScanner)

            End If

        Catch ex As System.Exception
            blnSuccess = False
            LogErrors("ProcessFile", "Error in ProcessFile", ex, True, False, False, eMSFileScannerErrorCodes.UnspecifiedError)
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

        Dim ioPath As System.IO.Path

        Dim ioFileMatch As System.IO.FileInfo
        Dim ioFolderMatch As System.IO.DirectoryInfo

        Dim ioFileInfo As System.IO.FileInfo
        Dim ioFolderInfo As System.IO.DirectoryInfo

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
                    strInputFolderPath = ioPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ioFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)

                ' Remove any directory information from strInputFileOrFolderPath
                strInputFileOrFolderPath = ioPath.GetFileName(strInputFileOrFolderPath)

                intMatchCount = 0
                For Each ioFileMatch In ioFolderInfo.GetFiles(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, blnResetErrorCode)

                    If Not blnSuccess Or mAbortProcessing Then Exit For
                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next ioFileMatch

                For Each ioFolderMatch In ioFolderInfo.GetDirectories(strInputFileOrFolderPath)

                    blnSuccess = ProcessMSFileOrFolder(ioFolderMatch.FullName, strOutputFolderPath, blnResetErrorCode)

                    If Not blnSuccess Or mAbortProcessing Then Exit For
                    intMatchCount += 1

                    If intMatchCount Mod 100 = 0 Then Console.Write(".")
                Next ioFolderMatch


                If intMatchCount = 0 And Me.ShowMessages Then
                    If mErrorCode = eMSFileScannerErrorCodes.NoError Then
                        MsgBox("No match was found for the input file path:" & ControlChars.NewLine & strInputFileOrFolderPath, MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "File not found")
                    End If
                Else
                    Console.WriteLine()
                End If
            Else
                blnSuccess = ProcessMSFileOrFolder(strInputFileOrFolderPath, strOutputFolderPath, blnResetErrorCode)
            End If

        Catch ex As System.Exception
            If Me.ShowMessages Then
                MsgBox("Error in ProcessFilesWildcard: " & ControlChars.NewLine & ex.Message, MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "Error")
            Else
                Throw New System.Exception("Error in ProcessFilesWildcard", ex)
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
        Dim ioPath As System.IO.Path
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
                    strInputFolderPath = ioPath.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If

                ' Remove any directory information from strInputFilePath
                strInputFilePathOrFolder = ioPath.GetFileName(strInputFilePathOrFolder)

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

                ' Call RecurseFoldersWork
                blnSuccess = RecurseFoldersWork(strInputFolderPath, strInputFilePathOrFolder, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, 1, intRecurseFoldersMaxLevels)

            Else
                SetErrorCode(eMSFileScannerErrorCodes.InvalidInputFilePath)
                Return False
            End If

        Catch ex As System.Exception
            LogErrors("ProcessMSFileOrFolder", ex.Message, ex, False, False, True, eMSFileScannerErrorCodes.InputFileReadError)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function RecurseFoldersWork(ByVal strInputFolderPath As String, ByVal strFileNameMatch As String, ByVal strOutputFolderPath As String, ByRef intFileProcessCount As Integer, ByRef intFileProcessFailCount As Integer, ByVal intRecursionLevel As Integer, ByVal intRecurseFoldersMaxLevels As Integer) As Boolean
        ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely

        Dim ioInputFolderInfo As System.IO.DirectoryInfo
        Dim ioSubFolderInfo As System.IO.DirectoryInfo

        Dim ioFileMatch As System.io.FileInfo

        Dim strFileExtensionsToParse() As String
        Dim strFolderExtensionsToParse() As String
        Dim blnProcessAllFileExtensions As Boolean

        Dim intExtensionIndex As Integer

        Dim blnSuccess As Boolean
        Dim blnFileProcessed As Boolean
        Dim blnProcessedZippedSFolder As Boolean

        Dim intSubFoldersProcessed As Integer
        Dim htSubFoldersProcessed As Hashtable

        Try
            ioInputFolderInfo = New System.IO.DirectoryInfo(strInputFolderPath)
        Catch ex As System.Exception
            ' Input folder path error
            LogErrors("RecurseFoldersWork", ex.Message, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
            Return False
        End Try

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
                blnFileProcessed = False
                For intExtensionIndex = 0 To strFileExtensionsToParse.Length - 1
                    If blnProcessAllFileExtensions OrElse ioFileMatch.Extension.ToUpper = strFileExtensionsToParse(intExtensionIndex) Then
                        blnFileProcessed = True
                        blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, True)
                        Exit For
                    End If

                    If mAbortProcessing Then Exit For
                Next intExtensionIndex

                If Not blnFileProcessed AndAlso Not blnProcessedZippedSFolder Then
                    ' Check for other valid files
                    If clsBrukerOneFolderInfoScanner.IsZippedSFolder(ioFileMatch.Name) Then
                        ' Only process this file if there is not a subfolder named "1" present"
                        If ioInputFolderInfo.GetDirectories(clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME).Length < 1 Then
                            blnFileProcessed = True
                            blnProcessedZippedSFolder = True
                            blnSuccess = ProcessMSFileOrFolder(ioFileMatch.FullName, strOutputFolderPath, True)
                        End If
                    End If
                End If

                If blnFileProcessed Then
                    If blnSuccess Then
                        intFileProcessCount += 1
                    Else
                        intFileProcessFailCount += 1
                        blnSuccess = True
                    End If
                End If

                If mAbortProcessing Then Exit For
            Next ioFileMatch

        Catch ex As System.Exception
            LogErrors("RecurseFoldersWork", ex.Message, ex, False, False, True, eMSFileScannerErrorCodes.InvalidInputFilePath)
            Return False
        End Try

        If Not mAbortProcessing Then
            ' Check the subfolders for those with known extensions

            intSubFoldersProcessed = 0
            htSubFoldersProcessed = New Hashtable
            For Each ioSubFolderInfo In ioInputFolderInfo.GetDirectories(strFileNameMatch)
                ' Check whether the folder name is BRUKER_ONE_FOLDER = "1"
                If ioSubFolderInfo.Name = clsBrukerOneFolderInfoScanner.BRUKER_ONE_FOLDER_NAME Then
                    blnSuccess = ProcessMSFileOrFolder(ioSubFolderInfo.FullName, strOutputFolderPath, True)
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
                            blnSuccess = ProcessMSFileOrFolder(ioSubFolderInfo.FullName, strOutputFolderPath, True)
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

                        If mAbortProcessing Then Exit For
                    Next intExtensionIndex
                End If
            Next ioSubFolderInfo

            ' If intRecurseFoldersMaxLevels is <=0 then we recurse infinitely
            '  otherwise, compare intRecursionLevel to intRecurseFoldersMaxLevels
            If intRecurseFoldersMaxLevels <= 0 OrElse intRecursionLevel <= intRecurseFoldersMaxLevels Then
                ' Call this function for each of the subfolders of ioInputFolderInfo
                ' However, do not step into folders listed in htSubFoldersProcessed
                For Each ioSubFolderInfo In ioInputFolderInfo.GetDirectories()
                    If intSubFoldersProcessed = 0 OrElse Not htSubFoldersProcessed.Contains(ioSubFolderInfo.Name) Then
                        blnSuccess = RecurseFoldersWork(ioSubFolderInfo.FullName, strFileNameMatch, strOutputFolderPath, intFileProcessCount, intFileProcessFailCount, intRecursionLevel + 1, intRecurseFoldersMaxLevels)
                    End If
                    If Not blnSuccess Then Exit For
                Next ioSubFolderInfo
            End If
        End If

        Return blnSuccess

    End Function

    Public Function SaveCachedResults() As Boolean
        Return SaveCachedResults(True)
    End Function

    Private Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean

        Dim fsOutfile As System.IO.FileStream
        Dim srOutFile As System.IO.StreamWriter

        Dim objRow As System.Data.DataRow
        Dim intIndex As Integer
        Dim blnSuccess As Boolean

        If Not mMSFileInfoDataset Is Nothing AndAlso mMSFileInfoDataset.Tables(0).Rows.Count > 0 AndAlso mCachedResultsState = eCachedResultsStateConstants.Modified Then

            Try
                ' Write all of mMSFileInfoDataset.Tables(0) to the results file
                If USE_XML_OUTPUT_FILE Then
                    fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    mMSFileInfoDataset.WriteXml(fsOutfile)
                    fsOutfile.Close()
                Else
                    fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
                    srOutFile = New System.IO.StreamWriter(fsOutfile)

                    srOutFile.WriteLine(ConstructHeaderLine())

                    For Each objRow In mMSFileInfoDataset.Tables(0).Rows
                        WriteResultsLine(srOutFile, objRow)
                    Next objRow

                    srOutFile.Close()
                End If

                mCachedResultsLastSaveTime = System.DateTime.Now()

                If blnClearCachedData Then
                    ' Clear the data table
                    ClearCachedResults()
                Else
                    mCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified
                End If

                blnSuccess = True
            Catch ex As System.Exception
                blnSuccess = False
                LogErrors("ProcessFile", "Error in SaveCachedResults", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
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

        Dim intIndex As Integer

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

    Private Function UpdateCachedFileInfo(ByVal udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Update the entry for this dataset in mMSFileInfoDataset.Tables(0)

        Dim objRow As System.Data.DataRow

        Dim intCurrentIndex As Integer
        Dim blnSuccess As Boolean

        Try
            ' Examine the data in memory and add or update the data for strDataset
            If CachedResultsContainsDataset(udtFileInfo.DatasetName, objRow) Then
                ' Item already present; update it
                Try
                    PopulateDataRowWithInfo(udtFileInfo, objRow)
                Catch ex As System.Exception
                    ' Ignore errors updating the entry
                End Try
            Else
                ' Item not present; add it
                objRow = mMSFileInfoDataset.Tables(0).NewRow
                PopulateDataRowWithInfo(udtFileInfo, objRow)
                mMSFileInfoDataset.Tables(0).Rows.Add(objRow)
            End If

            mCachedResultsState = eCachedResultsStateConstants.Modified

            blnSuccess = True
        Catch ex As System.Exception
            blnSuccess = False
            LogErrors("ProcessFile", "Error in UpdateCachedFileInfo", ex, True, False, False, eMSFileScannerErrorCodes.OutputFileWriteError)
        End Try

        Return blnSuccess

    End Function

    Private Function ValidateAcquisitionTimeFilePath() As Boolean

        Dim ioFileInfo As System.IO.FileInfo
        Dim blnValidFile As Boolean

        If mAcquisitionTimeFilePath Is Nothing OrElse mAcquisitionTimeFilePath.Length = 0 Then
            mAcquisitionTimeFilePath = System.IO.Path.Combine(GetAppFolderPath(), Me.DefaultAcquisitionTimeFilename)
        End If

        Try
            ioFileInfo = New System.IO.FileInfo(mAcquisitionTimeFilePath)

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

    Private Sub WriteResultsLine(ByRef srOutFile As System.IO.StreamWriter, ByRef objRow As System.Data.DataRow)
        With objRow
            ' Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(.Item(COL_NAME_DATASET_ID).ToString & mAcquisitionTimeFileSepChar & _
                                .Item(COL_NAME_DATASET_NAME).ToString & mAcquisitionTimeFileSepChar & _
                                .Item(COL_NAME_FILE_EXTENSION).ToString & mAcquisitionTimeFileSepChar & _
                                CType(.Item(COL_NAME_ACQ_TIME_START), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & mAcquisitionTimeFileSepChar & _
                                CType(.Item(COL_NAME_ACQ_TIME_END), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & mAcquisitionTimeFileSepChar & _
                                .Item(COL_NAME_SCAN_COUNT).ToString & mAcquisitionTimeFileSepChar & _
                                .Item(COL_NAME_FILE_SIZE_BYTES).ToString & mAcquisitionTimeFileSepChar & _
                                .Item(COL_NAME_LAST_MODIFIED).ToString & mAcquisitionTimeFileSepChar & _
                                CType(.Item(COL_NAME_FILE_MODIFICATION_DATE), DateTime).ToString("yyyy-MM-dd HH:mm:ss"))

        End With
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
End Class
