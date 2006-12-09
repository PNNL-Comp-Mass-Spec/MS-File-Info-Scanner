Set NoCount On

-- Retrieve the list of storage paths associated with datasets that were captured within the last 2 days
SELECT CONVERT(varchar(150), 'MSFileInfoScanner.exe /i:' + Storage_Path + ' /S:1') AS TheCommand
FROM (SELECT DISTINCT Storage_Path
      FROM (SELECT 	DS.Dataset_ID, DS.DS_created, EL.Entered, 
                	DS.DS_state_ID, DS.DS_storage_path_ID, 
                	S.SP_vol_name_client + S.SP_path AS Storage_Path
            FROM dbo.T_Dataset DS INNER JOIN
                 dbo.T_Event_Log EL ON DS.Dataset_ID = EL.Target_ID AND EL.Target_Type = 4 INNER JOIN
                 dbo.t_storage_path S ON DS.DS_storage_path_ID = S.SP_path_ID
            WHERE (EL.Target_State = 3) AND 
                  (EL.Entered >= GETDATE() - 2) -- AND S.SP_vol_name_client <> '\\proto-7\'
			) RecentDatasetQ
	  ) LookupQ
ORDER BY Storage_Path
