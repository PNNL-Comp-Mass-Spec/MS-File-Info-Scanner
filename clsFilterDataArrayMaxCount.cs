using System;
using System.Collections.Generic;
using System.Linq;

namespace MSFileInfoScanner
{
    public class clsFilterDataArrayMaxCount
    {

        // This class can be used to select the top N data points in a list, sorting descending
        // It does not require a full sort of the data, which allows for faster filtering of the data
        //
        // To use, first call AddDataPoint() for each source data point, specifying the value to sort on and a data point index
        // When done, call FilterData()
        //  This routine will determine which data points to retain
        //  For the remaining points, their data values will be changed to mSkipDataPointFlag (defaults to -1)


        private const int INITIAL_MEMORY_RESERVE = 50000;
        private const float DEFAULT_SKIP_DATA_POINT_FLAG = -1;

        // 4 steps in Sub FilterDataByMaxDataCountToLoad
        private const int SUBTASK_STEP_COUNT = 4;

        private List<float> mDataValues;
        private List<int> mDataIndices;

        private int mMaximumDataCountToKeep;

        private float mSkipDataPointFlag;
        private bool mTotalIntensityPercentageFilterEnabled;

        private float mTotalIntensityPercentageFilter;
        // Value between 0 and 100
        private float mProgress;

        public event ProgressChangedEventHandler ProgressChanged;
        public delegate void ProgressChangedEventHandler(float Progress);

        #region "Properties"
        public int MaximumDataCountToLoad {
            get { return mMaximumDataCountToKeep; }
            set { mMaximumDataCountToKeep = value; }
        }

        public float Progress {
            get { return mProgress; }
        }

        public float SkipDataPointFlag {
            get { return mSkipDataPointFlag; }
            set { mSkipDataPointFlag = value; }
        }

        public bool TotalIntensityPercentageFilterEnabled {
            get { return mTotalIntensityPercentageFilterEnabled; }
            set { mTotalIntensityPercentageFilterEnabled = value; }
        }

        public float TotalIntensityPercentageFilter {
            get { return mTotalIntensityPercentageFilter; }
            set { mTotalIntensityPercentageFilter = value; }
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public clsFilterDataArrayMaxCount()
        {
            mSkipDataPointFlag = DEFAULT_SKIP_DATA_POINT_FLAG;
            Clear();
        }

        /// <summary>
        /// Add a data point to track
        /// </summary>
        /// <param name="sngAbundance"></param>
        /// <param name="intDataPointIndex"></param>
        public void AddDataPoint(float sngAbundance, int intDataPointIndex)
        {
            mDataValues.Add(sngAbundance);
            mDataIndices.Add(intDataPointIndex);
        }

        /// <summary>
        /// Clear stored data
        /// </summary>
        public void Clear()
        {
            mMaximumDataCountToKeep = 400000;

            mTotalIntensityPercentageFilterEnabled = false;
            mTotalIntensityPercentageFilter = 90;

            mDataValues = new List<float>();
            mDataIndices = new List<int>();
        }

        public float GetAbundanceByIndex(int intDataPointIndex)
        {
            if (intDataPointIndex >= 0 & intDataPointIndex < mDataValues.Count)
            {
                return mDataValues[intDataPointIndex];
            }

            // Invalid data point index value
            return -1;
        }

        /// <summary>
        /// Filters data stored using AddDataPoint
        /// </summary>
        public void FilterData()
        {
            if (mDataValues.Count <= 0)
            {
                // Nothing to do
                return;
            }

            FilterDataByMaxDataCountToKeep();
        }

        private void FilterDataByMaxDataCountToKeep()
        {
            const int HISTOGRAM_BIN_COUNT = 5000;

            var intBinToSortDataCount = 0;

            try {
                var sngBinToSortAbundances = new float[10];
                var intBinToSortDataIndices = new int[10];

                UpdateProgress(0);

                var blnUseFullDataSort = false;
                if (mDataValues.Count == 0)
                {
                    // No data loaded
                    return;
                }
                
                if (mDataValues.Count <= mMaximumDataCountToKeep)
                {
                    // Loaded less than mMaximumDataCountToKeep data points
                    // Nothing to filter
                    UpdateProgress(4f / SUBTASK_STEP_COUNT * 100.0f);
                    return;

                }

                // In order to speed up the sorting, we're first going to make a histogram
                //  (aka frequency distribution) of the abundances in mDataValues

                // First, determine the maximum abundance value in mDataValues
                var sngMaxAbundance = mDataValues.Max();

                // Round sngMaxAbundance up to the next highest integer
                sngMaxAbundance = Convert.ToSingle(Math.Ceiling(sngMaxAbundance));

                // Now determine the histogram bin size
                double dblBinSize = sngMaxAbundance / HISTOGRAM_BIN_COUNT;
                if (dblBinSize < 1)
                    dblBinSize = 1;

                // Initialize intHistogramData
                var intBinCount = Convert.ToInt32(sngMaxAbundance / dblBinSize) + 1;
                var intHistogramBinCounts = new int[intBinCount];
                var dblHistogramBinStartIntensity = new double[intBinCount];

                for (var intIndex = 0; intIndex <= intBinCount - 1; intIndex++) {
                    dblHistogramBinStartIntensity[intIndex] = intIndex * dblBinSize;
                }

                // Parse mDataValues to populate intHistogramBinCounts
                var dataPointIndex = 0;
                var dataPointCount = mDataValues.Count;
                foreach (var dataPoint in mDataValues) {
                    var intTargetBin = 0;
                    if (dataPoint <= 0) {
                        intTargetBin = 0;
                    } else {
                        intTargetBin = Convert.ToInt32(Math.Floor(dataPoint / dblBinSize));
                    }

                    if (intTargetBin < intBinCount - 1) {
                        if (dataPoint >= dblHistogramBinStartIntensity[intTargetBin + 1])
                        {
                            intTargetBin += 1;
                        }
                    }

                    intHistogramBinCounts[intTargetBin] += 1;

                    if (dataPoint < dblHistogramBinStartIntensity[intTargetBin])
                    {
                        if (dataPoint < dblHistogramBinStartIntensity[intTargetBin] - dblBinSize / 1000)
                        {
                            // This is unexpected
                            Console.WriteLine(
                                "Unexpected code reached in clsFilterDataArrayMaxCount.FilterDataByMaxDataCountToKeep");
                        }
                    }

                    if (dataPointIndex % 10000 == 0)
                    {
                        UpdateProgress(Convert.ToSingle((0 + (dataPointIndex + 1) / Convert.ToDouble(dataPointCount)) / SUBTASK_STEP_COUNT * 100.0));
                    }
                    dataPointIndex++;
                }

                // Now examine the frequencies in intHistogramBinCounts() to determine the minimum value to consider when sorting
                var intPointTotal = 0;
                var intBinToSort = -1;
                for (var intIndex = intBinCount - 1; intIndex >= 0; intIndex += -1) {
                    intPointTotal = intPointTotal + intHistogramBinCounts[intIndex];
                    if (intPointTotal >= mMaximumDataCountToKeep) {
                        intBinToSort = intIndex;
                        break;
                    }
                }

                UpdateProgress(1.0f / SUBTASK_STEP_COUNT * 100.0f);

                if (intBinToSort >= 0) {
                    // Find the data with intensity >= dblHistogramBinStartIntensity(intBinToSort)
                    // We actually only need to sort the data in bin intBinToSort

                    var dblBinToSortAbundanceMinimum = dblHistogramBinStartIntensity[intBinToSort];
                    double dblBinToSortAbundanceMaximum = sngMaxAbundance + 1;
                    if (intBinToSort < intBinCount - 1) {
                        dblBinToSortAbundanceMaximum = dblHistogramBinStartIntensity[intBinToSort + 1];
                    }

                    if (Math.Abs(dblBinToSortAbundanceMaximum - dblBinToSortAbundanceMinimum) < Single.Epsilon) {
                        // Is this code ever reached?
                        // If yes, then the code below won't populate sngBinToSortAbundances() and intBinToSortDataIndices() with any data
                        blnUseFullDataSort = true;
                    }

                    if (!blnUseFullDataSort) {
                        intBinToSortDataCount = 0;
                        if (intHistogramBinCounts[intBinToSort] > 0) {
                            sngBinToSortAbundances = new float[intHistogramBinCounts[intBinToSort]];
                            intBinToSortDataIndices = new int[intHistogramBinCounts[intBinToSort]];
                        } else {
                            // Is this code ever reached?
                            blnUseFullDataSort = true;
                        }
                    }

                    if (!blnUseFullDataSort) {
                        var intDataCountImplicitlyIncluded = 0;                        
                        for (var intIndex = 0; intIndex <= dataPointCount - 1; intIndex++)
                        {
                            if (mDataValues[intIndex] < dblBinToSortAbundanceMinimum) {
                                // Skip this data point when re-reading the input data file
                                mDataValues[intIndex] = mSkipDataPointFlag;
                            } else if (mDataValues[intIndex] < dblBinToSortAbundanceMaximum) {
                                // Value is in the bin to sort; add to the BinToSort arrays

                                if (intBinToSortDataCount >= sngBinToSortAbundances.Length) {
                                    // Need to reserve more space (this is unexpected)
                                    Array.Resize(ref sngBinToSortAbundances, sngBinToSortAbundances.Length * 2);
                                    Array.Resize(ref intBinToSortDataIndices, sngBinToSortAbundances.Length);
                                }

                                sngBinToSortAbundances[intBinToSortDataCount] = mDataValues[intIndex];
                                intBinToSortDataIndices[intBinToSortDataCount] = mDataIndices[intIndex];
                                intBinToSortDataCount += 1;
                            } else {
                                intDataCountImplicitlyIncluded = intDataCountImplicitlyIncluded + 1;
                            }

                            if (intIndex % 10000 == 0) {
                                UpdateProgress(Convert.ToSingle((1 + (intIndex + 1) / Convert.ToDouble(dataPointCount)) / SUBTASK_STEP_COUNT * 100.0));
                            }
                        }

                        if (intBinToSortDataCount > 0) {
                            if (intBinToSortDataCount < sngBinToSortAbundances.Length) {
                                Array.Resize(ref sngBinToSortAbundances, intBinToSortDataCount);
                                Array.Resize(ref intBinToSortDataIndices, intBinToSortDataCount);
                            }
                        } else {
                            // This code shouldn't be reached
                        }

                        if (mMaximumDataCountToKeep - intDataCountImplicitlyIncluded - intBinToSortDataCount == 0) {
                            // No need to sort and examine the data for BinToSort since we'll ultimately include all of it
                        } else {
                            SortAndMarkPointsToSkip(sngBinToSortAbundances, intBinToSortDataIndices, intBinToSortDataCount, mMaximumDataCountToKeep - intDataCountImplicitlyIncluded, SUBTASK_STEP_COUNT);
                        }

                        // Synchronize the data in sngBinToSortAbundances and intBinToSortDataIndices with mDataValues and mDataValues
                        // mDataValues and mDataIndices have not been sorted and therefore mDataIndices should currently be sorted ascending on "valid data point index"
                        // intBinToSortDataIndices should also currently be sorted ascending on "valid data point index" so the following Do Loop within a For Loop should sync things up

                        var intOriginalDataArrayIndex = 0;
                        for (var intIndex = 0; intIndex <= intBinToSortDataCount - 1; intIndex++) {
                            while (intBinToSortDataIndices[intIndex] > mDataIndices[intOriginalDataArrayIndex]) {
                                intOriginalDataArrayIndex += 1;
                            }

                            if (Math.Abs(sngBinToSortAbundances[intIndex] - mSkipDataPointFlag) < Single.Epsilon) {
                                if (mDataIndices[intOriginalDataArrayIndex] == intBinToSortDataIndices[intIndex]) {
                                    mDataValues[intOriginalDataArrayIndex] = mSkipDataPointFlag;
                                } else {
                                    // This code shouldn't be reached
                                }
                            }
                            intOriginalDataArrayIndex += 1;

                            if (intBinToSortDataCount < 1000 | intBinToSortDataCount % 100 == 0) {
                                UpdateProgress(Convert.ToSingle((3 + (intIndex + 1) / Convert.ToDouble(intBinToSortDataCount)) / SUBTASK_STEP_COUNT * 100.0));
                            }
                        }
                    }
                } else {
                    blnUseFullDataSort = true;
                }

                if (blnUseFullDataSort) {
                    // This shouldn't normally be necessary

                    // We have to sort all of the data; this can be quite slow
                    SortAndMarkPointsToSkip(mDataValues, mDataIndices, mDataValues.Count, mMaximumDataCountToKeep, SUBTASK_STEP_COUNT);
                }

                UpdateProgress(4f / SUBTASK_STEP_COUNT * 100.0f);

            } catch (Exception ex) {
                throw new Exception("Error in FilterDataByMaxDataCountToKeep: " + ex.Message, ex);
            }

        }

        /// <summary>
        /// Sort the data and mark data points beyond the first intMaximumDataCountInArraysToLoad points with mSkipDataPointFlag
        /// </summary>
        /// <param name="sngAbundances"></param>
        /// <param name="intDataIndices"></param>
        /// <param name="intDataCount"></param>
        /// <param name="intMaximumDataCountInArraysToLoad"></param>
        /// <param name="intSubtaskStepCount"></param>
        /// <remarks>
        /// This sub uses a full sort to filter the data
        /// This will be slow for large arrays and you should therefore use FilterDataByMaxDataCountToKeep if possible
        /// </remarks>
        private void SortAndMarkPointsToSkip(float[] sngAbundances, int[] intDataIndices, int intDataCount, int intMaximumDataCountInArraysToLoad, int intSubtaskStepCount)
        {
            if (intDataCount > 0) {
                // Sort sngAbundances ascending, sorting intDataIndices in parallel
                Array.Sort(sngAbundances, intDataIndices, 0, intDataCount);

                UpdateProgress(Convert.ToSingle((2.333 / intSubtaskStepCount) * 100.0));

                // Change the abundance values to mSkipDataPointFlag for data up to index intDataCount-intMaximumDataCountInArraysToLoad-1
                for (var intIndex = 0; intIndex <= intDataCount - intMaximumDataCountInArraysToLoad - 1; intIndex++) {
                    sngAbundances[intIndex] = mSkipDataPointFlag;
                }

                UpdateProgress(Convert.ToSingle((2.666 / intSubtaskStepCount) * 100.0));

                // Re-sort, this time on intDataIndices with sngAbundances in parallel
                Array.Sort(intDataIndices, sngAbundances, 0, intDataCount);

            }

            UpdateProgress(Convert.ToSingle(3f / intSubtaskStepCount * 100.0f));

        }

        private void SortAndMarkPointsToSkip(List<float> sngAbundances, List<int> intDataIndices, int intDataCount, int intMaximumDataCountInArraysToLoad, int intSubtaskStepCount)
        {
            if (intDataCount > 0)
            {
                // Sort sngAbundances ascending, sorting intDataIndices in parallel
                Array.Sort(sngAbundances, intDataIndices, 0, intDataCount);

                UpdateProgress(Convert.ToSingle((2.333 / intSubtaskStepCount) * 100.0));

                // Change the abundance values to mSkipDataPointFlag for data up to index intDataCount-intMaximumDataCountInArraysToLoad-1
                for (var intIndex = 0; intIndex <= intDataCount - intMaximumDataCountInArraysToLoad - 1; intIndex++)
                {
                    sngAbundances[intIndex] = mSkipDataPointFlag;
                }

                UpdateProgress(Convert.ToSingle((2.666 / intSubtaskStepCount) * 100.0));

                // Re-sort, this time on intDataIndices with sngAbundances in parallel
                Array.Sort(intDataIndices, sngAbundances, 0, intDataCount);

            }

            UpdateProgress(Convert.ToSingle(3f / intSubtaskStepCount * 100.0f));

        }
        private void UpdateProgress(float sngProgress)
        {
            mProgress = sngProgress;

            if (ProgressChanged != null) {
                ProgressChanged(mProgress);
            }
        }
    }
}


