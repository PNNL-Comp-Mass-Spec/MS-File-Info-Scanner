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

        private const float DEFAULT_SKIP_DATA_POINT_FLAG = -1;

        // 4 steps in Sub FilterDataByMaxDataCountToLoad
        private const int SUBTASK_STEP_COUNT = 4;

        /// <summary>
        /// Data values to track, along with their associated data indices
        /// </summary>
        private List<Tuple<float, int>> mDataValues;

        private int mMaximumDataCountToKeep;

        private float mSkipDataPointFlag;
        private bool mTotalIntensityPercentageFilterEnabled;

        private float mTotalIntensityPercentageFilter;
        // Value between 0 and 100
        private float mProgress;

        public event ProgressChangedEventHandler ProgressChanged;
        public delegate void ProgressChangedEventHandler(float progress);

        #region "Properties"
        public int MaximumDataCountToLoad {
            get => mMaximumDataCountToKeep;
            set => mMaximumDataCountToKeep = value;
        }

        public float Progress => mProgress;

        public float SkipDataPointFlag {
            get => mSkipDataPointFlag;
            set => mSkipDataPointFlag = value;
        }

        public bool TotalIntensityPercentageFilterEnabled {
            get => mTotalIntensityPercentageFilterEnabled;
            set => mTotalIntensityPercentageFilterEnabled = value;
        }

        public float TotalIntensityPercentageFilter {
            get => mTotalIntensityPercentageFilter;
            set => mTotalIntensityPercentageFilter = value;
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
            mDataValues.Add(new Tuple<float, int> (sngAbundance, intDataPointIndex));
        }

        /// <summary>
        /// Clear stored data
        /// </summary>
        public void Clear()
        {
            mMaximumDataCountToKeep = 400000;

            mTotalIntensityPercentageFilterEnabled = false;
            mTotalIntensityPercentageFilter = 90;

            mDataValues = new List<Tuple<float, int>>();
        }

        public float GetAbundanceByIndex(int intDataPointIndex)
        {
            if (intDataPointIndex >= 0 && intDataPointIndex < mDataValues.Count)
            {
                return mDataValues[intDataPointIndex].Item1;
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

        private Tuple<float, int> GetSkippedDataPoint(Tuple<float, int> dataValue)
        {
            var indexSaved = dataValue.Item2;
            return new Tuple<float, int>(mSkipDataPointFlag, indexSaved);
        }

        private void FilterDataByMaxDataCountToKeep()
        {
            const int HISTOGRAM_BIN_COUNT = 5000;

            try {
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
                var sngMaxAbundance = (from item in mDataValues select item.Item1).Max();

                // Round sngMaxAbundance up to the next highest integer
                sngMaxAbundance = (float)Math.Ceiling(sngMaxAbundance);

                // Now determine the histogram bin size
                double dblBinSize = sngMaxAbundance / HISTOGRAM_BIN_COUNT;
                if (dblBinSize < 1)
                    dblBinSize = 1;

                // Initialize intHistogramData
                var intBinCount = (int)(sngMaxAbundance / dblBinSize) + 1;
                var intHistogramBinCounts = new int[intBinCount];
                var dblHistogramBinStartIntensity = new double[intBinCount];

                for (var intIndex = 0; intIndex <= intBinCount - 1; intIndex++) {
                    dblHistogramBinStartIntensity[intIndex] = intIndex * dblBinSize;
                }

                // Parse mDataValues to populate intHistogramBinCounts
                var dataPointIndex = 0;
                var dataPointCount = mDataValues.Count;
                foreach (var dataPoint in mDataValues) {
                    int intTargetBin;
                    if (dataPoint.Item1 <= 0) {
                        intTargetBin = 0;
                    } else {
                        intTargetBin = (int)Math.Floor(dataPoint.Item1 / dblBinSize);
                    }

                    if (intTargetBin < intBinCount - 1) {
                        if (dataPoint.Item1 >= dblHistogramBinStartIntensity[intTargetBin + 1])
                        {
                            intTargetBin += 1;
                        }
                    }

                    intHistogramBinCounts[intTargetBin] += 1;

                    if (dataPoint.Item1 < dblHistogramBinStartIntensity[intTargetBin])
                    {
                        if (dataPoint.Item1 < dblHistogramBinStartIntensity[intTargetBin] - dblBinSize / 1000)
                        {
                            // This is unexpected
                            Console.WriteLine(
                                "Unexpected code reached in clsFilterDataArrayMaxCount.FilterDataByMaxDataCountToKeep");
                        }
                    }

                    if (dataPointIndex % 10000 == 0)
                    {
                        UpdateProgress(0 + (dataPointIndex + 1) / (float)dataPointCount / SUBTASK_STEP_COUNT * 100.0f);
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

                    if (Math.Abs(dblBinToSortAbundanceMaximum - dblBinToSortAbundanceMinimum) < float.Epsilon) {
                        // Is this code ever reached?
                        // If yes, then the code below won't populate binnedData with any data
                        blnUseFullDataSort = true;
                    }

                    var binnedData = new List<Tuple<float, int>>();

                    if (!blnUseFullDataSort)
                    {
                        if (intHistogramBinCounts[intBinToSort] <= 0)
                        {
                            // Is this code ever reached?
                            blnUseFullDataSort = true;
                        }
                    }

                    if (!blnUseFullDataSort) {
                        var intDataCountImplicitlyIncluded = 0;
                        for (var intIndex = 0; intIndex <= dataPointCount - 1; intIndex++)
                        {
                            if (mDataValues[intIndex].Item1 < dblBinToSortAbundanceMinimum)
                            {
                                // Skip this data point
                                mDataValues[intIndex] = GetSkippedDataPoint(mDataValues[intIndex]);

                            } else if (mDataValues[intIndex].Item1 < dblBinToSortAbundanceMaximum) {
                                // Value is in the bin to sort; add to the BinToSort arrays

                                // Replaced with a list: sngBinToSortAbundances[intBinToSortDataCount] = mDataValues[intIndex].Item1;
                                // Replaced with a list: intBinToSortDataIndices[intBinToSortDataCount] = mDataValues[intIndex].Item2;
                                // Replaced with a list: intBinToSortDataCount += 1;

                                binnedData.Add(mDataValues[intIndex]);
                            } else {
                                intDataCountImplicitlyIncluded = intDataCountImplicitlyIncluded + 1;
                            }

                            if (intIndex % 10000 == 0) {
                                UpdateProgress(1 + (intIndex + 1) / (float)dataPointCount / SUBTASK_STEP_COUNT * 100.0f);
                            }
                        }

                        var binnedDataCount = binnedData.Count;

                        if (mMaximumDataCountToKeep - intDataCountImplicitlyIncluded - binnedDataCount == 0)
                        {
                            // No need to sort and examine the data for BinToSort since we'll ultimately include all of it
                        } else {
                            SortAndMarkPointsToSkip(binnedData, mMaximumDataCountToKeep - intDataCountImplicitlyIncluded, SUBTASK_STEP_COUNT);
                        }

                        // Synchronize the data in sngBinToSortAbundances and intBinToSortDataIndices with mDataValues
                        // mDataValues has not been sorted and therefore mDataIndices should currently be sorted ascending on "valid data point index"
                        // intBinToSortDataIndices should also currently be sorted ascending on "valid data point index" so the following Do Loop within a For Loop should sync things up

                        var intOriginalDataArrayIndex = 0;
                        for (var intIndex = 0; intIndex <= binnedDataCount - 1; intIndex++)
                        {
                            while (binnedData[intIndex].Item2 > mDataValues[intOriginalDataArrayIndex].Item2)
                            {
                                intOriginalDataArrayIndex += 1;
                            }

                            if (Math.Abs(binnedData[intIndex].Item1 - mSkipDataPointFlag) < float.Epsilon)
                            {
                                if (mDataValues[intOriginalDataArrayIndex].Item2 == binnedData[intIndex].Item2)
                                {
                                    mDataValues[intOriginalDataArrayIndex] = GetSkippedDataPoint(mDataValues[intOriginalDataArrayIndex]);
                                } else
                                {
                                    Console.WriteLine("Index mis-match in FilterDataByMaxDataCountToKeep; this code shouldn't be reached");
                                }
                            }
                            intOriginalDataArrayIndex += 1;

                            if (binnedDataCount < 1000 | intIndex % 100 == 0)
                            {
                                UpdateProgress(3 + (intIndex + 1) / (float)binnedDataCount / SUBTASK_STEP_COUNT * 100.0f);
                            }
                        }
                    }
                } else {
                    blnUseFullDataSort = true;
                }

                if (blnUseFullDataSort) {
                    // This shouldn't normally be necessary

                    // We have to sort all of the data; this can be quite slow
                    SortAndMarkPointsToSkip(mDataValues, mMaximumDataCountToKeep, SUBTASK_STEP_COUNT);
                }

                UpdateProgress(4f / SUBTASK_STEP_COUNT * 100.0f);

            } catch (Exception ex) {
                throw new Exception("Error in FilterDataByMaxDataCountToKeep: " + ex.Message, ex);
            }

        }

        /// <summary>
        /// Sort the data and mark data points beyond the first intMaximumDataCountInArraysToLoad points with mSkipDataPointFlag
        /// </summary>
        /// <param name="dataValuesAndIndices"></param>
        /// <param name="intMaximumDataCountInArraysToLoad"></param>
        /// <param name="intSubtaskStepCount"></param>
        /// <remarks>
        /// This sub uses a full sort to filter the data
        /// This will be slow for large arrays and you should therefore use FilterDataByMaxDataCountToKeep if possible
        /// </remarks>
        private void SortAndMarkPointsToSkip(
            List<Tuple<float,int>> dataValuesAndIndices,
            int intMaximumDataCountInArraysToLoad, int intSubtaskStepCount)
        {
            var dataCount = dataValuesAndIndices.Count;

            if (dataCount > 0)
            {
                dataValuesAndIndices.Sort();

                UpdateProgress(2.333f / intSubtaskStepCount * 100.0f);

                // Change the abundance values to mSkipDataPointFlag for data up to index intDataCount-intMaximumDataCountInArraysToLoad-1
                for (var intIndex = 0; intIndex <= dataCount - intMaximumDataCountInArraysToLoad - 1; intIndex++)
                {
                    dataValuesAndIndices[intIndex] = GetSkippedDataPoint(dataValuesAndIndices[intIndex]);
                }

                UpdateProgress(2.666f / intSubtaskStepCount * 100.0f);

                // Re-sort, this time on the data index value (.Item2)
                dataValuesAndIndices.Sort(new clsSortByIndex());

            }

            UpdateProgress(3f / intSubtaskStepCount * 100.0f);

        }
        private void UpdateProgress(float sngProgress)
        {
            mProgress = sngProgress;

            ProgressChanged?.Invoke(mProgress);
        }
    }

    class clsSortByIndex : IComparer<Tuple<float,int>>
    {
        public int Compare(Tuple<float, int> x, Tuple<float, int> y)
        {
            if (x == null)
            {
                return y == null ? 0 : 1;
            }

            if (y == null)
            {
                return 1;
            }

            if (x.Item2 < y.Item2)
                return -1;

            if (x.Item2 > y.Item2)
                return 1;

            return 0;
        }
    }
}
