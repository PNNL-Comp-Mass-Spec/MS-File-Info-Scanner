using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScannerInterfaces;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using PRISM;

namespace MSFileInfoScanner
{
    /// <summary>
    /// This class tracks the m/z and intensity values for a series of spectra
    /// It can then create a 2D plot of m/z vs. intensity
    /// To keep the plot from being too dense, it will filter the data to show at most MaxPointsToPlot data points
    /// Furthermore, it will bin the data by MZResolution m/z units (necessary if the data is not centroided)
    /// </summary>
    /// <remarks></remarks>
    public class clsLCMSDataPlotter : clsEventNotifier
    {

        #region "Constants, Enums, Structures"

        // Absolute maximum number of ions that will be tracked for a mass spectrum
        private const int MAX_ALLOWABLE_ION_COUNT = 50000;

        public enum eOutputFileTypes
        {
            LCMS = 0,
            LCMSMSn = 1
        }

        private struct udtOutputFileInfoType
        {
            public eOutputFileTypes FileType;
            public string FileName;
            public string FilePath;
        }

        public struct udtMSIonType
        {
            public double MZ;
            public double Intensity;

            public byte Charge;
            public override string ToString()
            {
                if (Charge > 0)
                {
                    return MZ.ToString("0.0###") + ", " + Intensity.ToString("0") + ", " + Charge + "+";
                }

                return MZ.ToString("0.0###") + ", " + Intensity.ToString("0");
            }
        }

        #endregion

        #region "Member variables"

        // Keeps track of the total number of data points cached in mScans
        private int mPointCountCached;

        private int mPointCountCachedAfterLastTrim;

        private readonly List<clsScanData> mScans;

        private clsLCMSDataPlotterOptions mOptions;

        private readonly List<udtOutputFileInfoType> mRecentFiles;

        private int mSortingWarnCount;
        private int mSpectraFoundExceedingMaxIonCount;
        private int mMaxIonCountReported;

        private DateTime mLastGCTime;

        #endregion

        #region "Properties"
        public clsLCMSDataPlotterOptions Options
        {
            get => mOptions;
            set => mOptions = value;
        }

        public int ScanCountCached => mScans.Count;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public clsLCMSDataPlotter()
            : this(new clsLCMSDataPlotterOptions())
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="objOptions"></param>
        public clsLCMSDataPlotter(clsLCMSDataPlotterOptions objOptions)
        {
            mOptions = objOptions;
            mRecentFiles = new List<udtOutputFileInfoType>();
            mSortingWarnCount = 0;
            mSpectraFoundExceedingMaxIonCount = 0;
            mMaxIonCountReported = 0;

            mLastGCTime = DateTime.UtcNow;

            mScans = new List<clsScanData>();
        }

        private void AddRecentFile(string strFilePath, eOutputFileTypes eFileType)
        {
            var udtOutputFileInfo = new udtOutputFileInfoType
            {
                FileType = eFileType,
                FileName = Path.GetFileName(strFilePath),
                FilePath = strFilePath
            };

            mRecentFiles.Add(udtOutputFileInfo);
        }

        public bool AddScan2D(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, int intIonCount, double[,] dblMassIntensityPairs)
        {
            try
            {
                if (intIonCount <= 0)
                {
                    // No data to add
                    return false;
                }

                // Make sure the data is sorted by m/z
                for (var intIndex = 1; intIndex <= intIonCount - 1; intIndex++)
                {
                    // Note that dblMassIntensityPairs[0, intIndex) is m/z
                    //       and dblMassIntensityPairs[1, intIndex) is intensity
                    if (dblMassIntensityPairs[0, intIndex] < dblMassIntensityPairs[0, intIndex - 1])
                    {
                        // May need to sort the data
                        // However, if the intensity of both data points is zero, then we can simply swap the data
                        if (Math.Abs(dblMassIntensityPairs[1, intIndex]) < double.Epsilon && Math.Abs(dblMassIntensityPairs[1, intIndex - 1]) < double.Epsilon)
                        {
                            // Swap the m/z values
                            var dblSwapVal = dblMassIntensityPairs[0, intIndex];
                            dblMassIntensityPairs[0, intIndex] = dblMassIntensityPairs[0, intIndex - 1];
                            dblMassIntensityPairs[0, intIndex - 1] = dblSwapVal;
                        }
                        else
                        {
                            // Need to sort
                            mSortingWarnCount += 1;
                            if (mSortingWarnCount <= 10)
                            {
                                Console.WriteLine("  Sorting m/z data (this typically shouldn't be required for Finnigan data, though can occur for high res orbitrap data)");
                            }
                            else if (mSortingWarnCount % 100 == 0)
                            {
                                Console.WriteLine("  Sorting m/z data (i = " + mSortingWarnCount + ")");
                            }

                            // We can't easily sort a 2D array in .NET
                            // Thus, we must copy the data into new arrays and then call AddScan()

                            var lstIons = new List<udtMSIonType>(intIonCount - 1);

                            for (var intCopyIndex = 0; intCopyIndex <= intIonCount - 1; intCopyIndex++)
                            {
                                var udtIon = new udtMSIonType
                                {
                                    MZ = dblMassIntensityPairs[0, intCopyIndex],
                                    Intensity = dblMassIntensityPairs[1, intCopyIndex]
                                };

                                lstIons.Add(udtIon);
                            }

                            return AddScan(intScanNumber, intMSLevel, sngScanTimeMinutes, lstIons);

                        }
                    }
                }

                var dblIonsMZFiltered = new double[intIonCount];
                var sngIonsIntensityFiltered = new float[intIonCount];
                var bytChargeFiltered = new byte[intIonCount];

                // Populate dblIonsMZFiltered & sngIonsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

                var intIonCountNew = 0;
                for (var intIndex = 0; intIndex <= intIonCount - 1; intIndex++)
                {
                    if (dblMassIntensityPairs[1, intIndex] > 0 && dblMassIntensityPairs[1, intIndex] >= mOptions.MinIntensity)
                    {
                        dblIonsMZFiltered[intIonCountNew] = dblMassIntensityPairs[0, intIndex];

                        if (dblMassIntensityPairs[1, intIndex] > float.MaxValue)
                        {
                            sngIonsIntensityFiltered[intIonCountNew] = float.MaxValue;
                        }
                        else
                        {
                            sngIonsIntensityFiltered[intIonCountNew] = (float)dblMassIntensityPairs[1, intIndex];
                        }

                        bytChargeFiltered[intIonCountNew] = 0;

                        intIonCountNew += 1;
                    }
                }

                const bool USE_LOG = false;
                if (USE_LOG)
                {
                    for (var intIndex = 0; intIndex <= intIonCountNew - 1; intIndex++)
                    {
                        if (sngIonsIntensityFiltered[intIndex] > 0)
                        {
                            sngIonsIntensityFiltered[intIndex] = (float)Math.Log10(sngIonsIntensityFiltered[intIndex]);
                        }
                    }
                }

                AddScanCheckData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCountNew, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered);

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScan2D: " + ex.Message, ex);
                return false;
            }

            return true;

        }

        public bool AddScan(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, int intIonCount, double[] dblIonsMZ, double[] dblIonsIntensity)
        {
            List<udtMSIonType> lstIons;

            if (intIonCount > MAX_ALLOWABLE_ION_COUNT) {
                Array.Sort(dblIonsIntensity, dblIonsMZ);

                var lstHighIntensityIons = new List<udtMSIonType>(MAX_ALLOWABLE_ION_COUNT);

                for (var intIndex = intIonCount - MAX_ALLOWABLE_ION_COUNT; intIndex <= intIonCount - 1; intIndex++) {
                    var udtIon = new udtMSIonType
                    {
                        MZ = dblIonsMZ[intIndex],
                        Intensity = dblIonsIntensity[intIndex]
                    };

                    lstHighIntensityIons.Add(udtIon);
                }

                lstIons = (from item in lstHighIntensityIons orderby item.MZ select item).ToList();

            } else {
                lstIons = new List<udtMSIonType>(intIonCount - 1);

                for (var intIndex = 0; intIndex <= intIonCount - 1; intIndex++) {
                    var udtIon = new udtMSIonType
                    {
                        MZ = dblIonsMZ[intIndex],
                        Intensity = dblIonsIntensity[intIndex]
                    };

                    lstIons.Add(udtIon);
                }
            }

            return AddScan(intScanNumber, intMSLevel, sngScanTimeMinutes, lstIons);

        }

        public bool AddScan(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, List<udtMSIonType> lstIons)
        {
            try
            {
                if (lstIons.Count == 0)
                {
                    // No data to add
                    return false;
                }

                // Make sure the data is sorted by m/z
                for (var intIndex = 1; intIndex <= lstIons.Count - 1; intIndex++)
                {
                    if (!(lstIons[intIndex].MZ < lstIons[intIndex - 1].MZ))
                    {
                        continue;
                    }

                    // May need to sort the data
                    // However, if the intensity of both data points is zero, then we can simply swap the data
                    if (Math.Abs(lstIons[intIndex].Intensity - 0) < double.Epsilon && Math.Abs(lstIons[intIndex - 1].Intensity - 0) < double.Epsilon)
                    {
                        // Swap the m/z values
                        var udtSwapVal = lstIons[intIndex];
                        lstIons[intIndex] = lstIons[intIndex - 1];
                        lstIons[intIndex - 1] = udtSwapVal;
                    }
                    else
                    {
                        // Need to sort
                        mSortingWarnCount += 1;
                        if (mSortingWarnCount <= 10)
                        {
                            Console.WriteLine("  Sorting m/z data (this typically shouldn't be required for Finnigan data, though can occur for high res orbitrap data)");
                        }
                        else if (mSortingWarnCount % 100 == 0)
                        {
                            Console.WriteLine("  Sorting m/z data (i = " + mSortingWarnCount + ")");
                        }
                        lstIons.Sort(new udtMSIonTypeComparer());
                        break;
                    }
                }

                var dblIonsMZFiltered = new double[lstIons.Count];
                var sngIonsIntensityFiltered = new float[lstIons.Count];
                var bytCharge = new byte[lstIons.Count];

                // Populate dblIonsMZFiltered & sngIonsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

                var intIonCountNew = 0;
                for (var intIndex = 0; intIndex <= lstIons.Count - 1; intIndex++)
                {
                    if (lstIons[intIndex].Intensity > 0 && lstIons[intIndex].Intensity >= mOptions.MinIntensity)
                    {
                        dblIonsMZFiltered[intIonCountNew] = lstIons[intIndex].MZ;

                        if (lstIons[intIndex].Intensity > float.MaxValue)
                        {
                            sngIonsIntensityFiltered[intIonCountNew] = float.MaxValue;
                        }
                        else
                        {
                            sngIonsIntensityFiltered[intIonCountNew] = (float)lstIons[intIndex].Intensity;
                        }

                        bytCharge[intIonCountNew] = lstIons[intIndex].Charge;

                        intIonCountNew += 1;
                    }
                }

                AddScanCheckData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCountNew, dblIonsMZFiltered, sngIonsIntensityFiltered, bytCharge);

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScan: " + ex.Message, ex);
                return false;
            }

            return true;

        }

        private void AddScanCheckData(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, int intIonCount, double[] dblIonsMZFiltered, float[] sngIonsIntensityFiltered, byte[] bytChargeFiltered)
        {
            // Check whether any of the data points is less than mOptions.MZResolution m/z units apart
            var blnCentroidRequired = false;
            for (var intIndex = 0; intIndex <= intIonCount - 2; intIndex++)
            {
                if (dblIonsMZFiltered[intIndex + 1] - dblIonsMZFiltered[intIndex] < mOptions.MZResolution)
                {
                    blnCentroidRequired = true;
                    break;
                }
            }

            if (blnCentroidRequired)
            {
                // Consolidate any points closer than mOptions.MZResolution m/z units
                CentroidMSData(mOptions.MZResolution, ref intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered);
            }

            // Instantiate a new ScanData var for this scan
            var objScanData = new clsScanData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered);

            var intMaxAllowableIonCount = MAX_ALLOWABLE_ION_COUNT;
            if (objScanData.IonCount > intMaxAllowableIonCount)
            {
                // Do not keep more than 50,000 ions
                mSpectraFoundExceedingMaxIonCount += 1;

                // Display a message at the console the first 10 times we encounter spectra with over intMaxAllowableIonCount ions
                // In addition, display a new message every time a new max value is encountered
                if (mSpectraFoundExceedingMaxIonCount <= 10 || objScanData.IonCount > mMaxIonCountReported)
                {
                    Console.WriteLine();
                    Console.WriteLine("Note: Scan " + intScanNumber + " has " + objScanData.IonCount + " ions; will only retain " + intMaxAllowableIonCount + " (trimmed " + mSpectraFoundExceedingMaxIonCount + " spectra)");

                    mMaxIonCountReported = objScanData.IonCount;
                }

                DiscardDataToLimitIonCount(objScanData, 0, 0, intMaxAllowableIonCount);
            }

            mScans.Add(objScanData);
            mPointCountCached += objScanData.IonCount;

            if (mPointCountCached > mOptions.MaxPointsToPlot * 5)
            {
                // Too many data points are being tracked; trim out the low abundance ones

                // However, only repeat the trim if the number of cached data points has increased by 10%
                // This helps speed up program execution by avoiding trimming data after every new scan is added

                if (mPointCountCached > mPointCountCachedAfterLastTrim * 1.1)
                {
                    // Step through the scans and reduce the number of points in memory
                    TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum);

                }
            }

        }

        public bool AddScanSkipFilters(clsScanData objSourceData)
        {

            bool blnSuccess;

            try
            {
                if (objSourceData == null || objSourceData.IonCount <= 0)
                {
                    // No data to add
                    return false;
                }

                // Copy the data in objSourceScan
                var objScanData = new clsScanData(objSourceData.ScanNumber, objSourceData.MSLevel, objSourceData.ScanTimeMinutes, objSourceData.IonCount, objSourceData.IonsMZ, objSourceData.IonsIntensity, objSourceData.Charge);

                mScans.Add(objScanData);
                mPointCountCached += objScanData.IonCount;

                if (mPointCountCached > mOptions.MaxPointsToPlot * 5)
                {
                    // Too many data points are being tracked; trim out the low abundance ones

                    // However, only repeat the trim if the number of cached data points has increased by 10%
                    // This helps speed up program execution by avoiding trimming data after every new scan is added

                    if (mPointCountCached > mPointCountCachedAfterLastTrim * 1.1)
                    {
                        // Step through the scans and reduce the number of points in memory
                        TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum);

                    }
                }

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScanSkipFilters: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public void ClearRecentFileInfo()
        {
            mRecentFiles.Clear();
        }

        public float ComputeAverageIntensityAllScans(int intMSLevelFilter)
        {
            if (intMSLevelFilter > 0)
            {
                ValidateMSLevel();
            }

            if (mPointCountCached > mOptions.MaxPointsToPlot)
            {
                // Need to step through the scans and reduce the number of points in memory

                // Note that the number of data points remaining after calling this function may still be
                //  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum
                //  (see TrimCachedData for more details)

                TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum);

            }

            var intDataCount = 0;
            double dblIntensitySum = 0;

            for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++)
            {

                if (intMSLevelFilter == 0 || mScans[intScanIndex].MSLevel == intMSLevelFilter)
                {
                    for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++)
                    {
                        dblIntensitySum += mScans[intScanIndex].IonsIntensity[intIonIndex];
                        intDataCount += 1;
                    }
                }
            }

            if (intDataCount > 0)
            {
                return (float)dblIntensitySum / intDataCount;
            }

            return 0;
        }

        private void CentroidMSData(float sngMZResolution, ref int intIonCount, double[] dblIonsMZ, float[] sngIonsIntensity, byte[] bytChargeFiltered)
        {
            if (sngMZResolution <= 0)
            {
                // Nothing to do
                return;
            }

            try
            {
                var sngIntensitySorted = new float[intIonCount];
                var intPointerArray = new int[intIonCount];

                for (var intIndex = 0; intIndex <= intIonCount - 1; intIndex++)
                {
                    if (sngIonsIntensity[intIndex] < 0)
                    {
                        // Do not allow for negative intensities; change it to 0
                        sngIonsIntensity[intIndex] = 0;
                    }
                    sngIntensitySorted[intIndex] = sngIonsIntensity[intIndex];
                    intPointerArray[intIndex] = intIndex;
                }

                // Sort by ascending intensity
                Array.Sort(sngIntensitySorted, intPointerArray);

                // Now process the data from the highest intensity to the lowest intensity
                // As each data point is processed, we will either:
                //  a) set its intensity to the negative of the actual intensity to mark it as being processed
                //  b) set its intensity to Single.MinValue (-3.40282347E+38) if the point is to be removed
                //     because it is within sngMZResolution m/z units of a point with a higher intensity

                var intPointerIndex = intIonCount - 1;

                while (intPointerIndex >= 0)
                {
                    var intIndex = intPointerArray[intPointerIndex];

                    if (sngIonsIntensity[intIndex] > 0)
                    {
                        // This point has not yet been processed

                        // Examine adjacent data points to the left (lower m/z)
                        var intIndexAdjacent = intIndex - 1;
                        while (intIndexAdjacent >= 0)
                        {
                            if (dblIonsMZ[intIndex] - dblIonsMZ[intIndexAdjacent] < sngMZResolution)
                            {
                                // Mark this data point for removal since it is too close to the point at intIndex
                                if (sngIonsIntensity[intIndexAdjacent] > 0)
                                {
                                    sngIonsIntensity[intIndexAdjacent] = float.MinValue;
                                }
                            }
                            else
                            {
                                break;
                            }
                            intIndexAdjacent -= 1;
                        }

                        // Examine adjacent data points to the right (higher m/z)
                        intIndexAdjacent = intIndex + 1;
                        while (intIndexAdjacent < intIonCount)
                        {
                            if (dblIonsMZ[intIndexAdjacent] - dblIonsMZ[intIndex] < sngMZResolution)
                            {
                                // Mark this data point for removal since it is too close to the point at intIndex
                                if (sngIonsIntensity[intIndexAdjacent] > 0)
                                {
                                    sngIonsIntensity[intIndexAdjacent] = float.MinValue;
                                }
                            }
                            else
                            {
                                break;
                            }
                            intIndexAdjacent += 1;
                        }

                        sngIonsIntensity[intIndex] = -sngIonsIntensity[intIndex];
                    }
                    intPointerIndex -= 1;
                }

                // Now consolidate the data by copying in place
                var intIonCountNew = 0;
                for (var intIndex = 0; intIndex <= intIonCount - 1; intIndex++)
                {
                    if (sngIonsIntensity[intIndex] > float.MinValue)
                    {
                        // Keep this point; need to flip the intensity back to being positive
                        dblIonsMZ[intIonCountNew] = dblIonsMZ[intIndex];
                        sngIonsIntensity[intIonCountNew] = -sngIonsIntensity[intIndex];
                        bytChargeFiltered[intIonCountNew] = bytChargeFiltered[intIndex];
                        intIonCountNew += 1;
                    }
                }
                intIonCount = intIonCountNew;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.CentroidMSData: " + ex.Message, ex);
            }

        }

        private void DiscardDataToLimitIonCount(clsScanData objMSSpectrum, double dblMZIgnoreRangeStart, double dblMZIgnoreRangeEnd, int intMaxIonCountToRetain)
        {
            // When this is true, then will write a text file of the mass spectrum before before and after it is filtered
            // Used for debugging
            var blnWriteDebugData = false;
            StreamWriter swOutFile = null;

            try
            {
                bool blnMZIgnoreRangleEnabled;
                if (dblMZIgnoreRangeStart > 0 | dblMZIgnoreRangeEnd > 0)
                {
                    blnMZIgnoreRangleEnabled = true;
                }
                else
                {
                    blnMZIgnoreRangleEnabled = false;
                }

                int intIonCountNew;
                if (objMSSpectrum.IonCount > intMaxIonCountToRetain)
                {
                    var objFilterDataArray = new clsFilterDataArrayMaxCount()
                    {
                        MaximumDataCountToLoad = intMaxIonCountToRetain,
                        TotalIntensityPercentageFilterEnabled = false
                    };

                    if (blnWriteDebugData)
                    {
                        swOutFile = new StreamWriter(new FileStream("DataDump_" + objMSSpectrum.ScanNumber.ToString() + "_BeforeFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                        swOutFile.WriteLine("m/z" + '\t' + "Intensity");
                    }

                    // Store the intensity values in objFilterDataArray
                    for (var intIonIndex = 0; intIonIndex <= objMSSpectrum.IonCount - 1; intIonIndex++)
                    {
                        objFilterDataArray.AddDataPoint(objMSSpectrum.IonsIntensity[intIonIndex], intIonIndex);
                        if (blnWriteDebugData)
                        {
                            swOutFile.WriteLine(objMSSpectrum.IonsMZ[intIonIndex] + '\t' + objMSSpectrum.IonsIntensity[intIonIndex]);
                        }
                    }

                    if (blnWriteDebugData)
                    {
                        swOutFile.Close();
                    }

                    // Call .FilterData, which will determine which data points to keep
                    objFilterDataArray.FilterData();

                    intIonCountNew = 0;

                    for (var intIonIndex = 0; intIonIndex <= objMSSpectrum.IonCount - 1; intIonIndex++)
                    {
                        bool blnPointPassesFilter;
                        if (blnMZIgnoreRangleEnabled)
                        {
                            if (objMSSpectrum.IonsMZ[intIonIndex] <= dblMZIgnoreRangeEnd && objMSSpectrum.IonsMZ[intIonIndex] >= dblMZIgnoreRangeStart)
                            {
                                // The m/z value is between dblMZIgnoreRangeStart and dblMZIgnoreRangeEnd
                                // Keep this point
                                blnPointPassesFilter = true;
                            }
                            else
                            {
                                blnPointPassesFilter = false;
                            }
                        }
                        else
                        {
                            blnPointPassesFilter = false;
                        }

                        if (!blnPointPassesFilter)
                        {
                            // See if the point's intensity is negative
                            if (objFilterDataArray.GetAbundanceByIndex(intIonIndex) >= 0)
                            {
                                blnPointPassesFilter = true;
                            }
                        }

                        if (blnPointPassesFilter)
                        {
                            objMSSpectrum.IonsMZ[intIonCountNew] = objMSSpectrum.IonsMZ[intIonIndex];
                            objMSSpectrum.IonsIntensity[intIonCountNew] = objMSSpectrum.IonsIntensity[intIonIndex];
                            objMSSpectrum.Charge[intIonCountNew] = objMSSpectrum.Charge[intIonIndex];
                            intIonCountNew += 1;
                        }

                    }
                }
                else
                {
                    intIonCountNew = objMSSpectrum.IonCount;
                }

                if (intIonCountNew < objMSSpectrum.IonCount)
                {
                    objMSSpectrum.IonCount = intIonCountNew;
                }

                if (blnWriteDebugData)
                {
                    swOutFile = new StreamWriter(new FileStream("DataDump_" + objMSSpectrum.ScanNumber.ToString() + "_PostFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                    swOutFile.WriteLine("m/z" + '\t' + "Intensity");

                    // Store the intensity values in objFilterDataArray
                    for (var intIonIndex = 0; intIonIndex <= objMSSpectrum.IonCount - 1; intIonIndex++)
                    {
                        swOutFile.WriteLine(objMSSpectrum.IonsMZ[intIonIndex] + '\t' + objMSSpectrum.IonsIntensity[intIonIndex]);
                    }
                    swOutFile.Close();
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error in clsLCMSDataPlotter.DiscardDataToLimitIonCount: " + ex.Message, ex);
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
        /// <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
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

        /// <summary>
        /// Returns the cached scan data for the scan index
        /// </summary>
        /// <param name="intIndex"></param>
        /// <returns>ScanData class</returns>
        /// <remarks></remarks>
        public clsScanData GetCachedScanByIndex(int intIndex)
        {
            if (intIndex >= 0 && intIndex < mScans.Count)
            {
                return mScans[intIndex];
            }

            return null;
        }

        public void Reset()
        {
            mPointCountCached = 0;
            mPointCountCachedAfterLastTrim = 0;

            mScans.Clear();
            ClearRecentFileInfo();
        }

        ///  <summary>
        ///  Filters the data stored in mScans to nominally retain the top intTargetDataPointCount data points, sorted by descending intensity
        ///  </summary>
        ///  <param name="intTargetDataPointCount">Target max number of data points (see remarks for caveat)</param>
        /// <param name="intMinPointsPerSpectrum"></param>
        /// <remarks>
        /// Note that the number of data points remaining after calling this function may still be
        ///  more than intTargetDataPointCount, depending on intMinPointsPerSpectrum .
        /// For example, if intMinPointsPerSpectrum = 5 and we have 5000 scans, then there will be
        ///  at least 5*5000 = 25000 data points in memory.  If intTargetDataPointCount = 10000, then
        ///  there could be as many as 25000 + 10000 = 25000 points in memory
        /// </remarks>
        private void TrimCachedData(int intTargetDataPointCount, int intMinPointsPerSpectrum)
        {

            try
            {
                var objFilterDataArray = new clsFilterDataArrayMaxCount
                {
                    MaximumDataCountToLoad = intTargetDataPointCount,
                    TotalIntensityPercentageFilterEnabled = false
                };

                // Store the intensity values for each scan in objFilterDataArray
                // However, skip scans for which there are <= intMinPointsPerSpectrum data points

                var intMasterIonIndex = 0;
                for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++)
                {
                    if (mScans[intScanIndex].IonCount > intMinPointsPerSpectrum)
                    {
                        // Store the intensity values in objFilterDataArray
                        for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++)
                        {
                            objFilterDataArray.AddDataPoint(mScans[intScanIndex].IonsIntensity[intIonIndex], intMasterIonIndex);
                            intMasterIonIndex += 1;
                        }
                    }
                }

                // Call .FilterData, which will determine which data points to keep
                objFilterDataArray.FilterData();

                // Step through the scans and trim the data as needed
                intMasterIonIndex = 0;
                mPointCountCached = 0;

                for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++)
                {
                    if (mScans[intScanIndex].IonCount <= intMinPointsPerSpectrum)
                    {
                        // Skip this can since it has too few points
                        // No need to update intMasterIonIndex since it was skipped above when calling objFilterDataArray.AddDataPoint

                    }
                    else
                    {
                        // See if fewer than intMinPointsPerSpectrum points will remain after filtering
                        // If so, we'll need to handle this scan differently

                        var intMasterIonIndexStart = intMasterIonIndex;

                        var intIonCountNew = 0;
                        for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++)
                        {
                            // If the point's intensity is >= 0, then we keep it
                            if (objFilterDataArray.GetAbundanceByIndex(intMasterIonIndex) >= 0)
                            {
                                intIonCountNew += 1;
                            }
                            intMasterIonIndex += 1;
                        }

                        if (intIonCountNew < intMinPointsPerSpectrum)
                        {
                            // Too few points will remain after filtering
                            // Retain the top intMinPointsPerSpectrum points in this spectrum

                            DiscardDataToLimitIonCount(mScans[intScanIndex], 0, 0, intMinPointsPerSpectrum);

                        }
                        else
                        {
                            // It's safe to filter the data

                            // Reset intMasterIonIndex to the saved value
                            intMasterIonIndex = intMasterIonIndexStart;

                            intIonCountNew = 0;

                            for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++)
                            {
                                // If the point's intensity is >= 0, then we keep it

                                if (objFilterDataArray.GetAbundanceByIndex(intMasterIonIndex) >= 0)
                                {
                                    // Copying in place (don't actually need to copy unless intIonCountNew <> intIonIndex)
                                    if (intIonCountNew != intIonIndex)
                                    {
                                        mScans[intScanIndex].IonsMZ[intIonCountNew] = mScans[intScanIndex].IonsMZ[intIonIndex];
                                        mScans[intScanIndex].IonsIntensity[intIonCountNew] = mScans[intScanIndex].IonsIntensity[intIonIndex];
                                        mScans[intScanIndex].Charge[intIonCountNew] = mScans[intScanIndex].Charge[intIonIndex];
                                    }

                                    intIonCountNew += 1;
                                }

                                intMasterIonIndex += 1;
                            }

                            mScans[intScanIndex].IonCount = intIonCountNew;

                        }

                        if (mScans[intScanIndex].IonsMZ.Length > 5 && mScans[intScanIndex].IonCount < mScans[intScanIndex].IonsMZ.Length / 2.0)
                        {
                            // Shrink the arrays to reduce the memory footprint
                            mScans[intScanIndex].ShrinkArrays();

                            if (DateTime.UtcNow.Subtract(mLastGCTime).TotalSeconds > 60)
                            {
                                // Perform garbage collection every 60 seconds
                                mLastGCTime = DateTime.UtcNow;
                                clsProgRunner.GarbageCollectNow();
                            }

                        }

                    }

                    // Bump up the total point count cached
                    mPointCountCached += mScans[intScanIndex].IonCount;

                }

                // Update mPointCountCachedAfterLastTrim
                mPointCountCachedAfterLastTrim = mPointCountCached;

            }
            catch (Exception ex)
            {
                throw new Exception("Error in clsLCMSDataPlotter.TrimCachedData: " + ex.Message, ex);
            }

        }

        private void UpdateMinMax(float sngValue, ref float sngMin, ref float sngMax)
        {
            if (sngValue < sngMin)
            {
                sngMin = sngValue;
            }

            if (sngValue > sngMax)
            {
                sngMax = sngValue;
            }
        }

        private void UpdateMinMax(double dblValue, ref double dblMin, ref double dblMax)
        {
            if (dblValue < dblMin)
            {
                dblMin = dblValue;
            }

            if (dblValue > dblMax)
            {
                dblMax = dblValue;
            }
        }

        private void ValidateMSLevel()
        {
            var blnMSLevelDefined = false;

            for (var intIndex = 0; intIndex <= mScans.Count - 1; intIndex++)
            {
                if (mScans[intIndex].MSLevel > 0)
                {
                    blnMSLevelDefined = true;
                    break;
                }
            }

            if (!blnMSLevelDefined)
            {
                // Set the MSLevel to 1 for all scans
                for (var intIndex = 0; intIndex <= mScans.Count - 1; intIndex++)
                {
                    mScans[intIndex].UpdateMSLevel(1);
                }
            }

        }

        #region "Plotting Functions"

        private void AddSeriesMonoMassVsScan(IList<List<ScatterPoint>> lstPointsByCharge, PlotModel myPlot)
        {
            // Determine the number of data points to be plotted
            var intTotalPoints = 0;
            for (var intCharge = 0; intCharge <= lstPointsByCharge.Count - 1; intCharge++)
            {
                intTotalPoints += lstPointsByCharge[intCharge].Count;
            }

            for (var intCharge = 0; intCharge <= lstPointsByCharge.Count - 1; intCharge++)
            {
                if (lstPointsByCharge[intCharge].Count == 0)
                    continue;

                var strTitle = intCharge + "+";

                var seriesColor = clsPlotContainer.GetColorByCharge(intCharge);

                var series = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerFill = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B),
                    Title = strTitle
                };

                // series.MarkerStroke = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B)

                // Customize the points
                if (mScans.Count < 250)
                {
                    // Use a point size of 2 when fewer than 250 scans
                    series.MarkerSize = 2;
                }
                else if (mScans.Count < 500)
                {
                    // Use a point size of 1 when 250 to 500 scans
                    series.MarkerSize = 1;
                }
                else
                {
                    // Use a point size of 0.8 or 0.6 when >= 500 scans
                    if (intTotalPoints < 80000)
                    {
                        series.MarkerSize = 0.8;
                    }
                    else
                    {
                        series.MarkerSize = 0.6;
                    }
                }

                series.Points.AddRange(lstPointsByCharge[intCharge]);

                myPlot.Series.Add(series);
            }

        }

        private void AddSeriesMzVsScan(string strTitle, IEnumerable<ScatterPoint> objPoints, float sngColorScaleMinIntensity, float sngColorScaleMaxIntensity, PlotModel myPlot)
        {
            // We use a linear color axis to color the data points based on intensity
            var colorAxis = new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Minimum = sngColorScaleMinIntensity,
                Maximum = sngColorScaleMaxIntensity,
                Palette = OxyPalettes.Jet(30),
                IsAxisVisible = false
            };

            myPlot.Axes.Add(colorAxis);

            var series = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                Title = strTitle
            };

            // Customize the point size
            if (mScans.Count < 250)
            {
                // Use a point size of 2 when fewer than 250 scans
                series.MarkerSize = 2;
            }
            else if (mScans.Count < 5000)
            {
                // Use a point size of 1 when 250 to 5000 scans
                series.MarkerSize = 1;
            }
            else
            {
                // Use a point size of 0.6 when >= 5000 scans
                series.MarkerSize = 0.6;
            }

            series.Points.AddRange(objPoints);

            myPlot.Series.Add(series);
        }

        private float ComputeMedian(float[] sngList, int intItemCount)
        {
            var blnAverage = false;

            if (sngList == null || sngList.Length < 1 || intItemCount < 1)
            {
                // List is empty (or intItemCount = 0)
                return 0;
            }

            if (intItemCount <= 1)
            {
                // Only 1 item; the median is the value
                return sngList[0];
            }

            // Sort sngList ascending, then find the midpoint
            Array.Sort(sngList, 0, intItemCount);

            int intMidpointIndex;
            if (intItemCount % 2 == 0)
            {
                // Even number
                intMidpointIndex = (int)Math.Floor(intItemCount / 2.0) - 1;
                blnAverage = true;
            }
            else
            {
                // Odd number
                intMidpointIndex = (int)Math.Floor(intItemCount / 2.0);
            }

            if (intMidpointIndex > intItemCount)
                intMidpointIndex = intItemCount - 1;
            if (intMidpointIndex < 0)
                intMidpointIndex = 0;

            if (blnAverage)
            {
                // Even number of items
                // Return the average of the two middle points
                return (sngList[intMidpointIndex] + sngList[intMidpointIndex + 1]) / 2;
            }

            // Odd number of items
            return sngList[intMidpointIndex];
        }

        private List<List<ScatterPoint>> GetMonoMassSeriesByCharge(int intMsLevelFilter, out double dblMinMz, out double dblMaxMz, out double dblScanTimeMax, out int intMinScan, out int intMaxScan)
        {
            intMinScan = int.MaxValue;
            intMaxScan = 0;
            dblMinMz = float.MaxValue;
            dblMaxMz = 0;

            var dblScanTimeMin = double.MaxValue;
            dblScanTimeMax = 0;

            // Determine the maximum charge state
            byte intMaxCharge = 1;

            for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++) {
                if (intMsLevelFilter == 0 || mScans[intScanIndex].MSLevel == intMsLevelFilter) {
                    if (mScans[intScanIndex].Charge.Length > 0) {
                        for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++) {
                            intMaxCharge = Math.Max(intMaxCharge, mScans[intScanIndex].Charge[intIonIndex]);
                        }
                    }
                }
            }

            // Initialize the data for each charge state
            var lstSeries = new List<List<ScatterPoint>>();

            for (var intCharge = 0; intCharge <= intMaxCharge; intCharge++) {
                lstSeries.Add(new List<ScatterPoint>());
            }

            // Store the data, segregating by charge
            for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++) {

                if (intMsLevelFilter == 0 || mScans[intScanIndex].MSLevel == intMsLevelFilter) {
                    for (var intIonIndex = 0; intIonIndex <= mScans[intScanIndex].IonCount - 1; intIonIndex++) {
                        var dataPoint = new ScatterPoint(mScans[intScanIndex].ScanNumber,
                                                         mScans[intScanIndex].IonsMZ[intIonIndex])
                        {
                            Value = mScans[intScanIndex].IonsIntensity[intIonIndex]
                        };

                        lstSeries[mScans[intScanIndex].Charge[intIonIndex]].Add(dataPoint);

                        UpdateMinMax(mScans[intScanIndex].IonsMZ[intIonIndex], ref dblMinMz, ref dblMaxMz);

                    }

                    UpdateMinMax(mScans[intScanIndex].ScanTimeMinutes, ref dblScanTimeMin, ref dblScanTimeMax);

                    if (mScans[intScanIndex].ScanNumber < intMinScan)
                    {
                        intMinScan = mScans[intScanIndex].ScanNumber;
                    }

                    if (mScans[intScanIndex].ScanNumber > intMaxScan)
                    {
                        intMaxScan = mScans[intScanIndex].ScanNumber;
                    }
                }

            }

            return lstSeries;

        }

        private List<ScatterPoint> GetMzVsScanSeries(int intMSLevelFilter, out float sngColorScaleMinIntensity, out float sngColorScaleMaxIntensity, out double dblMinMZ, out double dblMaxMZ, out double dblScanTimeMax, out int intMinScan, out int intMaxScan, bool blnWriteDebugData, StreamWriter swDebugFile)
        {

            var objPoints = new List<ScatterPoint>();

            double dblIntensitySum = 0;
            var intSortedIntensityListCount = 0;
            var sngSortedIntensityList = new float[mPointCountCached + 1];

            sngColorScaleMinIntensity = float.MaxValue;
            sngColorScaleMaxIntensity = 0;

            intMinScan = int.MaxValue;
            intMaxScan = 0;
            dblMinMZ = float.MaxValue;
            dblMaxMZ = 0;

            var dblScanTimeMin = double.MaxValue;
            dblScanTimeMax = 0;

            for (var intScanIndex = 0; intScanIndex <= mScans.Count - 1; intScanIndex++) {

                if (intMSLevelFilter != 0 && mScans[intScanIndex].MSLevel != intMSLevelFilter)
                {
                    continue;
                }

                for (var intIonIndex = 0; intIonIndex <=  mScans[intScanIndex].IonCount - 1; intIonIndex++) {
                    if (intSortedIntensityListCount >= sngSortedIntensityList.Length) {
                        // Need to reserve more room (this is unexpected)
                        Array.Resize(ref sngSortedIntensityList, sngSortedIntensityList.Length * 2);
                    }

                    sngSortedIntensityList[intSortedIntensityListCount] = mScans[intScanIndex].IonsIntensity[intIonIndex];
                    dblIntensitySum += sngSortedIntensityList[intSortedIntensityListCount];

                    var dataPoint = new ScatterPoint(mScans[intScanIndex].ScanNumber,
                                                     mScans[intScanIndex].IonsMZ[intIonIndex])
                    {
                        Value = mScans[intScanIndex].IonsIntensity[intIonIndex]
                    };

                    objPoints.Add(dataPoint);

                    if (blnWriteDebugData) {
                        swDebugFile.WriteLine( mScans[intScanIndex].ScanNumber + '\t' +  mScans[intScanIndex].IonsMZ[intIonIndex] + '\t' +  mScans[intScanIndex].IonsIntensity[intIonIndex]);
                    }

                    UpdateMinMax(sngSortedIntensityList[intSortedIntensityListCount], ref sngColorScaleMinIntensity, ref sngColorScaleMaxIntensity);
                    UpdateMinMax( mScans[intScanIndex].IonsMZ[intIonIndex], ref dblMinMZ, ref dblMaxMZ);

                    intSortedIntensityListCount += 1;
                }

                UpdateMinMax( mScans[intScanIndex].ScanTimeMinutes, ref dblScanTimeMin, ref dblScanTimeMax);

                if ( mScans[intScanIndex].ScanNumber < intMinScan) {
                    intMinScan =  mScans[intScanIndex].ScanNumber;
                }

                if ( mScans[intScanIndex].ScanNumber > intMaxScan) {
                    intMaxScan =  mScans[intScanIndex].ScanNumber;
                }
            }

            if (objPoints.Count > 0) {
                // Compute median and average intensity values
                if (intSortedIntensityListCount > 0) {
                    Array.Sort(sngSortedIntensityList, 0, intSortedIntensityListCount);
                    var sngMedianIntensity = ComputeMedian(sngSortedIntensityList, intSortedIntensityListCount);

                    // Set the minimum color intensity to the median
                    sngColorScaleMinIntensity = sngMedianIntensity;
                }

            }

            return objPoints;
        }

        /// <summary>
        /// When PlottingDeisotopedData is False, creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
        /// When PlottingDeisotopedData is True, creates a 2D plot of monoisotopic mass vs. scan number, using charge state as the 3rd dimension to color the data points
        /// </summary>
        /// <param name="strTitle">Title of the plot</param>
        /// <param name="intMSLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="blnSkipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for intMSLevelFilter, set blnSkipTrimCachedData to False on the first call and True on subsequent calls)</param>
        /// <returns>OxyPlot PlotContainer</returns>
        /// <remarks></remarks>
        private clsPlotContainer InitializePlot(string strTitle, int intMSLevelFilter, bool blnSkipTrimCachedData)
        {
            int intMinScan;
            int intMaxScan;

            float sngColorScaleMinIntensity = 0;
            float sngColorScaleMaxIntensity = 0;

            double dblMinMZ;
            double dblMaxMZ;

            double dblScanTimeMax;

            if (!blnSkipTrimCachedData && mPointCountCached > mOptions.MaxPointsToPlot) {
                // Need to step through the scans and reduce the number of points in memory

                // Note that the number of data points remaining after calling this function may still be
                //  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum
                //  (see TrimCachedData for more details)

                TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum);

            }

            // When this is true, then will write a text file of the mass spectrum before before and after it is filtered
            // Used for debugging
            var blnWriteDebugData = false;
            StreamWriter swDebugFile = null;

            if (blnWriteDebugData) {
                swDebugFile = new StreamWriter(new FileStream(strTitle + " - LCMS Top " + IntToEngineeringNotation(mOptions.MaxPointsToPlot) + " points.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                swDebugFile.WriteLine("scan" + '\t' + "m/z" + '\t' + "Intensity");
            }

            // Populate objPoints and objScanTimePoints with the data
            // At the same time, determine the range of m/z and intensity values
            // Lastly, compute the median and average intensity values

            // Instantiate the list to track the data points
            var lstPointsByCharge = new List<List<ScatterPoint>>();

            if (mOptions.PlottingDeisotopedData) {
                lstPointsByCharge = GetMonoMassSeriesByCharge(intMSLevelFilter, out dblMinMZ, out dblMaxMZ, out dblScanTimeMax, out intMinScan, out intMaxScan);
            } else {
                var objPoints = GetMzVsScanSeries(intMSLevelFilter, out sngColorScaleMinIntensity, out sngColorScaleMaxIntensity, out dblMinMZ, out dblMaxMZ, out dblScanTimeMax, out intMinScan, out intMaxScan, blnWriteDebugData, swDebugFile);
                lstPointsByCharge.Add(objPoints);
            }

            if (blnWriteDebugData) {
                swDebugFile.Close();
            }

            var dblMaxMzToUse = double.MaxValue;
            if (mOptions.PlottingDeisotopedData) {
                dblMaxMzToUse = mOptions.MaxMonoMassForDeisotopedPlot;
            }

            // Count the actual number of points that will be plotted
            var intPointsToPlot = 0;
            foreach (var objSeries in lstPointsByCharge) {
                foreach (var item in objSeries) {
                    if (item.Y < dblMaxMzToUse) {
                        intPointsToPlot += 1;
                    }
                }
            }

            if (intPointsToPlot == 0) {
                // Nothing to plot
                return new clsPlotContainer(new PlotModel());
            }

            // Round intMinScan down to the nearest multiple of 10
            intMinScan = (int)Math.Floor(intMinScan / 10.0) * 10;
            if (intMinScan < 0)
                intMinScan = 0;

            // Round intMaxScan up to the nearest multiple of 10
            intMaxScan = (int)Math.Ceiling(intMaxScan / 10.0) * 10;

            // Round dblMinMZ down to the nearest multiple of 100
            dblMinMZ = (long)Math.Floor(dblMinMZ / 100.0) * 100;

            // Round dblMaxMZ up to the nearest multiple of 100
            dblMaxMZ = (long)Math.Ceiling(dblMaxMZ / 100.0) * 100;

            string yAxisLabel;
            if (mOptions.PlottingDeisotopedData) {
                yAxisLabel = "Monoisotopic Mass";
            } else {
                yAxisLabel = "m/z";
            }

            var myPlot = clsOxyplotUtilities.GetBasicPlotModel(strTitle, "LC Scan Number", yAxisLabel);

            if (mOptions.PlottingDeisotopedData) {
                AddSeriesMonoMassVsScan(lstPointsByCharge, myPlot);
                myPlot.TitlePadding = 40;
            } else {
                AddSeriesMzVsScan(strTitle, lstPointsByCharge.First(), sngColorScaleMinIntensity, sngColorScaleMaxIntensity, myPlot);
            }

            // Update the axis format codes if the data values are small or the range of data is small
            var xVals = (from item in lstPointsByCharge.First() select item.X).ToList();
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[0], xVals, true);

            var yVals = (from item in lstPointsByCharge.First() select item.Y).ToList();
            clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[1], yVals, false);

            var plotContainer = new clsPlotContainer(myPlot)
            {
                FontSizeBase = clsOxyplotUtilities.FONT_SIZE_BASE,
                AnnotationBottomLeft = intPointsToPlot.ToString("0,000") + " points plotted"
            };

            // Add a label showing the number of points displayed

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

            }

            // Override the auto-computed X axis range
            if (mOptions.UseObservedMinScan) {
                myPlot.Axes[0].Minimum = intMinScan;
            } else {
                myPlot.Axes[0].Minimum = 0;
            }

            if (intMaxScan == 0) {
                myPlot.Axes[0].Maximum = 1;
            } else {
                myPlot.Axes[0].Maximum = intMaxScan;
            }

            if (Math.Abs(myPlot.Axes[0].Minimum - myPlot.Axes[0].Maximum) < 0.01) {
                intMinScan = (int)myPlot.Axes[0].Minimum;
                myPlot.Axes[0].Minimum = intMinScan - 1;
                myPlot.Axes[0].Maximum = intMinScan + 1;
            } else if (intMinScan == intMaxScan) {
                myPlot.Axes[0].Minimum = intMinScan - 1;
                myPlot.Axes[0].Maximum = intMinScan + 1;
            }

            // Assure that we don't see ticks between scan numbers
            clsOxyplotUtilities.ValidateMajorStep(myPlot.Axes[0]);

            // Set the maximum value for the Y-axis
            if (mOptions.PlottingDeisotopedData) {
                if (dblMaxMZ < mOptions.MaxMonoMassForDeisotopedPlot) {
                    dblMaxMzToUse = dblMaxMZ;
                } else {
                    dblMaxMzToUse = mOptions.MaxMonoMassForDeisotopedPlot;
                }
            } else {
                dblMaxMzToUse = dblMaxMZ;
            }

            // Override the auto-computed axis range
            myPlot.Axes[1].Minimum = dblMinMZ;
            myPlot.Axes[1].Maximum = dblMaxMzToUse;

            // Hide the legend
            myPlot.IsLegendVisible = false;

            return plotContainer;

        }

        /// <summary>
        /// Converts an integer to engineering notation
        /// For example, 50000 will be returned as 50K
        /// </summary>
        /// <param name="intValue"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private string IntToEngineeringNotation(int intValue)
        {
            if (intValue < 1000)
            {
                return intValue.ToString();
            }

            if (intValue < 1000000.0)
            {
                return (int)Math.Round(intValue / 1000.0, 0) + "K";
            }

            return (int)Math.Round(intValue / 1000.0 / 1000, 0) + "M";
        }

        public bool Save2DPlots(string strDatasetName, string strOutputFolderPath)
        {

            return Save2DPlots(strDatasetName, strOutputFolderPath, "", "");

        }

        public bool Save2DPlots(string strDatasetName, string strOutputFolderPath, string strFileNameSuffixAddon, string strScanModeSuffixAddon)
        {

            const bool EMBED_FILTER_SETTINGS_IN_NAME = false;

            bool blnSuccess;

            try
            {
                ClearRecentFileInfo();

                // Check whether all of the spectra have .MSLevel = 0
                // If they do, change the level to 1
                ValidateMSLevel();

                if (strFileNameSuffixAddon == null)
                    strFileNameSuffixAddon = string.Empty;
                if (strScanModeSuffixAddon == null)
                    strScanModeSuffixAddon = string.Empty;

                var colorGradients = new Dictionary<string, OxyPalette>
                {
                    {"BlackWhiteRed30", OxyPalettes.BlackWhiteRed(30)},
                    {"BlueWhiteRed30", OxyPalettes.BlueWhiteRed(30)},
                    {"Cool30", OxyPalettes.Cool(30)},
                    {"Gray30", OxyPalettes.Gray(30)},
                    {"Hot30", OxyPalettes.Hot(30)},
                    {"Hue30", OxyPalettes.Hue(30)},
                    {"HueDistinct30", OxyPalettes.HueDistinct(30)},
                    {"Jet30", OxyPalettes.Jet(30)},
                    {"Rainbow30", OxyPalettes.Rainbow(30)}
                };

                var plotContainer = InitializePlot(strDatasetName + " - " + mOptions.MS1PlotTitle, 1, false);
                plotContainer.PlottingDeisotopedData = mOptions.PlottingDeisotopedData;

                if (mOptions.TestGradientColorSchemes)
                {
                    plotContainer.AddGradients(colorGradients);
                }

                string strPNGFilePath;
                if (plotContainer.SeriesCount > 0)
                {
                    if (EMBED_FILTER_SETTINGS_IN_NAME)
                    {
                        strPNGFilePath = strDatasetName + "_" + strFileNameSuffixAddon + "LCMS_" + mOptions.MaxPointsToPlot + "_" + mOptions.MinPointsPerSpectrum + "_" + mOptions.MZResolution.ToString("0.00") + strScanModeSuffixAddon + ".png";
                    }
                    else
                    {
                        strPNGFilePath = strDatasetName + "_" + strFileNameSuffixAddon + "LCMS" + strScanModeSuffixAddon + ".png";
                    }
                    strPNGFilePath = Path.Combine(strOutputFolderPath, strPNGFilePath);
                    plotContainer.SaveToPNG(strPNGFilePath, 1024, 700, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMS);
                }

                plotContainer = InitializePlot(strDatasetName + " - " + mOptions.MS2PlotTitle, 2, true);
                if (plotContainer.SeriesCount > 0)
                {
                    strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName + "_" + strFileNameSuffixAddon + "LCMS_MSn" + strScanModeSuffixAddon + ".png");
                    plotContainer.SaveToPNG(strPNGFilePath, 1024, 700, 96);
                    AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMSMSn);
                }

                blnSuccess = true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.Save2DPlots: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        #endregion

        /// <summary>
        /// This class tracks the m/z and intensity values for a given scan
        /// It can optionally also track charge state
        /// Be sure to use .IonCount to determine the number of data points, not .IonsMZ.Length
        /// If you decrease .IonCount, you can optionally call .ShrinkArrays to reduce the allocated space
        /// </summary>
        /// <remarks></remarks>
        public class clsScanData
        {
            private int mMSLevel;

            public int IonCount;
            public double[] IonsMZ;
            public float[] IonsIntensity;

            public byte[] Charge;
            public int MSLevel => mMSLevel;

            public int ScanNumber { get; }

            public float ScanTimeMinutes { get; }

            public clsScanData(int intScanNumber, int intMSLevel, float sngScanTimeMinutes, int intDataCount, double[] dblIonsMZ, float[] sngIonsIntensity, byte[] bytCharge)
            {
                ScanNumber = intScanNumber;
                mMSLevel = intMSLevel;
                ScanTimeMinutes = sngScanTimeMinutes;

                IonCount = intDataCount;
                IonsMZ = new double[intDataCount];
                IonsIntensity = new float[intDataCount];
                Charge = new byte[intDataCount];

                // Populate the arrays to be filtered
                Array.Copy(dblIonsMZ, IonsMZ, intDataCount);
                Array.Copy(sngIonsIntensity, IonsIntensity, intDataCount);
                Array.Copy(bytCharge, Charge, intDataCount);
            }

            public void ShrinkArrays()
            {
                if (IonCount < IonsMZ.Length)
                {
                    Array.Resize(ref IonsMZ, IonCount);
                    Array.Resize(ref IonsIntensity, IonCount);
                    Array.Resize(ref Charge, IonCount);
                }
            }

            public void UpdateMSLevel(int NewMSLevel)
            {
                mMSLevel = NewMSLevel;
            }

        }

        public class udtMSIonTypeComparer : IComparer<udtMSIonType>
        {

            public int Compare(udtMSIonType x, udtMSIonType y)
            {
                return x.MZ.CompareTo(y.MZ);
            }
        }

    }
}
