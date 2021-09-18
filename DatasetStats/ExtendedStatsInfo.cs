
namespace MSFileInfoScanner.DatasetStats
{
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
        /// Collision mode
        /// </summary>
        public string CollisionMode { get; set; }

        /// <summary>
        /// Scan filter text
        /// </summary>
        public string ScanFilterText { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ExtendedStatsInfo()
        {
            Clear();
        }

        public void Clear()
        {
            IonInjectionTime = string.Empty;
            ScanSegment = string.Empty;
            ScanEvent = string.Empty;
            ChargeState = string.Empty;
            MonoisotopicMZ = string.Empty;
            CollisionMode = string.Empty;
            ScanFilterText = string.Empty;
        }
    }
}
