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

            var absValueMin = dataPoints.Count == 0 ? 0 : Math.Abs(dataPoints[0]);
            var absValueMax = absValueMin;

            foreach (var currentValAbs in from value in dataPoints select Math.Abs(value))
            {
                absValueMin = Math.Min(absValueMin, currentValAbs);
                absValueMax = Math.Max(absValueMax, currentValAbs);
            }

            GetAxisFormatInfo(absValueMin, absValueMax, integerData, axisInfo);
        }

        public static void GetAxisFormatInfo(
            double absValueMin,
            double absValueMax,
            bool integerData,
            clsAxisInfo axisInfo)
        {

            if (Math.Abs(absValueMin) < float.Epsilon && Math.Abs(absValueMax) < float.Epsilon)
            {
                axisInfo.StringFormat = "0";
                axisInfo.MinorGridlineThickness = 0;
                axisInfo.MajorStep = 1;
                return;
            }

            if (integerData)
            {
                if (absValueMax >= 1000000)
                {
                    axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
                }
                return;
            }

            var minDigitsPrecision = 0;

            if (absValueMax < 0.02)
            {
                axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }
            else if (absValueMax < 0.2)
            {
                minDigitsPrecision = 2;
                axisInfo.StringFormat = "0.00";
            }
            else if (absValueMax < 2)
            {
                minDigitsPrecision = 1;
                axisInfo.StringFormat = "0.0";
            }
            else if (absValueMax >= 1000000)
            {
                axisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }

            if (absValueMax - absValueMin < 1E-05)
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
                var minDigitsRangeBased = (int)Math.Ceiling(-Math.Log10(absValueMax - absValueMin));

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
