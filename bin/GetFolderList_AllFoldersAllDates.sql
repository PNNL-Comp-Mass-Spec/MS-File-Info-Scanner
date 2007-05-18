set nocount on


SELECT   CONVERT(VARCHAR(150),'MSFileInfoScanner.exe /i:' + Proto_Path + ' /S:1') AS TheCommand
FROM     (SELECT DISTINCT SPath.SP_vol_name_client + SPath.SP_path AS Proto_Path,
                          DAP.Archive_Path + '\' AS Archive_Path,
                          SPath.SP_instrument_name
          FROM   dbo.t_storage_path SPath
                 INNER JOIN dbo.T_Dataset DS
                   ON SPath.SP_path_ID = DS.DS_storage_path_ID
                 INNER JOIN dbo.V_Dataset_Archive_Path DAP
                   ON DS.Dataset_ID = DAP.Dataset_ID
          WHERE  (DS.DS_created >= GETDATE() - 220)
                 AND (SPath.SP_function IN ('raw-storage','old-storage'))) LookupQ
--WHERE    (Archive_Path <> 'unassigned\')
ORDER BY Proto_Path,
         SP_instrument_name

