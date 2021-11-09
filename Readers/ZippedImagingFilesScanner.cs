using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Zipped imaging file scanner
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)</remarks>
    public class ZippedImagingFilesScanner : MSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: AcqStartTime, AcqEndTime

        public const string ZIPPED_IMAGING_FILE_SEARCH_SPEC = "0_R*.zip";

        public const string ZIPPED_IMAGING_FILE_NAME_PREFIX = "0_R";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="lcms2DPlotOptions"></param>
        public ZippedImagingFilesScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        { }

        /// <summary>
        /// Examines the subdirectories in the specified zip file
        /// Determines the oldest and newest modified analysis.baf files (or apexAcquisition.method file if analysis.baf files are not found)
        /// Presumes this is the AcqStartTime and AcqEndTime
        /// </summary>
        /// <param name="zipFile"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if at least one valid file is found; otherwise false</returns>
        private void DetermineAcqStartEndTime(FileInfo zipFile, DatasetFileInfo datasetFileInfo)
        {
            var success = false;

            try
            {
                // Bump up the file size
                datasetFileInfo.FileSizeBytes += zipFile.Length;

                var fileNamesToFind = new List<string> {
                    "analysis.baf",
                    "apexAcquisition.method",
                    "submethods.xml"};

                var zipFileReader = new Ionic.Zip.ZipFile(zipFile.FullName);

                foreach (var fileNameToFind in fileNamesToFind)
                {
                    foreach (var item in zipFileReader)
                    {
                        if (item == null)
                            continue;

                        if (item.IsDirectory)
                        {
                            continue;
                        }

                        // Split the filename on the forward slash character
                        var nameParts = item.FileName.Split('/');

                        if (nameParts.Length == 0)
                        {
                            continue;
                        }

                        if (!string.Equals(nameParts[nameParts.Length - 1], fileNameToFind, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (item.LastModified < datasetFileInfo.AcqTimeStart)
                        {
                            datasetFileInfo.AcqTimeStart = item.LastModified;
                        }

                        if (item.LastModified > datasetFileInfo.AcqTimeEnd)
                        {
                            datasetFileInfo.AcqTimeEnd = item.LastModified;
                        }

                        // Bump up the scan count
                        datasetFileInfo.ScanCount++;

                        // Add a Scan Stats entry
                        var scanStatsEntry = new ScanStatsEntry
                        {
                            ScanNumber = datasetFileInfo.ScanCount,
                            ScanType = 1,
                            ScanTypeName = "MALDI-HMS",
                            ScanFilterText = string.Empty,
                            ElutionTime = "0",
                            TotalIonIntensity = "0",
                            BasePeakIntensity = "0",
                            BasePeakMZ = "0",
                            BasePeakSignalToNoiseRatio = "0",
                            IonCount = 0,
                            IonCountRaw = 0
                        };

                        // Base peak signal to noise ratio

                        mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);

                        success = true;
                    }

                    if (success)
                        break;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in DetermineAcqStartEndTime: {0}", ex.Message);
            }
        }

        private DirectoryInfo GetDatasetDirectory(string dataFilePath)
        {
            // First see if dataFilePath points to a valid file
            var datasetFile = MSFileInfoScanner.GetFileInfo(dataFilePath);

            if (datasetFile.Exists)
            {
                // User specified a file; assume the parent directory of this file is the dataset directory
                return datasetFile.Directory;
            }

            // Assume this is the path to the dataset directory
            return MSFileInfoScanner.GetDirectoryInfo(dataFilePath);
        }

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            var datasetName = string.Empty;

            try
            {
                // The dataset name for a dataset with zipped imaging files is the name of the parent directory
                // However, dataFilePath could be a file or a directory path, so use GetDatasetDirectory to get the dataset directory
                var datasetDirectory = GetDatasetDirectory(dataFilePath);
                datasetName = datasetDirectory.Name;

                if (datasetName.EndsWith(".d", StringComparison.OrdinalIgnoreCase))
                {
                    datasetName = datasetName.Substring(0, datasetName.Length - 2);
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }

            return datasetName;
        }

        public static bool IsZippedImagingFile(string imagingFilePath)
        {
            if (string.IsNullOrWhiteSpace(imagingFilePath))
            {
                return false;
            }

            var imagingFile = MSFileInfoScanner.GetFileInfo(imagingFilePath);

            return imagingFile.Name.StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(imagingFile.Extension, ".zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            try
            {
                // Determine whether dataFilePath points to a file or a directory

                var datasetDirectory = GetDatasetDirectory(dataFilePath);

                // Validate that we have selected a valid directory
                if (!datasetDirectory.Exists)
                {
                    OnErrorEvent("File/directory not found: {0}", dataFilePath);
                    return false;
                }

                // In case we cannot find any .Zip files, update the .AcqTime values to the directory creation date
                datasetFileInfo.AcqTimeStart = datasetDirectory.CreationTime;
                datasetFileInfo.AcqTimeEnd = datasetDirectory.CreationTime;

                // Look for the 0_R*.zip files
                // If we cannot find any zip files, return false

                var zipFiles = PathUtils.FindFilesWildcard(datasetDirectory, ZIPPED_IMAGING_FILE_SEARCH_SPEC);
                if (zipFiles.Count == 0)
                {
                    // 0_R*.zip files not found
                    OnErrorEvent("{0} files not found in {1}", ZIPPED_IMAGING_FILE_SEARCH_SPEC, datasetDirectory.FullName);
                    return false;
                }

                var firstImagingFile = zipFiles.First();

                // Initialize the .DatasetFileInfo
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = firstImagingFile.CreationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = firstImagingFile.LastWriteTime;

                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = datasetDirectory.Name;
                mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = firstImagingFile.Extension;
                mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = 0;
                mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

                // Update the dataset name and file extension
                datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetDirectory.FullName);
                datasetFileInfo.FileExtension = string.Empty;

                datasetFileInfo.AcqTimeEnd = DateTime.MinValue;
                datasetFileInfo.AcqTimeStart = DateTime.MaxValue;
                datasetFileInfo.ScanCount = 0;

                // Process each zip file

                foreach (var zipFile in zipFiles)
                {
                    // Examine all of the apexAcquisition.method files in this zip file
                    DetermineAcqStartEndTime(zipFile, datasetFileInfo);

                    if (Options.DisableInstrumentHash)
                    {
                        mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(zipFile);
                    }
                    else
                    {
                        mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(zipFile);
                    }
                }

                if (datasetFileInfo.AcqTimeEnd == DateTime.MinValue || datasetFileInfo.AcqTimeStart == DateTime.MaxValue)
                {
                    // Did not find any apexAcquisition.method files or submethods.xml files
                    // Use the file modification date of the first zip file
                    datasetFileInfo.AcqTimeStart = firstImagingFile.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = firstImagingFile.LastWriteTime;
                }

                // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo, false);

                PostProcessTasks();

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception processing Zipped Imaging Files: {0}", ex.Message);
                return false;
            }
        }
    }
}