Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified September 10, 2012

<CLSCompliant(False)>
Public Class clsBrukerXmassFolderInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	Public Const BRUKER_BAF_FILE_NAME As String = "analysis.baf"
	Public Const BRUKER_EXTENSION_BAF_FILE_NAME As String = "extension.baf"
	Public Const BRUKER_ANALYSIS_YEP_FILE_NAME As String = "analysis.yep"

	' Note: The extension must be in all caps
	Public Const BRUKER_BAF_FILE_EXTENSION As String = ".BAF"

	Private Const BRUKER_SCANINFO_XML_FILE As String = "scan.xml"
	Private Const BRUKER_XMASS_LOG_FILE As String = "log.txt"
	Private Const BRUKER_AUTOMS_FILE As String = "AutoMS.txt"

	Protected WithEvents mPWizParser As clsProteowizardDataParser

	''' <summary>
	''' Looks for a .m folder then looks for apexAcquisition.method or submethods.xml in that folder
	''' Uses the file modification time as the run start time
	''' Also looks for the .hdx file in the dataset folder and examine its modification time
	''' </summary>
	''' <param name="ioDatasetFolder"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns>True if a valid file is found; otherwise false</returns>
	''' <remarks></remarks>
	Protected Function DetermineAcqStartTime(ByVal ioDatasetFolder As System.IO.DirectoryInfo, _
	  ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim blnSuccess As Boolean = False

		Dim intIndex As Integer

		Dim ioSubFolders() As System.IO.DirectoryInfo
		Dim ioFile As System.IO.FileInfo

		Try
			' Look for the method folder (folder name should end in .m)
			ioSubFolders = ioDatasetFolder.GetDirectories("*.m")

			If ioSubFolders.Length = 0 Then
				' Match not found
				' Look for any XMass folders
				ioSubFolders = ioDatasetFolder.GetDirectories("XMass*")
			End If


			If ioSubFolders.Length > 0 Then
				' Look for the apexAcquisition.method in each matching subfolder
				' Assume the file modification time is the acquisition start time
				' Note that the submethods.xml file sometimes gets modified after the run starts, so it should not be used to determine run start time

				For intIndex = 0 To ioSubFolders.Length - 1
					For Each ioFile In ioSubFolders(intIndex).GetFiles("apexAcquisition.method")
						udtFileInfo.AcqTimeStart = ioFile.LastWriteTime
						blnSuccess = True
						Exit For
					Next
					If blnSuccess Then Exit For
				Next intIndex

				If Not blnSuccess Then
					' apexAcquisition.method not found; try submethods.xml instead
					For intIndex = 0 To ioSubFolders.Length - 1
						For Each ioFile In ioSubFolders(intIndex).GetFiles("submethods.xml")
							udtFileInfo.AcqTimeStart = ioFile.LastWriteTime
							blnSuccess = True
							Exit For
						Next
						If blnSuccess Then Exit For
					Next intIndex
				End If

			End If

			' Also look for the .hdx file
			' Its file modification time typically also matches the run start time

			For Each ioFile In ioDatasetFolder.GetFiles("*.hdx")
				If Not blnSuccess OrElse ioFile.LastWriteTime < udtFileInfo.AcqTimeStart Then
					udtFileInfo.AcqTimeStart = ioFile.LastWriteTime
				End If

				blnSuccess = True
				Exit For
			Next

			' Make sure AcqTimeEnd and AcqTimeStart match
			udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart

		Catch ex As System.Exception
			ReportError("Error finding XMass method folder: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Protected Function ParseAutoMSFile(ByVal ioDatasetFolder As System.IO.DirectoryInfo, _
	  ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim strAutoMSFilePath As String

		Dim ioFileInfo As System.IO.FileInfo
		Dim srReader As System.IO.StreamReader

		Dim strLineIn As String
		Dim strSplitLine() As String

		Dim blnSuccess As Boolean

		Dim intScanNumber As Integer
		Dim intMSLevel As Integer
		Dim strScanTypeName As String

		Try

			strAutoMSFilePath = System.IO.Path.Combine(ioDatasetFolder.FullName, BRUKER_AUTOMS_FILE)
			ioFileInfo = New System.IO.FileInfo(strAutoMSFilePath)

			If ioFileInfo.Exists Then

				srReader = New System.IO.StreamReader(New System.IO.FileStream(ioFileInfo.FullName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))

				Do While srReader.Peek() >= 0
					strLineIn = srReader.ReadLine

					If Not strLineIn Is Nothing AndAlso strLineIn.Length > 0 Then
						strSplitLine = strLineIn.Split(ControlChars.Tab)

						If strSplitLine.Length >= 2 Then
							If Integer.TryParse(strSplitLine(0), intScanNumber) Then
								' First column contains a number
								' See if the second column is a known scan type

								Select Case strSplitLine(1)
									Case "MS"
										strScanTypeName = "HMS"
										intMSLevel = 1
									Case "MSMS"
										strScanTypeName = "HMSn"
										intMSLevel = 2
									Case Else
										strScanTypeName = String.Empty
								End Select

								mDatasetStatsSummarizer.UpdateDatasetScanType(intScanNumber, intMSLevel, strScanTypeName)
							End If
						End If
					End If
				Loop

				srReader.Close()

				blnSuccess = True
			End If
		Catch ex As System.Exception
			ReportError("Error finding AutoMS.txt file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Protected Function ParseBAFFile(ByVal ioBAFFileInfo As System.IO.FileInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim blnSuccess As Boolean = False

		Dim blnTICStored As Boolean = False
		Dim blnSRMDataCached As Boolean = False

		' Override strDataFilePath here, if needed
		Dim blnOverride As Boolean = False
		If blnOverride Then
			Dim strNewDataFilePath As String = "c:\temp\analysis.baf"
			ioBAFFileInfo = New System.IO.FileInfo(strNewDataFilePath)
		End If

		mDatasetStatsSummarizer.ClearCachedData()
		mLCMS2DPlot.Options.UseObservedMinScan = False

		Try
			' Open the analysis.baf (or extension.baf) file using the ProteoWizardWrapper
			ShowMessage("Determining acquisition info using Proteowizard (this could take a while)")

			Dim objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader
			objPWiz = New pwiz.ProteowizardWrapper.MSDataFileReader(ioBAFFileInfo.FullName)

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

				udtFileInfo.ScanCount = objPWiz.ChromatogramCount
			End If


			If objPWiz.SpectrumCount > 0 And Not blnSRMDataCached Then
				' Process the spectral data (though only if we did not process SRM data)
				mPWizParser.StoreMSSpectraInfo(udtFileInfo, blnTICStored, dblRuntimeMinutes)
				mPWizParser.PossiblyUpdateAcqTimeStart(udtFileInfo, dblRuntimeMinutes)

				udtFileInfo.ScanCount = objPWiz.SpectrumCount
			End If

			blnSuccess = True
		Catch ex As Exception
			ReportError("Error using ProteoWizard reader: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Protected Function ParseScanXMLFile(ByVal ioDatasetFolder As System.IO.DirectoryInfo, _
	   ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim strScanXMLFilePath As String

		Dim ioFileInfo As System.IO.FileInfo
		Dim srReader As System.Xml.XmlTextReader

		Dim blnSuccess As Boolean = False
		Dim blnInScanNode As Boolean
		Dim blnSkipRead As Boolean

		Dim intScanCount As Integer
		Dim intScanNumber As Integer
		Dim sngElutionTime As Single
		Dim dblTIC As Double
		Dim dblBPI As Double
		Dim intMSLevel As Integer

		Dim dblMaxRunTimeMinutes As Double = 0

		Try

			If mSaveTICAndBPI Then
				' Initialize the TIC and BPI arrays
				MyBase.InitializeTICAndBPI()
			End If

			strScanXMLFilePath = System.IO.Path.Combine(ioDatasetFolder.FullName, BRUKER_SCANINFO_XML_FILE)
			ioFileInfo = New System.IO.FileInfo(strScanXMLFilePath)

			If ioFileInfo.Exists Then

				srReader = New System.Xml.XmlTextReader(New System.IO.FileStream(ioFileInfo.FullName, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read))

				intScanCount = 0
				Do While Not srReader.EOF
					If blnSkipRead Then
						blnSkipRead = False
					Else
						srReader.Read()
					End If

					Select Case srReader.NodeType
						Case Xml.XmlNodeType.Element
							If blnInScanNode Then
								Select Case srReader.Name
									Case "count"
										intScanNumber = srReader.ReadElementContentAsInt
										blnSkipRead = True
									Case "minutes"
										sngElutionTime = srReader.ReadElementContentAsFloat
										blnSkipRead = True
									Case "tic"
										dblTIC = srReader.ReadElementContentAsFloat
										blnSkipRead = True
									Case "maxpeak"
										dblBPI = srReader.ReadElementContentAsFloat
										blnSkipRead = True
									Case Else
										' Ignore it
								End Select
							Else
								If srReader.Name = "scan" Then
									blnInScanNode = True
									intScanNumber = 0
									sngElutionTime = 0
									dblTIC = 0
									dblBPI = 0
									intMSLevel = 1

									intScanCount += 1
								End If
							End If
						Case Xml.XmlNodeType.EndElement
							If srReader.Name = "scan" Then
								blnInScanNode = False

								If mSaveTICAndBPI AndAlso intScanNumber > 0 Then
									mTICandBPIPlot.AddData(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC)
								End If

								Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry


								objScanStatsEntry.ScanNumber = intScanNumber
								objScanStatsEntry.ScanType = intMSLevel

								objScanStatsEntry.ScanTypeName = "HMS"
								objScanStatsEntry.ScanFilterText = ""

								objScanStatsEntry.ElutionTime = sngElutionTime.ToString("0.0000")
								objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblTIC, 5)
								objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBPI, 5)
								objScanStatsEntry.BasePeakMZ = "0"

								' Base peak signal to noise ratio
								objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

								objScanStatsEntry.IonCount = 0
								objScanStatsEntry.IonCountRaw = 0

								Dim dblElutionTime As Double
								If Double.TryParse(objScanStatsEntry.ElutionTime, dblElutionTime) Then
									If dblElutionTime > dblMaxRunTimeMinutes Then
										dblMaxRunTimeMinutes = dblElutionTime
									End If
								End If

								mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)

							End If
					End Select

				Loop

				srReader.Close()

				udtFileInfo.ScanCount = intScanCount

				If dblMaxRunTimeMinutes > 0 Then
					udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblMaxRunTimeMinutes)
				End If

				blnSuccess = True

			End If
		Catch ex As System.Exception
			' Error finding Scan.xml file
			ReportError("Error finding " & BRUKER_SCANINFO_XML_FILE & "file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess
	End Function

	Protected Function GetDatasetFolder(ByVal strDataFilePath As String) As System.IO.DirectoryInfo

		Dim ioDatasetFolder As System.IO.DirectoryInfo
		Dim ioFileInfo As System.IO.FileInfo

		' First see if strFileOrFolderPath points to a valid file
		ioFileInfo = New System.IO.FileInfo(strDataFilePath)

		If ioFileInfo.Exists() Then
			' User specified a file; assume the parent folder of this file is the dataset folder
			ioDatasetFolder = ioFileInfo.Directory
		Else
			' Assume this is the path to the dataset folder
			ioDatasetFolder = New System.IO.DirectoryInfo(strDataFilePath)
		End If

		Return ioDatasetFolder

	End Function

	Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
		Dim ioDatasetFolder As System.IO.DirectoryInfo
		Dim strDatasetName As String = String.Empty

		Try
			' The dataset name for a Bruker Xmass folder is the name of the parent directory
			' However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
			ioDatasetFolder = GetDatasetFolder(strDataFilePath)
			strDatasetName = ioDatasetFolder.Name

			If strDatasetName.ToLower().EndsWith(".d") Then
				strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2)
			End If

		Catch ex As System.Exception
			' Ignore errors
		End Try

		If strDatasetName Is Nothing Then strDatasetName = String.Empty
		Return strDatasetName

	End Function

	Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the XMass files in the dataset folder)

		Dim ioFileInfo As System.IO.FileInfo
		Dim ioDatasetFolder As System.IO.DirectoryInfo

		Dim ioFiles() As System.IO.FileInfo

		Dim blnSuccess As Boolean

		Try
			' Determine whether strDataFilePath points to a file or a folder

			ioDatasetFolder = GetDatasetFolder(strDataFilePath)

			' Validate that we have selected a valid folder
			If Not ioDatasetFolder.Exists Then
				MyBase.ReportError("File/folder not found: " & strDataFilePath)
				Return False
			End If

			' In case we cannot find a .BAF file, update the .AcqTime values to the folder creation date
			' We have to assign a date, so we'll assign the date for the BAF file
			With udtFileInfo
				.AcqTimeStart = ioDatasetFolder.CreationTime
				.AcqTimeEnd = ioDatasetFolder.CreationTime
			End With

			' Look for the analysis.baf file in ioFolderInfo
			' Use its modification time as the AcqTime start and End values
			' If we cannot find the anslysis.baf file, return false

			ioFiles = ioDatasetFolder.GetFiles(BRUKER_BAF_FILE_NAME)
			If ioFiles Is Nothing OrElse ioFiles.Length = 0 Then
				' analysis.baf not found; what about the extension.baf file?
				ioFiles = ioDatasetFolder.GetFiles(BRUKER_EXTENSION_BAF_FILE_NAME)
			End If

			If ioFiles Is Nothing OrElse ioFiles.Length = 0 Then
				MyBase.ReportError(BRUKER_BAF_FILE_EXTENSION & " or " & BRUKER_EXTENSION_BAF_FILE_NAME & " file not found in " & ioDatasetFolder.FullName)
				blnSuccess = False
			Else
				ioFileInfo = ioFiles(0)

				' Read the file info from the file system
				' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
				UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)

				' Update the dataset name and file extension
				udtFileInfo.DatasetName = GetDatasetNameViaPath(ioDatasetFolder.FullName)
				udtFileInfo.FileExtension = String.Empty
				udtFileInfo.FileSizeBytes = ioFileInfo.Length

				' Find the apexAcquisition.method or submethods.xml file in the XMASS_Method.m subfolder to determine .AcqTimeStart
				' This function updates udtFileInfo.AcqTimeEnd and udtFileInfo.AcqTimeStart to have the same time
				blnSuccess = DetermineAcqStartTime(ioDatasetFolder, udtFileInfo)

				' Update the acquisition end time using the write time of the .baf file
				If ioFileInfo.LastWriteTime > udtFileInfo.AcqTimeEnd Then
					udtFileInfo.AcqTimeEnd = ioFileInfo.LastWriteTime
				End If

				' Parse the scan.xml file (if it exists) to determine the number of spectra acquired
				' We can also obtain TIC and elution time values from this file
				' However, it does not track whether a scan is MS or MSn
				' If the scans.xml file contains runtime entries (e.g. <minutes>100.0456</minutes>) then .AcqTimeEnd is updated using .AcqTimeStart + RunTimeMinutes
				blnSuccess = ParseScanXMLFile(ioDatasetFolder, udtFileInfo)

				If Not blnSuccess Then
					' Use ProteoWizard to extract the scan counts and acquisition time information
					blnSuccess = ParseBAFFile(ioFileInfo, udtFileInfo)
				End If

				' Parse the AutoMS.txt file (if it exists) to determine which scans are MS and which are MS/MS
				ParseAutoMSFile(ioDatasetFolder, udtFileInfo)

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
			End If
		Catch ex As System.Exception
			ReportError("Exception processing BAF data: " & ex.Message)
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
