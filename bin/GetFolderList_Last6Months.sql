Set NoCount On

-- Retrieve the list of storage paths associated with datasets that were created within the last 6 months
SELECT CONVERT(varchar(150), 'MSFileInfoScanner.exe /i:' + Storage_Path + ' /S:1') AS TheCommand
FROM (SELECT DISTINCT 
          S.SP_vol_name_client + S.SP_path AS Storage_Path, SP_instrument_name
      FROM dbo.t_storage_path S INNER JOIN
          dbo.T_Dataset DS ON 
          S.SP_path_ID = DS.DS_storage_path_ID
      WHERE ds_created >= GetDate() - 180 AND (S.SP_function IN ('raw-storage', 'old-storage'))) 
    LookupQ
ORDER BY Storage_Path