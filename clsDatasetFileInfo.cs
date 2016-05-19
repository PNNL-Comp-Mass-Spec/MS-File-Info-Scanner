using System;

namespace MSFileInfoScanner
{
    public class clsDatasetFileInfo
    {
        public DateTime FileSystemCreationTime { get; set; }
        public DateTime FileSystemModificationTime { get; set; }
        public int DatasetID { get; set; }
        public string DatasetName { get; set; }
        public string FileExtension { get; set; }
        public DateTime AcqTimeStart { get; set; }
        public DateTime AcqTimeEnd { get; set; }
        public int ScanCount { get; set; }
        public long FileSizeBytes { get; set; }
        public float OverallQualityScore { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetFileInfo()
        {
            Clear();
        }

        public clsDatasetFileInfo(int datasetId, string datasetName)
        {
            Clear();
            DatasetID = datasetId;
            DatasetName = datasetName;
        }

        public void Clear()
        {
            FileSystemCreationTime = DateTime.MinValue;
            FileSystemModificationTime = DateTime.MinValue;
            DatasetID = 0;
            DatasetName = string.Empty;
            FileExtension = string.Empty;
            AcqTimeStart = DateTime.MinValue;
            AcqTimeEnd  = DateTime.MinValue;
            ScanCount = 0;
            FileSizeBytes = 0;
            OverallQualityScore = 0;
        }
    }
}
