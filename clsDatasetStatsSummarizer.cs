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
// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
// Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov
// -------------------------------------------------------------------------------
//
// Licensed under the Apache License, Version 2.0; you may not use this file except
// in compliance with the License.  You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//

namespace MSFileInfoScanner
{

    public class clsDatasetStatsSummarizer : clsEventNotifier
    {

        #region "Constants and Enums"
        public const string SCANTYPE_STATS_SEPCHAR = "::###::";
        public const string DATASET_INFO_FILE_SUFFIX = "_DatasetInfo.xml";
        public const string DEFAULT_DATASET_STATS_FILENAME = "MSFileInfo_DatasetStats.txt";
        #endregion

        #region "Structures"

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
                if (!string.IsNullOrEmpty(SampleName) || !string.IsNullOrEmpty(Comment1) || !string.IsNullOrEmpty(Comment2)) {
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region "Classwide Variables"

        private string mDatasetStatsSummaryFileName;

        private string mErrorMessage;
        private readonly List<clsScanStatsEntry> mDatasetScanStats;

        public udtSampleInfoType SampleInfo;
        private clsSpectrumTypeClassifier mSpectraTypeClassifier;

        private clsSpectrumTypeClassifier SpectraTypeClassifier {
            get => mSpectraTypeClassifier;
            set {
                if (mSpectraTypeClassifier != null)
                {
                    mSpectraTypeClassifier.ErrorEvent -= mSpectraTypeClassifier_ErrorEvent;
                }
                mSpectraTypeClassifier = value;
                if (mSpectraTypeClassifier != null)
                {
                    mSpectraTypeClassifier.ErrorEvent += mSpectraTypeClassifier_ErrorEvent;
                }
            }

        }
        private bool mDatasetSummaryStatsUpToDate;

        private clsDatasetSummaryStats mDatasetSummaryStats;

        private readonly clsMedianUtilities mMedianUtils;

        #endregion

        #region "Properties"

        public clsDatasetFileInfo DatasetFileInfo { get; set; }

        public string DatasetStatsSummaryFileName {
            get => mDatasetStatsSummaryFileName;
            set {
                if ((value != null)) {
                    mDatasetStatsSummaryFileName = value;
                }
            }
        }

        public string ErrorMessage => mErrorMessage;

        public string FileDate { get; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetStatsSummarizer()
        {
            FileDate = "May 24, 2016";

            mErrorMessage = string.Empty;

            mMedianUtils = new clsMedianUtilities();
            mSpectraTypeClassifier = new clsSpectrumTypeClassifier();

            mDatasetScanStats = new List<clsScanStatsEntry>();
            mDatasetSummaryStats = new clsDatasetSummaryStats();

            DatasetFileInfo = new clsDatasetFileInfo();
            SampleInfo = new udtSampleInfoType();

            ClearCachedData();

        }

        public void AddDatasetScan(clsScanStatsEntry objScanStats)
        {
            mDatasetScanStats.Add(objScanStats);
            mDatasetSummaryStatsUpToDate = false;

        }

        public void ClassifySpectrum(List<double> lstMZs, int msLevel)
        {
            ClassifySpectrum(lstMZs, msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants.Unknown);
        }

        public void ClassifySpectrum(List<double> lstMZs, int msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants centroidingStatus)
        {
            mSpectraTypeClassifier.CheckSpectrum(lstMZs, msLevel, centroidingStatus);
        }

        public void ClassifySpectrum(double[] dblMZs, int msLevel)
        {
            mSpectraTypeClassifier.CheckSpectrum(dblMZs, msLevel);
        }

        public void ClassifySpectrum(int ionCount, double[] dblMZs, int msLevel)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, dblMZs, msLevel);
        }

        public void ClassifySpectrum(int ionCount, double[] dblMZs, int msLevel, clsSpectrumTypeClassifier.eCentroidStatusConstants centroidingStatus)
        {
            mSpectraTypeClassifier.CheckSpectrum(ionCount, dblMZs, msLevel, centroidingStatus);
        }

        public void ClearCachedData()
        {
            mDatasetScanStats.Clear();
            mDatasetSummaryStats.Clear();

            DatasetFileInfo.Clear();
            SampleInfo.Clear();

            mDatasetSummaryStatsUpToDate = false;

            mSpectraTypeClassifier.Reset();

        }

        /// <summary>
        /// Summarizes the scan info in objScanStats()
        /// </summary>
        /// <param name="objScanStats">ScanStats data to parse</param>
        /// <param name="objSummaryStats">Stats output</param>
        /// <returns>>True if success, false if error</returns>
        /// <remarks></remarks>
        public bool ComputeScanStatsSummary(List<clsScanStatsEntry> objScanStats, out clsDatasetSummaryStats objSummaryStats)
        {
            var intTICListMSCount = 0;
            var intTICListMSnCount = 0;
            var intBPIListMSCount = 0;
            var intBPIListMSnCount = 0;

            // Initialize objSummaryStats
            objSummaryStats = new clsDatasetSummaryStats();

            try {
                if (objScanStats == null) {
                    ReportError("objScanStats is Nothing; unable to continue");
                    return false;
                }

                mErrorMessage = "";

                var intScanStatsCount = objScanStats.Count;

                // Initialize the TIC and BPI List arrays
                var dblTICListMS = new double[intScanStatsCount];
                var dblBPIListMS = new double[intScanStatsCount];

                var dblTICListMSn = new double[intScanStatsCount];
                var dblBPIListMSn = new double[intScanStatsCount];

                foreach (var objEntry in objScanStats) {

                    if (objEntry.ScanType > 1) {
                        // MSn spectrum
                        ComputeScanStatsUpdateDetails(objEntry, ref objSummaryStats.ElutionTimeMax, ref objSummaryStats.MSnStats, dblTICListMSn, ref intTICListMSnCount, dblBPIListMSn, ref intBPIListMSnCount);
                    } else {
                        // MS spectrum
                        ComputeScanStatsUpdateDetails(objEntry, ref objSummaryStats.ElutionTimeMax, ref objSummaryStats.MSStats, dblTICListMS, ref intTICListMSCount, dblBPIListMS, ref intBPIListMSCount);
                    }

                    var strScanTypeKey = objEntry.ScanTypeName + SCANTYPE_STATS_SEPCHAR + objEntry.ScanFilterText;
                    if (objSummaryStats.objScanTypeStats.ContainsKey(strScanTypeKey)) {
                        objSummaryStats.objScanTypeStats[strScanTypeKey] += 1;
                    } else {
                        objSummaryStats.objScanTypeStats.Add(strScanTypeKey, 1);
                    }
                }

                objSummaryStats.MSStats.TICMedian = ComputeMedian(dblTICListMS, intTICListMSCount);
                objSummaryStats.MSStats.BPIMedian = ComputeMedian(dblBPIListMS, intBPIListMSCount);

                objSummaryStats.MSnStats.TICMedian = ComputeMedian(dblTICListMSn, intTICListMSnCount);
                objSummaryStats.MSnStats.BPIMedian = ComputeMedian(dblBPIListMSn, intBPIListMSnCount);

                return true;

            } catch (Exception ex) {
                ReportError("Error in ComputeScanStatsSummary: " + ex.Message);
                return false;
            }

        }

        private void ComputeScanStatsUpdateDetails(
            clsScanStatsEntry objScanStats,
            ref double dblElutionTimeMax,
            ref clsDatasetSummaryStats.udtSummaryStatDetailsType udtSummaryStatDetails,
            IList<double> dblTICList,
            ref int intTICListCount,
            IList<double> dblBPIList,
            ref int intBPIListCount)
        {

            if (!string.IsNullOrEmpty(objScanStats.ElutionTime))
            {
                if (double.TryParse(objScanStats.ElutionTime, out var dblElutionTime))
                {
                    if (dblElutionTime > dblElutionTimeMax)
                    {
                        dblElutionTimeMax = dblElutionTime;
                    }
                }
            }

            if (double.TryParse(objScanStats.TotalIonIntensity, out var dblTIC)) {
                if (dblTIC > udtSummaryStatDetails.TICMax) {
                    udtSummaryStatDetails.TICMax = dblTIC;
                }

                dblTICList[intTICListCount] = dblTIC;
                intTICListCount += 1;
            }

            if (double.TryParse(objScanStats.BasePeakIntensity, out var dblBPI)) {
                if (dblBPI > udtSummaryStatDetails.BPIMax) {
                    udtSummaryStatDetails.BPIMax = dblBPI;
                }

                dblBPIList[intBPIListCount] = dblBPI;
                intBPIListCount += 1;
            }

            udtSummaryStatDetails.ScanCount += 1;

        }

        private double ComputeMedian(IReadOnlyList<double> dblList, int intItemCount)
        {

            var lstData = new List<double>(intItemCount);
            for (var i = 0; i <= intItemCount - 1; i++) {
                lstData.Add(dblList[i]);
            }

            var dblMedian1 = mMedianUtils.Median(lstData);

            return dblMedian1;

        }

        /// <summary>
        /// Creates an XML file summarizing the data stored in this class (in mDatasetScanStats, Me.DatasetFileInfo, and Me.SampleInfo)
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strDatasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateDatasetInfoFile(string strDatasetName, string strDatasetInfoFilePath)
        {

            return CreateDatasetInfoFile(strDatasetName, strDatasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates an XML file summarizing the data in objScanStats and datasetFileInfo
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strDatasetInfoFilePath">File path to write the XML to</param>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateDatasetInfoFile(
            string strDatasetName,
            string strDatasetInfoFilePath,
            List<clsScanStatsEntry> objScanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            bool blnSuccess;

            try {
                if (objScanStats == null) {
                    ReportError("objScanStats is Nothing; unable to continue in CreateDatasetInfoFile");
                    return false;
                }

                mErrorMessage = "";

                // If CreateDatasetInfoXML() used a StringBuilder to cache the XML data, then we would have to use Encoding.Unicode
                // However, CreateDatasetInfoXML() now uses a MemoryStream, so we're able to use UTF8
                using (var swOutFile = new StreamWriter(new FileStream(strDatasetInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8)) {

                    swOutFile.WriteLine(CreateDatasetInfoXML(strDatasetName, objScanStats, datasetFileInfo, udtSampleInfo));

                }

                blnSuccess = true;

            } catch (Exception ex) {
                ReportError("Error in CreateDatasetInfoFile: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

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
        /// <param name="strDatasetName">Dataset Name</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(string strDatasetName)
        {
            return CreateDatasetInfoXML(strDatasetName, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in objScanStats and datasetFileInfo
        /// Auto-determines the dataset name using datasetFileInfo.DatasetName
        /// </summary>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(List<clsScanStatsEntry> objScanStats, clsDatasetFileInfo datasetFileInfo)
        {
            var udtSampleInfo = new udtSampleInfoType();
            udtSampleInfo.Clear();

            return CreateDatasetInfoXML(datasetFileInfo.DatasetName, objScanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in objScanStats, datasetFileInfo, and udtSampleInfo
        /// Auto-determines the dataset name using datasetFileInfo.DatasetName
        /// </summary>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(List<clsScanStatsEntry> objScanStats, clsDatasetFileInfo datasetFileInfo, udtSampleInfoType udtSampleInfo)
        {

            return CreateDatasetInfoXML(datasetFileInfo.DatasetName, objScanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in objScanStats and datasetFileInfo
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(string strDatasetName, ref List<clsScanStatsEntry> objScanStats, clsDatasetFileInfo datasetFileInfo)
        {

            var udtSampleInfo = new udtSampleInfoType();
            udtSampleInfo.Clear();

            return CreateDatasetInfoXML(strDatasetName, objScanStats, datasetFileInfo, udtSampleInfo);
        }

        /// <summary>
        /// Creates XML summarizing the data in objScanStats and datasetFileInfo
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo"></param>
        /// <returns>XML (as string)</returns>
        /// <remarks></remarks>
        public string CreateDatasetInfoXML(
            string strDatasetName,
            List<clsScanStatsEntry> objScanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            var includeCentroidStats = false;

            try {
                if (objScanStats == null) {
                    ReportError("objScanStats is Nothing; unable to continue in CreateDatasetInfoXML");
                    return string.Empty;
                }

                mErrorMessage = "";

                clsDatasetSummaryStats objSummaryStats;
                if (objScanStats == mDatasetScanStats) {
                    objSummaryStats = GetDatasetSummaryStats();

                    if (mSpectraTypeClassifier.TotalSpectra() > 0) {
                        includeCentroidStats = true;
                    }

                } else {

                    // Parse the data in objScanStats to compute the bulk values
                    var success = ComputeScanStatsSummary(objScanStats, out objSummaryStats);
                    if (!success)
                    {
                        ReportError("ComputeScanStatsSummary returned false; unable to continue in CreateDatasetInfoXML");
                        return string.Empty;
                    }
                    includeCentroidStats = false;
                }

                var objXMLSettings = new XmlWriterSettings
                {
                    CheckCharacters = true,
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = Encoding.UTF8,
                    CloseOutput = false     // Do not close output automatically so that MemoryStream can be read after the XmlWriter has been closed
                };

                // We could cache the text using a StringBuilder, like this:
                //
                // var sbDatasetInfo = new StringBuilder();
                // var objStringWriter = new StringWriter(sbDatasetInfo);
                // var objDSInfo = new XmlTextWriter(objStringWriter)
                // {
                //     Formatting = Formatting.Indented,
                //     Indentation = 2
                // };

                // However, when you send the output to a StringBuilder it is always encoded as Unicode (UTF-16)
                //  since this is the only character encoding used in the .NET Framework for String values,
                //  and thus you'll see the attribute encoding="utf-16" in the opening XML declaration
                // The alternative is to use a MemoryStream.  Here, the stream encoding is set by the XmlWriter
                //  and so you see the attribute encoding="utf-8" in the opening XML declaration encoding
                //  (since we used objXMLSettings.Encoding = Encoding.UTF8)
                //
                var objMemStream = new MemoryStream();
                var objDSInfo = XmlWriter.Create(objMemStream, objXMLSettings);

                objDSInfo.WriteStartDocument(true);

                //Write the beginning of the "Root" element.
                objDSInfo.WriteStartElement("DatasetInfo");

                objDSInfo.WriteElementString("Dataset", strDatasetName);

                objDSInfo.WriteStartElement("ScanTypes");

                foreach (var scanTypeEntry in objSummaryStats.objScanTypeStats)
                {
                    var strScanType = scanTypeEntry.Key;
                    var intIndexMatch = strScanType.IndexOf(SCANTYPE_STATS_SEPCHAR, StringComparison.Ordinal);

                    string strScanFilterText;
                    if (intIndexMatch >= 0) {
                        strScanFilterText = strScanType.Substring(intIndexMatch + SCANTYPE_STATS_SEPCHAR.Length);
                        if (intIndexMatch > 0) {
                            strScanType = strScanType.Substring(0, intIndexMatch);
                        } else {
                            strScanType = string.Empty;
                        }
                    } else {
                        strScanFilterText = string.Empty;
                    }

                    objDSInfo.WriteStartElement("ScanType");
                    objDSInfo.WriteAttributeString("ScanCount", scanTypeEntry.Value.ToString());
                    objDSInfo.WriteAttributeString("ScanFilterText", FixNull(strScanFilterText));
                    objDSInfo.WriteString(strScanType);
                    objDSInfo.WriteEndElement();
                    // ScanType EndElement
                }

                objDSInfo.WriteEndElement();
                // ScanTypes

                objDSInfo.WriteStartElement("AcquisitionInfo");

                var scanCountTotal = objSummaryStats.MSStats.ScanCount + objSummaryStats.MSnStats.ScanCount;
                if (scanCountTotal == 0 & datasetFileInfo.ScanCount > 0) {
                    scanCountTotal = datasetFileInfo.ScanCount;
                }

                objDSInfo.WriteElementString("ScanCount", scanCountTotal.ToString());

                objDSInfo.WriteElementString("ScanCountMS", objSummaryStats.MSStats.ScanCount.ToString());
                objDSInfo.WriteElementString("ScanCountMSn", objSummaryStats.MSnStats.ScanCount.ToString());
                objDSInfo.WriteElementString("Elution_Time_Max", objSummaryStats.ElutionTimeMax.ToString("0.00"));

                objDSInfo.WriteElementString("AcqTimeMinutes", datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes.ToString("0.00"));
                objDSInfo.WriteElementString("StartTime", datasetFileInfo.AcqTimeStart.ToString("yyyy-MM-dd hh:mm:ss tt"));
                objDSInfo.WriteElementString("EndTime", datasetFileInfo.AcqTimeEnd.ToString("yyyy-MM-dd hh:mm:ss tt"));

                objDSInfo.WriteElementString("FileSizeBytes", datasetFileInfo.FileSizeBytes.ToString());

                if (includeCentroidStats) {
                    var centroidedMS1Spectra = mSpectraTypeClassifier.CentroidedMS1Spectra();
                    var centroidedMSnSpectra = mSpectraTypeClassifier.CentroidedMSnSpectra();

                    var centroidedMS1SpectraClassifiedAsProfile = mSpectraTypeClassifier.CentroidedMS1SpectraClassifiedAsProfile();
                    var centroidedMSnSpectraClassifiedAsProfile = mSpectraTypeClassifier.CentroidedMSnSpectraClassifiedAsProfile();

                    var totalMS1Spectra = mSpectraTypeClassifier.TotalMS1Spectra();
                    var totalMSnSpectra = mSpectraTypeClassifier.TotalMSnSpectra();

                    if (totalMS1Spectra + totalMSnSpectra == 0) {
                        // None of the spectra had MSLevel 1 or MSLevel 2
                        // This shouldn't normally be the case; nevertheless, we'll report the totals, regardless of MSLevel, using the MS1 elements
                        centroidedMS1Spectra = mSpectraTypeClassifier.CentroidedSpectra();
                        totalMS1Spectra = mSpectraTypeClassifier.TotalSpectra();
                    }

                    objDSInfo.WriteElementString("ProfileScanCountMS1", (totalMS1Spectra - centroidedMS1Spectra).ToString());
                    objDSInfo.WriteElementString("ProfileScanCountMS2", (totalMSnSpectra - centroidedMSnSpectra).ToString());

                    objDSInfo.WriteElementString("CentroidScanCountMS1", centroidedMS1Spectra.ToString());
                    objDSInfo.WriteElementString("CentroidScanCountMS2", centroidedMSnSpectra.ToString());

                    if (centroidedMS1SpectraClassifiedAsProfile > 0 || centroidedMSnSpectraClassifiedAsProfile > 0) {
                        objDSInfo.WriteElementString("CentroidMS1ScansClassifiedAsProfile", centroidedMS1SpectraClassifiedAsProfile.ToString());
                        objDSInfo.WriteElementString("CentroidMS2ScansClassifiedAsProfile", centroidedMSnSpectraClassifiedAsProfile.ToString());
                    }

                }

                objDSInfo.WriteEndElement();
                // AcquisitionInfo EndElement

                objDSInfo.WriteStartElement("TICInfo");
                objDSInfo.WriteElementString("TIC_Max_MS", StringUtilities.ValueToString(objSummaryStats.MSStats.TICMax, 5));
                objDSInfo.WriteElementString("TIC_Max_MSn", StringUtilities.ValueToString(objSummaryStats.MSnStats.TICMax, 5));
                objDSInfo.WriteElementString("BPI_Max_MS", StringUtilities.ValueToString(objSummaryStats.MSStats.BPIMax, 5));
                objDSInfo.WriteElementString("BPI_Max_MSn", StringUtilities.ValueToString(objSummaryStats.MSnStats.BPIMax, 5));
                objDSInfo.WriteElementString("TIC_Median_MS", StringUtilities.ValueToString(objSummaryStats.MSStats.TICMedian, 5));
                objDSInfo.WriteElementString("TIC_Median_MSn", StringUtilities.ValueToString(objSummaryStats.MSnStats.TICMedian, 5));
                objDSInfo.WriteElementString("BPI_Median_MS", StringUtilities.ValueToString(objSummaryStats.MSStats.BPIMedian, 5));
                objDSInfo.WriteElementString("BPI_Median_MSn", StringUtilities.ValueToString(objSummaryStats.MSnStats.BPIMedian, 5));
                objDSInfo.WriteEndElement();
                // TICInfo EndElement

                // Only write the SampleInfo block if udtSampleInfo contains entries
                if (udtSampleInfo.HasData()) {
                    objDSInfo.WriteStartElement("SampleInfo");
                    objDSInfo.WriteElementString("SampleName", FixNull(udtSampleInfo.SampleName));
                    objDSInfo.WriteElementString("Comment1", FixNull(udtSampleInfo.Comment1));
                    objDSInfo.WriteElementString("Comment2", FixNull(udtSampleInfo.Comment2));
                    objDSInfo.WriteEndElement();
                    // SampleInfo EndElement
                }

                objDSInfo.WriteEndElement();
                //End the "Root" element (DatasetInfo)
                objDSInfo.WriteEndDocument();
                //End the document

                objDSInfo.Close();

                // Now Rewind the memory stream and output as a string
                objMemStream.Position = 0;
                var srStreamReader = new StreamReader(objMemStream);

                // Return the XML as text
                return srStreamReader.ReadToEnd();

            } catch (Exception ex) {
                ReportError("Error in CreateDatasetInfoXML: " + ex.Message);
            }

            // This code will only be reached if an exception occurs
            return string.Empty;

        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strScanStatsFilePath">File path to write the text file to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateScanStatsFile(string strDatasetName, string strScanStatsFilePath)
        {

            return CreateScanStatsFile(strDatasetName, strScanStatsFilePath, mDatasetScanStats, DatasetFileInfo);
        }

        /// <summary>
        /// Creates a tab-delimited text file with details on each scan tracked by this class (stored in mDatasetScanStats)
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strScanStatsFilePath">File path to write the text file to</param>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool CreateScanStatsFile(
            string strDatasetName,
            string strScanStatsFilePath,
            List<clsScanStatsEntry> objScanStats,
            clsDatasetFileInfo datasetFileInfo)
        {
            var intDatasetID = datasetFileInfo.DatasetID;
            var sbLineOut = new StringBuilder();

            try {
                if (objScanStats == null) {
                    ReportError("objScanStats is Nothing; unable to continue in CreateScanStatsFile");
                    return false;
                }

                mErrorMessage = "";

                // Define the path to the extended scan stats file
                var fiScanStatsFile = new FileInfo(strScanStatsFilePath);
                if (fiScanStatsFile.DirectoryName == null)
                {
                    ReportError("Unable to determine the parent directory for " + strScanStatsFilePath);
                    return false;
                }

                var strScanStatsExFilePath = Path.Combine(fiScanStatsFile.DirectoryName, Path.GetFileNameWithoutExtension(fiScanStatsFile.Name) + "Ex.txt");

                // Open the output files
                using (var swOutFile = new StreamWriter(new FileStream(fiScanStatsFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read)))
                using (var swScanStatsExFile = new StreamWriter(new FileStream(strScanStatsExFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))) {

                    // Write the headers
                    sbLineOut.Clear();
                    sbLineOut.Append("Dataset" + '\t' + "ScanNumber" + '\t' + "ScanTime" + '\t' + "ScanType" + '\t' + "TotalIonIntensity" + '\t' + "BasePeakIntensity" + '\t' + "BasePeakMZ" + '\t' + "BasePeakSignalToNoiseRatio" + '\t' + "IonCount" + '\t' + "IonCountRaw" + '\t' + "ScanTypeName");

                    swOutFile.WriteLine(sbLineOut.ToString());

                    sbLineOut.Clear();
                    sbLineOut.Append("Dataset" + '\t' + "ScanNumber" + '\t' + clsScanStatsEntry.SCANSTATS_COL_ION_INJECTION_TIME + '\t' + clsScanStatsEntry.SCANSTATS_COL_SCAN_SEGMENT + '\t' + clsScanStatsEntry.SCANSTATS_COL_SCAN_EVENT + '\t' + clsScanStatsEntry.SCANSTATS_COL_CHARGE_STATE + '\t' + clsScanStatsEntry.SCANSTATS_COL_MONOISOTOPIC_MZ + '\t' + clsScanStatsEntry.SCANSTATS_COL_COLLISION_MODE + '\t' + clsScanStatsEntry.SCANSTATS_COL_SCAN_FILTER_TEXT);

                    swScanStatsExFile.WriteLine(sbLineOut.ToString());

                    foreach (var objScanStatsEntry in objScanStats) {
                        sbLineOut.Clear();
                        sbLineOut.Append(intDatasetID.ToString() + '\t');
                        // Dataset number (aka Dataset ID)
                        sbLineOut.Append(objScanStatsEntry.ScanNumber.ToString() + '\t');
                        // Scan number
                        sbLineOut.Append(objScanStatsEntry.ElutionTime + '\t');
                        // Scan time (minutes)
                        sbLineOut.Append(objScanStatsEntry.ScanType.ToString() + '\t');
                        // Scan type (1 for MS, 2 for MS2, etc.)
                        sbLineOut.Append(objScanStatsEntry.TotalIonIntensity + '\t');
                        // Total ion intensity
                        sbLineOut.Append(objScanStatsEntry.BasePeakIntensity + '\t');
                        // Base peak ion intensity
                        sbLineOut.Append(objScanStatsEntry.BasePeakMZ + '\t');
                        // Base peak ion m/z
                        sbLineOut.Append(objScanStatsEntry.BasePeakSignalToNoiseRatio + '\t');
                        // Base peak signal to noise ratio
                        sbLineOut.Append(objScanStatsEntry.IonCount.ToString() + '\t');
                        // Number of peaks (aka ions) in the spectrum
                        sbLineOut.Append(objScanStatsEntry.IonCountRaw.ToString() + '\t');
                        // Number of peaks (aka ions) in the spectrum prior to any filtering
                        sbLineOut.Append(objScanStatsEntry.ScanTypeName);
                        // Scan type name

                        swOutFile.WriteLine(sbLineOut.ToString());

                        // Write the next entry to swScanStatsExFile
                        // Note that this file format is compatible with that created by MASIC
                        // However, only a limited number of columns are written out, since StoreExtendedScanInfo only stores a certain set of parameters

                        sbLineOut.Clear();
                        sbLineOut.Append(intDatasetID.ToString() + '\t');
                        // Dataset number
                        sbLineOut.Append(objScanStatsEntry.ScanNumber.ToString() + '\t');
                        // Scan number

                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.IonInjectionTime + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.ScanSegment + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.ScanEvent + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.ChargeState + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.MonoisotopicMZ + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.CollisionMode + '\t');
                        sbLineOut.Append(objScanStatsEntry.ExtendedScanInfo.ScanFilterText);

                        swScanStatsExFile.WriteLine(sbLineOut.ToString());

                    }

                }

                return true;

            } catch (Exception ex) {
                ReportError("Error in CreateScanStatsFile: " + ex.Message);
                return false;
            }

        }

        private string FixNull(string strText)
        {
            if (string.IsNullOrEmpty(strText)) {
                return string.Empty;
            }

            return strText;
        }

        public clsDatasetSummaryStats GetDatasetSummaryStats()
        {

            if (!mDatasetSummaryStatsUpToDate) {
                ComputeScanStatsSummary(mDatasetScanStats, out mDatasetSummaryStats);
                mDatasetSummaryStatsUpToDate = true;
            }

            return mDatasetSummaryStats;

        }

        private void ReportError(string message)
        {
            mErrorMessage = string.Copy(message);
            OnErrorEvent(message);
        }

        /// <summary>
        /// Updates the scan type information for the specified scan number
        /// </summary>
        /// <param name="intScanNumber"></param>
        /// <param name="intScanType"></param>
        /// <param name="strScanTypeName"></param>
        /// <returns>True if the scan was found and updated; otherwise false</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetScanType(int intScanNumber, int intScanType, string strScanTypeName)
        {

            var blnMatchFound = false;

            // Look for scan intScanNumber in mDatasetScanStats
            for (var intIndex = 0; intIndex <= mDatasetScanStats.Count - 1; intIndex++) {
                if (mDatasetScanStats[intIndex].ScanNumber == intScanNumber) {
                    mDatasetScanStats[intIndex].ScanType = intScanType;
                    mDatasetScanStats[intIndex].ScanTypeName = strScanTypeName;
                    mDatasetSummaryStatsUpToDate = false;

                    blnMatchFound = true;
                    break;
                }
            }

            return blnMatchFound;

        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data stored in this class (in mDatasetScanStats and Me.DatasetFileInfo)
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strDatasetInfoFilePath">File path to write the XML to</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetStatsTextFile(string strDatasetName, string strDatasetInfoFilePath)
        {

            return UpdateDatasetStatsTextFile(strDatasetName, strDatasetInfoFilePath, mDatasetScanStats, DatasetFileInfo, SampleInfo);
        }

        /// <summary>
        /// Updates a tab-delimited text file, adding a new line summarizing the data in objScanStats and datasetFileInfo
        /// </summary>
        /// <param name="strDatasetName">Dataset Name</param>
        /// <param name="strDatasetStatsFilePath">Tab-delimited file to create/update</param>
        /// <param name="objScanStats">Scan stats to parse</param>
        /// <param name="datasetFileInfo">Dataset Info</param>
        /// <param name="udtSampleInfo">Sample Info</param>
        /// <returns>True if success; False if failure</returns>
        /// <remarks></remarks>
        public bool UpdateDatasetStatsTextFile(
            string strDatasetName,
            string strDatasetStatsFilePath,
            List<clsScanStatsEntry> objScanStats,
            clsDatasetFileInfo datasetFileInfo,
            udtSampleInfoType udtSampleInfo)
        {

            var blnWriteHeaders = false;

            bool blnSuccess;

            try {
                if (objScanStats == null) {
                    ReportError("objScanStats is Nothing; unable to continue in UpdateDatasetStatsTextFile");
                    return false;
                }

                mErrorMessage = "";

                clsDatasetSummaryStats objSummaryStats;
                if (objScanStats == mDatasetScanStats) {
                    objSummaryStats = GetDatasetSummaryStats();
                } else {
                    // Parse the data in objScanStats to compute the bulk values
                    var success = ComputeScanStatsSummary(objScanStats, out objSummaryStats);
                    if (!success)
                    {
                        ReportError("ComputeScanStatsSummary returned false; unable to continue in UpdateDatasetStatsTextFile");
                        return false;
                    }
                }

                if (!File.Exists(strDatasetStatsFilePath)) {
                    blnWriteHeaders = true;
                }

                // Create or open the output file
                using (var swOutFile = new StreamWriter(new FileStream(strDatasetStatsFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))) {
                    string strLineOut;
                    if (blnWriteHeaders) {
                        // Write the header line
                        strLineOut = "Dataset" + '\t' + "ScanCount" + '\t' + "ScanCountMS" + '\t' + "ScanCountMSn" + '\t' + "Elution_Time_Max" + '\t' + "AcqTimeMinutes" + '\t' + "StartTime" + '\t' + "EndTime" + '\t' + "FileSizeBytes" + '\t' + "SampleName" + '\t' + "Comment1" + '\t' + "Comment2";

                        swOutFile.WriteLine(strLineOut);
                    }

                    strLineOut =
                        strDatasetName + '\t' +
                        (objSummaryStats.MSStats.ScanCount + objSummaryStats.MSnStats.ScanCount) + '\t'
                        + objSummaryStats.MSStats.ScanCount + '\t'
                        + objSummaryStats.MSnStats.ScanCount + '\t'
                        + objSummaryStats.ElutionTimeMax.ToString("0.00") + '\t'
                        + datasetFileInfo.AcqTimeEnd.Subtract(datasetFileInfo.AcqTimeStart).TotalMinutes.ToString("0.00") + '\t' +
                        datasetFileInfo.AcqTimeStart.ToString("yyyy-MM-dd hh:mm:ss tt") + '\t' +
                        datasetFileInfo.AcqTimeEnd.ToString("yyyy-MM-dd hh:mm:ss tt") + '\t' +
                        datasetFileInfo.FileSizeBytes + '\t' +
                        FixNull(udtSampleInfo.SampleName) + '\t' +
                        FixNull(udtSampleInfo.Comment1) + '\t' +
                        FixNull(udtSampleInfo.Comment2);

                    swOutFile.WriteLine(strLineOut);

                }

                blnSuccess = true;

            } catch (Exception ex) {
                ReportError("Error in UpdateDatasetStatsTextFile: " + ex.Message);
                blnSuccess = false;
            }

            return blnSuccess;

        }

        private void mSpectraTypeClassifier_ErrorEvent(string message)
        {
            ReportError("Error in SpectraTypeClassifier: " + message);
        }

    }

    public class clsScanStatsEntry
    {
        public const string SCANSTATS_COL_ION_INJECTION_TIME = "Ion Injection Time (ms)";
        public const string SCANSTATS_COL_SCAN_SEGMENT = "Scan Segment";
        public const string SCANSTATS_COL_SCAN_EVENT = "Scan Event";
        public const string SCANSTATS_COL_CHARGE_STATE = "Charge State";
        public const string SCANSTATS_COL_MONOISOTOPIC_MZ = "Monoisotopic M/Z";
        public const string SCANSTATS_COL_COLLISION_MODE = "Collision Mode";

        public const string SCANSTATS_COL_SCAN_FILTER_TEXT = "Scan Filter Text";

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

        // 1 for MS, 2 for MS2, 3 for MS3
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
        public readonly Dictionary<string, int> objScanTypeStats;

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

            objScanTypeStats.Clear();

        }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetSummaryStats()
        {
            objScanTypeStats = new Dictionary<string, int>();
            Clear();
        }

    }

}

