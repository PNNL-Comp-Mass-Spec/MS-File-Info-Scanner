using System;
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
        /// Class MzMLFileInfoScanner sets this to true
        /// </summary>
        public bool InputFileIsMzML { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Processing options</param>
        /// <param name="lcms2DPlotOptions">Plotting options</param>
        protected ProteoWizardScanner(InfoScannerOptions options, LCMSDataPlotterOptions lcms2DPlotOptions) : base(options, lcms2DPlotOptions)
        {
            if (options.SaveTICAndBPIPlots)
            {
                // Initialize the TIC and BPI arrays
                InitializeTICAndBPI();
            }

            if (options.SaveLCMS2DPlots)
            {
                InitializeLCMS2DPlot();
            }
        }

        /// <summary>
        /// This method is used to determine one or more overall quality scores
        /// </summary>
        /// <param name="msDataFileReader">MS data file reader</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        private void ComputeQualityScores(
            MSDataFileReader msDataFileReader,
            DatasetFileInfo datasetFileInfo)
        {
            float overallScore;

            double overallAvgIntensitySum = 0;
            var overallAvgCount = 0;

            if (mLCMS2DPlot.ScanCountCached > 0)
            {
                // Obtain the overall average intensity value using the data cached in mLCMS2DPlot
                // This avoids having to reload all the data using msDataFileReader
                const int msLevelFilter = 1;
                overallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(msLevelFilter);
            }
            else
            {
                var scanCount = msDataFileReader.SpectrumCount;
                GetStartAndEndScans(scanCount, out var scanStart, out var scanEnd);

                var scanNumberToIndexMap = msDataFileReader.GetScanToIndexMapping();

                for (var scanNumber = scanStart; scanNumber <= scanEnd; scanNumber++)
                {
                    if (!scanNumberToIndexMap.TryGetValue(scanNumber, out var scanIndex))
                    {
                        continue;
                    }

                    var spectrum = msDataFileReader.GetSpectrum(scanIndex, true);

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
        /// <param name="datasetFileOrDirectory">Dataset file or directory</param>
        /// <param name="datasetFileInfo">Instance of DatasetFileInfo</param>
        /// <param name="unknownCompressorIdIsWarning">When true, if error "unknown compressor id" occurs, report it as a warning instead of an error</param>
        /// <returns>True if successful, false if an error</returns>
        public bool ProcessWithProteoWizard(
            FileSystemInfo datasetFileOrDirectory,
            DatasetFileInfo datasetFileInfo,
            bool unknownCompressorIdIsWarning = false)
        {
            try
            {
                var msDataFileReader = new MSDataFileReader(datasetFileOrDirectory.FullName);

                try
                {
                    var runStartTime = GetRunStartTime(msDataFileReader);

                    // Possibly update AcqTimeStart
                    // In particular, if reading a .mzML file, AcqTimeStart and AcqTimeEnd will initially be set to the modification time of the .mzML file

                    if (runStartTime < datasetFileInfo.AcqTimeEnd && datasetFileInfo.AcqTimeEnd.Subtract(runStartTime).TotalDays < 1 ||
                        InputFileIsMzML)
                    {
                        UpdateAcqStartAndEndTimes(datasetFileInfo, msDataFileReader, runStartTime);
                    }
                }
                catch (Exception)
                {
                    datasetFileInfo.AcqTimeStart = datasetFileInfo.AcqTimeEnd;
                }

                // Instantiate the ProteoWizard Data Parser class
                // For Thermo .raw files, uses the FilterText value to determine if spectra are high res or low res MS1 and MS2
                // For other instrument types, assumes spectra are high res MS1 and MS2
                var pWizParser = new ProteoWizardDataParser(
                    msDataFileReader,
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

                if (msDataFileReader.ChromatogramCount > 0)
                {
                    // Process the chromatograms
                    pWizParser.StoreChromatogramInfo(datasetFileInfo, out ticStored, out srmDataCached, out runtimeMinutes);
                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = msDataFileReader.ChromatogramCount;
                }

                if (msDataFileReader.SpectrumCount > 0 && !srmDataCached)
                {
                    // Process the spectral data (though only if we did not process SRM data)
                    var skipExistingScans = (msDataFileReader.ChromatogramCount > 0);
                    pWizParser.StoreMSSpectraInfo(ticStored, ref runtimeMinutes,
                                                  skipExistingScans,
                                                  skipScansWithNoIons: true,
                                                  maxScansToTrackInDetail: MSFileInfoProcessorBaseClass.MAX_SCANS_TO_TRACK_IN_DETAIL,
                                                  maxScansForTicAndBpi: MSFileInfoProcessorBaseClass.MAX_SCANS_FOR_TIC_AND_BPI);

                    pWizParser.PossiblyUpdateAcqTimeStart(datasetFileInfo, runtimeMinutes);

                    datasetFileInfo.ScanCount = msDataFileReader.SpectrumCount;
                }

                if (Options.ComputeOverallQualityScores)
                {
                    // Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(msDataFileReader, datasetFileInfo);
                }

                if (Options.MS2MzMin > 0 && datasetFileInfo.ScanCount > 0)
                {
                    // Verify that each MS2 spectrum has m/z values below the required minimum
                    // Useful for validating that reporter ions can be detected
                    ValidateMS2MzMin();
                }

                msDataFileReader.Dispose();
                AppUtils.GarbageCollectNow();

                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.StartsWith("unknown compressor id") && unknownCompressorIdIsWarning)
                {
                    OnWarningEvent("Error in ProteoWizardScanner.ProcessFile: {0}", ex.Message);
                }
                else
                {
                    OnErrorEvent("Error in ProteoWizardScanner.ProcessFile", ex);
                }

                return false;
            }
        }
    }
}
