using System;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;

namespace MSFileInfoScanner.Plotting
{
    /// <summary>
    /// OxyPlot utilities
    /// </summary>
    public static class OxyPlotUtilities
    {
        // Ignore Spelling: Arial, Oxy

        /// <summary>
        /// Return plot model with the given title and axis labels
        /// </summary>
        /// <param name="title">Plot title</param>
        /// <param name="xAxisLabel">X-axis label</param>
        /// <param name="yAxisLabel">Y-axis label</param>
        /// <returns></returns>
#pragma warning disable CS3002 // Return type is not CLS-compliant
        public static PlotModel GetBasicPlotModel(string title, string xAxisLabel, string yAxisLabel)
#pragma warning restore CS3002 // Argument type is not CLS-compliant
        {
            var myPlot = new PlotModel
            {
                Title = string.Copy(title),
                TitleFont = "Arial",
                TitleFontSize = PlotContainer.DEFAULT_BASE_FONT_SIZE + 4,
                TitleFontWeight = FontWeights.Normal
            };

            myPlot.Padding = new OxyThickness(myPlot.Padding.Left, myPlot.Padding.Top, 30, myPlot.Padding.Bottom);

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Bottom, xAxisLabel, PlotContainer.DEFAULT_BASE_FONT_SIZE));
            myPlot.Axes[0].Minimum = 0;

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Left, yAxisLabel, PlotContainer.DEFAULT_BASE_FONT_SIZE));

            // Adjust the font sizes
            myPlot.Axes[0].FontSize = PlotContainer.DEFAULT_BASE_FONT_SIZE;
            myPlot.Axes[1].FontSize = PlotContainer.DEFAULT_BASE_FONT_SIZE;

            // Set the background color
            myPlot.PlotAreaBackground = OxyColor.FromRgb(243, 243, 243);

            return myPlot;
        }

        /// <summary>
        /// Obtain a linear axis
        /// </summary>
        /// <param name="position">Axis position</param>
        /// <param name="axisTitle">Axis title</param>
        /// <param name="baseFontSize">Base font size</param>
        /// <returns></returns>
#pragma warning disable CS3001 // Argument type is not CLS-compliant
#pragma warning disable CS3002 // Return type is not CLS-compliant
        public static LinearAxis MakeLinearAxis(AxisPosition position, string axisTitle, int baseFontSize)
#pragma warning restore CS3002 // Return type is not CLS-compliant
#pragma warning restore CS3001 // Argument type is not CLS-compliant
        {
            return new LinearAxis
            {
                Position = position,
                Title = axisTitle,
                TitleFontSize = baseFontSize + 2,
                TitleFontWeight = FontWeights.Normal,
                TitleFont = "Arial",
                AxisTitleDistance = 15,
                TickStyle = TickStyle.Crossing,
                AxislineColor = OxyColors.Black,
                AxislineStyle = LineStyle.Solid,
                MajorTickSize = 8,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                StringFormat = AxisInfo.DEFAULT_AXIS_LABEL_FORMAT,
                Font = "Arial"
            };
        }

        /// <summary>
        /// Examine the values in dataPoints to see if they are all less than 10 (or all less than 1)
        /// If they are, change the axis format code from the default of "#,##0" (see DEFAULT_AXIS_LABEL_FORMAT)
        /// </summary>
        /// <param name="currentAxis">Current axis</param>
        /// <param name="dataPoints">Data points</param>
        /// <param name="integerData">True if the data values are integers</param>
#pragma warning disable CS3001 // Argument type is not CLS-compliant
        public static void UpdateAxisFormatCodeIfSmallValues(Axis currentAxis, List<double> dataPoints, bool integerData)
#pragma warning restore CS3001
        {
            if (dataPoints.Count == 0)
                return;

            var axisInfo = new AxisInfo(currentAxis.MajorStep, currentAxis.MinorGridlineThickness, currentAxis.Title);
            PlotUtilities.GetAxisFormatInfo(dataPoints, integerData, axisInfo);

            currentAxis.StringFormat = axisInfo.StringFormat;
            currentAxis.MajorStep = axisInfo.MajorStep;
            currentAxis.MinorGridlineThickness = axisInfo.MinorGridlineThickness;
        }

        /// <summary>
        /// Validate the major step size for the given axis
        /// </summary>
        /// <param name="currentAxis">Axis</param>
#pragma warning disable CS3001 // Argument type is not CLS-compliant
        public static void ValidateMajorStep(Axis currentAxis)
#pragma warning restore CS3001
        {
            if (Math.Abs(currentAxis.ActualMajorStep) > float.Epsilon && currentAxis.ActualMajorStep < 1)
            {
                currentAxis.MajorStep = 1;
                currentAxis.MinorGridlineThickness = 0;
            }
        }
    }
}
