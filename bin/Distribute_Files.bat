rem Obsolete: xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\MSFileInfoScanner_Installer\Debug\MSFileInfoScanner_Installer.msi" \\floyd\Software\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt" \\floyd\Software\MSFileInfoScanner /y

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"   "F:\My Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Program\bin" /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll"   "F:\My Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common" /y

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"   "F:\My Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\RefLib" /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.dll" "F:\My Documents\Projects\DataMining\DMS_Managers\Capture_Task_Manager\DeployedFiles" /y

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"       "F:\My Documents\Projects\DataMining\DMS_Managers\Analysis_Manager\AM_Common" /y

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.exe"           \\floyd\Software\MSFileInfoScanner\Exe_Only /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"       \\floyd\Software\MSFileInfoScanner\Exe_Only /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"   \\floyd\Software\MSFileInfoScanner\Exe_Only /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                 \\floyd\Software\MSFileInfoScanner\Exe_Only /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\UIMFLibrary.*"                   \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MathNet.Numerics.*"              \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM.dll"                   \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"       \\floyd\Software\MSFileInfoScanner\Exe_Only /y /d

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     c:\dms_programs\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   c:\dms_programs\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              c:\dms_programs\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM.dll"                       c:\dms_programs\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           c:\dms_programs\MSFileInfoScanner /y /d


xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.exe"           C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.xml"           C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScanner.pdb"           C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\SpectraTypeClassifier.dll"       C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MSFileInfoScannerInterfaces.*"   C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                 C:\DMS_Programs\MSFileInfoScannerExe /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\UIMFLibrary.*"                   C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\MathNet.Numerics.*"              C:\DMS_Programs\MSFileInfoScannerExe /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM.dll"                   c:\dms_programs\MSFileInfoScannerExe /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"       c:\dms_programs\MSFileInfoScannerExe /y /d

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM.dll"                       \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\AnalysisToolManagerDistribution\MSFileInfoScanner /y /d

pause

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.dll"   \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution /y

xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.dll"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.pdb"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScanner.xml"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\SpectraTypeClassifier.dll"       \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MSFileInfoScannerInterfaces.*"   \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\RevisionHistory.txt"                     \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\UIMFLibrary.*"                   \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\MathNet.Numerics.*"              \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\PRISM.dll"                       \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d
xcopy "F:\My Documents\Projects\DataMining\MS_File_Info_Scanner\bin\DLL\ThermoRawFileReader.*"           \\pnl\projects\OmicsSW\DMS_Programs\CaptureTaskManagerDistribution\MSFileInfoScanner /y /d

pause
