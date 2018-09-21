using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using pwiz.CLI.data;
using PRISM;

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsProteowizardDataParser : EventNotifier
    {

        private readonly pwiz.ProteowizardWrapper.MSDataFileReader mPWiz;

        private readonly clsDatasetStatsSummarizer mDatasetStatsSummarizer;
        private readonly clsTICandBPIPlotter mTICandBPIPlot;

        private readonly clsLCMSDataPlotter mLCMS2DPlot;
        private readonly bool mSaveLCMS2DPlots;
        private readonly bool mSaveTICAndBPI;

        private readonly bool mCheckCentroidingStatus;
        private bool mHighResMS1;

        private readonly Regex mGetQ1MZ;
        private readonly Regex mGetQ3MZ;

        private bool mHighResMS2;
        public bool HighResMS1
        {
            get => mHighResMS1;
            set => mHighResMS1 = value;
        }

        public bool HighResMS2
        {
            get => mHighResMS2;
            set => mHighResMS2 = value;
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
        public clsProteowizardDataParser(
            pwiz.ProteowizardWrapper.MSDataFileReader objPWiz,
            clsDatasetStatsSummarizer objDatasetStatsSummarizer,
            clsTICandBPIPlotter objTICandBPIPlot,
            clsLCMSDataPlotter objLCMS2DPlot,
            bool blnSaveLCMS2DPlots,
            bool blnSaveTICandBPI,
            bool blnCheckCentroidingStatus)
        {
            mPWiz = objPWiz;
            mDatasetStatsSummarizer = objDatasetStatsSummarizer;
            mTICandBPIPlot = objTICandBPIPlot;
            mLCMS2DPlot = objLCMS2DPlot;

            mSaveLCMS2DPlots = blnSaveLCMS2DPlots;
            mSaveTICAndBPI = blnSaveTICandBPI;
            mCheckCentroidingStatus = blnCheckCentroidingStatus;

            mGetQ1MZ = new Regex("Q[0-9]=([0-9.]+)", RegexOptions.Compiled);

            mGetQ3MZ = new Regex("Q1=[0-9.]+ Q3=([0-9.]+)", RegexOptions.Compiled);

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
            if (reMatch.Success)
            {
                if (double.TryParse(reMatch.Groups[1].Value, out dblMZ))
                {
                    return true;
                }
            }

            dblMZ = 0;
            return false;
        }

        private int FindNearestInList(List<float> lstItems, float sngValToFind)
        {
            var intIndexMatch = lstItems.BinarySearch(sngValToFind);
            if (intIndexMatch >= 0)
            {
                // Exact match found
            }
            else
            {
                // Find the nearest match
                intIndexMatch = intIndexMatch ^ -1;
                if (intIndexMatch == lstItems.Count)
                {
                    intIndexMatch -= 1;
                }

                if (intIndexMatch > 0)
                {
                    // Possibly decrement intIndexMatch
                    if (Math.Abs(lstItems[intIndexMatch - 1] - sngValToFind) < Math.Abs(lstItems[intIndexMatch] - sngValToFind))
                    {
                        intIndexMatch -= 1;
                    }
                }

                if (intIndexMatch < lstItems.Count)
                {
                    // Possible increment intIndexMatch
                    if (Math.Abs(lstItems[intIndexMatch + 1] - sngValToFind) < Math.Abs(lstItems[intIndexMatch] - sngValToFind))
                    {
                        intIndexMatch += 1;
                    }
                }

                if (intIndexMatch < 0)
                {
                    intIndexMatch = 0;
                }
                else if (intIndexMatch == lstItems.Count)
                {
                    intIndexMatch = lstItems.Count - 1;
                }

            }

            return intIndexMatch;
        }

        public void PossiblyUpdateAcqTimeStart(clsDatasetFileInfo datasetFileInfo, double dblRuntimeMinutes)
        {
            if (dblRuntimeMinutes > 0)
            {
                var dtAcqTimeStartAlt = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblRuntimeMinutes);

                if (dtAcqTimeStartAlt < datasetFileInfo.AcqTimeStart && datasetFileInfo.AcqTimeStart.Subtract(dtAcqTimeStartAlt).TotalDays < 1)
                {
                    datasetFileInfo.AcqTimeStart = dtAcqTimeStartAlt;
                }
            }
        }

        private void ProcessSRM(
            string strChromID,
            float[] sngTimes,
            float[] sngIntensities,
            List<float> lstTICScanTimes,
            IReadOnlyList<int> lstTICScanNumbers,
            ref double dblRuntimeMinutes,
            IDictionary<int, Dictionary<double, double>> dct2DDataParent,
            IDictionary<int, Dictionary<double, double>> dct2DDataProduct,
            IDictionary<int, float> dct2DDataScanTimes)
        {

            // Attempt to parse out the product m/z
            var blnParentMZFound = ExtractQ1MZ(strChromID, out var dblParentMZ);
            var blnProductMZFound = ExtractQ3MZ(strChromID, out var dblProductMZ);

            for (var intIndex = 0; intIndex <= sngTimes.Length - 1; intIndex++)
            {
                // Find the ScanNumber in the TIC nearest to sngTimes[intIndex]
                var intIndexMatch = FindNearestInList(lstTICScanTimes, sngTimes[intIndex]);
                var intScanNumber = lstTICScanNumbers[intIndexMatch];

                // Bump up dblRuntimeMinutes if necessary
                if (sngTimes[intIndex] > dblRuntimeMinutes)
                {
                    dblRuntimeMinutes = sngTimes[intIndex];
                }

                var objScanStatsEntry = new clsScanStatsEntry
                {
                    ScanNumber = intScanNumber,
                    ScanType = 1,
                    ScanTypeName = "SRM",
                    ScanFilterText = StripExtraFromChromID(strChromID),
                    ElutionTime = sngTimes[intIndex].ToString("0.0###"),
                    TotalIonIntensity = sngIntensities[intIndex].ToString("0.0"),
                    BasePeakIntensity = sngIntensities[intIndex].ToString("0.0")
                };

                if (blnParentMZFound)
                {
                    objScanStatsEntry.BasePeakMZ = dblParentMZ.ToString("0.0###");
                }
                else if (blnProductMZFound)
                {
                    objScanStatsEntry.BasePeakMZ = dblProductMZ.ToString("0.0###");
                }
                else
                {
                    objScanStatsEntry.BasePeakMZ = "0";
                }

                // Base peak signal to noise ratio
                objScanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                objScanStatsEntry.IonCount = 1;
                objScanStatsEntry.IonCountRaw = 1;

                mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                if (mSaveLCMS2DPlots && sngIntensities[intIndex] > 0)
                {
                    // Store the m/z and intensity values in dct2DDataParent and dct2DDataProduct

                    if (blnParentMZFound)
                    {
                        Store2DPlotDataPoint(dct2DDataParent, intScanNumber, dblParentMZ, sngIntensities[intIndex]);
                    }

                    if (blnProductMZFound)
                    {
                        Store2DPlotDataPoint(dct2DDataProduct, intScanNumber, dblProductMZ, sngIntensities[intIndex]);
                    }

                    if (!dct2DDataScanTimes.ContainsKey(intScanNumber))
                    {
                        dct2DDataScanTimes[intScanNumber] = sngTimes[intIndex];
                    }

                }

            }

        }

        private void ProcessTIC(
            IReadOnlyList<float> sngTimes,
            IReadOnlyList<float> sngIntensities,
            List<float> lstTICScanTimes,
            List<int> lstTICScanNumbers,
            ref double dblRuntimeMinutes,
            bool blnStoreInTICandBPIPlot)
        {
            for (var intIndex = 0; intIndex <= sngTimes.Count - 1; intIndex++)
            {
                lstTICScanTimes.Add(sngTimes[intIndex]);
                lstTICScanNumbers.Add(intIndex + 1);

                // Bump up dblRuntimeMinutes if necessary
                if (sngTimes[intIndex] > dblRuntimeMinutes)
                {
                    dblRuntimeMinutes = sngTimes[intIndex];
                }

                if (blnStoreInTICandBPIPlot)
                {
                    // Use this TIC chromatogram for this dataset since there are no normal Mass Spectra
                    mTICandBPIPlot.AddDataTICOnly(intIndex + 1, 1, sngTimes[intIndex], sngIntensities[intIndex]);

                }

            }

            // Make sure lstTICScanTimes is sorted
            var blnNeedToSort = false;
            for (var intIndex = 1; intIndex <= lstTICScanTimes.Count - 1; intIndex++)
            {
                if (lstTICScanTimes[intIndex] < lstTICScanTimes[intIndex - 1])
                {
                    blnNeedToSort = true;
                    break;
                }
            }

            if (blnNeedToSort)
            {
                var sngTICScanTimes = new float[lstTICScanTimes.Count];
                var intTICScanNumbers = new int[lstTICScanTimes.Count];

                lstTICScanTimes.CopyTo(sngTICScanTimes);
                lstTICScanNumbers.CopyTo(intTICScanNumbers);

                Array.Sort(sngTICScanTimes, intTICScanNumbers);

                lstTICScanTimes.Clear();
                lstTICScanNumbers.Clear();

                for (var intIndex = 0; intIndex <= sngTICScanTimes.Length - 1; intIndex++)
                {
                    lstTICScanTimes.Add(sngTICScanTimes[intIndex]);
                    lstTICScanNumbers.Add(intTICScanNumbers[intIndex]);
                }

            }

        }

        public void StoreChromatogramInfo(clsDatasetFileInfo datasetFileInfo, out bool blnTICStored, out bool blnSRMDataCached, out double dblRuntimeMinutes)
        {
            var lstTICScanTimes = new List<float>();
            var lstTICScanNumbers = new List<int>();

            // This dictionary tracks the m/z and intensity values for parent (Q1) ions of each scan
            // Key is ScanNumber; Value is a dictionary holding m/z and intensity values for that scan
            var dct2DDataParent = new Dictionary<int, Dictionary<double, double>>();

            // This dictionary tracks the m/z and intensity values for product (Q3) ions of each scan
            var dct2DDataProduct = new Dictionary<int, Dictionary<double, double>>();

            // This dictionary tracks the scan times for each scan number tracked by dct2DDataParent and/or dct2DDataProduct
            var dct2DDataScanTimes = new Dictionary<int, float>();

            // Note that even for a small .Wiff file (1.5 MB), obtaining the first chromatogram will take some time (20 to 60 seconds)
            // The chromatogram at index 0 should be the TIC
            // The chromatogram at index >=1 will be each SRM

            dblRuntimeMinutes = 0;
            blnTICStored = false;
            blnSRMDataCached = false;

            for (var intChromIndex = 0; intChromIndex <= mPWiz.ChromatogramCount - 1; intChromIndex++)
            {
                try
                {
                    if (intChromIndex == 0)
                    {
                        OnStatusEvent("Obtaining chromatograms (this could take as long as 60 seconds)");
                    }
                    mPWiz.GetChromatogram(intChromIndex, out var strChromID, out var sngTimes, out var sngIntensities);

                    if (strChromID == null)
                        strChromID = string.Empty;

                    var oCVParams = mPWiz.GetChromatogramCVParams(intChromIndex);

                    if (TryGetCVParam(oCVParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, out var _))
                    {
                        // This chromatogram is the TIC

                        var blnStoreInTICandBPIPlot = (mSaveTICAndBPI && mPWiz.SpectrumCount == 0);

                        ProcessTIC(sngTimes, sngIntensities, lstTICScanTimes, lstTICScanNumbers, ref dblRuntimeMinutes, blnStoreInTICandBPIPlot);

                        blnTICStored = blnStoreInTICandBPIPlot;

                        datasetFileInfo.ScanCount = sngTimes.Length;

                    }

                    if (TryGetCVParam(oCVParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, out _))
                    {
                        // This chromatogram is an SRM scan

                        ProcessSRM(strChromID, sngTimes, sngIntensities, lstTICScanTimes, lstTICScanNumbers, ref dblRuntimeMinutes, dct2DDataParent, dct2DDataProduct, dct2DDataScanTimes);

                        blnSRMDataCached = true;
                    }

                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error processing chromatogram " + intChromIndex + ": " + ex.Message, ex);
                }

            }

            if (!mSaveLCMS2DPlots)
            {
                return;
            }

            if (dct2DDataParent.Count <= 0 && dct2DDataProduct.Count <= 0)
            {
                return;
            }

            // Now that all of the chromatograms have been processed, transfer data from dct2DDataParent and dct2DDataProduct into mLCMS2DPlot
            mLCMS2DPlot.Options.MS1PlotTitle = "Q1 m/z";
            mLCMS2DPlot.Options.MS2PlotTitle = "Q3 m/z";

            Store2DPlotData(dct2DDataScanTimes, dct2DDataParent, dct2DDataProduct);
        }

        public void StoreMSSpectraInfo(clsDatasetFileInfo datasetFileInfo, bool blnTICStored, ref double dblRuntimeMinutes)
        {
            try
            {
                double dblTIC = 0;
                double dblBPI = 0;

                OnStatusEvent("Obtaining scan times and MSLevels (this could take several minutes)");

                mPWiz.GetScanTimesAndMsLevels(out var dblScanTimes, out var intMSLevels);

                // The scan times returned by .GetScanTimesAndMsLevels() are the acquisition time in seconds from the start of the analysis
                // Convert these to minutes
                for (var intScanIndex = 0; intScanIndex <= dblScanTimes.Length - 1; intScanIndex++)
                {
                    dblScanTimes[intScanIndex] /= 60.0;
                }

                OnStatusEvent("Reading spectra");
                var dtLastProgressTime = DateTime.UtcNow;

                for (var intScanIndex = 0; intScanIndex <= dblScanTimes.Length - 1; intScanIndex++)
                {

                    try
                    {
                        var blnComputeTIC = true;
                        var blnComputeBPI = true;

                        // Obtain the raw mass spectrum
                        var oMSDataSpectrum = mPWiz.GetSpectrum(intScanIndex);

                        var objScanStatsEntry = new clsScanStatsEntry
                        {
                            ScanNumber = intScanIndex + 1,
                            ScanType = oMSDataSpectrum.Level
                        };

                        if (intMSLevels[intScanIndex] > 1)
                        {
                            if (mHighResMS2)
                            {
                                objScanStatsEntry.ScanTypeName = "HMSn";
                            }
                            else
                            {
                                objScanStatsEntry.ScanTypeName = "MSn";
                            }
                        }
                        else
                        {
                            if (mHighResMS1)
                            {
                                objScanStatsEntry.ScanTypeName = "HMS";
                            }
                            else
                            {
                                objScanStatsEntry.ScanTypeName = "MS";
                            }

                        }

                        objScanStatsEntry.ScanFilterText = "";
                        objScanStatsEntry.ElutionTime = dblScanTimes[intScanIndex].ToString("0.0###");

                        // Bump up dblRuntimeMinutes if necessary
                        if (dblScanTimes[intScanIndex] > dblRuntimeMinutes)
                        {
                            dblRuntimeMinutes = dblScanTimes[intScanIndex];
                        }

                        var oSpectrum = mPWiz.GetSpectrumObject(intScanIndex);

                        if (TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_total_ion_current, out var param))
                        {
                            dblTIC = param.value;
                            objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5);
                            blnComputeTIC = false;
                        }

                        if (TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_intensity, out param))
                        {
                            dblBPI = param.value;
                            objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5);

                            if (TryGetCVParam(oSpectrum.scanList.scans[0].cvParams, pwiz.CLI.cv.CVID.MS_base_peak_m_z, out param))
                            {
                                objScanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(param.value, 5);
                                blnComputeBPI = false;
                            }
                        }

                        // Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                        objScanStatsEntry.IonCount = oMSDataSpectrum.Mzs.Length;
                        objScanStatsEntry.IonCountRaw = objScanStatsEntry.IonCount;

                        if (blnComputeBPI || blnComputeTIC)
                        {
                            // Step through the raw data to compute the BPI and TIC

                            var dblMZs = oMSDataSpectrum.Mzs;
                            var dblIntensities = oMSDataSpectrum.Intensities;

                            dblTIC = 0;
                            dblBPI = 0;
                            double dblBasePeakMZ = 0;

                            for (var intIndex = 0; intIndex <= dblMZs.Length - 1; intIndex++)
                            {
                                dblTIC += dblIntensities[intIndex];
                                if (dblIntensities[intIndex] > dblBPI)
                                {
                                    dblBPI = dblIntensities[intIndex];
                                    dblBasePeakMZ = dblMZs[intIndex];
                                }
                            }

                            objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5);
                            objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5);
                            objScanStatsEntry.BasePeakMZ = StringUtilities.ValueToString(dblBasePeakMZ, 5);

                        }

                        mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                        if (mSaveTICAndBPI && !blnTICStored)
                        {
                            mTICandBPIPlot.AddData(objScanStatsEntry.ScanNumber, intMSLevels[intScanIndex], (float)dblScanTimes[intScanIndex], dblBPI, dblTIC);
                        }

                        if (mSaveLCMS2DPlots)
                        {
                            mLCMS2DPlot.AddScan(objScanStatsEntry.ScanNumber, intMSLevels[intScanIndex], (float)dblScanTimes[intScanIndex], oMSDataSpectrum.Mzs.Length, oMSDataSpectrum.Mzs, oMSDataSpectrum.Intensities);
                        }

                        if (mCheckCentroidingStatus)
                        {
                            mDatasetStatsSummarizer.ClassifySpectrum(oMSDataSpectrum.Mzs, intMSLevels[intScanIndex], "Scan " + objScanStatsEntry.ScanNumber);
                        }

                    }
                    catch (Exception ex)
                    {
                        OnErrorEvent("Error loading header info for scan " + intScanIndex + 1 + ": " + ex.Message);
                    }

                    if (DateTime.UtcNow.Subtract(dtLastProgressTime).TotalSeconds > 60)
                    {
                        OnDebugEvent(" ... " + ((intScanIndex + 1) / (double)dblScanTimes.Length * 100).ToString("0.0") + "% complete");
                        dtLastProgressTime = DateTime.UtcNow;
                    }

                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error obtaining scan times and MSLevels using GetScanTimesAndMsLevels: " + ex.Message, ex);
            }

        }

        private void Store2DPlotData(
            IReadOnlyDictionary<int, float> dct2DDataScanTimes,
            Dictionary<int, Dictionary<double, double>> dct2DDataParent,
            Dictionary<int, Dictionary<double, double>> dct2DDataProduct)
        {
            // This variable keeps track of the length of the largest Dictionary(Of Double, Double) var in dct2DData
            var intMax2DDataCount = 1;

            var int2DScanNumMin = int.MaxValue;
            var int2DScanNumMax = 0;

            // Determine the min/max scan numbers in dct2DDataParent
            // Also determine intMax2DDataCount

            UpdateDataRanges(dct2DDataParent, ref intMax2DDataCount, ref int2DScanNumMin, ref int2DScanNumMax);
            UpdateDataRanges(dct2DDataProduct, ref intMax2DDataCount, ref int2DScanNumMin, ref int2DScanNumMax);

            Store2DPlotDataWork(dct2DDataParent, dct2DDataScanTimes, 1, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax);
            Store2DPlotDataWork(dct2DDataProduct, dct2DDataScanTimes, 2, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax);

        }

        private void Store2DPlotDataPoint(IDictionary<int, Dictionary<double, double>> dct2DData, int intScanNumber, double dblMZ, double dblIntensity)
        {

            if (dct2DData.TryGetValue(intScanNumber, out var obj2DMzAndIntensity))
            {
                if (obj2DMzAndIntensity.TryGetValue(dblMZ, out var dblCurrentIntensity))
                {
                    // Bump up the stored intensity at dblProductMZ
                    obj2DMzAndIntensity[dblMZ] = dblCurrentIntensity + dblIntensity;
                }
                else
                {
                    obj2DMzAndIntensity.Add(dblMZ, dblIntensity);
                }
            }
            else
            {
                obj2DMzAndIntensity = new Dictionary<double, double> { { dblMZ, dblIntensity } };
            }

            // Store the data for this scan
            dct2DData[intScanNumber] = obj2DMzAndIntensity;

        }

        private void Store2DPlotDataWork(
            Dictionary<int, Dictionary<double, double>> dct2DData,
            IReadOnlyDictionary<int, float> dct2DDataScanTimes,
            int intMSLevel,
            int intMax2DDataCount,
            int int2DScanNumMin,
            int int2DScanNumMax)
        {
            var dblMZList = new double[intMax2DDataCount];
            var dblIntensityList = new double[intMax2DDataCount];

            if (dct2DData == null)
            {
                return;
            }

            using (var dct2DEnum = dct2DData.GetEnumerator())
            {
                while (dct2DEnum.MoveNext())
                {
                    var int2DPlotScanNum = dct2DEnum.Current.Key;

                    var obj2DMzAndIntensity = dct2DEnum.Current.Value;

                    obj2DMzAndIntensity.Keys.CopyTo(dblMZList, 0);
                    obj2DMzAndIntensity.Values.CopyTo(dblIntensityList, 0);

                    // Make sure the data is sorted
                    Array.Sort(dblMZList, dblIntensityList, 0, obj2DMzAndIntensity.Count);

                    // Store the data
                    mLCMS2DPlot.AddScan(dct2DEnum.Current.Key, intMSLevel, dct2DDataScanTimes[int2DPlotScanNum], obj2DMzAndIntensity.Count, dblMZList,
                                        dblIntensityList);
                }
            }

            if (int2DScanNumMin / (double)int2DScanNumMax > 0.5)
            {
                // Zoom in the 2D plot to prevent all of the the data from being scrunched to the right
                mLCMS2DPlot.Options.UseObservedMinScan = true;
            }

        }

        private string StripExtraFromChromID(string strText)
        {

            // If strText looks like:
            // SRM SIC Q1=506.6 Q3=132.1 sample=1 period=1 experiment=1 transition=0

            // then remove text from sample= on

            var intCharIndex = strText.IndexOf("sample=", StringComparison.InvariantCultureIgnoreCase);
            if (intCharIndex > 0)
            {
                strText = strText.Substring(0, intCharIndex).TrimEnd();
            }

            return strText;

        }

        public static bool TryGetCVParam(CVParamList oCVParams, pwiz.CLI.cv.CVID cvidToFind, out CVParam paramMatch)
        {
            foreach (var param in oCVParams)
            {
                if (param.cvid == cvidToFind)
                {
                    if (!param.empty())
                    {
                        paramMatch = param;
                        return true;
                    }
                }
            }
            paramMatch = null;
            return false;
        }

        private void UpdateDataRanges(
            Dictionary<int, Dictionary<double, double>> dct2DData,
            ref int intMax2DDataCount,
            ref int int2DScanNumMin,
            ref int int2DScanNumMax)
        {
            if (dct2DData == null)
                return;

            using (var dct2DEnum = dct2DData.GetEnumerator())
            {
                while (dct2DEnum.MoveNext())
                {
                    var int2DPlotScanNum = dct2DEnum.Current.Key;

                    if (dct2DEnum.Current.Value.Count > intMax2DDataCount)
                    {
                        intMax2DDataCount = dct2DEnum.Current.Value.Count;
                    }

                    if (int2DPlotScanNum < int2DScanNumMin)
                    {
                        int2DScanNumMin = int2DPlotScanNum;
                    }

                    if (int2DPlotScanNum > int2DScanNumMax)
                    {
                        int2DScanNumMax = int2DPlotScanNum;
                    }
                }
            }
        }

    }
}

