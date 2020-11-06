using System;
using System.Collections.Generic;

namespace MSFileInfoScanner.Plotting
{
    class AxisInfo
    {
        // Ignore Spelling: Autoscale, Gridline

        public const string DEFAULT_AXIS_LABEL_FORMAT = "#,##0";

        public const string EXPONENTIAL_FORMAT = "0.00E+00";

        public bool AutoScale { get; set; }

        public double Maximum { get; set; }

        public double Minimum { get; set; }

        public double MajorStep { get; set; }

        public double MinorGridlineThickness { get; set; }

        public string StringFormat { get; set; }

        public string Title { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public AxisInfo(string title = "Undefined")
        {
            AutoScale = true;
            MajorStep = double.NaN;
            MinorGridlineThickness = 1;
            StringFormat = DEFAULT_AXIS_LABEL_FORMAT;
            Title = title;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public AxisInfo(double majorStep, double minorGridlineThickness, string title = "Undefined")
        {
            AutoScale = true;
            MajorStep = majorStep;
            MinorGridlineThickness = minorGridlineThickness;
            StringFormat = DEFAULT_AXIS_LABEL_FORMAT;
            Title = title;
        }

        /// <summary>
        /// Get options as a semi colon separated list of key-value pairs
        /// </summary>
        public string GetOptions()
        {
            return GetOptions(new List<string>());
        }

        /// <summary>
        /// Get options as a semi colon separated list of key-value pairs
        /// </summary>
        public string GetOptions(List<string> additionalOptions)
        {
            var options = new List<string>();

            if (AutoScale)
            {
                options.Add("Autoscale=true");
            }
            else
            {
                options.Add("Autoscale=false");
                options.Add("Minimum=" + Minimum);
                options.Add("Maximum=" + Maximum);
            }

            options.Add("StringFormat=" + StringFormat);

            if (!double.IsNaN(MinorGridlineThickness))
                options.Add("MinorGridlineThickness=" + MinorGridlineThickness);

            if (!double.IsNaN(MajorStep))
                options.Add("MajorStep=" + MajorStep);

            if (additionalOptions?.Count > 0)
                options.AddRange(additionalOptions);

            return string.Join(";", options);
        }

        /// <summary>
        /// Set the axis range
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <remarks>Set min and max to 0 to enable auto scaling</remarks>
        public void SetRange(double min, double max)
        {
            if (Math.Abs(min) < float.Epsilon && Math.Abs(max) < float.Epsilon)
            {
                AutoScale = true;
                return;
            }

            AutoScale = false;

            Minimum = min;
            Maximum = max;
        }
    }
}
