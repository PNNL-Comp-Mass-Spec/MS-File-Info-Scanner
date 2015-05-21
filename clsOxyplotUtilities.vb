Imports OxyPlot
Imports OxyPlot.Axes

Public Class clsOxyplotUtilities

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
End Class
