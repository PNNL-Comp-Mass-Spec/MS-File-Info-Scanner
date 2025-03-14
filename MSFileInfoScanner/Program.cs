﻿using System;
using System.Collections.Generic;
using System.IO;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    /// <summary>
    /// <para>
    /// This program scans a series of MS data files (or data directories) and extracts the acquisition start and end times,
    /// number of spectra, and the total size of the Results are saved to MSFileInfoScanner.DefaultAcquisitionTimeFilename
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started in 2005
    /// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics
    /// </para>
    /// </remarks>
    public static class Program
    {
        // Ignore Spelling: Bruker, Conf, nnn, OxyPlot

        private static DateTime mLastProgressTime;

        /// <summary>
        /// Main method
        /// </summary>
        /// <remarks>The STAThread attribute is required for OxyPlot functionality</remarks>
        /// <returns>0 if no error, error code if an error</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            var exeName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
            var exePath = AppUtils.GetAppPath();

            var parser = new CommandLineParser<InfoScannerOptions>(exeName, GetAppVersion());

            var scannerInfo = new MSFileInfoScanner();
            parser.ProgramInfo = "This program will scan a series of MS data files (or data directories) and " +
                                 "extract the acquisition start and end times, number of spectra, and the " +
                                 "total size of the data, saving the values in the file " +
                                 MSFileInfoScanner.DefaultAcquisitionTimeFilename + ". " +
                                 "Supported file types are Thermo .RAW files, Agilent Ion Trap (.D directories), " +
                                 "Agilent or QStar/QTrap .WIFF files, MassLynx .Raw directories, Bruker 1 directories, " +
                                 "Bruker XMass analysis.baf files, .UIMF files (IMS), " +
                                 "zipped Bruker imaging datasets (with 0_R*.zip files), and " +
                                 "DeconTools _isos.csv files" + Environment.NewLine + Environment.NewLine +
                                 "Known file extensions: " + CollapseList(scannerInfo.GetKnownFileExtensionsList()) + Environment.NewLine +
                                 "Known directory extensions: " + CollapseList(scannerInfo.GetKnownDirectoryExtensionsList());
            parser.ContactInfo = "Program written by Matthew Monroe for PNNL (Richland, WA)" + Environment.NewLine +
                                 "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                                 "Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://www.pnnl.gov/integrative-omics";

            parser.UsageExamples.Add("Program syntax:" + Environment.NewLine + Path.GetFileName(exePath) + "\n" +
                                            " /I:InputFileNameOrDirectoryPath [/O:OutputDirectoryPath]\n" +
                                            " [/P:ParameterFilePath] [/S[:MaxLevel]]\n" +
                                            " [/IE] [/L:LogFilePath]\n" +
                                            " [/LC[:MaxPointsToPlot]] [/TIC] [/LCGrad]\n" +
                                            " [/DI] [/SS] [/QS] [/CC]\n" +
                                            " [/MS2MzMin:MzValue] [/NoHash]\n" +
                                            " [/DST:DatasetStatsFileName]\n" +
                                            " [/ScanStart:0] [/ScanEnd:0] [/Debug]\n" +
                                            " [/C] [/M:nnn] [/H] [/QZ]\n" +
                                            " [/CF] [/R] [/Z]\n" +
                                            " [/PythonPlot]\n" +
                                            " [/Conf:KeyValueParamFilePath] [/CreateParamFile]");

            // The default argument name for parameter files is /ParamFile or -ParamFile
            // Also allow /Conf or /P
            parser.AddParamFileKey("Conf");
            parser.AddParamFileKey("P");

            var result = parser.ParseArgs(args);
            var options = result.ParsedResults;

            if (!result.Success || !options.Validate())
            {
                if (parser.CreateParamFileProvided)
                {
                    return 0;
                }

                // Delay for 750 msec in case the user double-clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);
                return -1;
            }

            mLastProgressTime = DateTime.UtcNow;

            try
            {
                if (InvalidParameterFile(parser.ParameterFilePath))
                    return -1;

                var scanner = new MSFileInfoScanner(options);

                scanner.DebugEvent += MSFileScanner_DebugEvent;
                scanner.ErrorEvent += MSFileScanner_ErrorEvent;
                scanner.WarningEvent += MSFileScanner_WarningEvent;
                scanner.StatusEvent += MSFileScanner_MessageEvent;
                scanner.ProgressUpdate += MSFileScanner_ProgressUpdate;

                if (!string.IsNullOrEmpty(options.ParameterFilePath))
                {
                    scanner.LoadParameterFileSettings(options.ParameterFilePath);
                }

                scanner.ShowCurrentProcessingOptions();

                bool processingError;

                int returnCode;

                if (options.RecurseDirectories)
                {
                    if (scanner.ProcessMSFilesAndRecurseDirectories(options.InputDataFilePath, options.OutputDirectoryPath, options.MaxLevelsToRecurse))
                    {
                        returnCode = 0;
                        processingError = false;
                    }
                    else
                    {
                        returnCode = (int)scanner.ErrorCode;
                        processingError = true;
                    }
                }
                else
                {
                    if (scanner.ProcessMSFileOrDirectoryWildcard(options.InputDataFilePath, options.OutputDirectoryPath, true))
                    {
                        returnCode = 0;
                        processingError = false;
                    }
                    else
                    {
                        returnCode = (int)scanner.ErrorCode;
                        processingError = true;
                    }
                }

                if (processingError)
                {
                    if (returnCode != 0)
                    {
                        ShowErrorMessage("Error while processing: " + scanner.GetErrorMessage());
                    }
                    else
                    {
                        ShowErrorMessage("Unknown error while processing (ProcessMSFileOrDirectoryWildcard returned false but the ErrorCode is 0)");
                    }

                    System.Threading.Thread.Sleep(1500);
                }
                else if (scanner.ErrorCode == iMSFileInfoScanner.MSFileScannerErrorCodes.MS2MzMinValidationWarning)
                {
                    ConsoleMsgUtils.ShowWarning("MS2MzMin validation warning: " + scanner.MS2MzMinValidationMessage);
                }

                scanner.SaveCachedResults();

                return returnCode;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in Program->Main", ex);
                return -1;
            }
        }

        private static bool InvalidParameterFile(string parameterFilePath)
        {
            if (string.IsNullOrWhiteSpace(parameterFilePath))
                return false;

            // Assure that the user did not provide an old-style XML-based parameter file
            var paramFile = MSFileInfoScanner.GetFileInfo(parameterFilePath);

            if (!paramFile.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                return false;

            using var paramFileReader = new StreamReader(new FileStream(paramFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

            var linesRead = 0;

            while (!paramFileReader.EndOfStream && linesRead < 5)
            {
                var dataLine = paramFileReader.ReadLine();

                if (string.IsNullOrWhiteSpace(dataLine))
                    continue;

                linesRead++;

                var trimmedLine = dataLine.Trim();

                if (trimmedLine.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("<sections", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleMsgUtils.ShowWarning(
                        "MSFileInfoScanner v2.x uses Key=Value parameter files\n" +
                        "{0} is an XML file\n" +
                        "For example parameter files, see {1}, including\n  {2},\n  {3}, and\n  {4}",
                        paramFile.Name,
                        "https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/tree/master/docs",
                        "MSFileInfoScanner_ProcessingOptions_AllPlots.txt",
                        "MSFileInfoScanner_ProcessingOptions_AllPlots_ValidateTMT.txt",
                        "MSFileInfoScanner_ProcessingOptions_NoPlots.txt");
                    ConsoleMsgUtils.ShowWarning("Aborting");
                    return true;
                }
            }

            return false;
        }

        private static string GetAppVersion()
        {
            return AppUtils.GetAppVersion(MSFileInfoScanner.PROGRAM_DATE);
        }

        private static string CollapseList(IEnumerable<string> itemList)
        {
            return string.Join(", ", itemList);
        }

        private static void ShowErrorMessage(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void MSFileScanner_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void MSFileScanner_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowErrorCustom(message, ex, false);
        }

        private static void MSFileScanner_MessageEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void MSFileScanner_ProgressUpdate(string progressMessage, float percentComplete)
        {
            if (DateTime.UtcNow.Subtract(mLastProgressTime).TotalSeconds < 5)
                return;

            Console.WriteLine();
            mLastProgressTime = DateTime.UtcNow;
            MSFileScanner_DebugEvent(percentComplete.ToString("0.0") + "%, " + progressMessage);
        }

        private static void MSFileScanner_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }
    }
}
