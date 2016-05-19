using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OxyPlot;
using OxyPlot.Series;

namespace MSFileInfoScanner
{
    public class clsTICandBPIPlotter
    {

        #region "Constants, Enums, Structures"
        public enum eOutputFileTypes
        {
            TIC = 0,
            BPIMS = 1,
            BPIMSn = 2
        }

        protected struct udtOutputFileInfoType
        {
            public eOutputFileTypes FileType;
            public string FileName;
            public string FilePath;
        }

        #endregion

        #region "Member variables"
        // Data stored in mTIC will get plotted for all scans, both MS and MS/MS

        protected clsChromatogramInfo mTIC;
        // Data stored in mBPI will be plotted separately for MS and MS/MS spectra

        protected clsChromatogramInfo mBPI;
        protected string mTICXAxisLabel = "LC Scan Number";
        protected string mTICYAxisLabel = "Intensity";

        protected bool mTICYAxisExponentialNotation = true;
        protected string mBPIXAxisLabel = "LC Scan Number";
        protected string mBPIYAxisLabel = "Intensity";

        protected bool mBPIYAxisExponentialNotation = true;
        protected string mTICPlotAbbrev = "TIC";

        protected string mBPIPlotAbbrev = "BPI";
        protected bool mBPIAutoMinMaxY;
        protected bool mTICAutoMinMaxY;

        protected bool mRemoveZeroesFromEnds;
        #endregion
        protected List<udtOutputFileInfoType> mRecentFiles;


        public bool BPIAutoMinMaxY {
            get { return mBPIAutoMinMaxY; }
            set { mBPIAutoMinMaxY = value; }
        }

        public string BPIPlotAbbrev {
            get { return mBPIPlotAbbrev; }
            set { mBPIPlotAbbrev = value; }
        }

        public string BPIXAxisLabel {
            get { return mBPIXAxisLabel; }
            set { mBPIXAxisLabel = value; }
        }

        public string BPIYAxisLabel {
            get { return mBPIYAxisLabel; }
            set { mBPIYAxisLabel = value; }
        }

        public bool BPIYAxisExponentialNotation {
            get { return mBPIYAxisExponentialNotation; }
            set { mBPIYAxisExponentialNotation = value; }
        }

        public int CountBPI {
            get { return mBPI.ScanCount; }
        }

        public int CountTIC {
            get { return mTIC.ScanCount; }
        }

        public bool RemoveZeroesFromEnds {
            get { return mRemoveZeroesFromEnds; }
            set { mRemoveZeroesFromEnds = value; }
        }

        public bool TICAutoMinMaxY {
            get { return mTICAutoMinMaxY; }
            set { mTICAutoMinMaxY = value; }
        }

        public string TICPlotAbbrev {
            get { return mTICPlotAbbrev; }
            set { mTICPlotAbbrev = value; }
        }

        public string TICXAxisLabel {
            get { return mTICXAxisLabel; }
            set { mTICXAxisLabel = value; }
        }

        public string TICYAxisLabel {
            get { return mTICYAxisLabel; }
            set { mTICYAxisLabel = value; }
        }

        public bool TICYAxisExponentialNotation {
            get { return mTICYAxisExponentialNotation; }
            set { mTICYAxisExponentialNotation = value; }
        }

        public clsTICandBPIPlotter()
        {
            mRecentFiles = new List<udtOutputFileInfoType>();
            this.Reset();
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

        protected void AddRecentFile(string strFilePath, eOutputFileTypes eFileType)
        {
            var udtOutputFileInfo = default(udtOutputFileInfoType);

            udtOutputFileInfo.FileType = eFileType;
            udtOutputFileInfo.FileName = Path.GetFileName(strFilePath);
            udtOutputFileInfo.FilePath = strFilePath;

            mRecentFiles.Add(udtOutputFileInfo);
        }


        protected void AddSeries(PlotModel myplot, List<DataPoint> objPoints)
        {
            // Generate a black curve with no symbols
            var series = new LineSeries();

            if (objPoints.Count > 0) {
                var eSymbolType = MarkerType.None;
                if (objPoints.Count == 1) {
                    eSymbolType = MarkerType.Circle;
                }

                series.Color = OxyColors.Black;
                series.StrokeThickness = 1;
                series.MarkerType = eSymbolType;

                if (objPoints.Count == 1) {
                    series.MarkerSize = 8;
                    series.MarkerFill = OxyColors.DarkRed;
                }

                series.Points.AddRange(objPoints);

                myplot.Series.Add(series);
            }

        }

        /// <summary>
        /// Returns the file name of the recently saved file of the given type
        /// </summary>
        /// <param name="eFileType">File type to find</param>
        /// <returns>File name if found; empty string if this file type was not saved</returns>
        /// <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
        public string GetRecentFileInfo(eOutputFileTypes eFileType)
        {
            for (var intIndex = 0; intIndex <= mRecentFiles.Count - 1; intIndex++) {
                if (mRecentFiles[intIndex].FileType == eFileType) {
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
        /// <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
        public bool GetRecentFileInfo(eOutputFileTypes eFileType, ref string strFileName, ref string strFilePath)
        {
            for (var intIndex = 0; intIndex <= mRecentFiles.Count - 1; intIndex++) {
                if (mRecentFiles[intIndex].FileType == eFileType) {
                    strFileName = mRecentFiles[intIndex].FileName;
                    strFilePath = mRecentFiles[intIndex].FilePath;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Plots a BPI or TIC chromatogram
        /// </summary>
        /// <param name="objData">Data to display</param>
        /// <param name="strTitle">Title of the plot</param>
        /// <param name="intMSLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="strXAxisLabel"></param>
        /// <param name="strYAxisLabel"></param>
        /// <param name="blnAutoMinMaxY"></param>
        /// <param name="blnYAxisExponentialNotation"></param>
        /// <returns>OxyPlot PlotContainer</returns>
        /// <remarks></remarks>
        private clsPlotContainer InitializePlot(
            clsChromatogramInfo objData, 
            string strTitle, 
            int intMSLevelFilter, 
            string strXAxisLabel, 
            string strYAxisLabel, 
            bool blnAutoMinMaxY, 
            bool blnYAxisExponentialNotation)
        {

            var intMinScan = int.MaxValue;
            var intMaxScan = 0;
            double dblScanTimeMax = 0;
            double dblMaxIntensity = 0;

            // Instantiate the list to track the data points
            var objPoints = new List<DataPoint>();


            foreach (var chromDataPoint in objData.Scans) {
		    
                if (intMSLevelFilter != 0 && chromDataPoint.MSLevel != intMSLevelFilter &&
                    !(intMSLevelFilter == 2 & chromDataPoint.MSLevel >= 2))
                {
                    continue;
                }

                objPoints.Add(new DataPoint(chromDataPoint.ScanNum, chromDataPoint.Intensity));

                if (chromDataPoint.TimeMinutes > dblScanTimeMax) {
                    dblScanTimeMax = chromDataPoint.TimeMinutes;
                }

                if (chromDataPoint.ScanNum < intMinScan) {
                    intMinScan = chromDataPoint.ScanNum;
                }

                if (chromDataPoint.ScanNum > intMaxScan) {
                    intMaxScan = chromDataPoint.ScanNum;
                }

                if (chromDataPoint.Intensity > dblMaxIntensity) {
                    dblMaxIntensity = chromDataPoint.Intensity;
                }
            }

            if (objPoints.Count == 0) {
                // Nothing to plot
                return new clsPlotContainer(new PlotModel());
            }

            // Round intMaxScan down to the nearest multiple of 10
            intMaxScan = Convert.ToInt32(Math.Ceiling(intMaxScan / 10.0) * 10);

            // Multiple dblMaxIntensity by 2% and then round up to the nearest integer
            dblMaxIntensity = Convert.ToDouble(Math.Ceiling(dblMaxIntensity * 1.02));

            var myPlot = clsOxyplotUtilities.GetBasicPlotModel(strTitle, strXAxisLabel, strYAxisLabel);

            if (blnYAxisExponentialNotation) {
                myPlot.Axes[1].StringFormat = clsOxyplotUtilities.EXPONENTIAL_FORMAT;
            }

            AddSeries(myPlot, objPoints);

            // Update the axis format codes if the data values are small or the range of data is small
            var xVals = (from item in objPoints select item.X);
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[0], xVals, true);

            var yVals = (from item in objPoints select item.Y);
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[1], yVals, false);

            var plotContainer = new clsPlotContainer(myPlot) {
                FontSizeBase = clsOxyplotUtilities.FONT_SIZE_BASE
            };

            // Possibly add a label showing the maximum elution time

            if (dblScanTimeMax > 0) {
                string strCaption;
                if (dblScanTimeMax < 2) {
                    strCaption = Math.Round(dblScanTimeMax, 2).ToString("0.00") + " minutes";
                } else if (dblScanTimeMax < 10) {
                    strCaption = Math.Round(dblScanTimeMax, 1).ToString("0.0") + " minutes";
                } else {
                    strCaption = Math.Round(dblScanTimeMax, 0).ToString("0") + " minutes";
                }

                plotContainer.AnnotationBottomRight = strCaption;

                // Alternative method is to add a TextAnnotation, but these are inside the main plot area
                // and are tied to a data point

                //Dim objScanTimeMaxText = New TextAnnotation() With {
                //    .TextRotation = 0,
                //    .Text = strCaption,
                //    .Stroke = OxyColors.Black,
                //    .StrokeThickness = 2,
                //    .FontSize = FONT_SIZE_BASE
                //}

                //objScanTimeMaxText.TextPosition = New OxyPlot.DataPoint(intMaxScan, 0)
                //myPlot.Annotations.Add(objScanTimeMaxText)

            }

            // Override the auto-computed X axis range
            if (intMinScan == intMaxScan) {
                myPlot.Axes[0].Minimum = intMinScan - 1;
                myPlot.Axes[0].Maximum = intMinScan + 1;
            } else {
                myPlot.Axes[0].Minimum = 0;

                if (intMaxScan == 0) {
                    myPlot.Axes[0].Maximum = 1;
                } else {
                    myPlot.Axes[0].Maximum = intMaxScan;
                }
            }

            // Assure that we don't see ticks between scan numbers
            clsOxyplotUtilities.ValidateMajorStep(myPlot.Axes[0]);

            // Override the auto-computed Y axis range
            if (blnAutoMinMaxY) {
                // Auto scale
            } else {
                myPlot.Axes[1].Minimum = 0;
                myPlot.Axes[1].Maximum = dblMaxIntensity;
            }

            // Hide the legend
            myPlot.IsLegendVisible = false;

            return plotContainer;

        }


        public void Reset()
        {
            if (mBPI == null) {
                mBPI = new clsChromatogramInfo();
                mTIC = new clsChromatogramInfo();
            } else {
                mBPI.Initialize();
                mTIC.Initialize();
            }

            mRecentFiles.Clear();

        }

        public bool SaveTICAndBPIPlotFiles(string strDatasetName, string strOutputFolderPath, out string strErrorMessage)
        {
            bool blnSuccess;
            strErrorMessage = string.Empty;

            try {

                mRecentFiles.Clear();

                // Check whether all of the spectra have .MSLevel = 0
                // If they do, change the level to 1
                ValidateMSLevel(ref mBPI);
                ValidateMSLevel(ref mTIC);

                if (mRemoveZeroesFromEnds) {
                    // Check whether the last few scans have values if 0; if they do, remove them
                    RemoveZeroesAtFrontAndBack(ref mBPI);
                    RemoveZeroesAtFrontAndBack(ref mTIC);
                }

                var plotContainer = InitializePlot(mBPI, strDatasetName + " - " + mBPIPlotAbbrev + " - MS Spectra", 1, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation);
                string strPNGFilePath;
                if (plotContainer.SeriesCount > 0) {
                    strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + mBPIPlotAbbrev + "_MS.png");
                    plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMS);
                }

                plotContainer = InitializePlot(mBPI, strDatasetName + " - " + mBPIPlotAbbrev + " - MS2 Spectra", 2, mBPIXAxisLabel, mBPIYAxisLabel, mBPIAutoMinMaxY, mBPIYAxisExponentialNotation);
                if (plotContainer.SeriesCount > 0) {
                    strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + mBPIPlotAbbrev + "_MSn.png");
                    plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.BPIMSn);
                }

                plotContainer = InitializePlot(mTIC, strDatasetName + " - " + mTICPlotAbbrev + " - All Spectra", 0, mTICXAxisLabel, mTICYAxisLabel, mTICAutoMinMaxY, mTICYAxisExponentialNotation);
                if (plotContainer.SeriesCount > 0) {
                    strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + mTICPlotAbbrev + ".png");
                    plotContainer.SaveToPNG(strPNGFilePath, 1024, 600, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.TIC);
                }

                blnSuccess = true;
            } catch (Exception ex) {
                strErrorMessage = ex.Message;
                blnSuccess = false;
            }

            return blnSuccess;

        }

        protected void RemoveZeroesAtFrontAndBack(ref clsChromatogramInfo objChrom)
        {
            const int MAX_POINTS_TO_CHECK = 100;
            int intIndex;
            var intPointsChecked = 0;

            // See if the last few values are zero, but the data before them is non-zero
            // If this is the case, remove the final entries

            var intIndexNonZeroValue = -1;
            var intZeroPointCount = 0;
            for (intIndex = objChrom.ScanCount - 1; intIndex >= 0; intIndex += -1) {
                if (Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < float.Epsilon) {
                    intZeroPointCount += 1;
                } else {
                    intIndexNonZeroValue = intIndex;
                    break; // TODO: might not be correct. Was : Exit For
                }
                intPointsChecked += 1;
                if (intPointsChecked >= MAX_POINTS_TO_CHECK)
                    break; // TODO: might not be correct. Was : Exit For
            }

            if (intZeroPointCount > 0 && intIndexNonZeroValue >= 0) {
                objChrom.RemoveRange(intIndexNonZeroValue + 1, intZeroPointCount);
            }


            // Now check the first few values
            intIndexNonZeroValue = -1;
            intZeroPointCount = 0;
            for (intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++) {
                if (Math.Abs(objChrom.GetDataPoint(intIndex).Intensity) < float.Epsilon) {
                    intZeroPointCount += 1;
                } else {
                    intIndexNonZeroValue = intIndex;
                    break; // TODO: might not be correct. Was : Exit For
                }
                intPointsChecked += 1;
                if (intPointsChecked >= MAX_POINTS_TO_CHECK)
                    break; // TODO: might not be correct. Was : Exit For
            }

            if (intZeroPointCount > 0 && intIndexNonZeroValue >= 0) {
                objChrom.RemoveRange(0, intIndexNonZeroValue);
            }

        }

        protected void ValidateMSLevel(ref clsChromatogramInfo objChrom)
        {
            int intIndex;
            var blnMSLevelDefined = false;

            for (intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++) {
                if (objChrom.GetDataPoint(intIndex).MSLevel > 0) {
                    blnMSLevelDefined = true;
                    break; // TODO: might not be correct. Was : Exit For
                }
            }

            if (!blnMSLevelDefined) {
                // Set the MSLevel to 1 for all scans
                for (intIndex = 0; intIndex <= objChrom.ScanCount - 1; intIndex++) {
                    objChrom.GetDataPoint(intIndex).MSLevel = 1;
                }
            }

        }

        protected class clsChromatogramDataPoint
        {
            public int ScanNum { get; set; }
            public float TimeMinutes { get; set; }
            public double Intensity { get; set; }
            public int MSLevel { get; set; }
        }

        protected class clsChromatogramInfo
        {

            public int ScanCount {
                get { return mScans.Count; }
            }

            public IEnumerable<clsChromatogramDataPoint> Scans {
                get { return mScans; }
            }


            protected List<clsChromatogramDataPoint> mScans;
            public clsChromatogramInfo()
            {
                this.Initialize();
            }


            public void AddPoint(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, double dblIntensity)
            {
                if ((from item in mScans where item.ScanNum == intScanNumber select item).Any()) {
                    throw new Exception("Scan " + intScanNumber + " has already been added to the TIC or BPI; programming error");
                }

                var chromDataPoint = new clsChromatogramDataPoint {
                    ScanNum = intScanNumber,
                    TimeMinutes = sngScanTimeMinutes,
                    Intensity = dblIntensity,
                    MSLevel = intMSLevel
                };

                mScans.Add(chromDataPoint);
            }

            public clsChromatogramDataPoint GetDataPoint(int index)
            {
                if (mScans.Count == 0) {
                    throw new Exception("Chromatogram list is empty; cannot retrieve data point at index " + index);
                }
                if (index < 0 || index >= mScans.Count) {
                    throw new Exception("Chromatogram index out of range: " + index + "; should be between 0 and " + (mScans.Count - 1).ToString());
                }

                return mScans[index];

            }

            public void Initialize()
            {
                mScans = new List<clsChromatogramDataPoint>();
            }

            public void RemoveAt(int Index)
            {
                RemoveRange(Index, 1);
            }


            public void RemoveRange(int Index, int Count)
            {
                if (Index >= 0 & Index < ScanCount & Count > 0) {
                    mScans.RemoveRange(Index, Count);
                }

            }

        }
    }
}

