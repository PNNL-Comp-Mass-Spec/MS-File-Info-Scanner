using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PNNLOmics.Utilities;
using ThermoRawFileReader;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2013
//

namespace MSFileInfoScanner
{
    public class clsDeconToolsIsosInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsDeconToolsIsosInfoScanner()
        {
            MaxFit = DEFAUT_MAX_FIT;
        }

        // Note: The extension must be in all caps
        public const string DECONTOOLS_CSV_FILE_EXTENSION = ".CSV";
        public const string DECONTOOLS_ISOS_FILE_SUFFIX = "_ISOS.CSV";

        public const string DECONTOOLS_SCANS_FILE_SUFFIX = "_SCANS.CSV";

        public const float DEFAUT_MAX_FIT = 0.15f;

        private struct udtIsosDataType
        {
            public int Scan;
            public byte Charge;
            public double Abundance;
            public double MZ;
            public float Fit;
            public double MonoMass;
        }

        private struct udtScansDataType
        {
            public int Scan;
            public float ElutionTime;
            public int MSLevel;
            // BPI
            public double BasePeakIntensity;
            public double BasePeakMZ;
            // TIC
            public double TotalIonCurrent;
            public int NumPeaks;
            public int NumDeisotoped;
            public string FilterText;
        }

        public float MaxFit { get; set; }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="strDataFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override string GetDatasetNameViaPath(string strDataFilePath)
        {

            try
            {
                if (strDataFilePath.ToUpper().EndsWith(DECONTOOLS_ISOS_FILE_SUFFIX)) {
                    // The dataset name is simply the file name without _isos.csv
                    var datasetName = Path.GetFileName(strDataFilePath);
                    return datasetName.Substring(0, datasetName.Length - DECONTOOLS_ISOS_FILE_SUFFIX.Length);
                }

                return string.Empty;

            } catch (Exception) {
                return string.Empty;
            }
        }

        private int GetScanOrFrameColIndex(List<string> lstData, string strFileDescription)
        {
            var intColIndexScanOrFrameNum = lstData.IndexOf("frame_num");
            if (intColIndexScanOrFrameNum < 0) {
                intColIndexScanOrFrameNum = lstData.IndexOf("scan_num");
            }

            if (intColIndexScanOrFrameNum < 0) {
                throw new InvalidDataException("Required column not found in the " + strFileDescription + " file; must have scan_num or frame_num");
            }

            return intColIndexScanOrFrameNum;

        }


        private void LoadData(FileInfo fiIsosFile, clsDatasetFileInfo datasetFileInfo)
        {
            // Cache the data in the _isos.csv and _scans.csv files

            if (mSaveTICAndBPI) {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (mSaveLCMS2DPlots) {
                InitializeLCMS2DPlot();
            }

            var lstIsosData = LoadIsosFile(fiIsosFile.FullName, MaxFit);

            if (lstIsosData.Count == 0) {
                OnErrorEvent("No data found in the _isos.csv file: " + fiIsosFile.FullName);
                return;
            }

            var strScansFilePath = GetDatasetNameViaPath(fiIsosFile.Name) + DECONTOOLS_SCANS_FILE_SUFFIX;
            if (fiIsosFile.Directory != null)
            {
                strScansFilePath = Path.Combine(fiIsosFile.Directory.FullName, strScansFilePath);
            }

            var lstScanData = LoadScansFile(strScansFilePath);
            var scansFileIsMissing = false;

            if (lstScanData.Count > 0) {
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(lstScanData.Last().ElutionTime);
                datasetFileInfo.ScanCount = lstScanData.Last().Scan;
            } else {
                scansFileIsMissing = true;

                datasetFileInfo.ScanCount = (from item in lstIsosData select item.Scan).Max();

                for (var intScanIndex = 1; intScanIndex <= datasetFileInfo.ScanCount; intScanIndex++) {
                    var udtScanData = new udtScansDataType
                    {
                        Scan = intScanIndex,
                        ElutionTime = intScanIndex,
                        MSLevel = 1
                    };

                    lstScanData.Add(udtScanData);
                }
            }

            // Step through the isos data and call mLCMS2DPlot.AddScan() for each scan

            var lstIons = new List<clsLCMSDataPlotter.udtMSIonType>();
            var intCurrentScan = 0;

            // Note: we only need to update mLCMS2DPlot
            // The options for mLCMS2DPlotOverview will be cloned from mLCMS2DPlot.Options
            mLCMS2DPlot.Options.PlottingDeisotopedData = true;

            var dblMaxMonoMass = mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot;


            for (var intIndex = 0; intIndex <= lstIsosData.Count - 1; intIndex++) {
                if (lstIsosData[intIndex].Scan > intCurrentScan || intIndex == lstIsosData.Count - 1) {
                    // Store the cached values

                    if (lstIons.Count > 0) {
                        var udtCurrentScan = (from item in lstScanData where item.Scan == intCurrentScan select item).ToList().FirstOrDefault();

                        lstIons.Sort(new clsLCMSDataPlotter.udtMSIonTypeComparer());
                        mLCMS2DPlot.AddScan(intCurrentScan, udtCurrentScan.MSLevel, udtCurrentScan.ElutionTime, lstIons);

                        if (scansFileIsMissing && mSaveTICAndBPI) {
                            // Determine the TIC and BPI values using the data from the .isos file
                            double tic = 0;
                            double bpi = 0;

                            for (var dataIndex = 0; dataIndex <= lstIons.Count - 1; dataIndex++) {
                                tic += lstIons[dataIndex].Intensity;
                                if (lstIons[dataIndex].Intensity > bpi) {
                                    bpi = lstIons[dataIndex].Intensity;
                                }
                            }

                            mTICandBPIPlot.AddData(intCurrentScan, udtCurrentScan.MSLevel, udtCurrentScan.ElutionTime, bpi, tic);
                        }

                    }

                    intCurrentScan = lstIsosData[intIndex].Scan;
                    lstIons.Clear();
                }

                if (lstIsosData[intIndex].MonoMass <= dblMaxMonoMass) {
                    var udtIon = new clsLCMSDataPlotter.udtMSIonType
                    {
                        // Note that we store .MonoMass in a field called .mz; we'll still be plotting monoisotopic mass
                        MZ = lstIsosData[intIndex].MonoMass,
                        Intensity = lstIsosData[intIndex].Abundance,
                        Charge = lstIsosData[intIndex].Charge
                    };
                    
                    lstIons.Add(udtIon);
                }
            }

        }

        private List<udtIsosDataType> LoadIsosFile(string strIsosFilePath, float sngMaxFit)
        {

            var dctColumnInfo = new Dictionary<string, int>
            {
                {"charge", -1},
                {"abundance", -1},
                {"mz", -1},
                {"fit", -1},
                {"monoisotopic_mw", -1}
            };

            var intColIndexScanOrFrameNum = -1;

            var lstIsosData = new List<udtIsosDataType>();
            var intRowNumber = 0;

            var intLastScan = 0;
            var intLastScanParseErrors = 0;

            Console.WriteLine("  Reading the _isos.csv file");

            using (var srIsosFile = new StreamReader(new FileStream(strIsosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                while (!srIsosFile.EndOfStream) {
                    intRowNumber += 1;
                    var strLineIn = srIsosFile.ReadLine();
                    if (string.IsNullOrWhiteSpace(strLineIn))
                        continue;

                    var lstData = strLineIn.Split(',').ToList();

                    if (intRowNumber == 1) {
                        // Parse the header row
                        ParseColumnHeaders(dctColumnInfo, lstData, "_isos.csv");

                        intColIndexScanOrFrameNum = GetScanOrFrameColIndex(lstData, "_isos.csv");

                        continue;
                    }

                    var blnParseError = false;
                    var intCurrentScan = 0;

                    try {
                        var udtIsosData = new udtIsosDataType();
                        int.TryParse(lstData[intColIndexScanOrFrameNum], out udtIsosData.Scan);
                        intCurrentScan = udtIsosData.Scan;

                        byte.TryParse(lstData[dctColumnInfo["charge"]], out udtIsosData.Charge);
                        double.TryParse(lstData[dctColumnInfo["abundance"]], out udtIsosData.Abundance);
                        double.TryParse(lstData[dctColumnInfo["mz"]], out udtIsosData.MZ);
                        float.TryParse(lstData[dctColumnInfo["fit"]], out udtIsosData.Fit);
                        double.TryParse(lstData[dctColumnInfo["monoisotopic_mw"]], out udtIsosData.MonoMass);

                        if (udtIsosData.Charge > 1) {
                            //Console.WriteLine("Found it")
                        }

                        if (udtIsosData.Fit <= sngMaxFit) {
                            lstIsosData.Add(udtIsosData);
                        }

                    } catch (Exception) {
                        blnParseError = true;
                    }


                    if (intCurrentScan > intLastScan) {
                        if (intLastScanParseErrors > 0) {
                            OnWarningEvent("Warning: Skipped " + intLastScanParseErrors + " data points in scan " + intLastScan + " due to data conversion errors");
                        }

                        intLastScan = intCurrentScan;
                        intLastScanParseErrors = 0;
                    }

                    if (blnParseError) {
                        intLastScanParseErrors += 1;
                    }

                }
            }

            return lstIsosData;

        }

        private List<udtScansDataType> LoadScansFile(string strScansFilePath)
        {

            const string FILTERED_SCANS_SUFFIX = "_filtered_scans.csv";

            var dctColumnInfo = new Dictionary<string, int>
            {
                {"type", -1},
                {"bpi", -1},
                {"bpi_mz", -1},
                {"tic", -1},
                {"num_peaks", -1},
                {"num_deisotoped", -1}
            };

            var intColIndexScanOrFrameNum = -1;
            var intColIndexScanOrFrameTime = -1;

            var lstScanData = new List<udtScansDataType>();
            var intRowNumber = 0;
            var intColIndexScanInfo = -1;

            if (!File.Exists(strScansFilePath) && strScansFilePath.ToLower().EndsWith(FILTERED_SCANS_SUFFIX)) {
                strScansFilePath = strScansFilePath.Substring(0, strScansFilePath.Length - FILTERED_SCANS_SUFFIX.Length) + "_scans.csv";
            }

            if (!File.Exists(strScansFilePath)) {
                OnWarningEvent("Warning: _scans.csv file is missing; will plot vs. scan number instead of vs. elution time");
                return lstScanData;
            }

            Console.WriteLine("  Reading the _scans.csv file");

            using (var srIsosFile = new StreamReader(new FileStream(strScansFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
                while (!srIsosFile.EndOfStream) {
                    intRowNumber += 1;
                    var strLineIn = srIsosFile.ReadLine();
                    var lstData = strLineIn.Split(',').ToList();

                    if (intRowNumber == 1) {
                        // Parse the header row
                        ParseColumnHeaders(dctColumnInfo, lstData, "_scans.csv");

                        intColIndexScanOrFrameNum = GetScanOrFrameColIndex(lstData, "_scans.csv");

                        intColIndexScanOrFrameTime = lstData.IndexOf("frame_time");
                        if (intColIndexScanOrFrameTime < 0) {
                            intColIndexScanOrFrameTime = lstData.IndexOf("scan_time");
                        }

                        // The info column will have data of the form "FTMS + p NSI Full ms [400.00-2000.00]" for Thermo datasets
                        // For .mzXML files, this fill will simply have an integer (and thus isn't useful)
                        // It may not be present in older _scancs.csv files and is thus optional
                        intColIndexScanInfo = lstData.IndexOf("info");

                        continue;
                    }

                    try {
                        var udtScanData = new udtScansDataType();

                        int.TryParse(lstData[intColIndexScanOrFrameNum], out udtScanData.Scan);
                        float.TryParse(lstData[intColIndexScanOrFrameTime], out udtScanData.ElutionTime);
                        int.TryParse(lstData[dctColumnInfo["type"]], out udtScanData.MSLevel);
                        double.TryParse(lstData[dctColumnInfo["bpi"]], out udtScanData.BasePeakIntensity);
                        double.TryParse(lstData[dctColumnInfo["bpi_mz"]], out udtScanData.BasePeakMZ);
                        double.TryParse(lstData[dctColumnInfo["tic"]], out udtScanData.TotalIonCurrent);
                        int.TryParse(lstData[dctColumnInfo["num_peaks"]], out udtScanData.NumPeaks);
                        int.TryParse(lstData[dctColumnInfo["num_deisotoped"]], out udtScanData.NumDeisotoped);

                        if (intColIndexScanInfo > 0) {
                            var infoText = lstData[intColIndexScanInfo];
                            int infoValue;

                            // Only store infoText in .FilterText if infoText is not simply an integer
                            if (!int.TryParse(infoText, out infoValue)) {
                                udtScanData.FilterText = infoText;
                            }

                        }

                        lstScanData.Add(udtScanData);

                        if (mSaveTICAndBPI) {
                            mTICandBPIPlot.AddData(udtScanData.Scan, udtScanData.MSLevel, udtScanData.ElutionTime, udtScanData.BasePeakIntensity, udtScanData.TotalIonCurrent);
                        }

                        string scanTypeName;
                        if (string.IsNullOrWhiteSpace(udtScanData.FilterText))
                        {
                            udtScanData.FilterText = udtScanData.MSLevel > 1 ? "HMSn" : "HMS";
                            scanTypeName = udtScanData.FilterText;
                        }
                        else
                        {
                            scanTypeName = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(udtScanData.FilterText);
                        }

                        var objScanStatsEntry = new clsScanStatsEntry
                        {
                            ScanNumber = udtScanData.Scan,
                            ScanType = udtScanData.MSLevel,
                            ScanTypeName = scanTypeName,
                            ScanFilterText = XRawFileIO.MakeGenericFinniganScanFilter(udtScanData.FilterText),
                            ElutionTime = udtScanData.ElutionTime.ToString("0.0000"),
                            TotalIonIntensity = StringUtilities.ValueToString(udtScanData.TotalIonCurrent, 5),
                            BasePeakIntensity = StringUtilities.ValueToString(udtScanData.BasePeakIntensity, 5),
                            BasePeakMZ = StringUtilities.DblToString(udtScanData.BasePeakMZ, 4),
                            BasePeakSignalToNoiseRatio = "0",
                            IonCount = udtScanData.NumDeisotoped,
                            IonCountRaw = udtScanData.NumPeaks,
                            ExtendedScanInfo =
                            {
                                CollisionMode = string.Empty,
                                ScanFilterText = udtScanData.FilterText
                            }
                        };

                        mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry);

                    } catch (Exception ex) {
                        OnWarningEvent("Warning: Ignoring scan " + lstData[dctColumnInfo["scan_num"]] + " since data conversion error: " + ex.Message);
                    }

                }
            }

            return lstScanData;

        }


        private void ParseColumnHeaders(Dictionary<string, int> dctColumnInfo, List<string> lstData, string strFileDescription)
        {
            foreach (var columnName in dctColumnInfo.Keys.ToList()) {
                var intcolIndex = lstData.IndexOf(columnName);
                if (intcolIndex >= 0) {
                    dctColumnInfo[columnName] = intcolIndex;
                } else {
                    throw new InvalidDataException("Required column not found in the " + strFileDescription + " file: " + columnName);
                }
            }
        }

        /// <summary>
        /// Process the DeconTools results
        /// </summary>
        /// <param name="strDataFilePath">Isos file path</param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks>Will also read the _scans.csv file if present (to determine ElutionTime and MSLevel</remarks>
        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {

            var fiIsosFile = new FileInfo(strDataFilePath);

            if (!fiIsosFile.Exists) {
                OnErrorEvent("_isos.csv file not found: " + strDataFilePath);
                return false;
            }

            var intDatasetID = DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = fiIsosFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = fiIsosFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = intDatasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(fiIsosFile.Name);
            datasetFileInfo.FileExtension = fiIsosFile.Extension;
            datasetFileInfo.FileSizeBytes = fiIsosFile.Length;

            datasetFileInfo.ScanCount = 0;


            mDatasetStatsSummarizer.ClearCachedData();

            if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile || mSaveLCMS2DPlots) {
                // Load data from each scan
                // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                LoadData(fiIsosFile, datasetFileInfo);
            }

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            UpdateDatasetFileStats(fiIsosFile, intDatasetID);

            // Copy over the updated filetime info from datasetFileInfo to mDatasetFileInfo
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemCreationTime = datasetFileInfo.FileSystemCreationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSystemModificationTime = datasetFileInfo.FileSystemModificationTime;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetID = datasetFileInfo.DatasetID;
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;

            return true;

        }

    }
}

