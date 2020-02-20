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
    public class clsLCMSDataPlotter : EventNotifier
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

            /// <summary>
            /// Display the m/z, intensity, and charge
            /// </summary>
            /// <returns></returns>
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

        private readonly List<udtOutputFileInfoType> mRecentFiles;

        private int mSortingWarnCount;
        private int mSpectraFoundExceedingMaxIonCount;
        private int mMaxIonCountReported;

        private DateTime mLastGCTime;

        #endregion

        #region "Properties"

        public clsLCMSDataPlotterOptions Options { get; set; }

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
        /// <param name="options"></param>
        public clsLCMSDataPlotter(clsLCMSDataPlotterOptions options)
        {
            Options = options;
            mRecentFiles = new List<udtOutputFileInfoType>();
            mSortingWarnCount = 0;
            mSpectraFoundExceedingMaxIonCount = 0;
            mMaxIonCountReported = 0;

            mLastGCTime = DateTime.UtcNow;

            mScans = new List<clsScanData>();
        }

        private void AddRecentFile(string filePath, eOutputFileTypes eFileType)
        {
            var udtOutputFileInfo = new udtOutputFileInfoType
            {
                FileType = eFileType,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath
            };

            mRecentFiles.Add(udtOutputFileInfo);
        }

        public bool AddScan2D(int scanNumber, int msLevel, float scanTimeMinutes, int ionCount, double[,] massIntensityPairs)
        {
            try
            {
                if (ionCount <= 0)
                {
                    // No data to add
                    return false;
                }

                // Make sure the data is sorted by m/z
                for (var index = 1; index <= ionCount - 1; index++)
                {
                    // Note that massIntensityPairs[0, index) is m/z
                    //       and massIntensityPairs[1, index) is intensity
                    if (massIntensityPairs[0, index] < massIntensityPairs[0, index - 1])
                    {
                        // May need to sort the data
                        // However, if the intensity of both data points is zero, then we can simply swap the data
                        if (Math.Abs(massIntensityPairs[1, index]) < double.Epsilon && Math.Abs(massIntensityPairs[1, index - 1]) < double.Epsilon)
                        {
                            // Swap the m/z values
                            var swapVal = massIntensityPairs[0, index];
                            massIntensityPairs[0, index] = massIntensityPairs[0, index - 1];
                            massIntensityPairs[0, index - 1] = swapVal;
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

                            var ionList = new List<udtMSIonType>(ionCount - 1);

                            for (var copyIndex = 0; copyIndex <= ionCount - 1; copyIndex++)
                            {
                                var udtIon = new udtMSIonType
                                {
                                    MZ = massIntensityPairs[0, copyIndex],
                                    Intensity = massIntensityPairs[1, copyIndex]
                                };

                                ionList.Add(udtIon);
                            }

                            return AddScan(scanNumber, msLevel, scanTimeMinutes, ionList);

                        }
                    }
                }

                var ionsMZFiltered = new double[ionCount];
                var ionsIntensityFiltered = new float[ionCount];
                var chargeFiltered = new byte[ionCount];

                // Populate ionsMZFiltered and ionsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

                var ionCountNew = 0;
                for (var index = 0; index <= ionCount - 1; index++)
                {
                    if (massIntensityPairs[1, index] > 0 && massIntensityPairs[1, index] >= Options.MinIntensity)
                    {
                        ionsMZFiltered[ionCountNew] = massIntensityPairs[0, index];

                        if (massIntensityPairs[1, index] > float.MaxValue)
                        {
                            ionsIntensityFiltered[ionCountNew] = float.MaxValue;
                        }
                        else
                        {
                            ionsIntensityFiltered[ionCountNew] = (float)massIntensityPairs[1, index];
                        }

                        chargeFiltered[ionCountNew] = 0;

                        ionCountNew += 1;
                    }
                }

                AddScanCheckData(scanNumber, msLevel, scanTimeMinutes, ionCountNew, ionsMZFiltered, ionsIntensityFiltered, chargeFiltered);

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScan2D: " + ex.Message, ex);
                return false;
            }

            return true;

        }

        public bool AddScan(int scanNumber, int msLevel, float scanTimeMinutes, int ionCount, double[] ionsMZ, double[] ionsIntensity)
        {
            List<udtMSIonType> ionList;

            if (ionCount > MAX_ALLOWABLE_ION_COUNT)
            {
                Array.Sort(ionsIntensity, ionsMZ);

                var highIntensityIons = new List<udtMSIonType>(MAX_ALLOWABLE_ION_COUNT);

                for (var index = ionCount - MAX_ALLOWABLE_ION_COUNT; index <= ionCount - 1; index++)
                {
                    var udtIon = new udtMSIonType
                    {
                        MZ = ionsMZ[index],
                        Intensity = ionsIntensity[index]
                    };

                    highIntensityIons.Add(udtIon);
                }

                ionList = (from item in highIntensityIons orderby item.MZ select item).ToList();

            }
            else
            {
                ionList = new List<udtMSIonType>(ionCount);

                for (var index = 0; index <= ionCount - 1; index++)
                {
                    var udtIon = new udtMSIonType
                    {
                        MZ = ionsMZ[index],
                        Intensity = ionsIntensity[index]
                    };

                    ionList.Add(udtIon);
                }
            }

            return AddScan(scanNumber, msLevel, scanTimeMinutes, ionList);

        }

        public bool AddScan(int scanNumber, int msLevel, float scanTimeMinutes, List<udtMSIonType> ionList)
        {
            try
            {
                if (ionList.Count == 0)
                {
                    // No data to add
                    return false;
                }

                // Make sure the data is sorted by m/z
                for (var index = 1; index <= ionList.Count - 1; index++)
                {
                    if (!(ionList[index].MZ < ionList[index - 1].MZ))
                    {
                        continue;
                    }

                    // May need to sort the data
                    // However, if the intensity of both data points is zero, then we can simply swap the data
                    if (Math.Abs(ionList[index].Intensity) < double.Epsilon && Math.Abs(ionList[index - 1].Intensity) < double.Epsilon)
                    {
                        // Swap the m/z values
                        var udtSwapVal = ionList[index];
                        ionList[index] = ionList[index - 1];
                        ionList[index - 1] = udtSwapVal;
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
                        ionList.Sort(new udtMSIonTypeComparer());
                        break;
                    }
                }

                var ionsMZFiltered = new double[ionList.Count];
                var ionsIntensityFiltered = new float[ionList.Count];
                var charge = new byte[ionList.Count];

                // Populate ionsMZFiltered and ionsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

                var ionCountNew = 0;
                for (var index = 0; index <= ionList.Count - 1; index++)
                {
                    if (ionList[index].Intensity > 0 && ionList[index].Intensity >= Options.MinIntensity)
                    {
                        ionsMZFiltered[ionCountNew] = ionList[index].MZ;

                        if (ionList[index].Intensity > float.MaxValue)
                        {
                            ionsIntensityFiltered[ionCountNew] = float.MaxValue;
                        }
                        else
                        {
                            ionsIntensityFiltered[ionCountNew] = (float)ionList[index].Intensity;
                        }

                        charge[ionCountNew] = ionList[index].Charge;

                        ionCountNew += 1;
                    }
                }

                AddScanCheckData(scanNumber, msLevel, scanTimeMinutes, ionCountNew, ionsMZFiltered, ionsIntensityFiltered, charge);

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScan: " + ex.Message, ex);
                return false;
            }

            return true;

        }

        private void AddScanCheckData(int scanNumber, int msLevel, float scanTimeMinutes, int ionCount, double[] ionsMZFiltered, float[] ionsIntensityFiltered, byte[] chargeFiltered)
        {
            // Check whether any of the data points is less than mOptions.MZResolution m/z units apart
            var centroidRequired = false;
            for (var index = 0; index <= ionCount - 2; index++)
            {
                if (ionsMZFiltered[index + 1] - ionsMZFiltered[index] < Options.MZResolution)
                {
                    centroidRequired = true;
                    break;
                }
            }

            if (centroidRequired)
            {
                // Consolidate any points closer than mOptions.MZResolution m/z units
                CentroidMSData(Options.MZResolution, ref ionCount, ionsMZFiltered, ionsIntensityFiltered, chargeFiltered);
            }

            // Instantiate a new ScanData var for this scan
            var scanData = new clsScanData(scanNumber, msLevel, scanTimeMinutes, ionCount, ionsMZFiltered, ionsIntensityFiltered, chargeFiltered);

            var maxAllowableIonCount = MAX_ALLOWABLE_ION_COUNT;
            if (scanData.IonCount > maxAllowableIonCount)
            {
                // Do not keep more than 50,000 ions
                mSpectraFoundExceedingMaxIonCount += 1;

                // Display a message at the console the first 10 times we encounter spectra with over maxAllowableIonCount ions
                // In addition, display a new message every time a new max value is encountered
                if (mSpectraFoundExceedingMaxIonCount <= 10 || scanData.IonCount > mMaxIonCountReported)
                {
                    Console.WriteLine();
                    Console.WriteLine("Note: Scan " + scanNumber + " has " + scanData.IonCount + " ions; will only retain " + maxAllowableIonCount + " (trimmed " + mSpectraFoundExceedingMaxIonCount + " spectra)");

                    mMaxIonCountReported = scanData.IonCount;
                }

                DiscardDataToLimitIonCount(scanData, 0, 0, maxAllowableIonCount);
            }

            mScans.Add(scanData);
            mPointCountCached += scanData.IonCount;

            if (mPointCountCached <= Options.MaxPointsToPlot * 5)
                return;

            // Too many data points are being tracked; trim out the low abundance ones

            // However, only repeat the trim if the number of cached data points has increased by 10%
            // This helps speed up program execution by avoiding trimming data after every new scan is added

            if (mPointCountCached > mPointCountCachedAfterLastTrim * 1.1)
            {
                // Step through the scans and reduce the number of points in memory
                TrimCachedData(Options.MaxPointsToPlot, Options.MinPointsPerSpectrum);

            }
        }

        public bool AddScanSkipFilters(clsScanData sourceData)
        {

            bool success;

            try
            {
                if (sourceData == null || sourceData.IonCount <= 0)
                {
                    // No data to add
                    return false;
                }

                // Copy the data in sourceScan
                var scanData = new clsScanData(sourceData.ScanNumber, sourceData.MSLevel, sourceData.ScanTimeMinutes, sourceData.IonCount, sourceData.IonsMZ, sourceData.IonsIntensity, sourceData.Charge);

                mScans.Add(scanData);
                mPointCountCached += scanData.IonCount;

                if (mPointCountCached > Options.MaxPointsToPlot * 5)
                {
                    // Too many data points are being tracked; trim out the low abundance ones

                    // However, only repeat the trim if the number of cached data points has increased by 10%
                    // This helps speed up program execution by avoiding trimming data after every new scan is added

                    if (mPointCountCached > mPointCountCachedAfterLastTrim * 1.1)
                    {
                        // Step through the scans and reduce the number of points in memory
                        TrimCachedData(Options.MaxPointsToPlot, Options.MinPointsPerSpectrum);

                    }
                }

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.AddScanSkipFilters: " + ex.Message, ex);
                success = false;
            }

            return success;

        }

        public void ClearRecentFileInfo()
        {
            mRecentFiles.Clear();
        }

        public float ComputeAverageIntensityAllScans(int msLevelFilter)
        {
            if (msLevelFilter > 0)
            {
                ValidateMSLevel();
            }

            if (mPointCountCached > Options.MaxPointsToPlot)
            {
                // Need to step through the scans and reduce the number of points in memory

                // Note that the number of data points remaining after calling this function may still be
                //  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum
                //  (see TrimCachedData for more details)

                TrimCachedData(Options.MaxPointsToPlot, Options.MinPointsPerSpectrum);
            }

            var dataToAverage = new List<double>();

            for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
            {
                if (msLevelFilter != 0 && mScans[scanIndex].MSLevel != msLevelFilter)
                    continue;

                for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                {
                    dataToAverage.Add(mScans[scanIndex].IonsIntensity[ionIndex]);
                }
            }

            var average = MathNet.Numerics.Statistics.Statistics.Mean(dataToAverage);
            return (float)average;
        }

        private void CentroidMSData(
            float mzResolution,
            ref int ionCount,
            IList<double> ionsMZ,
            IList<float> ionsIntensity,
            IList<byte> chargeFiltered)
        {
            if (mzResolution <= 0)
            {
                // Nothing to do
                return;
            }

            try
            {
                var intensitySorted = new float[ionCount];
                var pointerArray = new int[ionCount];

                for (var index = 0; index <= ionCount - 1; index++)
                {
                    if (ionsIntensity[index] < 0)
                    {
                        // Do not allow for negative intensities; change it to 0
                        ionsIntensity[index] = 0;
                    }
                    intensitySorted[index] = ionsIntensity[index];
                    pointerArray[index] = index;
                }

                // Sort by ascending intensity
                Array.Sort(intensitySorted, pointerArray);

                // Now process the data from the highest intensity to the lowest intensity
                // As each data point is processed, we will either:
                //  a) set its intensity to the negative of the actual intensity to mark it as being processed
                //  b) set its intensity to float.MinValue (-3.40282347E+38) if the point is to be removed
                //     because it is within mzResolution m/z units of a point with a higher intensity

                var pointerIndex = ionCount - 1;

                while (pointerIndex >= 0)
                {
                    var index = pointerArray[pointerIndex];

                    if (ionsIntensity[index] > 0)
                    {
                        // This point has not yet been processed

                        // Examine adjacent data points to the left (lower m/z)
                        var indexAdjacent = index - 1;
                        while (indexAdjacent >= 0)
                        {
                            if (ionsMZ[index] - ionsMZ[indexAdjacent] < mzResolution)
                            {
                                // Mark this data point for removal since it is too close to the point at index
                                if (ionsIntensity[indexAdjacent] > 0)
                                {
                                    ionsIntensity[indexAdjacent] = float.MinValue;
                                }
                            }
                            else
                            {
                                break;
                            }
                            indexAdjacent -= 1;
                        }

                        // Examine adjacent data points to the right (higher m/z)
                        indexAdjacent = index + 1;
                        while (indexAdjacent < ionCount)
                        {
                            if (ionsMZ[indexAdjacent] - ionsMZ[index] < mzResolution)
                            {
                                // Mark this data point for removal since it is too close to the point at index
                                if (ionsIntensity[indexAdjacent] > 0)
                                {
                                    ionsIntensity[indexAdjacent] = float.MinValue;
                                }
                            }
                            else
                            {
                                break;
                            }
                            indexAdjacent += 1;
                        }

                        ionsIntensity[index] = -ionsIntensity[index];
                    }
                    pointerIndex -= 1;
                }

                // Now consolidate the data by copying in place
                var ionCountNew = 0;
                for (var index = 0; index <= ionCount - 1; index++)
                {
                    if (ionsIntensity[index] <= float.MinValue)
                        continue;

                    // Keep this point; need to flip the intensity back to being positive
                    ionsMZ[ionCountNew] = ionsMZ[index];
                    ionsIntensity[ionCountNew] = -ionsIntensity[index];
                    chargeFiltered[ionCountNew] = chargeFiltered[index];
                    ionCountNew += 1;
                }
                ionCount = ionCountNew;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.CentroidMSData: " + ex.Message, ex);
            }

        }

        private void DiscardDataToLimitIonCount(clsScanData msSpectrum, double mzIgnoreRangeStart, double mzIgnoreRangeEnd, int maxIonCountToRetain)
        {
            // When this is true, then will write a text file of the mass spectrum before before and after it is filtered
            // Used for debugging
            var writeDebugData = false;
            StreamWriter writer = null;

            try
            {
                bool mzIgnoreRangeEnabled;
                if (mzIgnoreRangeStart > 0 || mzIgnoreRangeEnd > 0)
                {
                    mzIgnoreRangeEnabled = true;
                }
                else
                {
                    mzIgnoreRangeEnabled = false;
                }

                int ionCountNew;
                if (msSpectrum.IonCount > maxIonCountToRetain)
                {
                    var filterDataArray = new clsFilterDataArrayMaxCount()
                    {
                        MaximumDataCountToLoad = maxIonCountToRetain,
                        TotalIntensityPercentageFilterEnabled = false
                    };

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (writeDebugData)
                    {
                        writer = new StreamWriter(new FileStream("DataDump_" + msSpectrum.ScanNumber.ToString() + "_BeforeFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                        writer.WriteLine("m/z" + '\t' + "Intensity");
                    }

                    // Store the intensity values in filterDataArray
                    for (var ionIndex = 0; ionIndex <= msSpectrum.IonCount - 1; ionIndex++)
                    {
                        filterDataArray.AddDataPoint(msSpectrum.IonsIntensity[ionIndex], ionIndex);

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (writeDebugData)
                        {
                            writer.WriteLine(msSpectrum.IonsMZ[ionIndex] + '\t' + msSpectrum.IonsIntensity[ionIndex]);
                        }
                    }

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (writeDebugData)
                    {
                        writer.Close();
                    }

                    // Call .FilterData, which will determine which data points to keep
                    filterDataArray.FilterData();

                    ionCountNew = 0;

                    for (var ionIndex = 0; ionIndex <= msSpectrum.IonCount - 1; ionIndex++)
                    {
                        bool pointPassesFilter;
                        if (mzIgnoreRangeEnabled)
                        {
                            if (msSpectrum.IonsMZ[ionIndex] <= mzIgnoreRangeEnd && msSpectrum.IonsMZ[ionIndex] >= mzIgnoreRangeStart)
                            {
                                // The m/z value is between mzIgnoreRangeStart and mzIgnoreRangeEnd
                                // Keep this point
                                pointPassesFilter = true;
                            }
                            else
                            {
                                pointPassesFilter = false;
                            }
                        }
                        else
                        {
                            pointPassesFilter = false;
                        }

                        if (!pointPassesFilter)
                        {
                            // See if the point's intensity is negative
                            if (filterDataArray.GetAbundanceByIndex(ionIndex) >= 0)
                            {
                                pointPassesFilter = true;
                            }
                        }

                        if (pointPassesFilter)
                        {
                            msSpectrum.IonsMZ[ionCountNew] = msSpectrum.IonsMZ[ionIndex];
                            msSpectrum.IonsIntensity[ionCountNew] = msSpectrum.IonsIntensity[ionIndex];
                            msSpectrum.Charge[ionCountNew] = msSpectrum.Charge[ionIndex];
                            ionCountNew += 1;
                        }

                    }
                }
                else
                {
                    ionCountNew = msSpectrum.IonCount;
                }

                if (ionCountNew < msSpectrum.IonCount)
                {
                    msSpectrum.IonCount = ionCountNew;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (writeDebugData)
                {
                    writer = new StreamWriter(new FileStream("DataDump_" + msSpectrum.ScanNumber.ToString() + "_PostFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                    writer.WriteLine("m/z" + '\t' + "Intensity");

                    // Store the intensity values in filterDataArray
                    for (var ionIndex = 0; ionIndex <= msSpectrum.IonCount - 1; ionIndex++)
                    {
                        writer.WriteLine(msSpectrum.IonsMZ[ionIndex] + '\t' + msSpectrum.IonsIntensity[ionIndex]);
                    }
                    writer.Close();
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
            for (var index = 0; index <= mRecentFiles.Count - 1; index++)
            {
                if (mRecentFiles[index].FileType == eFileType)
                {
                    return mRecentFiles[index].FileName;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns the file name and path of the recently saved file of the given type
        /// </summary>
        /// <param name="eFileType">File type to find</param>
        /// <param name="fileName">File name (output)</param>
        /// <param name="filePath">File Path (output)</param>
        /// <returns>True if a match was found; otherwise returns false</returns>
        /// <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
        public bool GetRecentFileInfo(eOutputFileTypes eFileType, out string fileName, out string filePath)
        {
            for (var index = 0; index <= mRecentFiles.Count - 1; index++)
            {
                if (mRecentFiles[index].FileType == eFileType)
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

        /// <summary>
        /// Returns the cached scan data for the scan index
        /// </summary>
        /// <param name="index"></param>
        /// <returns>ScanData class</returns>
        /// <remarks></remarks>
        public clsScanData GetCachedScanByIndex(int index)
        {
            if (index >= 0 && index < mScans.Count)
            {
                return mScans[index];
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
        ///  Filters the data stored in mScans to nominally retain the top targetDataPointCount data points, sorted by descending intensity
        ///  </summary>
        ///  <param name="targetDataPointCount">Target max number of data points (see remarks for caveat)</param>
        /// <param name="minPointsPerSpectrum"></param>
        /// <remarks>
        /// Note that the number of data points remaining after calling this function may still be
        ///  more than targetDataPointCount, depending on minPointsPerSpectrum .
        /// For example, if minPointsPerSpectrum = 5 and we have 5000 scans, then there will be
        ///  at least 5*5000 = 25000 data points in memory.  If targetDataPointCount = 10000, then
        ///  there could be as many as 25000 + 10000 = 25000 points in memory
        /// </remarks>
        private void TrimCachedData(int targetDataPointCount, int minPointsPerSpectrum)
        {

            try
            {
                var filterDataArray = new clsFilterDataArrayMaxCount
                {
                    MaximumDataCountToLoad = targetDataPointCount,
                    TotalIntensityPercentageFilterEnabled = false
                };

                // Store the intensity values for each scan in filterDataArray
                // However, skip scans for which there are <= minPointsPerSpectrum data points

                var masterIonIndex = 0;
                for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
                {
                    if (mScans[scanIndex].IonCount > minPointsPerSpectrum)
                    {
                        // Store the intensity values in filterDataArray
                        for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                        {
                            filterDataArray.AddDataPoint(mScans[scanIndex].IonsIntensity[ionIndex], masterIonIndex);
                            masterIonIndex += 1;
                        }
                    }
                }

                // Call .FilterData, which will determine which data points to keep
                filterDataArray.FilterData();

                // Step through the scans and trim the data as needed
                masterIonIndex = 0;
                mPointCountCached = 0;

                for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
                {
                    if (mScans[scanIndex].IonCount <= minPointsPerSpectrum)
                    {
                        // Skip this can since it has too few points
                        // No need to update masterIonIndex since it was skipped above when calling filterDataArray.AddDataPoint

                    }
                    else
                    {
                        // See if fewer than minPointsPerSpectrum points will remain after filtering
                        // If so, we'll need to handle this scan differently

                        var masterIonIndexStart = masterIonIndex;

                        var ionCountNew = 0;
                        for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                        {
                            // If the point's intensity is >= 0, then we keep it
                            if (filterDataArray.GetAbundanceByIndex(masterIonIndex) >= 0)
                            {
                                ionCountNew += 1;
                            }
                            masterIonIndex += 1;
                        }

                        if (ionCountNew < minPointsPerSpectrum)
                        {
                            // Too few points will remain after filtering
                            // Retain the top minPointsPerSpectrum points in this spectrum

                            DiscardDataToLimitIonCount(mScans[scanIndex], 0, 0, minPointsPerSpectrum);

                        }
                        else
                        {
                            // It's safe to filter the data

                            // Reset masterIonIndex to the saved value
                            masterIonIndex = masterIonIndexStart;

                            ionCountNew = 0;

                            for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                            {
                                // If the point's intensity is >= 0, then we keep it

                                if (filterDataArray.GetAbundanceByIndex(masterIonIndex) >= 0)
                                {
                                    // Copying in place (don't actually need to copy unless ionCountNew <> ionIndex)
                                    if (ionCountNew != ionIndex)
                                    {
                                        mScans[scanIndex].IonsMZ[ionCountNew] = mScans[scanIndex].IonsMZ[ionIndex];
                                        mScans[scanIndex].IonsIntensity[ionCountNew] = mScans[scanIndex].IonsIntensity[ionIndex];
                                        mScans[scanIndex].Charge[ionCountNew] = mScans[scanIndex].Charge[ionIndex];
                                    }

                                    ionCountNew += 1;
                                }

                                masterIonIndex += 1;
                            }

                            mScans[scanIndex].IonCount = ionCountNew;

                        }

                        if (mScans[scanIndex].IonsMZ.Length > 5 && mScans[scanIndex].IonCount < mScans[scanIndex].IonsMZ.Length / 2.0)
                        {
                            // Shrink the arrays to reduce the memory footprint
                            mScans[scanIndex].ShrinkArrays();

                            if (DateTime.UtcNow.Subtract(mLastGCTime).TotalSeconds > 60)
                            {
                                // Perform garbage collection every 60 seconds
                                mLastGCTime = DateTime.UtcNow;
                                ProgRunner.GarbageCollectNow();
                            }

                        }

                    }

                    // Bump up the total point count cached
                    mPointCountCached += mScans[scanIndex].IonCount;

                }

                // Update mPointCountCachedAfterLastTrim
                mPointCountCachedAfterLastTrim = mPointCountCached;

            }
            catch (Exception ex)
            {
                throw new Exception("Error in clsLCMSDataPlotter.TrimCachedData: " + ex.Message, ex);
            }

        }

        private void UpdateAbsValueRange(IEnumerable<double> dataPoints, ref double min, ref double max)
        {

            foreach (var currentValAbs in from value in dataPoints select Math.Abs(value))
            {
                min = Math.Min(min, currentValAbs);
                max = Math.Max(max, currentValAbs);
            }
        }

        private void UpdateMinMax(float value, ref float min, ref float max)
        {
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        private void UpdateMinMax(double value, ref double min, ref double max)
        {
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        private void ValidateMSLevel()
        {
            var msLevelDefined = false;

            for (var index = 0; index <= mScans.Count - 1; index++)
            {
                if (mScans[index].MSLevel > 0)
                {
                    msLevelDefined = true;
                    break;
                }
            }

            if (!msLevelDefined)
            {
                // Set the MSLevel to 1 for all scans
                for (var index = 0; index <= mScans.Count - 1; index++)
                {
                    mScans[index].UpdateMSLevel(1);
                }
            }

        }

        #region "Plotting Functions"

        private void AddOxyPlotSeriesMonoMassVsScan(IList<List<ScatterPoint>> pointsByCharge, PlotModel myPlot)
        {
            var markerSize = GetMarkerSize(mScans.Count, pointsByCharge);

            for (var charge = 0; charge <= pointsByCharge.Count - 1; charge++)
            {
                if (pointsByCharge[charge].Count == 0)
                    continue;

                var title = charge + "+";

                var seriesColor = clsPlotContainer.GetColorByCharge(charge);

                var series = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerFill = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B),
                    Title = title,
                    MarkerSize = markerSize
                };

                // ReSharper disable once CommentTypo
                // series.MarkerStroke = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B)

                series.Points.AddRange(pointsByCharge[charge]);

                myPlot.Series.Add(series);
            }

        }

        private void AddPythonPlotSeriesMonoMassVsScan(IList<List<ScatterPoint>> pointsByCharge, clsPythonPlotContainer3D plotContainer)
        {

            plotContainer.MarkerSize = GetMarkerSize(mScans.Count, pointsByCharge);

            for (var charge = 0; charge <= pointsByCharge.Count - 1; charge++)
            {
                if (pointsByCharge[charge].Count == 0)
                    continue;

                plotContainer.AddData(pointsByCharge[charge], charge);
            }

        }

        private void AddOxyPlotSeriesMzVsScan(
            string title,
            IEnumerable<ScatterPoint> points,
            float colorScaleMinIntensity, float colorScaleMaxIntensity,
            PlotModel myPlot)
        {
            // We use a linear color axis to color the data points based on intensity
            var colorAxis = new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Minimum = colorScaleMinIntensity,
                Maximum = colorScaleMaxIntensity,
                Palette = OxyPalettes.Jet(30),
                IsAxisVisible = false
            };

            myPlot.Axes.Add(colorAxis);

            var series = new ScatterSeries
            {
                MarkerType = MarkerType.Circle,
                Title = title,
                MarkerSize = GetMarkerSize(mScans.Count)
            };

            series.Points.AddRange(points);

            myPlot.Series.Add(series);
        }

        private void AddPythonPlotSeriesMzVsScan(
            string title,
            List<ScatterPoint> points,
            float colorScaleMinIntensity, float colorScaleMaxIntensity,
            clsPythonPlotContainer3D plotContainer)
        {

            plotContainer.PlotTitle = title;
            plotContainer.ColorScaleMinIntensity = colorScaleMinIntensity;
            plotContainer.ColorScaleMaxIntensity = colorScaleMaxIntensity;
            plotContainer.MarkerSize = GetMarkerSize(mScans.Count);

            plotContainer.AddData(points, 0);
        }

        [Obsolete("Use MathNet.Numerics.Statistics.Statistics.Median")]
        private float ComputeMedian(float[] values, int itemCount)
        {
            var average = false;

            if (values == null || values.Length < 1 || itemCount < 1)
            {
                // List is empty (or itemCount = 0)
                return 0;
            }

            if (itemCount <= 1)
            {
                // Only 1 item; the median is the value
                return values[0];
            }

            // Sort values ascending, then find the midpoint
            Array.Sort(values, 0, itemCount);

            int midpointIndex;
            if (itemCount % 2 == 0)
            {
                // Even number
                midpointIndex = (int)Math.Floor(itemCount / 2.0) - 1;
                average = true;
            }
            else
            {
                // Odd number
                midpointIndex = (int)Math.Floor(itemCount / 2.0);
            }

            if (midpointIndex > itemCount)
                midpointIndex = itemCount - 1;
            if (midpointIndex < 0)
                midpointIndex = 0;

            if (average)
            {
                // Even number of items
                // Return the average of the two middle points
                return (values[midpointIndex] + values[midpointIndex + 1]) / 2;
            }

            // Odd number of items
            return values[midpointIndex];
        }

        private IList<List<ScatterPoint>> GetDataToPlot(
            int msLevelFilter, bool skipTrimCachedData,
            out int pointsToPlot, out double scanTimeMax,
            out int minScan, out int maxScan,
            out double minMZ, out double maxMZ,
            out float colorScaleMinIntensity, out float colorScaleMaxIntensity)
        {

            colorScaleMinIntensity = 0;
            colorScaleMaxIntensity = 0;

            if (!skipTrimCachedData && mPointCountCached > Options.MaxPointsToPlot)
            {
                // Need to step through the scans and reduce the number of points in memory

                // Note that the number of data points remaining after calling this function may still be
                //  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum
                //  (see TrimCachedData for more details)

                TrimCachedData(Options.MaxPointsToPlot, Options.MinPointsPerSpectrum);
            }

            // Populate points and scanTimePoints with the data
            // At the same time, determine the range of m/z and intensity values

            // Instantiate the list to track the data points
            var pointsByCharge = new List<List<ScatterPoint>>();

            var maxMonoMass = double.MaxValue;
            if (Options.PlottingDeisotopedData)
            {
                maxMonoMass = Options.MaxMonoMassForDeisotopedPlot;
            }

            if (Options.PlottingDeisotopedData)
            {
                pointsByCharge = GetMonoMassSeriesByCharge(msLevelFilter, maxMonoMass, out minMZ, out maxMZ, out scanTimeMax, out minScan, out maxScan);
            }
            else
            {
                var points = GetMzVsScanSeries(
                    msLevelFilter,
                    out colorScaleMinIntensity, out colorScaleMaxIntensity,
                    out minMZ, out maxMZ,
                    out scanTimeMax, out minScan, out maxScan);

                pointsByCharge.Add(points);
            }

            // Count the actual number of points that will be plotted
            pointsToPlot = 0;
            foreach (var series in pointsByCharge)
            {
                pointsToPlot += series.Count;
            }

            // Round minScan down to the nearest multiple of 10
            minScan = (int)Math.Floor(minScan / 10.0) * 10;
            if (minScan < 0)
                minScan = 0;

            // Round maxScan up to the nearest multiple of 10
            maxScan = (int)Math.Ceiling(maxScan / 10.0) * 10;

            // Round minMZ down to the nearest multiple of 100
            minMZ = (long)Math.Floor(minMZ / 100.0) * 100;

            // Round maxMZ up to the nearest multiple of 100
            maxMZ = (long)Math.Ceiling(maxMZ / 100.0) * 100;

            return pointsByCharge;
        }

        private double GetMarkerSize(int scanCount)
        {
            if (scanCount < 250)
            {
                // Use a point size of 2 when fewer than 250 scans
                return 2;
            }

            if (scanCount < 5000)
            {
                // Use a point size of 1 when 250 to 5000 scans
                return 1;
            }

            // Use a point size of 0.6 when >= 5000 scans
            return 0.6;
        }

        private double GetMarkerSize(int scanCount, IEnumerable<List<ScatterPoint>> pointsByCharge)
        {
            // Determine the number of data points to be plotted
            var totalPoints = pointsByCharge.Sum(item => item.Count);

            // Customize the points
            if (scanCount < 250)
            {
                // Use a point size of 2 when fewer than 250 scans
                return 2;
            }

            if (mScans.Count < 500)
            {
                // Use a point size of 1 when 250 to 500 scans
                return 1;
            }

            // Use a point size of 0.8 or 0.6 when >= 500 scans
            if (totalPoints < 80000)
            {
                return 0.8;
            }

            return 0.6;
        }

        private List<List<ScatterPoint>> GetMonoMassSeriesByCharge(
            int msLevelFilter,
            double maxMonoMass,
            out double minMZ, out double maxMZ,
            out double scanTimeMax,
            out int minScan, out int maxScan)
        {
            minScan = int.MaxValue;
            maxScan = 0;
            minMZ = float.MaxValue;
            maxMZ = 0;

            var scanTimeMin = double.MaxValue;
            scanTimeMax = 0;

            // Determine the maximum charge state
            byte maxCharge = 1;

            for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
            {
                if (msLevelFilter != 0 && mScans[scanIndex].MSLevel != msLevelFilter)
                    continue;

                if (mScans[scanIndex].Charge.Length <= 0)
                    continue;

                for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                {
                    maxCharge = Math.Max(maxCharge, mScans[scanIndex].Charge[ionIndex]);
                }
            }

            // Initialize the data for each charge state
            var series = new List<List<ScatterPoint>>();

            for (var charge = 0; charge <= maxCharge; charge++)
            {
                series.Add(new List<ScatterPoint>());
            }

            // Store the data, segregating by charge
            for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
            {
                if (msLevelFilter != 0 && mScans[scanIndex].MSLevel != msLevelFilter)
                    continue;

                for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                {
                    var currentIonMonoMass = mScans[scanIndex].IonsMZ[ionIndex];
                    if (currentIonMonoMass > maxMonoMass)
                        continue;

                    var dataPoint = new ScatterPoint(mScans[scanIndex].ScanNumber, currentIonMonoMass)
                    {
                        Value = mScans[scanIndex].IonsIntensity[ionIndex]
                    };

                    series[mScans[scanIndex].Charge[ionIndex]].Add(dataPoint);

                    UpdateMinMax(mScans[scanIndex].IonsMZ[ionIndex], ref minMZ, ref maxMZ);

                }

                UpdateMinMax(mScans[scanIndex].ScanTimeMinutes, ref scanTimeMin, ref scanTimeMax);

                if (mScans[scanIndex].ScanNumber < minScan)
                {
                    minScan = mScans[scanIndex].ScanNumber;
                }

                if (mScans[scanIndex].ScanNumber > maxScan)
                {
                    maxScan = mScans[scanIndex].ScanNumber;
                }
            }

            return series;

        }

        private List<ScatterPoint> GetMzVsScanSeries(
            int msLevelFilter,
            out float colorScaleMinIntensity,
            out float colorScaleMaxIntensity,
            out double minMZ,
            out double maxMZ,
            out double scanTimeMax,
            out int minScan,
            out int maxScan,
            bool writeDebugData = false,
            TextWriter debugWriter = null)
        {

            var points = new List<ScatterPoint>();

            var intensityList = new List<float>();

            colorScaleMinIntensity = float.MaxValue;
            colorScaleMaxIntensity = 0;

            minScan = int.MaxValue;
            maxScan = 0;
            minMZ = float.MaxValue;
            maxMZ = 0;

            var scanTimeMin = double.MaxValue;
            scanTimeMax = 0;

            for (var scanIndex = 0; scanIndex <= mScans.Count - 1; scanIndex++)
            {

                if (msLevelFilter != 0 && mScans[scanIndex].MSLevel != msLevelFilter)
                {
                    continue;
                }

                for (var ionIndex = 0; ionIndex <= mScans[scanIndex].IonCount - 1; ionIndex++)
                {
                    var currentValue = mScans[scanIndex].IonsIntensity[ionIndex];

                    intensityList.Add(currentValue);

                    var dataPoint = new ScatterPoint(mScans[scanIndex].ScanNumber,
                                                     mScans[scanIndex].IonsMZ[ionIndex])
                    {
                        Value = currentValue
                    };

                    points.Add(dataPoint);

                    if (writeDebugData)
                    {
                        debugWriter?.WriteLine(
                            mScans[scanIndex].ScanNumber + '\t' +
                            mScans[scanIndex].IonsMZ[ionIndex] + '\t' +
                            currentValue);
                    }

                    UpdateMinMax(currentValue, ref colorScaleMinIntensity, ref colorScaleMaxIntensity);
                    UpdateMinMax(mScans[scanIndex].IonsMZ[ionIndex], ref minMZ, ref maxMZ);
                }

                UpdateMinMax(mScans[scanIndex].ScanTimeMinutes, ref scanTimeMin, ref scanTimeMax);

                if (mScans[scanIndex].ScanNumber < minScan)
                {
                    minScan = mScans[scanIndex].ScanNumber;
                }

                if (mScans[scanIndex].ScanNumber > maxScan)
                {
                    maxScan = mScans[scanIndex].ScanNumber;
                }
            }

            if (points.Count == 0 || intensityList.Count == 0)
                return points;

            // Compute median intensity value
            var medianIntensity = MathNet.Numerics.Statistics.Statistics.Median(intensityList);

            // Set the minimum color intensity to the median
            colorScaleMinIntensity = medianIntensity;

            return points;
        }

        /// <summary>
        /// When PlottingDeisotopedData is False, creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
        /// When PlottingDeisotopedData is True, creates a 2D plot of monoisotopic mass vs. scan number, using charge state as the 3rd dimension to color the data points
        /// </summary>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="skipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for msLevelFilter, set skipTrimCachedData to False on the first call and True on subsequent calls)</param>
        /// <returns>OxyPlot PlotContainer</returns>
        /// <remarks></remarks>
        private clsPlotContainerBase InitializePlot(string plotTitle, int msLevelFilter, bool skipTrimCachedData)
        {
            if (Options.PlotWithPython)
            {
                return InitializePythonPlot(plotTitle, msLevelFilter, skipTrimCachedData);
            }

            return InitializeOxyPlot(plotTitle, msLevelFilter, skipTrimCachedData);
        }

        /// <summary>
        /// When PlottingDeisotopedData is False, creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
        /// When PlottingDeisotopedData is True, creates a 2D plot of monoisotopic mass vs. scan number, using charge state as the 3rd dimension to color the data points
        /// </summary>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="skipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for msLevelFilter, set skipTrimCachedData to False on the first call and True on subsequent calls)</param>
        /// <returns>OxyPlot PlotContainer</returns>
        /// <remarks></remarks>
        private clsPlotContainerBase InitializeOxyPlot(string plotTitle, int msLevelFilter, bool skipTrimCachedData)
        {
            var pointsByCharge = GetDataToPlot(
                msLevelFilter, skipTrimCachedData,
                out var pointsToPlot, out var scanTimeMax,
                out var minScan, out var maxScan,
                out var minMZ, out var maxMZ,
                out var colorScaleMinIntensity, out var colorScaleMaxIntensity);

            // When this is true, then will write a text file of the mass spectrum before and after it is filtered
            // Used for debugging
            var writeDebugData = false;
            StreamWriter debugWriter = null;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (writeDebugData)
            {
                debugWriter = new StreamWriter(new FileStream(plotTitle + " - LCMS Top " + IntToEngineeringNotation(Options.MaxPointsToPlot) + " points.txt", FileMode.Create, FileAccess.Write, FileShare.Read));
                debugWriter.WriteLine("scan" + '\t' + "m/z" + '\t' + "Intensity");
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (writeDebugData)
            {
                debugWriter.Close();
            }

            if (pointsToPlot == 0)
            {
                // Nothing to plot
                return new clsPlotContainer(new PlotModel());
            }

            string yAxisLabel;
            if (Options.PlottingDeisotopedData)
            {
                yAxisLabel = "Monoisotopic Mass";
            }
            else
            {
                yAxisLabel = "m/z";
            }

            var myPlot = clsOxyPlotUtilities.GetBasicPlotModel(plotTitle, "LC Scan Number", yAxisLabel);

            if (Options.PlottingDeisotopedData)
            {
                AddOxyPlotSeriesMonoMassVsScan(pointsByCharge, myPlot);
                myPlot.TitlePadding = 40;
            }
            else
            {
                AddOxyPlotSeriesMzVsScan(plotTitle, pointsByCharge.First(), colorScaleMinIntensity, colorScaleMaxIntensity, myPlot);
            }

            // Update the axis format codes if the data values are small or the range of data is small
            var xVals = (from item in pointsByCharge.First() select item.X).ToList();
            clsOxyPlotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[0], xVals, true);

            var yVals = (from item in pointsByCharge.First() select item.Y).ToList();
            clsOxyPlotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes[1], yVals, false);

            var plotContainer = new clsPlotContainer(myPlot)
            {
                FontSizeBase = clsPlotContainer.DEFAULT_BASE_FONT_SIZE,
                // Add a label showing the number of points displayed
                AnnotationBottomLeft = pointsToPlot.ToString("0,000") + " points plotted"
            };

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

            // Override the auto-computed X axis range
            if (Options.UseObservedMinScan)
            {
                myPlot.Axes[0].Minimum = minScan;
            }
            else
            {
                myPlot.Axes[0].Minimum = 0;
            }

            if (maxScan == 0)
            {
                myPlot.Axes[0].Maximum = 1;
            }
            else
            {
                myPlot.Axes[0].Maximum = maxScan;
            }

            if (Math.Abs(myPlot.Axes[0].Minimum - myPlot.Axes[0].Maximum) < 0.01)
            {
                minScan = (int)myPlot.Axes[0].Minimum;
                myPlot.Axes[0].Minimum = minScan - 1;
                myPlot.Axes[0].Maximum = minScan + 1;
            }
            else if (minScan == maxScan)
            {
                myPlot.Axes[0].Minimum = minScan - 1;
                myPlot.Axes[0].Maximum = minScan + 1;
            }

            // Assure that we don't see ticks between scan numbers
            clsOxyPlotUtilities.ValidateMajorStep(myPlot.Axes[0]);

            double maxMzToUse;

            // Set the maximum value for the Y-axis
            if (Options.PlottingDeisotopedData)
            {
                if (maxMZ < Options.MaxMonoMassForDeisotopedPlot)
                {
                    maxMzToUse = maxMZ;
                }
                else
                {
                    maxMzToUse = Options.MaxMonoMassForDeisotopedPlot;
                }
            }
            else
            {
                maxMzToUse = maxMZ;
            }

            // Override the auto-computed axis range
            myPlot.Axes[1].Minimum = minMZ;
            myPlot.Axes[1].Maximum = maxMzToUse;

            // Hide the legend
            myPlot.IsLegendVisible = false;

            return plotContainer;

        }

        /// <summary>
        /// When PlottingDeisotopedData is False, creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
        /// When PlottingDeisotopedData is True, creates a 2D plot of monoisotopic mass vs. scan number, using charge state as the 3rd dimension to color the data points
        /// </summary>
        /// <param name="plotTitle">Title of the plot</param>
        /// <param name="msLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
        /// <param name="skipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for msLevelFilter, set skipTrimCachedData to False on the first call and True on subsequent calls)</param>
        /// <returns>OxyPlot PlotContainer</returns>
        /// <remarks></remarks>
        private clsPlotContainerBase InitializePythonPlot(string plotTitle, int msLevelFilter, bool skipTrimCachedData)
        {
            var pointsByCharge = GetDataToPlot(
                msLevelFilter, skipTrimCachedData,
                out var pointsToPlot, out var scanTimeMax,
                out var minScan, out var maxScan,
                out var minMZ, out var maxMZ,
                out var colorScaleMinIntensity, out var colorScaleMaxIntensity);

            if (pointsToPlot == 0)
            {
                // Nothing to plot
                return new clsPythonPlotContainer3D();
            }

            string yAxisLabel;
            if (Options.PlottingDeisotopedData)
            {
                yAxisLabel = "Monoisotopic Mass";
            }
            else
            {
                yAxisLabel = "m/z";
            }

            var plotContainer = new clsPythonPlotContainer3D(plotTitle, "LC Scan Number", yAxisLabel, "Intensity")
            {
                DeleteTempFiles = Options.DeleteTempFiles
            };

            if (Options.PlottingDeisotopedData)
            {
                AddPythonPlotSeriesMonoMassVsScan(pointsByCharge, plotContainer);
            }
            else
            {
                AddPythonPlotSeriesMzVsScan(plotTitle, pointsByCharge.First(), colorScaleMinIntensity, colorScaleMaxIntensity, plotContainer);
            }

            // These track the minimum and maximum values, using the Absolute value of any data in pointsByCharge
            var xMin = double.MaxValue;
            var xMax = double.MinValue;
            var yMin = double.MaxValue;
            var yMax = double.MinValue;

            // Determine min/max values for the X and Y data
            foreach (var dataSeries in pointsByCharge)
            {
                UpdateAbsValueRange((from item in dataSeries select item.X).ToList(), ref xMin, ref xMax);
                UpdateAbsValueRange((from item in dataSeries select item.Y).ToList(), ref yMin, ref yMax);
            }

            // Update the axis format codes if the data values are small or the range of data is small

            // Assume the X axis is plotting integers
            clsPlotUtilities.GetAxisFormatInfo(xMin, xMax, true, plotContainer.XAxisInfo);

            // Assume the Y axis is plotting doubles
            clsPlotUtilities.GetAxisFormatInfo(yMin, yMax, false, plotContainer.YAxisInfo);

            // Add a label showing the number of points displayed
            plotContainer.AnnotationBottomLeft = pointsToPlot.ToString("0,000") + " points plotted";

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

            // Override the auto-computed X axis range
            if (Options.UseObservedMinScan)
            {
                plotContainer.XAxisInfo.Minimum = minScan;
            }
            else
            {
                plotContainer.XAxisInfo.Minimum = 0;
            }

            if (maxScan == 0)
            {
                plotContainer.XAxisInfo.Maximum = 1;
            }
            else
            {
                plotContainer.XAxisInfo.Maximum = maxScan;
            }

            if (Math.Abs(plotContainer.XAxisInfo.Minimum - plotContainer.XAxisInfo.Maximum) < 0.01)
            {
                minScan = (int)plotContainer.XAxisInfo.Minimum;
                plotContainer.XAxisInfo.Minimum = minScan - 1;
                plotContainer.XAxisInfo.Maximum = minScan + 1;
            }
            else if (minScan == maxScan)
            {
                plotContainer.XAxisInfo.Minimum = minScan - 1;
                plotContainer.XAxisInfo.Maximum = minScan + 1;
            }

            double maxMzToUse;

            // Set the maximum value for the Y-axis
            if (Options.PlottingDeisotopedData)
            {
                if (maxMZ < Options.MaxMonoMassForDeisotopedPlot)
                {
                    maxMzToUse = maxMZ;
                }
                else
                {
                    maxMzToUse = Options.MaxMonoMassForDeisotopedPlot;
                }
            }
            else
            {
                maxMzToUse = maxMZ;
            }

            // Override the auto-computed axis range
            plotContainer.YAxisInfo.Minimum = minMZ;
            plotContainer.YAxisInfo.Maximum = maxMzToUse;

            return plotContainer;

        }

        /// <summary>
        /// Converts an integer to engineering notation
        /// For example, 50000 will be returned as 50K
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private string IntToEngineeringNotation(int value)
        {
            if (value < 1000)
            {
                return value.ToString();
            }

            if (value < 1000000.0)
            {
                return (int)Math.Round(value / 1000.0, 0) + "K";
            }

            return (int)Math.Round(value / 1000.0 / 1000, 0) + "M";
        }

        public bool Save2DPlots(string datasetName, string outputFolderPath)
        {
            return Save2DPlots(datasetName, outputFolderPath, string.Empty, string.Empty);
        }

        public bool Save2DPlots(string datasetName, string outputFolderPath, string fileNameSuffixAddon, string scanModeSuffixAddon)
        {

            try
            {
                ClearRecentFileInfo();

                // Check whether all of the spectra have .MSLevel = 0
                // If they do, change the level to 1
                ValidateMSLevel();

                if (fileNameSuffixAddon == null)
                    fileNameSuffixAddon = string.Empty;
                if (scanModeSuffixAddon == null)
                    scanModeSuffixAddon = string.Empty;

                var ms1Plot = InitializePlot(datasetName + " - " + Options.MS1PlotTitle, 1, false);
                RegisterEvents(ms1Plot);

                ms1Plot.PlottingDeisotopedData = Options.PlottingDeisotopedData;

                if (Options.TestGradientColorSchemes)
                {
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

                    var oxyPlotContainer = ms1Plot as clsPlotContainer;
                    oxyPlotContainer?.AddGradients(colorGradients);
                }

                var successMS1 = true;
                var successMS2 = true;

                if (ms1Plot.SeriesCount > 0)
                {
                    var pngFilename = datasetName + "_" + fileNameSuffixAddon + "LCMS" + scanModeSuffixAddon + ".png";
                    var pngFile = new FileInfo(Path.Combine(outputFolderPath, pngFilename));
                    successMS1 = ms1Plot.SaveToPNG(pngFile, 1024, 700, 96);
                    AddRecentFile(pngFile.FullName, eOutputFileTypes.LCMS);
                }

                var ms2Plot = InitializePlot(datasetName + " - " + Options.MS2PlotTitle, 2, true);
                RegisterEvents(ms2Plot);

                if (ms2Plot.SeriesCount > 0)
                {
                    var pngFile = new FileInfo(Path.Combine(outputFolderPath, datasetName + "_" + fileNameSuffixAddon + "LCMS_MSn" + scanModeSuffixAddon + ".png"));
                    successMS2 = ms2Plot.SaveToPNG(pngFile, 1024, 700, 96);
                    AddRecentFile(pngFile.FullName, eOutputFileTypes.LCMSMSn);
                }

                return successMS1 && successMS2;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in clsLCMSDataPlotter.Save2DPlots: " + ex.Message, ex);
                return false;
            }

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

            public int IonCount { get; set; }

            public double[] IonsMZ;
            public float[] IonsIntensity;

            public byte[] Charge;

            public int MSLevel { get; private set; }

            public int ScanNumber { get; }

            public float ScanTimeMinutes { get; }

            public clsScanData(int scanNumber, int msLevel, float scanTimeMinutes, int dataCount, double[] ionsMZ, float[] ionsIntensity, byte[] charge)
            {
                ScanNumber = scanNumber;
                MSLevel = msLevel;
                ScanTimeMinutes = scanTimeMinutes;

                IonCount = dataCount;
                IonsMZ = new double[dataCount];
                IonsIntensity = new float[dataCount];
                Charge = new byte[dataCount];

                // Populate the arrays to be filtered
                Array.Copy(ionsMZ, IonsMZ, dataCount);
                Array.Copy(ionsIntensity, IonsIntensity, dataCount);
                Array.Copy(charge, Charge, dataCount);
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

            public void UpdateMSLevel(int newMSLevel)
            {
                MSLevel = newMSLevel;
            }

            /// <summary>
            /// Display the scan number and MSLevel
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (MSLevel > 0)
                    return string.Format("Scan {0}, MS{1}", ScanNumber, MSLevel);

                return string.Format("Scan {0}", ScanNumber);
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
