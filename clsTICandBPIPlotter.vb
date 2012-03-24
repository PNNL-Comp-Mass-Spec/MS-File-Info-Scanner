Option Strict On

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

    Protected mRecentFiles As System.Collections.Generic.List(Of udtOutputFileInfoType)
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
        mRecentFiles = New System.Collections.Generic.List(Of udtOutputFileInfoType)
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
        udtOutputFileInfo.FileName = System.IO.Path.GetFileName(strFilePath)
        udtOutputFileInfo.FilePath = strFilePath

        mRecentFiles.Add(udtOutputFileInfo)
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
    Private Function InitializeGraphPane(ByRef objData As clsChromatogramInfo, _
                                         ByVal strTitle As String, _
                                         ByVal intMSLevelFilter As Integer, _
                                         ByVal strXAxisLabel As String, _
                                         ByVal strYAxisLabel As String, _
                                         ByVal blnAutoMinMaxY As Boolean, _
                                         ByVal blnYAxisExponentialNotation As Boolean) As ZedGraph.GraphPane

        Const FONT_SIZE_BASE As Integer = 11

        Dim myPane As New ZedGraph.GraphPane

        Dim objPoints As ZedGraph.PointPairList

        Dim intIndex As Integer

        Dim intMaxScan As Integer
        Dim dblScanTimeMax As Double
        Dim dblMaxIntensity As Double


        ' Instantiate the ZedGraph object to track the points
        objPoints = New ZedGraph.PointPairList

        intMaxScan = 0
        dblScanTimeMax = 0
        dblMaxIntensity = 0


        With objData

            For intIndex = 0 To .ScanCount - 1
                If intMSLevelFilter = 0 OrElse _
                   .ScanMSLevel(intIndex) = intMSLevelFilter OrElse _
                   intMSLevelFilter = 2 And .ScanMSLevel(intIndex) >= 2 Then

                    objPoints.Add(New ZedGraph.PointPair(.ScanNum(intIndex), .ScanIntensity(intIndex)))

                    If .ScanTimeMinutes(intIndex) > dblScanTimeMax Then
                        dblScanTimeMax = .ScanTimeMinutes(intIndex)
                    End If

                    If .ScanNum(intIndex) > intMaxScan Then
                        intMaxScan = .ScanNum(intIndex)
                    End If

                    If .ScanIntensity(intIndex) > dblMaxIntensity Then
                        dblMaxIntensity = .ScanIntensity(intIndex)
                    End If
                End If
            Next intIndex

        End With

        If objPoints.Count = 0 Then
            ' Nothing to plot
            Return myPane
        End If

        ' Round intMaxScan down to the nearest multiple of 10
        intMaxScan = CInt(Math.Ceiling(intMaxScan / 10.0) * 10)

        ' Multiple dblMaxIntensity by 2% and then round up to the nearest integer
        dblMaxIntensity = CLng(Math.Ceiling(dblMaxIntensity * 1.02))

        ' Set the titles and axis labels
        myPane.Title.Text = String.Copy(strTitle)
        myPane.XAxis.Title.Text = strXAxisLabel
        myPane.YAxis.Title.Text = strYAxisLabel

        ' Generate a black curve with no symbols
        Dim myCurve As ZedGraph.LineItem
        myPane.CurveList.Clear()

        If objPoints.Count > 0 Then
            myCurve = myPane.AddCurve(strTitle, objPoints, System.Drawing.Color.Black, ZedGraph.SymbolType.None)

            myCurve.Line.Width = 1
        End If

        ' Possibly add a label showing the maximum elution time
        If dblScanTimeMax > 0 Then

            Dim objScanTimeMaxText As New ZedGraph.TextObj(dblScanTimeMax.ToString("0") & " minutes", 1, 1, ZedGraph.CoordType.PaneFraction)

            With objScanTimeMaxText
                .FontSpec.Angle = 0
                .FontSpec.FontColor = Drawing.Color.Black
                .FontSpec.IsBold = False
                .FontSpec.Size = FONT_SIZE_BASE
                .FontSpec.Border.IsVisible = False
                .FontSpec.Fill.IsVisible = False
                .Location.AlignH = ZedGraph.AlignH.Right
                .Location.AlignV = ZedGraph.AlignV.Bottom
            End With
            myPane.GraphObjList.Add(objScanTimeMaxText)

        End If

        ' Hide the x and y axis grids
        myPane.XAxis.MajorGrid.IsVisible = False
        myPane.YAxis.MajorGrid.IsVisible = False

        ' Set the X-axis to display unmodified scan numbers (by default, ZedGraph scales them to a range between 0 and 10)
        myPane.XAxis.Scale.Mag = 0
        myPane.XAxis.Scale.MagAuto = False
        myPane.XAxis.Scale.MaxGrace = 0

        ' Override the auto-computed axis range
        myPane.XAxis.Scale.Min = 0
        myPane.XAxis.Scale.Max = intMaxScan

        '' Could set the Y-axis to display unmodified m/z values
        'myPane.YAxis.Scale.Mag = 0
        'myPane.YAxis.Scale.MagAuto = False
        'myPane.YAxis.Scale.MaxGrace = 0.01

        ' Override the auto-computed axis range
        If blnAutoMinMaxY Then
            myPane.YAxis.Scale.MinAuto = True
            myPane.YAxis.Scale.MaxAuto = True
        Else
            myPane.YAxis.Scale.Min = 0
            myPane.YAxis.Scale.Max = dblMaxIntensity
        End If

        myPane.YAxis.Title.IsOmitMag = True

        If blnYAxisExponentialNotation Then
            AddHandler myPane.YAxis.ScaleFormatEvent, AddressOf ZedGraphYScaleFormatter
        End If

        ' Align the Y axis labels so they are flush to the axis
        myPane.YAxis.Scale.Align = ZedGraph.AlignP.Inside

        ' Adjust the font sizes
        myPane.XAxis.Title.FontSpec.Size = FONT_SIZE_BASE
        myPane.XAxis.Title.FontSpec.IsBold = True
        myPane.XAxis.Scale.FontSpec.Size = FONT_SIZE_BASE

        myPane.YAxis.Title.FontSpec.Size = FONT_SIZE_BASE
        myPane.YAxis.Title.FontSpec.IsBold = True
        myPane.YAxis.Scale.FontSpec.Size = FONT_SIZE_BASE

        myPane.Title.FontSpec.Size = FONT_SIZE_BASE + 1
        myPane.Title.FontSpec.IsBold = True

        ' Fill the axis background with a gradient
        myPane.Chart.Fill = New ZedGraph.Fill(System.Drawing.Color.White, System.Drawing.Color.FromArgb(255, 230, 230, 230), 45.0F)

        ' Could use the following to simply fill with white
        'myPane.Chart.Fill = New ZedGraph.Fill(Drawing.Color.White)

        ' Hide the legend
        myPane.Legend.IsVisible = False

        ' Force a plot update
        myPane.AxisChange()

        Return myPane

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

        Dim myPane As ZedGraph.GraphPane
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

            myPane = InitializeGraphPane(mBPI, strDatasetName & " - " & mBPIPlotAbbrev & " - MS Spectra", 1, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_" & mBPIPlotAbbrev & "_MS.png")
                myPane.GetImage(1024, 600, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMS)
            End If

            myPane = InitializeGraphPane(mBPI, strDatasetName & " - " & mBPIPlotAbbrev & " - MS2 Spectra", 2, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_" & mBPIPlotAbbrev & "_MSn.png")
                myPane.GetImage(1024, 600, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMSn)
            End If

            myPane = InitializeGraphPane(mTIC, strDatasetName & " - " & mTICPlotAbbrev & " - All Spectra", 0, mTICXAxisLabel, mTICYAxisLabel, mTICAutoMinMaxY, mTICYAxisExponentialNotation)
            If myPane.CurveList.Count > 0 Then
                strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_" & mTICPlotAbbrev & ".png")
                myPane.GetImage(1024, 600, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.TIC)
            End If

            blnSuccess = True
        Catch ex As System.Exception
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
            If objChrom.ScanIntensity(intIndex) = 0 Then
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
            If objChrom.ScanIntensity(intIndex) = 0 Then
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

    Protected Sub ValidateMSLevel(ByRef objChrom As clsChromatogramInfo)
        Dim intIndex As Integer
        Dim blnMSLevelDefined As Boolean

        For intIndex = 0 To objChrom.ScanCount - 1
            If objChrom.ScanMSLevel(intIndex) > 0 Then
                blnMSLevelDefined = True
                Exit For
            End If
        Next intIndex

        If Not blnMSLevelDefined Then
            ' Set the MSLevel to 1 for all scans
            For intIndex = 0 To objChrom.ScanCount - 1
                objChrom.ScanMSLevel(intIndex) = 1
            Next intIndex
        End If

    End Sub

    Private Function ZedGraphYScaleFormatter(ByVal pane As ZedGraph.GraphPane, _
                                               ByVal axis As ZedGraph.Axis, _
                                               ByVal val As Double, _
                                               ByVal index As Int32) As String
        If val = 0 Then
            Return "0"
        Else
            Return val.ToString("0.00E+00")
        End If

    End Function

    Protected Class clsChromatogramInfo

        Public ScanCount As Integer
        Public ScanNum() As Integer
        Public ScanTimeMinutes() As Single
        Public ScanIntensity() As Double
        Public ScanMSLevel() As Integer

        Public Sub New()
            Me.Initialize()
        End Sub

        Public Sub AddPoint(ByVal intScanNumber As Integer, _
                            ByVal intMSLevel As Integer, _
                            ByVal sngScanTimeMinutes As Single, _
                            ByVal dblIntensity As Double)

            If Me.ScanCount >= Me.ScanNum.Length Then
                ReDim Preserve Me.ScanNum(Me.ScanNum.Length * 2 - 1)
                ReDim Preserve Me.ScanTimeMinutes(Me.ScanNum.Length - 1)
                ReDim Preserve Me.ScanIntensity(Me.ScanNum.Length - 1)
                ReDim Preserve Me.ScanMSLevel(Me.ScanNum.Length - 1)
            End If

            Me.ScanNum(Me.ScanCount) = intScanNumber
            Me.ScanTimeMinutes(Me.ScanCount) = sngScanTimeMinutes
            Me.ScanIntensity(Me.ScanCount) = dblIntensity
            Me.ScanMSLevel(Me.ScanCount) = intMSLevel

            Me.ScanCount += 1
        End Sub

        Public Sub Initialize()
            ScanCount = 0
            ReDim ScanNum(9)
            ReDim ScanTimeMinutes(9)
            ReDim ScanIntensity(9)
            ReDim ScanMSLevel(9)
        End Sub

        Public Sub TrimArrays()
            ReDim Preserve ScanNum(ScanCount - 1)
            ReDim Preserve ScanTimeMinutes(ScanCount - 1)
            ReDim Preserve ScanIntensity(ScanCount - 1)
            ReDim Preserve ScanMSLevel(ScanCount - 1)
        End Sub

        Public Sub RemoveAt(ByVal Index As Integer)
            RemoveRange(Index, 1)
        End Sub

        Public Sub RemoveRange(ByVal Index As Integer, ByVal Count As Integer)
            Dim intSourceIndex As Integer
            Dim i As Integer
            Dim intIndexMaxNew As Integer

            If Index >= 0 And Index < ScanCount And Count > 0 Then
                intSourceIndex = -1
                intIndexMaxNew = ScanCount - 1

                For i = Index To ScanCount - 1
                    If i + Count >= ScanCount Then
                        intIndexMaxNew = i - 1
                        Exit For
                    Else
                        intIndexMaxNew = i
                    End If
                    intSourceIndex = i + Count

                    ScanNum(i) = ScanNum(intSourceIndex)
                    ScanTimeMinutes(i) = ScanTimeMinutes(intSourceIndex)
                    ScanIntensity(i) = ScanIntensity(intSourceIndex)
                    ScanMSLevel(i) = ScanMSLevel(intSourceIndex)
                Next

                If intIndexMaxNew < ScanCount - 1 Then
                    ScanCount = intIndexMaxNew + 1
                    TrimArrays()
                End If
            End If

        End Sub

    End Class
End Class
