Set NoCount On

-- Retrieve the list of storage paths associated with datasets that were captured within the last 2 days
SELECT   CONVERT(VARCHAR(150),'MSFileInfoScanner.exe /i:' + Proto_Path + ' /S:1') AS TheCommand
FROM     (SELECT DISTINCT Proto_Path,
                          SP_instrument_name
          FROM   (SELECT SPath.SP_vol_name_client + SPath.SP_path AS Proto_Path,
                         DAP.Archive_Path + '\' AS Archive_Path,
                         SPath.SP_instrument_name
                  FROM   dbo.T_Dataset DS
                         INNER JOIN dbo.T_Event_Log EL
                           ON DS.Dataset_ID = EL.Target_ID
                              AND EL.Target_Type = 4
                         INNER JOIN dbo.t_storage_path SPath
                           ON DS.DS_storage_path_ID = SPath.SP_path_ID
                         INNER JOIN dbo.V_Dataset_Archive_Path DAP
                           ON DS.Dataset_ID = DAP.Dataset_ID
                  WHERE  (EL.Target_State = 3)
                         AND (EL.Entered >= GETDATE() - 2) -- AND SPath.SP_vol_name_client <> '\\proto-7\'
                 ) RecentDatasetQ) LookupQ
ORDER BY Proto_Path,
         SP_instrument_name



