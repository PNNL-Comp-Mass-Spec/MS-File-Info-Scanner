using System;
using System.IO;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScannerInterfaces;
using PRISM;

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
        /// Load data using the ProteoWizardWrapper
        /// </summary>
        /// <param name="datasetFile"></param>
        /// <param name="datasetFileInfo"></param>
        /// <returns></returns>
        public bool ProcessWithProteoWizard(FileInfo datasetFile, DatasetFileInfo datasetFileInfo)
        {
            try
            {

                var pWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(datasetFile.FullName);

                try
                {
                    var runStartTime = Convert.ToDateTime(pWiz.RunStartTime);

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
                    pWiz,
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

                if (pWiz.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = pWiz.ChromatogramCount;
                }

                if (pWiz.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    var skipExistingScans = (pWiz.ChromatogramCount > 0);
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans,
                                                  skipScansWithNoIons: true,
                                                  maxScansToTrackInDetail: MSFileInfoProcessorBaseClass.MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MSFileInfoProcessorBaseClass.MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = pWiz.SpectrumCount;
                }

                pWiz.Dispose();
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
