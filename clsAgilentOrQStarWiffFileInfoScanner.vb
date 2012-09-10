Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2005
'
' Updated in March 2012 to use Proteowizard to read data from QTrap .Wiff files
' (cannot read MS data or TIC values from Agilent .Wiff files)

<CLSCompliant(False)>
Public Class clsAgilentTOFOrQStarWiffFileInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	' Note: The extension must be in all caps
	Public Const AGILENT_TOF_OR_QSTAR_FILE_EXTENSION As String = ".WIFF"

	Protected WithEvents mPWizParser As clsProteowizardDataParser

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
				dtRunStartTime = dtRunStartTime.ToUniversalTime()
				If dtRunStartTime < udtFileInfo.AcqTimeEnd Then
					If udtFileInfo.AcqTimeEnd.Subtract(dtRunStartTime).TotalDays < 1 Then
						udtFileInfo.AcqTimeStart = dtRunStartTime
					End If
				End If

			Catch ex As Exception
				udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
			End Try

			' Instantiate the Proteowizard Data Parser class
			mPWizParser = New clsProteowizardDataParser(objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot, mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI)
			mPWizParser.HighResMS1 = True
			mPWizParser.HighResMS2 = True

			Dim dblRuntimeMinutes As Double = 0

			' Note that SRM .Wiff files will only have chromatograms, and no spectra
			If objPWiz.ChromatogramCount > 0 Then

				' Process the chromatograms
				mPWizParser.StoreChromatogramInfo(udtFileInfo, blnTICStored, blnSRMDataCached, dblRuntimeMinutes)
				mPWizParser.PossiblyUpdateAcqTimeStart(udtFileInfo, dblRuntimeMinutes)

			End If


			If objPWiz.SpectrumCount > 0 And Not blnSRMDataCached Then
				' Process the spectral data (though only if we did not process SRM data)
				mPWizParser.StoreMSSpectraInfo(udtFileInfo, blnTICStored, dblRuntimeMinutes)
				mPWizParser.PossiblyUpdateAcqTimeStart(udtFileInfo, dblRuntimeMinutes)
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

						If clsProteowizardDataParser.TryGetCVParam(oSpectrum.scanList.scans(0).cvParams, pwiz.CLI.cv.CVID.MS_scan_start_time, param) Then
							Dim intScanNum As Integer = intSpectrumIndex + 1
							dblStartTimeMinutes = param.timeInSeconds() / 60.0

							Console.WriteLine("ScanIndex " & intSpectrumIndex & ", Scan " & intScanNum & ", Elution Time " & dblStartTimeMinutes & " minutes")
						End If

					End If

					' Use the following to determine info on this spectrum
					If clsProteowizardDataParser.TryGetCVParam(oSpectrum.cvParams, pwiz.CLI.cv.CVID.MS_ms_level, param) Then
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

					If clsProteowizardDataParser.TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_TIC_chromatogram, param) Then
						strChromDescription = oChromatogram.id

						' Obtain the data
						oChromatogram.getTimeIntensityPairs(oTimeIntensityPairList)
					End If

					If clsProteowizardDataParser.TryGetCVParam(oChromatogram.cvParams, pwiz.CLI.cv.CVID.MS_selected_reaction_monitoring_chromatogram, param) Then

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

	Private Sub mPWizParser_ErrorEvent(Message As String) Handles mPWizParser.ErrorEvent
		ReportError(Message)
	End Sub

	Private Sub mPWizParser_MessageEvent(Message As String) Handles mPWizParser.MessageEvent
		ShowMessage(Message)
	End Sub
End Class
