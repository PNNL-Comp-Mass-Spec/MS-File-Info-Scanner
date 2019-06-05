using System;
using System.IO;
using MSFileInfoScanner.DatasetStats;

namespace MSFileInfoScanner
{
    class GenericFileInfoScanner : clsMSFileInfoProcessorBaseClass
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public GenericFileInfoScanner()
        {
            HideEmptyHtmlSections = true;
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            try
            {
                // The dataset name is simply the file name without the extension
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error or if the file has no scans</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            // Obtain the full path to the file
            var instrumentDataFile = new FileInfo(dataFilePath);

            if (!instrumentDataFile.Exists)
            {
                OnErrorEvent("Instrument file not found: " + dataFilePath);
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // datasetID = LookupDatasetID(datasetName)
            var datasetID = DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = instrumentDataFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = instrumentDataFile.LastWriteTime;

            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = datasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(instrumentDataFile.Name);
            datasetFileInfo.FileExtension = instrumentDataFile.Extension;
            datasetFileInfo.FileSizeBytes = instrumentDataFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            // Read the file info from the file system
            // This will also compute the SHA-1 hash of the .Raw file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(instrumentDataFile, datasetID);

            // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

            PostProcessTasks();

            return true;

        }

    }
}
