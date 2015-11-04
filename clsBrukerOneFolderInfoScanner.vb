Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2005
'
' Last modified September 17, 2005

Imports System.IO
Imports System.Text.RegularExpressions

Public Class clsBrukerOneFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    Public Const BRUKER_ONE_FOLDER_NAME As String = "1"

    Private Const BRUKER_LOCK_FILE As String = "LOCK"
    Private Const BRUKER_ACQU_FILE As String = "acqu"
    Private Const BRUKER_ACQU_FILE_ACQ_LINE_START As String = "##$AQ_DATE= <"
    Private Const BRUKER_ACQU_FILE_ACQ_LINE_END As Char = ">"c

    Private Const TIC_FILE_NUMBER_OF_FILES_LINE_START As String = "Number of files :"

    Private Const TIC_FILE_TIC_FILE_LIST_START As String = "TIC file list:"
    Private Const TIC_FILE_TIC_FILE_LIST_END As String = "TIC end of file list"
    Private Const TIC_FILE_COMMENT_SECTION_END As String = "CommentEnd"

    Private Const PEK_FILE_FILENAME_LINE As String = "Filename:"

    Private Const MINIMUM_ACCEPTABLE_ACQ_START_TIME As DateTime = #1/1/1975#

    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        Dim ioFolderInfo As DirectoryInfo
        Dim strDatasetName As String = String.Empty

        Try
            ' The dataset name for a Bruker 1 folder or zipped S folder is the name of the parent directory
            ioFolderInfo = New DirectoryInfo(strDataFilePath)
            strDatasetName = ioFolderInfo.Parent.Name
        Catch ex As Exception
            ' Ignore errors
        End Try

        If strDatasetName Is Nothing Then strDatasetName = String.Empty
        Return strDatasetName

    End Function

    Public Shared Function IsZippedSFolder(strFilePath As String) As Boolean

        Static reCheckFile As Regex = New Regex("s[0-9]+\.zip", Text.RegularExpressions.RegexOptions.Singleline Or Text.RegularExpressions.RegexOptions.Compiled Or Text.RegularExpressions.RegexOptions.IgnoreCase)

        Return reCheckFile.Match(strFilePath).Success

    End Function

    Private Function ParseBrukerDateFromArray(ByRef strLineIn As String, ByRef dtDate As DateTime) As Boolean
        Dim strDate As String
        Dim blnSuccess As Boolean

        Dim intStartIndex As Integer
        Dim intIndexCheck As Integer
        Dim intIndexCompare As Integer

        Dim strSplitLine() As String

        Try
            strSplitLine = strLineIn.Split(" "c)

            ' Remove any entries from strSplitLine() that are blank
            intIndexCheck = intStartIndex
            Do While intIndexCheck < strSplitLine.Length AndAlso strSplitLine.Length > 0
                If strSplitLine(intIndexCheck).Length = 0 Then
                    For intIndexCompare = intIndexCheck To strSplitLine.Length - 2
                        strSplitLine(intIndexCompare) = strSplitLine(intIndexCompare + 1)
                    Next intIndexCompare
                    ReDim Preserve strSplitLine(strSplitLine.Length - 2)
                Else
                    intIndexCheck += 1
                End If
            Loop

            If strSplitLine.Length >= 5 Then
                intStartIndex = strSplitLine.Length - 5
                strDate = strSplitLine(4 + intStartIndex) & "-" & strSplitLine(1 + intStartIndex) & "-" & strSplitLine(2 + intStartIndex) & " " & strSplitLine(3 + intStartIndex)
                dtDate = DateTime.Parse(strDate)
                blnSuccess = True
            Else
                blnSuccess = False
            End If
        Catch ex As Exception
            ' Date parse failed
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function ParseBrukerAcquFile(strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        Dim strLineIn As String

        Dim blnSuccess As Boolean

        Try
            ' Try to open the acqu file
            blnSuccess = False
            Using srInFile = New StreamReader(Path.Combine(strFolderPath, BRUKER_ACQU_FILE))
                Do While srInFile.Peek() >= 0
                    strLineIn = srInFile.ReadLine()

                    If Not strLineIn Is Nothing Then
                        If strLineIn.StartsWith(BRUKER_ACQU_FILE_ACQ_LINE_START) Then
                            ' Date line found
                            ' It is of the form: ##$AQ_DATE= <Sat Aug 20 07:56:55 2005> 
                            strLineIn = strLineIn.Substring(BRUKER_ACQU_FILE_ACQ_LINE_START.Length).Trim
                            strLineIn = strLineIn.TrimEnd(BRUKER_ACQU_FILE_ACQ_LINE_END)

                            blnSuccess = ParseBrukerDateFromArray(strLineIn, udtFileInfo.AcqTimeEnd)
                            Exit Do
                        End If
                    End If
                Loop
            End Using

        Catch ex As Exception
            ' Error opening the acqu file
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function ParseBrukerLockFile(strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        Dim strLineIn As String
        Dim strSplitLine() As String

        Dim blnSuccess As Boolean

        Try
            ' Try to open the Lock file
            ' The date line is the first (and only) line in the file
            blnSuccess = False
            Using srInFile = New StreamReader(Path.Combine(strFolderPath, BRUKER_LOCK_FILE))
                If srInFile.Peek() >= 0 Then
                    strLineIn = srInFile.ReadLine()
                    If Not strLineIn Is Nothing Then
                        ' Date line found
                        ' It is of the form: wd37119 2208 WD37119\9TOperator Sat Aug 20 06:10:31 2005
                        strSplitLine = strLineIn.Trim.Split(" "c)

                        blnSuccess = ParseBrukerDateFromArray(strLineIn, udtFileInfo.AcqTimeStart)
                    End If
                End If
            End Using

        Catch ex As Exception
            ' Error opening the Lock file
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function ParseBrukerZippedSFolders(ByRef ioFolderInfo As DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Looks through the s*.zip files to determine the total file size (uncompressed) of all files in all the matching .Zip files
        ' Updates udtFileInfo.FileSizeBytes with this info, while also updating udtFileInfo.ScanCount with the total number of files found
        ' Returns True if success and also if no matching Zip files were found; returns False if error

        Dim blnSuccess As Boolean = False

        udtFileInfo.FileSizeBytes = 0
        udtFileInfo.ScanCount = 0

        Try
            For Each ioFileMatch As FileInfo In ioFolderInfo.GetFiles("s*.zip")
                ' Get the info on each zip file

                Using objZipFile As Ionic.Zip.ZipFile = New Ionic.Zip.ZipFile(ioFileMatch.FullName)
                    For Each objZipEntry As Ionic.Zip.ZipEntry In objZipFile.Entries
                        udtFileInfo.FileSizeBytes += objZipEntry.UncompressedSize
                        udtFileInfo.ScanCount += 1
                    Next
                End Using

            Next ioFileMatch
            blnSuccess = True

        Catch ex As Exception
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function ParseICRFolder(ByRef ioFolderInfo As DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Look for and open the .Pek file in ioFolderInfo
        ' Count the number of PEK_FILE_FILENAME_LINE lines

        Dim ioFileMatch As FileInfo
        Dim strLineIn As String

        Dim intFileListCount As Integer
        Dim blnSuccess As Boolean

        For Each ioFileMatch In ioFolderInfo.GetFiles("*.pek")
            Try
                ' Try to open the PEK file
                blnSuccess = False
                intFileListCount = 0
                Using srInFile = New StreamReader(ioFileMatch.OpenRead())
                    Do While srInFile.Peek() >= 0
                        strLineIn = srInFile.ReadLine()

                        If Not strLineIn Is Nothing Then
                            If strLineIn.StartsWith(PEK_FILE_FILENAME_LINE) Then
                                intFileListCount += 1
                            End If
                        End If
                    Loop
                End Using
                blnSuccess = True

            Catch ex As Exception
                ' Error opening or parsing the PEK file
                blnSuccess = False
            End Try

            If intFileListCount > udtFileInfo.ScanCount Then
                udtFileInfo.ScanCount = intFileListCount
            End If

            ' Only parse the first .Pek file found
            Exit For
        Next ioFileMatch

        Return blnSuccess

    End Function

    Private Function ParseTICFolder(ByRef ioFolderInfo As DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, ByRef dtTICModificationDate As DateTime) As Boolean
        ' Look for and open the .Tic file in ioFolderInfo and look for the line listing the number of files
        ' As a second validation, count the number of lines between TIC_FILE_TIC_FILE_LIST_START and TIC_FILE_TIC_FILE_LIST_END

        Dim ioFileMatch As FileInfo
        Dim strLineIn As String

        Dim intFileListCount As Integer
        Dim blnParsingTICFileList As Boolean
        Dim blnSuccess As Boolean

        For Each ioFileMatch In ioFolderInfo.GetFiles("*.tic")
            Try
                ' Try to open the TIC file
                blnSuccess = False
                intFileListCount = 0
                Using srInFile = New StreamReader(ioFileMatch.OpenRead())
                    Do While srInFile.Peek() >= 0
                        strLineIn = srInFile.ReadLine()

                        If Not strLineIn Is Nothing Then
                            If blnParsingTICFileList Then
                                If strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_END) Then
                                    blnParsingTICFileList = False
                                    Exit Do
                                ElseIf strLineIn = TIC_FILE_COMMENT_SECTION_END Then
                                    ' Found the end of the text section; exit the loop
                                    Exit Do
                                Else
                                    intFileListCount += 1
                                End If
                            Else
                                If strLineIn.StartsWith(TIC_FILE_NUMBER_OF_FILES_LINE_START) Then
                                    ' Number of files line found
                                    ' Parse out the file count
                                    udtFileInfo.ScanCount = Integer.Parse(strLineIn.Substring(TIC_FILE_NUMBER_OF_FILES_LINE_START.Length).Trim)
                                ElseIf strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_START) Then
                                    blnParsingTICFileList = True
                                ElseIf strLineIn = TIC_FILE_COMMENT_SECTION_END Then
                                    ' Found the end of the text section; exit the loop
                                    Exit Do
                                End If
                            End If
                        End If
                    Loop
                End Using
                blnSuccess = True

                dtTICModificationDate = ioFileMatch.LastWriteTime

            Catch ex As Exception
                ' Error opening or parsing the TIC file
                blnSuccess = False
            End Try

            If intFileListCount > udtFileInfo.ScanCount Then
                udtFileInfo.ScanCount = intFileListCount
            End If

            ' Only parse the first .Tic file found
            Exit For
        Next ioFileMatch

        Return blnSuccess

    End Function

    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Process a Bruker 1 folder or Bruker s001.zip file, specified by strDataFilePath
        ' If a Bruker 1 folder, then it must contain file acqu and typically contains file LOCK

        Dim ioFileInfo As FileInfo
        Dim ioZippedSFilesFolderInfo As DirectoryInfo = Nothing
        Dim ioSubFolder As DirectoryInfo

        Dim intScanCountSaved As Integer
        Dim dtTICModificationDate As DateTime

        Dim blnParsingBrukerOneFolder As Boolean
        Dim blnSuccess As Boolean

        Try
            ' Determine whether strDataFilePath points to a file or a folder
            ' See if strFileOrFolderPath points to a valid file
            ioFileInfo = New FileInfo(strDataFilePath)

            If ioFileInfo.Exists() Then
                ' Parsing a zipped S folder
                blnParsingBrukerOneFolder = False

                ' The dataset name is equivalent to the name of the folder containing strDataFilePath
                ioZippedSFilesFolderInfo = ioFileInfo.Directory
                blnSuccess = True

                ' Cannot determine accurate acqusition start or end times
                ' We have to assign a date, so we'll assign the date for the zipped s-folder
                With udtFileInfo
                    .AcqTimeStart = ioFileInfo.LastWriteTime
                    .AcqTimeEnd = ioFileInfo.LastWriteTime
                End With

            Else
                ' Assuming it's a "1" folder
                blnParsingBrukerOneFolder = True

                ioZippedSFilesFolderInfo = New DirectoryInfo(strDataFilePath)
                If ioZippedSFilesFolderInfo.Exists Then
                    ' Determine the dataset name by looking up the name of the parent folder of strDataFilePath
                    ioZippedSFilesFolderInfo = ioZippedSFilesFolderInfo.Parent
                    blnSuccess = True
                Else
                    blnSuccess = False
                End If
            End If

            If blnSuccess Then
                With udtFileInfo
                    .FileSystemCreationTime = ioZippedSFilesFolderInfo.CreationTime
                    .FileSystemModificationTime = ioZippedSFilesFolderInfo.LastWriteTime

                    ' The acquisition times will get updated below to more accurate values
                    .AcqTimeStart = .FileSystemModificationTime
                    .AcqTimeEnd = .FileSystemModificationTime

                    .DatasetName = ioZippedSFilesFolderInfo.Name
                    .FileExtension = String.Empty
                    .FileSizeBytes = 0
                    .ScanCount = 0
                End With
            End If
        Catch ex As Exception
            blnSuccess = False
        End Try

        If blnSuccess AndAlso blnParsingBrukerOneFolder Then
            ' Parse the Acqu File to populate .AcqTimeEnd
            blnSuccess = ParseBrukerAcquFile(strDataFilePath, udtFileInfo)

            If blnSuccess Then
                ' Parse the Lock file to populate.AcqTimeStart
                blnSuccess = ParseBrukerLockFile(strDataFilePath, udtFileInfo)

                If Not blnSuccess Then
                    ' Use the end time as the start time
                    udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
                    blnSuccess = True
                End If
            End If
        End If

        If blnSuccess Then
            ' Look for the zipped S folders in ioZippedSFilesFolderInfo
            Try
                blnSuccess = ParseBrukerZippedSFolders(ioZippedSFilesFolderInfo, udtFileInfo)
                intScanCountSaved = udtFileInfo.ScanCount
            Catch ex As Exception
                ' Error parsing zipped S Folders; do not abort
            End Try

            Try
                blnSuccess = False

                ' Look for the TIC* folder to obtain the scan count from a .Tic file
                ' If the Scan Count in the TIC is larger than the scan count from ParseBrukerZippedSFolders,
                '  then we'll use that instead
                For Each ioSubFolder In ioZippedSFilesFolderInfo.GetDirectories("TIC*")
                    blnSuccess = ParseTICFolder(ioSubFolder, udtFileInfo, dtTICModificationDate)

                    If blnSuccess Then
                        ' Successfully parsed a TIC folder; do not parse any others
                        Exit For
                    End If
                Next ioSubFolder

                If Not blnSuccess Then
                    ' TIC folder not found; see if a .TIC file is present in ioZippedSFilesFolderInfo
                    blnSuccess = ParseTICFolder(ioZippedSFilesFolderInfo, udtFileInfo, dtTICModificationDate)
                End If

                If blnSuccess And Not blnParsingBrukerOneFolder AndAlso dtTICModificationDate >= MINIMUM_ACCEPTABLE_ACQ_START_TIME Then
                    ' If dtTICModificationDate is earlier than .AcqTimeStart then update to dtTICMOdificationDate
                    With udtFileInfo
                        If dtTICModificationDate < .AcqTimeStart Then
                            .AcqTimeStart = dtTICModificationDate
                            .AcqTimeEnd = dtTICModificationDate
                        End If
                    End With
                End If

                If Not blnSuccess Then
                    ' .Tic file not found in ioZippedSFilesFolderInfo
                    ' Look for an ICR* folder to obtain the scan count from a .Pek file
                    For Each ioSubFolder In ioZippedSFilesFolderInfo.GetDirectories("ICR*")
                        blnSuccess = ParseICRFolder(ioSubFolder, udtFileInfo)

                        If blnSuccess Then
                            ' Successfully parsed an ICR folder; do not parse any others
                            Exit For
                        End If
                    Next ioSubFolder
                End If

                If blnSuccess = True Then
                    If intScanCountSaved > udtFileInfo.ScanCount Then
                        udtFileInfo.ScanCount = intScanCountSaved
                    End If
                Else
                    ' Set success to true anyway since we do have enough information to save the MS file info
                    blnSuccess = True
                End If

            Catch ex As Exception
                ' Error parsing the TIC* or ICR* folders; do not abort
            End Try

            ' Validate udtFileInfo.AcqTimeStart vs. udtFileInfo.AcqTimeEnd
            With udtFileInfo
                If udtFileInfo.AcqTimeEnd >= MINIMUM_ACCEPTABLE_ACQ_START_TIME Then
                    If .AcqTimeStart > .AcqTimeEnd Then
                        ' Start time cannot be greater than the end time
                        .AcqTimeStart = .AcqTimeEnd
                    ElseIf .AcqTimeStart < MINIMUM_ACCEPTABLE_ACQ_START_TIME Then
                        .AcqTimeStart = .AcqTimeEnd
                    Else
                        ''Dim dtDateCompare As DateTime
                        ''If .ScanCount > 0 Then
                        ''    ' Make sure the start time is greater than the end time minus the scan count times 30 seconds per scan
                        ''    dtDateCompare = .AcqTimeEnd.Subtract(New TimeSpan(0, 0, .ScanCount * 30))
                        ''Else
                        ''    dtDateCompare = .AcqTimeEnd - 
                        ''End If

                        ''If .AcqTimeStart < dtDateCompare Then
                        ''    .AcqTimeStart = .AcqTimeEnd
                        ''End If
                    End If
                End If
            End With
        End If

        Return blnSuccess

    End Function

End Class
