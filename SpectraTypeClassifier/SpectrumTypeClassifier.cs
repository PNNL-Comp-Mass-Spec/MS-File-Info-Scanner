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
        // Ignore Spelling: centroided, ppmDiffs, PpmDiffThreshold

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

        public enum CentroidStatusConstants
        {
            Unknown = 0,
            Profile = 1,
            Centroid = 2
        }

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

        public event ReadingSpectraEventHandler ReadingSpectra;
        public delegate void ReadingSpectraEventHandler(int spectraProcessed);

        public event ProcessingCompleteEventHandler ProcessingComplete;
        public delegate void ProcessingCompleteEventHandler(int spectraProcessed);

        /// <summary>
        /// Number of centroided spectra
        /// </summary>
        public int CentroidedSpectra =>
            mCentroidedSpectra.Sum(item => item.Value);

        /// <summary>
        /// Number of centroided MS1 spectra
        /// </summary>
        public int CentroidedMS1Spectra => (from item in mCentroidedSpectra where item.Key <= 1 select item.Value).Sum();

        /// <summary>
        /// Number of centroided MS2 spectra
        /// </summary>
        public int CentroidedMSnSpectra =>
            (from item in mCentroidedSpectra where item.Key > 1 select item.Value).Sum();

        /// <summary>
        /// Number of MS1 spectra that empirically appears profile, but the calling class says is centroid
        /// </summary>
        public int CentroidedMS1SpectraClassifiedAsProfile =>
            (from item in mCentroidedSpectraClassifiedAsProfile where item.Key <= 1 select item.Value).Sum();

        /// <summary>
        /// Number of MSn spectra that empirically appears profile, but the calling class says is centroid
        /// </summary>
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
        public int TotalSpectra => mTotalSpectra.Sum(item => item.Value);

        /// <summary>
        /// Number of MS1 spectra analyzed
        /// </summary>
        public int TotalMS1Spectra => mTotalSpectra.Where(item => item.Key <= 1).Sum(item => item.Value);

        /// <summary>
        /// Number of MSn spectra analyzed
        /// </summary>
        public int TotalMSnSpectra => mTotalSpectra.Where(item => item.Key > 1).Sum(item => item.Value);

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

                using (var dtaFileReader = new StreamReader(new FileStream(concatenatedDtaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    var mzValues = new List<double>(2000);
                    var ppmDiffs = new List<double>(2000);

                    double previousMz = 0;

                    while (!dtaFileReader.EndOfStream)
                    {
                        var dataLine = dtaFileReader.ReadLine();

                        if (string.IsNullOrEmpty(dataLine))
                            continue;

                        if (dataLine.StartsWith("============="))
                        {
                            // DTA header line

                            // Process the data for the previous spectrum
                            CheckPPMDiffs(mzValues, ppmDiffs, 2, CentroidStatusConstants.Unknown);

                            // Reset the previous m/z value and skip the next line (since it has parent ion m/z and charge)
                            if (!dtaFileReader.EndOfStream)
                            {
                                dtaFileReader.ReadLine();
                            }

                            previousMz = 0;
                            mzValues.Clear();
                            ppmDiffs.Clear();

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

                        mzValues.Add(mz);

                        if (previousMz > 0 && mz > previousMz)
                        {
                            var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                            ppmDiffs.Add(delMassPPM);
                        }
                        previousMz = mz;
                    }

                    // Process the data for the previous spectrum
                    CheckPPMDiffs(mzValues, ppmDiffs, 2, CentroidStatusConstants.Unknown);
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
        /// Examines ppmDiffs to determine if the data is centroided data
        /// </summary>
        /// <remarks>
        /// Increments class property TotalSpectra if ppmDiffs is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="mzValues"></param>
        /// <param name="ppmDiffs"></param>
        /// <param name="msLevel">1 for MS1, 2 for MS2, etc.</param>
        /// <param name="centroidingStatus">Expected centroid mode</param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        private void CheckPPMDiffs(
            ICollection<double> mzValues,
            IList<double> ppmDiffs,
            int msLevel,
            CentroidStatusConstants centroidingStatus,
            string spectrumTitle = "")
        {
            if (ppmDiffs.Count == 0)
                return;

            IncrementDictionaryByMSLevel(mTotalSpectra, msLevel);

            if (RaiseDebugEvents)
            {
                if (string.IsNullOrWhiteSpace(spectrumTitle))
                    OnDebugEvent(string.Format("Examining MS{0} spectrum", msLevel));
                else
                    OnDebugEvent("Examining " + spectrumTitle);
            }

            var empiricalCentroidStatus = CentroidStatusConstants.Profile;

            if (IsDataCentroided(ppmDiffs, spectrumTitle))
            {
                empiricalCentroidStatus = CentroidStatusConstants.Centroid;
            }
            else
            {
                // Data appears profile
                // Examine the data by region to confirm that at least two thirds of the regions have profile mode data
                if (IsDataCentroidedInRegions(mzValues, DEFAULT_REGION_COUNT, spectrumTitle))
                {
                    // When examining regions, the data appears centroided
                    empiricalCentroidStatus = CentroidStatusConstants.Centroid;
                }
            }

            if (centroidingStatus == CentroidStatusConstants.Centroid && empiricalCentroidStatus == CentroidStatusConstants.Profile)
            {
                // The empirical algorithm has classified a centroid spectrum as profile
                // Change it back to centroid
                empiricalCentroidStatus = CentroidStatusConstants.Centroid;

                IncrementDictionaryByMSLevel(mCentroidedSpectraClassifiedAsProfile, msLevel);
            }

            if (empiricalCentroidStatus == CentroidStatusConstants.Centroid)
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
        /// Step through the MZ values in array mzValues and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <remarks>
        /// Increments class property TotalSpectra if mzValues is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="mzValues"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(List<double> mzValues, int msLevel, string spectrumTitle = "")
        {
            CheckSpectrum(mzValues, msLevel, false, CentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array mzValues and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <remarks>
        /// Increments class property TotalSpectra if mzValues is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="mzValues"></param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(List<double> mzValues, int msLevel, CentroidStatusConstants centroidingStatus, string spectrumTitle = "")
        {
            CheckSpectrum(mzValues, msLevel, false, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array mzValues and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <remarks>
        /// Increments class property TotalSpectra if mzValues is not empty
        /// Increments class property CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="mzValues"></param>
        /// <param name="msLevel"></param>
        /// <param name="assumeSorted"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(
            List<double> mzValues,
            int msLevel,
            bool assumeSorted,
            CentroidStatusConstants centroidingStatus = CentroidStatusConstants.Unknown,
            string spectrumTitle = "")
        {
            if (!assumeSorted)
            {
                // Check whether sorting is required
                for (var i = 1; i < mzValues.Count; i++)
                {
                    if (mzValues[i] < mzValues[i - 1])
                    {
                        mzValues.Sort();
                        break;
                    }
                }
            }

            var ppmDiffs = new List<double>(mzValues.Count);

            for (var i = 1; i < mzValues.Count; i++)
            {
                var mz = mzValues[i];
                var previousMz = mzValues[i - 1];

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    ppmDiffs.Add(delMassPPM);
                }
            }

            CheckPPMDiffs(mzValues, ppmDiffs, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array mzValues and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="mzValues"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(double[] mzValues, int msLevel, string spectrumTitle = "")
        {
            CheckSpectrum(mzValues.Length, mzValues, msLevel, CentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Step through the MZ values in array mzValues and compute the ppm-based mass difference between adjacent points
        /// </summary>
        /// <param name="ionCount">Number of items in mzValues; if -1, then parses all data in mzValues</param>
        /// <param name="mzValues"></param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void CheckSpectrum(
            int ionCount,
            double[] mzValues,
            int msLevel,
            CentroidStatusConstants centroidingStatus = CentroidStatusConstants.Unknown,
            string spectrumTitle = "")
        {
            if (ionCount < 0)
            {
                ionCount = mzValues.Length;
            }

            // Possibly sort mzValues
            for (var i = 1; i < ionCount; i++)
            {
                if (mzValues[i] < mzValues[i - 1])
                {
                    // Sort required
                    Array.Sort(mzValues);
                    break;
                }
            }

            var ppmDiffs = new List<double>(ionCount);

            for (var i = 1; i < ionCount; i++)
            {
                var mz = mzValues[i];
                var previousMz = mzValues[i - 1];

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    ppmDiffs.Add(delMassPPM);
                }
            }

            CheckPPMDiffs(mzValues.ToList(), ppmDiffs, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Computes the median of the ppm m/z difference values in ppmDiffs
        /// </summary>
        /// <remarks>Returns false if ppmDiffs has fewer than 4 data points (thus indicating sparse spectra)</remarks>
        /// <param name="ppmDiffs">List of mass difference values between adjacent data points, converted to ppm</param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <returns>True if the median is at least as large as PpmDiffThreshold</returns>
        public bool IsDataCentroided(IList<double> ppmDiffs, string spectrumTitle = "")
        {
            if (ppmDiffs.Count < 4)
                return true;

            var medianDelMassPPM = MathNet.Numerics.Statistics.Statistics.Median(ppmDiffs);
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
        /// <param name="mzValues">m/z values to examine</param>
        /// <param name="regionCount">Regions to divide data into</param>
        /// <param name="spectrumTitle">Regions to divide data into</param>
        /// <returns>
        /// True if less than one third of the regions appears to be profile mode
        /// False if two thirds of the regions is profile mode</returns>
        private bool IsDataCentroidedInRegions(ICollection<double> mzValues, int regionCount, string spectrumTitle)
        {
            if (mzValues.Count < 2)
                return false;

            if (regionCount < 1)
                regionCount = 1;

            var sortedMZs = mzValues.Distinct().OrderBy(item => item).ToList();

            var minimumMz = sortedMZs.First();
            var maximumMz = sortedMZs.Last();

            var mzRange = (int)(Math.Ceiling(maximumMz) - Math.Floor(minimumMz));

            if (mzRange <= 0)
                return false;

            var mzBinSize = (int)Math.Ceiling(mzRange / (double)regionCount);

            if (mzBinSize < 1)
                mzBinSize = 1;

            var ppmDiffs = new List<double>(2000);
            double previousMz = 0;

            var startMz = minimumMz;
            var endMz = startMz + mzBinSize;

            var centroidedRegions = 0;
            var profileModeRegions = 0;

            foreach (var mz in mzValues)
            {
                if (mz >= endMz)
                {
                    if (ppmDiffs.Count > 0)
                    {
                        if (IsDataCentroided(ppmDiffs))
                        {
                            centroidedRegions++;
                        }
                        else
                        {
                            profileModeRegions++;
                        }
                    }

                    ppmDiffs.Clear();
                    startMz += mzBinSize;
                    endMz = startMz + mzBinSize;
                    continue;
                }

                if (previousMz > 0 && mz > previousMz)
                {
                    var delMassPPM = 1000000.0 * (mz - previousMz) / mz;
                    ppmDiffs.Add(delMassPPM);
                }
                previousMz = mz;
            }

            if (ppmDiffs.Count > 0)
            {
                if (IsDataCentroided(ppmDiffs))
                {
                    centroidedRegions++;
                }
                else
                {
                    profileModeRegions++;
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
            {
                msg = string.Format("  Data originally appeared to be profile mode, but {0} / {1} regions appear centroided",
                    centroidedRegions, regionsWithData);
            }
            else
            {
                msg = string.Format("  {0} / {1} regions have profile mode data", profileModeRegions, regionsWithData);
            }

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
