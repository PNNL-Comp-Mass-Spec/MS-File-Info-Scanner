using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PRISM;
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
            RegisterEvents(classifier);

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

            Console.WriteLine("Total spectra:     {0}", classifier.TotalSpectra);
            Console.WriteLine("Total MS1 spectra: {0}", classifier.TotalMS1Spectra);
            Console.WriteLine("Total MSn spectra: {0}", classifier.TotalMSnSpectra);

            Console.WriteLine("Total Centroided spectra:     {0}", classifier.CentroidedSpectra);
            Console.WriteLine("Total Centroided MS1 spectra: {0}", classifier.CentroidedMS1Spectra);
            Console.WriteLine("Total Centroided MSn spectra: {0}", classifier.CentroidedMSnSpectra);

            Console.WriteLine("Total Centroided MS1 spectra classified as profile: {0}", classifier.CentroidedMS1SpectraClassifiedAsProfile);
            Console.WriteLine("Total Centroided MSn spectra classified as profile: {0}", classifier.CentroidedMSnSpectraClassifiedAsProfile);

            Console.WriteLine("Fraction centroided:     {0}", classifier.FractionCentroided);
            Console.WriteLine("Fraction centroided MSn: {0}", classifier.FractionCentroidedMSn);

            Assert.AreEqual(20, classifier.TotalSpectra, "Total spectra");
            Assert.AreEqual(4, classifier.TotalMS1Spectra, "Total MS1 spectra");
            Assert.AreEqual(16, classifier.TotalMSnSpectra, "Total MSn spectra");

            Assert.AreEqual(17, classifier.CentroidedSpectra, "Centroided spectra");
            Assert.AreEqual(1, classifier.CentroidedMS1Spectra, "Centroided MS1 spectra");
            Assert.AreEqual(16, classifier.CentroidedMSnSpectra, "Centroided MSn spectra");

            Assert.AreEqual(1, classifier.CentroidedMS1SpectraClassifiedAsProfile, "Centroided MS1 spectra classified as profile");
            Assert.AreEqual(0, classifier.CentroidedMSnSpectraClassifiedAsProfile, "Centroided MSn spectra classified as profile");

            Assert.AreEqual(0.85, classifier.FractionCentroided, 0.001, "Fraction centroided");
            Assert.AreEqual(1, classifier.FractionCentroidedMSn, 0.001, "Fraction centroided MSn");

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

        [Test]
        public void TestSparseCentroidSpectrum1()
        {
            var classifier = new clsSpectrumTypeClassifier();
            RegisterEvents(classifier);

            // This sparse data has a median ppm difference of 49.6 ppm, but when we parse the data by region, 75% of the regions appear centroided
            // Thus, it's flagged as centroid
            var lstMZs = new List<double>
            {
                113.9900742, 119.1407013, 122.9798279, 125.4036942, 129.7117462, 132.8143463, 141.4908905, 148.0389099,
                148.0714874, 148.0860596, 148.1080933, 148.1117096, 148.1154633, 148.1191559, 148.1265259, 148.1300659,
                148.1338043, 148.1374664, 148.1412659, 148.1447449, 148.148468, 148.1521454, 148.155838, 148.1594086,
                148.1631317, 148.1666412, 148.1705017, 148.1739807, 148.1781464, 148.1817322, 148.1938324, 148.1973724,
                148.2015228, 148.2051086, 148.2089386, 148.2125397, 148.2161713, 148.2197418, 148.2233276, 148.2272797,
                148.2310028, 148.2345123, 148.2382813, 148.2418213, 148.2493134, 148.2566376, 148.2639771, 148.2897949,
                148.3409271, 178.2723236, 178.2826233, 178.6445923, 187.3162842, 264.3228149, 294.0275574, 294.9516296,
                325.8938293, 344.7498779, 424.2921753, 461.8319702, 512.5773926, 635.0739746, 829.9396973, 920.074585,
                1084.942871, 1088.478516, 1164.5896
            };

            classifier.CheckSpectrum(lstMZs, 2, false);

            Assert.AreEqual(1, classifier.CentroidedMSnSpectra, "Centroided spectrum was classified as profile mode");
        }

        [Test]
        public void TestSparseCentroidSpectrum2()
        {
            var classifier = new clsSpectrumTypeClassifier();
            RegisterEvents(classifier);

            // This sparse data has a median ppm difference of 274 ppm
            // Thus, it's flagged as centroid
            var lstMZs = new List<double>
            {
                110.4047394, 111.1819611, 112.7316971, 123.9437485, 129.8884735, 137.3031464, 148.0263062, 148.0890045,
                148.0963593, 148.1039429, 148.1112366, 148.1150665, 148.1223602, 148.1260986, 148.1296997, 148.1335297,
                148.1370697, 148.1444855, 148.1482086, 148.1519165, 148.1556244, 148.1592255, 148.1629028, 148.1665039,
                148.1705627, 148.1740417, 148.1782227, 148.181839, 148.1941071, 148.1976929, 148.2018738, 148.205368,
                148.2093964, 148.2127533, 148.2166901, 148.2202911, 148.223938, 148.2276917, 148.2315369, 148.2388916,
                148.2462311, 148.2500153, 148.25737, 148.2612, 148.3018188, 148.3425751, 177.3431854, 202.2207336,
                223.0635986, 225.043045, 249.2411346, 274.8935547, 290.9095459, 292.9042053, 299.0614929, 310.9147339,
                314.8565369, 316.8493042, 324.3527832, 334.8607178, 350.8755493, 352.8709106, 356.0701904, 366.8887329,
                368.8859558, 370.8814697, 371.0762634, 371.8818359, 379.9917297, 384.8976135, 386.8973389, 388.8239136,
                388.8444519, 388.8919983, 388.9222717, 388.9797058, 406.8329773, 424.8470764, 442.8114624, 442.854248,
                512.204834, 547.5274658, 599.2316895, 628.8311768, 786.753479
            };

            classifier.CheckSpectrum(lstMZs, 2, false);

            Assert.AreEqual(1, classifier.CentroidedMSnSpectra, "Centroided spectrum was classified as profile mode");
        }

        [Test]
        public void TestSparseCentroidSpectrum3()
        {
            var classifier = new clsSpectrumTypeClassifier();
            RegisterEvents(classifier);

            // This sparse data has a median ppm difference of 22752 ppm
            // Thus, it's flagged as centroid
            var lstMZs = new List<double>
            {
                110.0714111, 114.2822266, 114.9388733, 117.6159668, 121.64431, 129.1021881, 136.0098724, 141.161087,
                145.532074, 169.3096466, 170.1072998, 175.1191254, 178.0537567, 178.8438416, 180.1127777, 193.340744,
                195.0873871, 206.3152771, 211.1187592, 222.0874786, 227.779129, 228.1337585, 234.8431702, 236.9946747,
                238.9276123, 239.1137848, 253.1049042, 269.9788818, 276.1687012, 288.8218079, 291.1661377, 321.663208,
                350.1741333, 352.1975098, 352.6270142, 375.0958252, 403.9938965, 406.191925, 406.7169189, 422.2680664,
                433.9514771, 442.3539429, 456.2511597, 466.2398987, 467.9884949, 468.0595398, 477.2394714, 481.249939,
                496.6159668, 496.9494934, 505.2612915, 507.6798401, 509.4258728, 527.3466187, 530.0685425, 530.2241821,
                530.2980957, 530.6329346, 566.9076538, 579.3247681, 642.3215332, 653.0263062, 675.8909912, 678.394104,
                699.3411865, 700.3435059, 708.2324219, 777.4619141, 812.4230957, 911.4946899, 915.8271484, 1010.560913,
                1392.917847, 1472.0177
            };

            classifier.CheckSpectrum(lstMZs, 2, false);

            Assert.AreEqual(1, classifier.CentroidedMSnSpectra, "Centroided spectrum was classified as profile mode");
        }

        [Test]
        public void TestSparseCentroidSpectrum4()
        {
            var classifier = new clsSpectrumTypeClassifier();
            RegisterEvents(classifier);

            // This sparse data has a median ppm difference of 44.9 ppm, but when we parse the data by region, 66% of the regions appear centroided
            // Thus, it's flagged as centroid
            var lstMZs = new List<double>
            {
                110.5883331, 112.3255081, 115.0596161, 115.0757828, 115.3761597, 116.6849365, 122.8492508, 147.8411102,
                148.0501556, 148.0543976, 148.0631561, 148.0720978, 148.0763397, 148.0809784, 148.085495, 148.0896912,
                148.10289, 148.1071625, 148.1113586, 148.1158447, 148.1201935, 148.1246643, 148.1289063, 148.1334839,
                148.1378479, 148.1420135, 148.1463928, 148.1507263, 148.1552887, 148.1595612, 148.1640625, 148.1682892,
                148.1726379, 148.1774445, 148.181839, 148.196167, 148.2009735, 148.2053528, 148.2099915, 148.2140503,
                148.2185974, 148.2228851, 148.2275696, 148.2317352, 148.2360077, 148.2404175, 148.244873, 148.2492828,
                148.2537537, 148.2579346, 148.2623291, 148.2669373, 148.271286, 148.2755737, 148.2800446, 148.2888031,
                148.2971497, 148.3062897, 148.3150177, 148.3278046, 148.4071655, 148.4113922, 148.468338, 177.1508636,
                191.2035217, 192.5015717, 198.7490082, 255.460083, 272.8143921, 280.9107056, 298.9213562, 306.8990784,
                331.3713074, 336.8422852, 340.8169556, 340.8997803, 353.4999695, 358.8260803, 358.8886719, 376.803009,
                376.8201294, 376.8374634, 376.854187, 376.8996277, 390.7409668, 394.8474426, 395.8630066, 485.6352844,
                612.6697998
            };

            classifier.CheckSpectrum(lstMZs, 2, false);

            Assert.AreEqual(1, classifier.CentroidedMSnSpectra, "Centroided spectrum was classified as profile mode");
        }

        [Test]
        public void TestProfileSpectrum()
        {
            var classifier = new clsSpectrumTypeClassifier();
            RegisterEvents(classifier);

            var lstProfileMZs = new List<double>
            {
                346.5204, 346.5221, 346.5239, 346.5256, 353.5831, 353.5849, 353.5867, 353.5885, 353.5902, 353.5920, 353.5938, 353.5956, 353.5974,
                353.5992, 353.6009, 353.6027, 353.6045, 353.6063, 354.0833, 354.0851, 354.0869, 354.0887, 354.0905, 354.0922, 354.0940, 354.0958,
                354.0976, 354.0994, 354.1012, 354.1030, 354.1048, 354.1066, 354.1083, 354.1101, 354.1495, 354.1513, 354.1531, 354.1548, 354.1566,
                354.1584, 354.1602, 354.1620, 354.1638, 354.1655, 354.1673, 354.1691, 354.1709, 355.0312, 355.0330, 355.0348, 355.0365, 355.0383,
                355.0401, 355.0419, 355.0437, 355.0455, 355.0473, 355.0491, 355.0509, 355.0528, 355.0546, 355.0564, 355.0582, 355.0600, 355.0618,
                355.0635, 355.0653, 355.0671, 355.0689, 355.0707, 355.0725, 355.0743, 355.0761, 355.0779, 355.0797, 355.0815, 355.0833, 355.0851,
                355.0869, 355.0887, 355.0905, 355.0923, 355.2522, 355.2540, 355.2558, 355.2576, 355.2594, 355.2612, 355.2630, 355.2648, 355.2666,
                355.2684, 355.2702, 355.2720, 355.2738, 355.2755, 355.2773, 355.2791, 356.0587, 356.0605, 356.0623, 356.0641, 356.0659, 356.0677,
                356.0695, 413.0445, 413.0467, 413.0490, 413.0512, 413.0535, 413.0557, 413.0580, 413.0603, 413.0625, 413.0641, 413.0663, 413.0686,
                413.0708, 413.0731, 413.0753, 413.0776, 413.0798, 413.0821, 413.0843, 413.0866, 413.0888, 413.0911, 413.0933, 413.0956, 413.0979,
                413.1001, 413.1024, 413.1046, 413.1069, 413.1091, 413.1114, 413.2421, 413.2444, 413.2466, 413.2489, 413.2511, 413.2534, 413.2557,
                413.2579, 413.2602, 413.2624, 413.2647, 413.2669, 413.2692, 413.5399, 413.5422, 413.5444, 413.5467, 413.5489, 413.5512, 413.5535,
                413.5557, 413.5580, 413.5602, 413.5625, 413.5648, 413.5670, 413.5693, 414.0392, 414.0415, 414.0438, 414.0460, 414.0483, 414.0505,
                414.0528, 414.0551, 414.0573, 414.0596, 414.0618, 414.0641, 414.0664, 414.0686, 414.0709, 414.0732, 414.0754, 414.0776, 414.0799,
                414.0822, 414.0844, 414.0867, 414.0890, 414.0912, 414.0935, 414.0957, 414.2541, 414.2564, 414.2586, 414.2609, 414.2632, 414.2654,
                414.2677, 414.2699, 414.2722, 414.2745, 414.2764, 414.2787, 414.2810, 414.2832, 414.5436, 414.5459, 414.5482, 414.5504, 414.5527,
                414.5550, 414.5572, 414.5595, 414.5618, 414.5640, 414.5663, 414.5686, 414.5708, 414.5731, 414.5767, 414.5789, 414.5812, 414.5835,
                415.0460, 415.0483, 415.0506, 415.0529, 415.0551, 415.0574, 415.0597, 628.0967, 628.1009, 628.1051, 628.1093, 628.1136, 628.7098,
                628.7140, 628.7182, 628.7224, 628.7267, 628.7309, 628.7351, 628.7394, 628.7436, 628.7478, 628.7521, 628.7563, 628.7605, 629.0907,
                629.0950, 629.0992, 629.1034, 629.1077, 629.1119, 629.1161, 629.1204, 629.1246, 629.1289, 629.1331, 629.1373, 629.1416, 635.3198,
                635.3241, 635.3284, 635.3327, 635.3370, 635.3413, 635.3456, 635.3499, 635.3542, 635.3585, 635.3628, 635.3671, 1010.1302, 1010.1389,
                1010.1475, 1010.1561, 1010.1647, 1010.1733, 1010.1798, 1010.1884, 1010.1970, 1010.2057, 1011.0853, 1011.0939, 1011.1025, 1011.1112,
                1011.1198, 1011.1284, 1011.1370, 1011.1457, 1011.1543, 1011.1629, 1011.1716, 1646.7466, 1646.7645, 1646.7824, 1646.8004, 1646.8183,
                1646.8363, 1646.8542, 1646.8721, 1646.8901, 1646.9080, 1650.4656, 1650.4836, 1650.5016, 1650.5196, 1650.5376, 1650.5556, 1650.5736,
                1650.5916, 1650.6096, 1650.6276, 1650.6456, 1650.6636, 1650.6816, 1650.6996, 1757.1266, 1757.1464, 1757.1661, 1757.1859, 1757.2057,
                1757.2254, 1757.2452, 1757.2650, 1757.2848, 1757.3045, 1757.3243, 1757.3441, 1757.3638, 1796.5864, 1796.6068, 1796.6272, 1796.6477,
                1796.6681, 1796.6886, 1796.7090, 1796.7294, 1796.7499, 1796.7703, 1796.7908, 1796.8112, 1796.8317, 1818.1372, 1818.1580, 1818.1788,
                1818.1996
            };

            classifier.CheckSpectrum(lstProfileMZs, 1, false);

            Assert.AreEqual(0, classifier.CentroidedMS1Spectra, "Profile mode spectrum was classified as centroided");
            Assert.AreEqual(1, classifier.TotalMS1Spectra, "MS1 spectrum count is not 1");
            Assert.AreEqual(0, classifier.TotalMSnSpectra, "MSn spectrum count is not 0");
        }

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="classifier"></param>
        private void RegisterEvents(clsSpectrumTypeClassifier classifier)
        {
            classifier.RaiseDebugEvents = true;
            classifier.DebugEvent += SourceClass_DebugEvent;
            classifier.StatusEvent += SourceClass_StatusEvent;
            classifier.ErrorEvent += SourceClass_ErrorEvent;
            classifier.WarningEvent += SourceClass_WarningEvent;
        }


        private void SourceClass_DebugEvent(string message)
        {
            ConsoleMsgUtils.ShowDebug(message);
        }

        private void SourceClass_StatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private void SourceClass_WarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }

        private void SourceClass_ErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

    }
}
