using System;
using System.Collections.Generic;
using System.IO;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.MassLynxData;
using MSFileInfoScannerInterfaces;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Class for reading data from Waters mass spectrometers (previously Micromass)
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005
    /// </remarks>
    public class WatersRawFolderInfoScanner : MSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: Micromass, Synapt

        // Note: The extension must be in all caps
        public const string MICROMASS_RAW_FOLDER_EXTENSION = ".RAW";

        private readonly DateTime MINIMUM_ACCEPTABLE_ACQ_START_TIME = new(1975, 1, 1);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Processing options</param>
        /// <param name="lcms2DPlotOptions">Plotting options</param>
        public WatersRawFolderInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        { }

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
        /// <param name="dataFilePath">Data file path</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            try
            {
                var datasetDirectory = MSFileInfoScanner.GetDirectoryInfo(dataFilePath);
                datasetFileInfo.FileSystemCreationTime = datasetDirectory.CreationTime;
                datasetFileInfo.FileSystemModificationTime = datasetDirectory.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetDirectory.Name);
                datasetFileInfo.FileExtension = datasetDirectory.Extension;

                datasetFileInfo.ScanCount = 0;

                mDatasetStatsSummarizer.ClearCachedData();
                mLCMS2DPlot.Options.UseObservedMinScan = false;

                ProcessRawDirectory(datasetDirectory, datasetFileInfo, out var primaryDataFiles);

                // Read the file info from the file system
                // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all the necessary steps are taken)
                // This will also add the primary data files to mDatasetStatsSummarizer.DatasetFileInfo
                // The SHA-1 hash of the first file in primaryDataFiles will also be computed
                UpdateDatasetFileStats(datasetDirectory, primaryDataFiles, datasetFileInfo.DatasetID);

                // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

                PostProcessTasks();

                return true;
            }
            catch (Exception)
            {
                PostProcessTasks();
                return false;
            }
        }

        private void ProcessRawDirectory(DirectoryInfo datasetDirectory, DatasetFileInfo datasetFileInfo, out List<FileInfo> primaryDataFiles)
        {
            primaryDataFiles = new List<FileInfo>();

            // Sum up the sizes of all the files in this directory
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

                if (item.Name.Equals("_header.txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Assign the file's modification time to .AcqTimeStart and .AcqTimeEnd
                    // These will get updated below to more precise values
                    datasetFileInfo.AcqTimeStart = item.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = item.LastWriteTime;
                }

                if (item.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    primaryDataFiles.Add(item);
                }

                fileCount++;
            }

            var nativeFileIO = new MassLynxNativeIO();

            if (nativeFileIO.GetFileInfo(datasetDirectory.FullName, out var headerInfo))
            {
                ReadMassLynxAcquisitionInfo(datasetDirectory, datasetFileInfo, nativeFileIO, headerInfo);
            }
            else
            {
                // Error getting the header info using MassLynxNativeIO
                // Continue anyway since we've populated some of the values
            }

            LoadScanDataWithProteoWizard(datasetDirectory, datasetFileInfo, true);
        }

        /// <summary>
        /// Reads the acquisition date and time from the .raw directory
        /// Also determines the total number of scans
        /// </summary>
        /// <param name="datasetDirectory">Dataset directory</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <param name="nativeFileIO">MassLynx data file reader class</param>
        /// <param name="headerInfo">MS header info</param>
        private void ReadMassLynxAcquisitionInfo(
            FileSystemInfo datasetDirectory,
            DatasetFileInfo datasetFileInfo,
            MassLynxNativeIO nativeFileIO,
            MSHeaderInfo headerInfo)
        {
            var newStartDate = DateTime.Parse(headerInfo.AcquDate + " " + headerInfo.AcquTime);

            var functionCount = nativeFileIO.GetFunctionCount(datasetDirectory.FullName);

            if (functionCount > 0)
            {
                // Sum up the scan count of all of the functions
                // Additionally, find the largest EndRT value in the functions
                float endRT = 0;

                for (var functionNumber = 1; functionNumber <= functionCount; functionNumber++)
                {
                    if (nativeFileIO.GetFunctionInfo(datasetDirectory.FullName, 1, out MassLynxData.MSFunctionInfo functionInfo))
                    {
                        datasetFileInfo.ScanCount += functionInfo.ScanCount;

                        // ReSharper disable once CommentTypo
                        // Synapt dataset 20191218_DV_Nglycan_P28_enzyme reports 1E+07 for functionInfo.EndRT (acquisition length)

                        // Only update endRT if the acquisition length is less than 7 days

                        if (functionInfo.EndRT > endRT && MinutesToTimeSpan(functionInfo.EndRT).TotalDays < 7)
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
    }
}
