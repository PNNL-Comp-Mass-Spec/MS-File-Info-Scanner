using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PRISM;

// ReSharper disable UnusedMember.Global

namespace SpectraTypeClassifier
{
    [CLSCompliant(true)]
    public class SpectrumTypeClassifier : EventNotifier
    {

        #region "Constants and Enums"

        /// <summary>
        /// Default spacing between adjacent data points for deciding that data is thresholded (default is 50 ppm)
        /// </summary>
        public const int DEFAULT_PPM_DIFF_THRESHOLD = 50;

        /// <summary>
        /// Default number of regions to divide the data into when confirming that the data is profile mode
        /// </summary>
        private const int DEFAULT_REGION_COUNT = 5;

        /// <summary>
        /// Consider a spectrum to have profile mode data if two thirds of the regions appear to have profile mode data
        /// </summary>
        private const double FRACTION_REGIONS_PROFILE = 2.0 / 3.0;

        public enum eCentroidStatusConstants
        {
            Unknown = 0,
            Profile = 1,
            Centroid = 2
        }

        #endregion

        #region "Class Wide Variables"

        private string mErrorMessage;

        /// <summary>
        /// Centroided spectra
        /// </summary>
        /// <remarks>Tracks the number of centroided spectra at each MSLevel (1 for MS1, 2 for MS2, etc.)</remarks>
        private readonly Dictionary<int, int> mCentroidedSpectra;

        /// <summary>
        /// Centroided spectra that were mis-classified as profile mode
        /// </summary>
        /// <remarks>Tracks the number of spectra at each MSLevel (1 for MS1, 2 for MS2, etc.)</remarks>
        private readonly Dictionary<int, int> mCentroidedSpectraClassifiedAsProfile;

        /// <summary>
        /// All spectra
        /// </summary>
        /// <remarks>Tracks the number of spectra at each MSLevel (1 for MS1, 2 for MS2, etc.)</remarks>
        private readonly Dictionary<int, int> mTotalSpectra;

        #endregion

        #region "Events"

        public event ReadingSpectraEventHandler ReadingSpectra;
        public delegate void ReadingSpectraEventHandler(int spectraProcessed);

        public event ProcessingCompleteEventHandler ProcessingComplete;
        public delegate void ProcessingCompleteEventHandler(int spectraProcessed);

        #endregion

        #region "Properties"

        /// <summary>
        /// Number of centroided spectra
        /// </summary>
        /// <returns></returns>
        public int CentroidedSpectra =>
            mCentroidedSpectra.Sum(item => item.Value);

        /// <summary>
        /// Number of centroided MS1 spectra
        /// </summary>
        /// <returns></returns>
        public int CentroidedMS1Spectra => (from item in mCentroidedSpectra where item.Key <= 1 select item.Value).Sum();


        /// <summary>
        /// Number of centroided MS2 spectra
        /// </summary>
        /// <returns></returns>
        public int CentroidedMSnSpectra =>
            (from item in mCentroidedSpectra where item.Key > 1 select item.Value).Sum();

        /// <summary>
        /// Number of MS1 spectra that empirically appears profile, but the calling class says is centroid
        /// </summary>
        /// <returns></returns>
        public int CentroidedMS1SpectraClassifiedAsProfile =>
            (from item in mCentroidedSpectraClassifiedAsProfile where item.Key <= 1 select item.Value).Sum();

        /// <summary>
        /// Number of MSn spectra that empirically appears profile, but the calling class says is centroid
        /// </summary>
        /// <returns></returns>
        public int CentroidedMSnSpectraClassifiedAsProfile =>
            (from item in mCentroidedSpectraClassifiedAsProfile where item.Key > 1 select item.Value).Sum();

        /// <summary>
        /// Most recent error message
        /// </summary>
        public string ErrorMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(mErrorMessage))
                    return string.Empty;
                return mErrorMessage;
            }
        }

        /// <summary>
        /// Fraction of all spectra that are centroided
        /// </summary>
        /// <returns></returns>
        public double FractionCentroided
        {
            get
            {
                var total = TotalSpectra;
                if (total == 0)
                {
                    return 0;
                }

                return CentroidedSpectra / (double)total;
            }
        }

        /// <summary>
        /// Fraction of MSn spectra that are centroided
        /// </summary>
        /// <returns></returns>
        public double FractionCentroidedMSn
        {
            get
            {
                var total = TotalMSnSpectra;
                if (total == 0)
                {
                    return 0;
                }

                return CentroidedMSnSpectra / (double)total;
            }
        }

        /// <summary>
        /// Spacing between adjacent data points for deciding that data is thresholded (default is 50 ppm)
        /// </summary>
        /// <remarks>If the median spacing between data points is lower than this threshold, the spectrum is considered profile mode data</remarks>
        public int PpmDiffThreshold { get; set; }

        /// <summary>
        /// Set to true to enable debug events
        /// </summary>
        /// <remarks>Off by default</remarks>
        public bool RaiseDebugEvents { get; set; }

        /// <summary>
        /// Number of spectra analyzed
        /// </summary>
        /// <returns></returns>
        public int TotalSpectra => mTotalSpectra.Sum(item => item.Value);

        /// <summary>
        /// Number of MS1 spectra analyzed
        /// </summary>
        /// <returns></returns>
        public int TotalMS1Spectra => mTotalSpectra.Where(item => item.Key <= 1).Sum(item => item.Value);

        /// <summary>
        /// Number of MSn spectra analyzed
        /// </summary>
        /// <returns></returns>
        public int TotalMSnSpectra => mTotalSpectra.Where(item => item.Key > 1).Sum(item => item.Value);

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public SpectrumTypeClassifier()
        {
            mCentroidedSpectra = new Dictionary<int, int>();
            mCentroidedSpectraClassifiedAsProfile = new Dictionary<int, int>();
            mTotalSpectra = new Dictionary<int, int>();

            PpmDiffThreshold = DEFAULT_PPM_DIFF_THRESHOLD;
        }

        /// <summary>
        /// Examine the spectra in a _DTA.txt file to determine the number of centroided spectra
        /// </summary>
        /// <param name="concatenatedDtaPath"></param>
        /// <returns>True on success, false if an error</returns>
        public bool CheckCDTAFile(string concatenatedDtaPath)
        {

            var dtLastStatusTime = DateTime.UtcNow;

            var splitChars = new[] { ' ' };

            try
            {
                if (!File.Exists(concatenatedDtaPath))
                {
                    throw new FileNotFoundException("CDTA file not found: " + concatenatedDtaPath);
                }

                // Read the m/z values in the _dta.txt file
                // Using a simple text reader here for speed purposes

                using (var srDtaFile = new StreamReader(new FileStream(concatenatedDtaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    var lstMZs = new List<double>(2000);
                    var lstPpmDiffs = new List<double>(2000);

                    double previousMz = 0;

                    while (!srDtaFile.EndOfStream)
                    {
                        var dataLine = srDtaFile.ReadLine();

                        if (string.IsNullOrEmpty(dataLine))
                            continue;

                        if (dataLine.StartsWith("============="))
                        {
                            // DTA header line

                            // Process the data for the previous spectrum
                            CheckPPMDiffs(lstMZs, lstPpmDiffs, 2, eCentroidStatusConstants.Unknown);

                            // Reset the previous m/z value and skip the next line (since it has parent ion m/z and charge)
                            if (!srDtaFile.EndOfStream)
                                srDtaFile.ReadLine();

                            previousMz = 0;
                            lstMZs.Clear();
                            lstPpmDiffs.Clear();

                            if (DateTime.UtcNow.Subtract(dtLastStatusTime).TotalSeconds >= 30)
                            {
                                ReadingSpectra?.Invoke(TotalSpectra);
                                dtLastStatusTime = DateTime.UtcNow;
                            }
                            continue;
                        }

                        var dataColumns = dataLine.Split(splitChars, 3);

                        if (!double.TryParse(dataColumns[0], out var mz))
                        {
                            continue;
                        }

                        lstMZs.Add(mz);

                        if (previousMz > 0 && mz > previousMz)
                        {
                            var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                            lstPpmDiffs.Add(delMassPPM);
                        }
                        previousMz = mz;
                    }

                    // Process the data for the previous spectrum
                    CheckPPMDiffs(lstMZs, lstPpmDiffs, 2, eCentroidStatusConstants.Unknown);

                }

                ProcessingComplete?.Invoke(TotalSpectra);

                return true;

            }
            catch (Exception ex)
            {
                mErrorMessage = "Exception in CheckCDTAFile: " + ex.Message;
                OnErrorEvent(mErrorMessage, ex);
                return false;
            }

        }

        /// <summary>
        /// Examines lstPpmDiffs to determine if the data is centroided data
        /// </summary>
        /// <param name="lstMZs"></param>
        /// <param name="lstPpmDiffs"></param>
        /// <param name="msLevel">1 for MS1, 2 for MS2, etc.</param>
        /// <param name="centroidingStatus">Expected centroid mode</param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments class property TotalSpectra if lstPpmDiffs is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        private void CheckPPMDiffs(
            ICollection<double> lstMZs,
            IList<double> lstPpmDiffs,
            int msLevel,
            eCentroidStatusConstants centroidingStatus,
            string spectrumTitle = "")
        {
            if (lstPpmDiffs.Count <= 0)
                return;

            IncrementDictionaryByMSLevel(mTotalSpectra, msLevel);

            if (RaiseDebugEvents)
            {
                if (string.IsNullOrWhiteSpace(spectrumTitle))
                    OnDebugEvent(string.Format("Examining MS{0} spectrum", msLevel));
                else
                    OnDebugEvent("Examining " + spectrumTitle);
            }

            var empiricalCentroidStatus = eCentroidStatusConstants.Profile;
            if (IsDataCentroided(lstPpmDiffs, spectrumTitle))
            {
                empiricalCentroidStatus = eCentroidStatusConstants.Centroid;
            }
            else
            {
                // Data appears profile
                // Examine the data by region to confirm that at least two thirds of the regions have profile mode data
                if (IsDataCentroidedInRegions(lstMZs, DEFAULT_REGION_COUNT, spectrumTitle))
                {
                    // When examining regions, the data appears centroided
                    empiricalCentroidStatus = eCentroidStatusConstants.Centroid;
                }
            }

            if (centroidingStatus == eCentroidStatusConstants.Centroid && empiricalCentroidStatus == eCentroidStatusConstants.Profile)
            {
                // The empirical algorithm has classified a centroid spectrum as profile
                // Change it back to centroid
                empiricalCentroidStatus = eCentroidStatusConstants.Centroid;

                IncrementDictionaryByMSLevel(mCentroidedSpectraClassifiedAsProfile, msLevel);
            }

            if (empiricalCentroidStatus == eCentroidStatusConstants.Centroid)
            {
                IncrementDictionaryByMSLevel(mCentroidedSpectra, msLevel);
            }
        }

        private void IncrementDictionaryByMSLevel(IDictionary<int, int> dctSpectrumCounts, int msLevel)
        {
            if (dctSpectrumCounts.TryGetValue(msLevel, out var spectraCount))
            {
                dctSpectrumCounts[msLevel] = spectraCount + 1;
            }
            else
            {
                dctSpectrumCounts[msLevel] = 1;
            }

        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="lstMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments class property TotalSpectra if lstMZs is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        public void CheckSpectrum(List<double> lstMZs, int msLevel, string spectrumTitle = "")
        {
            CheckSpectrum(lstMZs, msLevel, false, eCentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="lstMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments class property TotalSpectra if lstMZs is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        public void CheckSpectrum(List<double> lstMZs, int msLevel, eCentroidStatusConstants centroidingStatus, string spectrumTitle = "")
        {
            CheckSpectrum(lstMZs, msLevel, false, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="lstMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="assumeSorted"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments class property TotalSpectra if lstMZs is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        public void CheckSpectrum(
            List<double> lstMZs,
            int msLevel,
            bool assumeSorted,
            eCentroidStatusConstants centroidingStatus = eCentroidStatusConstants.Unknown,
            string spectrumTitle = "")
        {
            if (!assumeSorted)
            {
                // Check whether sorting is required
                for (var i = 1; i <= lstMZs.Count - 1; i++)
                {
                    if (lstMZs[i] < lstMZs[i - 1])
                    {
                        lstMZs.Sort();
                        break;
                    }
                }
            }

            var lstPpmDiffs = new List<double>(lstMZs.Count);

            for (var i = 1; i <= lstMZs.Count - 1; i++)
            {
                var mz = lstMZs[i];
                var previousMz = lstMZs[i - 1];

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    lstPpmDiffs.Add(delMassPPM);
                }
            }

            CheckPPMDiffs(lstMZs, lstPpmDiffs, msLevel, centroidingStatus, spectrumTitle);

        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="dblMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(double[] dblMZs, int msLevel, string spectrumTitle = "")
        {
            CheckSpectrum(dblMZs.Length, dblMZs, msLevel, eCentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="ionCount">Number of items in dblMZs; if -1, then parses all data in dblMZs</param>
        /// <param name="dblMZs"></param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(
            int ionCount,
            double[] dblMZs,
            int msLevel,
            eCentroidStatusConstants centroidingStatus = eCentroidStatusConstants.Unknown,
            string spectrumTitle = "")
        {
            if (ionCount < 0)
            {
                ionCount = dblMZs.Length;
            }

            // Possibly sort dblMZs
            for (var i = 1; i <= ionCount - 1; i++)
            {
                if (dblMZs[i] < dblMZs[i - 1])
                {
                    // Sort required
                    Array.Sort(dblMZs);
                    break;
                }
            }

            var lstPpmDiffs = new List<double>(ionCount);

            for (var i = 1; i <= ionCount - 1; i++)
            {
                var mz = dblMZs[i];
                var previousMz = dblMZs[i - 1];

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    lstPpmDiffs.Add(delMassPPM);
                }
            }

            CheckPPMDiffs(dblMZs.ToList(), lstPpmDiffs, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Computes the median of the ppm m/z difference values in lstPpmDiffs
        /// </summary>
        /// <param name="lstPpmDiffs">List of mass difference values between adjacent data points, converted to ppm</param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <returns>True if the median is at least as large as PpmDiffThreshold</returns>
        /// <remarks>Returns false if lstPpmDiffs has fewer than 4 data points (thus indicating sparse spectra)</remarks>
        public bool IsDataCentroided(IList<double> lstPpmDiffs, string spectrumTitle = "")
        {
            if (lstPpmDiffs.Count < 4)
                return true;

            var medianDelMassPPM = MathNet.Numerics.Statistics.Statistics.Median(lstPpmDiffs);
            bool centroided;
            string spectrumDescription;
            string comparison;

            if (medianDelMassPPM < PpmDiffThreshold)
            {
                // Profile mode data
                centroided = false;
                spectrumDescription = "Profile mode";
                comparison = "less than";
            }
            else
            {

                // Centroided data
                centroided = true;
                spectrumDescription = "Centroided";
                comparison = "greater than";
            }

            // Example messages:
            //  Profile mode spectrum, since 23.3 is less than 50 ppm
            //  Centroided spectrum, since 78.6 is greater than 50 ppm
            var msg = string.Format("  {0} spectrum, since {1:F1} is {2} {3} ppm", spectrumDescription, medianDelMassPPM, comparison, PpmDiffThreshold);

            NotifyDebug(spectrumTitle, msg);

            return centroided;

        }

        /// <summary>
        /// Divide the data into 5 regions, then call IsDataCentroided for the data in each region
        /// </summary>
        /// <param name="lstMZs">m/z values to examine</param>
        /// <param name="regionCount">Regions to divide data into</param>
        /// <param name="spectrumTitle">Regions to divide data into</param>
        /// <returns>
        /// True if less than one third of the regions appears to be profile mode
        /// False if two thirds of the regions is profile mode</returns>
        private bool IsDataCentroidedInRegions(ICollection<double> lstMZs, int regionCount, string spectrumTitle)
        {
            if (lstMZs.Count < 2)
                return false;

            if (regionCount < 1)
                regionCount = 1;

            var sortedMZs = lstMZs.Distinct().OrderBy(item => item).ToList();

            var minimumMz = sortedMZs.First();
            var maximumMz = sortedMZs.Last();

            var mzRange = (int)(Math.Ceiling(maximumMz) - Math.Floor(minimumMz));
            if (mzRange <= 0)
                return false;

            var mzBinSize = (int)Math.Ceiling(mzRange / (double)regionCount);
            if (mzBinSize < 1)
                mzBinSize = 1;

            var lstPpmDiffs = new List<double>(2000);
            double previousMz = 0;

            var startMz = minimumMz;
            var endMz = startMz + mzBinSize;

            var centroidedRegions = 0;
            var profileModeRegions = 0;

            foreach (var mz in lstMZs)
            {
                if (mz >= endMz)
                {
                    if (lstPpmDiffs.Count > 0)
                    {
                        if (IsDataCentroided(lstPpmDiffs))
                        {
                            centroidedRegions += 1;
                        }
                        else
                        {
                            profileModeRegions += 1;
                        }
                    }

                    lstPpmDiffs.Clear();
                    startMz += mzBinSize;
                    endMz = startMz + mzBinSize;
                    continue;
                }

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    lstPpmDiffs.Add(delMassPPM);
                }
                previousMz = mz;
            }

            if (lstPpmDiffs.Count > 0)
            {
                if (IsDataCentroided(lstPpmDiffs))
                {
                    centroidedRegions += 1;
                }
                else
                {
                    profileModeRegions += 1;
                }
            }

            var regionsWithData = centroidedRegions + profileModeRegions;
            if (regionsWithData <= 0)
            {
                // Very sparse spectrum; treat as centroid
                return true;
            }

            var fractionProfile = profileModeRegions / (double)(regionsWithData);

            // If less than two thirds of the spectra appear to be profile mode, assume Centroided data
            var centroided = fractionProfile < FRACTION_REGIONS_PROFILE;

            string msg;

            if (centroided)
                msg = string.Format("  Data originally appeared to be profile mode, but {0} / {1} regions appear centroided",
                    centroidedRegions, regionsWithData);
            else
                msg = string.Format("  {0} / {1} regions have profile mode data", profileModeRegions, regionsWithData);

            NotifyDebug(spectrumTitle, msg);

            return centroided;
        }

        /// <summary>
        /// Send a message via a debug event, optionally prefixing with a title
        /// </summary>
        /// <param name="spectrumTitle"></param>
        /// <param name="msg"></param>
        private void NotifyDebug(string spectrumTitle, string msg)
        {
            if (!RaiseDebugEvents)
                return;

            if (string.IsNullOrWhiteSpace(spectrumTitle))
                OnDebugEvent(msg);
            else
                OnDebugEvent(spectrumTitle + ": " + msg);
        }

        /// <summary>
        /// Clear cached data
        /// </summary>
        public void Reset()
        {
            mErrorMessage = string.Empty;
            mTotalSpectra.Clear();
            mCentroidedSpectra.Clear();
            mCentroidedSpectraClassifiedAsProfile.Clear();
        }

    }
}
