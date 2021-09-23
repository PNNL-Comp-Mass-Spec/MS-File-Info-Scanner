
namespace MSFileInfoScanner.MassLynxData
{
    /// <summary>
    /// The RawScanIndexRecord data read from the file is stored in this class
    /// </summary>
    internal class ScanIndexRecord
    {
        /// <summary>
        /// Start scan offset, in bytes
        /// </summary>
        /// <remarks>Offset from start of file where scan begins</remarks>
        public int StartScanOffset { get; set; }

        public int NumSpectralPeaks { get; set; }

        public short SegmentNumber { get; set; }

        public bool UseFollowingContinuum { get; set; }

        public bool ContinuumDataOverride { get; set; }

        public bool ScanContainsMolecularMasses { get; set; }

        public bool ScanContainsCalibratedMasses { get; set; }

        public bool ScanOverload { get; set; }

        public int BasePeakIntensity { get; set; }

        public float BasePeakMass { get; set; }

        /// <summary>
        /// TIC value (counts)
        /// </summary>
        public float TicValue { get; set; }

        /// <summary>
        /// Scan time (minutes)
        /// </summary>
        public float ScanTime { get; set; }

        public float LoMass { get; set; }

        public float HiMass { get; set; }

        public float SetMass { get; set; }
    }
}
