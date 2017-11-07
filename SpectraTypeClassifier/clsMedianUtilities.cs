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
    public class clsMedianUtilities
    {

        private readonly Random mRandom;
        public enum eEventListCountBehaviorType
        {
            ReportMidpointAverage = 0,
            ReportNearest = 1
        }

        public eEventListCountBehaviorType EvenNumberedListCountBehavior { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsMedianUtilities()
        {
            mRandom = new Random();
            EvenNumberedListCountBehavior = eEventListCountBehaviorType.ReportMidpointAverage;
        }

        /// <summary>
        /// Partitions the given list around a pivot element such that all elements on left of pivot are less than or equal to pivot
        /// and the ones at thr right are greater than pivot. This method can be used for sorting, N-order statistics such as
        /// as median finding algorithms.
        /// Pivot is selected ranodmly if random number generator is supplied else its selected as last element in the list.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
        /// </summary>
        private int Partition(IList<double> lstData, int startIndex, int endIndex, Random oRandom)
        {
            if (oRandom != null)
            {
                Swap(lstData, endIndex, oRandom.Next(startIndex, endIndex));
            }

            var pivot = lstData[endIndex];
            var lastLow = startIndex - 1;
            for (var i = startIndex; i <= endIndex - 1; i++)
            {
                if (lstData[i].CompareTo(pivot) <= 0)
                {
                    lastLow += 1;
                    Swap(lstData, i, lastLow);
                }
            }
            lastLow += 1;
            Swap(lstData, endIndex, lastLow);
            return lastLow;

        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        public double NthOrderStatistic(IList<double> lstData, int n)
        {
            return NthOrderStatistic(lstData, n, 0, lstData.Count - 1, mRandom);
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        public double NthOrderStatistic(IList<double> lstData, int n, Random oRandom)
        {
            return NthOrderStatistic(lstData, n, 0, lstData.Count - 1, oRandom);
        }

        /// <summary>
        /// Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
        /// Note: specified list will be mutated in the process.
        /// Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
        /// </summary>
        private double NthOrderStatistic(IList<double> lstData, int n, int startIndex, int endIndex, Random oRandom)
        {
            while (true)
            {
                var pivotIndex = Partition(lstData, startIndex, endIndex, oRandom);
                if (pivotIndex == n)
                {
                    return lstData[pivotIndex];
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
        public void Swap(IList<double> lstData, int i, int j)
        {
            if (i == j)
            {
                // Swap is not required
                return;
            }
            var temp = lstData[i];
            lstData[i] = lstData[j];
            lstData[j] = temp;
        }

        /// <summary>
        /// Compute the median of the values in lstData
        /// </summary>
        /// <remarks>lstData will be mutated (changed) when determining the median</remarks>
        public double Median(IList<double> lstData)
        {

            if (lstData == null || lstData.Count < 1)
            {
                // List is empty
                return 0;
            }

            if (lstData.Count <= 1)
            {
                // Only 1 item; the median is the value
                return lstData.First();
            }

            var midPoint1 = Convert.ToInt32(Math.Floor((lstData.Count - 1) / 2.0));
            var median1 = NthOrderStatistic(lstData, midPoint1);

            if (lstData.Count % 2 > 0 || EvenNumberedListCountBehavior == eEventListCountBehaviorType.ReportNearest)
            {
                return median1;
            }

            // List contains an even number of elements
            var midPoint2 = Convert.ToInt32(lstData.Count / 2);
            var median2 = NthOrderStatistic(lstData, midPoint2);

            // Median is the average of the two middle points
            return (median1 + median2) / 2.0;

        }

        /// <summary>
        /// Compute the median of a subset of lstData, selected using getValue
        /// </summary>
        public double Median(IEnumerable<double> lstData, Func<double, double> getValue)
        {
            var lstDataSubset = lstData.Select(getValue).ToList();
            return Median(lstDataSubset);
        }

    }
}
