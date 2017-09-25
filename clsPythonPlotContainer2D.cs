using System;
using System.Collections.Generic;
using System.IO;
using OxyPlot;

namespace MSFileInfoScanner
{
    /// <summary>
    /// Python data container for 2D data
    /// </summary>
    internal class clsPythonPlotContainer2D : clsPythonPlotContainer
    {

        public List<DataPoint> Data { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="plotTitle"></param>
        /// <param name="xAxisTitle"></param>
        /// <param name="yAxisTitle"></param>
        /// <param name="writeDebug"></param>
        /// <param name="dataSource"></param>
        public clsPythonPlotContainer2D(
            string plotTitle = "Undefined", string xAxisTitle = "X", string yAxisTitle = "Y",
            bool writeDebug = false, string dataSource = "") : base(plotTitle, xAxisTitle, yAxisTitle, writeDebug, dataSource)
        {
            Data = new List<DataPoint>();
            ClearData();
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

            FileInfo exportFile;

            try
            {
                exportFile = new FileInfo(Path.ChangeExtension(pngFilePath, null) + "_TmpExportData.txt");

                using (var writer = new StreamWriter(new FileStream(exportFile.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)))
                {
                    // Plot options: set of square brackets with semicolon separated key/value pairs
                    writer.WriteLine("[" + GetPlotOptions() + "]");

                    // Column options: semicolon separated key/value pairs for each column, e.g.
                    // Autoscale=false;Minimum=0;Maximum=4295028224;StringFormat=#,##0;MinorGridlineThickness=0;MajorStep=1
                    writer.WriteLine(XAxisInfo.GetOptions() + "\t" + YAxisInfo.GetOptions());

                    // Column names
                    writer.WriteLine(XAxisInfo.Title + "\t" + YAxisInfo.Title);

                    // Data
                    foreach (var dataPoint in Data)
                    {
                        writer.WriteLine(dataPoint.X + "\t" + dataPoint.Y);
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

                Console.WriteLine("ToDo: generate 2D plot with " + cmdLine);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error creating 2D plot with Python using " + exportFile.Name, ex);
            }

        }

        public void ClearData()
        {
            Data.Clear();
            mSeriesCount = 0;
        }

        public void SetData(List<DataPoint> objPoints)
        {
            if (objPoints.Count == 0)
            {
                ClearData();
                return;
            }

            Data = objPoints;
            mSeriesCount = 1;
        }
    }
}
