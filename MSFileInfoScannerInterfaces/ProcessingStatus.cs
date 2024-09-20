using System;

namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// MS File Info Scanner processing status
    /// </summary>
    public class ProcessingStatus
    {
        /// <summary>
        /// Error code
        /// </summary>
        public iMSFileInfoScanner.MSFileScannerErrorCodes ErrorCode { get; set; }

        /// <summary>
        /// Number of times an error message starting with "Unable to load data for scan" was raised by ThermoRawFileReader
        /// </summary>
        public int ErrorCountLoadDataForScan { get; set; }

        /// <summary>
        /// Number of times an error message starting with "Unknown format for Scan Filter" was raised by ThermoRawFileReader
        /// </summary>
        public int ErrorCountUnknownScanFilterFormat { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Processing status date/time
        /// </summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// MS2MzMin validation error or warning message
        /// </summary>
        public string MS2MzMinValidationMessage { get; set; }

        /// <summary>
        /// Progress message
        /// </summary>
        public string ProgressMessage { get; set; }

        /// <summary>
        /// Percent complete (value between 0 and 100)
        /// </summary>
        public float ProgressPercentComplete { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessingStatus()
        {
            Reset();
        }

        /// <summary>
        /// Reset stored values
        /// </summary>
        public void Reset()
        {
            ErrorCode = iMSFileInfoScanner.MSFileScannerErrorCodes.NoError;
            ErrorCountLoadDataForScan = 0;
            ErrorCountUnknownScanFilterFormat = 0;
            ErrorMessage = string.Empty;
            LastUpdate = DateTime.MinValue;
            MS2MzMinValidationMessage = string.Empty;
            ProgressMessage = string.Empty;
            ProgressPercentComplete = 0;
        }
    }
}
