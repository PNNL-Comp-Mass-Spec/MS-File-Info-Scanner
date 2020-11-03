using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using PRISM;
using ThermoRawFileReader;

namespace MSFileInfoScanner
{
    /// <summary>
    /// DeconTools .isos info scanner
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2013</remarks>
    public class DeconToolsIsosInfoScanner : clsMSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: isos, mw, deisotoped

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsDeconToolsIsosInfoScanner()
        {
            MaxFit = DEFAULT_MAX_FIT;
        }

        // Note: The extension must be in all caps
        public const string DECONTOOLS_CSV_FILE_EXTENSION = ".CSV";
        public const string DECONTOOLS_ISOS_FILE_SUFFIX = "_ISOS.CSV";

        public const string DECONTOOLS_SCANS_FILE_SUFFIX = "_SCANS.CSV";

        public const float DEFAULT_MAX_FIT = 0.15f;

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
        /// <param name="dataFilePath"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            try
            {
                if (dataFilePath.ToUpper().EndsWith(DECONTOOLS_ISOS_FILE_SUFFIX))
                {
                    // The dataset name is simply the file name without _isos.csv
                    var datasetName = Path.GetFileName(dataFilePath);
                    return datasetName.Substring(0, datasetName.Length - DECONTOOLS_ISOS_FILE_SUFFIX.Length);
                }

                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private int GetScanOrFrameColIndex(IList<string> data, string fileDescription)
        {
            var colIndexScanOrFrameNum = data.IndexOf("frame_num");
            if (colIndexScanOrFrameNum < 0)
            {
                colIndexScanOrFrameNum = data.IndexOf("scan_num");
            }

            if (colIndexScanOrFrameNum < 0)
            {
                throw new InvalidDataException("Required column not found in the " + fileDescription + " file; must have scan_num or frame_num");
            }

            return colIndexScanOrFrameNum;
        }

        private void LoadData(FileInfo isosFile, DatasetFileInfo datasetFileInfo)
        {
            // Cache the data in the _isos.csv and _scans.csv files

            if (mSaveTICAndBPI)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (mSaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            var isosData = LoadIsosFile(isosFile.FullName, MaxFit);

            if (isosData.Count == 0)
            {
                OnErrorEvent("No data found in the _isos.csv file: " + isosFile.FullName);
                return;
            }

            var scansFilePath = GetDatasetNameViaPath(isosFile.Name) + DECONTOOLS_SCANS_FILE_SUFFIX;
            if (isosFile.Directory != null)
            {
                scansFilePath = Path.Combine(isosFile.Directory.FullName, scansFilePath);
            }

            var scanData = LoadScansFile(scansFilePath);
            var scansFileIsMissing = false;

            if (scanData.Count > 0)
            {
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(scanData.Last().ElutionTime);
                datasetFileInfo.ScanCount = scanData.Last().Scan;
            }
            else
            {
                scansFileIsMissing = true;

                datasetFileInfo.ScanCount = (from item in isosData select item.Scan).Max();

                for (var scanIndex = 1; scanIndex <= datasetFileInfo.ScanCount; scanIndex++)
                {
                    var udtScanData = new udtScansDataType
                    {
                        Scan = scanIndex,
                        ElutionTime = scanIndex,
                        MSLevel = 1
                    };

                    scanData.Add(udtScanData);
                }
            }

            // Step through the isos data and call mLCMS2DPlot.AddScan() for each scan

            var ionList = new List<clsLCMSDataPlotter.udtMSIonType>();
            var currentScan = 0;

            // Note: we only need to update mLCMS2DPlot
            // The options for mLCMS2DPlotOverview will be cloned from mLCMS2DPlot.Options
            mLCMS2DPlot.Options.PlottingDeisotopedData = true;

            var maxMonoMass = mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot;

            for (var index = 0; index <= isosData.Count - 1; index++)
            {
                if (isosData[index].Scan > currentScan || index == isosData.Count - 1)
                {
                    // Store the cached values

                    if (ionList.Count > 0)
                    {
                        var udtCurrentScan = (from item in scanData where item.Scan == currentScan select item).ToList().FirstOrDefault();

                        ionList.Sort(new clsLCMSDataPlotter.udtMSIonTypeComparer());
                        mLCMS2DPlot.AddScan(currentScan, udtCurrentScan.MSLevel, udtCurrentScan.ElutionTime, ionList);

                        if (scansFileIsMissing && mSaveTICAndBPI)
                        {
                            // Determine the TIC and BPI values using the data from the .isos file
                            double tic = 0;
                            double bpi = 0;

                            for (var dataIndex = 0; dataIndex <= ionList.Count - 1; dataIndex++)
                            {
                                tic += ionList[dataIndex].Intensity;
                                if (ionList[dataIndex].Intensity > bpi)
                                {
                                    bpi = ionList[dataIndex].Intensity;
                                }
                            }

                            mTICAndBPIPlot.AddData(currentScan, udtCurrentScan.MSLevel, udtCurrentScan.ElutionTime, bpi, tic);
                        }
                    }

                    currentScan = isosData[index].Scan;
                    ionList.Clear();
                }

                if (isosData[index].MonoMass <= maxMonoMass)
                {
                    var udtIon = new clsLCMSDataPlotter.udtMSIonType
                    {
                        // Note that we store .MonoMass in a field called .mz; we'll still be plotting monoisotopic mass
                        MZ = isosData[index].MonoMass,
                        Intensity = isosData[index].Abundance,
                        Charge = isosData[index].Charge
                    };

                    ionList.Add(udtIon);
                }
            }
        }

        private List<udtIsosDataType> LoadIsosFile(string isosFilePath, float maxFit)
        {
            var dctColumnInfo = new Dictionary<string, int>
            {
                {"charge", -1},
                {"abundance", -1},
                {"mz", -1},
                {"fit", -1},
                {"monoisotopic_mw", -1}
            };

            var colIndexScanOrFrameNum = -1;

            var isosData = new List<udtIsosDataType>();
            var rowNumber = 0;

            var lastScan = 0;
            var lastScanParseErrors = 0;

            Console.WriteLine("  Reading the _isos.csv file");

            using (var reader = new StreamReader(new FileStream(isosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                while (!reader.EndOfStream)
                {
                    rowNumber++;
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    var dataColumns = dataLine.Split(',').ToList();

                    if (rowNumber == 1)
                    {
                        // Parse the header row
                        ParseColumnHeaders(dctColumnInfo, dataColumns, "_isos.csv");

                        colIndexScanOrFrameNum = GetScanOrFrameColIndex(dataColumns, "_isos.csv");

                        continue;
                    }

                    var parseError = false;
                    var currentScan = 0;

                    try
                    {
                        var udtIsosData = new udtIsosDataType();
                        int.TryParse(dataColumns[colIndexScanOrFrameNum], out udtIsosData.Scan);
                        currentScan = udtIsosData.Scan;

                        byte.TryParse(dataColumns[dctColumnInfo["charge"]], out udtIsosData.Charge);
                        double.TryParse(dataColumns[dctColumnInfo["abundance"]], out udtIsosData.Abundance);
                        double.TryParse(dataColumns[dctColumnInfo["mz"]], out udtIsosData.MZ);
                        float.TryParse(dataColumns[dctColumnInfo["fit"]], out udtIsosData.Fit);
                        double.TryParse(dataColumns[dctColumnInfo["monoisotopic_mw"]], out udtIsosData.MonoMass);

                        if (udtIsosData.Charge > 1)
                        {
                            //Console.WriteLine("Found it")
                        }

                        if (udtIsosData.Fit <= maxFit)
                        {
                            isosData.Add(udtIsosData);
                        }
                    }
                    catch (Exception)
                    {
                        parseError = true;
                    }

                    if (currentScan > lastScan)
                    {
                        if (lastScanParseErrors > 0)
                        {
                            OnWarningEvent("Warning: Skipped " + lastScanParseErrors + " data points in scan " + lastScan + " due to data conversion errors");
                        }

                        lastScan = currentScan;
                        lastScanParseErrors = 0;
                    }

                    if (parseError)
                    {
                        lastScanParseErrors++;
                    }
                }
            }

            return isosData;
        }

        private List<udtScansDataType> LoadScansFile(string scansFilePath)
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

            var colIndexScanOrFrameNum = -1;
            var colIndexScanOrFrameTime = -1;

            var scanData = new List<udtScansDataType>();
            var rowNumber = 0;
            var colIndexScanInfo = -1;

            if (!File.Exists(scansFilePath) && scansFilePath.EndsWith(FILTERED_SCANS_SUFFIX, StringComparison.OrdinalIgnoreCase))
            {
                scansFilePath = scansFilePath.Substring(0, scansFilePath.Length - FILTERED_SCANS_SUFFIX.Length) + "_scans.csv";
            }

            if (!File.Exists(scansFilePath))
            {
                OnWarningEvent("Warning: _scans.csv file is missing; will plot vs. scan number instead of vs. elution time");
                return scanData;
            }

            Console.WriteLine("  Reading the _scans.csv file");

            using (var reader = new StreamReader(new FileStream(scansFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                while (!reader.EndOfStream)
                {
                    rowNumber++;
                    var dataLine = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    var dataColumns = dataLine.Split(',').ToList();

                    if (rowNumber == 1)
                    {
                        // Parse the header row
                        ParseColumnHeaders(dctColumnInfo, dataColumns, "_scans.csv");

                        colIndexScanOrFrameNum = GetScanOrFrameColIndex(dataColumns, "_scans.csv");

                        colIndexScanOrFrameTime = dataColumns.IndexOf("frame_time");
                        if (colIndexScanOrFrameTime < 0)
                        {
                            colIndexScanOrFrameTime = dataColumns.IndexOf("scan_time");
                        }

                        // The info column will have data of the form "FTMS + p NSI Full ms [400.00-2000.00]" for Thermo datasets
                        // For .mzXML files, this fill will simply have an integer (and thus isn't useful)
                        // It may not be present in older _scans.csv files and is thus optional
                        colIndexScanInfo = dataColumns.IndexOf("info");

                        continue;
                    }

                    try
                    {
                        var udtScanData = new udtScansDataType();

                        int.TryParse(dataColumns[colIndexScanOrFrameNum], out udtScanData.Scan);
                        float.TryParse(dataColumns[colIndexScanOrFrameTime], out udtScanData.ElutionTime);
                        int.TryParse(dataColumns[dctColumnInfo["type"]], out udtScanData.MSLevel);
                        double.TryParse(dataColumns[dctColumnInfo["bpi"]], out udtScanData.BasePeakIntensity);
                        double.TryParse(dataColumns[dctColumnInfo["bpi_mz"]], out udtScanData.BasePeakMZ);
                        double.TryParse(dataColumns[dctColumnInfo["tic"]], out udtScanData.TotalIonCurrent);
                        int.TryParse(dataColumns[dctColumnInfo["num_peaks"]], out udtScanData.NumPeaks);
                        int.TryParse(dataColumns[dctColumnInfo["num_deisotoped"]], out udtScanData.NumDeisotoped);

                        if (colIndexScanInfo > 0)
                        {
                            var infoText = dataColumns[colIndexScanInfo];

                            // Only store infoText in .FilterText if infoText is not simply an integer
                            if (!int.TryParse(infoText, out _))
                            {
                                udtScanData.FilterText = infoText;
                            }
                        }

                        scanData.Add(udtScanData);

                        if (mSaveTICAndBPI)
                        {
                            mTICAndBPIPlot.AddData(udtScanData.Scan, udtScanData.MSLevel, udtScanData.ElutionTime, udtScanData.BasePeakIntensity, udtScanData.TotalIonCurrent);
                        }

                        string scanTypeName;
                        if (string.IsNullOrWhiteSpace(udtScanData.FilterText))
                        {
                            udtScanData.FilterText = udtScanData.MSLevel > 1 ? "HMSn" : "HMS";
                            scanTypeName = udtScanData.FilterText;
                        }
                        else
                        {
                            scanTypeName = XRawFileIO.GetScanTypeNameFromThermoScanFilterText(udtScanData.FilterText);
                        }

                        var scanStatsEntry = new ScanStatsEntry
                        {
                            ScanNumber = udtScanData.Scan,
                            ScanType = udtScanData.MSLevel,
                            ScanTypeName = scanTypeName,
                            ScanFilterText = XRawFileIO.MakeGenericThermoScanFilter(udtScanData.FilterText),
                            ElutionTime = udtScanData.ElutionTime.ToString("0.0###"),
                            TotalIonIntensity = StringUtilities.ValueToString(udtScanData.TotalIonCurrent, 5),
                            BasePeakIntensity = StringUtilities.ValueToString(udtScanData.BasePeakIntensity, 5),
                            BasePeakMZ = udtScanData.BasePeakMZ.ToString("0.0###"),
                            BasePeakSignalToNoiseRatio = "0",
                            IonCount = udtScanData.NumDeisotoped,
                            IonCountRaw = udtScanData.NumPeaks,
                            ExtendedScanInfo =
                            {
                                CollisionMode = string.Empty,
                                ScanFilterText = udtScanData.FilterText
                            }
                        };

                        mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);
                    }
                    catch (Exception ex)
                    {
                        OnWarningEvent("Warning: Ignoring scan " + dataColumns[dctColumnInfo["scan_num"]] + " since data conversion error: " + ex.Message);
                    }
                }
            }

            return scanData;
        }

        private void ParseColumnHeaders(Dictionary<string, int> dctColumnInfo, IList<string> dataColumns, string fileDescription)
        {
            foreach (var columnName in dctColumnInfo.Keys.ToList())
            {
                var colIndex = dataColumns.IndexOf(columnName);
                if (colIndex >= 0)
                {
                    dctColumnInfo[columnName] = colIndex;
                }
                else
                {
                    throw new InvalidDataException("Required column not found in the " + fileDescription + " file: " + columnName);
                }
            }
        }

        /// <summary>
        /// Process the DeconTools results
        /// </summary>
        /// <param name="dataFilePath">Isos file path</param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks>Will also read the _scans.csv file if present (to determine ElutionTime and MSLevel</remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            var isosFile = new FileInfo(dataFilePath);

            if (!isosFile.Exists)
            {
                OnErrorEvent("_isos.csv file not found: " + dataFilePath);
                return false;
            }

            var datasetID = DatasetID;

            // Record the file size and Dataset ID
            datasetFileInfo.FileSystemCreationTime = isosFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = isosFile.LastWriteTime;

            // The acquisition times will get updated below to more accurate values
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = datasetID;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(isosFile.Name);
            datasetFileInfo.FileExtension = isosFile.Extension;
            datasetFileInfo.FileSizeBytes = isosFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();

            if (mSaveTICAndBPI || mCreateDatasetInfoFile || mCreateScanStatsFile || mSaveLCMS2DPlots)
            {
                // Load data from each scan
                // This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                LoadData(isosFile, datasetFileInfo);
            }

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the isos file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(isosFile, datasetID);

            // Copy over the updated file time info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetStatsSummarizerUsingDatasetFileInfo(datasetFileInfo);

            PostProcessTasks();

            return true;
        }
    }
}

