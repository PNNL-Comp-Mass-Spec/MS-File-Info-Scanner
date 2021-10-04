# Overview

The MS File Info Scanner can be used to scan a series of MS data files 
(or data directories) and extract the acquisition start and end times, 
number of spectra, and the total size of the data, saving the values 
in the file DatasetTimeFile.txt

The program can also generate plots of the data, for QC purposes.  Plots include:
* BPI: Base peak intensity vs. scan number (i.e. time)
* TIC: Total ion intensity vs. scan number
* 2D LC/MS plots showing intensity as a function of m/z and scan number
* Analog device plots, e.g. LC pump pressure vs. scan number

Supported file types are:
* Thermo .raw files 
* Agilent Ion Trap (.d directories)
* Agilent or QStar .wiff files
* Waters (Masslynx) .raw folders
* Bruker 1 directories
* Bruker XMass analysis.baf
* .UIMF files (IMS or SLIM)
* DeconTools _isos.csv files (uses the _scans.csv file for elution time info)

## Downloads

Download a .zip file with the installer from:
https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/releases

The release page also includes a .zip file with MSFileInfoScanner.exe

### Example QC Graphics

Links to example QC graphic output files can be found on the documentation page at\
[https://pnnl-comp-mass-spec.github.io/MS-File-Info-Scanner](https://pnnl-comp-mass-spec.github.io/MS-File-Info-Scanner/index.html#results)

## Console Switches

MSFileInfoScanner is a command line application.\
Syntax:

```
MSFileInfoScanner.exe
 /I:InputFileNameOrDirectoryPath [/O:OutputDirectoryPath]
 [/P:ParameterFilePath] [/S[:MaxLevel]]
 [/IE] [/L:LogFilePath]
 [/LC[:MaxPointsToPlot]] [/TIC] [/LCGrad]
 [/DI] [/SS] [/QS] [/CC]
 [/MS2MzMin:MzValue] [/NoHash]
 [/DST:DatasetStatsFileName]
 [/ScanStart:0] [/ScanEnd:0] [/Debug]
 [/C] [/M:nnn] [/H] [/QZ]
 [/CF] [/R] [/Z]
 [/PythonPlot]
 [/Conf:KeyValueParamFilePath] [/CreateParamFile]
```

Use `/I` to specify the name of a file or directory to scan
* The path can contain the wildcard character *

The output directory path is optional
* If omitted, the output files will be created in the program directory

Use `/P` or `/Conf` to define a key/value parameter file with settings to load
* Example Key=Value parameter files
  * [MSFileInfoScanner_ProcessingOptions_NoPlots.txt](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/docs/MSFileInfoScanner_ProcessingOptions_NoPlots.txt)
  * [MSFileInfoScanner_ProcessingOptions_TICandBPI.txt](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/docs/MSFileInfoScanner_ProcessingOptions_TICandBPI.txt)
  * [MSFileInfoScanner_ProcessingOptions_AllPlots.txt](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/docs/MSFileInfoScanner_ProcessingOptions_AllPlots.txt)
  * [MSFileInfoScanner_ProcessingOptions_AllPlots_ValidateTMT.txt](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/docs/MSFileInfoScanner_ProcessingOptions_AllPlots_ValidateTMT.txt)

Use `/S` to process all valid files in the input directory and subdirectories
* Include a number after /S (like `/S:2`) to limit the level of subdirectories to examine
* Use `/IE` to ignore errors when recursing

Use `/L` to specify the file path for logging messages
* For example, `/L:InfoScannerLog.txt`
* Alternatively, define `LogMessagesToFile=True` in a key/value parameter file and optionally define the log file name using `LogFilePath=FilePath.txt`

Use `/LC` to create 2D LCMS plots (this process could take several minutes for each dataset)
* By default, plots the top 200000 points
* To plot the top 20000 points, use `/LC:20000`

Use `/LCDiv` to specify the divisor to use when creating the overview 2D LCMS plots
* By default, uses `/LCDiv:10`
* Use `/LCDiv:0` to disable creation of the overview plots

By default, the MS File Info Scanner creates TIC and BPI plots, showing intensity vs. time
* Use `/TIC:False` to disable creating TIC and BPI plots; also disables any device specific plots
* Plots created:
  * Separate BPI plots for MS and MS2 spectra
  * Single TIC plot for all spectra
* For Thermo .raw files where the acquisition software also controlled an LC system, 
  if the .raw file has pressure data (or similar) stored in it, this program will also create plots of pressure vs. time
  * These are labeled "Addnl Plots" in the index.html file
* For .UIMF files, if the file includes pressure information, a pressure vs. time plot will be created
* The software also creates an html file named `index.html` that shows an overview of each plot, plus a table with scan stats 

Use `/LCGrad` to save a series of 2D LC plots, each using a different color scheme
* The default color scheme is OxyPalettes.Jet

Use `/DatasetID:#` to define the dataset's DatasetID value (where # is an integer)
* Only appropriate if processing a single dataset
* If defined, the DatasetID is included in the dataset info XML file

Use `/DI` to create a dataset info XML file for each dataset

Use `/SS` to create a _ScanStats.txt  file for each dataset

Use `/QS` to compute an overall quality score for the data in each datasets

Use `/CC` to check spectral data for whether it is centroided or profile

Use `/MS2MzMin` to specify a minimum m/z value that all MS/MS spectra should have
* Will report an error if any MS/MS spectra have minimum m/z value larger than the threshold
* Useful for validating instrument files where the sample is iTRAQ or TMT labeled and it is important to detect the reporter ions in the MS/MS spectra
* Select the default minimum m/z for iTRAQ (113) using `/MS2MzMin:iTRAQ`
* Select the default mnimum m/z for TMT (126) using `/MS2MzMin:TMT`
* Specify a custom minimum m/z value using `/MS2MzMin:110`

A SHA-1 hash is computed for the primary instrument data file(s)
* Use `/NoHash` to disable this

Use `/DST` to update (or create) a tab-delimited text file with overview stats for the dataset
* If `/DI` is specified, the file will include detailed scan counts
  * Otherwise, it will just have the dataset name, acquisition date, and (if available) sample name and comment
* By default, the file is named MSFileInfo_DatasetStats.txt
  * To override, add the file name after the `/DST` switch, for example `/DST:DatasetStatsFileName.txt`

Use `/ScanStart` and `/ScanEnd` to limit the scan range to process
* Useful for files where the first few scans are corrupt
* For example, to start processing at scan 10, use `/ScanStart:10`

Use `/Debug` to display debug information at the console, including showing the
scan number prior to reading each scan's data

Use `/C` to perform an integrity check on all known file types
* This process will open known file types and verify that they contain the expected data
* This option is only used if you specify an Input Directory and use a wildcard
  * You will typically also want to use `/S` when using `/C`

Use `/M` to define the maximum number of lines to process when checking text or csv files
* Default is `/M:500`

Use `/H` to compute SHA-1 file hashes when verifying file integrity

Use `/QZ` to run a quick zip-file validation test when verifying file integrity
* When defined, the test does not check all data in the .Zip file

Use `/CF` to save/load information from the acquisition time file (cache file)
* This option is auto-enabled if you use `/C`

Use `/R` to reprocess files that are already defined in the acquisition time file

Use `/Z` to reprocess files that are already defined in the acquisition time file
only if their cached size is 0 bytes

Use `/PostToDMS` to store the dataset info in the DMS database
* To customize the server name and/or stored procedure to use for posting, use an XML parameter file
with the following settings :
  * `DSInfoConnectionString`
  * `DSInfoDBPostingEnabled`
  * `DSInfoStoredProcedure`

By default, plots are created using OxyPlot, which only works on Windows
* Use `/PythonPlot` to create plots with Python instead of OxyPlot
  * Alternatively, set `PythonPlot` to `True` in the key/value parameter file

The processing options can be specified in a parameter file using `/ParamFile:Options.conf` or `/Conf:Options.conf`
* Define options using the format `ArgumentName=Value`
* Lines starting with `#` or `;` will be treated as comments
* Additional arguments on the command line can supplement or override the arguments in the parameter file

Use `/CreateParamFile` to create an example key/value parameter file
* By default, the example parameter file content is shown at the console
* To create a file named Options.conf, use `/CreateParamFile:Options.conf`

Known file extensions: .RAW, .WIFF, .BAF, .MCF, .MCF_IDX, .UIMF, .CSV\
Known directory extensions: .D, .RAW

## Python Plotting Requirements

On Windows, MS File Info Scanner looks for `python.exe` in directories that start with "Python3" or "Python 3", searching below:
* C:\Program Files
* C:\Program Files (x86)
* C:\Users\Username\AppData\Local\Programs
* C:\ProgramData\Anaconda3
* C:\

On Linux, the program assumes Python is at `/usr/bin/python3`

Python plotting requires that three libraries be installed
* numpy
* matplotlib
* pandas

For Python library installation options, see the `Python_Setup.txt` file on GitHub
* https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/Python/Python_Setup.txt

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics

## License

MS File Info Scanner is licensed under the 2-Clause BSD License; you may not use this program 
except in compliance with the License. You may obtain a copy of the License at 
https://opensource.org/licenses/BSD-2-Clause

Copyright 2020 Battelle Memorial Institute

RawFileReader reading tool. Copyright © 2016 by Thermo Fisher Scientific, Inc. All rights reserved.
