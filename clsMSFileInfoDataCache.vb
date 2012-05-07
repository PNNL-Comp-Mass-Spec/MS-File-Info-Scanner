Option Strict On

Public Class clsMSFileInfoDataCache

#Region "Constants and Enums"
    Private Const MS_FILEINFO_DATATABLE As String = "MSFileInfoTable"
    Public Const COL_NAME_DATASET_ID As String = "DatasetID"
    Public Const COL_NAME_DATASET_NAME As String = "DatasetName"
    Public Const COL_NAME_FILE_EXTENSION As String = "FileExtension"
    Public Const COL_NAME_ACQ_TIME_START As String = "AcqTimeStart"
    Public Const COL_NAME_ACQ_TIME_END As String = "AcqTimeEnd"
    Public Const COL_NAME_SCAN_COUNT As String = "ScanCount"
    Public Const COL_NAME_FILE_SIZE_BYTES As String = "FileSizeBytes"
    Public Const COL_NAME_INFO_LAST_MODIFIED As String = "InfoLastModified"
    Public Const COL_NAME_FILE_MODIFICATION_DATE As String = "FileModificationDate"

    Private Const FOLDER_INTEGRITY_INFO_DATATABLE As String = "FolderIntegrityInfoTable"
    Public Const COL_NAME_FOLDER_ID As String = "FolderID"
    Public Const COL_NAME_FOLDER_PATH As String = "FolderPath"
    Public Const COL_NAME_FILE_COUNT As String = "FileCount"
    Public Const COL_NAME_COUNT_FAIL_INTEGRITY As String = "FileCountFailedIntegrity"

    Public Const COL_NAME_FILE_NAME As String = "FileName"
    Public Const COL_NAME_FAILED_INTEGRITY_CHECK As String = "FailedIntegrityCheck"
    Public Const COL_NAME_SHA1_HASH As String = "Sha1Hash"

    Private Const MINIMUM_DATETIME As DateTime = #1/1/1900#     ' Equivalent to DateTime.MinValue

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
#End Region


    Private Enum eCachedResultsStateConstants
        NotInitialized = 0
        InitializedButUnmodified = 1
        Modified = 2
    End Enum

#Region "Classwide Variables"
    Private mAcquisitionTimeFilePath As String
    Private mFolderIntegrityInfoFilePath As String

    Private mCachedResultsAutoSaveIntervalMinutes As Integer
    Private mCachedMSInfoResultsLastSaveTime As DateTime
    Private mCachedFolderIntegrityInfoLastSaveTime As DateTime

    Private mMSFileInfoDataset As System.Data.DataSet
    Private mMSFileInfoCachedResultsState As eCachedResultsStateConstants

    Private mFolderIntegrityInfoDataset As System.Data.DataSet
    Private mFolderIntegrityInfoResultsState As eCachedResultsStateConstants
    Private mMaximumFolderIntegrityInfoFolderID As Integer = 0

    Public Event ErrorEvent(ByVal Message As String)
    Public Event StatusEvent(ByVal Message As String)
#End Region

#Region "Properties"

    Public Property AcquisitionTimeFilePath() As String
        Get
            Return mAcquisitionTimeFilePath
        End Get
        Set(ByVal value As String)
            mAcquisitionTimeFilePath = value
        End Set
    End Property

    Public Property FolderIntegrityInfoFilePath() As String
        Get
            Return mFolderIntegrityInfoFilePath
        End Get
        Set(ByVal value As String)
            mFolderIntegrityInfoFilePath = value
        End Set
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

    Public Sub AutosaveCachedResults()

        If mCachedResultsAutoSaveIntervalMinutes > 0 Then
            If mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified Then
                If System.DateTime.UtcNow.Subtract(mCachedMSInfoResultsLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes Then
                    ' Auto save the cached results
                    SaveCachedMSInfoResults(False)
                End If
            End If

            If mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified Then
                If System.DateTime.UtcNow.Subtract(mCachedFolderIntegrityInfoLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes Then
                    ' Auto save the cached results
                    SaveCachedFolderIntegrityInfoResults(False)
                End If
            End If
        End If

    End Sub

    Public Function CachedMSInfoContainsDataset(ByVal strDatasetName As String) As Boolean
        Return CachedMSInfoContainsDataset(strDatasetName, Nothing)
    End Function

    Public Function CachedMSInfoContainsDataset(ByVal strDatasetName As String, ByRef objRowMatch As System.Data.DataRow) As Boolean
        Return DatasetTableContainsPrimaryKeyValue(mMSFileInfoDataset, MS_FILEINFO_DATATABLE, strDatasetName, objRowMatch)
    End Function


    Public Function CachedFolderIntegrityInfoContainsFolder(ByVal strFolderPath As String, ByRef intFolderID As Integer) As Boolean
        Return CachedFolderIntegrityInfoContainsFolder(strFolderPath, intFolderID, Nothing)
    End Function

    Public Function CachedFolderIntegrityInfoContainsFolder(ByVal strFolderPath As String, ByRef intFolderID As Integer, ByRef objRowMatch As System.Data.DataRow) As Boolean
        If DatasetTableContainsPrimaryKeyValue(mFolderIntegrityInfoDataset, FOLDER_INTEGRITY_INFO_DATATABLE, strFolderPath, objRowMatch) Then
            intFolderID = CInt(objRowMatch(COL_NAME_FOLDER_ID))
            Return True
        Else
            Return False
        End If
    End Function

    Private Sub ClearCachedMSInfoResults()
        mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Clear()
        mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized
    End Sub

    Private Sub ClearCachedFolderIntegrityInfoResults()
        mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Clear()
        mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized
        mMaximumFolderIntegrityInfoFolderID = 0
    End Sub

	Public Function ConstructHeaderLine(ByVal eDataFileType As MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants) As String
		Select Case eDataFileType
			Case MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo
				' Note: The order of the output should match eMSFileInfoResultsFileColumns
				Return COL_NAME_DATASET_ID & ControlChars.Tab & _
				  COL_NAME_DATASET_NAME & ControlChars.Tab & _
				  COL_NAME_FILE_EXTENSION & ControlChars.Tab & _
				  COL_NAME_ACQ_TIME_START & ControlChars.Tab & _
				  COL_NAME_ACQ_TIME_END & ControlChars.Tab & _
				  COL_NAME_SCAN_COUNT & ControlChars.Tab & _
				  COL_NAME_FILE_SIZE_BYTES & ControlChars.Tab & _
				  COL_NAME_INFO_LAST_MODIFIED & ControlChars.Tab & _
				  COL_NAME_FILE_MODIFICATION_DATE

			Case MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo
				' Note: The order of the output should match eFolderIntegrityInfoFileColumns
				Return COL_NAME_FOLDER_ID & ControlChars.Tab & _
				  COL_NAME_FOLDER_PATH & ControlChars.Tab & _
				  COL_NAME_FILE_COUNT & ControlChars.Tab & _
				  COL_NAME_COUNT_FAIL_INTEGRITY & ControlChars.Tab & _
				  COL_NAME_INFO_LAST_MODIFIED

			Case MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails
				' Note: The order of the output should match eFileIntegrityDetailsFileColumns
				Return COL_NAME_FOLDER_ID & ControlChars.Tab & _
				  COL_NAME_FILE_NAME & ControlChars.Tab & _
				  COL_NAME_FILE_SIZE_BYTES & ControlChars.Tab & _
				  COL_NAME_FILE_MODIFICATION_DATE & ControlChars.Tab & _
				  COL_NAME_FAILED_INTEGRITY_CHECK & ControlChars.Tab & _
				  COL_NAME_SHA1_HASH & ControlChars.Tab & _
				  COL_NAME_INFO_LAST_MODIFIED

			Case MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors
				Return "File_Path" & ControlChars.Tab & "Error_Message" & ControlChars.Tab & COL_NAME_INFO_LAST_MODIFIED
			Case Else
				Return "Unknown_File_Type"
		End Select
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

	Public Sub InitializeVariables()
		mCachedResultsAutoSaveIntervalMinutes = 5
		mCachedMSInfoResultsLastSaveTime = System.DateTime.UtcNow
		mCachedFolderIntegrityInfoLastSaveTime = System.DateTime.UtcNow

		Me.FolderIntegrityInfoFilePath = System.IO.Path.Combine(clsMSFileInfoScanner.GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo))

		Me.AcquisitionTimeFilePath = System.IO.Path.Combine(clsMSFileInfoScanner.GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo))
		clsMSFileInfoScanner.ValidateDataFilePath(Me.AcquisitionTimeFilePath, MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo)

		InitializeDatasets()
	End Sub

	Private Function IsNumber(ByVal strValue As String) As Boolean
		Dim objFormatProvider As New System.Globalization.NumberFormatInfo
		Try
			Return Double.TryParse(strValue, Globalization.NumberStyles.Any, objFormatProvider, 0)
		Catch ex As System.Exception
			Return False
		End Try
	End Function

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

	Public Sub LoadCachedResults(ByVal blnForceLoad As Boolean)
		If blnForceLoad OrElse mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized Then
			LoadCachedMSFileInfoResults()
			LoadCachedFolderIntegrityInfoResults()
		End If
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

		strSepChars = New Char() {ControlChars.Tab}

		' Clear the Folder Integrity Info Table
		ClearCachedFolderIntegrityInfoResults()

		clsMSFileInfoScanner.ValidateDataFilePath(mFolderIntegrityInfoFilePath, MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo)

		RaiseEvent StatusEvent("Loading cached folder integrity info from: " & System.IO.Path.GetFileName(mFolderIntegrityInfoFilePath))

		If System.IO.File.Exists(mFolderIntegrityInfoFilePath) Then
			' Read the entries from mFolderIntegrityInfoFilePath, populating mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE)

			If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
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
		Dim udtFileInfo As iMSFileInfoProcessor.udtFileInfoType
		Dim dtInfoLastModified As DateTime

		Dim objNewRow As System.Data.DataRow

		strSepChars = New Char() {ControlChars.Tab}

		' Clear the MS Info Table
		ClearCachedMSInfoResults()

		clsMSFileInfoScanner.ValidateDataFilePath(mAcquisitionTimeFilePath, MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo)

		RaiseEvent StatusEvent("Loading cached acquisition time file data from: " & System.IO.Path.GetFileName(mAcquisitionTimeFilePath))

		If System.IO.File.Exists(mAcquisitionTimeFilePath) Then
			' Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

			If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
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

	Private Sub PopulateMSInfoDataRow(ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow)
		PopulateMSInfoDataRow(udtFileInfo, objRow, System.DateTime.Now())
	End Sub

	Private Sub PopulateMSInfoDataRow(ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, ByRef objRow As System.Data.DataRow, ByVal dtInfoLastModified As DateTime)

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

	''' <summary>
	''' Writes out the cache files immediately
	''' </summary>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Function SaveCachedResults() As Boolean
		Return SaveCachedResults(True)
	End Function

	Public Function SaveCachedResults(ByVal blnClearCachedData As Boolean) As Boolean
		Dim blnSuccess1 As Boolean
		Dim blnSuccess2 As Boolean

		blnSuccess1 = SaveCachedMSInfoResults(blnClearCachedData)
		blnSuccess2 = SaveCachedFolderIntegrityInfoResults(blnClearCachedData)

		Return blnSuccess1 And blnSuccess2

	End Function

	Public Function SaveCachedFolderIntegrityInfoResults(ByVal blnClearCachedData As Boolean) As Boolean

		Dim fsOutfile As System.IO.FileStream
		Dim srOutFile As System.IO.StreamWriter

		Dim objRow As System.Data.DataRow
		Dim blnSuccess As Boolean

		If Not mFolderIntegrityInfoDataset Is Nothing AndAlso _
		   mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows.Count > 0 AndAlso _
		   mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified Then

			RaiseEvent StatusEvent("Saving cached folder integrity info to: " & System.IO.Path.GetFileName(mFolderIntegrityInfoFilePath))

			Try
				' Write all of mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE) to the results file
				If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
					fsOutfile = New System.IO.FileStream(mFolderIntegrityInfoFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
					mFolderIntegrityInfoDataset.WriteXml(fsOutfile)
					fsOutfile.Close()
				Else
					fsOutfile = New System.IO.FileStream(mFolderIntegrityInfoFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
					srOutFile = New System.IO.StreamWriter(fsOutfile)

					srOutFile.WriteLine(ConstructHeaderLine(MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo))

					For Each objRow In mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE).Rows
						WriteFolderIntegrityInfoDataLine(srOutFile, objRow)
					Next objRow

					srOutFile.Close()
				End If

				mCachedFolderIntegrityInfoLastSaveTime = System.DateTime.UtcNow

				If blnClearCachedData Then
					' Clear the data table
					ClearCachedFolderIntegrityInfoResults()
				Else
					mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified
				End If

				blnSuccess = True

			Catch ex As System.Exception
				RaiseEvent ErrorEvent("Error in SaveCachedFolderIntegrityInfoResults: " & ex.Message)
				blnSuccess = False
			Finally
				If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
					fsOutfile = Nothing
				Else
					fsOutfile = Nothing
					srOutFile = Nothing
				End If
			End Try
		End If

		Return blnSuccess

	End Function

	Public Function SaveCachedMSInfoResults(ByVal blnClearCachedData As Boolean) As Boolean

		Dim fsOutfile As System.IO.FileStream
		Dim srOutFile As System.IO.StreamWriter

		Dim objRow As System.Data.DataRow
		Dim blnSuccess As Boolean

		If Not mMSFileInfoDataset Is Nothing AndAlso _
		   mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows.Count > 0 AndAlso _
		   mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified Then

			RaiseEvent StatusEvent("Saving cached acquisition time file data to: " & System.IO.Path.GetFileName(mAcquisitionTimeFilePath))

			Try
				' Write all of mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE) to the results file
				If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
					fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
					mMSFileInfoDataset.WriteXml(fsOutfile)
					fsOutfile.Close()
				Else
					fsOutfile = New System.IO.FileStream(mAcquisitionTimeFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read)
					srOutFile = New System.IO.StreamWriter(fsOutfile)

					srOutFile.WriteLine(ConstructHeaderLine(MSFileInfoScannerInterfaces.iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo))

					For Each objRow In mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE).Rows
						WriteMSInfoDataLine(srOutFile, objRow)
					Next objRow

					srOutFile.Close()
				End If

				mCachedMSInfoResultsLastSaveTime = System.DateTime.UtcNow

				If blnClearCachedData Then
					' Clear the data table
					ClearCachedMSInfoResults()
				Else
					mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified
				End If

				blnSuccess = True

			Catch ex As System.Exception
				RaiseEvent ErrorEvent("Error in SaveCachedMSInfoResults: " & ex.Message)
				blnSuccess = False
			Finally
				If clsMSFileInfoScanner.USE_XML_OUTPUT_FILE Then
					fsOutfile = Nothing
				Else
					fsOutfile = Nothing
					srOutFile = Nothing
				End If
			End Try
		End If

		Return blnSuccess

	End Function

	Public Function UpdateCachedMSFileInfo(ByVal udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Update the entry for this dataset in mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

		Dim objRow As System.Data.DataRow = Nothing

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
			RaiseEvent ErrorEvent("Error in UpdateCachedMSFileInfo: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

    Public Function UpdateCachedFolderIntegrityInfo(ByVal udtFolderStats As clsFileIntegrityChecker.udtFolderStatsType, ByRef intFolderID As Integer) As Boolean
        ' Update the entry for this dataset in mFolderIntegrityInfoDataset.Tables(FOLDER_INTEGRITY_INFO_DATATABLE)

		Dim objRow As System.Data.DataRow = Nothing

        Dim blnSuccess As Boolean

        Try
            If mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized Then
                ' Coding error; this shouldn't be the case
                RaiseEvent ErrorEvent("mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized in UpdateCachedFolderIntegrityInfo; unable to continue")
                Return False
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
            RaiseEvent ErrorEvent("Error in UpdateCachedFolderIntegrityInfo: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function



    Private Sub WriteMSInfoDataLine(ByRef srOutFile As System.IO.StreamWriter, ByRef objRow As System.Data.DataRow)
        With objRow
            ' Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(.Item(COL_NAME_DATASET_ID).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_DATASET_NAME).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_FILE_EXTENSION).ToString & ControlChars.Tab & _
                                CType(.Item(COL_NAME_ACQ_TIME_START), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & ControlChars.Tab & _
                                CType(.Item(COL_NAME_ACQ_TIME_END), DateTime).ToString("yyyy-MM-dd HH:mm:ss") & ControlChars.Tab & _
                                .Item(COL_NAME_SCAN_COUNT).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_FILE_SIZE_BYTES).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_INFO_LAST_MODIFIED).ToString & ControlChars.Tab & _
                                CType(.Item(COL_NAME_FILE_MODIFICATION_DATE), DateTime).ToString("yyyy-MM-dd HH:mm:ss"))

        End With
    End Sub

    Private Sub WriteFolderIntegrityInfoDataLine(ByRef srOutFile As System.IO.StreamWriter, ByRef objRow As System.Data.DataRow)

        With objRow
            srOutFile.WriteLine(.Item(COL_NAME_FOLDER_ID).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_FOLDER_PATH).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_FILE_COUNT).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_COUNT_FAIL_INTEGRITY).ToString & ControlChars.Tab & _
                                .Item(COL_NAME_INFO_LAST_MODIFIED).ToString)
        End With
    End Sub

    Protected Overrides Sub Finalize()
        Me.SaveCachedResults()
        MyBase.Finalize()
    End Sub

End Class
