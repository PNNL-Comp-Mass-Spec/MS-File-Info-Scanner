Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified February 1, 2010

Public Class clsFinniganRawFileInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const FINNIGAN_RAW_FILE_EXTENSION As String = ".RAW"

    Protected Sub ComputeQualityScores(ByRef objXcaliburAccessor As FinniganFileIO.FinniganFileReaderBaseClass, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType)
        ' This function is used to determine one or more overall quality scores

        Dim intScanCount As Integer
        Dim intScanNumber As Integer
        Dim intIonIndex As Integer
        Dim intReturnCode As Integer

        Dim sngOverallScore As Single

        Dim udtScanHeaderInfo As FinniganFileIO.FinniganFileReaderBaseClass.udtScanHeaderInfoType
        Dim dblIonMZ() As Double
        Dim dblIonIntensity() As Double

        Dim dblIntensitySum As Double
        Dim dblOverallAvgIntensitySum As Double
        Dim intOverallAvgCount As Integer

        dblOverallAvgIntensitySum = 0
        intOverallAvgCount = 0

        intScanCount = objXcaliburAccessor.GetNumScans
        For intScanNumber = 1 To intScanCount
            ' This function returns the number of points in dblIonMZ() and dblIonIntensity()
            intReturnCode = objXcaliburAccessor.GetScanData(intScanNumber, dblIonMZ, dblIonIntensity, udtScanHeaderInfo)

            If intReturnCode > 0 Then

                If Not dblIonIntensity Is Nothing AndAlso dblIonIntensity.Length > 0 Then
                    ' ToDo: Analyze dblIonMZ and dblIonIntensity to compute a quality scores
                    ' Keep track of the quality scores and then store one or more overall quality scores in udtFileInfo.OverallQualityScore
                    ' For now, this just computes the average intensity for each scan and then computes and overall average intensity value

                    dblIntensitySum = 0
                    For intIonIndex = 0 To dblIonIntensity.Length - 1
                        dblIntensitySum += dblIonIntensity(intIonIndex)
                    Next intIonIndex

                    dblOverallAvgIntensitySum += dblIntensitySum / dblIonIntensity.Length

                    intOverallAvgCount += 1
                End If
            End If
        Next intScanNumber

        If intOverallAvgCount > 0 Then
            sngOverallScore = CSng(dblOverallAvgIntensitySum / intOverallAvgCount)
        Else
            sngOverallScore = 0
        End If

        udtFileInfo.OverallQualityScore = sngOverallScore

    End Sub


    Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
        ' The dataset name is simply the file name without .Raw
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Protected Sub LoadScanDetails(ByRef objXcaliburAccessor As FinniganFileIO.FinniganFileReaderBaseClass)

        Dim intScanCount As Integer
        Dim intScanNumber As Integer

        Dim udtScanHeaderInfo As FinniganFileIO.FinniganFileReaderBaseClass.udtScanHeaderInfoType
        Dim blnSuccess As Boolean

        If mCreateTICAndBPI Then
            ' Initialize the TIC and BPI arrays
            MyBase.InitializeTICandBPI()
        End If

        intScanCount = objXcaliburAccessor.GetNumScans
        For intScanNumber = 1 To intScanCount
            blnSuccess = objXcaliburAccessor.GetScanInfo(intScanNumber, udtScanHeaderInfo)

            If blnSuccess Then
                If mCreateTICAndBPI Then
                    With udtScanHeaderInfo
                        AddChromatogramPoint(mBPI, intScanNumber, .BasePeakIntensity, .MSLevel)
                        AddChromatogramPoint(mTIC, intScanNumber, .TotalIonCurrent, .MSLevel)
                    End With
                End If

                If mCreateDatasetInfoFile Then

                    Dim objScanStatsEntry As New DSSummarizer.clsScanStatsEntry

                    With udtScanHeaderInfo
                        objScanStatsEntry.ScanNumber = intScanNumber
                        objScanStatsEntry.ScanType = .MSLevel

                        objScanStatsEntry.ScanTypeName = FinniganFileIO.XRawFileIO.GetScanTypeNameFromFinniganScanFilterText(.FilterText)
                        objScanStatsEntry.ScanFilterText = FinniganFileIO.XRawFileIO.MakeGenericFinniganScanFilter(.FilterText)

                        objScanStatsEntry.ElutionTime = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(.RetentionTime, 5)
                        objScanStatsEntry.TotalIonIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(.TotalIonCurrent, 5)
                        objScanStatsEntry.BasePeakIntensity = DSSummarizer.clsDatasetStatsSummarizer.ValueToString(.BasePeakIntensity, 5)
                        objScanStatsEntry.BasePeakMZ = Math.Round(.BasePeakMZ, 4).ToString

                        ' Base peak signal to noise ratio
                        objScanStatsEntry.BasePeakSignalToNoiseRatio = "0"

                        objScanStatsEntry.IonCount = 0
                        objScanStatsEntry.IonCountRaw = 0
                    End With
                    mDatasetScanStats.Add(objScanStatsEntry)

                End If
             

            End If
        Next intScanNumber

        If mCreateTICAndBPI Then
            ' Note that the TIC and BPI are actually written to disk by clsMSFileScanner.SaveTICAndBPIPlotFiles

            ' Trim the TIC and BPI array to length
            If mBPI.ScanCount > 0 Then
                mBPI.TrimArrays()
            End If

            If mTIC.ScanCount > 0 Then
                mTIC.TrimArrays()
            End If

        End If
     
    End Sub

    Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
        ' Returns True if success, False if an error

        Dim objXcaliburAccessor As FinniganFileIO.FinniganFileReaderBaseClass
        Dim ioFileInfo As System.IO.FileInfo
        Dim udtScanHeaderInfo As MSFileInfoScanner.FinniganFileIO.XRawFileIO.udtScanHeaderInfoType

        Dim intDatasetID As Integer
        Dim intScanEnd As Integer

        Dim strDataFilePathLocal As String = String.Empty

        Dim blnReadError As Boolean
        Dim blnDeleteLocalFile As Boolean

        ' Obtain the full path to the file
        ioFileInfo = New System.IO.FileInfo(strDataFilePath)

        If Not ioFileInfo.Exists Then
            Return False
        End If

        ' Future, optional: Determine the DatasetID
        ' Unfortunately, this is not present in metadata.txt
        ' intDatasetID = LookupDatasetID(strDatasetName)
        intDatasetID = 0

        ' Record the file size and Dataset ID
        With udtFileInfo
            .FileSystemCreationTime = ioFileInfo.CreationTime
            .FileSystemModificationTime = ioFileInfo.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = intDatasetID
            .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
            .FileExtension = ioFileInfo.Extension
            .FileSizeBytes = ioFileInfo.Length

            .ScanCount = 0
        End With

        If mDatasetScanStats Is Nothing Then
            mDatasetScanStats = New System.Collections.Generic.List(Of DSSummarizer.clsScanStatsEntry)
        Else
            mDatasetScanStats.Clear()
        End If

        blnDeleteLocalFile = False
        blnReadError = False

        ' Use Xraw to read the .Raw file
        ' If reading from a SAMBA-mounted network share, and if the current user has 
        '  Read privileges but not Read&Execute privileges, then we will need to copy the file locally
        objXcaliburAccessor = New FinniganFileIO.XRawFileIO

        ' Open a handle to the data file
        If Not objXcaliburAccessor.OpenRawFile(ioFileInfo.FullName) Then
            ' File open failed
            Console.WriteLine("Call to .OpenRawFile failed for: " & ioFileInfo.FullName)
            blnReadError = True

            If clsMSFileScanner.GetAppFolderPath.Substring(0, 2).ToLower <> ioFileInfo.FullName.Substring(0, 2).ToLower Then
                ' Copy the file locally and try again

                Try
                    strDataFilePathLocal = System.IO.Path.Combine(clsMSFileScanner.GetAppFolderPath, System.IO.Path.GetFileName(strDataFilePath))

                    If strDataFilePathLocal.ToLower <> strDataFilePath.ToLower Then
                        Console.WriteLine("Copying file " & System.IO.Path.GetFileName(strDataFilePath) & " to the working folder")
                        System.IO.File.Copy(strDataFilePath, strDataFilePathLocal, True)

                        strDataFilePath = String.Copy(strDataFilePathLocal)
                        blnDeleteLocalFile = True

                        ' Update ioFileInfo then try to re-open
                        ioFileInfo = New System.IO.FileInfo(strDataFilePath)

                        If Not objXcaliburAccessor.OpenRawFile(ioFileInfo.FullName) Then
                            ' File open failed
                            Console.WriteLine("Call to .OpenRawFile failed for: " & ioFileInfo.FullName)
                            blnReadError = True
                        Else
                            blnReadError = False
                        End If
                    End If
                Catch ex As System.Exception
                    blnReadError = True
                End Try
            End If
        End If

        If Not blnReadError Then
            ' Read the file info
            Try
                udtFileInfo.AcqTimeStart = objXcaliburAccessor.FileInfo.CreationDate
            Catch ex As System.Exception
                ' Read error
                blnReadError = True
            End Try

            If Not blnReadError Then
                Try
                    ' Look up the end scan time then compute .AcqTimeEnd
                    intScanEnd = objXcaliburAccessor.FileInfo.ScanEnd
                    objXcaliburAccessor.GetScanInfo(intScanEnd, udtScanHeaderInfo)

                    With udtFileInfo
                        .AcqTimeEnd = .AcqTimeStart.AddMinutes(udtScanHeaderInfo.RetentionTime)
                        .ScanCount = objXcaliburAccessor.GetNumScans()
                    End With
                Catch ex As System.Exception
                    ' Error; use default values
                    With udtFileInfo
                        .AcqTimeEnd = .AcqTimeStart
                        .ScanCount = 0
                    End With
                End Try

                If mCreateTICAndBPI OrElse mCreateDatasetInfoFile Then
                    ' Load data from each scan
                    ' This is used to create the TIC and BPI, and/or to create the Dataset Info File
                    LoadScanDetails(objXcaliburAccessor)
                End If

                If mComputeOverallQualityScores Then
                    ' Note that this call will also create the TICs and BPIs
                    ComputeQualityScores(objXcaliburAccessor, udtFileInfo)
                End If
            End If
        End If

        ' Close the handle to the data file
        objXcaliburAccessor.CloseRawFile()
        objXcaliburAccessor = Nothing


        ' Read the file info from the file system
        ' (much of this is already in udtFileInfo, but we'll call UpdateDatasetFileStats() anyway to make sure all of the necessary steps are taken)
        UpdateDatasetFileStats(mDatasetFileInfo, ioFileInfo, intDatasetID)

        ' Copy over the upadted filetime info from udtFileInfo to mDatasetFileInfo
        With mDatasetFileInfo
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

        ' Delete the local copy of the data file
        If blnDeleteLocalFile Then
            Try
                System.IO.File.Delete(strDataFilePathLocal)
            Catch ex As System.Exception
                ' Deletion failed
                Console.WriteLine("Deletion failed for: " & System.IO.Path.GetFileName(strDataFilePathLocal))
            End Try
        End If

        Return Not blnReadError

    End Function

End Class
