﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using PRISM;
using ThermoRawFileReader;

namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// Dataset file info, including dataset ID, scan count, acquisition start time, etc.
    /// </summary>
    public class DatasetFileInfo
    {
        // Ignore Spelling: Acq

        /// <summary>
        /// File creation time (local time)
        /// </summary>
        public DateTime FileSystemCreationTime { get; set; }

        /// <summary>
        /// File modification time (local time)
        /// </summary>
        public DateTime FileSystemModificationTime { get; set; }

        /// <summary>
        /// Dataset ID
        /// </summary>
        public int DatasetID { get; set; }

        /// <summary>
        /// Dataset name
        /// </summary>
        public string DatasetName { get; set; }

        /// <summary>
        /// File extension
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// Acquisition start time
        /// </summary>
        public DateTime AcqTimeStart { get; set; }

        /// <summary>
        /// Acquisition end time
        /// </summary>
        public DateTime AcqTimeEnd { get; set; }

        /// <summary>
        /// Scan count (spectrum count)
        /// </summary>
        /// <remarks>For UIMF files, the number of frames</remarks>
        public int ScanCount { get; set; }

        /// <summary>
        /// Size of the file, in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Information on the devices with data stored in this dataset
        /// </summary>
        public List<DeviceInfo> DeviceList { get; }

        /// <summary>
        /// Tracks the file size and hash value for each primary instrument file
        /// </summary>
        public Dictionary<string, InstrumentFileInfo> InstrumentFiles { get; }

        /// <summary>
        /// Quality score
        /// </summary>
        /// <remarks>For Thermo files, this is simply the average ion intensity</remarks>
        public float OverallQualityScore { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public DatasetFileInfo()
        {
            InstrumentFiles = new Dictionary<string, InstrumentFileInfo>();
            DeviceList = new List<DeviceInfo>();
            Clear();
        }

        /// <summary>
        /// Constructor that takes Dataset ID and Dataset Name
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="datasetName">Dataset name</param>
        public DatasetFileInfo(int datasetId, string datasetName)
        {
            Clear();
            DatasetID = datasetId;
            DatasetName = datasetName;
        }

        /// <summary>
        /// Clear all values
        /// </summary>
        public void Clear()
        {
            FileSystemCreationTime = DateTime.MinValue;
            FileSystemModificationTime = DateTime.MinValue;
            DatasetID = 0;
            DatasetName = string.Empty;
            FileExtension = string.Empty;
            AcqTimeStart = DateTime.MinValue;
            AcqTimeEnd = DateTime.MinValue;
            ScanCount = 0;
            FileSizeBytes = 0;
            DeviceList.Clear();
            InstrumentFiles.Clear();
            OverallQualityScore = 0;
        }

        /// <summary>
        /// Compute the SHA-1 hash of the given file, then add it to InstrumentFiles
        /// </summary>
        /// <param name="instrumentFile">Instrument file</param>
        public bool AddInstrumentFile(FileInfo instrumentFile)
        {
            var sha1Hash = HashUtilities.ComputeFileHashSha1(instrumentFile.FullName);
            AddInstrumentFile(instrumentFile.Name, instrumentFile.Length, sha1Hash, HashUtilities.HashTypeConstants.SHA1);
            return true;
        }

        /// <summary>
        /// Add an instrument file, optionally including its file hash
        /// </summary>
        /// <param name="instrumentFileRelativePath">Relative path to the instrument file</param>
        /// <param name="fileSizeBytes">File size, in bytes</param>
        /// <param name="hashValue">Hash value</param>
        /// <param name="hashType">Hash type</param>
        public void AddInstrumentFile(string instrumentFileRelativePath, long fileSizeBytes, string hashValue, HashUtilities.HashTypeConstants hashType)
        {
            if (InstrumentFiles.ContainsKey(instrumentFileRelativePath))
            {
                throw new DuplicateNameException("Duplicate key in AddInstrumentFile; Instrument file already defined: " +
                                                 instrumentFileRelativePath);
            }

            var instFileInfo = new InstrumentFileInfo
            {
                Length = fileSizeBytes
            };

            if (string.IsNullOrWhiteSpace(hashValue))
            {
                instFileInfo.Hash = string.Empty;
                instFileInfo.HashType = HashUtilities.HashTypeConstants.Undefined;
            }
            else
            {
                instFileInfo.Hash = hashValue;
                instFileInfo.HashType = hashType;
            }

            InstrumentFiles.Add(instrumentFileRelativePath, instFileInfo);
        }

        /// <summary>
        /// Add an instrument file, but do not compute the SHA-1 hash
        /// </summary>
        /// <param name="instrumentFile">Instrument file info</param>
        public void AddInstrumentFileNoHash(FileInfo instrumentFile)
        {
            var sha1Hash = string.Empty;
            AddInstrumentFile(instrumentFile.Name, instrumentFile.Length, sha1Hash, HashUtilities.HashTypeConstants.Undefined);
        }

        /// <summary>
        /// Show the dataset name and scan count
        /// </summary>
        public override string ToString()
        {
            return string.Format("Dataset {0}, ScanCount={1}", DatasetName, ScanCount);
        }
    }
}
