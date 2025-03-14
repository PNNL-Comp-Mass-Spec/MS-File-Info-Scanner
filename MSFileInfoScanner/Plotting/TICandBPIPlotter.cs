﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OxyPlot;
using OxyPlot.Series;
using PRISM;
using ThermoFisher.CommonCore.Data.Business;

namespace MSFileInfoScanner.Plotting
{
    // ReSharper disable once IdentifierTypo
#pragma warning disable VSSpell001 // Spell Check
    /// <summary>
    /// TIC and BPI plotter
    /// </summary>
    public class TICandBPIPlotter : EventNotifier
#pragma warning restore VSSpell001 // Spell Check
    {
        // Ignore Spelling: OxyPlot, Zeroes

        // ReSharper disable InconsistentNaming
        // ReSharper disable IdentifierTypo

        /// <summary>
        /// Output file types
        /// </summary>
        public enum OutputFileTypes
        {
            /// <summary>
            /// Total Ion Chromatogram
            /// </summary>
            TIC = 0,

            /// <summary>
            /// Base Peak Intensity Chromatogram for MS1 spectra
            /// </summary>
            BPIMS = 1,

            /// <summary>
            /// Base Peak Intensity Chromatogram for MSn spectra
            /// </summary>
            BPIMSn = 2
        }

        // ReSharper restore IdentifierTypo
        // ReSharper restore InconsistentNaming

        private struct OutputFileInfoType
        {
            public OutputFileTypes FileType;
            public string FileName;
            public string FilePath;

            public readonly override string ToString()
            {
                return string.Format("{0,-15} {1}", FileType + " file:", FileName);
            }
        }

        /// <summary>
        /// Data stored in mTIC will get plotted for all scans, both MS and MS/MS
        /// </summary>
        private ChromatogramInfo mTIC;

        /// <summary>
        /// Data stored in mBPI will be plotted separately for MS and MS/MS spectra
        /// </summary>
        private ChromatogramInfo mBPI;

        private readonly List<OutputFileInfoType> mRecentFiles;

        private readonly string mDataSource;

        /// <summary>
        /// When true, create a debug file that tracks processing steps
        /// </summary>
        private readonly bool mWriteDebug;

        /// <summary>
        /// When true, auto-define the Y-axis range for the BPI
        /// </summary>
        public bool BPIAutoMinMaxY { get; set; }

        /// <summary>
        /// Plot title abbreviation
        /// </summary>
        /// <remarks>This name is included in the plot title and is used when creating the .png file</remarks>
        public string BPIPlotAbbrev { get; set; } = "BPI";

        // ReSharper disable IdentifierTypo

        /// <summary>
        /// BPI X-axis label
        /// </summary>
        public string BPIXAxisLabel { get; set; } = "LC Scan Number";

        /// <summary>
        /// When true, the BPI X-axis is time, in minutes
        /// </summary>
        public bool BPIXAxisIsTimeMinutes { get; set; }

        /// <summary>
        /// BPI Y-axis label
        /// </summary>
        public string BPIYAxisLabel { get; set; } = "Intensity";

        /// <summary>
        /// When true, use exponential notation for the BPI Y-axis
        /// </summary>
        public bool BPIYAxisExponentialNotation { get; set; } = true;

        // ReSharper restore IdentifierTypo

        /// <summary>
        /// Number of scans in the BPI chromatogram
        /// </summary>
        public int CountBPI => mBPI.ScanCount;

        /// <summary>
        /// Number of scans in the TIC
        /// </summary>
        public int CountTIC => mTIC.ScanCount;

        /// <summary>
        /// When true, delete temporary files after creating the plots
        /// </summary>
        public bool DeleteTempFiles { get; set; }

        /// <summary>
        /// Device type of the data in mTIC or mBPI
        /// </summary>
        public Device DeviceType { get; set; }

        /// <summary>
        /// When true, use Python script MSFileInfoScanner_Plotter.py instead of OxyPlot to create the plots
        /// </summary>
        public bool PlotWithPython { get; set; }

        /// <summary>
        /// When true, remove zero-intensity scans from the beginning and ending of the chromatograms
        /// </summary>
        public bool RemoveZeroesFromEnds { get; set; }

        /// <summary>
        /// When true, auto-define the Y-axis range for the TIC
        /// </summary>
        public bool TICAutoMinMaxY { get; set; }

        /// <summary>
        /// TIC title abbreviation
        /// </summary>
        /// <remarks>This name is included in the plot title and is used when creating the .png file</remarks>
        public string TICPlotAbbrev { get; set; } = "TIC";

        // ReSharper disable IdentifierTypo

        /// <summary>
        /// TIC X-axis label
        /// </summary>
        public string TICXAxisLabel { get; set; } = "LC Scan Number";

        /// <summary>
        /// When true, the TIC X-axis is time, in minutes
        /// </summary>
        public bool TICXAxisIsTimeMinutes { get; set; } = false;

        /// <summary>
        /// TIC Y-axis label
        /// </summary>
        public string TICYAxisLabel { get; set; } = "Intensity";

        /// <summary>
        /// When true, use exponential notation for the TIC Y-axis
        /// </summary>
        public bool TICYAxisExponentialNotation { get; set; } = true;

        // ReSharper restore IdentifierTypo

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataSource">Data source</param>
        /// <param name="writeDebug">When true, create a debug file that tracks processing steps</param>
        // ReSharper disable once IdentifierTypo
#pragma warning disable VSSpell001 // Spell Check
        public TICandBPIPlotter(string dataSource = "", bool writeDebug = false)
#pragma warning restore VSSpell001 // Spell Check
        {
            mRecentFiles = new List<OutputFileInfoType>();

            mDataSource = dataSource;
            mWriteDebug = writeDebug;

            DeviceType = Device.None;

            Reset();
        }

        /// <summary>
        /// Add a data point to the BPI and TIC
        /// </summary>
        /// <param name="scanNumber">Scan number</param>
        /// <param name="msLevel">MS Level</param>
        /// <param name="scanTimeMinutes">Scan time, in minutes</param>
        /// <param name="bpi">BPI</param>
        /// <param name="tic">TIC</param>
        public void AddData(int scanNumber, int msLevel, float scanTimeMinutes, double bpi, double tic)
        {
            mBPI.AddPoint(scanNumber, msLevel, scanTimeMinutes, bpi);
            mTIC.AddPoint(scanNumber, msLevel, scanTimeMinutes, tic);
        }

        /// <summary>
        /// Add a data point to the BPI
        /// </summary>
        /// <param name="scanNumber">Scan number</param>
        /// <param name="msLevel">MS Level</param>
        /// <param name="scanTimeMinutes">Scan time, in minutes</param>
        /// <param name="bpi">BPI</param>
        public void AddDataBPIOnly(int scanNumber, int msLevel, float scanTimeMinutes, double bpi)
        {
            mBPI.AddPoint(scanNumber, msLevel, scanTimeMinutes, bpi);
        }

        /// <summary>
        /// Add a data point to the TIC
        /// </summary>
        /// <param name="scanNumber">Scan number</param>
        /// <param name="msLevel">MS Level</param>
        /// <param name="scanTimeMinutes">Scan time, in minutes</param>
        /// <param name="tic">TIC</param>
        public void AddDataTICOnly(int scanNumber, int msLevel, float scanTimeMinutes, double tic)
        {
            mTIC.AddPoint(scanNumber, msLevel, scanTimeMinutes, tic);
        }

        private void AddRecentFile(string filePath, OutputFileTypes fileType)
        {
            var outputFileInfo = default(OutputFileInfoType);

            outputFileInfo.FileType = fileType;
            outputFileInfo.FileName = Path.GetFileName(filePath);
            outputFileInfo.FilePath = filePath;

            mRecentFiles.Add(outputFileInfo);
        }

        private void AddOxyPlotSeries(PlotModel myPlot, IReadOnlyCollection<DataPoint> points)
        {
            // Generate a black curve with no symbols
            var series = new LineSeries();

            if (points.Count == 0)
            {
                return;
            }

            var symbolType = points.Count == 1 ? MarkerType.Circle : MarkerType.None;

            series.Color = OxyColors.Black;
            series.StrokeThickness = 1;
            series.MarkerType = symbolType;

            if (points.Count == 1)
            {
                series.MarkerSize = 8;
                series.MarkerFill = OxyColors.DarkRed;
            }

            series.Points.AddRange(points);

            myPlot.Series.Add(series);
        }

        /// <summary>
        /// Returns the file name of the recently saved file of the given type
        /// </summary>
        /// <remarks>The list of recent files gets cleared each time you call SaveTICAndBPIPlotFiles() or Reset()</remarks>
        /// <param name="fileType">File type to find</param>
        /// <returns>File name if found; empty string if this file type was not saved</returns>
        public string GetRecentFileInfo(OutputFileTypes fileType)
        {
            for (var index = 0; index < mRecentFiles.Count; index++)
            {
                if (mRecentFiles[index].FileType == fileType)
                {
                    return mRecentFiles[index].FileName;
                }
            }
            return string.Empty;
        }

        // ReSharper disable once UnusedMember.Global

        /// <summary>
        /// Returns the file name and path of the recently saved file of the given type
        /// </summary>
        /// <remarks>The list of recent files gets cleared each time you call SaveTICAndBPIPlotFiles() or Reset()</remarks>
        /// <param name="fileType">File type to find</param>
        /// <param name="fileName">File name (output)</param>
        /// <param name="filePath">File Path (output)</param>
        /// <returns>True if a match was found; otherwise returns false</returns>
        public bool GetRecentFileInfo(OutputFileTypes fileType, out string fileName, out string filePath)
        {
            for (var index = 0; index < mRecentFiles.Count; index++)
            {
                if (mRecentFiles[index].FileType == fileType)
                {
                    fileName = mRecentFiles[index].FileName;
                    filePath = mRecentFiles[index].FilePath;
                    return true;
                }
            }

            fileName = string.Empty;
            filePath = string.Empty;

            return false;
        }

        private PlotContainerBase InitializePlot(
            ChromatogramInfo chromatogramData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation,
            bool xAxisIsTimeMinutes)
        {
            if (PlotWithPython)
            {
                return InitializePythonPlot(chromatogramData, plotTitle, msLevelFilter, xAxisLabel, yAxisLabel, autoMinMaxY, yAxisExponentialNotation, xAxisIsTimeMinutes);
            }

            return InitializeOxyPlot(chromatogramData, plotTitle, msLevelFilter, xAxisLabel, yAxisLabel, autoMinMaxY, yAxisExponentialNotation, xAxisIsTimeMinutes);
        }

        /// <summary>
        /// Plots a BPI or TIC chromatogram
        /// </summary>
        /// <param name="chromatogramData">Data to display</param>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="xAxisLabel">X-axis label</param>
        /// <param name="yAxisLabel">Y-axis label</param>
        /// <param name="autoMinMaxY">When true, auto define the Y-axis range</param>
        /// <param name="yAxisExponentialNotation">When true, label the Y-axis using exponential notation</param>
        /// <param name="xAxisIsTimeMinutes">If true, <see cref="ChromatogramDataPoint.TimeMinutes"/> is used for the X-axis instead of <see cref="ChromatogramDataPoint.ScanNum"/></param>
        /// <returns>OxyPlot PlotContainer</returns>
        private PlotContainer InitializeOxyPlot(
            ChromatogramInfo chromatogramData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation,
            bool xAxisIsTimeMinutes)
        {
            double minScan = int.MaxValue;
            double maxScan = 0;
            double scanTimeMax = 0;
            double maxIntensity = 0;

            Func<ChromatogramDataPoint, double> xAxisValueAccessor = point => point.ScanNum;

            if (xAxisIsTimeMinutes)
                xAxisValueAccessor = point => point.TimeMinutes;

            // Instantiate the list to track the data points
            var points = new List<DataPoint>();

            foreach (var dataPoint in chromatogramData.Scans)
            {
                if (msLevelFilter != 0 && dataPoint.MSLevel != msLevelFilter &&
                    !(msLevelFilter == 2 && dataPoint.MSLevel >= 2))
                {
                    continue;
                }

                points.Add(new DataPoint(xAxisValueAccessor(dataPoint), dataPoint.Intensity));

                if (dataPoint.TimeMinutes > scanTimeMax)
                {
                    scanTimeMax = dataPoint.TimeMinutes;
                }

                if (xAxisValueAccessor(dataPoint) < minScan)
                {
                    minScan = xAxisValueAccessor(dataPoint);
                }

                if (xAxisValueAccessor(dataPoint) > maxScan)
                {
                    maxScan = xAxisValueAccessor(dataPoint);
                }

                if (dataPoint.Intensity > maxIntensity)
                {
                    maxIntensity = dataPoint.Intensity;
                }
            }

            if (points.Count == 0)
            {
                // Nothing to plot
                var emptyContainer = new PlotContainer(new PlotModel(), mWriteDebug, mDataSource);
                emptyContainer.WriteDebugLog("points.Count == 0 in InitializeOxyPlot for plot " + plotTitle);
                return emptyContainer;
            }

            if (maxScan < 20 && xAxisIsTimeMinutes)
            {
                // Add 1% (for Oxyplot) so the max value is not at the limit of the plot area
                maxScan *= 1.01;
            }
            else if (maxScan < 200)
            {
                // Add one (for Oxyplot) so the max value is not at the limit of the plot area
                maxScan++;
            }
            else
            {
                // Round maxScan down to the nearest multiple of 10
                maxScan = (int)Math.Ceiling(maxScan / 10.0) * 10;
            }

            // Multiply maxIntensity by 2% and then round up to the nearest integer
            maxIntensity = Math.Ceiling(maxIntensity * 1.02);

            var myPlot = OxyPlotUtilities.GetBasicPlotModel(plotTitle, xAxisLabel, yAxisLabel);

            if (yAxisExponentialNotation)
            {
                myPlot.Axes[1].StringFormat = AxisInfo.EXPONENTIAL_FORMAT;
            }

            AddOxyPlotSeries(myPlot, points);

            // Update the axis format codes if the data values are small or the range of data is small
            var xVals = (from item in points select item.X).ToList();
            OxyPlotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[0], xVals, true);

            var yVals = (from item in points select item.Y).ToList();
            OxyPlotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[1], yVals, false);

            var plotContainer = new PlotContainer(myPlot, mWriteDebug, mDataSource)
            {
                FontSizeBase = PlotContainer.DEFAULT_BASE_FONT_SIZE
            };

            plotContainer.WriteDebugLog(string.Format("Instantiated plotContainer for plot {0}: {1} data points", plotTitle, points.Count));

            // Possibly add a label showing the maximum elution time
            if (scanTimeMax > 0)
            {
                string caption;

                if (scanTimeMax < 2)
                {
                    caption = Math.Round(scanTimeMax, 2).ToString("0.00") + " minutes";
                }
                else if (scanTimeMax < 10)
                {
                    caption = Math.Round(scanTimeMax, 1).ToString("0.0") + " minutes";
                }
                else
                {
                    caption = Math.Round(scanTimeMax, 0).ToString("0") + " minutes";
                }

                plotContainer.AnnotationBottomRight = caption;

                // Alternative method is to add a TextAnnotation, but these are inside the main plot area
                // and are tied to a data point
                //
                // var scanTimeMaxText = new OxyPlot.Annotations.TextAnnotation
                // {
                //     TextRotation = 0,
                //     Text = caption,
                //     Stroke = OxyColors.Black,
                //     StrokeThickness = 2,
                //     FontSize = PlotContainer.DEFAULT_BASE_FONT_SIZE
                // };
                //
                // scanTimeMaxText.TextPosition = new DataPoint(maxScan, 0);
                // myPlot.Annotations.Add(scanTimeMaxText);

            }

            // Override the auto-computed X-axis range
            if (Math.Abs(minScan - maxScan) < float.Epsilon)
            {
                myPlot.Axes[0].Minimum = minScan - 1;
                myPlot.Axes[0].Maximum = minScan + 1;
            }
            else
            {
                myPlot.Axes[0].Minimum = 0;

                if (maxScan == 0)
                {
                    myPlot.Axes[0].Maximum = 1;
                }
                else
                {
                    myPlot.Axes[0].Maximum = maxScan;
                }
            }

            if (myPlot.Axes[0].Maximum - myPlot.Axes[0].Minimum <= 1.1)
            {
                // Show decimal places on the plot axis if the range is small
                myPlot.Axes[0].StringFormat = "#0.0#";
            }
            else if (myPlot.Axes[0].Maximum - myPlot.Axes[0].Minimum <= 10)
            {
                // Make sure the major step is 1 if the range is less than 10
                myPlot.Axes[0].MajorStep = 1;
            }

            // Assure that we don't see ticks between scan numbers
            OxyPlotUtilities.ValidateMajorStep(myPlot.Axes[0]);

            // Override the auto-computed Y-axis range
            if (autoMinMaxY)
            {
                // Auto scale
            }
            else
            {
                myPlot.Axes[1].Minimum = 0;
                myPlot.Axes[1].Maximum = maxIntensity;
            }

            // Hide the legend
            myPlot.IsLegendVisible = false;

            return plotContainer;
        }

        /// <summary>
        /// Plots a BPI or TIC chromatogram
        /// </summary>
        /// <param name="chromatogramData">Data to display</param>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="xAxisLabel">X-axis label</param>
        /// <param name="yAxisLabel">Y-axis label</param>
        /// <param name="autoMinMaxY">When true, auto define the Y-axis range</param>
        /// <param name="yAxisExponentialNotation">When true, label the Y-axis using exponential notation</param>
        /// <param name="xAxisIsTimeMinutes">If true, <see cref="ChromatogramDataPoint.TimeMinutes"/> is used for the X-axis instead of <see cref="ChromatogramDataPoint.ScanNum"/></param>
        /// <returns>Python PlotContainer</returns>
        private PythonPlotContainer InitializePythonPlot(
            ChromatogramInfo chromatogramData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation,
            bool xAxisIsTimeMinutes)
        {
            double scanTimeMax = 0;
            double maxIntensity = 0;

            // Instantiate the list to track the data points
            var points = new List<DataPoint>();

            Func<ChromatogramDataPoint, double> xAxisValueAccessor = point => point.ScanNum;

            if (xAxisIsTimeMinutes)
                xAxisValueAccessor = point => point.TimeMinutes;

            foreach (var dataPoint in chromatogramData.Scans)
            {
                if (msLevelFilter != 0 && dataPoint.MSLevel != msLevelFilter &&
                    !(msLevelFilter == 2 && dataPoint.MSLevel >= 2))
                {
                    continue;
                }

                points.Add(new DataPoint(xAxisValueAccessor(dataPoint), dataPoint.Intensity));

                if (dataPoint.TimeMinutes > scanTimeMax)
                {
                    scanTimeMax = dataPoint.TimeMinutes;
                }

                if (dataPoint.Intensity > maxIntensity)
                {
                    maxIntensity = dataPoint.Intensity;
                }
            }

            if (points.Count == 0)
            {
                // Nothing to plot
                var emptyContainer = new PythonPlotContainer2D();
                return emptyContainer;
            }

            var plotContainer = new PythonPlotContainer2D(plotTitle, xAxisLabel, yAxisLabel)
            {
                DeleteTempFiles = DeleteTempFiles
            };

            if (yAxisExponentialNotation)
            {
                plotContainer.YAxisInfo.StringFormat = AxisInfo.EXPONENTIAL_FORMAT;
            }

            plotContainer.SetData(points);

            // Update the axis format codes if the data values are small or the range of data is small

            // Assume the X-axis is plotting integers
            var xVals = (from item in points select item.X).ToList();
            PlotUtilities.GetAxisFormatInfo(xVals, true, plotContainer.XAxisInfo);

            // Assume the Y-axis is plotting doubles
            var yVals = (from item in points select item.Y).ToList();
            PlotUtilities.GetAxisFormatInfo(yVals, false, plotContainer.YAxisInfo);

            // Possibly add a label showing the maximum elution time
            if (scanTimeMax > 0)
            {
                string caption;

                if (scanTimeMax < 2)
                {
                    caption = Math.Round(scanTimeMax, 2).ToString("0.00") + " minutes";
                }
                else if (scanTimeMax < 10)
                {
                    caption = Math.Round(scanTimeMax, 1).ToString("0.0") + " minutes";
                }
                else
                {
                    caption = Math.Round(scanTimeMax, 0).ToString("0") + " minutes";
                }

                plotContainer.AnnotationBottomRight = caption;
            }

            // Override the auto-computed Y-axis range
            if (autoMinMaxY)
            {
                // Auto scale
            }
            else
            {
                plotContainer.XAxisInfo.SetRange(0, maxIntensity);
            }

            return plotContainer;
        }

        private void RemoveZeroesAtFrontAndBack(ChromatogramInfo chromatogramInfo)
        {
            const int MAX_POINTS_TO_CHECK = 100;
            var pointsChecked = 0;

            // See if the last few values are zero, but the data before them is non-zero
            // If this is the case, remove the final entries

            var indexNonZeroValue = -1;
            var zeroPointCount = 0;

            for (var index = chromatogramInfo.ScanCount - 1; index >= 0; index += -1)
            {
                if (Math.Abs(chromatogramInfo.GetDataPoint(index).Intensity) < float.Epsilon)
                {
                    zeroPointCount++;
                }
                else
                {
                    indexNonZeroValue = index;
                    break;
                }
                pointsChecked++;

                if (pointsChecked >= MAX_POINTS_TO_CHECK)
                    break;
            }

            if (zeroPointCount > 0 && indexNonZeroValue >= 0)
            {
                chromatogramInfo.RemoveRange(indexNonZeroValue + 1, zeroPointCount);
            }

            // Now check the first few values
            indexNonZeroValue = -1;
            zeroPointCount = 0;

            for (var index = 0; index < chromatogramInfo.ScanCount; index++)
            {
                if (Math.Abs(chromatogramInfo.GetDataPoint(index).Intensity) < float.Epsilon)
                {
                    zeroPointCount++;
                }
                else
                {
                    indexNonZeroValue = index;
                    break;
                }
                pointsChecked++;

                if (pointsChecked >= MAX_POINTS_TO_CHECK)
                    break;
            }

            if (zeroPointCount > 0 && indexNonZeroValue >= 0)
            {
                chromatogramInfo.RemoveRange(0, indexNonZeroValue);
            }
        }

        /// <summary>
        /// Clear BPI and TIC data
        /// </summary>
        public void Reset()
        {
            if (mBPI == null)
            {
                mBPI = new ChromatogramInfo();
                mTIC = new ChromatogramInfo();
            }
            else
            {
                mBPI.Initialize();
                mTIC.Initialize();
            }

            mRecentFiles.Clear();
        }

        /// <summary>
        /// Save BPI and TIC plots
        /// </summary>
        /// <param name="datasetName">Dataset name</param>
        /// <param name="outputDirectory">Output directory</param>
        /// <returns>True if success, false if an error</returns>
        public bool SaveTICAndBPIPlotFiles(string datasetName, string outputDirectory)
        {
            try
            {
                mRecentFiles.Clear();

                // Check whether all the spectra have .MSLevel = 0
                // If they do, change the level to 1
                ValidateMSLevel(mBPI);
                ValidateMSLevel(mTIC);

                if (RemoveZeroesFromEnds)
                {
                    // See if the last few values in the BPI or TIC are zero, but the data before them is non-zero
                    // If this is the case, remove the final entries
                    RemoveZeroesAtFrontAndBack(mBPI);
                    RemoveZeroesAtFrontAndBack(mTIC);
                }

                var bpiPlotMS1 = InitializePlot(mBPI, datasetName + " - " + BPIPlotAbbrev + " - MS Spectra", 1, BPIXAxisLabel, BPIYAxisLabel, BPIAutoMinMaxY, BPIYAxisExponentialNotation, BPIXAxisIsTimeMinutes);
                RegisterEvents(bpiPlotMS1);

                var successMS1 = true;
                var successMS2 = true;
                var successTIC = true;

                if (bpiPlotMS1.SeriesCount > 0)
                {
                    var pngFile = MSFileInfoScanner.GetFileInfo(Path.Combine(outputDirectory, datasetName + "_" + BPIPlotAbbrev + "_MS.png"));
                    successMS1 = bpiPlotMS1.SaveToPNG(pngFile, 1024, 600, 96);
                    AddRecentFile(pngFile.FullName, OutputFileTypes.BPIMS);
                }

                var bpiPlotMS2 = InitializePlot(mBPI, datasetName + " - " + BPIPlotAbbrev + " - MS2 Spectra", 2, BPIXAxisLabel, BPIYAxisLabel, BPIAutoMinMaxY, BPIYAxisExponentialNotation, BPIXAxisIsTimeMinutes);
                RegisterEvents(bpiPlotMS2);

                if (bpiPlotMS2.SeriesCount > 0)
                {
                    var pngFile = MSFileInfoScanner.GetFileInfo(Path.Combine(outputDirectory, datasetName + "_" + BPIPlotAbbrev + "_MSn.png"));
                    successMS2 = bpiPlotMS2.SaveToPNG(pngFile, 1024, 600, 96);
                    AddRecentFile(pngFile.FullName, OutputFileTypes.BPIMSn);
                }

                var ticPlot = InitializePlot(mTIC, datasetName + " - " + TICPlotAbbrev + " - All Spectra", 0, TICXAxisLabel, TICYAxisLabel, TICAutoMinMaxY, TICYAxisExponentialNotation, TICXAxisIsTimeMinutes);
                RegisterEvents(ticPlot);

                if (ticPlot.SeriesCount > 0)
                {
                    // Replace forward and backslashes with underscores
                    // Change the micron symbol to a lowercase u (to avoid URLs with µ)
                    // Replace spaces with underscores

                    var plotAbbreviation = Regex.Replace(TICPlotAbbrev, @"[\/\\]", "_").Replace('µ', 'u').Replace(' ', '_');

                    // Dataset names should not have spaces, but replace with underscores in case one does
                    var plotFileName = datasetName.Replace(' ', '_') + "_" + plotAbbreviation + ".png";

                    var pngFile = MSFileInfoScanner.GetFileInfo(Path.Combine(outputDirectory, plotFileName));

                    successTIC = ticPlot.SaveToPNG(pngFile, 1024, 600, 96);
                    AddRecentFile(pngFile.FullName, OutputFileTypes.TIC);
                }

                return successMS1 && successMS2 && successTIC;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in SaveTICAndBPIPlotFiles", ex);
                return false;
            }
        }

        private void ValidateMSLevel(ChromatogramInfo chromatogramInfo)
        {
            var msLevelDefined = false;

            for (var index = 0; index < chromatogramInfo.ScanCount; index++)
            {
                if (chromatogramInfo.GetDataPoint(index).MSLevel > 0)
                {
                    msLevelDefined = true;
                    break;
                }
            }

            if (!msLevelDefined)
            {
                // Set the MSLevel to 1 for all scans
                for (var index = 0; index < chromatogramInfo.ScanCount; index++)
                {
                    chromatogramInfo.GetDataPoint(index).MSLevel = 1;
                }
            }
        }

        private class ChromatogramDataPoint
        {
            public int ScanNum { get; set; }
            public float TimeMinutes { get; set; }
            public double Intensity { get; set; }
            public int MSLevel { get; set; }
        }

        private class ChromatogramInfo
        {
            public int ScanCount => mScans.Count;

            public IEnumerable<ChromatogramDataPoint> Scans => mScans;

            private readonly List<ChromatogramDataPoint> mScans;

            private readonly SortedSet<int> mScanNumbers;

            /// <summary>
            /// Constructor
            /// </summary>
            public ChromatogramInfo()
            {
                mScans = new List<ChromatogramDataPoint>();
                mScanNumbers = new SortedSet<int>();
            }

            public void AddPoint(int scanNumber, int msLevel, float scanTimeMinutes, double intensity)
            {
                if (mScanNumbers.Contains(scanNumber))
                {
                    throw new Exception("Scan " + scanNumber + " has already been added to the TIC or BPI; programming error");
                }

                var dataPoint = new ChromatogramDataPoint
                {
                    ScanNum = scanNumber,
                    TimeMinutes = scanTimeMinutes,
                    Intensity = intensity,
                    MSLevel = msLevel
                };

                mScans.Add(dataPoint);
                mScanNumbers.Add(scanNumber);
            }

            public ChromatogramDataPoint GetDataPoint(int index)
            {
                if (mScans.Count == 0)
                {
                    throw new Exception("Chromatogram list is empty; cannot retrieve data point at index " + index);
                }
                if (index < 0 || index >= mScans.Count)
                {
                    throw new Exception("Chromatogram index out of range: " + index + "; should be between 0 and " + (mScans.Count - 1));
                }

                return mScans[index];
            }

            /// <summary>
            /// Clear cached data
            /// </summary>
            public void Initialize()
            {
                mScans.Clear();
                mScanNumbers.Clear();
            }

            // ReSharper disable once UnusedMember.Local
            public void RemoveAt(int index)
            {
                RemoveRange(index, 1);
            }

            public void RemoveRange(int index, int count)
            {
                if (index < 0 || index >= ScanCount || count <= 0)
                    return;

                var scansToRemove = new List<int>();
                var lastIndex = Math.Min(index + count, mScans.Count) - 1;

                for (var i = index; i <= lastIndex; i++)
                {
                    scansToRemove.Add(mScans[i].ScanNum);
                }

                mScans.RemoveRange(index, count);

                foreach (var scanNumber in scansToRemove)
                {
                    mScanNumbers.Remove(scanNumber);
                }
            }
        }
    }
}
