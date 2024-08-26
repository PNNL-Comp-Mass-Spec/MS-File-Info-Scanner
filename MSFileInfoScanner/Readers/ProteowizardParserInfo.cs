
namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// ProteoWizard parser info
    /// </summary>
    public class ProteoWizardParserInfo
    {
        // Ignore Spelling: Proteo

        /// <summary>
        /// Minimum scan index without scan times
        /// </summary>
        public int MinScanIndexWithoutScanTimes { get; set; }

        /// <summary>
        /// The calling method should set this to true if the TIC was already stored
        /// </summary>
        public bool TicStored { get; set; }

        /// <summary>
        /// Maximum acquisition time
        /// </summary>
        public double RuntimeMinutes { get; set; }

        /// <summary>
        /// When true, skip scans already defined in mDatasetStatsSummarizer
        /// </summary>
        public bool SkipExistingScans { get; set; }

        /// <summary>
        /// When true, skip scans that have no ions
        /// </summary>
        public bool SkipScansWithNoIons { get; set; }

        /// <summary>
        /// Maximum number of scans to store in mDatasetStatsSummarizer; limit to 1 million to reduce memory usage.
        /// If less than zero, store all scans
        /// </summary>
        public int MaxScansToTrackInDetail { get; set; }

        /// <summary>
        /// Maximum number of scans to store in mTICAndBPIPlot; limit to 2 million to reduce memory usage.
        /// If less than zero, store all scans
        /// </summary>
        public int MaxScansForTicAndBpi { get; set; }

        /// <summary>
        /// Number of scans successfully read
        /// </summary>
        public int ScanCountSuccess { get; set; }

        /// <summary>
        /// Number of scans that could not be read
        /// </summary>
        public int ScanCountError { get; set; }

        /// <summary>
        /// Number of skipped empty scans
        /// </summary>
        public int SkippedEmptyScans { get; set; }

        /// <summary>
        /// Number of stored spectra
        /// </summary>
        public int ScansStored { get; set; }

        /// <summary>
        /// Number of TIC and BPI scans stored
        /// </summary>
        public int TicAndBpiScansStored { get; set; }

        /// <summary>
        /// High-resolution MS scan count
        /// </summary>
        public int ScanCountHMS { get; set; }

        /// <summary>
        /// High-resolution MSn scan count
        /// </summary>
        public int ScanCountHMSn { get; set; }

        /// <summary>
        /// Low-resolution MS scan count
        /// </summary>
        public int ScanCountMS { get; set; }

        /// <summary>
        /// Low-resolution MSn scan count
        /// </summary>
        public int ScanCountMSn { get; set; }

        /// <summary>
        /// DIA scan count
        /// </summary>
        public int ScanCountDIA { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="runtimeMinutes">
        /// Maximum acquisition time, based on scans already read
        /// It will get updated if a new maximum time is found
        /// </param>
        public ProteoWizardParserInfo(double runtimeMinutes)
        {
            RuntimeMinutes = runtimeMinutes;
        }

        /// <summary>
        /// Reset scan counts
        /// </summary>
        public void ResetCounts()
        {
           ScanCountSuccess = 0;
           ScanCountError = 0;

           SkippedEmptyScans = 0;
           ScansStored = 0;
           TicAndBpiScansStored = 0;

           ScanCountHMS = 0;
           ScanCountHMSn = 0;
           ScanCountMS = 0;
           ScanCountMSn = 0;
           ScanCountDIA = 0;
        }
    }
}
