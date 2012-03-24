Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Updated in March 2012 to use Proteowizard to read data from QTrap .Wiff files
' (cannot read MS data or TIC values from Agilent .Wiff files)

Public Class clsAgilentTOFOrQStarWiffFileInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	' Note: The extension must be in all caps
	Public Const AGILENT_TOF_OR_QSTAR_FILE_EXTENSION As String = ".WIFF"

	Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
		' The dataset name is simply the file name without .wiff
		Try
			Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
		Catch ex As System.Exception
			Return String.Empty
		End Try
	End Function

	Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Returns True if success, False if an error

		Dim ioFileInfo As System.IO.FileInfo
		Dim blnSuccess As Boolean = False

		Dim blnTICStored As Boolean = False
		Dim blnSRMDataCached As Boolean = False

		' Override strDataFilePath here, if needed
		strDataFilePath = strDataFilePath

		' Obtain the full path to the file
		ioFileInfo = New System.IO.FileInfo(strDataFilePath)


		Dim blnTest As Boolean
		blnTest = False
		If blnTest Then
			TestPWiz(ioFileInfo.FullName)
		End If


		With udtFileInfo
			.FileSystemCreationTime = ioFileInfo.CreationTime
			.FileSystemModificationTime = ioFileInfo.LastWriteTime

			' Using the file system modification time as the acquisition end time
			.AcqTimeStart = .FileSystemModificationTime
			.AcqTimeEnd = .FileSystemModificationTime

			.DatasetID = 0
			.DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
			.FileExtension = ioFileInfo.Extension
			.FileSizeBytes = ioFileInfo.Length

			.ScanCount = 0
		End With

		mDatasetStatsSummarizer.ClearCachedData()
		mLCMS2DPlot.Options.UseObservedMinScan = False

		Try
			' Open the .Wiff file using the ProteoWizardWrapper

			Dim objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader
			objPWiz = New pwiz.ProteowizardWrapper.MSDataFileReader(ioFileInfo.FullName)


			Try
				Dim dtRunStartTime As System.DateTime = udtFileInfo.AcqTimeStart
				dtRunStartTime = CDate(objPWiz.RunStartTime())

				' Update AcqTimeEnd if possible
				' Found out by trial and error that we need to use .ToUniversalTime() to adjust the time reported by ProteoWizard
				If dtRunStartTime.ToUniversalTime < udtFileInfo.AcqTimeEnd Then
					If udtFileInfo.AcqTimeEnd.Subtract(dtRunStartTime.ToUniversalTime()).TotalDays < 1 Then
						udtFileInfo.AcqTimeStart = dtRunStartTime.ToUniversalTime
					End If
				End If

			Catch ex As Exception
				udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
			End Try


			' Note that SRM .Wiff files will only have chromatograms, and no spectra

			If objPWiz.ChromatogramCount > 0 Then

				' Process the chromatograms
				Dim dblRuntimeMinutes As Double = 0

				StoreChromatogramInfo(objPWiz, udtFileInfo, blnTICStored, blnSRMDataCached, dblRuntimeMinutes)

				If dblRuntimeMinutes > 0 Then
					' Possibly adjust .AcqTimeStart
					Dim dtAcqTimeStartAlt As System.DateTime
					dtAcqTimeStartAlt = udtFileInfo.AcqTimeEnd.AddMinutes(-dblRuntimeMinutes)

					If udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd Then
						udtFileInfo.AcqTimeStart = dtAcqTimeStartAlt
					End If

				End If
			End If


			If objPWiz.SpectrumCount > 0 And Not blnSRMDataCached Then

				' Process the spectral data (though only if we did not process SRM data)

				' Note that this code has not been tested (as of March 2012)
				StoreMSSpectraInfo(objPWiz, udtFileInfo, blnTICStored)
			End If

		Catch ex As Exception
			ReportError("Error using ProteoWizard reader: " & ex.Message)
		End Try


		' Read the file info from the file system
		' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
		UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)

		' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
		With mDatasetStatsSummarizer.DatasetFileInfo
			.DatasetName = String.Copy(udtFileInfo.DatasetName)
			.FileExtension = String.Copy(udtFileInfo.FileExtension)
			.FileSizeBytes = udtFileInfo.FileSizeBytes
			.AcqTimeStart = udtFileInfo.AcqTimeStart
			.AcqTimeEnd = udtFileInfo.AcqTimeEnd
			.ScanCount = udtFileInfo.ScanCount
		End With

		blnSuccess = True

		Return blnSuccess

	End Function

	Private Function ExtractQ1MZ(ByVal strChromID As String, ByRef dblMZ As Double) As Boolean

		Const Q_REGEX As String = "Q[0-9]=([0-9.]+)"
		Static reGetQ1MZ As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex(Q_REGEX, Text.RegularExpressions.RegexOptions.Compiled)

		Return ExtractQMZ(reGetQ1MZ, strChromID, dblMZ)
	
	End Function

	Private Function ExtractQ3MZ(ByVal strChromID As String, ByRef dblMZ As Double) As Boolean

		Const Q1_Q3_REGEX As String = "Q1=[0-9.]+ Q3=([0-9.]+)"
		Static reGetQ3MZ As System.Text.RegularExpressions.Regex = New System.Text.RegularExpressions.Regex(Q1_Q3_REGEX, Text.RegularExpressions.RegexOptions.Compiled)

		Return ExtractQMZ(reGetQ3MZ, strChromID, dblMZ)

	End Function

	Private Function ExtractQMZ(ByRef reGetMZ As System.Text.RegularExpressions.Regex, ByVal strChromID As String, ByRef dblMZ As Double) As Boolean
		Dim reMatch As System.Text.RegularExpressions.Match

		reMatch = reGetMZ.Match(strChromID)
		If reMatch.Success Then
			If Double.TryParse(reMatch.Groups(1).Value, dblMZ) Then
				Return True
			End If
		End If

		Return False
	End Function

	Private Function FindNearestInList(ByRef lstItems As System.Collections.Generic.List(Of Single), ByVal sngValToFind As Single) As Integer

		Dim intIndexMatch As Integer

		intIndexMatch = lstItems.BinarySearch(sngValToFind)
		If intIndexMatch >= 0 Then
			' Exact match found			
		Else
			' Find the nearest match
			intIndexMatch = intIndexMatch Xor -1
			If intIndexMatch = lstItems.Count Then
				intIndexMatch -= 1
			End If

			If intIndexMatch > 0 Then
				' Possibly decrement intIndexMatch
				If Math.Abs(lstItems.Item(intIndexMatch - 1) - sngValToFind) < Math.Abs(lstItems.Item(intIndexMatch) - sngValToFind) Then
					intIndexMatch -= 1
				End If
			End If

			If intIndexMatch < lstItems.Count Then
				' Possible increment intIndexMatch
				If Math.Abs(lstItems.Item(intIndexMatch + 1) - sngValToFind) < Math.Abs(lstItems.Item(intIndexMatch) - sngValToFind) Then
					intIndexMatch += 1
				End If
			End If

			If intIndexMatch < 0 Then
				intIndexMatch = 0
			ElseIf intIndexMatch = lstItems.Count Then
				intIndexMatch = lstItems.Count - 1
			End If

		End If

		Return intIndexMatch
	End Function

	Private Sub ProcessSRM(ByVal strChromID As String, _
	 ByRef sngTimes() As Single, _
	 ByRef sngIntensities() As Single, _
	 ByRef lstTICScanTimes As System.Collections.Generic.List(Of Single), _
	 ByRef lstTICScanNumbers As System.Collections.Generic.List(Of Integer), _
	 ByRef dblRuntimeMinutes As Double, _
	 ByRef dct2DDataParent As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), _
	 ByRef dct2DDataProduct As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), _
	 ByRef dct2DDataScanTimes As System.Collections.Generic.Dictionary(Of Integer, Single))

		Dim intScanNumber As Integer = 0
		Dim intIndexMatch As Integer

		Dim blnParentMZFound As Boolean
		Dim blnProductMZFound As Boolean

		Dim dblParentMZ As Double
		Dim dblProductMZ As Double

		' Attempt to parse out the product m/z
		blnParentMZFound = ExtractQ1MZ(strChromID, dblParentMZ)
		blnProductMZFound = ExtractQ3MZ(strChromID, dblProductMZ)

		For intIndex As Integer = 0 To sngTimes.Length - 1

			' Find the ScanNumber in the TIC nearest to sngTimes(intIndex)
			intIndexMatch = FindNearestInList(lstTICScanTimes, sngTimes(intIndex))
			intScanNumber = lstTICScanNumbers(intIndexMatch)

			' Bump up dblRuntimeMinutes if necessary
			If sngTimes(intIndex) > dblRuntimeMinutes Then
				dblRuntimeMinutes = sngTimes(intIndex)
			End If


			Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

			objScanStatsEntry.ScanNumber = intScanNumber
			objScanStatsEntry.ScanType = 1

			objScanStatsEntry.ScanTypeName = "SRM"
			objScanStatsEntry.ScanFilterText = StripExtraFromChromID(strChromID)

			objScanStatsEntry.ElutionTime = sngTimes(intIndex).ToString("0.000")
			objScanStatsEntry.TotalIonIntensity = sngIntensities(intIndex).ToString("0.0")
			objScanStatsEntry.BasePeakIntensity = sngIntensities(intIndex).ToString("0.0")

			If blnParentMZFound Then
				objScanStatsEntry.BasePeakMZ = dblParentMZ.ToString("0.000")
			ElseIf blnProductMZFound Then
				objScanStatsEntry.BasePeakMZ = dblProductMZ.ToString("0.000")
			Else
				objScanStatsEntry.BasePeakMZ = "0"
			End If

			' Base peak signal to noise ratio
			objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

			objScanStatsEntry.IonCount = 1
			objScanStatsEntry.IonCountRaw = 1

			mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


			If mSaveLCMS2DPlots AndAlso sngIntensities(intIndex) > 0 Then
				' Store the m/z and intensity values in dct2DDataParent and dct2DDataProduct

				If blnParentMZFound Then
					Store2DPlotDataPoint(dct2DDataParent, intScanNumber, dblParentMZ, sngIntensities(intIndex))
				End If

				If blnProductMZFound Then
					Store2DPlotDataPoint(dct2DDataProduct, intScanNumber, dblProductMZ, sngIntensities(intIndex))
				End If


				If Not dct2DDataScanTimes.ContainsKey(intScanNumber) Then
					dct2DDataScanTimes(intScanNumber) = sngTimes(intIndex)
				End If

			End If

		Next

	End Sub

	Private Sub ProcessTIC(ByVal strChromID As String, _
	 ByRef sngTimes() As Single, _
	 ByRef sngIntensities() As Single, _
	 ByRef lstTICScanTimes As System.Collections.Generic.List(Of Single), _
	 ByRef lstTICScanNumbers As System.Collections.Generic.List(Of Integer), _
	 ByRef dblRuntimeMinutes As Double, _
	 ByVal blnStoreInTICandBPIPlot As Boolean)

		For intIndex As Integer = 0 To sngTimes.Length - 1
			lstTICScanTimes.Add(sngTimes(intIndex))
			lstTICScanNumbers.Add(intIndex + 1)

			' Bump up dblRuntimeMinutes if necessary
			If sngTimes(intIndex) > dblRuntimeMinutes Then
				dblRuntimeMinutes = sngTimes(intIndex)
			End If

			If blnStoreInTICandBPIPlot Then
				' Use this TIC chromatogram for this dataset since there are no normal Mass Spectra
				mTICandBPIPlot.AddDataTICOnly(intIndex + 1, 1, sngTimes(intIndex), sngIntensities(intIndex))

			End If

		Next

		' Make sure lstTICScanTimes is sorted
		Dim blnNeedToSort As Boolean = False
		For intIndex As Integer = 1 To lstTICScanTimes.Count - 1
			If lstTICScanTimes(intIndex) < lstTICScanTimes(intIndex - 1) Then
				blnNeedToSort = True
				Exit For
			End If
		Next

		If blnNeedToSort Then
			Dim sngTICScanTimes() As Single
			Dim intTICScanNumbers() As Integer
			ReDim sngTICScanTimes(lstTICScanTimes.Count - 1)
			ReDim intTICScanNumbers(lstTICScanTimes.Count - 1)

			lstTICScanTimes.CopyTo(sngTICScanTimes)
			lstTICScanNumbers.CopyTo(intTICScanNumbers)

			Array.Sort(sngTICScanTimes, intTICScanNumbers)

			lstTICScanTimes.Clear()
			lstTICScanNumbers.Clear()

			For intIndex As Integer = 0 To sngTICScanTimes.Length - 1
				lstTICScanTimes.Add(sngTICScanTimes(intIndex))
				lstTICScanNumbers.Add(intTICScanNumbers(intIndex))
			Next


		End If

	End Sub

	Private Sub StoreChromatogramInfo(ByRef objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, _
   ByRef blnTICStored As Boolean, ByRef blnSRMDataCached As Boolean, ByRef dblRuntimeMinutes As Double)

		Dim strChromID As String = String.Empty
		Dim sngTimes() As Single
		Dim sngIntensities() As Single
		ReDim sngTimes(0)
		ReDim sngIntensities(0)

		Dim lstTICScanTimes As System.Collections.Generic.List(Of Single) = New System.Collections.Generic.List(Of Single)
		Dim lstTICScanNumbers As System.Collections.Generic.List(Of Integer) = New System.Collections.Generic.List(Of Integer)

		' This dictionary tracks the m/z and intensity values for parent (Q1) ions of each scan
		' Key is ScanNumber; Value is a dictionary holding m/z and intensity values for that scan
		Dim dct2DDataParent As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double))
		dct2DDataParent = New System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double))

		' This dictionary tracks the m/z and intensity values for product (Q3) ions of each scan
		Dim dct2DDataProduct As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double))
		dct2DDataProduct = New System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double))

		' This dictionary tracks the scan times for each scan number tracked by dct2DDataParent and/or dct2DDataProduct
		Dim dct2DDataScanTimes As System.Collections.Generic.Dictionary(Of Integer, Single)
		dct2DDataScanTimes = New System.Collections.Generic.Dictionary(Of Integer, Single)

		' Note that even for a small .Wiff file (1.5 MB), obtaining the first chromatogram will take some time (20 to 60 seconds)
		' The chromatogram at index 0 should be the TIC
		' The chromatogram at index >=1 will be each SRM

		dblRuntimeMinutes = 0

		For intChromIndex As Integer = 0 To objPWiz.ChromatogramCount - 1

			Try
				If intChromIndex = 0 Then
					ShowMessage("Obtaining chromatograms (this could take as long as 60 seconds)")
				End If
				objPWiz.GetChromatogram(intChromIndex, strChromID, sngTimes, sngIntensities)

				If strChromID Is Nothing Then strChromID = String.Empty

				Dim oCVParams As pwiz.CLI.data.CVParamList
				Dim param As pwiz.CLI.data.CVParam = Nothing
				oCVParams = objPWiz.GetChromatogramCVParams(intChromIndex)

				If TryGetCVParam(oCVParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, param) Then
					' This chromatogram is the TIC

					Dim blnStoreInTICandBPIPlot As Boolean = False
					If mSaveTICAndBPI AndAlso objPWiz.SpectrumCount = 0 Then
						blnStoreInTICandBPIPlot = True
					End If

					ProcessTIC(strChromID, sngTimes, sngIntensities, lstTICScanTimes, lstTICScanNumbers, dblRuntimeMinutes, blnStoreInTICandBPIPlot)

					blnTICStored = blnStoreInTICandBPIPlot

					udtFileInfo.ScanCount = sngTimes.Length

				End If

				If TryGetCVParam(oCVParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, param) Then
					' This chromatogram is an SRM scan

					ProcessSRM(strChromID, sngTimes, sngIntensities, lstTICScanTimes, lstTICScanNumbers, dblRuntimeMinutes, dct2DDataParent, dct2DDataProduct, dct2DDataScanTimes)

					blnSRMDataCached = True
				End If


			Catch ex As Exception
				ReportError("Error processing chromatogram " & intChromIndex & ": " & ex.Message)
			End Try
		Next intChromIndex


		If mSaveLCMS2DPlots Then
			' Now that all of the chromatograms have been processed, transfer data from dct2DDataParent and dct2DDataProduct into mLCMS2DPlot

			If dct2DDataParent.Count > 0 OrElse dct2DDataProduct.Count > 0 Then
				mLCMS2DPlot.Options.MS1PlotTitle = "Q1 m/z"
				mLCMS2DPlot.Options.MS2PlotTitle = "Q3 m/z"

				Store2DPlotData(dct2DDataScanTimes, dct2DDataParent, dct2DDataProduct)
			End If

		End If


	End Sub

	Private Sub Store2DPlotDataPoint(ByRef dct2DData As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), ByVal intScanNumber As Integer, ByVal dblMZ As Double, ByVal dblIntensity As Double)

		Dim obj2DMzAndIntensity As System.Collections.Generic.Dictionary(Of Double, Double) = Nothing

		If dct2DData.TryGetValue(intScanNumber, obj2DMzAndIntensity) Then
			Dim dblCurrentIntensity As Double
			If obj2DMzAndIntensity.TryGetValue(dblMZ, dblCurrentIntensity) Then
				' Bump up the stored intensity at dblProductMZ
				obj2DMzAndIntensity(dblMZ) = dblCurrentIntensity + dblIntensity
			Else
				obj2DMzAndIntensity.Add(dblMZ, dblIntensity)
			End If
		Else
			obj2DMzAndIntensity = New System.Collections.Generic.Dictionary(Of Double, Double)
			obj2DMzAndIntensity.Add(dblMZ, dblIntensity)
		End If

		' Store the data for this scan
		dct2DData(intScanNumber) = obj2DMzAndIntensity

	End Sub

	Private Sub Store2DPlotData(ByRef dct2DDataScanTimes As System.Collections.Generic.Dictionary(Of Integer, Single), _
	  ByRef dct2DDataParent As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), _
	  ByRef dct2DDataProduct As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)))

		' This variable keeps track of the length of the largest Dictionary(Of Double, Double) object in dct2DData
		Dim intMax2DDataCount As Integer = 1

		Dim int2DScanNumMin As Integer = Integer.MaxValue
		Dim int2DScanNumMax As Integer = 0


		' Determine the min/max scan numbers in dct2DDataParent
		' Also determine intMax2DDataCount

		UpdateDataRanges(dct2DDataParent, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax)
		UpdateDataRanges(dct2DDataProduct, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax)

		Store2DPlotDataWork(dct2DDataParent, dct2DDataScanTimes, 1, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax)
		Store2DPlotDataWork(dct2DDataProduct, dct2DDataScanTimes, 2, intMax2DDataCount, int2DScanNumMin, int2DScanNumMax)


	End Sub

	Private Sub Store2DPlotDataWork(ByRef dct2DData As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), _
	  ByRef dct2DDataScanTimes As System.Collections.Generic.Dictionary(Of Integer, Single), _
	  ByVal intMSLevel As Integer, ByVal intMax2DDataCount As Integer, ByVal int2DScanNumMin As Integer, ByVal int2DScanNumMax As Integer)

		Dim dblMZList() As Double
		Dim dblIntensityList() As Double
		ReDim dblMZList(intMax2DDataCount - 1)
		ReDim dblIntensityList(intMax2DDataCount - 1)

		Dim dct2DEnum As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)).Enumerator
		dct2DEnum = dct2DData.GetEnumerator()
		Do While dct2DEnum.MoveNext()
			Dim int2DPlotScanNum As Integer
			int2DPlotScanNum = dct2DEnum.Current.Key

			Dim obj2DMzAndIntensity As System.Collections.Generic.Dictionary(Of Double, Double)
			obj2DMzAndIntensity = dct2DEnum.Current.Value

			obj2DMzAndIntensity.Keys.CopyTo(dblMZList, 0)
			obj2DMzAndIntensity.Values.CopyTo(dblIntensityList, 0)

			' Make sure the data is sorted
			Array.Sort(dblMZList, dblIntensityList, 0, obj2DMzAndIntensity.Count)

			' Store the data
			mLCMS2DPlot.AddScan(dct2DEnum.Current.Key, intMSLevel, dct2DDataScanTimes(int2DPlotScanNum), obj2DMzAndIntensity.Count, dblMZList, dblIntensityList)

		Loop


		If int2DScanNumMin / CDbl(int2DScanNumMax) > 0.5 Then
			' Zoom in the 2D plot to prevent all of the the data from being scrunched to the right
			mLCMS2DPlot.Options.UseObservedMinScan = True
		End If

	End Sub

	Private Sub StoreMSSpectraInfo(ByRef objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, ByVal blnTICStored As Boolean)

		Try
			Dim dblScanTimes() As Double
			Dim intMSLevels() As Byte

			ReDim dblScanTimes(0)
			ReDim intMSLevels(0)
			Dim dblTIC As Double = 0
			Dim dblBPI As Double = 0

			objPWiz.GetScanTimesAndMsLevels(dblScanTimes, intMSLevels)

			For intScanIndex As Integer = 0 To dblScanTimes.Length - 1

				Try

					Dim blnComputeTIC As Boolean = True
					Dim blnComputeBPI As Boolean = True

					' Obtain the raw mass spectrum
					Dim oMSDataSpectrum As pwiz.ProteowizardWrapper.MsDataSpectrum
					oMSDataSpectrum = objPWiz.GetSpectrum(intScanIndex)


					Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

					objScanStatsEntry.ScanNumber = intScanIndex + 1
					objScanStatsEntry.ScanType = oMSDataSpectrum.Level

					' Might be able to determine scan type info from oMSDataSpectrum.Precursors(0)
					' Alternatively, use .GetSpectrumPWiz
					Dim oSpectrum As pwiz.CLI.msdata.Spectrum
					Dim param As pwiz.CLI.data.CVParam = Nothing
					oSpectrum = objPWiz.GetSpectrumObject(intScanIndex)
					objScanStatsEntry.ScanTypeName = "MS"

					objScanStatsEntry.ScanFilterText = oSpectrum.id
					objScanStatsEntry.ElutionTime = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblScanTimes(intScanIndex), 5)

					If TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_total_ion_current, param) Then
						dblTIC = param.value
						objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblTIC, 5)
						blnComputeTIC = False
					End If

					If TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_base_peak_intensity, param) Then
						dblBPI = param.value
						objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBPI, 5)

						If TryGetCVParam(oSpectrum.scanList.scans(0).cvParams, pwiz.CLI.cv.CVID.MS_base_peak_m_z, param) Then
							objScanStatsEntry.BasePeakMZ = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(param.value, 5)
							blnComputeBPI = False
						End If
					End If

					' Base peak signal to noise ratio
					objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

					objScanStatsEntry.IonCount = oMSDataSpectrum.Mzs.Length
					objScanStatsEntry.IonCountRaw = objScanStatsEntry.IonCount


					If blnComputeBPI Or blnComputeTIC Then
						' Step through the raw data to compute the BPI and TIC

						Dim dblMZs() As Double = oMSDataSpectrum.Mzs
						Dim dblIntensities() As Double = oMSDataSpectrum.Intensities
						Dim dblBasePeakMZ As Double

						dblTIC = 0
						dblBPI = 0
						dblBasePeakMZ = 0

						For intIndex As Integer = 0 To dblMZs.Length - 1
							dblTIC += dblIntensities(intIndex)
							If dblIntensities(intIndex) > dblBPI Then
								dblBPI = dblIntensities(intIndex)
								dblBasePeakMZ = dblMZs(intIndex)
							End If
						Next

						objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblTIC, 5)
						objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBPI, 5)
						objScanStatsEntry.BasePeakMZ = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBasePeakMZ, 5)

					End If

					mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)


					If mSaveTICAndBPI And Not blnTICStored Then
						mTICandBPIPlot.AddData(intScanIndex + 1, intMSLevels(intScanIndex), CSng(dblScanTimes(intScanIndex)), dblBPI, dblTIC)
					End If


					If mSaveLCMS2DPlots Then
						mLCMS2DPlot.AddScan(intScanIndex + 1, intMSLevels(intScanIndex), CSng(dblScanTimes(intScanIndex)), oMSDataSpectrum.Mzs.Length, oMSDataSpectrum.Mzs, oMSDataSpectrum.Intensities)
					End If

				Catch ex As System.Exception
					ReportError("Error loading header info for scan " & intScanIndex + 1 & ": " & ex.Message)
				End Try

			Next intScanIndex

		Catch ex As System.Exception
			ReportError("Error obtaining scan times and MSLevels using GetScanTimesAndMsLevels: " & ex.Message)
		End Try

	End Sub

	Private Function StripExtraFromChromID(ByVal strText As String) As String

		' If strText looks like:
		' SRM SIC Q1=506.6 Q3=132.1 sample=1 period=1 experiment=1 transition=0

		' then remove text from sample= on

		Dim intCharIndex As Integer

		intCharIndex = strText.IndexOf("sample=")
		If intCharIndex > 0 Then
			strText = strText.Substring(0, intCharIndex).TrimEnd()
		End If

		Return strText

	End Function

	Private Function TryGetCVParam(ByRef oCVParams As pwiz.CLI.data.CVParamList, ByVal cvidToFind As pwiz.CLI.cv.CVID, ByRef paramMatch As pwiz.CLI.data.CVParam) As Boolean

		For Each param As pwiz.CLI.data.CVParam In oCVParams
			If param.cvid = cvidToFind Then
				If Not param.empty() Then
					paramMatch = param
					Return True
				End If
			End If
		Next

		Return False
	End Function

	Private Sub UpdateDataRanges(ByRef dct2DData As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)), _
	  ByRef intMax2DDataCount As Integer, ByRef int2DScanNumMin As Integer, ByRef int2DScanNumMax As Integer)

		Dim dct2DEnum As System.Collections.Generic.Dictionary(Of Integer, System.Collections.Generic.Dictionary(Of Double, Double)).Enumerator
		Dim int2DPlotScanNum As Integer

		dct2DEnum = dct2DData.GetEnumerator()
		Do While dct2DEnum.MoveNext()

			int2DPlotScanNum = dct2DEnum.Current.Key

			If dct2DEnum.Current.Value.Count > intMax2DDataCount Then
				intMax2DDataCount = dct2DEnum.Current.Value.Count
			End If

			If int2DPlotScanNum < int2DScanNumMin Then
				int2DScanNumMin = int2DPlotScanNum
			End If

			If int2DPlotScanNum > int2DScanNumMax Then
				int2DScanNumMax = int2DPlotScanNum
			End If
		Loop

	End Sub

	Private Sub TestPWiz(ByVal strFilePath As String)

		Const RUN_BENCHMARKS As Boolean = False

		Try
			Dim objPWiz2 As pwiz.CLI.msdata.MSDataFile
			objPWiz2 = New pwiz.CLI.msdata.MSDataFile(strFilePath)


			Console.WriteLine("Spectrum count: " & objPWiz2.run.spectrumList.size)
			Console.WriteLine()
		
			If objPWiz2.run.spectrumList.size() > 0 Then
				Dim intSpectrumIndex As Integer = 0
				Dim param As pwiz.CLI.data.CVParam = Nothing

				Do

					Dim oSpectrum As pwiz.CLI.msdata.Spectrum
					oSpectrum = objPWiz2.run.spectrumList.spectrum(intSpectrumIndex, getBinaryData:=True)

					Dim intMSLevel As Integer = 0
					Dim dblStartTimeMinutes As Double = 0

					If oSpectrum.scanList.scans.Count > 0 Then

						If TryGetCVParam(oSpectrum.scanList.scans(0).cvParams, pwiz.CLI.cv.CVID.MS_scan_start_time, param) Then
							Dim intScanNum As Integer = intSpectrumIndex + 1
							dblStartTimeMinutes = param.timeInSeconds() / 60.0

							Console.WriteLine("ScanIndex " & intSpectrumIndex & ", Scan " & intScanNum & ", Elution Time " & dblStartTimeMinutes & " minutes")
						End If

					End If

					' Use the following to determine info on this spectrum
					If TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_ms_level, param) Then
						Int32.TryParse(param.value, intMSLevel)
					End If

					' Use the following to get the MZs and Intensities
					Dim oMZs As pwiz.CLI.msdata.BinaryDataArray
					Dim oIntensities As pwiz.CLI.msdata.BinaryDataArray

					oMZs = oSpectrum.getMZArray
					oIntensities = oSpectrum.getIntensityArray()

					If oMZs.data.Count > 0 Then

						Console.WriteLine("  Data count: " & oMZs.data.Count)

						If RUN_BENCHMARKS Then

							Dim dblTIC1 As Double = 0
							Dim dblTIC2 As Double = 0
							Dim dtStartTime As System.DateTime
							Dim dtEndTime As System.DateTime
							Dim dtRunTimeSeconds1 As Double
							Dim dtRunTimeSeconds2 As Double
							Const LOOP_ITERATIONS As Integer = 2000

							' Note from Matt Chambers (matt.chambers42@gmail.com) 
							' Repeatedly accessing items directly via oMZs.data() can be very slow
							' With 700 points and 2000 iterations, it takes anywhere from 0.6 to 1.1 seconds to run from dtStartTime to dtEndTime
							dtStartTime = System.DateTime.Now()
							For j As Integer = 1 To LOOP_ITERATIONS
								For intIndex As Integer = 0 To oMZs.data.Count - 1
									dblTIC1 += oMZs.data(intIndex)
								Next
							Next j
							dtEndTime = System.DateTime.Now()
							dtRunTimeSeconds1 = dtEndTime.Subtract(dtStartTime).TotalSeconds

							' The preferred method is to copy the data from .data to a locally-stored mzArray object
							' With 700 points and 2000 iterations, it takes 0.016 seconds to run from dtStartTime to dtEndTime
							dtStartTime = System.DateTime.Now()
							For j As Integer = 1 To LOOP_ITERATIONS
								Dim oMzArray As pwiz.CLI.msdata.BinaryData = oMZs.data
								For intIndex As Integer = 0 To oMzArray.Count - 1
									dblTIC2 += oMzArray(intIndex)
								Next
							Next j
							dtEndTime = System.DateTime.Now()
							dtRunTimeSeconds2 = dtEndTime.Subtract(dtStartTime).TotalSeconds

							Console.WriteLine("  " & oMZs.data.Count & " points with " & LOOP_ITERATIONS & " iterations gives Runtime1=" & dtRunTimeSeconds1.ToString("0.000") & " sec. vs. Runtime2=" & dtRunTimeSeconds2.ToString("0.000") & " sec.")

							If dblTIC1 <> dblTIC2 Then
								Console.WriteLine("  TIC values don't agree; this is unexpected")
							End If
						End If

					End If

					If intSpectrumIndex < 25 Then
						intSpectrumIndex += 1
					Else
						intSpectrumIndex += 50
					End If

				Loop While intSpectrumIndex < objPWiz2.run.spectrumList.size()
			End If


			If objPWiz2.run.chromatogramList.size() > 0 Then
				Dim intChromIndex As Integer = 0

				Do

					Dim oChromatogram As pwiz.CLI.msdata.Chromatogram
					Dim strChromDescription As String = ""
					Dim oTimeIntensityPairList As pwiz.CLI.msdata.TimeIntensityPairList = New pwiz.CLI.msdata.TimeIntensityPairList


					' Note that even for a small .Wiff file (1.5 MB), obtaining the Chromatogram list will take some time (20 to 60 seconds)
					' The chromatogram at index 0 should be the TIC
					' The chromatogram at index >=1 will be each SRM

					oChromatogram = objPWiz2.run.chromatogramList.chromatogram(intChromIndex, getBinaryData:=True)

					' Determine the chromatogram type
					Dim param As pwiz.CLI.data.CVParam = Nothing

					If TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, param) Then
						strChromDescription = oChromatogram.id

						' Obtain the data
						oChromatogram.getTimeIntensityPairs(oTimeIntensityPairList)
					End If

					If TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, param) Then

						strChromDescription = oChromatogram.id

						' Store the SRM scan
						oChromatogram.getTimeIntensityPairs(oTimeIntensityPairList)
					End If

					intChromIndex += 1
				Loop While intChromIndex < 50 AndAlso intChromIndex < objPWiz2.run.chromatogramList.size()
			End If

		Catch ex As Exception
			ReportError("Error using ProteoWizard reader: " & ex.Message)
		End Try

	End Sub
End Class
