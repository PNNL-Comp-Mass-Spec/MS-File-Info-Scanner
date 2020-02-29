rem clsAgilentGCDFolderInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\Agilent_GC_MS\Froze_Core_2015_S2_10_20_24_Metab.D /python /SS /DI /2D /CC /O:TestData_Results\Agilent_GC_MS

rem clsAgilentIonTrapDFolderInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\Agilent_IonTrap\JHall_HFBA2_DEINO1\JHall_HFBA2_DEINO1.D /python /SS /DI /2D /CC /O:TestData_Results\Agilent_IonTrap_DotD

rem clsAgilentOrQStarWiffFileInfoScanner.cs  (uses clsProteowizardDataParser)
..\bin\MSFileInfoScanner.exe TestData\Agilent_TOF\QC_06_01-3xDil-1b_23jun06_Earth_06-04-02.wiff /python /SS /DI /2D /CC /O:TestData_Results\Agilent_TOF_Wiff

rem clsAgilentTOFDFolderInfoScanner.cs  (uses clsProteowizardDataParser)
..\bin\MSFileInfoScanner.exe TestData\Agilent_QQQ\ISTD_l_MRM_CE10_30Octo15_Merry_40a.d /python /SS /DI /2D /CC /O:TestData_Results\Agilent_QQQ

rem clsBrukerOneFolderInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\9T\Gsulf302_ICR_run1_6May05_Griffin_0305-11\1 /python /SS /DI /2D /CC /O:TestData_Results\BrukerOneFolder
rem ..\bin\MSFileInfoScanner.exe TestData\Ecoli265_run2_29Apr08_Eagle_07-11-19\s001.zip /python /SS /DI /2D /CC /O:TestData_Results\BrukerZippedSFolder

rem clsBrukerXmassFolderInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\12T\2013_06_14_ITO_35um_fullMS_accum3s_0_P4_000001.d\analysis.baf                                 /python /SS /DI /2D /CC /O:TestData_Results\BrukerXmassBAF\12T
..\bin\MSFileInfoScanner.exe \\proto-11\15T_FTICR\2015_3\2015_08_16_MeOH_Blank_1_01_3924\2015_08_16_MeOH_Blank_1_01_3924.d\analysis.baf /python /SS /DI /2D /CC /O:TestData_Results\BrukerXmassBAF\15T

rem clsDeconToolsIsosInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\DeconTools_Isos\mhp_P2_F02_30Oct13\DLS201311011012_Auto991612\mhp_P2_F02_30Oct13_Frodo_13-04-05_isos.csv /python /SS /DI /2D /CC /O:TestData_Results\DeconTools_Isos\mhp_P2_F02
..\bin\MSFileInfoScanner.exe TestData\DeconTools_Isos\QC_Shew_15_01-500ng_4b_08May15_Falcon_15-02-04\DLS201505091327_Auto1194393\QC_Shew_15_01-500ng_4b_08May15_Falcon_15-02-04_isos.csv  /python /SS /DI /2D /CC /O:TestData_Results\DeconTools_Isos\QC_Shew_15_01

rem clsFinniganRawFileInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\Thermo\LCA_FS_PE_pool_13_Orbi_21Nov13_Tiger_13-07-36.raw             /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoLCA
..\bin\MSFileInfoScanner.exe TestData\Thermo\Mini_proteome_CytochromeC02-LCQ-1_22Oct04_Earth_0904-7.RAW    /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoLCQ
..\bin\MSFileInfoScanner.exe \\proto-3\QEHFX02\2020_1\QC_Mam_19_01_rep04_21Feb20_Remus_WBEH-20-02-05\QC_Mam_19_01_rep04_21Feb20_Remus_WBEH-20-02-05.raw     /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoQEHFX
..\bin\MSFileInfoScanner.exe TestData\Thermo\QC_04_1_04Nov04_Pegasus_0804-4_LT-only.RAW                    /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoLTQFT_LTOnly
..\bin\MSFileInfoScanner.exe TestData\Thermo\Sdata_Exp5NQ_PRISM_F21_03Apr17_Smeagol.raw                    /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoMRM
..\bin\MSFileInfoScanner.exe TestData\Thermo\Angiotensin_AllScans.raw                                      /python /ss /di /2D /CC /o:TestData_Results\Thermo\Angiotensin_AllScans
..\bin\MSFileInfoScanner.exe TestData\Thermo\CPTAC_PNNL_Batch1_First50Pep_Exp5_3Day_3_21Mar14_Smeagol_W22511A1.raw /python /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoMRM2
..\bin\MSFileInfoScanner.exe TestData\21T\Neurath_7_SPE_HESI_200K_28Dec18.raw                              /python /SS /DI /2D /CC /O:TestData_Results\Thermo\21T

..\bin\MSFileInfoScanner.exe \\proto-4\QExactHF05\2018_1\MSU1_Prot_Setaria_iTRAQ8_Set1_07_27Feb18_Aragorn_18-01-08\MSU1_Prot_Setaria_iTRAQ8_Set1_07_27Feb18_Aragorn_18-01-08.raw  /python /ms2mzmin:113 /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoITraqBad
..\bin\MSFileInfoScanner.exe \\proto-3\QExactP04\2018_2\EPICON_year1_iTRAQ21_11_prot_QE_Bane_13Apr18_18-03-01\EPICON_year1_iTRAQ21_11_prot_QE_Bane_13Apr18_18-03-01.raw           /python /ms2mzmin:113 /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoITraqGood
..\bin\MSFileInfoScanner.exe \\proto-11\Lumos02\2018_2\15CPTAC_UCEC_P_PNNL_20180503_B4S3_f11_REP-17-12-06\15CPTAC_UCEC_P_PNNL_20180503_B4S3_f11_REP-17-12-06.raw                  /python /ms2mzmin:126 /SS /DI /2D /CC /O:TestData_Results\Thermo\ThermoTMT

rem Future: QC_Shew_18_02_Excerpt.mzML

rem clsMicromassRawFolderInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\Waters_QTOF\FL_050903_QC_05_2_MMQTOF_c4_r11\FL_050903_QC_05_2_MMQTOF_c4_r11.raw     /python /SS /DI /2D /CC /O:TestData_Results\Waters_QTOF
..\bin\MSFileInfoScanner.exe \\proto-4\SynaptG2_01\2019_1\20190204QCShew_Intact_100ng_1\20190204QCShew_Intact_100ng_1.raw /python /SS /DI /2D /CC /O:TestData_Results\Waters_Synapt_HMS
..\bin\MSFileInfoScanner.exe \\proto-4\SynaptG2_01\2019_2\PPS20190130IN2-2A\PPS20190130IN2-2A.raw                         /python /SS /DI /2D /CC /O:TestData_Results\Waters_Synapt_IMS

rem clsUIMFInfoScanner.cs
..\bin\MSFileInfoScanner.exe TestData\IMS_UIMF\EXP-Solv_DBM_100pM_pos_2Dec14_Columbia_DI.uimf                             /python /SS /DI /2D /CC /O:TestData_Results\IMS_UIMF\EXP-Solv_DBM
..\bin\MSFileInfoScanner.exe TestData\IMS_UIMF\LIP-18PC_neg_31Mar14_Columbia_DI.uimf                                      /python /SS /DI /2D /CC /O:TestData_Results\IMS_UIMF\LIP-18PC_neg
..\bin\MSFileInfoScanner.exe TestData\IMS_UIMF\20180611_07_Mix10_2p4__2p41_7p8kH_12v_1ac_s4k_2.uimf                       /python /SS /DI /2D /CC /O:TestData_Results\IMS_UIMF\20180611_07_Mix10_2p4
..\bin\MSFileInfoScanner.exe \\proto-3\IMS08_AgQTOF05\2018_2\CCS_PG181Trans_Pos_3_4May18\CCS_PG181Trans_Pos_3_4May18.uimf /python /SS /DI /2D /CC /O:TestData_Results\IMS_UIMF\CCS_PG181Trans_Pos_3

rem clsZippedImagingFilesScanner.cs
..\bin\MSFileInfoScanner.exe TestData\20120510_grid /python /ms2mzmin:113 /SS /DI /2D /CC /O:TestData_Results\R00X
