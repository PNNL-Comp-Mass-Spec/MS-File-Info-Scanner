Option Strict On

Imports System.IO
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series

Public Class clsTICandBPIPlotter

#Region "Constants, Enums, Structures"
    Public Enum eOutputFileTypes
        TIC = 0
        BPIMS = 1
        BPIMSn = 2
    End Enum

    Protected Structure udtOutputFileInfoType
        Public FileType As eOutputFileTypes
        Public FileName As String
        Public FilePath As String
    End Structure

    Protected Const EXPONENTIAL_FORMAT As String = "0.00E+00"

#End Region

#Region "Member variables"
    ' Data stored in mTIC will get plotted for all scans, both MS and MS/MS
    Protected mTIC As clsChromatogramInfo

    ' Data stored in mBPI will be plotted separately for MS and MS/MS spectra
    Protected mBPI As clsChromatogramInfo

    Protected mTICXAxisLabel As String = "LC Scan Number"
    Protected mTICYAxisLabel As String = "Intensity"
    Protected mTICYAxisExponentialNotation As Boolean = True

    Protected mBPIXAxisLabel As String = "LC Scan Number"
    Protected mBPIYAxisLabel As String = "Intensity"
    Protected mBPIYAxisExponentialNotation As Boolean = True

    Protected mTICPlotAbbrev As String = "TIC"
    Protected mBPIPlotAbbrev As String = "BPI"

    Protected mBPIAutoMinMaxY As Boolean
    Protected mTICAutoMinMaxY As Boolean
    Protected mRemoveZeroesFromEnds As Boolean

    Protected mRecentFiles As List(Of udtOutputFileInfoType)
#End Region


    Public Property BPIAutoMinMaxY() As Boolean
        Get
            Return mBPIAutoMinMaxY
        End Get
        Set(ByVal value As Boolean)
            mBPIAutoMinMaxY = value
        End Set
    End Property

    Public Property BPIPlotAbbrev() As String
        Get
            Return mBPIPlotAbbrev
        End Get
        Set(ByVal value As String)
            mBPIPlotAbbrev = value
        End Set
    End Property

    Public Property BPIXAxisLabel() As String
        Get
            Return mBPIXAxisLabel
        End Get
        Set(ByVal value As String)
            mBPIXAxisLabel = value
        End Set
    End Property

    Public Property BPIYAxisLabel() As String
        Get
            Return mBPIYAxisLabel
        End Get
        Set(ByVal value As String)
            mBPIYAxisLabel = value
        End Set
    End Property

    Public Property BPIYAxisExponentialNotation() As Boolean
        Get
            Return mBPIYAxisExponentialNotation
        End Get
        Set(ByVal value As Boolean)
            mBPIYAxisExponentialNotation = value
        End Set
    End Property

    Public ReadOnly Property CountBPI() As Integer
        Get
            Return mBPI.ScanCount
        End Get
    End Property

    Public ReadOnly Property CountTIC() As Integer
        Get
            Return mTIC.ScanCount
        End Get
    End Property

    Public Property RemoveZeroesFromEnds() As Boolean
        Get
            Return mRemoveZeroesFromEnds
        End Get
        Set(ByVal value As Boolean)
            mRemoveZeroesFromEnds = value
        End Set
    End Property

    Public Property TICAutoMinMaxY() As Boolean
        Get
            Return mTICAutoMinMaxY
        End Get
        Set(ByVal value As Boolean)
            mTICAutoMinMaxY = value
        End Set
    End Property

    Public Property TICPlotAbbrev() As String
        Get
            Return mTICPlotAbbrev
        End Get
        Set(ByVal value As String)
            mTICPlotAbbrev = value
        End Set
    End Property

    Public Property TICXAxisLabel() As String
        Get
            Return mTICXAxisLabel
        End Get
        Set(ByVal value As String)
            mTICXAxisLabel = value
        End Set
    End Property

    Public Property TICYAxisLabel() As String
        Get
            Return mTICYAxisLabel
        End Get
        Set(ByVal value As String)
            mTICYAxisLabel = value
        End Set
    End Property

    Public Property TICYAxisExponentialNotation() As Boolean
        Get
            Return mTICYAxisExponentialNotation
        End Get
        Set(ByVal value As Boolean)
            mTICYAxisExponentialNotation = value
        End Set
    End Property

    Public Sub New()
        mRecentFiles = New List(Of udtOutputFileInfoType)
        Me.Reset()
    End Sub

    Public Sub AddData(ByVal intScanNumber As Integer, _
                       ByVal intMSLevel As Integer, _
                       ByVal sngScanTimeMinutes As Single, _
                       ByVal dblBPI As Double, _
                       ByVal dblTIC As Double)

        mBPI.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblBPI)
        mTIC.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblTIC)

    End Sub

    Public Sub AddDataBPIOnly(ByVal intScanNumber As Integer, _
                               ByVal intMSLevel As Integer, _
                               ByVal sngScanTimeMinutes As Single, _
                               ByVal dblBPI As Double)

        mBPI.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblBPI)

    End Sub

    Public Sub AddDataTICOnly(ByVal intScanNumber As Integer, _
                                 ByVal intMSLevel As Integer, _
                                 ByVal sngScanTimeMinutes As Single, _
                                 ByVal dblTIC As Double)

        mTIC.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblTIC)

    End Sub

    Protected Sub AddRecentFile(ByVal strFilePath As String, ByVal eFileType As eOutputFileTypes)
        Dim udtOutputFileInfo As udtOutputFileInfoType

        udtOutputFileInfo.FileType = eFileType
        udtOutputFileInfo.FileName = Path.GetFileName(strFilePath)
        udtOutputFileInfo.FilePath = strFilePath

        mRecentFiles.Add(udtOutputFileInfo)
    End Sub

    Protected Sub AddSeries(myplot As PlotModel, objPoints As List(Of DataPoint))

        ' Generate a black curve with no symbols
        Dim series = New LineSeries

        If objPoints.Count > 0 Then
            Dim eSymbolType = MarkerType.None
            If objPoints.Count = 1 Then
                eSymbolType = MarkerType.Circle
            End If

            series.Color = OxyColors.Black
            series.StrokeThickness = 1
            series.MarkerType = eSymbolType

            If objPoints.Count = 1 Then
                series.MarkerSize = 8
                series.MarkerFill = OxyColors.DarkRed
            End If

            series.Points.AddRange(objPoints)

            myplot.Series.Add(series)
        End If

    End Sub

    ''' <summary>
    ''' Returns the file name of the recently saved file of the given type
    ''' </summary>
    ''' <param name="eFileType">File type to find</param>
    ''' <returns>File name if found; empty string if this file type was not saved</returns>
    ''' <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
    Public Function GetRecentFileInfo(ByVal eFileType As eOutputFileTypes) As String
        Dim intIndex As Integer
        For intIndex = 0 To mRecentFiles.Count - 1
            If mRecentFiles(intIndex).FileType = eFileType Then
                Return mRecentFiles(intIndex).FileName
            End If
        Next
        Return String.Empty
    End Function

    ''' <summary>
    ''' Returns the file name and path of the recently saved file of the given type
    ''' </summary>
    ''' <param name="eFileType">File type to find</param>
    ''' <param name="strFileName">File name (output)</param>
    ''' <param name="strFilePath">File Path (output)</param>
    ''' <returns>True if a match was found; otherwise returns false</returns>
    ''' <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
    Public Function GetRecentFileInfo(ByVal eFileType As eOutputFileTypes, ByRef strFileName As String, ByRef strFilePath As String) As Boolean
        Dim intIndex As Integer
        For intIndex = 0 To mRecentFiles.Count - 1
            If mRecentFiles(intIndex).FileType = eFileType Then
                strFileName = mRecentFiles(intIndex).FileName
                strFilePath = mRecentFiles(intIndex).FilePath
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>
    ''' Plots a BPI or TIC chromatogram
    ''' </summary>
    ''' <param name="objData">Data to display</param>
    ''' <param name="strTitle">Title of the plot</param>
    ''' <param name="intMSLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
    ''' <returns>Zedgraph plot</returns>
    ''' <remarks></remarks>
    Private Function InitializePlot(
      objData As clsChromatogramInfo,
      strTitle As String,
      intMSLevelFilter As Integer,
      strXAxisLabel As String,
      strYAxisLabel As String,
      blnAutoMinMaxY As Boolean,
      blnYAxisExponentialNotation As Boolean) As clsPlotContainer

        Dim intMaxScan = 0
        Dim dblScanTimeMax As Double = 0
        Dim dblMaxIntensity As Double = 0

        ' Instantiate the ZedGraph object to track the points
        Dim objPoints = New List(Of DataPoint)

        For Each chromDataPoint In objData.Scans

            If intMSLevelFilter = 0 OrElse _
               chromDataPoint.MSLevel = intMSLevelFilter OrElse _
               intMSLevelFilter = 2 And chromDataPoint.MSLevel >= 2 Then

                objPoints.Add(New DataPoint(chromDataPoint.ScanNum, chromDataPoint.Intensity))

                If chromDataPoint.TimeMinutes > dblScanTimeMax Then
                    dblScanTimeMax = chromDataPoint.TimeMinutes
                End If

                If chromDataPoint.ScanNum > intMaxScan Then
                    intMaxScan = chromDataPoint.ScanNum
                End If

                If chromDataPoint.Intensity > dblMaxIntensity Then
                    dblMaxIntensity = chromDataPoint.Intensity
                End If
            End If
        Next

        If objPoints.Count = 0 Then
            ' Nothing to plot
            Return New clsPlotContainer(New PlotModel)
        End If

        ' Round intMaxScan down to the nearest multiple of 10
        intMaxScan = CInt(Math.Ceiling(intMaxScan / 10.0) * 10)

        ' Multiple dblMaxIntensity by 2% and then round up to the nearest integer
        dblMaxIntensity = CDbl(Math.Ceiling(dblMaxIntensity * 1.02))

        Dim myPlot = clsOxyplotUtilities.GetBasicPlotModel(strTitle, strXAxisLabel, strYAxisLabel)

        If blnYAxisExponentialNotation Then
            myPlot.Axes(1).StringFormat = EXPONENTIAL_FORMAT
        End If

        AddSeries(myPlot, objPoints)

        ' Update the axis format codes if the data values or small or the range of data is small
        Dim xVals = From item In objPoints Select item.X
        UpdateAxisFormatCodeIfSmallValues(myPlot.Axes(0), xVals)

        Dim yVals = From item In objPoints Select item.Y
        UpdateAxisFormatCodeIfSmallValues(myPlot.Axes(1), yVals)

        Dim plotContainer = New clsPlotContainer(myPlot)
        plotContainer.FontSizeBase = clsOxyplotUtilities.FONT_SIZE_BASE

        ' Possibly add a label showing the maximum elution time
        If dblScanTimeMax > 0 Then

            Dim strCaption As String
            If dblScanTimeMax < 2 Then
                strCaption = Math.Round(dblScanTimeMax, 2).ToString("0.00") & " minutes"
            ElseIf dblScanTimeMax < 10 Then
                strCaption = Math.Round(dblScanTimeMax, 1).ToString("0.0") & " minutes"
            Else
                strCaption = Math.Round(dblScanTimeMax, 0).ToString("0") & " minutes"
            End If

            plotContainer.AnnotationBottomRight = strCaption

            ' Alternative method is to add a TextAnnotation, but these are inside the main plot area
            ' and are tied to a data point

            'Dim objScanTimeMaxText = New TextAnnotation() With {
            '    .TextRotation = 0,
            '    .Text = strCaption,
            '    .Stroke = OxyColors.Black,
            '    .StrokeThickness = 2,
            '    .FontSize = FONT_SIZE_BASE
            '}

            'objScanTimeMaxText.TextPosition = New OxyPlot.DataPoint(intMaxScan, 0)
            'myPlot.Annotations.Add(objScanTimeMaxText)

        End If

        ' Override the auto-computed axis range
        myPlot.Axes(0).Minimum = 0
        myPlot.Axes(0).Maximum = intMaxScan

        ' Override the auto-computed axis range
        If blnAutoMinMaxY Then
            ' Auto scale
        Else
            myPlot.Axes(1).Minimum = 0
            myPlot.Axes(1).Maximum = dblMaxIntensity
        End If

        ' Hide the legend
        myPlot.IsLegendVisible = False

        Return plotContainer

    End Function

    Public Sub Reset()

        If mBPI Is Nothing Then
            mBPI = New clsChromatogramInfo
            mTIC = New clsChromatogramInfo
        Else
            mBPI.Initialize()
            mTIC.Initialize()
        End If

        mRecentFiles.Clear()

    End Sub

    Public Function SaveTICAndBPIPlotFiles(ByVal strDatasetName As String, _
                ByVal strOutputFolderPath As String, _
                ByRef strErrorMessage As String) As Boolean

        Dim plotContainer As clsPlotContainer
        Dim strPNGFilePath As String
        Dim blnSuccess As Boolean

        Try
            strErrorMessage = String.Empty

            mRecentFiles.Clear()

            ' Check whether all of the spectra have .MSLevel = 0
            ' If they do, change the level to 1
            ValidateMSLevel(mBPI)
            ValidateMSLevel(mTIC)

            If mRemoveZeroesFromEnds Then
                ' Check whether the last few scans have values if 0; if they do, remove them
                RemoveZeroesAtFrontAndBack(mBPI)
                RemoveZeroesAtFrontAndBack(mTIC)
            End If

            plotContainer = InitializePlot(mBPI, strDatasetName & " - " & mBPIPlotAbbrev & " - MS Spectra", 1, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation)
            If plotContainer.SeriesCount > 0 Then
                strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName & "_" & mBPIPlotAbbrev & "_MS.png")
                plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMS)
            End If

            plotContainer = InitializePlot(mBPI, strDatasetName & " - " & mBPIPlotAbbrev & " - MS2 Spectra", 2, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation)
            If plotContainer.SeriesCount > 0 Then
                strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName & "_" & mBPIPlotAbbrev & "_MSn.png")
                plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMSn)
            End If

            plotContainer = InitializePlot(mTIC, strDatasetName & " - " & mTICPlotAbbrev & " - All Spectra", 0, mTICXAxisLabel, mTICYAxisLabel, mTICAutoMinMaxY, mTICYAxisExponentialNotation)
            If plotContainer.SeriesCount > 0 Then
                strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName & "_" & mTICPlotAbbrev & ".png")
                plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.TIC)
            End If

            blnSuccess = True
        Catch ex As Exception
            strErrorMessage = ex.Message
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Protected Sub RemoveZeroesAtFrontAndBack(ByRef objChrom As clsChromatogramInfo)
        Const MAX_POINTS_TO_CHECK As Integer = 100
        Dim intIndex As Integer
        Dim intZeroPointCount As Integer
        Dim intPointsChecked As Integer
        Dim intIndexNonZeroValue As Integer

        ' See if the last few values are zero, but the data before them is non-zero
        ' If this is the case, remove the final entries

        intIndexNonZeroValue = -1
        intZeroPointCount = 0
        For intIndex = objChrom.ScanCount - 1 To 0 Step -1
            If Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < Single.Epsilon Then
                intZeroPointCount += 1
            Else
                intIndexNonZeroValue = intIndex
                Exit For
            End If
            intPointsChecked += 1
            If intPointsChecked >= MAX_POINTS_TO_CHECK Then Exit For
        Next intIndex

        If intZeroPointCount > 0 AndAlso intIndexNonZeroValue >= 0 Then
            objChrom.RemoveRange(intIndexNonZeroValue + 1, intZeroPointCount)
        End If


        ' Now check the first few values
        intIndexNonZeroValue = -1
        intZeroPointCount = 0
        For intIndex = 0 To objChrom.ScanCount - 1
            If Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < Single.Epsilon Then
                intZeroPointCount += 1
            Else
                intIndexNonZeroValue = intIndex
                Exit For
            End If
            intPointsChecked += 1
            If intPointsChecked >= MAX_POINTS_TO_CHECK Then Exit For
        Next intIndex

        If intZeroPointCount > 0 AndAlso intIndexNonZeroValue >= 0 Then
            objChrom.RemoveRange(0, intIndexNonZeroValue)
        End If

    End Sub

    ''' <summary>
    ''' Examine the values in dataPoints to see if they are all less than 10 (or all less than 1)
    ''' If they are, change the axis format code from the default of "#,##0" (see DEFAULT_AXIS_LABEL_FORMAT)
    ''' </summary>
    ''' <param name="currentAxis"></param>
    ''' <param name="dataPoints"></param>
    ''' <remarks></remarks>
    Private Sub UpdateAxisFormatCodeIfSmallValues(currentAxis As Axis, dataPoints As IEnumerable(Of Double))

        If Not dataPoints.Any Then Return

        Dim minValue = Math.Abs(dataPoints(0))
        Dim maxValue = minValue

        For Each currentValAbs In From value In dataPoints Select Math.Abs(value)
            minValue = Math.Min(minValue, currentValAbs)
            maxValue = Math.Max(maxValue, currentValAbs)
        Next

        Dim minDigitsPrecision = 0

        If maxValue < 0.02 Then
            currentAxis.StringFormat = EXPONENTIAL_FORMAT
        ElseIf maxValue < 0.2 Then
            minDigitsPrecision = 3
            currentAxis.StringFormat = "0.000"
        ElseIf maxValue < 2 Then
            minDigitsPrecision = 2
            currentAxis.StringFormat = "0.00"
        ElseIf maxValue < 20 Then
            minDigitsPrecision = 1
            currentAxis.StringFormat = "0.0"
        ElseIf maxValue >= 1000000 Then
            currentAxis.StringFormat = EXPONENTIAL_FORMAT
        End If

        If maxValue - minValue < 0.00001 Then
            currentAxis.StringFormat = "0.00000E+00"
        Else
            ' Examine the range of values between the minimum and the maximum
            ' If the range is small, e.g. between 3.95 and 3.98, then we need to guarantee that we have at least 2 digits of precision
            ' The following combination of Log10 and ceiling determins the minimum needed
            Dim minDigitsRangeBased = CInt(Math.Ceiling(-(Math.Log10(maxValue - minValue))))

            If minDigitsRangeBased > minDigitsPrecision Then
                minDigitsPrecision = minDigitsRangeBased
            End If

            If minDigitsPrecision > 0 Then
                currentAxis.StringFormat = "0." & New String("0"c, minDigitsPrecision)
            End If
        End If

    End Sub

    Protected Sub ValidateMSLevel(ByRef objChrom As clsChromatogramInfo)
        Dim intIndex As Integer
        Dim blnMSLevelDefined As Boolean

        For intIndex = 0 To objChrom.ScanCount - 1
            If objChrom.GetDataPoint(intIndex).MSLevel > 0 Then
                blnMSLevelDefined = True
                Exit For
            End If
        Next intIndex

        If Not blnMSLevelDefined Then
            ' Set the MSLevel to 1 for all scans
            For intIndex = 0 To objChrom.ScanCount - 1
                objChrom.GetDataPoint(intIndex).MSLevel = 1
            Next intIndex
        End If

    End Sub

    Protected Class clsChromatogramDataPoint
        Public Property ScanNum As Integer
        Public Property TimeMinutes As Single
        Public Property Intensity As Double
        Public Property MSLevel As Integer
    End Class

    Protected Class clsChromatogramInfo

        Public ReadOnly Property ScanCount As Integer
            Get
                Return mScans.Count
            End Get
        End Property

        Public ReadOnly Property Scans As IEnumerable(Of clsChromatogramDataPoint)
            Get
                Return mScans
            End Get
        End Property

        Protected mScans As List(Of clsChromatogramDataPoint)

        Public Sub New()
            Me.Initialize()
        End Sub

        Public Sub AddPoint(ByVal intScanNumber As Integer, _
                            ByVal intMSLevel As Integer, _
                            ByVal sngScanTimeMinutes As Single, _
                            ByVal dblIntensity As Double)

            If (From item In mScans Where item.ScanNum = intScanNumber Select item).Any() Then
                Throw New Exception("Scan " & intScanNumber & " has already been added to the TIC or BPI; programming error")
            End If

            Dim chromDataPoint = New clsChromatogramDataPoint With {
                .ScanNum = intScanNumber,
                .TimeMinutes = sngScanTimeMinutes,
                .Intensity = dblIntensity,
                .MSLevel = intMSLevel
            }

            mScans.Add(chromDataPoint)
        End Sub

        Public Function GetDataPoint(ByVal index As Integer) As clsChromatogramDataPoint
            If mScans.Count = 0 Then
                Throw New Exception("Chromatogram list is empty; cannot retrieve data point at index " & index)
            End If
            If index < 0 OrElse index >= mScans.Count Then
                Throw New Exception("Chromatogram index out of range: " & index & "; should be between 0 and " & mScans.Count - 1)
            End If

            Return mScans(index)

        End Function

        Public Sub Initialize()
            mScans = New List(Of clsChromatogramDataPoint)
        End Sub

        <Obsolete("No longer needed")>
        Public Sub TrimArrays()

        End Sub

        Public Sub RemoveAt(ByVal Index As Integer)
            RemoveRange(Index, 1)
        End Sub

        Public Sub RemoveRange(ByVal Index As Integer, ByVal Count As Integer)

            If Index >= 0 And Index < ScanCount And Count > 0 Then
                mScans.RemoveRange(Index, Count)
            End If

        End Sub

    End Class
End Class
