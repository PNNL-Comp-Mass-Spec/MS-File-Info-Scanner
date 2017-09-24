using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MSFileInfoScanner
{
    /// <summary>
    /// Python data container base clase
    /// </summary>
    internal abstract class clsPythonPlotContainer : clsPlotContainerBase
    {
        protected int mSeriesCount;

        public bool DeleteTempFiles { get; set; }

        public string PlotTitle { get; set; }

        /// <summary>
        /// Path to the python executable
        /// </summary>
        public string PythonPath { get; private set; }

        public override int SeriesCount => mSeriesCount;

        public clsAxisInfo XAxisInfo { get; }

        public clsAxisInfo YAxisInfo { get; }

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
            PythonPath = string.Empty;

            XAxisInfo = new clsAxisInfo(xAxisTitle);
            YAxisInfo = new clsAxisInfo(yAxisTitle);

        }

        /// <summary>
        /// Find the base candidate folder with Python 3.x
        /// </summary>
        /// <returns></returns>
        protected bool FindPython()
        {
            var pathsToCheck = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                @"C:\ProgramData\Anaconda3",
                @"C:\"
            };

            foreach (var folderPath in pathsToCheck)
            {
                var exePath = FindPython(folderPath);
                if (string.IsNullOrWhiteSpace(exePath))
                    continue;

                PythonPath = exePath;
                break;
            }

            return !string.IsNullOrWhiteSpace(PythonPath);

        }

        private string FindPython(string folderPath)
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

    }
}