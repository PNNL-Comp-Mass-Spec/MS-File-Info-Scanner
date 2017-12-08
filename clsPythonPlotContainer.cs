using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PRISM;

namespace MSFileInfoScanner
{
    /// <summary>
    /// Python data container base clase
    /// </summary>
    internal abstract class clsPythonPlotContainer : clsPlotContainerBase
    {
        protected const string TMP_FILE_SUFFIX = "_TmpExportData";

        protected int mSeriesCount;

        public bool DeleteTempFiles { get; set; }

        /// <summary>
        /// Path to the python executable
        /// </summary>
        public static string PythonPath { get; private set; }

        public override int SeriesCount => mSeriesCount;

        public clsAxisInfo XAxisInfo { get; }

        public clsAxisInfo YAxisInfo { get; }

        /// <summary>
        /// True if the Python .exe could be found, otherwise false
        /// </summary>
        public static bool PythonInstalled => FindPython();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="plotTitle"></param>
        /// <param name="xAxisTitle"></param>
        /// <param name="yAxisTitle"></param>
        /// <param name="writeDebug"></param>
        /// <param name="dataSource"></param>
        protected clsPythonPlotContainer(
            string plotTitle = "Undefined", string xAxisTitle = "X", string yAxisTitle = "Y",
            bool writeDebug = false, string dataSource = "") : base(writeDebug, dataSource)
        {

            mSeriesCount = 0;

            DeleteTempFiles = true;

            PlotTitle = plotTitle;
            if (PythonPath == null)
                PythonPath = string.Empty;

            XAxisInfo = new clsAxisInfo(xAxisTitle);
            YAxisInfo = new clsAxisInfo(yAxisTitle);

        }

        /// <summary>
        /// Find the best candidate folder with Python 3.x
        /// </summary>
        /// <returns>True if Python could be found, otherwise false</returns>
        protected static bool FindPython()
        {
            if (!string.IsNullOrWhiteSpace(PythonPath))
                return true;

            var pathsToCheck = PythonPathsToCheck();

            foreach (var folderPath in pathsToCheck)
            {
                var exePath = FindPythonExe(folderPath);
                if (string.IsNullOrWhiteSpace(exePath))
                    continue;

                PythonPath = exePath;
                break;
            }

            return !string.IsNullOrWhiteSpace(PythonPath);

        }

        /// <summary>
        /// Find the best candidate folder with Python 3.x
        /// </summary>
        /// <returns>Path to the python executable, otherwise an empty string</returns>
        private static string FindPythonExe(string folderPath)
        {
            var directory = new DirectoryInfo(folderPath);
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
            var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (exeDirectory == null)
            {
                OnErrorEvent("Unable to determine the path to the directory with the MSFileInfoScanner executable");
                return false;
            }

            var pythonScriptFile = new FileInfo(Path.Combine(exeDirectory, "MSFileInfoScanner_Plotter.py"));
            if (!pythonScriptFile.Exists)
            {
                OnErrorEvent("Python plotting script not found: " + pythonScriptFile.FullName);
                return false;
            }

            var args = clsPathUtils.PossiblyQuotePath(pythonScriptFile.FullName) + " " + clsPathUtils.PossiblyQuotePath(exportFile.FullName);

            OnDebugEvent(string.Format("{0} {1}", PythonPath, args));

            var progRunner = new clsProgRunner
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

            RegisterEvents(progRunner);

            const int MAX_RUNTIME_SECONDS = 600;
            const int MONITOR_INTERVAL_MILLISECONDS = 1000;
            var runtimeExceeded = false;

            try
            {
                // Start the program executing
                progRunner.StartAndMonitorProgram();

                var startTime = DateTime.UtcNow;

                // Loop until program is complete, or until MAX_RUNTIME_SECONDS seconds elapses
                while (progRunner.State != clsProgRunner.States.NotMonitoring)
                {
                    clsProgRunner.SleepMilliseconds(MONITOR_INTERVAL_MILLISECONDS);

                    if (DateTime.UtcNow.Subtract(startTime).TotalSeconds < MAX_RUNTIME_SECONDS)
                        continue;

                    OnErrorEvent(string.Format("Plot creation with Python has taken more than {0:F0} minutes; aborting", MAX_RUNTIME_SECONDS / 60.0));
                    progRunner.StopMonitoringProgram(kill: true);

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
            if (progRunner.ExitCode == 0)
            {
                var success = RenameTempPngFile(exportFile, workDir);

                return success;
            }

            OnErrorEvent("Python ExitCode = " + progRunner.ExitCode);
            return false;

        }

        protected void NotifyPythonNotFound(string currentTask)
        {
            OnErrorEvent(currentTask + "; Python not found");

            var debugMsg = "Paths searched:";
            foreach (var item in PythonPathsToCheck())
            {
                debugMsg += "\n  " + item;
            }

            OnDebugEvent(debugMsg);

        }

        private static IEnumerable<string> PythonPathsToCheck()
        {
            return new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
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
                var pngFile = new FileInfo(Path.Combine(workDir.FullName, Path.GetFileNameWithoutExtension(exportFile.Name) + ".png"));
                if (!pngFile.Exists)
                {
                    OnErrorEvent("Plot file not found: " + pngFile.FullName);
                    return false;
                }

                var baseFileName = Path.GetFileNameWithoutExtension(exportFile.Name);
                newFileName = baseFileName.Substring(0, baseFileName.Length - TMP_FILE_SUFFIX.Length) + ".png";

                var finalPngFile = new FileInfo(Path.Combine(workDir.FullName, newFileName));
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