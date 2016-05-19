Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Started in 2005
'
' Last modified May 20, 2015

Public Interface iMSFileInfoProcessor

    Enum ProcessingOptions
        CreateTICAndBPI = 0
        ComputeOverallQualityScores = 1
        CreateDatasetInfoFile = 2
        CreateLCMS2DPlots = 3
        CopyFileLocalOnReadError = 4
		UpdateDatasetStatsTextFile = 5
		CreateScanStatsFile = 6
		CheckCentroidingStatus = 7
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

    Function ProcessDataFile(strDataFilePath As String, ByRef udtFileInfo As udtFileInfoType) As Boolean
    Function CreateOutputFiles(strInputFileName As String, strOutputFolderPath As String) As Boolean

    Function GetDatasetInfoXML() As String
    Function GetDatasetNameViaPath(strDataFilePath As String) As String

    'ReadOnly Property BPI() As udtChromatogramInfoType
    'ReadOnly Property TIC() As udtChromatogramInfoType

    Property LCMS2DPlotOptions() As MSFileInfoScannerInterfaces.clsLCMSDataPlotterOptions
    Property LCMS2DOverviewPlotDivisor() As Integer

    Property DatasetStatsTextFileName() As String
    Property DatasetID As Integer

    Property ScanStart() As Integer
    Property ScanEnd() As Integer
    Property ShowDebugInfo() As Boolean

    Function GetOption(eOption As ProcessingOptions) As Boolean
    Sub SetOption(eOption As ProcessingOptions, blnValue As Boolean)

    Event ErrorEvent(Message As String)

    Event MessageEvent(message As String)

End Interface

