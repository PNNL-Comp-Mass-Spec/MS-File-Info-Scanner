using System;
using System.Data;
using System.IO;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    public class clsMSFileInfoDataCache : clsEventNotifier
    {

        #region "Constants and Enums"
        private const string MS_FILEINFO_DATATABLE = "MSFileInfoTable";
        public const string COL_NAME_DATASET_ID = "DatasetID";
        public const string COL_NAME_DATASET_NAME = "DatasetName";
        public const string COL_NAME_FILE_EXTENSION = "FileExtension";
        public const string COL_NAME_ACQ_TIME_START = "AcqTimeStart";
        public const string COL_NAME_ACQ_TIME_END = "AcqTimeEnd";
        public const string COL_NAME_SCAN_COUNT = "ScanCount";
        public const string COL_NAME_FILE_SIZE_BYTES = "FileSizeBytes";
        public const string COL_NAME_INFO_LAST_MODIFIED = "InfoLastModified";

        public const string COL_NAME_FILE_MODIFICATION_DATE = "FileModificationDate";
        private const string FOLDER_INTEGRITY_INFO_DATATABLE = "FolderIntegrityInfoTable";
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

        private DateTime AssureMinimumDate(DateTime dtDate, DateTime dtMinimumDate)
        {
            // Assures that dtDate is >= dtMinimumDate

            if (dtDate < dtMinimumDate)
            {
                return dtMinimumDate;
            }

            return dtDate;
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

        public bool CachedMSInfoContainsDataset(string strDatasetName)
        {
            DataRow objRowMatch;
            return CachedMSInfoContainsDataset(strDatasetName, out objRowMatch);
        }

        public bool CachedMSInfoContainsDataset(string strDatasetName, out DataRow objRowMatch)
        {
            return DatasetTableContainsPrimaryKeyValue(mMSFileInfoDataset, MS_FILEINFO_DATATABLE, strDatasetName, out objRowMatch);
        }


        public bool CachedFolderIntegrityInfoContainsFolder(string strFolderPath, out int intFolderID)
        {
            DataRow objRowMatch;
            return CachedFolderIntegrityInfoContainsFolder(strFolderPath, out intFolderID, out objRowMatch);
        }

        public bool CachedFolderIntegrityInfoContainsFolder(
            string strFolderPath,
            out int intFolderID,
            out DataRow objRowMatch)
        {
            if (DatasetTableContainsPrimaryKeyValue(mFolderIntegrityInfoDataset, FOLDER_INTEGRITY_INFO_DATATABLE, strFolderPath, out objRowMatch))
            {
                intFolderID = Convert.ToInt32(objRowMatch[COL_NAME_FOLDER_ID]);
                return true;
            }

            intFolderID = 0;
            objRowMatch = null;
            return false;
        }

        private void ClearCachedMSInfoResults()
        {
            mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].Clear();
            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized;
        }

        private void ClearCachedFolderIntegrityInfoResults()
        {
            mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].Clear();
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
            DataSet dsDataset, string strTableName, string strValueToFind)
        {
            DataRow objRowMatch;
            return DatasetTableContainsPrimaryKeyValue(dsDataset, strTableName, strValueToFind, out objRowMatch);
        }

        private bool DatasetTableContainsPrimaryKeyValue(
            DataSet dsDataset, string strTableName, string strValueToFind, out DataRow objRowMatch)
        {

            try
            {
                if (dsDataset == null || dsDataset.Tables[strTableName].Rows.Count == 0)
                {
                    objRowMatch = null;
                    return false;
                }

                // Look for strValueToFind in dsDataset
                try
                {
                    objRowMatch = dsDataset.Tables[strTableName].Rows.Find(strValueToFind);

                    if (objRowMatch == null)
                    {
                        return false;
                    }

                    return true;
                }
                catch (Exception)
                {
                    objRowMatch = null;
                    return false;
                }

            }
            catch (Exception)
            {
                objRowMatch = null;
                return false;
            }

        }

        public void InitializeVariables()
        {
            mCachedResultsAutoSaveIntervalMinutes = 5;
            mCachedMSInfoResultsLastSaveTime = DateTime.UtcNow;
            mCachedFolderIntegrityInfoLastSaveTime = DateTime.UtcNow;

            mFolderIntegrityInfoFilePath = Path.Combine(clsMSFileInfoScanner.GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo));

            mAcquisitionTimeFilePath = Path.Combine(clsMSFileInfoScanner.GetAppFolderPath(), clsMSFileInfoScanner.DefaultDataFileName(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo));
            clsMSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo);

            InitializeDatasets();
        }

        private bool IsNumber(string strValue)
        {
            try
            {
                double value;
                return double.TryParse(strValue, out value);
            }
            catch (Exception)
            {
                return false;
            }
        }


        private void InitializeDatasets()
        {
            var dtDefaultDate = DateTime.Now;

            // Make the MSFileInfo datatable
            var dtMSFileInfo = new DataTable(MS_FILEINFO_DATATABLE);

            // Add the columns to the datatable
            SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(ref dtMSFileInfo, COL_NAME_DATASET_ID);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(ref dtMSFileInfo, COL_NAME_DATASET_NAME);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(ref dtMSFileInfo, COL_NAME_FILE_EXTENSION);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(ref dtMSFileInfo, COL_NAME_ACQ_TIME_START, dtDefaultDate);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(ref dtMSFileInfo, COL_NAME_ACQ_TIME_END, dtDefaultDate);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(ref dtMSFileInfo, COL_NAME_SCAN_COUNT);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnLongToTable(ref dtMSFileInfo, COL_NAME_FILE_SIZE_BYTES);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(ref dtMSFileInfo, COL_NAME_INFO_LAST_MODIFIED, dtDefaultDate);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(ref dtMSFileInfo, COL_NAME_FILE_MODIFICATION_DATE, dtDefaultDate);

            // Use the dataset name as the primary key since we won't always know Dataset_ID
            var MSInfoPrimaryKeyColumn = new[] { dtMSFileInfo.Columns[COL_NAME_DATASET_NAME] };
            dtMSFileInfo.PrimaryKey = MSInfoPrimaryKeyColumn;


            // Make the Folder Integrity Info datatable
            var dtFolderIntegrityInfo = new DataTable(FOLDER_INTEGRITY_INFO_DATATABLE);

            // Add the columns to the datatable
            SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(ref dtFolderIntegrityInfo, COL_NAME_FOLDER_ID);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnStringToTable(ref dtFolderIntegrityInfo, COL_NAME_FOLDER_PATH);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(ref dtFolderIntegrityInfo, COL_NAME_FILE_COUNT);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnIntegerToTable(ref dtFolderIntegrityInfo, COL_NAME_COUNT_FAIL_INTEGRITY);
            SharedVBNetRoutines.ADONetRoutines.AppendColumnDateToTable(ref dtFolderIntegrityInfo, COL_NAME_INFO_LAST_MODIFIED, dtDefaultDate);

            // Use the folder path as the primary key
            var FolderInfoPrimaryKeyColumn = new[] {
                dtFolderIntegrityInfo.Columns[COL_NAME_FOLDER_PATH]
            };
            dtFolderIntegrityInfo.PrimaryKey = FolderInfoPrimaryKeyColumn;

            // Instantiate the datasets
            mMSFileInfoDataset = new DataSet("MSFileInfoDataset");
            mFolderIntegrityInfoDataset = new DataSet("FolderIntegrityInfoDataset");

            // Add the new DataTable to each DataSet
            mMSFileInfoDataset.Tables.Add(dtMSFileInfo);
            mFolderIntegrityInfoDataset.Tables.Add(dtFolderIntegrityInfo);

            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.NotInitialized;
            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized;
        }

        public void LoadCachedResults(bool blnForceLoad)
        {
            if (blnForceLoad || mMSFileInfoCachedResultsState == eCachedResultsStateConstants.NotInitialized)
            {
                LoadCachedMSFileInfoResults();
                LoadCachedFolderIntegrityInfoResults();
            }
        }


        private void LoadCachedFolderIntegrityInfoResults()
        {
            var udtFolderStats = default(clsFileIntegrityChecker.udtFolderStatsType);

            var strSepChars = new[] { '\t' };

            // Clear the Folder Integrity Info Table
            ClearCachedFolderIntegrityInfoResults();

            clsMSFileInfoScanner.ValidateDataFilePath(ref mFolderIntegrityInfoFilePath, iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo);

            OnDebugEvent("Loading cached folder integrity info from: " + Path.GetFileName(mFolderIntegrityInfoFilePath));

            if (File.Exists(mFolderIntegrityInfoFilePath))
            {
                // Read the entries from mFolderIntegrityInfoFilePath, populating mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE)

                if (clsMSFileInfoScanner.USE_XML_OUTPUT_FILE)
                {
                    var fsInFile = new FileStream(mFolderIntegrityInfoFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    mFolderIntegrityInfoDataset.ReadXml(fsInFile);
                    fsInFile.Close();
                }
                else
                {
                    var srInFile = new StreamReader(mFolderIntegrityInfoFilePath);
                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if (string.IsNullOrWhiteSpace(strLineIn))
                            continue;

                        var strSplitLine = strLineIn.Split(strSepChars);

                        if (strSplitLine.Length < 5)
                            continue;

                        var strFolderPath = strSplitLine[(int)eFolderIntegrityInfoFileColumns.FolderPath];

                        if (!IsNumber(strSplitLine[(int)eFolderIntegrityInfoFileColumns.FolderID]))
                            continue;

                        int intFolderID;
                        if (CachedFolderIntegrityInfoContainsFolder(strFolderPath, out intFolderID))
                            continue;

                        try
                        {
                            var objNewRow = mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].NewRow();

                            intFolderID = Convert.ToInt32(strSplitLine[(int)eFolderIntegrityInfoFileColumns.FolderID]);
                            udtFolderStats.FolderPath = strFolderPath;
                            udtFolderStats.FileCount = Convert.ToInt32(strSplitLine[(int)eFolderIntegrityInfoFileColumns.FileCount]);
                            udtFolderStats.FileCountFailIntegrity = Convert.ToInt32(strSplitLine[(int)eFolderIntegrityInfoFileColumns.FileCountFailedIntegrity]);

                            var dtInfoLastModified = ParseDate(strSplitLine[(int)eFolderIntegrityInfoFileColumns.InfoLastModified]);

                            PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objNewRow, dtInfoLastModified);
                            mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].Rows.Add(objNewRow);

                        }
                        catch (Exception)
                        {
                            // Do not add this entry
                        }
                    }
                    srInFile.Close();

                }
            }

            mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified;

        }


        private void LoadCachedMSFileInfoResults()
        {
            var strSepChars = new[] { '\t' };

            // Clear the MS Info Table
            ClearCachedMSInfoResults();

            clsMSFileInfoScanner.ValidateDataFilePath(ref mAcquisitionTimeFilePath, iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo);

            OnDebugEvent("Loading cached acquisition time file data from: " + Path.GetFileName(mAcquisitionTimeFilePath));

            if (File.Exists(mAcquisitionTimeFilePath))
            {
                // Read the entries from mAcquisitionTimeFilePath, populating mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

                if (clsMSFileInfoScanner.USE_XML_OUTPUT_FILE)
                {
                    using (var fsInFile = new FileStream(mAcquisitionTimeFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        mMSFileInfoDataset.ReadXml(fsInFile);
                    }
                }
                else
                {
                    var srInFile = new StreamReader(mAcquisitionTimeFilePath);
                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if ((strLineIn == null))
                        {
                            continue;
                        }
                        var strSplitLine = strLineIn.Split(strSepChars);

                        if (strSplitLine.Length < 8)
                        {
                            continue;
                        }

                        var strDatasetName = strSplitLine[(int)eMSFileInfoResultsFileColumns.DatasetName];

                        if (!IsNumber(strSplitLine[(int)eMSFileInfoResultsFileColumns.DatasetID]))
                        {
                            continue;
                        }

                        if (CachedMSInfoContainsDataset(strDatasetName))
                        {
                            continue;
                        }

                        try
                        {
                            var objNewRow = mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].NewRow();

                            var datasetId = Convert.ToInt32(strSplitLine[(int)eMSFileInfoResultsFileColumns.DatasetID]);
                            var datasetFileInfo = new clsDatasetFileInfo(datasetId, strDatasetName)
                            {
                                FileExtension = string.Copy(strSplitLine[(int)eMSFileInfoResultsFileColumns.FileExtension]),
                                AcqTimeStart = ParseDate(strSplitLine[(int)eMSFileInfoResultsFileColumns.AcqTimeStart]),
                                AcqTimeEnd = ParseDate(strSplitLine[(int)eMSFileInfoResultsFileColumns.AcqTimeEnd]),
                                ScanCount = Convert.ToInt32(strSplitLine[(int)eMSFileInfoResultsFileColumns.ScanCount]),
                                FileSizeBytes = Convert.ToInt64(strSplitLine[(int)eMSFileInfoResultsFileColumns.FileSizeBytes])
                            };

                            var dtInfoLastModified = ParseDate(strSplitLine[(int)eMSFileInfoResultsFileColumns.InfoLastModified]);

                            if (strSplitLine.Length >= 9)
                            {
                                datasetFileInfo.FileSystemModificationTime = ParseDate(strSplitLine[(int)eMSFileInfoResultsFileColumns.FileModificationDate]);
                            }

                            PopulateMSInfoDataRow(datasetFileInfo, objNewRow, dtInfoLastModified);
                            mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].Rows.Add(objNewRow);

                        }
                        catch (Exception)
                        {
                            // Do not add this entry
                        }
                    }
                    srInFile.Close();

                }
            }

            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified;

        }

        private DateTime ParseDate(string dateText)
        {
            DateTime parsedDate;
            if (DateTime.TryParse(dateText, out parsedDate))
                return parsedDate;

            return DateTime.MinValue;
        }

        private void PopulateMSInfoDataRow(clsDatasetFileInfo datasetFileInfo, DataRow objRow)
        {
            PopulateMSInfoDataRow(datasetFileInfo, objRow, DateTime.Now);
        }


        private void PopulateMSInfoDataRow(clsDatasetFileInfo datasetFileInfo, DataRow objRow, DateTime dtInfoLastModified)
        {
            // ToDo: Update datasetFileInfo to include some overall quality scores

            objRow[COL_NAME_DATASET_ID] = datasetFileInfo.DatasetID;
            objRow[COL_NAME_DATASET_NAME] = datasetFileInfo.DatasetName;
            objRow[COL_NAME_FILE_EXTENSION] = datasetFileInfo.FileExtension;
            objRow[COL_NAME_ACQ_TIME_START] = AssureMinimumDate(datasetFileInfo.AcqTimeStart, DateTime.MinValue);
            objRow[COL_NAME_ACQ_TIME_END] = AssureMinimumDate(datasetFileInfo.AcqTimeEnd, DateTime.MinValue);
            objRow[COL_NAME_SCAN_COUNT] = datasetFileInfo.ScanCount;
            objRow[COL_NAME_FILE_SIZE_BYTES] = datasetFileInfo.FileSizeBytes;
            objRow[COL_NAME_INFO_LAST_MODIFIED] = AssureMinimumDate(dtInfoLastModified, DateTime.MinValue);
            objRow[COL_NAME_FILE_MODIFICATION_DATE] = AssureMinimumDate(datasetFileInfo.FileSystemModificationTime, DateTime.MinValue);
            //[COL_NAME_QUALITY_SCORE] = datasetFileInfo.OverallQualityScore
        }

        private void PopulateFolderIntegrityInfoDataRow(
            int intFolderID,
            clsFileIntegrityChecker.udtFolderStatsType udtFolderStats,
            DataRow objRow)
        {
            PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow, DateTime.Now);
        }


        private void PopulateFolderIntegrityInfoDataRow(
            int intFolderID,
            clsFileIntegrityChecker.udtFolderStatsType udtFolderStats,
            DataRow objRow,
            DateTime dtInfoLastModified)
        {
            objRow[COL_NAME_FOLDER_ID] = intFolderID;
            objRow[COL_NAME_FOLDER_PATH] = udtFolderStats.FolderPath;
            objRow[COL_NAME_FILE_COUNT] = udtFolderStats.FileCount;
            objRow[COL_NAME_COUNT_FAIL_INTEGRITY] = udtFolderStats.FileCountFailIntegrity;
            objRow[COL_NAME_INFO_LAST_MODIFIED] = AssureMinimumDate(dtInfoLastModified, DateTime.MinValue);

            if (intFolderID > mMaximumFolderIntegrityInfoFolderID)
            {
                mMaximumFolderIntegrityInfoFolderID = intFolderID;
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

        public bool SaveCachedResults(bool blnClearCachedData)
        {
            var blnSuccess1 = SaveCachedMSInfoResults(blnClearCachedData);
            var blnSuccess2 = SaveCachedFolderIntegrityInfoResults(blnClearCachedData);

            return blnSuccess1 & blnSuccess2;

        }

        public bool SaveCachedFolderIntegrityInfoResults(bool blnClearCachedData)
        {
            bool blnSuccess;

            if ((mFolderIntegrityInfoDataset == null) ||
                mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].Rows.Count <= 0 ||
                mFolderIntegrityInfoResultsState != eCachedResultsStateConstants.Modified)
            {
                return false;
            }

            OnDebugEvent("Saving cached folder integrity info to: " + Path.GetFileName(mFolderIntegrityInfoFilePath));

            try
            {
                // Write all of mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE) to the results file
                if (clsMSFileInfoScanner.USE_XML_OUTPUT_FILE)
                {
                    using (var fsOutfile = new FileStream(mFolderIntegrityInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        mFolderIntegrityInfoDataset.WriteXml(fsOutfile);
                    }
                }
                else
                {
                    using (var srOutFile = new StreamWriter(new FileStream(mFolderIntegrityInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                    {

                        srOutFile.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.eDataFileTypeConstants.FolderIntegrityInfo));

                        foreach (DataRow objRow in mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].Rows)
                        {
                            WriteFolderIntegrityInfoDataLine(srOutFile, objRow);
                        }

                    }
                }

                mCachedFolderIntegrityInfoLastSaveTime = DateTime.UtcNow;

                if (blnClearCachedData)
                {
                    // Clear the data table
                    ClearCachedFolderIntegrityInfoResults();
                }
                else
                {
                    mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.InitializedButUnmodified;
                }

                blnSuccess = true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in SaveCachedFolderIntegrityInfoResults", ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool SaveCachedMSInfoResults(bool blnClearCachedData)
        {

            var blnSuccess = false;

            if ((mMSFileInfoDataset != null) &&
                mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].Rows.Count > 0 &&
                mMSFileInfoCachedResultsState == eCachedResultsStateConstants.Modified) {
                    OnDebugEvent("Saving cached acquisition time file data to: " + Path.GetFileName(mAcquisitionTimeFilePath));

                    try {
                        // Write all of mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE) to the results file
                        if (clsMSFileInfoScanner.USE_XML_OUTPUT_FILE) {
                            using (var fsOutfile = new FileStream(mAcquisitionTimeFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                mMSFileInfoDataset.WriteXml(fsOutfile);
                            }
                        } else {
                            using (var srOutFile = new StreamWriter(new FileStream(mAcquisitionTimeFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                            {

                                srOutFile.WriteLine(ConstructHeaderLine(iMSFileInfoScanner.eDataFileTypeConstants.MSFileInfo));

                                foreach (DataRow objRow in mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].Rows)
                                {
                                    WriteMSInfoDataLine(srOutFile, objRow);
                                }

                            }
                        }

                        mCachedMSInfoResultsLastSaveTime = DateTime.UtcNow;

                        if (blnClearCachedData) {
                            // Clear the data table
                            ClearCachedMSInfoResults();
                        } else {
                            mMSFileInfoCachedResultsState = eCachedResultsStateConstants.InitializedButUnmodified;
                        }

                        blnSuccess = true;

                    } catch (Exception ex) {
                        OnErrorEvent("Error in SaveCachedMSInfoResults", ex);
                        blnSuccess = false;
                    }
                }

            return blnSuccess;

        }

        public bool UpdateCachedMSFileInfo(clsDatasetFileInfo datasetFileInfo)
        {
            // Update the entry for this dataset in mMSFileInfoDataset.Tables(MS_FILEINFO_DATATABLE)

            bool blnSuccess;

            try
            {
                // Examine the data in memory and add or update the data for strDataset
                DataRow objRow;
                if (CachedMSInfoContainsDataset(datasetFileInfo.DatasetName, out objRow))
                {
                    // Item already present; update it
                    try
                    {
                        PopulateMSInfoDataRow(datasetFileInfo, objRow);
                    }
                    catch (Exception)
                    {
                        // Ignore errors updating the entry
                    }
                }
                else
                {
                    // Item not present; add it
                    objRow = mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].NewRow();
                    PopulateMSInfoDataRow(datasetFileInfo, objRow);
                    mMSFileInfoDataset.Tables[MS_FILEINFO_DATATABLE].Rows.Add(objRow);
                }

                mMSFileInfoCachedResultsState = eCachedResultsStateConstants.Modified;

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedMSFileInfo", ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public bool UpdateCachedFolderIntegrityInfo(
            clsFileIntegrityChecker.udtFolderStatsType udtFolderStats,
            out int intFolderID)
        {
            // Update the entry for this dataset in mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE)

            bool blnSuccess;

            intFolderID = -1;

            try
            {
                if (mFolderIntegrityInfoResultsState == eCachedResultsStateConstants.NotInitialized)
                {
                    // Coding error; this shouldn't be the case
                    OnErrorEvent("mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.NotInitialized in UpdateCachedFolderIntegrityInfo; unable to continue");
                    return false;
                }

                // Examine the data in memory and add or update the data for strDataset
                DataRow objRow;
                if (CachedFolderIntegrityInfoContainsFolder(udtFolderStats.FolderPath, out intFolderID, out objRow))
                {
                    // Item already present; update it
                    try
                    {
                        PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow);
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
                    intFolderID = mMaximumFolderIntegrityInfoFolderID + 1;

                    objRow = mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].NewRow();
                    PopulateFolderIntegrityInfoDataRow(intFolderID, udtFolderStats, objRow);
                    mFolderIntegrityInfoDataset.Tables[FOLDER_INTEGRITY_INFO_DATATABLE].Rows.Add(objRow);
                }

                mFolderIntegrityInfoResultsState = eCachedResultsStateConstants.Modified;

                blnSuccess = true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in UpdateCachedFolderIntegrityInfo", ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private void WriteMSInfoDataLine(StreamWriter srOutFile, DataRow objRow)
        {
            // Note: HH:mm:ss corresponds to time in 24 hour format
            srOutFile.WriteLine(
                objRow[COL_NAME_DATASET_ID].ToString() + '\t' +
                objRow[COL_NAME_DATASET_NAME] + '\t' +
                objRow[COL_NAME_FILE_EXTENSION] + '\t' +
                ((DateTime)objRow[COL_NAME_ACQ_TIME_START]).ToString("yyyy-MM-dd HH:mm:ss") + '\t' +
                ((DateTime)objRow[COL_NAME_ACQ_TIME_END]).ToString("yyyy-MM-dd HH:mm:ss") + '\t' +
                objRow[COL_NAME_SCAN_COUNT] + '\t' +
                objRow[COL_NAME_FILE_SIZE_BYTES] + '\t' +
                objRow[COL_NAME_INFO_LAST_MODIFIED] + '\t' +
                ((DateTime)objRow[COL_NAME_FILE_MODIFICATION_DATE]).ToString("yyyy-MM-dd HH:mm:ss"));

        }


        private void WriteFolderIntegrityInfoDataLine(StreamWriter srOutFile, DataRow objRow)
        {
            srOutFile.WriteLine(
                objRow[COL_NAME_FOLDER_ID].ToString() + '\t' +
                objRow[COL_NAME_FOLDER_PATH] + '\t' +
                objRow[COL_NAME_FILE_COUNT] + '\t' +
                objRow[COL_NAME_COUNT_FAIL_INTEGRITY] + '\t' +
                objRow[COL_NAME_INFO_LAST_MODIFIED]);
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

