== Overview ==

The MS File Info Scanner can be used to scan a series of MS data files (or 
data folders) and extract the acquisition start and end times, number of spectra,
and the total size of the data, saving the values in the file DatasetTimeFile.txt

Supported file types are:
 * Finnigan .RAW files 
 * Agilent Ion Trap (.D folders)
 * Agilent or QStar .WIFF files
 * Masslynx .Raw folders
 * Bruker 1 folders
 * Bruker XMass analysis.baf
 * .UIMF files (IMS)
 * DeconTools _isos.csv files (uses the _scans.csv file for elution time info)

Note that Finnigan's Xcalibur software needs to be installed in order to read 
.Raw files.  In addition, note that the C12.pek and Normal.pek files need to be
present in the folder to avoid seeing error messages from DeconWrapper.dll

== Syntax ==

Program syntax:
MSFileInfoScanner.exe
 /I:InputFileNameOrFolderPath [/O:OutputFolderName]
 [/P:ParamFilePath] [/S[:MaxLevel]] [/IE] [/L:LogFilePath]
 [/LC[:MaxPointsToPlot]] [/NoTIC] [/LCGrad]
 [/DI] [/SS] [/QS] [/CC]
 [/DST:DatasetStatsFileName]
 [/ScanStart:0] [/ScanEnd:0] [/Debug]
 [/C] [/M:nnn] [/H] [/QZ]
 [/CF] [/R] [/Z]
 [/PostToDMS] [/PythonPlot]

Use /I to specify the name of a file or folder to scan; the path can contain the wildcard character *
The output folder name is optional.  If omitted, the output files will be created in the program directory.

The param file switch is optional.  If supplied, it should point to a valid XML parameter file.  
If omitted, defaults are used.

Use /S to process all valid files in the input folder and subfolders. Include a number after /S (like /S:2)
to limit the level of subfolders to examine. Use /IE to ignore errors when recursing.

Use /L to specify the file path for logging messages.

Use /LC to create 2D LCMS plots (this process could take several minutes for each dataset).  
By default, plots the top 200000 points.  To plot the top 20000 points, use /LC:20000.

Use /LCDiv to specify the divisor to use when creating the overview 2D LCMS plots.  By default, 
uses /LCDiv:10; use /LCDiv:0 to disable creation of the overview plots.

Use /NoTIC to not save TIC and BPI plots.

Use /LCGrad to save a series of 2D LC plots, each using a different color scheme.  
The default color scheme is OxyPalettes.Jet

Use /DatasetID:# to define the dataset's DatasetID value (where # is an integer); 
only appropriate if processing a single dataset.

Use /DI to create a dataset info XML file for each dataset.

Use /SS to create a _ScanStats.txt  file for each dataset.
Use /QS to compute an overall quality score for the data in each datasets.
Use /CC to check spectral data for whether it is centroided or profile

Use /DST to update (or create) a tab-delimited text file with overview stats for the dataset.  
If /DI is used, will include detailed scan counts; otherwise, will just have the dataset name, 
acquisition date, and (if available) sample name and comment. By default, the file is named
MSFileInfo_DatasetStats.txt; to override, add the file name after the /DST switch, for example
/DST:DatasetStatsFileName.txt

Use /ScanStart and /ScanEnd to limit the scan range to process; useful for files where 
the first few scans are corrupt.  For example, to start processing at scan 10, use /ScanStart:10

Use /Debug to display debug information at the console, including showing the scan number 
prior to reading each scan's data

Use /C to perform an integrity check on all known file types; this process will open known file types 
and verify that they contain the expected   This option is only used if you specify an Input Folder 
and use a wildcard; you will typically also want to use /S when using /C.

Use /M to define the maximum number of lines to process when checking text or csv files; default is /M:500

Use /H to compute Sha-1 file hashes when verifying file integrity.

Use /QZ to run a quick zip-file validation test when verifying file integrity 
(the test does not check all data in the .Zip file).

Use /CF to save/load information from the acquisition time file (cache file).  
This option is auto-enabled if you use /C.

Use /R to reprocess files that are already defined in the acquisition time file.

Use /Z to reprocess files that are already defined in the acquisition time file 
only if their cached size is 0 bytes.

Use /PostToDMS to store the dataset info in the DMS database.  To customize the server name 
and/or stored procedure to use for posting, use an XML parameter file with settings 
DSInfoConnectionString, DSInfoDBPostingEnabled, and DSInfoStoredProcedure

Use /PythonPlot to create plots with Python instead of OxyPlot

Known file extensions: .RAW, .WIFF, .BAF, .MCF, .MCF_IDX, .UIMF, _isos.CSV
Known folder extensions: .D, .RAW

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
