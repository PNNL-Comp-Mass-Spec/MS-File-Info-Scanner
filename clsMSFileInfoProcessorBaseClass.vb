Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2007, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified December 19, 2007

Public MustInherit Class clsMSFileInfoProcessorBaseClass
    Implements iMSFileInfoProcessor

    Public Sub New()
        InitializeLocalVariables()
    End Sub

#Region "Member variables"
    Protected mCreateTICAndBPI As Boolean
    Protected mComputeOverallQualityScores As Boolean
    Protected mCreateDatasetInfoFile As Boolean

    Protected mBPI As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType
    Protected mTIC As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType

    Protected mDatasetScanStats As System.Collections.Generic.List(Of DSSummarizer.clsScanStatsEntry)
    Protected mDatasetFileInfo As DSSummarizer.clsDatasetStatsSummarizer.udtDatasetFileInfoType

#End Region

#Region "Properties"
    Public ReadOnly Property BPI() As iMSFileInfoProcessor.udtChromatogramInfoType Implements iMSFileInfoProcessor.BPI
        Get
            Return mBPI
        End Get
    End Property

    Public ReadOnly Property TIC() As iMSFileInfoProcessor.udtChromatogramInfoType Implements iMSFileInfoProcessor.TIC
        Get
            Return mTIC
        End Get
    End Property
#End Region

    Public Function GetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions) As Boolean Implements iMSFileInfoProcessor.GetOption
        Select Case eOption
            Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
                Return mCreateTICAndBPI
            Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
                Return mComputeOverallQualityScores
            Case iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile
                Return mCreateDatasetInfoFile
        End Select
    End Function

    Public Sub SetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions, ByVal blnValue As Boolean) Implements iMSFileInfoProcessor.SetOption
        Select Case eOption
            Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
                mCreateTICAndBPI = blnValue
            Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
                mComputeOverallQualityScores = blnValue
            Case iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile
                mCreateDatasetInfoFile = blnValue
        End Select

    End Sub

    Protected Sub AddChromatogramPoint(ByRef udtChromatogram As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType, ByVal intScanNumber As Integer, ByVal dblIntensity As Double, ByVal intMSLevel As Integer)

        With udtChromatogram
            If .ScanCount >= .ScanNum.Length Then
                ReDim Preserve .ScanNum(.ScanNum.Length * 2 - 1)
                ReDim Preserve .ScanIntensity(.ScanNum.Length - 1)
                ReDim Preserve .ScanMSLevel(.ScanNum.Length - 1)
            End If

            .ScanNum(.ScanCount) = intScanNumber
            .ScanIntensity(.ScanCount) = dblIntensity
            .ScanMSLevel(.ScanCount) = intMSLevel

            .ScanCount += 1
        End With
    End Sub

    Public Function CreateDatasetInfoFile(ByVal strInputFileName As String, ByVal strOutputFolderPath As String) As Boolean Implements iMSFileInfoProcessor.CreateDatasetInfoFile

        Dim blnSuccess As Boolean

        Dim strDatasetName As String
        Dim strDatasetInfoFilePath As String

        Dim objDatasetStatsSummarizer As DSSummarizer.clsDatasetStatsSummarizer

        Try
            strDatasetName = GetDatasetNameViaPath(strInputFileName)
            strDatasetInfoFilePath = System.IO.Path.Combine(strOutputFolderPath, _
                                                            System.IO.Path.GetFileNameWithoutExtension(strInputFileName))
            strDatasetInfoFilePath &= "_DatasetInfo.xml"

            objDatasetStatsSummarizer = New DSSummarizer.clsDatasetStatsSummarizer

            blnSuccess = objDatasetStatsSummarizer.CreateDatasetInfoFile(strDatasetName, strDatasetInfoFilePath, mDatasetScanStats, mDatasetFileInfo)

            If Not blnSuccess Then
                Console.WriteLine("Error calling objDatasetStatsSummarizer.CreateDatasetInfoFile: " & objDatasetStatsSummarizer.ErrorMessage)
            End If

        Catch ex As System.Exception
            Console.WriteLine("Error creating dataset info file: " & ex.Message)
            blnSuccess = False
        End Try

        Return blnSuccess

    End Function

    Protected Sub InitializeLocalVariables()
        InitializeTICAndBPI()

        mDatasetScanStats = New System.Collections.Generic.List(Of DSSummarizer.clsScanStatsEntry)
        mDatasetFileInfo.Clear()

    End Sub

    Protected Sub InitializeTICAndBPI()
        ' Initialize mBPI
        mBPI.Initialize()
        mTIC.Initialize()
    End Sub

    Protected Function UpdateDatasetFileStats(ByRef udtDatasetFileInfo As DSSummarizer.clsDatasetStatsSummarizer.udtDatasetFileInfoType, _
                                            ByRef ioFileInfo As System.IO.FileInfo, _
                                            ByVal intDatasetID As Integer) As Boolean

        Try
            If Not ioFileInfo.Exists Then Return False

            ' Record the file size and Dataset ID
            With udtDatasetFileInfo
                .FileSystemCreationTime = ioFileInfo.CreationTime
                .FileSystemModificationTime = ioFileInfo.LastWriteTime

                .AcqTimeStart = .FileSystemModificationTime
                .AcqTimeEnd = .FileSystemModificationTime

                .DatasetID = intDatasetID
                .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
                .FileExtension = ioFileInfo.Extension
                .FileSizeBytes = ioFileInfo.Length

                .ScanCount = 0
            End With

        Catch ex As System.Exception
            Return False
        End Try

        Return True

    End Function

    Public MustOverride Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean Implements iMSFileInfoProcessor.ProcessDatafile
    Public MustOverride Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String Implements iMSFileInfoProcessor.GetDatasetNameViaPath

End Class
