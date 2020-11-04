using System;
using System.IO;
using MSFileInfoScanner.DatasetStats;

namespace MSFileInfoScanner
{
    /// <summary>
    /// <para>Agilent TOF or QStar .Wiff file scanner</para>
    /// <para>
    /// Updated in March 2012 to use ProteoWizard to read data from QTrap .Wiff files
    /// (cannot read MS data or TIC values from Agilent .Wiff files)
    /// </para>
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005</remarks>
    public class AgilentTOFOrQStarWiffFileInfoScanner : MSFileInfoProcessorBaseClass
    {
        // Note: The extension must be in all caps
        public const string AGILENT_TOF_OR_QSTAR_FILE_EXTENSION = ".WIFF";

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            // The dataset name is simply the file name without .wiff
            try
            {
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
        /// <returns>True if success, False if an error</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            // Override dataFilePath here, if needed
            // dataFilePath = dataFilePath;

            // Obtain the full path to the file
            var datasetFile = new FileInfo(dataFilePath);

            datasetFileInfo.FileSystemCreationTime = datasetFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = datasetFile.LastWriteTime;

            // Using the file system modification time as the acquisition end time
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = 0;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetFile.Name);
            datasetFileInfo.FileExtension = datasetFile.Extension;
            datasetFileInfo.FileSizeBytes = datasetFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();
            mLCMS2DPlot.Options.UseObservedMinScan = false;

            LoadScanDataWithProteoWizard(datasetFile, datasetFileInfo, true);

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the .Wiff file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(datasetFile, datasetFileInfo.DatasetID);

            // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;

            PostProcessTasks();

            return true;
        }
    }
}
