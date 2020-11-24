rem Obsolete: xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner_Installer\Debug\MSFileInfoScanner_Installer.msi" \\floyd\Software\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt" \\floyd\Software\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md" \\floyd\Software\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"     "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Program\bin\" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"     "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d
                                                                                                     
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"       "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib\" /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"     "F:\Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\DeployedFiles\" /y /d
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"           "F:\Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common\" /y /d
                                                                                                      
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.exe"               \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"           \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\UIMFLibrary.*"                       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MathNet.Numerics.*"                  \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             C:\DMS_Programs\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             C:\DMS_Programs\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.exe"               C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.xml"               C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.pdb"               C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"           C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"       C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\UIMFLibrary.*"                       C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MathNet.Numerics.*"                  C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             C:\DMS_Programs\MSFileInfoScannerExe /y /d

@echo off
echo.
echo About to copy to \\pnl\projects\OmicsSW\DMS_Programs
echo.
echo Be sure you have built the DLL in Debug mode
echo.

pause
@echo on

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.dll" \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d

xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.exe"               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\Readme.md"                               \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner_Plotter.py"        \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\UIMFLibrary.*"                       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MathNet.Numerics.*"                  \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM*.dll"                      \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\Thermo*.dll"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\BrukerDataReader.dll"            \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.dll"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d
xcopy "F:\Documents\Projects\DataMining\MS_File_Info_Scanner\bin\ProteowizardWrapper.pdb"             \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner_Exe /y /d


pause
