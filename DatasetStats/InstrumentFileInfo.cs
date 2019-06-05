using PRISM;

namespace MSFileInfoScanner.DatasetStats
{
    public class InstrumentFileInfo
    {
        /// <summary>
        /// File size, in bytes
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// File hash (empty string if undefined)
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// File hash type
        /// </summary>
        public HashUtilities.HashTypeConstants HashType { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public InstrumentFileInfo()
        {
            Clear();
        }

        public void Clear()
        {
            Length = 0;
            Hash = string.Empty;
            HashType = HashUtilities.HashTypeConstants.Undefined;

        }
    }
}
