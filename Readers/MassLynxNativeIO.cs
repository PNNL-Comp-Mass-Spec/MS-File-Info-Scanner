using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MSFileInfoScanner.MassLynxData;

// ReSharper disable UnusedMember.Global
// ReSharper disable BuiltInTypeReferenceStyle
namespace MSFileInfoScanner.Readers
{
    /// <summary>
    /// <para>
    /// This class can read data from MassLynx data files using native disk access,
    ///  obviating the need to have MassLynx installed
    /// </para>
    /// <para>
    /// Note that native file IO is significantly slower than utilizing the
    ///  MassLynx API access functions (see clsMassLynxReader3 and clsMassLynxReader4)
    /// </para>
    /// <para>
    /// VB6 version Last modified January 22, 2004
    /// Updated to VB.NET September 17, 2005, though did not upgrade the extended function info functions or data point reading options
    /// Updated to C# in May 2016
    /// </para>
    /// </summary>
    /// <remarks>
    /// Written by Matthew Monroe for PNNL in Jan 2004
    /// Portions of this code were written at UNC in 2001
    /// </remarks>
    internal class MassLynxNativeIO
    {
        // Ignore Spelling: Acqu, CLng, func, Micromass

        //-------------------------------
        //-- Start Native IO Headers

        //Private Structure RAW_HEADER
        //	Dim nVersionMajor As Short 'Major version of file format.
        //	Dim nVersionMinor As Short 'Minor version of file format.
        //	Dim AcquName As String 'Acquired filename, no extension
        //	Dim AcquDate As String 'Acquired date: DD-MMM-YYYY.
        //	Dim AcquTime As String 'Acquired time: HH:MM:SS (24 hr).
        //	Dim JobCode As String
        //	Dim TaskCode As String
        //	Dim UserName As String
        //	Dim LabName As String
        //	Dim Instrument As String
        //	Dim Conditions As String
        //	Dim SampleDescription As String 'Sample description.
        //	Dim Submitter As String
        //	Dim SampleID As String
        //	Dim BottleNumber As String
        //	Dim SolventDelay As Double 'Solvent delay in decimal minutes.
        //	Dim bResolved As Integer 'TRUE if resolved data file.
        //	Dim PepFileName As String 'Assoc pep/EMBL filename-inc. directory+extension.
        //	Dim Process As String
        //	Dim bEncrypted As Integer
        //	' Fields added for MALDI-TOF support SCR
        //	Dim nAutoSamplerType As Integer
        //	Dim GasName As String
        //	Dim InstrumentType As String
        //	' Plate description string
        //	Dim PlateDescription As String
        //	'Analogue channel offset times
        //  Dim AnalogOffset() As Single              ' 1-based array, ranging from 1 to 4
        //	Dim MultiplexStream As Integer
        //   End Structure

        //   '***
        //   '*** DM_RAW_HEADER ***
        //   '*** Used at the DM level of MassLynx.
        //   '***
        //   Private Structure DM_RAW_HEADER
        //       Dim RawHeader As RAW_HEADER
        //       Dim FunctionsInFile As Short
        //       Dim wAnalogsInFile As Short
        //'   End Structure

        //-- End Native IO Headers
        //-------------------------------

        private enum ErrorCodeConstants
        {
            NoError = 0,
            InvalidDataFolderPath = 1,
            DataFolderHeaderReadError = 2,
            DataFolderReadError = 3
        }

        private readonly MSData mMSData;

        private readonly RawDataUtils mRawDataUtils;

        /// <summary>
        /// Constructor
        /// </summary>
        public MassLynxNativeIO()
        {
            mMSData = new MSData();
            mRawDataUtils = new RawDataUtils();
        }

        private ErrorCodeConstants mErrorCode;

        public string GetErrorMessage()
        {
            return mErrorCode switch
            {
                ErrorCodeConstants.NoError => string.Empty,
                ErrorCodeConstants.InvalidDataFolderPath => "Invalid data directory path",
                ErrorCodeConstants.DataFolderHeaderReadError => "The data directory header read error",
                ErrorCodeConstants.DataFolderReadError => "Data directory read error",
                _ => "Unknown error"
            };
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
        public bool GetFileInfo(string massLynxDataDirectoryPath, out MSHeaderInfo headerInfo)
        {
            bool success;

            headerInfo = new MSHeaderInfo();

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
            functionTypeText = string.Empty;

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
        public bool GetFunctionInfo(string massLynxDataDirectoryPath, int functionNumber, out MSFunctionInfo functionInfo)
        {
            bool success;

            functionInfo = new MSFunctionInfo(functionNumber);

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
            // Returns scan information in the out variables
            // Function returns True if no error, False if an error
            // Note that if LoadMSScanHeader returns 0, indicating no data points, this function will still return True
            //
            // Note that ScanType = 0 means MS-only scan (survey scan)
            // ScanType > 0 means ms/ms scan

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

            LoadMSScanHeader(out var scanStatsSingleScan, mMSData, functionNumber, scanNumber);

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

        /// <summary>
        /// Return true if the function has MS/MS data
        /// </summary>
        /// <param name="massLynxDataDirectoryPath">Instrument data directory path</param>
        /// <param name="functionNumber"></param>
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
        private bool LoadMSFileHeader(MSData thisMSData, string massLynxDataDirectoryPath)
        {
            var success = false;

            try
            {
                if (Directory.Exists(massLynxDataDirectoryPath))
                {
                    // Read the header information from the current file
                    success = NativeIOReadHeader(massLynxDataDirectoryPath, out var headerInfo);
                    thisMSData.HeaderInfo = headerInfo;
                    thisMSData.InitializeFunctionInfo(0);

                    return true;
                }

                SetErrorCode(ErrorCodeConstants.InvalidDataFolderPath);
                thisMSData.InitializeFunctionInfo(0);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFileHeader:" + ex.Message);

                if (!success)
                {
                    // Assume invalid data file
                    SetErrorCode(ErrorCodeConstants.DataFolderReadError);
                    thisMSData.InitializeFunctionInfo(0);
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
        private int LoadMSFunctionInfo(MSData thisMSData, string massLynxDataDirectoryPath)
        {
            var fileValidated = false;

            try
            {
                var massLynxFile = MSFileInfoScanner.GetFileInfo(massLynxDataDirectoryPath);
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

                if (!LoadMSFileHeader(thisMSData, cleanMassLynxDataFolderPath))
                {
                    thisMSData.InitializeFunctionInfo(0);
                    return thisMSData.FunctionCount;
                }

                thisMSData.UserSuppliedDataDirPath = massLynxDataDirectoryPath;
                thisMSData.CurrentDataDirPath = cleanMassLynxDataFolderPath;

                // Use FunctionInfo to read the header information from the current file
                var functionCount = NativeIOGetFunctionCount(cleanMassLynxDataFolderPath);
                thisMSData.InitializeFunctionInfo(functionCount);

                if (thisMSData.FunctionCount <= 0)
                {
                    return thisMSData.FunctionCount;
                }

                fileValidated = true;

                // Note that the function array is 1-based
                for (var functionNumber = 1; functionNumber <= thisMSData.FunctionCount; functionNumber++)
                {
                    if (NativeIOGetFunctionInfo(cleanMassLynxDataFolderPath, thisMSData.FunctionInfo[functionNumber]))
                    {
                        float startMass;
                        float endMass;
                        if (thisMSData.FunctionInfo[functionNumber].ScanCount > 0)
                        {
                            NativeIOGetScanInfo(cleanMassLynxDataFolderPath, thisMSData.FunctionInfo[functionNumber], 1, out _);

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

                if (thisMSData.FunctionCount > 0)
                {
                    NativeIOReadCalInfoFromHeader(thisMSData);
                }

                return thisMSData.FunctionCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.LoadMSFunctionInfo:" + ex.Message);

                if (!fileValidated)
                {
                    // Assume invalid data file
                    SetErrorCode(ErrorCodeConstants.DataFolderReadError);
                    thisMSData.InitializeFunctionInfo(0);
                }

                return thisMSData.FunctionCount;
            }
        }

        /// <summary>
        /// Loads information on the given scan for the given function
        /// </summary>
        /// <param name="scanStatsSingleScan"></param>
        /// <param name="thisMSData"></param>
        /// <param name="functionNumber"></param>
        /// <param name="scanNumber"></param>
        /// <remarks>
        /// The calling function must validate that functionNumber is valid
        /// Since this function uses thisMSData.FunctionInfo, one must call NativeIOGetFunctionInfo
        /// to populate .FunctionInfo before calling this function
        /// </remarks>
        private void LoadMSScanHeader(out MassLynxScanStats scanStatsSingleScan, MSData thisMSData, int functionNumber, int scanNumber)
        {
            scanStatsSingleScan = new MassLynxScanStats();

            try
            {
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

                if (NativeIOGetScanInfo(thisMSData.CurrentDataDirPath, thisMSData.FunctionInfo[functionNumber], scanNumber, out var scanIndexRecord))
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

        private void SetErrorCode(ErrorCodeConstants newErrorCode)
        {
            mErrorCode = newErrorCode;
        }

        private bool ValidateDataFolder(string massLynxDataDirectoryPath)
        {
            // Returns True if valid, False if not valid

            mErrorCode = ErrorCodeConstants.NoError;
            var validDataFolder = false;

            if (string.IsNullOrEmpty(massLynxDataDirectoryPath))
            {
                return false;
            }

            massLynxDataDirectoryPath = massLynxDataDirectoryPath.Trim();

            mMSData.UserSuppliedDataDirPath ??= string.Empty;

            if (mMSData.FunctionCount == 0 || !string.Equals(mMSData.UserSuppliedDataDirPath, massLynxDataDirectoryPath, StringComparison.OrdinalIgnoreCase))
            {
                var numFunctions = LoadMSFunctionInfo(mMSData, massLynxDataDirectoryPath);
                if (numFunctions > 0)
                {
                    validDataFolder = true;
                }
                else
                {
                    if (mErrorCode == ErrorCodeConstants.NoError)
                        SetErrorCode(ErrorCodeConstants.DataFolderReadError);
                }
            }
            else
            {
                validDataFolder = true;
                mErrorCode = ErrorCodeConstants.NoError;
            }

            return validDataFolder;
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
        private int NativeIOGetFunctionCount(string dataDirPath)
        {
            var functionCount = 0;

            try
            {
                var functionsFilePath = Path.Combine(dataDirPath, "_functns.inf");
                var functionsFile = MSFileInfoScanner.GetFileInfo(functionsFilePath);

                functionCount = 0;

                if (functionsFile.Exists)
                {
                    functionCount = (int)(functionsFile.Length / RawFunctionDescriptorRecord.NATIVE_FUNCTION_INFO_SIZE_BYTES);
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
        private bool NativeIOGetFunctionInfo(string dataDirPath, MSFunctionInfo msFunctionInfo)
        {
            try
            {
                var functionsFilePath = Path.Combine(dataDirPath, "_functns.inf");
                var functionsFile = MSFileInfoScanner.GetFileInfo(functionsFilePath);

                if (!functionsFile.Exists)
                {
                    return false;
                }

                using var reader = new BinaryReader(new FileStream(functionsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                var functionCount = (int)(functionsFile.Length / RawFunctionDescriptorRecord.NATIVE_FUNCTION_INFO_SIZE_BYTES);

                if (msFunctionInfo.FunctionNumber < 1 || msFunctionInfo.FunctionNumber > functionCount)
                {
                    return false;
                }

                // Since we're using Binary Access, we need to specify the Byte Offset to start reading at
                // The first byte is 1, and that is where Function 1 can be found
                // Function 2 can be found NATIVE_FUNCTION_INFO_SIZE_BYTES+1 bytes into the file

                reader.BaseStream.Seek((msFunctionInfo.FunctionNumber - 1) * RawFunctionDescriptorRecord.NATIVE_FUNCTION_INFO_SIZE_BYTES, SeekOrigin.Begin);

                var nativeFunctionInfo = new RawFunctionDescriptorRecord
                {
                    SegmentScanTimes = new int[32],
                    SegmentStartMasses = new int[32],
                    SegmentEndMasses = new int[32],
                    PackedFunctionInfo = reader.ReadInt16(),
                    CycleTime = reader.ReadSingle(),
                    InterScanDelay = reader.ReadSingle(),
                    StartRT = reader.ReadSingle(),
                    EndRT = reader.ReadSingle(),
                    ScanCount = reader.ReadInt32(),

                    // Packed MS/MS Info:
                    //   bits 0-7: collision energy
                    //   bits 8-15: segment/channel count
                    PackedMSMSInfo = reader.ReadInt16(),

                    // Additional MS/MS parameters
                    FunctionSetMass = reader.ReadSingle(),
                    InterSegmentChannelTime = reader.ReadSingle()
                };

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

                if (nativeFunctionInfo.PackedFunctionInfo == 0 &&
                    Math.Abs(nativeFunctionInfo.CycleTime) < float.Epsilon &&
                    Math.Abs(nativeFunctionInfo.InterScanDelay) < float.Epsilon)
                {
                    // Empty function record; see if file even exists
                    if (File.Exists(Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(functionCount + 1) + ".dat")))
                    {
                        // Nope, file does not exist, function is invalid
                        return false;
                    }
                }

                StoreFunctionInfo(dataDirPath, nativeFunctionInfo, msFunctionInfo);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetFunctionInfo:" + ex.Message);
                return false;
            }
        }

        private void StoreFunctionInfo(string dataDirPath, RawFunctionDescriptorRecord nativeFunctionInfo, MSFunctionInfo msFunctionInfo)
        {
            // Copy data from nativeFunctionInfo to msFunctionInfo
            msFunctionInfo.FunctionTypeID = mRawDataUtils.GetFunctionType(nativeFunctionInfo.PackedFunctionInfo);

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

            msFunctionInfo.IonMode = mRawDataUtils.GetIonMode(nativeFunctionInfo.PackedFunctionInfo);
            msFunctionInfo.AcquisitionDataType = mRawDataUtils.GetAcquisitionDataType(nativeFunctionInfo.PackedFunctionInfo);

            msFunctionInfo.CycleTime = nativeFunctionInfo.CycleTime;
            msFunctionInfo.InterScanDelay = nativeFunctionInfo.InterScanDelay;
            msFunctionInfo.StartRT = nativeFunctionInfo.StartRT;
            msFunctionInfo.EndRT = nativeFunctionInfo.EndRT;

            msFunctionInfo.MsMsCollisionEnergy = mRawDataUtils.GetMsMsCollisionEnergy(nativeFunctionInfo.PackedMSMSInfo);
            msFunctionInfo.MSMSSegmentOrChannelCount = mRawDataUtils.GetMSMSSegmentOrChannelCount(nativeFunctionInfo.PackedMSMSInfo);

            msFunctionInfo.FunctionSetMass = nativeFunctionInfo.FunctionSetMass;
            msFunctionInfo.InterSegmentChannelTime = nativeFunctionInfo.InterSegmentChannelTime;

            // Since nativeFunctionInfo.ScanCount is always 0, we need to use NativeIOGetScanCount instead
            var scanCount = NativeIOGetScanCount(dataDirPath, msFunctionInfo);
            if (msFunctionInfo.ScanCount != scanCount)
            {
                // This is unexpected
                Debug.WriteLine("Scan count values do not agree in NativeIOGetFunctionInfo");
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
        private int NativeIOGetScanCount(string dataDirPath, MSFunctionInfo msFunctionInfo)
        {
            try
            {
                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(msFunctionInfo.FunctionNumber) + ".idx");
                var indexFile = MSFileInfoScanner.GetFileInfo(indexFilePath);

                var numberOfScansInFunction = 0;
                if (indexFile.Exists)
                {
                    // The ScanCount stored in the function index file is always 0 rather than the correct number of scans
                    // Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
                    //  by the size of each RawScanIndexRecord

                    numberOfScansInFunction = (int)(indexFile.Length / RawScanIndexRecord.RAW_SCAN_INDEX_RECORD_SIZE);
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
            MSFunctionInfo msFunctionInfo,
            int scanNumber,
            out ScanIndexRecord scanIndexRecord,
            bool scanOffsetAndPeakCountOnly = false)
        {
            // This is used for most files
            var nativeScanIndexRecord = new RawScanIndexRecord();

            // This is used for files with msFunctionInfo.AcquisitionDataType = 0
            // The difference is that structure RawScanIndexRecordType ends in an Int16 then a Int32
            //  while this structure ends in a Int32, then an Int16
            // When this structure is used, its values are copied to nativeScanIndexRecord directly after reading
            var nativeScanIndexRecordCompressedScan = new RawScanIndexRecordCompressedScan();

            // Initialize the output variable
            scanIndexRecord = new ScanIndexRecord();

            var success = false;

            try
            {
                var indexFilePath = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(msFunctionInfo.FunctionNumber) + ".idx");
                // indexFilePath  = Path.Combine(dataDirPath, "_func" + GetFunctionNumberZeroPadded(msFunctionInfo.FunctionNumber) + ".sts");

                var indexFile = MSFileInfoScanner.GetFileInfo(indexFilePath);

                if (!indexFile.Exists)
                {
                    return false;
                }

                var numberOfScansInFunction = (int)(indexFile.Length / RawScanIndexRecord.RAW_SCAN_INDEX_RECORD_SIZE);

                using var reader = new BinaryReader(new FileStream(indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                // The ScanCount stored in the function index file is always 0 rather than the correct number of scans
                // Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
                //  by the size of each RawScanIndexRecord

                if (scanNumber < 1)
                    scanNumber = 1;

                if (numberOfScansInFunction > 0 && scanNumber <= numberOfScansInFunction)
                {
                    // Just read the record for this scan

                    // Jump to the appropriate file offset based on scanNumber
                    reader.BaseStream.Seek((scanNumber - 1) * RawScanIndexRecord.RAW_SCAN_INDEX_RECORD_SIZE, SeekOrigin.Begin);

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

                if (!success)
                {
                    return false;
                }

                StoreScanInfo(nativeScanIndexRecord, scanIndexRecord, msFunctionInfo, scanOffsetAndPeakCountOnly);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in clsMassLynxNativeIO.NativeIOGetScanInfo:" + ex.Message);
                return false;
            }
        }

        private void StoreScanInfo(RawScanIndexRecord nativeScanIndexRecord, ScanIndexRecord scanIndexRecord, MSFunctionInfo msFunctionInfo,
                                   bool scanOffsetAndPeakCountOnly)
        {
            scanIndexRecord.StartScanOffset = nativeScanIndexRecord.StartScanOffset;

            scanIndexRecord.NumSpectralPeaks = mRawDataUtils.GetNumSpectraPeaks(nativeScanIndexRecord.PackedScanInfo);

            if (!scanOffsetAndPeakCountOnly)
            {
                // 4194304 = 2^22
                scanIndexRecord.SegmentNumber = mRawDataUtils.GetSegmentNumber(nativeScanIndexRecord.PackedScanInfo);

                scanIndexRecord.UseFollowingContinuum = mRawDataUtils.GetUseFollowingContinuum(nativeScanIndexRecord.PackedScanInfo);
                scanIndexRecord.ContinuumDataOverride = mRawDataUtils.GetContinuumDataOverride(nativeScanIndexRecord.PackedScanInfo);
                scanIndexRecord.ScanContainsMolecularMasses = mRawDataUtils.GetContainsMolecularMasses(nativeScanIndexRecord.PackedScanInfo);
                scanIndexRecord.ScanContainsCalibratedMasses = mRawDataUtils.GetContainsCalibratedMasses(nativeScanIndexRecord.PackedScanInfo);
                if (nativeScanIndexRecord.PackedScanInfo != Math.Abs(nativeScanIndexRecord.PackedScanInfo))
                {
                    scanIndexRecord.ScanOverload = true;
                }

                scanIndexRecord.TicValue = nativeScanIndexRecord.TicValue;
                scanIndexRecord.ScanTime = nativeScanIndexRecord.ScanTime;

                scanIndexRecord.BasePeakIntensity = (int)mRawDataUtils.UnpackIntensity(nativeScanIndexRecord.PackedBasePeakIntensity,
                    nativeScanIndexRecord.PackedBasePeakInfo, msFunctionInfo.AcquisitionDataType);

                scanIndexRecord.BasePeakMass =
                    (float)mRawDataUtils.UnpackMass(nativeScanIndexRecord.PackedBasePeakInfo, msFunctionInfo.AcquisitionDataType, true);

                // ToDo: May need to calibrate the base peak mass
                // scanIndexRecord.BasePeakMass = scanIndexRecord.BasePeakMass;

                // ToDo: Figure out if this can be read from the FunctionIndex file
                scanIndexRecord.LoMass = 0;
                scanIndexRecord.HiMass = 0;

                // This will get populated later
                scanIndexRecord.SetMass = 0;
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

            for (var calIndex = 0; calIndex < calibrationParameters.Length; calIndex++)
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
                if (calibrationParameters[calIndex].StartsWith("T", StringComparison.OrdinalIgnoreCase))
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
        private bool NativeIOReadCalInfoFromHeader(MSData thisMSData)
        {
            const string CAL_FUNCTION_NAME = "CAL FUNCTION";

            // ReSharper disable once StringLiteralTypo
            const string CAL_STD_DEV_FUNCTION_NAME = "CAL STDDEV FUNCTION";

            try
            {
                var headerFilePath = Path.Combine(thisMSData.CurrentDataDirPath, "_HEADER.TXT");
                var headerFile = MSFileInfoScanner.GetFileInfo(headerFilePath);

                if (!headerFile.Exists)
                {
                    return false;
                }

                using var reader = new StreamReader(headerFilePath);

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
                                out var calibrationCoefficientCount,
                                thisMSData.FunctionInfo[functionNumber].CalibrationCoefficients,
                                out var calibrationTypeID);

                            thisMSData.FunctionInfo[functionNumber].CalibrationCoefficientCount = calibrationCoefficientCount;
                            thisMSData.FunctionInfo[functionNumber].CalTypeID = calibrationTypeID;
                        }
                        else
                        {
                            // Calibration equation for non-existent function
                            // This shouldn't happen
                        }
                    }
                    else if (dataLine.ToUpper().StartsWith(CAL_STD_DEV_FUNCTION_NAME))
                    {
                        functionNumber = CIntSafe(dataLine.Substring(CAL_STD_DEV_FUNCTION_NAME.Length, colonIndex - CAL_STD_DEV_FUNCTION_NAME.Length));
                        if (functionNumber >= 1 && functionNumber <= thisMSData.FunctionCount)
                        {
                            if (double.TryParse(keyValue, out var calStdDev))
                            {
                                thisMSData.FunctionInfo[functionNumber].CalStDev = calStdDev;
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
        private bool NativeIOReadHeader(string dataDirPath, out MSHeaderInfo headerInfo)
        {
            headerInfo = new MSHeaderInfo();

            try
            {
                var headerFilePath = Path.Combine(dataDirPath, "_HEADER.TXT");
                var headerFile = MSFileInfoScanner.GetFileInfo(headerFilePath);

                if (!headerFile.Exists)
                {
                    return false;
                }

                using var reader = new StreamReader(headerFilePath);

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
                                out var ms1CalibrationCoefficientCount,
                                headerInfo.CalMS1StaticCoefficients,
                                out var ms1CalibrationType);

                            headerInfo.CalMS1StaticCoefficientCount = ms1CalibrationCoefficientCount;
                            headerInfo.CalMS1StaticTypeID = ms1CalibrationType;
                            break;

                        case "CAL MS2 STATIC":
                            NativeIOParseCalibrationCoefficients(
                                keyValue,
                                out var ms2CalibrationCoefficientCount,
                                headerInfo.CalMS2StaticCoefficients,
                                out var ms2CalibrationType);
                            headerInfo.CalMS2StaticCoefficientCount = ms2CalibrationCoefficientCount;
                            headerInfo.CalMS2StaticTypeID = ms2CalibrationType;
                            break;

                        default:
                            // Ignore it
                            break;
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
    }
}
