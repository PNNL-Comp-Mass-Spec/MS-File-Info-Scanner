
Option Strict On

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

#End Region

#Region "Member variables"

    Protected mPointCountCached As Integer              ' Keeps track of the total number of data points cached in mScans
    Protected mPointCountCachedAfterLastTrim As Integer

    Protected mScans As System.Collections.Generic.List(Of clsLCMSDataPlotter.clsScanData)

    Protected mOptions As clsOptions

	Protected mRecentFiles As System.Collections.Generic.List(Of udtOutputFileInfoType)

	Public Event ErrorEvent(ByVal Message As String)
#End Region

#Region "Properties"
    Public Property Options() As clsOptions
		Get
			Return mOptions
		End Get
        Set(ByVal value As clsOptions)
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
		Me.New(New clsOptions)
	End Sub

	Public Sub New(ByVal objOptions As clsOptions)
		mOptions = objOptions
		mRecentFiles = New System.Collections.Generic.List(Of udtOutputFileInfoType)
		Me.Reset()
	End Sub

	Protected Sub AddRecentFile(ByVal strFilePath As String, ByVal eFileType As eOutputFileTypes)
		Dim udtOutputFileInfo As udtOutputFileInfoType

		udtOutputFileInfo.FileType = eFileType
		udtOutputFileInfo.FileName = System.IO.Path.GetFileName(strFilePath)
		udtOutputFileInfo.FilePath = strFilePath

		mRecentFiles.Add(udtOutputFileInfo)
	End Sub

	Public Function AddScan(ByVal intScanNumber As Integer, _
		  ByVal intMSLevel As Integer, _
		  ByVal sngScanTimeMinutes As Single, _
		  ByVal intIonCount As Integer, _
		  ByRef dblIonsMZ() As Double, _
		  ByRef dblIonsIntensity() As Double) As Boolean

		Static intSpectraFoundExceedingMaxIonCount As Integer = 0
		Static intMaxIonCountReported As Integer = 0
		Static intSortingWarnCount As Integer = 0

		Dim objScanData As clsLCMSDataPlotter.clsScanData

		Dim dblIonsMZFiltered() As Double
		Dim sngIonsIntensityFiltered() As Single

		Dim intIndex As Integer

		Dim intIonCountNew As Integer
		Dim intMaxAllowableIonCount As Integer

		Dim blnCentroidRequired As Boolean
		Dim blnSuccess As Boolean


		Try

			If intIonCount <= 0 Then
				' No data to add
				Return False
			End If

			' Make sure the data is sorted by m/z
			For intIndex = 1 To intIonCount - 1
				If dblIonsMZ(intIndex) < dblIonsMZ(intIndex - 1) Then
					' May need to sort the data
					' However, if the intensity of both data points is zero, then we can simply swap the data
					If dblIonsIntensity(intIndex) = 0 AndAlso dblIonsIntensity(intIndex - 1) = 0 Then
						' Swap the m/z values
						Dim dblSwapVal As Double = dblIonsIntensity(intIndex)
						dblIonsIntensity(intIndex) = dblIonsMZ(intIndex - 1)
						dblIonsMZ(intIndex - 1) = dblSwapVal
					Else
						' Need to sort
						intSortingWarnCount += 1
						If intSortingWarnCount <= 10 Then
							Console.WriteLine("  Sorting m/z data (this typically shouldn't be required for Finnigan data, though can occur for high res orbitrap data)")
						ElseIf intSortingWarnCount Mod 100 = 0 Then
							Console.WriteLine("  Sorting m/z data (i = " & intSortingWarnCount & ")")
						End If
						Array.Sort(dblIonsMZ, dblIonsIntensity, 0, intIonCount)
						Exit For
					End If
				End If
			Next


			ReDim dblIonsMZFiltered(intIonCount - 1)
			ReDim sngIonsIntensityFiltered(intIonCount - 1)

			' Populate dblIonsMZFiltered & sngIonsIntensityFiltered, skipping any data points with an intensity value of 0 or less than mMinIntensity

			intIonCountNew = 0
			For intIndex = 0 To intIonCount - 1
				If dblIonsIntensity(intIndex) > 0 AndAlso dblIonsIntensity(intIndex) >= mOptions.MinIntensity Then
					dblIonsMZFiltered(intIonCountNew) = dblIonsMZ(intIndex)

					If dblIonsIntensity(intIndex) > Single.MaxValue Then
						sngIonsIntensityFiltered(intIonCountNew) = Single.MaxValue
					Else
						sngIonsIntensityFiltered(intIonCountNew) = CSng(dblIonsIntensity(intIndex))
					End If

					intIonCountNew += 1
				End If
			Next
			intIonCount = intIonCountNew

			' Check whether any of the data points is less than mOptions.MZResolution m/z units apart
			blnCentroidRequired = False
			For intIndex = 0 To intIonCount - 2
				If dblIonsMZFiltered(intIndex + 1) - dblIonsMZFiltered(intIndex) < mOptions.MZResolution Then
					blnCentroidRequired = True
				End If
			Next

			If blnCentroidRequired Then
				' Consolidate any points closer than mOptions.MZResolution m/z units
				CentroidMSData(mOptions.MZResolution, intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered)
			End If


			' Instantiate a new ScanData object for this scan
			objScanData = New clsLCMSDataPlotter.clsScanData(intScanNumber, intMSLevel, sngScanTimeMinutes, _
						 intIonCount, dblIonsMZFiltered, sngIonsIntensityFiltered)


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

			Me.mScans.Add(objScanData)
			mPointCountCached += objScanData.IonCount

			If mPointCountCached > mOptions.MaxPointsToPlot * 5 Then
				' Only repeat the trim if the number of cached data points has increased by 10%
				' This helps speed up program execution by avoiding trimming data after every new scan is added
				If mPointCountCached > mPointCountCachedAfterLastTrim * 1.1 Then

					' Step through the scans and reduce the number of points in memory
					TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

				End If
			End If

		Catch ex As System.Exception
			RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.AddScan: " & ex.Message & "; inner exception: " & ex.InnerException.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Function AddScanSkipFilters(ByRef objSourceData As clsScanData) As Boolean

		Dim blnSuccess As Boolean
		Dim objScanData As clsScanData

		Try
			If objSourceData Is Nothing OrElse objSourceData.IonCount <= 0 Then
				' No data to add
				Return False
			End If

			' Copy the data in objSourceScan
			objScanData = New clsLCMSDataPlotter.clsScanData(objSourceData.ScanNumber, objSourceData.MSLevel, objSourceData.ScanTimeMinutes, _
						 objSourceData.IonCount, objSourceData.IonsMZ, objSourceData.IonsIntensity)

			Me.mScans.Add(objScanData)
			mPointCountCached += objScanData.IonCount

			If mPointCountCached > mOptions.MaxPointsToPlot * 5 Then
				' Only repeat the trim if the number of cached data points has increased by 10%
				' This helps speed up program execution by avoiding trimming data after every new scan is added
				If mPointCountCached > mPointCountCachedAfterLastTrim * 1.1 Then

					' Step through the scans and reduce the number of points in memory
					TrimCachedData(mOptions.MaxPointsToPlot, mOptions.MinPointsPerSpectrum)

				End If
			End If

		Catch ex As System.Exception
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

			Return sngList(intMidpointIndex)
		End If

	End Function

	Private Sub CentroidMSData(ByVal sngMZResolution As Single, _
			 ByRef intIonCount As Integer, _
			 ByRef dblIonsMZ() As Double, _
			 ByRef sngIonsIntensity() As Single)

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
					intIonCountNew += 1
				End If
			Next intIndex
			intIonCount = intIonCountNew

		Catch ex As System.Exception
			RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.CentroidMSData: " & ex.Message)
		End Try

	End Sub

	Private Sub DiscardDataToLimitIonCount(ByRef objMSSpectrum As clsScanData, _
				ByVal dblMZIgnoreRangeStart As Double, _
				ByVal dblMZIgnoreRangeEnd As Double, _
				ByVal intMaxIonCountToRetain As Integer)

		Dim intIonCountNew As Integer
		Dim intIonIndex As Integer
		Dim blnMZIgnoreRangleEnabled As Boolean
		Dim blnPointPassesFilter As Boolean

		Dim objFilterDataArray As clsFilterDataArrayMaxCount

		' When this is true, then will write a text file of the mass spectrum before before and after it is filtered
		' Used for debugging
		Dim blnWriteDebugData As Boolean
		Dim srOutFile As System.IO.StreamWriter = Nothing

		Try
			If dblMZIgnoreRangeStart <> 0 Or dblMZIgnoreRangeEnd <> 0 Then
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
						srOutFile = New System.IO.StreamWriter(New System.IO.FileStream("DataDump_" & objMSSpectrum.ScanNumber.ToString & "_BeforeFilter.txt", IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))
						srOutFile.WriteLine("m/z" & ControlChars.Tab & "Intensity")
					End If

					' Store the intensity values in objFilterDataArray
					For intIonIndex = 0 To .IonCount - 1
						objFilterDataArray.AddDataPoint(.IonsIntensity(intIonIndex), intIonIndex)
						If blnWriteDebugData Then
							srOutFile.WriteLine(.IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
						End If
					Next

					If blnWriteDebugData Then
						srOutFile.Close()
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
					srOutFile = New System.IO.StreamWriter(New System.IO.FileStream("DataDump_" & objMSSpectrum.ScanNumber.ToString & "_PostFilter.txt", IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))
					srOutFile.WriteLine("m/z" & ControlChars.Tab & "Intensity")

					' Store the intensity values in objFilterDataArray
					For intIonIndex = 0 To .IonCount - 1
						srOutFile.WriteLine(.IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
					Next
					srOutFile.Close()
				End If

			End With
		Catch ex As System.Exception
			Throw New System.Exception("Error in clsLCMSDataPlotter.DiscardDataToLimitIonCount: " & ex.Message, ex)
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

	''' <summary>
	''' Creates a 2D plot of m/z vs. scan number, using Intensity as the 3rd dimension to color the data points
	''' </summary>
	''' <param name="strTitle">Title of the plot</param>
	''' <param name="intMSLevelFilter">0 to use all of the data, 1 to use data from MS scans, 2 to use data from MS2 scans, etc.</param>
	''' <param name="blnSkipTrimCachedData">When True, then doesn't call TrimCachedData (when making several plots in success, each with a different value for intMSLevelFilter, set blnSkipTrimCachedData to False on the first call and True on subsequent calls)</param>
	''' <returns>Zedgraph plot</returns>
	''' <remarks></remarks>
	Private Function InitializeGraphPane(ByVal strTitle As String, _
			  ByVal intMSLevelFilter As Integer, _
			  ByVal blnSkipTrimCachedData As Boolean) As ZedGraph.GraphPane

		Const FONT_SIZE_BASE As Integer = 10

		Dim myPane As New ZedGraph.GraphPane

		Dim intScanIndex As Integer
		Dim intIonIndex As Integer

		Dim sngColorScaleMinIntensity As Single
		Dim sngColorScaleMaxIntensity As Single

		Dim intSortedIntensityListCount As Integer
		Dim sngSortedIntensityList() As Single

		Dim dblIntensitySum As Double
		Dim sngAvgIntensity As Single
		Dim sngMedianIntensity As Single

		Dim intMinScan As Integer
		Dim intMaxScan As Integer

		Dim dblMinMZ As Double
		Dim dblMaxMZ As Double

		Dim dblScanTimeMin As Double
		Dim dblScanTimeMax As Double

		Dim objPoints As ZedGraph.PointPairList

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
		Dim srOutFile As System.IO.StreamWriter = Nothing

		blnWriteDebugData = False
		If blnWriteDebugData Then
			srOutFile = New System.IO.StreamWriter(New System.IO.FileStream(strTitle & " - LCMS Top " & IntToEngineeringNotation(mOptions.MaxPointsToPlot) & " points.txt", IO.FileMode.Create, IO.FileAccess.Write, IO.FileShare.Read))
			srOutFile.WriteLine("scan" & ControlChars.Tab & "m/z" & ControlChars.Tab & "Intensity")
		End If

		' Populate objPoints and objScanTimePoints with the data
		' At the same time, determine the range of m/z and intensity values
		' Lastly, compute the median and average intensity values

		' Instantiate the ZedGraph object to track the points
		objPoints = New ZedGraph.PointPairList

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

						objPoints.Add(.ScanNumber, _
							 .IonsMZ(intIonIndex), _
							 .IonsIntensity(intIonIndex))

						If blnWriteDebugData Then
							srOutFile.WriteLine(.ScanNumber & ControlChars.Tab & .IonsMZ(intIonIndex) & ControlChars.Tab & .IonsIntensity(intIonIndex))
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

		If blnWriteDebugData Then
			srOutFile.Close()
		End If

		If objPoints.Count = 0 Then
			' Nothing to plot
			Return myPane
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

		' Compute median and average intensity values
		If intSortedIntensityListCount > 0 Then
			Array.Sort(sngSortedIntensityList, 0, intSortedIntensityListCount)
			sngMedianIntensity = ComputeMedian(sngSortedIntensityList, intSortedIntensityListCount)
			sngAvgIntensity = CSng(dblIntensitySum / intSortedIntensityListCount)

			' Set the minimum color intensity to the median
			sngColorScaleMinIntensity = sngMedianIntensity
		End If

		' Set the titles and axis labels
		myPane.Title.Text = String.Copy(strTitle)
		myPane.XAxis.Title.Text = "LC Scan Number"
		myPane.YAxis.Title.Text = "m/z"

		' Generate a black curve with no symbols
		Dim myCurve As ZedGraph.LineItem
		myPane.CurveList.Clear()

		If objPoints.Count > 0 Then
			myCurve = myPane.AddCurve(strTitle, objPoints, System.Drawing.Color.MediumBlue, ZedGraph.SymbolType.Circle)

			' Turn off the line, so the curve will by symbols only
			myCurve.Line.IsVisible = False

			' Turn off the symbol borders
			myCurve.Symbol.Border.IsVisible = False

			' Customize the points
			If mScans.Count < 250 Then
				' Use a point size of 2 when fewer than 250 scans
				myCurve.Symbol.Size = 3
			ElseIf mScans.Count < 500 Then
				' Use a point size of 2 when 250 to 500 scans
				myCurve.Symbol.Size = 2
			Else
				' Use a point size of 1 when >= 500 scans
				myCurve.Symbol.Size = 1
			End If

			myCurve.Symbol.Type = ZedGraph.SymbolType.Circle

			' Set up a red-blue color gradient to be used for the fill
			' Note that at symbol sizes larger than 1, the fill doesn't show solid colors 
			' It instead fades from red to myCurve.Symbol.Fill.SecondaryValueGradientColor or 
			'  from blue to myCurve.Symbol.Fill.SecondaryValueGradientColor

			myCurve.Symbol.Fill = New ZedGraph.Fill(System.Drawing.Color.Blue, System.Drawing.Color.Red, 180)

			' Instruct ZedGraph to fill the symbols by selecting a color out of the
			' blue-red gradient based on the Z value.  
			' A value of sngColorScaleMinIntensity or less will be blue
			' A value of sngColorScaleMaxIntensity or more will be red
			' Values in between will be a linearly apportioned color between blue and red

			myCurve.Symbol.Fill.Type = ZedGraph.FillType.GradientByZ
			myCurve.Symbol.Fill.RangeMin = sngColorScaleMinIntensity
			myCurve.Symbol.Fill.RangeMax = sngColorScaleMaxIntensity
			myCurve.Symbol.Fill.SecondaryValueGradientColor = Drawing.Color.Red

			' If you wanted to just display a solid color, you'd do this:
			'myCurve.Symbol.Fill = New ZedGraph.Fill(System.Drawing.Color.MediumBlue)

		End If

		' Add a label showing the number of points displayed
		Dim objPointCountText As New ZedGraph.TextObj(objPoints.Count.ToString("0,000") & " points plotted", 0, 1, ZedGraph.CoordType.PaneFraction)

		With objPointCountText
			.FontSpec.Angle = 0
			.FontSpec.FontColor = Drawing.Color.Black
			.FontSpec.IsBold = False
			.FontSpec.Size = FONT_SIZE_BASE
			.FontSpec.Border.IsVisible = False
			.FontSpec.Fill.IsVisible = False
			.Location.AlignH = ZedGraph.AlignH.Left
			.Location.AlignV = ZedGraph.AlignV.Bottom
		End With
		myPane.GraphObjList.Add(objPointCountText)


		' Possibly add a label showing the maximum elution time
		If dblScanTimeMax > 0 Then

			Dim objScanTimeMaxText As New ZedGraph.TextObj(Math.Round(dblScanTimeMax, 0).ToString("0") & " minutes", 1, 1, ZedGraph.CoordType.PaneFraction)

			With objScanTimeMaxText
				.FontSpec.Angle = 0
				.FontSpec.FontColor = Drawing.Color.Black
				.FontSpec.IsBold = False
				.FontSpec.Size = FONT_SIZE_BASE
				.FontSpec.Border.IsVisible = False
				.FontSpec.Fill.IsVisible = False
				.Location.AlignH = ZedGraph.AlignH.Right
				.Location.AlignV = ZedGraph.AlignV.Bottom
			End With
			myPane.GraphObjList.Add(objScanTimeMaxText)

		End If

		' Hide the x and y axis grids
		myPane.XAxis.MajorGrid.IsVisible = False
		myPane.YAxis.MajorGrid.IsVisible = False

		myPane.XAxis.MajorTic.IsOpposite = False
		myPane.XAxis.MinorTic.IsOpposite = False

		myPane.YAxis.MajorTic.IsOpposite = False
		myPane.YAxis.MinorTic.IsOpposite = False

		' Set the X-axis to display unmodified scan numbers (by default, ZedGraph scales them to a range between 0 and 10)
		myPane.XAxis.Scale.Mag = 0
		myPane.XAxis.Scale.MagAuto = False
		myPane.XAxis.Scale.MaxGrace = 0

		' Override the auto-computed axis range
		If Me.mOptions.UseObservedMinScan Then
			myPane.XAxis.Scale.Min = intMinScan
		Else
			myPane.XAxis.Scale.Min = 0
		End If

		myPane.XAxis.Scale.Max = intMaxScan

		' Set the Y-axis to display unmodified m/z values
		myPane.YAxis.Scale.Mag = 0
		myPane.YAxis.Scale.MagAuto = False
		myPane.YAxis.Scale.MaxGrace = 0

		' Override the auto-computed axis range
		myPane.YAxis.Scale.Min = dblMinMZ
		myPane.YAxis.Scale.Max = dblMaxMZ

		' Align the Y axis labels so they are flush to the axis
		myPane.YAxis.Scale.Align = ZedGraph.AlignP.Inside

		' Adjust the font sizes
		myPane.XAxis.Title.FontSpec.Size = FONT_SIZE_BASE
		myPane.XAxis.Title.FontSpec.IsBold = False
		myPane.XAxis.Scale.FontSpec.Size = FONT_SIZE_BASE

		myPane.YAxis.Title.FontSpec.Size = FONT_SIZE_BASE
		myPane.YAxis.Title.FontSpec.IsBold = False
		myPane.YAxis.Scale.FontSpec.Size = FONT_SIZE_BASE

		myPane.Title.FontSpec.Size = FONT_SIZE_BASE + 1
		myPane.Title.FontSpec.IsBold = True

		' Fill the axis background with a gradient
		myPane.Chart.Fill = New ZedGraph.Fill(System.Drawing.Color.White, System.Drawing.Color.FromArgb(255, 230, 230, 230), 45.0F)

		' Could use the following to simply fill with white
		'myPane.Chart.Fill = New ZedGraph.Fill(Drawing.Color.White)

		' Hide the legend
		myPane.Legend.IsVisible = False

		' Force a plot update
		myPane.AxisChange()

		Return myPane

	End Function

	''' <summary>
	''' Converts an integer to engineering notation
	''' For example, 50000 will be returned as 50K
	''' </summary>
	''' <param name="intValue"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Protected Function IntToEngineeringNotation(ByVal intValue As Integer) As String
		Dim strValue As String
		strValue = String.Empty

		If intValue < 1000 Then
			Return intValue.ToString
		ElseIf intValue < 1000000.0 Then
			Return CInt(Math.Round(intValue / 1000, 0)).ToString & "K"
		Else
			Return CInt(Math.Round(intValue / 1000 / 1000, 0)).ToString & "M"
		End If

	End Function

	Public Sub Reset()

		mPointCountCached = 0
		mPointCountCachedAfterLastTrim = 0

		If mScans Is Nothing Then
			mScans = New System.Collections.Generic.List(Of clsLCMSDataPlotter.clsScanData)
		Else
			mScans.Clear()
		End If

		ClearRecentFileInfo()
	End Sub

	Public Function Save2DPlots(ByVal strDatasetName As String, _
		   ByVal strOutputFolderPath As String) As Boolean

		Return Save2DPlots(strDatasetName, strOutputFolderPath, "")

	End Function

	Public Function Save2DPlots(ByVal strDatasetName As String, _
		   ByVal strOutputFolderPath As String, _
		   ByVal strFileNameSuffixAddon As String) As Boolean

		Const EMBED_FILTER_SETTINGS_IN_NAME As Boolean = False

		Dim myPane As ZedGraph.GraphPane
		Dim strPNGFilePath As String
		Dim blnSuccess As Boolean

		Try

			ClearRecentFileInfo()

			' Check whether all of the spectra have .MSLevel = 0
			' If they do, change the level to 1
			ValidateMSLevel()

			If strFileNameSuffixAddon Is Nothing Then strFileNameSuffixAddon = String.Empty

			Do
				myPane = InitializeGraphPane(strDatasetName & " - " & mOptions.MS1PlotTitle, 1, False)
				If myPane.CurveList.Count > 0 Then
					If EMBED_FILTER_SETTINGS_IN_NAME Then
						strPNGFilePath = strDatasetName & "_" & strFileNameSuffixAddon & "LCMS_" & mOptions.MaxPointsToPlot & "_" & mOptions.MinPointsPerSpectrum & "_" & mOptions.MZResolution.ToString("0.00") & ".png"
					Else
						strPNGFilePath = strDatasetName & "_" & strFileNameSuffixAddon & "LCMS.png"
					End If
					strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strPNGFilePath)
					myPane.GetImage(1024, 700, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
					AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMS)
				End If
			Loop While False

			myPane = InitializeGraphPane(strDatasetName & " - " & mOptions.MS2PlotTitle, 2, True)
			If myPane.CurveList.Count > 0 Then
				strPNGFilePath = System.IO.Path.Combine(strOutputFolderPath, strDatasetName & "_" & strFileNameSuffixAddon & "LCMS_MSn.png")
				myPane.GetImage(1024, 700, 300, False).Save(strPNGFilePath, System.Drawing.Imaging.ImageFormat.Png)
				AddRecentFile(strPNGFilePath, eOutputFileTypes.LCMSMSn)
			End If

			blnSuccess = True

		Catch ex As System.Exception
			RaiseEvent ErrorEvent("Error in clsLCMSDataPlotter.Save2DPlots: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function


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

		Static dtLastGCTime As System.DateTime = System.DateTime.UtcNow

		Dim intMasterIonIndex As Integer
		Dim intMasterIonIndexStart As Integer

		Dim intScanIndex As Integer
		Dim intIonIndex As Integer

		Dim intIonCountNew As Integer

		Dim objFilterDataArray As clsFilterDataArrayMaxCount

		Try

			objFilterDataArray = New clsFilterDataArrayMaxCount

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
									End If

									intIonCountNew += 1
								End If

								intMasterIonIndex += 1
							Next

							.IonCount = intIonCountNew
						End With

					End If


					If mScans(intScanIndex).IonsMZ.Length > 5 AndAlso _
					   mScans(intScanIndex).IonCount < mScans(intScanIndex).IonsMZ.Length / 2.0 Then

						' Shrink the arrays to reduce the memory footprint
						mScans(intScanIndex).ShrinkArrays()

						If System.DateTime.UtcNow.Subtract(dtLastGCTime).TotalSeconds > 60 Then
							' Perform garbage collection every 60 seconds
							dtLastGCTime = System.DateTime.UtcNow
							GC.Collect()
							GC.WaitForPendingFinalizers()
							System.Threading.Thread.Sleep(1000)
						End If

					End If

				End If

				' Bump up the total point count cached
				mPointCountCached += mScans(intScanIndex).IonCount

			Next intScanIndex

			' Update mPointCountCachedAfterLastTrim
			mPointCountCachedAfterLastTrim = mPointCountCached

		Catch ex As System.Exception
			Throw New System.Exception("Error in clsLCMSDataPlotter.TrimCachedData: " & ex.Message, ex)
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

	''' <summary>
	''' This class tracks the m/z and intensity values for a given scan
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

		Public Sub New(ByVal intScanNumber As Integer, _
			  ByVal intMSLevel As Integer, _
			  ByVal sngScanTimeMinutes As Single, _
			  ByVal intDataCount As Integer, _
			  ByRef dblIonsMZ() As Double, _
			  ByRef sngIonsIntensity() As Single)

			mScanNumber = intScanNumber
			mMSLevel = intMSLevel
			mScanTimeMinutes = sngScanTimeMinutes

			Me.IonCount = intDataCount
			ReDim Me.IonsMZ(intDataCount - 1)
			ReDim Me.IonsIntensity(intDataCount - 1)

			' Populate the arrays to be filtered
			Array.Copy(dblIonsMZ, Me.IonsMZ, intDataCount)
			Array.Copy(sngIonsIntensity, Me.IonsIntensity, intDataCount)
		End Sub

		Public Sub ShrinkArrays()
			If Me.IonCount < IonsMZ.Length Then
				ReDim Preserve IonsMZ(Me.IonCount - 1)
				ReDim Preserve IonsIntensity(Me.IonCount - 1)
			End If
		End Sub

		Public Sub UpdateMSLevel(ByVal NewMSLevel As Integer)
			mMSLevel = NewMSLevel
		End Sub

	End Class

	''' <summary>
	''' Options class for clsLCMSDatPlotter
	''' </summary>
	''' <remarks></remarks>
	Public Class clsOptions
		Public Const DEFAULT_MAX_POINTS_TO_PLOT As Integer = 500000
		Public Const DEFAULT_MIN_POINTS_PER_SPECTRUM As Integer = 2

		Public Const DEFAULT_MZ_RESOLUTION As Single = 0.4
		Public Const DEFAULT_MIN_INTENSITY As Single = 0

		Protected Const DEFAULT_MS1_PLOT_TITLE As String = "MS Spectra"
		Protected Const DEFAULT_MS2_PLOT_TITLE As String = "MS2 Spectra"

		Protected mMaxPointsToPlot As Integer
		Protected mMinPointsPerSpectrum As Integer

		Protected mMZResolution As Single
		Protected mMinIntensity As Single

		Protected mMS1PlotTitle As String
		Protected mMS2PlotTitle As String

		Protected mUseObservedMinScan As Boolean

        Public Property MaxPointsToPlot() As Integer
			Get
				Return mMaxPointsToPlot
			End Get
			Set(ByVal value As Integer)
				If value < 10 Then value = 10
				mMaxPointsToPlot = value
			End Set
		End Property

        Public Property MinPointsPerSpectrum() As Integer
			Get
				Return mMinPointsPerSpectrum
			End Get
			Set(ByVal value As Integer)
				If value < 0 Then value = 0
				mMinPointsPerSpectrum = value
			End Set
		End Property

		Public Property MS1PlotTitle() As String
			Get
				Return mMS1PlotTitle
			End Get
			Set(value As String)
				If String.IsNullOrEmpty(value) Then
					value = DEFAULT_MS1_PLOT_TITLE
				End If
				mMS1PlotTitle = value
			End Set
		End Property

		Public Property MS2PlotTitle() As String
			Get
				Return mMS2PlotTitle
			End Get
			Set(value As String)
				If String.IsNullOrEmpty(value) Then
					value = DEFAULT_MS2_PLOT_TITLE
				End If
				mMS2PlotTitle = value
			End Set
		End Property

        Public Property MZResolution() As Single
			Get
				Return mMZResolution
			End Get
			Set(ByVal value As Single)
				If value < 0 Then value = 0
				mMZResolution = value
			End Set
		End Property

        Public Property MinIntensity() As Single
			Get
				Return mMinIntensity
			End Get
			Set(ByVal value As Single)
				If value < 0 Then value = 0
				mMinIntensity = value
			End Set
		End Property

		Public Property UseObservedMinScan() As Boolean
			Get
				Return mUseObservedMinScan
			End Get
			Set(value As Boolean)
				mUseObservedMinScan = value
			End Set
		End Property

        Public Function Clone() As clsOptions
			Dim objClone As New clsOptions

			With objClone
				.MaxPointsToPlot = Me.MaxPointsToPlot
				.MinPointsPerSpectrum = Me.MinPointsPerSpectrum

				.MZResolution = Me.MZResolution
				.MinIntensity = Me.MinIntensity

				.MS1PlotTitle = Me.MS1PlotTitle
				.MS2PlotTitle = Me.MS2PlotTitle

				.UseObservedMinScan = Me.UseObservedMinScan
			End With

			Return objClone

		End Function

		Public Sub New()
			mMaxPointsToPlot = DEFAULT_MAX_POINTS_TO_PLOT
			mMinPointsPerSpectrum = DEFAULT_MIN_POINTS_PER_SPECTRUM

			mMZResolution = DEFAULT_MZ_RESOLUTION
			mMinIntensity = DEFAULT_MIN_INTENSITY

			mMS1PlotTitle = DEFAULT_MS1_PLOT_TITLE
			mMS2PlotTitle = DEFAULT_MS2_PLOT_TITLE

			mUseObservedMinScan = False
		End Sub

	End Class
End Class
