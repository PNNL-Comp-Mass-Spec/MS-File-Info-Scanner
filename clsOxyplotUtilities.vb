Imports OxyPlot
Imports OxyPlot.Axes

Public Class clsOxyplotUtilities

    Public Const FONT_SIZE_BASE As Integer = 16

    Public Shared Function GetBasicPlotModel(strTitle As String, xAxisLabel As String, yAxisLabel As String) As PlotModel

        Dim myPlot As New PlotModel
        
        ' Set the titles and axis labels
        myPlot.Title = String.Copy(strTitle)
        myPlot.Padding = New OxyThickness(myPlot.Padding.Left, myPlot.Padding.Top, 30, myPlot.Padding.Bottom)

        myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Bottom, xAxisLabel, FONT_SIZE_BASE))
        myPlot.Axes(0).Minimum = 0

        myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Left, yAxisLabel, FONT_SIZE_BASE))

        ' Adjust the font sizes
        myPlot.Axes(0).FontSize = FONT_SIZE_BASE
        myPlot.Axes(1).FontSize = FONT_SIZE_BASE

        myPlot.TitleFontSize = FONT_SIZE_BASE + 4
        ' myPlot.TitleFontWeight = 700

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
        axis.AxisTitleDistance = 15
        axis.TickStyle = TickStyle.Crossing
        'axis.AxislineThickness = 2
        axis.AxislineColor = OxyColors.Black
        axis.AxislineStyle = LineStyle.Solid
        axis.MajorTickSize = 8

        axis.MajorGridlineStyle = LineStyle.None
        axis.MinorGridlineStyle = LineStyle.None

        ' axis.MinorGridlineColor = OxyColor.Parse("#11000000")
        ' axis.Minimum = 0.0
        ' axis.AbsoluteMinimum = 0.0

        Return axis

    End Function
End Class
