; This is an Inno Setup configuration file
; https://jrsoftware.org/isinfo.php

#define ApplicationVersion GetFileVersion('..\MSFileInfoScanner\bin\MSFileInfoScanner.exe')

[CustomMessages]
AppName=MS File Info Scanner

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.
; Example with multiple lines:
; WelcomeLabel2=Welcome message%n%nAdditional sentence

[Files]
Source: ..\MSFileInfoScanner\bin\MSFileInfoScanner.exe                         ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\MSFileInfoScanner.pdb                         ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\agtsampleinforw.dll                           ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BaseCommon.dll                                ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BaseDataAccess.dll                            ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BaseError.dll                                 ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BaseTof.dll                                   ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BDal.CXt.Lc.dll                               ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BDal.CXt.Lc.Factory.dll                       ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BDal.CXt.Lc.Interfaces.dll                    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BDal.CXt.Lc.UntU2.dll                         ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\BrukerDataReader.dll                          ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ChemstationMSFileReader.dll                   ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\DataReader.dll                                ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\Interop.EDAL.dll                              ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\Interop.HSREADWRITELib.dll                    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\Ionic.Zip.dll                                 ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\MassSpecDataReader.dll                        ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\MathNet.Numerics.dll                          ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\Microsoft.Bcl.AsyncInterfaces.dll             ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\MIDAC.dll                                     ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\MSFileInfoScannerInterfaces.dll               ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\netcdf.dll                                    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\NetCDFReader.dll                              ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\Npgsql.dll                                    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\OxyPlot.dll                                   ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\OxyPlot.Wpf.dll                               ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\PRISM.dll                                     ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\PRISMDatabaseUtils.dll                        ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ProteowizardWrapper.dll                       ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\SpectraTypeClassifier.dll                     ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Buffers.dll                            ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Data.SQLite.dll                        ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Memory.dll                             ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Numerics.Vectors.dll                   ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Runtime.CompilerServices.Unsafe.dll    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Text.Encodings.Web.dll                 ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Text.Json.dll                          ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.Threading.Tasks.Extensions.dll         ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\System.ValueTuple.dll                         ; DestDir: {app}
Source: ..\MSFileInfoScanner\lib\RawFileReaderLicense.doc                              ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ThermoFisher.CommonCore.BackgroundSubtraction.dll     ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ThermoFisher.CommonCore.Data.dll                      ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ThermoFisher.CommonCore.MassPrecisionEstimator.dll    ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ThermoFisher.CommonCore.RawFileReader.dll             ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\ThermoRawFileReader.dll                               ; DestDir: {app}
Source: ..\MSFileInfoScanner\bin\UIMFLibrary.dll                                       ; DestDir: {app}

Source: ..\Readme.md                                                 ; DestDir: {app}
Source: ..\RevisionHistory.txt                                       ; DestDir: {app}

Source: ..\docs\MSFileInfoScanner_ProcessingOptions_AllPlots.txt              ; DestDir: {app}
Source: ..\docs\MSFileInfoScanner_ProcessingOptions_AllPlots_ValidateTMT.txt  ; DestDir: {app}
Source: ..\docs\MSFileInfoScanner_ProcessingOptions_NoPlots.txt               ; DestDir: {app}
Source: ..\docs\MSFileInfoScanner_ProcessingOptions_TICandBPI.txt             ; DestDir: {app}

Source: ..\Python\MSFileInfoScanner_Plotter.py                       ; DestDir: {app}
Source: ..\Python\Python_Setup.txt                                   ; DestDir: {app}

Source: Images\delete_16x.ico                                        ; DestDir: {app}

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
AppPublisherURL=https://omics.pnl.gov/software
AppSupportURL=https://omics.pnl.gov/software
AppUpdatesURL=https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner
ArchitecturesAllowed=x64 x86
ArchitecturesInstallIn64BitMode=x64
DefaultDirName={autopf}\MSFileInfoScanner
DefaultGroupName=PAST Toolkit
AppCopyright=© PNNL
;LicenseFile=.\License.rtf
PrivilegesRequired=admin
OutputBaseFilename=MSFileInfoScanner_Installer
VersionInfoVersion={#ApplicationVersion}
VersionInfoCompany=PNNL
VersionInfoDescription=MS File Info Scanner
VersionInfoCopyright=PNNL
DisableFinishedPage=yes
DisableWelcomePage=no
ShowLanguageDialog=no
ChangesAssociations=no
WizardStyle=modern
EnableDirDoesntExistWarning=no
AlwaysShowDirOnReadyPage=yes
UninstallDisplayIcon={app}\delete_16x.ico
ShowTasksTreeLines=yes
OutputDir=.\Output

[Registry]
;Root: HKCR; Subkey: MyAppFile; ValueType: string; ValueName: ; ValueDataMyApp File; Flags: uninsdeletekey
;Root: HKCR; Subkey: MyAppSetting\DefaultIcon; ValueType: string; ValueData: {app}\wand.ico,0; Flags: uninsdeletevalue

[UninstallDelete]
Name: {app}; Type: filesandordirs
