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

        public Dictionary<int, List<ScatterPoint>> PointsByCharge { get; }

        public float ColorScaleMinIntensity { get; set; }
        public float ColorScaleMaxIntensity { get; set; }
        public double MarkerSize { get; set; }

        public AxisInfo ZAxisInfo { get; }

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
        /// <remarks></remarks>
        public override bool SaveToPNG(FileInfo pngFile, int width, int height, int resolution)
        {
            if (pngFile == null)
                throw new ArgumentNullException(nameof(pngFile), "PNG file instance cannot be blank");

            var exportFile = new FileInfo(Path.ChangeExtension(pngFile.FullName, null) + TMP_FILE_SUFFIX + ".txt");

            try
            {
                using (var writer = new StreamWriter(new FileStream(exportFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    // Plot options: set of square brackets with semicolon separated key/value pairs
                    writer.WriteLine("[" + GetPlotOptions() + "]");

                    // Column options: semicolon separated key/value pairs for each column, with options for each column separated by a tab
                    // Note: these options aren't actually used by the Python plotting library

                    // Example XAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1
                    // Example YAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1
                    // Example ZAxis options: Autoscale=true;StringFormat=#,##0;MinorGridlineThickness=1;MarkerSize=0.6;ColorScaleMinIntensity=400.3625;ColorScaleMaxIntensity=9152594

                    var additionalZAxisOptions = new List<string> {
                        "MarkerSize=" + MarkerSize
                    };

                    if (ColorScaleMinIntensity > 0 || ColorScaleMaxIntensity > 0)
                    {
                        additionalZAxisOptions.Add("ColorScaleMinIntensity=" + ColorScaleMinIntensity);
                        additionalZAxisOptions.Add("ColorScaleMaxIntensity=" + ColorScaleMaxIntensity);
                    }
                    writer.WriteLine(XAxisInfo.GetOptions() + "\t" + YAxisInfo.GetOptions() + "\t" + ZAxisInfo.GetOptions(additionalZAxisOptions));

                    var charges = PointsByCharge.Keys.ToList();
                    charges.Sort();

                    var includeCharge = charges.Count > 1;

                    // Column names
                    if (includeCharge)
                        writer.WriteLine(XAxisInfo.Title + "\t" + YAxisInfo.Title + "\t" + ZAxisInfo.Title + "\t" + "Charge");
                    else
                        writer.WriteLine(XAxisInfo.Title + "\t" + YAxisInfo.Title + "\t" + ZAxisInfo.Title);

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
