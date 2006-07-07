@echo off
rem SELECT 'MSFileInfoScanner.exe /i:' + Storage_Path + ' /S:1' AS TheCommand
rem FROM (SELECT DISTINCT 
rem           dbo.t_storage_path.SP_vol_name_client + dbo.t_storage_path.SP_path AS Storage_Path, SP_instrument_name
rem       FROM dbo.t_storage_path INNER JOIN
rem           dbo.T_Dataset ON 
rem           dbo.t_storage_path.SP_path_ID = dbo.T_Dataset.DS_storage_path_ID
rem       WHERE ds_created >= GetDate() - 180 AND (dbo.t_storage_path.SP_function IN ('raw-storage', 'old-storage'))) 
rem     LookupQ
rem ORDER BY Storage_Path, SP_instrument_name

MSFileInfoScanner.exe /i:\\Proto-3\7T_DMS8\ /S:1
MSFileInfoScanner.exe /i:\\proto-4\9T_DMS1\ /S:1
MSFileInfoScanner.exe /i:\\proto-4\9T_DMS8\ /S:1
MSFileInfoScanner.exe /i:\\Proto-4\9T_Q_DMS3\ /S:1
MSFileInfoScanner.exe /i:\\proto-4\SWT_LCQ2\ /S:1
MSFileInfoScanner.exe /i:\\Proto-5\11T_DMS9\ /S:1
MSFileInfoScanner.exe /i:\\Proto-6\Agilent_TOF2_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-6\C1_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\Proto-6\C2_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-6\D2_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\Proto-6\QTMM1_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-6\XP1_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-6\XP2_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\12T_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-7\FHCRC_LTQ1_DMS1\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\LTQ_1_DMS3\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\LTQ_2_DMS3\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\LTQ_3_DMS3\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\LTQ_4_DMS3\ /S:1
MSFileInfoScanner.exe /i:\\Proto-7\LTQ_FT1_DMS2\ /S:1
MSFileInfoScanner.exe /i:\\proto-7\LTQ_RITE_DMS1\ /S:1
MSFileInfoScanner.exe /i:\\Proto-8\LTQ_FB1_DMS1\ /S:1
MSFileInfoScanner.exe /i:\\proto-9\LTQ_Orb1_DMS2\ /S:1

Copy DatasetTimeFile.txt \\pogo\AcqTimeStats\DatasetTimeFile.txt /y