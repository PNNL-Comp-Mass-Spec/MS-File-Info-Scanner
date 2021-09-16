using System;
using System.Collections.Generic;
using System.Linq;

namespace MSFileInfoScanner
{
    /// <summary>
    /// This class can be used to select the top N data points in a list, sorting descending
    /// It does not require a full sort of the data, which allows for faster filtering of the data
    /// </summary>
    /// <remarks>
    /// To use, first call AddDataPoint() for each source data point, specifying the value to sort on and a data point index
    /// When done, call FilterData()
    /// FilterData() will determine which data points to retain
    /// For the remaining points, their data values will be changed to mSkipDataPointFlag (defaults to -1)
    /// </remarks>
    public class FilterDataArrayMaxCount
    {
        private const float DEFAULT_SKIP_DATA_POINT_FLAG = -1;

        // 4 steps in Sub FilterDataByMaxDataCountToLoad
        private const int SUBTASK_STEP_COUNT = 4;

        /// <summary>
        /// Data values to track, along with their associated data indices
        /// </summary>
        private List<Tuple<float, int>> mDataValues;

        public event ProgressChangedEventHandler ProgressChanged;
        public delegate void ProgressChangedEventHandler(float progress);

        #region "Properties"

        public int MaximumDataCountToLoad { get; set; }

        public float Progress { get; private set; }

        public float SkipDataPointFlag { get; set; }

        public bool TotalIntensityPercentageFilterEnabled { get; set; }

        public float TotalIntensityPercentageFilter { get; set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public FilterDataArrayMaxCount()
        {
            SkipDataPointFlag = DEFAULT_SKIP_DATA_POINT_FLAG;
            Clear();
        }

        /// <summary>
        /// Add a data point to track
        /// </summary>
        /// <param name="abundance"></param>
        /// <param name="dataPointIndex"></param>
        public void AddDataPoint(float abundance, int dataPointIndex)
        {
            mDataValues.Add(new Tuple<float, int>(abundance, dataPointIndex));
        }

        /// <summary>
        /// Clear stored data
        /// </summary>
        public void Clear()
        {
            MaximumDataCountToLoad = 400000;

            TotalIntensityPercentageFilterEnabled = false;
            TotalIntensityPercentageFilter = 90;

            mDataValues = new List<Tuple<float, int>>();
        }

        public float GetAbundanceByIndex(int dataPointIndex)
        {
            if (dataPointIndex >= 0 && dataPointIndex < mDataValues.Count)
            {
                return mDataValues[dataPointIndex].Item1;
            }

            // Invalid data point index value
            return -1;
        }

        /// <summary>
        /// Filters data stored using AddDataPoint
        /// </summary>
        public void FilterData()
        {
            if (mDataValues.Count == 0)
            {
                // Nothing to do
                return;
            }

            FilterDataByMaxDataCountToKeep();
        }

        private Tuple<float, int> GetSkippedDataPoint(Tuple<float, int> dataValue)
        {
            var indexSaved = dataValue.Item2;
            return new Tuple<float, int>(SkipDataPointFlag, indexSaved);
        }

        private void FilterDataByMaxDataCountToKeep()
        {
            const int HISTOGRAM_BIN_COUNT = 5000;

            try
            {
                UpdateProgress(0);

                var useFullDataSort = false;
                if (mDataValues.Count == 0)
                {
                    // No data loaded
                    return;
                }

                if (mDataValues.Count <= MaximumDataCountToLoad)
                {
                    // Loaded less than mMaximumDataCountToKeep data points
                    // Nothing to filter
                    UpdateProgress(4f / SUBTASK_STEP_COUNT * 100.0f);
                    return;
                }

                // In order to speed up the sorting, we're first going to make a histogram
                //  (aka frequency distribution) of the abundances in mDataValues

                // First, determine the maximum abundance value in mDataValues
                var maxAbundance = (from item in mDataValues select item.Item1).Max();

                // Round maxAbundance up to the next highest integer
                maxAbundance = (float)Math.Ceiling(maxAbundance);

                // Now determine the histogram bin size
                double binSize = maxAbundance / HISTOGRAM_BIN_COUNT;
                if (binSize < 1)
                    binSize = 1;

                // Initialize histogramData
                var binCount = (int)(maxAbundance / binSize) + 1;
                var histogramBinCounts = new int[binCount];
                var histogramBinStartIntensity = new double[binCount];

                for (var index = 0; index < binCount; index++)
                {
                    histogramBinStartIntensity[index] = index * binSize;
                }

                // Parse mDataValues to populate histogramBinCounts
                var dataPointIndex = 0;
                var dataPointCount = mDataValues.Count;
                foreach (var dataPoint in mDataValues)
                {
                    int targetBin;
                    if (dataPoint.Item1 <= 0)
                    {
                        targetBin = 0;
                    }
                    else
                    {
                        targetBin = (int)Math.Floor(dataPoint.Item1 / binSize);
                    }

                    if (targetBin < binCount - 1)
                    {
                        if (dataPoint.Item1 >= histogramBinStartIntensity[targetBin + 1])
                        {
                            targetBin++;
                        }
                    }

                    histogramBinCounts[targetBin]++;

                    if (dataPoint.Item1 < histogramBinStartIntensity[targetBin])
                    {
                        if (dataPoint.Item1 < histogramBinStartIntensity[targetBin] - binSize / 1000)
                        {
                            // This is unexpected
                            Console.WriteLine(
                                "Unexpected code reached in FilterDataArrayMaxCount.FilterDataByMaxDataCountToKeep");
                        }
                    }

                    if (dataPointIndex % 10000 == 0)
                    {
                        UpdateProgress(0 + (dataPointIndex + 1) / (float)dataPointCount / SUBTASK_STEP_COUNT * 100.0f);
                    }
                    dataPointIndex++;
                }

                // Now examine the frequencies in histogramBinCounts() to determine the minimum value to consider when sorting
                var pointTotal = 0;
                var binToSort = -1;
                for (var index = binCount - 1; index >= 0; index += -1)
                {
                    pointTotal += histogramBinCounts[index];
                    if (pointTotal >= MaximumDataCountToLoad)
                    {
                        binToSort = index;
                        break;
                    }
                }

                UpdateProgress(1.0f / SUBTASK_STEP_COUNT * 100.0f);

                if (binToSort >= 0)
                {
                    // Find the data with intensity >= histogramBinStartIntensity(binToSort)
                    // We actually only need to sort the data in binToSort

                    var binToSortAbundanceMinimum = histogramBinStartIntensity[binToSort];
                    double binToSortAbundanceMaximum = maxAbundance + 1;
                    if (binToSort < binCount - 1)
                    {
                        binToSortAbundanceMaximum = histogramBinStartIntensity[binToSort + 1];
                    }

                    if (Math.Abs(binToSortAbundanceMaximum - binToSortAbundanceMinimum) < float.Epsilon)
                    {
                        // Is this code ever reached?
                        // If yes, then the code below won't populate binnedData with any data
                        useFullDataSort = true;
                    }

                    var binnedData = new List<Tuple<float, int>>();

                    if (!useFullDataSort)
                    {
                        if (histogramBinCounts[binToSort] <= 0)
                        {
                            // Is this code ever reached?
                            useFullDataSort = true;
                        }
                    }

                    if (!useFullDataSort)
                    {
                        var dataCountImplicitlyIncluded = 0;
                        for (var index = 0; index < dataPointCount; index++)
                        {
                            if (mDataValues[index].Item1 < binToSortAbundanceMinimum)
                            {
                                // Skip this data point
                                mDataValues[index] = GetSkippedDataPoint(mDataValues[index]);
                            }
                            else if (mDataValues[index].Item1 < binToSortAbundanceMaximum)
                            {
                                // Value is in the bin to sort; add to the BinToSort arrays

                                // Replaced with a list: binToSortAbundances[binToSortDataCount] = mDataValues[index].Item1;
                                // Replaced with a list: binToSortDataIndices[binToSortDataCount] = mDataValues[index].Item2;
                                // Replaced with a list: binToSortDataCount += 1;

                                binnedData.Add(mDataValues[index]);
                            }
                            else
                            {
                                dataCountImplicitlyIncluded++;
                            }

                            if (index % 10000 == 0)
                            {
                                UpdateProgress(1 + (index + 1) / (float)dataPointCount / SUBTASK_STEP_COUNT * 100.0f);
                            }
                        }

                        var binnedDataCount = binnedData.Count;

                        if (MaximumDataCountToLoad - dataCountImplicitlyIncluded - binnedDataCount == 0)
                        {
                            // No need to sort and examine the data for BinToSort since we'll ultimately include all of it
                        }
                        else
                        {
                            SortAndMarkPointsToSkip(binnedData, MaximumDataCountToLoad - dataCountImplicitlyIncluded, SUBTASK_STEP_COUNT);
                        }

                        // Synchronize the data in binToSortAbundances and binToSortDataIndices with mDataValues
                        // mDataValues has not been sorted and therefore mDataIndices should currently be sorted ascending on "valid data point index"
                        // binToSortDataIndices should also currently be sorted ascending on "valid data point index" so the following Do Loop within a For Loop should sync things up

                        var originalDataArrayIndex = 0;
                        for (var index = 0; index < binnedDataCount; index++)
                        {
                            while (binnedData[index].Item2 > mDataValues[originalDataArrayIndex].Item2)
                            {
                                originalDataArrayIndex++;
                            }

                            if (Math.Abs(binnedData[index].Item1 - SkipDataPointFlag) < float.Epsilon)
                            {
                                if (mDataValues[originalDataArrayIndex].Item2 == binnedData[index].Item2)
                                {
                                    mDataValues[originalDataArrayIndex] = GetSkippedDataPoint(mDataValues[originalDataArrayIndex]);
                                }
                                else
                                {
                                    Console.WriteLine("Index mis-match in FilterDataByMaxDataCountToKeep; this code shouldn't be reached");
                                }
                            }
                            originalDataArrayIndex++;

                            if (binnedDataCount < 1000 || index % 100 == 0)
                            {
                                UpdateProgress(3 + (index + 1) / (float)binnedDataCount / SUBTASK_STEP_COUNT * 100.0f);
                            }
                        }
                    }
                }
                else
                {
                    useFullDataSort = true;
                }

                if (useFullDataSort)
                {
                    // This shouldn't normally be necessary

                    // We have to sort all of the data; this can be quite slow
                    SortAndMarkPointsToSkip(mDataValues, MaximumDataCountToLoad, SUBTASK_STEP_COUNT);
                }

                UpdateProgress(4f / SUBTASK_STEP_COUNT * 100.0f);
            }
            catch (Exception ex)
            {
                throw new Exception("Error in FilterDataByMaxDataCountToKeep: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Sort the data and mark data points beyond the first maximumDataCountInArraysToLoad points with mSkipDataPointFlag
        /// </summary>
        /// <param name="dataValuesAndIndices"></param>
        /// <param name="maximumDataCountInArraysToLoad"></param>
        /// <param name="subtaskStepCount"></param>
        /// <remarks>
        /// This sub uses a full sort to filter the data
        /// This will be slow for large arrays and you should therefore use FilterDataByMaxDataCountToKeep if possible
        /// </remarks>
        private void SortAndMarkPointsToSkip(
            List<Tuple<float, int>> dataValuesAndIndices,
            int maximumDataCountInArraysToLoad, int subtaskStepCount)
        {
            var dataCount = dataValuesAndIndices.Count;

            if (dataCount > 0)
            {
                dataValuesAndIndices.Sort();

                UpdateProgress(2.333f / subtaskStepCount * 100.0f);

                // Change the abundance values to mSkipDataPointFlag for data up to index dataCount-maximumDataCountInArraysToLoad-1
                for (var index = 0; index < dataCount - maximumDataCountInArraysToLoad; index++)
                {
                    dataValuesAndIndices[index] = GetSkippedDataPoint(dataValuesAndIndices[index]);
                }

                UpdateProgress(2.666f / subtaskStepCount * 100.0f);

                // Re-sort, this time on the data index value (.Item2)
                dataValuesAndIndices.Sort(new SortByIndex());
            }

            UpdateProgress(3f / subtaskStepCount * 100.0f);
        }
        private void UpdateProgress(float progress)
        {
            Progress = progress;

            ProgressChanged?.Invoke(Progress);
        }
    }

    internal class SortByIndex : IComparer<Tuple<float, int>>
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
