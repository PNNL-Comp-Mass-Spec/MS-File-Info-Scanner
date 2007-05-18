Option Strict On

' Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.

' See clsMSFileScanner for a program description

Module modMain

    Public Const PROGRAM_DATE As String = "May 17, 2007"

    Private mInputDataFilePath As String            ' This path can contain wildcard characters, e.g. C:\*.raw
    Private mOutputFolderName As String             ' Optional
    Private mParameterFilePath As String            ' Optional

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer

    Private mPreventDuplicateEntriesInAcquisitionTimeFile As Boolean
    Private mReprocessingExistingFiles As Boolean
    Private mReprocessIfCachedSizeIsZero As Boolean

    Private mQuietMode As Boolean

    ''Private Function TestZipper(ByVal strFolderPath As String, ByVal strFileMatch As String) As Boolean

    ''    Dim ioFolderInfo As System.IO.DirectoryInfo

    ''    Dim ioFileMatch As System.IO.FileInfo
    ''    Dim objZipInfo As ICSharpCode.SharpZipLib.Zip.ZipFile
    ''    Dim zeZipEntry As ICSharpCode.SharpZipLib.Zip.ZipEntry

    ''    Dim blnSuccess As Boolean

    ''    Dim lngTotalBytes As Long = 0
    ''    Dim intFileCount As Integer = 0

    ''    Try
    ''        ioFolderInfo = New System.IO.DirectoryInfo(strFolderPath)
    ''        If ioFolderInfo.Exists Then
    ''            For Each ioFileMatch In ioFolderInfo.GetFiles(strFileMatch)
    ''                ' Get the info on each zip file

    ''                objZipInfo = New ICSharpCode.SharpZipLib.Zip.ZipFile(ioFileMatch.OpenRead)

    ''                For Each zeZipEntry In objZipInfo
    ''                    lngTotalBytes += zeZipEntry.Size
    ''                    intFileCount += 1

    ''                    If zeZipEntry.RequiresZip64 Then
    ''                        ' Requires Zip 64
    ''                    End If
    ''                Next zeZipEntry
    ''                objZipInfo.Close()
    ''                objZipInfo = Nothing

    ''                blnSuccess = objZipInfo.TestArchive(False)
    ''            Next ioFileMatch

    ''        End If
    ''        blnSuccess = True
    ''    Catch ex as System.Exception
    ''        blnSuccess = False
    ''    End Try

    ''    Return blnSuccess

    ''End Function

    Public Function Main() As Integer

        ' Returns 0 if no error, error code if an error

        Dim intReturnCode As Integer
        Dim objMSFileScanner As clsMSFileScanner
        Dim objParseCommandLine As New SharedVBNetRoutines.clsParseCommandLine
        Dim blnProceed As Boolean

        intReturnCode = 0
        mInputDataFilePath = String.Empty
        mOutputFolderName = String.Empty
        mParameterFilePath = String.Empty

        mRecurseFolders = False
        mRecurseFoldersMaxLevels = 0

        mReprocessingExistingFiles = False
        mReprocessIfCachedSizeIsZero = False

        ''TestZipper("\\proto-6\Db_Backups\Albert_Backup\MT_Shewanella_P196", "*.BAK.zip")
        ''Return 0

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If objParseCommandLine.ParameterCount = 0 OrElse Not blnProceed OrElse objParseCommandLine.NeedToShowHelp OrElse mInputDataFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else
                objMSFileScanner = New clsMSFileScanner

                With objMSFileScanner
                    .ShowMessages = Not mQuietMode
                    .ReprocessExistingFiles = mReprocessingExistingFiles
                    .ReprocessIfCachedSizeIsZero = mReprocessIfCachedSizeIsZero

                    If Not mParameterFilePath Is Nothing AndAlso mParameterFilePath.Length > 0 Then
                        .LoadParameterFileSettings(mParameterFilePath)
                    End If
                End With

                If mRecurseFolders Then
                    If objMSFileScanner.ProcessMSFilesAndRecurseFolders(mInputDataFilePath, mOutputFolderName, mRecurseFoldersMaxLevels) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = objMSFileScanner.ErrorCode
                    End If
                Else
                    If objMSFileScanner.ProcessMSFileOrFolderWildcard(mInputDataFilePath, mOutputFolderName, True) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = objMSFileScanner.ErrorCode
                        If intReturnCode <> 0 AndAlso Not mQuietMode Then
                            MsgBox("Error while processing: " & objMSFileScanner.GetErrorMessage(), MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "Error")
                        End If
                    End If
                End If


                objMSFileScanner.SaveCachedResults()
            End If

        Catch ex As System.Exception
            If mQuietMode Then
                Throw ex
            Else
                MsgBox("Error occurred: " & ControlChars.NewLine & ex.Message, MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "Error")
            End If
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As SharedVBNetRoutines.clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String
        Dim strValidParameters() As String = New String() {"I", "O", "P", "S", "R", "Z", "Q"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
                Return False
            Else

                ' Query objParseCommandLine to see if various parameters are present
                With objParseCommandLine
                    If .RetrieveValueForParameter("I", strValue) Then mInputDataFilePath = strValue
                    If .RetrieveValueForParameter("O", strValue) Then mOutputFolderName = strValue
                    If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

                    If .RetrieveValueForParameter("S", strValue) Then
                        mRecurseFolders = True
                        If IsNumeric(strValue) Then
                            mRecurseFoldersMaxLevels = CInt(strValue)
                        End If
                    End If

                    If .RetrieveValueForParameter("R", strValue) Then mReprocessingExistingFiles = True
                    If .RetrieveValueForParameter("Z", strValue) Then mReprocessIfCachedSizeIsZero = True
                    If .RetrieveValueForParameter("Q", strValue) Then mQuietMode = True
                End With

                Return True
            End If

        Catch ex As System.Exception
            If mQuietMode Then
                Throw New System.Exception("Error parsing the command line parameters", ex)
            Else
                MsgBox("Error parsing the command line parameters: " & ControlChars.NewLine & ex.Message, MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "Error")
            End If
        End Try

    End Function

    Private Sub ShowProgramHelp()

        Dim strSyntax As String
        Dim ioPath As System.IO.Path

        Try
            strSyntax = "This program will scan a series of MS data files (or data folders) and extract the acquisition start and end times, number of spectra, and the total size of the data, saving the values in the file " & clsMSFileScanner.DefaultAcquisitionTimeFilename & ". "
            strSyntax &= "Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar .WIFF files, Masslynx .Raw folders, and Bruker 1 folders." & ControlChars.NewLine & ControlChars.NewLine

            strSyntax &= "Program syntax:" & ControlChars.NewLine & ioPath.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location)
            strSyntax &= " /I:InputFilePath.raw [/O:OutputFolderName] [/P:ParamFilePath] [/S:[MaxLevel]] [/R] [/Z] [/Q]" & ControlChars.NewLine & ControlChars.NewLine
            strSyntax &= "The input file path can contain the wildcard character *" & ControlChars.NewLine
            strSyntax &= "The output folder name is optional.  If omitted, the acquisition time file will be created in the program directory.  If included, then a subfolder is created with the name OutputFolderName and the acquisition time file placed there." & ControlChars.NewLine
            strSyntax &= "The param file switch is optional.  If supplied, it should point to a valid XML parameter file.  If omitted, defaults are used." & ControlChars.NewLine
            strSyntax &= "Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine." & ControlChars.NewLine
            strSyntax &= "Use /R to reprocess files that are already defined in the acquisition time file." & ControlChars.NewLine
            strSyntax &= "Use /Z to reprocess files that are already defined in the acquisition time file only if their cached size is 0 bytes." & ControlChars.NewLine
            strSyntax &= "The optional /Q switch will suppress all error messages." & ControlChars.NewLine & ControlChars.NewLine

            strSyntax &= "Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005" & ControlChars.NewLine
            strSyntax &= "Copyright 2005, Battelle Memorial Institute.  All Rights Reserved." & ControlChars.NewLine
            strSyntax &= "This is version " & System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")" & ControlChars.NewLine & ControlChars.NewLine

            strSyntax &= "E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com" & ControlChars.NewLine
            strSyntax &= "Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/"

            If Not mQuietMode Then
                MsgBox(strSyntax, MsgBoxStyle.Information Or MsgBoxStyle.OKOnly, "Syntax")
            End If

        Catch ex As System.Exception
            If mQuietMode Then
                Throw New System.Exception("Error displaying the program syntax", ex)
            Else
                MsgBox("Error displaying the program syntax: " & ControlChars.NewLine & ex.Message, MsgBoxStyle.Exclamation Or MsgBoxStyle.OKOnly, "Error")
            End If
        End Try

    End Sub


End Module
