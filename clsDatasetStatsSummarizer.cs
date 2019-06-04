using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using PRISM;
using SpectraTypeClassifier;

// This class computes aggregate stats for a dataset
//
// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Program started May 7, 2009
// Ported from clsMASICScanStatsParser to clsDatasetStatsSummarizer in February 2010
//
// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
// -------------------------------------------------------------------------------
//
// Licensed under the 2-Clause BSD License; you may not use this file except
// in compliance with the License.  You may obtain a copy of the License at
// https://opensource.org/licenses/BSD-2-Clause
//

namespace MSFileInfoScanner
{

    public class clsDatasetStatsSummarizer : EventNotifier
    {

        #region "Constants and Enums"
        public const string SCAN_TYPE_STATS_SEP_CHAR = "::###::";
        public const string DATASET_INFO_FILE_SUFFIX = "_DatasetInfo.xml";
        public const string DEFAULT_DATASET_STATS_FILENAME = "MSFileInfo_DatasetStats.txt";
        #endregion

        #region "Structures"

        /// <summary>
        /// Sample info
        /// </summary>
        public struct udtSampleInfoType
        {
            public string SampleName;
            public string Comment1;

            public string Comment2;
            public void Clear()
            {
                SampleName = string.Empty;
                Comment1 = string.Empty;
                Comment2 = string.Empty;
            }

            public bool HasData()
            {
                if (!string.IsNullOrEmpty(SampleName) || !string.IsNullOrEmpty(Comment1) || !string.IsNullOrEmpty(Comment2))
                {
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region "Classwide Variables"

        private readonly List<clsScanStatsEntry> mDatasetScanStats;

        public udtSampleInfoType SampleInfo;
        private readonly clsSpectrumTypeClassifier mSpectraTypeClassifier;

        private bool mDatasetSummaryStatsUpToDate;

        private clsDatasetSummaryStats mDatasetSummaryStats;

        private readonly clsMedianUtilities mMedianUtils;

        #endregion

        #region "Properties"

        /// <summary>
        /// When false, do not create the scan stats files if no data was loaded
        /// Defaults to True
        /// </summary>
        public bool CreateEmptyScanStatsFiles { get; set; }

        /// <summary>
        /// Dataset file info
        /// </summary>
        public clsDatasetFileInfo DatasetFileInfo { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Dataset file modification time
        /// </summary>
        public string FileDate { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetStatsSummarizer()
        {
            FileDate = "May 27, 2019";

            ErrorMessage = string.Empty;

            mMedianUtils = new clsMedianUtilities();

            mSpectraTypeClassifier = new clsSpectrumTypeClassifier();
            RegisterEvents(mSpectraTypeClassifier);

            mDatasetScanStats = new List<clsScanStatsEntry>();
            mDatasetSummaryStats = new clsDatasetSummaryStats();

            DatasetFileInfo = new clsDatasetFileInfo();
            SampleInfo = new udtSampleInfoType();

            ClearCachedData();
        }

        /// <summary>
        /// Add a new scan
        /// </summary>
        /// <param name="scanStats"></param>
        public void AddDatasetScan(clsScanStatsEntry scanStats)
        {
            mDatasetScanStats.Add(scanStats);
            mDatasetSummaryStatsUpToDate = false;
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzList"></param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle"></param>
        public void ClassifySpectrum(List<double> mzList, int msLevel, string spectrumTitle)
        {
            ClassifySpectrum(mzList, msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzList">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        public void ClassifySpectrum(List<double> mzList, int msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants centroidingStatus, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(mzList, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        public void ClassifySpectrum(double[] mzArray, int msLevel, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(mzArray, msLevel, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="ionCount">Number of items in mzArray; if -1, then parses all data in mzArray</param>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        public void ClassifySpectrum(int ionCount, double[] mzArray, int msLevel, string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, mzArray, msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants.Unknown, spectrumTitle);
        }

        /// <summary>
        /// Examine the m/z values in the spectrum to determine if the data is centroided
        /// </summary>
        /// <param name="ionCount">Number of items in mzArray; if -1, then parses all data in mzArray</param>
        /// <param name="mzArray">MZ values</param>
        /// <param name="msLevel"></param>
        /// <param name="centroidingStatus"></param>
        /// <param name="spectrumTitle">Optional spectrum title (e.g. scan number)</param>
        /// <remarks>
        /// Increments mSpectraTypeClassifier.TotalSpectra if data is found
        /// Increments mSpectraTypeClassifier.CentroidedSpectra if the data is centroided
        /// </remarks>
        // ReSharper disable once UnusedMember.Global
        public void ClassifySpectrum(
            int ionCount,
            double[] mzArray,
            int msLevel,
            clsSpectrumTypeClassifier.eCentroidStatusConstants centroidingStatus,
            string spectrumTitle)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, mzArray, msLevel, centroidingStatus, spectrumTitle);
        }

        /// <summary>
        /// Clear cached data
        /// </summary>
        public void ClearCachedData()
        {
            mDatasetScanStats.Clear();
            mDatasetSummaryStats.Clear();

            DatasetFileInfo.Clear();
            SampleInfo.Clear();

            mDatasetSummaryStatsUpToDate = false;

            mSpectraTypeClassifier.Reset();

            CreateEmptyScanStatsFiles = true;
        }

        /// <summary>
        /// Summarizes the scan info in scanStats()
        /// </summary>
        /// <param name="scanStats">ScanStats data to parse</param>
        /// <param name="summaryStats">Stats output</param>
        /// <returns>>True if success, false if error</returns>
        /// <remarks></remarks>
        public bool ComputeScanStatsSummary(List<clsScanStatsEntry> scanStats, out clsDatasetSummaryStats summaryStats)
        {
            var ticListMSCount = 0;
            var ticListMSnCount = 0;
            var bpiListMSCount = 0;
            var bpiListMSnCount = 0;

            // Initialize summaryStats
            summaryStats = new clsDatasetSummaryStats();

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue");
                    return false;
                }

                ErrorMessage = "";

                var scanStatsCount = scanStats.Count;

                // Initialize the TIC and BPI List arrays
                var ticListMS = new double[scanStatsCount];
                var bpiListMS = new double[scanStatsCount];

                var ticListMSn = new double[scanStatsCount];
                var bpiListMSn = new double[scanStatsCount];

                foreach (var statEntry in scanStats)
                {

                    if (statEntry.ScanType > 1)
                    {
                        // MSn spectrum
                        ComputeScanStatsUpdateDetails(statEntry, ref summaryStats.ElutionTimeMax, ref summaryStats.MSnStats, ticListMSn, ref ticListMSnCount, bpiListMSn, ref bpiListMSnCount);
                    }
                    else
                    {
                        // MS spectrum
                        ComputeScanStatsUpdateDetails(statEntry, ref summaryStats.ElutionTimeMax, ref summaryStats.MSStats, ticListMS, ref ticListMSCount, bpiListMS, ref bpiListMSCount);
                    }

                    var scanTypeKey = statEntry.ScanTypeName + SCAN_TYPE_STATS_SEP_CHAR + statEntry.ScanFilterText;
                    if (summaryStats.ScanTypeStats.ContainsKey(scanTypeKey))
                    {
                        summaryStats.ScanTypeStats[scanTypeKey] += 1;
                    }
                    else
                    {
                        summaryStats.ScanTypeStats.Add(scanTypeKey, 1);
                    }
                }

                summaryStats.MSStats.TICMedian = ComputeMedian(ticListMS, ticListMSCount);
                summaryStats.MSStats.BPIMedian = ComputeMedian(bpiListMS, bpiListMSCount);

                summaryStats.MSnStats.TICMedian = ComputeMedian(ticListMSn, ticListMSnCount);
                summaryStats.MSnStats.BPIMedian = ComputeMedian(bpiListMSn, bpiListMSnCount);

                return true;

            }
            catch (Exception ex)
            {
                ReportError("Error in ComputeScanStatsSummary: " + ex.Message);
                return false;
            }

        }

        private void ComputeScanStatsUpdateDetails(
            clsScanStatsEntry scanStats,
            ref double elutionTimeMax,
            ref clsDatasetSummaryStats.udtSummaryStatDetailsType udtSummaryStatDetails,
            IList<double> ticList,
            ref int ticListCount,
            IList<double> bpiList,
            ref int bpiListCount)
        {

            if (!string.IsNullOrEmpty(scanStats.ElutionTime))
            {
                if (double.TryParse(scanStats.ElutionTime, out var elutionTime))
                {
                    if (elutionTime > elutionTimeMax)
                    {
                        elutionTimeMax = elutionTime;
                    }
                }
            }

            if (double.TryParse(scanStats.TotalIonIntensity, out var tic))
            {
                if (tic > udtSummaryStatDetails.TICMax)
                {
                    udtSummaryStatDetails.TICMax = tic;
                }

                ticList[ticListCount] = tic;
                ticListCount += 1;
            }

            if (double.TryParse(scanStats.BasePeakIntensity, out var bpi))
            {
                if (bpi > udtSummaryStatDetails.BPIMax)
                {
                    udtSummaryStatDetails.BPIMax = bpi;
                }

                bpiList[bpiListCount] = bpi;
                bpiListCount += 1;
            }

            udtSummaryStatDetails.ScanCount += 1;

        }

        private double ComputeMedian(IReadOnlyList<double> values, int itemCount)
        {

            var lstData = new List<double>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                lstData.Add(values[i]);
            }

            var medianValue = mMedianUtils.Median(lstData);

            return medianValue;

        }

        /// <summary>
        /// Creates an XML file summarizing the data stored in this class (in mDatasetScanStats, Me.DatasetFileInfo, and Me.SampleInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateDatasetInfoFile(string datasetName, string datasetInfoFilePath)
        {
            return CreateDatasetInfoFile(datasetName, datasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates an XML file summarizing the data in scanStats and datasetFileInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateDatasetInfoFile(
            string datasetName,
            string datasetInfoFilePath,
            List<clsScanStatsEntry> scanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            bool success;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in CreateDatasetInfoFile");
                    return false;
                }

                ErrorMessage = "";

                // If CreateDatasetInfoXML() used a StringBuilder to cache the XML data, then we would have to use Encoding.Unicode
                // However, CreateDatasetInfoXML() now uses a MemoryStream, so we're able to use UTF8
                using (var writer = new StreamWriter(new FileStream(datasetInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8))
                {
                    writer.WriteLine(CreateDatasetInfoXML(datasetName, scanStats, datasetFileInfo, udtSampleInfo));
                }

                success = true;

            }
            catch (Exception ex)
            {
                ReportError("Error in CreateDatasetInfoFile: " + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Creates XML summarizing the data stored in this class (in mDatasetScanStats, Me.DatasetFileInfo, and Me.SampleInfo)
        /// Auto-determines the dataset name using Me.DatasetFileInfo.DatasetName
        /// </summary>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML()
        {
            return CreateDatasetInfoXML(DatasetFileInfo.DatasetName, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data stored in this class (in mDatasetScanStats, Me.DatasetFileInfo, and Me.SampleInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(string datasetName)
        {
            return CreateDatasetInfoXML(datasetName, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetFileInfo
        /// Auto-determines the dataset name using datasetFileInfo.DatasetName
        /// </summary>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(List<clsScanStatsEntry> scanStats, clsDatasetFileInfo datasetFileInfo)
        {
            var udtSampleInfo = new udtSampleInfoType();
            udtSampleInfo.Clear();

            return CreateDatasetInfoXML(datasetFileInfo.DatasetName, scanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats, datasetFileInfo, and udtSampleInfo
        /// Auto-determines the dataset name using datasetFileInfo.DatasetName
        /// </summary>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(List<clsScanStatsEntry> scanStats, clsDatasetFileInfo datasetFileInfo, udtSampleInfoType udtSampleInfo)
        {
            return CreateDatasetInfoXML(datasetFileInfo.DatasetName, scanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetFileInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        // ReSharper disable once UnusedMember.Global
        public string CreateDatasetInfoXML(string datasetName, ref List<clsScanStatsEntry> scanStats, clsDatasetFileInfo datasetFileInfo)
        {

            var udtSampleInfo = new udtSampleInfoType();
            udtSampleInfo.Clear();

            return CreateDatasetInfoXML(datasetName, scanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in scanStats and datasetFileInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo"></param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(
            string datasetName,
            List<clsScanStatsEntry> scanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            var includeCentroidStats = false;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in CreateDatasetInfoXML");
                    return string.Empty;
                }

                ErrorMessage = "";

                clsDatasetSummaryStats summaryStats;
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

                writer.WriteElementString("Dataset", datasetName);

                writer.WriteStartElement("ScanTypes");

                foreach (var scanTypeEntry in summaryStats.ScanTypeStats)
                {
                    var scanType = scanTypeEntry.Key;
                    var indexMatch = scanType.IndexOf(SCAN_TYPE_STATS_SEP_CHAR, StringComparison.Ordinal);

                    string scanFilterText;
                    if (indexMatch >= 0)
                    {
                        scanFilterText = scanType.Substring(indexMatch + SCAN_TYPE_STATS_SEP_CHAR.Length);
                        if (indexMatch > 0)
                        {
                            scanType = scanType.Substring(0, indexMatch);
                        }
                        else
                        {
                            scanType = string.Empty;
                        }
                    }
                    else
                    {
                        scanFilterText = string.Empty;
                    }

                    writer.WriteStartElement("ScanType");
                    writer.WriteAttributeString("ScanCount", scanTypeEntry.Value.ToString());
                    writer.WriteAttributeString("ScanFilterText", FixNull(scanFilterText));
                    writer.WriteString(scanType);
                    writer.WriteEndElement();
                    // ScanType EndElement
                }

                writer.WriteEndElement();
                // ScanTypes

                writer.WriteStartElement("AcquisitionInfo");

                var scanCountTotal = summaryStats.MSStats.ScanCount + summaryStats.MSnStats.ScanCount;
                if (scanCountTotal == 0 && datasetFileInfo.ScanCount > 0)
                {
                    scanCountTotal = datasetFileInfo.ScanCount;
                }

                writer.WriteElementString("ScanCount", scanCountTotal.ToString());

                writer.WriteElementString("ScanCountMS", summaryStats.MSStats.ScanCount.ToString());
                writer.WriteElementString("ScanCountMSn", summaryStats.MSnStats.ScanCount.ToString());
                writer.WriteElementString("Elution_Time_Max", summaryStats.ElutionTimeMax.ToString("0.00"));

                writer.WriteElementString("AcqTimeMinutes", datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes.ToString("0.00"));
                writer.WriteElementString("StartTime", datasetFileInfo.AcqTimeStart.ToString("yyyy-MM-dd hh:mm:ss tt"));
                writer.WriteElementString("EndTime", datasetFileInfo.AcqTimeEnd.ToString("yyyy-MM-dd hh:mm:ss tt"));

                // For datasets based on a single file, this is the file's size
                // For datasets stored in a directory, this is the total size of the primary instrument files
                writer.WriteElementString("FileSizeBytes", datasetFileInfo.FileSizeBytes.ToString());

                if (datasetFileInfo.InstrumentFiles.Count > 0)
                {

                    writer.WriteStartElement("InstrumentFiles");

                    foreach (var instrumentFile in datasetFileInfo.InstrumentFiles)
                    {
                        writer.WriteStartElement("InstrumentFile");
                        writer.WriteAttributeString("Hash", FixNull(instrumentFile.Value.Hash));
                        writer.WriteAttributeString("HashType", instrumentFile.Value.HashType.ToString());
                        writer.WriteAttributeString("Size", instrumentFile.Value.Length.ToString());
                        writer.WriteString(instrumentFile.Key);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    // InstrumentFiles
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

                writer.WriteEndElement();
                // AcquisitionInfo EndElement

                writer.WriteStartElement("TICInfo");
                writer.WriteElementString("TIC_Max_MS", StringUtilities.ValueToString(summaryStats.MSStats.TICMax, 5));
                writer.WriteElementString("TIC_Max_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.TICMax, 5));
                writer.WriteElementString("BPI_Max_MS", StringUtilities.ValueToString(summaryStats.MSStats.BPIMax, 5));
                writer.WriteElementString("BPI_Max_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.BPIMax, 5));
                writer.WriteElementString("TIC_Median_MS", StringUtilities.ValueToString(summaryStats.MSStats.TICMedian, 5));
                writer.WriteElementString("TIC_Median_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.TICMedian, 5));
                writer.WriteElementString("BPI_Median_MS", StringUtilities.ValueToString(summaryStats.MSStats.BPIMedian, 5));
                writer.WriteElementString("BPI_Median_MSn", StringUtilities.ValueToString(summaryStats.MSnStats.BPIMedian, 5));
                writer.WriteEndElement();
                // TICInfo EndElement

                // Only write the SampleInfo block if udtSampleInfo contains entries
                if (udtSampleInfo.HasData())
                {
                    writer.WriteStartElement("SampleInfo");
                    writer.WriteElementString("SampleName", FixNull(udtSampleInfo.SampleName));
                    writer.WriteElementString("Comment1", FixNull(udtSampleInfo.Comment1));
                    writer.WriteElementString("Comment2", FixNull(udtSampleInfo.Comment2));
                    writer.WriteEndElement();
                    // SampleInfo EndElement
                }

                writer.WriteEndElement();
                //End the "Root" element (DatasetInfo)
                writer.WriteEndDocument();
                //End the document

                writer.Close();

                // Now Rewind the memory stream and output as a string
                memStream.Position = 0;
                var reader = new StreamReader(memStream);

                // Return the XML as text
                return reader.ReadToEnd();

            }
            catch (Exception ex)
            {
                ReportError("Error in CreateDatasetInfoXML: " + ex.Message);
            }

            // This code will only be reached if an exception occurs
            return string.Empty;

        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="scanStatsFilePath">File path to write the text file to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateScanStatsFile(string scanStatsFilePath)
        {
            return CreateScanStatsFile(scanStatsFilePath, mDatasetScanStats, DatasetFileInfo);
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="scanStatsFilePath">File path to write the text file to</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateScanStatsFile(
            string scanStatsFilePath,
            List<clsScanStatsEntry> scanStats,
            clsDatasetFileInfo datasetFileInfo)
        {
            var datasetID = datasetFileInfo.DatasetID;

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

                ErrorMessage = "";

                // Define the path to the extended scan stats file
                var scanStatsFile = new FileInfo(scanStatsFilePath);
                if (scanStatsFile.DirectoryName == null)
                {
                    ReportError("Unable to determine the parent directory for " + scanStatsFilePath);
                    return false;
                }

                var scanStatsExFilePath = Path.Combine(scanStatsFile.DirectoryName, Path.GetFileNameWithoutExtension(scanStatsFile.Name) + "Ex.txt");

                // Open the output files
                using (var scanStatsWriter = new StreamWriter(new FileStream(scanStatsFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read)))
                using (var scanStatsExWriter = new StreamWriter(new FileStream(scanStatsExFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {



                    // Write the headers
                    var dataValues = new List<string>
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


                    scanStatsWriter.WriteLine(string.Join("\t", dataValues));

                    dataValues.Clear();
                    dataValues.Add("Dataset");
                    dataValues.Add("ScanNumber");
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_ION_INJECTION_TIME);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_SCAN_SEGMENT);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_SCAN_EVENT);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_CHARGE_STATE);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_MONOISOTOPIC_MZ);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_COLLISION_MODE);
                    dataValues.Add(clsScanStatsEntry.SCAN_STATS_COL_SCAN_FILTER_TEXT);

                    scanStatsExWriter.WriteLine(string.Join("\t", dataValues));

                    foreach (var scanStatsEntry in scanStats)
                    {

                        dataValues.Clear();

                        // Dataset number (aka Dataset ID)
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

                }

                return true;

            }
            catch (Exception ex)
            {
                ReportError("Error in CreateScanStatsFile: " + ex.Message);
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

        public clsDatasetSummaryStats GetDatasetSummaryStats()
        {

            if (!mDatasetSummaryStatsUpToDate)
            {
                ComputeScanStatsSummary(mDatasetScanStats, out mDatasetSummaryStats);
                mDatasetSummaryStatsUpToDate = true;
            }

            return mDatasetSummaryStats;

        }

        private void ReportError(string message)
        {
            ErrorMessage = string.Copy(message);
            OnErrorEvent(message);
        }

        /// <summary>
        /// Updates the scan type information for the specified scan number
        /// </summary>
        /// <param name="scanNumber"></param>
        /// <param name="scanType"></param>
        /// <param name="scanTypeName"></param>
        /// <returns>True if the scan was found and updated; otherwise false</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetScanType(int scanNumber, int scanType, string scanTypeName)
        {

            var matchFound = false;

            // Look for scan scanNumber in mDatasetScanStats
            for (var index = 0; index <= mDatasetScanStats.Count - 1; index++)
            {
                if (mDatasetScanStats[index].ScanNumber == scanNumber)
                {
                    mDatasetScanStats[index].ScanType = scanType;
                    mDatasetScanStats[index].ScanTypeName = scanTypeName;
                    mDatasetSummaryStatsUpToDate = false;

                    matchFound = true;
                    break;
                }
            }

            return matchFound;

        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and Me.DatasetFileInfo)
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetStatsTextFile(string datasetName, string datasetInfoFilePath)
        {
            return UpdateDatasetStatsTextFile(datasetName, datasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data in scanStats and datasetFileInfo
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetStatsFilePath">Tab-delimited file to create/update</param>
        /// <param name="scanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetStatsTextFile(
            string datasetName,
            string datasetStatsFilePath,
            List<clsScanStatsEntry> scanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            var writeHeaders = false;

            bool success;

            try
            {
                if (scanStats == null)
                {
                    ReportError("scanStats is Nothing; unable to continue in UpdateDatasetStatsTextFile");
                    return false;
                }

                ErrorMessage = "";

                clsDatasetSummaryStats summaryStats;
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

                // Create or open the output file
                using (var writer = new StreamWriter(new FileStream(datasetStatsFilePath, FileMode.Append, FileAccess.Write, FileShare.Read)))
                {
                    var dataValues = new List<string>();

                    if (writeHeaders)
                    {
                        // Write the header line
                        dataValues.Add("Dataset");
                        dataValues.Add("ScanCount");
                        dataValues.Add("ScanCountMS");
                        dataValues.Add("ScanCountMSn");
                        dataValues.Add("Elution_Time_Max");
                        dataValues.Add("AcqTimeMinutes");
                        dataValues.Add("StartTime");
                        dataValues.Add("EndTime");
                        dataValues.Add("FileSizeBytes");
                        dataValues.Add("SampleName");
                        dataValues.Add("Comment1");
                        dataValues.Add("Comment2");

                        writer.WriteLine(string.Join("\t", dataValues));
                    }

                    dataValues.Clear();
                    dataValues.Add(datasetName);
                    dataValues.Add((summaryStats.MSStats.ScanCount + summaryStats.MSnStats.ScanCount).ToString());
                    dataValues.Add(summaryStats.MSStats.ScanCount.ToString());
                    dataValues.Add(summaryStats.MSnStats.ScanCount.ToString());
                    dataValues.Add(summaryStats.ElutionTimeMax.ToString("0.00"));
                    dataValues.Add(datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes.ToString("0.00"));
                    dataValues.Add(datasetFileInfo.AcqTimeStart.ToString("yyyy-MM-dd hh:mm:ss tt"));
                    dataValues.Add(datasetFileInfo.AcqTimeEnd.ToString("yyyy-MM-dd hh:mm:ss tt"));
                    dataValues.Add(datasetFileInfo.FileSizeBytes.ToString());
                    dataValues.Add(FixNull(udtSampleInfo.SampleName));
                    dataValues.Add(FixNull(udtSampleInfo.Comment1));
                    dataValues.Add(FixNull(udtSampleInfo.Comment2));

                    writer.WriteLine(string.Join("\t", dataValues));

                }

                success = true;

            }
            catch (Exception ex)
            {
                ReportError("Error in UpdateDatasetStatsTextFile: " + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Examine the minimum m/z value in MS2 spectra
        /// Keep track of the number of spectra where the minimum m/z value is greater than MS2MzMin
        /// Raise an error if at least 10% of the spectra have a minimum m/z higher than the threshold
        /// Log a warning if some spectra, but fewer than 10% of the total, have a minimum higher than the threshold
        /// </summary>
        /// <param name="requiredMzMin">Minimum m/z threshold; the </param>
        /// <param name="errorOrWarningMsg"></param>
        /// <param name="maxPercentAllowedFailed"></param>
        /// <returns>True if valid data, false if at least 10% of the spectra has a minimum m/z higher than the threshold</returns>
        /// <remarks>
        /// If a dataset has a mix of MS2 and MS3 spectra, and if all of the MS3 spectra meet the minimum m/z requirement, a warning is not raised
        /// Example dataset: UCLA_Dun_TMT_set2_03_QE_24May19_Rage_Rep-19-04-r01
        /// </remarks>
        public bool ValidateMS2MzMin(float requiredMzMin, out string errorOrWarningMsg, int maxPercentAllowedFailed)
        {

            // First examine MS2 spectra (or higher)
            var validMS2 = ValidateMSnMzMin(
                2,
                requiredMzMin, maxPercentAllowedFailed,
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
                out var scanCountWithDataMS3,
                out var messageMS3);

            if (scanCountWithDataMS3 > 0 && validMS3)
            {
                errorOrWarningMsg = messageMS3;
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

        private bool ValidateMSnMzMin(
            int msLevel,
            float requiredMzMin,
            int maxPercentAllowedFailed,
            out int scanCountWithData,
            out string errorOrWarningMsg)
        {

            scanCountWithData = 0;
            var scanCountInvalid = 0;

            foreach (var scan in mDatasetScanStats)
            {
                if (scan.ScanType != msLevel)
                    continue;

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

            if (scanCountWithData == 0)
            {
                // None of the MS2 spectra has data; cannot validate
                errorOrWarningMsg = string.Format("None of the {0} spectra has data; cannot validate", spectraType);
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

    public class clsScanStatsEntry
    {
        public const string SCAN_STATS_COL_ION_INJECTION_TIME = "Ion Injection Time (ms)";
        public const string SCAN_STATS_COL_SCAN_SEGMENT = "Scan Segment";
        public const string SCAN_STATS_COL_SCAN_EVENT = "Scan Event";
        public const string SCAN_STATS_COL_CHARGE_STATE = "Charge State";
        public const string SCAN_STATS_COL_MONOISOTOPIC_MZ = "Monoisotopic M/Z";
        public const string SCAN_STATS_COL_COLLISION_MODE = "Collision Mode";

        public const string SCAN_STATS_COL_SCAN_FILTER_TEXT = "Scan Filter Text";

        public struct udtExtendedStatsInfoType
        {
            public string IonInjectionTime;
            public string ScanSegment;
            public string ScanEvent;        // Only defined for LTQ-Orbitrap datasets and only for fragmentation spectra where the instrument could determine the charge and m/z
            public string ChargeState;      // Only defined for LTQ-Orbitrap datasets and only for fragmentation spectra where the instrument could determine the charge and m/z
            public string MonoisotopicMZ;
            public string CollisionMode;
            public string ScanFilterText;

            public void Clear()
            {
                IonInjectionTime = string.Empty;
                ScanSegment = string.Empty;
                ScanEvent = string.Empty;
                ChargeState = string.Empty;
                MonoisotopicMZ = string.Empty;
                CollisionMode = string.Empty;
                ScanFilterText = string.Empty;
            }
        }

        public int ScanNumber;

        /// <summary>
        /// Scan Type (aka MSLevel)
        /// </summary>
        /// <remarks>1 for MS, 2 for MS2, 3 for MS3</remarks>
        public int ScanType;

        // Example values: "FTMS + p NSI Full ms [400.00-2000.00]" or "ITMS + c ESI Full ms [300.00-2000.00]" or "ITMS + p ESI d Z ms [1108.00-1118.00]" or "ITMS + c ESI d Full ms2 342.90@cid35.00"
        public string ScanFilterText;

        // Example values: MS, HMS, Zoom, CID-MSn, or PQD-MSn
        public string ScanTypeName;

        // The following are strings to prevent the number formatting from changing
        public string ElutionTime;
        public string TotalIonIntensity;
        public string BasePeakIntensity;
        public string BasePeakMZ;

        public string BasePeakSignalToNoiseRatio;
        public int IonCount;

        public int IonCountRaw;

        /// <summary>
        /// Smallest m/z value in the scan
        /// </summary>
        public double MzMin;

        /// <summary>
        /// Largest m/z value in the scan
        /// </summary>
        public double MzMax;

        // Only used for Thermo data
        public udtExtendedStatsInfoType ExtendedScanInfo;

        public void Clear()
        {
            ScanNumber = 0;
            ScanType = 0;

            ScanFilterText = string.Empty;
            ScanTypeName = string.Empty;

            ElutionTime = "0";
            TotalIonIntensity = "0";
            BasePeakIntensity = "0";
            BasePeakMZ = "0";
            BasePeakSignalToNoiseRatio = "0";

            IonCount = 0;
            IonCountRaw = 0;

            MzMin = 0;
            MzMax = 0;

            ExtendedScanInfo.Clear();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsScanStatsEntry()
        {
            Clear();
        }
    }

    public class clsDatasetSummaryStats
    {

        public double ElutionTimeMax;
        public udtSummaryStatDetailsType MSStats;

        public udtSummaryStatDetailsType MSnStats;

        // The following collection keeps track of each ScanType in the dataset, along with the number of scans of this type
        // Example scan types:  FTMS + p NSI Full ms" or "ITMS + c ESI Full ms" or "ITMS + p ESI d Z ms" or "ITMS + c ESI d Full ms2 @cid35.00"
        public readonly Dictionary<string, int> ScanTypeStats;

        public struct udtSummaryStatDetailsType
        {
            public int ScanCount;
            public double TICMax;
            public double BPIMax;
            public double TICMedian;
            public double BPIMedian;
        }

        public void Clear()
        {
            ElutionTimeMax = 0;

            MSStats.ScanCount = 0;
            MSStats.TICMax = 0;
            MSStats.BPIMax = 0;
            MSStats.TICMedian = 0;
            MSStats.BPIMedian = 0;

            MSnStats.ScanCount = 0;
            MSnStats.TICMax = 0;
            MSnStats.BPIMax = 0;
            MSnStats.TICMedian = 0;
            MSnStats.BPIMedian = 0;

            ScanTypeStats.Clear();

        }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetSummaryStats()
        {
            ScanTypeStats = new Dictionary<string, int>();
            Clear();
        }

    }

}

