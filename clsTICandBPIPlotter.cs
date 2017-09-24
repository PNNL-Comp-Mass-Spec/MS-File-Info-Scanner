using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OxyPlot;
using OxyPlot.Series;
using PRISM;

namespace MSFileInfoScanner
{
    public class clsTICandBPIPlotter : clsEventNotifier
    {

        #region "Constants, Enums, Structures"
        public enum eOutputFileTypes
        {
            TIC = 0,
            BPIMS = 1,
            BPIMSn = 2
        }

        private struct udtOutputFileInfoType
        {
            public eOutputFileTypes FileType;
            public string FileName;
            public string FilePath;
        }

        #endregion

        #region "Member variables"

        // Data stored in mTIC will get plotted for all scans, both MS and MS/MS

        private clsChromatogramInfo mTIC;
        // Data stored in mBPI will be plotted separately for MS and MS/MS spectra

        private clsChromatogramInfo mBPI;

        private readonly List<udtOutputFileInfoType> mRecentFiles;

        private readonly string mDataSource;
        private readonly bool mWriteDebug;

        #endregion

        #region "Properties"

        public bool BPIAutoMinMaxY { get; set; }

        public string BPIPlotAbbrev { get; set; } = "BPI";

        public string BPIXAxisLabel { get; set; } = "LC Scan Number";

        public string BPIYAxisLabel { get; set; } = "Intensity";

        public bool BPIYAxisExponentialNotation { get; set; } = true;

        public int CountBPI => mBPI.ScanCount;

        public int CountTIC => mTIC.ScanCount;

        public bool DeleteTempFiles { get; set; }

        public bool PlotWithPython { get; set;  }

        public bool RemoveZeroesFromEnds { get; set; }

        public bool TICAutoMinMaxY { get; set; }

        public string TICPlotAbbrev { get; set; } = "TIC";

        public string TICXAxisLabel { get; set; } = "LC Scan Number";

        public string TICYAxisLabel { get; set; } = "Intensity";

        public bool TICYAxisExponentialNotation { get; set; } = true;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="writeDebug"></param>
        public clsTICandBPIPlotter(string dataSource = "", bool writeDebug = false)
        {
            mRecentFiles = new List<udtOutputFileInfoType>();

            mDataSource = dataSource;
            mWriteDebug = writeDebug;

            Reset();
        }

        public void AddData(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, double dblBPI, double dblTIC)
        {
            mBPI.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblBPI);
            mTIC.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblTIC);
        }

        public void AddDataBPIOnly(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, double dblBPI)
        {
            mBPI.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblBPI);
        }

        public void AddDataTICOnly(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, double dblTIC)
        {
            mTIC.AddPoint(intScanNumber, intMSLevel, sngScanTimeMinutes, dblTIC);
        }

        private void AddRecentFile(string strFilePath, eOutputFileTypes eFileType)
        {
            var udtOutputFileInfo = default(udtOutputFileInfoType);

            udtOutputFileInfo.FileType = eFileType;
            udtOutputFileInfo.FileName = Path.GetFileName(strFilePath);
            udtOutputFileInfo.FilePath = strFilePath;

            mRecentFiles.Add(udtOutputFileInfo);
        }

        private void AddOxyPlotSeries(PlotModel myplot, IReadOnlyCollection<DataPoint> objPoints)
        {
            // Generate a black curve with no symbols
            var series = new LineSeries();

            if (objPoints.Count <= 0)
            {
                return;
            }

            var eSymbolType = MarkerType.None;
            if (objPoints.Count == 1)
            {
                eSymbolType = MarkerType.Circle;
            }

            series.Color = OxyColors.Black;
            series.StrokeThickness = 1;
            series.MarkerType = eSymbolType;

            if (objPoints.Count == 1)
            {
                series.MarkerSize = 8;
                series.MarkerFill = OxyColors.DarkRed;
            }

            series.Points.AddRange(objPoints);

            myplot.Series.Add(series);
        }

        /// <summary>
        /// Returns the file name of the recently saved file of the given type
        /// </summary>
        /// <param name="eFileType">File type to find</param>
        /// <returns>File name if found; empty string if this file type was not saved</returns>
        /// <remarks>The list of recent files gets cleared each time you call SaveTICAndBPIPlotFiles() or Reset()</remarks>
        public string GetRecentFileInfo(eOutputFileTypes eFileType)
        {
            for (var intIndex = 0; intIndex <= mRecentFiles.Count - 1; intIndex++)
            {
                if (mRecentFiles[intIndex].FileType == eFileType)
                {
                    return mRecentFiles[intIndex].FileName;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns the file name and path of the recently saved file of the given type
        /// </summary>
        /// <param name="eFileType">File type to find</param>
        /// <param name="strFileName">File name (output)</param>
        /// <param name="strFilePath">File Path (output)</param>
        /// <returns>True if a match was found; otherwise returns false</returns>
        /// <remarks>The list of recent files gets cleared each time you call SaveTICAndBPIPlotFiles() or Reset()</remarks>
        public bool GetRecentFileInfo(eOutputFileTypes eFileType, out string strFileName, out string strFilePath)
        {
            for (var intIndex = 0; intIndex <= mRecentFiles.Count - 1; intIndex++)
            {
                if (mRecentFiles[intIndex].FileType == eFileType)
                {
                    strFileName = mRecentFiles[intIndex].FileName;
                    strFilePath = mRecentFiles[intIndex].FilePath;
                    return true;
                }
            }

            strFileName = string.Empty;
            strFilePath = string.Empty;

            return false;
        }

        private clsPlotContainerBase InitializePlot(
            clsChromatogramInfo objData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation)
        {
            if (PlotWithPython)
            {
                return InitializePythonPlot(objData, plotTitle, msLevelFilter, xAxisLabel, yAxisLabel, autoMinMaxY, yAxisExponentialNotation);
            }
            else
            {
                return InitializeOxyPlot(objData, plotTitle, msLevelFilter, xAxisLabel, yAxisLabel, autoMinMaxY, yAxisExponentialNotation);
            }
        }

        /// <summary>
        /// Plots a BPI or TIC chromatogram
        /// </summary>
        /// <param name="objData">Data to display</param>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="xAxisLabel"></param>
        /// <param name="yAxisLabel"></param>
        /// <param name="autoMinMaxY"></param>
        /// <param name="yAxisExponentialNotation"></param>
        /// <returns>OxyPlot PlotContainer</returns>
        private clsPlotContainer InitializeOxyPlot(
            clsChromatogramInfo objData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation)
        {

            var intMinScan = int.MaxValue;
            var intMaxScan = 0;
            double dblScanTimeMax = 0;
            double dblMaxIntensity = 0;

            // Instantiate the list to track the data points
            var objPoints = new List<DataPoint>();

            foreach (var chromDataPoint in objData.Scans)
            {

                if (msLevelFilter != 0 && chromDataPoint.MSLevel != msLevelFilter &&
                    !(msLevelFilter == 2 & chromDataPoint.MSLevel >= 2))
                {
                    continue;
                }

                objPoints.Add(new DataPoint(chromDataPoint.ScanNum, chromDataPoint.Intensity));

                if (chromDataPoint.TimeMinutes > dblScanTimeMax)
                {
                    dblScanTimeMax = chromDataPoint.TimeMinutes;
                }

                if (chromDataPoint.ScanNum < intMinScan)
                {
                    intMinScan = chromDataPoint.ScanNum;
                }

                if (chromDataPoint.ScanNum > intMaxScan)
                {
                    intMaxScan = chromDataPoint.ScanNum;
                }

                if (chromDataPoint.Intensity > dblMaxIntensity)
                {
                    dblMaxIntensity = chromDataPoint.Intensity;
                }
            }

            if (objPoints.Count == 0)
            {
                // Nothing to plot
                var emptyContainer = new clsPlotContainer(new PlotModel(), mWriteDebug, mDataSource);
                emptyContainer.WriteDebugLog("objPoints.Count == 0 in InitializeOxyPlot for plot " + plotTitle);
                return emptyContainer;
            }

            // Round intMaxScan down to the nearest multiple of 10
            intMaxScan = (int)Math.Ceiling(intMaxScan / 10.0) * 10;

            // Multiple dblMaxIntensity by 2% and then round up to the nearest integer
            dblMaxIntensity = Math.Ceiling(dblMaxIntensity * 1.02);

            var myPlot = clsOxyplotUtilities.GetBasicPlotModel(plotTitle, xAxisLabel, yAxisLabel);

            if (yAxisExponentialNotation)
            {
                myPlot.Axes[1].StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }

            AddOxyPlotSeries(myPlot, objPoints);

            // Update the axis format codes if the data values are small or the range of data is small
            var xVals = (from item in objPoints select item.X).ToList();
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[0], xVals, true);

            var yVals = (from item in objPoints select item.Y).ToList();
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[1], yVals, false);

            var plotContainer = new clsPlotContainer(myPlot, mWriteDebug, mDataSource)
            {
                FontSizeBase = clsPlotContainer.DEFAULT_BASE_FONT_SIZE
            };

            plotContainer.WriteDebugLog(string.Format("Instantiated plotContainer for plot {0}: {1} data points", plotTitle, objPoints.Count));

            // Possibly add a label showing the maximum elution time
            if (dblScanTimeMax > 0)
            {
                string strCaption;
                if (dblScanTimeMax < 2)
                {
                    strCaption = Math.Round(dblScanTimeMax, 2).ToString("0.00") + " minutes";
                }
                else if (dblScanTimeMax < 10)
                {
                    strCaption = Math.Round(dblScanTimeMax, 1).ToString("0.0") + " minutes";
                }
                else
                {
                    strCaption = Math.Round(dblScanTimeMax, 0).ToString("0") + " minutes";
                }

                plotContainer.AnnotationBottomRight = strCaption;

                // Alternative method is to add a TextAnnotation, but these are inside the main plot area
                // and are tied to a data point
                //
                // var objScanTimeMaxText = new OxyPlot.Annotations.TextAnnotation
                // {
                //     TextRotation = 0,
                //     Text = strCaption,
                //     Stroke = OxyColors.Black,
                //     StrokeThickness = 2,
                //     FontSize = clsPlotContainer.DEFAULT_BASE_FONT_SIZE
                // };
                //
                // objScanTimeMaxText.TextPosition = new DataPoint(intMaxScan, 0);
                // myPlot.Annotations.Add(objScanTimeMaxText);

            }

            // Override the auto-computed X axis range
            if (intMinScan == intMaxScan)
            {
                myPlot.Axes[0].Minimum = intMinScan - 1;
                myPlot.Axes[0].Maximum = intMinScan + 1;
            }
            else
            {
                myPlot.Axes[0].Minimum = 0;

                if (intMaxScan == 0)
                {
                    myPlot.Axes[0].Maximum = 1;
                }
                else
                {
                    myPlot.Axes[0].Maximum = intMaxScan;
                }
            }

            // Assure that we don't see ticks between scan numbers
            clsOxyplotUtilities.ValidateMajorStep(myPlot.Axes[0]);

            // Override the auto-computed Y axis range
            if (autoMinMaxY)
            {
                // Auto scale
            }
            else
            {
                myPlot.Axes[1].Minimum = 0;
                myPlot.Axes[1].Maximum = dblMaxIntensity;
            }

            // Hide the legend
            myPlot.IsLegendVisible = false;

            return plotContainer;
        }

        /// <summary>
        /// Plots a BPI or TIC chromatogram
        /// </summary>
        /// <param name="objData">Data to display</param>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="xAxisLabel"></param>
        /// <param name="yAxisLabel"></param>
        /// <param name="autoMinMaxY"></param>
        /// <param name="yAxisExponentialNotation"></param>
        /// <returns>Python PlotContainer</returns>
        private clsPythonPlotContainer InitializePythonPlot(
            clsChromatogramInfo objData,
            string plotTitle,
            int msLevelFilter,
            string xAxisLabel,
            string yAxisLabel,
            bool autoMinMaxY,
            bool yAxisExponentialNotation)
        {

            double dblScanTimeMax = 0;
            double dblMaxIntensity = 0;

            // Instantiate the list to track the data points
            var objPoints = new List<DataPoint>();

            foreach (var chromDataPoint in objData.Scans)
            {
                if (msLevelFilter != 0 && chromDataPoint.MSLevel != msLevelFilter &&
                    !(msLevelFilter == 2 & chromDataPoint.MSLevel >= 2))
                {
                    continue;
                }

                objPoints.Add(new DataPoint(chromDataPoint.ScanNum, chromDataPoint.Intensity));

                if (chromDataPoint.TimeMinutes > dblScanTimeMax)
                {
                    dblScanTimeMax = chromDataPoint.TimeMinutes;
                }

                if (chromDataPoint.Intensity > dblMaxIntensity)
                {
                    dblMaxIntensity = chromDataPoint.Intensity;
                }
            }

            if (objPoints.Count == 0)
            {
                // Nothing to plot
                var emptyContainer = new clsPythonPlotContainer2D();
                return emptyContainer;
            }

            var plotContainer = new clsPythonPlotContainer2D(plotTitle, xAxisLabel, yAxisLabel) {
                DeleteTempFiles = DeleteTempFiles
            };


            if (yAxisExponentialNotation)
            {
                plotContainer.YAxisInfo.StringFormat = clsAxisInfo.EXPONENTIAL_FORMAT;
            }

            plotContainer.SetData(objPoints);

            // Update the axis format codes if the data values are small or the range of data is small

            // Assume the X axis is plotting integers
            var xVals = (from item in objPoints select item.X).ToList();
            clsPlotUtilities.GetAxisFormatInfo(xVals, true, plotContainer.XAxisInfo);

            // Assume the Y axis is plotting doubles
            var yVals = (from item in objPoints select item.Y).ToList();
            clsPlotUtilities.GetAxisFormatInfo(yVals, false, plotContainer.YAxisInfo);

            // Possibly add a label showing the maximum elution time
            if (dblScanTimeMax > 0)
            {
                string strCaption;
                if (dblScanTimeMax < 2)
                {
                    strCaption = Math.Round(dblScanTimeMax, 2).ToString("0.00") + " minutes";
                }
                else if (dblScanTimeMax < 10)
                {
                    strCaption = Math.Round(dblScanTimeMax, 1).ToString("0.0") + " minutes";
                }
                else
                {
                    strCaption = Math.Round(dblScanTimeMax, 0).ToString("0") + " minutes";
                }

                plotContainer.AnnotationBottomRight = strCaption;
            }

            // Override the auto-computed Y axis range
            if (autoMinMaxY)
            {
                // Auto scale
            }
            else
            {
                plotContainer.XAxisInfo.SetRange(0, dblMaxIntensity);
            }

            return plotContainer;
        }
        /// <summary>
        /// Clear BPI and TIC data
        /// </summary>
        public void Reset()
        {
            if (mBPI == null)
            {
                mBPI = new clsChromatogramInfo();
                mTIC = new clsChromatogramInfo();
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
        /// <param name="strDatasetName"></param>
        /// <param name="strOutputFolderPath"></param>
        /// <returns>True if success, false if an error</returns>
        public bool SaveTICAndBPIPlotFiles(string strDatasetName, string strOutputFolderPath)
        {
            bool blnSuccess;

            try
            {

                mRecentFiles.Clear();

                // Check whether all of the spectra have .MSLevel = 0
                // If they do, change the level to 1
                ValidateMSLevel(mBPI);
                ValidateMSLevel(mTIC);

                if (RemoveZeroesFromEnds)
                {
                    // Check whether the last few scans have values if 0; if they do, remove them
                    RemoveZeroesAtFrontAndBack(mBPI);
                    RemoveZeroesAtFrontAndBack(mTIC);
                }

                var bpiPlotMS1 = InitializePlot(mBPI, strDatasetName + " - " + BPIPlotAbbrev + " - MS Spectra", 1, BPIXAxisLabel, BPIYAxisLabel, BPIAutoMinMaxY, BPIYAxisExponentialNotation);
                RegisterEvents(bpiPlotMS1);

                if (bpiPlotMS1.SeriesCount > 0)
                {
                    var strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + BPIPlotAbbrev + "_MS.png");
                    bpiPlotMS1.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMS);
                }

                var bpiPlotMS2 = InitializePlot(mBPI, strDatasetName + " - " + BPIPlotAbbrev + " - MS2 Spectra", 2, BPIXAxisLabel, BPIYAxisLabel, BPIAutoMinMaxY, BPIYAxisExponentialNotation);
                RegisterEvents(bpiPlotMS2);

                if (bpiPlotMS2.SeriesCount > 0)
                {
                    var strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + BPIPlotAbbrev + "_MSn.png");
                    bpiPlotMS2.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMSn);
                }

                var ticPlot = InitializePlot(mTIC, strDatasetName + " - " + TICPlotAbbrev + " - All Spectra", 0, TICXAxisLabel, TICYAxisLabel, TICAutoMinMaxY, TICYAxisExponentialNotation);
                RegisterEvents(ticPlot);

                if (ticPlot.SeriesCount > 0)
                {
                    var strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + TICPlotAbbrev + ".png");
                    ticPlot.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.TIC);
                }

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in SaveTICAndBPIPlotFiles", ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private void RemoveZeroesAtFrontAndBack(clsChromatogramInfo objChrom)
        {
            const int MAX_POINTS_TO_CHECK = 100;
            var intPointsChecked = 0;

            // See if the last few values are zero, but the data before them is non-zero
            // If this is the case, remove the final entries

            var intIndexNonZeroValue = -1;
            var intZeroPointCount = 0;
            for (var intIndex = objChrom.ScanCount - 1; intIndex >= 0; intIndex += -1)
            {
                if (Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < float.Epsilon)
                {
                    intZeroPointCount += 1;
                }
                else
                {
                    intIndexNonZeroValue = intIndex;
                    break;
                }
                intPointsChecked += 1;
                if (intPointsChecked >= MAX_POINTS_TO_CHECK)
                    break;
            }

            if (intZeroPointCount > 0 && intIndexNonZeroValue >= 0)
            {
                objChrom.RemoveRange(intIndexNonZeroValue + 1, intZeroPointCount);
            }

            // Now check the first few values
            intIndexNonZeroValue = -1;
            intZeroPointCount = 0;
            for (var intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++)
            {
                if (Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < float.Epsilon)
                {
                    intZeroPointCount += 1;
                }
                else
                {
                    intIndexNonZeroValue = intIndex;
                    break;
                }
                intPointsChecked += 1;
                if (intPointsChecked >= MAX_POINTS_TO_CHECK)
                    break;
            }

            if (intZeroPointCount > 0 && intIndexNonZeroValue >= 0)
            {
                objChrom.RemoveRange(0, intIndexNonZeroValue);
            }

        }

        private void ValidateMSLevel(clsChromatogramInfo objChrom)
        {
            var blnMSLevelDefined = false;

            for (var intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++)
            {
                if (objChrom.GetDataPoint(intIndex).MSLevel > 0)
                {
                    blnMSLevelDefined = true;
                    break;
                }
            }

            if (!blnMSLevelDefined)
            {
                // Set the MSLevel to 1 for all scans
                for (var intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++)
                {
                    objChrom.GetDataPoint(intIndex).MSLevel = 1;
                }
            }

        }

        private class clsChromatogramDataPoint
        {
            public int ScanNum { get; set; }
            public float TimeMinutes { get; set; }
            public double Intensity { get; set; }
            public int MSLevel { get; set; }
        }

        private class clsChromatogramInfo
        {

            public int ScanCount => mScans.Count;

            public IEnumerable<clsChromatogramDataPoint> Scans => mScans;

            private List<clsChromatogramDataPoint> mScans;
            public clsChromatogramInfo()
            {
                Initialize();
            }

            public void AddPoint(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, double dblIntensity)
            {
                if ((from item in mScans where item.ScanNum == intScanNumber select item).Any())
                {
                    throw new Exception("Scan " + intScanNumber + " has already been added to the TIC or BPI; programming error");
                }

                var chromDataPoint = new clsChromatogramDataPoint
                {
                    ScanNum = intScanNumber,
                    TimeMinutes = sngScanTimeMinutes,
                    Intensity = dblIntensity,
                    MSLevel = intMSLevel
                };

                mScans.Add(chromDataPoint);
            }

            public clsChromatogramDataPoint GetDataPoint(int index)
            {
                if (mScans.Count == 0)
                {
                    throw new Exception("Chromatogram list is empty; cannot retrieve data point at index " + index);
                }
                if (index < 0 || index >= mScans.Count)
                {
                    throw new Exception("Chromatogram index out of range: " + index + "; should be between 0 and " + (mScans.Count - 1).ToString());
                }

                return mScans[index];

            }

            public void Initialize()
            {
                mScans = new List<clsChromatogramDataPoint>();
            }

            // ReSharper disable once UnusedMember.Local
            public void RemoveAt(int index)
            {
                RemoveRange(index, 1);
            }

            public void RemoveRange(int index, int count)
            {
                if (index >= 0 & index < ScanCount & count > 0)
                {
                    mScans.RemoveRange(index, count);
                }

            }

        }

    }
}

