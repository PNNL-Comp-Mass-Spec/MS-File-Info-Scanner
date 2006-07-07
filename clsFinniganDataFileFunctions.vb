Option Strict On

' These functions utilize the XRawfile.ocx com object to extract scan header info and
' raw mass spectrum info from Finnigan LCQ, LTQ, and LTQ-FT files
' 
' Required Dlls (copy from Xcalibur installation folders): FControl2.dll, Fglobal.dll, Fileio.dll, Fregistry.dll, & XRawfile.ocx
' 
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in November 2004
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified September 26, 2005

Namespace FinniganFileIO

    Public Class XRawFileIO
        Inherits FinniganFileReaderBaseClass

#Region "Constants and Enums"

        Private Const MS_ONLY_C_TEXT As String = " c ms "
        Private Const MS_ONLY_P_TEXT As String = " p ms "
        Private Const FULL_MS_TEXT As String = "Full ms "
        Private Const SIM_MS_TEXT As String = "SIM ms "
        Private Const MS2_TEXT As String = "Full ms2 "

        ' Used with .GetSeqRowSampleType()
        Public Enum SampleTypeConstants
            Unknown = 0
            Blank = 1
            QC = 2
            StandardClear_None = 3
            StandardUpdate_None = 4
            StandardBracket_Open = 5
            StandardBracketStart_MultipleBrackets = 6
            StandardBracketEnd_multipleBrackets = 7
        End Enum

        ' Used with .SetController()
        Public Enum ControllerTypeConstants
            NoDevice = -1
            MS = 0
            Analog = 1
            AD_Card = 2
            PDA = 3
            UV = 4
        End Enum

        ' Used with .GetMassListXYZ()
        Public Enum IntensityCutoffTypeConstants
            None = 0                        ' AllValuesReturned
            AbsoluteIntensityUnits = 1
            RelativeToBasePeak = 2
        End Enum

        'Public Enum ErrorCodeConstants
        '    MassRangeFormatIncorrect = -6
        '    FilterFormatIncorrect = -5
        '    ParameterInvalid = -4
        '    OperationNotSupportedOnCurrentController = -3
        '    CurrentControllerInvalid = -2
        '    RawFileInvalid = -1
        '    Failed = 0
        '    Success = 1
        '    NoDataPresent = 2
        'End Enum

        Public Class InstFlags
            Public Const TIM As String = "Total Ion Map"
            Public Const NLM As String = "Neutral Loss Map"
            Public Const PIM As String = "Parent Ion Map"
            Public Const DDZMap As String = "Data Dependent ZoomScan Map"
        End Class

#End Region

#Region "Structures"

#End Region

#Region "Classwide Variables"

        ' Cached XRawFile object, for faster accessing
        Private mXRawFile As XRAWFILE2Lib.XRawfile

#End Region

        Public Overrides Function CheckFunctionality() As Boolean
            ' I have a feeling this doesn't actually work, and will always return True

            Try
                Dim objXRawFile As New XRAWFILE2Lib.XRawfile
                objXRawFile = Nothing

                ' If we get here, then all is fine
                Return True
            Catch ex As System.Exception
                Return False
            End Try

        End Function

        Public Overrides Sub CloseRawFile()

            Try
                If Not mXRawFile Is Nothing Then
                    mXRawFile.Close()
                End If
            Catch ex As System.Exception
                ' Ignore any errors
            Finally
                mXRawFile = Nothing
                mCachedFileName = String.Empty
            End Try

        End Sub

        Private Function ExtractParentIonMZFromFilterText(ByVal strFilterText As String, ByRef dblParentIonMZ As Double, ByRef intMSLevel As Integer) As Boolean

            ' Parse out the parent ion and collision energy from strFilterText
            ' It should be of the form "+ c d Full ms2 1312.95@45.00 [ 350.00-2000.00]"
            ' or "+ c d Full ms3 1312.95@45.00 873.85@45.00 [ 350.00-2000.00]"

            Const MS2_REGEX As String = "Full ms([2-9]) "

            Dim intCharIndex As Integer
            Dim strMZText As String
            Dim blnMatchFound As Boolean

            Dim intMatchTextLength As Integer

            Dim reFind As System.Text.RegularExpressions.Regex
            Dim reMatch As System.Text.RegularExpressions.Match

            intMSLevel = 1
            dblParentIonMZ = 0
            blnMatchFound = False

            Try

                intCharIndex = 0

                reMatch = reFind.Match(strFilterText, MS2_REGEX)

                If Not reMatch Is Nothing Then
                    If reMatch.Groups.Count = 2 Then
                        intMSLevel = CInt(reMatch.Groups(1).Value)
                        intCharIndex = strFilterText.ToLower.IndexOf(reMatch.ToString.ToLower)
                        intMatchTextLength = reMatch.ToString.Length
                    End If
                End If


                If intCharIndex > 0 Then
                    strMZText = strFilterText.Substring(intCharIndex + intMatchTextLength).Trim

                    intCharIndex = strMZText.IndexOf("@"c)
                    If intCharIndex > 0 Then
                        strMZText = strMZText.Substring(0, intCharIndex)

                        Try
                            dblParentIonMZ = Double.Parse(strMZText)
                            blnMatchFound = True
                        Catch ex As System.Exception
                            dblParentIonMZ = 0
                        End Try
                    ElseIf strMZText.Length > 0 Then
                        ' Find the longest contiguous number that strMZText starts with

                        intCharIndex = -1
                        Do While intCharIndex < strMZText.Length - 1
                            If Char.IsNumber(strMZText.Chars(intCharIndex + 1)) OrElse strMZText.Chars(intCharIndex + 1) = "."c Then
                                intCharIndex += 1
                            Else
                                Exit Do
                            End If
                        Loop

                        If intCharIndex >= 0 Then
                            Try
                                dblParentIonMZ = Double.Parse(strMZText.Substring(0, intCharIndex + 1))
                                blnMatchFound = True
                            Catch ex As System.Exception
                                dblParentIonMZ = 0
                            End Try
                        End If
                    End If
                End If

            Catch ex As System.Exception
                blnMatchFound = False
            End Try

            Return blnMatchFound

        End Function

        Protected Overrides Function FillFileInfo() As Boolean
            ' Populates the mFileInfo structure
            ' Function returns True if no error, False if an error

            Dim intResult As Integer

            Dim intIndex As Integer
            Dim intMethodCount As Integer
            Dim strMethod As String

            Try
                If mXRawFile Is Nothing Then Return False

                ' Make sure the MS controller is selected
                If Not SetMSController() Then Return False

                With mFileInfo

                    mXRawFile.GetCreationDate(.CreationDate)
                    mXRawFile.IsError(intResult)                        ' Unfortunately, .IsError() always returns 0, even if an error occurred
                    If intResult <> 0 Then Return False

                    mXRawFile.GetCreatorID(.CreatorID)

                    mXRawFile.GetInstFlags(.InstFlags)
                    mXRawFile.GetInstHardwareVersion(.InstHardwareVersion)
                    mXRawFile.GetInstSoftwareVersion(.InstSoftwareVersion)

                    mXRawFile.GetNumInstMethods(intMethodCount)

                    .InstMethod = String.Empty
                    For intIndex = 0 To intMethodCount - 1
                        If .InstMethod.Length > 0 Then .InstMethod &= ControlChars.NewLine
                        mXRawFile.GetInstMethod(intIndex, strMethod)
                        .InstMethod &= strMethod
                    Next intIndex

                    mXRawFile.GetInstModel(.InstModel)
                    mXRawFile.GetInstName(.InstName)
                    mXRawFile.GetInstrumentDescription(.InstrumentDescription)
                    mXRawFile.GetInstSerialNumber(.InstSerialNumber)

                    mXRawFile.GetVersionNumber(.VersionNumber)
                    mXRawFile.GetMassResolution(.MassResolution)

                    mXRawFile.GetFirstSpectrumNumber(.ScanStart)
                    mXRawFile.GetLastSpectrumNumber(.ScanEnd)

                    ' The following are typically blank, so we're not reading them
                    'mXRawFile.GetAcquisitionDate(.AcquistionDate)
                    'mXRawFile.GetAcquisitionFileName(.AcquisitionFilename)
                    'mXRawFile.GetComment1(.Comment1)
                    'mXRawFile.GetComment2(.Comment2)

                End With


            Catch ex As System.Exception
                Return False
            End Try

            Return True

        End Function

        Public Overrides Function GetNumScans() As Integer
            ' Returns the number of scans, or -1 if an error

            Dim intResult As Integer
            Dim intScanCount As Integer

            Try
                If mXRawFile Is Nothing Then Return -1

                mXRawFile.GetNumSpectra(intScanCount)
                mXRawFile.IsError(intResult)            ' Unfortunately, .IsError() always returns 0, even if an error occurred
                If intResult = 0 Then
                    Return intScanCount
                Else
                    Return -1
                End If
            Catch ex As System.Exception
                Return -1
            End Try

        End Function

        Public Overrides Function GetScanInfo(ByVal Scan As Integer, ByRef udtScanHeaderInfo As udtScanHeaderInfoType) As Boolean
            ' Function returns True if no error, False if an error

            Dim intResult As Integer

            Dim objLabels As Object
            Dim objValues As Object

            Dim intArrayCount As Integer
            Dim intIndex As Integer
            Dim intCharIndex As Integer

            Dim strFilterText As String

            Dim dblStatusLogRT As Double
            Dim intBooleanVal As Integer

            Dim dblParentIonMZ As Double
            Dim intMSLevel As Integer

            Try
                If mXRawFile Is Nothing Then Return False

                If Scan < mFileInfo.ScanStart Then
                    Scan = mFileInfo.ScanStart
                ElseIf Scan > mFileInfo.ScanEnd Then
                    Scan = mFileInfo.ScanEnd
                End If

                ' Make sure the MS controller is selected
                If Not SetMSController() Then Return False

                With udtScanHeaderInfo
                    ' Reset the values
                    .SIMScan = False
                    .NumPeaks = 0
                    .TotalIonCurrent = 0

                    mXRawFile.GetScanHeaderInfoForScanNum(Scan, .NumPeaks, .RetentionTime, .LowMass, .HighMass, .TotalIonCurrent, .BasePeakMZ, .BasePeakIntensity, .NumChannels, intBooleanVal, .Frequency)
                    mXRawFile.IsError(intResult)        ' Unfortunately, .IsError() always returns 0, even if an error occurred

                    If intResult = 0 Then
                        .UniformTime = CBool(intBooleanVal)

                        intBooleanVal = 0
                        mXRawFile.IsCentroidScanForScanNum(Scan, intBooleanVal)
                        .IsCentroidScan = CBool(intBooleanVal)

                        ' Retrieve the additional parameters for this scan (including Scan Event)
                        mXRawFile.GetTrailerExtraForScanNum(Scan, objLabels, objValues, intArrayCount)

                        .EventNumber = 1
                        If intArrayCount > 0 Then
                            .ScanEventNames = CType(objLabels, String())
                            .ScanEventValues = CType(objValues, String())

                            ' Look for the entry in strLabels named "Scan Event:"
                            ' Entries for the LCQ are:
                            '   Wideband Activation
                            '   Micro Scan Count
                            '   Ion Injection Time (ms)
                            '   Scan Segment
                            '   Scan Event
                            '   Elapsed Scan Time (sec)
                            '   API Source CID Energy
                            '   Resolution
                            '   Average Scan by Inst
                            '   BackGd Subtracted by Inst
                            '   Charge State

                            For intIndex = 0 To intArrayCount - 1
                                If .ScanEventNames(intIndex).ToLower.StartsWith("scan event") Then
                                    Try
                                        .EventNumber = CInt(.ScanEventValues(intIndex))
                                    Catch ex As System.Exception
                                        .EventNumber = 1
                                    End Try
                                    Exit For
                                End If
                            Next intIndex
                        Else
                            ReDim .ScanEventNames(-1)
                            ReDim .ScanEventValues(-1)
                        End If

                        ' Lookup the filter text for this scan
                        ' Parse out the parent ion m/z for fragmentation scans
                        mXRawFile.GetFilterForScanNum(Scan, strFilterText)
                        .FilterText = strFilterText
                        If .FilterText Is Nothing Then .FilterText = String.Empty

                        If .EventNumber <= 1 Then
                            ' XRaw periodically mislabels a scan as .EventNumber = 1 when it's really an MS/MS scan; check for this
                            intCharIndex = .FilterText.ToLower.IndexOf(MS2_TEXT.ToLower)
                            If intCharIndex > 0 Then
                                .EventNumber = 2
                            End If
                        End If

                        If .EventNumber > 1 Then
                            udtScanHeaderInfo.MSLevel = 2

                            ' Parse out the parent ion and collision energy from .FilterText
                            If ExtractParentIonMZFromFilterText(.FilterText, dblParentIonMZ, intMSLevel) Then
                                .ParentIonMZ = dblParentIonMZ
                                If intMSLevel > 2 Then
                                    udtScanHeaderInfo.MSLevel = intMSLevel
                                End If
                            Else
                                ' Could not find "Full ms2" in .FilterText
                                ' XRaw periodically mislabels a scan as .EventNumber > 1 when it's really an MS scan; check for this
                                If ValidateMSScan(.FilterText, .MSLevel, .SIMScan) Then
                                    ' Yes, scan is an MS scan
                                Else
                                    ' Unknown format for .FilterText; return an error
                                    Return False
                                End If
                            End If

                        Else
                            ' Make sure .FilterText contains FULL_MS_TEXT
                            If ValidateMSScan(.FilterText, .MSLevel, .SIMScan) Then
                                ' Yes, scan is an MS scan
                            Else
                                ' Unknown format for .FilterText; return an error
                                Return False
                            End If
                        End If

                        ' Retrieve the Status Log for this scan using the following
                        ' The Status Log includes numerous instrument parameters, including voltages, temperatures, pressures, turbo pump speeds, etc. 
                        objLabels = Nothing
                        objValues = Nothing

                        mXRawFile.GetStatusLogForScanNum(Scan, dblStatusLogRT, objLabels, objValues, intArrayCount)
                        If intArrayCount > 0 Then
                            .StatusLogNames = CType(objLabels, String())
                            .StatusLogValues = CType(objValues, String())
                        Else
                            ReDim .StatusLogNames(-1)
                            ReDim .StatusLogValues(-1)
                        End If

                    End If
                End With

            Catch ex As System.Exception
                Return False
            End Try

            Return True

        End Function

        Private Function SetMSController() As Boolean
            ' A controller is typically the MS, UV, analog, etc.
            ' See ControllerTypeConstants

            Dim intResult As Integer

            mXRawFile.SetCurrentController(ControllerTypeConstants.MS, 1)
            mXRawFile.IsError(intResult)        ' Unfortunately, .IsError() always returns 0, even if an error occurred

            If intResult = 0 Then
                Return True
            Else
                Return False
            End If

        End Function

        Private Function ValidateMSScan(ByVal strFilterText As String, ByRef intMSLevel As Integer, ByRef blnSIMScan As Boolean) As Boolean
            ' Returns True if strFilterText contains a known MS scan type

            Dim blnValidScan As Boolean

            If strFilterText.ToLower.IndexOf(FULL_MS_TEXT.ToLower) > 0 OrElse _
                strFilterText.ToLower.IndexOf(MS_ONLY_C_TEXT.ToLower) > 0 OrElse _
                strFilterText.ToLower.IndexOf(MS_ONLY_P_TEXT.ToLower) > 0 Then
                ' This is really a Full MS scan
                intMSLevel = 1
                blnSIMScan = False
                blnValidScan = True
            Else
                If strFilterText.ToLower.IndexOf(SIM_MS_TEXT.ToLower) > 0 Then
                    ' This is really a SIM MS scan
                    intMSLevel = 1
                    blnSIMScan = True
                    blnValidScan = True
                Else
                    blnValidScan = False
                End If
            End If

            Return blnValidScan
        End Function

        Public Overloads Overrides Function GetScanData(ByVal Scan As Integer, ByRef dblMZList() As Double, ByRef dblIntensityList() As Double, ByRef udtScanHeaderInfo As udtScanHeaderInfoType) As Integer
            ' Return all data points by passing 0 for intMaxNumberOfPeaks
            Return GetScanData(Scan, dblMZList, dblIntensityList, udtScanHeaderInfo, 0)
        End Function

        Public Overloads Overrides Function GetScanData(ByVal Scan As Integer, ByRef dblMZList() As Double, ByRef dblIntensityList() As Double, ByRef udtScanHeaderInfo As udtScanHeaderInfoType, ByVal intMaxNumberOfPeaks As Integer) As Integer
            ' Returns the number of data points, or -1 if an error
            ' If intMaxNumberOfPeaks is <=0, then returns all data; set intMaxNumberOfPeaks to > 0 to limit the number of data points returned

            Dim intDataCount As Integer

            Dim strFilter As String
            Dim intIntensityCutoffValue As Integer
            Dim intCentroidResult As Integer
            Dim dblCentroidPeakWidth As Double

            Dim MassIntensityPairsList As Object
            Dim PeakList As Object

            Dim dblData(,) As Double

            Dim intIndex As Integer

            intDataCount = 0

            Try
                If mXRawFile Is Nothing Then
                    intDataCount = -1
                    Exit Try
                End If

                ' Make sure the MS controller is selected
                If Not SetMSController() Then
                    intDataCount = -1
                    Exit Try
                End If

                If Scan < mFileInfo.ScanStart Then
                    Scan = mFileInfo.ScanStart
                ElseIf Scan > mFileInfo.ScanEnd Then
                    Scan = mFileInfo.ScanEnd
                End If

                strFilter = String.Empty            ' Could use this to filter the data returned from the scan; must use one of the filters defined in the file (see .GetFilters())
                intIntensityCutoffValue = 0

                If intMaxNumberOfPeaks < 0 Then intMaxNumberOfPeaks = 0
                intCentroidResult = 0           ' Set to 1 to indicate that peaks should be centroided (only appropriate for profile data)

                mXRawFile.GetMassListFromScanNum(Scan, strFilter, IntensityCutoffTypeConstants.None, _
                                                 intIntensityCutoffValue, intMaxNumberOfPeaks, intCentroidResult, dblCentroidPeakWidth, _
                                                 MassIntensityPairsList, PeakList, intDataCount)

                If intDataCount > 0 Then
                    dblData = CType(MassIntensityPairsList, Double(,))

                    If dblData.GetUpperBound(1) + 1 < intDataCount Then
                        intDataCount = dblData.GetUpperBound(1) + 1
                    End If

                    ReDim dblMZList(intDataCount - 1)
                    ReDim dblIntensityList(intDataCount - 1)

                    For intIndex = 0 To intDataCount - 1
                        dblMZList(intIndex) = dblData(0, intIndex)
                        dblIntensityList(intIndex) = dblData(1, intIndex)
                    Next intIndex

                End If

            Catch
                intDataCount = -1
            End Try

            If intDataCount <= 0 Then
                ReDim dblMZList(-1)
                ReDim dblIntensityList(-1)
            End If

            Return intDataCount

        End Function

        Public Overrides Function OpenRawFile(ByVal FileName As String) As Boolean
            Dim intResult As Integer
            Dim blnSuccess As Boolean

            Try

                ' Make sure any existing open files are closed
                CloseRawFile()

                If mXRawFile Is Nothing Then
                    mXRawFile = New XRAWFILE2Lib.XRawfile
                End If

                mXRawFile.Open(FileName)
                mXRawFile.IsError(intResult)        ' Unfortunately, .IsError() always returns 0, even if an error occurred

                If intResult = 0 Then
                    mCachedFileName = FileName
                    If FillFileInfo() Then
                        With mFileInfo
                            If .ScanStart = 0 And .ScanEnd = 0 And .VersionNumber = 0 And .MassResolution = 0 And .InstModel = Nothing Then
                                ' File actually didn't load correctly, since these shouldn't all be blank
                                blnSuccess = False
                            Else
                                blnSuccess = True
                            End If
                        End With
                    Else
                        blnSuccess = False
                    End If
                Else
                    blnSuccess = False
                End If

            Catch ex As System.Exception
                blnSuccess = False
            Finally
                If Not blnSuccess Then
                    mCachedFileName = String.Empty
                End If
            End Try

            Return blnSuccess

        End Function

        Public Sub New()
            CloseRawFile()
        End Sub

    End Class
End Namespace
