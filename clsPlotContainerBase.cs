using System;
using System.IO;
using PRISM;

namespace MSFileInfoScanner
{
    internal abstract class clsPlotContainerBase : clsEventNotifier
    {
        protected StreamWriter mLogWriter;

        public string AnnotationBottomLeft { get; set; }

        public string AnnotationBottomRight { get; set; }

        public bool PlottingDeisotopedData { get; set; }

        public abstract int SeriesCount { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="writeDebug"></param>
        /// <param name="dataSource"></param>
        protected clsPlotContainerBase(bool writeDebug = false, string dataSource = "")
        {
            AnnotationBottomLeft = string.Empty;
            AnnotationBottomRight = string.Empty;
            PlottingDeisotopedData = false;

            if (writeDebug)
            {
                OpenDebugFile(dataSource);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            mLogWriter?.Close();
        }

        protected void OpenDebugFile(string dataSource)
        {
            var logFolder = clsMSFileInfoScanner.GetAppDataFolderPath();

            string logFileName;
            if (string.IsNullOrWhiteSpace(dataSource))
                logFileName = "TICandBPIPlotter_Debug.txt";
            else
            {
                logFileName = dataSource + ".txt";
            }

            var logFile = new FileInfo(Path.Combine(logFolder, logFileName));
            var addBlankLink = logFile.Exists;

            mLogWriter = new StreamWriter(new FileStream(logFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };

            if (addBlankLink)
                mLogWriter.WriteLine();
        }

        public abstract void SaveToPNG(string pngFilePath, int width, int height, int resolution);

        public void WriteDebugLog(string message)
        {
            mLogWriter?.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + ": " + message);
        }

    }
}