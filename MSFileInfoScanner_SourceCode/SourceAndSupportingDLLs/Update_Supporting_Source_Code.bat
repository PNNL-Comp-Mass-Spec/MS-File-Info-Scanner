@echo on
Del SharedVBNetRoutines_Source*.zip
Del NetCDFReader_Source*.zip
Del SharpZipLib_Source*.zip

@echo off
echo;

Copy "F:\My Documents\Projects\DataMining\SharedVBNetRoutines\SharedVBNetRoutines_SourceCode\*.zip" .
Copy "F:\My Documents\Projects\_OldProjects\NETCDF.NET\NetCDFReader_SourceCode\*.zip" .
Copy "F:\My Documents\Projects\DataMining\SharpZipLib.NET\SharpZipLib_SourceCode\*.zip" .

