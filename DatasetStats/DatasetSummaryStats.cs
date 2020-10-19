using System.Collections.Generic;

namespace MSFileInfoScanner.DatasetStats
{
    public class DatasetSummaryStats
    {
        public double ElutionTimeMax { get; set; }

        public SummaryStatDetails MSStats { get;}

        public SummaryStatDetails MSnStats { get;}

        /// <summary>
        /// Keeps track of each ScanType in the dataset, along with the number of scans of this type
        /// </summary>
        /// <remarks>
        /// Keys are of the form "ScanTypeName::###::ScanFilterText"
        /// Values are number of scans with the given scan type and scan filter
        /// Examples
        ///   HMS::###::FTMS + p NSI Full ms
        ///   HMSn::###::FTMS + p NSI d Full ms2 0@hcd25.00
        ///   MS::###::ITMS + c ESI Full ms
        ///   MSn::###::ITMS + p ESI d Z ms
        ///   MSn::###::ITMS + c ESI d Full ms2 @cid35.00
        /// </remarks>
        public Dictionary<string, int> ScanTypeStats { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public DatasetSummaryStats()
        {
            MSStats = new SummaryStatDetails();
            MSnStats = new SummaryStatDetails();
            ScanTypeStats = new Dictionary<string, int>();
            Clear();
        }

        public void Clear()
        {
            ElutionTimeMax = 0;
            MSStats.Clear();
            MSnStats.Clear();
            ScanTypeStats.Clear();
        }
    }
}
