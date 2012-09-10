Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified July 23, 2012

Public Class clsZippedImagingFilesScanner

	Inherits clsMSFileInfoProcessorBaseClass

	Public Const ZIPPED_IMAGING_FILE_SEARCH_SPEC As String = "0_R*.zip"
	Public Const ZIPPED_IMAGING_FILE_NAME_PREFIX As String = "0_R"

	''' <summary>
	''' Examines the subdirectories in the specified zip file
	''' Determines the oldest and newest modified analysis.baf files (or apexAcquisition.method file if analysis.baf files are not found)
	''' Presumes this is the AcqStartTime and AcqEndTime
	''' </summary>
	''' <param name="ioZipFile"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns>True if at least one valid file is found; otherwise false</returns>
	''' <remarks></remarks>
	Protected Function DetermineAcqStartEndTime(ByVal ioZipFile As System.IO.FileInfo, _
	   ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim blnSuccess As Boolean = False

		Dim lstFileNamesToFind As System.Collections.Generic.List(Of String)
		Dim strNameParts() As String

		Try
			' Bump up the file size
			udtFileInfo.FileSizeBytes += ioZipFile.Length

			lstFileNamesToFind = New System.Collections.Generic.List(Of String)
			lstFileNamesToFind.Add("analysis.baf")
			lstFileNamesToFind.Add("apexAcquisition.method")
			lstFileNamesToFind.Add("submethods.xml")

			Dim oZipFile As Ionic.Zip.ZipFile
			Dim oZipEntry As System.Collections.Generic.IEnumerator(Of Ionic.Zip.ZipEntry)
			oZipFile = New Ionic.Zip.ZipFile(ioZipFile.FullName)

			For Each strFileNameToFind As String In lstFileNamesToFind
				oZipEntry = oZipFile.GetEnumerator()

				Do While oZipEntry.MoveNext

					If Not oZipEntry.Current.IsDirectory Then

						' Split the filename on the forward slash character
						strNameParts = oZipEntry.Current.FileName.Split("/"c)

						If Not strNameParts Is Nothing AndAlso strNameParts.Length > 0 Then

							If strNameParts(strNameParts.Length - 1).ToLower() = strFileNameToFind.ToLower() Then
								If oZipEntry.Current.LastModified < udtFileInfo.AcqTimeStart Then
									udtFileInfo.AcqTimeStart = oZipEntry.Current.LastModified
								End If

								If oZipEntry.Current.LastModified > udtFileInfo.AcqTimeEnd Then
									udtFileInfo.AcqTimeEnd = oZipEntry.Current.LastModified
								End If

								' Bump up the scan count
								udtFileInfo.ScanCount += 1

								' Add a Scan Stats entry
								Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

								objScanStatsEntry.ScanNumber = udtFileInfo.ScanCount
								objScanStatsEntry.ScanType = 1

								objScanStatsEntry.ScanTypeName = "MALDI-HMS"
								objScanStatsEntry.ScanFilterText = ""

								objScanStatsEntry.ElutionTime = "0"
								objScanStatsEntry.TotalIonIntensity = "0"
								objScanStatsEntry.BasePeakIntensity = "0"
								objScanStatsEntry.BasePeakMZ = "0"

								' Base peak signal to noise ratio
								objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

								objScanStatsEntry.IonCount = 0
								objScanStatsEntry.IonCountRaw = 0

								mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


								blnSuccess = True

							End If
						End If

					End If
				Loop

				If blnSuccess Then Exit For
			Next

		Catch ex As System.Exception
			ReportError("Error finding XMass method folder: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Protected Function GetDatasetFolder(ByVal strDataFilePath As String) As System.IO.DirectoryInfo

		Dim ioDatasetFolder As System.IO.DirectoryInfo
		Dim ioFileInfo As System.IO.FileInfo

		' First see if strFileOrFolderPath points to a valid file
		ioFileInfo = New System.IO.FileInfo(strDataFilePath)

		If ioFileInfo.Exists() Then
			' User specified a file; assume the parent folder of this file is the dataset folder
			ioDatasetFolder = ioFileInfo.Directory
		Else
			' Assume this is the path to the dataset folder
			ioDatasetFolder = New System.IO.DirectoryInfo(strDataFilePath)
		End If

		Return ioDatasetFolder

	End Function

	Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
		Dim ioDatasetFolder As System.IO.DirectoryInfo
		Dim strDatasetName As String = String.Empty

		Try
			' The dataset name for a dataset with zipped imaging files is the name of the parent directory
			' However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
			ioDatasetFolder = GetDatasetFolder(strDataFilePath)
			strDatasetName = ioDatasetFolder.Name

			If strDatasetName.ToLower().EndsWith(".d") Then
				strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2)
			End If

		Catch ex As System.Exception
			' Ignore errors
		End Try

		If strDatasetName Is Nothing Then strDatasetName = String.Empty
		Return strDatasetName

	End Function

	Public Shared Function IsZippedImagingFile(ByVal strFileName As String) As Boolean

		Dim ioFileInfo As System.IO.FileInfo = New System.IO.FileInfo(strFileName)

		If String.IsNullOrWhiteSpace(strFileName) Then
			Return False
		End If

		If ioFileInfo.Name.ToLower().StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX.ToLower()) AndAlso ioFileInfo.Extension.ToLower() = ".zip" Then
			Return True
		Else
			Return False
		End If

	End Function

	Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the Zip files in the dataset folder)

		Dim ioDatasetFolder As System.IO.DirectoryInfo

		Dim ioFiles() As System.IO.FileInfo

		Dim blnSuccess As Boolean

		Try
			' Determine whether strDataFilePath points to a file or a folder

			ioDatasetFolder = GetDatasetFolder(strDataFilePath)

			' Validate that we have selected a valid folder
			If Not ioDatasetFolder.Exists Then
				MyBase.ReportError("File/folder not found: " & strDataFilePath)
				Return False
			End If

			' In case we cannot find any .Zip files, update the .AcqTime values to the folder creation date
			With udtFileInfo
				.AcqTimeStart = ioDatasetFolder.CreationTime
				.AcqTimeEnd = ioDatasetFolder.CreationTime
			End With

			' Look for the 0_R*.zip files
			' If we cannot find any zip files, return false

			ioFiles = ioDatasetFolder.GetFiles(ZIPPED_IMAGING_FILE_SEARCH_SPEC)
			If ioFiles Is Nothing OrElse ioFiles.Length = 0 Then
				' 0_R*.zip files not found
				MyBase.ReportError(ZIPPED_IMAGING_FILE_SEARCH_SPEC & "files not found in " & ioDatasetFolder.FullName)
				blnSuccess = False
			Else

				' Initialize the .DatasetFileInfo
				With mDatasetStatsSummarizer.DatasetFileInfo
					.FileSystemCreationTime = ioFiles(0).CreationTime
					.FileSystemModificationTime = ioFiles(0).LastWriteTime

					.AcqTimeStart = .FileSystemModificationTime
					.AcqTimeEnd = .FileSystemModificationTime

					.DatasetID = udtFileInfo.DatasetID
					.DatasetName = ioDatasetFolder.Name
					.FileExtension = ioFiles(0).Extension
					.FileSizeBytes = 0
					.ScanCount = 0
				End With


				' Update the dataset name and file extension
				udtFileInfo.DatasetName = GetDatasetNameViaPath(ioDatasetFolder.FullName)
				udtFileInfo.FileExtension = String.Empty

				udtFileInfo.AcqTimeEnd = System.DateTime.MinValue
				udtFileInfo.AcqTimeStart = System.DateTime.MaxValue
				udtFileInfo.ScanCount = 0

				' Process each zip file
				For Each ioFileInfo As System.IO.FileInfo In ioFiles

					' Examine all of the apexAcquisition.method files in this zip file
					blnSuccess = DetermineAcqStartEndTime(ioFileInfo, udtFileInfo)

				Next

				If udtFileInfo.AcqTimeEnd = System.DateTime.MinValue OrElse udtFileInfo.AcqTimeStart = System.DateTime.MaxValue Then
					' Did not find any apexAcquisition.method files or submethods.xml files
					' Use the file modification date of the first zip file
					udtFileInfo.AcqTimeStart = ioFiles(0).LastWriteTime
					udtFileInfo.AcqTimeEnd = ioFiles(0).LastWriteTime
				End If


				' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
				With mDatasetStatsSummarizer.DatasetFileInfo
					.DatasetName = String.Copy(udtFileInfo.DatasetName)
					.FileExtension = String.Copy(udtFileInfo.FileExtension)
					.AcqTimeStart = udtFileInfo.AcqTimeStart
					.AcqTimeEnd = udtFileInfo.AcqTimeEnd
					.ScanCount = udtFileInfo.ScanCount
					.FileSizeBytes = udtFileInfo.FileSizeBytes
				End With

				blnSuccess = True
			End If
		Catch ex As System.Exception
			ReportError("Exception processing Zipped Imaging Files: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function


End Class
