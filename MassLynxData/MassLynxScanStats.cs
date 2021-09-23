
namespace MSFileInfoScanner.MassLynxData
{
    internal class MassLynxScanStats
    {
        /// <summary>
        /// Number of peaks in this scan
        /// </summary>
        public int PeakCount { get; set; }

        /// <summary>
        /// True if calibrated
        /// </summary>
        public bool Calibrated { get; set; }

        /// <summary>
        /// True if continuum (aka profile)
        /// </summary>
        public bool Continuum { get; set; }

        /// <summary>
        /// True if overload
        /// </summary>
        public bool Overload { get; set; }

        /// <summary>
        /// Starting mass (m/z)
        /// </summary>
        public float MassStart { get; set; }

        /// <summary>
        /// Ending mass (m/z)
        /// </summary>
        public float MassEnd { get; set; }

        // MS/MS Parent Ion Mass
        public float SetMass { get; set; }

        // Base peak intensity
        public float BPI { get; set; }

        /// <summary>
        /// Base peak mass
        /// </summary>
        public float BPIMass { get; set; }

        /// <summary>
        /// Total ion chromatogram (total intensity)
        /// </summary>
        public float TIC { get; set; }

        /// <summary>
        /// Elution time (retention time)
        /// </summary>
        public float RetentionTime { get; set; }
    }
}
