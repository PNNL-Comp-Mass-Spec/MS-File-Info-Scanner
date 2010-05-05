Option Strict On

' Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.

' See clsMSFileInfoScanner for a program description

Module modMain

    Public Const PROGRAM_DATE As String = "May 5, 2010"

    Private mInputDataFilePath As String            ' This path can contain wildcard characters, e.g. C:\*.raw
    Private mOutputFolderName As String             ' Optional
    Private mParameterFilePath As String            ' Optional
    Private mLogFilePath As String

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer
    Private mIgnoreErrorsWhenRecursing As Boolean

    Private mPreventDuplicateEntriesInAcquisitionTimeFile As Boolean
    Private mReprocessingExistingFiles As Boolean
    Private mReprocessIfCachedSizeIsZero As Boolean
    Private mUseCacheFiles As Boolean

    Private mSaveTICandBPIPlots As Boolean
    Private mSaveLCMS2DPlots As Boolean
    Private mLCMS2DMaxPointsToPlot As Integer
    Private mLCMS2DOverviewPlotDivisor As Integer

    Private mComputeOverallQualityScores As Boolean
    Private mCreateDatasetInfoFile As Boolean

    Private mCheckFileIntegrity As Boolean
    Private mMaximumTextFileLinesToCheck As Integer
    Private mComputeFileHashes As Boolean
    Private mZipFileCheckAllData As Boolean

    Private WithEvents mMSFileScanner As clsMSFileInfoScanner


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
        Dim objParseCommandLine As New clsParseCommandLine
        Dim blnProceed As Boolean

        intReturnCode = 0
        mInputDataFilePath = String.Empty
        mOutputFolderName = String.Empty
        mParameterFilePath = String.Empty
        mLogFilePath = String.Empty

        mRecurseFolders = False
        mRecurseFoldersMaxLevels = 0
        mIgnoreErrorsWhenRecursing = False

        mReprocessingExistingFiles = False
        mReprocessIfCachedSizeIsZero = False
        mUseCacheFiles = False

        mSaveTICandBPIPlots = True
        mSaveLCMS2DPlots = False
        mLCMS2DMaxPointsToPlot = clsLCMSDataPlotter.clsOptions.DEFAULT_MAX_POINTS_TO_PLOT
        mLCMS2DOverviewPlotDivisor = clsMSFileInfoProcessorBaseClass.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR

        mComputeOverallQualityScores = False
        mCreateDatasetInfoFile = False

        mCheckFileIntegrity = False
        mComputeFileHashes = False
        mZipFileCheckAllData = True

        mMaximumTextFileLinesToCheck = clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK

        ''TestZipper("\\proto-6\Db_Backups\Albert_Backup\MT_Shewanella_P196", "*.BAK.zip")
        ''Return 0

        Try
            blnProceed = False
            If objParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(objParseCommandLine) Then blnProceed = True
            End If

            If mInputDataFilePath Is Nothing Then mInputDataFilePath = String.Empty

            If Not blnProceed OrElse _
               objParseCommandLine.NeedToShowHelp OrElse _
               objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount = 0 OrElse _
               mInputDataFilePath.Length = 0 Then
                ShowProgramHelp()
                intReturnCode = -1
            Else
                mMSFileScanner = New clsMSFileInfoScanner

                If mCheckFileIntegrity Then mUseCacheFiles = True

                With mMSFileScanner
                    ' Note: These values will be overridden if /P was used and they are defined in the parameter file

                    .UseCacheFiles = mUseCacheFiles
                    .ReprocessExistingFiles = mReprocessingExistingFiles
                    .ReprocessIfCachedSizeIsZero = mReprocessIfCachedSizeIsZero

                    .SaveTICAndBPIPlots = mSaveTICandBPIPlots
                    .SaveLCMS2DPlots = mSaveLCMS2DPlots
                    .LCMS2DPlotMaxPointsToPlot = mLCMS2DMaxPointsToPlot
                    .LCMS2DOverviewPlotDivisor = mLCMS2DOverviewPlotDivisor

                    .ComputeOverallQualityScores = mComputeOverallQualityScores
                    .CreateDatasetInfoFile = mCreateDatasetInfoFile

                    .CheckFileIntegrity = mCheckFileIntegrity
                    .MaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck
                    .ComputeFileHashes = mComputeFileHashes
                    .ZipFileCheckAllData = mZipFileCheckAllData

                    .IgnoreErrorsWhenRecursing = mIgnoreErrorsWhenRecursing

                    If mLogFilePath.Length > 0 Then
                        .LogMessagesToFile = True
                        .LogFilePath = mLogFilePath
                    End If

                    If Not mParameterFilePath Is Nothing AndAlso mParameterFilePath.Length > 0 Then
                        .LoadParameterFileSettings(mParameterFilePath)
                    End If
                End With

                If mRecurseFolders Then
                    If mMSFileScanner.ProcessMSFilesAndRecurseFolders(mInputDataFilePath, mOutputFolderName, mRecurseFoldersMaxLevels) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mMSFileScanner.ErrorCode
                    End If
                Else
                    If mMSFileScanner.ProcessMSFileOrFolderWildcard(mInputDataFilePath, mOutputFolderName, True) Then
                        intReturnCode = 0
                    Else
                        intReturnCode = mMSFileScanner.ErrorCode
                        If intReturnCode <> 0 Then
                            Console.WriteLine("Error while processing: " & mMSFileScanner.GetErrorMessage())
                        End If
                    End If
                End If

                mMSFileScanner.SaveCachedResults()
            End If

        Catch ex As System.Exception
            Console.WriteLine("Error occurred in modMain->Main: " & ControlChars.NewLine & ex.Message)
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Function GetAppVersion() As String
        'Return System.Windows.Forms.Application.ProductVersion & " (" & PROGRAM_DATE & ")"

        Return System.Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim strValidParameters() As String = New String() {"I", "O", "P", "S", "IE", "L", "C", "M", "H", "QZ", "NoTIC", "LC", "LCDiv", "QS", "DI", "CF", "R", "Z"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(strValidParameters) Then
                Return False
            Else
                With objParseCommandLine
                    ' Query objParseCommandLine to see if various parameters are present
                    If .RetrieveValueForParameter("I", strValue) Then
                        mInputDataFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        ' Treat the first non-switch parameter as the input file
                        mInputDataFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("O", strValue) Then mOutputFolderName = strValue
                    If .RetrieveValueForParameter("P", strValue) Then mParameterFilePath = strValue

                    If .RetrieveValueForParameter("S", strValue) Then
                        mRecurseFolders = True
                        If Integer.TryParse(strValue, 0) Then
                            mRecurseFoldersMaxLevels = CInt(strValue)
                        End If
                    End If
                    If .RetrieveValueForParameter("IE", strValue) Then mIgnoreErrorsWhenRecursing = True

                    If .RetrieveValueForParameter("L", strValue) Then mLogFilePath = strValue

                    If .RetrieveValueForParameter("C", strValue) Then mCheckFileIntegrity = True
                    If .RetrieveValueForParameter("M", strValue) Then
                        If Integer.TryParse(strValue, 0) Then
                            mMaximumTextFileLinesToCheck = CInt(strValue)
                        End If
                    End If

                    If .RetrieveValueForParameter("H", strValue) Then mComputeFileHashes = True
                    If .RetrieveValueForParameter("QZ", strValue) Then mZipFileCheckAllData = False

                    If .RetrieveValueForParameter("NoTIC", strValue) Then mSaveTICandBPIPlots = False

                    If .RetrieveValueForParameter("LC", strValue) Then
                        mSaveLCMS2DPlots = True
                        If Integer.TryParse(strValue, 0) Then
                            mLCMS2DMaxPointsToPlot = CInt(strValue)
                        End If
                    End If

                    If .RetrieveValueForParameter("LCDiv", strValue) Then
                        If Integer.TryParse(strValue, 0) Then
                            mLCMS2DOverviewPlotDivisor = CInt(strValue)
                        End If
                    End If

                    If .RetrieveValueForParameter("QS", strValue) Then mComputeOverallQualityScores = True

                    If .RetrieveValueForParameter("DI", strValue) Then mCreateDatasetInfoFile = True

                    If .RetrieveValueForParameter("CF", strValue) Then mUseCacheFiles = True
                    If .RetrieveValueForParameter("R", strValue) Then mReprocessingExistingFiles = True
                    If .RetrieveValueForParameter("Z", strValue) Then mReprocessIfCachedSizeIsZero = True
                End With

                Return True
            End If

        Catch ex As System.Exception
            Console.WriteLine("Error parsing the command line parameters: " & ControlChars.NewLine & ex.Message)
        End Try

    End Function

    Private Sub ShowProgramHelp()

        Try
            Console.WriteLine("This program will scan a series of MS data files (or data folders) and extract the acquisition start and end times, number of spectra, and the total size of the data, saving the values in the file " & clsMSFileInfoScanner.DefaultAcquisitionTimeFilename & ". " & _
                              "Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar .WIFF files, Masslynx .Raw folders, Bruker 1 folders, and Bruker XMass analysis.baf files")
            Console.WriteLine()

            Console.WriteLine("Program syntax:" & ControlChars.NewLine & System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location))
            Console.WriteLine(" /I:InputFileNameOrFolderPath [/O:OutputFolderName]")
            Console.WriteLine(" [/P:ParamFilePath] [/S[:MaxLevel]] [IE] [/L:LogFilePath]")
            Console.WriteLine(" [/LC[:MaxPointsToPlot]] [/NoTIC] [/DI] [/QS]")
            Console.WriteLine(" [/C] [/M:nnn] [/H] /[QZ]")
            Console.WriteLine(" [/CF] [/R] [/Z]")
            Console.WriteLine()
            Console.WriteLine("Use /I to specify the name of a file or folder to scan; the path can contain the wildcard character *")
            Console.WriteLine("The output folder name is optional.  If omitted, the output files will be created in the program directory.")
            Console.WriteLine()

            Console.WriteLine("The param file switch is optional.  If supplied, it should point to a valid XML parameter file.  If omitted, defaults are used.")
            Console.WriteLine("Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. Use /IE to ignore errors when recursing.")
            Console.WriteLine("Use /L to specify the file path for logging messages.")
            Console.WriteLine()

            Console.WriteLine("Use /LC to create 2D LCMS plots (this process could take several minutes for each dataset).  By default, plots the top " & clsLCMSDataPlotter.clsOptions.DEFAULT_MAX_POINTS_TO_PLOT & " points.  To plot the top 20000 points, use /L:20000.")
            Console.WriteLine("Use /LCDiv to specify the divisor to use when creating the overview 2D LCMS plots.  By default, uses /LCDiv:" & clsMSFileInfoProcessorBaseClass.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR & "; use /LCDiv:0 to disable creation of the overview plots.")
            Console.WriteLine("Use /NoTIC to not save TIC and BPI plots.")
            Console.WriteLine("Use /DI to create a dataset info XML file for each dataset.")
            Console.WriteLine("Use /QS to compute an overall quality score for the data in each datasets.")
            Console.WriteLine()

            Console.WriteLine("Use /C to perform an integrity check on all known file types; this process will open known file types and verify that they contain the expected data.  This option is only used if you specify an Input Folder and use a wildcard; you will typically also want to use /S when using /C.")
            Console.WriteLine("Use /M to define the maximum number of lines to process when checking text or csv files; default is /M:" & clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK.ToString)
            Console.WriteLine()

            Console.WriteLine("Use /H to compute Sha-1 file hashes when verifying file integrity.")
            Console.WriteLine("Use /QZ to run a quick zip-file validation test when verifying file integrity (the test does not check all data in the .Zip file).")
            Console.WriteLine()

            Console.WriteLine("Use /CF to save/load information from the acquisition time file (cache file).  This option is auto-enabled if you use /C.")
            Console.WriteLine("Use /R to reprocess files that are already defined in the acquisition time file.")
            Console.WriteLine("Use /Z to reprocess files that are already defined in the acquisition time file only if their cached size is 0 bytes.")
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/")

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

        Catch ex As System.Exception
            Console.WriteLine("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub


    Private Sub mMSFileScanner_ErrorEvent(ByVal Message As String) Handles mMSFileScanner.ErrorEvent
        ' We could any error messages here
        ' However, mMSFileScanner already will have written out to the console, so there is no need to do so again
    End Sub

    Private Sub mMSFileScanner_MessageEvent(ByVal Message As String) Handles mMSFileScanner.MessageEvent
        ' We could any status messages here
        ' However, mMSFileScanner already will have written out to the console, so there is no need to do so again
    End Sub

End Module
