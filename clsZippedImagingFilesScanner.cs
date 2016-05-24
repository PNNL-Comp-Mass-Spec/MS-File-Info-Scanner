using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
//
// Last modified July 23, 2012

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
        protected bool DetermineAcqStartEndTime(FileInfo fiZipFile, clsDatasetFileInfo datasetFileInfo)
        {

            var blnSuccess = false;

            try {
                // Bump up the file size
                datasetFileInfo.FileSizeBytes += fiZipFile.Length;

                var lstFileNamesToFind = new List<string> {
                    "analysis.baf", 
                    "apexAcquisition.method", 
                    "submethods.xml"};

                var oZipFile = new Ionic.Zip.ZipFile(fiZipFile.FullName);

                foreach (var strFileNameToFind in lstFileNamesToFind) {
                    var oZipEntry = oZipFile.GetEnumerator();


                    while (oZipEntry.MoveNext()) {

                        if (!oZipEntry.Current.IsDirectory)
                        {
                            // Split the filename on the forward slash character
                            var strNameParts = oZipEntry.Current.FileName.Split('/');


                            if (strNameParts.Length > 0) {
                                if (string.Equals(strNameParts[strNameParts.Length - 1], strFileNameToFind, StringComparison.CurrentCultureIgnoreCase)) {
                                    if (oZipEntry.Current.LastModified < datasetFileInfo.AcqTimeStart) {
                                        datasetFileInfo.AcqTimeStart = oZipEntry.Current.LastModified;
                                    }

                                    if (oZipEntry.Current.LastModified > datasetFileInfo.AcqTimeEnd) {
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
                        }
                    }

                    if (blnSuccess)
                        break; // TODO: might not be correct. Was : Exit For
                }

            } catch (Exception ex) {
                ReportError("Error finding XMass method folder: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        protected DirectoryInfo GetDatasetFolder(string strDataFilePath)
        {

            // First see if strFileOrFolderPath points to a valid file
            var fiFileInfo = new FileInfo(strDataFilePath);

            if (fiFileInfo.Exists) {
                // User specified a file; assume the parent folder of this file is the dataset folder
                return fiFileInfo.Directory;
            }

            // Assume this is the path to the dataset folder
            return new DirectoryInfo(strDataFilePath);
        }

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            var strDatasetName = string.Empty;

            try {
                // The dataset name for a dataset with zipped imaging files is the name of the parent directory
                // However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
                var diDatasetFolder = GetDatasetFolder(strDataFilePath);
                strDatasetName = diDatasetFolder.Name;

                if (strDatasetName.ToLower().EndsWith(".d")) {
                    strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2);
                }

            } catch (Exception) {
                // Ignore errors
            }

            return strDatasetName;

        }

        public static bool IsZippedImagingFile(string strFileName)
        {

            var fiFileInfo = new FileInfo(strFileName);

            if (string.IsNullOrWhiteSpace(strFileName)) {
                return false;
            }

            if (fiFileInfo.Name.ToLower().StartsWith(ZIPPED_IMAGING_FILE_NAME_PREFIX.ToLower()) && fiFileInfo.Extension.ToLower() == ".zip") {
                return true;
            } else {
                return false;
            }

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the Zip files in the dataset folder)

            bool blnSuccess;

            try {
                // Determine whether strDataFilePath points to a file or a folder

                var diDatasetFolder = GetDatasetFolder(strDataFilePath);

                // Validate that we have selected a valid folder
                if (!diDatasetFolder.Exists) {
                    ReportError("File/folder not found: " + strDataFilePath);
                    return false;
                }

                // In case we cannot find any .Zip files, update the .AcqTime values to the folder creation date
                var _with1 = datasetFileInfo;
                _with1.AcqTimeStart = diDatasetFolder.CreationTime;
                _with1.AcqTimeEnd = diDatasetFolder.CreationTime;

                // Look for the 0_R*.zip files
                // If we cannot find any zip files, return false

                var lstFiles = diDatasetFolder.GetFiles(ZIPPED_IMAGING_FILE_SEARCH_SPEC).ToList();
                if (lstFiles.Count == 0) {
                    // 0_R*.zip files not found
                    ReportError(ZIPPED_IMAGING_FILE_SEARCH_SPEC + "files not found in " + diDatasetFolder.FullName);
                    blnSuccess = false;

                } else {
                    var fiFirstImagingFile = lstFiles.First();

                    // Initialize the .DatasetFileInfo
                    var _with2 = mDatasetStatsSummarizer.DatasetFileInfo;
                    _with2.FileSystemCreationTime = fiFirstImagingFile.CreationTime;
                    _with2.FileSystemModificationTime = fiFirstImagingFile.LastWriteTime;

                    _with2.AcqTimeStart = _with2.FileSystemModificationTime;
                    _with2.AcqTimeEnd = _with2.FileSystemModificationTime;

                    _with2.DatasetID = datasetFileInfo.DatasetID;
                    _with2.DatasetName = diDatasetFolder.Name;
                    _with2.FileExtension = fiFirstImagingFile.Extension;
                    _with2.FileSizeBytes = 0;
                    _with2.ScanCount = 0;


                    // Update the dataset name and file extension
                    datasetFileInfo.DatasetName = GetDatasetNameViaPath(diDatasetFolder.FullName);
                    datasetFileInfo.FileExtension = string.Empty;

                    datasetFileInfo.AcqTimeEnd = DateTime.MinValue;
                    datasetFileInfo.AcqTimeStart = DateTime.MaxValue;
                    datasetFileInfo.ScanCount = 0;

                    // Process each zip file

                    foreach (var fiFileInfo in lstFiles) {
                        // Examine all of the apexAcquisition.method files in this zip file
                        DetermineAcqStartEndTime(fiFileInfo, datasetFileInfo);
                    }

                    if (datasetFileInfo.AcqTimeEnd == DateTime.MinValue || datasetFileInfo.AcqTimeStart == DateTime.MaxValue) {
                        // Did not find any apexAcquisition.method files or submethods.xml files
                        // Use the file modification date of the first zip file
                        datasetFileInfo.AcqTimeStart = fiFirstImagingFile.LastWriteTime;
                        datasetFileInfo.AcqTimeEnd = fiFirstImagingFile.LastWriteTime;
                    }


                    // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

                    blnSuccess = true;
                }
            } catch (Exception ex) {
                ReportError("Exception processing Zipped Imaging Files: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }


    }
}