using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;
using pwiz.ProteowizardWrapper;
using SpectraTypeClassifier;
using ThermoRawFileReader;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// .mzML file info scanner
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2021</remarks>
    public class MzMLFileInfoScanner : ProteoWizardScanner
    {
        // Ignore Spelling: centroided, xcalibur

        // Note: The extension must be in all caps
        public const string MZML_FILE_EXTENSION = ".MZML";

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public MzMLFileInfoScanner() : this(new InfoScannerOptions(), new LCMSDataPlotterOptions())
        { }

        /// <summary>
        /// Constructor
        /// </summary>
        public MzMLFileInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        {
        }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath"></param>
        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            try
            {
                // The dataset name is simply the file name without .mzML
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error or if the file has no scans</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            // Obtain the full path to the file
            var dataFile = new FileInfo(dataFilePath);

            if (!dataFile.Exists)
            {
                OnErrorEvent(".mzML file not found: " + dataFilePath);
                return false;
            }

            // Future, optional: Determine the DatasetID
            // Unfortunately, this is not present in metadata.txt
            // datasetID = LookupDatasetID(datasetName)
            var datasetID = Options.DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = dataFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = dataFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = datasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(dataFile.Name);
            datasetFileInfo.FileExtension = dataFile.Extension;
            datasetFileInfo.FileSizeBytes = dataFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            var success = ProcessWithProteoWizard(dataFile, datasetFileInfo);

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the .mzML file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(dataFile, datasetID);

            // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

            PostProcessTasks();

            return success;
        }
    }
}