The MS File Info Scanner can be used to scan a series of MS data files (or 
data folders) and extract the acquisition start and end times, number of spectra,
and the total size of the data, saving the values in the file DatasetTimeFile.txt

Supported file types are:
 * Finnigan .RAW files 
 * Agilent Ion Trap (.D folders)
 * Agilent or QStar .WIFF files
 * Masslynx .Raw folders
 * Bruker 1 folders

Note that Finnigan's Xcalibur software needs to be installed in order to read 
.Raw files.  In addition, note that the C12.pek and Normal.pek files need to be
present in the folder to avoid seeing error messages from DeconWrapper.dll

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2006, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0

All publications that result from the use of this software should include 
the following acknowledgment statement:
 Portions of this research were supported by the W.R. Wiley Environmental 
 Molecular Science Laboratory, a national scientific user facility sponsored 
 by the U.S. Department of Energy's Office of Biological and Environmental 
 Research and located at PNNL.  PNNL is operated by Battelle Memorial Institute 
 for the U.S. Department of Energy under contract DE-AC05-76RL0 1830.

Notice: This computer software was prepared by Battelle Memorial Institute, 
hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
Department of Energy (DOE).  All rights in the computer software are reserved 
by DOE on behalf of the United States Government and the Contractor as 
provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
SOFTWARE.  This notice including this sentence must appear on any copies of 
this computer software.
