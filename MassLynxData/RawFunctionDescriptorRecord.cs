
namespace MSFileInfoScanner.MassLynxData
{
    /// <summary>
    /// Used when reading the _functns.inf file
    /// </summary>
    internal class RawFunctionDescriptorRecord
    {
        // Ignore Spelling: Dly

        /// <summary>
        /// Total record size, in bytes
        /// </summary>
        public const int NATIVE_FUNCTION_INFO_SIZE_BYTES = 416;

        /// <summary>
        /// Packed function info
        ///   bits 0-4: Function type (typically 2=Dly)
        ///   bits 5-9: Ion mode (typically 8=ES+)
        ///   bits 10-13: Acquisition data type (typically 9=high accuracy calibrated data)
        ///   bits 14-15: spare
        /// </summary>
        /// <remarks>2 bytes</remarks>
        public short PackedFunctionInfo { get; set; }

        /// <summary>
        /// Cycle time, in seconds
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float CycleTime { get; set; }

        /// <summary>
        /// Inter scan delay, in seconds
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float InterScanDelay { get; set; }

        /// <summary>
        /// Start elution time, in minutes
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float StartRT { get; set; }

        /// <summary>
        /// End elution time, in minutes
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float EndRT { get; set; }

        /// <summary>
        /// Scan count, but it is always 0 and thus we cannot trust it
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int ScanCount { get; set; }

        /// <summary>
        /// Packed MS/MS Info:
        ///   bits 0-7: collision energy
        ///   bits 8-15: segment/channel count
        /// </summary>
        /// <remarks>2 bytes</remarks>
        public short PackedMSMSInfo { get; set; }

        // The following are more MS/MS parameters

        /// <summary>
        /// Function set mass (parent ion mass)
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float FunctionSetMass { get; set; }

        /// <summary>
        /// Integer segment channel time, in seconds
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float InterSegmentChannelTime { get; set; }

        // Up to 32 segment scans can be conducted for a MS/MS run
        // The following three arrays store the segment times, start, and end masses

        /// <summary>
        /// Segment scan times
        /// </summary>
        /// <remarks>
        /// Ranges from 0 to 31 giving a 128 byte array
        /// </remarks>
        public int[] SegmentScanTimes { get; set; }

        /// <summary>
        /// Segment start masses
        /// </summary>
        /// <remarks>
        /// Ranges from 0 to 31 giving a 128 byte array
        /// </remarks>
        public int[] SegmentStartMasses { get; set; }

        /// <summary>
        /// Segment end masses
        /// </summary>
        /// <remarks>
        /// Ranges from 0 to 31 giving a 128 byte array
        /// </remarks>
        public int[] SegmentEndMasses { get; set; }
    }
}
