﻿using System;
using System.Collections.Generic;
using System.IO;
using PRISM;

namespace MSFileInfoScanner.Plotting
{
    internal abstract class PlotContainerBase : EventNotifier
    {
        // Ignore Spelling: deisotoped, png, yyyy-MM-dd hh:mm:ss

        protected StreamWriter mLogWriter;

        public string AnnotationBottomLeft { get; set; }

        public string AnnotationBottomRight { get; set; }

        public string PlotTitle { get; set; }

        public bool PlottingDeisotopedData { get; set; }

        public abstract int SeriesCount { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="writeDebug">When true, create a debug file that tracks processing steps</param>
        /// <param name="dataSource">Data source name</param>
        protected PlotContainerBase(bool writeDebug = false, string dataSource = "")
        {
            AnnotationBottomLeft = string.Empty;
            AnnotationBottomRight = string.Empty;
            PlotTitle = "Undefined Plot Title";

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

        /// <summary>
        /// Get semicolon separated list of plot options
        /// </summary>
        protected string GetPlotOptions()
        {
            var plotOptions = new List<string> {
                "Title=" + PlotTitle,
                "BottomLeft=" + AnnotationBottomLeft,
                "BottomRight=" + AnnotationBottomRight};

            return string.Join(";", plotOptions);
        }

        protected void OpenDebugFile(string dataSource)
        {
            var logDirectory = MSFileInfoScanner.GetAppDataDirectoryPath();

            string logFileName;

            if (string.IsNullOrWhiteSpace(dataSource))
            {
                logFileName = "TICAndBPIPlotter_Debug.txt";
            }
            else
            {
                logFileName = dataSource + ".txt";
            }

            var logFile = MSFileInfoScanner.GetFileInfo(Path.Combine(logDirectory, logFileName));
            var addBlankLink = logFile.Exists;

            mLogWriter = new StreamWriter(new FileStream(logFile.FullName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };

            if (addBlankLink)
                mLogWriter.WriteLine();
        }

        public abstract bool SaveToPNG(FileInfo pngFile, int width, int height, int resolution);

        public void WriteDebugLog(string message)
        {
            mLogWriter?.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + ": " + message);
        }
    }
}