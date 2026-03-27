namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// Natural organic matter statistics
    /// </summary>
    public class NaturalOrganicMatterStats
    {
        /// <summary>
        /// Number of m/z ions (aka peaks)
        /// </summary>
        public int MzIonCount { get; set; }

        /// <summary>
        /// Median m/z value
        /// </summary>
        public double MzMedian { get; set; }

        /// <summary>
        /// Skewness of m/z values
        /// </summary>
        public double MzSkew { get; set; }

        /// <summary>
        /// Kurtosis of m/z values
        /// </summary>
        public double MzKurtosis { get; set; }

        /// <summary>
        /// Organic count
        /// </summary>
        /// <remarks>
        /// Number of m/z values where the decimal value of the m/z is between 0.0 and 0.4
        /// </remarks>
        public int OrganicCount { get; set; }

        /// <summary>
        /// Organic intensity sum
        /// </summary>
        /// <remarks>
        /// Sum of the intensities of m/z values where the decimal value of the m/z is between 0.0 and 0.4
        /// </remarks>
        public double OrganicIntensitySum { get; set; }

        /// <summary>
        /// Inorganic count
        /// </summary>
        /// <remarks>
        /// Number of m/z values where the decimal value of the m/z is between 0.6 and 0.999
        /// </remarks>
        public int InorganicCount { get; set; }

        /// <summary>
        /// Inorganic intensity sum
        /// </summary>
        /// <remarks>
        /// Sum of the intensities of m/z values where the decimal value of the m/z is between 0.6 and 0.999
        /// </remarks>
        public double InorganicIntensitySum { get; set; }

        /// <summary>
        /// Organic/inorganic ratio (NaN if InorganicCount is 0)
        /// </summary>
        /// <remarks>
        /// OrganicCount / InorganicCount
        /// </remarks>
        public double OrganicInorganicRatio
        {
            get
            {
                if (InorganicCount <= 0)
                    return double.NaN;

                return OrganicCount / (double)InorganicCount;
            }
        }

        /// <summary>
        /// Organic/inorganic ratio, intensity weighted (NaN if InorganicIntensitySum is 0)
        /// </summary>
        /// <remarks>
        /// OrganicIntensitySum / InorganicIntensitySum
        /// </remarks>
        public double OrganicInorganicRatioWeightedIntensity
        {
            get
            {
                if (InorganicIntensitySum <= 0)
                    return double.NaN;

                return OrganicIntensitySum / InorganicIntensitySum;
            }
        }

        /// <summary>
        /// Carbon-13 pair count
        /// </summary>
        /// <remarks>
        /// Count of pairs of peaks separated by 1.003355 (plus/minus tolerance of 0.0005)
        /// </remarks>
        public int C13Count { get; set; }

        /// <summary>
        /// Carbon-13 intensity sum
        /// </summary>
        /// <remarks>
        /// Sum of the intensities of pairs of peaks separated by 1.003355 (plus/minus tolerance of 0.0005)
        /// Use minimum intensity of the paired peaks
        /// </remarks>
        public double C13IntensitySum { get; set; }

        /// <summary>
        /// Chlorine-37 pair count
        /// </summary>
        /// <remarks>
        /// Count of pairs of peaks separated by 1.99705 (plus/minus tolerance of 0.0005)
        /// </remarks>
        public int Chlorine37Count { get; set; }

        /// <summary>
        /// Chlorine-37 pair intensity sum
        /// </summary>
        /// <remarks>
        /// Sum of the intensities of pairs of peaks separated by 1.99705 (plus/minus tolerance of 0.0005)
        /// Use minimum intensity of the paired peaks
        /// </remarks>
        public double Chlorine37IntensitySum { get; set; }

        /// <summary>
        /// Carbon-13 to Chlorine-37 Pair Ratio (NaN if Chlorine37PairCount is 0)
        /// </summary>
        /// <remarks>
        /// C13PairCount / Cl37PairCount
        /// </remarks>
        public double C13Ratio
        {
            get
            {
                if (Chlorine37Count <= 0)
                    return double.NaN;

                return C13Count / (double)Chlorine37Count;
            }
        }

        /// <summary>
        /// C13 to Chlorine-37 Pair Intensity Ratio (NaN if Chlorine37IntensitySum is 0)
        /// </summary>
        /// <remarks>
        /// C13PairIntensitySum / Cl37PairIntensitySum
        /// </remarks>
        public double C13RatioWeightedIntensity
        {
            get
            {
                if (Chlorine37IntensitySum <= 0)
                    return double.NaN;

                return C13IntensitySum / Chlorine37IntensitySum;
            }
        }

        /// <summary>
        /// Chloride cluster count
        /// </summary>
        /// <remarks>
        /// Number of times that more than two peaks in series are sequentially separated by 1.99705 (plus/minus tolerance of 0.0005)
        /// </remarks>
        public int ChlorideClusterCount { get; set; }

        /// <summary>
        /// Maximum length of chloride clusters
        /// </summary>
        public int ChlorideClusterMaxLength { get; set; }

        /// <summary>
        /// Mean length of chloride clusters
        /// </summary>
        public double ChlorideClusterMeanLength { get; set; }

        /// <summary>
        /// Total number of peaks that are part of chloride clusters
        /// </summary>
        public int ChlorideClusterPeaksTotal { get; set; }

        /// <summary>
        /// Percent of peaks that are members of a chloride cluster
        /// </summary>
        public double ChlorideClusterPeaksPercent { get; set; }

        /// <summary>
        /// Total intensity of peaks that are members of a chloride cluster
        /// </summary>
        public double ChlorideClusterIntensityTotal { get; set; }

        /// <summary>
        /// Percent of the total intensity of the peaks in the mass spectrum that is associated with chloride clusters
        /// </summary>
        public double ChlorideClusterIntensityPercent { get; set; }

        /// <summary>
        /// Scan number
        /// </summary>
        public int ScanNumber { get; }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scanNumber"></param>
        public NaturalOrganicMatterStats(int scanNumber)
        {
            ScanNumber = scanNumber;
        }

        /// <summary>
        /// Deep clone the source stats
        /// </summary>
        /// <param name="sourceStats">Source stats</param>
        /// <returns>Natural organic matter stats instance</returns>
        public NaturalOrganicMatterStats Clone(NaturalOrganicMatterStats sourceStats)
        {
            return new NaturalOrganicMatterStats(sourceStats.ScanNumber)
            {
                MzIonCount = sourceStats.MzIonCount,
                MzMedian = sourceStats.MzMedian,
                MzSkew = sourceStats.MzSkew,
                MzKurtosis = sourceStats.MzKurtosis,
                OrganicCount = sourceStats.OrganicCount,
                OrganicIntensitySum = sourceStats.OrganicIntensitySum,
                InorganicCount = sourceStats.InorganicCount,
                InorganicIntensitySum = sourceStats.InorganicIntensitySum,
                C13Count = sourceStats.C13Count,
                C13IntensitySum = sourceStats.C13IntensitySum,
                Chlorine37Count = sourceStats.Chlorine37Count,
                Chlorine37IntensitySum = sourceStats.Chlorine37IntensitySum,
                ChlorideClusterCount = sourceStats.ChlorideClusterCount,
                ChlorideClusterMaxLength = sourceStats.ChlorideClusterMaxLength,
                ChlorideClusterMeanLength = sourceStats.ChlorideClusterMeanLength,
                ChlorideClusterPeaksTotal = sourceStats.ChlorideClusterPeaksTotal,
                ChlorideClusterPeaksPercent = sourceStats.ChlorideClusterPeaksPercent,
                ChlorideClusterIntensityTotal = sourceStats.ChlorideClusterIntensityTotal,
                ChlorideClusterIntensityPercent = sourceStats.ChlorideClusterIntensityPercent
            };
        }
    }
}
