Option Strict On

' Base class for derived classes that can read Finnigan .Raw files (LCQ, LTQ, etc.)
' 
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in November 2004
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified November 11, 2004

Namespace FinniganFileIO

    Public MustInherit Class FinniganFileReaderBaseClass

#Region "Structures"

        Public Structure udtFileInfoType
            'Public AcquistionDate As String         ' Typically only defined for instruments converted from other formats
            'Public AcquisitionFilename As String    ' Typically only defined for instruments converted from other formats
            'Public Comment1 As String               ' Typically only defined for instruments converted from other formats
            'Public Comment2 As String               ' Typically only defined for instruments converted from other formats
            Public CreationDate As DateTime
            Public CreatorID As String              ' Logon name of the user when the file was created
            Public InstFlags As String              ' Values should be one of the constants in InstFlags
            Public InstHardwareVersion As String
            Public InstSoftwareVersion As String
            Public InstMethod As String
            Public InstModel As String
            Public InstName As String
            Public InstrumentDescription As String  ' Typically only defined for instruments converted from other formats
            Public InstSerialNumber As String
            Public VersionNumber As Integer         ' File format Version Number
            Public MassResolution As Double
            Public ScanStart As Integer
            Public ScanEnd As Integer
        End Structure

        Public Structure udtScanHeaderInfoType
            Public MSLevel As Integer                   ' 1 means MS, 2 means MS/MS, 3 means MS^3 aka MS/MS/MS
            Public EventNumber As Integer               ' 1 for parent-ion scan; 2 for 1st frag scan, 3 for 2nd frag scan, etc.
            Public SIMScan As Boolean                   ' True if this is a selected ion monitoring scan (i.e. a small scan range is being examined)

            Public NumPeaks As Integer                  ' Number of mass intensity value pairs in the specified scan (may not be defined until .GetScanData() is called; -1 if unknown)
            Public RetentionTime As Double              ' Retention time (in minutes)
            Public LowMass As Double
            Public HighMass As Double
            Public TotalIonCurrent As Double
            Public BasePeakMZ As Double
            Public BasePeakIntensity As Double

            Public FilterText As String
            Public ParentIonMZ As Double

            Public NumChannels As Integer
            Public UniformTime As Boolean               ' Indicates whether the sampling time increment for the controller is constant
            Public Frequency As Double                  ' Sampling frequency for the current controller
            Public IsCentroidScan As Boolean            ' True if centroid (sticks) scan; False if profile (continuum) scan

            Public ScanEventNames() As String
            Public ScanEventValues() As String

            Public StatusLogNames() As String
            Public StatusLogValues() As String
        End Structure

#End Region

#Region "Classwide Variables"
        Protected mCachedFileName As String
        Protected mFileInfo As udtFileInfoType
#End Region

#Region "Interface Functions"
        Public ReadOnly Property FileInfo() As udtFileInfoType
            Get
                Return mFileInfo
            End Get
        End Property
#End Region

        Public MustOverride Function CheckFunctionality() As Boolean
        Public MustOverride Sub CloseRawFile()
        Public MustOverride Function GetNumScans() As Integer
        Public MustOverride Function GetScanInfo(ByVal Scan As Integer, ByRef udtScanHeaderInfo As udtScanHeaderInfoType) As Boolean

        Public MustOverride Overloads Function GetScanData(ByVal Scan As Integer, ByRef dblIonMZ() As Double, ByRef dblIonIntensity() As Double, ByRef udtScanHeaderInfo As udtScanHeaderInfoType) As Integer
        Public MustOverride Overloads Function GetScanData(ByVal Scan As Integer, ByRef dblIonMZ() As Double, ByRef dblIonIntensity() As Double, ByRef udtScanHeaderInfo As udtScanHeaderInfoType, ByVal intMaxNumberOfPeaks As Integer) As Integer

        Public MustOverride Function OpenRawFile(ByVal FileName As String) As Boolean

        Protected MustOverride Function FillFileInfo() As Boolean

    End Class
End Namespace