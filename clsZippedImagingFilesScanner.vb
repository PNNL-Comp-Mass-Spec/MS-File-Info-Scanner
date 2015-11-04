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
	''' <param name="fiZipFile"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns>True if at least one valid file is found; otherwise false</returns>
	''' <remarks></remarks>
    Protected Function DetermineAcqStartEndTime(
       fiZipFile As FileInfo,
      ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim blnSuccess As Boolean = False

        Dim lstFileNamesToFind As List(Of String)
        Dim strNameParts() As String

        Try
            ' Bump up the file size
            udtFileInfo.FileSizeBytes += fiZipFile.Length

            lstFileNamesToFind = New List(Of String)
            lstFileNamesToFind.Add("analysis.baf")
            lstFileNamesToFind.Add("apexAcquisition.method")
            lstFileNamesToFind.Add("submethods.xml")

            Dim oZipFile As Ionic.Zip.ZipFile
            Dim oZipEntry As IEnumerator(Of Ionic.Zip.ZipEntry)
            oZipFile = New Ionic.Zip.ZipFile(fiZipFile.FullName)

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

        Catch ex As Exception
            ReportError("Error finding XMass method folder: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Protected Function GetDatasetFolder(strDataFilePath As String) As DirectoryInfo

        ' First see if strFileOrFolderPath points to a valid file
        Dim fiFileInfo = New FileInfo(strDataFilePath)

        If fiFileInfo.Exists() Then
            ' User specified a file; assume the parent folder of this file is the dataset folder
            Return fiFileInfo.Directory
        Else
            ' Assume this is the path to the dataset folder
            Return New DirectoryInfo(strDataFilePath)
        End If

    End Function

    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        Dim strDatasetName As String = String.Empty

        Try
            ' The dataset name for a dataset with zipped imaging files is the name of the parent directory
            ' However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
            Dim diDatasetFolder = GetDatasetFolder(strDataFilePath)
            strDatasetName = diDatasetFolder.Name

            If strDatasetName.ToLower().EndsWith(".d") Then
                strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2)
            End If

        Catch ex As Exception
            ' Ignore errors
        End Try

        If strDatasetName Is Nothing Then strDatasetName = String.Empty
        Return strDatasetName

    End Function

    Public Shared Function IsZippedImagingFile(strFileName As String) As Boolean

        Dim fiFileInfo = New FileInfo(strFileName)

        If String.IsNullOrWhiteSpace(strFileName) Then
            Return False
        End If

        If fiFileInfo.Name.ToLower().StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX.ToLower()) AndAlso fiFileInfo.Extension.ToLower() = ".zip" Then
            Return True
        Else
            Return False
        End If

    End Function

    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the Zip files in the dataset folder)

        Dim blnSuccess As Boolean

        Try
            ' Determine whether strDataFilePath points to a file or a folder

            Dim diDatasetFolder = GetDatasetFolder(strDataFilePath)

            ' Validate that we have selected a valid folder
            If Not diDatasetFolder.Exists Then
                MyBase.ReportError("File/folder not found: " & strDataFilePath)
                Return False
            End If

            ' In case we cannot find any .Zip files, update the .AcqTime values to the folder creation date
            With udtFileInfo
                .AcqTimeStart = diDatasetFolder.CreationTime
                .AcqTimeEnd = diDatasetFolder.CreationTime
            End With

            ' Look for the 0_R*.zip files
            ' If we cannot find any zip files, return false

            Dim lstFiles = diDatasetFolder.GetFiles(ZIPPED_IMAGING_FILE_SEARCH_SPEC).ToList()
            If lstFiles Is Nothing OrElse lstFiles.Count = 0 Then
                ' 0_R*.zip files not found
                MyBase.ReportError(ZIPPED_IMAGING_FILE_SEARCH_SPEC & "files not found in " & diDatasetFolder.FullName)
                blnSuccess = False
            Else

                Dim fiFirstImagingFile = lstFiles.First

                ' Initialize the .DatasetFileInfo
                With mDatasetStatsSummarizer.DatasetFileInfo
                    .FileSystemCreationTime = fiFirstImagingFile.CreationTime
                    .FileSystemModificationTime = fiFirstImagingFile.LastWriteTime

                    .AcqTimeStart = .FileSystemModificationTime
                    .AcqTimeEnd = .FileSystemModificationTime

                    .DatasetID = udtFileInfo.DatasetID
                    .DatasetName = diDatasetFolder.Name
                    .FileExtension = fiFirstImagingFile.Extension
                    .FileSizeBytes = 0
                    .ScanCount = 0
                End With


                ' Update the dataset name and file extension
                udtFileInfo.DatasetName = GetDatasetNameViaPath(diDatasetFolder.FullName)
                udtFileInfo.FileExtension = String.Empty

                udtFileInfo.AcqTimeEnd = DateTime.MinValue
                udtFileInfo.AcqTimeStart = DateTime.MaxValue
                udtFileInfo.ScanCount = 0

                ' Process each zip file
                For Each fiFileInfo As FileInfo In lstFiles

                    ' Examine all of the apexAcquisition.method files in this zip file
                    blnSuccess = DetermineAcqStartEndTime(fiFileInfo, udtFileInfo)

                Next

                If udtFileInfo.AcqTimeEnd = DateTime.MinValue OrElse udtFileInfo.AcqTimeStart = DateTime.MaxValue Then
                    ' Did not find any apexAcquisition.method files or submethods.xml files
                    ' Use the file modification date of the first zip file
                    udtFileInfo.AcqTimeStart = fiFirstImagingFile.LastWriteTime
                    udtFileInfo.AcqTimeEnd = fiFirstImagingFile.LastWriteTime
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
        Catch ex As Exception
            ReportError("Exception processing Zipped Imaging Files: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function


End Class
