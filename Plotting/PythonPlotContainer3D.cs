using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OxyPlot.Series;

namespace MSFileInfoScanner.Plotting
{
    /// <summary>
    /// Python data container for 3D data
    /// </summary>
    internal class PythonPlotContainer3D : PythonPlotContainer
    {
        // Ignore Spelling: autoscale, gridline, png

        /// <summary>
        /// Keys are charge states, values are a list of data points for each charge state
        /// </summary>
        public Dictionary<int, List<ScatterPoint>> PointsByCharge { get; }

        /// <summary>
        /// Minimum intensity when mapping intensity to color
        /// </summary>
        public float ColorScaleMinIntensity { get; set; }

        /// <summary>
        /// Maximum intensity when mapping intensity to color
        /// </summary>
        public float ColorScaleMaxIntensity { get; set; }

        /// <summary>
        /// Marker size
        /// </summary>
        public double MarkerSize { get; set; }

        /// <summary>
        /// Maximum charge state to display on the plot
        /// </summary>
        public int MaxChargeToPlot { get; set; } = 6;

        /// <summary>
        /// Intensity options
        /// </summary>
        public AxisInfo ZAxisInfo { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="plotTitle"></param>
        /// <param name="xAxisTitle"></param>
        /// <param name="yAxisTitle"></param>
        /// <param name="zAxisTitle"></param>
        /// <param name="writeDebug"></param>
        /// <param name="dataSource"></param>
        public PythonPlotContainer3D(
            string plotTitle = "Undefined", string xAxisTitle = "X", string yAxisTitle = "Y", string zAxisTitle = "Z",
            bool writeDebug = false, string dataSource = "") : base(plotTitle, xAxisTitle, yAxisTitle, writeDebug, dataSource)
        {
            PointsByCharge = new Dictionary<int, List<ScatterPoint>>();
            ClearData();

            ZAxisInfo = new AxisInfo(zAxisTitle);
        }

        /// <summary>
        /// Save the plot, along with any defined annotations, to a png file
        /// </summary>
        /// <param name="pngFile">Output PNG file</param>
        /// <param name="width">PNG file width, in pixels</param>
        /// <param name="height">PNG file height, in pixels</param>
        /// <param name="resolution">Image resolution, in dots per inch</param>
        public override bool SaveToPNG(FileInfo pngFile, int width, int height, int resolution)
        {
            if (pngFile == null)
                throw new ArgumentNullException(nameof(pngFile), "PNG file instance cannot be blank");

            var exportFile = MSFileInfoScanner.GetFileInfo(Path.ChangeExtension(pngFile.FullName, null) + TMP_FILE_SUFFIX + ".txt");

            try
            {
                using var writer = new StreamWriter(new FileStream(exportFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

                // Plot options: set of square brackets with semicolon separated key/value pairs
                writer.WriteLine("[" + GetPlotOptions() + "]");

                // Column options: semicolon separated key/value pairs for each column, with options for each column separated by a tab
                // Note: these options aren't actually used by the Python plotting library

                // Example XAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1
                // Example YAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1
                // Example ZAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1;MarkerSize=0.6;ColorScaleMinIntensity=400.3625;ColorScaleMaxIntensity=9152594
                // Example Charge options: MaxChargeToPlot=6

                var additionalZAxisOptions = new List<string> {
                    "MarkerSize=" + MarkerSize
                };

                if (ColorScaleMinIntensity > 0 || ColorScaleMaxIntensity > 0)
                {
                    additionalZAxisOptions.Add("ColorScaleMinIntensity=" + ColorScaleMinIntensity);
                    additionalZAxisOptions.Add("ColorScaleMaxIntensity=" + ColorScaleMaxIntensity);
                }

                var charges = PointsByCharge.Keys.ToList();
                charges.Sort();

                var includeCharge = charges.Count > 1;

                var columnData = new List<string> {
                    XAxisInfo.GetOptions(),
                    YAxisInfo.GetOptions(),
                    ZAxisInfo.GetOptions(additionalZAxisOptions)};

                if (includeCharge && MaxChargeToPlot > 0)
                {
                    columnData.Add(string.Format("MaxChargeToPlot={0}", MaxChargeToPlot));
                }

                writer.WriteLine(string.Join("\t", columnData));

                // Column names
                columnData.Clear();
                columnData.Add(XAxisInfo.Title);
                columnData.Add(YAxisInfo.Title);
                columnData.Add(ZAxisInfo.Title);

                if (includeCharge)
                {
                    columnData.Add("Charge");
                }

                writer.WriteLine(string.Join("\t", columnData));

                // Data, by charge state
                foreach (var charge in charges)
                {
                    foreach (var dataPoint in PointsByCharge[charge])
                    {
                        if (includeCharge)
                            writer.WriteLine(dataPoint.X + "\t" + dataPoint.Y + "\t" + dataPoint.Value + "\t" + charge);
                        else
                            writer.WriteLine(dataPoint.X + "\t" + dataPoint.Y + "\t" + dataPoint.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error exporting data in SaveToPNG", ex);
                return false;
            }

            if (string.IsNullOrWhiteSpace(PythonPath) && !PythonInstalled)
            {
                NotifyPythonNotFound("Cannot export plot data for PNG creation");
                return false;
            }

            try
            {
                var success = GeneratePlotsWithPython(exportFile, pngFile.Directory);

                if (DeleteTempFiles)
                {
                    exportFile.Delete();
                }

                return success;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating 3D plot with Python using " + exportFile.Name, ex);
                return false;
            }
        }

        public void ClearData()
        {
            PointsByCharge.Clear();
            mSeriesCount = 0;
        }

        public void AddData(List<ScatterPoint> dataPoints, int charge)
        {
            if (dataPoints.Count == 0)
            {
                return;
            }

            if (PointsByCharge.ContainsKey(charge))
                PointsByCharge.Remove(charge);

            PointsByCharge.Add(charge, dataPoints);
            mSeriesCount = PointsByCharge.Count;
        }
    }
}
