Imports OxyPlot
Imports OxyPlot.Axes

Public Class clsOxyplotUtilities

    Public Const EXPONENTIAL_FORMAT As String = "0.00E+00"
    
    Public Const FONT_SIZE_BASE As Integer = 16

    Protected Const DEFAULT_AXIS_LABEL_FORMAT As String = "#,##0"

    Public Shared Function GetBasicPlotModel(strTitle As String, xAxisLabel As String, yAxisLabel As String) As PlotModel

        Dim myPlot As New PlotModel
        
        ' Set the titles and axis labels
        myPlot.Title = String.Copy(strTitle)
        myPlot.TitleFont = "Arial"
        myPlot.TitleFontSize = FONT_SIZE_BASE + 4
        myPlot.TitleFontWeight = OxyPlot.FontWeights.Normal

        myPlot.Padding = New OxyThickness(myPlot.Padding.Left, myPlot.Padding.Top, 30, myPlot.Padding.Bottom)

        myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Bottom, xAxisLabel, FONT_SIZE_BASE))
        myPlot.Axes(0).Minimum = 0

        myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Left, yAxisLabel, FONT_SIZE_BASE))

        ' Adjust the font sizes
        myPlot.Axes(0).FontSize = FONT_SIZE_BASE
        myPlot.Axes(1).FontSize = FONT_SIZE_BASE

        ' Set the background color
        myPlot.PlotAreaBackground = OxyColor.FromRgb(243, 243, 243)

        Return myPlot

    End Function

    Public Shared Function MakeLinearAxis(position As AxisPosition, axisTitle As String, baseFontSize As Integer) As LinearAxis
        Dim axis = New LinearAxis()

        axis.Position = position

        axis.Title = axisTitle
        axis.TitleFontSize = baseFontSize + 2
        axis.TitleFontWeight = OxyPlot.FontWeights.Normal
        axis.TitleFont = "Arial"

        axis.AxisTitleDistance = 15
        axis.TickStyle = TickStyle.Crossing

        axis.AxislineColor = OxyColors.Black
        axis.AxislineStyle = LineStyle.Solid
        axis.MajorTickSize = 8

        axis.MajorGridlineStyle = LineStyle.None
        axis.MinorGridlineStyle = LineStyle.None

        axis.StringFormat = DEFAULT_AXIS_LABEL_FORMAT
        axis.Font = "Arial"

        Return axis

    End Function

    ''' <summary>
    ''' Examine the values in dataPoints to see if they are all less than 10 (or all less than 1)
    ''' If they are, change the axis format code from the default of "#,##0" (see DEFAULT_AXIS_LABEL_FORMAT)
    ''' </summary>
    ''' <param name="currentAxis"></param>
    ''' <param name="dataPoints"></param>
    ''' <remarks></remarks>
    Public Shared Sub UpdateAxisFormatCodeIfSmallValues(currentAxis As Axis, dataPoints As IEnumerable(Of Double), integerData As Boolean)

        If Not dataPoints.Any Then Return

        Dim minValue = Math.Abs(dataPoints(0))
        Dim maxValue = minValue

        For Each currentValAbs In From value In dataPoints Select Math.Abs(value)
            minValue = Math.Min(minValue, currentValAbs)
            maxValue = Math.Max(maxValue, currentValAbs)
        Next

        If Math.Abs(minValue - 0) < Single.Epsilon And Math.Abs(maxValue - 0) < Single.Epsilon Then
            currentAxis.StringFormat = "0"
            currentAxis.MinorGridlineThickness = 0
            currentAxis.MajorStep = 1
            Return
        End If

        If integerData Then
            If maxValue >= 1000000 Then
                currentAxis.StringFormat = EXPONENTIAL_FORMAT
            End If
            Return
        End If

        Dim minDigitsPrecision = 0

        If maxValue < 0.02 Then
            currentAxis.StringFormat = EXPONENTIAL_FORMAT
        ElseIf maxValue < 0.2 Then
            minDigitsPrecision = 2
            currentAxis.StringFormat = "0.00"
        ElseIf maxValue < 2 Then
            minDigitsPrecision = 1
            currentAxis.StringFormat = "0.0"
        ElseIf maxValue >= 1000000 Then
            currentAxis.StringFormat = EXPONENTIAL_FORMAT
        End If

        If maxValue - minValue < 0.00001 Then
            If Not currentAxis.StringFormat.Contains(".") Then
                currentAxis.StringFormat = "0.00"
            End If
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

    Public Shared Sub ValidateMajorStep(currentAxis As Axis)
        If Math.Abs(currentAxis.ActualMajorStep) > Single.Epsilon AndAlso currentAxis.ActualMajorStep < 1 Then
            currentAxis.MinorGridlineThickness = 0
            currentAxis.MajorStep = 1
        End If
    End Sub
       
End Class
