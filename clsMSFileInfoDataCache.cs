using System;
using System.Data;
using System.IO;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    public class clsMSFileInfoDataCache : EventNotifier
    {

        #region "Constants and Enums"
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
        private const string FOLDER_INTEGRITY_INFO_DATA_TABLE = "FolderIntegrityInfoTable";
        public const string COL_NAME_FOLDER_ID = "FolderID";
        public const string COL_NAME_FOLDER_PATH = "FolderPath";
        public const string COL_NAME_FILE_COUNT = "FileCount";

        public const string COL_NAME_COUNT_FAIL_INTEGRITY = "FileCountFailedIntegrity";
        public const string COL_NAME_FILE_NAME = "FileName";
        public const string COL_NAME_FAILED_INTEGRITY_CHECK = "FailedIntegrityCheck";

        public const string COL_NAME_SHA1_HASH = "Sha1Hash";

        public enum eMSFileInfoResultsFileColumns
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

        public enum eFolderIntegrityInfoFileColumns
        {
            FolderID = 0,
            FolderPath = 1,
            FileCount = 2,
            FileCountFailedIntegrity = 3,
            InfoLastModified = 4
        }

        public enum eFileIntegrityDetailsFileColumns
        {
            FolderID = 0,
            FileName = 1,
            FileSizeBytes = 2,
            FileModified = 3,
            FailedIntegrityCheck = 4,
            Sha1Hash = 5,
            InfoLastModified = 6
        }
        #endregion

        private enum eCachedResultsStateConstants
        {
            NotInitialized = 0,
            InitializedButUnmodified = 1,
            Modified = 2
        }

        #region "Classwide Variables"
        private string mAcquisitionTimeFilePath;

        private string mFolderIntegrityInfoFilePath;
        private int mCachedResultsAutoSaveIntervalMinutes;
        private DateTime mCachedMSInfoResultsLastSaveTime;

        private DateTime mCachedFolderIntegrityInfoLastSaveTime;
        private DataSet mMSFileInfoDataset;

        private eCachedResultsStateConstants mMSFileInfoCachedResultsState;
        private DataSet mFolderIntegrityInfoDataset;
        private eCachedResultsStateConstants mFolderIntegrityInfoResultsState;

        private int mMaximumFolderIntegrityInfoFolderID;

        #endregion

        #region "Properties"

        public string AcquisitionTimeFilePath
        {
            get => mAcquisitionTimeFilePath;
            set => mAcquisitionTimeFilePath = value;
        }

        public string FolderIntegrityInfoFilePath
        {
            get => mFolderIntegrityInfoFilePath;
            set => mFolderIntegrityInfoFilePath = value;
        }

        #endregion

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
                if (mMSFileInfoCachedResultsState == eCachedResultsStateConstants.Modified)
                {
                    if (DateTime.UtcNow.Subtract(mCachedMSInfoResultsLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes)
                    {
                        // Auto save the cached results
                        SaveCachedMSInfoResults(false);
                    }
                }

                if (mFolderIntegrityInfoResultsState == eCachedResultsStateConstants.Modified)
                {
                    if (DateTime.UtcNow.Subtract(mCachedFolderIntegrityInfoLastSaveTime).TotalMinutes >= mCachedResultsAutoSaveIntervalMinutes)
                    {
                        // Auto save the cached results
                        SaveCachedFolderIntegrityInfoResults(false);
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

        public bool CachedFolderIntegrityInfoContainsDirectory(string folderPath, out int folderID)
        {
            return CachedFolderIntegrityInfoContainsDirectory(folderPath, out folderID, out _);
        }

        public bool CachedFolderIntegrityInfoContainsDirectory(
            string directoryPath,
            out int directoryID,
            out DataRow rowMatch)
        {
            if (DatasetTableContainsPrimaryKeyValue(mFolderIntegrityInfoDataset, FOLDER_INTEGRITY_INFO_DATA_TABLE, directoryPath, out rowMatch))
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
            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized;
        }

        private void ClearCachedFolderIntegrityInfoResults()
        {
            mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].Clear();
            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized;
            mMaximumFolderIntegrityInfoFolderID = 0;
        }

        public string ConstructHeaderLine(iMSFileInfoScanner.eDataFileTypeConstants eDataFileType)
        {
            switch (eDataFileType)
            {
                case iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo:
                    // Note: The order of the output should match eMSFileInfoResultsFileColumns

                    return COL_NAME_DATASET_ID + '\t' + COL_NAME_DATASET_NAME + '\t' + COL_NAME_FILE_EXTENSION + '\t' + COL_NAME_ACQ_TIME_START + '\t' + COL_NAME_ACQ_TIME_END + '\t' + COL_NAME_SCAN_COUNT + '\t' + COL_NAME_FILE_SIZE_BYTES + '\t' + COL_NAME_INFO_LAST_MODIFIED + '\t' + COL_NAME_FILE_MODIFICATION_DATE;
                case iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo:
                    // Note: The order of the output should match eFolderIntegrityInfoFileColumns

                    return COL_NAME_FOLDER_ID + '\t' + COL_NAME_FOLDER_PATH + '\t' + COL_NAME_FILE_COUNT + '\t' + COL_NAME_COUNT_FAIL_INTEGRITY + '\t' + COL_NAME_INFO_LAST_MODIFIED;
                case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityDetails:
                    // Note: The order of the output should match eFileIntegrityDetailsFileColumns

                    return COL_NAME_FOLDER_ID + '\t' + COL_NAME_FILE_NAME + '\t' + COL_NAME_FILE_SIZE_BYTES + '\t' + COL_NAME_FILE_MODIFICATION_DATE + '\t' + COL_NAME_FAILED_INTEGRITY_CHECK + '\t' + COL_NAME_SHA1_HASH + '\t' + COL_NAME_INFO_LAST_MODIFIED;
                case iMSFileInfoScanner.eDataFileTypeConstants.FileIntegrityErrors:
                    return "File_Path" + '\t' + "Error_Message" + '\t' + COL_NAME_INFO_LAST_MODIFIED;
                default:
                    return "Unknown_File_Type";
            }
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

                // Look for valueToFind in dsDataset
                try
                {
                    rowMatch = dsDataset.Tables[tableName].Rows.Find(valueToFind);

                    if (rowMatch == null)
                    {
                        return false;
                    }

                    return true;
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
            mCachedFolderIntegrityInfoLastSaveTime = DateTime.UtcNow;

            mFolderIntegrityInfoFilePath = Path.Combine(clsMSFileInfoScanner.GetAppDirectoryPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo));

            mAcquisitionTimeFilePath = Path.Combine(clsMSFileInfoScanner.GetAppDirectoryPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo));
            clsMSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo);

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
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(msFileInfo, COL_NAME_DATASET_ID);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnStringToTable(msFileInfo, COL_NAME_DATASET_NAME);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnStringToTable(msFileInfo, COL_NAME_FILE_EXTENSION);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_ACQ_TIME_START, defaultDate);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_ACQ_TIME_END, defaultDate);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(msFileInfo, COL_NAME_SCAN_COUNT);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnLongToTable(msFileInfo, COL_NAME_FILE_SIZE_BYTES);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_INFO_LAST_MODIFIED, defaultDate);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnDateToTable(msFileInfo, COL_NAME_FILE_MODIFICATION_DATE, defaultDate);

            // Use the dataset name as the primary key since we won't always know Dataset_ID
            var MSInfoPrimaryKeyColumn = new[] { msFileInfo.Columns[COL_NAME_DATASET_NAME] };
            msFileInfo.PrimaryKey = MSInfoPrimaryKeyColumn;

            // Make the Folder Integrity Info DataTable
            var folderIntegrityInfo = new DataTable(FOLDER_INTEGRITY_INFO_DATA_TABLE);

            // Add the columns to the DataTable
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(folderIntegrityInfo, COL_NAME_FOLDER_ID);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnStringToTable(folderIntegrityInfo, COL_NAME_FOLDER_PATH);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(folderIntegrityInfo, COL_NAME_FILE_COUNT);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnIntegerToTable(folderIntegrityInfo, COL_NAME_COUNT_FAIL_INTEGRITY);
            PRISM.DatabaseUtils.DataTableUtils.AppendColumnDateToTable(folderIntegrityInfo, COL_NAME_INFO_LAST_MODIFIED, defaultDate);

            // Use the directory path as the primary key
            var FolderInfoPrimaryKeyColumn = new[] {
                folderIntegrityInfo.Columns[COL_NAME_FOLDER_PATH]
            };
            folderIntegrityInfo.PrimaryKey = FolderInfoPrimaryKeyColumn;

            // Instantiate the datasets
            mMSFileInfoDataset = new DataSet("MSFileInfoDataset");
            mFolderIntegrityInfoDataset = new DataSet("FolderIntegrityInfoDataset");

            // Add the new DataTable to each DataSet
            mMSFileInfoDataset.Tables.Add(msFileInfo);
            mFolderIntegrityInfoDataset.Tables.Add(folderIntegrityInfo);

            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized;
            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized;
        }

        public void LoadCachedResults(bool forceLoad)
        {
            if (forceLoad || mMSFileInfoCachedResultsState == eCachedResultsStateConstants.NotInitialized)
            {
                LoadCachedMSFileInfoResults();
                LoadCachedFolderIntegrityInfoResults();
            }
        }

        private void LoadCachedFolderIntegrityInfoResults()
        {
            var udtFolderStats = default(clsFileIntegrityChecker.udtFolderStatsType);

            var sepChars = new[] { '\t' };

            // Clear the Folder Integrity Info Table
            ClearCachedFolderIntegrityInfoResults();

            clsMSFileInfoScanner.ValidateDataFilePath(ref mFolderIntegrityInfoFilePath, iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo);

            OnDebugEvent("Loading cached folder integrity info from: " + Path.GetFileName(mFolderIntegrityInfoFilePath));

            if (File.Exists(mFolderIntegrityInfoFilePath))
            {
                // Read the entries from mFolderIntegrityInfoFilePath, populating mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE)
                using (var reader = new StreamReader(mFolderIntegrityInfoFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        var splitLine = dataLine.Split(sepChars);

                        if (splitLine.Length < 5)
                            continue;

                        var folderPath = splitLine[(int)eFolderIntegrityInfoFileColumns.FolderPath];

                        if (!IsNumber(splitLine[(int)eFolderIntegrityInfoFileColumns.FolderID]))
                            continue;

                        if (CachedFolderIntegrityInfoContainsDirectory(folderPath, out var folderID))
                            continue;

                        try
                        {
                            var newRow = mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].NewRow();

                            folderID = Convert.ToInt32(splitLine[(int)eFolderIntegrityInfoFileColumns.FolderID]);
                            udtFolderStats.FolderPath = folderPath;
                            udtFolderStats.FileCount = Convert.ToInt32(splitLine[(int)eFolderIntegrityInfoFileColumns.FileCount]);
                            udtFolderStats.FileCountFailIntegrity =
                                Convert.ToInt32(splitLine[(int)eFolderIntegrityInfoFileColumns.FileCountFailedIntegrity]);

                            var infoLastModified = ParseDate(splitLine[(int)eFolderIntegrityInfoFileColumns.InfoLastModified]);

                            PopulateFolderIntegrityInfoDataRow(folderID, udtFolderStats, newRow, infoLastModified);
                            mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].Rows.Add(newRow);

                        }
                        catch (Exception)
                        {
                            // Do not add this entry
                        }
                    }
                }

            }

            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified;

        }

        private void LoadCachedMSFileInfoResults()
        {
            var sepChars = new[] { '\t' };

            // Clear the MS Info Table
            ClearCachedMSInfoResults();

            clsMSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo);

            OnDebugEvent("Loading cached acquisition time file data from: " + Path.GetFileName(mAcquisitionTimeFilePath));

            if (File.Exists(mAcquisitionTimeFilePath))
            {
                // Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(MS_FILE_INFO_DATA_TABLE)
                using (var reader = new StreamReader(mAcquisitionTimeFilePath))
                {
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

                        var datasetName = splitLine[(int)eMSFileInfoResultsFileColumns.DatasetName];

                        if (!IsNumber(splitLine[(int)eMSFileInfoResultsFileColumns.DatasetID]))
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

                            var datasetId = Convert.ToInt32(splitLine[(int)eMSFileInfoResultsFileColumns.DatasetID]);
                            var datasetFileInfo = new clsDatasetFileInfo(datasetId, datasetName)
                            {
                                FileExtension = string.Copy(splitLine[(int)eMSFileInfoResultsFileColumns.FileExtension]),
                                AcqTimeStart = ParseDate(splitLine[(int)eMSFileInfoResultsFileColumns.AcqTimeStart]),
                                AcqTimeEnd = ParseDate(splitLine[(int)eMSFileInfoResultsFileColumns.AcqTimeEnd]),
                                ScanCount = Convert.ToInt32(splitLine[(int)eMSFileInfoResultsFileColumns.ScanCount]),
                                FileSizeBytes = Convert.ToInt64(splitLine[(int)eMSFileInfoResultsFileColumns.FileSizeBytes])
                            };

                            var infoLastModified = ParseDate(splitLine[(int)eMSFileInfoResultsFileColumns.InfoLastModified]);

                            if (splitLine.Length >= 9)
                            {
                                datasetFileInfo.FileSystemModificationTime = ParseDate(splitLine[(int)eMSFileInfoResultsFileColumns.FileModificationDate]);
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

            }

            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified;

        }

        private DateTime ParseDate(string dateText)
        {
            if (DateTime.TryParse(dateText, out var parsedDate))
                return parsedDate;

            return DateTime.MinValue;
        }

        private void PopulateMSInfoDataRow(clsDatasetFileInfo datasetFileInfo, DataRow currentRow)
        {
            PopulateMSInfoDataRow(datasetFileInfo, currentRow, DateTime.Now);
        }

        private void PopulateMSInfoDataRow(clsDatasetFileInfo datasetFileInfo, DataRow currentRow, DateTime infoLastModified)
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

        private void PopulateFolderIntegrityInfoDataRow(
            int folderID,
            clsFileIntegrityChecker.udtFolderStatsType directoryStats,
            DataRow currentRow)
        {
            PopulateFolderIntegrityInfoDataRow(folderID, directoryStats, currentRow, DateTime.Now);
        }

        private void PopulateFolderIntegrityInfoDataRow(
            int folderID,
            clsFileIntegrityChecker.udtFolderStatsType directoryStats,
            DataRow currentRow,
            DateTime infoLastModified)
        {
            currentRow[COL_NAME_FOLDER_ID] = folderID;
            currentRow[COL_NAME_FOLDER_PATH] = directoryStats.FolderPath;
            currentRow[COL_NAME_FILE_COUNT] = directoryStats.FileCount;
            currentRow[COL_NAME_COUNT_FAIL_INTEGRITY] = directoryStats.FileCountFailIntegrity;
            currentRow[COL_NAME_INFO_LAST_MODIFIED] = AssureMinimumDate(infoLastModified, DateTime.MinValue);

            if (folderID > mMaximumFolderIntegrityInfoFolderID)
            {
                mMaximumFolderIntegrityInfoFolderID = folderID;
            }
        }

        /// <summary>
        /// Writes out the cache files immediately
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool SaveCachedResults()
        {
            return SaveCachedResults(true);
        }

        public bool SaveCachedResults(bool clearCachedData)
        {
            var success1 = SaveCachedMSInfoResults(clearCachedData);
            var success2 = SaveCachedFolderIntegrityInfoResults(clearCachedData);

            return success1 && success2;

        }

        public bool SaveCachedFolderIntegrityInfoResults(bool clearCachedData)
        {
            bool success;

            if (mFolderIntegrityInfoDataset == null ||
                mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].Rows.Count <= 0 ||
                mFolderIntegrityInfoResultsState != eCachedResultsStateConstants.Modified)
            {
                return false;
            }

            OnDebugEvent("Saving cached folder integrity info to: " + Path.GetFileName(mFolderIntegrityInfoFilePath));

            try
            {
                // Write all of mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE) to the results file
                using (var writer = new StreamWriter(new FileStream(mFolderIntegrityInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {

                    writer.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo));

                    foreach (DataRow currentRow in mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].Rows)
                    {
                        WriteFolderIntegrityInfoDataLine(writer, currentRow);
                    }

                }

                mCachedFolderIntegrityInfoLastSaveTime = DateTime.UtcNow;

                if (clearCachedData)
                {
                    // Clear the data table
                    ClearCachedFolderIntegrityInfoResults();
                }
                else
                {
                    mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified;
                }

                success = true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in SaveCachedFolderIntegrityInfoResults", ex);
                success = false;
            }

            return success;

        }

        public bool SaveCachedMSInfoResults(bool clearCachedData)
        {

            var success = false;

            if (mMSFileInfoDataset != null &&
                mMSFileInfoDataset.Tables[MS_FILE_INFO_DATA_TABLE].Rows.Count > 0 &&
                mMSFileInfoCachedResultsState == eCachedResultsStateConstants.Modified)
            {
                OnDebugEvent("Saving cached acquisition time file data to: " + Path.GetFileName(mAcquisitionTimeFilePath));

                try
                {
                    // Write all of mMSFileInfoDataset.Tables(MS_FILE_INFO_DATA_TABLE) to the results file
                    using (var writer = new StreamWriter(new FileStream(mAcquisitionTimeFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {

                        writer.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo));

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
                        mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified;
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

        public bool UpdateCachedMSFileInfo(clsDatasetFileInfo datasetFileInfo)
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

                mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified;

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedMSFileInfo", ex);
                success = false;
            }

            return success;

        }

        public bool UpdateCachedFolderIntegrityInfo(
            clsFileIntegrityChecker.udtFolderStatsType directoryStats,
            out int folderID)
        {
            // Update the entry for this dataset in mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE)

            bool success;

            folderID = -1;

            try
            {
                if (mFolderIntegrityInfoResultsState == eCachedResultsStateConstants.NotInitialized)
                {
                    // Coding error; this shouldn't be the case
                    OnErrorEvent("mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized in UpdateCachedFolderIntegrityInfo; unable to continue");
                    return false;
                }

                // Examine the data in memory and add or update the data for dataset
                if (CachedFolderIntegrityInfoContainsDirectory(directoryStats.FolderPath, out folderID, out var currentRow))
                {
                    // Item already present; update it
                    try
                    {
                        PopulateFolderIntegrityInfoDataRow(folderID, directoryStats, currentRow);
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
                    folderID = mMaximumFolderIntegrityInfoFolderID + 1;

                    currentRow = mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].NewRow();
                    PopulateFolderIntegrityInfoDataRow(folderID, directoryStats, currentRow);
                    mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATA_TABLE].Rows.Add(currentRow);
                }

                mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified;

                success = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedFolderIntegrityInfo", ex);
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

        private void WriteFolderIntegrityInfoDataLine(TextWriter writer, DataRow currentRow)
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
        ~clsMSFileInfoDataCache()
        {
            SaveCachedResults();
        }
    }
}

