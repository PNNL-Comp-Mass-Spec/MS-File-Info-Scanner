
namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// Extended stats info
    /// </summary>
    public class ExtendedStatsInfo
    {
        // Ignore Spelling: Orbitrap

        /// <summary>
        /// Ion injection time
        /// </summary>
        public string IonInjectionTime { get; set; }

        /// <summary>
        /// Scan segment
        /// </summary>
        public string ScanSegment { get; set; }

        /// <summary>
        /// Scan event
        /// </summary>
        /// <remarks>
        /// Only defined for LTQ-Orbitrap datasets and only for fragmentation spectra where the instrument could determine the charge and m/z
        /// </remarks>
        public string ScanEvent { get; set; }

        /// <summary>
        /// Charge state
        /// </summary>
        /// <remarks>
        /// Only defined for LTQ-Orbitrap datasets and only for fragmentation spectra where the instrument could determine the charge and m/z
        /// </remarks>
        public string ChargeState { get; set; }

        /// <summary>
        /// Monoisotopic m/z
        /// </summary>
        public string MonoisotopicMZ { get; set; }

        /// <summary>
        /// The window size, in m/z, of ions selected for MS/MS fragmentation
        /// </summary>
        public string IsolationWindowWidthMZ { get; set; }

        /// <summary>
        /// Collision mode
        /// </summary>
        public string CollisionMode { get; set; }

        /// <summary>
        /// Generic scan filter text
        /// </summary>
        public string ScanFilterText { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ExtendedStatsInfo()
        {
            Clear();
        }

        /// <summary>
        /// Reset cached data to empty strings
        /// </summary>
        public void Clear()
        {
            IonInjectionTime = string.Empty;
            ScanSegment = string.Empty;
            ScanEvent = string.Empty;
            ChargeState = string.Empty;
            MonoisotopicMZ = string.Empty;
            IsolationWindowWidthMZ = string.Empty;
            CollisionMode = string.Empty;
            ScanFilterText = string.Empty;
        }
    }
}
