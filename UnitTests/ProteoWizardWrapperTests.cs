using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSFileInfoScanner;
using NUnit.Framework;
using PRISM;

// ReSharper disable StringLiteralTypo
namespace MSFileInfoScannerUnitTests
{
    /// <summary>
    /// Test reading data with ProteoWizard
    /// </summary>
    /// <remarks>
    /// SetUpFixture ProteoWizardSetup looks for ProteoWizard prior to this class loading
    /// </remarks>
    [TestFixture]
    class ProteoWizardWrapperTests
    {

        [Test]
        [TestCase("Angiotensin_325-CID.raw", false, 10, 10, 26390, 1.4326E+008, 4.2483E+006)]
        [TestCase("Angiotensin_325-ETD.raw", false, 10, 10, 31505, 1.4025E+008, 2.6627E+006)]
        [TestCase("Angiotensin_325-ETciD-15.raw", false, 10, 10, 30883, 1.6188E+008, 3.0563E+006)]
        [TestCase("Angiotensin_325-HCD.raw", false, 10, 10, 36158, 8.3286E+007, 2.2395E+006)]
        [TestCase("Angiotensin_AllScans.raw", false, 1775, 51, 25782, 1.0861E+009, 1.2757E+008)]
        [TestCase("PPS20190130US1-1_1030TRANCID35.raw", true, 11600, 71, 241, 37.085, 6.8873)]
        public void TestPWiz(
            string fileOrDirectoryName,
            bool isDirectory,
            int expectedSpectraInFile,
            int expectedSpectraLoaded,
            int expectedTotalDataPoints,
            double expectedMedianTIC,
            double expectedMedianBPI)
        {
            try
            {
                if (!FindInstrumentData(fileOrDirectoryName, isDirectory, out var datasetPath))
                {
                    Assert.Fail("File or directory not found");
                    return;
                }

                var pWiz = new pwiz.ProteowizardWrapper.MSDataFileReader(datasetPath);


                Console.WriteLine("Chromatogram count: " + pWiz.ChromatogramCount);
                Console.WriteLine();

                var ticIntensities = new Dictionary<int, float>();

                for (var chromatogramIndex = 0; chromatogramIndex < pWiz.ChromatogramCount; chromatogramIndex++)
                {
                    // Note that even for a small .Wiff file (1.5 MB), obtaining the Chromatogram list will take some time (20 to 60 seconds)
                    // The chromatogram at index 0 should be the TIC
                    // The chromatogram at index >=1 will be each SRM

                    pWiz.GetChromatogram(chromatogramIndex, out var chromatogramID, out var timeArray, out var intensityArray);

                    // Determine the chromatogram type

                    if (chromatogramID == null)
                        chromatogramID = string.Empty;

                    var cvParams = pWiz.GetChromatogramCVParams(chromatogramIndex);

                    if (clsProteoWizardDataParser.TryGetCVParam(cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, out _))
                    {
                        // This chromatogram is the TIC
                        Console.WriteLine("TIC has id {0} and {1} data points", chromatogramID, timeArray.Length);

                        for (var i = 0; i < intensityArray.Length; i++)
                        {
                            ticIntensities.Add(i, intensityArray[i]);
                        }
                    }

                    if (clsProteoWizardDataParser.TryGetCVParam(cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, out _))
                    {
                        // This chromatogram is an SRM scan
                        Console.WriteLine("SRM scan has id {0} and {1} data points", chromatogramID, timeArray.Length);
                    }

                }

                Console.WriteLine("Spectrum count: " + pWiz.SpectrumCount);

                var spectraLoaded = 0;
                long totalPointsRead = 0;
                double ticSumAllSpectra = 0;
                double bpiSumAllSpectra = 0;

                var spectrumIndex = 0;
                while (spectrumIndex < pWiz.SpectrumCount)
                {
                    var spectrum = pWiz.GetSpectrum(spectrumIndex, getBinaryData: true);
                    spectraLoaded += 1;

                    Console.WriteLine();
                    Console.WriteLine("ScanIndex {0}, NativeId {1}, Elution Time {2:F2} minutes, MS Level {3}",
                                      spectrumIndex, spectrum.NativeId, spectrum.RetentionTime, spectrum.Level);

                    // Use the following to get the MZs and Intensities
                    var mzList = spectrum.Mzs.ToList();
                    var intensities = spectrum.Intensities.ToList();

                    if (mzList.Count > 0)
                    {

                        Console.WriteLine("  Data count: " + mzList.Count);

                        totalPointsRead += mzList.Count;

                        double tic = 0;
                        double bpi = 0;
                        for (var index = 0; index <= mzList.Count - 1; index++)
                        {
                            tic += intensities[index];
                            if (intensities[index] > bpi)
                                bpi = intensities[index];
                        }

                        ticSumAllSpectra += tic;
                        bpiSumAllSpectra += bpi;

                        if (!ticIntensities.TryGetValue(spectrumIndex, out var ticFromChromatogram))
                        {
                            ticFromChromatogram = -1;
                        }

                        var spectrumInfo = pWiz.GetSpectrumObject(spectrumIndex);

                        if (clsProteoWizardDataParser.TryGetCVParamDouble(spectrumInfo.cvParams, pwiz.CLI.cv.CVID.MS_total_ion_current,
                                                                          out var ticFromSpectrumObject))
                        {
                            if (ticFromChromatogram < 0)
                            {
                                // ticIntensities did not have an entry for spectrumIndex
                                // This could be the case on a Waters Synapt instrument, where ticIntensities has one TIC per frame,
                                // while pWiz.GetSpectrum() returns individual mass spectra, of which there could be hundreds of spectra per frame
                                Console.WriteLine("  TIC from actual data is {0:E2} vs. {1:E2} from the spectrum object",
                                                  tic, ticFromSpectrumObject);
                            }
                            else
                            {

                                // Note: the TIC value from the CvParams has been seen to be drastically off from the manually computed value
                                Console.WriteLine("  TIC from actual data is {0:E2} vs. {1:E2} from the chromatogram and {2:E2} from the spectrum object",
                                                  tic, ticFromChromatogram, ticFromSpectrumObject);
                            }
                        }

                        if (clsProteoWizardDataParser.TryGetCVParamDouble(spectrumInfo.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_intensity,
                                                                          out var bpiFromSpectrumObject))
                        {
                            if (clsProteoWizardDataParser.TryGetCVParamDouble(spectrumInfo.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_m_z,
                                                                              out var bpiMzFromSpectrumObject))
                            {
                                // Note: the BPI intensity from the CvParams has been seen to be drastically off from the manually computed value
                                Console.WriteLine("  BPI from spectrum object is {0:E2} at {1:F3} m/z",
                                                  bpiFromSpectrumObject, bpiMzFromSpectrumObject);
                            }
                        }
                    }

                    if (spectrumIndex < 25)
                    {
                        spectrumIndex += 1;
                    }
                    else if (spectrumIndex < 1250)
                    {
                        spectrumIndex += 50;
                    }
                    else
                    {
                        spectrumIndex += 500;
                    }

                }

                if (spectraLoaded > 0)
                {
                    var medianTIC = ticSumAllSpectra / spectraLoaded;
                    var medianBPI = bpiSumAllSpectra / spectraLoaded;

                    Console.WriteLine();
                    Console.WriteLine("Read {0:N0} data points from {1} spectra in {2}",
                                      totalPointsRead, spectraLoaded, Path.GetFileName(fileOrDirectoryName));

                    Console.WriteLine("Median TIC: {0:E4}", medianTIC);
                    Console.WriteLine("Median BPI: {0:E4}", medianBPI);

                    Assert.AreEqual(expectedSpectraInFile, pWiz.SpectrumCount, "Total spectrum count mismatch");

                    Assert.AreEqual(expectedSpectraLoaded, spectraLoaded, "Spectra loaded mismatch");

                    Assert.AreEqual(expectedTotalDataPoints, totalPointsRead, "Spectra loaded mismatch");
                    var ticComparisonTolerance = expectedMedianTIC * 0.01;
                    var bpiComparisonTolerance = expectedMedianBPI * 0.01;

                    Assert.AreEqual(expectedMedianTIC, medianTIC, ticComparisonTolerance, "Median TIC mismatch");
                    Assert.AreEqual(expectedMedianBPI, medianBPI, bpiComparisonTolerance, "Median BPI mismatch");
                }

            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error using ProteoWizard reader", ex);
            }
        }

        /// <summary>
        /// Look for the file in a directory named Data, in a parent directory to the work directory
        /// If not found, look on Proto-2
        /// </summary>
        /// <param name="fileOrDirectoryToFind"></param>
        /// <param name="isDirectory"></param>
        /// <param name="fileOrDirectoryPath"></param>
        /// <returns></returns>
        private bool FindInstrumentData(string fileOrDirectoryToFind, bool isDirectory, out string fileOrDirectoryPath)
        {
            const string UNIT_TEST_SHARE_PATH = @"\\proto-2\UnitTest_Files\MSFileInfoScanner";

            string datasetType;
            if (isDirectory)
                datasetType = "directory";
            else
                datasetType = "file";

            try
            {
                var startingDirectory = new DirectoryInfo(".");
                var directoryToCheck = new DirectoryInfo(".");

                while (true)
                {
                    var matchingDirectories = directoryToCheck.GetDirectories("Data");
                    if (matchingDirectories.Length > 0)
                    {
                        if (FindInstrumentData(fileOrDirectoryToFind, isDirectory, matchingDirectories[0], out fileOrDirectoryPath))
                            return true;

                        break;
                    }

                    if (directoryToCheck.Parent == null)
                    {
                        break;
                    }

                    directoryToCheck = directoryToCheck.Parent;
                }

                // Look in the unit test share
                var remoteShare = new DirectoryInfo(UNIT_TEST_SHARE_PATH);
                if (remoteShare.Exists)
                {
                    if (FindInstrumentData(fileOrDirectoryToFind, isDirectory, remoteShare, out fileOrDirectoryPath))
                        return true;
                }

                ConsoleMsgUtils.ShowWarning("Could not find {0} {1} in a Data directory above {2}", datasetType, fileOrDirectoryToFind, startingDirectory.FullName);

                fileOrDirectoryPath = string.Empty;
                return false;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError(ex, "Error looking for {0} {1}", datasetType, fileOrDirectoryToFind);

                fileOrDirectoryPath = string.Empty;
                return false;
            }
        }

        private bool FindInstrumentData(string fileOrDirectoryName, bool isDirectory, DirectoryInfo directoryToCheck, out string fileOrDirectoryPath)
        {

            if (isDirectory)
            {
                var matchingDatasetDirectories = directoryToCheck.GetDirectories(fileOrDirectoryName);
                if (matchingDatasetDirectories.Length > 0)
                {
                    fileOrDirectoryPath = matchingDatasetDirectories[0].FullName;
                    return true;
                }
            }
            else
            {
                var matchingDatasetFiles = directoryToCheck.GetFiles(fileOrDirectoryName);
                if (matchingDatasetFiles.Length > 0)
                {
                    fileOrDirectoryPath = matchingDatasetFiles[0].FullName;
                    return true;
                }
            }

            fileOrDirectoryPath = string.Empty;
            return false;
        }

    }
}
