using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using MSFileInfoScannerInterfaces;
using PRISM;

// -------------------------------------------------------------------------------
// This program scans a series of MS data files (or data folders) and extracts the acquisition start and end times,
// number of spectra, and the total size of the Results are saved to clsMSFileScanner.DefaultAcquisitionTimeFilename
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Program started in 2005
// Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
//
// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
// Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
// -------------------------------------------------------------------------------

namespace MSFileInfoScanner
{
    static class modMain
    {

        public const string PROGRAM_DATE = "September 28, 2017";

        // This path can contain wildcard characters, e.g. C:\*.raw
        private static string mInputDataFilePath;

        // Optional
        private static string mOutputFolderName;

        // Optional
        private static string mParameterFilePath;

        private static string mLogFilePath;
        private static bool mRecurseFolders;
        private static int mRecurseFoldersMaxLevels;

        private static bool mIgnoreErrorsWhenRecursing;
        private static bool mReprocessingExistingFiles;
        private static bool mReprocessIfCachedSizeIsZero;

        private static bool mUseCacheFiles;
        private static bool mSaveTICandBPIPlots;
        private static bool mSaveLCMS2DPlots;
        private static int mLCMS2DMaxPointsToPlot;
        private static int mLCMS2DOverviewPlotDivisor;

        private static bool mTestLCMSGradientColorSchemes;

        private static bool mCheckCentroidingStatus;
        private static int mScanStart;
        private static int mScanEnd;

        private static bool mShowDebugInfo;

        private static int mDatasetID;
        private static bool mComputeOverallQualityScores;
        private static bool mCreateDatasetInfoFile;

        private static bool mCreateScanStatsFile;
        private static bool mUpdateDatasetStatsTextFile;

        private static string mDatasetStatsTextFileName;
        private static bool mCheckFileIntegrity;
        private static int mMaximumTextFileLinesToCheck;
        private static bool mComputeFileHashes;

        private static bool mZipFileCheckAllData;

        private static bool mPostResultsToDMS;

        private static bool mPlotWithPython;

        private static DateTime mLastProgressTime;

        /// <summary>
        /// Main function
        /// </summary>
        /// <returns>0 if no error, error code if an error</returns>
        /// <remarks>The STAThread attribute is required for OxyPlot functionality</remarks>
        [STAThread]
        public static int Main()
        {

            int intReturnCode;
            var objParseCommandLine = new clsParseCommandLine();

            mInputDataFilePath = string.Empty;
            mOutputFolderName = string.Empty;
            mParameterFilePath = string.Empty;
            mLogFilePath = string.Empty;

            mRecurseFolders = false;
            mRecurseFoldersMaxLevels = 2;
            mIgnoreErrorsWhenRecursing = false;

            mReprocessingExistingFiles = false;
            mReprocessIfCachedSizeIsZero = false;
            mUseCacheFiles = false;

            mSaveTICandBPIPlots = true;
            mSaveLCMS2DPlots = false;
            mLCMS2DMaxPointsToPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT;
            mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR;
            mTestLCMSGradientColorSchemes = false;

            mCheckCentroidingStatus = false;

            mScanStart = 0;
            mScanEnd = 0;
            mShowDebugInfo = false;

            mComputeOverallQualityScores = false;
            mCreateDatasetInfoFile = false;
            mCreateScanStatsFile = false;

            mUpdateDatasetStatsTextFile = false;
            mDatasetStatsTextFileName = string.Empty;

            mCheckFileIntegrity = false;
            mComputeFileHashes = false;
            mZipFileCheckAllData = true;

            mMaximumTextFileLinesToCheck = clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;

            mPostResultsToDMS = false;
            mPlotWithPython = false;

            mLastProgressTime = DateTime.UtcNow;

            try
            {
                var blnProceed = false;
                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        blnProceed = true;
                }

                if (mInputDataFilePath == null)
                    mInputDataFilePath = string.Empty;


                if (!blnProceed || objParseCommandLine.NeedToShowHelp || objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount == 0 || mInputDataFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }

                var scanner = new clsMSFileInfoScanner();

                scanner.DebugEvent += mMSFileScanner_DebugEvent;
                scanner.ErrorEvent += mMSFileScanner_ErrorEvent;
                scanner.WarningEvent += mMSFileScanner_WarningEvent;
                scanner.StatusEvent += mMSFileScanner_MessageEvent;
                scanner.ProgressUpdate += mMSFileScanner_ProgressUpdate;

                if (mCheckFileIntegrity)
                    mUseCacheFiles = true;

                // Note: These values will be overridden if /P was used and they are defined in the parameter file

                scanner.UseCacheFiles = mUseCacheFiles;
                scanner.ReprocessExistingFiles = mReprocessingExistingFiles;
                scanner.ReprocessIfCachedSizeIsZero = mReprocessIfCachedSizeIsZero;

                scanner.PlotWithPython = mPlotWithPython;
                scanner.SaveTICAndBPIPlots = mSaveTICandBPIPlots;
                scanner.SaveLCMS2DPlots = mSaveLCMS2DPlots;
                scanner.LCMS2DPlotMaxPointsToPlot = mLCMS2DMaxPointsToPlot;
                scanner.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;
                scanner.TestLCMSGradientColorSchemes = mTestLCMSGradientColorSchemes;

                scanner.CheckCentroidingStatus = mCheckCentroidingStatus;

                scanner.ScanStart = mScanStart;
                scanner.ScanEnd = mScanEnd;
                scanner.ShowDebugInfo = mShowDebugInfo;

                scanner.ComputeOverallQualityScores = mComputeOverallQualityScores;
                scanner.CreateDatasetInfoFile = mCreateDatasetInfoFile;
                scanner.CreateScanStatsFile = mCreateScanStatsFile;

                scanner.UpdateDatasetStatsTextFile = mUpdateDatasetStatsTextFile;
                scanner.DatasetStatsTextFileName = mDatasetStatsTextFileName;

                scanner.CheckFileIntegrity = mCheckFileIntegrity;
                scanner.MaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck;
                scanner.ComputeFileHashes = mComputeFileHashes;
                scanner.ZipFileCheckAllData = mZipFileCheckAllData;

                scanner.IgnoreErrorsWhenRecursing = mIgnoreErrorsWhenRecursing;

                if (mLogFilePath.Length > 0)
                {
                    scanner.LogMessagesToFile = true;
                    scanner.LogFilePath = mLogFilePath;
                }

                scanner.DatasetIDOverride = mDatasetID;
                scanner.DSInfoDBPostingEnabled = mPostResultsToDMS;

                if (!string.IsNullOrEmpty(mParameterFilePath))
                {
                    scanner.LoadParameterFileSettings(mParameterFilePath);
                }

                if (mRecurseFolders)
                {
                    if (scanner.ProcessMSFilesAndRecurseFolders(mInputDataFilePath, mOutputFolderName, mRecurseFoldersMaxLevels))
                    {
                        intReturnCode = 0;
                    }
                    else
                    {
                        intReturnCode = (int)scanner.ErrorCode;
                    }
                }
                else
                {
                    if (scanner.ProcessMSFileOrFolderWildcard(mInputDataFilePath, mOutputFolderName, true))
                    {
                        intReturnCode = 0;
                    }
                    else
                    {
                        intReturnCode = (int)scanner.ErrorCode;
                        if (intReturnCode != 0)
                        {
                            ShowErrorMessage("Error while processing: " + scanner.GetErrorMessage());
                        }
                    }
                }

                scanner.SaveCachedResults();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in modMain->Main: " + Environment.NewLine + ex.Message);
                intReturnCode = -1;
            }

            return intReturnCode;

        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine parser)
        {
            // Returns True if no problems; otherwise, returns false

            var lstValidParameters = new List<string> {
                "I",
                "O",
                "P",
                "S",
                "IE",
                "L",
                "C",
                "M",
                "H",
                "QZ",
                "NoTIC",
                "LC",
                "LCDiv",
                "LCGrad",
                "CC",
                "QS",
                "ScanStart",
                "ScanEnd",
                "DatasetID",
                "DI",
                "DST",
                "SS",
                "CF",
                "R",
                "Z",
                "PostToDMS",
                "Debug",
                "Python",
                "PythonPlot"
            };

            try
            {
                // Make sure no invalid parameters are present
                if (parser.InvalidParametersPresent(lstValidParameters))
                {
                    ShowErrorMessage("Invalid commmand line parameters",
                        (from item in parser.InvalidParameters(lstValidParameters) select "/" + item).ToList());
                    return false;
                }

                int value;

                // Query parser to see if various parameters are present
                if (parser.RetrieveValueForParameter("I", out var strValue))
                {
                    mInputDataFilePath = strValue;
                }
                else if (parser.NonSwitchParameterCount > 0)
                {
                    // Treat the first non-switch parameter as the input file
                    mInputDataFilePath = parser.RetrieveNonSwitchParameter(0);
                }

                if (parser.RetrieveValueForParameter("O", out strValue))
                    mOutputFolderName = strValue;
                if (parser.RetrieveValueForParameter("P", out strValue))
                    mParameterFilePath = strValue;

                if (parser.RetrieveValueForParameter("S", out strValue))
                {
                    mRecurseFolders = true;
                    if (int.TryParse(strValue, out value))
                    {
                        mRecurseFoldersMaxLevels = value;
                    }
                }
                if (parser.RetrieveValueForParameter("IE", out strValue))
                    mIgnoreErrorsWhenRecursing = true;

                if (parser.RetrieveValueForParameter("L", out strValue))
                    mLogFilePath = strValue;

                if (parser.IsParameterPresent("C"))
                    mCheckFileIntegrity = true;
                if (parser.RetrieveValueForParameter("M", out strValue))
                {
                    if (int.TryParse(strValue, out value))
                    {
                        mMaximumTextFileLinesToCheck = value;
                    }
                }

                if (parser.IsParameterPresent("H"))
                    mComputeFileHashes = true;
                if (parser.IsParameterPresent("QZ"))
                    mZipFileCheckAllData = false;

                if (parser.IsParameterPresent("NoTIC"))
                    mSaveTICandBPIPlots = false;

                if (parser.RetrieveValueForParameter("LC", out strValue))
                {
                    mSaveLCMS2DPlots = true;
                    if (int.TryParse(strValue, out value))
                    {
                        mLCMS2DMaxPointsToPlot = value;
                    }
                }

                if (parser.RetrieveValueForParameter("LCDiv", out strValue))
                {
                    if (int.TryParse(strValue, out value))
                    {
                        mLCMS2DOverviewPlotDivisor = value;
                    }
                }

                if (parser.IsParameterPresent("LCGrad"))
                    mTestLCMSGradientColorSchemes = true;

                if (parser.IsParameterPresent("CC"))
                    mCheckCentroidingStatus = true;

                if (parser.RetrieveValueForParameter("ScanStart", out strValue))
                {
                    if (int.TryParse(strValue, out value))
                    {
                        mScanStart = value;
                    }
                }

                if (parser.RetrieveValueForParameter("ScanEnd", out strValue))
                {
                    if (int.TryParse(strValue, out value))
                    {
                        mScanEnd = value;
                    }
                }

                if (parser.IsParameterPresent("Debug"))
                    mShowDebugInfo = true;

                if (parser.IsParameterPresent("QS"))
                    mComputeOverallQualityScores = true;

                if (parser.RetrieveValueForParameter("DatasetID", out strValue))
                {
                    if (!int.TryParse(strValue, out mDatasetID))
                    {
                        ShowErrorMessage("DatasetID is not an integer");
                        return false;
                    }
                }

                if (parser.IsParameterPresent("DI"))
                    mCreateDatasetInfoFile = true;

                if (parser.IsParameterPresent("SS"))
                    mCreateScanStatsFile = true;

                if (parser.RetrieveValueForParameter("DST", out strValue))
                {
                    mUpdateDatasetStatsTextFile = true;
                    if (!string.IsNullOrEmpty(strValue))
                    {
                        mDatasetStatsTextFileName = strValue;
                    }
                }

                if (parser.IsParameterPresent("CF"))
                    mUseCacheFiles = true;
                if (parser.IsParameterPresent("R"))
                    mReprocessingExistingFiles = true;
                if (parser.IsParameterPresent("Z"))
                    mReprocessIfCachedSizeIsZero = true;

                if (parser.IsParameterPresent("PostToDMS"))
                    mPostResultsToDMS = true;

                if (parser.IsParameterPresent("PythonPlot") || parser.IsParameterPresent("Python"))
                    mPlotWithPython = true;

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message);
                return false;
            }

        }

        private static string CollapseList(List<string> lstList)
        {
            return string.Join(", ", lstList);
        }

        private static void ShowErrorMessage(string message)
        {
            ConsoleMsgUtils.ShowError(message);
        }

        private static void ShowErrorMessage(string strTitle, IEnumerable<string> items)
        {
            const string strSeparator = "------------------------------------------------------------------------------";

            Console.WriteLine();
            Console.WriteLine(strSeparator);
            ConsoleMsgUtils.ShowError(strTitle, null, false, false);
            var strMessage = strTitle + ":";

            foreach (var item in items)
            {
                ConsoleMsgUtils.ShowError("   " + item, null, false, false);
                strMessage += " " + item;
            }
            Console.WriteLine(strSeparator);
            Console.WriteLine();

            WriteToErrorStream(strMessage);
        }

        private static void ShowProgramHelp()
        {
            try
            {
                var scanner = new clsMSFileInfoScanner();

                Console.WriteLine("This program will scan a series of MS data files (or data folders) and " + "extract the acquisition start and end times, number of spectra, and the " + "total size of the data, saving the values in the file " + clsMSFileInfoScanner.DefaultAcquisitionTimeFilename + ". " + "Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), " + "Agilent or QStar/QTrap .WIFF files, Masslynx .Raw folders, Bruker 1 folders, " + "Bruker XMass analysis.baf files, .UIMF files (IMS), " + "zipped Bruker imaging datasets (with 0_R*.zip files), and " + "DeconTools _isos.csv files");

                Console.WriteLine();

                Console.WriteLine("Program syntax:" + Environment.NewLine + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location));
                Console.WriteLine(" /I:InputFileNameOrFolderPath [/O:OutputFolderName]");
                Console.WriteLine(" [/P:ParamFilePath] [/S[:MaxLevel]] [/IE] [/L:LogFilePath]");
                Console.WriteLine(" [/LC[:MaxPointsToPlot]] [/NoTIC] [/LCGrad]");
                Console.WriteLine(" [/DI] [/SS] [/QS] [/CC]");
                Console.WriteLine(" [/DST:DatasetStatsFileName]");
                Console.WriteLine(" [/ScanStart:0] [/ScanEnd:0] [/Debug]");
                Console.WriteLine(" [/C] [/M:nnn] [/H] [/QZ]");
                Console.WriteLine(" [/CF] [/R] [/Z]");
                Console.WriteLine(" [/PostToDMS] [/PythonPlot]");
                Console.WriteLine();
                Console.WriteLine("Use /I to specify the name of a file or folder to scan; the path can contain the wildcard character *");
                Console.WriteLine("The output folder name is optional.  If omitted, the output files will be created in the program directory.");
                Console.WriteLine();

                Console.WriteLine("The param file switch is optional.  If supplied, it should point to a valid XML parameter file.  If omitted, defaults are used.");
                Console.WriteLine("Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. Use /IE to ignore errors when recursing.");
                Console.WriteLine("Use /L to specify the file path for logging messages.");
                Console.WriteLine();

                Console.WriteLine("Use /LC to create 2D LCMS plots (this process could take several minutes for each dataset).  By default, plots the top " + clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT + " points.  To plot the top 20000 points, use /LC:20000.");
                Console.WriteLine("Use /LCDiv to specify the divisor to use when creating the overview 2D LCMS plots.  By default, uses /LCDiv:" + clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR + "; use /LCDiv:0 to disable creation of the overview plots.");
                Console.WriteLine("Use /NoTIC to not save TIC and BPI plots.");
                Console.WriteLine("Use /LCGrad to save a series of 2D LC plots, each using a different color scheme.  The default color scheme is OxyPalettes.Jet");
                Console.WriteLine();
                Console.WriteLine("Use /DatasetID:# to define the dataset's DatasetID value (where # is an integer); only appropriate if processing a single dataset");
                Console.WriteLine("Use /DI to create a dataset info XML file for each dataset.");
                Console.WriteLine();
                Console.WriteLine("Use /SS to create a _ScanStats.txt  file for each dataset.");
                Console.WriteLine("Use /QS to compute an overall quality score for the data in each datasets.");
                Console.WriteLine("Use /CC to check spectral data for whether it is centroided or profile");
                Console.WriteLine();

                Console.WriteLine("Use /DST to update (or create) a tab-delimited text file with overview stats for the dataset. " +
                                  "If /DI is used, will include detailed scan counts; otherwise, will just have the dataset name, " +
                                  "acquisition date, and (if available) sample name and comment. " +
                                  "By default, the file is named " + clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME + "; " +
                                  "to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt");
                Console.WriteLine();

                Console.WriteLine("Use /ScanStart and /ScanEnd to limit the scan range to process; useful for files where the first few scans are corrupt. " +
                                  "For example, to start processing at scan 10, use /ScanStart:10");
                Console.WriteLine("Use /Debug to display debug information at the console, including showing the scan number prior to reading each scan's data");
                Console.WriteLine();

                Console.WriteLine("Use /C to perform an integrity check on all known file types; this process will open known file types and " +
                                  "verify that they contain the expected   This option is only used if you specify an Input Folder and use a wildcard; you will typically also want to use /S when using /C.");
                Console.WriteLine(("Use /M to define the maximum number of lines to process when checking text or csv files;" +
                                           " default is /M:" + clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK));
                Console.WriteLine();

                Console.WriteLine("Use /H to compute Sha-1 file hashes when verifying file integrity.");
                Console.WriteLine("Use /QZ to run a quick zip-file validation test when verifying file integrity (the test does not check all data in the .Zip file).");
                Console.WriteLine();

                Console.WriteLine("Use /CF to save/load information from the acquisition time file (cache file).  This option is auto-enabled if you use /C.");
                Console.WriteLine("Use /R to reprocess files that are already defined in the acquisition time file.");
                Console.WriteLine("Use /Z to reprocess files that are already defined in the acquisition time file only if their cached size is 0 bytes.");
                Console.WriteLine();
                Console.WriteLine("Use /PostToDMS to store the dataset info in the DMS database.  To customize the server name and/or stored procedure to use for posting, use an XML parameter file with settings DSInfoConnectionString, DSInfoDBPostingEnabled, and DSInfoStoredProcedure");
                Console.WriteLine();
                Console.WriteLine("Use /PythonPlot to create plots with Python instead of OxyPlot");
                Console.WriteLine();
                Console.WriteLine("Known file extensions: " + CollapseList(scanner.GetKnownFileExtensionsList()));
                Console.WriteLine("Known folder extensions: " + CollapseList(scanner.GetKnownFolderExtensionsList()));
                Console.WriteLine();

                Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com");
                Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message);
            }

        }

        private static void WriteToErrorStream(string strErrorMessage)
        {
            try
            {
                using (var swErrorStream = new StreamWriter(Console.OpenStandardError()))
                {
                    swErrorStream.WriteLine(strErrorMessage);
                }
            }
            catch (Exception)
            {
                // Ignore errors here
            }
        }

        private static void mMSFileScanner_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private static void mMSFileScanner_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex, false);
        }

        private static void mMSFileScanner_MessageEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void mMSFileScanner_ProgressUpdate(string progressMessage, float percentComplete)
        {
            if (DateTime.UtcNow.Subtract(mLastProgressTime).TotalSeconds < 5)
                return;

            Console.WriteLine();
            mLastProgressTime = DateTime.UtcNow;
            mMSFileScanner_DebugEvent(percentComplete.ToString("0.0") + "%, " + progressMessage);
        }

        private static void mMSFileScanner_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

    }
}
