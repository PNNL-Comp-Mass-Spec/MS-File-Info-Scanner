
namespace MSFileInfoScanner.MassLynxData
{
    class MSHeaderInfo
    {
        #region "Properties"

        /// <summary>
        /// Acquisition date
        /// </summary>
        public string AcquDate {get; set; }

        /// <summary>
        /// Acquisition name
        /// </summary>
        public string AcquName {get; set; }

        /// <summary>
        /// Acquisition time
        /// </summary>
        public string AcquTime {get; set; }

        /// <summary>
        /// Job code
        /// </summary>
        public string JobCode {get; set; }

        /// <summary>
        /// Task code
        /// </summary>
        public string TaskCode {get; set; }

        /// <summary>
        /// Username
        /// </summary>
        public string UserName {get; set; }

        /// <summary>
        /// Instrument name
        /// </summary>
        public string Instrument {get; set; }

        /// <summary>
        /// Instrument type
        /// </summary>
        public string InstrumentType {get; set; }

        /// <summary>
        /// Conditions
        /// </summary>
        public string Conditions {get; set; }

        /// <summary>
        /// Lab name
        /// </summary>
        public string LabName {get; set; }

        /// <summary>
        /// Sample description
        /// </summary>
        public string SampleDesc {get; set; }

        /// <summary>
        /// Solvent delay
        /// </summary>
        public float SolventDelay {get; set; }

        /// <summary>
        /// Submitter
        /// </summary>
        public string Submitter {get; set; }

        /// <summary>
        /// Sample ID
        /// </summary>
        public string SampleID {get; set; }

        /// <summary>
        /// Bottle number
        /// </summary>
        public string BottleNumber {get; set; }

        /// <summary>
        /// Plate description
        /// </summary>
        public string PlateDesc {get; set; }

        /// <summary>
        /// Mux stream
        /// </summary>
        public int MuxStream {get; set; }

        /// <summary>
        /// Major version
        /// </summary>
        public int VersionMajor {get; set; }

        /// <summary>
        /// Minor version
        /// </summary>
        public int VersionMinor {get; set; }

        /// <summary>
        /// Static MS1 calibration coefficient count
        /// </summary>
        public short CalMS1StaticCoefficientCount {get; set; }

        /// <summary>
        /// Static MS1 calibration coefficients
        /// </summary>
        public double[] CalMS1StaticCoefficients {get; set; }

        /// <summary>
        /// Static MS1 calibration type
        /// </summary>
        /// <remarks>
        /// 0 = normal, 1 = Root mass
        /// </remarks>
        public short CalMS1StaticTypeID {get; set; }

        /// <summary>
        /// Static MS2 calibration coefficient count
        /// </summary>
        public short CalMS2StaticCoefficientCount {get; set; }

        /// <summary>
        /// Static MS2 calibration coefficients
        /// </summary>
        public double[] CalMS2StaticCoefficients {get; set; }

        /// <summary>
        /// Static MS2 calibration type
        /// </summary>
        /// <remarks>
        /// 0 = normal, 1 = Root mass
        /// </remarks>
        public short CalMS2StaticTypeID {get; set; }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MSHeaderInfo()
        {
            AcquDate = string.Empty;
            AcquName = string.Empty;
            AcquTime = string.Empty;
            JobCode = string.Empty;
            TaskCode = string.Empty;
            UserName = string.Empty;
            Instrument = string.Empty;
            InstrumentType = string.Empty;
            Conditions = string.Empty;
            LabName = string.Empty;
            SampleDesc = string.Empty;
            SolventDelay = 0;
            Submitter = string.Empty;
            SampleID = string.Empty;
            BottleNumber = string.Empty;
            PlateDesc = string.Empty;
            MuxStream = 0;
            VersionMajor = 0;
            VersionMinor = 0;
            CalMS1StaticCoefficientCount = 0;
            CalMS1StaticCoefficients = new double[7];
            CalMS1StaticTypeID = 0;
            CalMS2StaticCoefficientCount = 0;
            CalMS2StaticCoefficients = new double[7];
            CalMS2StaticTypeID = 0;
        }
    }
}
