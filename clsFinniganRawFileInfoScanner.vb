Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2005
'
' Last modified May 11, 2015

Imports MSFileInfoScanner.DSSummarizer.clsScanStatsEntry
Imports PNNLOmics.Utilities
Imports System.IO
Imports System.Text.RegularExpressions
Imports ThermoRawFileReaderDLL.FinniganFileIO
Imports SpectraTypeClassifier

Public Class clsFinniganRawFileInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const FINNIGAN_RAW_FILE_EXTENSION As String = ".RAW"

    ''' <summary>
    ''' This function is used to determine one or more overall quality scores
    ''' </summary>
    ''' <param name="objXcaliburAccessor"></param>
    ''' <param name="udtFileInfo"></param>
    ''' <remarks></remarks>
    Protected Sub ComputeQualityScores(objXcaliburAccessor As XRawFileIO, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType)

        Dim intScanCount As Integer
        Dim intScanNumber As Integer
        Dim intIonIndex As Integer
        Dim intReturnCode As Integer

        Dim sngOverallScore As Single

        Dim dblMassIntensityPairs(,) As Double = Nothing

        Dim dblIntensitySum As Double
        Dim dblOverallAvgIntensitySum As Double
        Dim intOverallAvgCount As Integer

        Dim intScanStart As Integer
        Dim intScanEnd As Integer

        dblOverallAvgIntensitySum = 0
        intOverallAvgCount = 0

        If mLCMS2DPlot.ScanCountCached > 0 Then
            ' Obtain the overall average intensity value using the data cached in mLCMS2DPlot
            ' This avoids having to reload all of the data using objXcaliburAccessor
            Const intMSLevelFilter = 1
            sngOverallScore = mLCMS2DPlot.ComputeAverageIntensityAllScans(intMSLevelFilter)
        Else

            intScanCount = objXcaliburAccessor.GetNumScans
            MyBase.GetStartAndEndScans(intScanCount, intScanStart, intScanEnd)

            For intScanNumber = intScanStart To intScanEnd
                ' This function returns the number of points in dblMassIntensityPairs()
                intReturnCode = objXcaliburAccessor.GetScanData2D(intScanNumber, dblMassIntensityPairs)

                If intReturnCode > 0 Then

                    If Not dblMassIntensityPairs Is Nothing AndAlso dblMassIntensityPairs.GetLength(1) > 0 Then
                        ' Keep track of the quality scores and then store one or more overall quality scores in udtFileInfo.OverallQualityScore
                        ' For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                        dblIntensitySum = 0
                        For intIonIndex = 0 To dblMassIntensityPairs.GetUpperBound(1)
                            dblIntensitySum += dblMassIntensityPairs(1, intIonIndex)
                        Next intIonIndex

                        dblOverallAvgIntensitySum += dblIntensitySum / dblMassIntensityPairs.GetLength(1)

                        intOverallAvgCount += 1
                    End If
                End If
            Next intScanNumber

            If intOverallAvgCount > 0 Then
                sngOverallScore = CSng(dblOverallAvgIntensitySum / intOverallAvgCount)
            Else
                sngOverallScore = 0
            End If

        End If

        udtFileInfo.OverallQualityScore = sngOverallScore

    End Sub

    Protected Function GetCentroidStatus(
       intScanNumber As Integer,
       udtScanHeaderInfoType As FinniganFileReaderBaseClass.udtScanHeaderInfoType) As clsSpectrumTypeClassifier.eCentroidStatusConstants

        Static reIsCentroid As New Regex("([FI]TMS [+-] c .+)|([FI]TMS {[^ ]+} +[+-] c .+)|(^ *[+-] c .+)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
        Static reIsProfileM As New Regex("([FI]TMS [+-] p .+)|([FI]TMS {[^ ]+} +[+-] p .+)|(^ *[+-] p .+)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)


        If udtScanHeaderInfoType.IsCentroidScan Then
            If reIsProfileM.IsMatch(udtScanHeaderInfoType.FilterText) Then
                ShowMessage("Warning: Scan " & intScanNumber & " appears to be profile mode data, yet XRawFileIO reported it to be centroid")
            End If

            Return clsSpectrumTypeClassifier.eCentroidStatusConstants.Centroid
        Else
            If reIsCentroid.IsMatch(udtScanHeaderInfoType.FilterText) Then
                ShowMessage("Warning: Scan " & intScanNumber & " appears to be centroided data, yet XRawFileIO reported it to be profile")
            End If

            Return clsSpectrumTypeClassifier.eCentroidStatusConstants.Profile
        End If

    End Function

    ''' <summary>
    ''' Returns the dataset name for the given file
    ''' </summary>
    ''' <param name="strDataFilePath"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        Try
            ' The dataset name is simply the file name without .Raw
            Return Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As Exception
            Return String.Empty
        End Try
    End Function

    Protected Sub LoadScanDetails(objXcaliburAccessor As XRawFileIO)

        Dim intScanCount As Integer
        Dim intScanNumber As Integer

        Dim udtScanHeaderInfo = New FinniganFileReaderBaseClass.udtScanHeaderInfoType
        Dim blnSuccess As Boolean

        Dim intScanStart As Integer
        Dim intScanEnd As Integer

        Console.Write("  Loading scan details")

        If mSaveTICAndBPI Then
            ' Initialize the TIC and BPI arrays
            MyBase.InitializeTICAndBPI()
        End If

        If mSaveLCMS2DPlots Then
            MyBase.InitializeLCMS2DPlot()
        End If

        Dim dtLastProgressTime = DateTime.UtcNow

        intScanCount = objXcaliburAccessor.GetNumScans
        MyBase.GetStartAndEndScans(intScanCount, intScanStart, intScanEnd)

        For intScanNumber = intScanStart To intScanEnd
            Try

                If mShowDebugInfo Then
                    Console.WriteLine(" ... scan " & intScanNumber)
                End If

                blnSuccess = objXcaliburAccessor.GetScanInfo(intScanNumber, udtScanHeaderInfo)

                If blnSuccess Then
                    If mSaveTICAndBPI Then
                        With udtScanHeaderInfo
                            mTICandBPIPlot.AddData(intScanNumber, .MSLevel, CSng(.RetentionTime), .BasePeakIntensity, .TotalIonCurrent)
                        End With
                    End If

                    Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                    With udtScanHeaderInfo
                        objScanStatsEntry.ScanNumber = intScanNumber
                        objScanStatsEntry.ScanType = .MSLevel

                        objScanStatsEntry.ScanTypeName = XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(.FilterText)
                        objScanStatsEntry.ScanFilterText = XRawFileIO.MakeGenericFinniganScanFilter(.FilterText)

                        objScanStatsEntry.ElutionTime = .RetentionTime.ToString("0.0000")
                        objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(.TotalIonCurrent, 5)
                        objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(.BasePeakIntensity, 5)
                        objScanStatsEntry.BasePeakMZ = Math.Round(.BasePeakMZ, 4).ToString

                        ' Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                        objScanStatsEntry.IonCount = .NumPeaks
                        objScanStatsEntry.IonCountRaw = .NumPeaks

                        ' Store the ScanEvent values in .ExtendedScanInfo
                        StoreExtendedScanInfo(objScanStatsEntry.ExtendedScanInfo, udtScanHeaderInfo.ScanEventNames, udtScanHeaderInfo.ScanEventValues)

                        ' Store the collision mode and the scan filter text
                        objScanStatsEntry.ExtendedScanInfo.CollisionMode = udtScanHeaderInfo.CollisionMode
                        objScanStatsEntry.ExtendedScanInfo.ScanFilterText = udtScanHeaderInfo.FilterText

                    End With
                    mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)

                End If
            Catch ex As Exception
                ReportError("Error loading header info for scan " & intScanNumber & ": " & ex.Message)
            End Try

            Try

                If mSaveLCMS2DPlots Or mCheckCentroidingStatus Then
                    ' Also need to load the raw data

                    Dim intIonCount As Integer
                    Dim dblMassIntensityPairs(,) As Double = Nothing

                    ' Load the ions for this scan
                    intIonCount = objXcaliburAccessor.GetScanData2D(intScanNumber, dblMassIntensityPairs)

                    If intIonCount > 0 Then
                        If mSaveLCMS2DPlots Then
                            mLCMS2DPlot.AddScan2D(intScanNumber, udtScanHeaderInfo.MSLevel, CSng(udtScanHeaderInfo.RetentionTime), intIonCount, dblMassIntensityPairs)
                        End If

                        If mCheckCentroidingStatus Then
                            Dim mzCount As Integer = dblMassIntensityPairs.GetLength(1)

                            Dim lstMZs = New List(Of Double)(mzCount)

                            For i As Integer = 0 To mzCount - 1
                                lstMZs.Add(dblMassIntensityPairs(0, i))
                            Next

                            Dim centroidingStatus As clsSpectrumTypeClassifier.eCentroidStatusConstants = GetCentroidStatus(intScanNumber, udtScanHeaderInfo)

                            mDatasetStatsSummarizer.ClassifySpectrum(lstMZs, udtScanHeaderInfo.MSLevel, centroidingStatus)
                        End If
                    End If

                End If

            Catch ex As Exception
                ReportError("Error loading m/z and intensity values for scan " & intScanNumber & ": " & ex.Message)
            End Try

            ShowProgress(intScanNumber, intScanCount, dtLastProgressTime)

        Next intScanNumber

        Console.WriteLine()

    End Sub

    ''' <summary>
    ''' Process the dataset
    ''' </summary>
    ''' <param name="strDataFilePath"></param>
    ''' <param name="udtFileInfo"></param>
    ''' <returns>True if success, False if an error</returns>
    ''' <remarks></remarks>
    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim objXcaliburAccessor As XRawFileIO
        Dim udtScanHeaderInfo = New XRawFileIO.udtScanHeaderInfoType

        Dim intScanEnd As Integer

        Dim strDataFilePathLocal As String = String.Empty

        Dim blnReadError As Boolean
        Dim blnDeleteLocalFile As Boolean

        ' Obtain the full path to the file
        Dim fiRawFile = New FileInfo(strDataFilePath)

        If Not fiRawFile.Exists Then
            ShowMessage(".Raw file not found: " + strDataFilePath)
            Return False
        End If

        ' Future, optional: Determine the DatasetID
        ' Unfortunately, this is not present in metadata.txt
        ' intDatasetID = LookupDatasetID(strDatasetName)
        Dim intDatasetID As Integer = MyBase.DatasetID

        ' Record the file size and Dataset ID
        With udtFileInfo
            .FileSystemCreationTime = fiRawFile.CreationTime
            .FileSystemModificationTime = fiRawFile.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = intDatasetID
            .DatasetName = GetDatasetNameViaPath(fiRawFile.Name)
            .FileExtension = fiRawFile.Extension
            .FileSizeBytes = fiRawFile.Length

            .ScanCount = 0
        End With

        mDatasetStatsSummarizer.ClearCachedData()

        blnDeleteLocalFile = False
        blnReadError = False

        ' Use Xraw to read the .Raw file
        ' If reading from a SAMBA-mounted network share, and if the current user has 
        '  Read privileges but not Read&Execute privileges, then we will need to copy the file locally
        objXcaliburAccessor = New XRawFileIO

        ' Open a handle to the data file
        If Not objXcaliburAccessor.OpenRawFile(fiRawFile.FullName) Then
            ' File open failed
            ReportError("Call to .OpenRawFile failed for: " & fiRawFile.FullName)
            blnReadError = True

            If clsMSFileInfoScanner.GetAppFolderPath.Substring(0, 2).ToLower <> fiRawFile.FullName.Substring(0, 2).ToLower Then

                If mCopyFileLocalOnReadError Then
                    ' Copy the file locally and try again

                    Try
                        strDataFilePathLocal = Path.Combine(clsMSFileInfoScanner.GetAppFolderPath, Path.GetFileName(strDataFilePath))

                        If strDataFilePathLocal.ToLower <> strDataFilePath.ToLower Then

                            ShowMessage("Copying file " & Path.GetFileName(strDataFilePath) & " to the working folder")
                            File.Copy(strDataFilePath, strDataFilePathLocal, True)

                            strDataFilePath = String.Copy(strDataFilePathLocal)
                            blnDeleteLocalFile = True

                            ' Update fiRawFile then try to re-open
                            fiRawFile = New FileInfo(strDataFilePath)

                            If Not objXcaliburAccessor.OpenRawFile(fiRawFile.FullName) Then
                                ' File open failed
                                ReportError("Call to .OpenRawFile failed for: " & fiRawFile.FullName)
                                blnReadError = True
                            Else
                                blnReadError = False
                            End If
                        End If
                    Catch ex As Exception
                        blnReadError = True
                    End Try
                End If

            End If

        End If

        If Not blnReadError Then
            ' Read the file info
            Try
                udtFileInfo.AcqTimeStart = objXcaliburAccessor.FileInfo.CreationDate
            Catch ex As Exception
                ' Read error
                blnReadError = True
            End Try

            If Not blnReadError Then
                Try
                    ' Look up the end scan time then compute .AcqTimeEnd
                    intScanEnd = objXcaliburAccessor.FileInfo.ScanEnd
                    objXcaliburAccessor.GetScanInfo(intScanEnd, udtScanHeaderInfo)

                    With udtFileInfo
                        .AcqTimeEnd = .AcqTimeStart.AddMinutes(udtScanHeaderInfo.RetentionTime)
                        .ScanCount = objXcaliburAccessor.GetNumScans()
                    End With
                Catch ex As Exception
                    ' Error; use default values
                    With udtFileInfo
                        .AcqTimeEnd = .AcqTimeStart
                        .ScanCount = 0
                    End With
                End Try

                If mSaveTICAndBPI OrElse mCreateDatasetInfoFile OrElse mCreateScanStatsFile OrElse mSaveLCMS2DPlots OrElse mCheckCentroidingStatus Then
                    ' Load data from each scan
                    ' This is used to create the TIC and BPI plot, the 2D LC/MS plot, and/or to create the Dataset Info File
                    LoadScanDetails(objXcaliburAccessor)
                End If

                If mComputeOverallQualityScores Then
                    ' Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(objXcaliburAccessor, udtFileInfo)
                End If
            End If
        End If


        With mDatasetStatsSummarizer.SampleInfo
            .SampleName = objXcaliburAccessor.FileInfo.SampleName
            .Comment1 = objXcaliburAccessor.FileInfo.Comment1
            .Comment2 = objXcaliburAccessor.FileInfo.Comment2

            If Not String.IsNullOrEmpty(objXcaliburAccessor.FileInfo.SampleComment) Then
                If String.IsNullOrEmpty(.Comment1) Then
                    .Comment1 = objXcaliburAccessor.FileInfo.SampleComment
                Else
                    If String.IsNullOrEmpty(.Comment2) Then
                        .Comment2 = objXcaliburAccessor.FileInfo.SampleComment
                    Else
                        ' Append the sample comment to comment 2
                        .Comment2 &= "; " & objXcaliburAccessor.FileInfo.SampleComment
                    End If
                End If
            End If

        End With

        ' Close the handle to the data file
        objXcaliburAccessor.CloseRawFile()

        ' Read the file info from the file system
        ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
        UpdateDatasetFileStats(fiRawFile, intDatasetID)

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

        ' Delete the local copy of the data file
        If blnDeleteLocalFile Then
            Try
                File.Delete(strDataFilePathLocal)
            Catch ex As Exception
                ' Deletion failed
                ReportError("Deletion failed for: " & Path.GetFileName(strDataFilePathLocal))
            End Try
        End If

        Return Not blnReadError

    End Function

    Protected Sub StoreExtendedScanInfo(ByRef udtExtendedScanInfo As udtExtendedStatsInfoType, strEntryName As String, strEntryValue As String)

        If strEntryValue Is Nothing Then
            strEntryValue = String.Empty
        End If

        ''Dim strEntryNames(0) As String
        ''Dim strEntryValues(0) As String

        ''strEntryNames(0) = String.Copy(strEntryName)
        ''strEntryValues(0) = String.Copy(strEntryValue)

        ''StoreExtendedScanInfo(htExtendedScanInfo, strEntryNames, strEntryValues)

        ' This command is equivalent to the above series of commands
        ' It converts strEntryName to an array and strEntryValue to a separate array and passes those arrays to StoreExtendedScanInfo()
        StoreExtendedScanInfo(udtExtendedScanInfo, New String() {strEntryName}, New String() {strEntryValue})

    End Sub

    Protected Sub StoreExtendedScanInfo(ByRef udtExtendedScanInfo As udtExtendedStatsInfoType, strEntryNames() As String, strEntryValues() As String)

        Dim cTrimChars = New Char() {":"c, " "c}
        Dim intIndex As Integer

        Try
            If Not (strEntryNames Is Nothing OrElse strEntryValues Is Nothing) Then

                For intIndex = 0 To strEntryNames.Length - 1
                    If strEntryNames(intIndex) Is Nothing OrElse strEntryNames(intIndex).Trim.Length = 0 Then
                        ' Empty entry name; do not add
                    Else
                        ' We're only storing certain entries from strEntryNames
                        Select Case strEntryNames(intIndex).ToLower.TrimEnd(cTrimChars)
                            Case SCANSTATS_COL_ION_INJECTION_TIME.ToLower
                                udtExtendedScanInfo.IonInjectionTime = strEntryValues(intIndex)

                            Case SCANSTATS_COL_SCAN_SEGMENT.ToLower
                                udtExtendedScanInfo.ScanSegment = strEntryValues(intIndex)

                            Case SCANSTATS_COL_SCAN_EVENT.ToLower
                                udtExtendedScanInfo.ScanEvent = strEntryValues(intIndex)

                            Case SCANSTATS_COL_CHARGE_STATE.ToLower
                                udtExtendedScanInfo.ChargeState = strEntryValues(intIndex)

                            Case SCANSTATS_COL_MONOISOTOPIC_MZ.ToLower
                                udtExtendedScanInfo.MonoisotopicMZ = strEntryValues(intIndex)

                            Case SCANSTATS_COL_COLLISION_MODE.ToLower
                                udtExtendedScanInfo.CollisionMode = strEntryValues(intIndex)

                            Case SCANSTATS_COL_SCAN_FILTER_TEXT.ToLower
                                udtExtendedScanInfo.ScanFilterText = strEntryValues(intIndex)

                        End Select

                    End If
                Next intIndex
            End If
        Catch ex As Exception
            ' Ignore any errors here
        End Try

    End Sub

End Class
