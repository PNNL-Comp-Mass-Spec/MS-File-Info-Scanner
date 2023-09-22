using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Plotting;
using MSFileInfoScannerInterfaces;
using PRISM;
using ThermoRawFileReader;

namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// DeconTools .isos info scanner
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2013
    /// </remarks>
    public class DeconToolsIsosInfoScanner : MSFileInfoProcessorBaseClass
    {
        // Ignore Spelling: Da, decon, decontools, deisotoped, isos, lcms, mw

        /// <summary>
        /// Constructor
        /// </summary>
        public DeconToolsIsosInfoScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) :
            base(options, lcms2DPlotOptions)
        {
            MaxFit = DEFAULT_MAX_FIT;
        }

        // Note: The extension must be in all caps
        public const string DECONTOOLS_CSV_FILE_EXTENSION = ".CSV";
        public const string DECONTOOLS_ISOS_FILE_SUFFIX = "_ISOS.CSV";

        public const string DECONTOOLS_SCANS_FILE_SUFFIX = "_SCANS.CSV";

        public const float DEFAULT_MAX_FIT = 0.15f;

        private struct IsosDataType
        {
            public int Scan;
            public byte Charge;
            public double Abundance;
            public double MZ;
            public float Fit;
            public double MonoMass;

            public override string ToString()
            {
                return string.Format("{0:N3} m/z, {1}+, {2:N3} Da", MZ, Charge, MonoMass);
            }
        }

        private struct ScansDataType
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

            public override string ToString()
            {
                return string.Format("Scan {0} at {1:N2} minutes: {2} peaks, {3} deisotoped", Scan, ElutionTime, NumPeaks, NumDeisotoped);
            }
        }

        public float MaxFit { get; set; }

        /// <summary>
        /// Returns the dataset name for the given file
        /// </summary>
        /// <param name="dataFilePath"></param>
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

            if (Options.SaveTICAndBPIPlots)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (Options.SaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }

            var isosData = LoadIsosFile(isosFile.FullName, MaxFit);

            if (isosData.Count == 0)
            {
                OnErrorEvent("No data found in the _isos.csv file: {0}", isosFile.FullName);
                return;
            }

            var scansFilePath = GetDatasetNameViaPath(isosFile.Name) + DECONTOOLS_SCANS_FILE_SUFFIX;

            if (isosFile.Directory != null)
            {
                scansFilePath = Path.Combine(isosFile.Directory.FullName, scansFilePath);
            }

            var scanList = LoadScansFile(scansFilePath);
            var scansFileIsMissing = false;

            if (scanList.Count > 0)
            {
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(scanList.Last().ElutionTime);
                datasetFileInfo.ScanCount = scanList.Last().Scan;
            }
            else
            {
                scansFileIsMissing = true;

                datasetFileInfo.ScanCount = (from item in isosData select item.Scan).Max();

                for (var scanIndex = 1; scanIndex <= datasetFileInfo.ScanCount; scanIndex++)
                {
                    var scanData = new ScansDataType
                    {
                        Scan = scanIndex,
                        ElutionTime = scanIndex,
                        MSLevel = 1
                    };

                    scanList.Add(scanData);
                }
            }

            // Step through the isos data and call mLCMS2DPlot.AddScan() for each scan

            var ionList = new List<LCMSDataPlotter.MSIonType>();
            var currentScanNumber = 0;

            // Note: we only need to update mLCMS2DPlot
            // The options for mLCMS2DPlotOverview will be cloned from mLCMS2DPlot.Options
            mLCMS2DPlot.Options.PlottingDeisotopedData = true;

            var maxMonoMass = mLCMS2DPlot.Options.MaxMonoMassForDeisotopedPlot;

            for (var index = 0; index < isosData.Count; index++)
            {
                if (isosData[index].Scan > currentScanNumber || index == isosData.Count - 1)
                {
                    // Store the cached values

                    if (ionList.Count > 0)
                    {
                        var currentScan = (from item in scanList where item.Scan == currentScanNumber select item).ToList().FirstOrDefault();

                        ionList.Sort(new LCMSDataPlotter.MSIonTypeComparer());
                        mLCMS2DPlot.AddScan(currentScanNumber, currentScan.MSLevel, currentScan.ElutionTime, ionList);

                        if (scansFileIsMissing && Options.SaveTICAndBPIPlots)
                        {
                            // Determine the TIC and BPI values using the data from the .isos file
                            double tic = 0;
                            double bpi = 0;

                            for (var dataIndex = 0; dataIndex < ionList.Count; dataIndex++)
                            {
                                tic += ionList[dataIndex].Intensity;

                                if (ionList[dataIndex].Intensity > bpi)
                                {
                                    bpi = ionList[dataIndex].Intensity;
                                }
                            }

                            mTICAndBPIPlot.AddData(currentScanNumber, currentScan.MSLevel, currentScan.ElutionTime, bpi, tic);
                        }
                    }

                    currentScanNumber = isosData[index].Scan;
                    ionList.Clear();
                }

                if (isosData[index].MonoMass <= maxMonoMass)
                {
                    var ion = new LCMSDataPlotter.MSIonType
                    {
                        // Note that we store .MonoMass in a field called .mz; we'll still be plotting monoisotopic mass
                        MZ = isosData[index].MonoMass,
                        Intensity = isosData[index].Abundance,
                        Charge = isosData[index].Charge
                    };

                    ionList.Add(ion);
                }
            }
        }

        private List<IsosDataType> LoadIsosFile(string isosFilePath, float maxFit)
        {
            // Keys in this dictionary are column names
            // Values are the column index, or -1 if not present
            var columnMapping = new Dictionary<string, int>
            {
                {"charge", -1},
                {"abundance", -1},
                {"mz", -1},
                {"fit", -1},
                {"monoisotopic_mw", -1}
            };

            var colIndexScanOrFrameNum = -1;

            var isosData = new List<IsosDataType>();
            var rowNumber = 0;

            var lastScan = 0;
            var lastScanParseErrors = 0;

            Console.WriteLine("  Reading the _isos.csv file; keeping data with fit <= {0:N2}", maxFit);

            using var reader = new StreamReader(new FileStream(isosFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

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
                    ParseColumnHeaders(columnMapping, dataColumns, "_isos.csv");

                    colIndexScanOrFrameNum = GetScanOrFrameColIndex(dataColumns, "_isos.csv");

                    continue;
                }

                var parseError = false;
                var currentScan = 0;

                try
                {
                    var isosItem = new IsosDataType();
                    int.TryParse(dataColumns[colIndexScanOrFrameNum], out isosItem.Scan);
                    currentScan = isosItem.Scan;

                    byte.TryParse(dataColumns[columnMapping["charge"]], out isosItem.Charge);
                    double.TryParse(dataColumns[columnMapping["abundance"]], out isosItem.Abundance);
                    double.TryParse(dataColumns[columnMapping["mz"]], out isosItem.MZ);
                    float.TryParse(dataColumns[columnMapping["fit"]], out isosItem.Fit);
                    double.TryParse(dataColumns[columnMapping["monoisotopic_mw"]], out isosItem.MonoMass);

                    if (isosItem.Fit <= maxFit)
                    {
                        isosData.Add(isosItem);
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
                        OnWarningEvent("Warning: Skipped {0} data points in scan {1} due to data conversion errors", lastScanParseErrors, lastScan);
                    }

                    lastScan = currentScan;
                    lastScanParseErrors = 0;
                }

                if (parseError)
                {
                    lastScanParseErrors++;
                }
            }

            return isosData;
        }

        private List<ScansDataType> LoadScansFile(string scansFilePath)
        {
            const string FILTERED_SCANS_SUFFIX = "_filtered_scans.csv";

            // Keys in this dictionary are column names
            // Values are the column index, or -1 if not present
            var columnMapping = new Dictionary<string, int>
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

            var scanList = new List<ScansDataType>();
            var rowNumber = 0;
            var colIndexScanInfo = -1;

            if (!File.Exists(scansFilePath) && scansFilePath.EndsWith(FILTERED_SCANS_SUFFIX, StringComparison.OrdinalIgnoreCase))
            {
                scansFilePath = scansFilePath.Substring(0, scansFilePath.Length - FILTERED_SCANS_SUFFIX.Length) + "_scans.csv";
            }

            if (!File.Exists(scansFilePath))
            {
                OnWarningEvent("Warning: _scans.csv file is missing; will plot vs. scan number instead of vs. elution time");
                return scanList;
            }

            Console.WriteLine("  Reading the _scans.csv file");

            using var reader = new StreamReader(new FileStream(scansFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

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
                    ParseColumnHeaders(columnMapping, dataColumns, "_scans.csv");

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
                    var scanData = new ScansDataType();

                    int.TryParse(dataColumns[colIndexScanOrFrameNum], out scanData.Scan);
                    float.TryParse(dataColumns[colIndexScanOrFrameTime], out scanData.ElutionTime);
                    int.TryParse(dataColumns[columnMapping["type"]], out scanData.MSLevel);
                    double.TryParse(dataColumns[columnMapping["bpi"]], out scanData.BasePeakIntensity);
                    double.TryParse(dataColumns[columnMapping["bpi_mz"]], out scanData.BasePeakMZ);
                    double.TryParse(dataColumns[columnMapping["tic"]], out scanData.TotalIonCurrent);
                    int.TryParse(dataColumns[columnMapping["num_peaks"]], out scanData.NumPeaks);
                    int.TryParse(dataColumns[columnMapping["num_deisotoped"]], out scanData.NumDeisotoped);

                    if (colIndexScanInfo > 0)
                    {
                        var infoText = dataColumns[colIndexScanInfo];

                        // Only store infoText in .FilterText if infoText is not simply an integer
                        if (!int.TryParse(infoText, out _))
                        {
                            scanData.FilterText = infoText;
                        }
                    }

                    scanList.Add(scanData);

                    if (Options.SaveTICAndBPIPlots)
                    {
                        mTICAndBPIPlot.AddData(scanData.Scan, scanData.MSLevel, scanData.ElutionTime, scanData.BasePeakIntensity, scanData.TotalIonCurrent);
                    }

                    string scanTypeName;

                    if (string.IsNullOrWhiteSpace(scanData.FilterText))
                    {
                        scanData.FilterText = scanData.MSLevel > 1 ? "HMSn" : "HMS";
                        scanTypeName = scanData.FilterText;
                    }
                    else
                    {
                        scanTypeName = XRawFileIO.GetScanTypeNameFromThermoScanFilterText(scanData.FilterText, false);
                    }

                    var scanStatsEntry = new ScanStatsEntry
                    {
                        ScanNumber = scanData.Scan,
                        ScanType = scanData.MSLevel,
                        ScanTypeName = scanTypeName,
                        ScanFilterText = XRawFileIO.MakeGenericThermoScanFilter(scanData.FilterText),
                        ElutionTime = scanData.ElutionTime.ToString("0.0###"),
                        TotalIonIntensity = StringUtilities.ValueToString(scanData.TotalIonCurrent, 5),
                        BasePeakIntensity = StringUtilities.ValueToString(scanData.BasePeakIntensity, 5),
                        BasePeakMZ = scanData.BasePeakMZ.ToString("0.0###"),
                        BasePeakSignalToNoiseRatio = "0",
                        IonCount = scanData.NumDeisotoped,
                        IonCountRaw = scanData.NumPeaks,
                        ExtendedScanInfo =
                        {
                            CollisionMode = string.Empty,
                            ScanFilterText = scanData.FilterText
                        }
                    };

                    mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);
                }
                catch (Exception ex)
                {
                    OnWarningEvent("Warning: Ignoring scan {0} since data conversion error: {1}", dataColumns[columnMapping["scan_num"]], ex.Message);
                }
            }

            return scanList;
        }

        /// <summary>
        /// Update the column mapping using the actual column names
        /// </summary>
        /// <param name="columnMapping">Dictionary where keys are column names and values are the column index, or -1 if not present</param>
        /// <param name="dataColumns">Column names from the header row of the input file</param>
        /// <param name="fileDescription"></param>
        private void ParseColumnHeaders(Dictionary<string, int> columnMapping, IList<string> dataColumns, string fileDescription)
        {
            foreach (var columnName in columnMapping.Keys.ToList())
            {
                var colIndex = dataColumns.IndexOf(columnName);

                if (colIndex >= 0)
                {
                    columnMapping[columnName] = colIndex;
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
        /// <remarks>Will also read the _scans.csv file if present (to determine ElutionTime and MSLevel</remarks>
        /// <param name="dataFilePath">Isos file path</param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            var isosFile = MSFileInfoScanner.GetFileInfo(dataFilePath);

            if (!isosFile.Exists)
            {
                OnErrorEvent("_isos.csv file not found: {0}", dataFilePath);
                return false;
            }

            var datasetID = Options.DatasetID;

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

            if (Options.SaveTICAndBPIPlots || Options.CreateDatasetInfoFile || Options.CreateScanStatsFile || Options.SaveLCMS2DPlots)
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
