Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2005
'
' Last modified September 17, 2005

Public Class clsAgilentIonTrapDFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const AGILENT_ION_TRAP_D_EXTENSION As String = ".D"

    Private Const AGILENT_YEP_FILE As String = "Analysis.yep"
    Private Const AGILENT_RUN_LOG_FILE As String = "RUN.LOG"
    Private Const AGILENT_ANALYSIS_CDF_FILE As String = "Analysis.cdf"

    Private Const RUN_LOG_FILE_METHOD_LINE_START As String = "Method"
    Private Const RUN_LOG_FILE_INSTRUMENT_RUNNING As String = "Instrument running sample"
    Private Const RUN_LOG_INSTRUMENT_RUN_COMPLETED As String = "Instrument run completed"

    Private Function ExtractMethodLineDate(ByVal strLineIn As String, ByRef dtDate As DateTime) As Boolean

        Dim strSplitLine() As String
        Dim blnSuccess As Boolean

        blnSuccess = False
        Try
            strSplitLine = strLineIn.Trim.Split(" "c)
            If strSplitLine.Length >= 2 Then
                dtDate = Date.Parse(strSplitLine(strSplitLine.Length - 1) & " " & strSplitLine(strSplitLine.Length - 2))
                blnSuccess = True
            End If
        Catch ex As System.Exception
            ' Ignore errors
        End Try

        Return blnSuccess
    End Function

    Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
        ' The dataset name is simply the folder name without .D
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Private Function SecondsToTimeSpan(ByVal dblSeconds As Double) As System.TimeSpan

        Dim dtTimeSpan As System.TimeSpan

        Try
            dtTimeSpan = New System.TimeSpan(0, 0, CInt(dblSeconds))
        Catch ex As System.Exception
            dtTimeSpan = New System.TimeSpan(0, 0, 0)
        End Try

        Return dtTimeSpan

    End Function

	Private Function ParseRunLogFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		Dim strLineIn As String
		Dim strMostRecentMethodLine As String = String.Empty

		Dim intCharLoc As Integer
		Dim dtMethodDate As DateTime

		Dim blnProcessedFirstMethodLine As Boolean
		Dim blnEndDateFound As Boolean
		Dim blnSuccess As Boolean

		Try
			' Try to open the Run.Log file
			Using srInFile As System.IO.StreamReader = New System.IO.StreamReader(System.IO.Path.Combine(strFolderPath, AGILENT_RUN_LOG_FILE))

				blnProcessedFirstMethodLine = False
				blnEndDateFound = False
				Do While srInFile.Peek() >= 0
					strLineIn = srInFile.ReadLine()

					If Not strLineIn Is Nothing Then
						If strLineIn.StartsWith(RUN_LOG_FILE_METHOD_LINE_START) Then
							strMostRecentMethodLine = String.Copy(strLineIn)

							' Method line found
							' See if the line contains a key phrase
							intCharLoc = strLineIn.IndexOf(RUN_LOG_FILE_INSTRUMENT_RUNNING)
							If intCharLoc > 0 Then
								If ExtractMethodLineDate(strLineIn, dtMethodDate) Then
									udtFileInfo.AcqTimeStart = dtMethodDate
								End If
								blnProcessedFirstMethodLine = True
							Else
								intCharLoc = strLineIn.IndexOf(RUN_LOG_INSTRUMENT_RUN_COMPLETED)
								If intCharLoc > 0 Then
									If ExtractMethodLineDate(strLineIn, dtMethodDate) Then
										udtFileInfo.AcqTimeEnd = dtMethodDate
										blnEndDateFound = True
									End If
								End If
							End If

							' If this is the first method line, then parse out the date and store in .AcqTimeStart
							If Not blnProcessedFirstMethodLine Then
								If ExtractMethodLineDate(strLineIn, dtMethodDate) Then
									udtFileInfo.AcqTimeStart = dtMethodDate
								End If
							End If
						End If
					End If
				Loop
			End Using

			If blnProcessedFirstMethodLine And Not blnEndDateFound Then
				' Use the last time in the file as the .AcqTimeEnd value
				If ExtractMethodLineDate(strMostRecentMethodLine, dtMethodDate) Then
					udtFileInfo.AcqTimeEnd = dtMethodDate
				End If
			End If

			blnSuccess = blnProcessedFirstMethodLine

		Catch ex As System.Exception
			' Run.log file not found
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Private Function ParseAnalysisCDFFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		Dim objNETCDFReader As NetCDFReader.clsMSNetCdf = Nothing

		Dim intScanCount As Integer, intScanNumber As Integer
		Dim dblScanTotalIntensity As Double, dblScanTime As Double
		Dim dblMassMin As Double, dblMassMax As Double

		Dim blnSuccess As Boolean

		Try
			objNETCDFReader = New NetCDFReader.clsMSNetCdf
			blnSuccess = objNETCDFReader.OpenMSCdfFile(System.IO.Path.Combine(strFolderPath, AGILENT_ANALYSIS_CDF_FILE))
			If blnSuccess Then
				intScanCount = objNETCDFReader.GetScanCount()

				If intScanCount > 0 Then
					' Lookup the scan time of the final scan
					If objNETCDFReader.GetScanInfo(intScanCount - 1, intScanNumber, dblScanTotalIntensity, dblScanTime, dblMassMin, dblMassMax) Then
						With udtFileInfo
							' Add 1 to intScanNumber since the scan number is off by one in the CDF file
							.ScanCount = intScanNumber + 1
							.AcqTimeEnd = udtFileInfo.AcqTimeStart.Add(SecondsToTimeSpan(dblScanTime))
						End With
					End If
				Else
					udtFileInfo.ScanCount = 0
				End If
			End If
		Catch ex As System.Exception
			blnSuccess = False
		Finally
			If Not objNETCDFReader Is Nothing Then
				objNETCDFReader.CloseMSCdfFile()
			End If
		End Try

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

				' Look for the Analysis.yep file
				' Use its modification time to get an initial estimate for the acquisition time
				' Assign the .Yep file's size to .FileSizeBytes
				ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioFolderInfo.FullName, AGILENT_YEP_FILE))
				If ioFileInfo.Exists Then
					.FileSizeBytes = ioFileInfo.Length
					.AcqTimeStart = ioFileInfo.LastWriteTime
					.AcqTimeEnd = ioFileInfo.LastWriteTime
					blnSuccess = True
				Else
					' Analysis.yep not found; look for Run.log
					ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioFolderInfo.FullName, AGILENT_RUN_LOG_FILE))
					If ioFileInfo.Exists Then
						.AcqTimeStart = ioFileInfo.LastWriteTime
						.AcqTimeEnd = ioFileInfo.LastWriteTime
						blnSuccess = True

						' Sum up the sizes of all of the files in this folder
						.FileSizeBytes = 0
						For Each ioFileInfo In ioFolderInfo.GetFiles()
							.FileSizeBytes += ioFileInfo.Length
						Next ioFileInfo
					End If
				End If

				.ScanCount = 0
			End With

			If blnSuccess Then
				Try
					' Parse the Run Log file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
					blnSuccess = ParseRunLogFile(strDataFilePath, udtFileInfo)

					' Parse the Analysis.cdf file to determine the scan count and to further refine .AcqTimeStart
					blnSuccess = ParseAnalysisCDFFile(strDataFilePath, udtFileInfo)
				Catch ex As System.Exception
					' Error parsing the Run Log file or the Analysis.cdf file; do not abort

				End Try

				blnSuccess = True
			End If

		Catch ex As System.Exception
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

End Class
