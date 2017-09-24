using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;

namespace MSFileInfoScanner
{
    public class clsOxyplotUtilities
    {

        public static PlotModel GetBasicPlotModel(string strTitle, string xAxisLabel, string yAxisLabel)
        {

            var myPlot = new PlotModel
            {
                Title = string.Copy(strTitle),
                TitleFont = "Arial",
                TitleFontSize = clsPlotContainer.DEFAULT_BASE_FONT_SIZE + 4,
                TitleFontWeight = FontWeights.Normal
            };

            myPlot.Padding = new OxyThickness(myPlot.Padding.Left, myPlot.Padding.Top, 30, myPlot.Padding.Bottom);

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Bottom, xAxisLabel, clsPlotContainer.DEFAULT_BASE_FONT_SIZE));
            myPlot.Axes[0].Minimum = 0;

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Left, yAxisLabel, clsPlotContainer.DEFAULT_BASE_FONT_SIZE));

            // Adjust the font sizes
            myPlot.Axes[0].FontSize = clsPlotContainer.DEFAULT_BASE_FONT_SIZE;
            myPlot.Axes[1].FontSize = clsPlotContainer.DEFAULT_BASE_FONT_SIZE;

            // Set the background color
            myPlot.PlotAreaBackground = OxyColor.FromRgb(243, 243, 243);

            return myPlot;

        }

        public static LinearAxis MakeLinearAxis(AxisPosition position, string axisTitle, int baseFontSize)
        {
            var axis = new LinearAxis
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
                StringFormat = clsAxisInfo.DEFAULT_AXIS_LABEL_FORMAT,
                Font = "Arial"
            };

            return axis;

        }

        /// <summary>
        /// Examine the values in dataPoints to see if they are all less than 10 (or all less than 1)
        /// If they are, change the axis format code from the default of "#,##0" (see DEFAULT_AXIS_LABEL_FORMAT)
        /// </summary>
        /// <param name="currentAxis"></param>
        /// <param name="dataPoints"></param>
        /// <param name="integerData"></param>
        /// <remarks></remarks>
        public static void UpdateAxisFormatCodeIfSmallValues(Axis currentAxis, List<double> dataPoints, bool integerData)
        {
            if (!dataPoints.Any())
                return;

            var axisInfo = new clsAxisInfo();
            clsPlotUtilities.GetAxisFormatInfo(dataPoints, integerData, axisInfo);

            currentAxis.StringFormat = axisInfo.StringFormat;
            currentAxis.MinorGridlineThickness = axisInfo.MajorStep;
            currentAxis.MajorStep = axisInfo.MinorGridlineThickness;
        }

        public static void ValidateMajorStep(Axis currentAxis)
        {
            if (Math.Abs(currentAxis.ActualMajorStep) > float.Epsilon && currentAxis.ActualMajorStep < 1) {
                currentAxis.MinorGridlineThickness = 0;
                currentAxis.MajorStep = 1;
            }
        }

    }
}
