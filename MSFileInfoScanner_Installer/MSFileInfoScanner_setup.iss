; This is an Inno Setup configuration file
; https://jrsoftware.org/isinfo.php

#define ApplicationVersion GetFileVersion('..\bin\MSFileInfoScanner.exe')

[CustomMessages]
AppName=MS File Info Scanner

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.
; Example with multiple lines:
; WelcomeLabel2=Welcome message%n%nAdditional sentence

[Files]
Source: ..\bin\MSFileInfoScanner.exe                         ; DestDir: {app}
Source: ..\bin\MSFileInfoScanner.pdb                         ; DestDir: {app}
Source: ..\bin\agtsampleinforw.dll                           ; DestDir: {app}
Source: ..\bin\BaseCommon.dll                                ; DestDir: {app}
Source: ..\bin\BaseDataAccess.dll                            ; DestDir: {app}
Source: ..\bin\BaseError.dll                                 ; DestDir: {app}
Source: ..\bin\BaseTof.dll                                   ; DestDir: {app}
Source: ..\bin\BDal.CXt.Lc.dll                               ; DestDir: {app}
Source: ..\bin\BDal.CXt.Lc.Factory.dll                       ; DestDir: {app}
Source: ..\bin\BDal.CXt.Lc.Interfaces.dll                    ; DestDir: {app}
Source: ..\bin\BDal.CXt.Lc.UntU2.dll                         ; DestDir: {app}
Source: ..\bin\BrukerDataReader.dll                          ; DestDir: {app}
Source: ..\bin\ChemstationMSFileReader.dll                   ; DestDir: {app}
Source: ..\bin\DataReader.dll                                ; DestDir: {app}
Source: ..\bin\Interop.EDAL.dll                              ; DestDir: {app}
Source: ..\bin\Interop.HSREADWRITELib.dll                    ; DestDir: {app}
Source: ..\bin\Ionic.Zip.dll                                 ; DestDir: {app}
Source: ..\bin\MassSpecDataReader.dll                        ; DestDir: {app}
Source: ..\bin\MathNet.Numerics.dll                          ; DestDir: {app}
Source: ..\bin\Microsoft.Bcl.AsyncInterfaces.dll             ; DestDir: {app}
Source: ..\bin\MIDAC.dll                                     ; DestDir: {app}
Source: ..\bin\MSFileInfoScannerInterfaces.dll               ; DestDir: {app}
Source: ..\bin\netcdf.dll                                    ; DestDir: {app}
Source: ..\bin\NetCDFReader.dll                              ; DestDir: {app}
Source: ..\bin\Npgsql.dll                                    ; DestDir: {app}
Source: ..\bin\OxyPlot.dll                                   ; DestDir: {app}
Source: ..\bin\OxyPlot.Wpf.dll                               ; DestDir: {app}
Source: ..\bin\PRISM.dll                                     ; DestDir: {app}
Source: ..\bin\PRISMDatabaseUtils.dll                        ; DestDir: {app}
Source: ..\bin\ProteowizardWrapper.dll                       ; DestDir: {app}
Source: ..\bin\SpectraTypeClassifier.dll                     ; DestDir: {app}
Source: ..\bin\System.Buffers.dll                            ; DestDir: {app}
Source: ..\bin\System.Data.SQLite.dll                        ; DestDir: {app}
Source: ..\bin\System.Memory.dll                             ; DestDir: {app}
Source: ..\bin\System.Numerics.Vectors.dll                   ; DestDir: {app}
Source: ..\bin\System.Runtime.CompilerServices.Unsafe.dll    ; DestDir: {app}
Source: ..\bin\System.Text.Encodings.Web.dll                 ; DestDir: {app}
Source: ..\bin\System.Text.Json.dll                          ; DestDir: {app}
Source: ..\bin\System.Threading.Tasks.Extensions.dll         ; DestDir: {app}
Source: ..\bin\System.ValueTuple.dll                         ; DestDir: {app}
Source: ..\lib\RawFileReaderLicense.doc                              ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.BackgroundSubtraction.dll     ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.Data.dll                      ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.MassPrecisionEstimator.dll    ; DestDir: {app}
Source: ..\bin\ThermoFisher.CommonCore.RawFileReader.dll             ; DestDir: {app}
Source: ..\bin\ThermoRawFileReader.dll                               ; DestDir: {app}
Source: ..\bin\UIMFLibrary.dll                                       ; DestDir: {app}

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
