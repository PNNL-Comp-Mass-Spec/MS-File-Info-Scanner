
// ReSharper disable UnusedMember.Global
namespace MSFileInfoScanner.Options
{
    /// <summary>
    /// Options class for the LCMSDataPlotter
    /// </summary>
    public class LCMSDataPlotterOptions
    {
        // Ignore Spelling: deisotoped

        public const int DEFAULT_MAX_POINTS_TO_PLOT = 200000;

        public const int DEFAULT_MIN_POINTS_PER_SPECTRUM = 2;

        public const int DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR = 10;

        public const float DEFAULT_MZ_RESOLUTION = 0.4f;

        public const float DEFAULT_MIN_INTENSITY = 0;

        private const string DEFAULT_MS1_PLOT_TITLE = "MS Spectra";

        private const string DEFAULT_MS2_PLOT_TITLE = "MS2 Spectra";

        public const double DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT = 12000;

        public const double DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT = 4000;

        private int mMaxPointsToPlot;

        private int mMinPointsPerSpectrum;

        private float mMZResolution;

        private float mMinIntensity;

        private string mMS1PlotTitle;

        private string mMS2PlotTitle;

        // The following is only used when PlottingDeisotopedData is true
        private double mMaxMonoMass;

        #region "Properties"

        public bool DeleteTempFiles { get; set; }

        public int LCMS2DOverviewPlotDivisor { get; set; }

        /// <summary>
        /// Maximum number of points to plot
        /// </summary>
        public int MaxPointsToPlot {
            get => mMaxPointsToPlot;
            set {
                if (value < 10)
                    value = 10;
                mMaxPointsToPlot = value;
            }
        }

        /// <summary>
        /// Minimum points per spectrum
        /// </summary>
        public int MinPointsPerSpectrum {
            get => mMinPointsPerSpectrum;
            set {
                if (value < 0)
                    value = 0;
                mMinPointsPerSpectrum = value;
            }
        }

        public string MS1PlotTitle {
            get => mMS1PlotTitle;
            set {
                if (string.IsNullOrEmpty(value)) {
                    value = DEFAULT_MS1_PLOT_TITLE;
                }
                mMS1PlotTitle = value;
            }
        }

        /// <summary>
        /// MS2 plot title
        /// </summary>
        public string MS2PlotTitle {
            get => mMS2PlotTitle;
            set {
                if (string.IsNullOrEmpty(value)) {
                    value = DEFAULT_MS2_PLOT_TITLE;
                }
                mMS2PlotTitle = value;
            }
        }

        /// <summary>
        /// m/z resolution
        /// </summary>
        public float MZResolution {
            get => mMZResolution;
            set {
                if (value < 0)
                    value = 0;
                mMZResolution = value;
            }
        }

        /// <summary>
        /// Minimum intensity
        /// </summary>
        public float MinIntensity {
            get => mMinIntensity;
            set {
                if (value < 0)
                    value = 0;
                mMinIntensity = value;
            }
        }

        /// <summary>
        /// Maximum monoisotopic mass for the deisotoped plot
        /// </summary>
        public double MaxMonoMassForDeisotopedPlot {
            get => mMaxMonoMass;
            set {
                if (value < 100)
                    value = 100;
                mMaxMonoMass = value;
            }
        }

        /// <summary>
        /// Set to true when plotting deisotoped data
        /// </summary>
        public bool PlottingDeisotopedData { get; set; }

        public bool PlotWithPython { get; set; }

        /// <summary>
        /// Set to True to print out a series of 2D plots, each using a different color scheme
        /// </summary>
        public bool TestGradientColorSchemes { get; set; }

        /// <summary>
        /// True if we should use the observed minimum scan
        /// </summary>
        public bool UseObservedMinScan { get; set; }

        #endregion

        /// <summary>
        /// Clone the options
        /// </summary>
        public LCMSDataPlotterOptions Clone()
        {
            var newOptions = new LCMSDataPlotterOptions
            {
                DeleteTempFiles = DeleteTempFiles,
                LCMS2DOverviewPlotDivisor = LCMS2DOverviewPlotDivisor,
                MaxPointsToPlot = MaxPointsToPlot,
                MinPointsPerSpectrum = MinPointsPerSpectrum,
                MZResolution = MZResolution,
                MinIntensity = MinIntensity,
                MS1PlotTitle = MS1PlotTitle,
                MS2PlotTitle = MS2PlotTitle,
                PlottingDeisotopedData = PlottingDeisotopedData,
                PlotWithPython = PlotWithPython,
                UseObservedMinScan = UseObservedMinScan,
                MaxMonoMassForDeisotopedPlot = MaxMonoMassForDeisotopedPlot
            };

            return newOptions;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LCMSDataPlotterOptions() : this (new InfoScannerOptions())
        {
        }

        /// <summary>
        /// Constructor that accepts an options class
        /// </summary>
        /// <param name="options"></param>
        public LCMSDataPlotterOptions(InfoScannerOptions options)
        {
            DeleteTempFiles = true;

            LCMS2DOverviewPlotDivisor = options.LCMS2DOverviewPlotDivisor;

            mMaxPointsToPlot = options.LCMS2DMaxPointsToPlot;
            mMinPointsPerSpectrum = DEFAULT_MIN_POINTS_PER_SPECTRUM;

            mMZResolution = DEFAULT_MZ_RESOLUTION;
            mMinIntensity = DEFAULT_MIN_INTENSITY;

            mMS1PlotTitle = DEFAULT_MS1_PLOT_TITLE;
            mMS2PlotTitle = DEFAULT_MS2_PLOT_TITLE;

            mMaxMonoMass = DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT;

            PlottingDeisotopedData = false;
            PlotWithPython = false;
            UseObservedMinScan = false;
        }
    }
}
