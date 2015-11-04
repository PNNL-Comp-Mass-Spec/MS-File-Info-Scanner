Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2013
'
' Last modified December 2, 2013

Imports ThermoRawFileReaderDLL.FinniganFileIO
Imports PNNLOmics.Utilities
Imports System.IO

Public Class clsDeconToolsIsosInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	''' <summary>
	''' Constructor
	''' </summary>
	''' <remarks></remarks>
	Public Sub New()
		MaxFit = DEFAUT_MAX_FIT
	End Sub

	' Note: The extension must be in all caps
	Public Const DECONTOOLS_CSV_FILE_EXTENSION As String = ".CSV"
	Public Const DECONTOOLS_ISOS_FILE_SUFFIX As String = "_ISOS.CSV"
	Public Const DECONTOOLS_SCANS_FILE_SUFFIX As String = "_SCANS.CSV"

	Public Const DEFAUT_MAX_FIT As Double = 0.15

	Protected Structure udtIsosDataType
		Public Scan As Integer
		Public Charge As Byte
		Public Abundance As Double
		Public MZ As Double
		Public Fit As Single
		Public MonoMass As Double
	End Structure

	Protected Structure udtScansDataType
		Public Scan As Integer
		Public ElutionTime As Single
		Public MSLevel As Integer
		Public BasePeakIntensity As Double		' BPI
		Public BasePeakMZ As Double
		Public TotalIonCurrent As Double		' TIC
		Public NumPeaks As Integer
		Public NumDeisotoped As Integer
		Public FilterText As String
	End Structure

	Public Property MaxFit As Single

	''' <summary>
	''' Returns the dataset name for the given file
	''' </summary>
	''' <param name="strDataFilePath"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String

		Try
			If strDataFilePath.ToUpper().EndsWith(DECONTOOLS_ISOS_FILE_SUFFIX) Then
				' The dataset name is simply the file name without _isos.csv
				Dim datasetName = Path.GetFileName(strDataFilePath)
				Return datasetName.Substring(0, datasetName.Length - DECONTOOLS_ISOS_FILE_SUFFIX.Length)
			Else
				Return String.Empty
			End If
		Catch ex As Exception
			Return String.Empty
		End Try
	End Function

    Private Function GetScanOrFrameColIndex(lstData As List(Of String), strFileDescription As String) As Integer
        Dim intColIndexScanOrFrameNum As Integer

        intColIndexScanOrFrameNum = lstData.IndexOf("frame_num")
        If intColIndexScanOrFrameNum < 0 Then
            intColIndexScanOrFrameNum = lstData.IndexOf("scan_num")
        End If

        If intColIndexScanOrFrameNum < 0 Then
            Throw New InvalidDataException("Required column not found in the " & strFileDescription & " file; must have scan_num or frame_num")
        End If

        Return intColIndexScanOrFrameNum

    End Function

    Private Sub LoadData(fiIsosFile As FileInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType)

        ' Cache the data in the _isos.csv and _scans.csv files

        If mSaveTICAndBPI Then
            ' Initialize the TIC and BPI arrays
            MyBase.InitializeTICAndBPI()
        End If

        If mSaveLCMS2DPlots Then
            MyBase.InitializeLCMS2DPlot()
        End If

        Dim lstIsosData = LoadIsosFile(fiIsosFile.FullName, Me.MaxFit)

        If lstIsosData.Count = 0 Then
            ReportError("No data found in the _isos.csv file: " & fiIsosFile.FullName)
            Return
        End If

        Dim strScansFilePath = GetDatasetNameViaPath(fiIsosFile.Name) & DECONTOOLS_SCANS_FILE_SUFFIX
        strScansFilePath = Path.Combine(fiIsosFile.Directory.FullName, strScansFilePath)

        Dim lstScanData = LoadScansFile(strScansFilePath)

        If lstScanData.Count > 0 Then
            udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(lstScanData.Last.ElutionTime)
            udtFileInfo.ScanCount = lstScanData.Last.Scan
        Else
            udtFileInfo.ScanCount = (From item In lstIsosData Select item.Scan).Max

            For intScanIndex = 1 To udtFileInfo.ScanCount
                Dim udtScanData = New udtScansDataType
                udtScanData.Scan = intScanIndex
                udtScanData.ElutionTime = intScanIndex
                udtScanData.MSLevel = 1

                lstScanData.Add(udtScanData)
            Next
        End If

        ' Step through the isos data and call mLCMS2DPlot.AddScan() for each scan

        Dim lstIons As New List(Of clsLCMSDataPlotter.udtMSIonType)
        Dim intCurrentScan = 0

        ' Note: we only need to update mLCMS2DPlot
        ' The options for mLCMS2DPlotOverview will be cloned from mLCMS2DPlot.Options
        mLCMS2DPlot.Options.PlottingDeisotopedData = True

        Dim dblMaxMonoMass = mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot

        For intIndex = 0 To lstIsosData.Count - 1

            If lstIsosData(intIndex).Scan > intCurrentScan OrElse intIndex = lstIsosData.Count - 1 Then
                ' Store the cached values
                If lstIons.Count > 0 Then

                    Dim udtCurrentScan = (From item In lstScanData Where item.Scan = intCurrentScan).ToList().FirstOrDefault

                    lstIons.Sort(New clsLCMSDataPlotter.udtMSIonTypeComparer)
                    mLCMS2DPlot.AddScan(intCurrentScan, udtCurrentScan.MSLevel, CSng(udtCurrentScan.ElutionTime), lstIons)

                End If

                intCurrentScan = lstIsosData(intIndex).Scan
                lstIons.Clear()
            End If

            If lstIsosData(intIndex).MonoMass <= dblMaxMonoMass Then
                Dim udtIon = New clsLCMSDataPlotter.udtMSIonType
                udtIon.MZ = lstIsosData(intIndex).MonoMass          ' Note that we store .MonoMass in a field called .mz; we'll still be plotting monoisotopic mass
                udtIon.Intensity = lstIsosData(intIndex).Abundance
                udtIon.Charge = lstIsosData(intIndex).Charge

                lstIons.Add(udtIon)
            End If
        Next

    End Sub

    Private Function LoadIsosFile(strIsosFilePath As String, sngMaxFit As Single) As List(Of udtIsosDataType)

        Dim dctColumnInfo As New Dictionary(Of String, Integer)
        dctColumnInfo.Add("charge", -1)
        dctColumnInfo.Add("abundance", -1)
        dctColumnInfo.Add("mz", -1)
        dctColumnInfo.Add("fit", -1)
        dctColumnInfo.Add("monoisotopic_mw", -1)

        Dim intColIndexScanOrFrameNum As Integer = -1

        Dim lstIsosData = New List(Of udtIsosDataType)
        Dim intRowNumber As Integer = 0

        Dim intLastScan As Integer = 0
        Dim intLastScanParseErrors = 0

        Console.WriteLine("  Reading the _isos.csv file")

        Using srIsosFile = New StreamReader(New FileStream(strIsosFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            While srIsosFile.Peek > -1
                intRowNumber += 1
                Dim strLineIn = srIsosFile.ReadLine()
                Dim lstData = strLineIn.Split(","c).ToList()

                If intRowNumber = 1 Then
                    ' Parse the header row
                    ParseColumnHeaders(dctColumnInfo, lstData, "_isos.csv")

                    intColIndexScanOrFrameNum = GetScanOrFrameColIndex(lstData, "_isos.csv")

                    Continue While
                End If

                Dim blnParseError = False
                Dim intCurrentScan As Integer = 0

                Try
                    Dim udtIsosData = New udtIsosDataType
                    With udtIsosData
                        Integer.TryParse(lstData(intColIndexScanOrFrameNum), .Scan)
                        intCurrentScan = .Scan

                        Byte.TryParse(lstData(dctColumnInfo("charge")), .Charge)
                        Double.TryParse(lstData(dctColumnInfo("abundance")), .Abundance)
                        Double.TryParse(lstData(dctColumnInfo("mz")), .MZ)
                        Single.TryParse(lstData(dctColumnInfo("fit")), .Fit)
                        Double.TryParse(lstData(dctColumnInfo("monoisotopic_mw")), .MonoMass)
                    End With

                    If udtIsosData.Charge > 1 Then
                        'Console.WriteLine("Found it")
                    End If

                    If udtIsosData.Fit <= sngMaxFit Then
                        lstIsosData.Add(udtIsosData)
                    End If

                Catch ex As Exception
                    blnParseError = True
                End Try

                If intCurrentScan > intLastScan Then

                    If intLastScanParseErrors > 0 Then
                        ShowMessage("Warning: Skipped " & intLastScanParseErrors & " data points in scan " & intLastScan & " due to data conversion errors")
                    End If

                    intLastScan = intCurrentScan
                    intLastScanParseErrors = 0
                End If

                If blnParseError Then
                    intLastScanParseErrors += 1
                End If

            End While
        End Using

        Return lstIsosData

    End Function

    Private Function LoadScansFile(strScansFilePath As String) As List(Of udtScansDataType)

        Const FILTERED_SCANS_SUFFIX As String = "_filtered_scans.csv"

        Dim dctColumnInfo As New Dictionary(Of String, Integer)
        dctColumnInfo.Add("type", -1)
        dctColumnInfo.Add("bpi", -1)
        dctColumnInfo.Add("bpi_mz", -1)
        dctColumnInfo.Add("tic", -1)
        dctColumnInfo.Add("num_peaks", -1)
        dctColumnInfo.Add("num_deisotoped", -1)

        Dim intColIndexScanOrFrameNum As Integer = -1
        Dim intColIndexScanOrFrameTime As Integer = -1

        Dim lstScanData = New List(Of udtScansDataType)
        Dim intRowNumber As Integer = 0
        Dim intColIndexScanInfo As Integer = -1

        If Not File.Exists(strScansFilePath) AndAlso strScansFilePath.ToLower().EndsWith(FILTERED_SCANS_SUFFIX) Then
            strScansFilePath = strScansFilePath.Substring(0, strScansFilePath.Length - FILTERED_SCANS_SUFFIX.Length) & "_scans.csv"
        End If

        If Not File.Exists(strScansFilePath) Then
            ShowMessage("Warning: _scans.csv file is missing; will plot vs. scan number instead of version elution time")
            Return lstScanData
        End If

        Console.WriteLine("  Reading the _scans.csv file")

        Using srIsosFile = New StreamReader(New FileStream(strScansFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            While srIsosFile.Peek > -1
                intRowNumber += 1
                Dim strLineIn = srIsosFile.ReadLine()
                Dim lstData = strLineIn.Split(","c).ToList()

                If intRowNumber = 1 Then
                    ' Parse the header row
                    ParseColumnHeaders(dctColumnInfo, lstData, "_scans.csv")

                    intColIndexScanOrFrameNum = GetScanOrFrameColIndex(lstData, "_scans.csv")

                    intColIndexScanOrFrameTime = lstData.IndexOf("frame_time")
                    If intColIndexScanOrFrameTime < 0 Then
                        intColIndexScanOrFrameTime = lstData.IndexOf("scan_time")
                    End If

                    ' The info column will have data of the form "FTMS + p NSI Full ms [400.00-2000.00]" for Thermo datasets
                    ' For .mzXML files, this fill will simply have an integer (and thus isn't useful)
                    ' It may not be present in older _scancs.csv files and is thus optional
                    intColIndexScanInfo = lstData.IndexOf("info")

                    Continue While
                End If

                Try
                    Dim udtScanData = New udtScansDataType
                    With udtScanData
                        Integer.TryParse(lstData(intColIndexScanOrFrameNum), .Scan)
                        Single.TryParse(lstData(intColIndexScanOrFrameTime), .ElutionTime)
                        Integer.TryParse(lstData(dctColumnInfo("type")), .MSLevel)
                        Double.TryParse(lstData(dctColumnInfo("bpi")), .BasePeakIntensity)
                        Double.TryParse(lstData(dctColumnInfo("bpi_mz")), .BasePeakMZ)
                        Double.TryParse(lstData(dctColumnInfo("tic")), .TotalIonCurrent)
                        Integer.TryParse(lstData(dctColumnInfo("num_peaks")), .NumPeaks)
                        Integer.TryParse(lstData(dctColumnInfo("num_deisotoped")), .NumDeisotoped)
                        If intColIndexScanInfo > 0 Then
                            Dim infoText = lstData(intColIndexScanInfo)
                            Dim infoValue As Integer

                            ' Only store infoText in .FilterText if infoText is not simply an integer
                            If Not Integer.TryParse(infoText, infoValue) Then
                                .FilterText = infoText
                            End If

                        End If

                    End With
                    lstScanData.Add(udtScanData)

                    If mSaveTICAndBPI Then
                        With udtScanData
                            mTICandBPIPlot.AddData(.Scan, .MSLevel, .ElutionTime, .BasePeakIntensity, .TotalIonCurrent)
                        End With
                    End If

                    Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                    With udtScanData
                        objScanStatsEntry.ScanNumber = .Scan
                        objScanStatsEntry.ScanType = .MSLevel

                        objScanStatsEntry.ScanTypeName = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(.FilterText)
                        objScanStatsEntry.ScanFilterText = XRawFileIO.MakeGenericFinniganScanFilter(.FilterText)

                        objScanStatsEntry.ElutionTime = .ElutionTime.ToString("0.0000")
                        objScanStatsEntry.TotalIonIntensity = MathUtilities.ValueToString(.TotalIonCurrent, 5)
                        objScanStatsEntry.BasePeakIntensity = MathUtilities.ValueToString(.BasePeakIntensity, 5)
                        objScanStatsEntry.BasePeakMZ = Math.Round(.BasePeakMZ, 4).ToString

                        ' Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                        objScanStatsEntry.IonCount = .NumDeisotoped
                        objScanStatsEntry.IonCountRaw = .NumPeaks

                        ' Store the collision mode and the scan filter text
                        objScanStatsEntry.ExtendedScanInfo.CollisionMode = String.Empty
                        objScanStatsEntry.ExtendedScanInfo.ScanFilterText = .FilterText

                    End With
                    mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)

                Catch ex As Exception
                    ShowMessage("Warning: Ignoring scan " & lstData(dctColumnInfo("scan_num")) & " since data conversion error: " & ex.Message)
                End Try

            End While
        End Using

        Return lstScanData

    End Function

    Private Sub ParseColumnHeaders(dctColumnInfo As Dictionary(Of String, Integer), lstData As List(Of String), strFileDescription As String)

        For Each columnName In dctColumnInfo.Keys.ToList()
            Dim intcolIndex = lstData.IndexOf(columnName)
            If intcolIndex >= 0 Then
                dctColumnInfo(columnName) = intcolIndex
            Else
                Throw New InvalidDataException("Required column not found in the " & strFileDescription & " file: " & columnName)
            End If
        Next
    End Sub

    ''' <summary>
    ''' Process the DeconTools results
    ''' </summary>
    ''' <param name="strDataFilePath">Isos file path</param>
    ''' <param name="udtFileInfo"></param>
    ''' <returns>True if success, False if an error</returns>
    ''' <remarks>Will also read the _scans.csv file if present (to determine ElutionTime and MSLevel</remarks>
    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim fiIsosFile = New FileInfo(strDataFilePath)

        If Not fiIsosFile.Exists Then
            ShowMessage("_isos.csv file not found: " + strDataFilePath)
            Return False
        End If

        Dim intDatasetID As Integer = MyBase.DatasetID

        ' Record the file size and Dataset ID
        With udtFileInfo
            .FileSystemCreationTime = fiIsosFile.CreationTime
            .FileSystemModificationTime = fiIsosFile.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = intDatasetID
            .DatasetName = GetDatasetNameViaPath(fiIsosFile.Name)
            .FileExtension = fiIsosFile.Extension
            .FileSizeBytes = fiIsosFile.Length

            .ScanCount = 0
        End With


        mDatasetStatsSummarizer.ClearCachedData()

        If mSaveTICAndBPI OrElse mCreateDatasetInfoFile OrElse mCreateScanStatsFile OrElse mSaveLCMS2DPlots Then
            ' Load data from each scan
            ' This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
            LoadData(fiIsosFile, udtFileInfo)
        End If

        ' Read the file info from the file system
        ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
        UpdateDatasetFileStats(fiIsosFile, intDatasetID)

        ' Copy over the updated filetime info from udtFileInfo to mDatasetFileInfo
        With mDatasetStatsSummarizer.DatasetFileInfo
            .FileSystemCreationTime = udtFileInfo.FileSystemCreationTime
            .FileSystemModificationTime = udtFileInfo.FileSystemModificationTime
            .DatasetID = udtFileInfo.DatasetID
            .DatasetName = String.Copy(udtFileInfo.DatasetName)
            .FileExtension = String.Copy(udtFileInfo.FileExtension)
            .AcqTimeStart = udtFileInfo.AcqTimeStart
            .AcqTimeEnd = udtFileInfo.AcqTimeEnd
            .ScanCount = udtFileInfo.ScanCount
            .FileSizeBytes = udtFileInfo.FileSizeBytes
        End With

        Return True

    End Function

End Class
