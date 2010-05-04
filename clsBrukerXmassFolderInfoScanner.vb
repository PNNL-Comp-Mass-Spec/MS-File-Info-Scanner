Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
'
' Last modified April 26, 2010

Public Class clsBrukerXmassFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const BRUKER_BAF_FILE_EXTENSION As String = ".BAF"

    Private Const BRUKER_SCANINFO_XML_FILE As String = "scan.xml"
    Private Const BRUKER_XMASS_LOG_FILE As String = "log.txt"
    Private Const BRUKER_AUTOMS_FILE As String = "AutoMS.txt"

    Protected Function FindSubmethodsFile(ByVal ioDatasetFolder As System.IO.DirectoryInfo, _
                                          ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if the file is found; false if not found

        Dim blnSuccess As Boolean

        Dim intIndex As Integer

        Dim ioSubFolders() As System.IO.DirectoryInfo
        Dim ioFile As System.IO.FileInfo

        Try
            ' Look for the XMass method folder (should be named XMASS_Method.m)
            ioSubFolders = ioDatasetFolder.GetDirectories("*.m")

            If ioSubFolders.Length = 0 Then
                ' Match not found
                ' Look for any XMass folders
                ioSubFolders = ioDatasetFolder.GetDirectories("XMass*")
            End If


            If ioSubFolders.Length > 0 Then
                ' Look for the submethods.xml file in each matching subfolder
                For intIndex = 0 To ioSubFolders.Length - 1
                    For Each ioFile In ioSubFolders(intIndex).GetFiles("submethods.xml")
                        ' Match found; assume the file modification time is the acquisition start time
                        ' A more accurate check is likely to open up apexAcquisition.method and look for an entry like: <date>Jan_29_2010 13:31:05.796</date>
                        udtFileInfo.AcqTimeStart = ioFile.LastWriteTime
                        blnSuccess = True
                        Exit For
                    Next
                    If blnSuccess Then Exit For
                Next intIndex
            End If

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


    Protected Function ParseScanXMLFile(ByVal ioDatasetFolder As System.IO.DirectoryInfo, _
                                        ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim strScanXMLFilePath As String

        Dim ioFileInfo As System.IO.FileInfo
        Dim srReader As System.Xml.XmlTextReader

        Dim blnSuccess As Boolean
        Dim blnInScanNode As Boolean
        Dim blnSkipRead As Boolean

        Dim intScanCount As Integer
        Dim intScanNumber As Integer
        Dim sngElutionTime As Single
        Dim dblTIC As Double
        Dim dblBPI As Double
        Dim intMSLevel As Integer

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

                                objScanStatsEntry.ElutionTime = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(sngElutionTime, 4)
                                objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblTIC, 5)
                                objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(dblBPI, 5)
                                objScanStatsEntry.BasePeakMZ = "0"

                                ' Base peak signal to noise ratio
                                objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                                objScanStatsEntry.IonCount = 0
                                objScanStatsEntry.IonCountRaw = 0

                                mDatasetStatsSummarizer.AddDatasetScan(objScanStatsEntry)

                            End If
                    End Select

                Loop

                srReader.Close()

                udtFileInfo.ScanCount = intScanCount

                blnSuccess = True

            End If
        Catch ex As System.Exception
            ReportError("Error finding scan.xml file: " & ex.Message)
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

        Dim intIndex As Integer
        Dim strAnalysisBafFileName As String

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
            strAnalysisBafFileName = "analysis" & BRUKER_BAF_FILE_EXTENSION

            ioFiles = ioDatasetFolder.GetFiles(strAnalysisBafFileName)
            If ioFiles Is Nothing OrElse ioFiles.Length = 0 Then
                MyBase.ReportError(BRUKER_BAF_FILE_EXTENSION & " file not found in " & ioDatasetFolder.FullName)
                blnSuccess = False
            Else
                ioFileInfo = ioFiles(0)
           
                ' Read the file info from the file system
                ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
                UpdateDatasetFileStats(ioFileInfo, udtFileInfo.DatasetID)

                ' Update the dataset name and file extension
                udtFileInfo.DatasetName = ioDatasetFolder.Name
                udtFileInfo.FileExtension = String.Empty

                ' Find the submethods.xml file in the XMASS_Method.m subfolder to determine .AcqTimeStart
                FindSubmethodsFile(ioDatasetFolder, udtFileInfo)

                ' Parse the scan.xml file (if it exists) to determine the number of spectra acquired
                ' We can also obtain TIC and elution time values from this file
                ' However, it does not track whether a scan is MS or MSn
                ParseScanXMLFile(ioDatasetFolder, udtFileInfo)

                ' Parse the AutoMS.txt file (if it exists) to determine which scans are MS and which are MS/MS
                ParseAutoMSFile(ioDatasetFolder, udtFileInfo)

                ' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
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

                blnSuccess = True
            End If
        Catch ex As System.Exception
            ReportError("Exception processing BAF data: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

End Class
