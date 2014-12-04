Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2007
'
' Last modified April 18, 2014

Imports System.IO

Public MustInherit Class clsMSFileInfoProcessorBaseClass
	Implements iMSFileInfoProcessor

	Public Sub New()
		InitializeLocalVariables()
	End Sub

#Region "Constants"
	Public Const DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR As Integer = 10
#End Region

#Region "Member variables"
	Protected mSaveTICAndBPI As Boolean
	Protected mSaveLCMS2DPlots As Boolean
	Protected mCheckCentroidingStatus As Boolean

	Protected mComputeOverallQualityScores As Boolean
	Protected mCreateDatasetInfoFile As Boolean					' When True, then creates an XML file with dataset info
	Protected mCreateScanStatsFile As Boolean					' When True, then creates a _ScanStats.txt file
	Protected mLCMS2DOverviewPlotDivisor As Integer

	Protected mUpdateDatasetStatsTextFile As Boolean			' When True, then adds a new row to a tab-delimited text file that has dataset stats
	Protected mDatasetStatsTextFileName As String

	Protected mScanStart As Integer
	Protected mScanEnd As Integer
	Protected mShowDebugInfo As Boolean

	Protected mDatasetID As Integer

	Protected mCopyFileLocalOnReadError As Boolean

	Protected WithEvents mTICandBPIPlot As clsTICandBPIPlotter
	Protected WithEvents mInstrumentSpecificPlots As clsTICandBPIPlotter

	Protected WithEvents mLCMS2DPlot As clsLCMSDataPlotter
	Protected WithEvents mLCMS2DPlotOverview As clsLCMSDataPlotter

	Protected WithEvents mDatasetStatsSummarizer As DSSummarizer.clsDatasetStatsSummarizer

	Public Event ErrorEvent(ByVal Message As String) Implements iMSFileInfoProcessor.ErrorEvent
	Public Event MessageEvent(ByVal Message As String) Implements iMSFileInfoProcessor.MessageEvent
	'Public Event ProgressUpdate(ByVal Progress As Single)
#End Region

#Region "Properties"

	''' <summary>
	''' This property allows the parent class to define the DatasetID value
	''' </summary>
	Public Property DatasetID As Integer Implements iMSFileInfoProcessor.DatasetID
		Get
			Return mDatasetID
		End Get
		Set(value As Integer)
			mDatasetID = value
		End Set
	End Property

	Public Property DatasetStatsTextFileName() As String Implements iMSFileInfoProcessor.DatasetStatsTextFileName
		Get
			Return mDatasetStatsTextFileName
		End Get
		Set(ByVal value As String)
			If String.IsNullOrEmpty(value) Then
				' Do not update mDatasetStatsTextFileName
			Else
				mDatasetStatsTextFileName = value
			End If
		End Set
	End Property

	Public Property LCMS2DPlotOptions() As clsLCMSDataPlotter.clsOptions Implements iMSFileInfoProcessor.LCMS2DPlotOptions
		Get
			Return mLCMS2DPlot.Options
		End Get
		Set(ByVal value As clsLCMSDataPlotter.clsOptions)
			mLCMS2DPlot.Options = value
			mLCMS2DPlotOverview.Options = value.Clone()
		End Set
	End Property

	Public Property LCMS2DOverviewPlotDivisor() As Integer Implements iMSFileInfoProcessor.LCMS2DOverviewPlotDivisor
		Get
			Return mLCMS2DOverviewPlotDivisor
		End Get
		Set(ByVal value As Integer)
			mLCMS2DOverviewPlotDivisor = value
		End Set
	End Property

	Public Property ScanStart() As Integer Implements iMSFileInfoProcessor.ScanStart
		Get
			Return mScanStart
		End Get
		Set(ByVal value As Integer)
			mScanStart = value
		End Set
	End Property

	Public Property ShowDebugInfo As Boolean Implements iMSFileInfoProcessor.ShowDebugInfo
		Get
			Return mShowDebugInfo
		End Get
		Set(value As Boolean)
			mShowDebugInfo = value
		End Set
	End Property

	''' <summary>
	''' When ScanEnd is > 0, then will stop processing at the specified scan number
	''' </summary>
	Public Property ScanEnd() As Integer Implements iMSFileInfoProcessor.ScanEnd
		Get
			Return mScanEnd
		End Get
		Set(ByVal value As Integer)
			mScanEnd = value
		End Set
	End Property

#End Region

	Public Function GetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions) As Boolean Implements iMSFileInfoProcessor.GetOption
		Select Case eOption
			Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
				Return mSaveTICAndBPI
			Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
				Return mComputeOverallQualityScores
			Case iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile
				Return mCreateDatasetInfoFile
			Case iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots
				Return mSaveLCMS2DPlots
			Case iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError
				Return mCopyFileLocalOnReadError
			Case iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile
				Return mUpdateDatasetStatsTextFile
			Case iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile
				Return mCreateScanStatsFile
			Case iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus
				Return mCheckCentroidingStatus
		End Select

		Throw New Exception("Unrecognized option, " & eOption.ToString)
	End Function

	Public Sub SetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions, ByVal blnValue As Boolean) Implements iMSFileInfoProcessor.SetOption
		Select Case eOption
			Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
				mSaveTICAndBPI = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
				mComputeOverallQualityScores = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile
				mCreateDatasetInfoFile = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.CreateLCMS2DPlots
				mSaveLCMS2DPlots = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.CopyFileLocalOnReadError
				mCopyFileLocalOnReadError = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.UpdateDatasetStatsTextFile
				mUpdateDatasetStatsTextFile = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.CreateScanStatsFile
				mCreateScanStatsFile = blnValue
			Case iMSFileInfoProcessor.ProcessingOptions.CheckCentroidingStatus
				mCheckCentroidingStatus = blnValue
			Case Else
				Throw New Exception("Unrecognized option, " & eOption.ToString)
		End Select

	End Sub

	Protected Function CreateDatasetInfoFile(ByVal strInputFileName As String, ByVal strOutputFolderPath As String) As Boolean

		Dim blnSuccess As Boolean

		Try
			Dim strDatasetName = GetDatasetNameViaPath(strInputFileName)
			Dim strDatasetInfoFilePath = Path.Combine(strOutputFolderPath, strDatasetName)
			strDatasetInfoFilePath &= DSSummarizer.clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX

			If mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = 0 AndAlso mDatasetID > 0 Then
				mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID
			End If

			blnSuccess = mDatasetStatsSummarizer.CreateDatasetInfoFile(strDatasetName, strDatasetInfoFilePath)

			If Not blnSuccess Then
				ReportError("Error calling objDatasetStatsSummarizer.CreateDatasetInfoFile: " & mDatasetStatsSummarizer.ErrorMessage)
			End If

		Catch ex As Exception
			ReportError("Error creating dataset info file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Function CreateDatasetScanStatsFile(ByVal strInputFileName As String, ByVal strOutputFolderPath As String) As Boolean

		Dim blnSuccess As Boolean

		Dim strDatasetName As String
		Dim strScanStatsFilePath As String

		strScanStatsFilePath = String.Empty

		Try
			strDatasetName = GetDatasetNameViaPath(strInputFileName)
			strScanStatsFilePath = Path.Combine(strOutputFolderPath, strDatasetName)
			strScanStatsFilePath &= "_ScanStats.txt"

			If mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = 0 AndAlso mDatasetID > 0 Then
				mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID
			End If

			blnSuccess = mDatasetStatsSummarizer.CreateScanStatsFile(strDatasetName, strScanStatsFilePath)

			If Not blnSuccess Then
				ReportError("Error calling objDatasetStatsSummarizer.CreateScanStatsFile: " & mDatasetStatsSummarizer.ErrorMessage)
			End If

		Catch ex As Exception
			ReportError("Error creating dataset ScanStats file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Function UpdateDatasetStatsTextFile(ByVal strInputFileName As String, _
	 ByVal strOutputFolderPath As String) As Boolean

		Return UpdateDatasetStatsTextFile(strInputFileName, strOutputFolderPath, DSSummarizer.clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME)

	End Function

	Public Function UpdateDatasetStatsTextFile(ByVal strInputFileName As String, _
	 ByVal strOutputFolderPath As String, _
	 ByVal strDatasetStatsFilename As String) As Boolean

		Dim blnSuccess As Boolean

		Dim strDatasetName As String
		Dim strDatasetStatsFilePath As String

		strDatasetStatsFilePath = String.Empty

		Try
			strDatasetName = GetDatasetNameViaPath(strInputFileName)

			strDatasetStatsFilePath = Path.Combine(strOutputFolderPath, strDatasetStatsFilename)

			blnSuccess = mDatasetStatsSummarizer.UpdateDatasetStatsTextFile(strDatasetName, strDatasetStatsFilePath)

			If Not blnSuccess Then
				ReportError("Error calling objDatasetStatsSummarizer.UpdateDatasetStatsTextFile: " & mDatasetStatsSummarizer.ErrorMessage)
			End If

		Catch ex As Exception
			ReportError("Error updating the dataset stats text file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Function GetDatasetInfoXML() As String Implements iMSFileInfoProcessor.GetDatasetInfoXML

		Try
			If mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = 0 AndAlso mDatasetID > 0 Then
				mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = mDatasetID
			End If

			Return mDatasetStatsSummarizer.CreateDatasetInfoXML()

		Catch ex As Exception
			ReportError("Error getting dataset info XML: " & ex.Message)
		End Try

		Return String.Empty

	End Function

	''' <summary>
	''' Returns the range of scan numbers to process
	''' </summary>
	''' <param name="intScanCount">Number of scans in the file</param>
	''' <param name="intScanStart">1 if mScanStart is zero; otherwise mScanStart</param>
	''' <param name="intScanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, intScanCount)</param>
	''' <remarks></remarks>
	Protected Sub GetStartAndEndScans(ByVal intScanCount As Integer, _
	 ByRef intScanStart As Integer, ByRef intScanEnd As Integer)
		GetStartAndEndScans(intScanCount, 1, intScanStart, intScanEnd)
	End Sub

	''' <summary>
	''' Returns the range of scan numbers to process
	''' </summary>
	''' <param name="intScanCount">Number of scans in the file</param>
	''' <param name="intScanNumFirst">The first scan number in the file</param>
	''' <param name="intScanStart">1 if mScanStart is zero; otherwise mScanStart</param>
	''' <param name="intScanEnd">intScanCount if mScanEnd is zero; otherwise Min(mScanEnd, intScanCount)</param>
	''' <remarks></remarks>
	Protected Sub GetStartAndEndScans(ByVal intScanCount As Integer, ByVal intScanNumFirst As Integer, _
	 ByRef intScanStart As Integer, ByRef intScanEnd As Integer)

		If mScanStart > 0 Then
			intScanStart = mScanStart
		Else
			intScanStart = 1
		End If

		If mScanEnd > 0 AndAlso mScanEnd < intScanCount Then
			intScanEnd = mScanEnd
		Else
			intScanEnd = intScanCount
		End If

	End Sub

	Protected Sub InitializeLocalVariables()

		mTICandBPIPlot = New clsTICandBPIPlotter()
		mInstrumentSpecificPlots = New clsTICandBPIPlotter()

		mLCMS2DPlot = New clsLCMSDataPlotter()
		mLCMS2DPlotOverview = New clsLCMSDataPlotter

		mLCMS2DOverviewPlotDivisor = DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR

		mSaveTICAndBPI = False
		mSaveLCMS2DPlots = False
		mCheckCentroidingStatus = False

		mComputeOverallQualityScores = False

		mCreateDatasetInfoFile = False
		mCreateScanStatsFile = False

		mUpdateDatasetStatsTextFile = False
		mDatasetStatsTextFileName = DSSummarizer.clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME

		mScanStart = 0
		mScanEnd = 0
		mShowDebugInfo = False

		mDatasetID = 0

		mDatasetStatsSummarizer = New DSSummarizer.clsDatasetStatsSummarizer

		mCopyFileLocalOnReadError = False

	End Sub

	Protected Sub InitializeTICAndBPI()
		' Initialize TIC, BPI, and m/z vs. time arrays
		mTICandBPIPlot.Reset()
		mInstrumentSpecificPlots.Reset()
	End Sub

	Protected Sub InitializeLCMS2DPlot()
		' Initialize object that tracks m/z vs. time
		mLCMS2DPlot.Reset()
		mLCMS2DPlotOverview.Reset()
	End Sub

	Protected Sub ReportError(ByVal strMessage As String)
		RaiseEvent ErrorEvent(strMessage)
	End Sub

	Protected Sub ShowMessage(ByVal strMessage As String)
		RaiseEvent MessageEvent(strMessage)
	End Sub

	Protected Function UpdateDatasetFileStats(ByRef fiFileInfo As FileInfo, ByVal intDatasetID As Integer) As Boolean

		Try
			If Not fiFileInfo.Exists Then Return False

			' Record the file size and Dataset ID
			With mDatasetStatsSummarizer.DatasetFileInfo
				.FileSystemCreationTime = fiFileInfo.CreationTime
				.FileSystemModificationTime = fiFileInfo.LastWriteTime

				.AcqTimeStart = .FileSystemModificationTime
				.AcqTimeEnd = .FileSystemModificationTime

				.DatasetID = intDatasetID
				.DatasetName = Path.GetFileNameWithoutExtension(fiFileInfo.Name)
				.FileExtension = fiFileInfo.Extension
				.FileSizeBytes = fiFileInfo.Length

				.ScanCount = 0
			End With

		Catch ex As Exception
			Return False
		End Try

		Return True

	End Function

	Protected Function UpdateDatasetFileStats(ByRef diFolderInfo As DirectoryInfo, ByVal intDatasetID As Integer) As Boolean

		Try
			If Not diFolderInfo.Exists Then Return False

			' Record the file size and Dataset ID
			With mDatasetStatsSummarizer.DatasetFileInfo
				.FileSystemCreationTime = diFolderInfo.CreationTime
				.FileSystemModificationTime = diFolderInfo.LastWriteTime

				.AcqTimeStart = .FileSystemModificationTime
				.AcqTimeEnd = .FileSystemModificationTime

				.DatasetID = intDatasetID
				.DatasetName = Path.GetFileNameWithoutExtension(diFolderInfo.Name)
				.FileExtension = diFolderInfo.Extension

				For Each fiFileInfo In diFolderInfo.GetFiles("*", SearchOption.AllDirectories)
					.FileSizeBytes += fiFileInfo.Length
				Next

				.ScanCount = 0
			End With

		Catch ex As Exception
			Return False
		End Try

		Return True

	End Function

	Protected Function CreateOverview2DPlots(
	 ByVal strDatasetName As String,
	 ByVal strOutputFolderPath As String,
	 ByVal intLCMS2DOverviewPlotDivisor As Integer) As Boolean

		Return CreateOverview2DPlots(strDatasetName, strOutputFolderPath, intLCMS2DOverviewPlotDivisor, String.Empty)

	End Function

	Protected Function CreateOverview2DPlots(
	  ByVal strDatasetName As String,
	  ByVal strOutputFolderPath As String,
	  ByVal intLCMS2DOverviewPlotDivisor As Integer,
	  ByVal strScanModeSuffixAddon As String) As Boolean

		Dim objScan As clsLCMSDataPlotter.clsScanData

		Dim blnSuccess As Boolean
		Dim intIndex As Integer

		If intLCMS2DOverviewPlotDivisor <= 1 Then
			' Nothing to do; just return True
			Return True
		End If

		mLCMS2DPlotOverview.Reset()

		mLCMS2DPlotOverview.Options = mLCMS2DPlot.Options.Clone()

		' Set MaxPointsToPlot in mLCMS2DPlotOverview to be intLCMS2DOverviewPlotDivisor times smaller 
		' than the MaxPointsToPlot value in mLCMS2DPlot
		mLCMS2DPlotOverview.Options.MaxPointsToPlot = CInt(Math.Round(mLCMS2DPlot.Options.MaxPointsToPlot / intLCMS2DOverviewPlotDivisor, 0))

		' Copy the data from mLCMS2DPlot to mLCMS2DPlotOverview
		' mLCMS2DPlotOverview will auto-filter the data to track, at most, mLCMS2DPlotOverview.Options.MaxPointsToPlot points
		For intIndex = 0 To mLCMS2DPlot.ScanCountCached - 1
			objScan = mLCMS2DPlot.GetCachedScanByIndex(intIndex)

			mLCMS2DPlotOverview.AddScanSkipFilters(objScan)
		Next

		' Write out the Overview 2D plot of m/z vs. intensity
		' Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
		blnSuccess = mLCMS2DPlotOverview.Save2DPlots(strDatasetName, strOutputFolderPath, "HighAbu_", strScanModeSuffixAddon)

		Return blnSuccess

	End Function

	Protected Function CreateOutputFiles(ByVal strInputFileName As String, _
	 ByVal strOutputFolderPath As String) As Boolean Implements iMSFileInfoProcessor.CreateOutputFiles

		Dim blnSuccess As Boolean
		Dim blnSuccessOverall As Boolean

		Dim strErrorMessage As String
		Dim strDatasetName As String

		Dim blnCreateQCPlotHtmlFile As Boolean

		Dim diFolderInfo As DirectoryInfo

		Try

			strDatasetName = Me.GetDatasetNameViaPath(strInputFileName)
			blnSuccessOverall = True
			blnCreateQCPlotHtmlFile = False

			If strOutputFolderPath Is Nothing Then strOutputFolderPath = String.Empty

			If strOutputFolderPath.Length > 0 Then
				' Make sure the output folder exists
				diFolderInfo = New DirectoryInfo(strOutputFolderPath)

				If Not diFolderInfo.Exists Then
					diFolderInfo.Create()
				End If
			Else
				diFolderInfo = New DirectoryInfo(".")
			End If

			If mSaveTICAndBPI Then
				' Write out the TIC and BPI plots
				strErrorMessage = String.Empty
				blnSuccess = mTICandBPIPlot.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName, strErrorMessage)
				If Not blnSuccess Then
					ReportError("Error calling mTICandBPIPlot.SaveTICAndBPIPlotFiles: " & strErrorMessage)
					blnSuccessOverall = False
				End If

				' Write out any instrument-specific plots
				blnSuccess = mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles(strDatasetName, diFolderInfo.FullName, strErrorMessage)
				If Not blnSuccess Then
					ReportError("Error calling mInstrumentSpecificPlots.SaveTICAndBPIPlotFiles: " & strErrorMessage)
					blnSuccessOverall = False
				End If

				blnCreateQCPlotHtmlFile = True
			End If

			If mSaveLCMS2DPlots Then
				' Write out the 2D plot of m/z vs. intensity
				' Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
				blnSuccess = mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName)
				If Not blnSuccess Then
					blnSuccessOverall = False
				Else
					If mLCMS2DOverviewPlotDivisor > 0 Then
						' Also save the Overview 2D Plots
						' Plots will be named Dataset_HighAbu_LCMS.png and Dataset_HighAbu_LCMSn.png
						blnSuccess = CreateOverview2DPlots(strDatasetName, strOutputFolderPath, mLCMS2DOverviewPlotDivisor)
						If Not blnSuccess Then
							blnSuccessOverall = False
						End If
					Else
						mLCMS2DPlotOverview.ClearRecentFileInfo()
					End If

					If blnSuccessOverall AndAlso mLCMS2DPlot.Options.PlottingDeisotopedData Then
						' Create two more plots 2D plots, but this with a smaller maximum m/z
						mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotter.clsOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT
						mLCMS2DPlotOverview.Options.MaxMonoMassForDeisotopedPlot = clsLCMSDataPlotter.clsOptions.DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT

						mLCMS2DPlot.Save2DPlots(strDatasetName, diFolderInfo.FullName, "", "_zoom")
						If mLCMS2DOverviewPlotDivisor > 0 Then
							CreateOverview2DPlots(strDatasetName, strOutputFolderPath, mLCMS2DOverviewPlotDivisor, "_zoom")
						End If
					End If
				End If
				blnCreateQCPlotHtmlFile = True
			End If

            If mCreateDatasetInfoFile Then
                ' Create the _DatasetInfo.xml file
                blnSuccess = Me.CreateDatasetInfoFile(strInputFileName, diFolderInfo.FullName)
                If Not blnSuccess Then
                    blnSuccessOverall = False
                End If
                blnCreateQCPlotHtmlFile = True
            End If

			If mCreateScanStatsFile Then
				' Create the _ScanStats.txt file
				blnSuccess = Me.CreateDatasetScanStatsFile(strInputFileName, diFolderInfo.FullName)
				If Not blnSuccess Then
					blnSuccessOverall = False
				End If
			End If

			If mUpdateDatasetStatsTextFile Then
				' Add a new row to the MSFileInfo_DatasetStats.txt file
				blnSuccess = Me.UpdateDatasetStatsTextFile(strInputFileName, diFolderInfo.FullName, mDatasetStatsTextFileName)
				If Not blnSuccess Then
					blnSuccessOverall = False
				End If
			End If

			If blnCreateQCPlotHtmlFile Then
				blnSuccess = CreateQCPlotHTMLFile(strDatasetName, diFolderInfo.FullName)
				If Not blnSuccess Then
					blnSuccessOverall = False
				End If
			End If

		Catch ex As Exception
			ReportError("Error creating output files: " & ex.Message)
			blnSuccessOverall = False
		End Try

		Return blnSuccessOverall

	End Function

	Protected Function CreateQCPlotHTMLFile(ByVal strDatasetName As String, _
	 ByVal strOutputFolderPath As String) As Boolean

		Dim swOutFile As StreamWriter

		Dim strHTMLFilePath As String
		Dim strFile1 As String
		Dim strFile2 As String
		Dim strFile3 As String

		Dim strTop As String

		Dim strDSInfoFileName As String

		Dim blnSuccess As Boolean

		Dim objSummaryStats As DSSummarizer.clsDatasetSummaryStats

		Try

			blnSuccess = False

			' Obtain the dataset summary stats (they will be auto-computed if not up to date)
			objSummaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats

			strHTMLFilePath = Path.Combine(strOutputFolderPath, "index.html")

			swOutFile = New StreamWriter(New FileStream(strHTMLFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

			swOutFile.WriteLine("<!DOCTYPE html PUBLIC ""-//W3C//DTD HTML 3.2//EN"">")
			swOutFile.WriteLine("<html>")
			swOutFile.WriteLine("<head>")
			swOutFile.WriteLine("  <title>" & strDatasetName & "</title>")
			swOutFile.WriteLine("</head>")
			swOutFile.WriteLine("")
			swOutFile.WriteLine("<body>")
			swOutFile.WriteLine("  <h2>" & strDatasetName & "</h2>")
			swOutFile.WriteLine("")
			swOutFile.WriteLine("  <table>")

			' First the plots with the top 50,000 points
			strFile1 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS)

			If mLCMS2DPlotOverview.Options.PlottingDeisotopedData Then
				strFile2 = strFile1.Replace("_zoom.png", ".png")
			Else
				strFile2 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn)
			End If

			strTop = IntToEngineeringNotation(mLCMS2DPlotOverview.Options.MaxPointsToPlot)

			If strFile1.Length > 0 OrElse strFile2.Length > 0 Then
				swOutFile.WriteLine("    <tr>")
				swOutFile.WriteLine("      <td valign=""middle"">LCMS<br>(Top " & strTop & ")</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile1, 250) & "</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile2, 250) & "</td>")
				swOutFile.WriteLine("    </tr>")
				swOutFile.WriteLine("")
			End If

			' Now the plots with the top 500,000 points
			strFile1 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS)

			If mLCMS2DPlotOverview.Options.PlottingDeisotopedData Then
				strFile2 = strFile1.Replace("_zoom.png", ".png")
			Else
				strFile2 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn)
			End If

			strTop = IntToEngineeringNotation(mLCMS2DPlot.Options.MaxPointsToPlot)

			If strFile1.Length > 0 OrElse strFile2.Length > 0 Then
				swOutFile.WriteLine("    <tr>")
				swOutFile.WriteLine("      <td valign=""middle"">LCMS<br>(Top " & strTop & ")</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile1, 250) & "</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile2, 250) & "</td>")
				swOutFile.WriteLine("    </tr>")
				swOutFile.WriteLine("")
			End If

			strFile1 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS)
			strFile2 = mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn)
			If strFile1.Length > 0 OrElse strFile2.Length > 0 Then
				swOutFile.WriteLine("    <tr>")
				swOutFile.WriteLine("      <td valign=""middle"">BPI</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile1, 250) & "</td>")
				swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile2, 250) & "</td>")
				swOutFile.WriteLine("    </tr>")
				swOutFile.WriteLine("")
			End If

			strFile1 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC)
			strFile2 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMS)
			strFile3 = mInstrumentSpecificPlots.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.BPIMSn)

			If strFile1.Length > 0 OrElse strFile2.Length > 0 OrElse strFile3.Length > 0 Then
				swOutFile.WriteLine("    <tr>")
				swOutFile.WriteLine("      <td valign=""middle"">Addnl Plots</td>")
				If strFile1.Length > 0 Then swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile1, 250) & "</td>") Else swOutFile.WriteLine("      <td></td>")
				If strFile2.Length > 0 Then swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile2, 250) & "</td>") Else swOutFile.WriteLine("      <td></td>")
				If strFile3.Length > 0 Then swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile3, 250) & "</td>") Else swOutFile.WriteLine("      <td></td>")
				swOutFile.WriteLine("    </tr>")
				swOutFile.WriteLine("")
			End If

			swOutFile.WriteLine("    <tr>")
			swOutFile.WriteLine("      <td valign=""middle"">TIC</td>")
			swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(mTICandBPIPlot.GetRecentFileInfo(clsTICandBPIPlotter.eOutputFileTypes.TIC), 250) & "</td>")
			swOutFile.WriteLine("      <td valign=""middle"">")

			GenerateQCScanTypeSummaryHTML(swOutFile, objSummaryStats, "        ")

			swOutFile.WriteLine("      </td>")
			swOutFile.WriteLine("    </tr>")

			swOutFile.WriteLine("    <tr>")
			swOutFile.WriteLine("      <td>&nbsp;</td>")
			swOutFile.WriteLine("      <td align=""center"">DMS <a href=""http://dms2.pnl.gov/dataset/show/" & strDatasetName & """>Dataset Detail Report</a></td>")

			strDSInfoFileName = strDatasetName & DSSummarizer.clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX
			If mCreateDatasetInfoFile OrElse File.Exists(Path.Combine(strOutputFolderPath, strDSInfoFileName)) Then
				swOutFile.WriteLine("      <td align=""center""><a href=""" & strDSInfoFileName & """>Dataset Info XML file</a></td>")
			Else
				swOutFile.WriteLine("      <td>&nbsp;</td>")
			End If

			swOutFile.WriteLine("    </tr>")

			swOutFile.WriteLine("")
			swOutFile.WriteLine("  </table>")
			swOutFile.WriteLine("")
			swOutFile.WriteLine("</body>")
			swOutFile.WriteLine("</html>")
			swOutFile.WriteLine("")

			swOutFile.Close()

			blnSuccess = True
		Catch ex As Exception
			ReportError("Error creating QC plot HTML file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Private Function GenerateQCFigureHTML(ByVal strFilename As String, ByVal intWidthPixels As Integer) As String

		If strFilename Is Nothing OrElse strFilename.Length = 0 Then
			Return "&nbsp;"
		Else
			Return "<a href=""" & strFilename & """>" & _
			 "<img src=""" & strFilename & """ width=""" & intWidthPixels.ToString & """ border=""0""></a>"
		End If

	End Function

	Private Sub GenerateQCScanTypeSummaryHTML(ByRef swOutFile As StreamWriter, _
	   ByRef objDatasetSummaryStats As DSSummarizer.clsDatasetSummaryStats, _
	   ByVal strIndent As String)

		Dim objEnum As Dictionary(Of String, Integer).Enumerator
		Dim strScanType As String
		Dim intIndexMatch As Integer

		Dim strScanFilterText As String
		Dim intScanCount As Integer

		If strIndent Is Nothing Then strIndent = String.Empty

		swOutFile.WriteLine(strIndent & "<table border=""1"">")
		swOutFile.WriteLine(strIndent & "  <tr><th>Scan Type</th><th>Scan Count</th><th>Scan Filter Text</th></tr>")

		objEnum = objDatasetSummaryStats.objScanTypeStats.GetEnumerator
		Do While objEnum.MoveNext

			strScanType = objEnum.Current.Key
			intIndexMatch = strScanType.IndexOf(DSSummarizer.clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR)

			If intIndexMatch >= 0 Then
				strScanFilterText = strScanType.Substring(intIndexMatch + DSSummarizer.clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR.Length)
				If intIndexMatch > 0 Then
					strScanType = strScanType.Substring(0, intIndexMatch)
				Else
					strScanType = String.Empty
				End If
			Else
				strScanFilterText = String.Empty
			End If
			intScanCount = objEnum.Current.Value


			swOutFile.WriteLine(strIndent & "  <tr><td>" & strScanType & "</td>" & _
			 "<td align=""center"">" & intScanCount & "</td>" & _
			 "<td>" & strScanFilterText & "</td></tr>")

		Loop

		swOutFile.WriteLine(strIndent & "</table>")

	End Sub

	''' <summary>
	''' Converts an integer to engineering notation
	''' For example, 50000 will be returned as 50K
	''' </summary>
	''' <param name="intValue"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Protected Function IntToEngineeringNotation(ByVal intValue As Integer) As String

		If intValue < 1000 Then
			Return intValue.ToString
		ElseIf intValue < 1000000.0 Then
			Return CInt(Math.Round(intValue / 1000, 0)).ToString & "K"
		Else
			Return CInt(Math.Round(intValue / 1000 / 1000, 0)).ToString & "M"
		End If

	End Function

	Public MustOverride Function ProcessDataFile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean Implements iMSFileInfoProcessor.ProcessDataFile
	Public MustOverride Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String Implements iMSFileInfoProcessor.GetDatasetNameViaPath

	Private Sub mLCMS2DPlot_ErrorEvent(ByVal Message As String) Handles mLCMS2DPlot.ErrorEvent
		ReportError("Error in LCMS2DPlot: " & Message)
	End Sub

	Private Sub mLCMS2DPlotOverview_ErrorEvent(ByVal Message As String) Handles mLCMS2DPlotOverview.ErrorEvent
		ReportError("Error in LCMS2DPlotOverview: " & Message)
	End Sub

	Private Sub mDatasetStatsSummarizer_ErrorEvent(errorMessage As String) Handles mDatasetStatsSummarizer.ErrorEvent
		ReportError(errorMessage)
	End Sub
End Class
