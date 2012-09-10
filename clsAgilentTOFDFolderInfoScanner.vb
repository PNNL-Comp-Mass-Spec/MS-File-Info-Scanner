Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
'
' Last modified August 22, 2012

<CLSCompliant(False)> Public Class clsAgilentTOFDFolderInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	' Note: The extension must be in all caps
	Public Const AGILENT_DATA_FOLDER_D_EXTENSION As String = ".D"

	Public Const AGILENT_ACQDATA_FOLDER_NAME As String = "AcqData"
	Public Const AGILENT_MS_SCAN_FILE As String = "MSScan.bin"
	Public Const AGILENT_XML_CONTENTS_FILE As String = "Contents.xml"
	Public Const AGILENT_TIME_SEGMENT_FILE As String = "MSTS.xml"

	Protected WithEvents mPWizParser As clsProteowizardDataParser

	Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
		' The dataset name is simply the folder name without .D
		Try
			Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
		Catch ex As System.Exception
			Return String.Empty
		End Try
	End Function

	''' <summary>
	''' Reads the Contents.xml file to look for the AcquiredTime entry
	''' </summary>
	''' <param name="strFolderPath"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns>True if the file exists and the AcquiredTime entry was successfully parsed; otherwise false</returns>
	''' <remarks></remarks>
	Private Function ProcessContentsXMLFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		Dim strFilePath As String = String.Empty
		Dim blnSuccess As Boolean

		Try
			blnSuccess = False

			' Open the Contents.xml file
			strFilePath = System.IO.Path.Combine(strFolderPath, AGILENT_XML_CONTENTS_FILE)

			Using srReader As System.Xml.XmlTextReader = New System.Xml.XmlTextReader(New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))

				Do While Not srReader.EOF
					srReader.Read()

					Select Case srReader.NodeType
						Case Xml.XmlNodeType.Element
							Select Case srReader.Name
								Case "AcquiredTime"
									Try
										Dim dtAcquisitionStartTime As System.DateTime
										dtAcquisitionStartTime = srReader.ReadElementContentAsDateTime
										' Convert from Universal time to Local time
										udtFileInfo.AcqTimeStart = dtAcquisitionStartTime.ToLocalTime
										blnSuccess = True
									Catch ex As Exception
										' Ignore errors here
									End Try

								Case Else
									' Ignore it
							End Select
						Case Else
					End Select

				Loop

			End Using


		Catch ex As System.Exception
			' Exception reading file
			ReportError("Exception reading " & AGILENT_XML_CONTENTS_FILE & ": " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	''' <summary>
	''' Reads the MSTS.xml file to determine the acquisition length and the number of scans
	''' </summary>
	''' <param name="strFolderPath"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Protected Function ProcessTimeSegmentFile(ByVal strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType, ByRef dblTotalAcqTimeMinutes As Double) As Boolean
		Dim strFilePath As String = String.Empty
		Dim blnSuccess As Boolean

		Dim dblStartTime As Double
		Dim dblEndTime As Double


		Try
			blnSuccess = False
			udtFileInfo.ScanCount = 0
			dblTotalAcqTimeMinutes = 0

			' Open the Contents.xml file
			strFilePath = System.IO.Path.Combine(strFolderPath, AGILENT_TIME_SEGMENT_FILE)

			Using srReader As System.Xml.XmlTextReader = New System.Xml.XmlTextReader(New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))

				Do While Not srReader.EOF
					srReader.Read()

					Select Case srReader.NodeType
						Case Xml.XmlNodeType.Element
							Select Case srReader.Name
								Case "TimeSegment"
									dblStartTime = 0
									dblEndTime = 0

								Case "StartTime"
									dblStartTime = srReader.ReadElementContentAsDouble()

								Case "EndTime"
									dblEndTime = srReader.ReadElementContentAsDouble()

								Case "NumOfScans"
									udtFileInfo.ScanCount += srReader.ReadElementContentAsInt()
									blnSuccess = True

								Case Else
									' Ignore it
							End Select

						Case Xml.XmlNodeType.EndElement
							If srReader.Name = "TimeSegment" Then
								' Store the acqtime for this time segment

								If dblEndTime > dblStartTime Then
									blnSuccess = True
									dblTotalAcqTimeMinutes += (dblEndTime - dblStartTime)
								End If

							End If

					End Select

				Loop

			End Using

		Catch ex As System.Exception
			' Exception reading file
			ReportError("Exception reading " & AGILENT_TIME_SEGMENT_FILE & ": " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Returns True if success, False if an error

		Dim dblAcquisitionLengthMinutes As Double = 0

		Dim blnAcqStartTimeDetermined As Boolean = False
		Dim blnValidMSTS As Boolean
		Dim blnSuccess As Boolean

		Dim ioRootFolderInfo As System.IO.DirectoryInfo
		Dim ioAcqDataFolderInfo As System.IO.DirectoryInfo

		Dim ioFileInfo As System.IO.FileInfo

		Try
			blnSuccess = False
			ioRootFolderInfo = New System.IO.DirectoryInfo(strDataFilePath)
			ioAcqDataFolderInfo = New System.IO.DirectoryInfo(System.IO.Path.Combine(ioRootFolderInfo.FullName, AGILENT_ACQDATA_FOLDER_NAME))

			With udtFileInfo
				.FileSystemCreationTime = ioAcqDataFolderInfo.CreationTime
				.FileSystemModificationTime = ioAcqDataFolderInfo.LastWriteTime

				' The acquisition times will get updated below to more accurate values
				.AcqTimeStart = .FileSystemModificationTime
				.AcqTimeEnd = .FileSystemModificationTime

				.DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioRootFolderInfo.Name)
				.FileExtension = ioRootFolderInfo.Extension
				.FileSizeBytes = 0
				.ScanCount = 0

				If ioAcqDataFolderInfo.Exists Then
					' Sum up the sizes of all of the files in the AcqData folder
					For Each ioFileInfo In ioAcqDataFolderInfo.GetFiles("*", IO.SearchOption.AllDirectories)
						.FileSizeBytes += ioFileInfo.Length
					Next ioFileInfo

					' Look for the MSScan.bin file
					' Use its modification time to get an initial estimate for the acquisition end time
					ioFileInfo = New System.IO.FileInfo(System.IO.Path.Combine(ioAcqDataFolderInfo.FullName, AGILENT_MS_SCAN_FILE))

					If ioFileInfo.Exists Then
						.AcqTimeStart = ioFileInfo.LastWriteTime
						.AcqTimeEnd = ioFileInfo.LastWriteTime

						' Read the file info from the file system
						' Several of these stats will be further updated later
						UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)
					Else
						' Read the file info from the file system
						' Several of these stats will be further updated later
						UpdateDatasetFileStats(ioAcqDataFolderInfo, udtFileInfo.DatasetID)
					End If

					blnSuccess = True
				End If

			End With

			If blnSuccess Then
				' The AcqData folder exists

				' Parse the Contents.xml file to determine the acquisition start time
				blnAcqStartTimeDetermined = ProcessContentsXMLFile(ioAcqDataFolderInfo.FullName, udtFileInfo)

				' Parse the MSTS.xml file to determine the acquisition length and number of scans
				blnValidMSTS = ProcessTimeSegmentFile(ioAcqDataFolderInfo.FullName, udtFileInfo, dblAcquisitionLengthMinutes)

				If Not blnAcqStartTimeDetermined AndAlso blnValidMSTS Then
					' Compute the start time from .AcqTimeEnd minus dblAcquisitionLengthMinutes
					udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblAcquisitionLengthMinutes)
				End If

				' Note: could parse the AcqMethod.xml file to determine if MS2 spectra are present
				'<AcqMethod>
				'	<QTOF>
				'		<TimeSegment>
				'	      <Acquisition>
				'	        <AcqMode>TargetedMS2</AcqMode>

				' Read the raw data to create the TIC and BPI
				ReadBinaryData(ioRootFolderInfo.FullName, udtFileInfo)

			End If

			If blnSuccess Then

				' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
				With mDatasetStatsSummarizer.DatasetFileInfo
					.DatasetName = String.Copy(udtFileInfo.DatasetName)
					.FileExtension = String.Copy(udtFileInfo.FileExtension)
					.FileSizeBytes = udtFileInfo.FileSizeBytes
					.AcqTimeStart = udtFileInfo.AcqTimeStart
					.AcqTimeEnd = udtFileInfo.AcqTimeEnd
					.ScanCount = udtFileInfo.ScanCount
				End With
			End If


		Catch ex As System.Exception
			ReportError("Exception parsing Agilent TOF .D folder: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Protected Function ReadBinaryData(ByVal strDataFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim blnTICStored As Boolean = False
		Dim blnSRMDataCached As Boolean = False

		Dim blnSuccess As Boolean

		Try
			' Open the data folder using the ProteoWizardWrapper

			Dim objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader
			objPWiz = New pwiz.ProteowizardWrapper.MSDataFileReader(strDataFolderPath)


			Try
				Dim dtRunStartTime As System.DateTime = udtFileInfo.AcqTimeStart
				dtRunStartTime = CDate(objPWiz.RunStartTime())

				' Update AcqTimeEnd if possible
				If dtRunStartTime < udtFileInfo.AcqTimeEnd Then
					If udtFileInfo.AcqTimeEnd.Subtract(dtRunStartTime).TotalDays < 1 Then
						udtFileInfo.AcqTimeStart = dtRunStartTime
					End If
				End If

			Catch ex As Exception
				' Leave the times unchanged
			End Try


			' Instantiate the Proteowizard Data Parser class
			mPWizParser = New clsProteowizardDataParser(objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot, mLCMS2DPlot, mSaveLCMS2DPlots, mSaveTICAndBPI)
			mPWizParser.HighResMS1 = True
			mPWizParser.HighResMS2 = True

			Dim dblRuntimeMinutes As Double = 0

			If objPWiz.ChromatogramCount > 0 Then

				' Process the chromatograms
				mPWizParser.StoreChromatogramInfo(udtFileInfo, blnTICStored, blnSRMDataCached, dblRuntimeMinutes)
				mPWizParser.PossiblyUpdateAcqTimeStart(udtFileInfo, dblRuntimeMinutes)

			End If

			If objPWiz.SpectrumCount > 0 Then
				' Process the spectral data
				mPWizParser.StoreMSSpectraInfo(udtFileInfo, blnTICStored, dblRuntimeMinutes)
				mPWizParser.PossiblyUpdateAcqTimeStart(udtFileInfo, dblRuntimeMinutes)
			End If

			blnSuccess = True

		Catch ex As Exception
			ReportError("Exception reading the Binary Data in the Agilent TOF .D folder using Proteowizard: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Private Sub mPWizParser_ErrorEvent(Message As String) Handles mPWizParser.ErrorEvent
		ReportError(Message)
	End Sub

	Private Sub mPWizParser_MessageEvent(Message As String) Handles mPWizParser.MessageEvent
		ShowMessage(Message)
	End Sub

End Class
