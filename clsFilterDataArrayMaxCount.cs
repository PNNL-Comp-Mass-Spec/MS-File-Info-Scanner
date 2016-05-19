using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

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


	private int mDataCount;
	private float[] mDataValues;

	private int[] mDataIndices;

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

	public clsFilterDataArrayMaxCount() : this(INITIAL_MEMORY_RESERVE)
	{
	}

	public clsFilterDataArrayMaxCount(int InitialCapacity)
	{
		mSkipDataPointFlag = DEFAULT_SKIP_DATA_POINT_FLAG;
		this.Clear(InitialCapacity);
	}


	public void AddDataPoint(float sngAbundance, int intDataPointIndex)
	{
		if (mDataCount >= mDataValues.Length) {
			Array.Resize(ref mDataValues, Convert.ToInt32(Math.Floor(mDataValues.Length * 1.5)));
			Array.Resize(ref mDataIndices, mDataValues.Length);
		}

		mDataValues(mDataCount) = sngAbundance;
		mDataIndices(mDataCount) = intDataPointIndex;

		mDataCount += 1;
	}

	public void Clear(int InitialCapacity)
	{
		mMaximumDataCountToKeep = 400000;

		mTotalIntensityPercentageFilterEnabled = false;
		mTotalIntensityPercentageFilter = 90;

		if (InitialCapacity < 4) {
			InitialCapacity = 4;
		}

		mDataCount = 0;
		mDataValues = new float[InitialCapacity];
		mDataIndices = new int[InitialCapacity];
	}

	public float GetAbundanceByIndex(int intDataPointIndex)
	{
		if (intDataPointIndex >= 0 & intDataPointIndex < mDataCount) {
			return mDataValues(intDataPointIndex);
		} else {
			// Invalid data point index value
			return -1;
		}
	}


	public void FilterData()
	{
		if (mDataCount <= 0) {
			// Nothing to do
		} else {
			//' Shrink the arrays to mDataCount
			if (mDataCount < mDataValues.Length) {
				Array.Resize(ref mDataValues, mDataCount);
				Array.Resize(ref mDataIndices, mDataCount);
			}

			FilterDataByMaxDataCountToKeep();

		}

	}


	private void FilterDataByMaxDataCountToKeep()
	{
		const int HISTOGRAM_BIN_COUNT = 5000;

		int intIndex = 0;
		int intPointTotal = 0;
		int intBinCount = 0;
		int intTargetBin = 0;
		int intBinToSort = 0;
		int intOriginalDataArrayIndex = 0;

		bool blnUseFullDataSort = false;

		float sngMaxAbundance = 0;
		double dblBinSize = 0;

		int[] intHistogramBinCounts = null;
		double[] dblHistogramBinStartIntensity = null;

		double dblBinToSortAbundanceMinimum = 0;
		double dblBinToSortAbundanceMaximum = 0;

		float[] sngBinToSortAbundances = null;
		int[] intBinToSortDataIndices = null;
		int intBinToSortDataCount = 0;
		int intDataCountImplicitlyIncluded = 0;


		try {
			sngBinToSortAbundances = new float[10];
			intBinToSortDataIndices = new int[10];

			UpdateProgress(0);

			blnUseFullDataSort = false;
			if (mDataCount == 0) {
				// No data loaded
			} else if (mDataCount <= mMaximumDataCountToKeep) {
				// Loaded less than mMaximumDataCountToKeep data points
				// Nothing to filter

			} else {
				// In order to speed up the sorting, we're first going to make a histogram
				//  (aka frequency distribution) of the abundances in mDataValues

				// First, determine the maximum abundance value in mDataValues
				sngMaxAbundance = float.MinValue;
				for (intIndex = 0; intIndex <= mDataCount - 1; intIndex++) {
					if (mDataValues[intIndex] > sngMaxAbundance) {
						sngMaxAbundance = mDataValues[intIndex];
					}
				}

				// Round sngMaxAbundance up to the next highest integer
				sngMaxAbundance = Convert.ToSingle(Math.Ceiling(sngMaxAbundance));

				// Now determine the histogram bin size
				dblBinSize = sngMaxAbundance / HISTOGRAM_BIN_COUNT;
				if (dblBinSize < 1)
					dblBinSize = 1;

				// Initialize intHistogramData
				intBinCount = Convert.ToInt32(sngMaxAbundance / dblBinSize) + 1;
				intHistogramBinCounts = new int[intBinCount];
				dblHistogramBinStartIntensity = new double[intBinCount];

				for (intIndex = 0; intIndex <= intBinCount - 1; intIndex++) {
					dblHistogramBinStartIntensity[intIndex] = intIndex * dblBinSize;
				}

				// Parse mDataValues to populate intHistogramBinCounts
				for (intIndex = 0; intIndex <= mDataCount - 1; intIndex++) {
					if (mDataValues[intIndex] <= 0) {
						intTargetBin = 0;
					} else {
						intTargetBin = Convert.ToInt32(Math.Floor(mDataValues[intIndex] / dblBinSize));
					}

					if (intTargetBin < intBinCount - 1) {
						if (mDataValues[intIndex] >= dblHistogramBinStartIntensity(intTargetBin + 1)) {
							intTargetBin += 1;
						}
					}

					intHistogramBinCounts(intTargetBin) += 1;

					if (mDataValues[intIndex] < dblHistogramBinStartIntensity(intTargetBin)) {
						if (mDataValues[intIndex] < dblHistogramBinStartIntensity(intTargetBin) - dblBinSize / 1000) {
							// This is unexpected
							mDataValues[intIndex] = mDataValues[intIndex];
						}
					}

					if (intIndex % 10000 == 0) {
						UpdateProgress(Convert.ToSingle((0 + (intIndex + 1) / Convert.ToDouble(mDataCount)) / SUBTASK_STEP_COUNT * 100.0));
					}
				}

				// Now examine the frequencies in intHistogramBinCounts() to determine the minimum value to consider when sorting
				intPointTotal = 0;
				intBinToSort = -1;
				for (intIndex = intBinCount - 1; intIndex >= 0; intIndex += -1) {
					intPointTotal = intPointTotal + intHistogramBinCounts[intIndex];
					if (intPointTotal >= mMaximumDataCountToKeep) {
						intBinToSort = intIndex;
						break; // TODO: might not be correct. Was : Exit For
					}
				}

				UpdateProgress(1 / SUBTASK_STEP_COUNT * 100.0);

				if (intBinToSort >= 0) {
					// Find the data with intensity >= dblHistogramBinStartIntensity(intBinToSort)
					// We actually only need to sort the data in bin intBinToSort

					dblBinToSortAbundanceMinimum = dblHistogramBinStartIntensity(intBinToSort);
					dblBinToSortAbundanceMaximum = sngMaxAbundance + 1;
					if (intBinToSort < intBinCount - 1) {
						dblBinToSortAbundanceMaximum = dblHistogramBinStartIntensity(intBinToSort + 1);
					}

					if (dblBinToSortAbundanceMaximum == dblBinToSortAbundanceMinimum) {
						// Is this code ever reached?
						// If yes, then the code below won't populate sngBinToSortAbundances() and intBinToSortDataIndices() with any data
						blnUseFullDataSort = true;
					}

					if (!blnUseFullDataSort) {
						intBinToSortDataCount = 0;
						if (intHistogramBinCounts(intBinToSort) > 0) {
							sngBinToSortAbundances = new float[intHistogramBinCounts(intBinToSort)];
							intBinToSortDataIndices = new int[intHistogramBinCounts(intBinToSort)];
						} else {
							// Is this code ever reached?
							blnUseFullDataSort = true;
						}
					}

					if (!blnUseFullDataSort) {
						intDataCountImplicitlyIncluded = 0;
						for (intIndex = 0; intIndex <= mDataCount - 1; intIndex++) {
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

								sngBinToSortAbundances(intBinToSortDataCount) = mDataValues[intIndex];
								intBinToSortDataIndices(intBinToSortDataCount) = mDataIndices[intIndex];
								intBinToSortDataCount += 1;
							} else {
								intDataCountImplicitlyIncluded = intDataCountImplicitlyIncluded + 1;
							}

							if (intIndex % 10000 == 0) {
								UpdateProgress(Convert.ToSingle((1 + (intIndex + 1) / Convert.ToDouble(mDataCount)) / SUBTASK_STEP_COUNT * 100.0));
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
							SortAndMarkPointsToSkip(ref sngBinToSortAbundances, ref intBinToSortDataIndices, intBinToSortDataCount, mMaximumDataCountToKeep - intDataCountImplicitlyIncluded, SUBTASK_STEP_COUNT);
						}

						// Synchronize the data in sngBinToSortAbundances and intBinToSortDataIndices with mDataValues and mDataValues
						// mDataValues and mDataIndices have not been sorted and therefore mDataIndices should currently be sorted ascending on "valid data point index"
						// intBinToSortDataIndices should also currently be sorted ascending on "valid data point index" so the following Do Loop within a For Loop should sync things up

						intOriginalDataArrayIndex = 0;
						for (intIndex = 0; intIndex <= intBinToSortDataCount - 1; intIndex++) {
							while (intBinToSortDataIndices[intIndex] > mDataIndices(intOriginalDataArrayIndex)) {
								intOriginalDataArrayIndex += 1;
							}

							if (sngBinToSortAbundances[intIndex] == mSkipDataPointFlag) {
								if (mDataIndices(intOriginalDataArrayIndex) == intBinToSortDataIndices[intIndex]) {
									mDataValues(intOriginalDataArrayIndex) = mSkipDataPointFlag;
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
					SortAndMarkPointsToSkip(ref mDataValues, ref mDataIndices, mDataCount, mMaximumDataCountToKeep, SUBTASK_STEP_COUNT);
				}

			}

			UpdateProgress(4 / SUBTASK_STEP_COUNT * 100.0);

			return;

		} catch (Exception ex) {
			throw new Exception("Error in FilterDataByMaxDataCountToKeep: " + ex.Message, ex);
		}

	}

	// This sub uses a full sort to filter the data
	// This will be slow for large arrays and you should therefore use FilterDataByMaxDataCountToKeep if possible

	private void SortAndMarkPointsToSkip(ref float[] sngAbundances, ref int[] intDataIndices, int intDataCount, int intMaximumDataCountInArraysToLoad, int intSubtaskStepCount)
	{
		int intIndex = 0;

		if (intDataCount > 0) {
			// Sort sngAbundances ascending, sorting intDataIndices in parallel
			Array.Sort(sngAbundances, intDataIndices, 0, intDataCount);

			UpdateProgress(Convert.ToSingle((2.333 / intSubtaskStepCount) * 100.0));

			// Change the abundance values to mSkipDataPointFlag for data up to index intDataCount-intMaximumDataCountInArraysToLoad-1
			for (intIndex = 0; intIndex <= intDataCount - intMaximumDataCountInArraysToLoad - 1; intIndex++) {
				sngAbundances[intIndex] = mSkipDataPointFlag;
			}

			UpdateProgress(Convert.ToSingle((2.666 / intSubtaskStepCount) * 100.0));

			// Re-sort, this time on intDataIndices with sngAbundances in parallel
			Array.Sort(intDataIndices, sngAbundances, 0, intDataCount);

		}

		UpdateProgress(Convert.ToSingle(3 / intSubtaskStepCount * 100.0));

	}

	private void UpdateProgress(float sngProgress)
	{
		mProgress = sngProgress;

		if (ProgressChanged != null) {
			ProgressChanged(mProgress);
		}
	}
}


