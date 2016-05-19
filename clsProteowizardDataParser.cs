using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

using PNNLOmics.Utilities;
using System.Text.RegularExpressions;
using MSFileInfoScanner;

[CLSCompliant(false)]
public class clsProteowizardDataParser
{


	public event ErrorEventEventHandler ErrorEvent;
	public delegate void ErrorEventEventHandler(string Message);
	public event MessageEventEventHandler MessageEvent;
	public delegate void MessageEventEventHandler(string Message);

	private pwiz.ProteowizardWrapper.MSDataFileReader mPWiz;

	private DSSummarizer.clsDatasetStatsSummarizer mDatasetStatsSummarizer;
	private clsTICandBPIPlotter mTICandBPIPlot;

	private clsLCMSDataPlotter mLCMS2DPlot;
	private bool mSaveLCMS2DPlots;
	private bool mSaveTICAndBPI;

	private bool mCheckCentroidingStatus;
	private bool mHighResMS1;

    private readonly Regex mGetQ1MZ;
    private readonly Regex mGetQ3MZ;

	private bool mHighResMS2;
	public bool HighResMS1 {
		get { return mHighResMS1; }
		set { mHighResMS1 = value; }
	}

	public bool HighResMS2 {
		get { return mHighResMS2; }
		set { mHighResMS2 = value; }
	}

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="objPWiz"></param>
    /// <param name="objDatasetStatsSummarizer"></param>
    /// <param name="objTICandBPIPlot"></param>
    /// <param name="objLCMS2DPlot"></param>
    /// <param name="blnSaveLCMS2DPlots"></param>
    /// <param name="blnSaveTICandBPI"></param>
    /// <param name="blnCheckCentroidingStatus"></param>
	public clsProteowizardDataParser(ref pwiz.ProteowizardWrapper.MSDataFileReader objPWiz, ref DSSummarizer.clsDatasetStatsSummarizer objDatasetStatsSummarizer, ref clsTICandBPIPlotter objTICandBPIPlot, ref clsLCMSDataPlotter objLCMS2DPlot, bool blnSaveLCMS2DPlots, bool blnSaveTICandBPI, bool blnCheckCentroidingStatus)
	{
		mPWiz = objPWiz;
		mDatasetStatsSummarizer = objDatasetStatsSummarizer;
		mTICandBPIPlot = objTICandBPIPlot;
		mLCMS2DPlot = objLCMS2DPlot;

		mSaveLCMS2DPlots = blnSaveLCMS2DPlots;
		mSaveTICAndBPI = blnSaveTICandBPI;
		mCheckCentroidingStatus = blnCheckCentroidingStatus;

        const string Q_REGEX = "Q[0-9]=([0-9.]+)";
        mGetQ1MZ = new Regex(Q_REGEX, RegexOptions.Compiled);

        const string Q1_Q3_REGEX = "Q1=[0-9.]+ Q3=([0-9.]+)";
        mGetQ3MZ = new Regex(Q1_Q3_REGEX, RegexOptions.Compiled);

	}

    private bool ExtractQ1MZ(string strChromID, out double dblMZ)
	{
        return ExtractQMZ(mGetQ1MZ, strChromID, out dblMZ);

	}

    private bool ExtractQ3MZ(string strChromID, out double dblMZ)
	{

        return ExtractQMZ(mGetQ3MZ, strChromID, out dblMZ);

	}

	private bool ExtractQMZ(Regex reGetMZ, string strChromID, out double dblMZ)
	{
		var reMatch = reGetMZ.Match(strChromID);
		if (reMatch.Success) {
			if (double.TryParse(reMatch.Groups[1].Value, out dblMZ)) {
				return true;
			}
		}

	    dblMZ = 0;
		return false;
	}

	private int FindNearestInList(ref List<float> lstItems, float sngValToFind)
	{

		int intIndexMatch = 0;

		intIndexMatch = lstItems.BinarySearch(sngValToFind);
		if (intIndexMatch >= 0) {
			// Exact match found			
		} else {
			// Find the nearest match
			intIndexMatch = intIndexMatch ^ -1;
			if (intIndexMatch == lstItems.Count) {
				intIndexMatch -= 1;
			}

			if (intIndexMatch > 0) {
				// Possibly decrement intIndexMatch
				if (Math.Abs(lstItems[intIndexMatch - 1] - sngValToFind) < Math.Abs(lstItems[intIndexMatch] - sngValToFind)) {
					intIndexMatch -= 1;
				}
			}

			if (intIndexMatch < lstItems.Count) {
				// Possible increment intIndexMatch
				if (Math.Abs(lstItems[intIndexMatch + 1] - sngValToFind) < Math.Abs(lstItems[intIndexMatch] - sngValToFind)) {
					intIndexMatch += 1;
				}
			}

			if (intIndexMatch < 0) {
				intIndexMatch = 0;
			} else if (intIndexMatch == lstItems.Count) {
				intIndexMatch = lstItems.Count - 1;
			}

		}

		return intIndexMatch;
	}


	public void PossiblyUpdateAcqTimeStart(clsDatasetFileInfo datasetFileInfo, double dblRuntimeMinutes)
	{
		if (dblRuntimeMinutes > 0) {
			var dtAcqTimeStartAlt = default(DateTime);
			dtAcqTimeStartAlt = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblRuntimeMinutes);

			if (dtAcqTimeStartAlt < datasetFileInfo.AcqTimeStart && datasetFileInfo.AcqTimeStart.Subtract(dtAcqTimeStartAlt).TotalDays < 1) {
				datasetFileInfo.AcqTimeStart = dtAcqTimeStartAlt;
			}
		}

	}


	private void ProcessSRM(string strChromID, ref float[] sngTimes, ref float[] sngIntensities, ref List<float> lstTICScanTimes, ref List<int> lstTICScanNumbers, ref double dblRuntimeMinutes, ref Dictionary<int, Dictionary<double, double>> dct2DDataParent, ref Dictionary<int, Dictionary<double, double>> dct2DDataProduct, ref Dictionary<int, float> dct2DDataScanTimes)
	{
		bool blnParentMZFound = false;
		bool blnProductMZFound = false;

		double dblParentMZ = 0;
		double dblProductMZ = 0;

		// Attempt to parse out the product m/z
		blnParentMZFound = ExtractQ1MZ(strChromID, out dblParentMZ);
        blnProductMZFound = ExtractQ3MZ(strChromID, out dblProductMZ);


		for (var intIndex = 0; intIndex <= sngTimes.Length - 1; intIndex++) {
			// Find the ScanNumber in the TIC nearest to sngTimes[intIndex]
			var intIndexMatch = FindNearestInList(ref lstTICScanTimes, sngTimes[intIndex]);
			var intScanNumber = lstTICScanNumbers[intIndexMatch];

			// Bump up dblRuntimeMinutes if necessary
			if (sngTimes[intIndex] > dblRuntimeMinutes) {
				dblRuntimeMinutes = sngTimes[intIndex];
			}


		    var objScanStatsEntry = new DSSummarizer.clsScanStatsEntry
		    {
		        ScanNumber = intScanNumber,
		        ScanType = 1,
		        ScanTypeName = "SRM",
		        ScanFilterText = StripExtraFromChromID(strChromID),
		        ElutionTime = sngTimes[intIndex].ToString("0.0000"),
		        TotalIonIntensity = sngIntensities[intIndex].ToString("0.0"),
		        BasePeakIntensity = sngIntensities[intIndex].ToString("0.0")
		    };


		    if (blnParentMZFound) {
				objScanStatsEntry.BasePeakMZ = dblParentMZ.ToString("0.000");
			} else if (blnProductMZFound) {
				objScanStatsEntry.BasePeakMZ = dblProductMZ.ToString("0.000");
			} else {
				objScanStatsEntry.BasePeakMZ = "0";
			}

			// Base peak signal to noise ratio
			objScanStatsEntry.BasePeakSignalToNoiseRatio = "0";

			objScanStatsEntry.IonCount = 1;
			objScanStatsEntry.IonCountRaw = 1;

			mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);


			if (mSaveLCMS2DPlots && sngIntensities[intIndex] > 0) {
				// Store the m/z and intensity values in dct2DDataParent and dct2DDataProduct

				if (blnParentMZFound) {
					Store2DPlotDataPoint(ref dct2DDataParent, intScanNumber, dblParentMZ, sngIntensities[intIndex]);
				}

				if (blnProductMZFound) {
					Store2DPlotDataPoint(ref dct2DDataProduct, intScanNumber, dblProductMZ, sngIntensities[intIndex]);
				}


				if (!dct2DDataScanTimes.ContainsKey(intScanNumber)) {
					dct2DDataScanTimes[intScanNumber] = sngTimes[intIndex];
				}

			}

		}

	}


	private void ProcessTIC(string strChromID, ref float[] sngTimes, ref float[] sngIntensities, ref List<float> lstTICScanTimes, ref List<int> lstTICScanNumbers, ref double dblRuntimeMinutes, bool blnStoreInTICandBPIPlot)
	{
		for (var intIndex = 0; intIndex <= sngTimes.Length - 1; intIndex++) {
			lstTICScanTimes.Add(sngTimes[intIndex]);
			lstTICScanNumbers.Add(intIndex + 1);

			// Bump up dblRuntimeMinutes if necessary
			if (sngTimes[intIndex] > dblRuntimeMinutes) {
				dblRuntimeMinutes = sngTimes[intIndex];
			}

			if (blnStoreInTICandBPIPlot) {
				// Use this TIC chromatogram for this dataset since there are no normal Mass Spectra
				mTICandBPIPlot.AddDataTICOnly(intIndex + 1, 1, sngTimes[intIndex], sngIntensities[intIndex]);

			}

		}

		// Make sure lstTICScanTimes is sorted
		object blnNeedToSort = false;
		for (intIndex = 1; intIndex <= lstTICScanTimes.Count - 1; intIndex++) {
			if (lstTICScanTimes[intIndex] < lstTICScanTimes(intIndex - 1)) {
				blnNeedToSort = true;
				break; // TODO: might not be correct. Was : Exit For
			}
		}

		if (blnNeedToSort) {
			float[] sngTICScanTimes = null;
			int[] intTICScanNumbers = null;
			sngTICScanTimes = new float[lstTICScanTimes.Count];
			intTICScanNumbers = new int[lstTICScanTimes.Count];

			lstTICScanTimes.CopyTo(sngTICScanTimes);
			lstTICScanNumbers.CopyTo(intTICScanNumbers);

			Array.Sort(sngTICScanTimes, intTICScanNumbers);

			lstTICScanTimes.Clear();
			lstTICScanNumbers.Clear();

			for (intIndex = 0; intIndex <= sngTICScanTimes.Length - 1; intIndex++) {
				lstTICScanTimes.Add(sngTICScanTimes[intIndex]);
				lstTICScanNumbers.Add(intTICScanNumbers[intIndex]);
			}


		}

	}

	private void ReportMessage(string strMessage)
	{
		if (MessageEvent != null) {
			MessageEvent(strMessage);
		}
	}

	private void ReportError(string strError)
	{
		if (ErrorEvent != null) {
			ErrorEvent(strError);
		}
	}


	public void StoreChromatogramInfo(clsDatasetFileInfo datasetFileInfo, ref bool blnTICStored, ref bool blnSRMDataCached, ref double dblRuntimeMinutes)
	{
		string strChromID = string.Empty;
		float[] sngTimes = null;
		float[] sngIntensities = null;
		sngTimes = new float[1];
		sngIntensities = new float[1];

		object lstTICScanTimes = new List<float>();
		object lstTICScanNumbers = new List<int>();

		// This dictionary tracks the m/z and intensity values for parent (Q1) ions of each scan
		// Key is ScanNumber; Value is a dictionary holding m/z and intensity values for that scan
		Dictionary<int, Dictionary<double, double>> dct2DDataParent = default(Dictionary<int, Dictionary<double, double>>);
		dct2DDataParent = new Dictionary<int, Dictionary<double, double>>();

		// This dictionary tracks the m/z and intensity values for product (Q3) ions of each scan
		Dictionary<int, Dictionary<double, double>> dct2DDataProduct = default(Dictionary<int, Dictionary<double, double>>);
		dct2DDataProduct = new Dictionary<int, Dictionary<double, double>>();

		// This dictionary tracks the scan times for each scan number tracked by dct2DDataParent and/or dct2DDataProduct
		Dictionary<int, float> dct2DDataScanTimes = default(Dictionary<int, float>);
		dct2DDataScanTimes = new Dictionary<int, float>();

		// Note that even for a small .Wiff file (1.5 MB), obtaining the first chromatogram will take some time (20 to 60 seconds)
		// The chromatogram at index 0 should be the TIC
		// The chromatogram at index >=1 will be each SRM

		dblRuntimeMinutes = 0;


		for (intChromIndex = 0; intChromIndex <= mPWiz.ChromatogramCount - 1; intChromIndex++) {
			try {
				if (intChromIndex == 0) {
					ReportMessage("Obtaining chromatograms (this could take as long as 60 seconds)");
				}
				mPWiz.GetChromatogram(intChromIndex, strChromID, sngTimes, sngIntensities);

				if (strChromID == null)
					strChromID = string.Empty;

				pwiz.CLI.data.CVParamList oCVParams = default(pwiz.CLI.data.CVParamList);
				pwiz.CLI.data.CVParam param = null;
				oCVParams = mPWiz.GetChromatogramCVParams(intChromIndex);

				if (TryGetCVParam(ref oCVParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, ref param)) {
					// This chromatogram is the TIC

					object blnStoreInTICandBPIPlot = false;
					if (mSaveTICAndBPI && mPWiz.SpectrumCount == 0) {
						blnStoreInTICandBPIPlot = true;
					}

					ProcessTIC(strChromID, ref sngTimes, ref sngIntensities, ref lstTICScanTimes, ref lstTICScanNumbers, ref dblRuntimeMinutes, blnStoreInTICandBPIPlot);

					blnTICStored = blnStoreInTICandBPIPlot;

					datasetFileInfo.ScanCount = sngTimes.Length;

				}

				if (TryGetCVParam(ref oCVParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, ref param)) {
					// This chromatogram is an SRM scan

					ProcessSRM(strChromID, ref sngTimes, ref sngIntensities, ref lstTICScanTimes, ref lstTICScanNumbers, ref dblRuntimeMinutes, ref dct2DDataParent, ref dct2DDataProduct, ref dct2DDataScanTimes);

					blnSRMDataCached = true;
				}


			} catch (Exception ex) {
				ReportError("Error processing chromatogram " + intChromIndex + ": " + ex.Message);
			}

		}


		if (mSaveLCMS2DPlots) {
			// Now that all of the chromatograms have been processed, transfer data from dct2DDataParent and dct2DDataProduct into mLCMS2DPlot

			if (dct2DDataParent.Count > 0 || dct2DDataProduct.Count > 0) {
				mLCMS2DPlot.Options.MS1PlotTitle = "Q1 m/z";
				mLCMS2DPlot.Options.MS2PlotTitle = "Q3 m/z";

				Store2DPlotData(ref dct2DDataScanTimes, ref dct2DDataParent, ref dct2DDataProduct);
			}

		}


	}


	public void StoreMSSpectraInfo(clsDatasetFileInfo datasetFileInfo, bool blnTICStored, ref double dblRuntimeMinutes)
	{
		try {
			double[] dblScanTimes = null;
			byte[] intMSLevels = null;

			dblScanTimes = new double[1];
			intMSLevels = new byte[1];
			double dblTIC = 0;
			double dblBPI = 0;

			DateTime dtLastProgressTime = default(DateTime);

			ReportMessage("Obtaining scan times and MSLevels (this could take several minutes)");

			mPWiz.GetScanTimesAndMsLevels(dblScanTimes, intMSLevels);

			// The scan times returned by .GetScanTimesAndMsLevels() are the acquisition time in seconds from the start of the analysis
			// Convert these to minutes
			for (int intScanIndex = 0; intScanIndex <= dblScanTimes.Length - 1; intScanIndex++) {
				dblScanTimes(intScanIndex) /= 60.0;
			}

			ReportMessage("Reading spectra");
			dtLastProgressTime = DateTime.UtcNow;


			for (int intScanIndex = 0; intScanIndex <= dblScanTimes.Length - 1; intScanIndex++) {

				try {
					object blnComputeTIC = true;
					object blnComputeBPI = true;

					// Obtain the raw mass spectrum
					pwiz.ProteowizardWrapper.MsDataSpectrum oMSDataSpectrum = default(pwiz.ProteowizardWrapper.MsDataSpectrum);
					oMSDataSpectrum = mPWiz.GetSpectrum(intScanIndex);

					DSSummarizer.clsScanStatsEntry objScanStatsEntry = new DSSummarizer.clsScanStatsEntry();

					objScanStatsEntry.ScanNumber = intScanIndex + 1;
					objScanStatsEntry.ScanType = oMSDataSpectrum.Level;

					// Might be able to determine scan type info from oMSDataSpectrum.Precursors(0)
					// Alternatively, use .GetSpectrumPWiz
					pwiz.CLI.msdata.Spectrum oSpectrum = default(pwiz.CLI.msdata.Spectrum);
					pwiz.CLI.data.CVParam param = null;
					oSpectrum = mPWiz.GetSpectrumObject(intScanIndex);

					if (intMSLevels(intScanIndex) > 1) {
						if (mHighResMS2) {
							objScanStatsEntry.ScanTypeName = "HMSn";
						} else {
							objScanStatsEntry.ScanTypeName = "MSn";
						}
					} else {
						if (mHighResMS1) {
							objScanStatsEntry.ScanTypeName = "HMS";
						} else {
							objScanStatsEntry.ScanTypeName = "MS";
						}

					}


					objScanStatsEntry.ScanFilterText = "";
					objScanStatsEntry.ElutionTime = dblScanTimes(intScanIndex).ToString("0.0000");

					// Bump up dblRuntimeMinutes if necessary
					if (dblScanTimes(intScanIndex) > dblRuntimeMinutes) {
						dblRuntimeMinutes = dblScanTimes(intScanIndex);
					}

					if (TryGetCVParam(ref oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_total_ion_current, ref param)) {
						dblTIC = param.value;
						objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5);
						blnComputeTIC = false;
					}

					if (TryGetCVParam(ref oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_intensity, ref param)) {
						dblBPI = param.value;
						objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5);

						if (TryGetCVParam(ref oSpectrum.scanList.scans(0).cvParams, pwiz.CLI.cv.CVID.MS_base_peak_m_z, ref param)) {
							objScanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(param.value, 5);
							blnComputeBPI = false;
						}
					}

					// Base peak signal to noise ratio
					objScanStatsEntry.BasePeakSignalToNoiseRatio = "0";

					objScanStatsEntry.IonCount = oMSDataSpectrum.Mzs.Length;
					objScanStatsEntry.IonCountRaw = objScanStatsEntry.IonCount;

					if (blnComputeBPI | blnComputeTIC) {
						// Step through the raw data to compute the BPI and TIC

						double[] dblMZs = oMSDataSpectrum.Mzs;
						double[] dblIntensities = oMSDataSpectrum.Intensities;
						double dblBasePeakMZ = 0;

						dblTIC = 0;
						dblBPI = 0;
						dblBasePeakMZ = 0;

						for (intIndex = 0; intIndex <= dblMZs.Length - 1; intIndex++) {
							dblTIC += dblIntensities[intIndex];
							if (dblIntensities[intIndex] > dblBPI) {
								dblBPI = dblIntensities[intIndex];
								dblBasePeakMZ = dblMZs[intIndex];
							}
						}

						objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5);
						objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5);
						objScanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(dblBasePeakMZ, 5);

					}

					mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

					if (mSaveTICAndBPI & !blnTICStored) {
						mTICandBPIPlot.AddData(intScanIndex + 1, intMSLevels(intScanIndex), Convert.ToSingle(dblScanTimes(intScanIndex)), dblBPI, dblTIC);
					}

					if (mSaveLCMS2DPlots) {
						mLCMS2DPlot.AddScan(intScanIndex + 1, intMSLevels(intScanIndex), Convert.ToSingle(dblScanTimes(intScanIndex)), oMSDataSpectrum.Mzs.Length, oMSDataSpectrum.Mzs, oMSDataSpectrum.Intensities);
					}

					if (mCheckCentroidingStatus) {
						mDatasetStatsSummarizer.ClassifySpectrum(oMSDataSpectrum.Mzs, intMSLevels(intScanIndex));
					}

				} catch (Exception ex) {
					ReportError("Error loading header info for scan " + intScanIndex + 1 + ": " + ex.Message);
				}

				if (DateTime.UtcNow.Subtract(dtLastProgressTime).TotalSeconds > 60) {
					ReportMessage(" ... " + ((intScanIndex + 1) / Convert.ToDouble(dblScanTimes.Length) * 100).ToString("0.0") + "% complete");
					dtLastProgressTime = DateTime.UtcNow;
				}

			}

		} catch (Exception ex) {
			ReportError("Error obtaining scan times and MSLevels using GetScanTimesAndMsLevels: " + ex.Message);
		}

	}


	private void Store2DPlotData(ref Dictionary<int, float> dct2DDataScanTimes, ref Dictionary<int, Dictionary<double, double>> dct2DDataParent, ref Dictionary<int, Dictionary<double, double>> dct2DDataProduct)
	{
		// This variable keeps track of the length of the largest Dictionary(Of Double, Double) object in dct2DData
		object intMax2DDataCount = 1;

		int int2DScanNumMin = int.MaxValue;
		object int2DScanNumMax = 0;

		// Determine the min/max scan numbers in dct2DDataParent
		// Also determine intMax2DDataCount

		UpdateDataRanges(ref dct2DDataParent, ref intMax2DDataCount, ref int2DScanNumMin, ref int2DScanNumMax);
		UpdateDataRanges(ref dct2DDataProduct, ref intMax2DDataCount, ref int2DScanNumMin, ref int2DScanNumMax);

		Store2DPlotDataWork(ref dct2DDataParent, ref dct2DDataScanTimes, 1, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax);
		Store2DPlotDataWork(ref dct2DDataProduct, ref dct2DDataScanTimes, 2, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax);


	}


	private void Store2DPlotDataPoint(ref Dictionary<int, Dictionary<double, double>> dct2DData, int intScanNumber, double dblMZ, double dblIntensity)
	{
		Dictionary<double, double> obj2DMzAndIntensity = null;

		if (dct2DData.TryGetValue(intScanNumber, obj2DMzAndIntensity)) {
			double dblCurrentIntensity = 0;
			if (obj2DMzAndIntensity.TryGetValue(dblMZ, dblCurrentIntensity)) {
				// Bump up the stored intensity at dblProductMZ
				obj2DMzAndIntensity(dblMZ) = dblCurrentIntensity + dblIntensity;
			} else {
				obj2DMzAndIntensity.Add(dblMZ, dblIntensity);
			}
		} else {
			obj2DMzAndIntensity = new Dictionary<double, double>();
			obj2DMzAndIntensity.Add(dblMZ, dblIntensity);
		}

		// Store the data for this scan
		dct2DData(intScanNumber) = obj2DMzAndIntensity;

	}


	private void Store2DPlotDataWork(ref Dictionary<int, Dictionary<double, double>> dct2DData, ref Dictionary<int, float> dct2DDataScanTimes, int intMSLevel, int intMax2DDataCount, int int2DScanNumMin, int int2DScanNumMax)
	{
		double[] dblMZList = null;
		double[] dblIntensityList = null;
		dblMZList = new double[intMax2DDataCount];
		dblIntensityList = new double[intMax2DDataCount];

		Dictionary<int, Dictionary<double, double>>.Enumerator dct2DEnum = default(Dictionary<int, Dictionary<double, double>>.Enumerator);
		dct2DEnum = dct2DData.GetEnumerator();
		while (dct2DEnum.MoveNext()) {
			int int2DPlotScanNum = 0;
			int2DPlotScanNum = dct2DEnum.Current.Key;

			Dictionary<double, double> obj2DMzAndIntensity = default(Dictionary<double, double>);
			obj2DMzAndIntensity = dct2DEnum.Current.Value;

			obj2DMzAndIntensity.Keys.CopyTo(dblMZList, 0);
			obj2DMzAndIntensity.Values.CopyTo(dblIntensityList, 0);

			// Make sure the data is sorted
			Array.Sort(dblMZList, dblIntensityList, 0, obj2DMzAndIntensity.Count);

			// Store the data
			mLCMS2DPlot.AddScan(dct2DEnum.Current.Key, intMSLevel, dct2DDataScanTimes(int2DPlotScanNum), obj2DMzAndIntensity.Count, dblMZList, dblIntensityList);

		}


		if (int2DScanNumMin / Convert.ToDouble(int2DScanNumMax) > 0.5) {
			// Zoom in the 2D plot to prevent all of the the data from being scrunched to the right
			mLCMS2DPlot.Options.UseObservedMinScan = true;
		}

	}

	private string StripExtraFromChromID(string strText)
	{

		// If strText looks like:
		// SRM SIC Q1=506.6 Q3=132.1 sample=1 period=1 experiment=1 transition=0

		// then remove text from sample= on

		int intCharIndex = 0;

		intCharIndex = strText.IndexOf("sample=");
		if (intCharIndex > 0) {
			strText = strText.Substring(0, intCharIndex).TrimEnd();
		}

		return strText;

	}

	public static bool TryGetCVParam(ref pwiz.CLI.data.CVParamList oCVParams, pwiz.CLI.cv.CVID cvidToFind, ref pwiz.CLI.data.CVParam paramMatch)
	{

		foreach (pwiz.CLI.data.CVParam param in oCVParams) {
			if (param.cvid == cvidToFind) {
				if (!param.empty()) {
					paramMatch = param;
					return true;
				}
			}
		}

		return false;
	}


	private void UpdateDataRanges(ref Dictionary<int, Dictionary<double, double>> dct2DData, ref int intMax2DDataCount, ref int int2DScanNumMin, ref int int2DScanNumMax)
	{
		Dictionary<int, Dictionary<double, double>>.Enumerator dct2DEnum = default(Dictionary<int, Dictionary<double, double>>.Enumerator);
		int int2DPlotScanNum = 0;

		dct2DEnum = dct2DData.GetEnumerator();

		while (dct2DEnum.MoveNext()) {
			int2DPlotScanNum = dct2DEnum.Current.Key;

			if (dct2DEnum.Current.Value.Count > intMax2DDataCount) {
				intMax2DDataCount = dct2DEnum.Current.Value.Count;
			}

			if (int2DPlotScanNum < int2DScanNumMin) {
				int2DScanNumMin = int2DPlotScanNum;
			}

			if (int2DPlotScanNum > int2DScanNumMax) {
				int2DScanNumMax = int2DPlotScanNum;
			}
		}

	}
	static bool InitStaticVariableHelper(Microsoft.VisualBasic.CompilerServices.StaticLocalInitFlag flag)
	{
		if (flag.State == 0) {
			flag.State = 2;
			return true;
		} else if (flag.State == 2) {
			throw new Microsoft.VisualBasic.CompilerServices.IncompleteInitialization();
		} else {
			return false;
		}
	}

}

