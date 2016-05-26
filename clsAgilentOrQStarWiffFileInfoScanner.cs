using System;
using System.IO;
using pwiz.CLI.msdata;

// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started in 2005
//
// Updated in March 2012 to use Proteowizard to read data from QTrap .Wiff files
// (cannot read MS data or TIC values from Agilent .Wiff files)

namespace MSFileInfoScanner
{
    [CLSCompliant(false)]
    public class clsAgilentTOFOrQStarWiffFileInfoScanner : clsMSFileInfoProcessorBaseClass
    {

        // Note: The extension must be in all caps
        public const string AGILENT_TOF_OR_QSTAR_FILE_EXTENSION = ".WIFF";

        public override string GetDatasetNameViaPath(string strDataFilePath)
        {
            // The dataset name is simply the file name without .wiff
            try {
                return Path.GetFileNameWithoutExtension(strDataFilePath);
            } catch (Exception) {
                return string.Empty;
            }
        }

        public override bool ProcessDataFile(string strDataFilePath, clsDatasetFileInfo datasetFileInfo)
        {
            // Returns True if success, False if an error

            // Override strDataFilePath here, if needed
            // strDataFilePath = strDataFilePath;

            // Obtain the full path to the file
            var fiDatasetFile = new FileInfo(strDataFilePath);


            var blnTest = false;
            if (blnTest) {
                TestPWiz(fiDatasetFile.FullName);
            }


            datasetFileInfo.FileSystemCreationTime = fiDatasetFile.CreationTime;
            datasetFileInfo.FileSystemModificationTime = fiDatasetFile.LastWriteTime;

            // Using the file system modification time as the acquisition end time
            datasetFileInfo.AcqTimeStart = datasetFileInfo.FileSystemModificationTime;
            datasetFileInfo.AcqTimeEnd = datasetFileInfo.FileSystemModificationTime;

            datasetFileInfo.DatasetID = 0;
            datasetFileInfo.DatasetName = GetDatasetNameViaPath(fiDatasetFile.Name);
            datasetFileInfo.FileExtension = fiDatasetFile.Extension;
            datasetFileInfo.FileSizeBytes = fiDatasetFile.Length;

            datasetFileInfo.ScanCount = 0;

            mDatasetStatsSummarizer.ClearCachedData();
            mLCMS2DPlot.Options.UseObservedMinScan = false;

            // Add the proteowizard assembly resolve prior to calling ProcessWiffFile
            pwiz.ProteowizardWrapper.DependencyLoader.AddAssemblyResolver();

            ProcessWiffFile(fiDatasetFile, datasetFileInfo);

            // Read the file info from the file system
            // (much of this is already in datasetFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
            UpdateDatasetFileStats(fiDatasetFile, datasetFileInfo.DatasetID);

            // Copy over the updated filetime info and scan info from datasetFileInfo to mDatasetFileInfo
            mDatasetStatsSummarizer.DatasetFileInfo.DatasetName = string.Copy(datasetFileInfo.DatasetName);
            mDatasetStatsSummarizer.DatasetFileInfo.FileExtension = string.Copy(datasetFileInfo.FileExtension);
            mDatasetStatsSummarizer.DatasetFileInfo.FileSizeBytes = datasetFileInfo.FileSizeBytes;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeStart;
            mDatasetStatsSummarizer.DatasetFileInfo.AcqTimeEnd = datasetFileInfo.AcqTimeEnd;
            mDatasetStatsSummarizer.DatasetFileInfo.ScanCount = datasetFileInfo.ScanCount;

            return true;

        }

        private void ProcessWiffFile(FileInfo fiDatasetFile, clsDatasetFileInfo datasetFileInfo)
        {
            try
            {
                // Open the .Wiff file using the ProteoWizardWrapper

                var objPWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(fiDatasetFile.FullName);

                try
                {
                    var dtRunStartTime = Convert.ToDateTime(objPWiz.RunStartTime);

                    // Update AcqTimeEnd if possible
                    // Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
                    dtRunStartTime = dtRunStartTime.ToUniversalTime();
                    if (dtRunStartTime < datasetFileInfo.AcqTimeEnd)
                    {
                        if (datasetFileInfo.AcqTimeEnd.Subtract(dtRunStartTime).TotalDays < 1)
                        {
                            datasetFileInfo.AcqTimeStart = dtRunStartTime;
                        }
                    }

                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the Proteowizard Data Parser class
                var pWizParser = new clsProteowizardDataParser(objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot,
                                                            mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI,
                                                            mCheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                pWizParser.ErrorEvent += mPWizParser_ErrorEvent;
                pWizParser.MessageEvent += mPWizParser_MessageEvent;

                var blnTICStored = false;
                var blnSRMDataCached = false;
                double dblRuntimeMinutes = 0;

                // Note that SRM .Wiff files will only have chromatograms, and no spectra

                if (objPWiz.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out blnTICStored, out blnSRMDataCached, out dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);

                }

                if (objPWiz.SpectrumCount > 0 & !blnSRMDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    pWizParser.StoreMSSpectraInfo(datasetFileInfo, blnTICStored, ref dblRuntimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, dblRuntimeMinutes);
                }

                objPWiz.Dispose();
                PRISM.Processes.clsProgRunner.GarbageCollectNow();

            }
            catch (Exception ex)
            {
                ReportError("Error using ProteoWizard reader: " + ex.Message);
            }
        }

        private void TestPWiz(string strFilePath)
        {
            const bool RUN_BENCHMARKS = false;

            try {
                var objPWiz2 = new MSDataFile(strFilePath);


                Console.WriteLine("Spectrum count: " + objPWiz2.run.spectrumList.size());
                Console.WriteLine();

                if (objPWiz2.run.spectrumList.size() > 0) {
                    var intSpectrumIndex = 0;


                    do {
                        var oSpectrum = objPWiz2.run.spectrumList.spectrum(intSpectrumIndex, getBinaryData: true);

                        pwiz.CLI.data.CVParam param;
                        if (oSpectrum.scanList.scans.Count > 0) {
                            if (clsProteowizardDataParser.TryGetCVParam(oSpectrum.scanList.scans[0].cvParams, pwiz.CLI.cv.CVID.MS_scan_start_time, out param)) {
                                var intScanNum = intSpectrumIndex + 1;
                                var dblStartTimeMinutes = param.timeInSeconds() / 60.0;

                                Console.WriteLine("ScanIndex " + intSpectrumIndex + ", Scan " + intScanNum + ", Elution Time " + dblStartTimeMinutes + " minutes");
                            }

                        }

                        // Use the following to determine info on this spectrum
                        if (clsProteowizardDataParser.TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_ms_level, out param))
                        {
                            int intMSLevel;
                            int.TryParse(param.value, out intMSLevel);
                        }

                        // Use the following to get the MZs and Intensities
                        var oMZs = oSpectrum.getMZArray();
                        oSpectrum.getIntensityArray();

                        if (oMZs.data.Count > 0) {
                            Console.WriteLine("  Data count: " + oMZs.data.Count);


                            if (RUN_BENCHMARKS) {
                                double dblTIC1 = 0;
                                double dblTIC2 = 0;
                                var dtStartTime = default(DateTime);
                                var dtEndTime = default(DateTime);
                                double dtRunTimeSeconds1 = 0;
                                double dtRunTimeSeconds2 = 0;
                                const int LOOP_ITERATIONS = 2000;

                                // Note from Matt Chambers (matt.chambers42@gmail.com) 
                                // Repeatedly accessing items directly via oMZs.data() can be very slow
                                // With 700 points and 2000 iterations, it takes anywhere from 0.6 to 1.1 seconds to run from dtStartTime to dtEndTime
                                dtStartTime = DateTime.Now;
                                for (var j = 1; j <= LOOP_ITERATIONS; j++) {
                                    for (var intIndex = 0; intIndex <= oMZs.data.Count - 1; intIndex++) {
                                        dblTIC1 += oMZs.data[intIndex];
                                    }
                                }
                                dtEndTime = DateTime.Now;
                                dtRunTimeSeconds1 = dtEndTime.Subtract(dtStartTime).TotalSeconds;

                                // The preferred method is to copy the data from .data to a locally-stored mzArray var
                                // With 700 points and 2000 iterations, it takes 0.016 seconds to run from dtStartTime to dtEndTime
                                dtStartTime = DateTime.Now;
                                for (var j = 1; j <= LOOP_ITERATIONS; j++) {
                                    var oMzArray = oMZs.data;
                                    for (var intIndex = 0; intIndex <= oMzArray.Count - 1; intIndex++) {
                                        dblTIC2 += oMzArray[intIndex];
                                    }
                                }
                                dtEndTime = DateTime.Now;
                                dtRunTimeSeconds2 = dtEndTime.Subtract(dtStartTime).TotalSeconds;

                                Console.WriteLine("  " + oMZs.data.Count + " points with " + LOOP_ITERATIONS + " iterations gives Runtime1=" + dtRunTimeSeconds1.ToString("0.000") + " sec. vs. Runtime2=" + dtRunTimeSeconds2.ToString("0.000") + " sec.");

                                if (Math.Abs(dblTIC1 - dblTIC2) > float.Epsilon) {
                                    Console.WriteLine("  TIC values don't agree; this is unexpected");
                                }
                            }

                        }

                        if (intSpectrumIndex < 25) {
                            intSpectrumIndex += 1;
                        } else {
                            intSpectrumIndex += 50;
                        }

                    } while (intSpectrumIndex < objPWiz2.run.spectrumList.size());
                }


                if (objPWiz2.run.chromatogramList.size() > 0) {
                    var intChromIndex = 0;


                    do {
                        var oTimeIntensityPairList = new TimeIntensityPairList();

                        // Note that even for a small .Wiff file (1.5 MB), obtaining the Chromatogram list will take some time (20 to 60 seconds)
                        // The chromatogram at index 0 should be the TIC
                        // The chromatogram at index >=1 will be each SRM

                        var oChromatogram = objPWiz2.run.chromatogramList.chromatogram(intChromIndex, getBinaryData: true);

                        // Determine the chromatogram type
                        pwiz.CLI.data.CVParam param;

                        if (clsProteowizardDataParser.TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, out param)) {
                            // Obtain the data
                            oChromatogram.getTimeIntensityPairs(ref oTimeIntensityPairList);
                        }

                        if (clsProteowizardDataParser.TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, out param)) {
                            // Obtain the SRM scan
                            oChromatogram.getTimeIntensityPairs(ref oTimeIntensityPairList);
                        }

                        intChromIndex += 1;
                    } while (intChromIndex < 50 && intChromIndex < objPWiz2.run.chromatogramList.size());
                }

            } catch (Exception ex) {
                ReportError("Error using ProteoWizard reader: " + ex.Message);
            }

        }

        private void mPWizParser_ErrorEvent(string message)
        {
            ReportError(message);
        }

        private void mPWizParser_MessageEvent(string message)
        {
            ShowMessage(message);
        }
    }
}
