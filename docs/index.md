# __<span style="color:#D57500">MS File Info Scanner</span>__
The MS File Info Scanner can be used to scan a series of MS data files (or data directories) and extract the acquisition start and end times, number of spectra, and the total size of the data, saving the values in the file DatasetTimeFile.txt. It also creates TIC and BPI plots of the data, and can optionally create 2D LCMS plots of m/z vs. time.

### Description
Supported file types are:

* Thermo .raw files
* Agilent Ion Trap (.d directories)
* Agilent or QStar .wiff files
* Waters (Masslynx) .raw folders
* Bruker 1 directories
* Bruker XMass analysis.baf
* .UIMF files (IMS or SLIM)
* DeconTools _isos.csv files (uses the _scans.csv file for elution time info)

The software can optionally generate overview plots for visual quality assessment of the data.

### Downloads
* [Latest version](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/releases/latest)
* [Source code on GitHub](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner)

#### Software Instructions
MSFileInfoScanner is a console application. See the [Readme on GitHub](https://github.com/PNNL-Comp-Mass-Spec/MS-File-Info-Scanner/blob/master/Readme.md) for details.

Example command line usage: <br>
`MSFileInfoScanner.exe QC_Mam_19_01.raw /python /DI /2D /CC /O:QC_Results`

* Process QC_Mam_19_01.raw
* Create plots with Python and MSFileInfoScanner_Plotter.py
* Create a dataset info XML file (named QC_Mam_19_01_DatasetInfo.xml)
* Create 2D LC-MS plots (in addition to the standard BPI and TIC plots)
* Check whether spectra are centroided or profile
* Create the output files in a subdirectory named QC_Results

#### Example Results

| Category | Description | Link |
|---|---|---|
| Agilent | GC/MS | [QC Plots](TestData_Results/Agilent_GC_MS/) |
| Agilent | QQQ | [QC Plots](TestData_Results/Agilent_QQQ/) |
| Agilent | AgQTOF | [QC Plots](TestData_Results/AgQTOF/) |
| Bruker | Bruker Xmass BAF 15T | [QC Plots](TestData_Results/BrukerXmassBAF\15T/) |
| DeconTools | QC_Shew_15_01 | [QC Plots](TestData_Results/DeconTools_Isos\QC_Shew_15_01/) |
| UIMF | CCS_PG181Trans_Pos_3 | [QC Plots](TestData_Results/IMS_UIMF\CCS_PG181Trans_Pos_3/) |
| UIMF | EXP-Solv_DBM | [QC Plots](TestData_Results/IMS_UIMF\EXP-Solv_DBM/) |
| Thermo | Angiotensin infusion | [QC Plots](TestData_Results/Thermo/Angiotensin_AllScans/) |
| Thermo | Thermo QE HFX | [QC Plots](TestData_Results/Thermo/ThermoQEHFX/) |
| Thermo | Thermo MRM | [QC Plots](TestData_Results/Thermo/ThermoMRM/) |
| Thermo | Thermo TMT | [QC Plots](TestData_Results/Thermo/ThermoTMT/) |
| Thermo | Thermo with LC device | [QC Plots](TestData_Results/Thermo/ThermoWithDionexLC/) |
| Waters | Waters_QTOF | [QC Plots](TestData_Results/Waters_QTOF/) |
| Waters | Waters_Synapt_HMS | [QC Plots](TestData_Results/Waters_Synapt_HMS/) |
| Waters | Waters_Synapt_IMS | [QC Plots](TestData_Results/Waters_Synapt_IMS/) |

### Acknowledgment

All publications that utilize this software should provide appropriate acknowledgement to PNNL and the MS-File-Info-Scanner GitHub repository. However, if the software is extended or modified, then any subsequent publications should include a more extensive statement, as shown in the Readme file for the given application or on the website that more fully describes the application.

### Disclaimer

These programs are primarily designed to run on Windows machines. Please use them at your own risk. This material was prepared as an account of work sponsored by an agency of the United States Government. Neither the United States Government nor the United States Department of Energy, nor Battelle, nor any of their employees, makes any warranty, express or implied, or assumes any legal liability or responsibility for the accuracy, completeness, or usefulness or any information, apparatus, product, or process disclosed, or represents that its use would not infringe privately owned rights.

Portions of this research were supported by the NIH National Center for Research Resources (Grant RR018522), the W.R. Wiley Environmental Molecular Science Laboratory (a national scientific user facility sponsored by the U.S. Department of Energy's Office of Biological and Environmental Research and located at PNNL), and the National Institute of Allergy and Infectious Diseases (NIH/DHHS through interagency agreement Y1-AI-4894-01). PNNL is operated by Battelle Memorial Institute for the U.S. Department of Energy under contract DE-AC05-76RL0 1830.

We would like your feedback about the usefulness of the tools and information provided by the Resource. Your suggestions on how to increase their value to you will be appreciated. Please e-mail any comments to proteomics@pnl.gov
