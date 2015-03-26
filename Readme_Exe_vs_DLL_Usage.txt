The CaptureTaskManager uses MSFileInfoScanner.dll to create the QC plots that appear in the QC folder below datasets

The Analysis Manager uses MSFileInfoScanner.dll to generate scan stats files, and also to generate QC plots after running DeconTools


MSFileInfoScanner.exe was previously used by the CaptureTaskManager, but we switched to using the DLL in 2015.
Thus, the .Exe is now only used for manual processing of datasets.
