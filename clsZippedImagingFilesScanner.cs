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
        /// <param name="fiZipFile"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if at least one valid file is found; otherwise false</returns>
        /// <remarks></remarks>
        private void DetermineAcqStartEndTime(FileInfo fiZipFile, clsDatasetFileInfo datasetFileInfo)
        {

            var blnSuccess = false;

            try
            {
                // Bump up the file size
                datasetFileInfo.FileSizeBytes += fiZipFile.Length;

                var lstFileNamesToFind = new List<string> {
                    "analysis.baf",
                    "apexAcquisition.method",
                    "submethods.xml"};

                var oZipFile = new Ionic.Zip.ZipFile(fiZipFile.FullName);

                foreach (var strFileNameToFind in lstFileNamesToFind)
                {
                    using (var oZipEntry = oZipFile.GetEnumerator())
                    {

                        while (oZipEntry.MoveNext())
                        {
                            if (oZipEntry.Current == null)
                                continue;

                            if (oZipEntry.Current.IsDirectory)
                            {
                                continue;
                            }

                            // Split the filename on the forward slash character
                            var strNameParts = oZipEntry.Current.FileName.Split('/');

                            if (strNameParts.Length <= 0)
                            {
                                continue;
                            }

                            if (!string.Equals(strNameParts[strNameParts.Length - 1], strFileNameToFind, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (oZipEntry.Current.LastModified < datasetFileInfo.AcqTimeStart)
                            {
                                datasetFileInfo.AcqTimeStart = oZipEntry.Current.LastModified;
                            }

                            if (oZipEntry.Current.LastModified > datasetFileInfo.AcqTimeEnd)
                            {
                                datasetFileInfo.AcqTimeEnd = oZipEntry.Current.LastModified;
                            }

                            // Bump up the scan count
                            datasetFileInfo.ScanCount += 1;

                            // Add a Scan Stats entry
                            var objScanStatsEntry = new clsScanStatsEntry
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

                            mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                            blnSuccess = true;
                        }
                    }

                    if (blnSuccess)
                        break;
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in DetermineAcqStartEndTime: " + ex.Message);
            }

        }

        private DirectoryInfo GetDatasetFolder(string strDataFilePath)
        {

            // First see if strFileOrFolderPath points to a valid file
            var fiFileInfo = new FileInfo(strDataFilePath);

            if (fiFileInfo.Exists)
            {
                // User specified a file; assume the parent folder of this file is the dataset folder
                return fiFileInfo.Directory;
            }

            // Assume this is the path to the dataset folder
            return new DirectoryInfo(strDataFilePath);
        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            var strDatasetName = string.Empty;

            try
            {
                // The dataset name for a dataset with zipped imaging files is the name of the parent directory
                // However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
                var diDatasetFolder = GetDatasetFolder(strDataFilePath);
                strDatasetName = diDatasetFolder.Name;

                if (strDatasetName.ToLower().EndsWith(".d"))
                {
                    strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2);
                }

            }
            catch (Exception)
            {
                // Ignore errors
            }

            return strDatasetName;

        }

        public static bool IsZippedImagingFile(string strFileName)
        {

            var fiFileInfo = new FileInfo(strFileName);

            if (string.IsNullOrWhiteSpace(strFileName))
            {
                return false;
            }

            if (fiFileInfo.Name.ToLower().StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX.ToLower()) && fiFileInfo.Extension.ToLower() == ".zip")
            {
                return true;
            }

            return false;
        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the Zip files in the dataset folder)

            bool blnSuccess;

            try
            {
                // Determine whether strDataFilePath points to a file or a folder

                var diDatasetFolder = GetDatasetFolder(strDataFilePath);

                // Validate that we have selected a valid folder
                if (!diDatasetFolder.Exists)
                {
                    OnErrorEvent("File/folder not found: " + strDataFilePath);
                    return false;
                }

                // In case we cannot find any .Zip files, update the .AcqTime values to the folder creation date
                datasetFileInfo.AcqTimeStart = diDatasetFolder.CreationTime;
                datasetFileInfo.AcqTimeEnd = diDatasetFolder.CreationTime;

                // Look for the 0_R*.zip files
                // If we cannot find any zip files, return false

                var lstFiles = diDatasetFolder.GetFiles(ZIPPED_IMAGING_FILE_SEARCH_SPEC).ToList();
                if (lstFiles.Count == 0)
                {
                    // 0_R*.zip files not found
                    OnErrorEvent(ZIPPED_IMAGING_FILE_SEARCH_SPEC + "files not found in " + diDatasetFolder.FullName);
                    blnSuccess = false;

                }
                else
                {
                    var fiFirstImagingFile = lstFiles.First();

                    // Initialize the .DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = fiFirstImagingFile.CreationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = fiFirstImagingFile.LastWriteTime;

                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime;

                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = diDatasetFolder.Name;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = fiFirstImagingFile.Extension;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = 0;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = 0;

                    // Update the dataset name and file extension
                    datasetFileInfo.DatasetName = GetDatasetNameViaPath(diDatasetFolder.FullName);
                    datasetFileInfo.FileExtension = string.Empty;

                    datasetFileInfo.AcqTimeEnd = DateTime.MinValue;
                    datasetFileInfo.AcqTimeStart = DateTime.MaxValue;
                    datasetFileInfo.ScanCount = 0;

                    // Process each zip file

                    foreach (var fiFileInfo in lstFiles)
                    {
                        // Examine all of the apexAcquisition.method files in this zip file
                        DetermineAcqStartEndTime(fiFileInfo, datasetFileInfo);

                        if (mDisableInstrumentHash)
                        {
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(fiFileInfo);
                        }
                        else
                        {
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(fiFileInfo);
                        }
                    }

                    if (datasetFileInfo.AcqTimeEnd == DateTime.MinValue || datasetFileInfo.AcqTimeStart == DateTime.MaxValue)
                    {
                        // Did not find any apexAcquisition.method files or submethods.xml files
                        // Use the file modification date of the first zip file
                        datasetFileInfo.AcqTimeStart = fiFirstImagingFile.LastWriteTime;
                        datasetFileInfo.AcqTimeEnd = fiFirstImagingFile.LastWriteTime;
                    }

                    // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

                    blnSuccess = true;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception processing Zipped Imaging Files: " + ex.Message);
                blnSuccess = false;
            }

            PostProcessTasks();
            return blnSuccess;

        }

    }
}