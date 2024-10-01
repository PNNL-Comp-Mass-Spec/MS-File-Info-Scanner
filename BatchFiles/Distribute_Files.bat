rem Obsolete: xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner_Installer\Debug\MSFileInfoScanner_Installer.msi" \\floyd\Software\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt" \\floyd\Software\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md" \\floyd\Software\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.dll"          "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Program\bin\" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.dll"          "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net48\MSFileInfoScannerInterfaces.*"            "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net48" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.pdb" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.xml" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.dll" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\net8.0-windows" /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScannerInterfaces\bin\Debug\net8.0-windows\MSFileInfoScannerInterfaces.dll" "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\DeployedFiles\net8.0-windows\" /Y /D
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScannerInterfaces.*"       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d

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
echo About to copy to \\pnl\projects\OmicsSW\DMS_Programs
echo.
echo Be sure you have built the Exe in Debug mode
echo.

if not "%1"=="NoPause" pause
@echo on

rem The Capture Task Manager calls MSFileInfoScanner.exe
rem Capture Task Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe.config"        \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.pdb"               \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.xml"               \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe"               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.exe.config"        \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.pdb"               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner.xml"               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\SpectraTypeClassifier.dll"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\*.dll"                               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                                       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                                                 \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\UIMFLibrary.*"                       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MathNet.Numerics.*"                  \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.dll"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\ProteowizardWrapper.pdb"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d

rem The Analysis Manager uses MSFileInfoScanner.dll after it runs DeconTools
rem Analysis Manager parameter "MSFileInfoScannerDir" refers to C:\DMS_Programs\MSFileInfoScanner\DLL
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\*.dll"                           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d /i
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.*"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.xml"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScanner.pdb"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\SpectraTypeClassifier.dll"       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\UIMFLibrary.*"                   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\MathNet.Numerics.*"              \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\PRISM*.dll"                      \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\Thermo*.dll"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\BrukerDataReader.dll"            \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.dll"         \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner\bin\DLL\ProteowizardWrapper.pdb"         \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner\DLL\ /y /d


if not "%1"=="NoPause" pause
