﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;
using pwiz.ProteowizardWrapper;

namespace MSFileInfoScanner.Readers
{
    public abstract class ProteoWizardScanner : MSFileInfoProcessorBaseClass
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        /// <param name="lcms2DPlotOptions"></param>
        protected ProteoWizardScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) : base(options, lcms2DPlotOptions)
        {
        }

        /// <summary>
        /// This function is used to determine one or more overall quality scores
        /// </summary>
        /// <param name="msFileReader"></param>
        /// <param name="datasetFileInfo"></param>
        private void ComputeQualityScores(
            MSDataFileReader msFileReader,
            DatasetFileInfo datasetFileInfo)
        {
            float overallScore;

            double overallAvgIntensitySum = 0;
            var overallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0)
            {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all of the data using msFileReader
                const int msLevelFilter = 1;
                overallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(msLevelFilter);
            }
            else
            {
                var scanCount = msFileReader.SpectrumCount;
                GetStartAndEndScans(scanCount, out var scanStart, out var scanEnd);

                var scanNumberToIndexMap = msFileReader.GetScanToIndexMapping();

                for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
                {
                    if (!scanNumberToIndexMap.TryGetValue(scanNumber, out var scanIndex))
                    {
                        continue;
                    }

                    var spectrum = msFileReader.GetSpectrum(scanIndex, true);

                    if (spectrum == null)
                    {
                        continue;
                    }

                    if (spectrum.Intensities.Length == 0)
                    {
                        continue;
                    }

                    // Keep track of the quality scores and then store one or more overall quality scores in datasetFileInfo.OverallQualityScore
                    // For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                    var intensitySum = spectrum.Intensities.Sum();

                    overallAvgIntensitySum += intensitySum / spectrum.Intensities.Length;

                    overallAvgCount++;
                }

                if (overallAvgCount > 0)
                {
                    overallScore = (float)(overallAvgIntensitySum / overallAvgCount);
                }
                else
                {
                    overallScore = 0;
                }
            }

            datasetFileInfo.OverallQualityScore = overallScore;
        }

        /// <summary>
        /// Load data using the ProteoWizardWrapper
        /// </summary>
        /// <param name="datasetFile"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool ProcessWithProteoWizard(FileInfo datasetFile, DatasetFileInfo datasetFileInfo)
        {
            try
            {
                var msFileReader = new pwiz.ProteowizardWrapper.MSDataFileReader(datasetFile.FullName);

                try
                {
                    var runStartTime = Convert.ToDateTime(msFileReader.RunStartTime);

                    // Update AcqTimeEnd if possible
                    // Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
                    runStartTime = runStartTime.ToUniversalTime();
                    if (runStartTime < datasetFileInfo.AcqTimeEnd && datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1)
                    {
                        datasetFileInfo.AcqTimeStart = runStartTime;
                    }
                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the ProteoWizard Data Parser class
                // Assumes spectra are high res MS1 and MS2
                var pWizParser = new ProteoWizardDataParser(
                    msFileReader,
                    mDatasetStatsSummarizer,
                    mTICAndBPIPlot,
                    mLCMS2DPlot,
                    Options.SaveLCMS2DPlots,
                    Options.SaveTICAndBPIPlots,
                    Options.CheckCentroidingStatus)
                {
                    HighResMS1 = true,
                    HighResMS2 = true
                };

                RegisterEvents(pWizParser);

                // Note that SRM .Wiff files will only have chromatograms, and no spectra

                var ticStored = false;
                var srmDataCached = false;
                double runtimeMinutes = 0;

                if (msFileReader.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = msFileReader.ChromatogramCount;
                }

                if (msFileReader.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    var skipExistingScans = (msFileReader.ChromatogramCount > 0);
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans,
                                                  skipScansWithNoIons: true,
                                                  maxScansToTrackInDetail: MSFileInfoProcessorBaseClass.MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MSFileInfoProcessorBaseClass.MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = msFileReader.SpectrumCount;
                }

                if (Options.ComputeOverallQualityScores)
                {
                    // Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(msFileReader, datasetFileInfo);
                }

                if (Options.MS2MzMin > 0 && datasetFileInfo.ScanCount > 0)
                {
                    // Verify that all of the MS2 spectra have m/z values below the required minimum
                    // Useful for validating that reporter ions can be detected
                    ValidateMS2MzMin();
                }

                msFileReader.Dispose();
                ProgRunner.GarbageCollectNow();

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in ProteoWizardScanner.ProcessFile", ex);
                return false;
            }
        }
    }
}