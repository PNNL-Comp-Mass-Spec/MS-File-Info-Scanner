using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MathNet.Numerics.Statistics;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// Natural organic matter stats processor
    /// </summary>
    public class NaturalOrganicMatterStatsProcessor : EventNotifier
    {
        /// <summary>
        /// Natural organic matter stats
        /// </summary>
        /// <remarks>
        /// Median values, if more than one scan
        /// </remarks>
        public NaturalOrganicMatterStats NOMStats { get; private set; }

        /// <summary>
        /// Processing options
        /// </summary>
        public InfoScannerOptions Options { get; protected set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Processing options</param>
        public NaturalOrganicMatterStatsProcessor(InfoScannerOptions options)
        {
            NOMStats = new NaturalOrganicMatterStats(0);
            Options = options;
        }

        /// <summary>
        /// Compute the natural organic matter stats
        /// </summary>
        /// <param name="massSpectra">Dictionary where keys are scan number and values are lists of m/z and intensity pairs</param>
        /// <returns>True if successful, false if an error</returns>
        public bool ComputeStats(Dictionary<int, List<KeyValuePair<double, double>>> massSpectra)
        {
            try
            {
                var nomStatsByScan = new SortedDictionary<int, NaturalOrganicMatterStats>();

                foreach (var scan in massSpectra)
                {
                    var success = ComputeStats(scan.Key, scan.Value, out var nomStats);
                    if (!success)
                        return false;

                    nomStatsByScan.Add(nomStats.ScanNumber, nomStats);
                }

                switch (nomStatsByScan.Count)
                {
                    case 0:
                        OnWarningEvent("No scans were found in the input file; NOM stats could not be computed");
                        return false;

                    case 1:
                        NOMStats = nomStatsByScan.First().Value;
                        return true;

                    default:
                        // Multiple scans; compute median values
                        ComputeMedianValues(nomStatsByScan);
                        return true;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in NaturalOrganicMatterStatsProcessor.ComputeStats", ex);
                return false;
            }
        }

        /// <summary>
        /// Calculate basic stats
        /// </summary>
        /// <param name="nomStats">Natural organic matter stats</param>
        /// <param name="massSpectrum">List of m/z and intensity key/value pairs</param>
        private void CalculateBasicStats(NaturalOrganicMatterStats nomStats, IReadOnlyCollection<KeyValuePair<double, double>> massSpectrum)
        {
            var mzs = massSpectrum.Select(item => item.Key).ToList();

            nomStats.MzIonCount = massSpectrum.Count;
            nomStats.MzMedian = mzs.Median();

            var (skewness, kurtosis) = mzs.PopulationSkewnessKurtosis();

            nomStats.MzSkew = skewness;
            nomStats.MzKurtosis = kurtosis;
        }

        /// <summary>
        /// Detect chloride cluster series (Cl, Cl2, Cl3, Cl4, ...)
        /// </summary>
        /// <remarks>
        /// Identifies sequential peaks separated by the Chlorine37 mass difference (1.99705 ± 0.0005), indicating molecules with multiple chlorine atoms
        /// Only counts clusters with 3 or more peaks (pairs are already covered by Chlorine37_count)
        ///
        /// </remarks>
        /// <param name="nomStats">Natural organic matter stats</param>
        /// <param name="massSpectrum">List of m/z and intensity key/value pairs</param>
        private void CalculateChlorideClusterMetrics(NaturalOrganicMatterStats nomStats, IEnumerable<KeyValuePair<double, double>> massSpectrum)
        {
            // Assure that the m/z list is sorted

            // Keys in this list are m/z values; values as Intensities
            var sortedPeaks = new List<KeyValuePair<double, double>>();

            // Track which peaks are already in clusters
            var peakUsedInCluster = new List<bool>(sortedPeaks.Count);

            foreach (var entry in (from item in massSpectrum orderby item.Key select item))
            {
                sortedPeaks.Add(entry);
                peakUsedInCluster.Add(false);
            }

            var mzCount = sortedPeaks.Count;

            var clusterCount = 0;

            // Although cluster lengths are integers, this list is of type double for compatibility with MathNet.Numerics.Statistics .Max() and .Mean()
            var clusterLengths = new List<double>();

            const double CHLORINE37_DELTA = 1.99705;
            const double MZ_DELTA_TOLERANCE = 0.0005;

            var clusterIndices = new List<int>();

            // Greedy search: build clusters starting from each unused peak
            for (var i = 0; i < mzCount; i++)
            {
                if (peakUsedInCluster[i])
                    continue;

                // Try to extend a cluster starting from peak i
                clusterIndices.Clear();
                clusterIndices.Add(i);

                var currentIndex = i;

                // Look for next peak in sequence
                for (var j = i+1; j < mzCount; j++)
                {
                    if (peakUsedInCluster[j])
                        continue;

                    var delta = sortedPeaks[j].Key - sortedPeaks[currentIndex].Key;

                    // Check if this peak continues the cluster
                    if (Math.Abs(delta - CHLORINE37_DELTA) <= MZ_DELTA_TOLERANCE)
                    {
                        clusterIndices.Add(j);
                        currentIndex = j;
                    }
                    else if(delta > CHLORINE37_DELTA + MZ_DELTA_TOLERANCE)
                    {
                        // Gone too far, no point checking further peaks
                        break;
                    }
                }

                // Count as cluster if length >= 3
                if (clusterIndices.Count >= 3)
                {
                    clusterCount++;
                    clusterLengths.Add(clusterIndices.Count);

                    // Mark all peaks in this cluster as used
                    foreach (var targetIndex in clusterIndices)
                    {
                        peakUsedInCluster[targetIndex] = true;
                    }
                }
            }

            nomStats.ChlorideClusterCount = clusterCount;

            double totalPeaksInClusters;

            if (clusterLengths.Count > 0)
            {
                nomStats.ChlorideClusterMaxLength = (int)clusterLengths.Maximum();
                nomStats.ChlorideClusterMeanLength = clusterLengths.Mean();
                totalPeaksInClusters = clusterLengths.Sum();
            }
            else
            {
                nomStats.ChlorideClusterMaxLength = 0;
                nomStats.ChlorideClusterMeanLength = 0;
                totalPeaksInClusters = 0;
            }

            // Calculate percentage of peaks in chloride clusters

            nomStats.ChlorideClusterPeakCount = (int)totalPeaksInClusters;

            double totalIntensitySum;

            if (mzCount > 0)
            {
                nomStats.ChlorideClusterPeakPercent = totalPeaksInClusters / mzCount * 100.0;
                totalIntensitySum = sortedPeaks.Sum(item => item.Value);
            }
            else
            {
                nomStats.ChlorideClusterPeakPercent = 0;
                totalIntensitySum = 0;
            }

            // Calculate intensity share associated with chloride clusters

            var clusterIntensitySum = 0.0;

            for (var i = 0; i < peakUsedInCluster.Count; i++)
            {
                if (peakUsedInCluster[i])
                {
                    clusterIntensitySum += sortedPeaks[i].Value;
                }
            }

            nomStats.ChlorideClusterIntensitySum = clusterIntensitySum;

            if (totalIntensitySum > 0)
            {
                nomStats.ChlorideClusterIntensityPercent = clusterIntensitySum / totalIntensitySum * 100.0;
            }
            else
            {
                nomStats.ChlorideClusterIntensityPercent = 0;
            }
        }

        /// <summary>
        /// Calculate isotopologue metrics (C13 and Chloride37)
        /// </summary>
        /// <remarks>
        /// Detection criteria:
        /// C13: m/z difference of 1.003355 ± 0.0005
        /// Cl37: m/z difference of 1.99705 ± 0.0005
        /// </remarks>
        /// <param name="nomStats">Natural organic matter stats</param>
        /// <param name="massSpectrum">List of m/z and intensity key/value pairs</param>
        private void CalculateIsotopologueMetrics(NaturalOrganicMatterStats nomStats, IEnumerable<KeyValuePair<double, double>> massSpectrum)
        {
            const double MAX_DELTA_MZ = 2.5;

            // Assure that the m/z list is sorted
            var sortedMZs = new List<KeyValuePair<double, double>>();

            sortedMZs.AddRange(from item in massSpectrum orderby item.Key select item);

            nomStats.C13PairCount = 0;
            nomStats.Cl37PairCount = 0;
            nomStats.C13PairIntensitySum = 0.0;
            nomStats.Cl37PairIntensitySum = 0.0;

            for (var i = 0; i < sortedMZs.Count; i++)
            {
                for (var j = i + 1; j < sortedMZs.Count && (sortedMZs[j].Key - sortedMZs[i].Key) <= MAX_DELTA_MZ; j++)
                {
                    var delta = sortedMZs[j].Key - sortedMZs[i].Key;

                    // Check for C13 isotopologue (1.003355 ± 0.0005)
                    if (Math.Abs(delta - 1.003355) <= 0.0005)
                    {
                        nomStats.C13PairCount++;

                        //  Use minimum intensity of the pair for weighting
                        nomStats.C13PairIntensitySum += Math.Min(sortedMZs[i].Value, sortedMZs[j].Value);
                    }

                    // Check for Chlorine37 isotopologue (1.99705 ± 0.0005)
                    if (Math.Abs(delta - 1.99705) <= 0.0005)
                    {
                        nomStats.Cl37PairCount++;
                        nomStats.Cl37PairIntensitySum += Math.Min(sortedMZs[i].Value, sortedMZs[j].Value);
                    }
                }
            }
        }

        /// <summary>
        /// Calculate organic and inorganic metrics
        /// </summary>
        /// <param name="nomStats">Natural organic matter stats</param>
        /// <param name="massSpectrum">List of m/z and intensity key/value pairs</param>
        private void CalculateOrganicInorganicMetrics(NaturalOrganicMatterStats nomStats, List<KeyValuePair<double, double>> massSpectrum)
        {
            var organicMZs = new List<double>();
            var inorganicMZs = new List<double>();

            var organicIntensitySum = 0.0;
            var inorganicIntensitySum = 0.0;

            foreach (var item in massSpectrum)
            {
                var massDefect = item.Key % 1;

                if (massDefect is >= 0.0 and <= 0.4)
                {
                    organicMZs.Add(item.Key);
                    organicIntensitySum += item.Value;
                }

                if (massDefect is >= 0.6 and < 1.0)
                {
                    inorganicMZs.Add(item.Key);
                    inorganicIntensitySum += item.Value;
                }
            }

            nomStats.OrganicCount = organicMZs.Count;
            nomStats.InorganicCount = inorganicMZs.Count;

            nomStats.OrganicIntensitySum = organicIntensitySum;
            nomStats.InorganicIntensitySum = inorganicIntensitySum;
        }

        private void ComputeMedianValues(SortedDictionary<int, NaturalOrganicMatterStats> nomStatsByScan)
        {
            var mzIonCounts = nomStatsByScan.Values.Select(item => item.MzIonCount).Select(dummy => (double)dummy).ToList();
            NOMStats.MzIonCount = (int)mzIonCounts.Median();

            var mzMedians = nomStatsByScan.Values.Select(item => item.MzMedian).ToList();
            NOMStats.MzMedian = (int)mzMedians.Median();

            var mzSkews = nomStatsByScan.Values.Select(item => item.MzSkew).ToList();
            NOMStats.MzSkew = (int)mzSkews.Median();

            var mzKurtosis = nomStatsByScan.Values.Select(item => item.MzKurtosis).ToList();
            NOMStats.MzKurtosis = (int)mzKurtosis.Median();

            var organicCounts = nomStatsByScan.Values.Select(item => item.OrganicCount).Select(dummy => (double)dummy).ToList();
            NOMStats.OrganicCount = (int)organicCounts.Median();

            var organicIntensitySums = nomStatsByScan.Values.Select(item => item.OrganicIntensitySum).ToList();
            NOMStats.OrganicIntensitySum = (int)organicIntensitySums.Median();

            var inorganicCounts = nomStatsByScan.Values.Select(item => item.InorganicCount).Select(dummy => (double)dummy).ToList();
            NOMStats.InorganicCount = (int)inorganicCounts.Median();

            var inorganicIntensitySums = nomStatsByScan.Values.Select(item => item.InorganicIntensitySum).ToList();
            NOMStats.InorganicIntensitySum = (int)inorganicIntensitySums.Median();

            var c13Counts = nomStatsByScan.Values.Select(item => item.C13PairCount).Select(dummy => (double)dummy).ToList();
            NOMStats.C13PairCount = (int)c13Counts.Median();

            var c13IntensitySums = nomStatsByScan.Values.Select(item => item.C13PairIntensitySum).ToList();
            NOMStats.C13PairIntensitySum = (int)c13IntensitySums.Median();

            var chlorine37Counts = nomStatsByScan.Values.Select(item => item.Cl37PairCount).Select(dummy => (double)dummy).ToList();
            NOMStats.Cl37PairCount = (int)chlorine37Counts.Median();

            var chlorine37IntensitySums = nomStatsByScan.Values.Select(item => item.Cl37PairIntensitySum).ToList();
            NOMStats.Cl37PairIntensitySum = (int)chlorine37IntensitySums.Median();

            var chlorideClusterCounts = nomStatsByScan.Values.Select(item => item.ChlorideClusterCount).Select(dummy => (double)dummy).ToList();
            NOMStats.ChlorideClusterCount = (int)chlorideClusterCounts.Median();

            var chlorideClusterMaxLengths = nomStatsByScan.Values.Select(item => item.ChlorideClusterMaxLength).Select(dummy => (double)dummy).ToList();
            NOMStats.ChlorideClusterMaxLength = (int)chlorideClusterMaxLengths.Median();

            var chlorideClusterMeanLengths = nomStatsByScan.Values.Select(item => item.ChlorideClusterMeanLength).ToList();
            NOMStats.ChlorideClusterMeanLength = (int)chlorideClusterMeanLengths.Median();

            var chlorideClusterPeaksTotals = nomStatsByScan.Values.Select(item => item.ChlorideClusterPeakCount).Select(dummy => (double)dummy).ToList();
            NOMStats.ChlorideClusterPeakCount = (int)chlorideClusterPeaksTotals.Median();

            var chlorideClusterPeaksPercents = nomStatsByScan.Values.Select(item => item.ChlorideClusterPeakPercent).ToList();
            NOMStats.ChlorideClusterPeakPercent = (int)chlorideClusterPeaksPercents.Median();

            var chlorideClusterIntensityTotals = nomStatsByScan.Values.Select(item => item.ChlorideClusterIntensitySum).ToList();
            NOMStats.ChlorideClusterIntensitySum = (int)chlorideClusterIntensityTotals.Median();

            var chlorideClusterIntensityPercents = nomStatsByScan.Values.Select(item => item.ChlorideClusterIntensityPercent).ToList();
            NOMStats.ChlorideClusterIntensityPercent = (int)chlorideClusterIntensityPercents.Median();
        }

        /// <summary>
        /// Compute NOM stats
        /// </summary>
        /// <param name="scanNumber">scan number</param>
        /// <param name="massSpectrum">List of m/z and intensity key/value pairs</param>
        /// <param name="nomStats">Output: Natural organic matter stats</param>
        /// <returns>True if successful, false if an error</returns>
        private bool ComputeStats(int scanNumber, List<KeyValuePair<double, double>> massSpectrum, out NaturalOrganicMatterStats nomStats)
        {
            nomStats = new NaturalOrganicMatterStats(scanNumber);

            try
            {
                CalculateBasicStats(nomStats, massSpectrum);
                CalculateOrganicInorganicMetrics(nomStats, massSpectrum);
                CalculateIsotopologueMetrics(nomStats, massSpectrum);
                CalculateChlorideClusterMetrics(nomStats, massSpectrum);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorEvent(string.Format("Error in NaturalOrganicMatterStatsProcessor.ComputeStats for scan {0}", scanNumber), ex);
                return false;
            }
        }

        /// <summary>
        /// Creates XML summarizing the data in nomStats
        /// </summary>
        /// <param name="datasetName">Dataset Name</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="nomStats">Natural organic matter stats</param>
        /// <returns>XML (as string)</returns>
        public string CreateNOMStatsXML(
            string datasetName,
            int datasetId,
            NaturalOrganicMatterStats nomStats)
        {
            try
            {
                if (nomStats == null)
                {
                    OnErrorEvent("nomStats is null; unable to continue in CreateNOMStatsXML");
                    return string.Empty;
                }

                var xmlSettings = new XmlWriterSettings
                {
                    CheckCharacters = true,
                    Indent = true,
                    IndentChars = "  ",
                    Encoding = Encoding.UTF8,
                    CloseOutput = false        // Do not close output automatically so that the MemoryStream can be read after the XmlWriter has been closed
                };

                // Cache the XML using a MemoryStream.  Here, the stream encoding is set by the XmlWriter
                // and so you see the attribute encoding="UTF-8" in the opening XML declaration encoding
                // (since we used xmlSettings.Encoding = Encoding.UTF8)
                //
                var memStream = new MemoryStream();
                var writer = XmlWriter.Create(memStream, xmlSettings);

                writer.WriteStartDocument(true);

                // Write the beginning of the "Root" element.
                writer.WriteStartElement("NOMStats");

                writer.WriteStartElement("Dataset");

                if (datasetId > 0)
                {
                    writer.WriteAttributeString("DatasetID", datasetId.ToString());
                }
                writer.WriteString(datasetName);
                writer.WriteEndElement();       // Dataset EndElement

                if (Options.IncludeMetricDescriptionsInNOMStatsXML)
                {
                    writer.WriteStartElement("metric_labels");
                    writer.WriteElementString("intrinsic_c13_pair_count", "C13 pair count");
                    writer.WriteElementString("intrinsic_c13_pair_intensity_sum", "C13 pair intensity");
                    writer.WriteElementString("intrinsic_c13_to_cl37_pair_intensity_ratio", "C13/Cl37 intensity ratio");
                    writer.WriteElementString("intrinsic_c13_to_cl37_pair_ratio", "C13/Cl37 pair ratio");
                    writer.WriteElementString("intrinsic_chloride_cluster_count", "Chloride cluster count");
                    writer.WriteElementString("intrinsic_chloride_cluster_intensity_percent", "Chloride cluster intensity %");
                    writer.WriteElementString("intrinsic_chloride_cluster_intensity_sum", "Chloride cluster intensity");
                    writer.WriteElementString("intrinsic_chloride_cluster_max_length", "Max chloride cluster length");
                    writer.WriteElementString("intrinsic_chloride_cluster_mean_length", "Mean chloride cluster length");
                    writer.WriteElementString("intrinsic_chloride_cluster_peak_count", "Chloride cluster peaks");
                    writer.WriteElementString("intrinsic_chloride_cluster_peak_percent", "Chloride cluster peaks %");
                    writer.WriteElementString("intrinsic_cl37_pair_count", "Cl37 pair count");
                    writer.WriteElementString("intrinsic_cl37_pair_intensity_sum", "Cl37 pair intensity");
                    writer.WriteElementString("intrinsic_inorganic_count", "Inorganic count");
                    writer.WriteElementString("intrinsic_inorganic_intensity_sum", "Inorganic intensity");
                    writer.WriteElementString("intrinsic_mz_kurtosis", "m/z kurtosis");
                    writer.WriteElementString("intrinsic_mz_median", "Median m/z");
                    writer.WriteElementString("intrinsic_mz_skewness", "m/z skewness");
                    writer.WriteElementString("intrinsic_organic_count", "Organic count");
                    writer.WriteElementString("intrinsic_organic_intensity_sum", "Organic intensity");
                    writer.WriteElementString("intrinsic_organic_to_inorganic_count_ratio", "Organic/Inorganic count ratio");
                    writer.WriteElementString("intrinsic_organic_to_inorganic_intensity_ratio", "Organic/Inorganic intensity ratio");
                    writer.WriteElementString("intrinsic_peak_count", "Peak count");

                    writer.WriteEndElement();       // metric_labels
                }

                writer.WriteStartElement("metrics");
                writer.WriteElementString("intrinsic_c13_pair_count", nomStats.C13PairCount.ToString());
                writer.WriteElementString("intrinsic_c13_pair_intensity_sum", nomStats.C13PairIntensitySum.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("intrinsic_c13_to_cl37_pair_intensity_ratio", StringUtilities.DblToString(nomStats.C13ToCl37PairIntensityRatio, 5));
                writer.WriteElementString("intrinsic_c13_to_cl37_pair_ratio", StringUtilities.DblToString(nomStats.C13ToCl37PairRatio, 5));
                writer.WriteElementString("intrinsic_chloride_cluster_count", nomStats.ChlorideClusterCount.ToString());
                writer.WriteElementString("intrinsic_chloride_cluster_intensity_percent", StringUtilities.DblToString(nomStats.ChlorideClusterIntensityPercent, 4));
                writer.WriteElementString("intrinsic_chloride_cluster_intensity_sum", nomStats.ChlorideClusterIntensitySum.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("intrinsic_chloride_cluster_max_length", nomStats.ChlorideClusterMaxLength.ToString());
                writer.WriteElementString("intrinsic_chloride_cluster_mean_length", StringUtilities.DblToString(nomStats.ChlorideClusterMeanLength, 4));
                writer.WriteElementString("intrinsic_chloride_cluster_peak_count", nomStats.ChlorideClusterPeakCount.ToString());
                writer.WriteElementString("intrinsic_chloride_cluster_peak_percent", StringUtilities.DblToString(nomStats.ChlorideClusterPeakPercent, 4));
                writer.WriteElementString("intrinsic_cl37_pair_count", nomStats.Cl37PairCount.ToString());
                writer.WriteElementString("intrinsic_cl37_pair_intensity_sum", nomStats.Cl37PairIntensitySum.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("intrinsic_inorganic_count", nomStats.InorganicCount.ToString());
                writer.WriteElementString("intrinsic_inorganic_intensity_sum", nomStats.InorganicIntensitySum.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("intrinsic_mz_kurtosis", StringUtilities.DblToString(nomStats.MzKurtosis, 5));
                writer.WriteElementString("intrinsic_mz_median", StringUtilities.DblToString(nomStats.MzMedian, 4));
                writer.WriteElementString("intrinsic_mz_skewness", StringUtilities.DblToString(nomStats.MzSkew, 5));
                writer.WriteElementString("intrinsic_organic_count", nomStats.OrganicCount.ToString());
                writer.WriteElementString("intrinsic_organic_intensity_sum", nomStats.OrganicIntensitySum.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("intrinsic_organic_to_inorganic_count_ratio", StringUtilities.DblToString(nomStats.OrganicToInorganicCountRatio, 5));
                writer.WriteElementString("intrinsic_organic_to_inorganic_intensity_ratio", StringUtilities.DblToString(nomStats.OrganicToInorganicIntensityRatio, 5));
                writer.WriteElementString("intrinsic_peak_count", nomStats.MzIonCount.ToString());

                writer.WriteEndElement();       // metrics

                writer.WriteEndElement();  // End the "Root" element (NOMStats)

                writer.WriteEndDocument(); // End the document

                writer.Close();

                // Now Rewind the memory stream and output as a string
                memStream.Position = 0;
                var reader = new StreamReader(memStream);

                // Return the XML as text
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error in CreateNOMStatsXML", ex);
            }

            // This code will only be reached if an exception occurs
            return string.Empty;
        }
    }
}
