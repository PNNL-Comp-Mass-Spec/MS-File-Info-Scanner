Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
'
' Last modified January 17, 2013

Imports PNNLOmics.Utilities
Imports System.Text.RegularExpressions

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
			Return Path.GetFileNameWithoutExtension(strDataFilePath)
		Catch ex As Exception
			Return String.Empty
		End Try
	End Function

	Private Function ExtractRunTime(ByVal strText As String, ByRef dblRunTimeMinutes As Double) As Boolean

		Static reExtractTime As Regex = New Regex("([0-9.]+) min", Text.RegularExpressions.RegexOptions.Singleline Or Text.RegularExpressions.RegexOptions.Compiled Or Text.RegularExpressions.RegexOptions.IgnoreCase)

		Dim reMatch As Match

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

		Dim dctRunTimeText As Dictionary(Of String, clsLineMatchSearchInfo)
		Dim dblTotalRuntime As Double = 0
		Dim dblRunTime As Double = 0

		Dim blnRunTimeFound As Boolean
		Dim blnSuccess As Boolean
		Dim blnMatchSuccess As Boolean

		Try
			' Open the acqmeth.txt file
			strFilePath = Path.Combine(strFolderPath, AGILENT_ACQ_METHOD_FILE)
			If Not File.Exists(strFilePath) Then
				Return False
			End If

			' Populate a dictionary object with the text strings for finding lines with runtime information
			' Note that "Post Run" occurs twice in the file, so we use clsLineMatchSearchInfo.Matched to track whether or not the text has been matched
			dctRunTimeText = New Dictionary(Of String, clsLineMatchSearchInfo)
			dctRunTimeText.Add(ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE, New clsLineMatchSearchInfo(True))
			dctRunTimeText.Add(ACQ_METHOD_FILE_RUN_TIME_LINE, New clsLineMatchSearchInfo(True))

            ' We could also add in the "Post Run" time for determining total acquisition time, but we don't do this, to stay consistent with run times reported by the MS file
            ' dctRunTimeText.Add(ACQ_METHOD_FILE_POST_RUN_LINE, New clsLineMatchSearchInfo(False))

            Using srInFile As StreamReader = New StreamReader(strFilePath)

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

        Catch ex As Exception
            ' Exception reading file
            ReportError("Exception reading " & AGILENT_ACQ_METHOD_FILE & ": " & ex.Message)
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
            strFilePath = Path.Combine(strFolderPath, AGILENT_GC_INI_FILE)
            If Not File.Exists(strFilePath) Then
                Return False
            End If

            Using srInFile As StreamReader = New StreamReader(strFilePath)

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

        Catch ex As Exception
            ' Exception reading file
            ReportError("Exception reading " & AGILENT_GC_INI_FILE & ": " & ex.Message)
            blnSuccess = False
        End Try

        If blnSuccess Then
            ' Update the acquisition start time
            udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblTotalRuntime)
        End If

        Return blnSuccess

    End Function

    Protected Function ProcessChemstationMSDataFile(ByVal strDatafilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim blnValidSpectrum As Boolean
        Dim blnSuccess As Boolean
        Dim intCurrentIndex As Integer = 0

        Try
            Using oReader As ChemstationMSFileReader.clsChemstationDataMSFileReader = New ChemstationMSFileReader.clsChemstationDataMSFileReader(strDatafilePath)

                udtFileInfo.AcqTimeStart = oReader.Header.AcqDate
                udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(oReader.Header.RetentionTimeMinutesEnd)

                udtFileInfo.ScanCount = oReader.Header.SpectraCount

                For intSpectrumIndex As Integer = 0 To udtFileInfo.ScanCount - 1
                    intCurrentIndex = intSpectrumIndex

                    Dim oSpectrum As ChemstationMSFileReader.clsSpectralRecord = Nothing
                    Dim lstMZs As List(Of Single) = Nothing
                    Dim lstIntensities As List(Of Int32) = Nothing
                    Const intMSLevel As Integer = 1

                    Try
                        oReader.GetSpectrum(intSpectrumIndex, oSpectrum)
                        lstMZs = oSpectrum.Mzs
                        lstIntensities = oSpectrum.Intensities
                        blnValidSpectrum = True
                    Catch ex As Exception
                        ReportError("Exception obtaining data from the MS file for spectrum index " & intCurrentIndex & ": " & ex.Message)
                        blnValidSpectrum = False
                    End Try


                    If blnValidSpectrum Then

                        Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                        objScanStatsEntry.ScanNumber = intSpectrumIndex + 1
                        objScanStatsEntry.ScanType = intMSLevel
                        objScanStatsEntry.ScanTypeName = "GC-MS"

                        objScanStatsEntry.ScanFilterText = ""
                        objScanStatsEntry.ElutionTime = oSpectrum.RetentionTimeMinutes.ToString("0.0000")
                        objScanStatsEntry.TotalIonIntensity = MathUtilities.ValueToString(oSpectrum.TIC, 1)

                        objScanStatsEntry.BasePeakIntensity = MathUtilities.ValueToString(oSpectrum.BasePeakAbundance, 1)
                        objScanStatsEntry.BasePeakMZ = MathUtilities.ValueToString(oSpectrum.BasePeakMZ, 5)

                        ' Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                        objScanStatsEntry.IonCount = lstMZs.Count
                        objScanStatsEntry.IonCountRaw = objScanStatsEntry.IonCount

                        mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


                        If mSaveTICAndBPI Then
                            mTICandBPIPlot.AddData(objScanStatsEntry.ScanNumber, intMSLevel, oSpectrum.RetentionTimeMinutes, oSpectrum.BasePeakAbundance, oSpectrum.TIC)

                            If lstMZs.Count > 0 Then
                                Dim dblIonsMZ() As Double
                                Dim dblIonsIntensity() As Double
                                ReDim dblIonsMZ(lstMZs.Count - 1)
                                ReDim dblIonsIntensity(lstMZs.Count - 1)

                                For intIndex As Integer = 0 To lstMZs.Count - 1
                                    dblIonsMZ(intIndex) = lstMZs(intIndex)
                                    dblIonsIntensity(intIndex) = lstIntensities(intIndex)
                                Next

                                mLCMS2DPlot.AddScan(objScanStatsEntry.ScanNumber, intMSLevel, oSpectrum.RetentionTimeMinutes, dblIonsMZ.Length, dblIonsMZ, dblIonsIntensity)
                            End If

                        End If

                        If mCheckCentroidingStatus Then
                            Dim lstMzDoubles = New List(Of Double)(lstMZs.Count)
                            lstMzDoubles.AddRange(lstMZs.Select(Function(ion) CType(ion, Double)))
                            mDatasetStatsSummarizer.ClassifySpectrum(lstMzDoubles, intMSLevel)
                        End If

                    End If

                Next

            End Using

            blnSuccess = True

        Catch ex As Exception
            ' Exception reading file
            ReportError("Exception reading data from the MS file at spectrum index " & intCurrentIndex & ": " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Overrides Function ProcessDataFile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim blnSuccess As Boolean
        Dim blnAcqTimeDetermined As Boolean

        Dim ioFolderInfo As DirectoryInfo
        Dim ioFileInfo As FileInfo
        Dim strMSDataFilePath As String

        Try
            blnSuccess = False
            ioFolderInfo = New DirectoryInfo(strDataFilePath)
            strMSDataFilePath = Path.Combine(ioFolderInfo.FullName, AGILENT_MS_DATA_FILE)

            With udtFileInfo
                .FileSystemCreationTime = ioFolderInfo.CreationTime
                .FileSystemModificationTime = ioFolderInfo.LastWriteTime

                ' The acquisition times will get updated below to more accurate values
                .AcqTimeStart = .FileSystemModificationTime
                .AcqTimeEnd = .FileSystemModificationTime

                .DatasetName = GetDatasetNameViaPath(ioFolderInfo.Name)
                .FileExtension = ioFolderInfo.Extension
                .FileSizeBytes = 0

                ' Look for the MS file
                ' Use its modification time to get an initial estimate for the acquisition end time
                ' Assign the .MS file's size to .FileSizeBytes
                ioFileInfo = New FileInfo(strMSDataFilePath)
                If ioFileInfo.Exists Then
                    .FileSizeBytes = ioFileInfo.Length
                    .AcqTimeStart = ioFileInfo.LastWriteTime
                    .AcqTimeEnd = ioFileInfo.LastWriteTime

                    ' Read the file info from the file system
                    UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)

                    blnSuccess = True
                End If

                .ScanCount = 0
            End With

            If blnSuccess Then
                ' Read the detailed data from the MS file
                blnSuccess = ProcessChemstationMSDataFile(strMSDataFilePath, udtFileInfo)

                If blnSuccess Then
                    blnAcqTimeDetermined = True
                End If

            End If

            If Not blnSuccess Then
                ' MS file not found (or problems parsing); use acqmeth.txt and/or GC.ini

                ' The timestamp of the acqmeth.txt file or GC.ini file is more accurate than the GC.ini file, so we'll use that
                ioFileInfo = New FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_ACQ_METHOD_FILE))
                If Not ioFileInfo.Exists Then
                    ioFileInfo = New FileInfo(Path.Combine(ioFolderInfo.FullName, AGILENT_GC_INI_FILE))
                End If


                If ioFileInfo.Exists Then
                    With udtFileInfo

                        ' Update the AcqTimes only if the LastWriteTime of the acqmeth.txt or GC.ini file is within the next 60 minutes of .AcqTimeEnd
                        If Not blnSuccess OrElse ioFileInfo.LastWriteTime.Subtract(.AcqTimeEnd).TotalMinutes < 60 Then
                            .AcqTimeStart = ioFileInfo.LastWriteTime
                            .AcqTimeEnd = ioFileInfo.LastWriteTime
                            blnSuccess = True
                        End If

                        If .FileSizeBytes = 0 Then
                            ' File size was not determined from the MS file
                            ' Instead, sum up the sizes of all of the files in this folder
                            For Each ioFileInfo In ioFolderInfo.GetFiles()
                                .FileSizeBytes += ioFileInfo.Length
                            Next ioFileInfo

                            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = udtFileInfo.FileSizeBytes
                        End If

                    End With
                End If
            End If


            If Not blnAcqTimeDetermined Then
                Try
                    ' Parse the acqmeth.txt file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                    blnSuccess = ParseAcqMethodFile(strDataFilePath, udtFileInfo)

                    If Not blnSuccess Then
                        ' Try to extract Runtime from the GC.ini file
                        blnSuccess = ParseGCIniFile(strDataFilePath, udtFileInfo)
                    End If

                Catch ex As Exception
                    ' Error parsing the acqmeth.txt file or GC.in file; do not abort
                End Try

                ' We set blnSuccess to true, even if either of the above functions fail
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


        Catch ex As Exception
            ReportError("Exception parsing GC .D folder: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

End Class
