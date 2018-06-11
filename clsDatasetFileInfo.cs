using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using PRISM;

namespace MSFileInfoScanner
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
        public DateTime FileSystemCreationTime { get; set; }
        public DateTime FileSystemModificationTime { get; set; }
        public int DatasetID { get; set; }
        public string DatasetName { get; set; }
        public string FileExtension { get; set; }
        public DateTime AcqTimeStart { get; set; }
        public DateTime AcqTimeEnd { get; set; }
        public int ScanCount { get; set; }
        public long FileSizeBytes { get; set; }
        /// <summary>
        /// Tracks the file size and hash value for each primary instrument file
        /// </summary>
        public Dictionary<string, InstrumentFileInfo> InstrumentFiles { get; } = new Dictionary<string, InstrumentFileInfo>();

        public float OverallQualityScore { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsDatasetFileInfo()
        {
            Clear();
        }

        public clsDatasetFileInfo(int datasetId, string datasetName)
        {
            Clear();
            DatasetID = datasetId;
            DatasetName = datasetName;
        }

        public void Clear()
        {
            FileSystemCreationTime = DateTime.MinValue;
            FileSystemModificationTime = DateTime.MinValue;
            DatasetID = 0;
            DatasetName = string.Empty;
            FileExtension = string.Empty;
            AcqTimeStart = DateTime.MinValue;
            AcqTimeEnd  = DateTime.MinValue;
            ScanCount = 0;
            FileSizeBytes = 0;
            InstrumentFiles.Clear();
            OverallQualityScore = 0;
        }

        /// <summary>
        /// Compute the SHA1 hash of the given file, then add it to InstrumentFiles
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
