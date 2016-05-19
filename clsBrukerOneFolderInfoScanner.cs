using System;
using System.IO;
using System.Text.RegularExpressions;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//
// Last modified September 17, 2005

namespace MSFileInfoScanner
{
    public class clsBrukerOneFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {


        public const string BRUKER_ONE_FOLDER_NAME = "1";
        private const string BRUKER_LOCK_FILE = "LOCK";
        private const string BRUKER_ACQU_FILE = "acqu";
        private const string BRUKER_ACQU_FILE_ACQ_LINE_START = "##$AQ_DATE= <";

        private const char BRUKER_ACQU_FILE_ACQ_LINE_END = '>';

        private const string TIC_FILE_NUMBER_OF_FILES_LINE_START = "Number of files :";
        private const string TIC_FILE_TIC_FILE_LIST_START = "TIC file list:";
        private const string TIC_FILE_TIC_FILE_LIST_END = "TIC end of file list";

        private const string TIC_FILE_COMMENT_SECTION_END = "CommentEnd";

        private const string PEK_FILE_FILENAME_LINE = "Filename:";

        private const DateTime MINIMUM_ACCEPTABLE_ACQ_START_TIME = 1/1/1975 12:00:00 AM;
        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            DirectoryInfo ioFolderInfo = default(DirectoryInfo);
            string strDatasetName = string.Empty;

            try {
                // The dataset name for a Bruker 1 folder or zipped S folder is the name of the parent directory
                ioFolderInfo = new DirectoryInfo(strDataFilePath);
                strDatasetName = ioFolderInfo.Parent.Name;
            } catch (Exception ex) {
                // Ignore errors
            }

            if (strDatasetName == null)
                strDatasetName = string.Empty;
            return strDatasetName;

        }

        public static bool IsZippedSFolder(string strFilePath)
        {
            var reIsZippedSFolder = new Regex("s[0-9]+\\.zip", Text.RegularExpressions.RegexOptions.IgnoreCase);
            return reIsZippedSFolder.Match(strFilePath).Success;
        }

        private bool ParseBrukerDateFromArray(ref string strLineIn, ref DateTime dtDate)
        {
            string strDate = null;
            bool blnSuccess = false;

            int intStartIndex = 0;
            int intIndexCheck = 0;
            int intIndexCompare = 0;

            string[] strSplitLine[ = null;

            try {
                strSplitLine[ = strLineIn.Split(' ');

                // Remove any entries from strSplitLine[) that are blank
                intIndexCheck = intStartIndex;
                while (intIndexCheck < strSplitLine[.Length && strSplitLine[.Length > 0) {
                    if (strSplitLine[intIndexCheck).Length == 0) {
                        for (intIndexCompare = intIndexCheck; intIndexCompare <= strSplitLine[.Length - 2; intIndexCompare++) {
                            strSplitLine[intIndexCompare) = strSplitLine[intIndexCompare + 1);
                        }
                        Array.Resize(ref strSplitLine[, strSplitLine[.Length - 1);
                    } else {
                        intIndexCheck += 1;
                    }
                }

                if (strSplitLine[.Length >= 5) {
                    intStartIndex = strSplitLine[.Length - 5;
                    strDate = strSplitLine[4 + intStartIndex) + "-" + strSplitLine[1 + intStartIndex) + "-" + strSplitLine[2 + intStartIndex) + " " + strSplitLine[3 + intStartIndex);
                    dtDate = DateTime.Parse(strDate);
                    blnSuccess = true;
                } else {
                    blnSuccess = false;
                }
            } catch (Exception ex) {
                // Date parse failed
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerAcquFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            string strLineIn = null;

            bool blnSuccess = false;

            try {
                // Try to open the acqu file
                blnSuccess = false;
                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, BRUKER_ACQU_FILE))) {
                    while (!srInFile.EndOfStream) {
                        strLineIn = srInFile.ReadLine();

                        if ((strLineIn != null)) {
                            if (strLineIn.StartsWith(BRUKER_ACQU_FILE_ACQ_LINE_START)) {
                                // Date line found
                                // It is of the form: ##$AQ_DATE= <Sat Aug 20 07:56:55 2005> 
                                strLineIn = strLineIn.Substring(BRUKER_ACQU_FILE_ACQ_LINE_START.Length).Trim;
                                strLineIn = strLineIn.TrimEnd(BRUKER_ACQU_FILE_ACQ_LINE_END);

                                blnSuccess = ParseBrukerDateFromArray(ref strLineIn, ref datasetFileInfo.AcqTimeEnd);
                                break; // TODO: might not be correct. Was : Exit Do
                            }
                        }
                    }
                }

            } catch (Exception ex) {
                // Error opening the acqu file
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerLockFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            string strLineIn = null;
            string[] strSplitLine[ = null;

            bool blnSuccess = false;

            try {
                // Try to open the Lock file
                // The date line is the first (and only) line in the file
                blnSuccess = false;
                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, BRUKER_LOCK_FILE))) {
                    if (!srInFile.EndOfStream) {
                        strLineIn = srInFile.ReadLine();
                        if ((strLineIn != null)) {
                            // Date line found
                            // It is of the form: wd37119 2208 WD37119\9TOperator Sat Aug 20 06:10:31 2005
                            strSplitLine[ = strLineIn.Trim.Split(' ');

                            blnSuccess = ParseBrukerDateFromArray(ref strLineIn, ref datasetFileInfo.AcqTimeStart);
                        }
                    }
                }

            } catch (Exception ex) {
                // Error opening the Lock file
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerZippedSFolders(ref DirectoryInfo ioFolderInfo, clsDatasetFileInfo datasetFileInfo)
        {
            // Looks through the s*.zip files to determine the total file size (uncompressed) of all files in all the matching .Zip files
            // Updates datasetFileInfo.FileSizeBytes with this info, while also updating datasetFileInfo.ScanCount with the total number of files found
            // Returns True if success and also if no matching Zip files were found; returns False if error

            bool blnSuccess = false;

            datasetFileInfo.FileSizeBytes = 0;
            datasetFileInfo.ScanCount = 0;

            try {
                foreach (FileInfo ioFileMatch in ioFolderInfo.GetFiles("s*.zip")) {
                    // Get the info on each zip file

                    using (Ionic.Zip.ZipFile objZipFile = new Ionic.Zip.ZipFile(ioFileMatch.FullName)) {
                        foreach (Ionic.Zip.ZipEntry objZipEntry in objZipFile.Entries) {
                            datasetFileInfo.FileSizeBytes += objZipEntry.UncompressedSize;
                            datasetFileInfo.ScanCount += 1;
                        }
                    }

                }
                blnSuccess = true;

            } catch (Exception ex) {
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseICRFolder(ref DirectoryInfo ioFolderInfo, clsDatasetFileInfo datasetFileInfo)
        {
            // Look for and open the .Pek file in ioFolderInfo
            // Count the number of PEK_FILE_FILENAME_LINE lines

            FileInfo ioFileMatch = default(FileInfo);
            string strLineIn = null;

            int intFileListCount = 0;
            bool blnSuccess = false;

            foreach ( ioFileMatch in ioFolderInfo.GetFiles("*.pek")) {
                try {
                    // Try to open the PEK file
                    blnSuccess = false;
                    intFileListCount = 0;
                    using (var srInFile = new StreamReader(ioFileMatch.OpenRead())) {
                        while (!srInFile.EndOfStream) {
                            strLineIn = srInFile.ReadLine();

                            if ((strLineIn != null)) {
                                if (strLineIn.StartsWith(PEK_FILE_FILENAME_LINE)) {
                                    intFileListCount += 1;
                                }
                            }
                        }
                    }
                    blnSuccess = true;

                } catch (Exception ex) {
                    // Error opening or parsing the PEK file
                    blnSuccess = false;
                }

                if (intFileListCount > datasetFileInfo.ScanCount) {
                    datasetFileInfo.ScanCount = intFileListCount;
                }

                // Only parse the first .Pek file found
                break; // TODO: might not be correct. Was : Exit For
            }

            return blnSuccess;

        }

        private bool ParseTICFolder(ref DirectoryInfo ioFolderInfo, clsDatasetFileInfo datasetFileInfo, ref DateTime dtTICModificationDate)
        {
            // Look for and open the .Tic file in ioFolderInfo and look for the line listing the number of files
            // As a second validation, count the number of lines between TIC_FILE_TIC_FILE_LIST_START and TIC_FILE_TIC_FILE_LIST_END

            FileInfo ioFileMatch = default(FileInfo);
            string strLineIn = null;

            int intFileListCount = 0;
            bool blnParsingTICFileList = false;
            bool blnSuccess = false;

            foreach ( ioFileMatch in ioFolderInfo.GetFiles("*.tic")) {
                try {
                    // Try to open the TIC file
                    blnSuccess = false;
                    intFileListCount = 0;
                    using (var srInFile = new StreamReader(ioFileMatch.OpenRead())) {
                        while (!srInFile.EndOfStream) {
                            strLineIn = srInFile.ReadLine();

                            if ((strLineIn != null)) {
                                if (blnParsingTICFileList) {
                                    if (strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_END)) {
                                        blnParsingTICFileList = false;
                                        break; // TODO: might not be correct. Was : Exit Do
                                    } else if (strLineIn == TIC_FILE_COMMENT_SECTION_END) {
                                        // Found the end of the text section; exit the loop
                                        break; // TODO: might not be correct. Was : Exit Do
                                    } else {
                                        intFileListCount += 1;
                                    }
                                } else {
                                    if (strLineIn.StartsWith(TIC_FILE_NUMBER_OF_FILES_LINE_START)) {
                                        // Number of files line found
                                        // Parse out the file count
                                        datasetFileInfo.ScanCount = int.Parse(strLineIn.Substring(TIC_FILE_NUMBER_OF_FILES_LINE_START.Length).Trim);
                                    } else if (strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_START)) {
                                        blnParsingTICFileList = true;
                                    } else if (strLineIn == TIC_FILE_COMMENT_SECTION_END) {
                                        // Found the end of the text section; exit the loop
                                        break; // TODO: might not be correct. Was : Exit Do
                                    }
                                }
                            }
                        }
                    }
                    blnSuccess = true;

                    dtTICModificationDate = ioFileMatch.LastWriteTime;

                } catch (Exception ex) {
                    // Error opening or parsing the TIC file
                    blnSuccess = false;
                }

                if (intFileListCount > datasetFileInfo.ScanCount) {
                    datasetFileInfo.ScanCount = intFileListCount;
                }

                // Only parse the first .Tic file found
                break; // TODO: might not be correct. Was : Exit For
            }

            return blnSuccess;

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Process a Bruker 1 folder or Bruker s001.zip file, specified by strDataFilePath
            // If a Bruker 1 folder, then it must contain file acqu and typically contains file LOCK

            FileInfo ioFileInfo = default(FileInfo);
            DirectoryInfo ioZippedSFilesFolderInfo = null;
            DirectoryInfo ioSubFolder = default(DirectoryInfo);

            int intScanCountSaved = 0;
            DateTime dtTICModificationDate = default(DateTime);

            bool blnParsingBrukerOneFolder = false;
            bool blnSuccess = false;

            try {
                // Determine whether strDataFilePath points to a file or a folder
                // See if strFileOrFolderPath points to a valid file
                ioFileInfo = new FileInfo(strDataFilePath);

                if (ioFileInfo.Exists()) {
                    // Parsing a zipped S folder
                    blnParsingBrukerOneFolder = false;

                    // The dataset name is equivalent to the name of the folder containing strDataFilePath
                    ioZippedSFilesFolderInfo = ioFileInfo.Directory;
                    blnSuccess = true;

                    // Cannot determine accurate acqusition start or end times
                    // We have to assign a date, so we'll assign the date for the zipped s-folder
                    var _with1 = datasetFileInfo;
                    _with1.AcqTimeStart = ioFileInfo.LastWriteTime;
                    _with1.AcqTimeEnd = ioFileInfo.LastWriteTime;

                } else {
                    // Assuming it's a "1" folder
                    blnParsingBrukerOneFolder = true;

                    ioZippedSFilesFolderInfo = new DirectoryInfo(strDataFilePath);
                    if (ioZippedSFilesFolderInfo.Exists) {
                        // Determine the dataset name by looking up the name of the parent folder of strDataFilePath
                        ioZippedSFilesFolderInfo = ioZippedSFilesFolderInfo.Parent;
                        blnSuccess = true;
                    } else {
                        blnSuccess = false;
                    }
                }

                if (blnSuccess) {
                    var _with2 = datasetFileInfo;
                    _with2.FileSystemCreationTime = ioZippedSFilesFolderInfo.CreationTime;
                    _with2.FileSystemModificationTime = ioZippedSFilesFolderInfo.LastWriteTime;

                    // The acquisition times will get updated below to more accurate values
                    _with2.AcqTimeStart = _with2.FileSystemModificationTime;
                    _with2.AcqTimeEnd = _with2.FileSystemModificationTime;

                    _with2.DatasetName = ioZippedSFilesFolderInfo.Name;
                    _with2.FileExtension = string.Empty;
                    _with2.FileSizeBytes = 0;
                    _with2.ScanCount = 0;
                }
            } catch (Exception ex) {
                blnSuccess = false;
            }

            if (blnSuccess && blnParsingBrukerOneFolder) {
                // Parse the Acqu File to populate .AcqTimeEnd
                blnSuccess = ParseBrukerAcquFile(strDataFilePath, ref datasetFileInfo);

                if (blnSuccess) {
                    // Parse the Lock file to populate.AcqTimeStart
                    blnSuccess = ParseBrukerLockFile(strDataFilePath, ref datasetFileInfo);

                    if (!blnSuccess) {
                        // Use the end time as the start time
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                        blnSuccess = true;
                    }
                }
            }

            if (blnSuccess) {
                // Look for the zipped S folders in ioZippedSFilesFolderInfo
                try {
                    blnSuccess = ParseBrukerZippedSFolders(ref ioZippedSFilesFolderInfo, ref datasetFileInfo);
                    intScanCountSaved = datasetFileInfo.ScanCount;
                } catch (Exception ex) {
                    // Error parsing zipped S Folders; do not abort
                }

                try {
                    blnSuccess = false;

                    // Look for the TIC* folder to obtain the scan count from a .Tic file
                    // If the Scan Count in the TIC is larger than the scan count from ParseBrukerZippedSFolders,
                    //  then we'll use that instead
                    foreach ( ioSubFolder in ioZippedSFilesFolderInfo.GetDirectories("TIC*")) {
                        blnSuccess = ParseTICFolder(ref ioSubFolder, ref datasetFileInfo, ref dtTICModificationDate);

                        if (blnSuccess) {
                            // Successfully parsed a TIC folder; do not parse any others
                            break; // TODO: might not be correct. Was : Exit For
                        }
                    }

                    if (!blnSuccess) {
                        // TIC folder not found; see if a .TIC file is present in ioZippedSFilesFolderInfo
                        blnSuccess = ParseTICFolder(ref ioZippedSFilesFolderInfo, ref datasetFileInfo, ref dtTICModificationDate);
                    }

                    if (blnSuccess & !blnParsingBrukerOneFolder && dtTICModificationDate >= MINIMUM_ACCEPTABLE_ACQ_START_TIME) {
                        // If dtTICModificationDate is earlier than .AcqTimeStart then update to dtTICMOdificationDate
                        var _with3 = datasetFileInfo;
                        if (dtTICModificationDate < _with3.AcqTimeStart) {
                            _with3.AcqTimeStart = dtTICModificationDate;
                            _with3.AcqTimeEnd = dtTICModificationDate;
                        }
                    }

                    if (!blnSuccess) {
                        // .Tic file not found in ioZippedSFilesFolderInfo
                        // Look for an ICR* folder to obtain the scan count from a .Pek file
                        foreach ( ioSubFolder in ioZippedSFilesFolderInfo.GetDirectories("ICR*")) {
                            blnSuccess = ParseICRFolder(ref ioSubFolder, ref datasetFileInfo);

                            if (blnSuccess) {
                                // Successfully parsed an ICR folder; do not parse any others
                                break; // TODO: might not be correct. Was : Exit For
                            }
                        }
                    }

                    if (blnSuccess == true) {
                        if (intScanCountSaved > datasetFileInfo.ScanCount) {
                            datasetFileInfo.ScanCount = intScanCountSaved;
                        }
                    } else {
                        // Set success to true anyway since we do have enough information to save the MS file info
                        blnSuccess = true;
                    }

                } catch (Exception ex) {
                    // Error parsing the TIC* or ICR* folders; do not abort
                }

                // Validate datasetFileInfo.AcqTimeStart vs. datasetFileInfo.AcqTimeEnd
                var _with4 = datasetFileInfo;
                if (datasetFileInfo.AcqTimeEnd >= MINIMUM_ACCEPTABLE_ACQ_START_TIME) {
                    if (_with4.AcqTimeStart > _with4.AcqTimeEnd) {
                        // Start time cannot be greater than the end time
                        _with4.AcqTimeStart = _with4.AcqTimeEnd;
                    } else if (_with4.AcqTimeStart < MINIMUM_ACCEPTABLE_ACQ_START_TIME) {
                        _with4.AcqTimeStart = _with4.AcqTimeEnd;
                    } else {
                        //'Dim dtDateCompare As DateTime
                        //'If .ScanCount > 0 Then
                        //'    ' Make sure the start time is greater than the end time minus the scan count times 30 seconds per scan
                        //'    dtDateCompare = .AcqTimeEnd.Subtract(New TimeSpan(0, 0, .ScanCount * 30))
                        //'Else
                        //'    dtDateCompare = .AcqTimeEnd - 
                        //'End If

                        //'If .AcqTimeStart < dtDateCompare Then
                        //'    .AcqTimeStart = .AcqTimeEnd
                        //'End If
                    }
                }
            }

            return blnSuccess;

        }

    }
}
