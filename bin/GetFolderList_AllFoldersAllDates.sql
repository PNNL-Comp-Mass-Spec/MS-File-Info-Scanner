set nocount on

SELECT Convert(varchar(150), 'MSFileInfoScanner.exe /i:' + Storage_Path + ' /S:1') AS TheCommand
FROM (SELECT DISTINCT 
          dbo.t_storage_path.SP_vol_name_client + dbo.t_storage_path.SP_path AS Storage_Path, SP_instrument_name
      FROM dbo.t_storage_path INNER JOIN
          dbo.T_Dataset ON 
          dbo.t_storage_path.SP_path_ID = dbo.T_Dataset.DS_storage_path_ID
      WHERE ds_created >= GetDate() - 220 AND (dbo.t_storage_path.SP_function IN ('raw-storage', 'old-storage'))) 
    LookupQ
ORDER BY Storage_Path, SP_instrument_name