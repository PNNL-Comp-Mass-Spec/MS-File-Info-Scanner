rem Obsolete: xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner_Installer\Debug\MSFileInfoScanner_Installer.msi" \\proto-2\Software\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt" \\proto-2\Software\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md" \\proto-2\Software\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.dll"          "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Program\bin\" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.dll"          "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.*"            "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net48" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.pdb" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.xml" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.dll" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.dll" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\DeployedFiles\net8.0-windows\" /Y /D
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScannerInterfaces.*"       \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\proto-2\Software\MSFileInfoScanner\Exe_Only /y /d

rem The Analysis Manager uses MSFileInfoScanner.dll after it runs DeconTools
rem Analysis Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner\DLL
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\*.dll"                           C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d /i
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.*"             C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.xml"           C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.pdb"           C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\SpectraTypeClassifier.dll"       C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScannerInterfaces.*"   C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\UIMFLibrary.*"                   C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MathNet.Numerics.*"              C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\PRISM*.dll"                      C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ThermoRawFileReader.*"           C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\Thermo*.dll"                     C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\BrukerDataReader.dll"            C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.dll"         C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.pdb"         C:\DMS_Programs\MSFileInfoScanner\DLL\ /y /d

rem The Capture Task Manager calls MSFileInfoScanner.exe
rem Capture Task Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.pdb"               C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScannerInterfaces.*"       C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             C:\DMS_Programs\MSFileInfoScanner /y /d

@echo off
echo.
echo About to copy to \\Proto-3\DMS_Programs_Dist
echo.
echo Be sure you have built the Exe in Debug mode
echo.

if not "%1"=="NoPause" pause
@echo on

rem The Capture Task Manager calls MSFileInfoScanner.exe
rem Capture Task Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe.config"        \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.pdb"               \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.xml"               \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\Proto-3\DMS_Programs_Dist\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe.config"        \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.pdb"               \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.xml"               \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d

rem The Analysis Manager uses MSFileInfoScanner.dll after it runs DeconTools
rem Analysis Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner\DLL
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\*.dll"                           \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d /i
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.*"             \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.xml"           \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.pdb"           \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\SpectraTypeClassifier.dll"       \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\UIMFLibrary.*"                   \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MathNet.Numerics.*"              \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\PRISM*.dll"                      \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ThermoRawFileReader.*"           \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\Thermo*.dll"                     \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\BrukerDataReader.dll"            \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.dll"         \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.pdb"         \\Proto-3\DMS_Programs_Dist\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d


if not "%1"=="NoPause" pause
