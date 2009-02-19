@echo on
Del PRISM_Class_Library_Source*.zip
Del SharedVBNetRoutines_Source*.zip
Del NetCDFReader_Source*.zip
Del SharpZipLib_Source*.zip

@echo off
echo;

Copy "F:\My Documents\Projects\DataMining\DMS_Managers\PRISM_Class_Library\PRISM_Class_Library_Source*.zip" .
Copy "F:\My Documents\Projects\DataMining\SharedVBNetRoutines\SharedVBNetRoutines_SourceCode\*.zip" .
Copy "F:\My Documents\Projects\DataMining\NETCDF.NET\NetCDFReader_SourceCode\*.zip" .
Copy "F:\My Documents\Projects\DataMining\SharpZipLib.NET\SharpZipLib_SourceCode\*.zip" .

