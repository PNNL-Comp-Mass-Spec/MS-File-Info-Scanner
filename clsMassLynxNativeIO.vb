Option Strict Off
Option Explicit On

Friend Class clsMassLynxNativeIO

    Public Sub New()
        MyBase.New()
        CreateNativeDataMasks()
    End Sub

    ' This class can read data from MassLynx data files using native disk access,
    '  obviating the need to have MassLynx installed
    ' Note that native file IO is signficantly slower than utilizing the
    '  MassLynx API access functions (see clsMassLynxReader3 and clsMassLynxReader4)
    '
    ' Written by Matthew Monroe for PNNL in Jan 2004
    ' Portions of this code were written at UNC in 2001
    '
    ' VB6 version Last modified January 22, 2004
    ' Updated to VB.NET September 17, 2005, though did not upgrade the extended function info functions or data point reading options

    '-------------------------------
    '-- Start Native IO Headers

    'Private Structure RAWHEADER
    '	Dim nVersionMajor As Short 'Major version of file format.
    '	Dim nVersionMinor As Short 'Minor version of file format.
    '	Dim szAcquName As String 'Acquired filename, no extn.
    '	Dim szAcquDate As String 'Acquired date: DD-MMM-YYYY.
    '	Dim szAcquTime As String 'Acquired time: HH:MM:SS (24 hr).
    '	Dim szJobCode As String
    '	Dim szTaskCode As String
    '	Dim szUserName As String
    '	Dim szLabName As String
    '	Dim szInstrument As String
    '	Dim szConditions As String
    '	Dim szSampleDesc As String 'Sample description.
    '	'szSampleDesc As String 'Sample description.
    '	Dim szSubmitter As String
    '	Dim szSampleID As String
    '	Dim szBottleNumber As String
    '	Dim lfSolventDelay As Double 'Solvent delay in decimal minutes.
    '	Dim bResolved As Integer 'TRUE if resolved data file.
    '	Dim szPepFileName As String 'Assoc pep/embl filename-inc. dir+ext.
    '	Dim szProcess As String
    '	Dim bEncypted As Integer
    '	' Fields added for Maldi-Tof support SCR
    '	Dim nAutosamplerType As Integer
    '	Dim szGasName As String
    '	Dim szInstrumentType As String
    '	' Plate description string
    '	Dim szPlateDesc As String
    '	'Analogue chanel offset times
    '       Dim afAnalogOffset() As Single              ' 1-based array, ranging from 1 to 4
    '	Dim nMuxStream As Integer
    '   End Structure

    '   '***
    '   '*** DMRAWHEADER ***
    '   '*** Used at the DM level of MassLynx.
    '   '***
    '   Private Structure DMRAWHEADER
    '       Dim sRawHeader As RAWHEADER
    '       Dim wFuncsInFile As Short
    '       Dim wAnalogsInFile As Short
    ''   End Structure

    ' Used when reading the _functns.inf file
    Private Const NATIVE_FUNCTION_INFO_SIZE_BYTES As Integer = 416
    Private Structure udtRawFunctionDescriptorRecordType
        ' The first 2 bytes of the record contain info on the function,
        '   with the information stored in packed form:
        '   bits 0-4: Function type (typically 2=Dly)
        '   bits 5-9: Ion mode (typically 8=ES+)
        '   bits 10-13: Acquisition data type (typically 9=high accuracy calibrated data)
        '   bits 14-15: spare
        Dim PackedFunctionInfo As Short ' 2 bytes
        Dim CycleTime As Single ' 4 bytes (in seconds)
        Dim InterScanDelay As Single ' 4 bytes (in seconds)
        Dim StartRT As Single ' 4 bytes (in minutes)
        Dim EndRT As Single ' 4 bytes (in minutes)
        Dim ScanCount As Integer ' 4 bytes; unfortunately, this is always 0 and thus we cannot trust it
        ' Packed MS/MS Info:
        '   bits 0-7: collision energy
        '   bits 8-15: segment/channel count
        Dim PackedMSMSInfo As Short ' 2 bytes
        ' The following are more MS/MS parameters
        Dim FunctionSetMass As Single ' 4 bytes
        Dim InterSegmentChannelTime As Single ' 4 bytes (in seconds)
        ' Up to 32 segment scans can be conducted for a MS/MS run
        ' The following three arrays store the segment times, start, and end masses
        Dim SegmentScanTimes() As Integer ' Ranges from 0 to 31 giving a 128 byte array
        Dim SegmentStartMasses() As Integer ' Ranges from 0 to 31 giving a 128 byte array
        Dim SegmentEndMasses() As Integer ' Ranges from 0 to 31 giving a 128 byte array
    End Structure

    ' Used when reading the _func001.idx file
    Const RAW_SCAN_INDEX_RECORD_SIZE As Short = 22 ' Total size in bytes
    Private Structure udtRawScanIndexRecordType
        Dim StartScanOffset As Integer ' 4 bytes
        ' The next 4 bytes are stored as a Long Integer, but are in fact
        '   seven different numbers packed into one Long Integer:
        '   bits 0-21: number of spectral peaks in scan
        '   bits 22-26: segment number (MTOF function)
        '   bit 27: use following continuum data flag
        '   bit 28: continuum data override flag
        '   bit 29: scan contains molecular masses
        '   bit 30: scan contains calibrated masses
        '   bit 31: scan overload flag
        '
        Dim PackedScanInfo As Integer ' 4 bytes
        Dim TicValue As Single ' 4 bytes
        Dim ScanTime As Single ' 4 bytes, time in minutes
        ' The remaining 6 bytes of the record contain a duplicate of the scan's base peak,
        '   with the information stored in packed form:
        ' The method used to pack the data depends on the Acquisition data type
        ' Note that the data type ID is stored in packed form in udtRawFunctionDescriptorRecord.PackedFunctionInfo
        ' After unpacking, it is stored in .FunctionInfo().AcquisitionDataType
        ' The UnpackIntensity and UnpackMass functions
        '  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo
        ' For Acquisition Data Type ID 0 (Compressed scan)
        ' Use udtRawScanIndexRecordCompressedScanType instead of udtRawScanIndexRecordType
        ' For Acquisition Data Type ID 1 (Standard scan)
        '   bits 0-2: intensity scale
        '   bits 3-15: intensity
        '   bits 16-39: mass * 1024
        '   bits 40-47: spare
        ' For Acquisition Data Type ID 2 through 7 (Uncalibrated data)
        '   bits 0-2: intensity scale
        '   bits 3-15: intensity
        '   bits 16-43: channel number
        '   bits 44-47: spare
        ' For Acquisition Data Type ID 8 (High intensity calibrated data)
        '   bits 0-15: intensity
        '   bits 16-19: intensity scale
        '   bits 20-47: mass * 128
        ' For Acquisition Data Type ID 9, 11, and 12 (High accuracy calibrated, enhanced uncalibrated, and enhanced calibrated)
        ' Note that this is the form for the LCT and various Q-Tof's
        '   bits 0-15: intensity
        '   bits 16-19: intensity scale
        '   bits 20-24: mass exponent
        '   bits 25-47: mass mantissa
        Dim PackedBasePeakIntensity As Short ' 2 bytes
        Dim PackedBasePeakInfo As Integer ' 4 bytes
    End Structure

    Private Structure udtRawScanIndexRecordCompressedScanType
        Dim StartScanOffset As Integer ' 4 bytes
        Dim PackedScanInfo As Integer ' 4 bytes
        Dim TicValue As Single ' 4 bytes
        Dim ScanTime As Single ' 4 bytes, time in minutes
        ' The remaining 6 bytes of the record contain a duplicate of the scan's base peak,
        '   with the information stored in packed form:
        ' The method used to pack the data depends on the Acquisition data type
        ' Note that the data type ID is stored in packed form in udtRawFunctionDescriptorRecord.PackedFunctionInfo
        ' After unpacking, it is stored in .FunctionInfo().AcquisitionDataType
        ' The UnpackIntensity and UnpackMass functions
        '  are used to unpack the values from PackedBasePeakIntensity and PackedBasePeakInfo
        ' For Acquisition Data Type ID 0 (Compressed scan)
        '   bits 0-2: intensity scale
        '   bits 3-10: intensity
        '   bits 11-31: mass * 128
        '   bits 32-47: spare
        Dim PackedBasePeakInfo As Integer ' 4 bytes
        Dim Spare As Short ' 2 bytes, unused
    End Structure

    ' The udtRawScanIndexRecordType data read from the file is stored in this structure
    Private Structure udtScanIndexRecordType
        Dim StartScanOffset As Integer ' offset (in bytes) from start of file where scan begins
        Dim NumSpectralPeaks As Integer
        Dim SegmentNumber As Short
        Dim UseFollowingContinuum As Boolean
        Dim ContiuumDataOverride As Boolean
        Dim ScanContainsMolecularMasses As Boolean
        Dim ScanContainsCalibratedMasses As Boolean
        Dim ScanOverload As Boolean
        Dim BasePeakIntensity As Integer
        Dim BasePeakMass As Single
        Dim TicValue As Single ' counts
        Dim ScanTime As Single ' minutes
        Dim LoMass As Single
        Dim HiMass As Single
        Dim SetMass As Single
    End Structure

    ' Function Info Masks
    Private maskFunctionType As Short
    Private maskIonMode As Short
    Private maskAcquisitionDataType As Short
    Private maskCollisionEnergy As Short
    Private maskSegmentChannelCount As Integer

    ' Scan Info Masks
    Private maskSpectralPeak As Integer
    Private maskSegment As Integer
    Private maskUseFollowingContinuum As Integer
    Private maskContiuumDataOverride As Integer
    Private maskScanContainsMolecularMasses As Integer
    Private maskScanContainsCalibratedMasses As Integer

    ' Packed mass and packed intensity masks
    Private maskBPIntensityScale As Integer
    Private maskBPMassExponent As Integer

    Private maskBPCompressedDataIntensityScale As Integer
    Private maskBPCompressedDataIntensity As Integer

    Private maskBPStandardDataIntensityScale As Integer
    Private maskBPStandardDataIntensity As Integer
    Private maskBPStandardDataMass As Integer

    Private maskBPUncalibratedDataChannelNumber As Integer


    '-- End Native IO Headers
    '-------------------------------

    Private Enum eErrorCodeConstants
        NoError = 0
        InvalidDataFolderPath = 1
        DataFolderHeaderReadError = 2
        DataFolderReadError = 3
    End Enum

    Public Structure udtMSHeaderInfoType
        Dim AcquDate As String
        Dim AcquName As String
        Dim AcquTime As String
        Dim JobCode As String
        Dim TaskCode As String
        Dim UserName As String
        Dim Instrument As String
        Dim InstrumentType As String
        Dim Conditions As String
        Dim LabName As String
        Dim SampleDesc As String
        Dim SolventDelay As Single
        Dim Submitter As String
        Dim SampleID As String
        Dim BottleNumber As String
        Dim PlateDesc As String
        Dim MuxStream As Integer
        Dim VersionMajor As Integer
        Dim VersionMinor As Integer
        Dim CalMS1StaticCoeffCount As Short
        Dim CalMS1StaticCoeffs() As Double
        Dim CalMS1StaticTypeID As Short ' 0 = normal, 1 = Root mass
        Dim CalMS2StaticCoeffCount As Short
        Dim CalMS2StaticCoeffs() As Double
        Dim CalMS2StaticTypeID As Short ' 0 = normal, 1 = Root mass
    End Structure

    Private Structure udtScanStatsType
        Dim PeakCount As Integer
        Dim Calibrated As Boolean
        Dim Continuum As Boolean
        Dim Overload As Boolean
        Dim MassStart As Single
        Dim MassEnd As Single
        Dim SetMass As Single ' MS/MS Parent Ion Mass
        Dim BPI As Single ' Base peak intensity
        Dim BPIMass As Single ' Base peak mass
        Dim TIC As Single
        Dim RetnTime As Single
    End Structure

    Public Structure udtMSFunctionInfoType
        Dim FunctionNumber As Integer ' The function number that this data corresponds to
        Dim ProcessNumber As Short
        Dim StartRT As Single
        Dim EndRT As Single
        Dim FunctionTypeID As Short ' 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
        ' 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
        ' 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
        Dim FunctionType As Short ' 0 for MS-only; 1 for MS/MS
        Dim FunctionTypeText As String
        Dim StartMass As Single
        Dim EndMass As Single
        Dim ScanCount As Integer
        Dim IonMode As Short
        Dim AcquisitionDataType As Short ' 0=Compressed scan, 1=Standard Scan, 2=SIR or MRM Data, 3=Scanning Continuum,
        ' 4=MCA Data, 5=MCA data with SD, 6=MCB data, 7=MCB data with SD
        ' 8=Molecular weight data, 9=High accuracy calibrated data
        ' 10=Single float precision (not used), 11=Enhanced uncalibrated data
        ' 12=Enhanced calibrated data
        Dim CycleTime As Single ' in seconds
        Dim InterScanDelay As Single ' in seconds
        Dim MsMsCollisionEnergy As Short ' in eV
        Dim MSMSSegmentOrChannelCount As Short
        Dim FunctionSetMass As Single
        Dim InterSegmentChannelTime As Single ' in seconds
        Dim CalibrationCoefficientCount As Short ' Should be 0 or 6 or 7  (typically 6 coefficients)
        Dim CalibrationCoefficients() As Double ' Calibration coefficients
        Dim CalTypeID As Short ' 0 = normal, 1 = Root mass
        Dim CalStDev As Double ' Calibration standard deviation
    End Structure

    Public Structure udtMSDataType
        Dim UserSuppliedDataDirPath As String
        Dim CurrentDataDirPath As String ' The currently loaded data file path
        Dim HeaderInfo As udtMSHeaderInfoType
        Dim FunctionCount As Integer
        Dim FunctionInfo() As udtMSFunctionInfoType ' 1-based array (to stay consistent with Micromass VB example conventions)
    End Structure

    Private MSData As udtMSDataType
    Private mErrorCode As eErrorCodeConstants

    Public Function GetErrorMessage() As String
        Dim strError As String

        Select Case mErrorCode
            Case eErrorCodeConstants.NoError
                strError = ""
            Case eErrorCodeConstants.InvalidDataFolderPath
                strError = "Invalid data folder path"
            Case eErrorCodeConstants.DataFolderHeaderReadError
                strError = "The data folder header read error"
            Case eErrorCodeConstants.DataFolderReadError
                strError = "Data folder read error"
            Case Else
                strError = "Unknown error"
        End Select

        GetErrorMessage = strError
    End Function

    Public Function GetFileInfo(ByVal strMLynxDataFolderPath As String, ByRef strAcquDate As String, ByVal strAcquTime As String, ByRef strAcquName As String, ByRef strInstrument As String, ByRef strInstrumentType As String, ByRef strSampleDesc As String, ByRef lngVersionMajor As Integer, ByRef lngVersionMinor As Integer) As Boolean
        ' Returns information on the given MassLynx data file (actually a folder)
        ' Returns True if success, false if failure

        Dim blnSuccess As Boolean

        Try
            blnSuccess = ValidateDataFolder(strMLynxDataFolderPath)
            If blnSuccess Then
                With MSData.HeaderInfo
                    strAcquDate = .AcquDate & " " & .AcquTime
                    strAcquName = .AcquName
                    strInstrument = .Instrument
                    strInstrumentType = .InstrumentType
                    strSampleDesc = .SampleDesc
                    lngVersionMajor = .VersionMajor
                    lngVersionMinor = .VersionMinor
                End With
            End If

        Catch ex As System.Exception
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function GetFileInfo(ByVal strMLynxDataFolderPath As String, ByRef udtHeaderInfo As udtMSHeaderInfoType) As Boolean
        ' Returns information on the given MassLynx data file (actually a folder)
        ' Returns True if success, false if failure

        Dim blnSuccess As Boolean

        Try
            blnSuccess = ValidateDataFolder(strMLynxDataFolderPath)
            If blnSuccess Then
                udtHeaderInfo = MSData.HeaderInfo
            End If
        Catch ex As System.Exception
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function GetFunctionAcquisitionDataType(ByVal strMLynxDataFolderPath As String, ByVal lngFunctionNumber As Integer) As Short
        Dim intAcquisitionDataTypeID As Short

        On Error GoTo GetFunctionAcquisitionDataTypeExit

        intAcquisitionDataTypeID = -1
        If ValidateDataFolder(strMLynxDataFolderPath) Then
            If lngFunctionNumber >= 1 And lngFunctionNumber <= MSData.FunctionCount Then
                With MSData.FunctionInfo(lngFunctionNumber)
                    intAcquisitionDataTypeID = .AcquisitionDataType
                End With
            End If
        End If

GetFunctionAcquisitionDataTypeExit:
        GetFunctionAcquisitionDataType = intAcquisitionDataTypeID

    End Function

    Public Function GetFunctionInfo(ByVal strMLynxDataFolderPath As String, ByVal lngFunctionNumber As Integer, ByRef lngScanCount As Integer, ByRef sngStartRT As Single, ByRef sngEndRT As Single, ByRef sngStartMass As Single, ByRef sngEndMass As Single, ByRef intFunctionType As Short, Optional ByRef strFunctionTypeText As String = "", Optional ByRef dblFunctionSetMass As Double = 0) As Boolean
        ' Returns information on the given function
        ' Returns True if success, false if failure

        Dim blnSuccess As Boolean

        Try
            blnSuccess = ValidateDataFolder(strMLynxDataFolderPath)
            If blnSuccess Then
                If lngFunctionNumber >= 1 And lngFunctionNumber <= MSData.FunctionCount Then
                    With MSData.FunctionInfo(lngFunctionNumber)
                        lngScanCount = .ScanCount
                        sngStartRT = .StartRT
                        sngEndRT = .EndRT
                        sngStartMass = .StartMass
                        sngEndMass = .EndMass
                        intFunctionType = .FunctionType
                        dblFunctionSetMass = .FunctionSetMass
                        strFunctionTypeText = .FunctionTypeText
                    End With
                Else
                    blnSuccess = False
                End If
            End If
        Catch ex As System.Exception
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function GetFunctionInfo(ByVal strMLynxDataFolderPath As String, ByVal lngFunctionNumber As Integer, ByRef udtFunctionInfo As udtMSFunctionInfoType) As Boolean
        ' Returns information on the given function
        ' Returns True if success, false if failure

        Dim blnSuccess As Boolean

        Try
            blnSuccess = ValidateDataFolder(strMLynxDataFolderPath)
            If blnSuccess Then
                If lngFunctionNumber >= 1 And lngFunctionNumber <= MSData.FunctionCount Then
                    udtFunctionInfo = MSData.FunctionInfo(lngFunctionNumber)
                Else
                    blnSuccess = False
                End If
            End If
        Catch ex As System.Exception
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Function GetFunctionCount(ByVal strMLynxDataFolderPath As String) As Integer
        ' Function returns the number of functions in the datafile
        ' Returns 0 if an error

        Dim lngFunctionCount As Integer

        On Error GoTo GetNumScansExit

        lngFunctionCount = 0
        If ValidateDataFolder(strMLynxDataFolderPath) Then
            lngFunctionCount = MSData.FunctionCount
        End If

GetNumScansExit:
        GetFunctionCount = lngFunctionCount

    End Function

    Public Function GetNumScans(ByVal strMLynxDataFolderPath As String, Optional ByVal lngFunctionNumber As Integer = 1) As Integer
        ' Function returns the number of scans for the given function
        ' Returns 0 if an error

        Dim lngScanCount As Integer

        On Error GoTo GetNumScansExit

        lngScanCount = 0
        If ValidateDataFolder(strMLynxDataFolderPath) Then
            If lngFunctionNumber >= 1 And lngFunctionNumber <= MSData.FunctionCount Then
                lngScanCount = MSData.FunctionInfo(lngFunctionNumber).ScanCount
            Else
                lngScanCount = 0
            End If
        End If

GetNumScansExit:
        GetNumScans = lngScanCount
    End Function


    Public Function GetScanInfo(ByVal strMLynxDataFolderPath As String, ByVal lngFunctionNumber As Integer, ByVal lngScanNumber As Integer, ByRef lngScanType As Integer, ByRef sngBasePeakMZ As Single, ByRef sngParentIonMZ As Single, ByRef sngRT As Single, ByRef sngBasePeakIntensity As Single, ByRef sngTotalIonCurrent As Single) As Boolean
        ' Returns scan information in the ByRef variables
        ' Function returns True if no error, False if an error
        '
        ' Note that ScanType = 0 means MS-only scan (survey scan)
        ' ScanType > 0 means ms/ms scan

        GetScanInfo = GetScanInfoEx(strMLynxDataFolderPath, lngFunctionNumber, lngScanNumber, lngScanType, sngBasePeakMZ, sngParentIonMZ, sngRT, sngBasePeakIntensity, sngTotalIonCurrent, False, False, False, 0, 0)
    End Function

    Public Function GetScanInfoEx(ByVal strMLynxDataFolderPath As String, ByVal lngFunctionNumber As Integer, ByVal lngScanNumber As Integer, ByRef lngScanType As Integer, ByRef sngBasePeakMZ As Single, ByRef sngParentIonMZ As Single, ByRef sngRT As Single, ByRef sngBasePeakIntensity As Single, ByRef sngTotalIonCurrent As Single, ByRef blnCalibrated As Boolean, ByRef blnContinuum As Boolean, ByRef blnOverload As Boolean, ByRef sngMassStart As Single, ByRef sngMassEnd As Single) As Boolean
        ' Returns scan information in the ByRef variables
        ' Function returns True if no error, False if an error
        ' Note that if LoadMSScanHeader returns 0, indicating no data points, this function will still return True
        '
        ' Note that ScanType = 0 means MS-only scan (survey scan)
        ' ScanType > 0 means ms/ms scan

        Dim udtScanStatsSingleScan As udtScanStatsType

        If ValidateDataFolder(strMLynxDataFolderPath) Then
            If lngFunctionNumber >= 1 And lngFunctionNumber <= MSData.FunctionCount Then
                LoadMSScanHeader(udtScanStatsSingleScan, MSData, lngFunctionNumber, lngScanNumber)

                lngScanType = MSData.FunctionInfo(lngFunctionNumber).FunctionType
                With udtScanStatsSingleScan
                    sngBasePeakMZ = .BPIMass
                    sngParentIonMZ = .SetMass
                    sngRT = .RetnTime
                    sngBasePeakIntensity = .BPI
                    sngTotalIonCurrent = .TIC
                    blnCalibrated = .Calibrated
                    blnContinuum = .Continuum
                    blnOverload = .Overload
                    sngMassStart = .MassStart
                    sngMassEnd = .MassEnd
                End With
                GetScanInfoEx = True
            Else
                GetScanInfoEx = False
            End If
        Else
            GetScanInfoEx = False
        End If
    End Function

    Private Sub InitializeFunctionInfo(ByRef udtMSFunctionInfo As udtMSFunctionInfoType, ByRef lngFunctionNumber As Integer)
        With udtMSFunctionInfo
            .FunctionNumber = lngFunctionNumber
            .ProcessNumber = 0

            .CalibrationCoefficientCount = 0

            ReDim .CalibrationCoefficients(6)
            .CalTypeID = 0
            .CalStDev = 0
        End With
    End Sub

    Private Sub InitializeNativeFunctionInfo(ByRef udtNativeFunctionInfo As udtRawFunctionDescriptorRecordType)
        With udtNativeFunctionInfo
            ReDim .SegmentScanTimes(31)
            ReDim .SegmentStartMasses(31)
            ReDim .SegmentEndMasses(31)
        End With
    End Sub

    Public Function IsFunctionMsMs(ByVal strMLynxDataFolderPath As String, ByRef lngFunctionNumber As Integer) As Boolean
        Dim intFunctionType As Short

        If GetFunctionInfo(strMLynxDataFolderPath, lngFunctionNumber, 0, 0, 0, 0, 0, intFunctionType) Then
            IsFunctionMsMs = (intFunctionType <> 0)
        Else
            IsFunctionMsMs = False
        End If

    End Function

    Public Function IsSpectrumContinuumData(ByVal strMLynxDataFolderPath As String, ByRef lngFunctionNumber As Integer, Optional ByRef lngScanNumber As Integer = 1) As Boolean
        Dim blnContinuum As Boolean

        If GetScanInfoEx(strMLynxDataFolderPath, lngFunctionNumber, lngScanNumber, 0, 0, 0, 0, 0, 0, 0, blnContinuum, 0, 0, 0) Then
            IsSpectrumContinuumData = blnContinuum
        Else
            IsSpectrumContinuumData = False
        End If

    End Function

    Public Function IsMassLynxData(ByVal strMLynxDataFolderPath As String) As Boolean
        ' strMLynxDataFolderPath should contain the path to a folder that ends in the text .RAW
        ' If strMLynxDataFolderPath contains the path to a file, then the ValidateDataFolder function
        '  will strip off the filename and only examine the folder

        IsMassLynxData = ValidateDataFolder(strMLynxDataFolderPath)

    End Function

    Public Function IsMassLynxInstalled() As Boolean
        ' This function is included for compatibility with MassLynxReader3 and MassLynxReader4
        ' It always returns True since this class doesn't require MassLynx
        IsMassLynxInstalled = True
    End Function

    Private Function LoadMSFileHeader(ByRef udtThisMSData As udtMSDataType, ByVal strMLynxDataFolderPath As String) As Boolean
        ' Verifies that strMLynxDataFolderPath exists
        ' Loads the header information for the given MassLynx folder path
        ' Returns True if success, false if failure

        ''Dim udtHeaderInfo As DMRAWHEADER
        Dim blnSuccess As Boolean

        On Error GoTo LoadMSHeaderErrorHandler

        If System.IO.Directory.Exists(strMLynxDataFolderPath) Then

            ' Read the header information from the current file
            blnSuccess = NativeIOReadHeader(strMLynxDataFolderPath, udtThisMSData.HeaderInfo)

            udtThisMSData.FunctionCount = 0
        Else
            SetErrorCode(eErrorCodeConstants.InvalidDataFolderPath)
            blnSuccess = False
            udtThisMSData.FunctionCount = 0
        End If

LoadMSHeaderCleanup:

        On Error Resume Next
        LoadMSFileHeader = blnSuccess
        Exit Function

LoadMSHeaderErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in LoadMSFileHeader:" & Err.Description)

        If Not blnSuccess Then
            ' Assume invalid data file
            SetErrorCode(eErrorCodeConstants.DataFolderReadError)
            udtThisMSData.FunctionCount = 0
        End If
        Resume LoadMSHeaderCleanup

    End Function

    Private Function LoadMSFunctionInfo(ByRef udtThisMSData As udtMSDataType, ByVal strMLynxDataFolderPath As String) As Integer
        ' Determines the number of functions in the given data file
        ' Returns the function count, or 0 on failure

        Dim udtScanIndexRecord As udtScanIndexRecordType
        Dim strCleanMLynxDataFolderPath As String

        Dim ioFileInfo As System.IO.FileInfo

        Dim intFunctionType As Short
        Dim lngFunctionNumber As Integer
        Dim lngScanCount As Integer
        Dim blnFileValidated As Boolean

        Dim sngStartMass, sngEndMass As Single
        Dim sngStartRT, sngEndRT As Single

        On Error GoTo LoadMSFunctionInfoErrorHandler

        ioFileInfo = New System.IO.FileInfo(strMLynxDataFolderPath)
        If ioFileInfo.Exists Then
            ' strMLynxDataFolderPath contains a file; remove the filename from strMLynxDataFolderPath
            strCleanMLynxDataFolderPath = ioFileInfo.Directory.FullName
        Else
            strCleanMLynxDataFolderPath = String.Copy(strMLynxDataFolderPath)
        End If

        blnFileValidated = False
        If LoadMSFileHeader(udtThisMSData, strCleanMLynxDataFolderPath) Then
            udtThisMSData.UserSuppliedDataDirPath = strMLynxDataFolderPath
            udtThisMSData.CurrentDataDirPath = strCleanMLynxDataFolderPath

            With udtThisMSData
                ' Use sFuncInfo to read the header information from the current file
                .FunctionCount = NativeIOGetFunctionCount(strCleanMLynxDataFolderPath)

                If .FunctionCount > 0 Then
                    blnFileValidated = True
                    ReDim .FunctionInfo(.FunctionCount)

                    ' Note that the function array is 1-based
                    For lngFunctionNumber = 1 To .FunctionCount

                        InitializeFunctionInfo(.FunctionInfo(lngFunctionNumber), lngFunctionNumber)

                        If NativeIOGetFunctionInfo(strCleanMLynxDataFolderPath, .FunctionInfo(lngFunctionNumber)) Then

                            If .FunctionInfo(lngFunctionNumber).ScanCount > 0 Then
                                Call NativeIOGetScanInfo(strCleanMLynxDataFolderPath, .FunctionInfo(lngFunctionNumber), 1, udtScanIndexRecord)

                                ' ToDo: Get the Start and End mass for the given scan
                                sngStartMass = 0
                                sngEndMass = 0

                                ' Since the first scan may not have the full mass range, we'll also check a scan
                                '  in the middle of the file as a random comparison
                                If .FunctionInfo(lngFunctionNumber).ScanCount >= 3 Then
                                    'Call sScanStats.GetScanStats(strCleanMLynxDataFolderPath, lngFunctionNumber, .ProcessNumber, CLng(.ScanCount / 3))
                                    'If sScanStats.LoMass < sngStartMass Then sngStartMass = sScanStats.LoMass
                                    'If sScanStats.HiMass > sngEndMass Then sngEndMass = sScanStats.HiMass
                                End If

                                If .FunctionInfo(lngFunctionNumber).ScanCount >= 2 Then
                                    'Call sScanStats.GetScanStats(strCleanMLynxDataFolderPath, lngFunctionNumber, .ProcessNumber, CLng(.ScanCount / 2))
                                    'If sScanStats.LoMass < sngStartMass Then sngStartMass = sScanStats.LoMass
                                    'If sScanStats.HiMass > sngEndMass Then sngEndMass = sScanStats.HiMass
                                End If

                                'Call sScanStats.GetScanStats(strCleanMLynxDataFolderPath, lngFunctionNumber, .ProcessNumber, .ScanCount)
                                'If sScanStats.LoMass < sngStartMass Then sngStartMass = sScanStats.LoMass
                                'If sScanStats.HiMass > sngEndMass Then sngEndMass = sScanStats.HiMass
                            Else
                                sngStartMass = 0
                                sngEndMass = 0
                            End If

                            .FunctionInfo(lngFunctionNumber).StartMass = sngStartMass
                            .FunctionInfo(lngFunctionNumber).EndMass = sngEndMass
                        Else
                            .FunctionInfo(lngFunctionNumber).ScanCount = 0
                        End If
                    Next lngFunctionNumber
                Else
                    .FunctionCount = 0
                End If
            End With

            If udtThisMSData.FunctionCount > 0 Then
                Call NativeIOReadCalInfoFromHeader(udtThisMSData)
            End If
        Else
            udtThisMSData.FunctionCount = 0
        End If

        LoadMSFunctionInfo = udtThisMSData.FunctionCount
        Exit Function

LoadMSFunctionInfoErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in LoadMSFunctionInfo:" & Err.Description)

        If Not blnFileValidated Then
            ' Assume invalid data file
            SetErrorCode(eErrorCodeConstants.DataFolderReadError)
            udtThisMSData.FunctionCount = 0
        End If
        LoadMSFunctionInfo = udtThisMSData.FunctionCount

    End Function

    Private Function LoadMSScanHeader(ByRef udtScanStatsSingleScan As udtScanStatsType, ByRef udtThisMSData As udtMSDataType, ByVal lngFunctionNumber As Integer, ByVal lngScanNumber As Integer) As Integer
        ' Loads information on the given scan for the given function
        ' Returns the number of peaks in the scan; returns 0 if an error
        '
        ' Note that the calling function must validate that lngFunctionNumber is valid
        ' Since this function uses MSData.FunctionInfo, one must call NativeIOGetFunctionInfo
        '  to populate .FunctionInfo before calling this function

        Dim udtScanIndexRecord As udtScanIndexRecordType

        On Error GoTo LoadMSScanHeaderErrorHandler

        udtScanStatsSingleScan.PeakCount = 0

        With udtScanStatsSingleScan
            .Calibrated = False
            .Continuum = False
            .Overload = False

            .MassStart = 0
            .MassEnd = 0
            .SetMass = 0

            .BPI = 0
            .BPIMass = 0
            .TIC = 0

            .PeakCount = 0
            .RetnTime = 0
        End With

        If NativeIOGetScanInfo(udtThisMSData.CurrentDataDirPath, udtThisMSData.FunctionInfo(lngFunctionNumber), lngScanNumber, udtScanIndexRecord) Then
            With udtScanStatsSingleScan
                .Calibrated = udtScanIndexRecord.ScanContainsCalibratedMasses
                .Continuum = udtScanIndexRecord.ContiuumDataOverride
                .Overload = udtScanIndexRecord.ScanOverload

                .MassStart = udtScanIndexRecord.LoMass
                .MassEnd = udtScanIndexRecord.HiMass

                .BPI = udtScanIndexRecord.BasePeakIntensity
                .BPIMass = udtScanIndexRecord.BasePeakMass
                .TIC = udtScanIndexRecord.TicValue

                .PeakCount = udtScanIndexRecord.NumSpectralPeaks
                .RetnTime = udtScanIndexRecord.ScanTime

                .SetMass = udtScanIndexRecord.SetMass
            End With

        End If

LoadMSScanHeaderCleanup:
        On Error Resume Next
        LoadMSScanHeader = udtScanStatsSingleScan.PeakCount
        Exit Function

LoadMSScanHeaderErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in LoadMSScanHeader:" & Err.Description)

        Resume LoadMSScanHeaderCleanup

    End Function

    Private Sub SetErrorCode(ByRef eNewErrorCode As eErrorCodeConstants)
        mErrorCode = eNewErrorCode
    End Sub

    Private Function ValidateDataFolder(ByVal strMLynxDataFolderPath As String) As Boolean
        ' Returns True if valid, False if not valid

        Dim blnValidDataFolder As Boolean
        Dim lngNumFunctions As Integer

        mErrorCode = eErrorCodeConstants.NoError
        blnValidDataFolder = False

        If strMLynxDataFolderPath Is Nothing OrElse strMLynxDataFolderPath.Length = 0 Then
            Return False
        Else
            strMLynxDataFolderPath = strMLynxDataFolderPath.Trim
        End If
        If MSData.UserSuppliedDataDirPath Is Nothing Then
            MSData.UserSuppliedDataDirPath = String.Empty
        End If

        If MSData.FunctionCount = 0 OrElse MSData.UserSuppliedDataDirPath.ToLower <> strMLynxDataFolderPath.ToLower Then

            lngNumFunctions = LoadMSFunctionInfo(MSData, strMLynxDataFolderPath)
            If lngNumFunctions > 0 Then
                blnValidDataFolder = True
            Else
                If mErrorCode = eErrorCodeConstants.NoError Then SetErrorCode(eErrorCodeConstants.DataFolderReadError)
            End If
        Else
            blnValidDataFolder = True
            mErrorCode = eErrorCodeConstants.NoError
        End If

        Return blnValidDataFolder

    End Function


    '---------------------------------------------------------
    ' The following functions are used for Native file IO
    '---------------------------------------------------------

    Private Function ConstructValidDataFilePath(ByRef strDesiredDataFilePath As String, ByRef DataFilePath As String) As Boolean
        ' Fill Datafile variable with directory selected in selection box
        Dim x As Short

        x = InStr(LCase(strDesiredDataFilePath), ".raw")
        If x = 0 Then
            SetErrorCode(eErrorCodeConstants.InvalidDataFolderPath)
            ConstructValidDataFilePath = False
        Else
            DataFilePath = strDesiredDataFilePath
            ConstructValidDataFilePath = True
        End If

    End Function

    Private Function CreateMask(ByRef StartBit As Byte, ByRef EndBit As Byte) As Int64
        ' Note: The mask needs to be Int64 data type to allow for unsigned Int32 masks
        ' This is because the VB Int32 data type has a maximum value of 2^32 / 2 - 1 while
        '  unsigned Int32 can be up to 2^32-1

        Dim BitIndex As Byte
        Dim ThisMask As Int64

        If StartBit = 0 Then
            ThisMask = (2 ^ (EndBit + 1)) - 1
        Else
            ThisMask = 0
            For BitIndex = StartBit To EndBit
                ThisMask += (2 ^ BitIndex)
            Next BitIndex
        End If

        Return ThisMask

    End Function

    Private Sub CreateNativeDataMasks()

        ' Create the bit masks for the PackedFunctionInfo
        maskFunctionType = CreateMask(0, 4)
        maskIonMode = CreateMask(5, 9)
        maskAcquisitionDataType = CreateMask(10, 13)

        ' Create the bit masks for the Packed MS/MS Info
        maskCollisionEnergy = CreateMask(0, 7)
        maskSegmentChannelCount = CreateMask(8, 15)

        ' Create the bit masks for the packed scan info
        maskSpectralPeak = CreateMask(0, 21)
        maskSegment = CreateMask(22, 26)
        maskUseFollowingContinuum = CreateMask(27, 27)
        maskContiuumDataOverride = CreateMask(28, 28)
        maskScanContainsMolecularMasses = CreateMask(29, 29)
        maskScanContainsCalibratedMasses = CreateMask(30, 30)

        ' Create the masks for the packed base peak info
        maskBPIntensityScale = CreateMask(0, 3) ' Also applies to High Intensity Calibrated data and High Accuracy Calibrated Data
        maskBPMassExponent = CreateMask(4, 8)

        maskBPCompressedDataIntensityScale = CreateMask(0, 2)
        maskBPCompressedDataIntensity = CreateMask(3, 10)

        maskBPStandardDataIntensityScale = CreateMask(0, 2) ' Also applies to Uncalibrated data
        maskBPStandardDataIntensity = CreateMask(3, 15) ' Also applies to Uncalibrated data
        maskBPStandardDataMass = CreateMask(0, 23)

        maskBPUncalibratedDataChannelNumber = CreateMask(0, 27)

    End Sub

    Private Function CLngSafe(ByRef strValue As String) As Integer
        On Error Resume Next
        If IsNumeric(strValue) Then
            CLngSafe = CInt(strValue)
        End If
    End Function

    Private Function CSngSafe(ByRef strValue As String) As Single
        If IsNumeric(strValue) Then
            CSngSafe = CSng(strValue)
        End If
    End Function

    Private Function GetFunctionNumberZeroPadded(ByRef lngFunctionNumber As Integer) As String
        Return lngFunctionNumber.ToString.PadLeft(3, "0")
    End Function

    Private Function NativeIOGetFunctionCount(ByRef DataDirPath As String) As Integer
        ' Returns the number of functions, 0 if an error

        Dim strFunctnsFile, strDataFile As String

        Dim intFileNumber As Short
        Dim lngFunctionCount As Integer

        On Error GoTo NativeIOGetNumFunctionsErrorHandler


        strFunctnsFile = System.IO.Path.Combine(DataDirPath, "_functns.inf")
        lngFunctionCount = 0

        If System.IO.File.Exists(strFunctnsFile) Then
            intFileNumber = FreeFile()
            FileOpen(intFileNumber, strFunctnsFile, OpenMode.Random, OpenAccess.Read, OpenShare.Shared)
            lngFunctionCount = LOF(intFileNumber) / NATIVE_FUNCTION_INFO_SIZE_BYTES
            FileClose(intFileNumber)
            intFileNumber = -1
        End If

NativeIOGetNumFunctionsCleanup:
        NativeIOGetFunctionCount = lngFunctionCount
        Exit Function

NativeIOGetNumFunctionsErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOGetFunctionCount: " & Err.Description)

        On Error Resume Next
        If intFileNumber > 0 Then FileClose(intFileNumber)
        Resume NativeIOGetNumFunctionsCleanup

    End Function

    Private Function NativeIOGetFunctionInfo(ByRef DataDirPath As String, ByRef udtMSFunctionInfo As udtMSFunctionInfoType) As Boolean
        ' Returns True if success, False if failure

        Dim udtNativeFunctionInfo As udtRawFunctionDescriptorRecordType
        InitializeNativeFunctionInfo(udtNativeFunctionInfo)

        Dim strFunctnsFile As String

        Dim brInFile As System.IO.BinaryReader

        Dim intIndex As Integer
        Dim lngFunctionCount As Integer
        Dim lngScanCount As Integer

        Dim blnSuccess As Boolean

        On Error GoTo NativeIOGetFunctionInfoErrorHandler

        strFunctnsFile = System.IO.Path.Combine(DataDirPath, "_functns.inf")
        lngFunctionCount = 0

        If System.IO.File.Exists(strFunctnsFile) Then
            brInFile = New System.IO.BinaryReader(New System.IO.FileStream(strFunctnsFile, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))
            lngFunctionCount = brInFile.BaseStream.Length / NATIVE_FUNCTION_INFO_SIZE_BYTES

            If udtMSFunctionInfo.FunctionNumber < 1 Or udtMSFunctionInfo.FunctionNumber > lngFunctionCount Then
                brInFile.Close()
                blnSuccess = False
            Else
                ' Since we're using Binary Access, we need to specify the Byte Offset to start reading at
                ' The first byte is 1, and that is where Function 1 can be found
                ' Function 2 can be found NATIVE_FUNCTION_INFO_SIZE_BYTES+1 bytes into the file

                brInFile.BaseStream.Seek((udtMSFunctionInfo.FunctionNumber - 1) * NATIVE_FUNCTION_INFO_SIZE_BYTES, IO.SeekOrigin.Begin)

                With udtNativeFunctionInfo
                    .PackedFunctionInfo = brInFile.ReadInt16()
                    .CycleTime = brInFile.ReadSingle()
                    .InterScanDelay = brInFile.ReadSingle()
                    .StartRT = brInFile.ReadSingle()
                    .EndRT = brInFile.ReadSingle()
                    .ScanCount = brInFile.ReadInt32()

                    ' Packed MS/MS Info:
                    '   bits 0-7: collision energy
                    '   bits 8-15: segment/channel count
                    .PackedMSMSInfo = brInFile.ReadInt16()

                    ' The following are more MS/MS parameters
                    .FunctionSetMass = brInFile.ReadSingle()
                    .InterSegmentChannelTime = brInFile.ReadSingle()

                    ' Up to 32 segment scans can be conducted for a MS/MS run
                    ' The following three arrays store the segment times, start, and end masses
                    For intIndex = 0 To 31
                        .SegmentScanTimes(intIndex) = brInFile.ReadInt32()
                    Next intIndex
                    For intIndex = 0 To 31
                        .SegmentStartMasses(intIndex) = brInFile.ReadInt32()
                    Next intIndex
                    For intIndex = 0 To 31
                        .SegmentEndMasses(intIndex) = brInFile.ReadInt32()
                    Next intIndex


                End With
                brInFile.Close()

                blnSuccess = True
                With udtNativeFunctionInfo
                    If .PackedFunctionInfo = 0 And .CycleTime = 0 And .InterScanDelay = 0 Then
                        ' Empty function record; see if file even exists
                        If System.IO.File.Exists(System.IO.Path.Combine(DataDirPath, "_func" & GetFunctionNumberZeroPadded(lngFunctionCount + 1) & ".dat")) Then
                            ' Nope, file does not exist, function is invalid
                            blnSuccess = False
                        End If
                    End If
                End With

                If blnSuccess Then
                    ' Copy data from udtNativeFunctionInfo to udtFunctionInfo
                    With udtMSFunctionInfo
                        .FunctionTypeID = udtNativeFunctionInfo.PackedFunctionInfo And maskFunctionType

                        ' 0=MS, 1=SIR, 2=DLY, 3=CAT, 4=OFF, 5=PAR, 6=DAU, 7=NL, 8=NG,
                        ' 9=MRM, 10=Q1F, 11=MS2, 12=DAD, 13=TOF, 14=PSD
                        ' 16=QTOF MS/MS, 17=MTOF, 18=LCT/QTOF Normal
                        .FunctionType = 0
                        Select Case .FunctionTypeID
                            Case 0 : .FunctionTypeText = "MS"
                            Case 1 : .FunctionTypeText = "SIR"
                            Case 2 : .FunctionTypeText = "DLY"
                            Case 3 : .FunctionTypeText = "CAT"
                            Case 4 : .FunctionTypeText = "OFF"
                            Case 5 : .FunctionTypeText = "PAR"
                            Case 6 : .FunctionTypeText = "DAU"
                                .FunctionType = 1
                            Case 7 : .FunctionTypeText = "NL"
                            Case 8 : .FunctionTypeText = "NG"
                            Case 9 : .FunctionTypeText = "MRM"
                            Case 10 : .FunctionTypeText = "Q1F"
                            Case 11 : .FunctionTypeText = "MS2"
                                .FunctionType = 1
                            Case 12 : .FunctionTypeText = "DAD"
                            Case 13 : .FunctionTypeText = "TOF"
                            Case 14 : .FunctionTypeText = "PSD"
                            Case 16 : .FunctionTypeText = "TOF MS/MS"
                                .FunctionType = 1
                            Case 17 : .FunctionTypeText = "TOF MS"
                            Case 18 : .FunctionTypeText = "TOF MS"
                            Case Else : .FunctionTypeText = "MS Unknown"
                        End Select

                        .IonMode = CShort(udtNativeFunctionInfo.PackedFunctionInfo And maskIonMode) / 32 ' 32 = 2^5
                        .AcquisitionDataType = CShort(udtNativeFunctionInfo.PackedFunctionInfo And maskAcquisitionDataType) / 1024 ' 1024 = 2^10

                        .CycleTime = udtNativeFunctionInfo.CycleTime
                        .InterScanDelay = udtNativeFunctionInfo.InterScanDelay
                        .StartRT = udtNativeFunctionInfo.StartRT
                        .EndRT = udtNativeFunctionInfo.EndRT

                        .MsMsCollisionEnergy = udtNativeFunctionInfo.PackedMSMSInfo And maskCollisionEnergy
                        .MSMSSegmentOrChannelCount = NumConversion.Int32ToUnsigned(udtNativeFunctionInfo.PackedMSMSInfo) / 256 ' 256 = 2^8

                        .FunctionSetMass = udtNativeFunctionInfo.FunctionSetMass
                        .InterSegmentChannelTime = udtNativeFunctionInfo.InterSegmentChannelTime

                    End With

                    ' Since udtNativeFunctionInfo.ScanCount is always 0, we need to use NativeIOGetScanCount instead
                    lngScanCount = NativeIOGetScanCount(DataDirPath, udtMSFunctionInfo)
                    If udtMSFunctionInfo.ScanCount <> lngScanCount Then
                        ' This is unexpected
                        Debug.WriteLine("Scan count values do not agree in NativeIOGetFunctionInfo")
                    End If

                End If
            End If

        End If

NativeIOGetFunctionInfoCleanup:
        NativeIOGetFunctionInfo = blnSuccess
        Exit Function

NativeIOGetFunctionInfoErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOGetFunctionInfo: " & Err.Description)

        On Error Resume Next
        If Not brInFile Is Nothing Then brInFile.Close()

        Resume NativeIOGetFunctionInfoCleanup

    End Function

    Private Function NativeIOGetScanCount(ByRef DataDirPath As String, ByRef udtMSFunctionInfo As udtMSFunctionInfoType) As Integer
        ' Returns the number of scans for the given function
        ' Also updates udtMSFunctionInfo.ScanCount
        ' Returns 0 if an error
        '
        ' Note that udtMSFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount

        ' This udt is used for most files
        Dim udtNativeScanIndexRecord As udtRawScanIndexRecordType

        Dim strFuncIdxFile As String

        Dim ioFileInfo As System.IO.FileInfo
        Dim lngNumberOfScansInFunction As Integer

        On Error GoTo NativeIOGetScanCountErrorHandler

        strFuncIdxFile = System.IO.Path.Combine(DataDirPath, "_func" & GetFunctionNumberZeroPadded(udtMSFunctionInfo.FunctionNumber) & ".idx")
        ioFileInfo = New System.IO.FileInfo(strFuncIdxFile)

        lngNumberOfScansInFunction = 0
        If ioFileInfo.Exists Then
            ' The ScanCount stored in the function index file is always 0 rather than the correct number of scans
            ' Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
            '  by the size of each udtRawScanIndexRecordType

            lngNumberOfScansInFunction = ioFileInfo.Length / RAW_SCAN_INDEX_RECORD_SIZE
            udtMSFunctionInfo.ScanCount = lngNumberOfScansInFunction

        End If

NativeIOGetScanCountCleanup:
        On Error Resume Next

        Return lngNumberOfScansInFunction
        Exit Function

NativeIOGetScanCountErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOGetScanCount: " & Err.Description)
        Resume NativeIOGetScanCountCleanup

    End Function

    Private Function NativeIOGetScanInfo(ByRef DataDirPath As String, ByRef udtMSFunctionInfo As udtMSFunctionInfoType, ByVal lngScanNumber As Integer, ByRef udtScanIndexRecord As udtScanIndexRecordType, Optional ByVal blnScanOffsetAndPeakCountOnly As Boolean = False) As Boolean
        ' Returns information on the given scan for the given function
        ' Returns True if success, False if failure
        '
        ' Note that udtMSFunctionInfo.FunctionNumber should correspond to the function number, ranging from 1 to MSData.FunctionCount

        ' This udt is used for most files
        Dim udtNativeScanIndexRecord As udtRawScanIndexRecordType

        ' This udt is used for files with udtMSFunctionInfo.AcquisitionDataType = 0
        ' The difference is that udtRawScanIndexRecordType ends in an Integer then a Long
        '  while this udt ends in a Long, then an Integer
        ' When this udt is used, its values are copied to udtNativeScanIndexRecord directly after reading
        Dim udtNativeScanIndexRecordCompressedScan As udtRawScanIndexRecordCompressedScanType


        Dim strFuncIdxFile As String
        Dim strFuncStsFile As String

        Dim lngNumberOfScansInFunction As Integer

        Dim brInFile As System.IO.BinaryReader
        Dim blnSuccess As Boolean

        On Error GoTo NativeIOGetScanInfoErrorHandler

        strFuncIdxFile = System.IO.Path.Combine(DataDirPath, "_func" & GetFunctionNumberZeroPadded(udtMSFunctionInfo.FunctionNumber) & ".idx")
        strFuncStsFile = System.IO.Path.Combine(DataDirPath, "_func" & GetFunctionNumberZeroPadded(udtMSFunctionInfo.FunctionNumber) & ".sts")

        blnSuccess = False
        If System.IO.File.Exists(strFuncIdxFile) Then
            brInFile = New System.IO.BinaryReader(New System.IO.FileStream(strFuncIdxFile, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))
            lngNumberOfScansInFunction = brInFile.BaseStream.Length / RAW_SCAN_INDEX_RECORD_SIZE

            ' The ScanCount stored in the function index file is always 0 rather than the correct number of scans
            ' Thus, we can determine the number of scans in the function by dividing the size of the file (in bytes)
            '  by the size of each udtRawScanIndexRecordType

            If lngScanNumber < 1 Then lngScanNumber = 1
            If lngNumberOfScansInFunction > 0 And lngScanNumber <= lngNumberOfScansInFunction Then
                ' Just read the record for this scan

                ' Jump to the appropriate file offset based on lngScanNumber
                brInFile.BaseStream.Seek((lngScanNumber - 1) * RAW_SCAN_INDEX_RECORD_SIZE, IO.SeekOrigin.Begin)

                If udtMSFunctionInfo.AcquisitionDataType = 0 Then
                    ' File saved with Acquisition Data Type 0
                    With udtNativeScanIndexRecordCompressedScan
                        .StartScanOffset = brInFile.ReadInt32()
                        .PackedScanInfo = brInFile.ReadInt32()
                        .TicValue = brInFile.ReadSingle()
                        .ScanTime = brInFile.ReadSingle()
                        .PackedBasePeakInfo = brInFile.ReadInt32()
                        .Spare = brInFile.ReadInt16()
                    End With

                    ' Copy from udtNativeScanIndexRecordCompressedScan to udtNativeScanIndexRecord
                    With udtNativeScanIndexRecord
                        .StartScanOffset = udtNativeScanIndexRecordCompressedScan.StartScanOffset
                        .PackedScanInfo = udtNativeScanIndexRecordCompressedScan.PackedScanInfo

                        .TicValue = udtNativeScanIndexRecordCompressedScan.TicValue
                        .ScanTime = udtNativeScanIndexRecordCompressedScan.ScanTime

                        .PackedBasePeakIntensity = 0 ' Unused
                        .PackedBasePeakInfo = udtNativeScanIndexRecordCompressedScan.PackedBasePeakInfo
                    End With
                Else
                    ' File saved with Acquisition Data Type other than 0
                    With udtNativeScanIndexRecord
                        .StartScanOffset = brInFile.ReadInt32()
                        .PackedScanInfo = brInFile.ReadInt32()
                        .TicValue = brInFile.ReadSingle()
                        .ScanTime = brInFile.ReadSingle()
                        .PackedBasePeakIntensity = brInFile.ReadInt16
                        .PackedBasePeakInfo = brInFile.ReadInt32()
                    End With
                End If

                blnSuccess = True
            End If
            brInFile.Close()

        End If

        If blnSuccess Then
            With udtScanIndexRecord
                .StartScanOffset = udtNativeScanIndexRecord.StartScanOffset

                .NumSpectralPeaks = udtNativeScanIndexRecord.PackedScanInfo And maskSpectralPeak

                If Not blnScanOffsetAndPeakCountOnly Then
                    .SegmentNumber = CShort(udtNativeScanIndexRecord.PackedScanInfo And maskSegment) / 4194304 ' 4194304 = 2^22
                    .UseFollowingContinuum = (udtNativeScanIndexRecord.PackedScanInfo And maskUseFollowingContinuum)
                    .ContiuumDataOverride = (udtNativeScanIndexRecord.PackedScanInfo And maskContiuumDataOverride)
                    .ScanContainsMolecularMasses = (udtNativeScanIndexRecord.PackedScanInfo And maskScanContainsMolecularMasses)
                    .ScanContainsCalibratedMasses = (udtNativeScanIndexRecord.PackedScanInfo And maskScanContainsCalibratedMasses)
                    If udtNativeScanIndexRecord.PackedScanInfo <> System.Math.Abs(udtNativeScanIndexRecord.PackedScanInfo) Then
                        .ScanOverload = True
                    End If

                    .TicValue = udtNativeScanIndexRecord.TicValue
                    .ScanTime = udtNativeScanIndexRecord.ScanTime

                    .BasePeakIntensity = UnpackIntensity(udtNativeScanIndexRecord.PackedBasePeakIntensity, udtNativeScanIndexRecord.PackedBasePeakInfo, udtMSFunctionInfo.AcquisitionDataType)

                    .BasePeakMass = UnpackMass(udtNativeScanIndexRecord.PackedBasePeakInfo, udtMSFunctionInfo.AcquisitionDataType, True)

                    ' ToDo: May need to calibrate the base peak mass
                    .BasePeakMass = .BasePeakMass

                    .LoMass = 0 ' ToDo: Figure out if this can be read from the FunctionIndex file
                    .HiMass = 0
                    .SetMass = 0 ' This will get populated below
                End If
            End With

        End If


NativeIOGetScanInfoCleanup:
        On Error Resume Next
        If Not brInFile Is Nothing Then brInFile.Close()

        NativeIOGetScanInfo = blnSuccess
        Exit Function

NativeIOGetScanInfoErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOGetScanInfo: " & Err.Description)
        Resume NativeIOGetScanInfoCleanup

    End Function

    Private Sub NativeIOParseCalibrationCoeffs(ByVal strTextToParse As String, ByRef intCalibrationCoeffCount As Short, ByRef dblCalibrationCoeffs() As Double, ByRef intCalibrationTypeID As Short)

        Dim strCalParameters() As String
        Dim intCalIndex As Short

        strCalParameters = strTextToParse.Split(","c)
        intCalibrationCoeffCount = 0
        For intCalIndex = 0 To strCalParameters.Length - 1
            If IsNumeric(strCalParameters(intCalIndex)) Then
                dblCalibrationCoeffs(intCalIndex) = CDbl(strCalParameters(intCalIndex))
                intCalibrationCoeffCount = intCalibrationCoeffCount + 1
            Else
                ' Non-numeric coefficient encountered; stop populating the coefficients
                Exit For
            End If
        Next intCalIndex

        For intCalIndex = strCalParameters.Length - 1 To 0 Step -1
            If UCase(Left(strCalParameters(intCalIndex), 1)) = "T" Then
                strCalParameters(intCalIndex) = Mid(strCalParameters(intCalIndex), 2)
                If IsNumeric(strCalParameters(intCalIndex)) Then
                    intCalibrationTypeID = CShort(strCalParameters(intCalIndex))
                End If
                Exit For
            End If
        Next intCalIndex

    End Sub

    Private Function NativeIOReadCalInfoFromHeader(ByRef udtThisMSData As udtMSDataType) As Boolean
        ' Looks for the "$$ Cal Function" lines in the _HEADER.TXT file
        ' Returns True if successful, False if not
        '
        ' This function should only be called by LoadMSFunctionInfo and only after the functions have been determined

        Const CAL_FUNCTION_NAME As String = "CAL FUNCTION"
        Const CAL_STDDEV_FUNCTION_NAME As String = "CAL STDDEV FUNCTION"

        Dim strFilePath, strLineIn As String
        Dim intColonLoc As Short
        Dim strKeyValue As String

        Dim lngFunctionNumber As Integer

        Dim blnSuccess As Boolean

        Dim srInFile As System.IO.StreamReader

        On Error GoTo NativeIOReadCalInfoFromHeaderErrorHandler

        strFilePath = System.IO.Path.Combine(udtThisMSData.CurrentDataDirPath, "_HEADER.TXT")
        blnSuccess = False

        If System.IO.File.Exists(strFilePath) Then
            srInFile = New System.IO.StreamReader(strFilePath)
            Do While srInFile.Peek() >= 0
                strLineIn = srInFile.ReadLine()

                If Not strLineIn Is Nothing Then
                    ' All valid lines start with $$
                    If strLineIn.StartsWith("$$") Then
                        ' Remove the first three characters (we actually remove the first 2 then Trim, since the third character is supposed to be a space)

                        strLineIn = strLineIn.Substring(2).Trim
                        intColonLoc = strLineIn.IndexOf(":"c)
                        strKeyValue = strLineIn.Substring(intColonLoc + 1).Trim

                        If strLineIn.ToUpper.StartsWith(CAL_FUNCTION_NAME) Then
                            ' Calibration equation for one of the functions
                            lngFunctionNumber = CLngSafe(strLineIn.Substring(CAL_FUNCTION_NAME.Length, intColonLoc - CAL_FUNCTION_NAME.Length))
                            If lngFunctionNumber >= 1 And lngFunctionNumber <= udtThisMSData.FunctionCount Then
                                With udtThisMSData.FunctionInfo(lngFunctionNumber)
                                    NativeIOParseCalibrationCoeffs(strKeyValue, .CalibrationCoefficientCount, .CalibrationCoefficients, .CalTypeID)
                                End With
                            Else
                                ' Calibration equation for non-existent function
                                ' This shouldn't happen
                            End If
                        ElseIf strLineIn.ToUpper.StartsWith(CAL_STDDEV_FUNCTION_NAME) Then
                            lngFunctionNumber = CLngSafe(strLineIn.Substring(CAL_STDDEV_FUNCTION_NAME.Length, intColonLoc - CAL_STDDEV_FUNCTION_NAME.Length))
                            If lngFunctionNumber >= 1 And lngFunctionNumber <= udtThisMSData.FunctionCount Then
                                With udtThisMSData.FunctionInfo(lngFunctionNumber)
                                    If IsNumeric(strKeyValue) Then
                                        .CalStDev = CDbl(strKeyValue)
                                    End If
                                End With
                            End If
                        End If

                    End If

                End If
            Loop

            srInFile.Close()
            blnSuccess = True
        End If

NativeIOReadCalInfoFromHeaderCleanup:
        On Error Resume Next
        NativeIOReadCalInfoFromHeader = blnSuccess
        Exit Function

NativeIOReadCalInfoFromHeaderErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOReadCalInfoFromHeader: " & Err.Description)
        Resume NativeIOReadCalInfoFromHeaderCleanup

    End Function

    Private Function NativeIOReadHeader(ByRef DataDirPath As String, ByRef HeaderInfo As udtMSHeaderInfoType) As Boolean
        ' Duplicates job of DMRawReadHeader to avoid need for calling Dll
        ' Returns True if successful, False if not

        Dim strFilePath, strLineIn As String
        Dim intColonLoc, intCommaLoc As Short

        Dim strKeyName As String
        Dim strKeyValue As String
        Dim blnSuccess As Boolean

        Dim srInFile As System.IO.StreamReader

        On Error GoTo NativeIOReadHeaderErrorHandler

        strFilePath = System.IO.Path.Combine(DataDirPath, "_HEADER.TXT")
        blnSuccess = False

        If System.IO.File.Exists(strFilePath) Then
            With HeaderInfo
                .AcquDate = ""
                .AcquName = ""
                .AcquTime = ""
                .JobCode = ""
                .TaskCode = ""
                .UserName = ""
                .Instrument = ""
                .InstrumentType = ""
                .Conditions = ""
                .LabName = ""
                .SampleDesc = ""
                .SolventDelay = 0
                .Submitter = ""
                .SampleID = ""
                .BottleNumber = ""
                .PlateDesc = ""
                .MuxStream = 0
                .VersionMajor = 0
                .VersionMinor = 0

                .CalMS1StaticCoeffCount = 0
                ReDim .CalMS1StaticCoeffs(6)
                .CalMS1StaticTypeID = 0

                .CalMS2StaticCoeffCount = 0
                ReDim .CalMS2StaticCoeffs(6)
                .CalMS2StaticTypeID = 0
            End With

            srInFile = New System.IO.StreamReader(strFilePath)
            Do While srInFile.Peek() >= 0
                strLineIn = srInFile.ReadLine()

                If Not strLineIn Is Nothing Then
                    ' All valid lines start with $$
                    If strLineIn.StartsWith("$$") Then
                        ' Remove the first three characters (we actually remove the first 2 then Trim, since the third character is supposed to be a space)
                        strLineIn = strLineIn.Substring(2).Trim
                        intColonLoc = strLineIn.IndexOf(":"c)
                        strKeyName = strLineIn.Substring(0, intColonLoc).ToUpper
                        strKeyValue = strLineIn.Substring(intColonLoc + 1).Trim
                        With HeaderInfo
                            Select Case strKeyName
                                Case "VERSION"
                                    If IsNumeric(strKeyValue) Then
                                        .VersionMajor = CShort(strKeyValue)
                                        .VersionMinor = CSng(strKeyValue) - .VersionMajor
                                    End If
                                Case "ACQUIRED NAME" : .AcquName = strKeyValue
                                Case "ACQUIRED DATE" : .AcquDate = strKeyValue
                                Case "ACQUIRED TIME" : .AcquTime = strKeyValue
                                Case "JOB CODE" : .JobCode = strKeyValue
                                Case "TASK CODE" : .TaskCode = strKeyValue
                                Case "USER NAME" : .UserName = strKeyValue
                                Case "INSTRUMENT" : .Instrument = strKeyValue
                                Case "CONDITIONS" : .Conditions = strKeyValue
                                Case "LABORATORY NAME" : .LabName = strKeyValue
                                Case "SAMPLE DESCRIPTION" : .SampleDesc = strKeyValue
                                Case "SOLVENT DELAY" : .SolventDelay = CSngSafe(strKeyValue)
                                Case "SUBMITTER" : .Submitter = strKeyValue
                                Case "SAMPLEID" : .SampleID = strKeyValue
                                Case "BOTTLE NUMBER" : .BottleNumber = strKeyValue
                                Case "PLATE DESC" : .PlateDesc = strKeyValue
                                Case "MUX STREAM" : .MuxStream = CLngSafe(strKeyValue)
                                Case "CAL MS1 STATIC"
                                    NativeIOParseCalibrationCoeffs(strKeyValue, .CalMS1StaticCoeffCount, .CalMS1StaticCoeffs, .CalMS1StaticTypeID)
                                Case "CAL MS2 STATIC"
                                    NativeIOParseCalibrationCoeffs(strKeyValue, .CalMS2StaticCoeffCount, .CalMS2StaticCoeffs, .CalMS2StaticTypeID)
                                Case Else
                                    ' Ignore it
                            End Select
                        End With
                    End If

                End If
            Loop
            srInFile.Close()
            blnSuccess = True
        End If

NativeIOReadHeaderCleanup:
        On Error Resume Next
        NativeIOReadHeader = blnSuccess
        Exit Function

NativeIOReadHeaderErrorHandler:
        System.Diagnostics.Debug.WriteLine("Error in NativeIOReadHeader: " & Err.Description)
        Resume NativeIOReadHeaderCleanup

    End Function

    Private Function ExtractFromBitsInt32(ByRef lngPackedValue As Integer, ByRef intStartBit As Byte, ByRef intEndBit As Byte) As Int32

        Dim intUnpackedValue As Integer

        If intEndBit < 31 Then

            If intStartBit = 0 Then
                intUnpackedValue = lngPackedValue And CreateMask(0, intEndBit)
            Else
                intUnpackedValue = (lngPackedValue / (2 ^ intStartBit)) And CreateMask(0, intEndBit - intStartBit)
            End If
        Else
            intUnpackedValue = CInt(NumConversion.Int32ToUnsigned(lngPackedValue) / 2 ^ intStartBit)
        End If

        ExtractFromBitsInt32 = intUnpackedValue
    End Function

    Private Function UnpackIntensity(ByRef PackedBasePeakIntensity As Short, ByRef PackedBasePeakInfo As Integer, ByRef intAcquisitionDataType As Short) As Single
        ' See note for Acquisition Data Types 9 to 12 below
        Dim sngUnpackedIntensity As Single

        Select Case intAcquisitionDataType
            Case 9 To 12
                ' Includes type 9, 11, and 12; type 10 is officially unused
                '  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                ' Note: Only use this function to unpack intensities for data in the .IDX file, not for data in the .DAT file
                '       See the NativeIOGetSpectrum function for the method of unpacking intensities in .DAT files

                sngUnpackedIntensity = PackedBasePeakIntensity * 4 ^ (PackedBasePeakInfo And maskBPIntensityScale)
                'Debug.Assert sngUnpackedIntensity = PackedBasePeakIntensity * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 3)
            Case 0
                ' Compressed data
                sngUnpackedIntensity = CShort(PackedBasePeakInfo And maskBPCompressedDataIntensity) / 8 * 4 ^ (PackedBasePeakInfo And maskBPCompressedDataIntensityScale)
                'Debug.Assert sngUnpackedIntensity = ExtractFromBitsInt32(PackedBasePeakInfo, 3, 10) * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 2)
            Case 1 To 7
                ' Standard data and Uncalibrated data
                sngUnpackedIntensity = CShort(PackedBasePeakIntensity And maskBPStandardDataIntensity) / 8 * 4 ^ (PackedBasePeakIntensity And maskBPStandardDataIntensityScale)
                'Debug.Assert sngUnpackedIntensity = ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 3, 15) * 4 ^ ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 0, 2)
            Case 8
                '  High intensity calibrated data
                sngUnpackedIntensity = PackedBasePeakIntensity * 4 ^ (PackedBasePeakInfo And maskBPIntensityScale)
            Case Else
                sngUnpackedIntensity = 0
        End Select
        UnpackIntensity = sngUnpackedIntensity

    End Function

    Private Function UnpackMass(ByRef PackedBasePeakInfo As Integer, ByRef intAcquisitionDataType As Short, ByRef blnProcessingFunctionIndexFile As Boolean) As Double
        ' See note for Acquisition Data Types 9 to 12 below
        Dim MassMantissa As Integer
        Dim MassExponent As Integer
        Dim dblUnpackedMass As Double

        Select Case intAcquisitionDataType
            Case 9 To 12
                ' Includes type 9, 11, and 12; type 10 is officially unused
                '  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                ' Note: Only use this function to unpack massees for data in the .IDX file, not for data in the .DAT file
                '       See the NativeIOGetSpectrum function for the method of unpacking masses in .DAT files

                ' Compute the MassMantissa Value by converting the Packed Value to an Unsigned Int32 (and storing in a Int64),
                '  then right shifting 9 bits by dividing by 2^9
                ' It would be more straightforward to use PackedBasePeakInfo And CreateMask(9, 31) but VB won't let us
                '  And a Currency Value with a Long; this gives an OverFlow error
                MassMantissa = CInt(NumConversion.Int32ToUnsigned(PackedBasePeakInfo) / 512) ' 512 = 2^9

                ' Compute the MassExponent value by multiplying the Packed Value by the appropriate BitMask, then right shifting 4 bits by dividing by 2^4
                MassExponent = CShort(PackedBasePeakInfo And maskBPMassExponent) / 16 ' 16 = 2^4

                If blnProcessingFunctionIndexFile Then
                    ' When computing the BasePeakMass based on data in the _func001.idx
                    '  file, must bump up the Mass Exponent by 8 in order to multiply the
                    '  Mass Mantissa by an additional value of 256 to get the correct value
                    If MassExponent < 6 Then
                        If intAcquisitionDataType = 9 Then
                            ' This only seems to be necessary for files with Acquisition data type 9
                            ' The following Assertion is here to test for that
                            MassExponent = MassExponent + 8
                        End If
                    End If
                End If

                ' Note that we divide by 2^23 to convert the mass mantissa to fractional form
                dblUnpackedMass = MassMantissa / 8388608 * (2 ^ MassExponent) ' 8388608 = 2^23
            Case 0
                ' Compressed data
                ' Compute the MassMantissa Value by converting the Packed Value to an Unsigned Int32 (and storing in a Int64),
                '  then right shifting 11 bits by dividing by 2^11
                ' It would be more straightforward to use PackedBasePeakInfo And CreateMask(11, 31) but VB won't let us
                '  And a Currency Value with a Long; this gives an OverFlow error
                ' We must divide the MassMantissa by 128 to get the mass
                dblUnpackedMass = CInt(NumConversion.Int32ToUnsigned(PackedBasePeakInfo) / 2048) / 128 ' 2048 = 2^11
                'Debug.Assert dblUnpackedMass = ExtractFromBitsInt32(PackedBasePeakInfo, 11, 31) / 128
            Case 1
                ' Standard data
                ' We must divide the MassMantissa by 1024 to get the mass
                dblUnpackedMass = CShort(PackedBasePeakInfo And maskBPStandardDataMass) / 1024
            Case 2 To 7
                ' Uncalibrated data
                ' This type of data doesn't have a base peak mass
                dblUnpackedMass = 0
            Case 8
                ' High intensity calibrated data
                ' Compute the MassMantissa Value by converting the Packed Value to an Unsigned Int32 (and storing in a Int64),
                '  then right shifting 4 bits by dividing by 2^4
                ' We must divide the MassMantissa by 128 to get the mass
                dblUnpackedMass = CInt(NumConversion.Int32ToUnsigned(PackedBasePeakInfo) / 16) / 128 ' 16 = 2^4
                'Debug.Assert dblUnpackedMass = ExtractFromBitsInt32(PackedBasePeakInfo, 4, 31) / 128
            Case Else
                dblUnpackedMass = 0
        End Select
        UnpackMass = dblUnpackedMass

    End Function

    Private Class NumConversion

        Private Const OFFSET_4 As Int64 = 4294967296
        Private Const MAXINT_4 As Int64 = 2147483647
        Private Const OFFSET_2 As Int32 = 65536
        Private Const MAXINT_2 As Int16 = 32767

        Public Shared Function UnsignedToInt32(ByRef Value As Int64) As Int32
            ' If Value < 0 Or Value >= OFFSET_4 Then Error 6 ' Overflow
            If Value <= MAXINT_4 Then
                Return Value
            Else
                Return Value - OFFSET_4
            End If
        End Function

        Public Shared Function Int32ToUnsigned(ByRef Value As Int32) As Int64
            If Value < 0 Then
                Return Value + OFFSET_4
            Else
                Return Value
            End If
        End Function

        Public Shared Function UnsignedToInt16(ByRef Value As Int32) As Int16
            If Value < 0 Or Value >= OFFSET_2 Then Error (6) ' Overflow
            If Value <= MAXINT_2 Then
                Return Value
            Else
                Return Value - OFFSET_2
            End If
        End Function

        Public Shared Function Int16ToUnsigned(ByRef Value As Int16) As Int32
            If Value < 0 Then
                Return Value + OFFSET_2
            Else
                Return Value
            End If
        End Function
    End Class

End Class