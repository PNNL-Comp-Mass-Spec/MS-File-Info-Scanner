using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpectraTypeClassifier
{
    [CLSCompliant(true)]
    public class clsSpectrumTypeClassifier
    {

        #region "Constants and Enums"

        public const int DEFAULT_PPM_DIFF_THRESHOLD = 50;
        public enum eCentroidStatusConstants
        {
            Unknown = 0,
            Profile = 1,
            Centroid = 2
        }

        #endregion


        #region "Module Wide Variables"
        protected string mErrorMessage;

        protected int mPpmDiffThreshold;

        // The following dictionaries keep track of the number of spectra at each MSLevel (1 for MS1, 2 for MS2, etc.)

        /// <summary>
        /// Centroided spectra
        /// </summary>
        /// <remarks></remarks>
        protected Dictionary<int, int> mCentroidedSpectra;

        /// <summary>
        /// Centroided spectra that were mis-classified as profile mode
        /// </summary>
        /// <remarks></remarks>

        protected Dictionary<int, int> mCentroidedSpectraClassifiedAsProfile;
        
        /// <summary>
        /// All spectra
        /// </summary>
        /// <remarks></remarks>
        protected Dictionary<int, int> mTotalSpectra;

        #endregion
        protected readonly clsMedianUtilities mMedianUtils;

        #region "Events"
        public event ReadingSpectraEventHandler ReadingSpectra;
        public delegate void ReadingSpectraEventHandler(int spectraProcessed);
        public event ProcessingCompleteEventHandler ProcessingComplete;
        public delegate void ProcessingCompleteEventHandler(int spectraProcessed);

        public event ErrorEventEventHandler ErrorEvent;
        public delegate void ErrorEventEventHandler(string Message);
        #endregion

        #region "Properties"

        public string ErrorMessage {
            get {
                if (string.IsNullOrWhiteSpace(mErrorMessage))
                    return string.Empty;
                return mErrorMessage;
            }
        }

        public int PpmDiffThreshold {
            get { return mPpmDiffThreshold; }
            set { mPpmDiffThreshold = value; }
        }

        #endregion

        public clsSpectrumTypeClassifier()
        {
            mMedianUtils = new clsMedianUtilities();

            mCentroidedSpectra = new Dictionary<int, int>();
            mCentroidedSpectraClassifiedAsProfile = new Dictionary<int, int>();
            mTotalSpectra = new Dictionary<int, int>();

            mPpmDiffThreshold = DEFAULT_PPM_DIFF_THRESHOLD;
        }

        public int CentroidedSpectra()
        {
            return mCentroidedSpectra.Sum(item => item.Value);
        }

        public int CentroidedMS1Spectra()
        {
            return (from item in mCentroidedSpectra where item.Key <= 1 select item.Value).Sum();
        }

        public int CentroidedMSnSpectra()
        {
            return (from item in mCentroidedSpectra where item.Key > 1 select item.Value).Sum();
        }

        public int CentroidedMS1SpectraClassifiedAsProfile()
        {
            return (from item in mCentroidedSpectraClassifiedAsProfile where item.Key <= 1 select item.Value).Sum();
        }

        public int CentroidedMSnSpectraClassifiedAsProfile()
        {
            return (from item in mCentroidedSpectraClassifiedAsProfile where item.Key > 1 select item.Value).Sum();
        }

        public double FractionCentroided()
        {

            var total = TotalSpectra();
            if (total == 0) {
                return 0;
            } else {
                return CentroidedSpectra() / Convert.ToDouble(total);
            }

        }

        public double FractionCentroidedMSn()
        {

            var total = TotalMSnSpectra();
            if (total == 0) {
                return 0;
            } else {
                return CentroidedMSnSpectra() / Convert.ToDouble(total);
            }

        }

        public int TotalSpectra()
        {
            return mTotalSpectra.Sum(item => item.Value);
        }

        public int TotalMS1Spectra()
        {
            return mTotalSpectra.Where(item => item.Key <= 1).Sum(item => item.Value);
        }

        public int TotalMSnSpectra()
        {
            return mTotalSpectra.Where(item => item.Key > 1).Sum(item => item.Value);
        }


        /// <summary>
        /// Examine the spectra in a _DTA.txt file to determine the number of centroided spectra
        /// </summary>
        /// <param name="strCDTAPath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool CheckCDTAFile(string strCDTAPath)
        {

            var dtLastStatusTime = DateTime.UtcNow;

            var splitChars = new[] { ' ' };

            try {
                if (!File.Exists(strCDTAPath)) {
                    throw new FileNotFoundException("CDTA file not found: " + strCDTAPath);
                }

                // Read the m/z values in the _dta.txt file
                // Using a simple text reader here for speed purposes

                using (var srDtaFile = new StreamReader(new FileStream(strCDTAPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {

                    var lstPpmDiffs = new List<double>(2000);

                    double dblPreviousMZ = 0;

                    while (!srDtaFile.EndOfStream)
                    {
                        var strLineIn = srDtaFile.ReadLine();

                        if (!string.IsNullOrEmpty(strLineIn)) {
                            if (strLineIn.StartsWith("=============")) {
                                // DTA header line

                                // Process the data for the previous spectrum
                                CheckPPMDiffs(lstPpmDiffs, 2, eCentroidStatusConstants.Unknown);

                                // Reset the previous m/z value and skip the next line
                                if (!srDtaFile.EndOfStream)
                                    srDtaFile.ReadLine();

                                dblPreviousMZ = 0;
                                lstPpmDiffs.Clear();

                                if (DateTime.UtcNow.Subtract(dtLastStatusTime).TotalSeconds >= 30) {
                                    if (ReadingSpectra != null) {
                                        ReadingSpectra(TotalSpectra());
                                    }
                                    dtLastStatusTime = DateTime.UtcNow;
                                }

                            } else {
                                var dataColumns = strLineIn.Split(splitChars, 3);

                                double dblMZ;
                                if (!double.TryParse(dataColumns[0], out dblMZ))
                                {
                                    continue;
                                }

                                if (dblPreviousMZ > 0 && dblMZ > dblPreviousMZ) {
                                    var delMPPM = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ;
                                    lstPpmDiffs.Add(delMPPM);
                                }
                                dblPreviousMZ = dblMZ;
                            }

                        }

                    }

                    // Process the data for the previous spectrum
                    CheckPPMDiffs(lstPpmDiffs, 2, eCentroidStatusConstants.Unknown);

                }

                if (ProcessingComplete != null) {
                    ProcessingComplete(TotalSpectra());
                }

                return true;

            } catch (Exception ex) {
                mErrorMessage = "Exception in CheckCDTAFile: " + ex.Message;
                if (ErrorEvent != null) {
                    ErrorEvent(mErrorMessage);
                }
                return false;
            }

        }

        /// <summary>
        /// Examines lstPpmDiffs to determine if the data is centroided data
        /// </summary>
        /// <param name="lstPpmDiffs"></param>
        /// <param name="msLevel">1 for MS1, 2 for MS2, etc.</param>
        /// <param name="centroidingStatus"></param>
        /// <remarks>Increments class property TotalSpectra if lstPpmDiffs is not empty; increments class property CentroidedSpectra if the data is centroided</remarks>
        protected void CheckPPMDiffs(List<double> lstPpmDiffs, int msLevel, eCentroidStatusConstants centroidingStatus)
        {
            if (lstPpmDiffs.Count > 0) {
                IncrementDictionaryByMSLevel(ref mTotalSpectra, msLevel);

                var empiricalCentroidStatus = eCentroidStatusConstants.Profile;
                if (IsDataCentroided(lstPpmDiffs)) {
                    empiricalCentroidStatus = eCentroidStatusConstants.Centroid;
                }

                if (centroidingStatus == eCentroidStatusConstants.Centroid & empiricalCentroidStatus == eCentroidStatusConstants.Profile) {
                    // The empirical algorithm has classified a centroid spectrum as profile
                    // Change it back to centroid
                    empiricalCentroidStatus = eCentroidStatusConstants.Centroid;

                    IncrementDictionaryByMSLevel(ref mCentroidedSpectraClassifiedAsProfile, msLevel);
                }

                if (empiricalCentroidStatus == eCentroidStatusConstants.Centroid) {
                    IncrementDictionaryByMSLevel(ref mCentroidedSpectra, msLevel);
                }
            }

        }


        private void IncrementDictionaryByMSLevel(ref Dictionary<int, int> dctSpectrumCounts, int msLevel)
        {
            int spectraCount;

            if (dctSpectrumCounts.TryGetValue(msLevel, out spectraCount)) {
                dctSpectrumCounts[msLevel] = spectraCount + 1;
            } else {
                dctSpectrumCounts[msLevel] = 1;
            }

        }

        public void CheckSpectrum(List<double> lstMZs, int msLevel)
        {
            CheckSpectrum(lstMZs, msLevel, assumeSorted: false);
        }

        public void CheckSpectrum(List<double> lstMZs, int msLevel, eCentroidStatusConstants centroidingStatus)
        {
            CheckSpectrum(lstMZs, msLevel, assumeSorted: false, centroidingStatus: centroidingStatus);
        }


        public void CheckSpectrum(List<double> lstMZs, int msLevel, bool assumeSorted, eCentroidStatusConstants centroidingStatus = eCentroidStatusConstants.Unknown)
        {
            if (!assumeSorted) {
                // Check whether sorting is required
                for (var i = 1; i <= lstMZs.Count - 1; i++) {
                    if (lstMZs[i] < lstMZs[i - 1]) {
                        lstMZs.Sort();
                        break;
                    }
                }
            }

            var lstPpmDiffs = new List<double>(lstMZs.Count);

            for (var i = 1; i <= lstMZs.Count - 1; i++) {
                var dblMZ = lstMZs[i];
                var dblPreviousMZ = lstMZs[i - 1];

                if (dblPreviousMZ > 0 && dblMZ > dblPreviousMZ) {
                    var delMppm = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ;
                    lstPpmDiffs.Add(delMppm);
                }
            }

            CheckPPMDiffs(lstPpmDiffs, msLevel, centroidingStatus);

        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="dblMZs"></param>
        /// <param name="msLevel"></param>
        /// <remarks>Assumes the ions are sorted</remarks>
        public void CheckSpectrum(double[] dblMZs, int msLevel)
        {
            CheckSpectrum(dblMZs.Length, dblMZs, msLevel);
        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="ionCount">Number of items in dblMZs; if -1, then parses all data in dblMZs</param>
        /// <param name="dblMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <remarks>Assumes the ions are sorted</remarks>
        public void CheckSpectrum(int ionCount, double[] dblMZs, int msLevel, eCentroidStatusConstants centroidingStatus = eCentroidStatusConstants.Unknown)
        {
            if (ionCount < 0) {
                ionCount = dblMZs.Length;
            }

            // Possibly sort dblMZs
            for (var i = 1; i <= ionCount - 1; i++) {
                if (dblMZs[i] < dblMZs[i - 1]) {
                    // Sort required
                    Array.Sort(dblMZs);
                    break; // TODO: might not be correct. Was : Exit For
                }
            }

            var lstPpmDiffs = new List<double>(ionCount);

            for (var i = 1; i <= ionCount - 1; i++) {
                var dblMZ = dblMZs[i];
                var dblPreviousMZ = dblMZs[i - 1];

                if (dblPreviousMZ > 0 && dblMZ > dblPreviousMZ) {
                    var delMppm = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ;
                    lstPpmDiffs.Add(delMppm);
                }
            }

            CheckPPMDiffs(lstPpmDiffs, msLevel, centroidingStatus);
        }

        public void Reset()
        {
            mErrorMessage = string.Empty;
            mTotalSpectra.Clear();
            mCentroidedSpectra.Clear();
            mCentroidedSpectraClassifiedAsProfile.Clear();
        }

        /// <summary>
        /// Computes the median of the ppm m/z difference values in lstPpmDiffs
        /// </summary>
        /// <param name="lstPpmDiffs">List of mass difference values between adjacent data points, converted to ppm</param>
        /// <returns>True if the median is at least as large as PpmDiffThreshold</returns>
        /// <remarks></remarks>
        public bool IsDataCentroided(IList<double> lstPpmDiffs)
        {

            var medianDelMppm = mMedianUtils.Median(lstPpmDiffs);

            if (medianDelMppm < PpmDiffThreshold) {
                // Profile mode data
                return false;
            } else {
                // Centroided data
                return true;
            }

        }

    }
}
