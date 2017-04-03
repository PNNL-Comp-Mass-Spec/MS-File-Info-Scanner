using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpectraTypeClassifier;

namespace MSFileInfoScannerUnitTests
{
    [TestFixture]
    public class SpectraTypeClassifierTests
    {
        [Test]
        public void TestStats()
        {
            var classifier = new clsSpectrumTypeClassifier();

            var lstProfileMZs = new List<double>
            {
                701.76070, 701.98029, 701.99028, 702.00026, 702.01025, 702.02023, 702.03022, 702.04020, 702.05019,
                702.06017, 702.07016, 702.08014, 702.09013, 702.10012, 702.11010, 702.12009, 702.13008, 702.14007,
                702.15005, 702.16004, 702.17003, 702.18002, 702.19001, 702.19999, 702.20999, 702.21998, 702.22997,
                702.23996, 713.10317, 713.11340, 713.12362, 713.13384, 713.14407, 713.15429, 713.16451, 713.17474,
                713.18496, 713.19518, 713.20541, 713.21592, 713.22615, 713.23637, 713.24660, 716.19035, 716.20064,
                716.21093, 716.22122, 716.23151, 716.24180, 716.25209, 716.26238, 716.27267, 716.28296, 716.29325,
                716.30354, 716.31383, 716.32412
            };

            classifier.CheckSpectrum(lstProfileMZs, 1, false, clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile);

            var lstCentroidMZs = new List<double>
            {
                500.29,500.34,501.18,502.18,503.18,504.18,505.17,506.17,507.17,507.24,507.30
            };

            classifier.CheckSpectrum(lstCentroidMZs, 2, false, clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid);

            for (var scan = 1; scan < 19; scan++)
            {

                if (scan % 5 == 0)
                {
                    // Profile mode

                    var updatedProfileMZs = lstProfileMZs.Select(value => value + scan / 10.0).ToList();

                    // Purposely mis-classify the spectrum every 10 scans
                    var centroidingStatusMS1 = scan % 10 == 0
                                                ? clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid
                                                : clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile;

                    classifier.CheckSpectrum(updatedProfileMZs, 1, false, centroidingStatusMS1);

                    continue;
                }

                // Centroid mode

                var updatedCentroidMZs = lstCentroidMZs.Select(value => value + scan * 2).ToList();

                // Purposely mis-classify the spectrum every 4 scans
                // (though the SpectrumClassifier class doesn't really care if this happens)
                var centroidingStatusMS2 = scan % 4 == 0
                                            ? clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile
                                            : clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid;

                classifier.CheckSpectrum(updatedCentroidMZs, 2, false, centroidingStatusMS2);
            }

            Console.WriteLine("Total spectra:     {0}", classifier.TotalSpectra());
            Console.WriteLine("Total MS1 spectra: {0}", classifier.TotalMS1Spectra());
            Console.WriteLine("Total MSn spectra: {0}", classifier.TotalMSnSpectra());

            Console.WriteLine("Total Centroided spectra:     {0}", classifier.CentroidedSpectra());
            Console.WriteLine("Total Centroided MS1 spectra: {0}", classifier.CentroidedMS1Spectra());
            Console.WriteLine("Total Centroided MSn spectra: {0}", classifier.CentroidedMSnSpectra());

            Console.WriteLine("Total Centroided MS1 spectra classified as profile: {0}", classifier.CentroidedMS1SpectraClassifiedAsProfile());
            Console.WriteLine("Total Centroided MSn spectra classified as profile: {0}", classifier.CentroidedMSnSpectraClassifiedAsProfile());

            Console.WriteLine("Fraction centroided:     {0}", classifier.FractionCentroided());
            Console.WriteLine("Fraction centroided MSn: {0}", classifier.FractionCentroidedMSn());

            Assert.AreEqual(20, classifier.TotalSpectra(), "Total spectra");
            Assert.AreEqual(4, classifier.TotalMS1Spectra(), "Total MS1 spectra");
            Assert.AreEqual(16, classifier.TotalMSnSpectra(), "Total MSn spectra");

            Assert.AreEqual(17, classifier.CentroidedSpectra(), "Centroided spectra");
            Assert.AreEqual(1, classifier.CentroidedMS1Spectra(), "Centroided MS1 spectra");
            Assert.AreEqual(16, classifier.CentroidedMSnSpectra(), "Centroided MSn spectra");

            Assert.AreEqual(1, classifier.CentroidedMS1SpectraClassifiedAsProfile(), "Centroided MS1 spectra classified as profile");
            Assert.AreEqual(0, classifier.CentroidedMSnSpectraClassifiedAsProfile(), "Centroided MSn spectra classified as profile");

            Assert.AreEqual(0.85, classifier.FractionCentroided(), 0.001, "Fraction centroided");
            Assert.AreEqual(1, classifier.FractionCentroidedMSn(), 0.001, "Fraction centroided MSn");


        }

        [Test]
        public void TestMedian()
        {
            var medianUtilities = new clsMedianUtilities();

            var testValues = new List<double> { 5, 9, 20, 6, 8, 9.2, 9.5, 12, 15, 18, 20, 15 };

            medianUtilities.EvenNumberedListCountBehavior = clsMedianUtilities.eEventListCountBehaviorType.ReportMidpointAverage;
            var medianMidpointAverage = medianUtilities.Median(testValues);

            testValues.Sort();

            Console.WriteLine("Compute the median of the following values");
            foreach (var value in testValues)
            {
                Console.WriteLine(value);
            }

            Console.WriteLine("Median using the average of the values around the midpoint: {0}", medianMidpointAverage);
            Assert.AreEqual(10.75, medianMidpointAverage, 0.0001, "Median mode: midpoint average");

            medianUtilities.EvenNumberedListCountBehavior = clsMedianUtilities.eEventListCountBehaviorType.ReportNearest;
            var medianReportNearest = medianUtilities.Median(testValues);

            Console.WriteLine("Median using the value nearest the midpoint:                {0}", medianReportNearest);
            Assert.AreEqual(9.5, medianReportNearest, 0.0001, "Median mode: report nearest");
        }
    }
}
