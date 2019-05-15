using System;
using System.Collections.Generic;
using System.IO;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//

namespace MSFileInfoScanner
{
    /// <summary>
    /// Class for reading data from Waters mass spectrometers (previously Micromass)
    /// </summary>
    public class clsMicromassRawFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string MICROMASS_RAW_FOLDER_EXTENSION = ".RAW";

        private readonly DateTime MINIMUM_ACCEPTABLE_ACQ_START_TIME = new DateTime(1975, 1, 1);

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            // The dataset name is simply the directory name without .Raw
            try
            {
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private TimeSpan MinutesToTimeSpan(double decimalMinutes)
        {
            try
            {
                var minutes = (int)Math.Floor(decimalMinutes);
                var seconds = (int)Math.Round((decimalMinutes - minutes) * 60, 0);

                return new TimeSpan(0, minutes, seconds);
            }
            catch (Exception)
            {
                return new TimeSpan(0, 0, 0);
            }

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
            ResetResults();

            try
            {
                var datasetDirectory = new DirectoryInfo(dataFilePath);
                datasetFileInfo.FileSystemCreationTime = datasetDirectory.CreationTime;
                datasetFileInfo.FileSystemModificationTime = datasetDirectory.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetDirectory.Name);
                datasetFileInfo.FileExtension = datasetDirectory.Extension;



                ProcessRawDirectory(datasetDirectory, datasetFileInfo, out var primaryDataFiles);



        private void ProcessRawDirectory(DirectoryInfo datasetDirectory, clsDatasetFileInfo datasetFileInfo, out List<FileInfo> primaryDataFiles)
        {

            primaryDataFiles = new List<FileInfo>();

            // Sum up the sizes of all of the files in this directory
            datasetFileInfo.FileSizeBytes = 0;

            var fileCount = 0;
            foreach (var item in datasetDirectory.GetFiles())
            {
                datasetFileInfo.FileSizeBytes += item.Length;

                if (fileCount == 0)
                {
                    // Assign the first file's modification time to .AcqTimeStart and .AcqTimeEnd
                    // Necessary in case _header.txt is missing
                    datasetFileInfo.AcqTimeStart = item.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = item.LastWriteTime;
                }

                if (item.Name.ToLower() == "_header.txt")
                {
                    // Assign the file's modification time to .AcqTimeStart and .AcqTimeEnd
                    // These will get updated below to more precise values
                    datasetFileInfo.AcqTimeStart = item.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = item.LastWriteTime;
                }

                if (item.Extension.ToLower().Equals(".dat"))
                {
                    primaryDataFiles.Add(item);
                }

                fileCount += 1;
            }

            var nativeFileIO = new clsMassLynxNativeIO();

            if (nativeFileIO.GetFileInfo(datasetDirectory.FullName, out var udtHeaderInfo))
            {
                var newStartDate = DateTime.Parse(udtHeaderInfo.AcquDate + " " + udtHeaderInfo.AcquTime);

                var functionCount = nativeFileIO.GetFunctionCount(datasetDirectory.FullName);

                if (functionCount > 0)
                {
                    // Sum up the scan count of all of the functions
                    // Additionally, find the largest EndRT value in all of the functions
                    float endRT = 0;
                    for (var functionNumber = 1; functionNumber <= functionCount; functionNumber++)
                    {
                        if (nativeFileIO.GetFunctionInfo(datasetDirectory.FullName, 1, out MassLynxData.MSFunctionInfo functionInfo))
                        {
                            datasetFileInfo.ScanCount += functionInfo.ScanCount;
                            if (functionInfo.EndRT > endRT)
                            {
                                endRT = functionInfo.EndRT;
                            }
                        }
                    }

                    if (newStartDate >= MINIMUM_ACCEPTABLE_ACQ_START_TIME)
                    {
                        datasetFileInfo.AcqTimeStart = newStartDate;

                        if (endRT > 0)
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.Add(MinutesToTimeSpan(endRT));
                        }
                        else
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        }
                    }
                    else
                    {
                        // Keep .AcqTimeEnd as the file modification date
                        // Set .AcqTimeStart based on .AcqEndTime
                        if (endRT > 0)
                        {
                            datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.Subtract(MinutesToTimeSpan(endRT));
                        }
                        else
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart;
                        }
                    }
                }
                else
                {
                    if (newStartDate >= MINIMUM_ACCEPTABLE_ACQ_START_TIME)
                    {
                        datasetFileInfo.AcqTimeStart = newStartDate;
                    }
                }

            }
            else
            {
                // Error getting the header info using clsMassLynxNativeIO
                // Continue anyway since we've populated some of the values
            }
        }
    }
}
