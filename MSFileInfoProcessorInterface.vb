Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified September 17, 2005

Public Interface iMSFileInfoProcessor

    Enum ProcessingOptions
        CreateTICAndBPI = 0
        ComputeOverallQualityScores = 1
    End Enum

    ' ToDo: Update udtFileInfo to include some overall quality scores

    Structure udtFileInfoType
        Public FileSystemCreationTime As DateTime
        Public FileSystemModificationTime As DateTime
        Public DatasetID As Integer
        Public DatasetName As String
        Public FileExtension As String
        Public AcqTimeStart As DateTime
        Public AcqTimeEnd As DateTime
        Public ScanCount As Integer
        Public FileSizeBytes As Long
        Public OverallQualityScore As Single
    End Structure

    Structure udtChromatogramInfoType
        Public ScanCount As Integer
        Public ScanNum() As Integer
        Public ScanIntensity() As Double
        Public ScanMSLevel() As Integer

        Public Sub Initialize()
            ScanCount = 0
            ReDim ScanNum(9)
            ReDim ScanIntensity(9)
            ReDim ScanMSLevel(9)
        End Sub

        Public Sub TrimArrays()
            ReDim Preserve ScanNum(ScanCount - 1)
            ReDim Preserve ScanIntensity(ScanCount - 1)
            ReDim Preserve ScanMSLevel(ScanCount - 1)
        End Sub
    End Structure

    Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As udtFileInfoType) As Boolean
    Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String

    ReadOnly Property BPI() As udtChromatogramInfoType
    ReadOnly Property TIC() As udtChromatogramInfoType

    Function GetOption(ByVal eOption As ProcessingOptions) As Boolean
    Sub SetOption(ByVal eOption As ProcessingOptions, ByVal blnValue As Boolean)
End Interface

