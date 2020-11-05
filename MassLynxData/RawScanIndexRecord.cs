using System;

namespace MSFileInfoScanner.MassLynxData
{
    /// <summary>
    /// Used when reading the _func001.idx file
    /// </summary>
    class RawScanIndexRecord
    {
        private readonly RawDataUtils mRawDataUtils;

        /// <summary>
        /// Total record size, in bytes
        /// </summary>
        public const short RAW_SCAN_INDEX_RECORD_SIZE = 22;

        #region "Properties"

        /// <summary>
        /// Start scan offset
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int StartScanOffset { get; set; }

        // The next 4 bytes are stored as a 32-bit integer, but are in fact
        //   seven different numbers packed into one integer:
        //   bits 0-21: number of spectral peaks in scan
        //   bits 22-26: segment number (MTOF function)
        //   bit 27: use following continuum data flag
        //   bit 28: continuum data override flag
        //   bit 29: scan contains molecular masses
        //   bit 30: scan contains calibrated masses
        //   bit 31: scan overload flag
        //
        /// <summary>
        /// Packed scan info
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int PackedScanInfo { get; set; }

        /// <summary>
        /// TIC Value
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
        // Note that the data type ID is stored in packed form in rawFunctionDescriptorRecord.PackedFunctionInfo

        // After unpacking, it is stored in .FunctionInfo().AcquisitionDataType

        // The UnpackIntensity and UnpackMass functions
        //  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo

        // For Acquisition Data Type ID 0 (Compressed scan)
        // use RawScanIndexRecordCompressedScan instead of RawScanIndexRecord

        // For Acquisition Data Type ID 1 (Standard scan)
        //   bits 0-2: intensity scale
        //   bits 3-15: intensity
        //   bits 16-39: mass * 1024
        //   bits 40-47: spare

        // For Acquisition Data Type ID 2 through 7 (Uncalibrated data)
        //   bits 0-2: intensity scale
        //   bits 3-15: intensity
        //   bits 16-43: channel number
        //   bits 44-47: spare

        // For Acquisition Data Type ID 8 (High intensity calibrated data)
        //   bits 0-15: intensity
        //   bits 16-19: intensity scale
        //   bits 20-47: mass * 128

        // For Acquisition Data Type ID 9, 11, and 12 (High accuracy calibrated, enhanced uncalibrated, and enhanced calibrated)

        // ReSharper disable once CommentTypo

        // Note that this is the form for the LCT and various Q-TOF's
        //   bits 0-15: intensity
        //   bits 16-19: intensity scale
        //   bits 20-24: mass exponent
        //   bits 25-47: mass mantissa

        /// <summary>
        /// Packed base peak intensity
        /// </summary>
        /// <remarks>2 bytes</remarks>
        public short PackedBasePeakIntensity { get; set; }

        /// <summary>
        /// Packed base peak info
        /// </summary>
        /// <remarks>4 bytes</remarks>
        public int PackedBasePeakInfo { get; set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rawDataUtils"></param>
        public RawScanIndexRecord(RawDataUtils rawDataUtils)
        {
            mRawDataUtils = rawDataUtils;
        }
    }
}
