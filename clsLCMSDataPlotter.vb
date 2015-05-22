Option Strict On

Imports System.IO
Imports System.Security.Cryptography
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series
Imports PNNLOmics.Data.Features

''' <summary>
''' This class tracks the m/z and intensity values for a series of spectra
''' It can then create a 2D plot of m/z vs. intensity
''' To keep the plot from being too dense, it will filter the data to show at most MaxPointsToPlot data points
''' Furthermore, it will bin the data by MZResolution m/z units (necessary if the data is not centroided)
''' </summary>
''' <remarks></remarks>
Public Class clsLCMSDataPlotter

#Region "Constants, Enums, Structures"
    Private Const MAX_ALLOWABLE_ION_COUNT As Integer = 50000        ' Absolute maximum number of ions that will be tracked for a mass spectrum

    Public Enum eOutputFileTypes
        LCMS = 0
        LCMSMSn = 1
    End Enum

    Protected Structure udtOutputFileInfoType
        Public FileType As eOutputFileTypes
        Public FileName As String
        Public FilePath As String
    End Structure

    Public Structure udtMSIonType
        Public MZ As Double
        Public Intensity As Double
        Public Charge As Byte

        Public Overrides Function ToString() As String
            If Charge > 0 Then
                Return MZ.ToString("0.000") & ", " & Intensity.ToString("0") & ", " & Charge & "+"
            Else
                Return MZ.ToString("0.000") & ", " & Intensity.ToString("0")
            End If
        End Function
    End Structure

#End Region

#Region "Member variables"

    Protected mPointCountCached As Integer              ' Keeps track of the total number of data points cached in mScans
    Protected mPointCountCachedAfterLastTrim As Integer

    Protected mScans As List(Of clsLCMSDataPlotter.clsScanData)

    Protected mOptions As MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions

    Protected mRecentFiles As List(Of udtOutputFileInfoType)

    Public Event ErrorEvent(ByVal Message As String)
#End Region

#Region "Properties"
    Public Property Options() As MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions
        Get
            Return mOptions
        End Get
        Set(ByVal value As MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions)
            mOptions = value
        End Set
    End Property

    Public ReadOnly Property ScanCountCached() As Integer
        Get
            Return mScans.Count
        End Get
    End Property

#End Region

    Public Sub New()
        Me.New(New MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions)
    End Sub

    Public Sub New(ByVal objOptions As MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions)
        mOptions = objOptions
        mRecentFiles = New List(Of udtOutputFileInfoType)
        Reset()
    End Sub

    Protected Sub AddRecentFile(ByVal strFilePath As String, ByVal eFileType As eOutputFileTypes)
        Dim udtOutputFileInfo As udtOutputFileInfoType

        udtOutputFileInfo.FileType = eFileType
        udtOutputFileInfo.FileName = Path.GetFileName(strFilePath)
        udtOutputFileInfo.FilePath = strFilePath

        mRecentFiles.Add(udtOutputFileInfo)
    End Sub

    Public Function AddScan2D(ByVal intScanNumber As Integer,
      ByVal intMSLevel As Integer,
      ByVal sngScanTimeMinutes As Single,
      ByVal intIonCount As Integer,
      ByVal dblMassIntensityPairs(,) As Double) As Boolean

        Static intSortingWarnCount As Integer = 0

        Dim intIndex As Integer
        Dim intIonCountNew As Integer

        Try

            If intIonCount <= 0 Then
                ' No data to add
                Return False
            End If

            ' Make sure the data is sorted by m/z
            For intIndex = 1 To intIonCount - 1
                ' Note that dblMassIntensityPairs(0, intIndex) is m/z
                '       and dblMassIntensityPairs(1, intIndex) is intensity
                If dblMassIntensityPairs(0, intIndex) < dblMassIntensityPairs(0, intIndex - 1) Then
                    ' May need to sort the data
                    ' However, if the intensity of both data points is zero, then we can simply swap the data
                    If Math.Abs(dblMassIntensityPairs(1, intIndex)) < Double.Epsilon AndAlso Math.Abs(dblMassIntensityPairs(1, intIndex - 1)) < Double.Epsilon Then
                        ' Swap the m/z values
                        Dim dblSwapVal As Double = dblMassIntensityPairs(0, intIndex)
                        dblMassIntensityPairs(0, intIndex) = dblMassIntensityPairs(0, intIndex - 1)
                        dblMassIntensityPairs(0, intIndex - 1) = dblSwapVal
                    Else
                        ' Need to sort
                        intSortingWarnCount += 1
                        If intSortingWarnCount <= 10 Then
                            Console.WriteLine("  Sorting m/z data (this typically shouldn't be required for Finnigan data, though can occur for high res orbitrap data)")
                        ElseIf intSortingWarnCount Mod 100 = 0 Then
                            Console.WriteLine("  Sorting m/z data (i = " & intSortingWarnCount & ")")
                        End If

                        ' We can't easily sort a 2D array in .NET
                        ' Thus, we must copy the data into new arrays and then call AddScan()

                        Dim lstIons = New List(Of udtMSIonType)(intIonCount - 1)

                        For intCopyIndex = 0 To intIonCount - 1
                            Dim udtIon = New udtMSIonType

                            udtIon.MZ = dblMassIntensityPairs(0, intCopyIndex)
                            udtIon.Intensity = dblMassIntensityPairs(1, intCopyIndex)

                            lstIons.Add(udtIon)
                        Next

                        Return AddScan(intScanNumber, intMSLevel, sngScanTimeMinutes, lstIons)

                    End If
                End If
            Next


            Dim dblIonsMZFiltered() As Double
            Dim sngIonsIntensityFiltered() As Single
            Dim bytChargeFiltered() As Byte

            ReDim dblIonsMZFiltered(intIonCount - 1)
            ReDim sngIonsIntensityFiltered(intIonCount - 1)
            ReDim bytChargeFiltered(intIonCount - 1)

            ' Populate dblIonsMZFiltered & sngIonsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

            intIonCountNew = 0
            For intIndex = 0 To intIonCount - 1
                If dblMassIntensityPairs(1, intIndex) > 0 AndAlso dblMassIntensityPairs(1, intIndex) >= mOptions.MinIntensity Then
                    dblIonsMZFiltered(intIonCountNew) = dblMassIntensityPairs(0, intIndex)

                    If dblMassIntensityPairs(1, intIndex) > Single.MaxValue Then
                        sngIonsIntensityFiltered(intIonCountNew) = Single.MaxValue
                    Else
                        sngIonsIntensityFiltered(intIonCountNew) = CSng(dblMassIntensityPairs(1, intIndex))
                    End If

                    bytChargeFiltered(intIonCountNew) = 0

                    intIonCountNew += 1
                End If
            Next

            Const useLog = False
            If useLog Then
                For intIndex = 0 To intIonCountNew - 1
                    If sngIonsIntensityFiltered(intIndex) > 0 Then
                        sngIonsIntensityFiltered(intIndex) = CSng(Math.Log10(sngIonsIntensityFiltered(intIndex)))
                    End If
                Next
            End If

            AddScanCheckData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCountNew, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered)


        Catch ex As Exception
            RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.AddScan2D: " & ex.Message & "; inner exception: " & ex.InnerException.Message)
            Return False
        End Try

        Return True

    End Function

    Public Function AddScan(
      ByVal intScanNumber As Integer,
      ByVal intMSLevel As Integer,
      ByVal sngScanTimeMinutes As Single,
      ByVal intIonCount As Integer,
      ByVal dblIonsMZ() As Double,
      ByVal dblIonsIntensity() As Double) As Boolean

        Dim lstIons As List(Of udtMSIonType)

        If intIonCount > MAX_ALLOWABLE_ION_COUNT Then
            Array.Sort(dblIonsIntensity, dblIonsMZ)

            Dim lstHighIntensityIons = New List(Of udtMSIonType)(MAX_ALLOWABLE_ION_COUNT)

            For intIndex As Integer = intIonCount - MAX_ALLOWABLE_ION_COUNT To intIonCount - 1
                Dim udtIon = New udtMSIonType

                udtIon.MZ = dblIonsMZ(intIndex)
                udtIon.Intensity = dblIonsIntensity(intIndex)

                lstHighIntensityIons.Add(udtIon)
            Next

            lstIons = (From item In lstHighIntensityIons Select item Order By item.MZ).ToList

        Else
            lstIons = New List(Of udtMSIonType)(intIonCount - 1)

            For intIndex As Integer = 0 To intIonCount - 1
                Dim udtIon = New udtMSIonType

                udtIon.MZ = dblIonsMZ(intIndex)
                udtIon.Intensity = dblIonsIntensity(intIndex)

                lstIons.Add(udtIon)
            Next
        End If

        Return AddScan(intScanNumber, intMSLevel, sngScanTimeMinutes, lstIons)

    End Function

    Public Function AddScan(
      ByVal intScanNumber As Integer,
      ByVal intMSLevel As Integer,
      ByVal sngScanTimeMinutes As Single,
      ByVal lstIons As List(Of udtMSIonType)) As Boolean

        Static intSortingWarnCount As Integer = 0

        Dim intIndex As Integer
        Dim intIonCountNew As Integer

        Try

            If lstIons.Count = 0 Then
                ' No data to add
                Return False
            End If

            ' Make sure the data is sorted by m/z
            For intIndex = 1 To lstIons.Count - 1
                If lstIons(intIndex).MZ < lstIons(intIndex - 1).MZ Then
                    ' May need to sort the data
                    ' However, if the intensity of both data points is zero, then we can simply swap the data
                    If Math.Abs(lstIons(intIndex).Intensity - 0) < Double.Epsilon AndAlso Math.Abs(lstIons(intIndex - 1).Intensity - 0) < Double.Epsilon Then
                        ' Swap the m/z values
                        Dim udtSwapVal As udtMSIonType = lstIons(intIndex)
                        lstIons(intIndex) = lstIons(intIndex - 1)
                        lstIons(intIndex - 1) = udtSwapVal
                    Else
                        ' Need to sort
                        intSortingWarnCount += 1
                        If intSortingWarnCount <= 10 Then
                            Console.WriteLine("  Sorting m/z data (this typically shouldn't be required for Finnigan data, though can occur for high res orbitrap data)")
                        ElseIf intSortingWarnCount Mod 100 = 0 Then
                            Console.WriteLine("  Sorting m/z data (i = " & intSortingWarnCount & ")")
                        End If
                        lstIons.Sort(New udtMSIonTypeComparer)
                        Exit For
                    End If
                End If
            Next


            Dim dblIonsMZFiltered() As Double
            Dim sngIonsIntensityFiltered() As Single
            Dim bytCharge() As Byte

            ReDim dblIonsMZFiltered(lstIons.Count - 1)
            ReDim sngIonsIntensityFiltered(lstIons.Count - 1)
            ReDim bytCharge(lstIons.Count - 1)

            ' Populate dblIonsMZFiltered & sngIonsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

            intIonCountNew = 0
            For intIndex = 0 To lstIons.Count - 1
                If lstIons(intIndex).Intensity > 0 AndAlso lstIons(intIndex).Intensity >= mOptions.MinIntensity Then
                    dblIonsMZFiltered(intIonCountNew) = lstIons(intIndex).MZ

                    If lstIons(intIndex).Intensity > Single.MaxValue Then
                        sngIonsIntensityFiltered(intIonCountNew) = Single.MaxValue
                    Else
                        sngIonsIntensityFiltered(intIonCountNew) = CSng(lstIons(intIndex).Intensity)
                    End If

                    bytCharge(intIonCountNew) = lstIons(intIndex).Charge

                    intIonCountNew += 1
                End If
            Next

            AddScanCheckData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCountNew, dblIonsMZFiltered, sngIonsIntensityFiltered, bytCharge)

        Catch ex As Exception
            RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.AddScan: " & ex.Message & "; inner exception: " & ex.InnerException.Message)
            Return False
        End Try

        Return True

    End Function

    Private Sub AddScanCheckData(
      ByVal intScanNumber As Integer,
      ByVal intMSLevel As Integer,
      ByVal sngScanTimeMinutes As Single,
      ByVal intIonCount As Integer,
      ByVal dblIonsMZFiltered As Double(),
      ByVal sngIonsIntensityFiltered As Single(),
      ByVal bytChargeFiltered As Byte())

        Static intSpectraFoundExceedingMaxIonCount As Integer = 0
        Static intMaxIonCountReported As Integer = 0

        Dim intMaxAllowableIonCount As Integer
        Dim blnCentroidRequired As Boolean
        Dim intIndex As Integer

        ' Check whether any of the data points is less than mOptions.MZResolution m/z units apart
        blnCentroidRequired = False
        For intIndex = 0 To intIonCount - 2
            If dblIonsMZFiltered(intIndex + 1) - dblIonsMZFiltered(intIndex) < mOptions.MZResolution Then
                blnCentroidRequired = True
                Exit For
            End If
        Next

        If blnCentroidRequired Then
            ' Consolidate any points closer than mOptions.MZResolution m/z units
            CentroidMSData(mOptions.MZResolution, intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered)
        End If

        ' Instantiate a new ScanData object for this scan
        Dim objScanData = New clsScanData(intScanNumber, intMSLevel, sngScanTimeMinutes, intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered, bytChargeFiltered)

        intMaxAllowableIonCount = MAX_ALLOWABLE_ION_COUNT
        If objScanData.IonCount > intMaxAllowableIonCount Then
            ' Do not keep more than 50,000 ions
            intSpectraFoundExceedingMaxIonCount += 1

            ' Display a message at the console the first 10 times we encounter spectra with over intMaxAllowableIonCount ions
            ' In addition, display a new message every time a new max value is encountered
            If intSpectraFoundExceedingMaxIonCount <= 10 OrElse objScanData.IonCount > intMaxIonCountReported Then
                Console.WriteLine()
                Console.WriteLine("Note: Scan " & intScanNumber & " has " & objScanData.IonCount & " ions; will only retain " & intMaxAllowableIonCount & " (trimmed " & intSpectraFoundExceedingMaxIonCount.ToString & " spectra)")

                intMaxIonCountReported = objScanData.IonCount
            End If

            DiscardDataToLimitIonCount(objScanData, 0, 0, intMaxAllowableIonCount)
        End If

        mScans.Add(objScanData)
        mPointCountCached += objScanData.IonCount

        If mPointCountCached > mOptions.MaxPointsToPlot * 5 Then
            ' Too many data points are being tracked; trim out the low abundance ones

            ' However, only repeat the trim if the number of cached data points has increased by 10%
            ' This helps speed up program execution by avoiding trimming data after every new scan is added
            If mPointCountCached > mPointCountCachedAfterLastTrim * 1.1 Then

                ' Step through the scans and reduce the number of points in memory
                TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

            End If
        End If

    End Sub

    Public Function AddScanSkipFilters(ByRef objSourceData As clsScanData) As Boolean

        Dim blnSuccess As Boolean
        Dim objScanData As clsScanData

        Try
            If objSourceData Is Nothing OrElse objSourceData.IonCount <= 0 Then
                ' No data to add
                Return False
            End If

            ' Copy the data in objSourceScan
            objScanData = New clsLCMSDataPlotter.clsScanData(
              objSourceData.ScanNumber,
              objSourceData.MSLevel,
              objSourceData.ScanTimeMinutes,
              objSourceData.IonCount,
              objSourceData.IonsMZ,
              objSourceData.IonsIntensity,
              objSourceData.Charge)

            mScans.Add(objScanData)
            mPointCountCached += objScanData.IonCount

            If mPointCountCached > mOptions.MaxPointsToPlot * 5 Then
                ' Too many data points are being tracked; trim out the low abundance ones

                ' However, only repeat the trim if the number of cached data points has increased by 10%
                ' This helps speed up program execution by avoiding trimming data after every new scan is added
                If mPointCountCached > mPointCountCachedAfterLastTrim * 1.1 Then

                    ' Step through the scans and reduce the number of points in memory
                    TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

                End If
            End If

        Catch ex As Exception
            RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.AddScanSkipFilters: " & ex.Message & "; inner exception: " & ex.InnerException.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Sub ClearRecentFileInfo()
        mRecentFiles.Clear()
    End Sub

    Public Function ComputeAverageIntensityAllScans(ByVal intMSLevelFilter As Integer) As Single

        Dim intScanIndex As Integer
        Dim intIonIndex As Integer

        Dim intDataCount As Integer
        Dim dblIntensitySum As Double

        If intMSLevelFilter > 0 Then
            ValidateMSLevel()
        End If

        If mPointCountCached > mOptions.MaxPointsToPlot Then
            ' Need to step through the scans and reduce the number of points in memory

            ' Note that the number of data points remaining after calling this function may still be
            '  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum 
            '  (see TrimCachedData for more details)

            TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

        End If


        intDataCount = 0
        dblIntensitySum = 0

        For intScanIndex = 0 To mScans.Count - 1
            If intMSLevelFilter = 0 OrElse mScans(intScanIndex).MSLevel = intMSLevelFilter Then

                With mScans(intScanIndex)
                    For intIonIndex = 0 To .IonCount - 1
                        dblIntensitySum += .IonsIntensity(intIonIndex)
                        intDataCount += 1
                    Next
                End With
            End If
        Next intScanIndex

        If intDataCount > 0 Then
            Return CSng(dblIntensitySum / intDataCount)
        Else
            Return 0
        End If
    End Function

    Private Sub CentroidMSData(
      ByVal sngMZResolution As Single,
      ByRef intIonCount As Integer,
      ByRef dblIonsMZ() As Double,
      ByRef sngIonsIntensity() As Single,
      ByRef bytChargeFiltered() As Byte)

        Dim sngIntensitySorted() As Single
        Dim intPointerArray() As Integer

        Dim intIndex As Integer
        Dim intIndexAdjacent As Integer
        Dim intPointerIndex As Integer
        Dim intIonCountNew As Integer

        If sngMZResolution <= 0 Then
            ' Nothing to do
            Exit Sub
        End If

        Try
            ReDim sngIntensitySorted(intIonCount - 1)
            ReDim intPointerArray(intIonCount - 1)

            For intIndex = 0 To intIonCount - 1
                If sngIonsIntensity(intIndex) < 0 Then
                    ' Do not allow for negative intensities; change it to 0
                    sngIonsIntensity(intIndex) = 0
                End If
                sngIntensitySorted(intIndex) = sngIonsIntensity(intIndex)
                intPointerArray(intIndex) = intIndex
            Next

            ' Sort by ascending intensity
            Array.Sort(sngIntensitySorted, intPointerArray)

            ' Now process the data from the highest intensity to the lowest intensity
            ' As each data point is processed, we will either: 
            '  a) set its intensity to the negative of the actual intensity to mark it as being processed
            '  b) set its intensity to Single.MinValue (-3.40282347E+38) if the point is to be removed
            '     because it is within sngMZResolution m/z units of a point with a higher intensity

            intPointerIndex = intIonCount - 1
            Do While intPointerIndex >= 0

                intIndex = intPointerArray(intPointerIndex)
                If sngIonsIntensity(intIndex) > 0 Then

                    ' This point has not yet been processed

                    ' Examine adjacent data points to the left (lower m/z)
                    intIndexAdjacent = intIndex - 1
                    Do While intIndexAdjacent >= 0
                        If dblIonsMZ(intIndex) - dblIonsMZ(intIndexAdjacent) < sngMZResolution Then
                            ' Mark this data point for removal since it is too close to the point at intIndex
                            If sngIonsIntensity(intIndexAdjacent) > 0 Then
                                sngIonsIntensity(intIndexAdjacent) = Single.MinValue
                            End If
                        Else
                            Exit Do
                        End If
                        intIndexAdjacent -= 1
                    Loop

                    ' Examine adjacent data points to the right (higher m/z)
                    intIndexAdjacent = intIndex + 1
                    Do While intIndexAdjacent < intIonCount
                        If dblIonsMZ(intIndexAdjacent) - dblIonsMZ(intIndex) < sngMZResolution Then
                            ' Mark this data point for removal since it is too close to the point at intIndex
                            If sngIonsIntensity(intIndexAdjacent) > 0 Then
                                sngIonsIntensity(intIndexAdjacent) = Single.MinValue
                            End If
                        Else
                            Exit Do
                        End If
                        intIndexAdjacent += 1
                    Loop

                    sngIonsIntensity(intIndex) = -sngIonsIntensity(intIndex)
                End If
                intPointerIndex -= 1
            Loop

            ' Now consolidate the data by copying in place
            intIonCountNew = 0
            For intIndex = 0 To intIonCount - 1
                If sngIonsIntensity(intIndex) > Single.MinValue Then
                    ' Keep this point; need to flip the intensity back to being positive
                    dblIonsMZ(intIonCountNew) = dblIonsMZ(intIndex)
                    sngIonsIntensity(intIonCountNew) = -sngIonsIntensity(intIndex)
                    bytChargeFiltered(intIonCountNew) = bytChargeFiltered(intIndex)
                    intIonCountNew += 1
                End If
            Next intIndex
            intIonCount = intIonCountNew

        Catch ex As Exception
            RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.CentroidMSData: " & ex.Message)
        End Try

    End Sub

    Private Sub DiscardDataToLimitIonCount(ByRef objMSSpectrum As clsScanData,
      ByVal dblMZIgnoreRangeStart As Double,
      ByVal dblMZIgnoreRangeEnd As Double,
      ByVal intMaxIonCountToRetain As Integer)

        Dim intIonCountNew As Integer
        Dim intIonIndex As Integer
        Dim blnMZIgnoreRangleEnabled As Boolean
        Dim blnPointPassesFilter As Boolean

        Dim objFilterDataArray As clsFilterDataArrayMaxCount

        ' When this is true, then will write a text file of the mass spectrum before before and after it is filtered
        ' Used for debugging
        Dim blnWriteDebugData As Boolean
        Dim swOutFile As StreamWriter = Nothing

        Try
            If dblMZIgnoreRangeStart > 0 Or dblMZIgnoreRangeEnd > 0 Then
                blnMZIgnoreRangleEnabled = True
            Else
                blnMZIgnoreRangleEnabled = False
            End If


            With objMSSpectrum

                If objMSSpectrum.IonCount > intMaxIonCountToRetain Then
                    objFilterDataArray = New clsFilterDataArrayMaxCount(objMSSpectrum.IonCount)

                    objFilterDataArray.MaximumDataCountToLoad = intMaxIonCountToRetain
                    objFilterDataArray.TotalIntensityPercentageFilterEnabled = False

                    blnWriteDebugData = False
                    If blnWriteDebugData Then
                        swOutFile = New StreamWriter(New FileStream("DataDump_" & objMSSpectrum.ScanNumber.ToString & "_BeforeFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read))
                        swOutFile.WriteLine("m/z" & ControlChars.Tab & "Intensity")
                    End If

                    ' Store the intensity values in objFilterDataArray
                    For intIonIndex = 0 To .IonCount - 1
                        objFilterDataArray.AddDataPoint(.IonsIntensity(intIonIndex), intIonIndex)
                        If blnWriteDebugData Then
                            swOutFile.WriteLine(.IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
                        End If
                    Next

                    If blnWriteDebugData Then
                        swOutFile.Close()
                    End If


                    ' Call .FilterData, which will determine which data points to keep
                    objFilterDataArray.FilterData()

                    intIonCountNew = 0
                    For intIonIndex = 0 To .IonCount - 1

                        If blnMZIgnoreRangleEnabled Then
                            If .IonsMZ(intIonIndex) <= dblMZIgnoreRangeEnd AndAlso .IonsMZ(intIonIndex) >= dblMZIgnoreRangeStart Then
                                ' The m/z value is between dblMZIgnoreRangeStart and dblMZIgnoreRangeEnd
                                ' Keep this point
                                blnPointPassesFilter = True
                            Else
                                blnPointPassesFilter = False
                            End If
                        Else
                            blnPointPassesFilter = False
                        End If

                        If Not blnPointPassesFilter Then
                            ' See if the point's intensity is negative
                            If objFilterDataArray.GetAbundanceByIndex(intIonIndex) >= 0 Then
                                blnPointPassesFilter = True
                            End If
                        End If

                        If blnPointPassesFilter Then
                            .IonsMZ(intIonCountNew) = .IonsMZ(intIonIndex)
                            .IonsIntensity(intIonCountNew) = .IonsIntensity(intIonIndex)
                            .Charge(intIonCountNew) = .Charge(intIonIndex)
                            intIonCountNew += 1
                        End If

                    Next intIonIndex
                Else
                    intIonCountNew = .IonCount
                End If

                If intIonCountNew < .IonCount Then
                    .IonCount = intIonCountNew
                End If

                If blnWriteDebugData Then
                    swOutFile = New StreamWriter(New FileStream("DataDump_" & objMSSpectrum.ScanNumber.ToString & "_PostFilter.txt", FileMode.Create, FileAccess.Write, FileShare.Read))
                    swOutFile.WriteLine("m/z" & ControlChars.Tab & "Intensity")

                    ' Store the intensity values in objFilterDataArray
                    For intIonIndex = 0 To .IonCount - 1
                        swOutFile.WriteLine(.IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
                    Next
                    swOutFile.Close()
                End If

            End With
        Catch ex As Exception
            Throw New Exception("Error in clsLCMSDataPlotter.DiscardDataToLimitIonCount: " & ex.Message, ex)
        End Try

    End Sub

    ''' <summary>
    ''' Returns the file name of the recently saved file of the given type
    ''' </summary>
    ''' <param name="eFileType">File type to find</param>
    ''' <returns>File name if found; empty string if this file type was not saved</returns>
    ''' <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
    Public Function GetRecentFileInfo(ByVal eFileType As eOutputFileTypes) As String
        Dim intIndex As Integer
        For intIndex = 0 To mRecentFiles.Count - 1
            If mRecentFiles(intIndex).FileType = eFileType Then
                Return mRecentFiles(intIndex).FileName
            End If
        Next
        Return String.Empty
    End Function

    ''' <summary>
    ''' Returns the file name and path of the recently saved file of the given type
    ''' </summary>
    ''' <param name="eFileType">File type to find</param>
    ''' <param name="strFileName">File name (output)</param>
    ''' <param name="strFilePath">File Path (output)</param>
    ''' <returns>True if a match was found; otherwise returns false</returns>
    ''' <remarks>The list of recent files gets cleared each time you call Save2DPlots() or Reset()</remarks>
    Public Function GetRecentFileInfo(ByVal eFileType As eOutputFileTypes, ByRef strFileName As String, ByRef strFilePath As String) As Boolean
        Dim intIndex As Integer
        For intIndex = 0 To mRecentFiles.Count - 1
            If mRecentFiles(intIndex).FileType = eFileType Then
                strFileName = mRecentFiles(intIndex).FileName
                strFilePath = mRecentFiles(intIndex).FilePath
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>
    ''' Returns the cached scan data for the scan index
    ''' </summary>
    ''' <param name="intIndex"></param>
    ''' <returns>ScanData class</returns>
    ''' <remarks></remarks>
    Public Function GetCachedScanByIndex(ByVal intIndex As Integer) As clsScanData

        If intIndex >= 0 AndAlso intIndex < mScans.Count Then
            Return mScans(intIndex)
        Else
            Return Nothing
        End If

    End Function

    Public Sub Reset()

        mPointCountCached = 0
        mPointCountCachedAfterLastTrim = 0

        If mScans Is Nothing Then
            mScans = New List(Of clsLCMSDataPlotter.clsScanData)
        Else
            mScans.Clear()
        End If

        ClearRecentFileInfo()
    End Sub

    ''' <summary>
    ''' Filters the data stored in mScans to nominally retain the top intTargetDataPointCount data points, sorted by descending intensity
    ''' </summary>
    ''' <param name="intTargetDataPointCount">Target max number of data points (see remarks for caveat)</param>
    ''' <remarks>Note that the number of data points remaining after calling this function may still be
    '''          more than intTargetDataPointCount, depending on intMinPointsPerSpectrum 
    ''' For example, if intMinPointsPerSpectrum = 5 and we have 5000 scans, then there will be
    '''   at least 5*5000 = 25000 data points in memory.  If intTargetDataPointCount = 10000, then 
    '''   there could be as many as 25000 + 10000 = 25000 points in memory
    '''</remarks>
    Protected Sub TrimCachedData(ByVal intTargetDataPointCount As Integer, ByVal intMinPointsPerSpectrum As Integer)

        Static dtLastGCTime As DateTime = DateTime.UtcNow

        Dim intMasterIonIndex As Integer
        Dim intMasterIonIndexStart As Integer

        Dim intScanIndex As Integer
        Dim intIonIndex As Integer

        Dim intIonCountNew As Integer

        Try

            Dim objFilterDataArray = New clsFilterDataArrayMaxCount

            objFilterDataArray.MaximumDataCountToLoad = intTargetDataPointCount
            objFilterDataArray.TotalIntensityPercentageFilterEnabled = False

            ' Store the intensity values for each scan in objFilterDataArray
            ' However, skip scans for which there are <= intMinPointsPerSpectrum data points

            intMasterIonIndex = 0
            For intScanIndex = 0 To mScans.Count - 1
                With mScans(intScanIndex)
                    If .IonCount > intMinPointsPerSpectrum Then
                        ' Store the intensity values in objFilterDataArray
                        For intIonIndex = 0 To .IonCount - 1
                            objFilterDataArray.AddDataPoint(.IonsIntensity(intIonIndex), intMasterIonIndex)
                            intMasterIonIndex += 1
                        Next intIonIndex
                    End If
                End With
            Next intScanIndex

            ' Call .FilterData, which will determine which data points to keep
            objFilterDataArray.FilterData()

            ' Step through the scans and trim the data as needed
            intMasterIonIndex = 0
            mPointCountCached = 0

            For intScanIndex = 0 To mScans.Count - 1

                If mScans(intScanIndex).IonCount <= intMinPointsPerSpectrum Then
                    ' Skip this can since it has too few points
                    ' No need to update intMasterIonIndex since it was skipped above when calling objFilterDataArray.AddDataPoint
                Else

                    ' See if fewer than intMinPointsPerSpectrum points will remain after filtering
                    ' If so, we'll need to handle this scan differently

                    intMasterIonIndexStart = intMasterIonIndex

                    intIonCountNew = 0
                    For intIonIndex = 0 To mScans(intScanIndex).IonCount - 1
                        ' If the point's intensity is >= 0, then we keep it
                        If objFilterDataArray.GetAbundanceByIndex(intMasterIonIndex) >= 0 Then
                            intIonCountNew += 1
                        End If
                        intMasterIonIndex += 1
                    Next

                    If intIonCountNew < intMinPointsPerSpectrum Then
                        ' Too few points will remain after filtering
                        ' Retain the top intMinPointsPerSpectrum points in this spectrum

                        DiscardDataToLimitIonCount(mScans(intScanIndex), 0, 0, intMinPointsPerSpectrum)

                    Else
                        ' It's safe to filter the data

                        With mScans(intScanIndex)

                            ' Reset intMasterIonIndex to the saved value
                            intMasterIonIndex = intMasterIonIndexStart

                            intIonCountNew = 0
                            For intIonIndex = 0 To .IonCount - 1

                                ' If the point's intensity is >= 0, then we keep it
                                If objFilterDataArray.GetAbundanceByIndex(intMasterIonIndex) >= 0 Then

                                    ' Copying in place (don't actually need to copy unless intIonCountNew <> intIonIndex)
                                    If intIonCountNew <> intIonIndex Then
                                        .IonsMZ(intIonCountNew) = .IonsMZ(intIonIndex)
                                        .IonsIntensity(intIonCountNew) = .IonsIntensity(intIonIndex)
                                        .Charge(intIonCountNew) = .Charge(intIonIndex)
                                    End If

                                    intIonCountNew += 1
                                End If

                                intMasterIonIndex += 1
                            Next

                            .IonCount = intIonCountNew
                        End With

                    End If


                    If mScans(intScanIndex).IonsMZ.Length > 5 AndAlso
                       mScans(intScanIndex).IonCount < mScans(intScanIndex).IonsMZ.Length / 2.0 Then

                        ' Shrink the arrays to reduce the memory footprint
                        mScans(intScanIndex).ShrinkArrays()

                        If DateTime.UtcNow.Subtract(dtLastGCTime).TotalSeconds > 60 Then
                            ' Perform garbage collection every 60 seconds
                            dtLastGCTime = DateTime.UtcNow
                            PRISM.Processes.clsProgRunner.GarbageCollectNow()
                        End If

                    End If

                End If

                ' Bump up the total point count cached
                mPointCountCached += mScans(intScanIndex).IonCount

            Next intScanIndex

            ' Update mPointCountCachedAfterLastTrim
            mPointCountCachedAfterLastTrim = mPointCountCached

        Catch ex As Exception
            Throw New Exception("Error in clsLCMSDataPlotter.TrimCachedData: " & ex.Message, ex)
        End Try

    End Sub

    Protected Sub UpdateMinMax(ByVal sngValue As Single, ByRef sngMin As Single, ByRef sngMax As Single)
        If sngValue < sngMin Then
            sngMin = sngValue
        End If

        If sngValue > sngMax Then
            sngMax = sngValue
        End If
    End Sub

    Protected Sub UpdateMinMax(ByVal dblValue As Double, ByRef dblMin As Double, ByRef dblMax As Double)
        If dblValue < dblMin Then
            dblMin = dblValue
        End If

        If dblValue > dblMax Then
            dblMax = dblValue
        End If
    End Sub

    Protected Sub ValidateMSLevel()
        Dim intIndex As Integer
        Dim blnMSLevelDefined As Boolean

        For intIndex = 0 To mScans.Count - 1
            If mScans(intIndex).MSLevel > 0 Then
                blnMSLevelDefined = True
                Exit For
            End If
        Next intIndex

        If Not blnMSLevelDefined Then
            ' Set the MSLevel to 1 for all scans
            For intIndex = 0 To mScans.Count - 1
                mScans(intIndex).UpdateMSLevel(1)
            Next intIndex
        End If

    End Sub

#Region "Plotting Functions"

    Private Sub AddSeriesMonoMassVsScan(
      ByVal lstPointsByCharge As IList(Of List(Of ScatterPoint)),
      ByVal myPlot As PlotModel)

        ' Determine the number of data points to be plotted
        Dim intTotalPoints = 0
        For intCharge = 0 To lstPointsByCharge.Count - 1
            intTotalPoints += lstPointsByCharge(intCharge).Count
        Next

        For intCharge = 0 To lstPointsByCharge.Count - 1

            If lstPointsByCharge(intCharge).Count = 0 Then Continue For

            Dim strTitle = intCharge & "+"

            Dim seriesColor As Color = clsPlotContainer.GetColorByCharge(intCharge)

            Dim series = New ScatterSeries

            ' series.MarkerStroke = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B)
            series.MarkerType = MarkerType.Circle
            series.MarkerFill = OxyColor.FromArgb(seriesColor.A, seriesColor.R, seriesColor.G, seriesColor.B)
            series.Title = strTitle

            ' Customize the points
            If mScans.Count < 250 Then
                ' Use a point size of 2 when fewer than 250 scans
                series.MarkerSize = 2
            ElseIf mScans.Count < 500 Then
                ' Use a point size of 1 when 250 to 500 scans
                series.MarkerSize = 1
            Else
                ' Use a point size of 0.8 or 0.6 when >= 500 scans
                If intTotalPoints < 80000 Then
                    series.MarkerSize = 0.8
                Else
                    series.MarkerSize = 0.6
                End If
            End If

            series.Points.AddRange(lstPointsByCharge(intCharge))

            myPlot.Series.Add(series)
        Next

    End Sub

    Private Sub AddSeriesMzVsScan(
      ByVal strTitle As String,
      ByVal objPoints As IEnumerable(Of ScatterPoint),
      ByVal sngColorScaleMinIntensity As Single,
      ByVal sngColorScaleMaxIntensity As Single,
      ByVal myPlot As PlotModel)

        ' We use a linear color axis to color the data points based on intensity
        Dim colorAxis = New LinearColorAxis With {
            .Position = AxisPosition.Right,
            .Minimum = sngColorScaleMinIntensity,
            .Maximum = sngColorScaleMaxIntensity,
            .Palette = OxyPalettes.Jet(30),
            .IsAxisVisible = False
        }

        myPlot.Axes.Add(colorAxis)

        Dim series = New ScatterSeries

        series.MarkerType = MarkerType.Circle
        series.Title = strTitle

        ' Customize the point size
        If mScans.Count < 250 Then
            ' Use a point size of 2 when fewer than 250 scans
            series.MarkerSize = 2
        ElseIf mScans.Count < 5000 Then
            ' Use a point size of 1 when 250 to 5000 scans
            series.MarkerSize = 1
        Else
            ' Use a point size of 0.6 when >= 5000 scans
            series.MarkerSize = 0.6
        End If

        series.Points.AddRange(objPoints)

        myPlot.Series.Add(series)
    End Sub

    Protected Function ComputeMedian(ByRef sngList() As Single, ByVal intItemCount As Integer) As Single

        Dim intMidpointIndex As Integer
        Dim blnAverage As Boolean

        If sngList Is Nothing OrElse sngList.Length < 1 OrElse intItemCount < 1 Then
            ' List is empty (or intItemCount = 0)
            Return 0
        ElseIf intItemCount <= 1 Then
            ' Only 1 item; the median is the value
            Return sngList(0)
        Else
            ' Sort sngList ascending, then find the midpoint
            Array.Sort(sngList, 0, intItemCount)

            If intItemCount Mod 2 = 0 Then
                ' Even number
                intMidpointIndex = CInt(Math.Floor(intItemCount / 2)) - 1
                blnAverage = True
            Else
                ' Odd number
                intMidpointIndex = CInt(Math.Floor(intItemCount / 2))
            End If

            If intMidpointIndex > intItemCount Then intMidpointIndex = intItemCount - 1
            If intMidpointIndex < 0 Then intMidpointIndex = 0

            If blnAverage Then
                ' Even number of items
                ' Return the average of the two middle points
                Return (sngList(intMidpointIndex) + sngList(intMidpointIndex + 1)) / 2
            Else
                ' Odd number of items
                Return sngList(intMidpointIndex)
            End If

        End If

    End Function

    Private Function GetMonoMassSeriesByCharge(
      ByVal intMsLevelFilter As Integer,
      ByRef dblMinMz As Double,
      ByRef dblMaxMz As Double,
      ByRef dblScanTimeMax As Double,
      ByRef intMinScan As Integer,
      ByRef intMaxScan As Integer) As List(Of List(Of ScatterPoint))

        Dim dblScanTimeMin As Double

        intMinScan = Integer.MaxValue
        intMaxScan = 0
        dblMinMz = Single.MaxValue
        dblMaxMz = 0

        dblScanTimeMin = Double.MaxValue
        dblScanTimeMax = 0

        ' Determine the maximum charge state
        Dim intMaxCharge As Byte = 1

        For intScanIndex = 0 To mScans.Count - 1
            If intMsLevelFilter = 0 OrElse mScans(intScanIndex).MSLevel = intMsLevelFilter Then
                If mScans(intScanIndex).Charge.Count > 0 Then
                    For intIonIndex = 0 To mScans(intScanIndex).IonCount - 1
                        intMaxCharge = Math.Max(intMaxCharge, mScans(intScanIndex).Charge(intIonIndex))
                    Next
                End If
            End If
        Next

        ' Initialize the data for each charge state
        Dim lstSeries = New List(Of List(Of ScatterPoint))

        For intCharge = 0 To intMaxCharge
            lstSeries.Add(New List(Of ScatterPoint))
        Next

        ' Store the data, segregating by charge
        For intScanIndex = 0 To mScans.Count - 1
            If intMsLevelFilter = 0 OrElse mScans(intScanIndex).MSLevel = intMsLevelFilter Then

                With mScans(intScanIndex)
                    For intIonIndex = 0 To .IonCount - 1

                        Dim dataPoint = New ScatterPoint(.ScanNumber, .IonsMZ(intIonIndex))
                        dataPoint.Value = .IonsIntensity(intIonIndex)

                        lstSeries(.Charge(intIonIndex)).Add(dataPoint)

                        UpdateMinMax(.IonsMZ(intIonIndex), dblMinMz, dblMaxMz)

                    Next

                    UpdateMinMax(.ScanTimeMinutes, dblScanTimeMin, dblScanTimeMax)

                    If .ScanNumber < intMinScan Then
                        intMinScan = .ScanNumber
                    End If

                    If .ScanNumber > intMaxScan Then
                        intMaxScan = .ScanNumber
                    End If
                End With
            End If

        Next intScanIndex

        Return lstSeries

    End Function

    Private Function GetMzVsScanSeries(
      ByVal intMSLevelFilter As Integer,
      ByRef sngColorScaleMinIntensity As Single,
      ByRef sngColorScaleMaxIntensity As Single,
      ByRef dblMinMZ As Double,
      ByRef dblMaxMZ As Double,
      ByRef dblScanTimeMax As Double,
      ByRef intMinScan As Integer,
      ByRef intMaxScan As Integer,
      ByVal blnWriteDebugData As Boolean,
      ByVal swDebugFile As StreamWriter) As List(Of ScatterPoint)

        Dim intScanIndex As Integer
        Dim intIonIndex As Integer

        Dim intSortedIntensityListCount As Integer
        Dim sngSortedIntensityList() As Single

        Dim dblIntensitySum As Double
        Dim sngAvgIntensity As Single
        Dim sngMedianIntensity As Single

        Dim dblScanTimeMin As Double

        Dim objPoints = New List(Of ScatterPoint)

        dblIntensitySum = 0
        intSortedIntensityListCount = 0
        ReDim sngSortedIntensityList(mPointCountCached)

        sngColorScaleMinIntensity = Single.MaxValue
        sngColorScaleMaxIntensity = 0

        intMinScan = Integer.MaxValue
        intMaxScan = 0
        dblMinMZ = Single.MaxValue
        dblMaxMZ = 0

        dblScanTimeMin = Double.MaxValue
        dblScanTimeMax = 0

        For intScanIndex = 0 To mScans.Count - 1
            If intMSLevelFilter = 0 OrElse mScans(intScanIndex).MSLevel = intMSLevelFilter Then

                With mScans(intScanIndex)
                    For intIonIndex = 0 To .IonCount - 1
                        If intSortedIntensityListCount >= sngSortedIntensityList.Length Then
                            ' Need to reserve more room (this is unexpected)
                            ReDim Preserve sngSortedIntensityList(sngSortedIntensityList.Length * 2 - 1)
                        End If

                        sngSortedIntensityList(intSortedIntensityListCount) = .IonsIntensity(intIonIndex)
                        dblIntensitySum += sngSortedIntensityList(intSortedIntensityListCount)

                        Dim dataPoint = New ScatterPoint(.ScanNumber, .IonsMZ(intIonIndex))
                        dataPoint.Value = .IonsIntensity(intIonIndex)

                        objPoints.Add(dataPoint)

                        If blnWriteDebugData Then
                            swDebugFile.WriteLine(.ScanNumber & ControlChars.Tab & .IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
                        End If

                        UpdateMinMax(sngSortedIntensityList(intSortedIntensityListCount), sngColorScaleMinIntensity, sngColorScaleMaxIntensity)
                        UpdateMinMax(.IonsMZ(intIonIndex), dblMinMZ, dblMaxMZ)

                        intSortedIntensityListCount += 1
                    Next

                    UpdateMinMax(.ScanTimeMinutes, dblScanTimeMin, dblScanTimeMax)

                    If .ScanNumber < intMinScan Then
                        intMinScan = .ScanNumber
                    End If

                    If .ScanNumber > intMaxScan Then
                        intMaxScan = .ScanNumber
                    End If
                End With
            End If

        Next intScanIndex

        If objPoints.Count > 0 Then

            ' Compute median and average intensity values
            If intSortedIntensityListCount > 0 Then
                Array.Sort(sngSortedIntensityList, 0, intSortedIntensityListCount)
                sngMedianIntensity = ComputeMedian(sngSortedIntensityList, intSortedIntensityListCount)
                sngAvgIntensity = CSng(dblIntensitySum / intSortedIntensityListCount)

                ' Set the minimum color intensity to the median
                sngColorScaleMinIntensity = sngMedianIntensity
            End If

        End If

        Return objPoints
    End Function

    ''' <summary>
    ''' When PlottingDeisotopedData is False, creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
    ''' When PlottingDeisotopedData is True, creates a 2D plot of monoisotopic mass vs. scan number, using charge state as the 3rd dimension to color the data points
    ''' </summary>
    ''' <param name="strTitle">Title of the plot</param>
    ''' <param name="intMSLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
    ''' <param name="blnSkipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for intMSLevelFilter, set blnSkipTrimCachedData to False on the first call and True on subsequent calls)</param>
    ''' <returns>OxyPlot PlotContainer</returns>
    ''' <remarks></remarks>
    Private Function InitializePlot(
      ByVal strTitle As String,
      ByVal intMSLevelFilter As Integer,
      ByVal blnSkipTrimCachedData As Boolean) As clsPlotContainer

        Dim intMinScan As Integer
        Dim intMaxScan As Integer

        Dim sngColorScaleMinIntensity As Single
        Dim sngColorScaleMaxIntensity As Single

        Dim dblMinMZ As Double
        Dim dblMaxMZ As Double

        Dim dblScanTimeMax As Double

        If Not blnSkipTrimCachedData AndAlso mPointCountCached > mOptions.MaxPointsToPlot Then
            ' Need to step through the scans and reduce the number of points in memory

            ' Note that the number of data points remaining after calling this function may still be
            '  more than mOptions.MaxPointsToPlot, depending on mOptions.MinPointsPerSpectrum 
            '  (see TrimCachedData for more details)

            TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

        End If

        ' When this is true, then will write a text file of the mass spectrum before before and after it is filtered
        ' Used for debugging
        Dim blnWriteDebugData As Boolean
        Dim swDebugFile As StreamWriter = Nothing

        blnWriteDebugData = False
        If blnWriteDebugData Then
            swDebugFile = New StreamWriter(New FileStream(strTitle & " - LCMS Top " & IntToEngineeringNotation(mOptions.MaxPointsToPlot) & " points.txt", FileMode.Create, FileAccess.Write, FileShare.Read))
            swDebugFile.WriteLine("scan" & ControlChars.Tab & "m/z" & ControlChars.Tab & "Intensity")
        End If

        ' Populate objPoints and objScanTimePoints with the data
        ' At the same time, determine the range of m/z and intensity values
        ' Lastly, compute the median and average intensity values

        ' Instantiate the list to track the data points
        Dim lstPointsByCharge = New List(Of List(Of ScatterPoint))

        If mOptions.PlottingDeisotopedData Then
            lstPointsByCharge = GetMonoMassSeriesByCharge(intMSLevelFilter, dblMinMZ, dblMaxMZ, dblScanTimeMax, intMinScan, intMaxScan)
        Else
            Dim objPoints = GetMzVsScanSeries(intMSLevelFilter, sngColorScaleMinIntensity, sngColorScaleMaxIntensity, dblMinMZ, dblMaxMZ, dblScanTimeMax, intMinScan, intMaxScan, blnWriteDebugData, swDebugFile)
            lstPointsByCharge.Add(objPoints)
        End If

        If blnWriteDebugData Then
            swDebugFile.Close()
        End If

        Dim dblMaxMzToUse = Double.MaxValue
        If mOptions.PlottingDeisotopedData Then
            dblMaxMzToUse = mOptions.MaxMonoMassForDeisotopedPlot
        End If

        ' Count the actual number of points that will be plotted
        Dim intPointsToPlot As Integer = 0
        For Each objSeries In lstPointsByCharge
            For Each item In objSeries
                If item.Y < dblMaxMzToUse Then
                    intPointsToPlot += 1
                End If
            Next
        Next

        If intPointsToPlot = 0 Then
            ' Nothing to plot
            Return New clsPlotContainer(New PlotModel)
        End If

        ' Round intMinScan down to the nearest multiple of 10
        intMinScan = CInt(Math.Floor(intMinScan / 10.0) * 10)
        If intMinScan < 0 Then intMinScan = 0

        ' Round intMaxScan up to the nearest multiple of 10
        intMaxScan = CInt(Math.Ceiling(intMaxScan / 10.0) * 10)

        ' Round dblMinMZ down to the nearest multiple of 100
        dblMinMZ = CLng(Math.Floor(dblMinMZ / 100.0) * 100)

        ' Round dblMaxMZ up to the nearest multiple of 100
        dblMaxMZ = CLng(Math.Ceiling(dblMaxMZ / 100.0) * 100)

        Dim yAxisLabel As String
        If mOptions.PlottingDeisotopedData Then
            yAxisLabel = "Monoisotopic Mass"
        Else
            yAxisLabel = "m/z"
        End If

        Dim myPlot = clsOxyplotUtilities.GetBasicPlotModel(strTitle, "LC Scan Number", yAxisLabel)

        If mOptions.PlottingDeisotopedData Then
            AddSeriesMonoMassVsScan(lstPointsByCharge, myPlot)
            myPlot.TitlePadding = 40
        Else
            AddSeriesMzVsScan(strTitle, lstPointsByCharge.First(), sngColorScaleMinIntensity, sngColorScaleMaxIntensity, myPlot)
        End If

        ' Update the axis format codes if the data values are small or the range of data is small
        Dim xVals = From item In lstPointsByCharge.First() Select item.X
        clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes(0), xVals, True)

        Dim yVals = From item In lstPointsByCharge.First() Select item.Y
        clsOxyplotUtilities.UpdateAxisFormatCodeIfSmallValues(myPlot.Axes(1), yVals, False)

        Dim plotContainer = New clsPlotContainer(myPlot)
        plotContainer.FontSizeBase = clsOxyplotUtilities.FONT_SIZE_BASE

        ' Add a label showing the number of points displayed
        plotContainer.AnnotationBottomLeft = intPointsToPlot.ToString("0,000") & " points plotted"

        ' Possibly add a label showing the maximum elution time
        If dblScanTimeMax > 0 Then

            Dim strCaption As String
            If dblScanTimeMax < 2 Then
                strCaption = Math.Round(dblScanTimeMax, 2).ToString("0.00") & " minutes"
            ElseIf dblScanTimeMax < 10 Then
                strCaption = Math.Round(dblScanTimeMax, 1).ToString("0.0") & " minutes"
            Else
                strCaption = Math.Round(dblScanTimeMax, 0).ToString("0") & " minutes"
            End If

            plotContainer.AnnotationBottomRight = strCaption

        End If

        ' Override the auto-computed X axis range
        If mOptions.UseObservedMinScan Then
            myPlot.Axes(0).Minimum = intMinScan
        Else
            myPlot.Axes(0).Minimum = 0
        End If

        If intMaxScan = 0 Then
            myPlot.Axes(0).Maximum = 1
        Else
            myPlot.Axes(0).Maximum = intMaxScan
        End If

        If Math.Abs(myPlot.Axes(0).Minimum - myPlot.Axes(0).Maximum) < 0.01 Then
            intMinScan = CInt(myPlot.Axes(0).Minimum)
            myPlot.Axes(0).Minimum = intMinScan - 1
            myPlot.Axes(0).Maximum = intMinScan + 1
        ElseIf intMinScan = intMaxScan Then
            myPlot.Axes(0).Minimum = intMinScan - 1
            myPlot.Axes(0).Maximum = intMinScan + 1
        End If

         ' Assure that we don't see ticks between scan numbers
        clsOxyplotUtilities.ValidateMajorStep(myPlot.Axes(0))

        ' Set the maximum value for the Y-axis
        If mOptions.PlottingDeisotopedData Then
            If dblMaxMZ < mOptions.MaxMonoMassForDeisotopedPlot Then
                dblMaxMzToUse = dblMaxMZ
            Else
                dblMaxMzToUse = mOptions.MaxMonoMassForDeisotopedPlot
            End If
        Else
            dblMaxMzToUse = dblMaxMZ
        End If

        ' Override the auto-computed axis range
        myPlot.Axes(1).Minimum = dblMinMZ
        myPlot.Axes(1).Maximum = dblMaxMzToUse

        ' Hide the legend
        myPlot.IsLegendVisible = False

        Return plotContainer

    End Function

    ''' <summary>
    ''' Converts an integer to engineering notation
    ''' For example, 50000 will be returned as 50K
    ''' </summary>
    ''' <param name="intValue"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Protected Function IntToEngineeringNotation(ByVal intValue As Integer) As String

        If intValue < 1000 Then
            Return intValue.ToString
        ElseIf intValue < 1000000.0 Then
            Return CInt(Math.Round(intValue / 1000, 0)).ToString & "K"
        Else
            Return CInt(Math.Round(intValue / 1000 / 1000, 0)).ToString & "M"
        End If

    End Function

    Public Function Save2DPlots(ByVal strDatasetName As String, ByVal strOutputFolderPath As String) As Boolean

        Return Save2DPlots(strDatasetName, strOutputFolderPath, "", "")

    End Function

    Public Function Save2DPlots(
      ByVal strDatasetName As String,
      ByVal strOutputFolderPath As String,
      ByVal strFileNameSuffixAddon As String,
      ByVal strScanModeSuffixAddon As String) As Boolean

        Const EMBED_FILTER_SETTINGS_IN_NAME As Boolean = False

        Dim plotContainer As clsPlotContainer
        Dim strPNGFilePath As String
        Dim blnSuccess As Boolean

        Try

            ClearRecentFileInfo()

            ' Check whether all of the spectra have .MSLevel = 0
            ' If they do, change the level to 1
            ValidateMSLevel()

            If strFileNameSuffixAddon Is Nothing Then strFileNameSuffixAddon = String.Empty
            If strScanModeSuffixAddon Is Nothing Then strScanModeSuffixAddon = String.Empty


            Dim colorGradients = New Dictionary(Of String, OxyPalette)
            colorGradients.Add("BlackWhiteRed30", OxyPalettes.BlackWhiteRed(30))
            colorGradients.Add("BlueWhiteRed30", OxyPalettes.BlueWhiteRed(30))
            colorGradients.Add("Cool30", OxyPalettes.Cool(30))
            colorGradients.Add("Gray30", OxyPalettes.Gray(30))
            colorGradients.Add("Hot30", OxyPalettes.Hot(30))
            colorGradients.Add("Hue30", OxyPalettes.Hue(30))
            colorGradients.Add("HueDistinct30", OxyPalettes.HueDistinct(30))
            colorGradients.Add("Jet30", OxyPalettes.Jet(30))
            colorGradients.Add("Rainbow30", OxyPalettes.Rainbow(30))

            plotContainer = InitializePlot(strDatasetName & " - " & mOptions.MS1PlotTitle, 1, False)
            plotContainer.PlottingDeisotopedData = mOptions.PlottingDeisotopedData

            If mOptions.TestGradientColorSchemes Then
                plotContainer.AddGradients(colorGradients)
            End If

            If plotContainer.SeriesCount > 0 Then
                If EMBED_FILTER_SETTINGS_IN_NAME Then
                    strPNGFilePath = strDatasetName & "_" & strFileNameSuffixAddon & "LCMS_" & mOptions.MaxPointsToPlot & "_" & mOptions.MinPointsPerSpectrum & "_" & mOptions.MZResolution.ToString("0.00") & strScanModeSuffixAddon & ".png"
                Else
                    strPNGFilePath = strDatasetName & "_" & strFileNameSuffixAddon & "LCMS" & strScanModeSuffixAddon & ".png"
                End If
                strPNGFilePath = Path.Combine(strOutputFolderPath, strPNGFilePath)
                plotContainer.SaveToPNG(strPNGFilePath, 1024, 700, 96)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMS)
            End If

            plotContainer = InitializePlot(strDatasetName & " - " & mOptions.MS2PlotTitle, 2, True)
            If plotContainer.SeriesCount > 0 Then
                strPNGFilePath = Path.Combine(strOutputFolderPath, strDatasetName & "_" & strFileNameSuffixAddon & "LCMS_MSn" & strScanModeSuffixAddon & ".png")
                plotContainer.SaveToPNG(strPNGFilePath, 1024, 700, 96)
                AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMSMSn)
            End If

            blnSuccess = True

        Catch ex As Exception
            RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.Save2DPlots: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

#End Region

    ''' <summary>
    ''' This class tracks the m/z and intensity values for a given scan
    ''' It can optionally also track charge state
    ''' Be sure to use .IonCount to determine the number of data points, not .IonsMZ.Length
    ''' If you decrease .IonCount, you can optionally call .ShrinkArrays to reduce the allocated space
    ''' </summary>
    ''' <remarks></remarks>
    Public Class clsScanData

        Protected mScanNumber As Integer
        Protected mMSLevel As Integer
        Protected mScanTimeMinutes As Single

        Public IonCount As Integer
        Public IonsMZ() As Double
        Public IonsIntensity() As Single
        Public Charge() As Byte

        Public ReadOnly Property MSLevel() As Integer
            Get
                Return mMSLevel
            End Get
        End Property

        Public ReadOnly Property ScanNumber() As Integer
            Get
                Return mScanNumber
            End Get
        End Property

        Public ReadOnly Property ScanTimeMinutes() As Single
            Get
                Return mScanTimeMinutes
            End Get
        End Property

        Public Sub New(ByVal intScanNumber As Integer,
          ByVal intMSLevel As Integer,
          ByVal sngScanTimeMinutes As Single,
          ByVal intDataCount As Integer,
          ByVal dblIonsMZ() As Double,
          ByVal sngIonsIntensity() As Single,
          ByVal bytCharge() As Byte)

            mScanNumber = intScanNumber
            mMSLevel = intMSLevel
            mScanTimeMinutes = sngScanTimeMinutes

            IonCount = intDataCount
            ReDim IonsMZ(intDataCount - 1)
            ReDim IonsIntensity(intDataCount - 1)
            ReDim Charge(intDataCount - 1)

            ' Populate the arrays to be filtered
            Array.Copy(dblIonsMZ, IonsMZ, intDataCount)
            Array.Copy(sngIonsIntensity, IonsIntensity, intDataCount)
            Array.Copy(bytCharge, Charge, intDataCount)
        End Sub

        Public Sub ShrinkArrays()
            If IonCount < IonsMZ.Length Then
                ReDim Preserve IonsMZ(IonCount - 1)
                ReDim Preserve IonsIntensity(IonCount - 1)
                ReDim Preserve Charge(IonCount - 1)
            End If
        End Sub

        Public Sub UpdateMSLevel(ByVal NewMSLevel As Integer)
            mMSLevel = NewMSLevel
        End Sub

    End Class

    Public Class udtMSIonTypeComparer
        Implements IComparer(Of udtMSIonType)

        Public Function Compare(x As udtMSIonType, y As udtMSIonType) As Integer Implements IComparer(Of udtMSIonType).Compare
            Return x.MZ.CompareTo(y.MZ)
        End Function
    End Class

End Class
