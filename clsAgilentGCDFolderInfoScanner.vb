Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified March 21, 2012

Public Class clsAgilentGCDFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const AGILENT_DATA_FOLDER_D_EXTENSION As String = ".D"

    Public Const AGILENT_MS_DATA_FILE As String = "DATA.MS"
    Public Const AGILENT_ACQ_METHOD_FILE As String = "acqmeth.txt"
    Public Const AGILENT_GC_INI_FILE As String = "GC.ini"

    Private Const ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE As String = "Equilibration Time"
    Private Const ACQ_METHOD_FILE_RUN_TIME_LINE As String = "Run Time"
    Private Const ACQ_METHOD_FILE_POST_RUN_LINE As String = "(Post Run)"

    Private Class clsLineMatchSearchInfo
        Public MatchLineStart As Boolean
        Public Matched As Boolean

        Public Sub New(bMatchLineStart As Boolean)
            MatchLineStart = bMatchLineStart
            Matched = False
        End Sub
    End Class


    Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
        ' The dataset name is simply the folder name without .D
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Private Function ExtractRunTime(ByVal strText As String, ByRef dblRunTimeMinutes As Double) As Boolean

        Static reExtractTime As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex("([0-9.]+) min", Text.RegularExpressions.RegexOptions.Singleline Or Text.RegularExpressions.RegexOptions.Compiled Or Text.RegularExpressions.RegexOptions.IgnoreCase)

        Dim reMatch As System.Text.RegularExpressions.Match

        reMatch = reExtractTime.Match(strText)

        If Not reMatch Is Nothing AndAlso reMatch.Success Then
            If Double.TryParse(reMatch.Groups(1).Value, dblRunTimeMinutes) Then
                Return True
            End If
        End If

        Return False

    End Function

    Private Function ParseAcqMethodFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        Dim strFilePath As String = String.Empty
        Dim strLineIn As String

        Dim dctRunTimeText As System.Collections.Generic.Dictionary(Of String, clsLineMatchSearchInfo)
        Dim dblTotalRuntime As Double = 0
        Dim dblRunTime As Double = 0

        Dim blnRunTimeFound As Boolean
        Dim blnSuccess As Boolean
        Dim blnMatchSuccess As Boolean

        Try
            ' Open the acqmeth.txt file
            strFilePath = System.IO.Path.Combine(strFolderPath, AGILENT_ACQ_METHOD_FILE)
            If Not System.IO.File.Exists(strFilePath) Then
                Return False
            End If

			' Populate a dictionary object with the text strings for finding lines with runtime information
			' Since "Post Run" occurs twice in the file, we use a clsLineMatchSearchInfo.Matched to track whether or not the text has been matched
            dctRunTimeText = New System.Collections.Generic.Dictionary(Of String, clsLineMatchSearchInfo)
            dctRunTimeText.Add(ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE, New clsLineMatchSearchInfo(True))
            dctRunTimeText.Add(ACQ_METHOD_FILE_RUN_TIME_LINE, New clsLineMatchSearchInfo(True))
            dctRunTimeText.Add(ACQ_METHOD_FILE_POST_RUN_LINE, New clsLineMatchSearchInfo(False))

            Using srInFile As System.IO.StreamReader = New System.IO.StreamReader(strFilePath)

                Do While srInFile.Peek() >= 0
                    strLineIn = srInFile.ReadLine()

                    If Not String.IsNullOrWhiteSpace(strLineIn) Then
                        For Each strKey As String In dctRunTimeText.Keys
                            If Not dctRunTimeText.Item(strKey).Matched Then
                                If dctRunTimeText.Item(strKey).MatchLineStart Then
                                    blnMatchSuccess = strLineIn.StartsWith(strKey)
                                Else
                                    blnMatchSuccess = strLineIn.Contains(strKey)
                                End If

                                If blnMatchSuccess Then
                                    If ExtractRunTime(strLineIn, dblRunTime) Then
                                        dctRunTimeText.Item(strKey).Matched = True
                                        dblTotalRuntime += dblRunTime
										blnRunTimeFound = True
										Exit For
                                    End If
                                End If
                            End If
                        Next

                    End If

                Loop
            End Using

            blnSuccess = blnRunTimeFound

        Catch ex As System.Exception
            ' Exception reading file
            blnSuccess = False
        End Try

        If blnSuccess Then
            ' Update the acquisition start time
            udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblTotalRuntime)
        End If

        Return blnSuccess

    End Function

    Private Function ParseGCIniFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        Dim strFilePath As String = String.Empty
        Dim strLineIn As String
        Dim strSplitLine() As String

        Dim dblTotalRuntime As Double = 0

        Dim blnSuccess As Boolean

        Try
            ' Open the GC.ini file
            strFilePath = System.IO.Path.Combine(strFolderPath, AGILENT_GC_INI_FILE)
            If Not System.IO.File.Exists(strFilePath) Then
                Return False
            End If

            Using srInFile As System.IO.StreamReader = New System.IO.StreamReader(strFilePath)

                Do While srInFile.Peek() >= 0
                    strLineIn = srInFile.ReadLine()

                    If Not String.IsNullOrWhiteSpace(strLineIn) Then
                        If strLineIn.StartsWith("gc.runlength") Then
                            ' Runtime is the value after the equals sign
                            strSplitLine = strLineIn.Split("="c)
                            If strSplitLine.Length > 1 Then
                                If Double.TryParse(strSplitLine(1), dblTotalRuntime) Then
                                    blnSuccess = True
                                End If
                            End If
                        End If

                    End If

                Loop
            End Using

        Catch ex As System.Exception
            ' Exception reading file
            blnSuccess = False
        End Try

        If blnSuccess Then
            ' Update the acquisition start time
            udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblTotalRuntime)
        End If

        Return blnSuccess

    End Function

    Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim blnSuccess As Boolean
        Dim ioFolderInfo As System.IO.DirectoryInfo
        Dim ioFileInfo As System.IO.FileInfo

        Try
            blnSuccess = False
            ioFolderInfo = New System.IO.DirectoryInfo(strDataFilePath)

            With udtFileInfo
                .FileSystemCreationTime = ioFolderInfo.CreationTime
                .FileSystemModificationTime = ioFolderInfo.LastWriteTime

                ' The acquisition times will get updated below to more accurate values
                .AcqTimeStart = .FileSystemModificationTime
                .AcqTimeEnd = .FileSystemModificationTime

                .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFolderInfo.Name)
                .FileExtension = ioFolderInfo.Extension
				.FileSizeBytes = 0

                ' Look for the DATA.MS file
                ' Use its modification time to get an initial estimate for the acquisition end time
                ' Assign the .MS file's size to .FileSizeBytes
                ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioFolderInfo.FullName, AGILENT_MS_DATA_FILE))
                If ioFileInfo.Exists Then
					.FileSizeBytes = ioFileInfo.Length
					.AcqTimeStart = ioFileInfo.LastWriteTime
					.AcqTimeEnd = ioFileInfo.LastWriteTime

					' Read the file info from the file system
					UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)

                    blnSuccess = True
				End If

				' The timestamp of the acqmeth.txt file or GC.ini file is more accurate than the GC.ini file, so we'll use that
				ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioFolderInfo.FullName, AGILENT_ACQ_METHOD_FILE))
				If Not ioFileInfo.Exists Then
					ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioFolderInfo.FullName, AGILENT_GC_INI_FILE))
				End If

				If ioFileInfo.Exists Then
					' Update the AcqTimes only if the LastWriteTime of the acqmeth.txt or GC.ini file is within the next 60 minutes of .AcqTimeEnd
					If Not blnSuccess OrElse ioFileInfo.LastWriteTime.Subtract(.AcqTimeEnd).TotalMinutes < 60 Then
						.AcqTimeStart = ioFileInfo.LastWriteTime
						.AcqTimeEnd = ioFileInfo.LastWriteTime
						blnSuccess = True
					End If

					If .FileSizeBytes = 0 Then
						' File size was not determined from the DATA.MS file
						' Instead, sum up the sizes of all of the files in this folder
						For Each ioFileInfo In ioFolderInfo.GetFiles()
							.FileSizeBytes += ioFileInfo.Length
						Next ioFileInfo

						mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = udtFileInfo.FileSizeBytes
					End If

				End If

				.ScanCount = 0
			End With

            If blnSuccess Then
                Try
                    ' Parse the acqmeth.txt file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                    blnSuccess = ParseAcqMethodFile(strDataFilePath, udtFileInfo)

                    If Not blnSuccess Then
                        ' Try to extract Runtime from the GC.ini file
                        blnSuccess = ParseGCIniFile(strDataFilePath, udtFileInfo)
                    End If

                Catch ex As System.Exception
                    ' Error parsing the acqmeth.txt file or GC.in file; do not abort

                End Try

                blnSuccess = True
            End If

			If blnSuccess Then

				' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
				With mDatasetStatsSummarizer.DatasetFileInfo
					.DatasetName = String.Copy(udtFileInfo.DatasetName)
					.FileExtension = String.Copy(udtFileInfo.FileExtension)

					.AcqTimeStart = udtFileInfo.AcqTimeStart
					.AcqTimeEnd = udtFileInfo.AcqTimeEnd
					.ScanCount = udtFileInfo.ScanCount
				End With
			End If
		

        Catch ex As System.Exception
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

End Class
