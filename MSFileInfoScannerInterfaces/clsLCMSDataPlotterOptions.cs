
namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// Options class for clsLCMSDatPlotter
    /// </summary>
    /// <remarks></remarks>
    public class clsLCMSDataPlotterOptions
    {

        public const int DEFAULT_MAX_POINTS_TO_PLOT = 200000;

        public const int DEFAULT_MIN_POINTS_PER_SPECTRUM = 2;

        public const int DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR = 10;
        public const float DEFAULT_MZ_RESOLUTION = 0.4f;

        public const float DEFAULT_MIN_INTENSITY = 0;
        protected const string DEFAULT_MS1_PLOT_TITLE = "MS Spectra";

        protected const string DEFAULT_MS2_PLOT_TITLE = "MS2 Spectra";
        public const double DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT = 12000;

        public const double DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT = 4000;
        protected int mMaxPointsToPlot;

        protected int mMinPointsPerSpectrum;
        protected float mMZResolution;

        protected float mMinIntensity;
        protected string mMS1PlotTitle;

        protected string mMS2PlotTitle;

        // The following is only used when PlottingDeisotopedData is true
        protected double mMaxMonoMass;

        #region "Properties"

        public bool DeleteTempFiles { get; set; }

        public int MaxPointsToPlot {
            get => mMaxPointsToPlot;
            set {
                if (value < 10)
                    value = 10;
                mMaxPointsToPlot = value;
            }
        }

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

        public string MS2PlotTitle {
            get => mMS2PlotTitle;
            set {
                if (string.IsNullOrEmpty(value)) {
                    value = DEFAULT_MS2_PLOT_TITLE;
                }
                mMS2PlotTitle = value;
            }
        }

        public float MZResolution {
            get => mMZResolution;
            set {
                if (value < 0)
                    value = 0;
                mMZResolution = value;
            }
        }

        public float MinIntensity {
            get => mMinIntensity;
            set {
                if (value < 0)
                    value = 0;
                mMinIntensity = value;
            }
        }

        public double MaxMonoMassForDeisotopedPlot {
            get => mMaxMonoMass;
            set {
                if (value < 100)
                    value = 100;
                mMaxMonoMass = value;
            }
        }

        public bool PlottingDeisotopedData { get; set; }

        public bool PlotWithPython { get; set; }

        /// <summary>
        /// Set to True to print out a series of 2D plots, each using a different color scheme
        /// </summary>
        public bool TestGradientColorSchemes { get; set; }

        public bool UseObservedMinScan { get; set; }

        #endregion

        public clsLCMSDataPlotterOptions Clone()
        {
            var objClone = new clsLCMSDataPlotterOptions
            {
                DeleteTempFiles = DeleteTempFiles,
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

            return objClone;

        }

        /// <summary>
        /// Constructor
        /// </summary>
        public clsLCMSDataPlotterOptions()
        {
            DeleteTempFiles = true;

            mMaxPointsToPlot = DEFAULT_MAX_POINTS_TO_PLOT;
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
