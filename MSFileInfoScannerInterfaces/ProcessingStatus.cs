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
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; }

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
            ErrorMessage = string.Empty;
            ProgressMessage = string.Empty;
            ProgressPercentComplete = 0;
        }
    }
}
