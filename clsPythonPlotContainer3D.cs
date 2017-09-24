using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OxyPlot.Series;

namespace MSFileInfoScanner
{
    /// <summary>
    /// Python data container for 3D data
    /// </summary>
    internal class clsPythonPlotContainer3D : clsPythonPlotContainer
    {

        public Dictionary<int, List<ScatterPoint>> PointsByCharge { get; }

        public float ColorScaleMinIntensity { get; set; }
        public float ColorScaleMaxIntensity { get; set; }
        public double MarkerSize { get; set; }

        public clsAxisInfo ZAxisInfo { get; }

        public clsPythonPlotContainer3D(
            string plotTitle = "Undefined", string xAxisTitle = "X", string yAxisTitle = "Y", string zAxisTitle = "Z",
            bool writeDebug = false, string dataSource = "") : base(plotTitle, xAxisTitle, yAxisTitle, writeDebug, dataSource)
        {
            PointsByCharge = new Dictionary<int, List<ScatterPoint>>();
            ClearData();

            ZAxisInfo = new clsAxisInfo(zAxisTitle);
        }

        /// <summary>
        /// Save the plot, along with any defined annotations, to a png file
        /// </summary>
        /// <param name="pngFilePath">Output file path</param>
        /// <param name="width">PNG file width, in pixels</param>
        /// <param name="height">PNG file height, in pixels</param>
        /// <param name="resolution">Image resolution, in dots per inch</param>
        /// <remarks></remarks>
        public override void SaveToPNG(string pngFilePath, int width, int height, int resolution)
        {
            if (string.IsNullOrWhiteSpace(pngFilePath))
                throw new ArgumentException("PNG file path cannot be blank", nameof(pngFilePath));

            var exportFile = new FileInfo("TmpExport_" + Path.ChangeExtension(pngFilePath, null) + "_data.txt");

            try
            {

                using (var writer = new StreamWriter(new FileStream(exportFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    writer.WriteLine("Charge\t" + XAxisInfo.Title + "\t" + YAxisInfo.Title + "\t" + ZAxisInfo.Title);

                    var additionalOptions = new List<string> {
                        "MarkerSize=" + MarkerSize
                    };

                    if (ColorScaleMinIntensity > 0 || ColorScaleMaxIntensity > 0)
                    {
                        additionalOptions.Add("ColorScaleMinIntensity=" + ColorScaleMinIntensity);
                        additionalOptions.Add("ColorScaleMaxIntensity=" + ColorScaleMaxIntensity);
                    }
                    writer.WriteLine(XAxisInfo.GetOptions() + "\t" + YAxisInfo.GetOptions() + "\t" + ZAxisInfo.GetOptions(additionalOptions));

                    var charges = PointsByCharge.Keys.ToList();
                    charges.Sort();

                    foreach (var charge in charges)
                    {
                        foreach (var dataPoint in PointsByCharge[charge])
                        {
                            writer.WriteLine(charge + "\t" + dataPoint.X + "\t" + dataPoint.Y + "\t" + dataPoint.Value);
                        }
                    }
                }

                if (DeleteTempFiles)
                {
                    exportFile.Delete();
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error exporting data in SaveToPNG", ex);
                return;
            }

            if (string.IsNullOrWhiteSpace(PythonPath) && !FindPython())
            {
                OnErrorEvent("Cannot export plot data for PNG creation; Python not found");
                return;
            }

            try
            {
                var args = "";

                var cmdLine = string.Format("{0} {1} {2}", PythonPath, PRISM.clsPathUtils.PossiblyQuotePath(exportFile.FullName), args);

                Console.WriteLine("ToDo: generate 3D plot with " + cmdLine);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating 3D plot with Python using " + exportFile.Name, ex);
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
