Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified December 09, 2005

Public Class clsAgilentTOFOrQStarWiffFileInfoScanner
    Implements MSFileInfoScanner.iMSFileInfoProcessor

    ' Note: The extension must be in all caps
    Public Const AGILENT_TOF_OR_QSTAR_FILE_EXTENSION As String = ".WIFF"

    Protected Enum DeconToolsFileTypeConstants As Integer
        BRUKER = 0
        IONSPEC
        MIDAS
        FINNIGAN
        SUNEXTREL
        AGILENT_TOF
        ICR2LSRAWDATA
        MICROMASSRAWDATA
    End Enum

    Protected mDeconTools As DeconWrapperManaged.clsDeconWrapperManaged

    Private Function GetAppFolderPath() As String
        ' Could use Application.StartupPath, but .GetExecutingAssembly is better
        Return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    End Function

    Public Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String Implements iMSFileInfoProcessor.GetDatasetNameViaPath
        ' The dataset name is simply the file name without .wiff
        Try
            Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
        Catch ex As System.Exception
            Return String.Empty
        End Try
    End Function

    Public Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean Implements iMSFileInfoProcessor.ProcessDatafile
        ' Returns True if success, False if an error

        Dim ioFileInfo As System.IO.FileInfo
        Dim strDataFilePathLocal As String
        Dim blnSuccess As Boolean
        Dim blnDeleteLocalFile As Boolean

        Dim intScanEnd As Integer
        Dim dblAcquisitionLengthMinutes As Double
        Dim intMinutes As Integer
        Dim intSeconds As Integer

        ' Obtain the full path to the file
        ioFileInfo = New System.IO.FileInfo(strDataFilePath)

        With udtFileInfo
            .FileSystemCreationTime = ioFileInfo.CreationTime
            .FileSystemModificationTime = ioFileInfo.LastWriteTime

            ' The acquisition times will get updated below to more accurate values
            .AcqTimeStart = .FileSystemModificationTime
            .AcqTimeEnd = .FileSystemModificationTime

            .DatasetID = 0
            .DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
            .FileExtension = ioFileInfo.Extension
            .FileSizeBytes = ioFileInfo.Length
        End With

        ' Use DeconTools to read the .Wiff file
        ' Unfortunately, we must copy the file to the local drive (placing it in a writable folder);
        '  otherwise, DeconTools cannot open it

        Try
            blnDeleteLocalFile = False
            strDataFilePathLocal = System.IO.Path.Combine(GetAppFolderPath, System.IO.Path.GetFileName(strDataFilePath))

            If strDataFilePathLocal.ToLower <> strDataFilePath.ToLower Then
                If Not System.IO.File.Exists(strDataFilePathLocal) Then
                    Console.WriteLine("Copying file " & System.IO.Path.GetFileName(strDataFilePath) & " to the working folder")
                    System.IO.File.Copy(strDataFilePath, strDataFilePathLocal)
                    blnDeleteLocalFile = True
                End If
            End If
        Catch ex As System.Exception
            Return False
        End Try

        If mDeconTools Is Nothing Then
            mDeconTools = New DeconWrapperManaged.clsDeconWrapperManaged
        End If

        ' Open a handle to the data file
        Try

            mDeconTools.LoadFile(strDataFilePathLocal, DeconToolsFileTypeConstants.AGILENT_TOF)
            blnSuccess = True
        Catch ex As System.Exception
            blnSuccess = False
        End Try

        If blnSuccess Then
            ' Read the file info
            udtFileInfo.ScanCount = mDeconTools.NumScans()

            ' We have to use the file modification time as the acquisition end time
            udtFileInfo.AcqTimeEnd = udtFileInfo.FileSystemModificationTime

            intScanEnd = udtFileInfo.ScanCount
            dblAcquisitionLengthMinutes = 0
            blnSuccess = False
            Do
                Try
                    dblAcquisitionLengthMinutes = mDeconTools.GetScanTime(intScanEnd)
                    blnSuccess = True
                Catch ex As System.Exception
                    intScanEnd -= 1
                    If intScanEnd < 0 Then
                        blnSuccess = True
                    End If
                End Try
            Loop While Not blnSuccess

            If blnSuccess Then
                intMinutes = CInt(Math.Floor(dblAcquisitionLengthMinutes))
                intSeconds = CInt(Math.Round((dblAcquisitionLengthMinutes - intMinutes) * 60.0, 0))

                udtFileInfo.AcqTimeEnd = udtFileInfo.FileSystemModificationTime
                If intMinutes > 0 Or intSeconds > 0 Then
                    udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd.Subtract(New System.TimeSpan(0, intMinutes, intSeconds))
                Else
                    udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
                End If
            Else
                udtFileInfo.AcqTimeStart = udtFileInfo.AcqTimeEnd
            End If

        End If

        ' Close the handle to the data file
        mDeconTools.CloseFile()
        mDeconTools = Nothing

        ' Delete the local copy of the data file
        If blnDeleteLocalFile Then
            Try
                System.IO.File.Delete(strDataFilePathLocal)
            Catch ex As System.Exception
                ' Deletion failed
                Console.WriteLine("Deletion failed for: " & System.IO.Path.GetFileName(strDataFilePath))
            End Try
        End If

        Return blnSuccess
    End Function

End Class
