Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified August 27, 2013

Imports PNNLOmics.Utilities
Imports System.IO
Imports UIMFLibrary

Public Class clsUIMFInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const UIMF_FILE_EXTENSION As String = ".UIMF"

    Private Sub ComputeQualityScores(
       ByRef objUIMFReader As DataReader,
       ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType,
       ByRef dctMasterFrameList As Dictionary(Of Integer, DataReader.FrameType),
       ByRef intMasterFrameNumList() As Integer)

        ' This function is used to determine one or more overall quality scores

        Dim objFrameParams As FrameParams
        Dim objGlobalParams As GlobalParams

        Dim intFrameStart As Integer
        Dim intFrameEnd As Integer

        Dim intGlobalMaxBins As Integer

        Dim dblMZList() As Double
        Dim intIntensityList() As Integer

        Dim sngOverallScore As Single

        Dim dblIntensitySum As Double
        Dim dblOverallAvgIntensitySum As Double
        Dim intOverallAvgCount As Integer

        dblOverallAvgIntensitySum = 0
        intOverallAvgCount = 0

        If mLCMS2DPlot.ScanCountCached > 0 Then
            ' Obtain the overall average intensity value using the data cached in mLCMS2DPlot
            ' This avoids having to reload all of the data using objUIMFReader
            Const intMSLevelFilter As Integer = 1
            sngOverallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(intMSLevelFilter)
        Else

            objGlobalParams = objUIMFReader.GetGlobalParams()

            intGlobalMaxBins = objGlobalParams.Bins


            ReDim dblMZList(intGlobalMaxBins)
            ReDim intIntensityList(intGlobalMaxBins)

            ' Call .GetStartAndEndScans to get the start and end Frames
            MyBase.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd)

            For intMasterFrameNumIndex As Integer = 0 To intMasterFrameNumList.Length - 1

                Dim intFrameNumber As Integer
                Dim eFrameType As DataReader.FrameType
                intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex)
                eFrameType = dctMasterFrameList(intFrameNumber)


                ' Check whether the frame number is within the desired range
                If intFrameNumber < intFrameStart OrElse intFrameNumber > intFrameEnd Then
                    Continue For
                End If

                Try
                    objFrameParams = objUIMFReader.GetFrameParams(intFrameNumber)
                Catch ex As Exception
                    Console.WriteLine("Exception obtaining frame parameters for frame " & intFrameNumber & "; will skip this frame")
                    objFrameParams = Nothing
                End Try

                If objFrameParams Is Nothing Then
                    Continue For
                End If

                Dim intIonCount As Integer
                Dim intTargetIndex As Integer

                ' We have to clear the m/z and intensity arrays before calling GetSpectrum

                Array.Clear(dblMZList, 0, dblMZList.Length)
                Array.Clear(intIntensityList, 0, intIntensityList.Length)

                ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                ' Scans likely range from 0 to objFrameParams.Scans-1, but we'll use objFrameParams.Scans just to be safe
                intIonCount = objUIMFReader.GetSpectrum(intFrameNumber, intFrameNumber, eFrameType, 0, objFrameParams.Scans, dblMZList, intIntensityList)

                If intIonCount <= 0 Then
                    Continue For
                End If

                ' The m/z and intensity arrays might contain entries with m/z values of 0; 
                ' need to copy the data in place to get the data in the correct format.

                If intIonCount > dblMZList.Length Then
                    intIonCount = dblMZList.Length
                End If

                intTargetIndex = 0
                For intIonIndex As Integer = 0 To intIonCount - 1
                    If dblMZList(intIonIndex) > 0 Then
                        If intTargetIndex <> intIonIndex Then
                            dblMZList(intTargetIndex) = dblMZList(intIonIndex)
                            intIntensityList(intTargetIndex) = intIntensityList(intIonIndex)
                        End If
                        intTargetIndex += 1
                    End If
                Next

                intIonCount = intTargetIndex

                If intIonCount > 0 Then

                    ' ToDo: Analyze dblIonMZ and dblIonIntensity to compute a quality scores
                    ' Keep track of the quality scores and then store one or more overall quality scores in udtFileInfo.OverallQualityScore
                    ' For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                    dblIntensitySum = 0
                    For intIonIndex As Integer = 0 To intIonCount - 1
                        dblIntensitySum += intIntensityList(intIonIndex)
                    Next intIonIndex

                    dblOverallAvgIntensitySum += dblIntensitySum / intIonCount

                    intOverallAvgCount += 1

                End If

            Next

            If intOverallAvgCount > 0 Then
                sngOverallScore = CSng(dblOverallAvgIntensitySum / intOverallAvgCount)
            Else
                sngOverallScore = 0
            End If

        End If

        udtFileInfo.OverallQualityScore = sngOverallScore

    End Sub

    Private Sub ConstructTICandBPI(
      ByRef objUIMFReader As DataReader,
       intFrameStart As Integer,
       intFrameEnd As Integer,
      ByRef dctTIC As Dictionary(Of Integer, Double),
      ByRef dctBPI As Dictionary(Of Integer, Double))

        Try
            ' Obtain the TIC and BPI for each MS frame

            Console.WriteLine("  Loading TIC values")
            dctTIC = objUIMFReader.GetTICByFrame(intFrameStart, intFrameEnd, 0, 0)

            Console.WriteLine("  Loading BPI values")
            dctBPI = objUIMFReader.GetBPIByFrame(intFrameStart, intFrameEnd, 0, 0)

        Catch ex As Exception
            ReportError("Error obtaining TIC and BPI for overall dataset: " & ex.Message)
        End Try

    End Sub

    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        ' The dataset name is simply the file name without .UIMF
        Try
            Return Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As Exception
            Return String.Empty
        End Try
    End Function

    Private Sub LoadFrameDetails(
     ByRef objUIMFReader As DataReader,
     ByRef dctMasterFrameList As Dictionary(Of Integer, DataReader.FrameType),
     ByRef intMasterFrameNumList As Integer())

        Const BAD_TIC_OR_BPI As Integer = Integer.MinValue

        Dim dtLastProgressTime As DateTime

        Dim dctTIC As Dictionary(Of Integer, Double) = New Dictionary(Of Integer, Double)()
        Dim dctBPI As Dictionary(Of Integer, Double) = New Dictionary(Of Integer, Double)()

        Dim intFrameStart As Integer
        Dim intFrameEnd As Integer

        ' The StartTime value for each frame is the number of minutes since 12:00 am
        ' If acquiring data from 11:59 pm through 12:00 am, then the StartTime will reset to zero
        Dim dblFrameStartTimeInitial As Double
        Dim dblFrameStartTimeAddon As Double

        Dim dblFrameStartTimePrevious As Double
        Dim dblFrameStartTimeCurrent As Double

        Dim dblElutionTime As Double
        Dim intNonZeroPointsInFrame As Integer

        Dim intGlobalMaxBins As Integer

        Dim dblMZList() As Double
        Dim intIntensityList() As Integer
        Dim dblIonsIntensity() As Double

        Dim dblPressure As Double

        If mSaveTICAndBPI Then
            ' Initialize the TIC and BPI arrays
            MyBase.InitializeTICAndBPI()
            mTICandBPIPlot.BPIXAxisLabel = "Frame number"
            mTICandBPIPlot.TICXAxisLabel = "Frame number"

            mInstrumentSpecificPlots.BPIXAxisLabel = "Frame number"
            mInstrumentSpecificPlots.TICXAxisLabel = "Frame number"

            mInstrumentSpecificPlots.TICYAxisLabel = "Pressure"
            mInstrumentSpecificPlots.TICYAxisExponentialNotation = False

            mInstrumentSpecificPlots.TICPlotAbbrev = "Pressure"
            mInstrumentSpecificPlots.TICAutoMinMaxY = True
            mInstrumentSpecificPlots.RemoveZeroesFromEnds = True
        End If

        If mSaveLCMS2DPlots Then
            MyBase.InitializeLCMS2DPlot()
        End If

        dtLastProgressTime = DateTime.UtcNow

        Dim objGlobalParams = objUIMFReader.GetGlobalParams()

        intGlobalMaxBins = objGlobalParams.Bins

        ReDim dblMZList(intGlobalMaxBins)
        ReDim intIntensityList(intGlobalMaxBins)
        ReDim dblIonsIntensity(intGlobalMaxBins)

        ' Call .GetStartAndEndScans to get the start and end Frames
        MyBase.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd)

        ' Construct the TIC and BPI (of all frames)
        ConstructTICandBPI(objUIMFReader, intFrameStart, intFrameEnd, dctTIC, dctBPI)

        Console.Write("  Loading frame details")

        ' Initialize the frame starttime variables
        dblFrameStartTimeInitial = -1
        dblFrameStartTimeAddon = 0

        dblFrameStartTimePrevious = -1
        dblFrameStartTimeCurrent = 0

        For intMasterFrameNumIndex As Integer = 0 To intMasterFrameNumList.Length - 1

            Dim intFrameNumber As Integer
            Dim eFrameType As DataReader.FrameType
            intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex)
            eFrameType = dctMasterFrameList(intFrameNumber)
            Dim intMSLevel = 1

            ' Check whether the frame number is within the desired range
            If intFrameNumber < intFrameStart OrElse intFrameNumber > intFrameEnd Then
                Continue For
            End If

            Try

                Dim objFrameParams As FrameParams

                Try
                    objFrameParams = objUIMFReader.GetFrameParams(intFrameNumber)
                Catch ex As Exception
                    Console.WriteLine("Exception obtaining frame parameters for frame " & intFrameNumber & "; will skip this frame")
                    objFrameParams = Nothing
                End Try

                If objFrameParams Is Nothing OrElse eFrameType = DataReader.FrameType.Calibration Then
                    Continue For
                End If

                intNonZeroPointsInFrame = objUIMFReader.GetCountPerFrame(intFrameNumber)

                If objFrameParams.FrameType = DataReader.FrameType.MS2 Then
                    intMSLevel = 2
                Else
                    intMSLevel = 1
                End If

                ' Read the frame StartTime
                ' This will be zero in older .UIMF files, or in files converted from Agilent .D folders
                ' In newer files, it is the number of minutes since 12:00 am
                dblFrameStartTimeCurrent = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes)
                If intMasterFrameNumIndex = 0 OrElse dblFrameStartTimeInitial < -0.9 Then
                    dblFrameStartTimeInitial = dblFrameStartTimeCurrent
                End If

                If dblFrameStartTimePrevious > 1400 AndAlso dblFrameStartTimePrevious > dblFrameStartTimeCurrent Then
                    ' We likely rolled over midnight; bump up dblFrameStartTimeAddon by 1440 minutes
                    dblFrameStartTimeAddon += 60 * 24
                End If

                ' Compute the elution time (in minutes) of this frame                    
                dblElutionTime = dblFrameStartTimeCurrent + dblFrameStartTimeAddon - dblFrameStartTimeInitial

                Dim dblTIC As Double
                Dim dblBPI As Double

                If Not dctBPI.TryGetValue(intFrameNumber, dblBPI) Then
                    dblBPI = BAD_TIC_OR_BPI
                End If

                If Not dctTIC.TryGetValue(intFrameNumber, dblTIC) Then
                    dblTIC = BAD_TIC_OR_BPI
                End If

                If mSaveTICAndBPI Then

                    If dblTIC > BAD_TIC_OR_BPI AndAlso dblTIC > BAD_TIC_OR_BPI Then
                        mTICandBPIPlot.AddData(intFrameNumber, intMSLevel, CSng(dblElutionTime), dblBPI, dblTIC)
                    End If

                    dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.PressureBack)
                    If Math.Abs(dblPressure) < Single.Epsilon Then dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.RearIonFunnelPressure)
                    If Math.Abs(dblPressure) < Single.Epsilon Then dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.IonFunnelTrapPressure)
                    If Math.Abs(dblPressure) < Single.Epsilon Then dblPressure = objFrameParams.GetValueDouble(FrameParamKeyType.PressureFront)

                    mInstrumentSpecificPlots.AddDataTICOnly(intFrameNumber, intMSLevel, CSng(dblElutionTime), dblPressure)
                End If


                Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                objScanStatsEntry.ScanNumber = intFrameNumber
                objScanStatsEntry.ScanType = intMSLevel

                If intMSLevel <= 1 Then
                    objScanStatsEntry.ScanTypeName = "HMS"
                Else
                    objScanStatsEntry.ScanTypeName = "HMSn"
                End If

                objScanStatsEntry.ScanFilterText = ""

                objScanStatsEntry.ElutionTime = dblElutionTime.ToString("0.0000")
                If dblTIC > BAD_TIC_OR_BPI Then
                    objScanStatsEntry.TotalIonIntensity = MathUtilities.ValueToString(dblTIC, 5)
                Else
                    objScanStatsEntry.TotalIonIntensity = "0"
                End If

                If dblBPI > BAD_TIC_OR_BPI Then
                    objScanStatsEntry.BasePeakIntensity = MathUtilities.ValueToString(dblBPI, 5)
                Else
                    objScanStatsEntry.BasePeakIntensity = "0"
                End If

                objScanStatsEntry.BasePeakMZ = "0"

                ' Base peak signal to noise ratio
                objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                objScanStatsEntry.IonCount = intNonZeroPointsInFrame
                objScanStatsEntry.IonCountRaw = intNonZeroPointsInFrame

                mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


                If mSaveLCMS2DPlots Or mCheckCentroidingStatus Then
                    Try
                        ' Also need to load the raw data

                        Dim intIonCount As Integer
                        Dim intTargetIndex As Integer

                        ' We have to clear the m/z and intensity arrays before calling GetSpectrum

                        Array.Clear(dblMZList, 0, dblMZList.Length)
                        Array.Clear(intIntensityList, 0, intIntensityList.Length)

                        ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame

                        ' In UIMF files from IMS04, if Frame_Parameters.Scans = 360 then Frame_Scans will have scans 0 through 359
                        ' In UIMF files from IMS08, prior to December 1, 2014, if Frame_Parameters.Scans = 374 then Frame_Scans will have scans 0 through 373
                        ' in UIMF files from IMS08, after December 1, 2014     if Frame_Parameters.Scans = 374 then Frame_Scans will have scans 1 through 374

                        intIonCount = objUIMFReader.GetSpectrum(intFrameNumber, intFrameNumber, eFrameType, 0, objFrameParams.Scans, dblMZList, intIntensityList)

                        If intIonCount > 0 Then
                            ' The m/z and intensity arrays might contain entries with m/z values of 0; 
                            ' need to copy the data in place to get the data in the correct format.
                            ' In addition, we'll copy the intensity values from intIntensityList() into dblIonsIntensity()

                            If intIonCount > dblMZList.Length Then
                                intIonCount = dblMZList.Length
                            End If

                            If dblIonsIntensity.Length < intIonCount Then
                                ReDim Preserve dblIonsIntensity(intIonCount - 1)
                            End If

                            intTargetIndex = 0
                            For intIonIndex As Integer = 0 To intIonCount - 1
                                If dblMZList(intIonIndex) > 0 Then
                                    dblMZList(intTargetIndex) = dblMZList(intIonIndex)
                                    dblIonsIntensity(intTargetIndex) = intIntensityList(intIonIndex)
                                    intTargetIndex += 1
                                End If
                            Next

                            intIonCount = intTargetIndex

                            If intIonCount > 0 Then
                                If dblIonsIntensity.Length > intIonCount Then
                                    ReDim Preserve dblIonsIntensity(intIonCount - 1)
                                End If

                                If mSaveLCMS2DPlots Then
                                    mLCMS2DPlot.AddScan(intFrameNumber, intMSLevel, CSng(dblElutionTime), intIonCount, dblMZList, dblIonsIntensity)
                                End If

                                If mCheckCentroidingStatus Then
                                    mDatasetStatsSummarizer.ClassifySpectrum(intIonCount, dblMZList, intMSLevel)
                                End If
                            End If
                        End If

                    Catch ex As Exception
                        ReportError("Error loading m/z and intensity values for frame " & intFrameNumber & ": " & ex.Message)
                    End Try
                End If

            Catch ex As Exception
                ReportError("Error loading header info for frame " & intFrameNumber & ": " & ex.Message)
            End Try

            ShowProgress(intMasterFrameNumIndex, intMasterFrameNumList.Length, dtLastProgressTime)

            dblFrameStartTimePrevious = dblFrameStartTimeCurrent

        Next intMasterFrameNumIndex

        Console.WriteLine()

    End Sub

    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim objUIMFReader As DataReader = Nothing
        Dim objGlobalParams As GlobalParams = Nothing
        Dim objFrameParams As FrameParams

        Dim intDatasetID As Integer
        Dim intIndex As Integer

        Dim blnReadError As Boolean
        Dim blnInaccurateStartTime As Boolean

        Dim intMasterFrameNumList As Integer()
        ReDim intMasterFrameNumList(0)

        ' Obtain the full path to the file
        Dim fiFileInfo = New FileInfo(strDataFilePath)

        If Not fiFileInfo.Exists Then
            Return False
        End If

        ' Future, optional: Determine the DatasetID
        ' Unfortunately, this is not present in metadata.txt
        ' intDatasetID = LookupDatasetID(strDatasetName)
        intDatasetID = 0

        With udtFileInfo
            .FileSystemCreationTime = fiFileInfo.CreationTime
            .FileSystemModificationTime = fiFileInfo.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = intDatasetID
            .DatasetName = GetDatasetNameViaPath(fiFileInfo.Name)
            .FileExtension = fiFileInfo.Extension
            .FileSizeBytes = fiFileInfo.Length

            .ScanCount = 0
        End With

        mDatasetStatsSummarizer.ClearCachedData()

        blnReadError = False
        blnInaccurateStartTime = False

        Try
            ' Use the UIMFLibrary to read the .UIMF file
            objUIMFReader = New DataReader(fiFileInfo.FullName)
        Catch ex As Exception
            ' File open failed
            ReportError("Call to .OpenUIMF failed for " & fiFileInfo.Name & ": " & ex.Message)
            blnReadError = True
        End Try

        If Not blnReadError Then
            Try
                ' First obtain the global parameters
                objGlobalParams = objUIMFReader.GetGlobalParams()
            Catch ex As Exception
                ' Read error
                blnReadError = True
            End Try
        End If

        If Not blnReadError Then
            ' Read the file info

            Dim dctMasterFrameList As Dictionary(Of Integer, DataReader.FrameType)
            dctMasterFrameList = New Dictionary(Of Integer, DataReader.FrameType)

            Try

                ' Construct a master list of frame numbers and frame types
                dctMasterFrameList = objUIMFReader.GetMasterFrameList()

                If dctMasterFrameList.Count > 0 Then

                    ' Copy the frame numbers into an array so that we can assure it's sorted
                    ReDim intMasterFrameNumList(dctMasterFrameList.Keys.Count - 1)
                    dctMasterFrameList.Keys.CopyTo(intMasterFrameNumList, 0)

                    Array.Sort(intMasterFrameNumList)
                End If

                ' Extract the acquisition time information
                ' The Global_Parameters table records the start time of the entire dataset in field DateStarted
                ' The Frame_Parameters table records the start time of reach frame in field StartTime

                ' The DateStarted column in the Global_Parameters table should be represented by one of these values
                '   A text-based date, like "5/2/2011 4:26:59 PM"; example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse.uimf
                '   A text-based date (no time info), like "Thursday, January 13, 2011"; example: QC_Shew_11_01_pt5_c2_030311_earth_4ms_0001
                '   A tick-based date, like 129272890050787740 (number of ticks since January 1, 1601); example: BATs_TS_01_c4_Eagle_10-02-06_0000

                ' The StartTime column in the Frame_Parameters table should be represented by one of these values
                '   Integer between 0 and 1440 representing number of minutes since midnight (can loop from 1439.9 to 0); example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse.uimf
                '   Integer between 0 and 60 representing number of minutes since past the current hour (can loop from 59.9 to 0); example: BATs_TS_01_c4_Eagle_10-02-06_0000.uimf
                '   A tick-based date, like 634305349108974800 (number of ticks since January 1, 0001); example: QC_Shew_11_01_pt5_c2_030311_earth_4ms_0001
                '   A negative number representing number of minutes from the start of the run in UTC time to the start of the current frame, in local time; example: Sarc_P08_G03_0747_7Dec11_Cheetah_11-09-05.uimf
                '      Examine values: Frame 1 has StartTime = -479.993 and Frame 1177 has StartTime = -417.509
                '   A positive integer representing number of minutes since the start of the run
                '      Theoretically, this will be the case for IMS_TOF_4 acquired after 12/14/2011

                Dim dblStartTime As Double
                Dim dblEndTime As Double
                Dim dblRunTime As Double

                ' First examine objGlobalParams.DateStarted
                Try
                    Dim strReportedDateStarted As String
                    Dim dtReportedDateStarted As DateTime

                    Dim blnValidStartTime As Boolean

                    blnValidStartTime = False
                    strReportedDateStarted = objGlobalParams.GetValue(GlobalParamKeyType.DateStarted)

                    If Not DateTime.TryParse(strReportedDateStarted, dtReportedDateStarted) Then
                        ' Invalid date; log a message
                        ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " & strReportedDateStarted & "; will use the time the datafile was last modified")
                        blnInaccurateStartTime = True
                    Else
                        If dtReportedDateStarted.Year < 450 Then
                            ' Some .UIMF files have DateStarted values represented by huge integers, e.g. 127805472000000000 or 129145004045937500; example: BATs_TS_01_c4_Eagle_10-02-06_0000
                            ' These numbers are the number of ticks since 1 January 1601 (where each tick is 100 ns)
                            ' This value is returned by function GetSystemTimeAsFileTime (see http://en.wikipedia.org/wiki/System_time)

                            ' When SQLite parses these numbers, it converts them to years around 0410
                            ' To get the correct year, simply add 1600

                            dtReportedDateStarted = dtReportedDateStarted.AddYears(1600)
                            blnValidStartTime = True

                        ElseIf dtReportedDateStarted.Year < 2000 Or dtReportedDateStarted.Year > DateTime.Now.Year + 1 Then
                            ' Invalid date; log a message
                            ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " & dtReportedDateStarted.ToString & "; will use the time the datafile was last modified")
                            blnInaccurateStartTime = True

                        Else
                            blnValidStartTime = True
                        End If
                    End If


                    If blnValidStartTime Then
                        udtFileInfo.AcqTimeStart = dtReportedDateStarted

                        ' Update the end time to match the start time; we'll update it below using the start/end times obtained from the frame parameters
                        udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart
                    End If

                Catch ex2 As Exception
                    ShowMessage("Exception extracting the DateStarted date from table Global_Parameters in the .UIMF file: " & ex2.Message)
                End Try

                ' NumFrames is the total number of frames in the file (for all frame types)
                udtFileInfo.ScanCount = objGlobalParams.NumFrames
                If intMasterFrameNumList.Length > udtFileInfo.ScanCount Then
                    udtFileInfo.ScanCount = intMasterFrameNumList.Length
                End If

                If intMasterFrameNumList.Length > 0 Then

                    ' Ideally, we would lookup the acquisition time of the first frame and the last frame, then subtract the two times to determine the run time
                    ' However, given the odd values that can be present in the StartTime field, we need to construct a full list of start times and then parse it

                    ' Get the start time of the first frame
                    ' See above for the various numbers that could be stored in the StartTime column
                    objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList(0))
                    dblStartTime = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes)

                    ' Get the start time of the last frame
                    ' If the reported start time is zero, then step back until a non-zero start time is reported

                    intIndex = intMasterFrameNumList.Length - 1
                    Do
                        objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList(intIndex))
                        dblEndTime = objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes)

                        If Math.Abs(dblEndTime) < Single.Epsilon Then
                            intIndex -= 1
                        End If
                    Loop While Math.Abs(dblEndTime) < Single.Epsilon AndAlso intIndex >= 0

                    ' Check whether the StartTime and EndTime values are based on ticks
                    If dblStartTime >= 1.0E+17 And dblEndTime > 1.0E+17 Then
                        ' StartTime and Endtime were stored as the number of ticks (where each tick is 100 ns)
                        ' Tick start date is either 1 January 1601 or 1 January 0001

                        Dim dtRunTime As DateTime
                        dtRunTime = DateTime.MinValue.AddTicks(CLng(dblEndTime - dblStartTime))

                        dblRunTime = dtRunTime.Subtract(DateTime.MinValue).TotalMinutes

                        ' In some .UIMF files, the DateStarted column in Global_Parameters is simply the date, and not a specific time of day
                        ' If that's the case, then update udtFileInfo.AcqTimeStart to be based on dblRunTime
                        If udtFileInfo.AcqTimeStart.Date = udtFileInfo.AcqTimeStart Then
                            Dim dtReportedDateStarted As DateTime
                            dtReportedDateStarted = DateTime.MinValue.AddTicks(CLng(dblStartTime))

                            If dtReportedDateStarted.Year < 500 Then
                                dtReportedDateStarted = dtReportedDateStarted.AddYears(1600)
                            End If

                            If dtReportedDateStarted.Year >= 2000 And dtReportedDateStarted.Year <= DateTime.Now.Year + 1 Then
                                ' Date looks valid
                                If blnInaccurateStartTime Then
                                    udtFileInfo.AcqTimeStart = dtReportedDateStarted
                                    udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart
                                Else
                                    ' How does it compare to udtFileInfo.AcqTimeStart?
                                    If dtReportedDateStarted.Subtract(udtFileInfo.AcqTimeStart).TotalHours < 24 Then
                                        ' Update the date
                                        udtFileInfo.AcqTimeStart = dtReportedDateStarted
                                        udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart
                                    End If
                                End If
                            End If
                        End If
                    Else
                        ' Ideally, we'd just compute RunTime like this: dblRunTime = dblEndTime - dblStartTime
                        ' But, given the idiosyncracies that can occur, we need to construct a full list of start times

                        Dim lstStartTimes As List(Of Double) = New List(Of Double)
                        Dim dblEndTimeAddon As Double = 0

                        For intIndex = 0 To intMasterFrameNumList.Length - 1
                            objFrameParams = objUIMFReader.GetFrameParams(intMasterFrameNumList(intIndex))
                            lstStartTimes.Add(objFrameParams.GetValueDouble(FrameParamKeyType.StartTimeMinutes))
                        Next intIndex

                        ' Some datasets erroneously have zeroes stored in the .UIMF file for the StartTime of the last two frames; example: Sarc_MS2_26_2Apr11_Cheetah_11-02-18_inverse
                        ' Check for this and remove them
                        Dim intFrameCountRemoved As Integer = 0
                        Do While (Math.Abs(lstStartTimes(lstStartTimes.Count - 1)) < Single.Epsilon)
                            lstStartTimes.RemoveAt(lstStartTimes.Count - 1)
                            intFrameCountRemoved += 1
                            If lstStartTimes.Count = 0 Then Exit Do
                        Loop

                        If intFrameCountRemoved > 0 Then
                            If lstStartTimes.Count > 2 Then
                                ' Compute the amount of time (in minutes) to addon to the total run time
                                ' We're computing the time between two frames, and multiplying that by intFrameCountRemoved
                                dblEndTimeAddon += intFrameCountRemoved * (lstStartTimes(lstStartTimes.Count - 1) - lstStartTimes(lstStartTimes.Count - 2))
                            End If
                        End If

                        ' Now check for the StartTime changing to a smaller number from one frame to the next
                        ' This could happen if the StartTime changed from 1439 to 0 as the system clock hits midnight
                        ' Or if the StartTime changes from 59.9 to 0 as the system clock hits the top of a new hour
                        For intIndex = 1 To lstStartTimes.Count - 1
                            If lstStartTimes(intIndex) < lstStartTimes(intIndex - 1) Then
                                If lstStartTimes(intIndex - 1) > 1439 Then
                                    dblEndTimeAddon += 1440
                                ElseIf lstStartTimes(intIndex - 1) > 59.7 Then
                                    dblEndTimeAddon += 60
                                End If
                            End If
                        Next intIndex

                        If lstStartTimes.Count > 0 Then
                            ' Compute the runtime
                            ' Luckily, even if dblStartTime is -479.993 and dblEntTime is -417.509, this works out to a positive, accurate runtime
                            dblEndTime = lstStartTimes(lstStartTimes.Count - 1)
                            dblRunTime = dblEndTime + dblEndTimeAddon - dblStartTime
                        End If

                    End If

                Else
                    dblRunTime = 0
                End If

                If dblRunTime > 0 Then
                    If dblRunTime > 24000 Then
                        ShowMessage("Invalid runtime computed using the StartTime value from the first and last frames: " & dblRunTime)
                    Else
                        If blnInaccurateStartTime Then
                            udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblRunTime)
                        Else
                            udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblRunTime)
                        End If
                    End If
                End If

            Catch ex As Exception
                ShowMessage("Exception extracting acquisition time information: " & ex.Message)
            End Try

            If mSaveTICAndBPI OrElse mCreateDatasetInfoFile OrElse mCreateScanStatsFile OrElse mSaveLCMS2DPlots Then
                ' Load data from each frame
                ' This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                LoadFrameDetails(objUIMFReader, dctMasterFrameList, intMasterFrameNumList)
            End If

            If mComputeOverallQualityScores Then
                ' Note that this call will also create the TICs and BPIs
                ComputeQualityScores(objUIMFReader, udtFileInfo, dctMasterFrameList, intMasterFrameNumList)
            End If
        End If

        If Not objUIMFReader Is Nothing Then
            ' Close the handle to the data file
            objUIMFReader.Dispose()
        End If


        ' Read the file info from the file system
        ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
        UpdateDatasetFileStats(fiFileInfo, intDatasetID)

        ' Copy over the updated filetime info from udtFileInfo to mDatasetFileInfo
        With mDatasetStatsSummarizer.DatasetFileInfo
            .FileSystemCreationTime = udtFileInfo.FileSystemCreationTime
            .FileSystemModificationTime = udtFileInfo.FileSystemModificationTime
            .DatasetID = udtFileInfo.DatasetID
            .DatasetName = String.Copy(udtFileInfo.DatasetName)
            .FileExtension = String.Copy(udtFileInfo.FileExtension)
            .AcqTimeStart = udtFileInfo.AcqTimeStart
            .AcqTimeEnd = udtFileInfo.AcqTimeEnd
            .ScanCount = udtFileInfo.ScanCount
            .FileSizeBytes = udtFileInfo.FileSizeBytes
        End With

        Return Not blnReadError

    End Function

End Class

