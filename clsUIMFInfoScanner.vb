Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified April 22, 2011

Public Class clsUIMFInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const UIMF_FILE_EXTENSION As String = ".UIMF"

    Private Structure udtFrameInfoType
        Public FrameType As Integer
        Public FrameIndex As Integer
    End Structure

    Private Sub ComputeQualityScores(ByRef objUIMFReader As UIMFLibrary.DataReader, _
                                     ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, _
                                     ByRef dctMasterFrameList As System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType), _
                                     ByRef intMasterFrameNumList As Integer())

        ' This function is used to determine one or more overall quality scores

        Dim objFrameParams As UIMFLibrary.FrameParameters
        Dim objGlobalParams As UIMFLibrary.GlobalParameters

        Dim eFrameType As UIMFLibrary.DataReader.iFrameType
        Dim intFrameCount As Integer

        Dim intFrameStart As Integer
        Dim intFrameEnd As Integer

        Dim intGlobalMaxBins As Integer

        Dim dblMZList() As Double
        Dim intIntensityList() As Integer
        Dim dblIonsIntensity() As Double

        Dim intIonIndex As Integer

        Dim sngOverallScore As Single

        Dim dblIntensitySum As Double
        Dim dblOverallAvgIntensitySum As Double
        Dim intOverallAvgCount As Integer

        dblOverallAvgIntensitySum = 0
        intOverallAvgCount = 0

        If mLCMS2DPlot.ScanCountCached > 0 Then
            ' Obtain the overall average intensity value using the data cached in mLCMS2DPlot
            ' This avoids having to reload all of the data using objUIMFReader
            Dim intMSLevelFilter As Integer = 1
            sngOverallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(intMSLevelFilter)
        Else

            objGlobalParams = objUIMFReader.GetGlobalParameters()

            intGlobalMaxBins = objGlobalParams.Bins


            ReDim dblMZList(intGlobalMaxBins)
            ReDim intIntensityList(intGlobalMaxBins)
            ReDim dblIonsIntensity(intGlobalMaxBins)

            ' Call .GetStartAndEndScans to get the start and end Frames
            MyBase.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd)

            For intMasterFrameNumIndex As Integer = 0 To intMasterFrameNumList.Length - 1

                Dim intFrameNumber As Integer
                intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex)

                ' Make sure the correct frame type is defined
                objUIMFReader.set_FrameType(dctMasterFrameList(intFrameNumber).FrameType)

                Try
                    objFrameParams = objUIMFReader.GetFrameParameters(dctMasterFrameList(intFrameNumber).FrameIndex)
                Catch ex As System.Exception
                    Console.WriteLine("Exception obtaining frame parameters for frame " & intFrameNumber & "; will skip this frame")
                    objFrameParams = Nothing
                End Try

                If Not objFrameParams Is Nothing Then

                    ' Check whether the frame number is within the desired range
                    If objFrameParams.FrameNum >= intFrameStart And objFrameParams.FrameNum <= intFrameEnd Then

                        Dim intIonCount As Integer
                        Dim intIndex As Integer
                        Dim intTargetIndex As Integer

                        ' We have to clear the m/z and intensity arrays before calling SumScans

                        Array.Clear(dblMZList, 0, dblMZList.Length)
                        Array.Clear(intIntensityList, 0, intIntensityList.Length)

                        ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                        ' Could do this using DataReader.SumScans()
                        'intIonCount = objUIMFReader.SumScans(dblMZList, intIntensityList, intFrameType, intFrameNumber)

                        intIonCount = objUIMFReader.SumScansNonCached(dblMZList, intIntensityList, dctMasterFrameList(intFrameNumber).FrameType, dctMasterFrameList(intFrameNumber).FrameIndex, dctMasterFrameList(intFrameNumber).FrameIndex, 0, objFrameParams.Scans - 1)

                        If intIonCount > 0 Then
                            ' The m/z and intensity arrays likely contain numerous entries with m/z values of 0; 
                            ' need to copy the data in place to get the data in the correct format.
                            ' In addition, we'll copy the intensity values from intIntensityList() into dblIonsIntensity()

                            intTargetIndex = 0
                            For intIndex = 0 To intIonCount - 1
                                If dblMZList(intIndex) > 0 Then
                                    dblMZList(intTargetIndex) = dblMZList(intIndex)
                                    intIntensityList(intTargetIndex) = intIntensityList(intIndex)
                                    intTargetIndex += 1
                                End If
                            Next

                            intIonCount = intTargetIndex

                            If intIonCount > 0 Then

                                ' ToDo: Analyze dblIonMZ and dblIonIntensity to compute a quality scores
                                ' Keep track of the quality scores and then store one or more overall quality scores in udtFileInfo.OverallQualityScore
                                ' For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                                dblIntensitySum = 0
                                For intIonIndex = 0 To intIonCount - 1
                                    dblIntensitySum += intIntensityList(intIonIndex)
                                Next intIonIndex

                                dblOverallAvgIntensitySum += dblIntensitySum / intIonCount

                                intOverallAvgCount += 1

                            End If

                        End If

                    End If

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

    Private Function ConstructMasterFrameList(ByRef objUIMFReader As UIMFLibrary.DataReader) As System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType)

        Dim dctMasterFrameList As System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType)
        dctMasterFrameList = New System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType)

        Dim udtFrameInfo As udtFrameInfoType
        Dim intFrameCount As Integer
        Dim intFrameNumbers() As Integer

        For intFrameType As Integer = 0 To 5
            intFrameCount = objUIMFReader.set_FrameType(intFrameType)
            If intFrameCount > 0 Then
                intFrameNumbers = objUIMFReader.GetFrameNumbers()

                For intIndex As Integer = 0 To intFrameNumbers.Length - 1
                    If Not dctMasterFrameList.ContainsKey(intFrameNumbers(intIndex)) Then
                        udtFrameInfo.FrameType = intFrameType
                        udtFrameInfo.FrameIndex = intIndex
                        dctMasterFrameList.Add(intFrameNumbers(intIndex), udtFrameInfo)
                    End If
                Next
            End If
        Next

        Return dctMasterFrameList

    End Function

    Private Sub ConstructTICandBPI(ByRef objUIMFReader As UIMFLibrary.DataReader, ByVal intFrameStart As Integer, ByVal intFrameEnd As Integer, _
                                   ByRef dblTIC As Double(), ByRef dblBPI As Double())

        Try
            ' Obtain the TIC and BPI for each MS frame

            Dim intMSFrameNumbers() As Integer
            Dim intMSFrameCount As Integer
            Dim eFrameType As UIMFLibrary.DataReader.iFrameType

            ' Select the appropriate frame type
            intMSFrameCount = SelectFirstFrameType(objUIMFReader, eFrameType)

            intMSFrameNumbers = objUIMFReader.GetFrameNumbers()

            Dim intStartFrameIndex As Integer
            Dim intEndFrameIndex As Integer
            intStartFrameIndex = 0
            intEndFrameIndex = intMSFrameNumbers.Length - 1

            ' Update intStartFrameIndex if intFrameStart > 0
            Do While intStartFrameIndex < intEndFrameIndex AndAlso intMSFrameNumbers(intStartFrameIndex) < intFrameStart
                intStartFrameIndex += 1
            Loop

            ' Update intEndFrameIndex if intFrameEnd is < intMSFrameNumbers(intEndFrameIndex)
            Do While intEndFrameIndex > intStartFrameIndex AndAlso intMSFrameNumbers(intEndFrameIndex) > intFrameEnd
                intEndFrameIndex -= 1
            Loop

            objUIMFReader.GetTIC(dblTIC, objUIMFReader.FrameTypeEnumToInt(eFrameType), intStartFrameIndex, intEndFrameIndex, 0, 0)
            objUIMFReader.GetBPI(dblBPI, objUIMFReader.FrameTypeEnumToInt(eFrameType), intStartFrameIndex, intEndFrameIndex, 0, 0)

        Catch ex As System.Exception
            ReportError("Error obtaining TIC and BPI for overall dataset: " & ex.Message)
        End Try

    End Sub

    Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
        ' The dataset name is simply the file name without .UIMF
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Private Sub LoadFrameDetails(ByRef objUIMFReader As UIMFLibrary.DataReader, _
                                 ByRef dctMasterFrameList As System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType), _
                                 ByRef intMasterFrameNumList As Integer())

        Dim objGlobalParams As UIMFLibrary.GlobalParameters
        Dim objFrameParams As UIMFLibrary.FrameParameters

        Dim sngProgress As Single
        Dim dtLastProgressTime As System.DateTime

        Dim dblTIC() As Double
        Dim dblBPI() As Double

        Dim intFrameStart As Integer
        Dim intFrameEnd As Integer
        Dim intTICIndex As Integer

        Dim intMSLevel As Integer

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

        Console.Write("  Loading frame details")

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

        dtLastProgressTime = System.DateTime.Now()

        objGlobalParams = objUIMFReader.GetGlobalParameters()

        intGlobalMaxBins = objGlobalParams.Bins

        ReDim dblMZList(intGlobalMaxBins)
        ReDim intIntensityList(intGlobalMaxBins)
        ReDim dblIonsIntensity(intGlobalMaxBins)

        ' Call .GetStartAndEndScans to get the start and end Frames
        MyBase.GetStartAndEndScans(objGlobalParams.NumFrames, intFrameStart, intFrameEnd)

        If intMasterFrameNumList.Length > 0 Then
            ReDim dblTIC(intMasterFrameNumList.Length - 1)
            ReDim dblBPI(intMasterFrameNumList.Length - 1)
        Else
            ReDim dblTIC(0)
            ReDim dblBPI(0)
        End If

        ' Construct the TIC and BPI
        ConstructTICandBPI(objUIMFReader, intFrameStart, intFrameEnd, dblTIC, dblBPI)

        ' Initialize the frame starttime variables
        dblFrameStartTimeInitial = -1
        dblFrameStartTimeAddon = 0

        dblFrameStartTimePrevious = -1
        dblFrameStartTimeCurrent = 0


        intTICIndex = 0
        For intMasterFrameNumIndex As Integer = 0 To intMasterFrameNumList.Length - 1

            Dim intFrameNumber As Integer
            intFrameNumber = intMasterFrameNumList(intMasterFrameNumIndex)
            intMSLevel = 1

            ' Make sure the correct frame type is defined
            objUIMFReader.set_FrameType(dctMasterFrameList(intFrameNumber).FrameType)

            Try

                Try
                    objFrameParams = objUIMFReader.GetFrameParameters(dctMasterFrameList(intFrameNumber).FrameIndex)
                Catch ex As System.Exception
                    Console.WriteLine("Exception obtaining frame parameters for frame " & intFrameNumber & "; will skip this frame")
                    objFrameParams = Nothing
                End Try

                If Not objFrameParams Is Nothing Then

                    ' Check whether the frame number is within the desired range
                    If objFrameParams.FrameNum >= intFrameStart And objFrameParams.FrameNum <= intFrameEnd Then

                        intNonZeroPointsInFrame = objUIMFReader.GetCountPerFrame(dctMasterFrameList(intFrameNumber).FrameIndex)

                        If objFrameParams.FrameType = 2 Then
                            intMSLevel = 2
                        Else
                            intMSLevel = 1
                        End If

                        ' Read the frame StartTime
                        ' This will be zero in older .UIMF files
                        ' In newer files, it is the number of minutes since 12:00 am
                        dblFrameStartTimeCurrent = objFrameParams.StartTime
                        If intMasterFrameNumIndex = 0 OrElse dblFrameStartTimeInitial = -1 Then
                            dblFrameStartTimeInitial = dblFrameStartTimeCurrent
                        End If

                        If dblFrameStartTimePrevious > 1400 AndAlso dblFrameStartTimePrevious > dblFrameStartTimeCurrent Then
                            ' We likely rolled over midnight; bump up dblFrameStartTimeAddon by 1440 minutes
                            dblFrameStartTimeAddon += 60 * 24
                        End If

                        ' Compute the elution time (in minutes) of this frame                    
                        dblElutionTime = dblFrameStartTimeCurrent + dblFrameStartTimeAddon - dblFrameStartTimeInitial


                        If mSaveTICAndBPI Then
                            If intTICIndex < dblTIC.Length Then
                                mTICandBPIPlot.AddData(objFrameParams.FrameNum, intMSLevel, CSng(dblElutionTime), dblBPI(intTICIndex), dblTIC(intTICIndex))
                            End If

                            dblPressure = objFrameParams.PressureBack
                            If dblPressure = 0 Then dblPressure = objFrameParams.RearIonFunnelPressure
                            If dblPressure = 0 Then dblPressure = objFrameParams.IonFunnelTrapPressure
                            If dblPressure = 0 Then dblPressure = objFrameParams.PressureFront

                            mInstrumentSpecificPlots.AddDataTICOnly(objFrameParams.FrameNum, intMSLevel, CSng(dblElutionTime), dblPressure)
                        End If


                        Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                        objScanStatsEntry.ScanNumber = objFrameParams.FrameNum
                        objScanStatsEntry.ScanType = intMSLevel

                        If intMSLevel <= 1 Then
                            objScanStatsEntry.ScanTypeName = "HMS"
                        Else
                            objScanStatsEntry.ScanTypeName = "HMSn"
                        End If

                        objScanStatsEntry.ScanFilterText = ""

                        objScanStatsEntry.ElutionTime = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblElutionTime, 5)
                        If intTICIndex < dblTIC.Length Then
                            objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblTIC(intTICIndex), 5)
                            objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBPI(intTICIndex), 5)
                        Else
                            objScanStatsEntry.TotalIonIntensity = "0"
                            objScanStatsEntry.BasePeakIntensity = "0"
                        End If

                        objScanStatsEntry.BasePeakMZ = "0"

                        ' Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                        objScanStatsEntry.IonCount = intNonZeroPointsInFrame
                        objScanStatsEntry.IonCountRaw = intNonZeroPointsInFrame

                        mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


                        If mSaveLCMS2DPlots Then
                            Try
                                ' Also need to load the raw data

                                Dim intIonCount As Integer
                                Dim intTargetIndex As Integer

                                ' We have to clear the m/z and intensity arrays before calling SumScans

                                Array.Clear(dblMZList, 0, dblMZList.Length)
                                Array.Clear(intIntensityList, 0, intIntensityList.Length)

                                ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame

                                intIonCount = objUIMFReader.SumScansNonCached(dblMZList, intIntensityList, dctMasterFrameList(intFrameNumber).FrameType, dctMasterFrameList(intFrameNumber).FrameIndex, dctMasterFrameList(intFrameNumber).FrameIndex, 0, objFrameParams.Scans - 1)

                                If intIonCount > 0 Then
                                    ' The m/z and intensity arrays likely contain numerous entries with m/z values of 0; 
                                    ' need to copy the data in place to get the data in the correct format.
                                    ' In addition, we'll copy the intensity values from intIntensityList() into dblIonsIntensity()

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
                                        mLCMS2DPlot.AddScan(objFrameParams.FrameNum, intMSLevel, CSng(dblElutionTime), _
                                                            intIonCount, dblMZList, dblIonsIntensity)
                                    End If

                                End If


                            Catch ex As System.Exception
                                ReportError("Error loading m/z and intensity values for frame " & intFrameNumber & ": " & ex.Message)
                            End Try
                        End If

                    End If
                End If

            Catch ex As System.Exception
                ReportError("Error loading header info for frame " & intFrameNumber & ": " & ex.Message)
            End Try

            If intMasterFrameNumIndex Mod 100 = 0 Then
                Console.Write(".")

                If intMasterFrameNumList.Length > 0 Then
                    sngProgress = CSng(intMasterFrameNumIndex / intMasterFrameNumList.Length * 100)

                    If System.DateTime.Now.Subtract(dtLastProgressTime).TotalSeconds > 30 Then
                        dtLastProgressTime = System.DateTime.Now
                        Console.WriteLine()
                        Console.Write(sngProgress.ToString("0.0") & "% ")
                    End If
                End If

            End If

            dblFrameStartTimePrevious = dblFrameStartTimeCurrent
            intTICIndex += 1

        Next intMasterFrameNumIndex

        Console.WriteLine()

    End Sub

    Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim objUIMFReader As UIMFLibrary.DataReader
        Dim objGlobalParams As UIMFLibrary.GlobalParameters
        Dim objFrameParams As UIMFLibrary.FrameParameters

        Dim ioFileInfo As System.IO.FileInfo

        Dim intDatasetID As Integer

        Dim blnReadError As Boolean
        Dim blnInaccurateStartTime As Boolean

        Dim dctMasterFrameList As System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType)
        Dim intMasterFrameNumList As Integer()

        dctMasterFrameList = New System.Collections.Generic.Dictionary(Of Integer, udtFrameInfoType)
        ReDim intMasterFrameNumList(0)

        ' Obtain the full path to the file
        ioFileInfo = New System.IO.FileInfo(strDataFilePath)

        If Not ioFileInfo.Exists Then
            Return False
        End If

        ' Future, optional: Determine the DatasetID
        ' Unfortunately, this is not present in metadata.txt
        ' intDatasetID = LookupDatasetID(strDatasetName)
        intDatasetID = 0

        ' Record the file size and Dataset ID
        With udtFileInfo
            .FileSystemCreationTime = ioFileInfo.CreationTime
            .FileSystemModificationTime = ioFileInfo.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = intDatasetID
            .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
            .FileExtension = ioFileInfo.Extension
            .FileSizeBytes = ioFileInfo.Length

            .ScanCount = 0
        End With

        mDatasetStatsSummarizer.ClearCachedData()

        blnReadError = False
        blnInaccurateStartTime = False

        ' Use the UIMFLibrary to read the .UIMF file
        objUIMFReader = New UIMFLibrary.DataReader

        ' Open a handle to the data file
        If Not objUIMFReader.OpenUIMF(ioFileInfo.FullName) Then
            ' File open failed
            ReportError("Call to .OpenUIMF failed for: " & ioFileInfo.FullName)
            blnReadError = True
        End If

        If Not blnReadError Then
            ' Read the file info

            Try
                ' First obtain the global parameters
                objGlobalParams = objUIMFReader.GetGlobalParameters()
            Catch ex As System.Exception
                ' Read error
                blnReadError = True
            End Try

            If Not blnReadError Then

                Try

                    ' Construct a master list of frame numbers, frame types, and frame indices
                    dctMasterFrameList = ConstructMasterFrameList(objUIMFReader)

                    If dctMasterFrameList.Count > 0 Then

                        ' Copy the frame numbers into an array so that we can assure it's sorted
                        ReDim intMasterFrameNumList(dctMasterFrameList.Keys.Count - 1)
                        dctMasterFrameList.Keys.CopyTo(intMasterFrameNumList, 0)

                        Array.Sort(intMasterFrameNumList)
                    End If

                    ' Extract the acquisition time information
                    Dim dblStartTime As Double
                    Dim dblEndTime As Double
                    Dim dblRunTime As Double

                    ' First examine objGlobalParams.DateStarted
                    ' Buggy .UIMF files will report odd dates (like year 410) so we need to check for that
                    Try
                        Dim strReportedDateStarted As String
                        Dim dtReportedDateStarted As DateTime
                        strReportedDateStarted = objGlobalParams.DateStarted

                        If Not System.DateTime.TryParse(strReportedDateStarted, dtReportedDateStarted) Then
                            ' Invalid date; log a message
                            ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " & strReportedDateStarted & "; will use the time the datafile was last modified")
                            blnInaccurateStartTime = True
                        Else
                            If dtReportedDateStarted.Year < 2000 Or dtReportedDateStarted.Year > System.DateTime.Now.Year + 1 Then
                                ' Invalid date; log a message
                                ShowMessage(".UIMF file has an invalid DateStarted value in table Global_Parameters: " & dtReportedDateStarted.ToString & "; will use the time the datafile was last modified")
                                blnInaccurateStartTime = True
                            Else
                                udtFileInfo.AcqTimeStart = dtReportedDateStarted

                                ' Update the end time to match the start time; we'll update it below using the start/end times obtained from the frame parameters
                                udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart
                            End If
                        End If

                    Catch ex2 As System.Exception
                        ShowMessage("Exception extracting the DateStarted date from table Global_Parameters in the .UIMF file: " & ex2.Message)
                    End Try

                    ' NumFrames is the total number of frames in the file (for all frame types)
                    udtFileInfo.ScanCount = objGlobalParams.NumFrames
                    If intMasterFrameNumList.Length > udtFileInfo.ScanCount Then
                        udtFileInfo.ScanCount = intMasterFrameNumList.Length
                    End If

                    If intMasterFrameNumList.Length > 0 Then
                        ' Look up the acquisition time of the first frame and the last frame
                        ' Subtract the two times to determine the run time

                        Dim intFrameNumber As Integer

                        ' Get the start time of the first frame
                        intFrameNumber = intMasterFrameNumList(0)
                        objUIMFReader.set_FrameType(dctMasterFrameList(intFrameNumber).FrameType)

                        objFrameParams = objUIMFReader.GetFrameParameters(dctMasterFrameList(intFrameNumber).FrameIndex)
                        dblStartTime = objFrameParams.StartTime

                        ' Get the start time of the last frame
                        intFrameNumber = intMasterFrameNumList(intMasterFrameNumList.Length - 1)
                        objUIMFReader.set_FrameType(dctMasterFrameList(intFrameNumber).FrameType)

                        objFrameParams = objUIMFReader.GetFrameParameters(dctMasterFrameList(intFrameNumber).FrameIndex)
                        dblEndTime = objFrameParams.StartTime

                        ' Run time should be in minutes
                        dblRunTime = dblEndTime - dblStartTime
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

                Catch ex As System.Exception
                    ShowMessage("Exception extracting acquisition time information: " & ex.Message)
                End Try

                If mSaveTICAndBPI OrElse mCreateDatasetInfoFile OrElse mSaveLCMS2DPlots Then
                    ' Load data from each frame
                    ' This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                    LoadFrameDetails(objUIMFReader, dctMasterFrameList, intMasterFrameNumList)
                End If

                If mComputeOverallQualityScores Then
                    ' Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(objUIMFReader, udtFileInfo, dctMasterFrameList, intMasterFrameNumList)
                End If
            End If
        End If

        ' Close the handle to the data file
        objUIMFReader.CloseUIMF(ioFileInfo.FullName)
        objUIMFReader = Nothing


        ' Read the file info from the file system
        ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
        UpdateDatasetFileStats(ioFileInfo, intDatasetID)

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

    Protected Function SelectFirstFrameType(ByRef objUIMFReader As UIMFLibrary.DataReader, _
                                            ByRef eFrameType As UIMFLibrary.DataReader.iFrameType) As Integer

        Dim intFrameCount As Integer

        ' Select the first frame type that actually has data
        eFrameType = objUIMFReader.FrameTypeIntToEnum(0)
        intFrameCount = objUIMFReader.set_FrameType(eFrameType)
        Do While intFrameCount = 0 And eFrameType < UIMFLibrary.DataReader.iFrameType.Calibration
            ' Increment frame type
            eFrameType = objUIMFReader.FrameTypeIntToEnum(objUIMFReader.FrameTypeEnumToInt(eFrameType) + 1)
            intFrameCount = objUIMFReader.set_FrameType(eFrameType)
        Loop

        Return intFrameCount
    End Function
End Class

