Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified September 14, 2015
Imports System.Runtime.InteropServices
Imports PNNLOmics.Utilities
Imports System.Data

<CLSCompliant(False)>
Public Class clsBrukerXmassFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    Public Const BRUKER_BAF_FILE_NAME As String = "analysis.baf"
    Public Const BRUKER_SER_FILE_NAME As String = "ser"
    Public Const BRUKER_FID_FILE_NAME As String = "fid"
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
        Public AcqTime As DateTime
        Public SpotNumber As String      ' Only used with MALDI imaging
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

    Protected Sub AddDatasetScan(intScanNumber As Integer, intMSLevel As Integer, sngElutionTime As Single, dblBPI As Double, dblTIC As Double, strScanTypeName As String, ByRef dblMaxRunTimeMinutes As Double)

        If mSaveTICAndBPI AndAlso intScanNumber > 0 Then
            mTICandBPIPlot.AddData(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC)
        End If

        Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry
        objScanStatsEntry.ScanNumber = intScanNumber
        objScanStatsEntry.ScanType = intMSLevel

        objScanStatsEntry.ScanTypeName = strScanTypeName
        objScanStatsEntry.ScanFilterText = ""

        objScanStatsEntry.ElutionTime = sngElutionTime.ToString("0.0000")
        objScanStatsEntry.TotalIonIntensity = StringUtilities.ValueToString(dblTIC, 5)
        objScanStatsEntry.BasePeakIntensity = StringUtilities.ValueToString(dblBPI, 5)
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
    ''' <param name="diDatasetFolder"></param>
    ''' <param name="udtFileInfo"></param>
    ''' <returns>True if a valid file is found; otherwise false</returns>
    ''' <remarks></remarks>
    Protected Function DetermineAcqStartTime(diDatasetFolder As DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim blnSuccess As Boolean = False

        Dim diSubFolders As List(Of DirectoryInfo)

        Try
            ' Look for the method folder (folder name should end in .m)
            diSubFolders = diDatasetFolder.GetDirectories("*.m").ToList()

            If diSubFolders.Count = 0 Then
                ' Match not found
                ' Look for any XMass folders
                diSubFolders = diDatasetFolder.GetDirectories("XMass*").ToList()
            End If

            If diSubFolders.Count > 0 Then
                ' Look for the apexAcquisition.method in each matching subfolder
                ' We have historically used that file's modification time as the acquisition start time for the dataset
                ' However, we've found that on the 12T a series of datasets will all use the same method file and thus the modification time is not necessarily appropriate

                ' Note that the submethods.xml file sometimes gets modified after the run starts, so it should not be used to determine run start time

                For Each diSubFolder In diSubFolders
                    For Each fiFile In diSubFolder.GetFiles("apexAcquisition.method")
                        udtFileInfo.AcqTimeStart = fiFile.LastWriteTime
                        blnSuccess = True
                        Exit For
                    Next
                    If blnSuccess Then Exit For
                Next

                If Not blnSuccess Then
                    ' apexAcquisition.method not found; try submethods.xml instead
                    For Each diSubFolder In diSubFolders
                        For Each fiFile In diSubFolder.GetFiles("submethods.xml")
                            udtFileInfo.AcqTimeStart = fiFile.LastWriteTime
                            blnSuccess = True
                            Exit For
                        Next
                        If blnSuccess Then Exit For
                    Next
                End If

            End If

            ' Also look for the .hdx file
            ' Its file modification time typically also matches the run start time

            For Each fiFile In diDatasetFolder.GetFiles("*.hdx")
                If Not blnSuccess OrElse fiFile.LastWriteTime < udtFileInfo.AcqTimeStart Then
                    udtFileInfo.AcqTimeStart = fiFile.LastWriteTime
                End If

                blnSuccess = True
                Exit For
            Next

            ' Make sure AcqTimeEnd and AcqTimeStart match
            udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart

        Catch ex As Exception
            ReportError("Error finding XMass method folder: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function FindBrukerSettingsFile(diDotDFolder As DirectoryInfo) As FileInfo

        Dim dotMethodFiles = diDotDFolder.GetFiles("*.method", SearchOption.AllDirectories)

        If dotMethodFiles Is Nothing OrElse dotMethodFiles.Length = 0 Then
            Return Nothing
        End If

        Dim acquistionMethodFiles = (From methodFile In dotMethodFiles Where methodFile.Name.ToLower().EndsWith("apexacquisition.method") Select methodFile).ToList()

        If acquistionMethodFiles.Count = 0 Then
            Return Nothing
        End If

        If acquistionMethodFiles.Count = 1 Then
            Return acquistionMethodFiles.First
        End If

        ReportError("Multiple 'apexAcquisition.method' files were found in the .D folder; not sure which to use")
        Return Nothing

    End Function

    Private Function FindBrukerAcqusFile(diDotDFolder As DirectoryInfo) As FileInfo

        Dim acqusFiles = diDotDFolder.GetFiles("acqus", SearchOption.AllDirectories)

        If acqusFiles Is Nothing OrElse acqusFiles.Length = 0 Then
            Return Nothing
        End If

        If acqusFiles.Length = 1 Then
            Return acqusFiles.First
        End If

        ' Often the Bruker file structures contain multiple Acqus files. I will select 
        ' the one that is in the same folder as the 'ser' file and if that isn't present,
        ' the same folder as the 'fid' file. Otherwise, throw errors


        For Each acquFile In acqusFiles
            If acquFile.Directory.Name.Equals(diDotDFolder.Name, StringComparison.OrdinalIgnoreCase) Then
                Return acquFile
            End If
        Next

        ReportError("Multiple 'acqus' files were found in the .D folder; not sure which one to use")
        Return Nothing

    End Function

    Protected Function GetMetaDataFieldAndTable(eMcfMetadataField As eMcfMetadataFields, ByRef strField As String, ByRef strTable As String) As Boolean

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

    Protected Function ParseAutoMSFile(
       diDatasetFolder As DirectoryInfo,
      ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim strAutoMSFilePath As String

        Dim strLineIn As String
        Dim strSplitLine() As String

        Dim intScanNumber As Integer
        Dim intMSLevel As Integer
        Dim strScanTypeName As String

        Try

            strAutoMSFilePath = Path.Combine(diDatasetFolder.FullName, BRUKER_AUTOMS_FILE)
            Dim fiFileInfo = New FileInfo(strAutoMSFilePath)

            If Not fiFileInfo.Exists Then
                Return False
            End If

            Using srReader = New StreamReader(New FileStream(fiFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))

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

            End Using

            Return True

        Catch ex As Exception
            ReportError("Error finding AutoMS.txt file: " & ex.Message)
            Return False
        End Try

    End Function

    Protected Function ParseBAFFile(fiBAFFileInfo As FileInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim blnSuccess As Boolean

        Dim blnTICStored As Boolean = False
        Dim blnSRMDataCached As Boolean = False

        ' Override strDataFilePath here, if needed
        Dim blnOverride As Boolean = False
        If blnOverride Then
            Dim strNewDataFilePath As String = "c:\temp\analysis.baf"
            fiBAFFileInfo = New FileInfo(strNewDataFilePath)
        End If

        mDatasetStatsSummarizer.ClearCachedData()
        mLCMS2DPlot.Options.UseObservedMinScan = False

        Try
            If fiBAFFileInfo.Length > 1024 * 1024 * 1024 Then
                ShowMessage("analysis.baf file is over 1 GB; ProteoWizard typically cannot handle .baf files this large")

                ' Look for a ser file
                If File.Exists(Path.Combine(fiBAFFileInfo.Directory.FullName, "ser")) Then
                    ShowMessage("Will parse the ser file instead")
                    Return False
                Else
                    ShowMessage("Ser file not found; trying ProteoWizard anyway")
                End If

            End If

            ' Open the analysis.baf (or extension.baf) file using the ProteoWizardWrapper
            ShowMessage("Determining acquisition info using Proteowizard (this could take a while)")


            Dim objPWiz As pwiz.ProteowizardWrapper.MSDataFileReader
            objPWiz = New pwiz.ProteowizardWrapper.MSDataFileReader(fiBAFFileInfo.FullName)

            Try
                Dim dtRunStartTime As DateTime = CDate(objPWiz.RunStartTime())

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
            mPWizParser = New clsProteowizardDataParser(
              objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot, mLCMS2DPlot,
              mSaveLCMS2DPlots, mSaveTICAndBPI, mCheckCentroidingStatus)

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

            objPWiz.Dispose()
            PRISM.Processes.clsProgRunner.GarbageCollectNow()

            blnSuccess = True
        Catch ex As Exception
            ReportError("Error using ProteoWizard reader: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Protected Function ParseMcfIndexFiles(diDatasetFolder As DirectoryInfo, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Try

            Dim lstMetadataNameToID = New Dictionary(Of String, Integer)(StringComparer.CurrentCultureIgnoreCase)
            Dim lstMetadataNameToDescription = New Dictionary(Of String, String)
            Dim lstScanData = New Dictionary(Of String, udtMCFScanInfoType)

            If mSaveTICAndBPI Then
                ' Initialize the TIC and BPI arrays
                MyBase.InitializeTICAndBPI()
            End If

            If mSaveLCMS2DPlots Then
                MyBase.InitializeLCMS2DPlot()
            End If

            Dim strMetadataFile = Path.Combine(diDatasetFolder.FullName, BRUKER_SQLITE_INDEX_FILE_NAME)
            Dim fiFileInfo = New FileInfo(strMetadataFile)

            If Not fiFileInfo.Exists Then
                ' Storage.mcf_idx not found
                ShowMessage("Note: " & BRUKER_SQLITE_INDEX_FILE_NAME & " file does not exist")
                Return False
            End If

            Dim strConnectionString = "Data Source = " + fiFileInfo.FullName + "; Version=3; DateTimeFormat=Ticks;"

            ' Open the Storage.mcf_idx file to lookup the metadata name to ID mapping
            Using cnDB = New SQLite.SQLiteConnection(strConnectionString, True)
                cnDB.Open()

                Dim cmd = New SQLite.SQLiteCommand(cnDB)

                cmd.CommandText = "SELECT metadataId, permanentName, displayName FROM MetadataId"

                Using drReader As SQLite.SQLiteDataReader = cmd.ExecuteReader()
                    While drReader.Read()

                        Dim intMetadataId = ReadDbInt(drReader, "metadataId")
                        Dim strMetadataName = ReadDbString(drReader, "permanentName")
                        Dim strMetadataDescription = ReadDbString(drReader, "displayName")

                        If intMetadataId > 0 Then
                            lstMetadataNameToID.Add(strMetadataName, intMetadataId)
                            lstMetadataNameToDescription.Add(strMetadataName, strMetadataDescription)
                        End If
                    End While
                End Using

                cnDB.Close()
            End Using

            Dim fiFiles = diDatasetFolder.GetFiles("*_1.mcf_idx").ToList()

            If fiFiles.Count = 0 Then
                ' Storage.mcf_idx not found
                ShowMessage("Note: " & BRUKER_SQLITE_INDEX_FILE_NAME & " file was found but _1.mcf_idx file does not exist")
                Return False
            End If

            strConnectionString = "Data Source = " + fiFiles.First.FullName + "; Version=3; DateTimeFormat=Ticks;"

            ' Open the .mcf file to read the scan info
            Using cnDB = New SQLite.SQLiteConnection(strConnectionString, True)
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

            Dim dtAcqTimeStart As DateTime = DateTime.MaxValue
            Dim dtAcqTimeEnd As DateTime = DateTime.MinValue

            Dim intScanCount = 0
            Dim dblMaxRunTimeMinutes As Double = 0

            For intIndex = 0 To oScanDataSorted.Length - 1
                intScanCount += 1
                Dim intScanNumber = intScanCount

                If oScanDataSorted(intIndex).AcqTime < dtAcqTimeStart Then
                    If oScanDataSorted(intIndex).AcqTime > DateTime.MinValue Then
                        dtAcqTimeStart = oScanDataSorted(intIndex).AcqTime
                    End If
                End If

                If oScanDataSorted(intIndex).AcqTime > dtAcqTimeEnd Then
                    If oScanDataSorted(intIndex).AcqTime < DateTime.MaxValue Then
                        dtAcqTimeEnd = oScanDataSorted(intIndex).AcqTime
                    End If
                End If

                If oScanDataSorted(intIndex).MSLevel = 0 Then oScanDataSorted(intIndex).MSLevel = 1
                Dim sngElutionTime = CSng(oScanDataSorted(intIndex).RT / 60.0)
                Dim strScanTypeName As String

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
                    udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblMaxRunTimeMinutes)
                End If

                If dtAcqTimeStart > DateTime.MinValue AndAlso dtAcqTimeEnd < DateTime.MaxValue Then
                    ' Update the acquisition times if they are within 7 days of udtFileInfo.AcqTimeEnd
                    If Math.Abs(udtFileInfo.AcqTimeEnd.Subtract(dtAcqTimeEnd).TotalDays) <= 7 Then
                        udtFileInfo.AcqTimeStart = dtAcqTimeStart
                        udtFileInfo.AcqTimeEnd = dtAcqTimeEnd
                    End If

                End If

                Return True
            End If

        Catch ex As Exception
            ' Error parsing Storage.mcf_idx file
            ReportError("Error parsing " & BRUKER_SQLITE_INDEX_FILE_NAME & " file: " & ex.Message)
            Return False
        End Try

        Return False

    End Function

    Protected Function ParseScanXMLFile(
        diDatasetFolder As DirectoryInfo,
       ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType,
       <Out> ByRef scanElutionTimeMap As Dictionary(Of Integer, Single)) As Boolean

        scanElutionTimeMap = New Dictionary(Of Integer, Single)

        Try

            If mSaveTICAndBPI Then
                ' Initialize the TIC and BPI arrays
                MyBase.InitializeTICAndBPI()
            End If

            Dim strScanXMLFilePath = Path.Combine(diDatasetFolder.FullName, BRUKER_SCANINFO_XML_FILE)
            Dim fiFileInfo = New FileInfo(strScanXMLFilePath)

            If Not fiFileInfo.Exists Then
                Return False
            End If

            Dim intScanCount = 0
            Dim dblMaxRunTimeMinutes As Double = 0
            Dim validFile = False

            Using srReader = New Xml.XmlTextReader(New FileStream(fiFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))

                Dim blnSkipRead = False
                Dim blnInScanNode = False

                Dim intScanNumber As Integer
                Dim sngElutionTime As Single
                Dim dblTIC As Double
                Dim dblBPI As Double
                Dim intMSLevel As Integer

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
                                If srReader.Name = "scanlist" Then
                                    validFile = True
                                ElseIf srReader.Name = "scan" Then
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

                                scanElutionTimeMap.Add(intScanNumber, sngElutionTime)
                                AddDatasetScan(intScanNumber, intMSLevel, sngElutionTime, dblBPI, dblTIC, "HMS", dblMaxRunTimeMinutes)

                            End If
                    End Select

                Loop

            End Using

            If intScanCount > 0 Then
                udtFileInfo.ScanCount = intScanCount

                If dblMaxRunTimeMinutes > 0 Then
                    If Math.Abs(udtFileInfo.AcqTimeEnd.Subtract(udtFileInfo.AcqTimeStart).TotalMinutes) < dblMaxRunTimeMinutes Then
                        udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblMaxRunTimeMinutes)
                    Else
                        udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.AddMinutes(-dblMaxRunTimeMinutes)
                    End If
                End If

                Return True
            ElseIf validFile Then
                ' The XML file is valid, but no scans were listed; must be a bad dataset
                ' Return true because there is no point in opening this dataset with ProteoWizard
                Return True
            End If

        Catch ex As Exception
            ' Error parsing the Scan.xml file
            ReportError("Error parsing " & BRUKER_SCANINFO_XML_FILE & " file: " & ex.Message)
            Return False
        End Try

        Return False

    End Function

    Protected Function GetDatasetFolder(strDataFilePath As String) As DirectoryInfo

        ' First see if strFileOrFolderPath points to a valid file
        Dim fiFileInfo = New FileInfo(strDataFilePath)

        If fiFileInfo.Exists() Then
            ' User specified a file; assume the parent folder of this file is the dataset folder
            Return fiFileInfo.Directory
        Else
            ' Assume this is the path to the dataset folder
            Return New DirectoryInfo(strDataFilePath)
        End If

    End Function

    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        Dim strDatasetName As String = String.Empty

        Try
            ' The dataset name for a Bruker Xmass folder is the name of the parent directory
            ' However, strDataFilePath could be a file or a folder path, so use GetDatasetFolder to get the dataset folder
            Dim diDatasetFolder = GetDatasetFolder(strDataFilePath)
            strDatasetName = diDatasetFolder.Name

            If strDatasetName.ToLower().EndsWith(".d") Then
                strDatasetName = strDatasetName.Substring(0, strDatasetName.Length - 2)
            End If

        Catch ex As Exception
            ' Ignore errors
        End Try

        If strDatasetName Is Nothing Then strDatasetName = String.Empty
        Return strDatasetName

    End Function

    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Process a Bruker Xmass folder, specified by strDataFilePath (which can either point to the dataset folder containing the XMass files, or any of the XMass files in the dataset folder)

        Dim fiFileInfo As FileInfo
        Dim diDatasetFolder As DirectoryInfo

        Dim blnSuccess As Boolean

        Try
            ' Determine whether strDataFilePath points to a file or a folder

            diDatasetFolder = GetDatasetFolder(strDataFilePath)

            ' Validate that we have selected a valid folder
            If Not diDatasetFolder.Exists Then
                ReportError("File/folder not found: " & strDataFilePath)
                Return False
            End If

            ' In case we cannot find a .BAF file, update the .AcqTime values to the folder creation date
            ' We have to assign a date, so we'll assign the date for the BAF file
            With udtFileInfo
                .AcqTimeStart = diDatasetFolder.CreationTime
                .AcqTimeEnd = diDatasetFolder.CreationTime
            End With

            ' Look for the analysis.baf file in diFolderInfo
            ' Use its modification time as the AcqTime start and End values
            ' If we cannot find the anslysis.baf file, then look for a ser file or a fid file

            Dim lstInstrumentDataFiles = New List(Of String) From {BRUKER_BAF_FILE_NAME, BRUKER_SER_FILE_NAME, BRUKER_FID_FILE_NAME, BRUKER_EXTENSION_BAF_FILE_NAME}
            Dim fiFiles = New List(Of FileInfo)

            For Each instrumentDataFile In lstInstrumentDataFiles
                fiFiles = diDatasetFolder.GetFiles(instrumentDataFile).ToList()
                If Not fiFiles Is Nothing AndAlso fiFiles.Count > 0 Then
                    Exit For
                End If
            Next

            If fiFiles Is Nothing OrElse fiFiles.Count = 0 Then
                '.baf files not found; look for any .mcf files
                fiFiles = diDatasetFolder.GetFiles("*" & BRUKER_MCF_FILE_EXTENSION).ToList()

                If fiFiles.Count > 0 Then
                    ' Find the largest .mcf file (not .mcf_idx file)
                    Dim fiLargestMCF As FileInfo = Nothing

                    For Each fiMCFFile As FileInfo In fiFiles
                        If fiMCFFile.Extension.ToUpper() = BRUKER_MCF_FILE_EXTENSION Then
                            If fiLargestMCF Is Nothing Then
                                fiLargestMCF = fiMCFFile
                            ElseIf fiMCFFile.Length > fiLargestMCF.Length Then
                                fiLargestMCF = fiMCFFile
                            End If
                        End If
                    Next

                    If fiLargestMCF Is Nothing Then
                        ' Didn't actually find a .MCF file; clear fiFiles
                        fiFiles.Clear()
                    Else
                        fiFiles.Clear()
                        fiFiles.Add(fiLargestMCF)
                    End If
                End If
            End If

            If fiFiles Is Nothing OrElse fiFiles.Count = 0 Then
                ReportError(String.Join(" or ", lstInstrumentDataFiles) & " or " & BRUKER_MCF_FILE_EXTENSION & " or " & BRUKER_SQLITE_INDEX_EXTENSION & " file not found in " & diDatasetFolder.FullName)
                blnSuccess = False
            Else
                fiFileInfo = fiFiles.First

                ' Read the file info from the file system
                ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
                UpdateDatasetFileStats(fiFileInfo, udtFileInfo.DatasetID)

                ' Update the dataset name and file extension
                udtFileInfo.DatasetName = GetDatasetNameViaPath(diDatasetFolder.FullName)
                udtFileInfo.FileExtension = String.Empty
                udtFileInfo.FileSizeBytes = fiFileInfo.Length

                ' Find the apexAcquisition.method or submethods.xml file in the XMASS_Method.m subfolder to determine .AcqTimeStart
                ' This function updates udtFileInfo.AcqTimeEnd and udtFileInfo.AcqTimeStart to have the same time
                blnSuccess = DetermineAcqStartTime(diDatasetFolder, udtFileInfo)

                ' Update the acquisition end time using the write time of the .baf file
                If fiFileInfo.LastWriteTime > udtFileInfo.AcqTimeEnd Then
                    udtFileInfo.AcqTimeEnd = fiFileInfo.LastWriteTime

                    If udtFileInfo.AcqTimeEnd.Subtract(udtFileInfo.AcqTimeStart).TotalMinutes > 60 Then
                        ' Update the start time to match the end time to prevent accidentally reporting an inaccurately long acquisition length
                        udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
                    End If
                End If

                ' Look for the Storage.mcf_idx file and the corresponding .mcf_idx file
                ' If they exist, we can extract information from them using SqLite
                blnSuccess = ParseMcfIndexFiles(diDatasetFolder, udtFileInfo)

                If Not blnSuccess Then
                    Dim scanElutionTimeMap As Dictionary(Of Integer, Single) = Nothing

                    ' Parse the scan.xml file (if it exists) to determine the number of spectra acquired
                    ' We can also obtain TIC and elution time values from this file
                    ' However, it does not track whether a scan is MS or MSn
                    ' If the scans.xml file contains runtime entries (e.g. <minutes>100.0456</minutes>) then .AcqTimeEnd is updated using .AcqTimeStart + RunTimeMinutes
                    blnSuccess = ParseScanXMLFile(diDatasetFolder, udtFileInfo, scanElutionTimeMap)

                    Dim bafFileParsed As Boolean

                    If Not blnSuccess Then
                        ' Use ProteoWizard to extract the scan counts and acquisition time information
                        ' If mSaveLCMS2DPlots = True, this method will also read the m/z and intensity values from each scan so that we can make 2D plots
                        bafFileParsed = ParseBAFFile(fiFileInfo, udtFileInfo)
                    End If

                    If mSaveTICAndBPI And mTICandBPIPlot.CountBPI + mTICandBPIPlot.CountTIC = 0 OrElse
                       mSaveLCMS2DPlots And mLCMS2DPlot.ScanCountCached = 0 Then
                        ' If a ser or fid file exists, we can read the data from it to create the TIC and BPI plots, plus also the 2D plot

                        Dim serOrFidParsed = ParseSerOrFidFile(fiFileInfo.Directory, scanElutionTimeMap)

                        If Not serOrFidParsed And Not bafFileParsed Then
                            ' Look for an analysis.baf file
                            bafFileParsed = ParseBAFFile(fiFileInfo, udtFileInfo)
                        End If

                    End If
                End If

                ' Parse the AutoMS.txt file (if it exists) to determine which scans are MS and which are MS/MS
                ParseAutoMSFile(diDatasetFolder, udtFileInfo)

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
        Catch ex As Exception
            ReportError("Exception processing BAF data: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Private Function ParseSerOrFidFile(
       diDotDFolder As DirectoryInfo,
       scanElutionTimeMap As Dictionary(Of Integer, Single)) As Boolean

        Try

            Dim fiSerOrFidFile = New FileInfo(Path.Combine(diDotDFolder.FullName, "ser"))

            If Not fiSerOrFidFile.Exists Then
                fiSerOrFidFile = New FileInfo(Path.Combine(diDotDFolder.FullName, "fid"))
                If Not fiSerOrFidFile.Exists Then Return False
            End If


            ' Look for the apexAcquisition.method
            Dim fiSettingsFile As FileInfo = FindBrukerSettingsFile(diDotDFolder)

            If fiSettingsFile Is Nothing Then
                ' Not found; look for an acqus file
                Dim fiAcqusFile As FileInfo = FindBrukerAcqusFile(diDotDFolder)

                If fiAcqusFile Is Nothing Then
                    ' Not found; cannot parse the ser file
                    Return False
                End If

                fiSettingsFile = fiAcqusFile
            End If

            Dim needToSaveTICAndBPI = (mSaveTICAndBPI AndAlso mTICandBPIPlot.CountBPI + mTICandBPIPlot.CountTIC = 0)
            Dim dtLastProgressTime = DateTime.UtcNow

            Dim serReader = New BrukerDataReader.DataReader(fiSerOrFidFile.FullName, fiSettingsFile.FullName)

            Dim scanCount = serReader.GetNumMSScans()
            Dim mzValues As Single() = Nothing
            Dim intensities As Single() = Nothing

            ' BrukerDataReader.DataReader treats scan 0 as the first scan

            For scanNumber = 0 To scanCount - 1
                Try
                    serReader.GetMassSpectrum(scanNumber, mzValues, intensities)
                Catch ex As Exception

                    If scanNumber >= scanCount - 1 Then
                        If scanNumber = 0 Then
                            ' Silently ignore this
                            Continue For
                        End If
                        ' Treat this as a warning
                        ShowMessage("Unable to retrieve scan " & scanNumber & " using the BrukerDataReader: " & ex.Message)
                    Else
                        ' Treat this as an error
                        ReportError("Error retrieving scan " & scanNumber & " using the BrukerDataReader: " & ex.Message)
                    End If

                    ' Ignore this scan
                    Continue For
                End Try

                Const msLevel = 1
                Dim elutionTime As Single
                If Not scanElutionTimeMap.TryGetValue(scanNumber, elutionTime) Then
                    elutionTime = scanNumber / 60.0F
                End If

                If needToSaveTICAndBPI Then
                    Dim basePeakIntensity As Double = 0
                    Dim totalIonCurrent As Double = 0

                    If intensities.Count > 0 Then
                        basePeakIntensity = intensities.Max
                        totalIonCurrent = intensities.Sum
                    End If

                    mTICandBPIPlot.AddData(scanNumber, msLevel, elutionTime, basePeakIntensity, totalIonCurrent)
                End If

                If mzValues.Length > 0 Then
                    If mSaveLCMS2DPlots Then
                        Dim dblMassIntensityPairs As Double(,)
                        ReDim dblMassIntensityPairs(1, mzValues.Length)

                        For i = 0 To mzValues.Length - 1
                            dblMassIntensityPairs(0, i) = mzValues(i)
                            dblMassIntensityPairs(1, i) = intensities(i)
                        Next

                        mLCMS2DPlot.AddScan2D(scanNumber, msLevel, elutionTime, mzValues.Length, dblMassIntensityPairs)
                    End If

                End If

                ShowProgress(scanNumber, scanCount, dtLastProgressTime, 2)
            Next

            Return True

        Catch ex As Exception
            ReportError("Exception processing Bruker ser or fid file: " & ex.Message)
            Return False
        End Try

    End Function

    Protected Function ReadAndStoreMcfIndexData(
       cnDB As SQLite.SQLiteConnection,
       lstMetadataNameToID As Dictionary(Of String, Integer),
      ByRef lstScanData As Dictionary(Of String, udtMCFScanInfoType),
       eMcfMetadataField As eMcfMetadataFields) As Boolean

        Dim cmd As SQLite.SQLiteCommand
        cmd = New SQLite.SQLiteCommand(cnDB)

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

            Using drReader As SQLite.SQLiteDataReader = cmd.ExecuteReader()
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

    Protected Function ReadDbString(drReader As SQLite.SQLiteDataReader, strColumnName As String) As String
        Return ReadDbString(drReader, strColumnName, strValueIfNotFound:=String.Empty)
    End Function

    Protected Function ReadDbString(drReader As SQLite.SQLiteDataReader, strColumnName As String, strValueIfNotFound As String) As String
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

    Protected Function ReadDbInt(drReader As SQLite.SQLiteDataReader, strColumnName As String) As Integer
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

    Private Sub UpdateScanInfo(eMcfMetadataField As eMcfMetadataFields, strValue As String, ByRef udtScanInfo As udtMCFScanInfoType)

        Dim intValue As Integer
        Dim dblValue As Double
        Dim dtValue As DateTime

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
        Implements IComparer(Of udtMCFScanInfoType)

        Public Function Compare(x As udtMCFScanInfoType, y As udtMCFScanInfoType) As Integer Implements IComparer(Of udtMCFScanInfoType).Compare

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

