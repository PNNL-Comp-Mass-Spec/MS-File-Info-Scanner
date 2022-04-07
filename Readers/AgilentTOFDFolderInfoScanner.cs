using System;
using System.Collections.Generic;
using System.IO;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Agilent TOF .D folder Info Scanner; Uses ProteoWizard to read files
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
    /// </remarks>
    // ReSharper disable once IdentifierTypo
    public class AgilentTOFDFolderInfoScanner : MSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: AcqData, AcqTime

        // Note: The extension must be in all caps
        public const string AGILENT_DATA_FOLDER_D_EXTENSION = ".D";

        // ReSharper disable once IdentifierTypo
        public const string AGILENT_ACQDATA_FOLDER_NAME = "AcqData";
        public const string AGILENT_MS_PEAK_FILE = "MSPeak.bin";

        // ReSharper disable once IdentifierTypo
        public const string AGILENT_MS_PEAK_PERIODIC_ACTUALS_FILE = "MSPeriodicActuals.bin";
        public const string AGILENT_MS_PROFILE_FILE = "MSProfile.bin";
        public const string AGILENT_MS_SCAN_FILE = "MSScan.bin";
        public const string AGILENT_XML_CONTENTS_FILE = "Contents.xml";

        public const string AGILENT_TIME_SEGMENT_FILE = "MSTS.xml";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="lcms2DPlotOptions"></param>
        // ReSharper disable once IdentifierTypo
        public AgilentTOFDFolderInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        { }

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            // The dataset name is simply the directory name without .D
            try
            {
                return Path.GetFileNameWithoutExtension(dataFilePath);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads the Contents.xml file to look for the AcquiredTime entry
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if the file exists and the AcquiredTime entry was successfully parsed; otherwise false</returns>
        private bool ProcessContentsXMLFile(string directoryPath, DatasetFileInfo datasetFileInfo)
        {
            var success = false;

            try
            {
                // Open the Contents.xml file
                var filePath = Path.Combine(directoryPath, AGILENT_XML_CONTENTS_FILE);

                using var reader = new System.Xml.XmlTextReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EOF)
                {
                    reader.Read();

                    switch (reader.NodeType)
                    {
                        case System.Xml.XmlNodeType.Element:

                            if (reader.Name == "AcquiredTime")
                            {
                                try
                                {
                                    var acquisitionStartTime = reader.ReadElementContentAsDateTime();

                                    // Convert from Universal time to Local time
                                    var acquisitionTime = acquisitionStartTime.ToLocalTime();

                                    // ReSharper disable CommentTypo

                                    // There have been some cases where the acquisition start time is several years before the file modification time,
                                    // for example XG_A83CapiHSSWash1.d where the time in the Contents.xml file is 3/20/2005 while the file modification time is 2010
                                    // Thus, we use a sanity check of a maximum run time of 24 hours

                                    // ReSharper restore CommentTypo

                                    if (datasetFileInfo.AcqTimeEnd.Subtract(acquisitionTime).TotalDays < 1)
                                    {
                                        datasetFileInfo.AcqTimeStart = acquisitionStartTime.ToLocalTime();
                                        success = true;
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore errors here
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent(string.Format("Exception reading {0}: {1}", AGILENT_XML_CONTENTS_FILE, ex.Message), ex);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Reads the MSTS.xml file to determine the acquisition length and the number of scans
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <param name="totalAcqTimeMinutes"></param>
        private bool ProcessTimeSegmentFile(string directoryPath, DatasetFileInfo datasetFileInfo, out double totalAcqTimeMinutes)
        {
            var success = false;

            double startTime = 0;
            double endTime = 0;

            totalAcqTimeMinutes = 0;

            try
            {
                datasetFileInfo.ScanCount = 0;

                // Open the Contents.xml file
                var filePath = Path.Combine(directoryPath, AGILENT_TIME_SEGMENT_FILE);

                using var reader = new System.Xml.XmlTextReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EOF)
                {
                    reader.Read();

                    switch (reader.NodeType)
                    {
                        case System.Xml.XmlNodeType.Element:
                            switch (reader.Name)
                            {
                                case "TimeSegment":
                                    startTime = 0;
                                    endTime = 0;
                                    break;

                                case "StartTime":
                                    startTime = reader.ReadElementContentAsDouble();
                                    break;

                                case "EndTime":
                                    endTime = reader.ReadElementContentAsDouble();
                                    break;

                                case "NumOfScans":
                                    datasetFileInfo.ScanCount += reader.ReadElementContentAsInt();
                                    success = true;
                                    break;

                                default:
                                    // Ignore it
                                    break;
                            }
                            break;

                        case System.Xml.XmlNodeType.EndElement:
                            if (reader.Name == "TimeSegment")
                            {
                                // Store the AcqTime for this time segment

                                if (endTime > startTime)
                                {
                                    success = true;
                                    totalAcqTimeMinutes += endTime - startTime;
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent(string.Format("Exception reading {0}: {1}", AGILENT_TIME_SEGMENT_FILE, ex.Message), ex);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            var success = false;

            ResetResults();

            try
            {
                var rootDirectory = MSFileInfoScanner.GetDirectoryInfo(dataFilePath);
                var acqDataDirectory = MSFileInfoScanner.GetDirectoryInfo(Path.Combine(rootDirectory.FullName, AGILENT_ACQDATA_FOLDER_NAME));

                datasetFileInfo.FileSystemCreationTime = acqDataDirectory.CreationTime;
                datasetFileInfo.FileSystemModificationTime = acqDataDirectory.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(rootDirectory.Name);
                datasetFileInfo.FileExtension = rootDirectory.Extension;
                datasetFileInfo.FileSizeBytes = 0;
                datasetFileInfo.ScanCount = 0;

                if (acqDataDirectory.Exists)
                {
                    // Sum up the sizes of all of the files in the AcqData directory
                    foreach (var file in PathUtils.FindFilesWildcard(acqDataDirectory, "*", true))
                    {
                        datasetFileInfo.FileSizeBytes += file.Length;
                    }

                    // Look for the MSScan.bin file
                    // Use its modification time to get an initial estimate for the acquisition end time
                    var msScanFile = MSFileInfoScanner.GetFileInfo(Path.Combine(acqDataDirectory.FullName, AGILENT_MS_SCAN_FILE));

                    bool primaryFileAdded;

                    if (msScanFile.Exists)
                    {
                        datasetFileInfo.AcqTimeStart = msScanFile.LastWriteTime;
                        datasetFileInfo.AcqTimeEnd = msScanFile.LastWriteTime;

                        // Read the file info from the file system
                        // Several of these stats will be further updated later
                        // This will also compute the SHA-1 hash of the MSScan.bin file and add it to mDatasetStatsSummarizer.DatasetFileInfo
                        UpdateDatasetFileStats(msScanFile, datasetFileInfo.DatasetID, out primaryFileAdded);
                    }
                    else
                    {
                        // Read the file info from the file system
                        // Several of these stats will be further updated later
                        UpdateDatasetFileStats(acqDataDirectory, msScanFile, datasetFileInfo.DatasetID);
                        primaryFileAdded = false;
                    }

                    var additionalFilesToHash = new List<FileInfo>
                    {
                        new(Path.Combine(acqDataDirectory.FullName, AGILENT_MS_PEAK_FILE)),
                        new(Path.Combine(acqDataDirectory.FullName, AGILENT_MS_PEAK_PERIODIC_ACTUALS_FILE)),
                        new(Path.Combine(acqDataDirectory.FullName, AGILENT_MS_PROFILE_FILE)),
                        new(Path.Combine(acqDataDirectory.FullName, AGILENT_XML_CONTENTS_FILE))
                    };

                    foreach (var addnlFile in additionalFilesToHash)
                    {
                        if (!addnlFile.Exists)
                            continue;

                        if (Options.DisableInstrumentHash)
                        {
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFileNoHash(addnlFile);
                        }
                        else
                        {
                            mDatasetStatsSummarizer.DatasetFileInfo.AddInstrumentFile(addnlFile);
                        }
                        primaryFileAdded = true;
                    }

                    if (!primaryFileAdded)
                    {
                        // Add largest file in the AcqData directory
                        AddLargestInstrumentFile(acqDataDirectory);
                    }

                    success = true;
                }

                if (success)
                {
                    // The AcqData directory exists

                    // Parse the Contents.xml file to determine the acquisition start time
                    var acqStartTimeDetermined = ProcessContentsXMLFile(acqDataDirectory.FullName, datasetFileInfo);

                    // Parse the MSTS.xml file to determine the acquisition length and number of scans
                    var validMSTS = ProcessTimeSegmentFile(acqDataDirectory.FullName, datasetFileInfo, out var acquisitionLengthMinutes);

                    if (!acqStartTimeDetermined && validMSTS)
                    {
                        // Compute the start time from .AcqTimeEnd minus acquisitionLengthMinutes
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-acquisitionLengthMinutes);
                    }

                    // Note: could parse the AcqMethod.xml file to determine if MS2 spectra are present
                    //<AcqMethod>
                    //	<QTOF>
                    //		<TimeSegment>
                    //	      <Acquisition>
                    //	        <AcqMode>TargetedMS2</AcqMode>

                    // Read the raw data to create the TIC and BPI
                    ReadBinaryData(rootDirectory.FullName, datasetFileInfo, acquisitionLengthMinutes);
                }

                if (success)
                {
                    // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Exception parsing Agilent TOF .D directory: {0}", ex.Message), ex);
                success = false;
            }

            PostProcessTasks();

            return success;
        }

        private void ReadBinaryData(string dataDirectoryPath, DatasetFileInfo datasetFileInfo, double acquisitionLengthMinutes)
        {
            try
            {
                // Open the data directory using the ProteoWizardWrapper

                var msDataFileReader = new pwiz.ProteowizardWrapper.MSDataFileReader(dataDirectoryPath);

                try
                {
                    var runStartTime = Convert.ToDateTime(msDataFileReader.RunStartTime);

                    // Update AcqTimeEnd if possible
                    if (runStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                        {
                            datasetFileInfo.AcqTimeStart = runStartTime;
                            if (acquisitionLengthMinutes > 0)
                            {
                                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(acquisitionLengthMinutes);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Leave the times unchanged
                }

                // Instantiate the ProteoWizard Data Parser class
                var pWizParser = new ProteoWizardDataParser(msDataFileReader, mDatasetStatsSummarizer, mTICAndBPIPlot,
                                                            mLCMS2DPlot, Options.SaveLCMS2DPlots, Options.SaveTICAndBPIPlots,
                                                            Options.CheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                RegisterEvents(pWizParser);

                var ticStored = false;
                double runtimeMinutes = 0;

                if (msDataFileReader.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out _, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                if (msDataFileReader.SpectrumCount > 0)
                {
                    // Process the spectral data
                    var skipExistingScans = (msDataFileReader.ChromatogramCount > 0);

                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans,
                                                  skipScansWithNoIons: true,
                                                  maxScansToTrackInDetail: MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                msDataFileReader.Dispose();
                ProgRunner.GarbageCollectNow();
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Exception reading the Binary Data in the Agilent TOF .D directory using ProteoWizard: {0}", ex.Message), ex);
            }
        }
    }
}
