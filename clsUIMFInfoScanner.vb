Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified May 17, 2010

Public Class clsUIMFInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const UIMF_FILE_EXTENSION As String = ".UIMF"

    Private Sub ComputeQualityScores(ByRef objUIMFReader As UIMFLibrary.DataReader, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType)
        ' This function is used to determine one or more overall quality scores

        Dim objGlobalParams As UIMFLibrary.GlobalParameters

        Dim intFrameCount As Integer
        Dim intFrameNumber As Integer
        Dim intFrameType As Integer

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

            objGlobalParams = DirectCast(objUIMFReader.GetGlobalParameters(), UIMFLibrary.GlobalParameters)

            intFrameCount = objGlobalParams.NumFrames
            intGlobalMaxBins = objGlobalParams.Bins

            ReDim dblMZList(intGlobalMaxBins)
            ReDim intIntensityList(intGlobalMaxBins)
            ReDim dblIonsIntensity(intGlobalMaxBins)

            MyBase.GetStartAndEndScans(intFrameCount, intFrameStart, intFrameEnd)

            intFrameType = 0

            For intFrameNumber = intFrameStart To intFrameEnd


                Dim intIonCount As Integer
                Dim intIndex As Integer
                Dim intTargetIndex As Integer

                ' We have to clear the m/z and intensity arrays before calling SumScans

                Array.Clear(dblMZList, 0, dblMZList.Length)
                Array.Clear(intIntensityList, 0, intIntensityList.Length)

                ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                ' Do this using DataReader.SumScans()
                intIonCount = objUIMFReader.SumScans(dblMZList, intIntensityList, intFrameType, intFrameNumber)

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

            Next intFrameNumber

            If intOverallAvgCount > 0 Then
                sngOverallScore = CSng(dblOverallAvgIntensitySum / intOverallAvgCount)
            Else
                sngOverallScore = 0
            End If

        End If

        udtFileInfo.OverallQualityScore = sngOverallScore

    End Sub


    Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
        ' The dataset name is simply the file name without .UIMF
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Private Sub LoadScanDetails(ByRef objUIMFReader As UIMFLibrary.DataReader)

        Dim objGlobalParams As UIMFLibrary.GlobalParameters
        Dim objFrameParams As UIMFLibrary.FrameParameters

        Dim intFrameCount As Integer

        Dim intFrameNumber As Integer

        Dim sngProgress As Single
        Dim dtLastProgressTime As System.DateTime

        Dim dblTIC() As Double
        Dim dblBPI() As Double
        Dim intFrameType As Integer

        Dim intFrameStart As Integer
        Dim intFrameEnd As Integer
        Dim intTICIndex As Integer


        Dim intMSLevel As Integer
        Dim dblElutionTime As Double
        Dim intNonZeroPointsInFrame As Integer

        Dim intGlobalMaxBins As Integer

        Dim dblMZList() As Double
        Dim intIntensityList() As Integer
        Dim dblIonsIntensity() As Double

        Console.Write("  Loading scan details")

        If mSaveTICAndBPI Then
            ' Initialize the TIC and BPI arrays
            MyBase.InitializeTICAndBPI()
        End If

        If mSaveLCMS2DPlots Then
            MyBase.InitializeLCMS2DPlot()
        End If

        dtLastProgressTime = System.DateTime.Now()


        objGlobalParams = DirectCast(objUIMFReader.GetGlobalParameters(), UIMFLibrary.GlobalParameters)

        intFrameCount = objGlobalParams.NumFrames
        intGlobalMaxBins = objGlobalParams.Bins

        ReDim dblMZList(intGlobalMaxBins)
        ReDim intIntensityList(intGlobalMaxBins)
        ReDim dblIonsIntensity(intGlobalMaxBins)

        MyBase.GetStartAndEndScans(intFrameCount, intFrameStart, intFrameEnd)

        Try
            ' Obtain the TIC and BPI for each frame
            ReDim dblTIC(intFrameCount - 1)
            ReDim dblBPI(intFrameCount - 1)

            intFrameType = 0

            objUIMFReader.GetTIC(dblTIC, intFrameType, intFrameStart, intFrameEnd, 0, 0)
            objUIMFReader.GetBPI(dblBPI, intFrameType, intFrameStart, intFrameEnd, 0, 0)

        Catch ex As System.Exception
            ReportError("Error obtaining TIC and BPI for overall dataset: " & ex.Message)
        End Try
      

        intTICIndex = 0
        For intFrameNumber = intFrameStart To intFrameEnd

            intMSLevel = 1

            Try

                objFrameParams = DirectCast(objUIMFReader.GetFrameParameters(intFrameNumber), UIMFLibrary.FrameParameters)

                If Not objFrameParams Is Nothing Then


                    ' For-loop based code:
                    'intNonZeroPointsInFrame = 0
                    'For intScanIndex As Integer = 0 To objFrameParams.Scans - 1
                    '    intNonZeroPointsInFrame += objUIMFReader.GetCountPerSpectrum(intFrameNumber, intScanIndex)
                    'Next

                    ' Single function call-based
                    intNonZeroPointsInFrame = objUIMFReader.GetCountPerFrame(intFrameNumber)

                    If objFrameParams.FrameType <= 1 Then
                        intMSLevel = 1
                    Else
                        intMSLevel = objFrameParams.FrameType
                    End If

                    ' Compute the elution time of this frame
                    dblElutionTime = objFrameParams.StartTime

                    If mSaveTICAndBPI AndAlso intTICIndex < dblTIC.Length Then
                        mTICandBPIPlot.AddData(intFrameNumber, intMSLevel, CSng(dblElutionTime), dblBPI(intTICIndex), dblTIC(intTICIndex))
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

                End If
            Catch ex As System.Exception
                ReportError("Error loading header info for frame " & intFrameNumber & ": " & ex.Message)
            End Try

            Try

                If mSaveLCMS2DPlots Then
                    ' Also need to load the raw data

                    Dim intIonCount As Integer
                    Dim intIndex As Integer
                    Dim intTargetIndex As Integer

                    ' We have to clear the m/z and intensity arrays before calling SumScans

                    Array.Clear(dblMZList, 0, dblMZList.Length)
                    Array.Clear(intIntensityList, 0, intIntensityList.Length)

                    ' Process all of the IMS scans in this Frame to compute a summed spectrum representative of the frame
                    ' Do this using DataReader.SumScans()
                    intIonCount = objUIMFReader.SumScans(dblMZList, intIntensityList, intFrameType, intFrameNumber)

                    If intIonCount > 0 Then
                        ' The m/z and intensity arrays likely contain numerous entries with m/z values of 0; 
                        ' need to copy the data in place to get the data in the correct format.
                        ' In addition, we'll copy the intensity values from intIntensityList() into dblIonsIntensity()

                        intTargetIndex = 0
                        For intIndex = 0 To intIonCount - 1
                            If dblMZList(intIndex) > 0 Then
                                dblMZList(intTargetIndex) = dblMZList(intIndex)
                                dblIonsIntensity(intTargetIndex) = intIntensityList(intIndex)
                                intTargetIndex += 1
                            End If
                        Next

                        intIonCount = intTargetIndex

                        If intIonCount > 0 Then
                            mLCMS2DPlot.AddScan(intFrameNumber, intMSLevel, CSng(dblElutionTime), _
                                                intIonCount, dblMZList, dblIonsIntensity)
                        End If

                    End If

                End If

            Catch ex As System.Exception
                ReportError("Error loading m/z and intensity values for scan " & intFrameNumber & ": " & ex.Message)
            End Try

            If intFrameNumber Mod 100 = 0 Then
                Console.Write(".")

                If intFrameCount > 0 Then
                    sngProgress = CSng(intFrameNumber / intFrameCount * 100)

                    If System.DateTime.Now.Subtract(dtLastProgressTime).TotalSeconds > 30 Then
                        dtLastProgressTime = System.DateTime.Now
                        Console.WriteLine()
                        Console.Write(sngProgress.ToString("0.0") & "% ")
                    End If
                End If

            End If

            intTICIndex += 1
        Next intFrameNumber

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
                objGlobalParams = DirectCast(objUIMFReader.GetGlobalParameters(), UIMFLibrary.GlobalParameters)
            Catch ex As System.Exception
                ' Read error
                blnReadError = True
            End Try

            If Not blnReadError Then

                Try
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
                            End If
                        End If

                    Catch ex2 As System.Exception
                        ShowMessage("Exception extracting the DateStarted date from table Global_Parameters in the .UIMF file: " & ex2.Message)
                    End Try

                    ' Look up the acquisition time of the first frame and the last frame
                    ' Subtract the two times to determine the run time
                    ' Note that frame numbers range from 1 to objGlobalParams.NumFrames

                    objFrameParams = DirectCast(objUIMFReader.GetFrameParameters(1), UIMFLibrary.FrameParameters)
                    dblStartTime = objFrameParams.StartTime

                    udtFileInfo.ScanCount = objGlobalParams.NumFrames
                    objFrameParams = DirectCast(objUIMFReader.GetFrameParameters(udtFileInfo.ScanCount), UIMFLibrary.FrameParameters)
                    dblEndTime = objFrameParams.StartTime

                    dblRunTime = dblEndTime - dblStartTime

                    If dblRunTime > 0 Then
                        If blnInaccurateStartTime Then
                            udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblRunTime)
                        Else
                            udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblRunTime)
                        End If
                    End If

                Catch ex As System.Exception
                    ' Error; use default values
                    With udtFileInfo
                        .AcqTimeEnd = .AcqTimeStart
                        .ScanCount = 0
                    End With
                End Try

                If mSaveTICAndBPI OrElse mCreateDatasetInfoFile OrElse mSaveLCMS2DPlots Then
                    ' Load data from each scan
                    ' This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                    LoadScanDetails(objUIMFReader)
                End If

                If mComputeOverallQualityScores Then
                    ' Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(objUIMFReader, udtFileInfo)
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

End Class
