using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    public class MSFileInfoDataCache : EventNotifier
    {
        // Ignore Spelling: AcqTime, yyyy-MM-dd, HH:mm:ss

        private const string MS_FILE_INFO_DATA_TABLE = "MSFileInfoTable";

        public const string COL_NAME_DATASET_ID = "DatasetID";
        public const string COL_NAME_DATASET_NAME = "DatasetName";
        public const string COL_NAME_FILE_EXTENSION = "FileExtension";
        public const string COL_NAME_ACQ_TIME_START = "AcqTimeStart";
        public const string COL_NAME_ACQ_TIME_END = "AcqTimeEnd";
        public const string COL_NAME_SCAN_COUNT = "ScanCount";
        public const string COL_NAME_FILE_SIZE_BYTES = "FileSizeBytes";
        public const string COL_NAME_INFO_LAST_MODIFIED = "InfoLastModified";

        public const string COL_NAME_FILE_MODIFICATION_DATE = "FileModificationDate";
        public const string COL_NAME_FOLDER_ID = "FolderID";
        public const string COL_NAME_FOLDER_PATH = "FolderPath";
        public const string COL_NAME_FILE_COUNT = "FileCount";

        public const string COL_NAME_COUNT_FAIL_INTEGRITY = "FileCountFailedIntegrity";
        public const string COL_NAME_FILE_NAME = "FileName";
        public const string COL_NAME_FAILED_INTEGRITY_CHECK = "FailedIntegrityCheck";

        public const string COL_NAME_SHA1_HASH = "Sha1Hash";

        private const string DIRECTORY_INTEGRITY_INFO_DATA_TABLE = "DirectoryIntegrityInfoTable";

        public enum MSFileInfoResultsFileColumns
        {
            DatasetID = 0,
            DatasetName = 1,
            FileExtension = 2,
            AcqTimeStart = 3,
            AcqTimeEnd = 4,
            ScanCount = 5,
            FileSizeBytes = 6,
            InfoLastModified = 7,
            FileModificationDate = 8
        }

        public enum DirectoryIntegrityInfoFileColumns
        {
            DirectoryID = 0,
            DirectoryPath = 1,
            FileCount = 2,
            FileCountFailedIntegrity = 3,
            InfoLastModified = 4
        }

        public enum FileIntegrityDetailsFileColumns
        {
            FolderID = 0,
            FileName = 1,
            FileSizeBytes = 2,
            FileModified = 3,
            FailedIntegrityCheck = 4,
            Sha1Hash = 5,
            InfoLastModified = 6
        }

        private enum CachedResultsStateConstants
        {
            NotInitialized = 0,
            InitializedButUnmodified = 1,
            Modified = 2
        }

        private string mAcquisitionTimeFilePath;

        private string mDirectoryIntegrityInfoFilePath;
        private int mCachedResultsAutoSaveIntervalMinutes;
        private DateTime mCachedMSInfoResultsLastSaveTime;

        private DateTime mCachedDirectoryIntegrityInfoLastSaveTime;
        private DataSet mMSFileInfoDataset;

        private CachedResultsStateConstants mMSFileInfoCachedResultsState;
        private DataSet mDirectoryIntegrityInfoDataset;
        private CachedResultsStateConstants mDirectoryIntegrityInfoResultsState;

        private int mMaximumDirectoryIntegrityInfoDirectoryID;

        public string AcquisitionTimeFilePath
        {
            get => mAcquisitionTimeFilePath;
            set => mAcquisitionTimeFilePath = value;
        }

        public string DirectoryIntegrityInfoFilePath
        {
            get => mDirectoryIntegrityInfoFilePath;
            set => mDirectoryIntegrityInfoFilePath = value;
        }

        private DateTime AssureMinimumDate(DateTime date, DateTime minimumDate)
        {
            // Assures that date is >= minimumDate

            if (date < minimumDate)
            {
                return minimumDate;
            }

            return date;
        }

        public void AutosaveCachedResults()
        {
            if (mCachedResultsAutoSaveIntervalMinutes > 0)
            {
                if (mMSFileInfoCachedResultsState == CachedResultsStateConstants.Modified)
                {
                    if (DateTime.UtcNow.Subtract(mCachedMSInfoResultsLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes)
                    {
                        // Auto save the cached results
                        SaveCachedMSInfoResults(false);
                    }
                }

                if (mDirectoryIntegrityInfoResultsState == CachedResultsStateConstants.Modified)
                {
                    if (DateTime.UtcNow.Subtract(mCachedDirectoryIntegrityInfoLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes)
                    {
                        // Auto save the cached results
                        SaveCachedDirectoryIntegrityInfoResults(false);
                    }
                }
            }
        }

        public bool CachedMSInfoContainsDataset(string datasetName)
        {
            return CachedMSInfoContainsDataset(datasetName, out _);
        }

        public bool CachedMSInfoContainsDataset(string datasetName, out DataRow rowMatch)
        {
            return DatasetTableContainsPrimaryKeyValue(mMSFileInfoDataset, MS_FILE_INFO_DATA_TABLE, datasetName, out rowMatch);
        }

        public bool CachedDirectoryIntegrityInfoContainsDirectory(string directoryPath, out int directoryID)
        {
            return CachedDirectoryIntegrityInfoContainsDirectory(directoryPath, out directoryID, out _);
        }

        public bool CachedDirectoryIntegrityInfoContainsDirectory(
            string directoryPath,
            out int directoryID,
            out DataRow rowMatch)
        {
            if (DatasetTableContainsPrimaryKeyValue(mDirectoryIntegrityInfoDataset, DIRECTORY_INTEGRITY_INFO_DATA_TABLE, directoryPath, out rowMatch))
            {
                directoryID = Convert.ToInt32(rowMatch[COL_NAME_FOLDER_ID]);
                return true;
            }

            directoryID = 0;
            rowMatch = null;
            return false;
        }

        private void ClearCachedMSInfoResults()
        {
            mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].Clear();
            mMSFileInfoCachedResultsState = CachedResultsStateConstants.NotInitialized;
        }

        private void ClearCachedDirectoryIntegrityInfoResults()
        {
            mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].Clear();
            mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.NotInitialized;
            mMaximumDirectoryIntegrityInfoDirectoryID = 0;
        }

        public string ConstructHeaderLine(iMSFileInfoScanner.DataFileTypeConstants dataFileType)
        {
            var columnNames = dataFileType switch
            {
                iMSFileInfoScanner.DataFileTypeConstants.MSFileInfo => new List<string>
                {
                    // Note: The order of the output should match MSFileInfoResultsFileColumns
                    COL_NAME_DATASET_ID,
                    COL_NAME_DATASET_NAME,
                    COL_NAME_FILE_EXTENSION,
                    COL_NAME_ACQ_TIME_START,
                    COL_NAME_ACQ_TIME_END,
                    COL_NAME_SCAN_COUNT,
                    COL_NAME_FILE_SIZE_BYTES,
                    COL_NAME_INFO_LAST_MODIFIED,
                    COL_NAME_FILE_MODIFICATION_DATE
                },
                iMSFileInfoScanner.DataFileTypeConstants.DirectoryIntegrityInfo => new List<string>
                {
                    // Note: The order of the output should match DirectoryIntegrityInfoFileColumns
                    COL_NAME_FOLDER_ID,
                    COL_NAME_FOLDER_PATH,
                    COL_NAME_FILE_COUNT,
                    COL_NAME_COUNT_FAIL_INTEGRITY,
                    COL_NAME_INFO_LAST_MODIFIED
                },
                iMSFileInfoScanner.DataFileTypeConstants.FileIntegrityDetails => new List<string>
                {
                    // Note: The order of the output should match FileIntegrityDetailsFileColumns
                    COL_NAME_FOLDER_ID,
                    COL_NAME_FILE_NAME,
                    COL_NAME_FILE_SIZE_BYTES,
                    COL_NAME_FILE_MODIFICATION_DATE,
                    COL_NAME_FAILED_INTEGRITY_CHECK,
                    COL_NAME_SHA1_HASH,
                    COL_NAME_INFO_LAST_MODIFIED
                },
                iMSFileInfoScanner.DataFileTypeConstants.FileIntegrityErrors => new List<string>
                {
                    "File_Path",
                    "Error_Message",
                    COL_NAME_INFO_LAST_MODIFIED
                },
                _ => new List<string> { "Unknown_File_Type" }
            };

            return string.Join("\t", columnNames);
        }

        private bool DatasetTableContainsPrimaryKeyValue(
            DataSet dsDataset, string tableName, string valueToFind, out DataRow rowMatch)
        {
            try
            {
                if (dsDataset == null || dsDataset.Tables[tableName].Rows.Count == 0)
                {
                    rowMatch = null;
                    return false;
                }

                // Look for valueToFind in the dataset
                try
                {
                    rowMatch = dsDataset.Tables[tableName].Rows.Find(valueToFind);

                    return rowMatch != null;
                }
                catch (Exception)
                {
                    rowMatch = null;
                    return false;
                }
            }
            catch (Exception)
            {
                rowMatch = null;
                return false;
            }
        }

        public void InitializeVariables()
        {
            mCachedResultsAutoSaveIntervalMinutes = 5;
            mCachedMSInfoResultsLastSaveTime = DateTime.UtcNow;
            mCachedDirectoryIntegrityInfoLastSaveTime = DateTime.UtcNow;

            mDirectoryIntegrityInfoFilePath = Path.Combine(MSFileInfoScanner.GetAppDirectoryPath(), MSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.DataFileTypeConstants.DirectoryIntegrityInfo));

            mAcquisitionTimeFilePath = Path.Combine(MSFileInfoScanner.GetAppDirectoryPath(), MSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.DataFileTypeConstants.MSFileInfo));
            MSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.DataFileTypeConstants.MSFileInfo);

            InitializeDatasets();
        }

        private bool IsNumber(string value)
        {
            try
            {
                return double.TryParse(value, out _);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void InitializeDatasets()
        {
            var defaultDate = DateTime.Now;

            // Make the MSFileInfo DataTable
            var msFileInfo = new DataTable(MS_FILE_INFO_DATA_TABLE);

            // Add the columns to the DataTable
            PRISMDatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(msFileInfo, COL_NAME_DATASET_ID);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnStringToTable(msFileInfo, COL_NAME_DATASET_NAME);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnStringToTable(msFileInfo, COL_NAME_FILE_EXTENSION);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_ACQ_TIME_START, defaultDate);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_ACQ_TIME_END, defaultDate);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(msFileInfo, COL_NAME_SCAN_COUNT);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnLongToTable(msFileInfo, COL_NAME_FILE_SIZE_BYTES);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_INFO_LAST_MODIFIED, defaultDate);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_FILE_MODIFICATION_DATE, defaultDate);

            // Use the dataset name as the primary key since we won't always know Dataset_ID
            var MSInfoPrimaryKeyColumn = new[] { msFileInfo.Columns[COL_NAME_DATASET_NAME] };
            msFileInfo.PrimaryKey = MSInfoPrimaryKeyColumn;

            // Make the Folder Integrity Info DataTable
            var directoryIntegrityInfo = new DataTable(DIRECTORY_INTEGRITY_INFO_DATA_TABLE);

            // Add the columns to the DataTable
            PRISMDatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(directoryIntegrityInfo, COL_NAME_FOLDER_ID);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnStringToTable(directoryIntegrityInfo, COL_NAME_FOLDER_PATH);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(directoryIntegrityInfo, COL_NAME_FILE_COUNT);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(directoryIntegrityInfo, COL_NAME_COUNT_FAIL_INTEGRITY);
            PRISMDatabaseUtils.DataTableUtils.AppendColumnDateToTable(directoryIntegrityInfo, COL_NAME_INFO_LAST_MODIFIED, defaultDate);

            // Use the directory path as the primary key
            var FolderInfoPrimaryKeyColumn = new[] {
                directoryIntegrityInfo.Columns[COL_NAME_FOLDER_PATH]
            };
            directoryIntegrityInfo.PrimaryKey = FolderInfoPrimaryKeyColumn;

            // Instantiate the datasets
            mMSFileInfoDataset = new DataSet("MSFileInfoDataset");
            mDirectoryIntegrityInfoDataset = new DataSet("DirectoryIntegrityInfoDataset");

            // Add the new DataTable to each DataSet
            mMSFileInfoDataset.Tables.Add(msFileInfo);
            mDirectoryIntegrityInfoDataset.Tables.Add(directoryIntegrityInfo);

            mMSFileInfoCachedResultsState = CachedResultsStateConstants.NotInitialized;
            mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.NotInitialized;
        }

        public void LoadCachedResults(bool forceLoad)
        {
            if (forceLoad || mMSFileInfoCachedResultsState == CachedResultsStateConstants.NotInitialized)
            {
                LoadCachedMSFileInfoResults();
                LoadCachedDirectoryIntegrityInfoResults();
            }
        }

        private void LoadCachedDirectoryIntegrityInfoResults()
        {
            var sepChars = new[] { '\t' };

            // Clear the Folder Integrity Info Table
            ClearCachedDirectoryIntegrityInfoResults();

            MSFileInfoScanner.ValidateDataFilePath(ref mDirectoryIntegrityInfoFilePath, iMSFileInfoScanner.DataFileTypeConstants.DirectoryIntegrityInfo);

            OnDebugEvent("Loading cached directory integrity info from: {0}", Path.GetFileName(mDirectoryIntegrityInfoFilePath));

            if (File.Exists(mDirectoryIntegrityInfoFilePath))
            {
                // Read the entries from mDirectoryIntegrityInfoFilePath, populating mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE)
                using var reader = new StreamReader(mDirectoryIntegrityInfoFilePath);

                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    var splitLine = dataLine.Split(sepChars);

                    if (splitLine.Length < 5)
                        continue;

                    var directoryPath = splitLine[(int)DirectoryIntegrityInfoFileColumns.DirectoryPath];

                    if (!IsNumber(splitLine[(int)DirectoryIntegrityInfoFileColumns.DirectoryID]))
                        continue;

                    if (CachedDirectoryIntegrityInfoContainsDirectory(directoryPath, out _))
                        continue;

                    try
                    {
                        var newRow = mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].NewRow();

                        var directoryID = Convert.ToInt32(splitLine[(int)DirectoryIntegrityInfoFileColumns.DirectoryID]);

                        var directoryStats = new FileIntegrityChecker.DirectoryStatsType
                        {
                            DirectoryPath = directoryPath,
                            FileCount = Convert.ToInt32(splitLine[(int)DirectoryIntegrityInfoFileColumns.FileCount]),
                            FileCountFailIntegrity = Convert.ToInt32(splitLine[(int)DirectoryIntegrityInfoFileColumns.FileCountFailedIntegrity])
                        };

                        var infoLastModified = ParseDate(splitLine[(int)DirectoryIntegrityInfoFileColumns.InfoLastModified]);

                        PopulateDirectoryIntegrityInfoDataRow(directoryID, directoryStats, newRow, infoLastModified);
                        mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].Rows.Add(newRow);
                    }
                    catch (Exception)
                    {
                        // Do not add this entry
                    }
                }
            }

            mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.InitializedButUnmodified;
        }

        private void LoadCachedMSFileInfoResults()
        {
            var sepChars = new[] { '\t' };

            // Clear the MS Info Table
            ClearCachedMSInfoResults();

            MSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.DataFileTypeConstants.MSFileInfo);

            OnDebugEvent("Loading cached acquisition time file data from: {0}", Path.GetFileName(mAcquisitionTimeFilePath));

            if (File.Exists(mAcquisitionTimeFilePath))
            {
                // Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(MS_FILE_INFO_DATA_TABLE)
                using var reader = new StreamReader(mAcquisitionTimeFilePath);

                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    var splitLine = dataLine.Split(sepChars);

                    if (splitLine.Length < 8)
                    {
                        continue;
                    }

                    var datasetName = splitLine[(int)MSFileInfoResultsFileColumns.DatasetName];

                    if (!IsNumber(splitLine[(int)MSFileInfoResultsFileColumns.DatasetID]))
                    {
                        continue;
                    }

                    if (CachedMSInfoContainsDataset(datasetName))
                    {
                        continue;
                    }

                    try
                    {
                        var newRow = mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].NewRow();

                        var datasetId = Convert.ToInt32(splitLine[(int)MSFileInfoResultsFileColumns.DatasetID]);
                        var datasetFileInfo = new DatasetFileInfo(datasetId, datasetName)
                        {
                            FileExtension = string.Copy(splitLine[(int)MSFileInfoResultsFileColumns.FileExtension]),
                            AcqTimeStart = ParseDate(splitLine[(int)MSFileInfoResultsFileColumns.AcqTimeStart]),
                            AcqTimeEnd = ParseDate(splitLine[(int)MSFileInfoResultsFileColumns.AcqTimeEnd]),
                            ScanCount = Convert.ToInt32(splitLine[(int)MSFileInfoResultsFileColumns.ScanCount]),
                            FileSizeBytes = Convert.ToInt64(splitLine[(int)MSFileInfoResultsFileColumns.FileSizeBytes])
                        };

                        var infoLastModified = ParseDate(splitLine[(int)MSFileInfoResultsFileColumns.InfoLastModified]);

                        if (splitLine.Length >= 9)
                        {
                            datasetFileInfo.FileSystemModificationTime = ParseDate(splitLine[(int)MSFileInfoResultsFileColumns.FileModificationDate]);
                        }

                        PopulateMSInfoDataRow(datasetFileInfo, newRow, infoLastModified);
                        mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].Rows.Add(newRow);
                    }
                    catch (Exception)
                    {
                        // Do not add this entry
                    }
                }
            }

            mMSFileInfoCachedResultsState = CachedResultsStateConstants.InitializedButUnmodified;
        }

        private DateTime ParseDate(string dateText)
        {
            if (DateTime.TryParse(dateText, out var parsedDate))
                return parsedDate;

            return DateTime.MinValue;
        }

        private void PopulateMSInfoDataRow(DatasetFileInfo datasetFileInfo, DataRow currentRow)
        {
            PopulateMSInfoDataRow(datasetFileInfo, currentRow, DateTime.Now);
        }

        private void PopulateMSInfoDataRow(DatasetFileInfo datasetFileInfo, DataRow currentRow, DateTime infoLastModified)
        {
            // ToDo: Update datasetFileInfo to include some overall quality scores

            currentRow[COL_NAME_DATASET_ID] = datasetFileInfo.DatasetID;
            currentRow[COL_NAME_DATASET_NAME] = datasetFileInfo.DatasetName;
            currentRow[COL_NAME_FILE_EXTENSION] = datasetFileInfo.FileExtension;
            currentRow[COL_NAME_ACQ_TIME_START] = AssureMinimumDate(datasetFileInfo.AcqTimeStart, DateTime.MinValue);
            currentRow[COL_NAME_ACQ_TIME_END] = AssureMinimumDate(datasetFileInfo.AcqTimeEnd, DateTime.MinValue);
            currentRow[COL_NAME_SCAN_COUNT] = datasetFileInfo.ScanCount;
            currentRow[COL_NAME_FILE_SIZE_BYTES] = datasetFileInfo.FileSizeBytes;
            currentRow[COL_NAME_INFO_LAST_MODIFIED] = AssureMinimumDate(infoLastModified, DateTime.MinValue);
            currentRow[COL_NAME_FILE_MODIFICATION_DATE] = AssureMinimumDate(datasetFileInfo.FileSystemModificationTime, DateTime.MinValue);
            //[COL_NAME_QUALITY_SCORE] = datasetFileInfo.OverallQualityScore
        }

        private void PopulateDirectoryIntegrityInfoDataRow(
            int directoryID,
            FileIntegrityChecker.DirectoryStatsType directoryStats,
            DataRow currentRow)
        {
            PopulateDirectoryIntegrityInfoDataRow(directoryID, directoryStats, currentRow, DateTime.Now);
        }

        private void PopulateDirectoryIntegrityInfoDataRow(
            int directoryID,
            FileIntegrityChecker.DirectoryStatsType directoryStats,
            DataRow currentRow,
            DateTime infoLastModified)
        {
            currentRow[COL_NAME_FOLDER_ID] = directoryID;
            currentRow[COL_NAME_FOLDER_PATH] = directoryStats.DirectoryPath;
            currentRow[COL_NAME_FILE_COUNT] = directoryStats.FileCount;
            currentRow[COL_NAME_COUNT_FAIL_INTEGRITY] = directoryStats.FileCountFailIntegrity;
            currentRow[COL_NAME_INFO_LAST_MODIFIED] = AssureMinimumDate(infoLastModified, DateTime.MinValue);

            if (directoryID > mMaximumDirectoryIntegrityInfoDirectoryID)
            {
                mMaximumDirectoryIntegrityInfoDirectoryID = directoryID;
            }
        }

        /// <summary>
        /// Writes out the cache files immediately
        /// </summary>
        public bool SaveCachedResults()
        {
            return SaveCachedResults(true);
        }

        public bool SaveCachedResults(bool clearCachedData)
        {
            var success1 = SaveCachedMSInfoResults(clearCachedData);
            var success2 = SaveCachedDirectoryIntegrityInfoResults(clearCachedData);

            return success1 && success2;
        }

        public bool SaveCachedDirectoryIntegrityInfoResults(bool clearCachedData)
        {
            bool success;

            if (mDirectoryIntegrityInfoDataset == null ||
                mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].Rows.Count <= 0 ||
                mDirectoryIntegrityInfoResultsState != CachedResultsStateConstants.Modified)
            {
                return false;
            }

            OnDebugEvent("Saving cached directory integrity info to: {0}", Path.GetFileName(mDirectoryIntegrityInfoFilePath));

            try
            {
                // Write all of mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE) to the results file
                using (var writer = new StreamWriter(new FileStream(mDirectoryIntegrityInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.DataFileTypeConstants.DirectoryIntegrityInfo));

                    foreach (DataRow currentRow in mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].Rows)
                    {
                        WriteDirectoryIntegrityInfoDataLine(writer, currentRow);
                    }
                }

                mCachedDirectoryIntegrityInfoLastSaveTime = DateTime.UtcNow;

                if (clearCachedData)
                {
                    // Clear the data table
                    ClearCachedDirectoryIntegrityInfoResults();
                }
                else
                {
                    mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.InitializedButUnmodified;
                }

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in SaveCachedDirectoryIntegrityInfoResults", ex);
                success = false;
            }

            return success;
        }

        public bool SaveCachedMSInfoResults(bool clearCachedData)
        {
            var success = false;

            if (mMSFileInfoDataset?.Tables[MS_FILE_INFO_DATA_TABLE].Rows.Count > 0 &&
                mMSFileInfoCachedResultsState == CachedResultsStateConstants.Modified)
            {
                OnDebugEvent("Saving cached acquisition time file data to: {0}", Path.GetFileName(mAcquisitionTimeFilePath));

                try
                {
                    // Write all of mMSFileInfoDataset.Tables(MS_FILE_INFO_DATA_TABLE) to the results file
                    using (var writer = new StreamWriter(new FileStream(mAcquisitionTimeFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {
                        writer.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.DataFileTypeConstants.MSFileInfo));

                        foreach (DataRow currentRow in mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].Rows)
                        {
                            WriteMSInfoDataLine(writer, currentRow);
                        }
                    }

                    mCachedMSInfoResultsLastSaveTime = DateTime.UtcNow;

                    if (clearCachedData)
                    {
                        // Clear the data table
                        ClearCachedMSInfoResults();
                    }
                    else
                    {
                        mMSFileInfoCachedResultsState = CachedResultsStateConstants.InitializedButUnmodified;
                    }

                    success = true;
                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error in SaveCachedMSInfoResults", ex);
                    success = false;
                }
            }

            return success;
        }

        public bool UpdateCachedMSFileInfo(DatasetFileInfo datasetFileInfo)
        {
            // Update the entry for this dataset in mMSFileInfoDataset.Tables(MS_FILE_INFO_DATA_TABLE)

            bool success;

            try
            {
                // Examine the data in memory and add or update the data for dataset
                if (CachedMSInfoContainsDataset(datasetFileInfo.DatasetName, out var currentRow))
                {
                    // Item already present; update it
                    try
                    {
                        PopulateMSInfoDataRow(datasetFileInfo, currentRow);
                    }
                    catch (Exception)
                    {
                        // Ignore errors updating the entry
                    }
                }
                else
                {
                    // Item not present; add it
                    currentRow = mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].NewRow();
                    PopulateMSInfoDataRow(datasetFileInfo, currentRow);
                    mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].Rows.Add(currentRow);
                }

                mMSFileInfoCachedResultsState = CachedResultsStateConstants.Modified;

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedMSFileInfo", ex);
                success = false;
            }

            return success;
        }

        public bool UpdateCachedDirectoryIntegrityInfo(
            FileIntegrityChecker.DirectoryStatsType directoryStats,
            out int directoryID)
        {
            // Update the entry for this dataset in mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE)

            bool success;

            directoryID = -1;

            try
            {
                if (mDirectoryIntegrityInfoResultsState == CachedResultsStateConstants.NotInitialized)
                {
                    // Coding error; this shouldn't be the case
                    OnErrorEvent("mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.NotInitialized in UpdateCachedDirectoryIntegrityInfo; unable to continue");
                    return false;
                }

                // Examine the data in memory and add or update the data for dataset
                if (CachedDirectoryIntegrityInfoContainsDirectory(directoryStats.DirectoryPath, out directoryID, out var currentRow))
                {
                    // Item already present; update it
                    try
                    {
                        PopulateDirectoryIntegrityInfoDataRow(directoryID, directoryStats, currentRow);
                    }
                    catch (Exception)
                    {
                        // Ignore errors updating the entry
                    }
                }
                else
                {
                    // Item not present; add it

                    // Auto-assign the next available FolderID value
                    directoryID = mMaximumDirectoryIntegrityInfoDirectoryID + 1;

                    currentRow = mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].NewRow();
                    PopulateDirectoryIntegrityInfoDataRow(directoryID, directoryStats, currentRow);
                    mDirectoryIntegrityInfoDataset.Tables[DIRECTORY_INTEGRITY_INFO_DATA_TABLE].Rows.Add(currentRow);
                }

                mDirectoryIntegrityInfoResultsState = CachedResultsStateConstants.Modified;

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedDirectoryIntegrityInfo", ex);
                success = false;
            }

            return success;
        }

        private void WriteMSInfoDataLine(TextWriter writer, DataRow currentRow)
        {
            // Note: HH:mm:ss corresponds to time in 24 hour format
            writer.WriteLine(
                currentRow[COL_NAME_DATASET_ID].ToString() + '\t' +
                currentRow[COL_NAME_DATASET_NAME] + '\t' +
                currentRow[COL_NAME_FILE_EXTENSION] + '\t' +
                ((DateTime)currentRow[COL_NAME_ACQ_TIME_START]).ToString("yyyy-MM-dd HH:mm:ss") + '\t' +
                ((DateTime)currentRow[COL_NAME_ACQ_TIME_END]).ToString("yyyy-MM-dd HH:mm:ss") + '\t' +
                currentRow[COL_NAME_SCAN_COUNT] + '\t' +
                currentRow[COL_NAME_FILE_SIZE_BYTES] + '\t' +
                currentRow[COL_NAME_INFO_LAST_MODIFIED] + '\t' +
                ((DateTime)currentRow[COL_NAME_FILE_MODIFICATION_DATE]).ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private void WriteDirectoryIntegrityInfoDataLine(TextWriter writer, DataRow currentRow)
        {
            writer.WriteLine(
                currentRow[COL_NAME_FOLDER_ID].ToString() + '\t' +
                currentRow[COL_NAME_FOLDER_PATH] + '\t' +
                currentRow[COL_NAME_FILE_COUNT] + '\t' +
                currentRow[COL_NAME_COUNT_FAIL_INTEGRITY] + '\t' +
                currentRow[COL_NAME_INFO_LAST_MODIFIED]);
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~MSFileInfoDataCache()
        {
            SaveCachedResults();
        }
    }
}
