using System;
using System.IO;
using MSFileInfoScanner;
using NUnit.Framework;
using PRISM;
using pwiz.CLI.msdata;

// ReSharper disable StringLiteralTypo
namespace MSFileInfoScannerUnitTests
{
    [TestFixture]
    class ProteoWizardWrapperTests
    {

        [Test]
        [TestCase("Angiotensin_325-ETD.raw", false)]
        [TestCase("Angiotensin_325-ETciD-15.raw", false)]
        [TestCase("Angiotensin_325-HCD.raw", false)]
        [TestCase("Angiotensin_325-CID.raw", false)]
        [TestCase("PPS20190130US1-1_1030TRANCID35.raw", true)]
        public void TestPWiz(string fileOrDirectoryName, bool isDirectory)
        {
            const bool RUN_BENCHMARKS = false;

            try
            {
                if (!FindInstrumentData(fileOrDirectoryName, isDirectory, out var datasetPath))
                {
                    Assert.Fail("File or directory not found");
                    return;
                }

                var pWiz2 = new MSDataFile(datasetPath);

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
