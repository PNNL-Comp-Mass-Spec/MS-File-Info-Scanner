Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified October 8, 2012

<CLSCompliant(False)>
Public Class clsBrukerXmassFolderInfoScanner
	Inherits clsMSFileInfoProcessorBaseClass

	Public Const BRUKER_BAF_FILE_NAME As String = "analysis.baf"
	Public Const BRUKER_EXTENSION_BAF_FILE_NAME As String = "extension.baf"
	Public Const BRUKER_ANALYSIS_YEP_FILE_NAME As String = "analysis.yep"
	Public Const BRUKER_SQLITE_INDEX_FILE_NAME As String = "Storage.mcf_idx"

	' Note: The extension must be in all caps
	Public Const BRUKER_BAF_FILE_EXTENSION As String = ".BAF"
	Public Const BRUKER_MCF_FILE_EXTENSION As String = ".MCF"
	Public Const BRUKER_SQLITE_INDEX_EXTENSION As String = ".MCF_IDX"

	Private Const BRUKER_SCANINFO_XML_FILE As String = "scan.xml"
	Private Const BRUKER_XMASS_LOG_FILE As String = "log.txt"
	Private Const BRUKER_AUTOMS_FILE As String = "AutoMS.txt"

	Protected WithEvents mPWizParser As clsProteowizardDataParser

	Protected Structure udtMCFScanInfoType
		Public ScanMode As Double
		Public MSLevel As Integer
		Public RT As Double
		Public BPI As Double
		Public TIC As Double
		Public AcqTime As System.DateTime
		Public SpotNumber As String		 ' Only used with MALDI imaging
	End Structure

	Protected Enum eMcfMetadataFields
		ScanMode = 0
		MSLevel = 1
		RT = 2
		BPI = 3
		TIC = 4
		AcqTime = 5
		SpotNumber = 6
	End Enum

	Protected Sub AddDatasetScan(ByVal intScanNumber As Integer, ByVal intMSLevel As Integer, ByVal sngElutionTime As Single, ByVal dblBPI As Double, ByVal dblTIC As Double, ByVal strScanTypeName As String, ByRef dblMaxRunTimeMinutes As Double)

		If mSaveTICAndBPI AndAlso intScanNumber > 0 Then
			mTICandBPIPlot.AddData(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC)
		End If

		Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry
		objScanStatsEntry.ScanNumber = intScanNumber
		objScanStatsEntry.ScanType = intMSLevel

		objScanStatsEntry.ScanTypeName = strScanTypeName
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

	End Sub

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

	Protected Function GetMetaDataFieldAndTable(ByVal eMcfMetadataField As eMcfMetadataFields, ByRef strField As String, ByRef strTable As String) As Boolean

		Select Case eMcfMetadataField
			Case eMcfMetadataFields.ScanMode
				strField = "pScanMode"
				strTable = "MetaDataInt"

			Case eMcfMetadataFields.MSLevel
				strField = "pMSLevel"
				strTable = "MetaDataInt"

			Case eMcfMetadataFields.RT
				strField = "pRT"
				strTable = "MetaDataDouble"

			Case eMcfMetadataFields.BPI
				strField = "pIntMax"
				strTable = "MetaDataDouble"

			Case eMcfMetadataFields.TIC
				strField = "pTic"
				strTable = "MetaDataDouble"

			Case eMcfMetadataFields.AcqTime
				strField = "pDateTime"
				strTable = "MetaDataString"

			Case eMcfMetadataFields.SpotNumber
				strField = "pSpotNo"
				strTable = "MetaDataString"

			Case Else
				' Unknown field
				strField = String.Empty
				strTable = String.Empty
				Return False
		End Select

		Return True
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

	Protected Function ParseMcfIndexFiles(ByVal ioDatasetFolder As System.IO.DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

		Dim strMetadataFile As String
		Dim strConnectionString As String

		Dim ioFileInfo As System.IO.FileInfo

		Dim blnSuccess As Boolean = False
		Dim intMetadataId As Integer
		Dim strMetadataName As String
		Dim strMetadataDescription As String

		Dim lstMetadataNameToID As Generic.Dictionary(Of String, Integer)
		Dim lstMetadataNameToDescription As Generic.Dictionary(Of String, String)

		Dim lstScanData As Generic.Dictionary(Of String, udtMCFScanInfoType)

		Dim intScanCount As Integer = 0
		Dim intScanNumber As Integer
		Dim sngElutionTime As Single
		Dim strScanTypeName As String

		Dim dblMaxRunTimeMinutes As Double = 0

		Dim dtAcqTimeStart As System.DateTime = System.DateTime.MaxValue
		Dim dtAcqTimeEnd As System.DateTime = System.DateTime.MinValue

		Try

			lstMetadataNameToID = New Generic.Dictionary(Of String, Integer)(StringComparer.CurrentCultureIgnoreCase)
			lstMetadataNameToDescription = New Generic.Dictionary(Of String, String)
			lstScanData = New Generic.Dictionary(Of String, udtMCFScanInfoType)

			If mSaveTICAndBPI Then
				' Initialize the TIC and BPI arrays
				MyBase.InitializeTICAndBPI()
			End If

			strMetadataFile = System.IO.Path.Combine(ioDatasetFolder.FullName, BRUKER_SQLITE_INDEX_FILE_NAME)
			ioFileInfo = New System.IO.FileInfo(strMetadataFile)

			If Not ioFileInfo.Exists Then
				' Storage.mcf_idx not found
				ShowMessage("Note: " & BRUKER_SQLITE_INDEX_FILE_NAME & " file does not exist")
				Return False
			Else

				strConnectionString = "Data Source = " + ioFileInfo.FullName + "; Version=3; DateTimeFormat=Ticks;"

				' Open the Storage.mcf_idx file to lookup the metadata name to ID mapping
				Using cnDB As System.Data.SQLite.SQLiteConnection = New System.Data.SQLite.SQLiteConnection(strConnectionString)
					cnDB.Open()

					Dim cmd As System.Data.SQLite.SQLiteCommand

					cmd = New System.Data.SQLite.SQLiteCommand(cnDB)

					cmd.CommandText = "SELECT metadataId, permanentName, displayName FROM MetadataId"

					Using drReader As System.Data.SQLite.SQLiteDataReader = cmd.ExecuteReader()
						While drReader.Read()

							intMetadataId = ReadDbInt(drReader, "metadataId")
							strMetadataName = ReadDbString(drReader, "permanentName")
							strMetadataDescription = ReadDbString(drReader, "displayName")

							If intMetadataId > 0 Then
								lstMetadataNameToID.Add(strMetadataName, intMetadataId)
								lstMetadataNameToDescription.Add(strMetadataName, strMetadataDescription)
							End If
						End While
					End Using

					cnDB.Close()
				End Using

				Dim fiFiles() As System.IO.FileInfo

				fiFiles = ioDatasetFolder.GetFiles("*_1.mcf_idx")

				If fiFiles.Length = 0 Then
					' Storage.mcf_idx not found
					ShowMessage("Note: " & BRUKER_SQLITE_INDEX_FILE_NAME & " file was found but _1.mcf_idx file does not exist")
					Return False
				Else

					strConnectionString = "Data Source = " + fiFiles(0).FullName + "; Version=3; DateTimeFormat=Ticks;"

					' Open the .mcf file to read the scan info
					Using cnDB As System.Data.SQLite.SQLiteConnection = New System.Data.SQLite.SQLiteConnection(strConnectionString)
						cnDB.Open()

						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.AcqTime)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.ScanMode)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.MSLevel)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.RT)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.BPI)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.TIC)
						ReadAndStoreMcfIndexData(cnDB, lstMetadataNameToID, lstScanData, eMcfMetadataFields.SpotNumber)

						cnDB.Close()
					End Using


					' Parse each entry in lstScanData
					' Copy the values to a generic list so that we can sort them
					Dim oScanDataSorted() As udtMCFScanInfoType
					ReDim oScanDataSorted(lstScanData.Count - 1)
					lstScanData.Values.CopyTo(oScanDataSorted, 0)

					Dim oScanDataSortComparer As New clsScanDataSortComparer
					Array.Sort(oScanDataSorted, oScanDataSortComparer)

					intScanCount = 0
					For intIndex As Integer = 0 To oScanDataSorted.Length - 1
						intScanCount += 1
						intScanNumber = intScanCount

						If oScanDataSorted(intIndex).AcqTime < dtAcqTimeStart Then
							If oScanDataSorted(intIndex).AcqTime > System.DateTime.MinValue Then
								dtAcqTimeStart = oScanDataSorted(intIndex).AcqTime
							End If
						End If

						If oScanDataSorted(intIndex).AcqTime > dtAcqTimeEnd Then
							If oScanDataSorted(intIndex).AcqTime < System.DateTime.MaxValue Then
								dtAcqTimeEnd = oScanDataSorted(intIndex).AcqTime
							End If
						End If

						If oScanDataSorted(intIndex).MSLevel = 0 Then oScanDataSorted(intIndex).MSLevel = 1
						sngElutionTime = CSng(oScanDataSorted(intIndex).RT / 60.0)

						With oScanDataSorted(intIndex)
							If String.IsNullOrEmpty(.SpotNumber) Then
								strScanTypeName = "HMS"
							Else
								strScanTypeName = "MALDI-HMS"
							End If

							AddDatasetScan(intScanNumber, .MSLevel, sngElutionTime, .BPI, .TIC, strScanTypeName, dblMaxRunTimeMinutes)
						End With

					Next

					If intScanCount > 0 Then
						udtFileInfo.ScanCount = intScanCount

						If dblMaxRunTimeMinutes > 0 Then
							udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblMaxRunTimeMinutes)
						End If

						If dtAcqTimeStart > System.DateTime.MinValue AndAlso dtAcqTimeEnd < System.DateTime.MaxValue Then
							' Update the acquisition times if they are within 7 days of udtFileInfo.AcqTimeEnd
							If Math.Abs(udtFileInfo.AcqTimeEnd.Subtract(dtAcqTimeEnd).TotalDays) <= 7 Then
								udtFileInfo.AcqTimeStart = dtAcqTimeStart
								udtFileInfo.AcqTimeEnd = dtAcqTimeEnd
							End If
							
						End If

						blnSuccess = True
					End If

				End If

			End If
		Catch ex As System.Exception
			' Error finding Scan.xml file
			ReportError("Error parsing " & BRUKER_SQLITE_INDEX_FILE_NAME & "file: " & ex.Message)
			blnSuccess = False
		End Try

		Return blnSuccess

	End Function

	Protected Function ParseScanXMLFile(ByVal ioDatasetFolder As System.IO.DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

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

								AddDatasetScan(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC, "HMS", dblMaxRunTimeMinutes)

							End If
					End Select

				Loop

				srReader.Close()

				If intScanCount > 0 Then
					udtFileInfo.ScanCount = intScanCount

					If dblMaxRunTimeMinutes > 0 Then
						udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblMaxRunTimeMinutes)
					End If

					blnSuccess = True


				End If

			End If
		Catch ex As System.Exception
			' Error finding Scan.xml file
			ReportError("Error parsing " & BRUKER_SCANINFO_XML_FILE & "file: " & ex.Message)
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
				ReportError("File/folder not found: " & strDataFilePath)
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
				'.baf files not found; look for any .mcf files
				ioFiles = ioDatasetFolder.GetFiles("*" & BRUKER_MCF_FILE_EXTENSION)

				If ioFiles.Length > 0 Then
					' Find the largest .mcf file (not .mcf_idx file)
					Dim ioLargestMCF As System.IO.FileInfo = Nothing

					For Each ioMCFFile As System.IO.FileInfo In ioFiles
						If ioMCFFile.Extension.ToUpper() = BRUKER_MCF_FILE_EXTENSION Then
							If ioLargestMCF Is Nothing Then
								ioLargestMCF = ioMCFFile
							ElseIf ioMCFFile.Length > ioLargestMCF.Length Then
								ioLargestMCF = ioMCFFile
							End If
						End If
					Next

					If ioLargestMCF Is Nothing Then
						' Didn't actually find a .MCF file; clear ioFiles
						ReDim ioFiles(-1)
					Else
						ioFiles(0) = ioLargestMCF
					End If
				End If
			End If

			If ioFiles Is Nothing OrElse ioFiles.Length = 0 Then
				ReportError(BRUKER_BAF_FILE_EXTENSION & " or " & BRUKER_EXTENSION_BAF_FILE_NAME & " or " & BRUKER_MCF_FILE_EXTENSION & " or " & BRUKER_SQLITE_INDEX_EXTENSION & " file not found in " & ioDatasetFolder.FullName)
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

				' Look for the Storage.mcf_idx file and the corresponding .mcf_idx file
				' If they exist, then we can extract information from them using SqLite
				blnSuccess = ParseMcfIndexFiles(ioDatasetFolder, udtFileInfo)

				If Not blnSuccess Then
					' Parse the scan.xml file (if it exists) to determine the number of spectra acquired
					' We can also obtain TIC and elution time values from this file
					' However, it does not track whether a scan is MS or MSn
					' If the scans.xml file contains runtime entries (e.g. <minutes>100.0456</minutes>) then .AcqTimeEnd is updated using .AcqTimeStart + RunTimeMinutes
					blnSuccess = ParseScanXMLFile(ioDatasetFolder, udtFileInfo)

					If Not blnSuccess Then
						' Use ProteoWizard to extract the scan counts and acquisition time information
						blnSuccess = ParseBAFFile(ioFileInfo, udtFileInfo)
					End If

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

	Protected Function ReadAndStoreMcfIndexData(ByVal cnDB As System.Data.SQLite.SQLiteConnection, _
	  ByVal lstMetadataNameToID As Generic.Dictionary(Of String, Integer), _
	  ByRef lstScanData As Generic.Dictionary(Of String, udtMCFScanInfoType), _
	  ByVal eMcfMetadataField As eMcfMetadataFields) As Boolean

		Dim cmd As System.Data.SQLite.SQLiteCommand
		cmd = New System.Data.SQLite.SQLiteCommand(cnDB)

		Dim strTable As String = String.Empty
		Dim strField As String = String.Empty

		Dim intMetadataId As Integer
		Dim strValue As String
		Dim strGuid As String

		Dim blnNewEntry As Boolean

		If Not GetMetaDataFieldAndTable(eMcfMetadataField, strField, strTable) Then
			Return False
		End If

		If lstMetadataNameToID.TryGetValue(strField, intMetadataId) Then

			cmd.CommandText = "SELECT GuidA, MetaDataId, Value FROM " & strTable & " WHERE MetaDataId = " & intMetadataId

			Using drReader As System.Data.SQLite.SQLiteDataReader = cmd.ExecuteReader()
				While drReader.Read()

					strGuid = ReadDbString(drReader, "GuidA")
					strValue = ReadDbString(drReader, "Value")

					Dim udtScanInfo As udtMCFScanInfoType = Nothing
					If lstScanData.TryGetValue(strGuid, udtScanInfo) Then
						blnNewEntry = False
					Else
						udtScanInfo = New udtMCFScanInfoType
						blnNewEntry = True
					End If

					UpdateScanInfo(eMcfMetadataField, strValue, udtScanInfo)

					If blnNewEntry Then
						lstScanData.Add(strGuid, udtScanInfo)
					Else
						lstScanData(strGuid) = udtScanInfo
					End If

				End While
			End Using

		End If

		Return True

	End Function

	Protected Function ReadDbString(ByVal drReader As System.Data.SQLite.SQLiteDataReader, ByVal strColumnName As String) As String
		Return ReadDbString(drReader, strColumnName, strValueIfNotFound:=String.Empty)
	End Function

	Protected Function ReadDbString(ByVal drReader As System.Data.SQLite.SQLiteDataReader, ByVal strColumnName As String, ByVal strValueIfNotFound As String) As String
		Dim strValue As String

		Try
			strValue = drReader(strColumnName).ToString()
			If strValue Is Nothing Then
				strValue = strValueIfNotFound
			End If

		Catch ex As Exception
			strValue = strValueIfNotFound
		End Try

		Return strValue
	End Function

	Protected Function ReadDbInt(ByVal drReader As System.Data.SQLite.SQLiteDataReader, ByVal strColumnName As String) As Integer
		Dim intValue As Integer
		Dim strValue As String

		Try
			strValue = drReader(strColumnName).ToString()
			If Not String.IsNullOrEmpty(strValue) Then
				If Integer.TryParse(strValue, intValue) Then
					Return intValue
				End If
			End If

		Catch ex As Exception
			' Ignore errors here
		End Try

		Return 0

	End Function

	Private Sub UpdateScanInfo(ByVal eMcfMetadataField As eMcfMetadataFields, ByVal strValue As String, ByRef udtScanInfo As udtMCFScanInfoType)

		Dim intValue As Integer
		Dim dblValue As Double
		Dim dtValue As System.DateTime

		Select Case eMcfMetadataField
			Case eMcfMetadataFields.ScanMode
				If Integer.TryParse(strValue, intValue) Then
					udtScanInfo.ScanMode = intValue
				End If

			Case eMcfMetadataFields.MSLevel
				If Integer.TryParse(strValue, intValue) Then
					udtScanInfo.MSLevel = intValue
				End If

			Case eMcfMetadataFields.RT
				If Double.TryParse(strValue, dblValue) Then
					udtScanInfo.RT = dblValue
				End If

			Case eMcfMetadataFields.BPI
				If Double.TryParse(strValue, dblValue) Then
					udtScanInfo.BPI = dblValue
				End If

			Case eMcfMetadataFields.TIC
				If Double.TryParse(strValue, dblValue) Then
					udtScanInfo.TIC = dblValue
				End If

			Case eMcfMetadataFields.AcqTime
				If DateTime.TryParse(strValue, dtValue) Then
					udtScanInfo.AcqTime = dtValue
				End If

			Case eMcfMetadataFields.SpotNumber
				udtScanInfo.SpotNumber = strValue
			Case Else
				' Unknown field
		End Select

	End Sub

	Private Sub mPWizParser_ErrorEvent(Message As String) Handles mPWizParser.ErrorEvent
		ReportError(Message)
	End Sub

	Private Sub mPWizParser_MessageEvent(Message As String) Handles mPWizParser.MessageEvent
		ShowMessage(Message)
	End Sub

	Protected Class clsScanDataSortComparer
		Implements System.Collections.Generic.IComparer(Of udtMCFScanInfoType)

		Public Function Compare(x As udtMCFScanInfoType, y As udtMCFScanInfoType) As Integer Implements System.Collections.Generic.IComparer(Of udtMCFScanInfoType).Compare

			If x.RT < y.RT Then
				Return -1
			ElseIf x.RT > y.RT Then
				Return 1
			Else
				If x.AcqTime < y.AcqTime Then
					Return -1
				ElseIf x.AcqTime > y.AcqTime Then
					Return 1
				Else
					If String.IsNullOrEmpty(x.SpotNumber) OrElse String.IsNullOrEmpty(y.SpotNumber) Then
						Return 0
					Else
						If x.SpotNumber < y.SpotNumber Then
							Return -1
						ElseIf x.SpotNumber > y.SpotNumber Then
							Return 1
						Else
							Return 0
						End If
					End If

				End If
			End If

		End Function
	End Class

End Class

