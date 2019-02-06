; This is an Inno Setup configuration file
; http://www.jrsoftware.org/isinfo.php

#define ApplicationVersion GetFileVersion('..\bin\MSFileInfoScanner.exe')

[CustomMessages]
AppName=MS File Info Scanner

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.
; Example with multiple lines:
; WelcomeLabel2=Welcome message%n%nAdditional sentence

[Files]
Source: ..\bin\MSFileInfoScanner.exe                 ; DestDir: {app}
Source: ..\bin\MSFileInfoScanner.pdb                 ; DestDir: {app}
Source: ..\bin\BrukerDataReader.dll                  ; DestDir: {app}
Source: ..\bin\ChemstationMSFileReader.dll           ; DestDir: {app}
Source: ..\bin\Ionic.Zip.dll                         ; DestDir: {app}
Source: ..\bin\MathNet.Numerics.dll                  ; DestDir: {app}
Source: ..\bin\MSFileInfoScannerInterfaces.dll       ; DestDir: {app}
Source: ..\bin\netcdf.dll                            ; DestDir: {app}
Source: ..\bin\NetCDFReader.dll                      ; DestDir: {app}
Source: ..\bin\OxyPlot.dll                           ; DestDir: {app}
Source: ..\bin\OxyPlot.Wpf.dll                       ; DestDir: {app}
Source: ..\bin\PRISM.dll                             ; DestDir: {app}
Source: ..\bin\ProteowizardWrapper.dll               ; DestDir: {app}
Source: ..\bin\SharedVBNetRoutines.dll               ; DestDir: {app}
Source: ..\bin\SpectraTypeClassifier.dll             ; DestDir: {app}
Source: ..\bin\System.Data.SQLite.dll                ; DestDir: {app}
Source: ..\lib\RawFileReaderLicense.doc              ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.BackgroundSubtraction.dll    ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.Data.dll                     ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.MassPrecisionEstimator.dll   ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.RawFileReader.dll            ; DestDir: {app}
Source: ..\bin\ThermoRawFileReader.dll                              ; DestDir: {app}
Source: ..\bin\UIMFLibrary.dll                                      ; DestDir: {app}

Source: ..\Readme.md                                  ; DestDir: {app}
Source: ..\RevisionHistory.txt                        ; DestDir: {app}
Source: Images\delete_16x.ico                         ; DestDir: {app}

[Dirs]
Name: {commonappdata}\MSFileInfoScanner; Flags: uninsalwaysuninstall

[Tasks]
Name: desktopicon; Description: {cm:CreateDesktopIcon}; GroupDescription: {cm:AdditionalIcons}; Flags: unchecked
; Name: quicklaunchicon; Description: {cm:CreateQuickLaunchIcon}; GroupDescription: {cm:AdditionalIcons}; Flags: unchecked

[Icons]
Name: {commondesktop}\MS File Info Scanner; Filename: {app}\MSFileInfoScanner.exe; Tasks: desktopicon; Comment: MSFileInfoScanner
Name: {group}\MS File Info Scanner; Filename: {app}\MSFileInfoScanner.exe; Comment: MS File Info Scanner

[Setup]
AppName=MS File Info Scanner
AppVersion={#ApplicationVersion}
;AppVerName=MSFileInfoScanner
AppID=MSFileInfoScannerId
AppPublisher=Pacific Northwest National Laboratory
AppPublisherURL=http://omics.pnl.gov/software
AppSupportURL=http://omics.pnl.gov/software
AppUpdatesURL=http://omics.pnl.gov/software
DefaultDirName={pf}\MSFileInfoScanner
DefaultGroupName=PAST Toolkit
AppCopyright=© PNNL
;LicenseFile=.\License.rtf
PrivilegesRequired=poweruser
OutputBaseFilename=MSFileInfoScanner_Installer
VersionInfoVersion={#ApplicationVersion}
VersionInfoCompany=PNNL
VersionInfoDescription=MS File Info Scanner
VersionInfoCopyright=PNNL
DisableFinishedPage=true
ShowLanguageDialog=no
ChangesAssociations=false
EnableDirDoesntExistWarning=false
AlwaysShowDirOnReadyPage=true
UninstallDisplayIcon={app}\delete_16x.ico
ShowTasksTreeLines=true
OutputDir=.\Output

[Registry]
;Root: HKCR; Subkey: MyAppFile; ValueType: string; ValueName: ; ValueDataMyApp File; Flags: uninsdeletekey
;Root: HKCR; Subkey: MyAppSetting\DefaultIcon; ValueType: string; ValueData: {app}\wand.ico,0; Flags: uninsdeletevalue

[UninstallDelete]
Name: {app}; Type: filesandordirs
