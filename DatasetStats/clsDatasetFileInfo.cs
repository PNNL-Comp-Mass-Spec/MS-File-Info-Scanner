using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using PRISM;

namespace MSFileInfoScanner.DatasetStats
{
    public class clsDatasetFileInfo
    {
        public struct InstrumentFileInfo
        {
            /// <summary>
            /// File size, in bytes
            /// </summary>
            public long Length;

            /// <summary>
            /// File hash (empty string if undefined)
            /// </summary>
            public string Hash;

            /// <summary>
            /// File hash type
            /// </summary>
            public HashUtilities.HashTypeConstants HashType;
        }

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
        /// Dataset Name
        /// </summary>
        public string DatasetName { get; set; }

        /// <summary>
        /// File extension
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// Dataset acquisition time start
        /// </summary>
        public DateTime AcqTimeStart { get; set; }

        /// <summary>
        /// Dataset acquisition time end
        /// </summary>
        public DateTime AcqTimeEnd { get; set; }

        /// <summary>
        /// Number of spectra (scans)
        /// </summary>
        /// <remarks>For UIMF files, the number of frames</remarks>
        public int ScanCount { get; set; }

        /// <summary>
        /// File size, in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Tracks the file size and hash value for each primary instrument file
        /// </summary>
        public Dictionary<string, InstrumentFileInfo> InstrumentFiles { get; } = new Dictionary<string, InstrumentFileInfo>();

        /// <summary>
        /// Quality score
        /// </summary>
        /// <remarks>For Thermo files, this is simply the average ion intensity</remarks>
        public float OverallQualityScore { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetFileInfo()
        {
            Clear();
        }

        /// <summary>
        /// Constructor that takes Dataset ID and Dataset Name
        /// </summary>
        /// <param name="datasetId"></param>
        /// <param name="datasetName"></param>
        public clsDatasetFileInfo(int datasetId, string datasetName)
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
            InstrumentFiles.Clear();
            OverallQualityScore = 0;
        }

        /// <summary>
        /// Compute the SHA-1 hash of the given file, then add it to InstrumentFiles
        /// </summary>
        /// <param name="instrumentFile"></param>
        /// <returns></returns>
        public bool AddInstrumentFile(FileInfo instrumentFile)
        {
            var sha1Hash = HashUtilities.ComputeFileHashSha1(instrumentFile.FullName);
            AddInstrumentFile(instrumentFile.Name, instrumentFile.Length, sha1Hash, HashUtilities.HashTypeConstants.SHA1);
            return true;
        }

        /// <summary>
        /// Add an instrument file, optionally including its file hash
        /// </summary>
        /// <param name="instrumentFileRelativePath"></param>
        /// <param name="fileSizeBytes"></param>
        /// <param name="hashValue"></param>
        /// <param name="hashType"></param>
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

        public void AddInstrumentFileNoHash(FileInfo instrumentFile)
        {
            var sha1Hash = "";
            AddInstrumentFile(instrumentFile.Name, instrumentFile.Length, sha1Hash, HashUtilities.HashTypeConstants.Undefined);
        }
    }
}
