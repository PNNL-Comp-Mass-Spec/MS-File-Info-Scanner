using System;
using System.IO;
using PRISM;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
//

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsAgilentTOFDFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string AGILENT_DATA_FOLDER_D_EXTENSION = ".D";
        public const string AGILENT_ACQDATA_FOLDER_NAME = "AcqData";
        public const string AGILENT_MS_SCAN_FILE = "MSScan.bin";
        public const string AGILENT_XML_CONTENTS_FILE = "Contents.xml";

        public const string AGILENT_TIME_SEGMENT_FILE = "MSTS.xml";
       
        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            // The dataset name is simply the folder name without .D
            try {
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            } catch (Exception) {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads the Contents.xml file to look for the AcquiredTime entry
        /// </summary>
        /// <param name="strFolderPath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if the file exists and the AcquiredTime entry was successfully parsed; otherwise false</returns>
        /// <remarks></remarks>
        private bool ProcessContentsXMLFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo)
        {
            var blnSuccess = false;

            try {

                // Open the Contents.xml file
                var strFilePath = Path.Combine(strFolderPath, AGILENT_XML_CONTENTS_FILE);

                using (var srReader = new System.Xml.XmlTextReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

                    while (!srReader.EOF) {
                        srReader.Read();

                        switch (srReader.NodeType) {
                            case System.Xml.XmlNodeType.Element:

                                if (srReader.Name == "AcquiredTime") {
                                    try {
                                        var dtAcquisitionStartTime = srReader.ReadElementContentAsDateTime();

                                        // Convert from Universal time to Local time
                                        var dtAcquisitionTime = dtAcquisitionStartTime.ToLocalTime();

                                        // There have been some cases where the acquisition start time is several years before the file modification time, 
                                        // for example XG_A83CapiHSSWash1.d where the time in the Contents.xml file is 3/20/2005 while the file modification time is 2010
                                        // Thus, we use a sanity check of a maximum run time of 24 hours

                                        if (datasetFileInfo.AcqTimeEnd.Subtract(dtAcquisitionTime).TotalDays < 1) {
                                            datasetFileInfo.AcqTimeStart = dtAcquisitionStartTime.ToLocalTime();
                                            blnSuccess = true;
                                        }

                                    } catch (Exception) {
                                        // Ignore errors here
                                    }

                                }

                                break;
                        }

                    }

                }


            } catch (Exception ex) {
                // Exception reading file
                OnErrorEvent("Exception reading " + AGILENT_XML_CONTENTS_FILE + ": " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        /// <summary>
        /// Reads the MSTS.xml file to determine the acquisition length and the number of scans
        /// </summary>
        /// <param name="strFolderPath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <param name="dblTotalAcqTimeMinutes"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private bool ProcessTimeSegmentFile(string strFolderPath, clsDatasetFileInfo datasetFileInfo, out double dblTotalAcqTimeMinutes)
        {

            var blnSuccess = false;

            double dblStartTime = 0;
            double dblEndTime = 0;

            dblTotalAcqTimeMinutes = 0;

            try
            {
                datasetFileInfo.ScanCount = 0;

                // Open the Contents.xml file
                var strFilePath = Path.Combine(strFolderPath, AGILENT_TIME_SEGMENT_FILE);

                using (var srReader = new System.Xml.XmlTextReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

                    while (!srReader.EOF) {
                        srReader.Read();

                        switch (srReader.NodeType) {
                            case System.Xml.XmlNodeType.Element:
                                switch (srReader.Name) {
                                    case "TimeSegment":
                                        dblStartTime = 0;
                                        dblEndTime = 0;

                                        break;
                                    case "StartTime":
                                        dblStartTime = srReader.ReadElementContentAsDouble();

                                        break;
                                    case "EndTime":
                                        dblEndTime = srReader.ReadElementContentAsDouble();

                                        break;
                                    case "NumOfScans":
                                        datasetFileInfo.ScanCount += srReader.ReadElementContentAsInt();
                                        blnSuccess = true;

                                        break;
                                    default:
                                        break;
                                    // Ignore it
                                }

                                break;
                            case System.Xml.XmlNodeType.EndElement:
                                if (srReader.Name == "TimeSegment") {
                                    // Store the acqtime for this time segment

                                    if (dblEndTime > dblStartTime) {
                                        blnSuccess = true;
                                        dblTotalAcqTimeMinutes += (dblEndTime - dblStartTime);
                                    }

                                }

                                break;
                        }

                    }

                }

            } catch (Exception ex) {
                // Exception reading file
                OnErrorEvent("Exception reading " + AGILENT_TIME_SEGMENT_FILE + ": " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            var blnSuccess = false;

            try {
                var diRootFolder = new DirectoryInfo(strDataFilePath);
                var diAcqDataFolder = new DirectoryInfo(Path.Combine(diRootFolder.FullName, AGILENT_ACQDATA_FOLDER_NAME));

                datasetFileInfo.FileSystemCreationTime = diAcqDataFolder.CreationTime;
                datasetFileInfo.FileSystemModificationTime = diAcqDataFolder.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(diRootFolder.Name);
                datasetFileInfo.FileExtension = diRootFolder.Extension;
                datasetFileInfo.FileSizeBytes = 0;
                datasetFileInfo.ScanCount = 0;

                if (diAcqDataFolder.Exists) {
                    // Sum up the sizes of all of the files in the AcqData folder
                    foreach (var fiFile in diAcqDataFolder.GetFiles("*", SearchOption.AllDirectories)) {
                        datasetFileInfo.FileSizeBytes += fiFile.Length;
                    }

                    // Look for the MSScan.bin file
                    // Use its modification time to get an initial estimate for the acquisition end time
                    var fiMSScanfile = new FileInfo(Path.Combine(diAcqDataFolder.FullName, AGILENT_MS_SCAN_FILE));

                    if (fiMSScanfile.Exists) {
                        datasetFileInfo.AcqTimeStart = fiMSScanfile.LastWriteTime;
                        datasetFileInfo.AcqTimeEnd = fiMSScanfile.LastWriteTime;

                        // Read the file info from the file system
                        // Several of these stats will be further updated later
                        UpdateDatasetFileStats(fiMSScanfile, datasetFileInfo.DatasetID);
                    } else {
                        // Read the file info from the file system
                        // Several of these stats will be further updated later
                        UpdateDatasetFileStats(diAcqDataFolder, datasetFileInfo.DatasetID);
                    }

                    blnSuccess = true;
                }


                if (blnSuccess) {
                    // The AcqData folder exists

                    // Parse the Contents.xml file to determine the acquisition start time
                    var blnAcqStartTimeDetermined = ProcessContentsXMLFile(diAcqDataFolder.FullName, datasetFileInfo);

                    double dblAcquisitionLengthMinutes;

                    // Parse the MSTS.xml file to determine the acquisition length and number of scans
                    var blnValidMSTS = ProcessTimeSegmentFile(diAcqDataFolder.FullName, datasetFileInfo, out dblAcquisitionLengthMinutes);

                    if (!blnAcqStartTimeDetermined && blnValidMSTS) {
                        // Compute the start time from .AcqTimeEnd minus dblAcquisitionLengthMinutes
                        datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-dblAcquisitionLengthMinutes);
                    }

                    // Note: could parse the AcqMethod.xml file to determine if MS2 spectra are present
                    //<AcqMethod>
                    //	<QTOF>
                    //		<TimeSegment>
                    //	      <Acquisition>
                    //	        <AcqMode>TargetedMS2</AcqMode>

                    // Read the raw data to create the TIC and BPI
                    ReadBinaryData(diRootFolder.FullName, datasetFileInfo);

                }


                if (blnSuccess) {
                    // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                }


            } catch (Exception ex) {
                OnErrorEvent("Exception parsing Agilent TOF .D folder: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private bool ReadBinaryData(string strDataFolderPath, clsDatasetFileInfo datasetFileInfo)
        {

            bool blnSuccess;

            try {
                // Open the data folder using the ProteoWizardWrapper

                var objPWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(strDataFolderPath);

                try {
                    var dtRunStartTime = Convert.ToDateTime(objPWiz.RunStartTime);

                    // Update AcqTimeEnd if possible
                    if (dtRunStartTime < datasetFileInfo.AcqTimeEnd) {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(dtRunStartTime).TotalDays < 1) {
                            datasetFileInfo.AcqTimeStart = dtRunStartTime;
                        }
                    }

                } catch (Exception) {
                    // Leave the times unchanged
                }

                // Instantiate the Proteowizard Data Parser class
                var pWizParser = new clsProteowizardDataParser(objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot,
                                                            mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI,
                                                            mCheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                RegisterEvents(pWizParser);

                var blnTICStored = false;
                double dblRuntimeMinutes = 0;

                if (objPWiz.ChromatogramCount > 0) {
                    // Process the chromatograms
                    bool blnSRMDataCached;
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out blnTICStored, out blnSRMDataCached, out dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);

                }

                if (objPWiz.SpectrumCount > 0) {
                    // Process the spectral data
                    pWizParser.StoreMSSpectraInfo(datasetFileInfo, blnTICStored, ref dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);
                }

                objPWiz.Dispose();
                clsProgRunner.GarbageCollectNow();

                blnSuccess = true;

            } catch (Exception ex) {
                OnErrorEvent("Exception reading the Binary Data in the Agilent TOF .D folder using Proteowizard: " + ex.Message, ex);
                blnSuccess = false;
            }

            return blnSuccess;
        }


    }
}
