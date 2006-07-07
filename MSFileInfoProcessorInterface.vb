Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified September 17, 2005

Public Interface iMSFileInfoProcessor

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
    End Structure

    Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As udtFileInfoType) As Boolean
    Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String

End Interface

