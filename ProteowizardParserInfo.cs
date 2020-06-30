
namespace MSFileInfoScanner
{
    public class ProteoWizardParserInfo
    {
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

        public int SkippedEmptyScans { get; set; }
        public int ScansStored { get; set; }
        public int TicAndBpiScansStored { get; set; }

        public int ScanCountHMS { get; set; }
        public int ScanCountHMSn { get; set; }
        public int ScanCountMS { get; set; }
        public int ScanCountMSn { get; set; }

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
        }
    }
}
