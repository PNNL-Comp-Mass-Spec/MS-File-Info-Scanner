using System;
using System.Collections.Generic;
using System.Linq;

namespace MSFileInfoScanner
{
    class clsPlotUtilities
    {

        public static void GetAxisFormatInfo(
            IList<double> dataPoints,
            bool integerData,
            clsAxisInfo axisInfo)
        {

            var minValue = Math.Abs(dataPoints[0]);
            var maxValue = minValue;

            foreach (var currentValAbs in from value in dataPoints select Math.Abs(value))
            {
                minValue = Math.Min(minValue, currentValAbs);
                maxValue = Math.Max(maxValue, currentValAbs);
            }

            if (Math.Abs(minValue - 0) < float.Epsilon & Math.Abs(maxValue - 0) < float.Epsilon)
            {
                axisInfo.StringFormat = "0";
                axisInfo.MinorGridlineThickness = 0;
                axisInfo.MajorStep = 1;
                return;
            }

            if (integerData)
            {
                if (maxValue >= 1000000)
                {
                    axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
                }
                return;
            }

            var minDigitsPrecision = 0;

            if (maxValue < 0.02)
            {
                axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }
            else if (maxValue < 0.2)
            {
                minDigitsPrecision = 2;
                axisInfo.StringFormat = "0.00";
            }
            else if (maxValue < 2)
            {
                minDigitsPrecision = 1;
                axisInfo.StringFormat = "0.0";
            }
            else if (maxValue >= 1000000)
            {
                axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }

            if (maxValue - minValue < 1E-05)
            {
                if (!axisInfo.StringFormat.Contains("."))
                {
                    axisInfo.StringFormat = "0.00";
                }
            }
            else
            {
                // Examine the range of values between the minimum and the maximum
                // If the range is small, e.g. between 3.95 and 3.98, then we need to guarantee that we have at least 2 digits of precision
                // The following combination of Log10 and ceiling determins the minimum needed
                var minDigitsRangeBased = (int)Math.Ceiling(-(Math.Log10(maxValue - minValue)));

                if (minDigitsRangeBased > minDigitsPrecision)
                {
                    minDigitsPrecision = minDigitsRangeBased;
                }

                if (minDigitsPrecision > 0)
                {
                    axisInfo.StringFormat = "0." + new string('0', minDigitsPrecision);
                }
            }
        }
    }
}
