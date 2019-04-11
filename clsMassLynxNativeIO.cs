using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MSFileInfoScanner
{
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

        // This class can read data from MassLynx data files using native disk access,
        //  obviating the need to have MassLynx installed
        // Note that native file IO is significantly slower than utilizing the
        //  MassLynx API access functions (see clsMassLynxReader3 and clsMassLynxReader4)
        //
        // Written by Matthew Monroe for PNNL in Jan 2004
        // Portions of this code were written at UNC in 2001
        //
        // VB6 version Last modified January 22, 2004
        // Updated to VB.NET September 17, 2005, though did not upgrade the extended function info functions or data point reading options
        // Updated to C# in May 2016

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

        // The udtRawScanIndexRecordType data read from the file is stored in this structure
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

        public struct udtMSHeaderInfoType
        {
            public string AcquDate;
            public string AcquName;
            public string AcquTime;
            public string JobCode;
            public string TaskCode;
            public string UserName;
            public string Instrument;
            public string InstrumentType;
            public string Conditions;
            public string LabName;
            public string SampleDesc;
            public float SolventDelay;
            public string Submitter;
            public string SampleID;
            public string BottleNumber;
            public string PlateDesc;
            public int MuxStream;
            public int VersionMajor;
            public int VersionMinor;
            public short CalMS1StaticCoefficientCount;
            public double[] CalMS1StaticCoefficients;

            // 0 = normal, 1 = Root mass
            public short CalMS1StaticTypeID;
            public short CalMS2StaticCoefficientCount;
            public double[] CalMS2StaticCoefficients;

            // 0 = normal, 1 = Root mass
            public short CalMS2StaticTypeID;
        }

        private struct udtScanStatsType
        {
            public int PeakCount;
            public bool Calibrated;
            public bool Continuum;
            public bool Overload;
            public float MassStart;
            public float MassEnd;

            // MS/MS Parent Ion Mass
            public float SetMass;

            // Base peak intensity
            public float BPI;

            // Base peak mass
            public float BPIMass;
            public float TIC;
            public float RetentionTime;
        }

        public struct udtMSFunctionInfoType
        {
            // The function number that this data corresponds to
            public int FunctionNumber;
            public short ProcessNumber;
            public float StartRT;
            public float EndRT;

            // 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
            // 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
            // 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
            // 0 for MS-only; 1 for MS/MS
            public short FunctionTypeID;

            public short FunctionType;
            public string FunctionTypeText;
            public float StartMass;
            public float EndMass;
            public int ScanCount;
            public short IonMode;

            // 0=Compressed scan, 1=Standard Scan, 2=SIR or MRM Data, 3=Scanning Continuum,
            public short AcquisitionDataType;

            // 4=MCA Data, 5=MCA data with SD, 6=MCB data, 7=MCB data with SD
            // 8=Molecular weight data, 9=High accuracy calibrated data
            // 10=Single float precision (not used), 11=Enhanced uncalibrated data
            // 12=Enhanced calibrated data
            // in seconds
            public float CycleTime;

            // in seconds
            public float InterScanDelay;

            // in eV
            public short MsMsCollisionEnergy;
            public short MSMSSegmentOrChannelCount;
            public float FunctionSetMass;

            // in seconds
            public float InterSegmentChannelTime;

            // Should be 0 or 6 or 7  (typically 6 coefficients)
            public short CalibrationCoefficientCount;

            // Calibration coefficients
            public double[] CalibrationCoefficients;

            // 0 = normal, 1 = Root mass
            public short CalTypeID;

            // Calibration standard deviation
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
            // Returns information on the given MassLynx data file (actually a directory)
            // Returns True if success, false if failure

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

        public bool GetFileInfo(string massLynxDataDirectoryPath, out udtMSHeaderInfoType udtHeaderInfo)
        {
            // Returns information on the given MassLynx data file (actually a directory)
            // Returns True if success, false if failure

            bool success;

            udtHeaderInfo = new udtMSHeaderInfoType();

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    udtHeaderInfo = mMSData.HeaderInfo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.GetFileInfo:" + ex.Message);
                success = false;
            }

            return success;

        }

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
            // Returns information on the given function
            // Returns True if success, false if failure

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

        public bool GetFunctionInfo(string massLynxDataDirectoryPath, int functionNumber, out udtMSFunctionInfoType udtFunctionInfo)
        {
            // Returns information on the given function
            // Returns True if success, false if failure

            bool success;

            udtFunctionInfo = new udtMSFunctionInfoType();

            try
            {
                success = ValidateDataFolder(massLynxDataDirectoryPath);
                if (success)
                {
                    if (functionNumber >= 1 && functionNumber <= mMSData.FunctionCount)
                    {
                        udtFunctionInfo = mMSData.FunctionInfo[functionNumber];
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

        public int GetFunctionCount(string massLynxDataDirectoryPath)
        {
            // Function returns the number of functions in the datafile
            // Returns 0 if an error

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

        public int GetNumScans(string massLynxDataDirectoryPath, int functionNumber = 1)
        {
            // Function returns the number of scans for the given function
            // Returns 0 if an error

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
            // Returns scan information in the ByRef variables
            // Function returns True if no error, False if an error
            //
            // Note that ScanType = 0 means MS-only scan (survey scan)
            // ScanType > 0 means ms/ms scan

            return GetScanInfoEx(massLynxDataDirectoryPath, functionNumber, scanNumber,
                                 out scanType, out basePeakMZ, out parentIonMZ, out retentionTime,
                                 out basePeakIntensity, out totalIonCurrent, out _,
                                 out _, out _, out _, out _);
        }

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

            var udtScanStatsSingleScan = default(udtScanStatsType);

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

            LoadMSScanHeader(ref udtScanStatsSingleScan, mMSData, functionNumber, scanNumber);

            scanType = mMSData.FunctionInfo[functionNumber].FunctionType;
            basePeakMZ = udtScanStatsSingleScan.BPIMass;
            parentIonMZ = udtScanStatsSingleScan.SetMass;
            retentionTime = udtScanStatsSingleScan.RetentionTime;
            basePeakIntensity = udtScanStatsSingleScan.BPI;
            totalIonCurrent = udtScanStatsSingleScan.TIC;
            calibrated = udtScanStatsSingleScan.Calibrated;
            continuum = udtScanStatsSingleScan.Continuum;
            overload = udtScanStatsSingleScan.Overload;
            massStart = udtScanStatsSingleScan.MassStart;
            massEnd = udtScanStatsSingleScan.MassEnd;
            return true;
        }

        private void InitializeFunctionInfo(ref udtMSFunctionInfoType udtMSFunctionInfo, int functionNumber)
        {
            udtMSFunctionInfo.FunctionNumber = functionNumber;
            udtMSFunctionInfo.ProcessNumber = 0;

            udtMSFunctionInfo.CalibrationCoefficientCount = 0;
            udtMSFunctionInfo.CalibrationCoefficients = new double[7];

            udtMSFunctionInfo.CalTypeID = 0;
            udtMSFunctionInfo.CalStDev = 0;
        }

        private void InitializeNativeFunctionInfo(ref udtRawFunctionDescriptorRecordType udtNativeFunctionInfo)
        {
            udtNativeFunctionInfo.SegmentScanTimes = new int[32];
            udtNativeFunctionInfo.SegmentStartMasses = new int[32];
            udtNativeFunctionInfo.SegmentEndMasses = new int[32];

        }

        public bool IsFunctionMsMs(string massLynxDataDirectoryPath, int functionNumber)
        {

            if (GetFunctionInfo(massLynxDataDirectoryPath, functionNumber, out short functionType))
            {
                return functionType != 0;
            }

            return false;
        }

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

        public bool IsMassLynxData(string massLynxDataDirectoryPath)
        {
            // massLynxDataDirectoryPath should contain the path to a directory that ends in the text .RAW
            // If massLynxDataDirectoryPath contains the path to a file, then the ValidateDataFolder function
            //  will strip off the filename and only examine the directory

            return ValidateDataFolder(massLynxDataDirectoryPath);

        }

        public bool IsMassLynxInstalled()
        {
            // This function is included for compatibility with MassLynxReader3 and MassLynxReader4
            // It always returns True since this class doesn't require MassLynx
            return true;
        }

        private bool LoadMSFileHeader(ref udtMSDataType udtThisMSData, string massLynxDataDirectoryPath)
        {

            // Verifies that massLynxDataDirectoryPath exists
            // Loads the header information for the given MassLynx directory path
            // Returns True if success, false if failure

            var success = false;

            try
            {

                if (Directory.Exists(massLynxDataDirectoryPath))
                {
                    // Read the header information from the current file
                    success = NativeIOReadHeader(massLynxDataDirectoryPath, out udtThisMSData.HeaderInfo);

                    udtThisMSData.FunctionCount = 0;
                }
                else
                {
                    SetErrorCode(eErrorCodeConstants.InvalidDataFolderPath);
                    udtThisMSData.FunctionCount = 0;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFileHeader:" + ex.Message);

                if (!success)
                {
                    // Assume invalid data file
                    SetErrorCode(eErrorCodeConstants.DataFolderReadError);
                    udtThisMSData.FunctionCount = 0;
                }
            }

            return success;
        }

        private int LoadMSFunctionInfo(ref udtMSDataType udtThisMSData, string massLynxDataDirectoryPath)
        {

            // Determines the number of functions in the given data file
            // Returns the function count, or 0 on failure

            var udtScanIndexRecord = default(udtScanIndexRecordType);

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

                if (LoadMSFileHeader(ref udtThisMSData, cleanMassLynxDataFolderPath))
                {
                    udtThisMSData.UserSuppliedDataDirPath = massLynxDataDirectoryPath;
                    udtThisMSData.CurrentDataDirPath = cleanMassLynxDataFolderPath;

                    // Use sFuncInfo to read the header information from the current file
                    udtThisMSData.FunctionCount = NativeIOGetFunctionCount(ref cleanMassLynxDataFolderPath);

                    if (udtThisMSData.FunctionCount > 0)
                    {
                        fileValidated = true;
                        udtThisMSData.FunctionInfo = new udtMSFunctionInfoType[udtThisMSData.FunctionCount + 1];

                        // Note that the function array is 1-based
                        for (var functionNumber = 1; functionNumber <= udtThisMSData.FunctionCount; functionNumber++)
                        {
                            InitializeFunctionInfo(ref udtThisMSData.FunctionInfo[functionNumber], functionNumber);

                            if (NativeIOGetFunctionInfo(cleanMassLynxDataFolderPath, ref udtThisMSData.FunctionInfo[functionNumber]))
                            {
                                float startMass;
                                float endMass;
                                if (udtThisMSData.FunctionInfo[functionNumber].ScanCount > 0)
                                {
                                    NativeIOGetScanInfo(cleanMassLynxDataFolderPath, udtThisMSData.FunctionInfo[functionNumber], 1, ref udtScanIndexRecord);

                                    // ToDo: Get the Start and End mass for the given scan
                                    startMass = 0;
                                    endMass = 0;

                                    // Since the first scan may not have the full mass range, we'll also check a scan
                                    // in the middle of the file as a random comparison
                                    if (udtThisMSData.FunctionInfo[functionNumber].ScanCount >= 3)
                                    {
                                        //Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, CLng(.ScanCount / 3))
                                        //If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                        //If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                    }

                                    if (udtThisMSData.FunctionInfo[functionNumber].ScanCount >= 2)
                                    {
                                        //Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, CLng(.ScanCount / 2))
                                        //If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                        //If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                    }

                                    //Call sScanStats.GetScanStats(cleanMassLynxDataFolderPath, functionNumber, .ProcessNumber, .ScanCount)
                                    //If sScanStats.LoMass < startMass Then startMass = scanStats.LoMass
                                    //If sScanStats.HiMass > endMass Then endMass = scanStats.HiMass
                                }
                                else
                                {
                                    startMass = 0;
                                    endMass = 0;
                                }

                                udtThisMSData.FunctionInfo[functionNumber].StartMass = startMass;
                                udtThisMSData.FunctionInfo[functionNumber].EndMass = endMass;
                            }
                            else
                            {
                                udtThisMSData.FunctionInfo[functionNumber].ScanCount = 0;
                            }
                        }
                    }
                    else
                    {
                        udtThisMSData.FunctionCount = 0;
                    }

                    if (udtThisMSData.FunctionCount > 0)
                    {
                        NativeIOReadCalInfoFromHeader(ref udtThisMSData);
                    }
                }
                else
                {
                    udtThisMSData.FunctionCount = 0;
                }

                return udtThisMSData.FunctionCount;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFunctionInfo:" + ex.Message);

                if (!fileValidated)
                {
                    // Assume invalid data file
                    SetErrorCode(eErrorCodeConstants.DataFolderReadError);
                    udtThisMSData.FunctionCount = 0;
                }

                return udtThisMSData.FunctionCount;
            }

        }

        private void LoadMSScanHeader(ref udtScanStatsType udtScanStatsSingleScan, udtMSDataType udtThisMSData, int functionNumber, int scanNumber)
        {
            // Loads information on the given scan for the given function
            // Returns the number of peaks in the scan; returns 0 if an error
            //
            // Note that the calling function must validate that functionNumber is valid
            // Since this function uses mMSData.FunctionInfo, one must call NativeIOGetFunctionInfo
            //  to populate .FunctionInfo before calling this function

            var udtScanIndexRecord = default(udtScanIndexRecordType);

            try
            {

                udtScanStatsSingleScan.PeakCount = 0;

                udtScanStatsSingleScan.Calibrated = false;
                udtScanStatsSingleScan.Continuum = false;
                udtScanStatsSingleScan.Overload = false;

                udtScanStatsSingleScan.MassStart = 0;
                udtScanStatsSingleScan.MassEnd = 0;
                udtScanStatsSingleScan.SetMass = 0;

                udtScanStatsSingleScan.BPI = 0;
                udtScanStatsSingleScan.BPIMass = 0;
                udtScanStatsSingleScan.TIC = 0;

                udtScanStatsSingleScan.PeakCount = 0;
                udtScanStatsSingleScan.RetentionTime = 0;

                if (NativeIOGetScanInfo(udtThisMSData.CurrentDataDirPath, udtThisMSData.FunctionInfo[functionNumber], scanNumber, ref udtScanIndexRecord))
                {
                    udtScanStatsSingleScan.Calibrated = udtScanIndexRecord.ScanContainsCalibratedMasses;
                    udtScanStatsSingleScan.Continuum = udtScanIndexRecord.ContinuumDataOverride;
                    udtScanStatsSingleScan.Overload = udtScanIndexRecord.ScanOverload;

                    udtScanStatsSingleScan.MassStart = udtScanIndexRecord.LoMass;
                    udtScanStatsSingleScan.MassEnd = udtScanIndexRecord.HiMass;

                    udtScanStatsSingleScan.BPI = udtScanIndexRecord.BasePeakIntensity;
                    udtScanStatsSingleScan.BPIMass = udtScanIndexRecord.BasePeakMass;
                    udtScanStatsSingleScan.TIC = udtScanIndexRecord.TicValue;

                    udtScanStatsSingleScan.PeakCount = udtScanIndexRecord.NumSpectralPeaks;
                    udtScanStatsSingleScan.RetentionTime = udtScanIndexRecord.ScanTime;

                    udtScanStatsSingleScan.SetMass = udtScanIndexRecord.SetMass;

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

            if (mMSData.FunctionCount == 0 || mMSData.UserSuppliedDataDirPath.ToLower() != massLynxDataDirectoryPath.ToLower())
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

        //---------------------------------------------------------
        // The following functions are used for Native file IO
        //---------------------------------------------------------

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

        private long CreateMask(byte startBit, byte endBit)
        {
            // Note: The mask needs to be long data type to allow for unsigned Int32 masks
            // This is because the VB Int32 data type has a maximum value of 2^32 / 2 - 1 while
            //  unsigned Int32 can be up to 2^32-1

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

        private int NativeIOGetFunctionCount(ref string dataDirPath)
        {
            // Returns the number of functions, 0 if an error
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

        private bool NativeIOGetFunctionInfo(string dataDirPath, ref udtMSFunctionInfoType udtMSFunctionInfo)
        {
            // Returns True if success, False if failure

            var udtNativeFunctionInfo = new udtRawFunctionDescriptorRecordType();
            InitializeNativeFunctionInfo(ref udtNativeFunctionInfo);

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

                    if (udtMSFunctionInfo.FunctionNumber < 1 || udtMSFunctionInfo.FunctionNumber > functionCount)
                    {
                        return false;
                    }

                    // Since we're using Binary Access, we need to specify the Byte Offset to start reading at
                    // The first byte is 1, and that is where Function 1 can be found
                    // Function 2 can be found NATIVE_FUNCTION_INFO_SIZE_BYTES+1 bytes into the file

                    reader.BaseStream.Seek((udtMSFunctionInfo.FunctionNumber - 1) * NATIVE_FUNCTION_INFO_SIZE_BYTES,
                                             SeekOrigin.Begin);

                    udtNativeFunctionInfo.PackedFunctionInfo = reader.ReadInt16();
                    udtNativeFunctionInfo.CycleTime = reader.ReadSingle();
                    udtNativeFunctionInfo.InterScanDelay = reader.ReadSingle();
                    udtNativeFunctionInfo.StartRT = reader.ReadSingle();
                    udtNativeFunctionInfo.EndRT = reader.ReadSingle();
                    udtNativeFunctionInfo.ScanCount = reader.ReadInt32();

                    // Packed MS/MS Info:
                    //   bits 0-7: collision energy
                    //   bits 8-15: segment/channel count
                    udtNativeFunctionInfo.PackedMSMSInfo = reader.ReadInt16();

                    // The following are more MS/MS parameters
                    udtNativeFunctionInfo.FunctionSetMass = reader.ReadSingle();
                    udtNativeFunctionInfo.InterSegmentChannelTime = reader.ReadSingle();

                    // Up to 32 segment scans can be conducted for a MS/MS run
                    // The following three arrays store the segment times, start, and end masses
                    for (var index = 0; index <= 31; index++)
                    {
                        udtNativeFunctionInfo.SegmentScanTimes[index] = reader.ReadInt32();
                    }
                    for (var index = 0; index <= 31; index++)
                    {
                        udtNativeFunctionInfo.SegmentStartMasses[index] = reader.ReadInt32();
                    }
                    for (var index = 0; index <= 31; index++)
                    {
                        udtNativeFunctionInfo.SegmentEndMasses[index] = reader.ReadInt32();
                    }

                } // end using

                var success = true;

                if (udtNativeFunctionInfo.PackedFunctionInfo == 0 &&
                    Math.Abs(udtNativeFunctionInfo.CycleTime) < float.Epsilon &&
                    Math.Abs(udtNativeFunctionInfo.InterScanDelay) < float.Epsilon)
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
                    // Copy data from udtNativeFunctionInfo to udtFunctionInfo
                    udtMSFunctionInfo.FunctionTypeID = (short)(udtNativeFunctionInfo.PackedFunctionInfo & maskFunctionType);

                    // 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
                    // 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
                    // 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
                    udtMSFunctionInfo.FunctionType = 0;
                    switch (udtMSFunctionInfo.FunctionTypeID)
                    {
                        case 0:
                            udtMSFunctionInfo.FunctionTypeText = "MS";
                            break;
                        case 1:
                            udtMSFunctionInfo.FunctionTypeText = "SIR";
                            break;
                        case 2:
                            udtMSFunctionInfo.FunctionTypeText = "DLY";
                            break;
                        case 3:
                            udtMSFunctionInfo.FunctionTypeText = "CAT";
                            break;
                        case 4:
                            udtMSFunctionInfo.FunctionTypeText = "OFF";
                            break;
                        case 5:
                            udtMSFunctionInfo.FunctionTypeText = "PAR";
                            break;
                        case 6:
                            udtMSFunctionInfo.FunctionTypeText = "DAU";
                            udtMSFunctionInfo.FunctionType = 1;
                            break;
                        case 7:
                            udtMSFunctionInfo.FunctionTypeText = "NL";
                            break;
                        case 8:
                            udtMSFunctionInfo.FunctionTypeText = "NG";
                            break;
                        case 9:
                            udtMSFunctionInfo.FunctionTypeText = "MRM";
                            break;
                        case 10:
                            udtMSFunctionInfo.FunctionTypeText = "Q1F";
                            break;
                        case 11:
                            udtMSFunctionInfo.FunctionTypeText = "MS2";
                            udtMSFunctionInfo.FunctionType = 1;
                            break;
                        case 12:
                            udtMSFunctionInfo.FunctionTypeText = "DAD";
                            break;
                        case 13:
                            udtMSFunctionInfo.FunctionTypeText = "TOF";
                            break;
                        case 14:
                            udtMSFunctionInfo.FunctionTypeText = "PSD";
                            break;
                        case 16:
                            udtMSFunctionInfo.FunctionTypeText = "TOF MS/MS";
                            udtMSFunctionInfo.FunctionType = 1;
                            break;
                        case 17:
                            udtMSFunctionInfo.FunctionTypeText = "TOF MS";
                            break;
                        case 18:
                            udtMSFunctionInfo.FunctionTypeText = "TOF MS";
                            break;
                        default:
                            udtMSFunctionInfo.FunctionTypeText = "MS Unknown";
                            break;
                    }

                    udtMSFunctionInfo.IonMode = (short)((short)(udtNativeFunctionInfo.PackedFunctionInfo & maskIonMode) / 32f);     // 32 = 2^5
                    udtMSFunctionInfo.AcquisitionDataType = (short)((short)(udtNativeFunctionInfo.PackedFunctionInfo & maskAcquisitionDataType) / 1024f);    // 1024 = 2^10

                    udtMSFunctionInfo.CycleTime = udtNativeFunctionInfo.CycleTime;
                    udtMSFunctionInfo.InterScanDelay = udtNativeFunctionInfo.InterScanDelay;
                    udtMSFunctionInfo.StartRT = udtNativeFunctionInfo.StartRT;
                    udtMSFunctionInfo.EndRT = udtNativeFunctionInfo.EndRT;

                    udtMSFunctionInfo.MsMsCollisionEnergy = (short)(udtNativeFunctionInfo.PackedMSMSInfo & maskCollisionEnergy);
                    udtMSFunctionInfo.MSMSSegmentOrChannelCount = (short)(NumberConversion.Int32ToUnsigned(udtNativeFunctionInfo.PackedMSMSInfo) / 256f);      // 256 = 2^8

                    udtMSFunctionInfo.FunctionSetMass = udtNativeFunctionInfo.FunctionSetMass;
                    udtMSFunctionInfo.InterSegmentChannelTime = udtNativeFunctionInfo.InterSegmentChannelTime;

                    // Since udtNativeFunctionInfo.ScanCount is always 0, we need to use NativeIOGetScanCount instead
                    var scanCount = NativeIOGetScanCount(dataDirPath, ref udtMSFunctionInfo);
                    if (udtMSFunctionInfo.ScanCount != scanCount)
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

        private int NativeIOGetScanCount(string dataDirPath, ref udtMSFunctionInfoType udtMSFunctionInfo)
        {
            // Returns the number of scans for the given function
            // Also updates udtMSFunctionInfo.ScanCount
            // Returns 0 if an error
            //
            // Note that udtMSFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount

            try
            {

                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(udtMSFunctionInfo.FunctionNumber) + ".idx");
                var indexFile = new FileInfo(indexFilePath);

                var numberOfScansInFunction = 0;
                if (indexFile.Exists)
                {
                    // The ScanCount stored in the function index file is always 0 rather than the correct number of scans
                    // Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
                    //  by the size of each udtRawScanIndexRecordType

                    numberOfScansInFunction = (int)(indexFile.Length / RAW_SCAN_INDEX_RECORD_SIZE);
                    udtMSFunctionInfo.ScanCount = numberOfScansInFunction;

                }

                return numberOfScansInFunction;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetScanCount:" + ex.Message);
                return 0;
            }

        }

        private bool NativeIOGetScanInfo(
            string dataDirPath,
            udtMSFunctionInfoType udtMSFunctionInfo,
            int scanNumber,
            ref udtScanIndexRecordType udtScanIndexRecord,
            bool scanOffsetAndPeakCountOnly = false)
        {

            // Returns information on the given scan for the given function
            // Returns True if success, False if failure
            //
            // Note that udtMSFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount

            // This udt is used for most files
            var udtNativeScanIndexRecord = default(udtRawScanIndexRecordType);

            // This udt is used for files with udtMSFunctionInfo.AcquisitionDataType = 0
            // The difference is that udtRawScanIndexRecordType ends in an Integer then a Long
            //  while this udt ends in a Long, then an Integer
            // When this udt is used, its values are copied to udtNativeScanIndexRecord directly after reading
            var udtNativeScanIndexRecordCompressedScan = default(udtRawScanIndexRecordCompressedScanType);

            var success = false;

            try
            {

                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(udtMSFunctionInfo.FunctionNumber) + ".idx");
                // indexFilePath  = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(ref udtMSFunctionInfo.FunctionNumber) + ".sts");

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

                        if (udtMSFunctionInfo.AcquisitionDataType == 0)
                        {
                            // File saved with Acquisition Data Type 0

                            udtNativeScanIndexRecordCompressedScan.StartScanOffset = reader.ReadInt32();
                            udtNativeScanIndexRecordCompressedScan.PackedScanInfo = reader.ReadInt32();
                            udtNativeScanIndexRecordCompressedScan.TicValue = reader.ReadSingle();
                            udtNativeScanIndexRecordCompressedScan.ScanTime = reader.ReadSingle();
                            udtNativeScanIndexRecordCompressedScan.PackedBasePeakInfo = reader.ReadInt32();
                            udtNativeScanIndexRecordCompressedScan.Spare = reader.ReadInt16();

                            // Copy from udtNativeScanIndexRecordCompressedScan to udtNativeScanIndexRecord
                            udtNativeScanIndexRecord.StartScanOffset = udtNativeScanIndexRecordCompressedScan.StartScanOffset;
                            udtNativeScanIndexRecord.PackedScanInfo = udtNativeScanIndexRecordCompressedScan.PackedScanInfo;

                            udtNativeScanIndexRecord.TicValue = udtNativeScanIndexRecordCompressedScan.TicValue;
                            udtNativeScanIndexRecord.ScanTime = udtNativeScanIndexRecordCompressedScan.ScanTime;

                            // Unused
                            udtNativeScanIndexRecord.PackedBasePeakIntensity = 0;

                            udtNativeScanIndexRecord.PackedBasePeakInfo = udtNativeScanIndexRecordCompressedScan.PackedBasePeakInfo;
                        }
                        else
                        {
                            // File saved with Acquisition Data Type other than 0
                            udtNativeScanIndexRecord.StartScanOffset = reader.ReadInt32();
                            udtNativeScanIndexRecord.PackedScanInfo = reader.ReadInt32();
                            udtNativeScanIndexRecord.TicValue = reader.ReadSingle();
                            udtNativeScanIndexRecord.ScanTime = reader.ReadSingle();
                            udtNativeScanIndexRecord.PackedBasePeakIntensity = reader.ReadInt16();
                            udtNativeScanIndexRecord.PackedBasePeakInfo = reader.ReadInt32();
                        }

                        success = true;
                    }
                }

                if (success)
                {
                    udtScanIndexRecord.StartScanOffset = udtNativeScanIndexRecord.StartScanOffset;

                    udtScanIndexRecord.NumSpectralPeaks = udtNativeScanIndexRecord.PackedScanInfo & maskSpectralPeak;

                    if (!scanOffsetAndPeakCountOnly)
                    {
                        // 4194304 = 2^22
                        udtScanIndexRecord.SegmentNumber = (short)((short)(udtNativeScanIndexRecord.PackedScanInfo & maskSegment) / 4194304);

                        udtScanIndexRecord.UseFollowingContinuum = NumberConversion.ValueToBool(udtNativeScanIndexRecord.PackedScanInfo & maskUseFollowingContinuum);
                        udtScanIndexRecord.ContinuumDataOverride = NumberConversion.ValueToBool(udtNativeScanIndexRecord.PackedScanInfo & maskContinuumDataOverride);
                        udtScanIndexRecord.ScanContainsMolecularMasses = NumberConversion.ValueToBool(udtNativeScanIndexRecord.PackedScanInfo & maskScanContainsMolecularMasses);
                        udtScanIndexRecord.ScanContainsCalibratedMasses = NumberConversion.ValueToBool(udtNativeScanIndexRecord.PackedScanInfo & maskScanContainsCalibratedMasses);
                        if (udtNativeScanIndexRecord.PackedScanInfo != Math.Abs(udtNativeScanIndexRecord.PackedScanInfo))
                        {
                            udtScanIndexRecord.ScanOverload = true;
                        }

                        udtScanIndexRecord.TicValue = udtNativeScanIndexRecord.TicValue;
                        udtScanIndexRecord.ScanTime = udtNativeScanIndexRecord.ScanTime;

                        udtScanIndexRecord.BasePeakIntensity = (int)UnpackIntensity(udtNativeScanIndexRecord.PackedBasePeakIntensity, udtNativeScanIndexRecord.PackedBasePeakInfo, udtMSFunctionInfo.AcquisitionDataType);

                        udtScanIndexRecord.BasePeakMass = (float)UnpackMass(udtNativeScanIndexRecord.PackedBasePeakInfo, udtMSFunctionInfo.AcquisitionDataType, true);

                        // ToDo: May need to calibrate the base peak mass
                        // udtScanIndexRecord.BasePeakMass = udtScanIndexRecord.BasePeakMass;

                        udtScanIndexRecord.LoMass = 0;

                        // ToDo: Figure out if this can be read from the FunctionIndex file
                        udtScanIndexRecord.HiMass = 0;
                        udtScanIndexRecord.SetMass = 0;
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

        private bool NativeIOReadCalInfoFromHeader(ref udtMSDataType udtThisMSData)
        {

            // Looks for the "$$ Cal Function" lines in the _HEADER.TXT file
            // Returns True if successful, False if not
            //
            // This function should only be called by LoadMSFunctionInfo and only after the functions have been determined

            const string CAL_FUNCTION_NAME = "CAL FUNCTION";
            const string CAL_STDDEV_FUNCTION_NAME = "CAL STDDEV FUNCTION";

            try
            {

                var headerFilePath = Path.Combine(udtThisMSData.CurrentDataDirPath, "_HEADER.TXT");
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
                            if (functionNumber >= 1 && functionNumber <= udtThisMSData.FunctionCount)
                            {

                                NativeIOParseCalibrationCoefficients(
                                    keyValue,
                                    out udtThisMSData.FunctionInfo[functionNumber].CalibrationCoefficientCount,
                                    udtThisMSData.FunctionInfo[functionNumber].CalibrationCoefficients,
                                    out udtThisMSData.FunctionInfo[functionNumber].CalTypeID);

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
                            if (functionNumber >= 1 && functionNumber <= udtThisMSData.FunctionCount)
                            {
                                if (double.TryParse(keyValue, out var calStdDev))
                                {
                                    udtThisMSData.FunctionInfo[functionNumber].CalStDev = calStdDev;
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

        private bool NativeIOReadHeader(string dataDirPath, out udtMSHeaderInfoType headerInfo)
        {

            // Duplicates job of DMRawReadHeader to avoid need for calling Dll
            // Returns True if successful, False if not

            headerInfo = new udtMSHeaderInfoType
            {
                AcquDate = "",
                AcquName = "",
                AcquTime = "",
                JobCode = "",
                TaskCode = "",
                UserName = "",
                Instrument = "",
                InstrumentType = "",
                Conditions = "",
                LabName = "",
                SampleDesc = "",
                SolventDelay = 0,
                Submitter = "",
                SampleID = "",
                BottleNumber = "",
                PlateDesc = "",
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
                    break;
                //Debug.Assert unpackedIntensity = PackedBasePeakIntensity * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 3)
                case 0:
                    // Compressed data
                    unpackedIntensity = (float)((short)(PackedBasePeakInfo & maskBPCompressedDataIntensity) / 8f * Math.Pow(4, PackedBasePeakInfo & maskBPCompressedDataIntensityScale));
                    break;
                //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(PackedBasePeakInfo, 3, 10) * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 2)
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    // Standard data and Uncalibrated data
                    unpackedIntensity = (float)((short)(PackedBasePeakIntensity & maskBPStandardDataIntensity) / 8f * Math.Pow(4, PackedBasePeakIntensity & maskBPStandardDataIntensityScale));
                    break;
                //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 3, 15) * 4 ^ ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 0, 2)
                case 8:
                    //  High intensity calibrated data
                    unpackedIntensity = (float)(PackedBasePeakIntensity * Math.Pow(4, PackedBasePeakInfo & maskBPIntensityScale));
                    break;
                default:
                    unpackedIntensity = 0;
                    break;
            }
            return unpackedIntensity;

        }

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
                    var MassMantissa = (int)(NumberConversion.Int32ToUnsigned(PackedBasePeakInfo) / 512f);
                    // 512 = 2^9

                    // Compute the MassExponent value by multiplying the Packed value by the appropriate BitMask, then right shifting 4 bits by dividing by 2^4
                    var MassExponent = (short)(PackedBasePeakInfo & maskBPMassExponent) / 16f;
                    // 16 = 2^4

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
                    unpackedMass = MassMantissa / 8388608f * Math.Pow(2, MassExponent);
                    // 8388608 = 2^23
                    break;
                case 0:
                    // Compressed data
                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 11 bits by dividing by 2^11
                    // It would be more straightforward to use PackedBasePeakInfo And CreateMask(11, 31) but VB won't let us
                    //  And a Currency value with a Long; this gives an OverFlow error
                    // We must divide the MassMantissa by 128 to get the mass
                    unpackedMass = (int)(NumberConversion.Int32ToUnsigned(PackedBasePeakInfo) / 2048f) / 128f;      // 2048 = 2^11
                    break;
                // Debug.Assert(unpackedMass == ExtractFromBitsInt32(PackedBasePeakInfo, 11, 31) / 128f);
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
                    break;
                //Debug.Assert(unpackedMass == ExtractFromBitsInt32(PackedBasePeakInfo, 4, 31) / 128f);
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
