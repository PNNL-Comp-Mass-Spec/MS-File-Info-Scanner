using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//

namespace MSFileInfoScanner
{
    public class clsZippedImagingFilesScanner : clsMSFileInfoProcessorBaseClass
    {

        public const string ZIPPED_IMAGING_FILE_SEARCH_SPEC = "0_R*.zip";

        public const string ZIPPED_IMAGING_FILE_NAME_PREFIX = "0_R";

        /// <summary>
        /// Examines the subdirectories in the specified zip file
        /// Determines the oldest and newest modified analysis.baf files (or apexAcquisition.method file if analysis.baf files are not found)
        /// Presumes this is the AcqStartTime and AcqEndTime
        /// </summary>
        /// <param name="zipFile"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if at least one valid file is found; otherwise false</returns>
        /// <remarks></remarks>
        private void DetermineAcqStartEndTime(FileInfo zipFile, clsDatasetFileInfo datasetFileInfo)
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

                var oZipFile = new Ionic.Zip.ZipFile(zipFile.FullName);

                foreach (var fileNameToFind in fileNamesToFind)
                {
                    using (var zipEntry = oZipFile.GetEnumerator())
                    {

                        while (zipEntry.MoveNext())
                        {
                            if (zipEntry.Current == null)
                                continue;

                            if (zipEntry.Current.IsDirectory)
                            {
                                continue;
                            }

                            // Split the filename on the forward slash character
                            var nameParts = zipEntry.Current.FileName.Split('/');

                            if (nameParts.Length <= 0)
                            {
                                continue;
                            }

                            if (!string.Equals(nameParts[nameParts.Length - 1], fileNameToFind, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (zipEntry.Current.LastModified < datasetFileInfo.AcqTimeStart)
                            {
                                datasetFileInfo.AcqTimeStart = zipEntry.Current.LastModified;
                            }

                            if (zipEntry.Current.LastModified > datasetFileInfo.AcqTimeEnd)
                            {
                                datasetFileInfo.AcqTimeEnd = zipEntry.Current.LastModified;
                            }

                            // Bump up the scan count
                            datasetFileInfo.ScanCount += 1;

                            // Add a Scan Stats entry
                            var scanStatsEntry = new clsScanStatsEntry
                            {
                                ScanNumber = datasetFileInfo.ScanCount,
                                ScanType = 1,
                                ScanTypeName = "MALDI-HMS",
                                ScanFilterText = "",
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
                    }

                    if (success)
                        break;
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in DetermineAcqStartEndTime: " + ex.Message);
            }

        }

        private DirectoryInfo GetDatasetDirectory(string dataFilePath)
        {

            // First see if dataFilePath points to a valid file
            var datasetFile = new FileInfo(dataFilePath);

            if (datasetFile.Exists)
            {
                // User specified a file; assume the parent directory of this file is the dataset directory
                return datasetFile.Directory;
            }

            // Assume this is the path to the dataset directory
            return new DirectoryInfo(dataFilePath);
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

                if (datasetName.ToLower().EndsWith(".d"))
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

            var imagingFile = new FileInfo(imagingFilePath);

            if (imagingFile.Name.ToLower().StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX.ToLower()) && imagingFile.Extension.ToLower() == ".zip")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, clsDatasetFileInfo datasetFileInfo)
        {

            bool success;

            ResetResults();

            try
            {
                // Determine whether dataFilePath points to a file or a directory

                var datasetDirectory = GetDatasetDirectory(dataFilePath);

                // Validate that we have selected a valid directory
                if (!datasetDirectory.Exists)
                {
                    OnErrorEvent("File/directory not found: " + dataFilePath);
                    return false;
                }

                // In case we cannot find any .Zip files, update the .AcqTime values to the directory creation date
                datasetFileInfo.AcqTimeStart = datasetDirectory.CreationTime;
                datasetFileInfo.AcqTimeEnd = datasetDirectory.CreationTime;

                // Look for the 0_R*.zip files
                // If we cannot find any zip files, return false

                var zipFiles = datasetDirectory.GetFiles(ZIPPED_IMAGING_FILE_SEARCH_SPEC).ToList();
                if (zipFiles.Count == 0)
                {
                    // 0_R*.zip files not found
                    OnErrorEvent(ZIPPED_IMAGING_FILE_SEARCH_SPEC + "files not found in " + datasetDirectory.FullName);
                    success = false;

                }
                else
                {
                    var firstImagingFile = zipFiles.First();

                    // Initialize the .DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = firstImagingFile.CreationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = firstImagingFile.LastWriteTime;

                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
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

                        if (mDisableInstrumentHash)
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

                    // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

                    success = true;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception processing Zipped Imaging Files: " + ex.Message);
                success = false;
            }

            PostProcessTasks();
            return success;

        }

    }
}