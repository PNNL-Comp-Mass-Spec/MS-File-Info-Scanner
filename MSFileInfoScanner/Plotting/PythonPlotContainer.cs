using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PRISM;

namespace MSFileInfoScanner.Plotting
{
    /// <summary>
    /// Python data container base class
    /// </summary>
    internal abstract class PythonPlotContainer : PlotContainerBase
    {
        // Ignore Spelling: Tmp, usr

        protected const string TMP_FILE_SUFFIX = "_TmpExportData";

        protected int mSeriesCount;

        /// <summary>
        /// When true, delete the temporary text files that contain data for Python to plot
        /// </summary>
        public bool DeleteTempFiles { get; set; }

        /// <summary>
        /// Path to the python executable
        /// </summary>
        public static string PythonPath { get; private set; }

        /// <summary>
        /// Path to MSFileInfoScanner_Plotter.py
        /// </summary>
        private string ScriptPath { get; set; } = string.Empty;

        public override int SeriesCount => mSeriesCount;

        public AxisInfo XAxisInfo { get; }

        public AxisInfo YAxisInfo { get; }

        /// <summary>
        /// True if the Python .exe could be found, otherwise false
        /// </summary>
        public static bool PythonInstalled => FindPython();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="plotTitle">Plot title</param>
        /// <param name="xAxisTitle">X-axis label</param>
        /// <param name="yAxisTitle">Y-axis label</param>
        /// <param name="writeDebug">When true, create a debug file that tracks processing steps</param>
        /// <param name="dataSource">Data source name</param>
        protected PythonPlotContainer(
            string plotTitle = "Undefined", string xAxisTitle = "X", string yAxisTitle = "Y",
            bool writeDebug = false, string dataSource = "") : base(writeDebug, dataSource)
        {
            mSeriesCount = 0;

            DeleteTempFiles = true;

            PlotTitle = plotTitle;
            PythonPath ??= string.Empty;

            XAxisInfo = new AxisInfo(xAxisTitle);
            YAxisInfo = new AxisInfo(yAxisTitle);
        }

        /// <summary>
        /// Find the plotting script file
        /// </summary>
        /// <returns>True if the script is found, otherwise false</returns>
        private bool FindPlottingScript()
        {
            const string PYTHON_PLOT_SCRIPT_NAME = "MSFileInfoScanner_Plotter.py";

            var exeDirectoryPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            if (exeDirectoryPath == null)
            {
                OnErrorEvent("Unable to determine the path to the directory with the MSFileInfoScanner executable or DLL");
                return false;
            }

            var exeDirectory = MSFileInfoScanner.GetDirectoryInfo(exeDirectoryPath);

            var directoriesToCheck = new List<string> {
                exeDirectory.FullName
            };

            var directoryInDMSPrograms = Path.Combine(exeDirectory.Root.FullName, @"DMS_Programs\MSFileInfoScanner");
            directoriesToCheck.Add(directoryInDMSPrograms);

            if (!directoryInDMSPrograms.StartsWith(@"C:\"))
            {
                directoriesToCheck.Add(@"C:\DMS_Programs\MSFileInfoScanner");
            }

            FileInfo pythonScriptFile = null;

            foreach (var directoryPath in directoriesToCheck)
            {
                var candidateFile = MSFileInfoScanner.GetFileInfo(Path.Combine(directoryPath, PYTHON_PLOT_SCRIPT_NAME));

                if (!candidateFile.Exists)
                    continue;

                pythonScriptFile = candidateFile;
                break;
            }

            if (pythonScriptFile == null)
            {
                // Script not found in the directory with the executing assembly or in the other standard locations
                OnErrorEvent("Python plotting script not found: {0}", Path.Combine(directoriesToCheck[0], PYTHON_PLOT_SCRIPT_NAME));
                return false;
            }

            ScriptPath = pythonScriptFile.FullName;
            return true;
        }

        /// <summary>
        /// Find the best candidate directory with Python 3.x
        /// </summary>
        /// <returns>True if Python could be found, otherwise false</returns>
        protected static bool FindPython()
        {
            if (!string.IsNullOrWhiteSpace(PythonPath))
                return true;

            if (SystemInfo.IsLinux)
            {
                PythonPath = "/usr/bin/python3";
                ConsoleMsgUtils.ShowDebug("Assuming Python 3 is at {0}", PythonPath);
                return true;
            }

            foreach (var directoryPath in PythonPathsToCheck())
            {
                var exePath = FindPythonExe(directoryPath);

                if (string.IsNullOrWhiteSpace(exePath))
                    continue;

                PythonPath = exePath;
                break;
            }

            return !string.IsNullOrWhiteSpace(PythonPath);
        }

        /// <summary>
        /// Find the best candidate directory with Python 3.x
        /// </summary>
        /// <returns>Path to the python executable, otherwise an empty string</returns>
        private static string FindPythonExe(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath);

            if (!directory.Exists)
                return string.Empty;

            var subDirectories = directory.GetDirectories("Python3*").ToList();
            subDirectories.AddRange(directory.GetDirectories("Python 3*"));
            subDirectories.Add(directory);

            var candidates = new List<FileInfo>();

            foreach (var subDirectory in subDirectories)
            {
                var files = subDirectory.GetFiles("python.exe");

                if (files.Length == 0)
                    continue;

                candidates.Add(files.First());
            }

            if (candidates.Count == 0)
                return string.Empty;

            // Find the newest .exe
            var query = (from item in candidates orderby item.LastWriteTime select item.FullName);

            return query.First();
        }

        protected bool GeneratePlotsWithPython(FileInfo exportFile, DirectoryInfo workDir)
        {
            if (!PythonInstalled)
            {
                NotifyPythonNotFound("Could not find the python executable");
                return false;
            }

            if (string.IsNullOrEmpty(ScriptPath) && !FindPlottingScript())
            {
                return false;
            }

            var args = PathUtils.PossiblyQuotePath(ScriptPath) + " " + PathUtils.PossiblyQuotePath(exportFile.FullName);

            OnDebugEvent("{0} {1}", PythonPath, args);

            var programRunner = new ProgRunner
            {
                Arguments = args,
                CreateNoWindow = true,
                MonitoringInterval = 2000,
                Name = "PythonPlotter",
                Program = PythonPath,
                Repeat = false,
                RepeatHoldOffTime = 0,
                WorkDir = workDir.FullName
            };

            RegisterEvents(programRunner);

            const int MAX_RUNTIME_SECONDS = 600;
            const int MONITOR_INTERVAL_MILLISECONDS = 1000;
            var runtimeExceeded = false;

            try
            {
                // Start the program executing
                programRunner.StartAndMonitorProgram();

                var startTime = DateTime.UtcNow;

                // Loop until program is complete, or until MAX_RUNTIME_SECONDS seconds elapses
                while (programRunner.State != ProgRunner.States.NotMonitoring)
                {
                    AppUtils.SleepMilliseconds(MONITOR_INTERVAL_MILLISECONDS);

                    if (DateTime.UtcNow.Subtract(startTime).TotalSeconds < MAX_RUNTIME_SECONDS)
                        continue;

                    OnErrorEvent("Plot creation with Python has taken more than {0:F0} minutes; aborting", MAX_RUNTIME_SECONDS / 60.0);
                    programRunner.StopMonitoringProgram(kill: true);

                    runtimeExceeded = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception creating plots using Python", ex);
                return false;
            }

            if (runtimeExceeded)
                return false;

            // Examine the exit code
            if (programRunner.ExitCode == 0)
            {
                var success = RenameTempPngFile(exportFile, workDir);

                return success;
            }

            OnErrorEvent("Python ExitCode = {0}", programRunner.ExitCode);
            return false;
        }

        protected void NotifyPythonNotFound(string currentTask)
        {
            OnErrorEvent("{0}; Python not found", currentTask);

            var debugMsg = "Paths searched:";

            foreach (var item in PythonPathsToCheck())
            {
                debugMsg += "\n  " + item;
            }

            OnDebugEvent(debugMsg);
        }

        public static IEnumerable<string> PythonPathsToCheck()
        {
            return new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python"),
                @"C:\ProgramData\Anaconda3",
                @"C:\"
            };
        }

        private bool RenameTempPngFile(FileSystemInfo exportFile, FileSystemInfo workDir)
        {
            string newFileName = null;

            try
            {
                // Confirm that the PNG file was created, then rename it
                var pngFile = MSFileInfoScanner.GetFileInfo(Path.Combine(workDir.FullName, Path.GetFileNameWithoutExtension(exportFile.Name) + ".png"));

                if (!pngFile.Exists)
                {
                    OnErrorEvent("Plot file not found: {0}", pngFile.FullName);
                    return false;
                }

                var baseFileName = Path.GetFileNameWithoutExtension(exportFile.Name);
                newFileName = baseFileName.Substring(0, baseFileName.Length - TMP_FILE_SUFFIX.Length) + ".png";

                var finalPngFile = MSFileInfoScanner.GetFileInfo(Path.Combine(workDir.FullName, newFileName));

                if (finalPngFile.Exists)
                    finalPngFile.Delete();

                pngFile.MoveTo(finalPngFile.FullName);

                return true;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(newFileName))
                {
                    OnErrorEvent("Error renaming the Plot file", ex);
                }
                else
                {
                    OnErrorEvent(string.Format("Error renaming the Plot file from {0} to {1}", exportFile.Name, newFileName), ex);
                }

                return false;
            }
        }
    }
}