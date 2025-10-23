namespace MSFileInfoScanner.DatasetStats
{
    internal class MSnMzMinStats
    {
        /// <summary>
        /// Error or warning message
        /// </summary>
        public string ErrorOrWarningMessage { get; set; } = string.Empty;

        /// <summary>
        /// Maximum percentage of spectra allowed to have a minimum m/z larger than the required minimum (value between 0 and 100)
        /// </summary>
        private int MaxPercentAllowedFailed { get; }

        /// <summary>
        /// Percent of spectra with a minimum m/z value larger than <see cref="RequiredMzMin">RequiredMzMin</see>
        /// </summary>
        /// <remarks>Value between 0 and 100</remarks>
        public float PercentInvalid { get; private set; }

        /// <summary>
        /// True if a sufficient number of spectra have a minimum m/z value below the required minimum, false if too many do not have the required minimum
        /// </summary>
        public bool PercentInvalidPassesFilter { get; private set; }

        /// <summary>
        /// Required minimum m/z value
        /// </summary>
        public float RequiredMzMin { get; }

        /// <summary>
        /// Scan filter text (for Thermo files, generic scan filter text, created using XRawFileIO.MakeGenericThermoScanFilter)
        /// </summary>
        public string ScanFilter { get; }

        /// <summary>
        /// Number of spectra with a minimum m/z value larger than <see cref="RequiredMzMin">RequiredMzMin</see>
        /// </summary>
        public int ScanCountInvalid { get; set; }

        /// <summary>
        /// Scan count with data
        /// </summary>
        public int ScanCountWithData { get; set; }

        /// <summary>
        /// Percent invalid, as a string, rounded to the nearest integer if 10 or larger, or as one digit after the decimal if less than 10
        /// </summary>
        public string PercentInvalidRounded => PercentInvalid.ToString(PercentInvalid < 10 ? "F1" : "F0");

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scanType">
        /// Scan filter text (for Thermo files, generic scan filter text, created using XRawFileIO.MakeGenericThermoScanFilter)
        /// </param>
        /// <param name="maxPercentAllowedFailed">
        /// Maximum percentage of spectra allowed to have a minimum m/z larger than the required minimum (value between 0 and 100)
        /// </param>
        /// <param name="requiredMzMin">Required minimum m/z value</param>
        public MSnMzMinStats(string scanType, int maxPercentAllowedFailed, float requiredMzMin)
        {
            MaxPercentAllowedFailed = maxPercentAllowedFailed;
            RequiredMzMin = requiredMzMin;
            ScanFilter = scanType;
        }

        /// <summary>
        /// Compute the values for <see cref="PercentInvalid">PercentInvalid</see> and <see cref="PercentInvalidPassesFilter">PercentInvalidPassesFilter</see>
        /// </summary>
        public void ComputePercentInvalid()
        {
            PercentInvalid = ScanCountInvalid / (float)ScanCountWithData * 100;
            PercentInvalidPassesFilter = PercentInvalid <= MaxPercentAllowedFailed;
        }

        /// <summary>
        /// Show the percentage of spectra with a minimum m/z value larger than <see cref="RequiredMzMin">RequiredMzMin</see>
        /// </summary>
        public override string ToString()
        {
            if (ScanCountInvalid > 0 && PercentInvalid < 0.000001)
            {
                ComputePercentInvalid();
            }

            return string.Format("{0}% of the MSn spectra have a minimum m/z value larger than {1:F1} m/z ({2:N0} / {3:N0})",
                PercentInvalidRounded, RequiredMzMin, ScanCountInvalid, ScanCountWithData);
        }
    }
}
