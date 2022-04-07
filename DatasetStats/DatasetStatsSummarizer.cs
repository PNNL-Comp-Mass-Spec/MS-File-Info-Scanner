using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using PRISM;
using SpectraTypeClassifier;

namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// <para>This class computes aggregate stats for a dataset</para>
    /// <para>
    /// Program started May 7, 2009
    /// Ported from clsMASICScanStatsParser to clsDatasetStatsSummarizer in February 2010
    /// </para>
    /// <para>
    /// Licensed under the 2-Clause BSD License; you may not use this file except
    /// in compliance with the License.  You may obtain a copy of the License at
    /// https://opensource.org/licenses/BSD-2-Clause
    /// </para>
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// </remarks>
    public class DatasetStatsSummarizer : EventNotifier
    {
        // Ignore Spelling: AcqTime, centroided, utf, yyyy-MM-dd hh:mm:ss tt

        public const string SCAN_TYPE_STATS_SEP_CHAR = "::###::";
        public const string DATASET_INFO_FILE_SUFFIX = "_DatasetInfo.xml";

        public const string DATE_TIME_FORMAT_STRING = "yyyy-MM-dd hh:mm:ss tt";

        private readonly SortedSet<int> mDatasetScanNumbers;

        private readonly List<ScanStatsEntry> mDatasetScanStats;

        private readonly SpectrumTypeClassifier mSpectraTypeClassifier;

        private bool mDatasetSummaryStatsUpToDate;

        private DatasetSummaryStats mDatasetSummaryStats;

        private int ScanCountHMS;
        private int ScanCountHMSn;
        private int ScanCountMS;
        private int ScanCountMSn;
        private double ElutionTimeMax;

        /// <summary>
        /// When false, do not create the scan stats files if no data was loaded
        /// Defaults to True
        /// </summary>
        public bool CreateEmptyScanStatsFiles { get; set; }

        /// <summary>
        /// Dataset file info
        /// </summary>
        public DatasetFileInfo DatasetFileInfo { get; }

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Dataset file modification time
        /// </summary>
        public string FileDate { get; }

        /// <summary>
        /// Sample info
        /// </summary>
        public SampleInfo SampleInfo { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public DatasetStatsSummarizer()
        {
            FileDate = "September 23, 2021";

            ErrorMessage = string.Empty;

            mSpectraTypeClassifier = new SpectrumTypeClassifier();
            RegisterEvents(mSpectraTypeClassifier);

            mDatasetScanNumbers = new SortedSet<int>();
            mDatasetScanStats = new List<ScanStatsEntry>();
            mDatasetSummaryStats = new DatasetSummaryStats();

            mDatasetSummaryStatsUpToDate = false;

            DatasetFileInfo = new DatasetFileInfo();
            SampleInfo = new SampleInfo();

            ClearCachedData();
        }

        /// <summary>
        /// Add a new scan
        /// </summary>
        /// <param name="scanStats"></param>
        public void AddDatasetScan(ScanStatsEntry scanStats)
        {
            if (!mDatasetScanNumbers.Contains(scanStats.ScanNumber))
            {
                mDatasetScanNumbers.Add(scanStats.ScanNumber);
            }

            mDatasetScanStats.Add(scanStats);
            mDatasetSummaryStatsUpToDate = false;
        }

        private void AdjustSummaryStats(DatasetSummaryStats summaryStats)
        {
            // Keys in this dictionary are keys in summaryStats.ScanTypeStats
            // Values are basic (simplified) scan types
            var basicScanTypeByScanTypeKey = new Dictionary<string, string>();

            // Keys in this dictionary are keys in summaryStats.ScanTypeStats
            // Values are scan counts
            var scanCountsByScanTypeKey = new Dictionary<string, int>();

            var totalScansInSummaryStats = 0;

            // Determine the basic (simplified) scan type for each entry in summaryStats
            // Also cache the scan counts
            foreach (var scanTypeEntry in summaryStats.ScanTypeStats)
            {
                var scanCountForType = GetScanTypeAndFilter(scanTypeEntry, out _, out var basicScanType, out _);

                basicScanTypeByScanTypeKey.Add(scanTypeEntry.Key, basicScanType);
                scanCountsByScanTypeKey.Add(scanTypeEntry.Key, scanCountForType);
                totalScansInSummaryStats += scanCountForType;
            }

            // Only adjust the scan stats if the total number of stored scans is less than 98% of the sum of the ScanCount member variables
            if (totalScansInSummaryStats >= (ScanCountMS + ScanCountHMS + ScanCountMSn + ScanCountHMSn) * 0.98)
                return;

            // The dataset summary stats object does not contain data for all of the scans

            // Adjust the scan counts in summaryStats.ScanTypeStats using the counts in
            // ScanCountMS, ScanCountHMS, ScanCountMSn, and ScanCountHMSn

            OnWarningEvent(
                "This dataset has a large number of missing spectra; detailed scan info was stored for {0:N0} of the {1:N0} total spectra. " +
                "Will now extrapolate the scan counts based on the stored data.",
                totalScansInSummaryStats, ScanCountMS + ScanCountHMS + ScanCountMSn + ScanCountHMSn);

            // Determine the total scans for each basic scan type
            var scanCountsByBasicScanType = new Dictionary<string, int>();

            foreach (var scanTypeEntry in basicScanTypeByScanTypeKey)
            {
                var basicScanType = scanTypeEntry.Value;
                if (!(basicScanType.Equals("HMS", StringComparison.OrdinalIgnoreCase) ||
                      basicScanType.Equals("HMSn", StringComparison.OrdinalIgnoreCase) ||
                      basicScanType.Equals("MS", StringComparison.OrdinalIgnoreCase) ||
                      basicScanType.Equals("MSn", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var scanCountToAdd = scanCountsByScanTypeKey[scanTypeEntry.Key];

                if (scanCountsByBasicScanType.TryGetValue(basicScanType, out var scanCountForBasicScanType))
                {
                    scanCountsByBasicScanType[basicScanType] = scanCountForBasicScanType + scanCountToAdd;
                }
                else
                {
                    scanCountsByBasicScanType.Add(basicScanType, scanCountToAdd);
                }
            }

            // Adjust the scan counts in summaryStats.ScanTypeStats
            foreach (var scanTypeEntry in basicScanTypeByScanTypeKey)
            {
                GetScanTypeAndFilter(scanTypeEntry.Key, out var scanType, out _, out var scanTypeFilter);

                var basicScanType = scanTypeEntry.Value;
                if (!scanCountsByBasicScanType.TryGetValue(basicScanType, out var totalStoredScanCount))
                {
                    continue;
                }

                var storedScanCount = scanCountsByScanTypeKey[scanTypeEntry.Key];
                var percentOfTotal = storedScanCount / (double)totalStoredScanCount;

                var updatedScanCount = basicScanType switch
                {
                    "HMS" => ScanCountHMS * percentOfTotal,
                    "HMSn" => ScanCountHMSn * percentOfTotal,
                    "MS" => ScanCountMS * percentOfTotal,
                    "MSn" => ScanCountMSn * percentOfTotal,
                    _ => -1
                };

                if (updatedScanCount < 0)
                    continue;

                var updatedScanCountInt = (int)updatedScanCount;

                summaryStats.ScanTypeStats[scanTypeEntry.Key] = updatedScanCountInt;

                if (string.IsNullOrWhiteSpace(scanTypeFilter))
                {
                    OnStatusEvent("Adjusted the scan count for {0} from {1:N0} to {2:N0}", scanType, storedScanCount, updatedScanCountInt);
                }
                else
                {
                    OnStatusEvent("Adjusted the scan count for {0} ({1}) from {2:N0} to {3:N0}", scanType, scanTypeFilter, storedScanCount, updatedScanCountInt);
                }
            }

            // Assure that the MS and MSn scan counts are also correct
            summaryStats.MSStats.ScanCount = Math.Max(summaryStats.MSStats.ScanCount, ScanCountHMS + ScanCountMS);
            summaryStats.MSnStats.ScanCount = Math.Max(summaryStats.MSnStats.ScanCount, ScanCountHMSn + ScanCountMSn);
            summaryStats.ElutionTimeMax = Math.Max(summaryStats.ElutionTimeMax, ElutionTimeMax);
        }

        private double AssureNumeric(double value)
        {
            if (double.IsNaN(value))
                return 0;

            if (double.IsPositiveInfinity(value))
                return double.MaxValue;

            if (double.IsNegativeInfinity(value))
                return double.MinValue;

            return value;
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzList"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle"></param>
        public void ClassifySpectrum(List<double> mzList, int msLevel, string spectrumTitle)
        {
            ClassifySpectrum(mzList, msLevel, SpectrumTypeClassifier.CentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzList">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void ClassifySpectrum(List<double> mzList, int msLevel, SpectrumTypeClassifier.CentroidStatusConstants centroidingStatus, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(mzList, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void ClassifySpectrum(double[] mzArray, int msLevel, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(mzArray, msLevel, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="ionCount">Number of items in mzArray; if -1, then parses all data in mzArray</param>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void ClassifySpectrum(int ionCount, double[] mzArray, int msLevel, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, mzArray, msLevel, SpectrumTypeClassifier.CentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        /// <param name="ionCount">Number of items in mzArray; if -1, then parses all data in mzArray</param>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        // ReSharper disable once UnusedMember.Global
        public void ClassifySpectrum(
            int ionCount,
            double[] mzArray,
            int msLevel,
            SpectrumTypeClassifier.CentroidStatusConstants centroidingStatus,
            string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, mzArray, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Clear cached data
        /// </summary>
        public void ClearCachedData()
        {
            mDatasetScanNumbers.Clear();
            mDatasetScanStats.Clear();
            mDatasetSummaryStats.Clear();

            DatasetFileInfo.Clear();
            SampleInfo.Clear();

            mDatasetSummaryStatsUpToDate = false;

            mSpectraTypeClassifier.Reset();

            CreateEmptyScanStatsFiles = true;

            ScanCountHMS = 0;
            ScanCountHMSn = 0;
            ScanCountMS = 0;
            ScanCountMSn = 0;
            ElutionTimeMax = 0;
        }

        /// <summary>
        /// Summarizes the scan info in scanStats()
        /// </summary>
        /// <param name="scanStats">ScanStats data to parse</param>
        /// <param name="summaryStats">Stats output</param>
        /// <returns>>True if success, false if error</returns>
        public bool ComputeScanStatsSummary(List<ScanStatsEntry> scanStats, out DatasetSummaryStats summaryStats)
        {
            summaryStats = new DatasetSummaryStats();

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue");
                    return false;
                }

                ErrorMessage = string.Empty;

                var scanStatsCount = scanStats.Count;

                // Initialize the TIC and BPI Lists
                var ticListMS = new List<double>(scanStatsCount);
                var bpiListMS = new List<double>(scanStatsCount);

                var ticListMSn = new List<double>(scanStatsCount);
                var bpiListMSn = new List<double>(scanStatsCount);

                foreach (var statEntry in scanStats)
                {
                    if (statEntry.ScanType > 1)
                    {
                        // MSn spectrum
                        ComputeScanStatsUpdateDetails(statEntry,
                                                      summaryStats,
                                                      summaryStats.MSnStats,
                                                      ticListMSn,
                                                      bpiListMSn);
                    }
                    else
                    {
                        // MS spectrum
                        ComputeScanStatsUpdateDetails(statEntry,
                                                      summaryStats,
                                                      summaryStats.MSStats,
                                                      ticListMS,
                                                      bpiListMS);
                    }

                    var scanTypeKey = statEntry.ScanTypeName + SCAN_TYPE_STATS_SEP_CHAR + statEntry.ScanFilterText;
                    if (summaryStats.ScanTypeStats.ContainsKey(scanTypeKey))
                    {
                        summaryStats.ScanTypeStats[scanTypeKey]++;
                    }
                    else
                    {
                        summaryStats.ScanTypeStats.Add(scanTypeKey, 1);
                    }
                }

                summaryStats.MSStats.TICMedian = AssureNumeric(MathNet.Numerics.Statistics.Statistics.Median(ticListMS));
                summaryStats.MSStats.BPIMedian = AssureNumeric(MathNet.Numerics.Statistics.Statistics.Median(bpiListMS));

                summaryStats.MSnStats.TICMedian = AssureNumeric(MathNet.Numerics.Statistics.Statistics.Median(ticListMSn));
                summaryStats.MSnStats.BPIMedian = AssureNumeric(MathNet.Numerics.Statistics.Statistics.Median(bpiListMSn));

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in ComputeScanStatsSummary", ex);
                return false;
            }
        }

        private void ComputeScanStatsUpdateDetails(
            ScanStatsEntry scanStats,
            DatasetSummaryStats summaryStats,
            SummaryStatDetails summaryStatDetails,
            ICollection<double> ticList,
            ICollection<double> bpiList)
        {
            if (!string.IsNullOrWhiteSpace(scanStats.ElutionTime))
            {
                if (double.TryParse(scanStats.ElutionTime, out var elutionTime))
                {
                    if (elutionTime > summaryStats.ElutionTimeMax)
                    {
                        summaryStats.ElutionTimeMax = elutionTime;
                    }
                }
            }

            if (double.TryParse(scanStats.TotalIonIntensity, out var tic))
            {
                if (tic > summaryStatDetails.TICMax)
                {
                    summaryStatDetails.TICMax = tic;
                }

                ticList.Add(tic);
            }

            if (double.TryParse(scanStats.BasePeakIntensity, out var bpi))
            {
                if (bpi > summaryStatDetails.BPIMax)
                {
                    summaryStatDetails.BPIMax = bpi;
                }

                bpiList.Add(bpi);
            }

            summaryStatDetails.ScanCount++;
        }

        /// <summary>
        /// Creates an XML file summarizing the data stored in this class (in mDatasetScanStats, this.DatasetFileInfo, and this.SampleInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        public bool CreateDatasetInfoFile(string datasetName, string datasetInfoFilePath)
        {
            return CreateDatasetInfoFile(datasetName, datasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates an XML file summarizing the data in scanStats and datasetInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <param name="sampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        public bool CreateDatasetInfoFile(
            string datasetName,
            string datasetInfoFilePath,
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo,
            SampleInfo sampleInfo)
        {
            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in CreateDatasetInfoFile");
                    return false;
                }

                ErrorMessage = string.Empty;

                // If CreateDatasetInfoXML() used a StringBuilder to cache the XML data, then we would have to use Encoding.Unicode
                // However, CreateDatasetInfoXML() now uses a MemoryStream, so we're able to use UTF8
                using var writer = new StreamWriter(new FileStream(datasetInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);

                writer.WriteLine(CreateDatasetInfoXML(datasetName, scanStats, datasetInfo, sampleInfo));

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in CreateDatasetInfoFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates XML summarizing the data stored in this class (in mDatasetScanStats, this.DatasetFileInfo, and this.SampleInfo)
        /// Auto-determines the dataset name using this.DatasetFileInfo.DatasetName
        /// </summary>
        /// <returns>XML (as string)</returns>
        public string CreateDatasetInfoXML()
        {
            return CreateDatasetInfoXML(DatasetFileInfo.DatasetName, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data stored in this class (in mDatasetScanStats, this.DatasetFileInfo, and this.SampleInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <returns>XML (as string)</returns>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(string datasetName)
        {
            return CreateDatasetInfoXML(datasetName, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetInfo
        /// Auto-determines the dataset name using datasetInfo.DatasetName
        /// </summary>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(List<ScanStatsEntry> scanStats, DatasetFileInfo datasetInfo)
        {
            return CreateDatasetInfoXML(datasetInfo.DatasetName, scanStats, datasetInfo, new SampleInfo());
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats, datasetInfo, and sampleInfo
        /// Auto-determines the dataset name using datasetInfo.DatasetName
        /// </summary>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <param name="sampleInfo">Sample Info</param>
        /// <returns>XML (as string)</returns>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(List<ScanStatsEntry> scanStats, DatasetFileInfo datasetInfo, SampleInfo sampleInfo)
        {
            return CreateDatasetInfoXML(datasetInfo.DatasetName, scanStats, datasetInfo, sampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(string datasetName, List<ScanStatsEntry> scanStats, DatasetFileInfo datasetInfo)
        {
            return CreateDatasetInfoXML(datasetName, scanStats, datasetInfo, new SampleInfo());
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <param name="sampleInfo"></param>
        /// <returns>XML (as string)</returns>
        public string CreateDatasetInfoXML(
            string datasetName,
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo,
            SampleInfo sampleInfo)
        {
            var includeCentroidStats = false;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in CreateDatasetInfoXML");
                    return string.Empty;
                }

                ErrorMessage = string.Empty;

                DatasetSummaryStats summaryStats;

                if (scanStats == mDatasetScanStats)
                {
                    summaryStats = GetDatasetSummaryStats();

                    if (mSpectraTypeClassifier.TotalSpectra > 0)
                    {
                        includeCentroidStats = true;
                    }
                }
                else
                {
                    // Parse the data in scanStats to compute the bulk values
                    var success = ComputeScanStatsSummary(scanStats, out summaryStats);
                    if (!success)
                    {
                        ReportError("ComputeScanStatsSummary returned false; unable to continue in CreateDatasetInfoXML");
                        return string.Empty;
                    }
                    // includeCentroidStats is already false;
                }

                var xmlSettings = new XmlWriterSettings
                {
                    CheckCharacters = true,
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = Encoding.UTF8,
                    CloseOutput = false     // Do not close output automatically so that MemoryStream can be read after the XmlWriter has been closed
                };

                // We could cache the text using a StringBuilder, like this:
                //
                // var datasetInfoBuilder = new StringBuilder();
                // var stringWriter = new StringWriter(datasetInfoBuilder);
                // var writer = new XmlTextWriter(stringWriter)
                // {
                //     Formatting = Formatting.Indented,
                //     Indentation = 2
                // };

                // However, when you send the output to a StringBuilder it is always encoded as Unicode (UTF-16)
                //  since this is the only character encoding used in the .NET Framework for String values,
                //  and thus you'll see the attribute encoding="utf-16" in the opening XML declaration

                // The alternative is to use a MemoryStream.  Here, the stream encoding is set by the XmlWriter
                //  and so you see the attribute encoding="utf-8" in the opening XML declaration encoding
                //  (since we used xmlSettings.Encoding = Encoding.UTF8)
                //
                var memStream = new MemoryStream();
                var writer = XmlWriter.Create(memStream, xmlSettings);

                writer.WriteStartDocument(true);

                //Write the beginning of the "Root" element.
                writer.WriteStartElement("DatasetInfo");

                writer.WriteStartElement("Dataset");
                if (datasetInfo.DatasetID > 0)
                {
                    writer.WriteAttributeString("DatasetID", datasetInfo.DatasetID.ToString());
                }
                writer.WriteString(datasetName);
                writer.WriteEndElement();       // Dataset EndElement

                writer.WriteStartElement("ScanTypes");

                foreach (var scanTypeEntry in summaryStats.ScanTypeStats)
                {
                    var scanCountForType = GetScanTypeAndFilter(scanTypeEntry, out var scanType, out _, out var scanFilterText);

                    writer.WriteStartElement("ScanType");
                    writer.WriteAttributeString("ScanCount", scanCountForType.ToString());
                    writer.WriteAttributeString("ScanFilterText", FixNull(scanFilterText));
                    writer.WriteString(scanType);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();           // ScanTypes EndElement

                writer.WriteStartElement("AcquisitionInfo");

                var scanCountTotal = summaryStats.MSStats.ScanCount + summaryStats.MSnStats.ScanCount;
                if (scanCountTotal == 0 && datasetInfo.ScanCount > 0)
                {
                    scanCountTotal = datasetInfo.ScanCount;
                }
                else if (datasetInfo.ScanCount > scanCountTotal)
                {
                    scanCountTotal = datasetInfo.ScanCount;
                }

                writer.WriteElementString("ScanCount", scanCountTotal.ToString());

                writer.WriteElementString("ScanCountMS", summaryStats.MSStats.ScanCount.ToString());
                writer.WriteElementString("ScanCountMSn", summaryStats.MSnStats.ScanCount.ToString());
                writer.WriteElementString("Elution_Time_Max", summaryStats.ElutionTimeMax.ToString("0.00"));

                var acqTimeMinutes = datasetInfo.AcqTimeEnd.Subtract(datasetInfo.AcqTimeStart).TotalMinutes;
                writer.WriteElementString("AcqTimeMinutes", acqTimeMinutes.ToString("0.00"));
                writer.WriteElementString("StartTime", datasetInfo.AcqTimeStart.ToString(DATE_TIME_FORMAT_STRING));
                writer.WriteElementString("EndTime", datasetInfo.AcqTimeEnd.ToString(DATE_TIME_FORMAT_STRING));

                // For datasets based on a single file, this is the file's size
                // For datasets stored in a directory, this is the total size of the primary instrument files
                writer.WriteElementString("FileSizeBytes", datasetInfo.FileSizeBytes.ToString());

                if (datasetInfo.InstrumentFiles.Count > 0)
                {
                    writer.WriteStartElement("InstrumentFiles");

                    foreach (var instrumentFile in datasetInfo.InstrumentFiles)
                    {
                        writer.WriteStartElement("InstrumentFile");
                        writer.WriteAttributeString("Hash", FixNull(instrumentFile.Value.Hash));
                        writer.WriteAttributeString("HashType", instrumentFile.Value.HashType.ToString());
                        writer.WriteAttributeString("Size", instrumentFile.Value.Length.ToString());
                        writer.WriteString(instrumentFile.Key);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();       // InstrumentFiles EndElement
                }

                if (datasetInfo.DeviceList.Count > 0)
                {
                    writer.WriteStartElement("DeviceList");

                    // In Thermo files, the same device might be listed more than once in deviceList, e.g. if an LC is tracking pressure from two different locations in the pump
                    // This SortedSet is used to avoid displaying the same device twice
                    var devicesDisplayed = new SortedSet<string>();

                    foreach (var device in datasetInfo.DeviceList)
                    {
                        var deviceKey = string.Format("{0}_{1}_{2}", device.InstrumentName, device.Model, device.SerialNumber);

                        if (devicesDisplayed.Contains(deviceKey))
                            continue;

                        devicesDisplayed.Add(deviceKey);

                        writer.WriteStartElement("Device");
                        writer.WriteAttributeString("Type", device.DeviceType.ToString());
                        writer.WriteAttributeString("Number", device.DeviceNumber.ToString());
                        writer.WriteAttributeString("Name", device.InstrumentName);
                        writer.WriteAttributeString("Model", device.Model);
                        writer.WriteAttributeString("SerialNumber", device.SerialNumber);
                        writer.WriteAttributeString("SoftwareVersion", device.SoftwareVersion);
                        writer.WriteString(device.DeviceDescription);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();       // DeviceList
                }

                if (includeCentroidStats)
                {
                    var centroidedMS1Spectra = mSpectraTypeClassifier.CentroidedMS1Spectra;
                    var centroidedMSnSpectra = mSpectraTypeClassifier.CentroidedMSnSpectra;

                    var centroidedMS1SpectraClassifiedAsProfile = mSpectraTypeClassifier.CentroidedMS1SpectraClassifiedAsProfile;
                    var centroidedMSnSpectraClassifiedAsProfile = mSpectraTypeClassifier.CentroidedMSnSpectraClassifiedAsProfile;

                    var totalMS1Spectra = mSpectraTypeClassifier.TotalMS1Spectra;
                    var totalMSnSpectra = mSpectraTypeClassifier.TotalMSnSpectra;

                    if (totalMS1Spectra + totalMSnSpectra == 0)
                    {
                        // None of the spectra had MSLevel 1 or MSLevel 2
                        // This shouldn't normally be the case; nevertheless, we'll report the totals, regardless of MSLevel, using the MS1 elements
                        centroidedMS1Spectra = mSpectraTypeClassifier.CentroidedSpectra;
                        totalMS1Spectra = mSpectraTypeClassifier.TotalSpectra;
                    }

                    writer.WriteElementString("ProfileScanCountMS1", (totalMS1Spectra - centroidedMS1Spectra).ToString());
                    writer.WriteElementString("ProfileScanCountMS2", (totalMSnSpectra - centroidedMSnSpectra).ToString());

                    writer.WriteElementString("CentroidScanCountMS1", centroidedMS1Spectra.ToString());
                    writer.WriteElementString("CentroidScanCountMS2", centroidedMSnSpectra.ToString());

                    if (centroidedMS1SpectraClassifiedAsProfile > 0 || centroidedMSnSpectraClassifiedAsProfile > 0)
                    {
                        writer.WriteElementString("CentroidMS1ScansClassifiedAsProfile", centroidedMS1SpectraClassifiedAsProfile.ToString());
                        writer.WriteElementString("CentroidMS2ScansClassifiedAsProfile", centroidedMSnSpectraClassifiedAsProfile.ToString());
                    }
                }

                writer.WriteEndElement();   // AcquisitionInfo EndElement

                writer.WriteStartElement("TICInfo");
                writer.WriteElementString("TIC_Max_MS", StringUtilities.ValueToString(summaryStats.MSStats.TICMax, 5));
                writer.WriteElementString("TIC_Max_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.TICMax, 5));
                writer.WriteElementString("BPI_Max_MS", StringUtilities.ValueToString(summaryStats.MSStats.BPIMax, 5));
                writer.WriteElementString("BPI_Max_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.BPIMax, 5));
                writer.WriteElementString("TIC_Median_MS", StringUtilities.ValueToString(summaryStats.MSStats.TICMedian, 5));
                writer.WriteElementString("TIC_Median_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.TICMedian, 5));
                writer.WriteElementString("BPI_Median_MS", StringUtilities.ValueToString(summaryStats.MSStats.BPIMedian, 5));
                writer.WriteElementString("BPI_Median_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.BPIMedian, 5));
                writer.WriteEndElement();       // TICInfo EndElement

                // Only write the SampleInfo block if sampleInfo contains entries
                if (sampleInfo.HasData())
                {
                    writer.WriteStartElement("SampleInfo");
                    writer.WriteElementString("SampleName", FixNull(sampleInfo.SampleName));
                    writer.WriteElementString("Comment1", FixNull(sampleInfo.Comment1));
                    writer.WriteElementString("Comment2", FixNull(sampleInfo.Comment2));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();           // DatasetInfo EndElement (note that DatasetInfo is the "root" element)

                writer.WriteEndDocument();          // End the document

                writer.Close();

                // Now Rewind the memory stream and output as a string
                memStream.Position = 0;
                var reader = new StreamReader(memStream);

                // Return the XML as text
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                ReportError("Error in CreateDatasetInfoXML", ex);
            }

            // This code will only be reached if an exception occurs
            return string.Empty;
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="scanStatsFilePath">File path to write the text file to</param>
        /// <returns>True if success; False if failure</returns>
        public bool CreateScanStatsFile(string scanStatsFilePath)
        {
            return CreateScanStatsFile(scanStatsFilePath, mDatasetScanStats, DatasetFileInfo);
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="scanStatsFilePath">File path to write the text file to</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <returns>True if success; False if failure</returns>
        public bool CreateScanStatsFile(
            string scanStatsFilePath,
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo)
        {
            var datasetID = datasetInfo.DatasetID;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in CreateScanStatsFile");
                    return false;
                }

                if (scanStats.Count == 0 && !CreateEmptyScanStatsFiles)
                {
                    return true;
                }

                ErrorMessage = string.Empty;

                // Define the path to the extended scan stats file
                var scanStatsFile = MSFileInfoScanner.GetFileInfo(scanStatsFilePath);
                if (scanStatsFile.DirectoryName == null)
                {
                    ReportError("Unable to determine the parent directory for " + scanStatsFilePath);
                    return false;
                }

                var scanStatsExFilePath = Path.Combine(scanStatsFile.DirectoryName, Path.GetFileNameWithoutExtension(scanStatsFile.Name) + "Ex.txt");

                // Open the output files
                using var scanStatsWriter = new StreamWriter(new FileStream(scanStatsFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));
                using var scanStatsExWriter = new StreamWriter(new FileStream(scanStatsExFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));

                var includeDriftTime = false;

                foreach (var scanStatsEntry in scanStats)
                {
                    if (!double.TryParse(scanStatsEntry.DriftTimeMsec, out var driftTimeMsec) || driftTimeMsec < float.Epsilon) continue;
                    includeDriftTime = true;
                    break;
                }

                // Write the headers
                var headerNames = new List<string>
                {
                    "Dataset",
                    "ScanNumber",
                    "ScanTime",
                    "ScanType",
                    "TotalIonIntensity",
                    "BasePeakIntensity",
                    "BasePeakMZ",
                    "BasePeakSignalToNoiseRatio",
                    "IonCount",
                    "IonCountRaw",
                    "ScanTypeName"
                };

                if (includeDriftTime)
                {
                    headerNames.Add("DriftTime");
                }

                scanStatsWriter.WriteLine(string.Join("\t", headerNames));

                var headerNamesEx = new List<string>
                {
                    "Dataset",
                    "ScanNumber",
                    ScanStatsEntry.SCAN_STATS_COL_ION_INJECTION_TIME,
                    ScanStatsEntry.SCAN_STATS_COL_SCAN_SEGMENT,
                    ScanStatsEntry.SCAN_STATS_COL_SCAN_EVENT,
                    ScanStatsEntry.SCAN_STATS_COL_CHARGE_STATE,
                    ScanStatsEntry.SCAN_STATS_COL_MONOISOTOPIC_MZ,
                    ScanStatsEntry.SCAN_STATS_COL_COLLISION_MODE,
                    ScanStatsEntry.SCAN_STATS_COL_SCAN_FILTER_TEXT
                };

                scanStatsExWriter.WriteLine(string.Join("\t", headerNamesEx));

                var dataValues = new List<string>();

                foreach (var scanStatsEntry in scanStats)
                {
                    dataValues.Clear();

                    // Dataset ID
                    dataValues.Add(datasetID.ToString());

                    // Scan number
                    dataValues.Add(scanStatsEntry.ScanNumber.ToString());

                    // Scan time (minutes)
                    dataValues.Add(scanStatsEntry.ElutionTime);

                    // Scan type (1 for MS, 2 for MS2, etc.)
                    dataValues.Add(scanStatsEntry.ScanType.ToString());

                    // Total ion intensity
                    dataValues.Add(scanStatsEntry.TotalIonIntensity);

                    // Base peak ion intensity
                    dataValues.Add(scanStatsEntry.BasePeakIntensity);

                    // Base peak ion m/z
                    dataValues.Add(scanStatsEntry.BasePeakMZ);

                    // Base peak signal to noise ratio
                    dataValues.Add(scanStatsEntry.BasePeakSignalToNoiseRatio);

                    // Number of peaks (aka ions) in the spectrum
                    dataValues.Add(scanStatsEntry.IonCount.ToString());

                    // Number of peaks (aka ions) in the spectrum prior to any filtering
                    dataValues.Add(scanStatsEntry.IonCountRaw.ToString());

                    // Scan type name
                    dataValues.Add(scanStatsEntry.ScanTypeName);

                    // Drift time (optional)
                    if (includeDriftTime)
                    {
                        dataValues.Add(scanStatsEntry.DriftTimeMsec);
                    }

                    scanStatsWriter.WriteLine(string.Join("\t", dataValues));

                    // Write the next entry to scanStatsExWriter
                    // Note that this file format is compatible with that created by MASIC
                    // However, only a limited number of columns are written out, since StoreExtendedScanInfo only stores a certain set of parameters

                    dataValues.Clear();

                    // Dataset number
                    dataValues.Add(datasetID.ToString());

                    // Scan number
                    dataValues.Add(scanStatsEntry.ScanNumber.ToString());

                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.IonInjectionTime);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.ScanSegment);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.ScanEvent);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.ChargeState);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.MonoisotopicMZ);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.CollisionMode);
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.ScanFilterText);

                    scanStatsExWriter.WriteLine(string.Join("\t", dataValues));
                }

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in CreateScanStatsFile", ex);
                return false;
            }
        }

        private string FixNull(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text;
        }

        /// <summary>
        /// Get the DatasetSummaryStats object
        /// </summary>
        public DatasetSummaryStats GetDatasetSummaryStats()
        {
            if (mDatasetSummaryStatsUpToDate)
                return mDatasetSummaryStats;

            ComputeScanStatsSummary(mDatasetScanStats, out mDatasetSummaryStats);

            AdjustSummaryStats(mDatasetSummaryStats);

            mDatasetSummaryStatsUpToDate = true;

            return mDatasetSummaryStats;
        }

        /// <summary>
        /// Extract out the scan type and filter text from scanTypeKey
        /// </summary>
        /// <param name="scanTypeKey"></param>
        /// <param name="scanType">Scan Type, e.g. HMS or HCD-HMSn</param>
        /// <param name="basicScanType">Simplified scan type, e.g. HMS or HMSn</param>
        /// <param name="scanFilterText">Scan filter text, e.g. "FTMS + p NSI Full ms" or "FTMS + p NSI d Full ms2 0@hcd25.00" or "IMS"</param>
        private void GetScanTypeAndFilter(
            string scanTypeKey,
            out string scanType,
            out string basicScanType,
            out string scanFilterText)
        {
            var placeholderEntry = new KeyValuePair<string, int>(scanTypeKey, 0);
            GetScanTypeAndFilter(placeholderEntry, out scanType, out basicScanType, out scanFilterText);
        }

        /// <summary>
        /// Extract out the scan type and filter text from the key in scanTypeEntry
        /// </summary>
        /// <param name="scanTypeEntry"></param>
        /// <param name="scanType">Scan Type, e.g. HMS or HCD-HMSn</param>
        /// <param name="basicScanType">Simplified scan type, e.g. HMS or HMSn</param>
        /// <param name="scanFilterText">Scan filter text, e.g. "FTMS + p NSI Full ms" or "FTMS + p NSI d Full ms2 0@hcd25.00" or "IMS"</param>
        /// <returns>Scan count for this scan type and filter string</returns>
        private int GetScanTypeAndFilter(
            KeyValuePair<string, int> scanTypeEntry,
            out string scanType,
            out string basicScanType,
            out string scanFilterText)
        {
            var scanTypeKey = scanTypeEntry.Key;
            var indexMatch = scanTypeKey.IndexOf(SCAN_TYPE_STATS_SEP_CHAR, StringComparison.Ordinal);

            if (indexMatch >= 0)
            {
                scanFilterText = scanTypeKey.Substring(indexMatch + SCAN_TYPE_STATS_SEP_CHAR.Length);
                if (indexMatch > 0)
                {
                    scanType = scanTypeKey.Substring(0, indexMatch);
                }
                else
                {
                    scanType = string.Empty;
                }
            }
            else
            {
                scanType = scanTypeKey;
                scanFilterText = string.Empty;
            }

            var dashIndex = scanType.IndexOf('-');

            if (dashIndex > 0 && dashIndex < scanType.Length - 1)
            {
                basicScanType = scanType.Substring(dashIndex + 1);
            }
            else
            {
                basicScanType = scanType;
            }

            return scanTypeEntry.Value;
        }

        /// <summary>
        /// Return true if the given scan number has been stored using AddDatasetScan
        /// </summary>
        /// <param name="scanNumber"></param>
        public bool HasScanNumber(int scanNumber)
        {
            return mDatasetScanNumbers.Contains(scanNumber);
        }

        private void ReportError(string message, Exception ex = null)
        {
            if (ex is null)
            {
                ErrorMessage = message;
            }
            else
            {
                ErrorMessage = message + ": " + ex.Message;
            }

            OnErrorEvent(message, ex);
        }

        /// <summary>
        /// Store scan counts, by scan type
        /// </summary>
        /// <remarks>
        /// Counts passed to this method are relevant when reading datasets with millions of spectra
        /// and we limited the amount of detailed scan info stored in mDatasetScanStats
        /// </remarks>
        /// <param name="scanCountHMS"></param>
        /// <param name="scanCountHMSn"></param>
        /// <param name="scanCountMS"></param>
        /// <param name="scanCountMSn"></param>
        /// <param name="elutionTimeMax"></param>
        public void StoreScanTypeTotals(int scanCountHMS, int scanCountHMSn, int scanCountMS, int scanCountMSn, double elutionTimeMax)
        {
            ScanCountHMS = scanCountHMS;
            ScanCountHMSn = scanCountHMSn;
            ScanCountMS = scanCountMS;
            ScanCountMSn = scanCountMSn;
            ElutionTimeMax = elutionTimeMax;
        }

        /// <summary>
        /// Updates the scan type information for the specified scan number
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <param name="scanType"></param>
        /// <param name="scanTypeName"></param>
        /// <returns>True if the scan was found and updated; otherwise false</returns>
        public bool UpdateDatasetScanType(int scanNumber, int scanType, string scanTypeName)
        {
            var matchFound = false;

            // Look for scanNumber in mDatasetScanStats
            foreach (var scan in mDatasetScanStats)
            {
                if (scan.ScanNumber != scanNumber) continue;

                scan.ScanType = scanType;
                scan.ScanTypeName = scanTypeName;
                mDatasetSummaryStatsUpToDate = false;

                matchFound = true;
                break;
            }

            return matchFound;
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and this.DatasetFileInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        public bool UpdateDatasetStatsTextFile(string datasetName, string datasetInfoFilePath)
        {
            return UpdateDatasetStatsTextFile(datasetName, datasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data in scanStats and datasetInfo
        /// This method does not check for duplicate entries; it simply appends a new line
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetStatsFilePath">Tab-delimited file to create/update</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <param name="sampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        public bool UpdateDatasetStatsTextFile(
            string datasetName,
            string datasetStatsFilePath,
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo,
            SampleInfo sampleInfo)
        {
            var writeHeaders = false;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in UpdateDatasetStatsTextFile");
                    return false;
                }

                ErrorMessage = string.Empty;

                DatasetSummaryStats summaryStats;
                if (scanStats == mDatasetScanStats)
                {
                    summaryStats = GetDatasetSummaryStats();
                }
                else
                {
                    // Parse the data in scanStats to compute the bulk values
                    var summarySuccess = ComputeScanStatsSummary(scanStats, out summaryStats);
                    if (!summarySuccess)
                    {
                        ReportError("ComputeScanStatsSummary returned false; unable to continue in UpdateDatasetStatsTextFile");
                        return false;
                    }
                }

                if (!File.Exists(datasetStatsFilePath))
                {
                    writeHeaders = true;
                }

                OnDebugEvent("Updating {0}", datasetStatsFilePath);

                // Create or open the output file
                using var writer = new StreamWriter(new FileStream(datasetStatsFilePath, FileMode.Append, FileAccess.Write, FileShare.Read));

                if (writeHeaders)
                {
                    // Write the header line
                    var headerNames = new List<string>
                    {
                        "Dataset",
                        "ScanCount",
                        "ScanCountMS",
                        "ScanCountMSn",
                        "Elution_Time_Max",
                        "AcqTimeMinutes",
                        "StartTime",
                        "EndTime",
                        "FileSizeBytes",
                        "SampleName",
                        "Comment1",
                        "Comment2"
                    };

                    writer.WriteLine(string.Join("\t", headerNames));
                }

                var dataValues = new List<string>
                {
                    datasetName,
                    (summaryStats.MSStats.ScanCount + summaryStats.MSnStats.ScanCount).ToString(),
                    summaryStats.MSStats.ScanCount.ToString(),
                    summaryStats.MSnStats.ScanCount.ToString(),
                    summaryStats.ElutionTimeMax.ToString("0.00"),
                    datasetInfo.AcqTimeEnd.Subtract(datasetInfo.AcqTimeStart).TotalMinutes.ToString("0.00"),
                    datasetInfo.AcqTimeStart.ToString(DATE_TIME_FORMAT_STRING),
                    datasetInfo.AcqTimeEnd.ToString(DATE_TIME_FORMAT_STRING),
                    datasetInfo.FileSizeBytes.ToString(),
                    FixNull(sampleInfo.SampleName),
                    FixNull(sampleInfo.Comment1),
                    FixNull(sampleInfo.Comment2)
                };

                writer.WriteLine(string.Join("\t", dataValues));

                return true;
            }
            catch (Exception ex)
            {
                ReportError("Error in UpdateDatasetStatsTextFile", ex);
                return false;
            }
        }

        /// <summary>
        /// Examine the minimum m/z value in MS2 spectra
        /// Keep track of the number of spectra where the minimum m/z value is greater than MS2MzMin
        /// Raise an error if at least 10% of the spectra have a minimum m/z higher than the threshold
        /// Log a warning if some spectra, but fewer than 10% of the total, have a minimum higher than the threshold
        /// </summary>
        /// <remarks>
        /// If a dataset has a mix of MS2 and MS3 spectra, and if all of the MS3 spectra meet the minimum m/z requirement, a warning is not raised
        /// Example dataset: UCLA_Dun_TMT_set2_03_QE_24May19_Rage_Rep-19-04-r01
        /// </remarks>
        /// <param name="requiredMzMin">Minimum m/z threshold; the </param>
        /// <param name="errorOrWarningMsg"></param>
        /// <param name="maxPercentAllowedFailed"></param>
        /// <returns>True if valid data, false if at least 10% of the spectra has a minimum m/z higher than the threshold</returns>
        public bool ValidateMS2MzMin(float requiredMzMin, out string errorOrWarningMsg, int maxPercentAllowedFailed)
        {
            // First examine MS2 spectra
            var validMS2 = ValidateMSnMzMin(
                2,
                requiredMzMin, maxPercentAllowedFailed,
                out var scanCountMS2,
                out var scanCountWithDataMS2,
                out var messageMS2);

            if (scanCountWithDataMS2 > 0 && validMS2)
            {
                errorOrWarningMsg = messageMS2;
                return true;
            }

            // MS2 spectra did not meet the requirements; check MS3 spectra
            var validMS3 = ValidateMSnMzMin(
                3,
                requiredMzMin, maxPercentAllowedFailed,
                out var scanCountMS3,
                out var scanCountWithDataMS3,
                out var messageMS3);

            if (scanCountWithDataMS3 > 0 && validMS3)
            {
                errorOrWarningMsg = messageMS3;
                return true;
            }

            if (scanCountMS2 == 0 && scanCountMS3 == 0)
            {
                // No MS2 or MS3 spectra
                // Treat this as "valid data"
                errorOrWarningMsg = "No MS2 or MS3 spectra";
                return true;
            }

            if (scanCountWithDataMS2 > 0 && scanCountWithDataMS3 == 0)
                errorOrWarningMsg = messageMS2;
            else if (scanCountWithDataMS2 == 0 && scanCountWithDataMS3 > 0)
                errorOrWarningMsg = messageMS3;
            else
                errorOrWarningMsg = messageMS2 + "; " + messageMS3;

            return false;
        }

        /// <summary>
        /// Determine the percentage of scans with the given msLevel that have a minimum m/z value greater than requiredMzMin
        /// </summary>
        /// <param name="msLevel"></param>
        /// <param name="requiredMzMin"></param>
        /// <param name="maxPercentAllowedFailed"></param>
        /// <param name="scanCountForMSLevel"></param>
        /// <param name="scanCountWithData"></param>
        /// <param name="errorOrWarningMsg"></param>
        private bool ValidateMSnMzMin(
            int msLevel,
            float requiredMzMin,
            int maxPercentAllowedFailed,
            out int scanCountForMSLevel,
            out int scanCountWithData,
            out string errorOrWarningMsg)
        {
            scanCountWithData = 0;
            scanCountForMSLevel = 0;

            var scanCountInvalid = 0;

            foreach (var scan in mDatasetScanStats)
            {
                if (scan.ScanType != msLevel)
                    continue;

                scanCountForMSLevel++;
                if (scan.IonCount == 0 && scan.IonCountRaw == 0)
                    continue;

                scanCountWithData++;
                if (scan.MzMin > requiredMzMin)
                {
                    scanCountInvalid++;
                }
            }

            string spectraType;
            if (msLevel == 2)
                spectraType = "MS2";
            else if (msLevel == 3)
                spectraType = "MS3";
            else
                spectraType = "MSn";

            if (scanCountForMSLevel == 0)
            {
                // There are no MS2 (or MS3) spectra
                errorOrWarningMsg = string.Format("Dataset has no {0} spectra; cannot validate minimum m/z", spectraType);
                return false;
            }

            if (scanCountWithData == 0)
            {
                // None of the MS2 (or MS3) spectra has data; cannot validate
                errorOrWarningMsg = string.Format("None of the {0} spectra has data; cannot validate minimum m/z", spectraType);
                return false;
            }

            if (scanCountInvalid == 0)
            {
                errorOrWarningMsg = string.Empty;
                return true;
            }

            var percentInvalid = scanCountInvalid / (float)scanCountWithData * 100;

            var percentRounded = percentInvalid.ToString(percentInvalid < 10 ? "F1" : "F0");

            // Example messages:
            // 3.8% of the MS2 spectra have a minimum m/z value larger than 113.0 m/z (950 / 25,000)
            // 2.5% of the MS3 spectra have a minimum m/z value larger than 113.0 m/z (75 / 3,000)
            // 100% of the MS2 spectra have a minimum m/z value larger than 126.0 m/z (32,489 / 32,489)

            errorOrWarningMsg = string.Format("{0}% of the {1} spectra have a minimum m/z value larger than {2:F1} m/z ({3:N0} / {4:N0})",
                                              percentRounded, spectraType, requiredMzMin, scanCountInvalid, scanCountWithData);

            return percentInvalid < maxPercentAllowedFailed;
        }
    }
}
