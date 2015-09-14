Option Strict On

Imports MSFileInfoScannerInterfaces
' Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005

' See clsMSFileInfoScanner for a program description

Module modMain

    Public Const PROGRAM_DATE As String = "September 14, 2015"

    Private mInputDataFilePath As String            ' This path can contain wildcard characters, e.g. C:\*.raw
    Private mOutputFolderName As String             ' Optional
    Private mParameterFilePath As String            ' Optional
    Private mLogFilePath As String

    Private mRecurseFolders As Boolean
    Private mRecurseFoldersMaxLevels As Integer
    Private mIgnoreErrorsWhenRecursing As Boolean

    Private mReprocessingExistingFiles As Boolean
    Private mReprocessIfCachedSizeIsZero As Boolean
    Private mUseCacheFiles As Boolean

    Private mSaveTICandBPIPlots As Boolean
    Private mSaveLCMS2DPlots As Boolean
    Private mLCMS2DMaxPointsToPlot As Integer
    Private mLCMS2DOverviewPlotDivisor As Integer
    Private mTestLCMSGradientColorSchemes As Boolean

    Private mCheckCentroidingStatus As Boolean

    Private mScanStart As Integer
    Private mScanEnd As Integer
    Private mShowDebugInfo As Boolean

    Private mDatasetID As Integer

    Private mComputeOverallQualityScores As Boolean
    Private mCreateDatasetInfoFile As Boolean
    Private mCreateScanStatsFile As Boolean

    Private mUpdateDatasetStatsTextFile As Boolean
    Private mDatasetStatsTextFileName As String

    Private mCheckFileIntegrity As Boolean
    Private mMaximumTextFileLinesToCheck As Integer
    Private mComputeFileHashes As Boolean
    Private mZipFileCheckAllData As Boolean

    Private WithEvents mMSFileScanner As clsMSFileInfoScanner

    <System.STAThreadAttribute()>
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
        mLCMS2DMaxPointsToPlot = clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT
        mLCMS2DOverviewPlotDivisor = clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR
        mTestLCMSGradientColorSchemes = False

        mCheckCentroidingStatus = False

        mScanStart = 0
        mScanEnd = 0
        mShowDebugInfo = False

        mComputeOverallQualityScores = False
        mCreateDatasetInfoFile = False
        mCreateScanStatsFile = False

        mUpdateDatasetStatsTextFile = False
        mDatasetStatsTextFileName = String.Empty

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
                    .TestLCMSGradientColorSchemes = mTestLCMSGradientColorSchemes

                    .CheckCentroidingStatus = mCheckCentroidingStatus

                    .ScanStart = mScanStart
                    .ScanEnd = mScanEnd
                    .ShowDebugInfo = mShowDebugInfo

                    .ComputeOverallQualityScores = mComputeOverallQualityScores
                    .CreateDatasetInfoFile = mCreateDatasetInfoFile
                    .CreateScanStatsFile = mCreateScanStatsFile

                    .UpdateDatasetStatsTextFile = mUpdateDatasetStatsTextFile
                    .DatasetStatsTextFileName = mDatasetStatsTextFileName

                    .CheckFileIntegrity = mCheckFileIntegrity
                    .MaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck
                    .ComputeFileHashes = mComputeFileHashes
                    .ZipFileCheckAllData = mZipFileCheckAllData

                    .IgnoreErrorsWhenRecursing = mIgnoreErrorsWhenRecursing

                    If mLogFilePath.Length > 0 Then
                        .LogMessagesToFile = True
                        .LogFilePath = mLogFilePath
                    End If

                    .DatasetIDOverride = mDatasetID

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
                            ShowErrorMessage("Error while processing: " & mMSFileScanner.GetErrorMessage())
                        End If
                    End If
                End If

                mMSFileScanner.SaveCachedResults()
            End If

        Catch ex As Exception
            ShowErrorMessage("Error occurred in modMain->Main: " & Environment.NewLine & ex.Message)
            intReturnCode = -1
        End Try

        Return intReturnCode

    End Function

    Private Function GetAppVersion() As String
        Return Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(ByVal objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim lstValidParameters = New List(Of String) From {
            "I", "O", "P", "S", "IE", "L", "C", "M", "H", "QZ", "NoTIC",
            "LC", "LCDiv", "LCGrad",
            "CC", "QS",
            "ScanStart", "ScanEnd",
            "DatasetID", "DI", "DST",
            "SS", "CF", "R", "Z", "Debug"}

        Try
            ' Make sure no invalid parameters are present
            If objParseCommandLine.InvalidParametersPresent(lstValidParameters) Then
                ShowErrorMessage("Invalid commmand line parameters",
                  (From item In objParseCommandLine.InvalidParameters(lstValidParameters) Select "/" + item).ToList())
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

                    If .IsParameterPresent("C") Then mCheckFileIntegrity = True
                    If .RetrieveValueForParameter("M", strValue) Then
                        If Integer.TryParse(strValue, 0) Then
                            mMaximumTextFileLinesToCheck = CInt(strValue)
                        End If
                    End If

                    If .IsParameterPresent("H") Then mComputeFileHashes = True
                    If .IsParameterPresent("QZ") Then mZipFileCheckAllData = False

                    If .IsParameterPresent("NoTIC") Then mSaveTICandBPIPlots = False

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

                    If .IsParameterPresent("LCGrad") Then mTestLCMSGradientColorSchemes = True

                    If .IsParameterPresent("CC") Then mCheckCentroidingStatus = True

                    If .RetrieveValueForParameter("ScanStart", strValue) Then
                        If Integer.TryParse(strValue, 0) Then
                            mScanStart = CInt(strValue)
                        End If
                    End If

                    If .RetrieveValueForParameter("ScanEnd", strValue) Then
                        If Integer.TryParse(strValue, 0) Then
                            mScanEnd = CInt(strValue)
                        End If
                    End If

                    If .IsParameterPresent("Debug") Then mShowDebugInfo = True

                    If .IsParameterPresent("QS") Then mComputeOverallQualityScores = True

                    If .RetrieveValueForParameter("DatasetID", strValue) Then
                        If Not Integer.TryParse(strValue, mDatasetID) Then
                            ShowErrorMessage("DatasetID is not an integer")
                            Return False
                        End If
                    End If

                    If .IsParameterPresent("DI") Then mCreateDatasetInfoFile = True

                    If .IsParameterPresent("SS") Then mCreateScanStatsFile = True

                    If .RetrieveValueForParameter("DST", strValue) Then
                        mUpdateDatasetStatsTextFile = True
                        If Not String.IsNullOrEmpty(strValue) Then
                            mDatasetStatsTextFileName = strValue
                        End If
                    End If


                    If .IsParameterPresent("CF") Then mUseCacheFiles = True
                    If .IsParameterPresent("R") Then mReprocessingExistingFiles = True
                    If .IsParameterPresent("Z") Then mReprocessIfCachedSizeIsZero = True

                End With

                Return True
            End If

        Catch ex As Exception
            ShowErrorMessage("Error parsing the command line parameters: " & Environment.NewLine & ex.Message)
            Return False
        End Try

    End Function

    Private Function CollapseList(ByVal lstList As List(Of String)) As String
        Dim strCollapsed As String

        If lstList Is Nothing Then
            Return String.Empty
        Else
            strCollapsed = String.Copy(lstList.Item(0))
        End If

        For intIndex As Integer = 1 To lstList.Count - 1
            strCollapsed &= ", " & lstList.Item(intIndex)
        Next

        Return strCollapsed

    End Function

    Private Function CollapseList(ByVal strList() As String) As String
        Dim strCollapsed As String

        If strList Is Nothing Then
            Return String.Empty
        Else
            strCollapsed = String.Copy(strList(0))
        End If

        For intIndex As Integer = 1 To strList.Length - 1
            strCollapsed &= ", " & strList(intIndex)
        Next

        Return strCollapsed
    End Function

    Private Sub ShowErrorMessage(ByVal strMessage As String)
        Const strSeparator As String = "------------------------------------------------------------------------------"

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strMessage)
        Console.WriteLine(strSeparator)
        Console.WriteLine()

        WriteToErrorStream(strMessage)
    End Sub

    Private Sub ShowErrorMessage(ByVal strTitle As String, ByVal items As List(Of String))
        Const strSeparator As String = "------------------------------------------------------------------------------"
        Dim strMessage As String

        Console.WriteLine()
        Console.WriteLine(strSeparator)
        Console.WriteLine(strTitle)
        strMessage = strTitle & ":"

        For Each item As String In items
            Console.WriteLine("   " + item)
            strMessage &= " " & item
        Next
        Console.WriteLine(strSeparator)
        Console.WriteLine()

        WriteToErrorStream(strMessage)
    End Sub

    Private Sub ShowProgramHelp()

        Try
            mMSFileScanner = New clsMSFileInfoScanner

            Console.WriteLine("This program will scan a series of MS data files (or data folders) and extract the acquisition start and end times, number of spectra, and the total size of the data, saving the values in the file " & clsMSFileInfoScanner.DefaultAcquisitionTimeFilename & ". " & _
               "Supported file types are Finnigan .RAW files, Agilent Ion Trap (.D folders), Agilent or QStar/QTrap .WIFF files, Masslynx .Raw folders, Bruker 1 folders, Bruker XMass analysis.baf files, .UIMF files (IMS), and zipped Bruker imaging datasets (with 0_R*.zip files)")
            Console.WriteLine()

            Console.WriteLine("Program syntax:" & Environment.NewLine & Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location))
            Console.WriteLine(" /I:InputFileNameOrFolderPath [/O:OutputFolderName]")
            Console.WriteLine(" [/P:ParamFilePath] [/S[:MaxLevel]] [/IE] [/L:LogFilePath]")
            Console.WriteLine(" [/LC[:MaxPointsToPlot]] [/NoTIC] [/LCGrad]")
            Console.WriteLine(" [/DI] [/SS] [/QS] [/CC]")
            Console.WriteLine(" [/DST:DatasetStatsFileName]")
            Console.WriteLine(" [/ScanStart:0] [/ScanEnd:0] [/Debug]")
            Console.WriteLine(" [/C] [/M:nnn] [/H] [/QZ]")
            Console.WriteLine(" [/CF] [/R] [/Z]")
            Console.WriteLine()
            Console.WriteLine("Use /I to specify the name of a file or folder to scan; the path can contain the wildcard character *")
            Console.WriteLine("The output folder name is optional.  If omitted, the output files will be created in the program directory.")
            Console.WriteLine()

            Console.WriteLine("The param file switch is optional.  If supplied, it should point to a valid XML parameter file.  If omitted, defaults are used.")
            Console.WriteLine("Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2) to limit the level of subfolders to examine. Use /IE to ignore errors when recursing.")
            Console.WriteLine("Use /L to specify the file path for logging messages.")
            Console.WriteLine()

            Console.WriteLine("Use /LC to create 2D LCMS plots (this process could take several minutes for each dataset).  By default, plots the top " & clsLCMSDataPlotterOptions.DEFAULT_MAX_POINTS_TO_PLOT & " points.  To plot the top 20000 points, use /LC:20000.")
            Console.WriteLine("Use /LCDiv to specify the divisor to use when creating the overview 2D LCMS plots.  By default, uses /LCDiv:" & clsLCMSDataPlotterOptions.DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR & "; use /LCDiv:0 to disable creation of the overview plots.")
            Console.WriteLine("Use /NoTIC to not save TIC and BPI plots.")
            Console.WriteLine("Use /LCGrad to save a series of 2D LC plots, each using a different color scheme.  The default color scheme is OxyPalettes.Jet")
            Console.WriteLine()
            Console.WriteLine("Use /DatasetID:# to define the dataset's DatasetID value (where # is an integer); only appropriate if processing a single dataset")
            Console.WriteLine("Use /DI to create a dataset info XML file for each dataset.")
            Console.WriteLine()
            Console.WriteLine("Use /SS to create a _ScanStats.txt  file for each dataset.")
            Console.WriteLine("Use /QS to compute an overall quality score for the data in each datasets.")
            Console.WriteLine("Use /CC to check spectral data for whether it is centroided or profile")
            Console.WriteLine()

            Console.WriteLine("Use /DST to update (or create) a tab-delimited text file with overview stats for the dataset.  If /DI is used, then will include detailed scan counts; otherwise, will just have the dataset name, acquisition date, and (if available) sample name and comment. By default, the file is named " & DSSummarizer.clsDatasetStatsSummarizer.DEFAULT_DATASET_STATS_FILENAME & "; to override, add the file name after the /DST switch, for example /DST:DatasetStatsFileName.txt")
            Console.WriteLine()

            Console.WriteLine("Use /ScanStart and /ScanEnd to limit the scan range to process; useful for files where the first few scans are corrupt.  For example, to start processing at scan 10, use /ScanStart:10")
            Console.WriteLine("Use /Debug to display debug information at the console, including showing the scan number prior to reading each scan's data")
            Console.WriteLine()

            Console.WriteLine("Use /C to perform an integrity check on all known file types; this process will open known file types and verify that they contain the expected   This option is only used if you specify an Input Folder and use a wildcard; you will typically also want to use /S when using /C.")
            Console.WriteLine("Use /M to define the maximum number of lines to process when checking text or csv files; default is /M:" & clsFileIntegrityChecker.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK.ToString)
            Console.WriteLine()

            Console.WriteLine("Use /H to compute Sha-1 file hashes when verifying file integrity.")
            Console.WriteLine("Use /QZ to run a quick zip-file validation test when verifying file integrity (the test does not check all data in the .Zip file).")
            Console.WriteLine()

            Console.WriteLine("Use /CF to save/load information from the acquisition time file (cache file).  This option is auto-enabled if you use /C.")
            Console.WriteLine("Use /R to reprocess files that are already defined in the acquisition time file.")
            Console.WriteLine("Use /Z to reprocess files that are already defined in the acquisition time file only if their cached size is 0 bytes.")
            Console.WriteLine()

            Console.WriteLine("Known file extensions: " & CollapseList(mMSFileScanner.GetKnownFileExtensionsList()))
            Console.WriteLine("Known folder extensions: " & CollapseList(mMSFileScanner.GetKnownFolderExtensionsList()))
            Console.WriteLine()

            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2005")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            System.Threading.Thread.Sleep(750)

        Catch ex As Exception
            ShowErrorMessage("Error displaying the program syntax: " & ex.Message)
        End Try

    End Sub

    Private Sub WriteToErrorStream(strErrorMessage As String)
        Try
            Using swErrorStream = New StreamWriter(Console.OpenStandardError())
                swErrorStream.WriteLine(strErrorMessage)
            End Using
        Catch ex As Exception
            ' Ignore errors here
        End Try
    End Sub

    Private Sub mMSFileScanner_ErrorEvent(ByVal Message As String) Handles mMSFileScanner.ErrorEvent
        ' We could display any error messages here
        ' However, mMSFileScanner already will have written out to the console, so there is no need to do so again

        WriteToErrorStream(Message)
    End Sub

    Private Sub mMSFileScanner_MessageEvent(ByVal Message As String) Handles mMSFileScanner.MessageEvent
        ' We could display any status messages here
        ' However, mMSFileScanner already will have written out to the console, so there is no need to do so again
    End Sub

End Module
