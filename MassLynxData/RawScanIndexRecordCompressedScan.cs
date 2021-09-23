
namespace MSFileInfoScanner.MassLynxData
{
    internal class RawScanIndexRecordCompressedScan
    {
        /// <summary>
        /// Start Scan Offset
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int StartScanOffset { get; set; }

        /// <summary>
        /// Packed scan info
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int PackedScanInfo { get; set; }

        /// <summary>
        /// TIC value
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float TicValue { get; set; }

        /// <summary>
        /// Scan time, in minutes
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public float ScanTime { get; set; }

        // The remaining 6 bytes of the record contain a duplicate of the scan's base peak,
        //   with the information stored in packed form:

        // The method used to pack the data depends on the Acquisition data type
        // Note that the data type ID is stored in packed form in RawFunctionDescriptorRecord.PackedFunctionInfo

        // After unpacking, it is stored in .FunctionInfo().AcquisitionDataType

        // The UnpackIntensity and UnpackMass functions
        //  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo

        // For Acquisition Data Type ID 0 (Compressed scan)
        //   bits 0-2: intensity scale
        //   bits 3-10: intensity
        //   bits 11-31: mass * 128
        //   bits 32-47: spare

        /// <summary>
        /// Packed base peak info
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int PackedBasePeakInfo { get; set; }

        /// <summary>
        /// Unused
        /// </summary>
        /// <remarks>2 bytes</remarks>
        public short Spare { get; set; }
    }
}
