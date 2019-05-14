using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
// ReSharper disable BuiltInTypeReferenceStyle

namespace MSFileInfoScanner
{

    /// This class can read data from MassLynx data files using native disk access,
    ///  obviating the need to have MassLynx installed
    ///
    /// Note that native file IO is significantly slower than utilizing the
    ///  MassLynx API access functions (see clsMassLynxReader3 and clsMassLynxReader4)
    ///
    /// Written by Matthew Monroe for PNNL in Jan 2004
    /// Portions of this code were written at UNC in 2001
    ///
    /// VB6 version Last modified January 22, 2004
    /// Updated to VB.NET September 17, 2005, though did not upgrade the extended function info functions or data point reading options
    /// Updated to C# in May 2016
    internal class clsMassLynxNativeIO
    {

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks></remarks>
        public clsMassLynxNativeIO()
        {
            CreateNativeDataMasks();
        }

        //-------------------------------
        //-- Start Native IO Headers

        //Private Structure RAWHEADER
        //	Dim nVersionMajor As Short 'Major version of file format.
        //	Dim nVersionMinor As Short 'Minor version of file format.
        //	Dim szAcquName As String 'Acquired filename, no extn.
        //	Dim szAcquDate As String 'Acquired date: DD-MMM-YYYY.
        //	Dim szAcquTime As String 'Acquired time: HH:MM:SS (24 hr).
        //	Dim szJobCode As String
        //	Dim szTaskCode As String
        //	Dim szUserName As String
        //	Dim szLabName As String
        //	Dim szInstrument As String
        //	Dim szConditions As String
        //	Dim szSampleDesc As String 'Sample description.
        //	'szSampleDesc As String 'Sample description.
        //	Dim szSubmitter As String
        //	Dim szSampleID As String
        //	Dim szBottleNumber As String
        //	Dim lfSolventDelay As Double 'Solvent delay in decimal minutes.
        //	Dim bResolved As Integer 'TRUE if resolved data file.
        //	Dim szPepFileName As String 'Assoc pep/embl filename-inc. dir+ext.
        //	Dim szProcess As String
        //	Dim bEncypted As Integer
        //	' Fields added for Maldi-Tof support SCR
        //	Dim nAutosamplerType As Integer
        //	Dim szGasName As String
        //	Dim szInstrumentType As String
        //	' Plate description string
        //	Dim szPlateDesc As String
        //	'Analogue chanel offset times
        //       Dim afAnalogOffset() As Single              ' 1-based array, ranging from 1 to 4
        //	Dim nMuxStream As Integer
        //   End Structure

        //   '***
        //   '*** DMRAWHEADER ***
        //   '*** Used at the DM level of MassLynx.
        //   '***
        //   Private Structure DMRAWHEADER
        //       Dim sRawHeader As RAWHEADER
        //       Dim wFuncsInFile As Short
        //       Dim wAnalogsInFile As Short
        //'   End Structure

        // Used when reading the _functns.inf file
        private const int NATIVE_FUNCTION_INFO_SIZE_BYTES = 416;
        private struct udtRawFunctionDescriptorRecordType
        {
            // The first 2 bytes of the record contain info on the function,
            //   with the information stored in packed form:
            //   bits 0-4: Function type (typically 2=Dly)
            //   bits 5-9: Ion mode (typically 8=ES+)
            //   bits 10-13: Acquisition data type (typically 9=high accuracy calibrated data)
            //   bits 14-15: spare

            // 2 bytes
            public short PackedFunctionInfo;

            // 4 bytes (in seconds)
            public float CycleTime;

            // 4 bytes (in seconds)
            public float InterScanDelay;

            // 4 bytes (in minutes)
            public float StartRT;

            // 4 bytes (in minutes)
            public float EndRT;

            // 4 bytes; unfortunately, this is always 0 and thus we cannot trust it
            public int ScanCount;

            // Packed MS/MS Info:
            //   bits 0-7: collision energy
            //   bits 8-15: segment/channel count
            // 2 bytes
            public short PackedMSMSInfo;

            // The following are more MS/MS parameters
            // 4 bytes
            public float FunctionSetMass;

            // 4 bytes (in seconds)
            public float InterSegmentChannelTime;

            // Up to 32 segment scans can be conducted for a MS/MS run
            // The following three arrays store the segment times, start, and end masses

            // Ranges from 0 to 31 giving a 128 byte array
            public int[] SegmentScanTimes;

            // Ranges from 0 to 31 giving a 128 byte array
            public int[] SegmentStartMasses;

            // Ranges from 0 to 31 giving a 128 byte array
            public int[] SegmentEndMasses;
        }

        // Used when reading the _func001.idx file
        // Total size in bytes
        const short RAW_SCAN_INDEX_RECORD_SIZE = 22;
        private struct udtRawScanIndexRecordType
        {
            // 4 bytes
            public int StartScanOffset;

            // The next 4 bytes are stored as a Long Integer, but are in fact
            //   seven different numbers packed into one Long Integer:
            //   bits 0-21: number of spectral peaks in scan
            //   bits 22-26: segment number (MTOF function)
            //   bit 27: use following continuum data flag
            //   bit 28: continuum data override flag
            //   bit 29: scan contains molecular masses
            //   bit 30: scan contains calibrated masses
            //   bit 31: scan overload flag
            //
            // 4 bytes
            public int PackedScanInfo;

            // 4 bytes
            public float TicValue;

            // 4 bytes, time in minutes
            public float ScanTime;

            // The remaining 6 bytes of the record contain a duplicate of the scan's base peak,
            //   with the information stored in packed form:
            // The method used to pack the data depends on the Acquisition data type
            // Note that the data type ID is stored in packed form in udtRawFunctionDescriptorRecord.PackedFunctionInfo
            // After unpacking, it is stored in .FunctionInfo().AcquisitionDataType
            // The UnpackIntensity and UnpackMass functions
            //  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo
            // For Acquisition Data Type ID 0 (Compressed scan)
            // Use udtRawScanIndexRecordCompressedScanType instead of udtRawScanIndexRecordType
            // For Acquisition Data Type ID 1 (Standard scan)
            //   bits 0-2: intensity scale
            //   bits 3-15: intensity
            //   bits 16-39: mass * 1024
            //   bits 40-47: spare
            // For Acquisition Data Type ID 2 through 7 (Uncalibrated data)
            //   bits 0-2: intensity scale
            //   bits 3-15: intensity
            //   bits 16-43: channel number
            //   bits 44-47: spare
            // For Acquisition Data Type ID 8 (High intensity calibrated data)
            //   bits 0-15: intensity
            //   bits 16-19: intensity scale
            //   bits 20-47: mass * 128
            // For Acquisition Data Type ID 9, 11, and 12 (High accuracy calibrated, enhanced uncalibrated, and enhanced calibrated)
            // Note that this is the form for the LCT and various Q-Tof's
            //   bits 0-15: intensity
            //   bits 16-19: intensity scale
            //   bits 20-24: mass exponent
            //   bits 25-47: mass mantissa

            // 2 bytes
            public short PackedBasePeakIntensity;

            // 4 bytes
            public int PackedBasePeakInfo;
        }

        private struct udtRawScanIndexRecordCompressedScanType
        {
            // 4 bytes
            public int StartScanOffset;

            // 4 bytes
            public int PackedScanInfo;

            // 4 bytes
            public float TicValue;

            // 4 bytes, time in minutes
            public float ScanTime;

            // The remaining 6 bytes of the record contain a duplicate of the scan's base peak,
            //   with the information stored in packed form:
            // The method used to pack the data depends on the Acquisition data type
            // Note that the data type ID is stored in packed form in udtRawFunctionDescriptorRecord.PackedFunctionInfo
            // After unpacking, it is stored in .FunctionInfo().AcquisitionDataType
            // The UnpackIntensity and UnpackMass functions
            //  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo
            // For Acquisition Data Type ID 0 (Compressed scan)
            //   bits 0-2: intensity scale
            //   bits 3-10: intensity
            //   bits 11-31: mass * 128
            //   bits 32-47: spare

            // 4 bytes
            public int PackedBasePeakInfo;

            // 2 bytes, unused
            public short Spare;
        }

        /// <summary>
        /// The udtRawScanIndexRecordType data read from the file is stored in this structure
        /// </summary>
        private struct udtScanIndexRecordType
        {
            // offset (in bytes) from start of file where scan begins
            public int StartScanOffset;
            public int NumSpectralPeaks;
            public short SegmentNumber;
            public bool UseFollowingContinuum;
            public bool ContinuumDataOverride;
            public bool ScanContainsMolecularMasses;
            public bool ScanContainsCalibratedMasses;
            public bool ScanOverload;
            public int BasePeakIntensity;
            public float BasePeakMass;
            public float TicValue;      // counts
            public float ScanTime;      // minutes
            public float LoMass;
            public float HiMass;
            public float SetMass;
        }

        // Function Info Masks
        private short maskFunctionType;
        private short maskIonMode;
        private short maskAcquisitionDataType;
        private short maskCollisionEnergy;

        private int maskSegmentChannelCount;

        // Scan Info Masks
        private int maskSpectralPeak;
        private int maskSegment;
        private int maskUseFollowingContinuum;
        private int maskContinuumDataOverride;
        private int maskScanContainsMolecularMasses;

        private int maskScanContainsCalibratedMasses;

        // Packed mass and packed intensity masks
        private int maskBPIntensityScale;

        private int maskBPMassExponent;
        private int maskBPCompressedDataIntensityScale;

        private int maskBPCompressedDataIntensity;
        private int maskBPStandardDataIntensityScale;
        private int maskBPStandardDataIntensity;

        private int maskBPStandardDataMass;

        private int maskBPUncalibratedDataChannelNumber;

        //-- End Native IO Headers
        //-------------------------------

        private enum eErrorCodeConstants
        {
            NoError = 0,
            InvalidDataFolderPath = 1,
            DataFolderHeaderReadError = 2,
            DataFolderReadError = 3
        }

        /// <summary>
        /// MS file header info
        /// </summary>
        public struct udtMSHeaderInfoType
        {
            /// <summary>
            /// Acquisition date
            /// </summary>
            public string AcquDate;

            /// <summary>
            /// Acquisition name
            /// </summary>
            public string AcquName;

            /// <summary>
            /// Acquisition time
            /// </summary>
            public string AcquTime;

            /// <summary>
            /// Job code
            /// </summary>
            public string JobCode;

            /// <summary>
            /// Task code
            /// </summary>
            public string TaskCode;

            /// <summary>
            /// Username
            /// </summary>
            public string UserName;

            /// <summary>
            /// Instrument name
            /// </summary>
            public string Instrument;

            /// <summary>
            /// Instrument type
            /// </summary>
            public string InstrumentType;

            /// <summary>
            /// Conditions
            /// </summary>
            public string Conditions;

            /// <summary>
            /// Lab name
            /// </summary>
            public string LabName;

            /// <summary>
            /// Sample description
            /// </summary>
            public string SampleDesc;

            /// <summary>
            /// Solvent delay
            /// </summary>
            public float SolventDelay;

            /// <summary>
            /// Submitter
            /// </summary>
            public string Submitter;

            /// <summary>
            /// Sample ID
            /// </summary>
            public string SampleID;

            /// <summary>
            /// Bottle number
            /// </summary>
            public string BottleNumber;

            /// <summary>
            /// Plate description
            /// </summary>
            public string PlateDesc;

            /// <summary>
            /// Mux stream
            /// </summary>
            public int MuxStream;

            /// <summary>
            /// Major version
            /// </summary>
            public int VersionMajor;

            /// <summary>
            /// Minor version
            /// </summary>
            public int VersionMinor;

            /// <summary>
            /// Static MS1 calibration coefficient count
            /// </summary>
            public short CalMS1StaticCoefficientCount;

            /// <summary>
            /// Static MS1 calibration coefficients
            /// </summary>
            public double[] CalMS1StaticCoefficients;

            /// <summary>
            /// Static MS1 calibration type
            /// </summary>
            /// <remarks>
            /// 0 = normal, 1 = Root mass
            /// </remarks>
            public short CalMS1StaticTypeID;

            /// <summary>
            /// Static MS2 calibration coefficient count
            /// </summary>
            public short CalMS2StaticCoefficientCount;

            /// <summary>
            /// Static MS2 calibration coefficients
            /// </summary>
            public double[] CalMS2StaticCoefficients;

            // 0 = normal, 1 = Root mass
            public short CalMS2StaticTypeID;
        }

        /// <summary>
        /// Scan stats
        /// </summary>
        private struct udtScanStatsType
        {
            /// <summary>
            /// Number of peaks in this scan
            /// </summary>
            public int PeakCount;

            /// <summary>
            /// True if calibrated
            /// </summary>
            public bool Calibrated;

            /// <summary>
            /// True if continuum (aka profile)
            /// </summary>
            public bool Continuum;

            /// <summary>
            /// True if overload
            /// </summary>
            public bool Overload;

            /// <summary>
            /// Starting mass (m/z)
            /// </summary>
            public float MassStart;

            /// <summary>
            /// Ending mass (m/z)
            /// </summary>
            public float MassEnd;

            // MS/MS Parent Ion Mass
            public float SetMass;

            // Base peak intensity
            public float BPI;

            /// <summary>
            /// Base peak mass
            /// </summary>
            public float BPIMass;

            /// <summary>
            /// Total ion chromatogram (total intensity)
            /// </summary>
            public float TIC;

            /// <summary>
            /// Elution time (retention time)
            /// </summary>
            public float RetentionTime;
        }

        public struct udtMSFunctionInfoType
        {
            /// <summary>
            /// The function number that this data corresponds to
            /// </summary>
            public int FunctionNumber;

            /// <summary>
            /// Process number
            /// </summary>
            public short ProcessNumber;

            /// <summary>
            /// Starting elution time
            /// </summary>
            public float StartRT;

            /// <summary>
            /// Ending elution time
            /// </summary>
            public float EndRT;

            /// <summary>
            /// Function TypeID (mass spec method type)
            /// </summary>
            /// <remarks>
            /// 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
            /// 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
            /// 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
            /// </remarks>
            public short FunctionTypeID;

            /// <summary>
            /// Function type (fragmentation type)
            /// </summary>
            /// <remarks>0 for MS-only; 1 for MS/MS</remarks>
            public short FunctionType;

            /// <summary>
            /// User-friendly version of FunctionTypeID
            /// </summary>
            public string FunctionTypeText;

            /// <summary>
            /// Start mass (minimum mass)
            /// </summary>
            public float StartMass;

            /// <summary>
            /// End mass (maximum mass)
            /// </summary>
            public float EndMass;

            /// <summary>
            /// Scan count
            /// </summary>
            public int ScanCount;

            /// <summary>
            /// Ion mode
            /// </summary>
            public short IonMode;

            /// <summary>
            /// Acquisition type
            /// </summary>
            /// <remarks>
            /// 0=Compressed scan, 1=Standard Scan, 2=SIR or MRM Data, 3=Scanning Continuum,
            /// </remarks>
            public short AcquisitionDataType;

            /// <summary>
            /// Cycle time
            /// </summary>
            /// <remarks>
            /// 4=MCA Data, 5=MCA data with SD, 6=MCB data, 7=MCB data with SD
            /// 8=Molecular weight data, 9=High accuracy calibrated data
            /// 10=Single float precision (not used), 11=Enhanced uncalibrated data
            /// 12=Enhanced calibrated data
            /// </remarks>
            public float CycleTime;

            /// <summary>
            /// Inter scan delay, in seconds
            /// </summary>
            public float InterScanDelay;

            /// <summary>
            /// MS/MS collision energy, in eV
            /// </summary>
            public short MsMsCollisionEnergy;

            /// <summary>
            /// MS/MS segment or channel count
            /// </summary>
            public short MSMSSegmentOrChannelCount;


            /// <summary>
            /// Function set mass (aka parent ion mass)
            /// </summary>
            public float FunctionSetMass;

            /// <summary>
            /// Inter segment channel time, in seconds
            /// </summary>
            public float InterSegmentChannelTime;

            /// <summary>
            /// Calibration coefficient count (length of CalibrationCoefficients array)
            /// </summary>
            /// <remarks>
            /// Should be 0 or 6 or 7  (typically 6 coefficients)
            /// </remarks>
            public short CalibrationCoefficientCount;

            /// <summary>
            /// Calibration coefficients
            /// </summary>
            public double[] CalibrationCoefficients;

            /// <summary>
            /// Calibration type
            /// </summary>
            /// <remarks>
            /// 0 = normal, 1 = Root mass
            /// </remarks>
            public short CalTypeID;

            /// <summary>
            /// Calibration standard deviation
            /// </summary>
            public double CalStDev;
        }

        public struct udtMSDataType
        {
            public string UserSuppliedDataDirPath;

            // The currently loaded data file path
            public string CurrentDataDirPath;
            public udtMSHeaderInfoType HeaderInfo;
            public int FunctionCount;

            // 1-based array (to stay consistent with Micromass VB example conventions)
            public udtMSFunctionInfoType[] FunctionInfo;
        }

        private udtMSDataType mMSData;

        private eErrorCodeConstants mErrorCode;
        public string GetErrorMessage()
        {
            string message;

            switch (mErrorCode)
            {
                case eErrorCodeConstants.NoError:
                    message = "";
                    break;
                case eErrorCodeConstants.InvalidDataFolderPath:
                    message = "Invalid data directory path";
                    break;
                case eErrorCodeConstants.DataFolderHeaderReadError:
                    message = "The data directory header read error";
                    break;
                case eErrorCodeConstants.DataFolderReadError:
                    message = "Data directory read error";
                    break;
                default:
                    message = "Unknown error";
                    break;
            }

            return message;
        }

        /// <summary>
        /// Retrieves information on the given MassLynx data file (actually a directory)
        /// </summary>
        /// <param name="massLynxDataDirectoryPath"></param>
        /// <param name="acquDate"></param>
        /// <param name="acquName"></param>
        /// <param name="instrument"></param>
        /// <param name="instrumentType"></param>
        /// <param name="sampleDesc"></param>
        /// <param name="versionMajor"></param>
        /// <param name="versionMinor"></param>
        /// <returns>True if success, false if failure</returns>
        public bool GetFileInfo(
            string massLynxDataDirectoryPath,
            out string acquDate,
            out string acquName,
            out string instrument,
            out string instrumentType,
            out string sampleDesc,
            out int versionMajor,
            out int versionMinor)
        {
            bool success;

            acquDate = string.Empty;
            acquName = string.Empty;
            instrument = string.Empty;
            instrumentType = string.Empty;
            sampleDesc = string.Empty;
            versionMajor = 0;
            versionMinor = 0;

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    acquDate = mMSData.HeaderInfo.AcquDate + " " + mMSData.HeaderInfo.AcquTime;
                    acquName = mMSData.HeaderInfo.AcquName;
                    instrument = mMSData.HeaderInfo.Instrument;
                    instrumentType = mMSData.HeaderInfo.InstrumentType;
                    sampleDesc = mMSData.HeaderInfo.SampleDesc;
                    versionMajor = mMSData.HeaderInfo.VersionMajor;
                    versionMinor = mMSData.HeaderInfo.VersionMinor;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.GetFileInfo:" + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Retrieves information on the given MassLynx data file (actually a directory)
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="headerInfo">Output: file info</param>
        /// <returns> True if success, false if failure</returns>
        public bool GetFileInfo(string massLynxDataDirectoryPath, out udtMSHeaderInfoType headerInfo)
        {

            bool success;

            headerInfo = new udtMSHeaderInfoType();

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    headerInfo = mMSData.HeaderInfo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.GetFileInfo:" + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Get acquisition type of the given function
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber">Function number</param>
        /// <returns></returns>
        public short GetFunctionAcquisitionDataType(string massLynxDataDirectoryPath, int functionNumber)
        {
            short acquisitionDataTypeID = -1;

            try
            {
                if (ValidateDataFolder(massLynxDataDirectoryPath))
                {
                    if (functionNumber >= 1 && functionNumber <= mMSData.FunctionCount)
                    {
                        acquisitionDataTypeID = mMSData.FunctionInfo[functionNumber].AcquisitionDataType;
                    }
                }
            }
            catch
            {
                // Ignore errors here
            }

            return acquisitionDataTypeID;

        }

        /// <summary>
        /// Retrieves information on the given function
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber">Function number</param>
        /// <param name="functionType">Output: function type (numeric code)</param>
        /// <returns></returns>
        public bool GetFunctionInfo(
            string massLynxDataDirectoryPath,
            int functionNumber,
            out short functionType)
        {

            return GetFunctionInfo(massLynxDataDirectoryPath, functionNumber,
                                   out _, out _, out _,
                                   out _, out _,
                                   out functionType, out _,
                                   out _);
        }

        /// <summary>
        /// Retrieves information on the given function
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber">Function number</param>
        /// <param name="scanCount">Output: scan count</param>
        /// <param name="startRT">Output: Start elution time</param>
        /// <param name="endRT">Output: End elution time</param>
        /// <param name="startMass">Output: Start mass</param>
        /// <param name="endMass">Output: End mass</param>
        /// <param name="functionType">Output: function type (numeric code)</param>
        /// <param name="functionTypeText">Output: function type (text)</param>
        /// <param name="functionSetMass">Output: function set mass</param>
        /// <returns>True if success, false if failure</returns>
        public bool GetFunctionInfo(
            string massLynxDataDirectoryPath,
            int functionNumber,
            out int scanCount,
            out float startRT,
            out float endRT,
            out float startMass,
            out float endMass,
            out short functionType,
            out string functionTypeText,
            out double functionSetMass)
        {
            bool success;

            scanCount = 0;
            startRT = 0;
            endRT = 0;
            startMass = 0;
            endMass = 0;
            functionType = 0;
            functionSetMass = 0;
            functionTypeText = "";

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    if (functionNumber >= 1 && functionNumber <= mMSData.FunctionCount)
                    {
                        scanCount = mMSData.FunctionInfo[functionNumber].ScanCount;
                        startRT = mMSData.FunctionInfo[functionNumber].StartRT;
                        endRT = mMSData.FunctionInfo[functionNumber].EndRT;
                        startMass = mMSData.FunctionInfo[functionNumber].StartMass;
                        endMass = mMSData.FunctionInfo[functionNumber].EndMass;
                        functionType = mMSData.FunctionInfo[functionNumber].FunctionType;
                        functionSetMass = mMSData.FunctionInfo[functionNumber].FunctionSetMass;
                        functionTypeText = mMSData.FunctionInfo[functionNumber].FunctionTypeText;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.GetFunctionInfo:" + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Retrieves information on the given function
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber">Function number</param>
        /// <param name="functionInfo">Output: function info</param>
        /// <returns>True if success, false if failure</returns>
        public bool GetFunctionInfo(string massLynxDataDirectoryPath, int functionNumber, out udtMSFunctionInfoType functionInfo)
        {
            bool success;

            functionInfo = new udtMSFunctionInfoType();

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    if (functionNumber >= 1 && functionNumber <= mMSData.FunctionCount)
                    {
                        functionInfo = mMSData.FunctionInfo[functionNumber];
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.GetFunctionInfo:" + ex.Message);
                success = false;
            }

            return success;

        }

        /// <summary>
        /// Retrieves the number of functions in the data file
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <returns>Function count, or 0 if an error</returns>
        public int GetFunctionCount(string massLynxDataDirectoryPath)
        {
            var functionCount = 0;

            try
            {
                if (ValidateDataFolder(massLynxDataDirectoryPath))
                {
                    functionCount = mMSData.FunctionCount;
                }
            }
            catch
            {
                // Ignore errors here
            }

            return functionCount;

        }

        /// <summary>
        /// Retrieve the number of scans for the given function
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
        /// <returns>Number of scans, or 0 if an error</returns>
        public int GetNumScans(string massLynxDataDirectoryPath, int functionNumber = 1)
        {
            var scanCount = 0;

            try
            {
                if (ValidateDataFolder(massLynxDataDirectoryPath))
                {
                    if (functionNumber >= 1 && functionNumber <= mMSData.FunctionCount)
                    {
                        scanCount = mMSData.FunctionInfo[functionNumber].ScanCount;
                    }
                    else
                    {
                        scanCount = 0;
                    }
                }
            }
            catch
            {
                // Ignore errors here
            }

            return scanCount;
        }

        /// <summary>
        /// Retrieves scan information
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
        /// <param name="scanNumber"></param>
        /// <param name="scanType">Output: 0 means MS-only scan (survey scan), 1 or greater means MS/MS scan</param>
        /// <param name="basePeakMZ"></param>
        /// <param name="parentIonMZ"></param>
        /// <param name="retentionTime"></param>
        /// <param name="basePeakIntensity"></param>
        /// <param name="totalIonCurrent"></param>
        /// <returns>True if no error, False if an error</returns>
        public bool GetScanInfo(
            string massLynxDataDirectoryPath,
            int functionNumber,
            int scanNumber,
            out int scanType,
            out float basePeakMZ,
            out float parentIonMZ,
            out float retentionTime,
            out float basePeakIntensity,
            out float totalIonCurrent)
        {
            return GetScanInfoEx(massLynxDataDirectoryPath, functionNumber, scanNumber,
                                 out scanType, out basePeakMZ, out parentIonMZ, out retentionTime,
                                 out basePeakIntensity, out totalIonCurrent, out _,
                                 out _, out _, out _, out _);
        }

        /// <summary>
        /// Retrieves scan information
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
        /// <param name="scanNumber"></param>
        /// <param name="scanType">Output: 0 means MS-only scan (survey scan), 1 or greater means MS/MS scan</param>
        /// <param name="basePeakMZ"></param>
        /// <param name="parentIonMZ"></param>
        /// <param name="retentionTime"></param>
        /// <param name="basePeakIntensity"></param>
        /// <param name="totalIonCurrent"></param>
        /// <param name="calibrated"></param>
        /// <param name="continuum"></param>
        /// <param name="overload"></param>
        /// <param name="massStart"></param>
        /// <param name="massEnd"></param>
        /// <returns></returns>
        public bool GetScanInfoEx(
            string massLynxDataDirectoryPath,
            int functionNumber,
            int scanNumber,
            out int scanType,
            out float basePeakMZ,
            out float parentIonMZ,
            out float retentionTime,
            out float basePeakIntensity,
            out float totalIonCurrent,
            out bool calibrated,
            out bool continuum,
            out bool overload,
            out float massStart,
            out float massEnd)
        {

            // Returns scan information in the ByRef variables
            // Function returns True if no error, False if an error
            // Note that if LoadMSScanHeader returns 0, indicating no data points, this function will still return True
            //
            // Note that ScanType = 0 means MS-only scan (survey scan)
            // ScanType > 0 means ms/ms scan

            var scanStatsSingleScan = default(udtScanStatsType);

            scanType = 0;
            basePeakMZ = 0;
            parentIonMZ = 0;
            retentionTime = 0;
            basePeakIntensity = 0;
            totalIonCurrent = 0;
            calibrated = false;
            continuum = false;
            overload = false;
            massStart = 0;
            massEnd = 0;

            if (!ValidateDataFolder(massLynxDataDirectoryPath))
            {
                return false;
            }

            if (!(functionNumber >= 1 && functionNumber <= mMSData.FunctionCount))
            {
                return false;
            }

            LoadMSScanHeader(ref scanStatsSingleScan, mMSData, functionNumber, scanNumber);

            scanType = mMSData.FunctionInfo[functionNumber].FunctionType;
            basePeakMZ = scanStatsSingleScan.BPIMass;
            parentIonMZ = scanStatsSingleScan.SetMass;
            retentionTime = scanStatsSingleScan.RetentionTime;
            basePeakIntensity = scanStatsSingleScan.BPI;
            totalIonCurrent = scanStatsSingleScan.TIC;
            calibrated = scanStatsSingleScan.Calibrated;
            continuum = scanStatsSingleScan.Continuum;
            overload = scanStatsSingleScan.Overload;
            massStart = scanStatsSingleScan.MassStart;
            massEnd = scanStatsSingleScan.MassEnd;
            return true;
        }

        private void InitializeFunctionInfo(ref udtMSFunctionInfoType msFunctionInfo, int functionNumber)
        {
            msFunctionInfo.FunctionNumber = functionNumber;
            msFunctionInfo.ProcessNumber = 0;

            msFunctionInfo.CalibrationCoefficientCount = 0;
            msFunctionInfo.CalibrationCoefficients = new double[7];

            msFunctionInfo.CalTypeID = 0;
            msFunctionInfo.CalStDev = 0;
        }

        private void InitializeNativeFunctionInfo(ref udtRawFunctionDescriptorRecordType nativeFunctionInfo)
        {
            nativeFunctionInfo.SegmentScanTimes = new int[32];
            nativeFunctionInfo.SegmentStartMasses = new int[32];
            nativeFunctionInfo.SegmentEndMasses = new int[32];

        }

        /// <summary>
        /// Return true if the function has MS/MS data
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
        /// <returns></returns>
        public bool IsFunctionMsMs(string massLynxDataDirectoryPath, int functionNumber)
        {

            if (GetFunctionInfo(massLynxDataDirectoryPath, functionNumber, out short functionType))
            {
                return functionType != 0;
            }

            return false;
        }

        /// <summary>
        /// Return true if the spectrum has continuum (profile) data
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
        /// <param name="scanNumber"></param>
        /// <returns></returns>
        public bool IsSpectrumContinuumData(string massLynxDataDirectoryPath, int functionNumber, int scanNumber = 1)
        {

            if (GetScanInfoEx(massLynxDataDirectoryPath, functionNumber, scanNumber,
                              out _, out _, out _, out _,
                              out _, out _, out _,
                              out var continuum, out _, out _, out _))
            {
                return continuum;
            }

            return false;
        }

        /// <summary>
        /// Return true if the directory is a validate Waters / Micromass / MassLynx data directory
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path; alternatively, the path to a file in the .raw directory</param>
        /// <returns></returns>
        public bool IsMassLynxData(string massLynxDataDirectoryPath)
        {
            return ValidateDataFolder(massLynxDataDirectoryPath);
        }

        /// <summary>
        /// Included for compatibility with MassLynxReader3 and MassLynxReader4
        /// </summary>
        /// <returns>Always returns True since this class doesn't require MassLynx</returns>
        public bool IsMassLynxInstalled()
        {
            return true;
        }

        /// <summary>
        /// Verifies that massLynxDataDirectoryPath exists, then loads the header information
        /// </summary>
        /// <param name="thisMSData">Info on the dataset directory; the calling method must initialize it</param>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <returns>True if success, false if failure</returns>
        private bool LoadMSFileHeader(ref udtMSDataType thisMSData, string massLynxDataDirectoryPath)
        {
            var success = false;

            try
            {

                if (Directory.Exists(massLynxDataDirectoryPath))
                {
                    // Read the header information from the current file
                    success = NativeIOReadHeader(massLynxDataDirectoryPath, out thisMSData.HeaderInfo);

                    thisMSData.FunctionCount = 0;
                    return true;
                }
                else
                {
                    SetErrorCode(eErrorCodeConstants.InvalidDataFolderPath);
                    thisMSData.FunctionCount = 0;
                    return false;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFileHeader:" + ex.Message);

                if (!success)
                {
                    // Assume invalid data file
                    SetErrorCode(eErrorCodeConstants.DataFolderReadError);
                    thisMSData.FunctionCount = 0;
                }
            }

            return success;
        }

        /// <summary>
        /// Determines the number of functions in the given data file
        /// </summary>
        /// <param name="thisMSData"></param>
        /// <param name="massLynxDataDirectoryPath"></param>
        /// <returns>The function count, or 0 on failure</returns>
        private int LoadMSFunctionInfo(ref udtMSDataType thisMSData, string massLynxDataDirectoryPath)
        {
            var scanIndexRecord = default(udtScanIndexRecordType);

            var fileValidated = false;

            try
            {

                var massLynxFile = new FileInfo(massLynxDataDirectoryPath);
                string cleanMassLynxDataFolderPath;

                if (massLynxFile.Exists)
                {
                    // massLynxDataDirectoryPath contains a file; remove the filename from massLynxDataDirectoryPath
                    if (massLynxFile.Directory == null)
                    {
                        Console.WriteLine("Unable to determine the parent directory of " + massLynxFile.FullName);
                        return 0;
                    }

                    cleanMassLynxDataFolderPath = massLynxFile.Directory.FullName;
                }
                else
                {
                    cleanMassLynxDataFolderPath = string.Copy(massLynxDataDirectoryPath);
                }

                if (LoadMSFileHeader(ref thisMSData, cleanMassLynxDataFolderPath))
                {
                    thisMSData.UserSuppliedDataDirPath = massLynxDataDirectoryPath;
                    thisMSData.CurrentDataDirPath = cleanMassLynxDataFolderPath;

                    // Use sFuncInfo to read the header information from the current file
                    thisMSData.FunctionCount = NativeIOGetFunctionCount(ref cleanMassLynxDataFolderPath);

                    if (thisMSData.FunctionCount > 0)
                    {
                        fileValidated = true;
                        thisMSData.FunctionInfo = new udtMSFunctionInfoType[thisMSData.FunctionCount + 1];

                        // Note that the function array is 1-based
                        for (var functionNumber = 1; functionNumber <= thisMSData.FunctionCount; functionNumber++)
                        {
                            InitializeFunctionInfo(ref thisMSData.FunctionInfo[functionNumber], functionNumber);

                            if (NativeIOGetFunctionInfo(cleanMassLynxDataFolderPath, ref thisMSData.FunctionInfo[functionNumber]))
                            {
                                float startMass;
                                float endMass;
                                if (thisMSData.FunctionInfo[functionNumber].ScanCount > 0)
                                {
                                    NativeIOGetScanInfo(cleanMassLynxDataFolderPath, thisMSData.FunctionInfo[functionNumber], 1, ref scanIndexRecord);

                                    // ToDo: Get the Start and End mass for the given scan
                                    startMass = 0;
                                    endMass = 0;

                                    // Since the first scan may not have the full mass range, we'll also check a scan
                                    // in the middle of the file as a random comparison
                                    if (thisMSData.FunctionInfo[functionNumber].ScanCount >= 3)
                                    {
                                        // Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, CLng(.ScanCount / 3))
                                        // If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                        // If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                    }

                                    if (thisMSData.FunctionInfo[functionNumber].ScanCount >= 2)
                                    {
                                        // Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, CLng(.ScanCount / 2))
                                        // If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                        // If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                    }

                                    // Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, .ScanCount)
                                    // If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                    // If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                }
                                else
                                {
                                    startMass = 0;
                                    endMass = 0;
                                }

                                thisMSData.FunctionInfo[functionNumber].StartMass = startMass;
                                thisMSData.FunctionInfo[functionNumber].EndMass = endMass;
                            }
                            else
                            {
                                thisMSData.FunctionInfo[functionNumber].ScanCount = 0;
                            }
                        }
                    }
                    else
                    {
                        thisMSData.FunctionCount = 0;
                    }

                    if (thisMSData.FunctionCount > 0)
                    {
                        NativeIOReadCalInfoFromHeader(ref thisMSData);
                    }
                }
                else
                {
                    thisMSData.FunctionCount = 0;
                }

                return thisMSData.FunctionCount;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFunctionInfo:" + ex.Message);

                if (!fileValidated)
                {
                    // Assume invalid data file
                    SetErrorCode(eErrorCodeConstants.DataFolderReadError);
                    thisMSData.FunctionCount = 0;
                }

                return thisMSData.FunctionCount;
            }

        }

        /// <summary>
        /// Loads information on the given scan for the given function
        /// Updates scanStatsSingleScan.PeakCount with the number of peaks in the scan; 0 if an error
        /// </summary>
        /// <param name="scanStatsSingleScan"></param>
        /// <param name="thisMSData"></param>
        /// <param name="functionNumber"></param>
        /// <param name="scanNumber"></param>
        /// <remarks>
        /// The calling function must validate that functionNumber is valid
        /// Since this function uses mMSData.FunctionInfo, one must call NativeIOGetFunctionInfo
        /// to populate .FunctionInfo before calling this function
        /// </remarks>
        private void LoadMSScanHeader(ref udtScanStatsType scanStatsSingleScan, udtMSDataType thisMSData, int functionNumber, int scanNumber)
        {
            var scanIndexRecord = default(udtScanIndexRecordType);

            try
            {

                scanStatsSingleScan.PeakCount = 0;

                scanStatsSingleScan.Calibrated = false;
                scanStatsSingleScan.Continuum = false;
                scanStatsSingleScan.Overload = false;

                scanStatsSingleScan.MassStart = 0;
                scanStatsSingleScan.MassEnd = 0;
                scanStatsSingleScan.SetMass = 0;

                scanStatsSingleScan.BPI = 0;
                scanStatsSingleScan.BPIMass = 0;
                scanStatsSingleScan.TIC = 0;

                scanStatsSingleScan.PeakCount = 0;
                scanStatsSingleScan.RetentionTime = 0;

                if (NativeIOGetScanInfo(thisMSData.CurrentDataDirPath, thisMSData.FunctionInfo[functionNumber], scanNumber, ref scanIndexRecord))
                {
                    scanStatsSingleScan.Calibrated = scanIndexRecord.ScanContainsCalibratedMasses;
                    scanStatsSingleScan.Continuum = scanIndexRecord.ContinuumDataOverride;
                    scanStatsSingleScan.Overload = scanIndexRecord.ScanOverload;

                    scanStatsSingleScan.MassStart = scanIndexRecord.LoMass;
                    scanStatsSingleScan.MassEnd = scanIndexRecord.HiMass;

                    scanStatsSingleScan.BPI = scanIndexRecord.BasePeakIntensity;
                    scanStatsSingleScan.BPIMass = scanIndexRecord.BasePeakMass;
                    scanStatsSingleScan.TIC = scanIndexRecord.TicValue;

                    scanStatsSingleScan.PeakCount = scanIndexRecord.NumSpectralPeaks;
                    scanStatsSingleScan.RetentionTime = scanIndexRecord.ScanTime;

                    scanStatsSingleScan.SetMass = scanIndexRecord.SetMass;

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSScanHeader:" + ex.Message);
            }
        }

        private void SetErrorCode(eErrorCodeConstants eNewErrorCode)
        {
            mErrorCode = eNewErrorCode;
        }

        private bool ValidateDataFolder(string massLynxDataDirectoryPath)
        {
            // Returns True if valid, False if not valid

            mErrorCode = eErrorCodeConstants.NoError;
            var validDataFolder = false;

            if (string.IsNullOrEmpty(massLynxDataDirectoryPath))
            {
                return false;
            }
            else
            {
                massLynxDataDirectoryPath = massLynxDataDirectoryPath.Trim();
            }
            if (mMSData.UserSuppliedDataDirPath == null)
            {
                mMSData.UserSuppliedDataDirPath = string.Empty;
            }

            if (mMSData.FunctionCount == 0 || !string.Equals(mMSData.UserSuppliedDataDirPath, massLynxDataDirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                var numFunctions = LoadMSFunctionInfo(ref mMSData, massLynxDataDirectoryPath);
                if (numFunctions > 0)
                {
                    validDataFolder = true;
                }
                else
                {
                    if (mErrorCode == eErrorCodeConstants.NoError)
                        SetErrorCode(eErrorCodeConstants.DataFolderReadError);
                }
            }
            else
            {
                validDataFolder = true;
                mErrorCode = eErrorCodeConstants.NoError;
            }

            return validDataFolder;

        }

        private bool ConstructValidDataFilePath(string desiredDataFilePath, out string dataFilePath)
        {

            // Make sure the dataFilePath contains ".raw"
            if (desiredDataFilePath.ToLower().IndexOf(".raw", StringComparison.Ordinal) < 0)
            {
                SetErrorCode(eErrorCodeConstants.InvalidDataFolderPath);
                dataFilePath = string.Empty;
                return false;
            }

            dataFilePath = desiredDataFilePath;
            return true;
        }

        /// <summary>
        /// Create a mask
        /// </summary>
        /// <param name="startBit"></param>
        /// <param name="endBit"></param>
        /// <returns></returns>
        /// <remarks>Returns a long value to allow for unsigned Int32 masks</remarks>
        private long CreateMask(byte startBit, byte endBit)
        {
            long thisMask;

            if (startBit == 0)
            {
                thisMask = (long)(Math.Pow(2, endBit + 1) - 1);
            }
            else
            {
                thisMask = 0;
                for (var bitIndex = startBit; bitIndex <= endBit; bitIndex++)
                {
                    thisMask += (long)Math.Pow(2, bitIndex);
                }
            }

            return thisMask;

        }

        private void CreateNativeDataMasks()
        {
            // Create the bit masks for the PackedFunctionInfo
            maskFunctionType = (short)CreateMask(0, 4);
            maskIonMode = (short)CreateMask(5, 9);
            maskAcquisitionDataType = (short)CreateMask(10, 13);

            // Create the bit masks for the Packed MS/MS Info
            maskCollisionEnergy = (short)CreateMask(0, 7);
            maskSegmentChannelCount = (int)CreateMask(8, 15);

            // Create the bit masks for the packed scan info
            maskSpectralPeak = (int)CreateMask(0, 21);
            maskSegment = (int)CreateMask(22, 26);
            maskUseFollowingContinuum = (int)CreateMask(27, 27);
            maskContinuumDataOverride = (int)CreateMask(28, 28);
            maskScanContainsMolecularMasses = (int)CreateMask(29, 29);
            maskScanContainsCalibratedMasses = (int)CreateMask(30, 30);

            // Create the masks for the packed base peak info
            maskBPIntensityScale = (int)CreateMask(0, 3);

            // Also applies to High Intensity Calibrated data and High Accuracy Calibrated Data
            maskBPMassExponent = (int)CreateMask(4, 8);

            maskBPCompressedDataIntensityScale = (int)CreateMask(0, 2);
            maskBPCompressedDataIntensity = (int)CreateMask(3, 10);

            maskBPStandardDataIntensityScale = (int)CreateMask(0, 2);

            // Also applies to Uncalibrated data
            maskBPStandardDataIntensity = (int)CreateMask(3, 15);

            // Also applies to Uncalibrated data
            maskBPStandardDataMass = (int)CreateMask(0, 23);

            maskBPUncalibratedDataChannelNumber = (int)CreateMask(0, 27);
        }

        private int CIntSafe(string valueText)
        {
            if (int.TryParse(valueText, out var value))
            {
                return value;
            }
            return 0;
        }

        private float CFloatSafe(string valueText)
        {
            if (float.TryParse(valueText, out var value))
            {
                return value;
            }
            return 0;
        }

        private string GetFunctionNumberZeroPadded(int functionNumber)
        {
            return functionNumber.ToString().PadLeft(3, '0');
        }

        /// <summary>
        /// Retrieves the number of functions
        /// </summary>
        /// <param name="dataDirPath"></param>
        /// <returns>The number of functions, 0 if an error</returns>
        private int NativeIOGetFunctionCount(ref string dataDirPath)
        {

            var functionCount = 0;

            try
            {

                var functionsFilePath = Path.Combine(dataDirPath, "_functns.inf");
                var functionsFile = new FileInfo(functionsFilePath);

                functionCount = 0;

                if (functionsFile.Exists)
                {
                    functionCount = (int)(functionsFile.Length / NATIVE_FUNCTION_INFO_SIZE_BYTES);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetFunctionCount:" + ex.Message);
            }

            return functionCount;

        }

        /// <summary>
        /// Retrieve the function info
        /// </summary>
        /// <param name="dataDirPath"></param>
        /// <param name="msFunctionInfo"></param>
        /// <returns>True if success, False if failure</returns>
        private bool NativeIOGetFunctionInfo(string dataDirPath, ref udtMSFunctionInfoType msFunctionInfo)
        {
            var nativeFunctionInfo = new udtRawFunctionDescriptorRecordType();
            InitializeNativeFunctionInfo(ref nativeFunctionInfo);

            try
            {

                var functionsFilePath = Path.Combine(dataDirPath, "_functns.inf");
                var functionsFile = new FileInfo(functionsFilePath);

                int functionCount;

                if (!functionsFile.Exists)
                {
                    return false;
                }

                using (var reader = new BinaryReader(new FileStream(functionsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    functionCount = (int)(functionsFile.Length / NATIVE_FUNCTION_INFO_SIZE_BYTES);

                    if (msFunctionInfo.FunctionNumber < 1 || msFunctionInfo.FunctionNumber > functionCount)
                    {
                        return false;
                    }

                    // Since we're using Binary Access, we need to specify the Byte Offset to start reading at
                    // The first byte is 1, and that is where Function 1 can be found
                    // Function 2 can be found NATIVE_FUNCTION_INFO_SIZE_BYTES+1 bytes into the file

                    reader.BaseStream.Seek((msFunctionInfo.FunctionNumber - 1) * NATIVE_FUNCTION_INFO_SIZE_BYTES,
                                             SeekOrigin.Begin);

                    nativeFunctionInfo.PackedFunctionInfo = reader.ReadInt16();
                    nativeFunctionInfo.CycleTime = reader.ReadSingle();
                    nativeFunctionInfo.InterScanDelay = reader.ReadSingle();
                    nativeFunctionInfo.StartRT = reader.ReadSingle();
                    nativeFunctionInfo.EndRT = reader.ReadSingle();
                    nativeFunctionInfo.ScanCount = reader.ReadInt32();

                    // Packed MS/MS Info:
                    //   bits 0-7: collision energy
                    //   bits 8-15: segment/channel count
                    nativeFunctionInfo.PackedMSMSInfo = reader.ReadInt16();

                    // The following are more MS/MS parameters
                    nativeFunctionInfo.FunctionSetMass = reader.ReadSingle();
                    nativeFunctionInfo.InterSegmentChannelTime = reader.ReadSingle();

                    // Up to 32 segment scans can be conducted for a MS/MS run
                    // The following three arrays store the segment times, start, and end masses
                    for (var index = 0; index <= 31; index++)
                    {
                        nativeFunctionInfo.SegmentScanTimes[index] = reader.ReadInt32();
                    }
                    for (var index = 0; index <= 31; index++)
                    {
                        nativeFunctionInfo.SegmentStartMasses[index] = reader.ReadInt32();
                    }
                    for (var index = 0; index <= 31; index++)
                    {
                        nativeFunctionInfo.SegmentEndMasses[index] = reader.ReadInt32();
                    }

                } // end using

                var success = true;

                if (nativeFunctionInfo.PackedFunctionInfo == 0 &&
                    Math.Abs(nativeFunctionInfo.CycleTime) < float.Epsilon &&
                    Math.Abs(nativeFunctionInfo.InterScanDelay) < float.Epsilon)
                {
                    // Empty function record; see if file even exists
                    if (File.Exists(Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(functionCount + 1) + ".dat")))
                    {
                        // Nope, file does not exist, function is invalid
                        success = false;
                    }
                }

                if (success)
                {
                    // Copy data from nativeFunctionInfo to udtFunctionInfo
                    msFunctionInfo.FunctionTypeID = (short)(nativeFunctionInfo.PackedFunctionInfo & maskFunctionType);

                    // 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
                    // 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
                    // 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
                    msFunctionInfo.FunctionType = 0;
                    switch (msFunctionInfo.FunctionTypeID)
                    {
                        case 0:
                            msFunctionInfo.FunctionTypeText = "MS";
                            break;
                        case 1:
                            msFunctionInfo.FunctionTypeText = "SIR";
                            break;
                        case 2:
                            msFunctionInfo.FunctionTypeText = "DLY";
                            break;
                        case 3:
                            msFunctionInfo.FunctionTypeText = "CAT";
                            break;
                        case 4:
                            msFunctionInfo.FunctionTypeText = "OFF";
                            break;
                        case 5:
                            msFunctionInfo.FunctionTypeText = "PAR";
                            break;
                        case 6:
                            msFunctionInfo.FunctionTypeText = "DAU";
                            msFunctionInfo.FunctionType = 1;
                            break;
                        case 7:
                            msFunctionInfo.FunctionTypeText = "NL";
                            break;
                        case 8:
                            msFunctionInfo.FunctionTypeText = "NG";
                            break;
                        case 9:
                            msFunctionInfo.FunctionTypeText = "MRM";
                            break;
                        case 10:
                            msFunctionInfo.FunctionTypeText = "Q1F";
                            break;
                        case 11:
                            msFunctionInfo.FunctionTypeText = "MS2";
                            msFunctionInfo.FunctionType = 1;
                            break;
                        case 12:
                            msFunctionInfo.FunctionTypeText = "DAD";
                            break;
                        case 13:
                            msFunctionInfo.FunctionTypeText = "TOF";
                            break;
                        case 14:
                            msFunctionInfo.FunctionTypeText = "PSD";
                            break;
                        case 16:
                            msFunctionInfo.FunctionTypeText = "TOF MS/MS";
                            msFunctionInfo.FunctionType = 1;
                            break;
                        case 17:
                            msFunctionInfo.FunctionTypeText = "TOF MS";
                            break;
                        case 18:
                            msFunctionInfo.FunctionTypeText = "TOF MS";
                            break;
                        default:
                            msFunctionInfo.FunctionTypeText = "MS Unknown";
                            break;
                    }

                    msFunctionInfo.IonMode = (short)((short)(nativeFunctionInfo.PackedFunctionInfo & maskIonMode) / 32f);     // 32 = 2^5
                    msFunctionInfo.AcquisitionDataType = (short)((short)(nativeFunctionInfo.PackedFunctionInfo & maskAcquisitionDataType) / 1024f);    // 1024 = 2^10

                    msFunctionInfo.CycleTime = nativeFunctionInfo.CycleTime;
                    msFunctionInfo.InterScanDelay = nativeFunctionInfo.InterScanDelay;
                    msFunctionInfo.StartRT = nativeFunctionInfo.StartRT;
                    msFunctionInfo.EndRT = nativeFunctionInfo.EndRT;

                    msFunctionInfo.MsMsCollisionEnergy = (short)(nativeFunctionInfo.PackedMSMSInfo & maskCollisionEnergy);
                    msFunctionInfo.MSMSSegmentOrChannelCount = (short)(NumberConversion.Int32ToUnsigned(nativeFunctionInfo.PackedMSMSInfo) / 256f);      // 256 = 2^8

                    msFunctionInfo.FunctionSetMass = nativeFunctionInfo.FunctionSetMass;
                    msFunctionInfo.InterSegmentChannelTime = nativeFunctionInfo.InterSegmentChannelTime;

                    // Since nativeFunctionInfo.ScanCount is always 0, we need to use NativeIOGetScanCount instead
                    var scanCount = NativeIOGetScanCount(dataDirPath, ref msFunctionInfo);
                    if (msFunctionInfo.ScanCount != scanCount)
                    {
                        // This is unexpected
                        Debug.WriteLine("Scan count values do not agree in NativeIOGetFunctionInfo");
                    }

                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetFunctionInfo:" + ex.Message);
                return false;
            }

        }

        /// <summary>
        /// Returns the number of scans in the given function
        /// Also updates msFunctionInfo.ScanCount
        /// </summary>
        /// <param name="dataDirPath"></param>
        /// <param name="msFunctionInfo"></param>
        /// <returns>Number of scans, or 0 if an error</returns>
        /// <remarks>
        /// msFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount
        /// </remarks>
        private int NativeIOGetScanCount(string dataDirPath, ref udtMSFunctionInfoType msFunctionInfo)
        {

            try
            {

                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(msFunctionInfo.FunctionNumber) + ".idx");
                var indexFile = new FileInfo(indexFilePath);

                var numberOfScansInFunction = 0;
                if (indexFile.Exists)
                {
                    // The ScanCount stored in the function index file is always 0 rather than the correct number of scans
                    // Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
                    //  by the size of each udtRawScanIndexRecordType

                    numberOfScansInFunction = (int)(indexFile.Length / RAW_SCAN_INDEX_RECORD_SIZE);
                    msFunctionInfo.ScanCount = numberOfScansInFunction;
                }

                return numberOfScansInFunction;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetScanCount:" + ex.Message);
                return 0;
            }

        }

        /// <summary>
        /// Retrieves information on the given scan for the given function
        /// </summary>
        /// <param name="dataDirPath"></param>
        /// <param name="msFunctionInfo"></param>
        /// <param name="scanNumber"></param>
        /// <param name="scanIndexRecord"></param>
        /// <param name="scanOffsetAndPeakCountOnly"></param>
        /// <returns>True if success, False if failure</returns>
        /// <remarks>
        /// msFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount
        /// </remarks>
        private bool NativeIOGetScanInfo(
            string dataDirPath,
            udtMSFunctionInfoType msFunctionInfo,
            int scanNumber,
            ref udtScanIndexRecordType scanIndexRecord,
            bool scanOffsetAndPeakCountOnly = false)
        {

            // This udt is used for most files
            var nativeScanIndexRecord = default(udtRawScanIndexRecordType);

            // This udt is used for files with msFunctionInfo.AcquisitionDataType = 0
            // The difference is that udtRawScanIndexRecordType ends in an Integer then a Long
            //  while this udt ends in a Long, then an Integer
            // When this udt is used, its values are copied to nativeScanIndexRecord directly after reading
            var nativeScanIndexRecordCompressedScan = default(udtRawScanIndexRecordCompressedScanType);

            var success = false;

            try
            {

                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(msFunctionInfo.FunctionNumber) + ".idx");
                // indexFilePath  = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(ref msFunctionInfo.FunctionNumber) + ".sts");

                var indexFile = new FileInfo(indexFilePath);

                if (!indexFile.Exists)
                {
                    return false;
                }

                var numberOfScansInFunction = (int)(indexFile.Length / RAW_SCAN_INDEX_RECORD_SIZE);

                using (var reader = new BinaryReader(new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {

                    // The ScanCount stored in the function index file is always 0 rather than the correct number of scans
                    // Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
                    //  by the size of each udtRawScanIndexRecordType

                    if (scanNumber < 1)
                        scanNumber = 1;

                    if (numberOfScansInFunction > 0 && scanNumber <= numberOfScansInFunction)
                    {
                        // Just read the record for this scan

                        // Jump to the appropriate file offset based on scanNumber
                        reader.BaseStream.Seek((scanNumber - 1) * RAW_SCAN_INDEX_RECORD_SIZE, SeekOrigin.Begin);

                        if (msFunctionInfo.AcquisitionDataType == 0)
                        {
                            // File saved with Acquisition Data Type 0

                            nativeScanIndexRecordCompressedScan.StartScanOffset = reader.ReadInt32();
                            nativeScanIndexRecordCompressedScan.PackedScanInfo = reader.ReadInt32();
                            nativeScanIndexRecordCompressedScan.TicValue = reader.ReadSingle();
                            nativeScanIndexRecordCompressedScan.ScanTime = reader.ReadSingle();
                            nativeScanIndexRecordCompressedScan.PackedBasePeakInfo = reader.ReadInt32();
                            nativeScanIndexRecordCompressedScan.Spare = reader.ReadInt16();

                            // Copy from nativeScanIndexRecordCompressedScan to nativeScanIndexRecord
                            nativeScanIndexRecord.StartScanOffset = nativeScanIndexRecordCompressedScan.StartScanOffset;
                            nativeScanIndexRecord.PackedScanInfo = nativeScanIndexRecordCompressedScan.PackedScanInfo;

                            nativeScanIndexRecord.TicValue = nativeScanIndexRecordCompressedScan.TicValue;
                            nativeScanIndexRecord.ScanTime = nativeScanIndexRecordCompressedScan.ScanTime;

                            // Unused
                            nativeScanIndexRecord.PackedBasePeakIntensity = 0;

                            nativeScanIndexRecord.PackedBasePeakInfo = nativeScanIndexRecordCompressedScan.PackedBasePeakInfo;
                        }
                        else
                        {
                            // File saved with Acquisition Data Type other than 0
                            nativeScanIndexRecord.StartScanOffset = reader.ReadInt32();
                            nativeScanIndexRecord.PackedScanInfo = reader.ReadInt32();
                            nativeScanIndexRecord.TicValue = reader.ReadSingle();
                            nativeScanIndexRecord.ScanTime = reader.ReadSingle();
                            nativeScanIndexRecord.PackedBasePeakIntensity = reader.ReadInt16();
                            nativeScanIndexRecord.PackedBasePeakInfo = reader.ReadInt32();
                        }

                        success = true;
                    }
                }

                if (success)
                {
                    scanIndexRecord.StartScanOffset = nativeScanIndexRecord.StartScanOffset;

                    scanIndexRecord.NumSpectralPeaks = nativeScanIndexRecord.PackedScanInfo & maskSpectralPeak;

                    if (!scanOffsetAndPeakCountOnly)
                    {
                        // 4194304 = 2^22
                        scanIndexRecord.SegmentNumber = (short)((short)(nativeScanIndexRecord.PackedScanInfo & maskSegment) / 4194304);

                        scanIndexRecord.UseFollowingContinuum = NumberConversion.ValueToBool(nativeScanIndexRecord.PackedScanInfo & maskUseFollowingContinuum);
                        scanIndexRecord.ContinuumDataOverride = NumberConversion.ValueToBool(nativeScanIndexRecord.PackedScanInfo & maskContinuumDataOverride);
                        scanIndexRecord.ScanContainsMolecularMasses = NumberConversion.ValueToBool(nativeScanIndexRecord.PackedScanInfo & maskScanContainsMolecularMasses);
                        scanIndexRecord.ScanContainsCalibratedMasses = NumberConversion.ValueToBool(nativeScanIndexRecord.PackedScanInfo & maskScanContainsCalibratedMasses);
                        if (nativeScanIndexRecord.PackedScanInfo != Math.Abs(nativeScanIndexRecord.PackedScanInfo))
                        {
                            scanIndexRecord.ScanOverload = true;
                        }

                        scanIndexRecord.TicValue = nativeScanIndexRecord.TicValue;
                        scanIndexRecord.ScanTime = nativeScanIndexRecord.ScanTime;

                        scanIndexRecord.BasePeakIntensity = (int)UnpackIntensity(nativeScanIndexRecord.PackedBasePeakIntensity, nativeScanIndexRecord.PackedBasePeakInfo, msFunctionInfo.AcquisitionDataType);

                        scanIndexRecord.BasePeakMass = (float)UnpackMass(nativeScanIndexRecord.PackedBasePeakInfo, msFunctionInfo.AcquisitionDataType, true);

                        // ToDo: May need to calibrate the base peak mass
                        // scanIndexRecord.BasePeakMass = scanIndexRecord.BasePeakMass;

                        scanIndexRecord.LoMass = 0;

                        // ToDo: Figure out if this can be read from the FunctionIndex file
                        scanIndexRecord.HiMass = 0;
                        scanIndexRecord.SetMass = 0;
                        // This will get populated below
                    }

                }
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetScanInfo:" + ex.Message);
                return false;
            }

        }

        private void NativeIOParseCalibrationCoefficients(
            string textToParse,
            out short calibrationCoefficientCount,
            IList<double> calibrationCoefficients,
            out short calibrationTypeID)
        {
            var calibrationParameters = textToParse.Split(',');
            calibrationCoefficientCount = 0;
            calibrationTypeID = 0;

            for (var calIndex = 0; calIndex <= calibrationParameters.Length - 1; calIndex++)
            {
                if (double.TryParse(calibrationParameters[calIndex], out var paramValue))
                {
                    calibrationCoefficients[calIndex] = paramValue;
                    calibrationCoefficientCount++;
                }
                else
                {
                    // Non-numeric coefficient encountered; stop populating the coefficients
                    break;
                }
            }

            for (var calIndex = calibrationParameters.Length - 1; calIndex >= 0; calIndex += -1)
            {
                if (calibrationParameters[calIndex].ToUpper().StartsWith("T"))
                {
                    calibrationParameters[calIndex] = calibrationParameters[calIndex].Substring(1);
                    if (short.TryParse(calibrationParameters[calIndex], out var calTypeID))
                    {
                        calibrationTypeID = calTypeID;
                    }
                    break;
                }
            }

        }

        /// <summary>
        /// Looks for the "$$ Cal Function" lines in the _HEADER.TXT file
        /// </summary>
        /// <param name="thisMSData"></param>
        /// <returns>True if successful, False if not</returns>
        /// <remarks>Should only be called by LoadMSFunctionInfo and only after the functions have been determined</remarks>
        private bool NativeIOReadCalInfoFromHeader(ref udtMSDataType thisMSData)
        {
            const string CAL_FUNCTION_NAME = "CAL FUNCTION";
            const string CAL_STDDEV_FUNCTION_NAME = "CAL STDDEV FUNCTION";

            try
            {

                var headerFilePath = Path.Combine(thisMSData.CurrentDataDirPath, "_HEADER.TXT");
                var headerFile = new FileInfo(headerFilePath);

                if (!headerFile.Exists)
                {
                    return false;
                }

                using (var reader = new StreamReader(headerFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        // All valid lines start with $$
                        if (!dataLine.StartsWith("$$"))
                            continue;

                        // Remove the first three characters (we actually remove the first 2 then Trim, since the third character is supposed to be a space)

                        dataLine = dataLine.Substring(2).Trim();
                        var colonIndex = dataLine.IndexOf(':');
                        var keyValue = dataLine.Substring(colonIndex + 1).Trim();

                        int functionNumber;
                        if (dataLine.ToUpper().StartsWith(CAL_FUNCTION_NAME))
                        {
                            // Calibration equation for one of the functions
                            functionNumber = CIntSafe(dataLine.Substring(CAL_FUNCTION_NAME.Length, colonIndex - CAL_FUNCTION_NAME.Length));
                            if (functionNumber >= 1 && functionNumber <= thisMSData.FunctionCount)
                            {

                                NativeIOParseCalibrationCoefficients(
                                    keyValue,
                                    out thisMSData.FunctionInfo[functionNumber].CalibrationCoefficientCount,
                                    thisMSData.FunctionInfo[functionNumber].CalibrationCoefficients,
                                    out thisMSData.FunctionInfo[functionNumber].CalTypeID);

                            }
                            else
                            {
                                // Calibration equation for non-existent function
                                // This shouldn't happen
                            }
                        }
                        else if (dataLine.ToUpper().StartsWith(CAL_STDDEV_FUNCTION_NAME))
                        {
                            functionNumber = CIntSafe(dataLine.Substring(CAL_STDDEV_FUNCTION_NAME.Length, colonIndex - CAL_STDDEV_FUNCTION_NAME.Length));
                            if (functionNumber >= 1 && functionNumber <= thisMSData.FunctionCount)
                            {
                                if (double.TryParse(keyValue, out var calStdDev))
                                {
                                    thisMSData.FunctionInfo[functionNumber].CalStDev = calStdDev;
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOReadCalInfoFromHeader:" + ex.Message);
                return false;
            }

        }

        /// <summary>
        /// Populates the header info by reading _HEADER.TXT
        /// </summary>
        /// <param name="dataDirPath"></param>
        /// <param name="headerInfo"></param>
        /// <returns>True if successful, False if not</returns>
        private bool NativeIOReadHeader(string dataDirPath, out udtMSHeaderInfoType headerInfo)
        {

            headerInfo = new udtMSHeaderInfoType
            {
                AcquDate = string.Empty,
                AcquName = string.Empty,
                AcquTime = string.Empty,
                JobCode = string.Empty,
                TaskCode = string.Empty,
                UserName = string.Empty,
                Instrument = string.Empty,
                InstrumentType = string.Empty,
                Conditions = string.Empty,
                LabName = string.Empty,
                SampleDesc = string.Empty,
                SolventDelay = 0,
                Submitter = string.Empty,
                SampleID = string.Empty,
                BottleNumber = string.Empty,
                PlateDesc = string.Empty,
                MuxStream = 0,
                VersionMajor = 0,
                VersionMinor = 0,
                CalMS1StaticCoefficientCount = 0,
                CalMS1StaticCoefficients = new double[7],
                CalMS1StaticTypeID = 0,
                CalMS2StaticCoefficientCount = 0,
                CalMS2StaticCoefficients = new double[7],
                CalMS2StaticTypeID = 0
            };

            try
            {


                var headerFilePath = Path.Combine(dataDirPath, "_HEADER.TXT");
                var headerFile = new FileInfo(headerFilePath);

                if (!headerFile.Exists)
                {
                    return false;
                }

                using (var reader = new StreamReader(headerFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        // All valid lines start with $$
                        if (!dataLine.StartsWith("$$"))
                            continue;

                        // Remove the first three characters (we actually remove the first 2 then Trim, since the third character is supposed to be a space)
                        dataLine = dataLine.Substring(2).Trim();
                        var colonIndex = dataLine.IndexOf(':');
                        var keyName = dataLine.Substring(0, colonIndex).ToUpper();
                        var keyValue = dataLine.Substring(colonIndex + 1).Trim();

                        switch (keyName)
                        {
                            case "VERSION":
                                if (short.TryParse(keyValue, out var versionMajor))
                                {
                                    headerInfo.VersionMajor = versionMajor;
                                    headerInfo.VersionMinor = (int)(Convert.ToSingle(keyValue) - headerInfo.VersionMajor);
                                }
                                break;
                            case "ACQUIRED NAME":
                                headerInfo.AcquName = keyValue;
                                break;
                            case "ACQUIRED DATE":
                                headerInfo.AcquDate = keyValue;
                                break;
                            case "ACQUIRED TIME":
                                headerInfo.AcquTime = keyValue;
                                break;
                            case "JOB CODE":
                                headerInfo.JobCode = keyValue;
                                break;
                            case "TASK CODE":
                                headerInfo.TaskCode = keyValue;
                                break;
                            case "USER NAME":
                                headerInfo.UserName = keyValue;
                                break;
                            case "INSTRUMENT":
                                headerInfo.Instrument = keyValue;
                                break;
                            case "CONDITIONS":
                                headerInfo.Conditions = keyValue;
                                break;
                            case "LABORATORY NAME":
                                headerInfo.LabName = keyValue;
                                break;
                            case "SAMPLE DESCRIPTION":
                                headerInfo.SampleDesc = keyValue;
                                break;
                            case "SOLVENT DELAY":
                                headerInfo.SolventDelay = CFloatSafe(keyValue);
                                break;
                            case "SUBMITTER":
                                headerInfo.Submitter = keyValue;
                                break;
                            // ReSharper disable once StringLiteralTypo
                            case "SAMPLEID":
                                headerInfo.SampleID = keyValue;
                                break;
                            case "BOTTLE NUMBER":
                                headerInfo.BottleNumber = keyValue;
                                break;
                            case "PLATE DESC":
                                headerInfo.PlateDesc = keyValue;
                                break;
                            case "MUX STREAM":
                                headerInfo.MuxStream = CIntSafe(keyValue);
                                break;
                            case "CAL MS1 STATIC":
                                NativeIOParseCalibrationCoefficients(
                                    keyValue,
                                    out headerInfo.CalMS1StaticCoefficientCount,
                                    headerInfo.CalMS1StaticCoefficients,
                                    out headerInfo.CalMS1StaticTypeID);
                                break;
                            case "CAL MS2 STATIC":
                                NativeIOParseCalibrationCoefficients(
                                    keyValue,
                                    out headerInfo.CalMS2StaticCoefficientCount,
                                    headerInfo.CalMS2StaticCoefficients,
                                    out headerInfo.CalMS2StaticTypeID);
                                break;
                            default:
                                // Ignore it
                                break;
                        }
                    }
                }

                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOReadHeader:" + ex.Message);
                return false;
            }

        }

        private Int32 ExtractFromBitsInt32(int packedValue, byte startBit, byte endBit)
        {

            int unpackedValue;

            if (endBit < 31)
            {
                if (startBit == 0)
                {
                    unpackedValue = (int)(packedValue & CreateMask(0, endBit));
                }
                else
                {
                    unpackedValue = (int)((long)(packedValue / Math.Pow(2, startBit)) & CreateMask(0, (byte)(endBit - startBit)));
                }
            }
            else
            {
                unpackedValue = (int)(NumberConversion.Int32ToUnsigned(packedValue) / Math.Pow(2, startBit));
            }

            return unpackedValue;
        }

        /// <summary>
        /// Extracts packed intensity data
        /// </summary>
        /// <param name="PackedBasePeakIntensity"></param>
        /// <param name="PackedBasePeakInfo"></param>
        /// <param name="acquisitionDataType"></param>
        /// <returns></returns>
        private float UnpackIntensity(short PackedBasePeakIntensity, int PackedBasePeakInfo, short acquisitionDataType)
        {
            // See note for Acquisition Data Types 9 to 12 below
            float unpackedIntensity;

            switch (acquisitionDataType)
            {
                case 9:
                case 10:
                case 11:
                case 12:
                    // Includes type 9, 11, and 12; type 10 is officially unused
                    //  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                    // Note: Only use this function to unpack intensities for data in the .IDX file, not for data in the .DAT file
                    //       See the NativeIOGetSpectrum function for the method of unpacking intensities in .DAT files
                    unpackedIntensity = (float)(PackedBasePeakIntensity * Math.Pow(4, PackedBasePeakInfo & maskBPIntensityScale));
                    //Debug.Assert unpackedIntensity = PackedBasePeakIntensity * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 3)
                    break;

                case 0:
                    // Compressed data
                    unpackedIntensity = (float)((short)(PackedBasePeakInfo & maskBPCompressedDataIntensity) / 8f * Math.Pow(4, PackedBasePeakInfo & maskBPCompressedDataIntensityScale));
                    //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(PackedBasePeakInfo, 3, 10) * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 2)
                    break;

                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    // Standard data and Uncalibrated data
                    unpackedIntensity = (float)((short)(PackedBasePeakIntensity & maskBPStandardDataIntensity) / 8f * Math.Pow(4, PackedBasePeakIntensity & maskBPStandardDataIntensityScale));
                    //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 3, 15) * 4 ^ ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 0, 2)
                    break;

                case 8:
                    // High intensity calibrated data
                    unpackedIntensity = (float)(PackedBasePeakIntensity * Math.Pow(4, PackedBasePeakInfo & maskBPIntensityScale));
                    break;

                default:
                    unpackedIntensity = 0;
                    break;
            }
            return unpackedIntensity;

        }

        /// <summary>
        /// Extracts packed mass data
        /// </summary>
        /// <param name="PackedBasePeakInfo"></param>
        /// <param name="acquisitionDataType"></param>
        /// <param name="processingFunctionIndexFile"></param>
        /// <returns></returns>
        private double UnpackMass(int PackedBasePeakInfo, short acquisitionDataType, bool processingFunctionIndexFile)
        {
            // See note for Acquisition Data Types 9 to 12 below
            double unpackedMass;

            switch (acquisitionDataType)
            {
                case 9:
                case 10:
                case 11:
                case 12:
                    // Includes type 9, 11, and 12; type 10 is officially unused
                    //  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                    // Note: Only use this function to unpack masses for data in the .IDX file, not for data in the .DAT file
                    //       See the NativeIOGetSpectrum function for the method of unpacking masses in .DAT files

                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 9 bits by dividing by 2^9
                    // It would be more straightforward to use PackedBasePeakInfo And CreateMask(9, 31) but VB won't let us
                    //  And a Currency value with a Long; this gives an OverFlow error
                    var MassMantissa = (int)(NumberConversion.Int32ToUnsigned(PackedBasePeakInfo) / 512f);      // 512 = 2^9

                    // Compute the MassExponent value by multiplying the Packed value by the appropriate BitMask, then right shifting 4 bits by dividing by 2^4
                    var MassExponent = (short)(PackedBasePeakInfo & maskBPMassExponent) / 16f;                  // 16 = 2^4

                    if (processingFunctionIndexFile)
                    {
                        // When computing the BasePeakMass based on data in the _func001.idx
                        //  file, must bump up the Mass Exponent by 8 in order to multiply the
                        //  Mass Mantissa by an additional value of 256 to get the correct value
                        if (MassExponent < 6)
                        {
                            if (acquisitionDataType == 9)
                            {
                                // This only seems to be necessary for files with Acquisition data type 9
                                // The following Assertion is here to test for that
                                MassExponent = MassExponent + 8;
                            }
                        }
                    }

                    // Note that we divide by 2^23 to convert the mass mantissa to fractional form
                    unpackedMass = MassMantissa / 8388608f * Math.Pow(2, MassExponent);      // 8388608 = 2^23
                    break;

                case 0:
                    // Compressed data
                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 11 bits by dividing by 2^11
                    // It would be more straightforward to use PackedBasePeakInfo And CreateMask(11, 31) but VB won't let us
                    //  And a Currency value with a Long; this gives an OverFlow error
                    // We must divide the MassMantissa by 128 to get the mass
                    unpackedMass = (int)(NumberConversion.Int32ToUnsigned(PackedBasePeakInfo) / 2048f) / 128f;      // 2048 = 2^11
                    // Debug.Assert(unpackedMass == ExtractFromBitsInt32(PackedBasePeakInfo, 11, 31) / 128f);
                    break;

                case 1:
                    // Standard data
                    // We must divide the MassMantissa by 1024 to get the mass
                    unpackedMass = (short)(PackedBasePeakInfo & maskBPStandardDataMass) / 1024f;
                    break;

                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    // Uncalibrated data
                    // This type of data doesn't have a base peak mass
                    unpackedMass = 0;
                    break;

                case 8:
                    // High intensity calibrated data
                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 4 bits by dividing by 2^4
                    // We must divide the MassMantissa by 128 to get the mass
                    unpackedMass = (int)(NumberConversion.Int32ToUnsigned(PackedBasePeakInfo) / 16f) / 128f;        // 16 = 2^4
                    //Debug.Assert(unpackedMass == ExtractFromBitsInt32(PackedBasePeakInfo, 4, 31) / 128f);
                    break;

                default:
                    unpackedMass = 0;
                    break;
            }
            return unpackedMass;

        }

        private static class NumberConversion
        {

            private const long OFFSET_4 = 4294967296L;
            private const long MAXINT_4 = 2147483647;
            private const Int32 OFFSET_2 = 65536;

            private const Int16 MAXINT_2 = 32767;
            public static Int32 UnsignedToInt32(long value)
            {
                if (value <= MAXINT_4)
                {
                    return (Int32)value;
                }
                else
                {
                    return (Int32)(value - OFFSET_4);
                }
            }

            public static long Int32ToUnsigned(Int32 value)
            {
                if (value < 0)
                {
                    return value + OFFSET_4;
                }

                return value;
            }

            public static Int16 UnsignedToInt16(Int32 value)
            {
                if (value < 0 || value >= OFFSET_2)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value <= MAXINT_2)
                {
                    return (Int16)value;
                }
                else
                {
                    return (Int16)(value - OFFSET_2);
                }
            }

            public static Int32 Int16ToUnsigned(Int16 value)
            {
                if (value < 0)
                {
                    return value + OFFSET_2;
                }

                return value;
            }

            public static bool ValueToBool(int value)
            {
                return value != 0;
            }
        }

    }
}
