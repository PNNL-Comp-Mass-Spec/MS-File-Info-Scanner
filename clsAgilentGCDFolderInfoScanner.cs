using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MSFileInfoScanner.DatasetStats;
using PRISM;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
//

namespace MSFileInfoScanner
{
    public class clsAgilentGCDFolderInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps

        public const string AGILENT_DATA_FOLDER_D_EXTENSION = ".D";
        public const string AGILENT_MS_DATA_FILE = "DATA.MS";
        public const string AGILENT_ACQ_METHOD_FILE = "acqmeth.txt";

        public const string AGILENT_GC_INI_FILE = "GC.ini";
        private const string ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE = "Equilibration Time";
        private const string ACQ_METHOD_FILE_RUN_TIME_LINE = "Run Time";

        private const string ACQ_METHOD_FILE_POST_RUN_LINE = "(Post Run)";

        private readonly Regex mExtractTime;

        private class clsLineMatchSearchInfo
        {
            public readonly bool MatchLineStart;

            public bool Matched;
            public clsLineMatchSearchInfo(bool bMatchLineStart)
            {
                MatchLineStart = bMatchLineStart;
                Matched = false;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public clsAgilentGCDFolderInfoScanner()
        {
            mExtractTime = new Regex("([0-9.]+) min", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

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

        private bool ExtractRunTime(string runTimeText, out double runTimeMinutes)
        {

            var reMatch = mExtractTime.Match(runTimeText);

            if (reMatch.Success)
            {
                if (double.TryParse(reMatch.Groups[1].Value, out runTimeMinutes))
                {
                    return true;
                }
            }

            runTimeMinutes = 0;
            return false;

        }

        private bool ParseAcqMethodFile(string directoryPath, DatasetFileInfo datasetFileInfo)
        {
            double totalRuntime = 0;

            var runTimeFound = false;
            bool success;

            try
            {
                // Open the acqmeth.txt file
                var filePath = Path.Combine(directoryPath, AGILENT_ACQ_METHOD_FILE);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                // Populate a dictionary var with the text strings for finding lines with runtime information
                // Note that "Post Run" occurs twice in the file, so we use clsLineMatchSearchInfo.Matched to track whether or not the text has been matched
                var dctRunTimeText = new Dictionary<string, clsLineMatchSearchInfo>
                {
                    {ACQ_METHOD_FILE_EQUILIBRATION_TIME_LINE, new clsLineMatchSearchInfo(true)},
                    {ACQ_METHOD_FILE_RUN_TIME_LINE, new clsLineMatchSearchInfo(true)}
                };

                // We could also add in the "Post Run" time for determining total acquisition time, but we don't do this, to stay consistent with run times reported by the MS file
                // dctRunTimeText.Add(ACQ_METHOD_FILE_POST_RUN_LINE, New clsLineMatchSearchInfo(False))

                using (var reader = new StreamReader(filePath))
                {

                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        foreach (var key in dctRunTimeText.Keys)
                        {
                            if (dctRunTimeText[key].Matched)
                            {
                                continue;
                            }

                            bool matchSuccess;
                            if (dctRunTimeText[key].MatchLineStart)
                            {
                                matchSuccess = dataLine.StartsWith(key);
                            }
                            else
                            {
                                matchSuccess = dataLine.Contains(key);
                            }

                            if (!matchSuccess)
                            {
                                continue;
                            }

                            if (!ExtractRunTime(dataLine, out var runTime))
                            {
                                continue;
                            }

                            dctRunTimeText[key].Matched = true;
                            totalRuntime += runTime;
                            runTimeFound = true;
                            break;
                        }
                    }
                }

                success = runTimeFound;

            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent("Exception reading " + AGILENT_ACQ_METHOD_FILE + ": " + ex.Message, ex);
                success = false;
            }

            if (success)
            {
                // Update the acquisition start time
                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-totalRuntime);
            }

            return success;

        }

        private bool ParseGCIniFile(string directoryPath, DatasetFileInfo datasetFileInfo)
        {
            double totalRuntime = 0;

            var success = false;

            try
            {
                // Open the GC.ini file
                var filePath = Path.Combine(directoryPath, AGILENT_GC_INI_FILE);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                using (var reader = new StreamReader(filePath))
                {

                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (!dataLine.StartsWith("gc.runlength"))
                            continue;

                        // Runtime is the value after the equals sign
                        var splitLine = dataLine.Split('=');
                        if (splitLine.Length <= 1)
                        {
                            continue;
                        }

                        if (double.TryParse(splitLine[1], out totalRuntime))
                        {
                            success = true;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                // Exception reading file
                OnErrorEvent("Exception reading " + AGILENT_GC_INI_FILE + ": " + ex.Message, ex);
                success = false;
            }

            if (success)
            {
                // Update the acquisition start time
                datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd.AddMinutes(-totalRuntime);
            }

            return success;

        }

        private bool ProcessChemstationMSDataFile(string datafilePath, DatasetFileInfo datasetFileInfo)
        {
            bool success;
            var currentIndex = 0;

            try
            {
                using (var reader = new ChemstationMSFileReader.clsChemstationDataMSFileReader(datafilePath))
                {

                    datasetFileInfo.AcqTimeStart = reader.Header.AcqDate;
                    datasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeStart.AddMinutes(reader.Header.RetentionTimeMinutesEnd);

                    datasetFileInfo.ScanCount = reader.Header.SpectraCount;

                    for (var spectrumIndex = 0; spectrumIndex <= datasetFileInfo.ScanCount - 1; spectrumIndex++)
                    {
                        currentIndex = spectrumIndex;

                        ChemstationMSFileReader.clsSpectralRecord spectrum = null;
                        List<float> mzList = null;
                        List<int> intensityList = null;
                        const int msLevel = 1;

                        bool validSpectrum;
                        try
                        {
                            reader.GetSpectrum(spectrumIndex, ref spectrum);
                            mzList = spectrum.Mzs;
                            intensityList = spectrum.Intensities;
                            validSpectrum = true;
                        }
                        catch (Exception ex)
                        {
                            OnWarningEvent("Exception obtaining data from the MS file for spectrum index " + currentIndex + ": " + ex.Message);
                            validSpectrum = false;
                        }

                        if (validSpectrum)
                        {
                            var scanStatsEntry = new ScanStatsEntry
                            {
                                ScanNumber = spectrumIndex + 1,
                                ScanType = msLevel,
                                ScanTypeName = "GC-MS",
                                ScanFilterText = "",
                                ElutionTime = spectrum.RetentionTimeMinutes.ToString("0.0###"),
                                TotalIonIntensity = StringUtilities.ValueToString(spectrum.TIC, 1),
                                BasePeakIntensity = StringUtilities.ValueToString(spectrum.BasePeakAbundance, 1),
                                BasePeakMZ = spectrum.BasePeakMZ.ToString("0.0###"),
                                BasePeakSignalToNoiseRatio = "0",
                                IonCount = mzList.Count
                            };

                            scanStatsEntry.IonCountRaw = scanStatsEntry.IonCount;

                            mDatasetStatsSummarizer.AddDatasetScan(scanStatsEntry);

                            if (mSaveTICAndBPI)
                            {
                                mTICAndBPIPlot.AddData(scanStatsEntry.ScanNumber, msLevel, spectrum.RetentionTimeMinutes, spectrum.BasePeakAbundance, spectrum.TIC);

                                if (mzList.Count > 0)
                                {
                                    var ionsMZ = new double[mzList.Count];
                                    var ionsIntensity = new double[mzList.Count];

                                    for (var index = 0; index <= mzList.Count - 1; index++)
                                    {
                                        ionsMZ[index] = mzList[index];
                                        ionsIntensity[index] = intensityList[index];
                                    }

                                    mLCMS2DPlot.AddScan(scanStatsEntry.ScanNumber, msLevel, spectrum.RetentionTimeMinutes, ionsMZ.Length, ionsMZ, ionsIntensity);
                                }

                            }

                            if (mCheckCentroidingStatus)
                            {
                                var mzDoubles = new List<double>(mzList.Count);
                                mzDoubles.AddRange(mzList.Select(ion => (double)ion));
                                mDatasetStatsSummarizer.ClassifySpectrum(mzDoubles, msLevel, "Scan " + scanStatsEntry.ScanNumber);
                            }

                        }

                    }

                }

                success = true;

            }
            catch (Exception ex)
            {
                // Exception reading file
                OnWarningEvent("Exception reading data from the MS file at spectrum index " + currentIndex + ": " + ex.Message);
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
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, DatasetFileInfo datasetFileInfo)
        {

            var success = false;
            var acqTimeDetermined = false;

            ResetResults();

            try
            {
                var agilentDFolder = new DirectoryInfo(dataFilePath);
                var msDataFilePath = Path.Combine(agilentDFolder.FullName, AGILENT_MS_DATA_FILE);

                datasetFileInfo.FileSystemCreationTime = agilentDFolder.CreationTime;
                datasetFileInfo.FileSystemModificationTime = agilentDFolder.LastWriteTime;

                // The acquisition times will get updated below to more accurate values
                datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
                datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

                datasetFileInfo.DatasetName = GetDatasetNameViaPath(agilentDFolder.Name);
                datasetFileInfo.FileExtension = agilentDFolder.Extension;
                datasetFileInfo.FileSizeBytes = 0;

                // Look for the MS file
                // Use its modification time to get an initial estimate for the acquisition end time
                // Assign the .MS file's size to .FileSizeBytes
                var msDatafile = new FileInfo(msDataFilePath);
                if (msDatafile.Exists)
                {
                    datasetFileInfo.FileSizeBytes = msDatafile.Length;
                    datasetFileInfo.AcqTimeStart = msDatafile.LastWriteTime;
                    datasetFileInfo.AcqTimeEnd = msDatafile.LastWriteTime;

                    // Read the file info from the file system
                    // This will also compute the SHA-1 hash of the Data.MS file and add it to mDatasetStatsSummarizer.DatasetFileInfo
                    UpdateDatasetFileStats(msDatafile, datasetFileInfo.DatasetID);

                    success = true;
                }
                else
                {
                    // Compute the hash of the largest file in the .D directory
                    AddLargestInstrumentFile(agilentDFolder);
                }

                datasetFileInfo.ScanCount = 0;

                if (success)
                {
                    // Read the detailed data from the MS file
                    success = ProcessChemstationMSDataFile(msDataFilePath, datasetFileInfo);

                    if (success)
                    {
                        acqTimeDetermined = true;
                    }

                }

                if (!success)
                {
                    // MS file not found (or problems parsing); use acqmeth.txt and/or GC.ini

                    // The timestamp of the acqmeth.txt file or GC.ini file is more accurate than the GC.ini file, so we'll use that
                    var methodFile = new FileInfo(Path.Combine(agilentDFolder.FullName, AGILENT_ACQ_METHOD_FILE));
                    if (!methodFile.Exists)
                    {
                        methodFile = new FileInfo(Path.Combine(agilentDFolder.FullName, AGILENT_GC_INI_FILE));
                    }

                    if (methodFile.Exists)
                    {

                        // ReSharper disable once CommentTypo
                        // Update the AcqTimes only if the LastWriteTime of the acqmeth.txt or GC.ini file is within the next 60 minutes of .AcqTimeEnd
                        if (!success || methodFile.LastWriteTime.Subtract(datasetFileInfo.AcqTimeEnd).TotalMinutes < 60)
                        {
                            datasetFileInfo.AcqTimeStart = methodFile.LastWriteTime;
                            datasetFileInfo.AcqTimeEnd = methodFile.LastWriteTime;
                            success = true;
                        }

                        if (datasetFileInfo.FileSizeBytes == 0)
                        {
                            // File size was not determined from the MS file
                            // Instead, sum up the sizes of all of the files in this directory
                            foreach (var item in agilentDFolder.GetFiles())
                            {
                                datasetFileInfo.FileSizeBytes += item.Length;
                            }

                            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
                        }

                    }
                }

                if (!acqTimeDetermined)
                {
                    try
                    {
                        // Parse the acqmeth.txt file to determine the actual values for .AcqTimeStart and .AcqTimeEnd
                        success = ParseAcqMethodFile(dataFilePath, datasetFileInfo);

                        if (!success)
                        {
                            // Try to extract Runtime from the GC.ini file
                            success = ParseGCIniFile(dataFilePath, datasetFileInfo);
                        }

                    }
                    catch (Exception)
                    {
                        // Error parsing the acqmeth.txt file or GC.in file; do not abort
                    }

                    // We set success to true, even if either of the above functions fail
                    success = true;
                }

                if (success)
                {
                    // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
                    mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
                    mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);

                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
                    mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
                    mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception parsing GC .D directory: " + ex.Message, ex);
                success = false;
            }

            PostProcessTasks();
            return success;
        }

    }
}
