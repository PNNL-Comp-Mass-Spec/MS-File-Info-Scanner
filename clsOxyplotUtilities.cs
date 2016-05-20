using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;

namespace MSFileInfoScanner
{
    public class clsOxyplotUtilities
    {

        public const string EXPONENTIAL_FORMAT = "0.00E+00";


        public const int FONT_SIZE_BASE = 16;

        protected const string DEFAULT_AXIS_LABEL_FORMAT = "#,##0";
        public static PlotModel GetBasicPlotModel(string strTitle, string xAxisLabel, string yAxisLabel)
        {

            var myPlot = new PlotModel
            {
                Title = string.Copy(strTitle),
                TitleFont = "Arial",
                TitleFontSize = FONT_SIZE_BASE + 4,
                TitleFontWeight = FontWeights.Normal
            };

            myPlot.Padding = new OxyThickness(myPlot.Padding.Left, myPlot.Padding.Top, 30, myPlot.Padding.Bottom);

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Bottom, xAxisLabel, FONT_SIZE_BASE));
            myPlot.Axes[0].Minimum = 0;

            myPlot.Axes.Add(MakeLinearAxis(AxisPosition.Left, yAxisLabel, FONT_SIZE_BASE));

            // Adjust the font sizes
            myPlot.Axes[0].FontSize = FONT_SIZE_BASE;
            myPlot.Axes[1].FontSize = FONT_SIZE_BASE;

            // Set the background color
            myPlot.PlotAreaBackground = OxyColor.FromRgb(243, 243, 243);

            return myPlot;

        }

        public static LinearAxis MakeLinearAxis(AxisPosition position, string axisTitle, int baseFontSize)
        {
            var axis = new LinearAxis();

            axis.Position = position;

            axis.Title = axisTitle;
            axis.TitleFontSize = baseFontSize + 2;
            axis.TitleFontWeight = FontWeights.Normal;
            axis.TitleFont = "Arial";

            axis.AxisTitleDistance = 15;
            axis.TickStyle = TickStyle.Crossing;

            axis.AxislineColor = OxyColors.Black;
            axis.AxislineStyle = LineStyle.Solid;
            axis.MajorTickSize = 8;

            axis.MajorGridlineStyle = LineStyle.None;
            axis.MinorGridlineStyle = LineStyle.None;

            axis.StringFormat = DEFAULT_AXIS_LABEL_FORMAT;
            axis.Font = "Arial";

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

            var minValue = Math.Abs(dataPoints[0]);
            var maxValue = minValue;

            foreach (var currentValAbs in from value in dataPoints select Math.Abs(value)) {
                minValue = Math.Min(minValue, currentValAbs);
                maxValue = Math.Max(maxValue, currentValAbs);
            }

            if (Math.Abs(minValue - 0) < float.Epsilon & Math.Abs(maxValue - 0) < float.Epsilon) {
                currentAxis.StringFormat = "0";
                currentAxis.MinorGridlineThickness = 0;
                currentAxis.MajorStep = 1;
                return;
            }

            if (integerData) {
                if (maxValue >= 1000000) {
                    currentAxis.StringFormat = EXPONENTIAL_FORMAT;
                }
                return;
            }

            var minDigitsPrecision = 0;

            if (maxValue < 0.02) {
                currentAxis.StringFormat = EXPONENTIAL_FORMAT;
            } else if (maxValue < 0.2) {
                minDigitsPrecision = 2;
                currentAxis.StringFormat = "0.00";
            } else if (maxValue < 2) {
                minDigitsPrecision = 1;
                currentAxis.StringFormat = "0.0";
            } else if (maxValue >= 1000000) {
                currentAxis.StringFormat = EXPONENTIAL_FORMAT;
            }

            if (maxValue - minValue < 1E-05) {
                if (!currentAxis.StringFormat.Contains(".")) {
                    currentAxis.StringFormat = "0.00";
                }
            } else {
                // Examine the range of values between the minimum and the maximum
                // If the range is small, e.g. between 3.95 and 3.98, then we need to guarantee that we have at least 2 digits of precision
                // The following combination of Log10 and ceiling determins the minimum needed
                var minDigitsRangeBased = Convert.ToInt32(Math.Ceiling(-(Math.Log10(maxValue - minValue))));

                if (minDigitsRangeBased > minDigitsPrecision) {
                    minDigitsPrecision = minDigitsRangeBased;
                }

                if (minDigitsPrecision > 0) {
                    currentAxis.StringFormat = "0." + new string('0', minDigitsPrecision);
                }
            }

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
