using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

using MSFileInfoScannerInterfaces;
// Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005

// See clsMSFileInfoScanner for a program description

static class modMain
{


	public const string PROGRAM_DATE = "April 29, 2016";
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
	private static clsMSFileInfoScanner withEventsField_mMSFileScanner;
	private static clsMSFileInfoScanner mMSFileScanner {
		get { return withEventsField_mMSFileScanner; }
		set {
			if (withEventsField_mMSFileScanner != null) {
				withEventsField_mMSFileScanner.ErrorEvent -= mMSFileScanner_ErrorEvent;
				withEventsField_mMSFileScanner.MessageEvent -= mMSFileScanner_MessageEvent;
			}
			withEventsField_mMSFileScanner = value;
			if (withEventsField_mMSFileScanner != null) {
				withEventsField_mMSFileScanner.ErrorEvent += mMSFileScanner_ErrorEvent;
				withEventsField_mMSFileScanner.MessageEvent += mMSFileScanner_MessageEvent;
			}
		}

	}
	[System.STAThreadAttribute()]
	public static int Main()
	{

		// Returns 0 if no error, error code if an error

		int intReturnCode = 0;
		clsParseCommandLine objParseCommandLine = new clsParseCommandLine();
		bool blnProceed = false;

		intReturnCode = 0;
		mInputDataFilePath = string.Empty;
		mOutputFolderName = string.Empty;
		mParameterFilePath = string.Empty;
		mLogFilePath = string.Empty;

		mRecurseFolders = false;
		mRecurseFoldersMaxLevels = 0;
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

		//'TestZipper("\\proto-6\Db_Backups\Albert_Backup\MT_Shewanella_P196", "*.BAK.zip")
		//'Return 0

		try {
			blnProceed = false;
			if (objParseCommandLine.ParseCommandLine) {
				if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
					blnProceed = true;
			}

			if (mInputDataFilePath == null)
				mInputDataFilePath = string.Empty;

			if (!blnProceed || objParseCommandLine.NeedToShowHelp || objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount == 0 || mInputDataFilePath.Length == 0) {
				ShowProgramHelp();
				intReturnCode = -1;
			} else {
				mMSFileScanner = new clsMSFileInfoScanner();

				if (mCheckFileIntegrity)
					mUseCacheFiles = true;

				var _with1 = mMSFileScanner;
				// Note: These values will be overridden if /P was used and they are defined in the parameter file

				_with1.UseCacheFiles = mUseCacheFiles;
				_with1.ReprocessExistingFiles = mReprocessingExistingFiles;
				_with1.ReprocessIfCachedSizeIsZero = mReprocessIfCachedSizeIsZero;

				_with1.SaveTICAndBPIPlots = mSaveTICandBPIPlots;
				_with1.SaveLCMS2DPlots = mSaveLCMS2DPlots;
				_with1.LCMS2DPlotMaxPointsToPlot = mLCMS2DMaxPointsToPlot;
				_with1.LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor;
				_with1.TestLCMSGradientColorSchemes = mTestLCMSGradientColorSchemes;

				_with1.CheckCentroidingStatus = mCheckCentroidingStatus;

				_with1.ScanStart = mScanStart;
				_with1.ScanEnd = mScanEnd;
				_with1.ShowDebugInfo = mShowDebugInfo;

				_with1.ComputeOverallQualityScores = mComputeOverallQualityScores;
				_with1.CreateDatasetInfoFile = mCreateDatasetInfoFile;
				_with1.CreateScanStatsFile = mCreateScanStatsFile;

				_with1.UpdateDatasetStatsTextFile = mUpdateDatasetStatsTextFile;
				_with1.DatasetStatsTextFileName = mDatasetStatsTextFileName;

				_with1.CheckFileIntegrity = mCheckFileIntegrity;
				_with1.MaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck;
				_with1.ComputeFileHashes = mComputeFileHashes;
				_with1.ZipFileCheckAllData = mZipFileCheckAllData;

				_with1.IgnoreErrorsWhenRecursing = mIgnoreErrorsWhenRecursing;

				if (mLogFilePath.Length > 0) {
					_with1.LogMessagesToFile = true;
					_with1.LogFilePath = mLogFilePath;
				}

				_with1.DatasetIDOverride = mDatasetID;
				_with1.DSInfoDBPostingEnabled = mPostResultsToDMS;

				if ((mParameterFilePath != null) && mParameterFilePath.Length > 0) {
					_with1.LoadParameterFileSettings(mParameterFilePath);
				}

				if (mRecurseFolders) {
					if (mMSFileScanner.ProcessMSFilesAndRecurseFolders(mInputDataFilePath, mOutputFolderName, mRecurseFoldersMaxLevels)) {
						intReturnCode = 0;
					} else {
						intReturnCode = mMSFileScanner.ErrorCode;
					}
				} else {
					if (mMSFileScanner.ProcessMSFileOrFolderWildcard(mInputDataFilePath, mOutputFolderName, true)) {
						intReturnCode = 0;
					} else {
						intReturnCode = mMSFileScanner.ErrorCode;
						if (intReturnCode != 0) {
							ShowErrorMessage("Error while processing: " + mMSFileScanner.GetErrorMessage());
						}
					}
				}

				mMSFileScanner.SaveCachedResults();
			}

		} catch (Exception ex) {
			ShowErrorMessage("Error occurred in modMain->Main: " + Environment.NewLine + ex.Message);
			intReturnCode = -1;
		}

		return intReturnCode;

	}

	private static string GetAppVersion()
	{
		return Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (" + PROGRAM_DATE + ")";
	}

	private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
	{
		// Returns True if no problems; otherwise, returns false

		string strValue = string.Empty;
		object lstValidParameters = new List<string> {
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
			"Debug"
		};

		try {
			// Make sure no invalid parameters are present
			if (objParseCommandLine.InvalidParametersPresent(lstValidParameters)) {
				ShowErrorMessage("Invalid commmand line parameters", (from item in objParseCommandLine.InvalidParameters(lstValidParameters)"/" + item).ToList());
				return false;
			} else {
				var _with2 = objParseCommandLine;
				// Query objParseCommandLine to see if various parameters are present
				if (_with2.RetrieveValueForParameter("I", strValue)) {
					mInputDataFilePath = strValue;
				} else if (_with2.NonSwitchParameterCount > 0) {
					// Treat the first non-switch parameter as the input file
					mInputDataFilePath = _with2.RetrieveNonSwitchParameter(0);
				}

				if (_with2.RetrieveValueForParameter("O", strValue))
					mOutputFolderName = strValue;
				if (_with2.RetrieveValueForParameter("P", strValue))
					mParameterFilePath = strValue;

				if (_with2.RetrieveValueForParameter("S", strValue)) {
					mRecurseFolders = true;
					if (int.TryParse(strValue, 0)) {
						mRecurseFoldersMaxLevels = Convert.ToInt32(strValue);
					}
				}
				if (_with2.RetrieveValueForParameter("IE", strValue))
					mIgnoreErrorsWhenRecursing = true;

				if (_with2.RetrieveValueForParameter("L", strValue))
					mLogFilePath = strValue;

				if (_with2.IsParameterPresent("C"))
					mCheckFileIntegrity = true;
				if (_with2.RetrieveValueForParameter("M", strValue)) {
					if (int.TryParse(strValue, 0)) {
						mMaximumTextFileLinesToCheck = Convert.ToInt32(strValue);
					}
				}

				if (_with2.IsParameterPresent("H"))
					mComputeFileHashes = true;
				if (_with2.IsParameterPresent("QZ"))
					mZipFileCheckAllData = false;

				if (_with2.IsParameterPresent("NoTIC"))
					mSaveTICandBPIPlots = false;

				if (_with2.RetrieveValueForParameter("LC", strValue)) {
					mSaveLCMS2DPlots = true;
					if (int.TryParse(strValue, 0)) {
						mLCMS2DMaxPointsToPlot = Convert.ToInt32(strValue);
					}
				}

				if (_with2.RetrieveValueForParameter("LCDiv", strValue)) {
					if (int.TryParse(strValue, 0)) {
						mLCMS2DOverviewPlotDivisor = Convert.ToInt32(strValue);
					}
				}

				if (_with2.IsParameterPresent("LCGrad"))
					mTestLCMSGradientColorSchemes = true;

				if (_with2.IsParameterPresent("CC"))
					mCheckCentroidingStatus = true;

				if (_with2.RetrieveValueForParameter("ScanStart", strValue)) {
					if (int.TryParse(strValue, 0)) {
						mScanStart = Convert.ToInt32(strValue);
					}
				}

				if (_with2.RetrieveValueForParameter("ScanEnd", strValue)) {
					if (int.TryParse(strValue, 0)) {
						mScanEnd = Convert.ToInt32(strValue);
					}
				}

				if (_with2.IsParameterPresent("Debug"))
					mShowDebugInfo = true;

				if (_with2.IsParameterPresent("QS"))
					mComputeOverallQualityScores = true;

				if (_with2.RetrieveValueForParameter("DatasetID", strValue)) {
					if (!int.TryParse(strValue, mDatasetID)) {
						ShowErrorMessage("DatasetID is not an integer");
						return false;
					}
				}

				if (_with2.IsParameterPresent("DI"))
					mCreateDatasetInfoFile = true;

				if (_with2.IsParameterPresent("SS"))
					mCreateScanStatsFile = true;

				if (_with2.RetrieveValueForParameter("DST", strValue)) {
					mUpdateDatasetStatsTextFile = true;
					if (!string.IsNullOrEmpty(strValue)) {
						mDatasetStatsTextFileName = strValue;
					}
				}


				if (_with2.IsParameterPresent("CF"))
					mUseCacheFiles = true;
				if (_with2.IsParameterPresent("R"))
					mReprocessingExistingFiles = true;
				if (_with2.IsParameterPresent("Z"))
					mReprocessIfCachedSizeIsZero = true;

				if (_with2.IsParameterPresent("PostToDMS"))
					mPostResultsToDMS = true;


				return true;
			}

		} catch (Exception ex) {
			ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message);
			return false;
		}

	}

	private static string CollapseList(List<string> lstList)
	{
		string strCollapsed = null;

		if (lstList == null) {
			return string.Empty;
		} else {
			strCollapsed = string.Copy(lstList.Item(0));
		}

		for (int intIndex = 1; intIndex <= lstList.Count - 1; intIndex++) {
			strCollapsed += ", " + lstList.Item(intIndex);
		}

		return strCollapsed;

	}

	private static string CollapseList(string[] strList)
	{
		string strCollapsed = null;

		if (strList == null) {
			return string.Empty;
		} else {
			strCollapsed = string.Copy(strList(0));
		}

		for (int intIndex = 1; intIndex <= strList.Length - 1; intIndex++) {
			strCollapsed += ", " + strList(intIndex);
		}

		return strCollapsed;
	}

	private static void ShowErrorMessage(string strMessage)
	{
		const string strSeparator = "------------------------------------------------------------------------------";

		Console.WriteLine();
		Console.WriteLine(strSeparator);
		Console.WriteLine(strMessage);
		Console.WriteLine(strSeparator);
		Console.WriteLine();

		WriteToErrorStream(strMessage);
	}

	private static void ShowErrorMessage(string strTitle, List<string> items)
	{
		const string strSeparator = "------------------------------------------------------------------------------";
		string strMessage = null;

		Console.WriteLine();
		Console.WriteLine(strSeparator);
		Console.WriteLine(strTitle);
		strMessage = strTitle + ":";

		foreach (string item in items) {
			Console.WriteLine("   " + item);
			strMessage += " " + item;
		}
		Console.WriteLine(strSeparator);
		Console.WriteLine();

		WriteToErrorStream(strMessage);
	}


	private static void ShowProgramHelp()
	{
		try {
			mMSFileScanner = new clsMSFileInfoScanner();

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
			Console.WriteLine(" [/PostToDMS]");
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

			Console.WriteLine("Use /DST to update (or create) a tab-delimited text file with overview stats for the dataset.  If /DI is used, will include detailed scan counts; otherwise, will just have the dataset name, acquisition date, and (if available) sample name and comment. By default, the file is named " + DSSummarizer.clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME + "; to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt");
			Console.WriteLine();

			Console.WriteLine("Use /ScanStart and /ScanEnd to limit the scan range to process; useful for files where the first few scans are corrupt.  For example, to start processing at scan 10, use /ScanStart:10");
			Console.WriteLine("Use /Debug to display debug information at the console, including showing the scan number prior to reading each scan's data");
			Console.WriteLine();

			Console.WriteLine("Use /C to perform an integrity check on all known file types; this process will open known file types and verify that they contain the expected   This option is only used if you specify an Input Folder and use a wildcard; you will typically also want to use /S when using /C.");
			Console.WriteLine("Use /M to define the maximum number of lines to process when checking text or csv files; default is /M:" + clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK.ToString);
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
			Console.WriteLine("Known file extensions: " + CollapseList(mMSFileScanner.GetKnownFileExtensionsList()));
			Console.WriteLine("Known folder extensions: " + CollapseList(mMSFileScanner.GetKnownFolderExtensionsList()));
			Console.WriteLine();

			Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005");
			Console.WriteLine("Version: " + GetAppVersion());
			Console.WriteLine();

			Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com");
			Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/");
			Console.WriteLine();

			// Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
			System.Threading.Thread.Sleep(750);

		} catch (Exception ex) {
			ShowErrorMessage("Error displaying the program syntax: " + ex.Message);
		}

	}

	private static void WriteToErrorStream(string strErrorMessage)
	{
		try {
			using (swErrorStream == new StreamWriter(Console.OpenStandardError())) {
				swErrorStream.WriteLine(strErrorMessage);
			}
		} catch (Exception ex) {
			// Ignore errors here
		}
	}

	private static void mMSFileScanner_ErrorEvent(string Message)
	{
		// We could display any error messages here
		// However, mMSFileScanner already will have written out to the console, so there is no need to do so again

		WriteToErrorStream(Message);
	}

	private static void mMSFileScanner_MessageEvent(string Message)
	{
		// We could display any status messages here
		// However, mMSFileScanner already will have written out to the console, so there is no need to do so again
	}

}
