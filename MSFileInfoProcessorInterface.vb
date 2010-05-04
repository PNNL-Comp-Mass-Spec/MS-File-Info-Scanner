Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified February 1, 2010

Public Interface iMSFileInfoProcessor

    Enum ProcessingOptions
        CreateTICAndBPI = 0
        ComputeOverallQualityScores = 1
        CreateDatasetInfoFile = 2
        CreateLCMS2DPlots = 3
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

    Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As udtFileInfoType) As Boolean
    Function CreateOutputFiles(ByVal strInputFileName As String, ByVal strOutputFolderPath As String) As Boolean

    Function GetDatasetInfoXML() As String
    Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String

    'ReadOnly Property BPI() As udtChromatogramInfoType
    'ReadOnly Property TIC() As udtChromatogramInfoType

    Property LCMS2DPlotOptions() As clsLCMSDataPlotter.clsOptions
    Property LCMS2DOverviewPlotDivisor() As Integer

    Function GetOption(ByVal eOption As ProcessingOptions) As Boolean
    Sub SetOption(ByVal eOption As ProcessingOptions, ByVal blnValue As Boolean)

    Event ErrorEvent(ByVal Message As String)
End Interface

