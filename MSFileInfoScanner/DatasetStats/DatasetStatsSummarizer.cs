using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Ignore Spelling: AcqTime, centroided, centroiding, lcms, sep, utf, yyyy-MM-dd hh:mm:ss tt

        /// <summary>
        /// Scan type stats separation character
        /// </summary>
        public const string SCAN_TYPE_STATS_SEP_CHAR = "::###::";

        /// <summary>
        /// Dataset info file suffix
        /// </summary>
        public const string DATASET_INFO_FILE_SUFFIX = "_DatasetInfo.xml";

        /// <summary>
        /// Date/time format string
        /// </summary>
        public const string DATE_TIME_FORMAT_STRING = "yyyy-MM-dd hh:mm:ss tt";

        private struct SummaryStatsStatus
        {
            public bool UpToDate;
            public bool ScanFiltersIncludePrecursorMZValues;

            public readonly override string ToString()
            {
                return string.Format("Up-to-date: {0}; Scan filters including precursor m/z: {1}", UpToDate, ScanFiltersIncludePrecursorMZValues);
            }
        }

        public struct SummaryStatsScanInfo
        {
            public int ScanCount;
            public string IsolationWindowWidths;

            public readonly override string ToString()
            {
                return string.IsNullOrWhiteSpace(IsolationWindowWidths)
                    ? string.Format("{0} scans", ScanCount)
                    : string.Format("{0} scans with isolation window {1} m/z", ScanCount, IsolationWindowWidths);
            }
        }

        private readonly SortedSet<int> mDatasetScanNumbers;

        private readonly List<ScanStatsEntry> mDatasetScanStats;

        /// <summary>
        /// The spectrum type classifier determines if spectra are centroided or profile
        /// by examining the m/z distance between the ions in a spectrum
        /// </summary>
        private readonly SpectrumTypeClassifier mSpectraTypeClassifier;

        private DatasetSummaryStats mDatasetSummaryStats;

        private SummaryStatsStatus mDatasetStatsSummaryStatus;

        /// <summary>
        /// Number of DIA spectra
        /// </summary>
        private int ScanCountDIA;

        /// <summary>
        /// Number of HMS spectra
        /// </summary>
        private int ScanCountHMS;

        /// <summary>
        /// Number of HMSn spectra
        /// </summary>
        private int ScanCountHMSn;

        /// <summary>
        /// Number of low res MS spectra
        /// </summary>
        private int ScanCountMS;

        /// <summary>
        /// Number of low res MSn spectra
        /// </summary>
        private int ScanCountMSn;

        /// <summary>
        /// Maximum elution time (in minutes)
        /// </summary>
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
            FileDate = "April 28, 2023";

            ErrorMessage = string.Empty;

            mSpectraTypeClassifier = new SpectrumTypeClassifier();
            RegisterEvents(mSpectraTypeClassifier);

            mDatasetScanNumbers = new SortedSet<int>();
            mDatasetScanStats = new List<ScanStatsEntry>();
            mDatasetSummaryStats = new DatasetSummaryStats();

            mDatasetStatsSummaryStatus.UpToDate = false;
            mDatasetStatsSummaryStatus.ScanFiltersIncludePrecursorMZValues = false;

            DatasetFileInfo = new DatasetFileInfo();
            SampleInfo = new SampleInfo();

            ClearCachedData();
        }

        /// <summary>
        /// Add a new scan
        /// </summary>
        /// <param name="scanStats">Scan stats to add</param>
        public void AddDatasetScan(ScanStatsEntry scanStats)
        {
            // Add the scan number (if not yet present)
            mDatasetScanNumbers.Add(scanStats.ScanNumber);

            mDatasetScanStats.Add(scanStats);
            mDatasetStatsSummaryStatus.UpToDate = false;
        }

        /// <summary>
        /// When reading datasets with millions of spectra, we limit the amount of detailed scan info stored in mDatasetScanStats
        /// This method compares the counts in summaryStats.ScanTypeStats with the sum of (ScanCountMS + ScanCountHMS + ScanCountMSn + ScanCountHMSn)
        /// If the number of spectra tracked by summaryStats.ScanTypeStats is >= 98% of the sum, nothing is adjusted
        /// If the number of spectra tracked by summaryStats.ScanTypeStats is less than 98%, values in summaryStats.ScanTypeStats are increased based on the spectrum distribution actually stored in summaryStats
        /// </summary>
        /// <param name="summaryStats">Summarized scan stats to update</param>
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

            // The dataset summary stats object does not contain data for all the scans

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
                GetScanTypeAndFilter(scanTypeEntry.Key, out var scanType, out var scanTypeFilter);

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
            summaryStats.DIAScanCount = Math.Max(summaryStats.DIAScanCount, ScanCountDIA);
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
        /// <param name="mzList">m/z values</param>
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void ClassifySpectrum(List<double> mzList, int msLevel, string spectrumTitle)
        {
            ClassifySpectrum(mzList, msLevel, SpectrumTypeClassifier.CentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzList">m/z values</param>
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
        /// <param name="centroidingStatus">Centroiding status</param>
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
        /// <param name="mzArray">m/z values</param>
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
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
        /// <param name="mzArray">m/z values</param>
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
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
        /// <param name="mzArray">m/z values</param>
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
        /// <param name="centroidingStatus">Centroiding status</param>
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

            mDatasetStatsSummaryStatus.UpToDate = false;
            mDatasetStatsSummaryStatus.ScanFiltersIncludePrecursorMZValues = false;

            mSpectraTypeClassifier.Reset();

            CreateEmptyScanStatsFiles = true;

            ScanCountDIA = 0;

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
        /// <param name="includePrecursorMZ">
        /// When true, include precursor m/z values in the generic scan filters
        /// When false, replace the actual precursor m/z with 0
        /// </param>
        /// <param name="summaryStats">Output: summarized scan stats</param>
        /// <returns>>True if success, false if error</returns>
        public bool ComputeScanStatsSummary(
            List<ScanStatsEntry> scanStats,
            bool includePrecursorMZ,
            out DatasetSummaryStats summaryStats)
        {
            summaryStats = new DatasetSummaryStats();

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is null; unable to continue in ComputeScanStatsSummary");
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
                    var genericScanFilter = includePrecursorMZ
                        ? statEntry.ScanFilterText
                        : ThermoRawFileReader.XRawFileIO.GetScanFilterWithGenericPrecursorMZ(statEntry.ScanFilterText);

                    var msLevel = statEntry.ScanType;

                    if (!summaryStats.ScanTypeNamesByMSLevel.ContainsKey(msLevel))
                    {
                        summaryStats.ScanTypeNameOrder.Add(msLevel, new List<string>());
                        summaryStats.ScanTypeNamesByMSLevel.Add(msLevel, new SortedSet<string>());
                    }

                    if (!summaryStats.ScanTypeNamesByMSLevel[msLevel].Contains(statEntry.ScanTypeName))
                    {
                        summaryStats.ScanTypeNamesByMSLevel[msLevel].Add(statEntry.ScanTypeName);
                        summaryStats.ScanTypeNameOrder[msLevel].Add(statEntry.ScanTypeName);
                    }

                    if (statEntry.ScanType > 1)
                    {
                        // MSn spectrum
                        ComputeScanStatsUpdateDetails(
                            statEntry,
                            summaryStats,
                            summaryStats.MSnStats,
                            ticListMSn,
                            bpiListMSn);
                    }
                    else
                    {
                        // MS spectrum
                        ComputeScanStatsUpdateDetails(
                            statEntry,
                            summaryStats,
                            summaryStats.MSStats,
                            ticListMS,
                            bpiListMS);
                    }

                    // The scan type key is of the form "ScanTypeName::###::GenericScanFilter"
                    var scanTypeKey = statEntry.ScanTypeName + SCAN_TYPE_STATS_SEP_CHAR + genericScanFilter;

                    if (summaryStats.ScanTypeStats.ContainsKey(scanTypeKey))
                    {
                        summaryStats.ScanTypeStats[scanTypeKey]++;

                        // Add the window width (if not yet present)
                        summaryStats.ScanTypeWindowWidths[scanTypeKey].Add(statEntry.IsolationWindowWidth);
                    }
                    else
                    {
                        summaryStats.ScanTypeStats.Add(scanTypeKey, 1);
                        summaryStats.ScanTypeWindowWidths.Add(scanTypeKey, new SortedSet<double> { statEntry.IsolationWindowWidth });
                    }

                    if (statEntry.IsDIA)
                    {
                        summaryStats.DIAScanCount++;
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
            if (!string.IsNullOrWhiteSpace(scanStats.ElutionTime) &&
                double.TryParse(scanStats.ElutionTime, out var elutionTime) &&
                elutionTime > summaryStats.ElutionTimeMax)
            {
                summaryStats.ElutionTimeMax = elutionTime;
            }

            if (double.TryParse(scanStats.TotalIonIntensity, out var totalIonCurrent))
            {
                if (totalIonCurrent > summaryStatDetails.TICMax)
                {
                    summaryStatDetails.TICMax = totalIonCurrent;
                }

                ticList.Add(totalIonCurrent);
            }

            if (double.TryParse(scanStats.BasePeakIntensity, out var basePeakIntensity))
            {
                if (basePeakIntensity > summaryStatDetails.BPIMax)
                {
                    summaryStatDetails.BPIMax = basePeakIntensity;
                }

                bpiList.Add(basePeakIntensity);
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
                    ReportError("scanStats is null; unable to continue in CreateDatasetInfoFile");
                    return false;
                }

                ErrorMessage = string.Empty;

                // If CreateDatasetInfoXML() used a StringBuilder to cache the XML data, we would have to use System.Encoding.Unicode
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
        public string CreateDatasetInfoXML(
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo,
            SampleInfo sampleInfo)
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
        public string CreateDatasetInfoXML(
            string datasetName,
            List<ScanStatsEntry> scanStats,
            DatasetFileInfo datasetInfo)
        {
            return CreateDatasetInfoXML(datasetName, scanStats, datasetInfo, new SampleInfo());
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetInfo">Dataset Info</param>
        /// <param name="sampleInfo">Sample Info</param>
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
                    ReportError("scanStats is null; unable to continue in CreateDatasetInfoXML");
                    return string.Empty;
                }

                ErrorMessage = string.Empty;

                DatasetSummaryStats summaryStats;

                // This is true in MASIC, but false in MS_File_Info_Scanner
                const bool includePrecursorMZ = false;

                if (scanStats == mDatasetScanStats)
                {
                    summaryStats = GetDatasetSummaryStats(includePrecursorMZ);

                    if (mSpectraTypeClassifier.TotalSpectra > 0)
                    {
                        includeCentroidStats = true;
                    }
                }
                else
                {
                    // Parse the data in scanStats to compute the bulk values
                    var success = ComputeScanStatsSummary(scanStats, includePrecursorMZ, out summaryStats);

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
                    CloseOutput = false        // Do not close output automatically so that the MemoryStream can be read after the XmlWriter has been closed
                };

                // We could cache the text using a StringBuilder, like this:
                //
                // var datasetInfo = new StringBuilder();
                // var stringWriter = new StringWriter(datasetInfo);
                // var writer = new XmlTextWriter(stringWriter)
                // {
                //     Formatting = Formatting.Indented,
                //     Indentation = 2
                // };

                // However, when you send the output to a StringBuilder it is always encoded as Unicode (UTF-16)
                // since this is the only character encoding used in the .NET Framework for String values,
                // and thus you'll see the attribute encoding="UTF-16" in the opening XML declaration

                // The alternative is to use a MemoryStream.  Here, the stream encoding is set by the XmlWriter
                // and so you see the attribute encoding="UTF-8" in the opening XML declaration encoding
                // (since we used xmlSettings.Encoding = Encoding.UTF8)
                //
                var memStream = new MemoryStream();
                var writer = XmlWriter.Create(memStream, xmlSettings);

                writer.WriteStartDocument(true);

                // Write the beginning of the "Root" element.
                writer.WriteStartElement("DatasetInfo");

                writer.WriteStartElement("Dataset");

                if (datasetInfo.DatasetID > 0)
                {
                    writer.WriteAttributeString("DatasetID", datasetInfo.DatasetID.ToString());
                }
                writer.WriteString(datasetName);
                writer.WriteEndElement();       // Dataset EndElement

                writer.WriteStartElement("ScanTypes");

                GetSortedScanTypeSummaryTypes(summaryStats, out var scanInfoByScanType);

                foreach (var scanTypeList in summaryStats.ScanTypeNameOrder)
                {
                    foreach (var scanTypeName in scanTypeList.Value)
                    {
                        foreach (var scanFilterInfo in scanInfoByScanType[scanTypeName])
                        {
                            var genericScanFilter = scanFilterInfo.Key;
                            var scanCountForType = scanFilterInfo.Value.ScanCount;
                            var windowWidths = scanFilterInfo.Value.IsolationWindowWidths;

                            writer.WriteStartElement("ScanType");
                            writer.WriteAttributeString("ScanCount", scanCountForType.ToString());
                            writer.WriteAttributeString("ScanFilterText", FixNull(genericScanFilter));
                            writer.WriteAttributeString("IsolationWindowMZ", FixNull(windowWidths));
                            writer.WriteString(scanTypeName);
                            writer.WriteEndElement(); // ScanType
                        }
                    }
                }

                writer.WriteEndElement();       // ScanTypes

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
                writer.WriteElementString("ScanCountDIA", summaryStats.DIAScanCount.ToString());

                writer.WriteElementString("Elution_Time_Max", summaryStats.ElutionTimeMax.ToString("0.0###"));

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

                        // Add the device if not yet present
                        if (!devicesDisplayed.Add(deviceKey))
                            continue;

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

                writer.WriteEndElement();       // AcquisitionInfo

                writer.WriteStartElement("TICInfo");
                writer.WriteElementString("TIC_Max_MS", ValueToString(summaryStats.MSStats.TICMax, 5));
                writer.WriteElementString("TIC_Max_MSn", ValueToString(summaryStats.MSnStats.TICMax, 5));
                writer.WriteElementString("BPI_Max_MS", ValueToString(summaryStats.MSStats.BPIMax, 5));
                writer.WriteElementString("BPI_Max_MSn", ValueToString(summaryStats.MSnStats.BPIMax, 5));
                writer.WriteElementString("TIC_Median_MS", ValueToString(summaryStats.MSStats.TICMedian, 5));
                writer.WriteElementString("TIC_Median_MSn", ValueToString(summaryStats.MSnStats.TICMedian, 5));
                writer.WriteElementString("BPI_Median_MS", ValueToString(summaryStats.MSStats.BPIMedian, 5));
                writer.WriteElementString("BPI_Median_MSn", ValueToString(summaryStats.MSnStats.BPIMedian, 5));
                writer.WriteEndElement();       // TICInfo

                // Only write the SampleInfo block if sampleInfo contains entries
                if (sampleInfo.HasData())
                {
                    writer.WriteStartElement("SampleInfo");
                    writer.WriteElementString("SampleName", FixNull(sampleInfo.SampleName));
                    writer.WriteElementString("Comment1", FixNull(sampleInfo.Comment1));
                    writer.WriteElementString("Comment2", FixNull(sampleInfo.Comment2));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();  // End the "Root" element (DatasetInfo)

                writer.WriteEndDocument(); // End the document

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
                    ReportError("scanStats is null; unable to continue in CreateScanStatsFile");
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

                // Write the ScanStats headers
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

                // Write the extended scan stats headers
                var headerNamesEx = new List<string>
                {
                    "Dataset",
                    "ScanNumber",
                    ScanStatsEntry.SCAN_STATS_COL_ION_INJECTION_TIME,
                    ScanStatsEntry.SCAN_STATS_COL_SCAN_SEGMENT,
                    ScanStatsEntry.SCAN_STATS_COL_SCAN_EVENT,
                    ScanStatsEntry.SCAN_STATS_COL_CHARGE_STATE,
                    ScanStatsEntry.SCAN_STATS_COL_MONOISOTOPIC_MZ,
                    ScanStatsEntry.SCAN_STATS_COL_MS2_ISOLATION_WIDTH,
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

                    // Base peak signal-to-noise ratio
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
                    dataValues.Add(scanStatsEntry.ExtendedScanInfo.IsolationWindowWidthMZ);
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
        /// Get dataset summary stats
        /// </summary>
        /// <param name="includePrecursorMZ">
        /// When true, include precursor m/z values in the generic scan filters
        /// When false, replace the actual precursor m/z with 0
        /// </param>
        public DatasetSummaryStats GetDatasetSummaryStats(bool includePrecursorMZ)
        {
            if (mDatasetStatsSummaryStatus.UpToDate && mDatasetStatsSummaryStatus.ScanFiltersIncludePrecursorMZValues == includePrecursorMZ)
                return mDatasetSummaryStats;

            ComputeScanStatsSummary(mDatasetScanStats, includePrecursorMZ, out mDatasetSummaryStats);

            AdjustSummaryStats(mDatasetSummaryStats);

            mDatasetStatsSummaryStatus.UpToDate = true;
            mDatasetStatsSummaryStatus.ScanFiltersIncludePrecursorMZValues = includePrecursorMZ;

            return mDatasetSummaryStats;
        }

        /// <summary>
        /// Convert the non-zero isolation window widths to a comma separated list
        /// </summary>
        /// <param name="scanTypeKey">Scan type key</param>
        /// <param name="scanTypeWindowWidths">Scan type window widths</param>
        /// <returns>Comma separated list, or an empty string if no valid window widths</returns>
        public static string GetDelimitedWindowWidthList(string scanTypeKey, Dictionary<string, SortedSet<double>> scanTypeWindowWidths)
        {
            if (!scanTypeWindowWidths.TryGetValue(scanTypeKey, out var windowWidthList))
                return string.Empty;

            var windowWidthsToShow = new SortedSet<double>();

            foreach (var item in windowWidthList.Where(item => item > 0))
            {
                windowWidthsToShow.Add(item);
            }

            return string.Join(", ", windowWidthsToShow);
        }

        /// <summary>
        /// Extract out the scan type and filter text from scanTypeKey
        /// </summary>
        /// <param name="scanTypeKey">Scan type key</param>
        /// <param name="scanType">Scan Type, e.g. HMS or HCD-HMSn</param>
        /// <param name="scanFilterText">Scan filter text, e.g. "FTMS + p NSI Full ms" or "FTMS + p NSI d Full ms2 0@hcd25.00" or "IMS"</param>
        private void GetScanTypeAndFilter(
            string scanTypeKey,
            out string scanType,
            out string scanFilterText)
        {
            var placeholderEntry = new KeyValuePair<string, int>(scanTypeKey, 0);
            GetScanTypeAndFilter(placeholderEntry, out scanType, out _, out scanFilterText);
        }

        /// <summary>
        /// Extract out the scan type and filter text from the key in scanTypeEntry
        /// </summary>
        /// <param name="scanTypeEntry">Key is scan type, value is number of scans with the given scan type</param>
        /// <param name="scanType">Scan Type, e.g. HMS or HCD-HMSn or DIA-HCD-HMSn</param>
        /// <param name="basicScanType">Simplified scan type, e.g. HMS or HMSn</param>
        /// <param name="scanFilterText">Scan filter text, e.g. "FTMS + p NSI Full ms" or "FTMS + p NSI d Full ms2 0@hcd25.00" or "IMS"</param>
        /// <returns>Scan count for this scan type and filter string</returns>
        private static int GetScanTypeAndFilter(
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

            var dashIndex = scanType.LastIndexOf('-');

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
        /// <param name="scanNumber">Scan number</param>
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
        /// Counts passed to this method are relevant when reading datasets with millions of spectra,
        /// and we limited the amount of detailed scan info stored in mDatasetScanStats
        /// </remarks>
        /// <param name="scanCountHMS">High resolution MS1 scans</param>
        /// <param name="scanCountHMSn">High resolution MSn scans</param>
        /// <param name="scanCountMS">Low resolution MS1 scans</param>
        /// <param name="scanCountMSn">Low resolution MSn scans</param>
        /// <param name="scanCountDIA">DIA scans</param>
        /// <param name="elutionTimeMax">Elution time max</param>
        public void StoreScanTypeTotals(int scanCountHMS, int scanCountHMSn, int scanCountMS, int scanCountMSn, int scanCountDIA, double elutionTimeMax)
        {
            ScanCountHMS = scanCountHMS;
            ScanCountHMSn = scanCountHMSn;
            ScanCountMS = scanCountMS;
            ScanCountMSn = scanCountMSn;
            ScanCountDIA = scanCountDIA;

            ElutionTimeMax = elutionTimeMax;
        }
        /// <summary>
        /// Step through the scan type stats tracked by summaryStats and populate two dictionaries with the details
        /// </summary>
        /// <param name="summaryStats">Summarized scan stats</param>
        /// <param name="scanInfoByScanType">Output: dictionary where keys are scan type names and values are a sorted dictionary of generic scan filters and the ScanCount IsolationWindowWidths for each generic scan filter</param>
        public static void GetSortedScanTypeSummaryTypes(
            DatasetSummaryStats summaryStats,
            out Dictionary<string, SortedDictionary<string, SummaryStatsScanInfo>> scanInfoByScanType)
        {
            // Keys in this dictionary are scan type names
            // Values are a sorted dictionary where keys are scan filters (e.g. "FTMS + p NSI d Full ms2 0@hcd25.00") and values are the number of scans of that type
            scanInfoByScanType = new Dictionary<string, SortedDictionary<string, SummaryStatsScanInfo>>();

            var scanTypeNames = new SortedSet<string>();

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var scanTypeList in summaryStats.ScanTypeNameOrder)
            {
                foreach (var scanTypeName in scanTypeList.Value)
                {
                    if (!scanTypeNames.Add(scanTypeName))
                    {
                        throw new Exception(string.Format("Scan type {0} occurs more than once in ScanTypeNameOrder; this is a programming bug", scanTypeName));
                    }
                }
            }

            foreach (var scanTypeEntry in summaryStats.ScanTypeStats)
            {
                var scanCountForType = GetScanTypeAndFilter(scanTypeEntry, out var scanTypeName, out _, out var scanFilterText);

                if (!scanTypeNames.Contains(scanTypeName))
                {
                    throw new Exception(string.Format("ScanTypeStats has scan type {0}, but that name is not present in ScanTypeNameOrder; this is a programming bug", scanTypeName));
                }

                if (!scanInfoByScanType.ContainsKey(scanTypeName))
                {
                    scanInfoByScanType.Add(scanTypeName, new SortedDictionary<string, SummaryStatsScanInfo>());
                }

                var windowWidths = GetDelimitedWindowWidthList(scanTypeEntry.Key, summaryStats.ScanTypeWindowWidths);

                var scanInfo = new SummaryStatsScanInfo
                {
                    ScanCount = scanCountForType,
                    IsolationWindowWidths = windowWidths
                };

                scanInfoByScanType[scanTypeName].Add(scanFilterText, scanInfo);
            }
        }

        /// <summary>
        /// Updates the scan type information for the specified scan number
        /// </summary>
        /// <param name="scanNumber">Scan number</param>
        /// <param name="scanType"> Scan Type (aka MSLevel)</param>
        /// <param name="scanTypeName">Scan type name</param>
        /// <returns>True if the scan was found and updated; otherwise false</returns>
        public bool UpdateDatasetScanType(int scanNumber, int scanType, string scanTypeName)
        {
            var matchFound = false;

            // Look for scanNumber in mDatasetScanStats
            foreach (var scan in mDatasetScanStats)
            {
                if (scan.ScanNumber != scanNumber)
                    continue;

                scan.ScanType = scanType;
                scan.ScanTypeName = scanTypeName;
                mDatasetStatsSummaryStatus.UpToDate = false;

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
                    ReportError("scanStats is null; unable to continue in UpdateDatasetStatsTextFile");
                    return false;
                }

                ErrorMessage = string.Empty;

                DatasetSummaryStats summaryStats;

                const bool includePrecursorMZ = false;

                if (scanStats == mDatasetScanStats)
                {
                    summaryStats = GetDatasetSummaryStats(includePrecursorMZ);
                }
                else
                {
                    // Parse the data in scanStats to compute the bulk values
                    var summarySuccess = ComputeScanStatsSummary(scanStats, includePrecursorMZ, out summaryStats);

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
        /// If a dataset has a mix of MS2 and MS3 spectra, and if all the MS3 spectra meet the minimum m/z requirement, a warning is not raised
        /// Example dataset: UCLA_Dun_TMT_set2_03_QE_24May19_Rage_Rep-19-04-r01
        /// </remarks>
        /// <param name="requiredMzMin">Required minimum m/z value</param>
        /// <param name="errorOrWarningMsg">Output: error or warning message</param>
        /// <param name="maxPercentAllowedFailed">Maximum percentage of spectra allowed to have a minimum m/z larger than the required minimum</param>
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
        /// <param name="msLevel">MS level (1 for MS1, 2 for MS2, etc.)</param>
        /// <param name="requiredMzMin">Required minimum m/z value</param>
        /// <param name="maxPercentAllowedFailed">Maximum percentage of spectra allowed to have a minimum m/z larger than the required minimum</param>
        /// <param name="scanCountForMSLevel">Output: scan count for the given MS level</param>
        /// <param name="scanCountWithData">Output: scan count with data</param>
        /// <param name="errorOrWarningMsg">Output: error or warning message</param>
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

        private string ValueToString(double value, byte digitsOfPrecision)
        {
            if (double.IsNaN(value))
            {
                return 0.ToString();
            }

            if (double.IsNegativeInfinity(value))
            {
                return StringUtilities.ValueToString(double.MinValue, digitsOfPrecision);
            }

            if (double.IsPositiveInfinity(value))
            {
                return StringUtilities.ValueToString(double.MaxValue, digitsOfPrecision);
            }

            return StringUtilities.ValueToString(value, 5);
        }
    }
}
