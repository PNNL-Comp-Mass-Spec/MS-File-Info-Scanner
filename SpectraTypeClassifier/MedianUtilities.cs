using System;
using System.Collections.Generic;
using System.Linq;

namespace SpectraTypeClassifier
{
    /// <summary>
    /// Set of utilities for computing the median value given a list of numbers
    /// Also supports the NthOrderStatistic (return the Nth smallest item from a list)
    /// The algorithm is more efficient than performing a full sort on the list of numbers
    /// </summary>
    /// <remarks>From http://stackoverflow.com/questions/4140719/i-need-c-sharp-function-that-will-calculate-median </remarks>
    [CLSCompliant(true)]
    [Obsolete("Use MathNet.Numerics.Statistics.Statistics.Median()")]
    public class MedianUtilities
    {
        // Ignore Spelling: Corman et al

        private readonly Random mRandom;
        public enum EventListCountBehaviorType
        {
            ReportMidpointAverage = 0,
            ReportNearest = 1
        }

        public EventListCountBehaviorType EvenNumberedListCountBehavior { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public MedianUtilities()
        {
            mRandom = new Random();
            EvenNumberedListCountBehavior = EventListCountBehaviorType.ReportMidpointAverage;
        }

        /// <summary>
        /// Partitions the given list around a pivot element such that all elements on left of pivot are less than or equal to pivot
        /// and the ones at the right are greater than pivot. This method can be used for sorting, N-order statistics such as
        /// as median finding algorithms.
        /// Pivot is selected randomly if random number generator is supplied else its selected as last element in the list.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
        /// </summary>
        private int Partition(IList<double> dataPoints, int startIndex, int endIndex, Random oRandom)
        {
            if (oRandom != null)
            {
                Swap(dataPoints, endIndex, oRandom.Next(startIndex, endIndex));
            }

            var pivot = dataPoints[endIndex];
            var lastLow = startIndex - 1;
            for (var i = startIndex; i <= endIndex - 1; i++)
            {
                if (dataPoints[i].CompareTo(pivot) <= 0)
                {
                    lastLow++;
                    Swap(dataPoints, i, lastLow);
                }
            }
            lastLow++;
            Swap(dataPoints, endIndex, lastLow);
            return lastLow;
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        public double NthOrderStatistic(IList<double> dataPoints, int n)
        {
            return NthOrderStatistic(dataPoints, n, 0, dataPoints.Count - 1, mRandom);
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public double NthOrderStatistic(IList<double> dataPoints, int n, Random oRandom)
        {
            return NthOrderStatistic(dataPoints, n, 0, dataPoints.Count - 1, oRandom);
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        private double NthOrderStatistic(IList<double> dataPoints, int n, int startIndex, int endIndex, Random oRandom)
        {
            while (true)
            {
                var pivotIndex = Partition(dataPoints, startIndex, endIndex, oRandom);
                if (pivotIndex == n)
                {
                    return dataPoints[pivotIndex];
                }

                if (n < pivotIndex)
                {
                    endIndex = pivotIndex - 1;
                }
                else
                {
                    startIndex = pivotIndex + 1;
                }
            }
        }

        /// <summary>
        /// Swap two items in a list
        /// </summary>
        public void Swap(IList<double> dataPoints, int i, int j)
        {
            if (i == j)
            {
                // Swap is not required
                return;
            }
            var temp = dataPoints[i];
            dataPoints[i] = dataPoints[j];
            dataPoints[j] = temp;
        }

        /// <summary>
        /// Compute the median of the values in dataPoints
        /// </summary>
        /// <remarks>dataPoints will be mutated (changed) when determining the median</remarks>
        public double Median(IList<double> dataPoints)
        {
            var median = MathNet.Numerics.Statistics.Statistics.Median(dataPoints);
            return median;
        }

        /// <summary>
        /// Compute the median of the values in dataPoints
        /// </summary>
        /// <remarks>dataPoints will be mutated (changed) when determining the median</remarks>
        public double Median_Old(IList<double> dataPoints)
        {
            if (dataPoints == null || dataPoints.Count < 1)
            {
                // List is empty
                return 0;
            }

            if (dataPoints.Count == 1)
            {
                // Only 1 item; the median is the value
                return dataPoints[0];
            }

            var midPoint1 = Convert.ToInt32(Math.Floor((dataPoints.Count - 1) / 2.0));
            var median1 = NthOrderStatistic(dataPoints, midPoint1);

            if (dataPoints.Count % 2 > 0 || EvenNumberedListCountBehavior == EventListCountBehaviorType.ReportNearest)
            {
                return median1;
            }

            // List contains an even number of elements
            var midPoint2 = Convert.ToInt32(dataPoints.Count / 2);
            var median2 = NthOrderStatistic(dataPoints, midPoint2);

            // Median is the average of the two middle points
            return (median1 + median2) / 2.0;
        }

        /// <summary>
        /// Compute the median of a subset of dataPoints, selected using getValue
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public double Median(IEnumerable<double> dataPoints, Func<double, double> getValue)
        {
            var dataPointsSubset = dataPoints.Select(getValue).ToList();
            return Median(dataPointsSubset);
        }
    }
}
