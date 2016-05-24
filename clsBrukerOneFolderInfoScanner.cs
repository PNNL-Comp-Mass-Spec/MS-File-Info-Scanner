using System;
using System.IO;
using System.Linq;
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

        private readonly DateTime MINIMUM_ACCEPTABLE_ACQ_START_TIME = new DateTime(1975, 1, 1);

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            var strDatasetName = string.Empty;

            try
            {
                // The dataset name for a Bruker 1 folder or zipped S folder is the name of the parent directory
                var ioFolderInfo = new DirectoryInfo(strDataFilePath);
                strDatasetName = ioFolderInfo.Parent.Name;
            }
            catch (Exception)
            {
                // Ignore errors
            }

            return strDatasetName;

        }

        public static bool IsZippedSFolder(string strFilePath)
        {
            var reIsZippedSFolder = new Regex("s[0-9]+\\.zip", RegexOptions.IgnoreCase);
            return reIsZippedSFolder.Match(strFilePath).Success;
        }

        /// <summary>
        /// Parse out the date from a string of the form 
        /// Sat Aug 20 07:56:55 2005
        /// </summary>
        /// <param name="strLineIn"></param>
        /// <param name="dtDate"></param>
        /// <returns></returns>
        private bool ParseBrukerDateFromArray(string strLineIn, out DateTime dtDate)
        {
            bool blnSuccess;
            dtDate = DateTime.MinValue;

            try
            {
                var strSplitLine = strLineIn.Split(' ').ToList();

                // Remove any entries from strSplitLine that are blank
                var lineParts = (from item in strSplitLine where !string.IsNullOrEmpty(item) select item).ToList();

                if (lineParts.Count >= 5)
                {
                    var intStartIndex = lineParts.Count - 5;

                    // Construct date in the format yyyy-MMM-dd hh:mm:ss
                    // For example:                 2005-Aug-20 07:56:55
                    var strDate =
                        lineParts[4 + intStartIndex] + "-" +
                        lineParts[1 + intStartIndex] + "-" +
                        lineParts[2 + intStartIndex] + " " +
                        lineParts[3 + intStartIndex];

                    blnSuccess = DateTime.TryParse(strDate, out dtDate);
                }
                else
                {
                    blnSuccess = false;
                }
            }
            catch (Exception)
            {
                // Date parse failed
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerAcquFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            bool blnSuccess;

            try
            {
                // Try to open the acqu file
                blnSuccess = false;
                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, BRUKER_ACQU_FILE)))
                {
                    while (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();

                        if ((strLineIn != null))
                        {
                            if (strLineIn.StartsWith(BRUKER_ACQU_FILE_ACQ_LINE_START))
                            {
                                // Date line found
                                // It is of the form: ##$AQ_DATE= <Sat Aug 20 07:56:55 2005>
                                strLineIn = strLineIn.Substring(BRUKER_ACQU_FILE_ACQ_LINE_START.Length).Trim();
                                strLineIn = strLineIn.TrimEnd(BRUKER_ACQU_FILE_ACQ_LINE_END);

                                DateTime dtDate;
                                blnSuccess = ParseBrukerDateFromArray(strLineIn, out dtDate);
                                if (blnSuccess)
                                    datasetFileInfo.AcqTimeEnd = dtDate;
                                break;
                            }
                        }
                    }
                }

            }
            catch (Exception)
            {
                // Error opening the acqu file
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerLockFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            bool blnSuccess;

            try
            {
                // Try to open the Lock file
                // The date line is the first (and only) line in the file
                blnSuccess = false;
                using (var srInFile = new StreamReader(Path.Combine(strFolderPath, BRUKER_LOCK_FILE)))
                {
                    if (!srInFile.EndOfStream)
                    {
                        var strLineIn = srInFile.ReadLine();
                        if ((strLineIn != null))
                        {
                            // Date line found
                            // It is of the form: wd37119 2208 WD37119\9TOperator Sat Aug 20 06:10:31 2005

                            DateTime dtDate;
                            blnSuccess = ParseBrukerDateFromArray(strLineIn, out dtDate);
                            if (blnSuccess)
                                datasetFileInfo.AcqTimeStart = dtDate;
                        }
                    }
                }

            }
            catch (Exception)
            {
                // Error opening the Lock file
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseBrukerZippedSFolders(DirectoryInfo diZippedSFilesFolderInfo, clsDatasetFileInfo datasetFileInfo)
        {
            // Looks through the s*.zip files to determine the total file size (uncompressed) of all files in all the matching .Zip files
            // Updates datasetFileInfo.FileSizeBytes with this info, while also updating datasetFileInfo.ScanCount with the total number of files found
            // Returns True if success and also if no matching Zip files were found; returns False if error

            bool blnSuccess;

            datasetFileInfo.FileSizeBytes = 0;
            datasetFileInfo.ScanCount = 0;

            try
            {
                foreach (var zippedSFile in diZippedSFilesFolderInfo.GetFiles("s*.zip"))
                {
                    // Get the info on each zip file

                    using (var objZipFile = new Ionic.Zip.ZipFile(zippedSFile.FullName))
                    {
                        foreach (var objZipEntry in objZipFile.Entries)
                        {
                            datasetFileInfo.FileSizeBytes += objZipEntry.UncompressedSize;
                            datasetFileInfo.ScanCount += 1;
                        }
                    }

                }
                blnSuccess = true;

            }
            catch (Exception)
            {
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ParseICRFolder(DirectoryInfo icrFolderInfo, clsDatasetFileInfo datasetFileInfo)
        {
            // Look for and open the .Pek file in ioFolderInfo
            // Count the number of PEK_FILE_FILENAME_LINE lines

            var intFileListCount = 0;
            var blnSuccess = false;

            foreach (var pekFile in icrFolderInfo.GetFiles("*.pek"))
            {
                try
                {
                    // Try to open the PEK file
                    intFileListCount = 0;
                    using (var srInFile = new StreamReader(pekFile.OpenRead()))
                    {
                        while (!srInFile.EndOfStream)
                        {
                            var strLineIn = srInFile.ReadLine();

                            if ((strLineIn != null))
                            {
                                if (strLineIn.StartsWith(PEK_FILE_FILENAME_LINE))
                                {
                                    intFileListCount += 1;
                                }
                            }
                        }
                    }
                    blnSuccess = true;

                }
                catch (Exception)
                {
                    // Error opening or parsing the PEK file
                    blnSuccess = false;
                }

                if (intFileListCount > datasetFileInfo.ScanCount)
                {
                    datasetFileInfo.ScanCount = intFileListCount;
                }

                // Only parse the first .Pek file found
                break;
            }

            return blnSuccess;

        }

        private bool ParseTICFolder(DirectoryInfo ticFolderInfo, clsDatasetFileInfo datasetFileInfo, out DateTime dtTICModificationDate)
        {
            // Look for and open the .Tic file in ioFolderInfo and look for the line listing the number of files
            // As a second validation, count the number of lines between TIC_FILE_TIC_FILE_LIST_START and TIC_FILE_TIC_FILE_LIST_END

            var intFileListCount = 0;
            var blnParsingTICFileList = false;
            var blnSuccess = false;

            dtTICModificationDate = DateTime.MinValue;

            foreach (var ticFile in ticFolderInfo.GetFiles("*.tic"))
            {
                try
                {
                    // Try to open the TIC file
                    intFileListCount = 0;
                    using (var srInFile = new StreamReader(ticFile.OpenRead()))
                    {
                        while (!srInFile.EndOfStream)
                        {
                            var strLineIn = srInFile.ReadLine();

                            if ((string.IsNullOrEmpty(strLineIn)))
                            {
                                continue;
                            }

                            if (blnParsingTICFileList)
                            {
                                if (strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_END))
                                {
                                    blnParsingTICFileList = false;
                                    break;
                                }

                                if (strLineIn == TIC_FILE_COMMENT_SECTION_END)
                                {
                                    // Found the end of the text section; exit the loop
                                    break;
                                }

                                intFileListCount += 1;
                            }
                            else
                            {
                                if (strLineIn.StartsWith(TIC_FILE_NUMBER_OF_FILES_LINE_START))
                                {
                                    // Number of files line found
                                    // Parse out the file count
                                    datasetFileInfo.ScanCount = int.Parse(strLineIn.Substring(TIC_FILE_NUMBER_OF_FILES_LINE_START.Length).Trim());
                                }
                                else if (strLineIn.StartsWith(TIC_FILE_TIC_FILE_LIST_START))
                                {
                                    blnParsingTICFileList = true;
                                }
                                else if (strLineIn == TIC_FILE_COMMENT_SECTION_END)
                                {
                                    // Found the end of the text section; exit the loop
                                    break;
                                }
                            }
                        }
                    }
                    blnSuccess = true;

                    dtTICModificationDate = ticFile.LastWriteTime;

                }
                catch (Exception)
                {
                    // Error opening or parsing the TIC file
                    blnSuccess = false;
                }

                if (intFileListCount > datasetFileInfo.ScanCount)
                {
                    datasetFileInfo.ScanCount = intFileListCount;
                }

                // Only parse the first .Tic file found
                break;
            }

            return blnSuccess;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strDataFilePath">Bruker 1 folder path or Bruker s001.zip file</param>
        /// <param name="datasetFileInfo"></param>
        /// <returns></returns>
        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Process a Bruker 1 folder or Bruker s001.zip file, specified by strDataFilePath
            // If a Bruker 1 folder, it must contain file acqu and typically contains file LOCK

            DirectoryInfo diZippedSFilesFolderInfo = null;

            var intScanCountSaved = 0;
            var dtTICModificationDate = DateTime.MinValue;

            var blnParsingBrukerOneFolder = false;
            bool blnSuccess;

            try
            {
                // Determine whether strDataFilePath points to a file or a folder
                // See if strFileOrFolderPath points to a valid file
                var brukerDatasetfile = new FileInfo(strDataFilePath);

                if (brukerDatasetfile.Exists)
                {
                    // Parsing a zipped S folder
                    blnParsingBrukerOneFolder = false;

                    // The dataset name is equivalent to the name of the folder containing strDataFilePath
                    diZippedSFilesFolderInfo = brukerDatasetfile.Directory;
                    blnSuccess = true;

                    // Cannot determine accurate acqusition start or end times
                    // We have to assign a date, so we'll assign the date for the zipped s-folder
                    datasetFileInfo.AcqTimeStart = brukerDatasetfile.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = brukerDatasetfile.LastWriteTime;

                }
                else
                {
                    // Assuming it's a "1" folder
                    blnParsingBrukerOneFolder = true;

                    diZippedSFilesFolderInfo = new DirectoryInfo(strDataFilePath);
                    if (diZippedSFilesFolderInfo.Exists)
                    {
                        // Determine the dataset name by looking up the name of the parent folder of strDataFilePath
                        diZippedSFilesFolderInfo = diZippedSFilesFolderInfo.Parent;
                        blnSuccess = true;
                    }
                    else
                    {
                        blnSuccess = false;
                    }
                }

                if (blnSuccess && diZippedSFilesFolderInfo != null)
                {
                    datasetFileInfo.FileSystemCreationTime = diZippedSFilesFolderInfo.CreationTime;
                    datasetFileInfo.FileSystemModificationTime = diZippedSFilesFolderInfo.LastWriteTime;

                    // The acquisition times will get updated below to more accurate values
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                    datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                    datasetFileInfo.DatasetName = diZippedSFilesFolderInfo.Name;
                    datasetFileInfo.FileExtension = string.Empty;
                    datasetFileInfo.FileSizeBytes = 0;
                    datasetFileInfo.ScanCount = 0;
                }
            }
            catch (Exception)
            {
                blnSuccess = false;
            }

            if (blnSuccess && blnParsingBrukerOneFolder)
            {
                // Parse the Acqu File to populate .AcqTimeEnd
                blnSuccess = ParseBrukerAcquFile(strDataFilePath, datasetFileInfo);

                if (blnSuccess)
                {
                    // Parse the Lock file to populate.AcqTimeStart
                    blnSuccess = ParseBrukerLockFile(strDataFilePath, datasetFileInfo);

                    if (!blnSuccess)
                    {
                        // Use the end time as the start time
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                        blnSuccess = true;
                    }
                }
            }

            if (blnSuccess)
            {
                // Look for the zipped S folders in ioZippedSFilesFolderInfo
                try
                {
                    blnSuccess = ParseBrukerZippedSFolders(diZippedSFilesFolderInfo, datasetFileInfo);
                    intScanCountSaved = datasetFileInfo.ScanCount;
                }
                catch (Exception)
                {
                    // Error parsing zipped S Folders; do not abort
                }

                try
                {
                    blnSuccess = false;

                    // Look for the TIC* folder to obtain the scan count from a .Tic file
                    // If the Scan Count in the TIC is larger than the scan count from ParseBrukerZippedSFolders,
                    //  then we'll use that instead
                    foreach (var subFolder in diZippedSFilesFolderInfo.GetDirectories("TIC*"))
                    {
                        blnSuccess = ParseTICFolder(subFolder, datasetFileInfo, out dtTICModificationDate);

                        if (blnSuccess)
                        {
                            // Successfully parsed a TIC folder; do not parse any others
                            break;
                        }
                    }

                    if (!blnSuccess)
                    {
                        // TIC folder not found; see if a .TIC file is present in ioZippedSFilesFolderInfo
                        blnSuccess = ParseTICFolder(diZippedSFilesFolderInfo, datasetFileInfo, out dtTICModificationDate);
                    }

                    if (blnSuccess & !blnParsingBrukerOneFolder && dtTICModificationDate >= MINIMUM_ACCEPTABLE_ACQ_START_TIME)
                    {
                        // If dtTICModificationDate is earlier than .AcqTimeStart then update to dtTICMOdificationDate
                        if (dtTICModificationDate < datasetFileInfo.AcqTimeStart)
                        {
                            datasetFileInfo.AcqTimeStart = dtTICModificationDate;
                            datasetFileInfo.AcqTimeEnd = dtTICModificationDate;
                        }
                    }

                    if (!blnSuccess)
                    {
                        // .Tic file not found in ioZippedSFilesFolderInfo
                        // Look for an ICR* folder to obtain the scan count from a .Pek file
                        foreach (var subFolder in diZippedSFilesFolderInfo.GetDirectories("ICR*"))
                        {
                            blnSuccess = ParseICRFolder(subFolder, datasetFileInfo);

                            if (blnSuccess)
                            {
                                // Successfully parsed an ICR folder; do not parse any others
                                break;
                            }
                        }
                    }

                    if (blnSuccess)
                    {
                        if (intScanCountSaved > datasetFileInfo.ScanCount)
                        {
                            datasetFileInfo.ScanCount = intScanCountSaved;
                        }
                    }
                    else
                    {
                        // Set success to true anyway since we do have enough information to save the MS file info
                        blnSuccess = true;
                    }

                }
                catch (Exception)
                {
                    // Error parsing the TIC* or ICR* folders; do not abort
                }

                // Validate datasetFileInfo.AcqTimeStart vs. datasetFileInfo.AcqTimeEnd
                if (datasetFileInfo.AcqTimeEnd >= MINIMUM_ACCEPTABLE_ACQ_START_TIME)
                {
                    if (datasetFileInfo.AcqTimeStart > datasetFileInfo.AcqTimeEnd)
                    {
                        // Start time cannot be greater than the end time
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                    }
                    else if (datasetFileInfo.AcqTimeStart < MINIMUM_ACCEPTABLE_ACQ_START_TIME)
                    {
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                    }
                    else
                    {
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
