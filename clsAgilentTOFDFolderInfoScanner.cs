Option Strict On

Imports System.Runtime.InteropServices

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2012
'
' Last modified November 3, 2015

<CLSCompliant(False)> Public Class clsAgilentTOFDFolderInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const AGILENT_DATA_FOLDER_D_EXTENSION As String = ".D"

    Public Const AGILENT_ACQDATA_FOLDER_NAME As String = "AcqData"
    Public Const AGILENT_MS_SCAN_FILE As String = "MSScan.bin"
    Public Const AGILENT_XML_CONTENTS_FILE As String = "Contents.xml"
    Public Const AGILENT_TIME_SEGMENT_FILE As String = "MSTS.xml"

    Protected WithEvents mPWizParser As clsProteowizardDataParser

    Public Overrides Function GetDatasetNameViaPath(strDataFilePath As String) As String
        ' The dataset name is simply the folder name without .D
        Try
            Return Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As Exception
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
    Private Function ProcessContentsXMLFile(strFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        Dim blnSuccess As Boolean

        Try
            blnSuccess = False

            ' Open the Contents.xml file
            Dim strFilePath = Path.Combine(strFolderPath, AGILENT_XML_CONTENTS_FILE)

            Using srReader = New Xml.XmlTextReader(New FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))

                Do While Not srReader.EOF
                    srReader.Read()

                    Select Case srReader.NodeType
                        Case Xml.XmlNodeType.Element

                            If srReader.Name = "AcquiredTime" Then
                                Try
                                    Dim dtAcquisitionStartTime As DateTime
                                    dtAcquisitionStartTime = srReader.ReadElementContentAsDateTime

                                    ' Convert from Universal time to Local time
                                    Dim dtAcquisitionTime = dtAcquisitionStartTime.ToLocalTime

                                    ' There have been some cases where the acquisition start time is several years before the file modification time, 
                                    ' for example XG_A83CapiHSSWash1.d where the time in the Contents.xml file is 3/20/2005 while the file modification time is 2010
                                    ' Thus, we use a sanity check of a maximum run time of 24 hours

                                    If udtFileInfo.AcqTimeEnd.Subtract(dtAcquisitionTime).TotalDays < 1 Then
                                        udtFileInfo.AcqTimeStart = dtAcquisitionStartTime.ToLocalTime
                                        blnSuccess = True
                                    End If

                                Catch ex As Exception
                                    ' Ignore errors here
                                End Try

                            End If

                    End Select

                Loop

            End Using


        Catch ex As Exception
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
    Protected Function ProcessTimeSegmentFile(
       strFolderPath As String,
       ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType,
       <Out> ByRef dblTotalAcqTimeMinutes As Double) As Boolean

        Dim blnSuccess As Boolean

        Dim dblStartTime As Double
        Dim dblEndTime As Double


        Try
            blnSuccess = False
            udtFileInfo.ScanCount = 0
            dblTotalAcqTimeMinutes = 0

            ' Open the Contents.xml file
            Dim strFilePath = Path.Combine(strFolderPath, AGILENT_TIME_SEGMENT_FILE)

            Using srReader = New Xml.XmlTextReader(New FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))

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

        Catch ex As Exception
            ' Exception reading file
            ReportError("Exception reading " & AGILENT_TIME_SEGMENT_FILE & ": " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Public Overrides Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim blnSuccess As Boolean

        Try
            blnSuccess = False
            Dim diRootFolder = New DirectoryInfo(strDataFilePath)
            Dim diAcqDataFolder = New DirectoryInfo(Path.Combine(diRootFolder.FullName, AGILENT_ACQDATA_FOLDER_NAME))

            With udtFileInfo
                .FileSystemCreationTime = diAcqDataFolder.CreationTime
                .FileSystemModificationTime = diAcqDataFolder.LastWriteTime

                ' The acquisition times will get updated below to more accurate values
                .AcqTimeStart = .FileSystemModificationTime
                .AcqTimeEnd = .FileSystemModificationTime

                .DatasetName = GetDatasetNameViaPath(diRootFolder.Name)
                .FileExtension = diRootFolder.Extension
                .FileSizeBytes = 0
                .ScanCount = 0

                If diAcqDataFolder.Exists Then
                    ' Sum up the sizes of all of the files in the AcqData folder
                    For Each fiFile In diAcqDataFolder.GetFiles("*", SearchOption.AllDirectories)
                        .FileSizeBytes += fiFile.Length
                    Next

                    ' Look for the MSScan.bin file
                    ' Use its modification time to get an initial estimate for the acquisition end time
                    Dim fiMSScanfile = New FileInfo(Path.Combine(diAcqDataFolder.FullName, AGILENT_MS_SCAN_FILE))

                    If fiMSScanfile.Exists Then
                        .AcqTimeStart = fiMSScanfile.LastWriteTime
                        .AcqTimeEnd = fiMSScanfile.LastWriteTime

                        ' Read the file info from the file system
                        ' Several of these stats will be further updated later
                        UpdateDatasetFileStats(fiMSScanfile, udtFileInfo.DatasetID)
                    Else
                        ' Read the file info from the file system
                        ' Several of these stats will be further updated later
                        UpdateDatasetFileStats(diAcqDataFolder, udtFileInfo.DatasetID)
                    End If

                    blnSuccess = True
                End If

            End With

            If blnSuccess Then
                ' The AcqData folder exists

                ' Parse the Contents.xml file to determine the acquisition start time
                Dim blnAcqStartTimeDetermined = ProcessContentsXMLFile(diAcqDataFolder.FullName, udtFileInfo)

                Dim dblAcquisitionLengthMinutes As Double

                ' Parse the MSTS.xml file to determine the acquisition length and number of scans
                Dim blnValidMSTS = ProcessTimeSegmentFile(diAcqDataFolder.FullName, udtFileInfo, dblAcquisitionLengthMinutes)

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
                ReadBinaryData(diRootFolder.FullName, udtFileInfo)

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


        Catch ex As Exception
            ReportError("Exception parsing Agilent TOF .D folder: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Protected Function ReadBinaryData(strDataFolderPath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean

        Dim blnTICStored = False
        Dim blnSRMDataCached = False

        Dim blnSuccess As Boolean

        Try
            ' Open the data folder using the ProteoWizardWrapper

            Dim objPWiz = New pwiz.ProteowizardWrapper.MSDataFileReader(strDataFolderPath)

            Try
                Dim dtRunStartTime = CDate(objPWiz.RunStartTime())

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
            mPWizParser = New clsProteowizardDataParser(
              objPWiz, mDatasetStatsSummarizer, mTICandBPIPlot, mLCMS2DPlot,
              mSaveLCMS2DPlots, mSaveTICAndBPI, mCheckCentroidingStatus)

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

            objPWiz.Dispose()
            PRISM.Processes.clsProgRunner.GarbageCollectNow()

            blnSuccess = True

        Catch ex As Exception
            ReportError("Exception reading the Binary Data in the Agilent TOF .D folder using Proteowizard: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess
    End Function

    Private Sub mPWizParser_ErrorEvent(message As String) Handles mPWizParser.ErrorEvent
        ReportError(message)
    End Sub

    Private Sub mPWizParser_MessageEvent(message As String) Handles mPWizParser.MessageEvent
        ShowMessage(message)
    End Sub

End Class
