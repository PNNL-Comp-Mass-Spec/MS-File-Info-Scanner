using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using Agilent.MassSpectrometry.DataAnalysis;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Plotting;
using MSFileInfoScannerInterfaces;
using PRISM;
using Device = ThermoFisher.CommonCore.Data.Business.Device;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// Agilent MassHunter .D folder Info Scanner (MassHunter is used for TOF/QTOF/IM-QTOF/QQQ instruments, and always contain a "AcqData" folder)
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012</remarks>
    // ReSharper disable once IdentifierTypo
    public class AgilentMassHunterDFolderInfoScanner : MSFileInfoProcessorBaseClass
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
        public const string AGILENT_IMS_FRAME_METHOD_FILE = "IMSFrameMeth.xml";
        public const string AGILENT_IMS_FRAME_BIN_FILE = "IMSFrame.bin";

        public const string AGILENT_TIME_SEGMENT_FILE = "MSTS.xml";

        private bool mIsImsData = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="lcms2DPlotOptions"></param>
        // ReSharper disable once IdentifierTypo
        public AgilentMassHunterDFolderInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
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
                OnErrorEvent("Exception reading " + AGILENT_XML_CONTENTS_FILE + ": " + ex.Message, ex);
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
                OnErrorEvent("Exception reading " + AGILENT_TIME_SEGMENT_FILE + ": " + ex.Message, ex);
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

                    mIsImsData = File.Exists(Path.Combine(acqDataDirectory.FullName, AGILENT_IMS_FRAME_METHOD_FILE)) ||
                                 File.Exists(Path.Combine(acqDataDirectory.FullName, AGILENT_IMS_FRAME_BIN_FILE));

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

                    if (mIsImsData)
                    {
                        additionalFilesToHash.Add(new(Path.Combine(acqDataDirectory.FullName, AGILENT_IMS_FRAME_METHOD_FILE)));
                        additionalFilesToHash.Add(new(Path.Combine(acqDataDirectory.FullName, AGILENT_IMS_FRAME_BIN_FILE)));
                    }

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
                OnErrorEvent("Exception parsing Agilent TOF .D directory: " + ex.Message, ex);
                success = false;
            }

            PostProcessTasks();

            return success;
        }

        private void ReadBinaryData(string dataDirectoryPath, DatasetFileInfo datasetFileInfo, double acquisitionLengthMinutes)
        {
            TICandBPIPlotter pressurePlot = null;
            mInstrumentSpecificPlots.Clear();

            if (Options.SaveTICAndBPIPlots)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();

                if (mInstrumentSpecificPlots.Count == 0 && mIsImsData)
                {
                    mTICAndBPIPlot.BPIXAxisLabel = "Frame number";
                    mTICAndBPIPlot.TICXAxisLabel = "Frame number";

                    AddInstrumentSpecificPlot("Drift tube Pressure");

                    // Track drift tube pressures using mTIC in pressurePlot
                    pressurePlot = mInstrumentSpecificPlots.First();
                    pressurePlot.DeviceType = Device.Analog;

                    pressurePlot.BPIXAxisLabel = "Frame number";
                    pressurePlot.TICXAxisLabel = "Frame number";

                    pressurePlot.TICYAxisLabel = "Pressure";
                    pressurePlot.TICYAxisExponentialNotation = false;

                    pressurePlot.TICPlotAbbrev = "Pressure";
                    pressurePlot.TICAutoMinMaxY = true;
                    pressurePlot.RemoveZeroesFromEnds = true;
                }
            }

            pressurePlot ??= new TICandBPIPlotter();

            if (Options.SaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            try
            {
                // Open the data directory using the ProteoWizardWrapper

                var massSpecDataReader = (IMsdrDataReader) new MassSpecDataReader();
                var open = massSpecDataReader.OpenDataFile(dataDirectoryPath);
                // TODO: Check value of "open"!

                try
                {
                    var runStartTime = massSpecDataReader.FileInformation.AcquisitionTime;

                    // Update AcqTimeEnd if possible
                    if (runStartTime < datasetFileInfo.AcqTimeEnd && datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                    {
                        datasetFileInfo.AcqTimeStart = runStartTime;
                        if (acquisitionLengthMinutes > 0)
                        {
                            datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(acquisitionLengthMinutes);
                        }
                    }
                }
                catch (Exception)
                {
                    // Leave the times unchanged
                }

                var driftTimeMsec = -1.0;
                if (mIsImsData)
                {
                    // The only way to get this is with MIDAC, or else parsing IMSFrameMeth.xml.
                    // For IMS files, we only store one entry per frame
                    // Use the median drift time as the representative drift time
                    // Only get this once, and only worry about the first value;
                    //   the instruments thus far do not support varying the drift time period or number of drift scans per frame

                    try
                    {
                        var document = new XmlDocument();
                        document.Load(Path.Combine(dataDirectoryPath, AGILENT_ACQDATA_FOLDER_NAME, AGILENT_IMS_FRAME_METHOD_FILE));
                        var manager = new XmlNamespaceManager(document.NameTable);
                        var query = "FrameMethods/FrameMethod/FrameDtPeriod";
                        var query2 = "FrameMethods/FrameMethod/MaxMsPerFrame";
                        var dtPeriodString = document.SelectNodes(query, manager)?[0].InnerText;
                        var maxMsString = document.SelectNodes(query2, manager)?[0].InnerText;

                        if (double.TryParse(dtPeriodString, out var dtPeriod) &&
                            double.TryParse(maxMsString, out var maxMs))
                        {
                            var middleDriftScan = (maxMs - 1) / 2;

                            driftTimeMsec = middleDriftScan * dtPeriod;
                        }
                    }
                    catch
                    {
                        // Do nothing
                    }
                }

                var scanCount = massSpecDataReader.MSScanFileInformation.TotalScansPresent;
                var scanStats = new ScanStatsEntry[scanCount];
                var scanMapper = new double[scanCount];
                var hasMS1 = false;
                var hasMSn = false;
                for (var i = 0; i < scanCount; i++)
                {
                    // NOTE: This does not extract any drift scans for IM-QTOF files.
                    var scan = massSpecDataReader.GetScanRecord(i);

                    // Array of scan index->retention time for binary search with chromatograms
                    scanMapper[i] = scan.RetentionTime;

                    var scanNumber = scan.ScanID;
                    if (mIsImsData)
                    {
                        scanNumber = i + 1;
                    }

                    // TODO: Midac allows access to the frame-level "SpectrumDetails.FragmentationClass == FragmentationClass.HighEnergy"
                    // Here, just check for a collision energy above '0'
                    // TODO: How to properly handle this for HiLo QTOF data?
                    var msLevel = scan.MSLevel == MSLevel.MSMS || scan.CollisionEnergy > 0 ? 2 : 1;

                    if (msLevel == 1)
                    {
                        hasMS1 = true;
                    }
                    else
                    {
                        hasMSn = true;
                    }

                    var scanStatsEntry = new ScanStatsEntry
                    {
                        ScanNumber = scanNumber,
                        ScanType = msLevel,
                    };

                    if (scanStatsEntry.ScanType > 1)
                    {
                        scanStatsEntry.ScanTypeName = "HMSn";
                    }
                    else
                    {
                        scanStatsEntry.ScanTypeName = "HMS";
                    }

                    if (mIsImsData)
                    {
                        scanStatsEntry.ScanFilterText = "IMS";
                    }

                    scanStatsEntry.ExtendedScanInfo.ScanFilterText = scanStatsEntry.ScanFilterText;

                    scanStatsEntry.ElutionTime = scan.RetentionTime.ToString("0.0###");

                    if (!mIsImsData)
                    {
                        // Just for parity with the ProteoWizard-using TofD file code
                        scanStatsEntry.ExtendedScanInfo.IonInjectionTime = "0";
                    }

                    if (scan.Tic > 0)
                    {
                        scanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(scan.Tic, 5);
                    }
                    else
                    {
                        scanStatsEntry.TotalIonIntensity = "0";
                    }

                    if (scan.BasePeakIntensity > 0)
                    {
                        scanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(scan.BasePeakIntensity, 5);
                        scanStatsEntry.BasePeakMZ = StringUtilities.DblToString(scan.BasePeakMZ, 2);
                    }
                    else
                    {
                        scanStatsEntry.BasePeakIntensity = "0";
                        scanStatsEntry.BasePeakMZ = "0";
                    }

                    // Base peak signal to noise ratio
                    scanStatsEntry.BasePeakSignalToNoiseRatio = "0";

                    // NOTE: These are set when spectra are read
                    scanStatsEntry.IonCount = 0;
                    scanStatsEntry.IonCountRaw = 0;

                    if (mIsImsData && driftTimeMsec > 0)
                    {
                        // The only way to get this is with MIDAC, or else parsing IMSFrameMeth.xml.
                        // For IMS files, we only store one entry per frame
                        // Use the median drift time as the representative drift time
                        scanStatsEntry.DriftTimeMsec = driftTimeMsec.ToString("0.0###");
                    }

                    mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);
                    scanStats[i] = scanStatsEntry;
                }

                // Assumption: if we have both MS1 and MSn data, then we shouldn't sum "cycles" (frame or scan sets) in the chromatograms
                // This may not be 100% correct, but it helps with data output
                var chromatogramSumCycles = !(hasMS1 && hasMSn);

                GetDeviceInfo(massSpecDataReader);

                ReadChromatograms(massSpecDataReader, scanMapper, scanStats, chromatogramSumCycles);

                ReadSpectra(massSpecDataReader, scanStats);

                if (Options.SaveTICAndBPIPlots && mIsImsData && massSpecDataReader.ActualsInformation.IsActualsPresent())
                {
                    var names = massSpecDataReader.ActualsInformation.GetActualNames();
                    if (names.Contains("Drift Tube Pressure"))
                    {
                        massSpecDataReader.ActualsInformation.GetActualValue("Drift Tube Pressure", out var xArray, out var yArray);

                        var lastScanIndex = -1;
                        var lastValue = -1.0;

                        for (var i = 0; i < xArray.Length; i++)
                        {
                            var time = xArray[i];
                            var scanIndex = Array.BinarySearch(scanMapper, time);
                            if (scanIndex < 0)
                            {
                                scanIndex = ~scanIndex;
                                if (scanIndex > 0 && time - scanMapper[scanIndex - 1] < scanMapper[scanIndex] - time)
                                    --scanIndex;
                            }

                            // Square plot: Actuals only reports values when they change, make a boxy plot
                            if (lastScanIndex >= 0 && scanIndex - 1 > lastScanIndex)
                            {
                                var previousScanNumber = scanStats[scanIndex - 1].ScanNumber;
                                var previousMsLevel = scanStats[scanIndex - 1].ScanType;
                                pressurePlot.AddDataTICOnly(previousScanNumber, previousMsLevel, (float)scanMapper[scanIndex - 1], lastValue);
                            }

                            var scanNumber = scanStats[scanIndex].ScanNumber;
                            var msLevel = scanStats[scanIndex].ScanType;
                            pressurePlot.AddDataTICOnly(scanNumber, msLevel, (float) xArray[i], yArray[i]);
                            lastScanIndex = scanIndex;
                            lastValue = yArray[i];
                        }
                    }
                }

                massSpecDataReader.CloseDataFile();
                ProgRunner.GarbageCollectNow();
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception reading the Binary Data in the Agilent TOF .D directory using Agilent MassSpecDataReader: " + ex.Message, ex);
            }
        }

        private void GetDeviceInfo(IMsdrDataReader massSpecDataReader)
        {
            mDatasetStatsSummarizer.DatasetFileInfo.DeviceList.Clear();

            var devices = massSpecDataReader.FileInformation.GetDeviceTable(StoredDataType.All);

            var columns = devices.Columns.OfType<DataColumn>().ToList();
            //var format = string.Join("\t", Enumerable.Range(0, columns.Count).Select(x => $"{{{x}}}"));
            //Console.WriteLine(format, columns.Select(x => (object) x.ColumnName).ToArray());

            var dataTypeCol = columns.FirstOrDefault(x => x.ColumnName.Equals("StoredDataType", StringComparison.OrdinalIgnoreCase));
            var deviceIdCol = columns.FirstOrDefault(x => x.ColumnName.Equals("DeviceID", StringComparison.OrdinalIgnoreCase));
            if (dataTypeCol == null || deviceIdCol == null)
            {
                // Necessary column missing?
                return;
            }

            foreach (DataRow deviceData in devices.Rows)
            {
                var storedDataType = (StoredDataType)deviceData[dataTypeCol]; // flags
                var diDeviceType = Device.Other;
                if (storedDataType.HasFlag(StoredDataType.MassSpectra))
                    diDeviceType = Device.MS;
                else if (storedDataType.HasFlag(StoredDataType.Spectra))
                    diDeviceType = Device.MSAnalog;
                else if (storedDataType.HasFlag(StoredDataType.InstrumentCurves))
                    diDeviceType = Device.Analog;
                else if (storedDataType.HasFlag(StoredDataType.Chromatograms))
                    diDeviceType = Device.MS;

                var id = (int)deviceData[deviceIdCol];
                var deviceInfo = new AgilentDeviceInfo(diDeviceType, id);

                //Console.WriteLine(format, device.ItemArray);
                foreach (var column in columns)
                {
                    var value = deviceData[column];
                    switch (column.ColumnName.ToLower())
                    {
                        case "deviceid":
                            //var deviceId = (int) value;
                            break;
                        case "name":
                            deviceInfo.InstrumentName = (string)value;
                            break;
                        case "modelnumber":
                            deviceInfo.Model = (string)value;
                            break;
                        case "ordinalnumber":
                            deviceInfo.OrdinalNumber = (int)value;
                            break;
                        case "serialnumber":
                            deviceInfo.SerialNumber = (string)value;
                            break;
                        case "type":
                            var deviceType = (DeviceType)value;
                            deviceInfo.AgilentDeviceType = deviceType;
                            deviceInfo.SoftwareVersion = deviceType.ToString();
                            break;
                        case "storeddatatype":
                            deviceInfo.StoredDataTypes = (StoredDataType)value; // flags
                            break;
                        case "delay":
                            //var delay = (int) value;
                            break;
                        case "displayname":
                            deviceInfo.DisplayName = (string)value;
                            break;
                        case "multipledevicespresent":
                            // Boolean; Ignore
                            break;
                        case "vendor":
                            //var vendor = (DeviceVendor) value;
                            break;
                        case "driverversion":
                            deviceInfo.DriverVersion = value?.ToString() ?? "";
                            break;
                        case "firmwareversion":
                            deviceInfo.FirmwareVersion = value?.ToString() ?? "";
                            break;
                    }
                }

                deviceInfo.SoftwareVersion = deviceInfo.AgilentDeviceType.ToString();
                if (!string.IsNullOrWhiteSpace(deviceInfo.DriverVersion))
                    deviceInfo.SoftwareVersion += $" {deviceInfo.DriverVersion}";

                mDatasetStatsSummarizer.DatasetFileInfo.DeviceList.Add(deviceInfo);
            }
        }

        private class AgilentDeviceInfo : ThermoRawFileReader.DeviceInfo
        {
            public AgilentDeviceInfo(Device deviceType, int id) : base(deviceType, id)
            { }

            public DeviceType AgilentDeviceType { get; set; } = Agilent.MassSpectrometry.DataAnalysis.DeviceType.Unknown;

            public string DisplayName { get; set; }

            public StoredDataType StoredDataTypes { get; set; }

            public string DriverVersion { get; set; }
            public string FirmwareVersion { get; set; }
            public int OrdinalNumber { get; set; }
        }

        private void ReadChromatograms(IMsdrDataReader massSpecDataReader, double[] scanMapper, ScanStatsEntry[] scanStats, bool sumCycles)
        {
            if (Options.SaveTICAndBPIPlots && massSpecDataReader.FileInformation.IsMSDataPresent())
            {
                IBDAChromFilter filter = new BDAChromFilter();

                // Cycle Summing: produces a much cleaner and meaningful BPC/TIC in QQQ data, but bad results in IM-QTOF alternating/HiLo data.
                filter.DoCycleSum = sumCycles;

                // Process the chromatograms
                // NOTE: The TIC and BPC times are not full matches, so better to process them separately
                // Convenience method always sums cycles; don't use it
                //var ticData = massSpecDataReader.GetTIC();
                filter.ChromatogramType = ChromType.TotalIon;
                var ticData = massSpecDataReader.GetChromatogram(filter)[0];
                AddChromatogram(ticData, scanMapper, scanStats);

                // Convenience method always sums cycles; don't use it
                //var bpcData = massSpecDataReader.GetBPC();
                filter.ChromatogramType = ChromType.BasePeak;
                var bpcData = massSpecDataReader.GetChromatogram(filter)[0];
                AddChromatogram(bpcData, scanMapper, scanStats);

                /* NOTE: The following code may extract other chromatogram types; the primary ones seen are MRM and EIC. This does not work for instrument curves (pump pressure).
                var filter = (IBDAChromFilter)new BDAChromFilter();
                filter.ChromatogramType = ChromType.Unspecified;

                var chromTypes = Enum.GetValues(typeof(ChromType)).Cast<ChromType>().ToList();

                foreach (var chromType in chromTypes)
                {
                    filter.ChromatogramType = chromType;
                    try
                    {
                        var chromatograms = massSpecDataReader.GetChromatogram(filter);
                        foreach (var chromatogram in chromatograms)
                        {
                            Console.WriteLine($"Chromatogram: '{chromatogram.ChromatogramType}':'{chromatogram.MSScanType}', Device: '{chromatogram.DeviceType}' '{chromatogram.DeviceName}', Signal: '{chromatogram.SignalName}', '{chromatogram.SignalDescription}'");
                        }
                    }
                    catch
                    {
                        // Do nothing; we are just figuring out what chromatograms are available
                    }
                }
                */
            }

            if (Options.SaveTICAndBPIPlots /* && massSpecDataReader.FileInformation.IsNonMSDataPresent()*/)
            {
                // Read instrument curves
                var lcDataReader = (INonmsDataReader) massSpecDataReader;
                var devices = lcDataReader.GetNonmsDevices();

                if (devices == null || devices.Length == 0)
                {
                    // No non-MS devices
                    return;
                }

                // ReSharper disable once RedundantNameQualifier
                var deviceFilterList = new List<Agilent.MassSpectrometry.DataAnalysis.DeviceType>
                {
                    DeviceType.IsocraticPump,
                    DeviceType.BinaryPump,
                    DeviceType.QuaternaryPump,
                    DeviceType.CapillaryPump,
                    DeviceType.NanoPump,
                    DeviceType.LowFlowPump,
                    DeviceType.CompactLCIsoPump,
                    DeviceType.CompactLCGradPump,
                    DeviceType.CompactLC1220IsoPump,
                    DeviceType.CompactLC1220GradPump,
                };

                var signalFilterList = new List<string>
                {
                    "Pressure",
                    "Flow",
                    "Solvent Ratio B"
                };

                foreach (var device in devices)
                {
                    if (!deviceFilterList.Contains(device.DeviceType))
                    {
                        // Skip device
                        continue;
                    }

                    AgilentDeviceInfo agDevice = null;

                    foreach (var devInfo in mDatasetStatsSummarizer.DatasetFileInfo.DeviceList)
                    {
                        if (devInfo is not AgilentDeviceInfo agDevInfo)
                        {
                            continue;
                        }

                        if (agDevInfo.AgilentDeviceType == device.DeviceType &&
                            agDevInfo.InstrumentName.Equals(device.DeviceName) &&
                            agDevInfo.OrdinalNumber == device.OrdinalNumber)
                        {
                            agDevice = agDevInfo;
                            break;
                        }
                    }

                    if (agDevice == null)
                    {
                        continue;
                    }

                    // NOTE: not reading chromatograms from DAD devices (that requires 'StoredDataType.Chromatograms')
                    // NOTE: To preview these "Instrument Curves" externally, open the file in Agilent's Qualitative Analysis, then select the menu item Actions->"Extract All Instrument Curves"
                    var signals = lcDataReader.GetSignalInfo(device, StoredDataType.InstrumentCurves);

                    foreach (var signal in signals)
                    {
                        var chromData = lcDataReader.GetSignal(signal);

                        var description = chromData.SignalDescription;
                        if (string.IsNullOrWhiteSpace(description) || !signalFilterList.Any(x =>
                            description.IndexOf(x, StringComparison.OrdinalIgnoreCase) > -1))
                        {
                            // Skip signal - not in filter list
                            continue;
                        }

                        chromData.GetXAxisInfoChrom(out var xUnits, out var xValueType);
                        chromData.GetYAxisInfoChrom(out var yUnits, out var yValueType);

                        var devicePlot = AddInstrumentSpecificPlot(agDevice.DeviceDescription + description.Trim().Replace(" ", ""));

                        var xLabel = GetLabel(xValueType);
                        var yLabel = GetLabel(yValueType);
                        var xLabelUnits = GetUnitLabel(xUnits);
                        var yLabelUnits = GetUnitLabel(yUnits);

                        if (!xLabel.Contains(xLabelUnits) && !xLabelUnits.Contains(xLabel))
                        {
                            xLabel += $" ({xLabelUnits})";
                        }

                        if (!yLabel.Contains(yLabelUnits) && !yLabelUnits.Contains(yLabel))
                        {
                            yLabel += $" ({yLabelUnits})";
                        }

                        if (string.IsNullOrWhiteSpace(yLabel))
                        {
                            if (description.IndexOf("Ratio", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                yLabel = $"{description.Trim()} (%)";
                            }
                            else
                            {
                                yLabel = description.Trim();
                            }
                        }

                        devicePlot.TICXAxisLabel = xLabel;
                        devicePlot.TICYAxisLabel = yLabel;

                        devicePlot.TICYAxisExponentialNotation = false;

                        devicePlot.DeviceType = agDevice.DeviceType;
                        // Add description to avoid overwriting files when there are multiple plots per device
                        devicePlot.TICPlotAbbrev = string.Format("{0}{1}{2}", agDevice.AgilentDeviceType.ToString(), agDevice.DeviceNumber, description.Trim().Replace(" ", ""));
                        devicePlot.TICAutoMinMaxY = true;
                        devicePlot.RemoveZeroesFromEnds = false;
                        devicePlot.TICXAxisIsTimeMinutes = true;

                        for (var i = 0; i < chromData.XArray.Length; i++)
                        {
                            var time = chromData.XArray[i];
                            var value = chromData.YArray[i];
                            // NOTE: values can be too close to cast as int, and don't correlate with actual scan numbers so collisions are likely
                            // Use an artificial scan number
                            devicePlot.AddDataTICOnly(i + 1, 1, (float)time, value);
                        }
                    }
                }
            }
        }

        private string GetUnitLabel(Agilent.MassSpectrometry.DataAnalysis.DataUnit dataUnit)
        {
            switch (dataUnit)
            {
                case DataUnit.Minutes:
                    return "min";
                case DataUnit.Seconds:
                    return "sec";
                case DataUnit.Milliseconds:
                    return "ms";
                case DataUnit.Microseconds:
                    return "μs";
                case DataUnit.Nanoseconds:
                    return "ns";
                case DataUnit.ResponseUnits:
                    return "";
            }

            return dataUnit.ToString();
        }

        private string GetLabel(Agilent.MassSpectrometry.DataAnalysis.DataValueType dataValueType)
        {
            switch (dataValueType)
            {
                case DataValueType.AcqTime:
                    return "Acquisition Time";
                case DataValueType.ScanNumber:
                    return "Scan Number";
                case DataValueType.MassToCharge:
                    return "m/z";
                case DataValueType.IonAbundance:
                    return "Abundance";
                case DataValueType.Ordinate:
                    return "";
            }

            return dataValueType.ToString();
        }

        private void AddChromatogram(IBDAChromData chromatogram, double[] scanMapper, ScanStatsEntry[] scanStats)
        {
            if (chromatogram.ChromatogramType == ChromType.TotalIon)
            {
                AddChromatogram(mTICAndBPIPlot.AddDataTICOnly, chromatogram, scanMapper, scanStats);
            }
            else if (chromatogram.ChromatogramType == ChromType.BasePeak)
            {
                AddChromatogram(mTICAndBPIPlot.AddDataBPIOnly, chromatogram, scanMapper, scanStats);
            }
        }

        private void AddChromatogram(Action<int, int, float, double> addMethod, IBDAChromData chromatogram, double[] scanMapper, ScanStatsEntry[] scanStats)
        {
            if (addMethod == null)
            {
                return;
            }

            var times = chromatogram.XArray;
            var values = chromatogram.YArray;

            // Process the chromatograms
            for (var i = 0; i < times.Length; i++)
            {
                var time = times[i];
                var scanIndex = Array.BinarySearch(scanMapper, time);
                if (scanIndex < 0)
                {
                    scanIndex = ~scanIndex;
                    if (scanIndex > 0 && time - scanMapper[scanIndex - 1] < scanMapper[scanIndex] - time)
                        --scanIndex;
                }

                //mTICAndBPIPlot.AddData(scanNumber, scanMsLevel[scanNumber], (float)time, bpAbundances[i], abundances[i]);
                //mTICAndBPIPlot.AddDataTICOnly(scanStats[scanIndex].ScanNumber, scanStats[scanIndex].ScanType, (float)time, values[i]);
                addMethod(scanStats[scanIndex].ScanNumber, scanStats[scanIndex].ScanType, (float)time, values[i]);
            }
        }

        private void ReadSpectra(IMsdrDataReader massSpecDataReader, ScanStatsEntry[] scanStats)
        {
            var lastProgressTime = DateTime.UtcNow;

            // Note that this starts at 2 seconds, but is extended after each progress message is shown (maxing out at 30 seconds)
            var progressThresholdSeconds = 2;

            var scansProcessed = 0;

            var scanCount = massSpecDataReader.MSScanFileInformation.TotalScansPresent;
            for (var i = 0; i < scanCount; i++)
            {
                var scan = massSpecDataReader.GetScanRecord(i);
                var scanStatsEntry = scanStats[i];

                var spec = massSpecDataReader.GetSpectrum(i, null, null, DesiredMSStorageType.ProfileElsePeak);

                var xArray = spec.XArray;
                var yArray = spec.YArray;

                //
                var data = new List<LCMSDataPlotter.MSIonType>(xArray.Length);

                for (var j = 0; j < xArray.Length; j++)
                {
                    if (yArray[j] > 0)
                    {
                        data.Add(new LCMSDataPlotter.MSIonType
                        {
                            MZ = xArray[j],
                            Intensity = yArray[j]
                        });
                    }
                }

                // TODO: These need to be set, but it looks like we must read the spectrum to get them.
                // TODO: TotalDataPoints and XArray are full-TOF-range, not non-zero data points.
                //scanStatsEntry.IonCount = spec.TotalDataPoints;
                //scanStatsEntry.IonCount = xArray.Length;
                //scanStatsEntry.IonCountRaw = scanStatsEntry.IonCount;
                //scanStatsEntry.MzMin = xArray[0];
                //scanStatsEntry.MzMax = xArray[xArray.Length - 1];

                scanStatsEntry.IonCount = data.Count;
                scanStatsEntry.IonCountRaw = scanStatsEntry.IonCount;
                scanStatsEntry.MzMin = data[0].MZ;
                scanStatsEntry.MzMax = data[data.Count - 1].MZ;

                if (Options.SaveLCMS2DPlots)
                {
                    mLCMS2DPlot.AddScan(scanStatsEntry.ScanNumber, scanStatsEntry.ScanType, (float)scan.RetentionTime,
                        //scanStatsEntry.IonCount, xArray, yArray);
                        data);
                }

                if (Options.CheckCentroidingStatus)
                {
                    // Supply full array to not mis-classify sparse data
                    mDatasetStatsSummarizer.ClassifySpectrum(xArray, scanStatsEntry.ScanType, "Scan " + scanStatsEntry.ScanNumber);
                    //mDatasetStatsSummarizer.ClassifySpectrum(data.Select(x => x.MZ).ToList(), scanStatsEntry.ScanType, "Scan " + scanStatsEntry.ScanNumber);
                }

                scansProcessed++;

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < progressThresholdSeconds)
                    continue;

                lastProgressTime = DateTime.UtcNow;
                if (progressThresholdSeconds < 30)
                    progressThresholdSeconds += 2;

                var percentComplete = scansProcessed / (float)scanCount * 100;
                OnProgressUpdate(string.Format("Spectra processed: {0:N0}", scansProcessed), percentComplete);
            }
        }
    }
}
