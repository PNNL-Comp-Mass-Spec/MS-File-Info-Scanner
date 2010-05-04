Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2007, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified December 19, 2007

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

    Protected mComputeOverallQualityScores As Boolean
    Protected mCreateDatasetInfoFile As Boolean
    Protected mLCMS2DOverviewPlotDivisor As Integer

    Protected WithEvents mTICandBPIPlot As clsTICandBPIPlotter
    Protected WithEvents mLCMS2DPlot As clsLCMSDataPlotter
    Protected WithEvents mLCMS2DPlotOverview As clsLCMSDataPlotter

    Protected mDatasetStatsSummarizer As DSSummarizer.clsDatasetStatsSummarizer

    Public Event ErrorEvent(ByVal Message As String) Implements iMSFileInfoProcessor.ErrorEvent
#End Region

#Region "Properties"
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
        End Select
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
        End Select

    End Sub

    Protected Function CreateDatasetInfoFile(ByVal strInputFileName As String, _
                                             ByVal strOutputFolderPath As String) As Boolean

        Dim blnSuccess As Boolean

        Dim strDatasetName As String
        Dim strDatasetInfoFilePath As String

        strDatasetInfoFilePath = String.Empty

        Try
            strDatasetName = GetDatasetNameViaPath(strInputFileName)
            strDatasetInfoFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName)
            strDatasetInfoFilePath &= DSSummarizer.clsDatasetStatsSummarizer.DATASET_INFO_FILE_SUFFIX

            blnSuccess = mDatasetStatsSummarizer.CreateDatasetInfoFile(strDatasetName, strDatasetInfoFilePath)

            If Not blnSuccess Then
                ReportError("Error calling objDatasetStatsSummarizer.CreateDatasetInfoFile: " & mDatasetStatsSummarizer.ErrorMessage)
            End If

        Catch ex As System.Exception
            ReportError("Error creating dataset info file: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function GetDatasetInfoXML() As String Implements iMSFileInfoProcessor.GetDatasetInfoXML

        Try

            Return mDatasetStatsSummarizer.CreateDatasetInfoXML()

        Catch ex As System.Exception
            ReportError("Error getting dataset info XML: " & ex.Message)
        End Try

        Return String.Empty

    End Function

    Protected Sub InitializeLocalVariables()

        mTICandBPIPlot = New clsTICandBPIPlotter()

        mLCMS2DPlot = New clsLCMSDataPlotter()
        mLCMS2DPlotOverview = New clsLCMSDataPlotter

        mLCMS2DOverviewPlotDivisor = DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR

        mDatasetStatsSummarizer = New DSSummarizer.clsDatasetStatsSummarizer

    End Sub

    Protected Sub InitializeTICAndBPI()
        ' Initialize TIC, BPI, and m/z vs. time arrays
        mTICandBPIPlot.Reset()
    End Sub

    Protected Sub InitializeLCMS2DPlot()
        ' Initialize object that tracks m/z vs. time
        mLCMS2DPlot.Reset()
        mLCMS2DPlotOverview.Reset()
    End Sub

    Protected Sub ReportError(ByVal strMessage As String)
        RaiseEvent ErrorEvent(strMessage)
    End Sub

    Protected Function UpdateDatasetFileStats(ByRef ioFileInfo As System.IO.FileInfo, _
                                              ByVal intDatasetID As Integer) As Boolean

        Try
            If Not ioFileInfo.Exists Then Return False

            ' Record the file size and Dataset ID
            With mDatasetStatsSummarizer.DatasetFileInfo
                .FileSystemCreationTime = ioFileInfo.CreationTime
                .FileSystemModificationTime = ioFileInfo.LastWriteTime

                .AcqTimeStart = .FileSystemModificationTime
                .AcqTimeEnd = .FileSystemModificationTime

                .DatasetID = intDatasetID
                .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
                .FileExtension = ioFileInfo.Extension
                .FileSizeBytes = ioFileInfo.Length

                .ScanCount = 0
            End With

        Catch ex As System.Exception
            Return False
        End Try

        Return True

    End Function

    Protected Function CreateOverview2DPlots(ByVal strDatasetName As String, _
                                             ByVal strOutputFolderPath As String, _
                                             ByVal intLCMS2DOverviewPlotDivisor As Integer) As Boolean

        Dim objScan As clsLCMSDataPlotter.clsScanData

        Dim blnSuccess As Boolean
        Dim intIndex As Integer

        If intLCMS2DOverviewPlotDivisor <= 1 Then
            ' Nothing to do; just return True
            Return True
        End If

        mLCMS2DPlotOverview.Reset()

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
        blnSuccess = mLCMS2DPlotOverview.Save2DPlots(strDatasetName, strOutputFolderPath, "HighAbu_")

        Return blnSuccess

    End Function

    Protected Function CreateOutputFiles(ByVal strInputFileName As String, _
                                         ByVal strOutputFolderPath As String) As Boolean Implements iMSFileInfoProcessor.CreateOutputFiles

        Dim blnSuccess As Boolean
        Dim blnSuccessOverall As Boolean

        Dim strErrorMessage As String
        Dim strDatasetName As String

        Dim blnCreateQCPlotHtmlFile As Boolean

        Dim ioFolderInfo As System.IO.DirectoryInfo

        Try

            strDatasetName = Me.GetDatasetNameViaPath(strInputFileName)
            blnSuccessOverall = True
            blnCreateQCPlotHtmlFile = False

            If strOutputFolderPath Is Nothing Then strOutputFolderPath = String.Empty

            If strOutputFolderPath.Length > 0 Then
                ' Make sure the output folder exists
                ioFolderInfo = New System.IO.DirectoryInfo(strOutputFolderPath)

                If Not ioFolderInfo.Exists Then
                    ioFolderInfo.Create()
                End If
            Else
                ioFolderInfo = New System.IO.DirectoryInfo(".")
            End If

            If mSaveTICAndBPI Then
                ' Write out the TIC and BPI plots
                strErrorMessage = String.Empty
                blnSuccess = mTICandBPIPlot.SaveTICAndBPIPlotFiles(strDatasetName, ioFolderInfo.FullName, strErrorMessage)
                If Not blnSuccess Then
                    ReportError("Error calling SaveTICAndBPIPlotFiles: " & strErrorMessage)
                    blnSuccessOverall = False
                End If
                blnCreateQCPlotHtmlFile = True
            End If

            If mSaveLCMS2DPlots Then
                ' Write out the 2D plot of m/z vs. intensity
                ' Plots will be named Dataset_LCMS.png and Dataset_LCMSn.png
                blnSuccess = mLCMS2DPlot.Save2DPlots(strDatasetName, ioFolderInfo.FullName)
                If Not blnSuccess Then
                    blnSuccessOverall = False
                Else
                    If mLCMS2DOverviewPlotDivisor > 0 Then
                        ' Also save the Overview 2D Plots
                        blnSuccess = CreateOverview2DPlots(strDatasetName, strOutputFolderPath, mLCMS2DOverviewPlotDivisor)
                        If Not blnSuccess Then
                            blnSuccessOverall = False
                        End If
                    Else
                        mLCMS2DPlotOverview.ClearRecentFileInfo()
                    End If
                End If
                blnCreateQCPlotHtmlFile = True
            End If

            If mCreateDatasetInfoFile Then
                ' Create the _DatasetInfo.xml file
                blnSuccess = Me.CreateDatasetInfoFile(strInputFileName, ioFolderInfo.FullName)
                If Not blnSuccess Then
                    blnSuccessOverall = False
                End If
                blnCreateQCPlotHtmlFile = True
            End If

            If blnCreateQCPlotHtmlFile Then
                blnSuccess = CreateQCPlotHTMLFile(strDatasetName, ioFolderInfo.FullName)
                If Not blnSuccess Then
                    blnSuccessOverall = False
                End If
            End If

        Catch ex As System.Exception
            ReportError("Error creating output files: " & ex.Message)
            blnSuccessOverall = False
        End Try

        Return blnSuccessOverall

    End Function

    Protected Function CreateQCPlotHTMLFile(ByVal strDatasetName As String, _
                                            ByVal strOutputFolderPath As String) As Boolean

        Dim swOutFile As System.IO.StreamWriter

        Dim strHTMLFilePath As String
        Dim strFile1 As String
        Dim strFile2 As String
        Dim strTop As String

        Dim strDSInfoFileName As String

        Dim blnSuccess As Boolean

        Dim objSummaryStats As DSSummarizer.clsDatasetSummaryStats

        Try

            blnSuccess = False

            ' Obtain the dataset summary stats (they will be auto-computed if not up to date)
            objSummaryStats = mDatasetStatsSummarizer.GetDatasetSummaryStats

            strHTMLFilePath = System.IO.Path.Combine(strOutputFolderPath, "index.html")

            swOutFile = New System.IO.StreamWriter(New System.IO.FileStream(strHTMLFilePath, IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))

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

            strFile1 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS)
            strFile2 = mLCMS2DPlotOverview.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn)
            strTop = IntToEngineeringNotation(mLCMS2DPlotOverview.Options.MaxPointsToPlot)

            If strFile1.Length > 0 OrElse strFile2.Length > 0 Then
                swOutFile.WriteLine("    <tr>")
                swOutFile.WriteLine("      <td valign=""middle"">LCMS<br>(Top " & strTop & ")</td>")
                swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile1, 250) & "</td>")
                swOutFile.WriteLine("      <td>" & GenerateQCFigureHTML(strFile2, 250) & "</td>")
                swOutFile.WriteLine("    </tr>")
                swOutFile.WriteLine("")
            End If

            strFile1 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMS)
            strFile2 = mLCMS2DPlot.GetRecentFileInfo(clsLCMSDataPlotter.eOutputFileTypes.LCMSMSn)
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
            If mCreateDatasetInfoFile OrElse System.IO.File.Exists(System.IO.Path.Combine(strOutputFolderPath, strDSInfoFileName)) Then
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
        Catch ex As System.Exception
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

    Private Sub GenerateQCScanTypeSummaryHTML(ByRef swOutFile As System.IO.StreamWriter, _
                                              ByRef objDatasetSummaryStats As DSSummarizer.clsDatasetSummaryStats, _
                                              ByVal strIndent As String)

        Dim objEnum As System.Collections.Generic.Dictionary(Of String, Integer).Enumerator
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
        Dim strValue As String
        strValue = String.Empty

        If intValue < 1000 Then
            Return intValue.ToString
        ElseIf intValue < 1000000.0 Then
            Return CInt(Math.Round(intValue / 1000, 0)).ToString & "K"
        Else
            Return CInt(Math.Round(intValue / 1000 / 1000, 0)).ToString & "M"
        End If

    End Function

    Public MustOverride Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean Implements iMSFileInfoProcessor.ProcessDatafile
    Public MustOverride Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String Implements iMSFileInfoProcessor.GetDatasetNameViaPath

    Private Sub mLCMS2DPlot_ErrorEvent(ByVal Message As String) Handles mLCMS2DPlot.ErrorEvent
        ReportError("Error in LCMS2DPlot: " & Message)
    End Sub

    Private Sub mLCMS2DPlotOverview_ErrorEvent(ByVal Message As String) Handles mLCMS2DPlotOverview.ErrorEvent
        ReportError("Error in LCMS2DPlotOverview: " & Message)
    End Sub
End Class
