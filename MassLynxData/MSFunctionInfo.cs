
namespace MSFileInfoScanner.MassLynxData
{
    internal class MSFunctionInfo
    {
        /// <summary>
        /// The function number that this data corresponds to
        /// </summary>
        public int FunctionNumber {get; set; }

        /// <summary>
        /// Process number
        /// </summary>
        public short ProcessNumber {get; set; }

        /// <summary>
        /// Starting elution time, in minutes
        /// </summary>
        public float StartRT {get; set; }

        /// <summary>
        /// Ending elution time, in minutes
        /// </summary>
        public float EndRT {get; set; }

        /// <summary>
        /// Function TypeID (mass spec method type)
        /// </summary>
        /// <remarks>
        /// 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
        /// 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
        /// 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
        /// </remarks>
        public short FunctionTypeID {get; set; }

        /// <summary>
        /// Function type (fragmentation type)
        /// </summary>
        /// <remarks>0 for MS-only; 1 for MS/MS</remarks>
        public short FunctionType {get; set; }

        /// <summary>
        /// User-friendly version of FunctionTypeID
        /// </summary>
        public string FunctionTypeText {get; set; }

        /// <summary>
        /// Start mass (minimum mass)
        /// </summary>
        public float StartMass {get; set; }

        /// <summary>
        /// End mass (maximum mass)
        /// </summary>
        public float EndMass {get; set; }

        /// <summary>
        /// Scan count
        /// </summary>
        public int ScanCount {get; set; }

        /// <summary>
        /// Ion mode
        /// </summary>
        public short IonMode {get; set; }

        /// <summary>
        /// Acquisition type
        /// </summary>
        /// <remarks>
        /// 0=Compressed scan, 1=Standard Scan, 2=SIR or MRM Data, 3=Scanning Continuum,
        /// 4=MCA Data, 5=MCA data with SD, 6=MCB data, 7=MCB data with SD
        /// 8=Molecular weight data, 9=High accuracy calibrated data
        /// 10=Single float precision (not used), 11=Enhanced uncalibrated data
        /// 12=Enhanced calibrated data
        /// </remarks>
        public short AcquisitionDataType {get; set; }

        /// <summary>
        /// Cycle time
        /// </summary>
        /// <remarks>
        /// </remarks>
        public float CycleTime {get; set; }

        /// <summary>
        /// Inter scan delay, in seconds
        /// </summary>
        public float InterScanDelay {get; set; }

        /// <summary>
        /// MS/MS collision energy, in eV
        /// </summary>
        public short MsMsCollisionEnergy {get; set; }

        /// <summary>
        /// MS/MS segment or channel count
        /// </summary>
        public short MSMSSegmentOrChannelCount {get; set; }

        /// <summary>
        /// Function set mass (aka parent ion mass)
        /// </summary>
        public float FunctionSetMass {get; set; }

        /// <summary>
        /// Inter segment channel time, in seconds
        /// </summary>
        public float InterSegmentChannelTime {get; set; }

        /// <summary>
        /// Calibration coefficient count (length of CalibrationCoefficients array)
        /// </summary>
        /// <remarks>
        /// Should be 0 or 6 or 7  (typically 6 coefficients)
        /// </remarks>
        public short CalibrationCoefficientCount {get; set; }

        /// <summary>
        /// Calibration coefficients
        /// </summary>
        public double[] CalibrationCoefficients {get; set; }

        /// <summary>
        /// Calibration type
        /// </summary>
        /// <remarks>
        /// 0 = normal, 1 = Root mass
        /// </remarks>
        public short CalTypeID {get; set; }

        /// <summary>
        /// Calibration standard deviation
        /// </summary>
        public double CalStDev {get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="functionNumber"></param>
        public MSFunctionInfo(int functionNumber)
        {
            Initialize(functionNumber);
        }

        /// <summary>
        /// Initialize the variables, including reserving space for 7 calibration coefficients
        /// </summary>
        /// <param name="functionNumber"></param>
        private void Initialize(int functionNumber)
        {
            FunctionNumber = functionNumber;
            ProcessNumber = 0;

            CalibrationCoefficientCount = 0;
            CalibrationCoefficients = new double[7];

            CalTypeID = 0;
            CalStDev = 0;
        }
    }
}
