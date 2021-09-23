
// ReSharper disable UnusedMember.Global
namespace MSFileInfoScannerInterfaces
{
    /// <summary>
    /// Options class for the LCMSDataPlotter
    /// </summary>
    public class LCMSDataPlotterOptions
    {
        // Ignore Spelling: centroiding, deisotoped

        public const int DEFAULT_MAX_CHARGE_STATE = 12;

        public const int DEFAULT_MAX_POINTS_TO_PLOT = 200000;

        public const int DEFAULT_MIN_POINTS_PER_SPECTRUM = 2;

        public const int DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR = 10;

        public const float DEFAULT_MZ_RESOLUTION = 0.4f;

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

        /// <summary>
        /// Maximum monoisotopic mass for the deisotoped plot
        /// </summary>
        /// <remarks>
        /// This is only used when PlottingDeisotopedData is true,
        /// which will be the case if the source data is a DeconTools _isos.csv file
        /// </remarks>
        private double mMaxMonoMass;

        /// <summary>
        /// When true, delete temporary files
        /// </summary>
        public bool DeleteTempFiles { get; set; }

        /// <summary>
        /// The divisor to use when creating the overview 2D LC/MS plots
        /// The max points to plot value (MaxPointsToPlot) is divided by the overview plot divisor to compute the number of points to include on the overview plot
        /// </summary>
        /// <remarks>If 0, do not create overview plots</remarks>
        public int OverviewPlotDivisor { get; set; }

        /// <summary>
        /// Maximum charge state to display when plotting deisotoped data (from a DeconTools _isos.csv file)
        /// </summary>
        public int MaxChargeToPlot { get; set; }

        /// <summary>
        /// Maximum number of points to plot
        /// </summary>
        public int MaxPointsToPlot
        {
            get => mMaxPointsToPlot;
            set
            {
                if (value < 10)
                    mMaxPointsToPlot = 10;
                else
                    mMaxPointsToPlot = value;
            }
        }

        /// <summary>
        /// Minimum points per spectrum for inclusion on LC/MS 2D plots
        /// </summary>
        public int MinPointsPerSpectrum
        {
            get => mMinPointsPerSpectrum;
            set
            {
                if (value < 0)
                    mMinPointsPerSpectrum = 0;
                else
                    mMinPointsPerSpectrum = value;
            }
        }

        /// <summary>
        /// MS1 plot title
        /// </summary>
        public string MS1PlotTitle
        {
            get => mMS1PlotTitle;
            set
            {
                if (string.IsNullOrEmpty(value))
                    mMS1PlotTitle = DEFAULT_MS1_PLOT_TITLE;
                else
                    mMS1PlotTitle = value;
            }
        }

        /// <summary>
        /// MS2 plot title
        /// </summary>
        public string MS2PlotTitle
        {
            get => mMS2PlotTitle;
            set
            {
                if (string.IsNullOrEmpty(value))
                    mMS2PlotTitle = DEFAULT_MS2_PLOT_TITLE;
                else
                    mMS2PlotTitle = value;
            }
        }

        /// <summary>
        /// m/z resolution to use when centroiding spectra
        /// </summary>
        public float MZResolution
        {
            get => mMZResolution;
            set
            {
                if (value < 0)
                    mMZResolution = 0;
                else
                    mMZResolution = value;
            }
        }

        /// <summary>
        /// Minimum intensity to require for each mass spectrum data point when adding data to LC/MS 2D plots
        /// </summary>
        public float MinIntensity
        {
            get => mMinIntensity;
            set
            {
                if (value < 0)
                    mMinIntensity = 0;
                else
                    mMinIntensity = value;
            }
        }

        /// <summary>
        /// Maximum monoisotopic mass for deisotoped LC/MS plots
        /// </summary>
        /// <remarks>
        /// This is only used when PlottingDeisotopedData is true,
        /// which will be the case if the source data is a DeconTools _isos.csv file
        /// </remarks>
        public double MaxMonoMassForDeisotopedPlot
        {
            get => mMaxMonoMass;
            set
            {
                if (value < 100)
                    mMaxMonoMass = 100;
                else
                    mMaxMonoMass = value;
            }
        }

        /// <summary>
        /// This will be set to true when we're plotting deisotoped data (from a DeconTools _isos.csv file)
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

        /// <summary>
        /// Clone the options
        /// </summary>
        public LCMSDataPlotterOptions Clone()
        {
            return new LCMSDataPlotterOptions
            {
                DeleteTempFiles = DeleteTempFiles,
                OverviewPlotDivisor = OverviewPlotDivisor,
                MaxChargeToPlot = MaxChargeToPlot,
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
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LCMSDataPlotterOptions() : this(new InfoScannerOptions())
        {
        }

        /// <summary>
        /// Constructor that accepts an options class
        /// </summary>
        /// <param name="options"></param>
        public LCMSDataPlotterOptions(InfoScannerOptions options)
        {
            DeleteTempFiles = true;

            OverviewPlotDivisor = options.LCMSOverviewPlotDivisor;

            MaxChargeToPlot = options.LCMSPlotMaxChargeState;

            mMaxPointsToPlot = options.LCMSPlotMaxPointsToPlot;
            mMinPointsPerSpectrum = options.LCMSPlotMinPointsPerSpectrum;

            mMZResolution = options.LCMSPlotMzResolution;
            mMinIntensity = options.LCMSPlotMinIntensity;

            mMS1PlotTitle = DEFAULT_MS1_PLOT_TITLE;
            mMS2PlotTitle = DEFAULT_MS2_PLOT_TITLE;

            mMaxMonoMass = options.LCMSPlotMaxMonoMass;

            PlottingDeisotopedData = false;
            PlotWithPython = false;
            UseObservedMinScan = false;
        }
    }
}
