using System;
using System.IO;
using pwiz.CLI.msdata;
using PRISM;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//
// Updated in March 2012 to use ProteoWizard to read data from QTrap .Wiff files
// (cannot read MS data or TIC values from Agilent .Wiff files)

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsAgilentTOFOrQStarWiffFileInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps
        public const string AGILENT_TOF_OR_QSTAR_FILE_EXTENSION = ".WIFF";

        public override string GetDatasetNameViaPath(string dataFilePath)
        {
            // The dataset name is simply the file name without .wiff
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
        /// Process the dataset
        /// </summary>
        /// <param name="dataFilePath"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if success, False if an error</returns>
        /// <remarks></remarks>
        public override bool ProcessDataFile(string dataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            ResetResults();

            // Override dataFilePath here, if needed
            // dataFilePath = dataFilePath;

            // Obtain the full path to the file
            var datasetFile = new FileInfo(dataFilePath);

            datasetFileInfo.FileSystemCreationTime = datasetFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = datasetFile.LastWriteTime;

            // Using the file system modification time as the acquisition end time
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = 0;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(datasetFile.Name);
            datasetFileInfo.FileExtension = datasetFile.Extension;
            datasetFileInfo.FileSizeBytes = datasetFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();
            mLCMS2DPlot.Options.UseObservedMinScan = false;

            ProcessWiffFile(datasetFile, datasetFileInfo);

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            // This will also compute the SHA-1 hash of the .Wiff file and add it to mDatasetStatsSummarizer.DatasetFileInfo
            UpdateDatasetFileStats(datasetFile, datasetFileInfo.DatasetID);

            // Copy over the updated file time info and scan info from datasetFileInfo to mDatasetStatsSummarizer.DatasetFileInfo
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;

            PostProcessTasks();

            return true;

        }

        private void ProcessWiffFile(FileSystemInfo datasetFile, clsDatasetFileInfo datasetFileInfo)
        {
            try
            {
                // Open the .Wiff file using the ProteoWizardWrapper

                var pWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(datasetFile.FullName);

                try
                {
                    var runStartTime = Convert.ToDateTime(pWiz.RunStartTime);

                    // Update AcqTimeEnd if possible
                    // Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
                    runStartTime = runStartTime.ToUniversalTime();
                    if (runStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                        {
                            datasetFileInfo.AcqTimeStart = runStartTime;
                        }
                    }

                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the ProteoWizard Data Parser class
                var pWizParser = new clsProteoWizardDataParser(pWiz, mDatasetStatsSummarizer, mTICAndBPIPlot,
                                                               mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI,
                                                               mCheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                RegisterEvents(pWizParser);

                var ticStored = false;
                var srmDataCached = false;
                double runtimeMinutes = 0;

                // Note that SRM .Wiff files will only have chromatograms, and no spectra

                if (pWiz.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                }

                if (pWiz.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);
                }

                pWiz.Dispose();
                ProgRunner.GarbageCollectNow();

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error using ProteoWizard reader: " + ex.Message, ex);
            }
        }

        private void TestPWiz(string filePath)
        {
            const bool RUN_BENCHMARKS = false;

            try
            {
                var pWiz2 = new MSDataFile(filePath);

                Console.WriteLine("Spectrum count: " + pWiz2.run.spectrumList.size());
                Console.WriteLine();

                if (pWiz2.run.spectrumList.size() > 0)
                {
                    var spectrumIndex = 0;

                    do
                    {
                        var spectrum = pWiz2.run.spectrumList.spectrum(spectrumIndex, getBinaryData: true);

                        pwiz.CLI.data.CVParam param;
                        if (spectrum.scanList.scans.Count > 0)
                        {
                            if (clsProteoWizardDataParser.TryGetCVParam(spectrum.scanList.scans[0].cvParams, pwiz.CLI.cv.CVID.MS_scan_start_time, out param))
                            {
                                var scanNum = spectrumIndex + 1;
                                var startTimeMinutes = param.timeInSeconds() / 60.0;

                                Console.WriteLine("ScanIndex " + spectrumIndex + ", Scan " + scanNum + ", Elution Time " + startTimeMinutes + " minutes");
                            }

                        }

                        // Use the following to determine info on this spectrum
                        if (clsProteoWizardDataParser.TryGetCVParam(spectrum.cvParams, pwiz.CLI.cv.CVID.MS_ms_level, out param))
                        {
                            int.TryParse(param.value, out _);
                        }

                        // Use the following to get the MZs and Intensities
                        var mzList = spectrum.getMZArray();
                        spectrum.getIntensityArray();

                        if (mzList.data.Count > 0)
                        {
                            Console.WriteLine("  Data count: " + mzList.data.Count);

                            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                            if (RUN_BENCHMARKS)
                            {
                                double tic1 = 0;
                                double tic2 = 0;
                                var startTime = default(DateTime);
                                var endTime = default(DateTime);
                                double runTimeSeconds1 = 0;
                                double runTimeSeconds2 = 0;
                                const int LOOP_ITERATIONS = 2000;

                                // Note from Matt Chambers (matt.chambers42 at GMail)
                                // Repeatedly accessing items directly via mzList.data() can be very slow
                                // With 700 points and 2000 iterations, it takes anywhere from 0.6 to 1.1 seconds to run from startTime to endTime
                                startTime = DateTime.Now;
                                for (var j = 1; j <= LOOP_ITERATIONS; j++)
                                {
                                    for (var index = 0; index <= mzList.data.Count - 1; index++)
                                    {
                                        tic1 += mzList.data[index];
                                    }
                                }
                                endTime = DateTime.Now;
                                runTimeSeconds1 = endTime.Subtract(startTime).TotalSeconds;

                                // The preferred method is to copy the data from .data to a locally-stored mzArray var
                                // With 700 points and 2000 iterations, it takes 0.016 seconds to run from startTime to endTime
                                startTime = DateTime.Now;
                                for (var j = 1; j <= LOOP_ITERATIONS; j++)
                                {
                                    var mzArray = mzList.data;
                                    for (var index = 0; index <= mzArray.Count - 1; index++)
                                    {
                                        tic2 += mzArray[index];
                                    }
                                }
                                endTime = DateTime.Now;
                                runTimeSeconds2 = endTime.Subtract(startTime).TotalSeconds;

                                Console.WriteLine("  " + mzList.data.Count + " points with " + LOOP_ITERATIONS + " iterations gives Runtime1=" + runTimeSeconds1.ToString("0.0##") + " sec. vs. Runtime2=" + runTimeSeconds2.ToString("0.0##") + " sec.");

                                if (Math.Abs(tic1 - tic2) > float.Epsilon)
                                {
                                    Console.WriteLine("  TIC values don't agree; this is unexpected");
                                }
                            }

                        }

                        if (spectrumIndex < 25)
                        {
                            spectrumIndex += 1;
                        }
                        else
                        {
                            spectrumIndex += 50;
                        }

                    } while (spectrumIndex < pWiz2.run.spectrumList.size());
                }

                if (pWiz2.run.chromatogramList.size() > 0)
                {
                    var chromatogramIndex = 0;

                    do
                    {
                        var timeIntensityPairList = new TimeIntensityPairList();

                        // Note that even for a small .Wiff file (1.5 MB), obtaining the Chromatogram list will take some time (20 to 60 seconds)
                        // The chromatogram at index 0 should be the TIC
                        // The chromatogram at index >=1 will be each SRM

                        var chromatogram = pWiz2.run.chromatogramList.chromatogram(chromatogramIndex, getBinaryData: true);

                        // Determine the chromatogram type

                        if (clsProteoWizardDataParser.TryGetCVParam(chromatogram.cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, out var param))
                        {
                            // Obtain the data
                            chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                        }

                        if (clsProteoWizardDataParser.TryGetCVParam(chromatogram.cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, out param))
                        {
                            // Obtain the SRM scan
                            chromatogram.getTimeIntensityPairs(ref timeIntensityPairList);
                        }

                        chromatogramIndex += 1;
                    } while (chromatogramIndex < 50 && chromatogramIndex < pWiz2.run.chromatogramList.size());
                }

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error using ProteoWizard reader: " + ex.Message, ex);
            }

        }

    }
}
