using System;
using System.Collections.Generic;
using System.IO;
using ExtensionMethods;

// This class will check the integrity of files in a given folder
//
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Started May 16, 2008

namespace MSFileInfoScanner
{
    public class clsFileIntegrityChecker
    {

        public clsFileIntegrityChecker()
        {
            mFileDate = "April 2, 2012";
            InitializeLocalVariables();
        }

        #region "Constants and Enums"
        public const int DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK = 500;

        public const int DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK = 500;
        public const string FILE_EXTENSION_TXT = ".TXT";
        public const string FILE_EXTENSION_LOG = ".LOG";
        public const string FILE_EXTENSION_PARAMS = ".PARAMS";
        public const string FILE_EXTENSION_DAT = ".DAT";
        public const string FILE_EXTENSION_TIC = ".TIC";
        public const string FILE_EXTENSION_ZIP = ".ZIP";
        public const string FILE_EXTENSION_CSV = ".CSV";

        public const string FILE_EXTENSION_XML = ".XML";

        public const string FINNIGAN_RAW_FILE_EXTENSION = ".RAW";

        public const string AGILENT_TOF_OR_QTRAP_FILE_EXTENSION = ".WIFF";
        #endregion

        #region "Structures"

        public struct udtFolderStatsType
        {
            public string FolderPath;
            public int FileCount;
            public int FileCountFailIntegrity;
        }

        public struct udtFileStatsType
        {
            public string FileName;
            public long SizeBytes;
            public DateTime ModificationDate;
            public bool FailIntegrity;

            public string FileHash;
            public void Initialize()
            {
                FileName = string.Empty;
                SizeBytes = 0;
                ModificationDate = DateTime.MinValue;
                FailIntegrity = false;
                FileHash = string.Empty;
            }
        }

        protected struct udtZipFileWorkParamsType
        {
            public string FilePath;
            public bool CheckAllData;
            public bool ZipIsValid;
            public string FailureMessage;
        }
        #endregion

        #region "Classwide Variables"
        protected string mFileDate;

        protected string mStatusMessage;
        protected int mMaximumTextFileLinesToCheck;

        protected int mMaximumXMLElementNodesToCheck;
        protected bool mZipFileCheckAllData;
        protected float mZipFileLargeSizeThresholdMB;

        protected bool mFastZippedSFileCheck;

        protected bool mComputeFileHashes;

        protected udtZipFileWorkParamsType mZipFileWorkParams;
        public event ErrorCaughtEventHandler ErrorCaught;
        public delegate void ErrorCaughtEventHandler(string strMessage);
        public event FileIntegrityFailureEventHandler FileIntegrityFailure;
        public delegate void FileIntegrityFailureEventHandler(string strFilePath, string strMessage);

        #endregion

        #region "Processing Options and Interface Functions"

        /// <summary>
        /// When True, then computes an MD5 hash on every file
        /// </summary>
        public bool ComputeFileHashes {
            get { return mComputeFileHashes; }
            set { mComputeFileHashes = value; }
        }

        public int MaximumTextFileLinesToCheck {
            get { return mMaximumTextFileLinesToCheck; }
            set {
                if (value < 0)
                    value = 0;
                mMaximumTextFileLinesToCheck = value;
            }
        }

        public int MaximumXMLElementNodesToCheck {
            get { return mMaximumXMLElementNodesToCheck; }
            set {
                if (value < 0)
                    value = 0;
                mMaximumXMLElementNodesToCheck = value;
            }
        }

        public string StatusMessage {
            get { return mStatusMessage; }
        }

        /// <summary>
        /// When True, then performs an exhaustive CRC check of each Zip file; otherwise, performs a quick test
        /// </summary>
        public bool ZipFileCheckAllData {
            get { return mZipFileCheckAllData; }
            set { mZipFileCheckAllData = value; }
        }
        #endregion

        private string ByteArrayToString(byte[] arrInput)
        {
            // Converts a byte array into a hex string

            var strOutput = new System.Text.StringBuilder(arrInput.Length);

            for (int i = 0; i <= arrInput.Length - 1; i++) {
                strOutput.Append(arrInput(i).ToString("X2"));
            }

            return strOutput.ToString().ToLower;

        }

        /// <summary>
        /// Checks the integrity of a text file
        /// </summary>
        /// <param name="strFilePath">File path to check</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckTextFile(string strFilePath)
        {

            string strFileNameLower = null;
            bool blnFileIsValid = true;

            strFileNameLower = Path.GetFileName(strFilePath).ToLower;

            // Analysis Manager Summary File
            if (strFileNameLower == "analysissummary.txt") {
                // Free form text file
                // Example contents:
                //  Job Number	306839
                //  Date	5/16/2008 7:49:00 PM
                //  Processor	SeqCluster2
                //  Tool	Sequest
                blnFileIsValid = CheckTextFileWork(strFilePath, 10, 0, new string[] {
                    "Job",
                    "Date",
                    "FileVersion:",
                    "ProductVersion:"
                }, false, true, 2);

                // DEX Manager Summary File
            } else if (strFileNameLower == "dataextractionsummary.txt") {
                // Free form text file
                // Example contents:
                //  Job Number: 306839
                //  Date: 5/16/2008 7:53:50 PM
                //  Processor: Mash-01
                blnFileIsValid = CheckTextFileWork(strFilePath, 5, 0, new string[] {
                    "Job",
                    "Date",
                    "FileVersion:",
                    "ProductVersion:"
                }, false, true, 2);

                // Analysis Manager MetaData file
            } else if (strFileNameLower == "metadata.txt") {
                // Free form text file
                // Example contents (I'm not sure if this file always looks like this):
                //  Proteomics
                //  Mass spectrometer
                //  OU_CN32_002_run3_3Apr08_Draco_07-12-25
                //  Apr  4 2008 10:01AM
                //  LTQ_Orb_1
                blnFileIsValid = CheckTextFileWork(strFilePath, 2, 0, "Proteomics", false, false);

                // MASIC
            } else if (strFileNameLower.EndsWith("_scanstats.txt")) {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Dataset	ScanNumber	ScanTime	ScanType	TotalIonIntensity	BasePeakIntensity	BasePeakMZ	BasePeakSignalToNoiseRatio	IonCount	IonCountRaw
                //  113591	1	0.00968	1	145331	12762	531.0419	70.11	3147	3147
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 6, true);

                // MASIC
            } else if (strFileNameLower.EndsWith("_scanstatsconstant.txt")) {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Setting	Value
                //  AGC	On
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, true);

                // MASIC
            } else if (strFileNameLower.EndsWith("_scanstatsex.txt")) {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Dataset	ScanNumber	Ion Injection Time (ms)	Scan Segment	Scan Event	Master Index	Elapsed Scan Time (sec)	Charge State	Monoisotopic M/Z	MS2 Isolation Width	FT Analyzer Settings	FT Analyzer Message	FT Resolution	Conversion Parameter B	Conversion Parameter C	Conversion Parameter D	Conversion Parameter E	Collision Mode	Scan Filter Text
                //  113591	1	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, true);

                // MASIC
            } else if (strFileNameLower.EndsWith("_msmethod.txt")) {
                // Free form text file
                // Example contents:
                //  Instrument model: LTQ Orbitrap
                //  Instrument name: LTQ Orbitrap
                //  Instrument description: 
                //  Instrument serial number: SN1006B
                //
                //  Creator: LTQ
                //  Last modified: 12/10/2007 by LTQ
                //
                //  MS Run Time (min): 99.50
                blnFileIsValid = CheckTextFileWork(strFilePath, 10, 0, new string[] {
                    "Instrument",
                    "Creator"
                }, false);

                // MASIC
            } else if (strFileNameLower.EndsWith("_sicstats.txt")) {
                // Note: Header line could be missing, but the file will usually (but not always) contain data
                // Example contents:
                //  Dataset	ParentIonIndex	MZ	SurveyScanNumber	FragScanNumber	OptimalPeakApexScanNumber	PeakApexOverrideParentIonIndex	CustomSICPeak	PeakScanStart	PeakScanEnd	PeakScanMaxIntensity	PeakMaxIntensity	PeakSignalToNoiseRatio	FWHMInScans	PeakArea	ParentIonIntensity	PeakBaselineNoiseLevel	PeakBaselineNoiseStDev	PeakBaselinePointsUsed	StatMomentsArea	CenterOfMassScan	PeakStDev	PeakSkew	PeakKSStat	StatMomentsDataCountUsed
                //  113591	0	445.12	8	9	133	79	0	3	86	66	14881	0.4267	78	906920	11468	34870	22736	768	293248	68	6.36	-0.16	0.4162	5
                blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, true);

                // SEQUEST
            } else if (strFileNameLower.StartsWith("cat_log")) {
                // Example contents:
                //  5/16/2008 7:41:55 PM, 14418 'dta' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_dta.txt', Normal, 
                //  5/16/2008 7:48:47 PM, 14418 'out' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_out.txt', Normal, 
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 0, new string[] {
                    "were concatenated",
                    "_dta.txt",
                    "_out.txt"
                }, false, false, 1);

                // SEQUEST
            } else if (strFileNameLower.EndsWith("_fht.txt")) {
                // Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
                // Example contents:
                //  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
                //  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
                blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, true, true);

                // SEQUEST
            } else if (strFileNameLower.EndsWith("_syn.txt")) {
                // Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
                // Example contents:
                //  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
                //  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
                blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, true, true);

                // SEQUEST
            } else if (strFileNameLower.EndsWith("_fht_prot.txt")) {
                // Header line should always be present
                // Example contents:
                //  RankXc	ScanNum	ChargeState	MultiProteinID	Reference	
                //  1	9	1	1	CN32_0001
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "RankXc", true);

                // SEQUEST
            } else if (strFileNameLower.EndsWith("_irr.txt")) {
                // Header line should always be present
                // Example contents:
                //  Scannum	CS	RankXc	ObservedIons	PossibleIons	
                //  9	1	1	4	6	
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "Scannum", true);

                // SEQUEST
            } else if (strFileNameLower.EndsWith("_nli.txt")) {
                // Note: Header line could be missing
                // Example contents:
                //  Scannum	NL1_Intensity	NL2_Intensity	NL3_Intensity	
                //  9	0	0	0	
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, true);

                // X!Tandem
            } else if (strFileNameLower.EndsWith("_xt.txt")) {
                // Header line should always be present
                // Example contents:
                //  Result_ID	Group_ID	Scan	Charge	Peptide_MH	Peptide_Hyperscore	Peptide_Expectation_Value_Log(e)	Multiple_Protein_Count	Peptide_Sequence	DeltaCn2	y_score	y_ions	b_score	b_ions	Delta_Mass	Peptide_Intensity_Log(I)
                //  1	3125	3541	2	1990.0049	74.4	-10.174	0	R.TDMESALPVTVLSAEDIAK.T	0.6949	12.9	11	11.7	11	-0.0054	6.22
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 10, true);

            } else if (strFileNameLower == "lcq_dta.txt") {
                // Free form text file
                // Example contents:
                //  extract_msn ver 4.0, Copyright 1997-2007
                //  Licensed to Thermo Fisher Scientific Inc.
                //  OU_CN32_002_run3_3Apr08_Draco_07-12-25  05/16/08, 07:39 PM
                //
                //  group scan          = 1
                //  min group count     = 1
                //  min ion threshold   = 35
                //  intensity threshold = 0
                //  precursor tolerance = 3.0000 amu
                //  mass range          = 200.0000 - 5000.0000
                //  scan range          = 1 - 15240
                //
                //     #     Scan   MasterScan   Precursor   Charge     (M+H)+  
                //  ------  ------  ----------  -----------  ------  -----------
                blnFileIsValid = CheckTextFileWork(strFilePath, 6, 0, new string[] {
                    "group scan",
                    "mass range",
                    "mass:",
                    "Charge"
                }, false, false, 1);

            } else if (strFileNameLower == "lcq_profile.txt") {
                // Example contents:
                //  Datafile FullScanSumBP FullScanMaxBP ZoomScanSumBP ZoomScanMaxBP SumTIC MaxTIC
                //  OU_CN32_002_run3_3Apr08_Draco_07-12-25.9.9.1.dta 11861 11861 0 0 13482 13482
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 0, "Datafile", false);

            } else if (strFileNameLower == "xtandem_processing_log.txt") {
                // Example contents:
                //  2008-05-16 10:48:19	X! Tandem starting
                //  2008-05-16 10:48:19	loading spectra
                //  2008-05-16 10:48:23	.
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, true, true);

            } else if (strFileNameLower == "mass_correction_tags.txt") {
                // Example contents:
                //  6C13    	6.02013	-
                //  6C132N15	8.0143	-
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, true);

            } else if (strFileNameLower.EndsWith("_moddefs.txt")) {
                // Note: File could be empty
                // Example contents:
                //  *	15.9949	M	D	Plus1Oxy
                //  #	42.0106	<	D	Acetyl
                //  @	57.0215	C	D	IodoAcet
                //  &	-17.026549	Q	D	NH3_Loss
                //  $	-18.0106	E	D	MinusH2O
                blnFileIsValid = CheckTextFileWork(strFilePath, 0, 4, true);

                // PHRP
            } else if (strFileNameLower.EndsWith("_moddetails.txt")) {
                // Example contents:
                //  Unique_Seq_ID	Mass_Correction_Tag	Position
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 2, "Unique_Seq_ID", true);

                // PHRP
            } else if (strFileNameLower.EndsWith("_modsummary.txt")) {
                // Example contents:
                //  Modification_Symbol	Modification_Mass	Target_Residues	Modification_Type	Mass_Correction_Tag	Occurence_Count
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "Modification_Symbol", true);

                // PHRP
            } else if (strFileNameLower.EndsWith("_resulttoseqmap.txt")) {
                // Example contents:
                //  Result_ID	Unique_Seq_ID
                //  1	1
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, "Result_ID", true);

                // PHRP
            } else if (strFileNameLower.EndsWith("_seqinfo.txt")) {
                // Example contents:
                //  Unique_Seq_ID	Mod_Count	Mod_Description	Monoisotopic_Mass
                //  1	0		2617.3685121
                //
                // OR
                //
                // Row_ID	Unique_Seq_ID	Cleavage_State	Terminus_State	Mod_Count	Mod_Description	Monoisotopic_Mass
                // 1	1	2	0	2	IodoAcet:3,IodoAcet:30	4436.0728061

                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, "Unique_Seq_ID", true, false);

                // PHRP
            } else if (strFileNameLower.EndsWith("_seqtoproteinmap.txt")) {
                // Example contents:
                //  Unique_Seq_ID	Cleavage_State	Terminus_State	Protein_Name	Protein_Expectation_Value_Log(e)	Protein_Intensity_Log(I)
                //  1	2	0	P005|G3P_RABIT		
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 5, "Unique_Seq_ID", true);

                // Peptide Prophet
            } else if (strFileNameLower.EndsWith("_pepprophet.txt")) {
                // Example contents:
                //  HitNum	FScore	Probability	negOnly
                //  1	9.5844	1	0
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, "HitNum", true);

            } else if (strFileNameLower == "PeptideProphet_Coefficients.txt") {
                // Example contents:
                //  CS	Xcorr	DeltaCn2	RankSp	DelM	Const
                //  1	5.49	4.643	-0.455	-0.84	0.646
                blnFileIsValid = CheckTextFileWork(strFilePath, 1, 5, "CS", true);

            } else if (strFileNameLower == "sequest.log") {
                // Free form text file
                // Example contents:
                //  TurboSEQUEST - PVM Master v.27 (rev. 12), (c) 1998-2005
                //  Molecular Biotechnology, Univ. of Washington, J.Eng/S.Morgan/J.Yates
                //  Licensed to Thermo Electron Corp.
                // 
                //  NumHosts = 10, NumArch = 1
                // 
                //    Arch:WIN32  CPU:1  Tid:40000  Name:p1
                //    Arch:LINUXI386  CPU:4  Tid:80000  Name:node18
                blnFileIsValid = CheckTextFileWork(strFilePath, 5, 0);

            }

            return blnFileIsValid;

        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, and minimum tab count
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, false, false, new string[], true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, and blnRequireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, bool blnRequireEqualTabsPerLine)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, new string[], true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, blnRequireEqualTabsPerLine, and blnCharCountSkipsBlankLines
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, bool blnRequireEqualTabsPerLine, bool blnCharCountSkipsBlankLines)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, new string[], true, blnCharCountSkipsBlankLines, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, and blnRequireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, string strRequiredTextLineHeader, bool blnRequireEqualTabsPerLine)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, new string[] { strRequiredTextLineHeader }, true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, blnRequireEqualTabsPerLine, and blnRequiredTextMatchesLineStart
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, string strRequiredTextLineHeader, bool blnRequireEqualTabsPerLine, bool blnRequiredTextMatchesLineStart)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, new string[] { strRequiredTextLineHeader }, blnRequiredTextMatchesLineStart, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, and blnRequireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, string[] strRequiredTextLineHeaders, bool blnRequireEqualTabsPerLine)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, strRequiredTextLineHeaders, true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, blnRequireEqualTabsPerLine, and blnRequiredTextMatchesLineStart
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, string[] strRequiredTextLineHeaders, bool blnRequireEqualTabsPerLine, bool blnRequiredTextMatchesLineStart, int intRequiredTextMinMatchCount)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, false, strRequiredTextLineHeaders, blnRequiredTextMatchesLineStart, false, intRequiredTextMinMatchCount);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, and minimum comma count
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, int intMinimumCommaCount)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, intMinimumCommaCount, false, false, new string[], true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, minimum comma count, blnRequireEqualTabsPerLine, and blnRequireEqualCommasPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, int intMinimumCommaCount, bool blnRequireEqualTabsPerLine, bool blnRequireEqualCommasPerLine)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, intMinimumCommaCount, blnRequireEqualTabsPerLine, blnRequireEqualCommasPerLine, new string[], true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, and single required text line header
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, string strRequiredTextLineHeader, bool blnRequiredTextMatchesLineStart)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, 0, 0, false, false, new string[] { strRequiredTextLineHeader }, blnRequiredTextMatchesLineStart, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, and array of required text line headers
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, string[] strRequiredTextLineHeaders, bool blnRequiredTextMatchesLineStart)
        {
            return CheckTextFileWork(strFilePath, intMinimumLineCount, 0, 0, false, false, strRequiredTextLineHeaders, blnRequiredTextMatchesLineStart, false, 0);
        }

        /// <summary>
        /// Checks the integrity of a text file
        /// </summary>
        /// <param name="strFilePath">File path to check</param>
        /// <param name="intMinimumLineCount">Minimum number of lines to examine; maximum number of lines is defined by mMaximumTextFileLinesToCheck</param>
        /// <param name="intMinimumTabCount">Minimum number of tabs to require in each line</param>
        /// <param name="intMinimumCommaCount">Minimum number of commas to require in each line</param>
        /// <param name="blnRequireEqualTabsPerLine">If True, then requires that every line have an equal number of Tab characters</param>
        /// <param name="blnRequireEqualCommasPerLine">If True, then requires that every line have an equal number of commas</param>
        /// <param name="strRequiredTextLineHeaders">Optional list of text that must be found at the start of any of the text lines (within the first mMaximumTextFileLinesToCheck lines); the search text is case-sensitive</param>
        /// <param name="blnRequiredtextMatchesLineStart">When True, then only examine the start of the line for the text in strRequiredTextLineHeaders</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckTextFileWork(string strFilePath, int intMinimumLineCount, int intMinimumTabCount, int intMinimumCommaCount, bool blnRequireEqualTabsPerLine, bool blnRequireEqualCommasPerLine, string[] strRequiredTextLineHeaders, bool blnRequiredTextMatchesLineStart, bool blnCharCountSkipsBlankLines, int intRequiredTextMinMatchCount)
        {

            // Open the text file and read the text line-by-line
            // Check for a minimum number of lines being present, a minimum number of tab characters, and a minimum number of commas
            // Additionally, look for lines that start with the text defined in strRequiredTextLineHeaders()
            // File will fail the check if all of these conditions are not met

            bool blnCheckLineHeadersForThisFile = false;
            bool blnNeedToCheckLineHeaders = false;
            // If blnCheckLineHeadersForThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
            int intLineHeaderMatchCount = 0;
            bool[] blnLineHeaderFound = null;

            bool blnSuccess = false;

            int intLinesRead = 0;
            int intMaximumTextFileLinesToCheck = 0;

            string strLineIn = null;
            bool blnBlankLineRead = false;

            int intExpectedTabCount = 0;
            int intExpectedCommaCount = 0;

            string strErrorMessage = string.Empty;
            bool blnErrorLogged = false;

            if ((strRequiredTextLineHeaders != null) && strRequiredTextLineHeaders.Length > 0) {
                blnCheckLineHeadersForThisFile = true;
                blnNeedToCheckLineHeaders = true;
                blnLineHeaderFound = new bool[strRequiredTextLineHeaders.Length];
            } else {
                blnLineHeaderFound = new bool[1];
            }

            if (mMaximumTextFileLinesToCheck <= 0) {
                intMaximumTextFileLinesToCheck = int.MaxValue;
            } else {
                intMaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck;
            }

            if (intMaximumTextFileLinesToCheck < 1)
                intMaximumTextFileLinesToCheck = 1;
            if (intMaximumTextFileLinesToCheck < intMinimumLineCount) {
                intMaximumTextFileLinesToCheck = intMinimumLineCount;
            }

            try {
                // '' FHT protein files may have an extra tab at the end of the header line; need to account for this
                //'If strFilePath.EndsWith("_fht_prot.txt") Then
                //'    blnFhtProtFile = True
                //'End If

                // Open the file
                using (var srInFile = new StreamReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

                    // Read each line and examine it
                    while (!srInFile.EndOfStream && intLinesRead < intMaximumTextFileLinesToCheck) {
                        strLineIn = srInFile.ReadLine();
                        intLinesRead += 1;

                        if (blnCharCountSkipsBlankLines && strLineIn.Trim.Length == 0) {
                            blnSuccess = true;

                        } else {
                            if (intMinimumTabCount > 0) {
                                // Count the number of tabs
                                blnSuccess = CheckTextFileCountChars(ref strLineIn, ref blnBlankLineRead, intLinesRead, ref intExpectedTabCount, '\t', "Tab", intMinimumTabCount, blnRequireEqualTabsPerLine, ref strErrorMessage);

                                if (!blnSuccess) {
                                    LogFileIntegrityError(strFilePath, strErrorMessage);
                                    blnErrorLogged = true;
                                    break; // TODO: might not be correct. Was : Exit Do
                                }

                            }

                            if (intMinimumCommaCount > 0) {
                                // Count the number of commas
                                blnSuccess = CheckTextFileCountChars(ref strLineIn, ref blnBlankLineRead, intLinesRead, ref intExpectedCommaCount, ',', "Comma", intMinimumCommaCount, blnRequireEqualCommasPerLine, ref strErrorMessage);

                                if (!blnSuccess) {
                                    LogFileIntegrityError(strFilePath, strErrorMessage);
                                    blnErrorLogged = true;
                                    break; // TODO: might not be correct. Was : Exit Do
                                }

                            }


                            if (blnNeedToCheckLineHeaders) {
                                FindRequiredTextInLine(ref strLineIn, ref blnNeedToCheckLineHeaders, ref strRequiredTextLineHeaders, ref blnLineHeaderFound, ref intLineHeaderMatchCount, blnRequiredTextMatchesLineStart);
                            } else if (intMinimumTabCount == 0 && intMinimumCommaCount == 0 && intLinesRead > intMinimumLineCount) {
                                // All conditions have been met; no need to continue reading the file
                                break; // TODO: might not be correct. Was : Exit Do
                            }

                        }

                    }

                }

                // Make sure that all of the required line headers were found; log an error if any were missing
                ValidateRequiredTextFound(strFilePath, "line headers", blnCheckLineHeadersForThisFile, blnNeedToCheckLineHeaders, ref strRequiredTextLineHeaders, ref blnLineHeaderFound, intRequiredTextMinMatchCount, ref blnErrorLogged);

                if (!blnErrorLogged && intLinesRead < intMinimumLineCount) {
                    strErrorMessage = "File contains " + intLinesRead.ToString + " lines of text, but the required minimum is " + intMinimumLineCount.ToString;
                    LogFileIntegrityError(strFilePath, strErrorMessage);
                    blnErrorLogged = true;
                }

            } catch (Exception ex) {
                strErrorMessage = "Error checking file: " + strFilePath + "; " + ex.Message;
                LogFileIntegrityError(strFilePath, strErrorMessage);
                blnErrorLogged = true;
            }

            return !blnErrorLogged;

        }

        /// <summary>
        /// Counts the number of occurrences of a given character in strLineIn
        /// </summary>
        /// <param name="strLineIn">Line to check</param>
        /// <param name="blnBlankLineRead">Set to true if a blank line is read; however, if already true, and a non-blank line with an insufficient number of characters is read, then this function will return an error</param>
        /// <param name="intLinesRead">Number of lines that have been read; when first calling this function for a new file, set this to 1 so that intExpectedCharCount will be initialized </param>
        /// <param name="intExpectedCharCount">The number of occurrences of the given character in the previous line; used when blnRequireEqualCharsPerLine is True</param>
        /// <param name="chCharToCount">The character to look for</param>
        /// <param name="strCharDescription">A description of the character (used to populate strMessage when an error occurs)</param>
        /// <param name="intMinimumCharCount">Minimum character count</param>
        /// <param name="blnRequireEqualCharsPerLine">If True, then each line must contain an equal occurrence count of the given character (based on the first line in the file)</param>
        /// <param name="strErrorMessage">Error message</param>
        /// <returns>True if the line is valid; otherwise False; when False, then updates strErrorMessage</returns>
        /// <remarks></remarks>
        protected bool CheckTextFileCountChars(ref string strLineIn, ref bool blnBlankLineRead, int intLinesRead, ref int intExpectedCharCount, char chCharToCount, string strCharDescription, int intMinimumCharCount, bool blnRequireEqualCharsPerLine, ref string strErrorMessage)
        {

            int intCharCount = 0;

            bool blnLineIsValid = true;

            // Count the number of chCharToCount characters
            intCharCount = CountChars(strLineIn, chCharToCount);

            if (strLineIn.EndsWith(chCharToCount) && intCharCount > 1 && intCharCount > intMinimumCharCount) {
                // Decrement the char count by one since the line ends in the character we're counting
                intCharCount -= 1;
            }

            if (intCharCount < intMinimumCharCount) {
                // Character not found the minimum number of times

                if (strLineIn.Length == 0 && !blnBlankLineRead) {
                    blnBlankLineRead = true;
                } else if (blnBlankLineRead && !(strLineIn.Length == 0)) {
                    // Previously read a blank line; now found a line that's not blank
                    strErrorMessage = "Line " + intLinesRead.ToString + " has " + intCharCount.ToString + " " + strCharDescription + "s, but the required minimum is " + intMinimumCharCount.ToString;
                    blnLineIsValid = false;
                } else {
                    strErrorMessage = "Line " + intLinesRead.ToString + " has " + intCharCount.ToString + " " + strCharDescription + "s, but the required minimum is " + intMinimumCharCount.ToString;
                    blnLineIsValid = false;
                }
            }

            if (blnLineIsValid && blnRequireEqualCharsPerLine) {
                if (intLinesRead <= 1) {
                    intExpectedCharCount = intCharCount;
                } else {
                    if (intCharCount != intExpectedCharCount) {
                        if (strLineIn.Length > 0) {
                            strErrorMessage = "Line " + intLinesRead.ToString + " has " + intCharCount.ToString + " " + strCharDescription + "s, but previous line has " + intExpectedCharCount.ToString + " tabs";
                            blnLineIsValid = false;
                        }
                    }
                }
            }

            return blnLineIsValid;

        }

        /// <summary>
        /// Checks the integrity of files without an extension
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckExtensionFreeFile(string strFilePath)
        {
            bool blnLineIsValid = true;

            switch (Path.GetFileNameWithoutExtension(strFilePath).ToLower) {
                case "acqu":
                case "acqus":
                    blnLineIsValid = CheckTextFileWork(strFilePath, 50, new string[] {
                        "##TITLE",
                        "##DATA"
                    }, true);

                    break;
                case "lock":
                    blnLineIsValid = CheckTextFileWork(strFilePath, 1, new string[] { "ftms" }, true);

                    break;
                case "sptype":
                    // Skip this file
                    blnLineIsValid = true;
                    break;
            }

            return blnLineIsValid;
        }

        /// <summary>
        /// Checks the integrity of a Sequest Params file
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckParamsFile(string strFilePath)
        {

            const string MASS_TOLERANCE_LINE = "peptide_mass_tolerance";
            const string FRAGMENT_TOLERANCE_LINE = "fragment_ion_tolerance";
            const int MAX_LINES_TO_READ = 50;

            int intLinesRead = 0;

            string strLineIn = null;

            bool blnMassToleranceFound = false;
            bool blnFragmentToleranceFound = false;
            bool blnFileIsValid = false;

            try {
                // Open the file
                using (var srInFile = new StreamReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

                    // Read each line in the file and look for the expected parameter lines
                    while (!srInFile.EndOfStream && intLinesRead < MAX_LINES_TO_READ) {
                        strLineIn = srInFile.ReadLine();
                        intLinesRead += 1;

                        if (strLineIn.StartsWith(MASS_TOLERANCE_LINE)) {
                            blnMassToleranceFound = true;
                        }

                        if (strLineIn.StartsWith(FRAGMENT_TOLERANCE_LINE)) {
                            blnFragmentToleranceFound = true;
                        }

                        if (blnMassToleranceFound && blnFragmentToleranceFound) {
                            blnFileIsValid = true;
                            break; // TODO: might not be correct. Was : Exit Do
                        }
                    }

                }



            } catch (Exception ex) {
                LogFileIntegrityError(strFilePath, "Error checking Sequest params file: " + strFilePath + "; " + ex.Message);
                blnFileIsValid = false;
            }

            return blnFileIsValid;

        }

        /// <summary>
        /// Checks the integrity of an ICR-2LS TIC file
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckTICFile(string strFilePath)
        {

            const string ICR2LS_LINE_START = "ICR-2LS";
            const string VERSION_LINE_START = "VERSION";

            int intLinesRead = 0;

            string strLineIn = null;

            // Assume True for now
            bool blnFileIsValid = true;

            try {
                // Open the file
                using (var srInFile = new StreamReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {


                    // Confirm that the first two lines look like:
                    //  ICR-2LS Data File (GA Anderson & JE Bruce); output from MASIC by Matthew E Monroe
                    //  Version 2.4.2974.38283; February 22, 2008

                    while (!srInFile.EndOfStream && intLinesRead < 2) {
                        strLineIn = srInFile.ReadLine();
                        intLinesRead += 1;

                        if (intLinesRead == 1) {
                            if (!strLineIn.ToUpper.StartsWith(ICR2LS_LINE_START)) {
                                blnFileIsValid = false;
                                break; // TODO: might not be correct. Was : Exit Do
                            }
                        } else if (intLinesRead == 2) {
                            if (!strLineIn.ToUpper.StartsWith(VERSION_LINE_START)) {
                                blnFileIsValid = false;
                                break; // TODO: might not be correct. Was : Exit Do
                            }
                        } else {
                            // This code shouldn't be reached
                            break; // TODO: might not be correct. Was : Exit Do
                        }

                    }

                }

            } catch (Exception ex) {
                LogFileIntegrityError(strFilePath, "Error checking TIC file: " + strFilePath + "; " + ex.Message);
                blnFileIsValid = false;
            }

            return blnFileIsValid;

        }

        /// <summary>
        /// Opens the given zip file and uses Ionic Zip's .TestArchive function to validate that it is valid
        /// </summary>
        /// <param name="strFilePath">File path to check</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckZIPFile(string strFilePath)
        {

            const float MAX_THREAD_RATE_CHECK_ALL_DATA = 0.25;
            // minutes/MB
            const float MAX_THREAD_RATE_QUICK_CHECK = 0.125;
            // minutes/MB

            string strFileNameLCase = null;
            FileInfo objFileInfo = default(FileInfo);
            double dblFileSizeMB = 0;

            bool blnZipIsValid = false;
            string strMessage = null;

            System.Threading.Thread objZipLibTest = default(System.Threading.Thread);
            DateTime dtStartTime = default(DateTime);
            float sngMaxExecutionTimeMinutes = 0;

            bool blnZipFileCheckAllData = false;
            blnZipFileCheckAllData = mZipFileCheckAllData;

            // Either run a fast check, or entirely skip this .Zip file if it's too large
            objFileInfo = new FileInfo(strFilePath);
            dblFileSizeMB = objFileInfo.Length / 1024.0 / 1024.0;

            if (blnZipFileCheckAllData && mZipFileLargeSizeThresholdMB > 0) {
                if (dblFileSizeMB > mZipFileLargeSizeThresholdMB) {
                    blnZipFileCheckAllData = false;
                }
            }

            if (blnZipFileCheckAllData && mFastZippedSFileCheck) {
                strFileNameLCase = Path.GetFileName(strFilePath).ToLower();
                if (strFileNameLCase == "0.ser.zip") {
                    // Do not run a full check on 0.ser.zip files
                    blnZipFileCheckAllData = false;

                } else if (strFileNameLCase.Length > 2 && strFileNameLCase.StartsWith("s") && char.IsNumber(strFileNameLCase.Chars(1))) {
                    // Run a full check on s001.zip but not the other s*.zip files
                    if (strFileNameLCase != "s001.zip") {
                        blnZipFileCheckAllData = false;
                    }
                }
            }

            mZipFileWorkParams.FilePath = strFilePath;
            mZipFileWorkParams.CheckAllData = blnZipFileCheckAllData;
            mZipFileWorkParams.ZipIsValid = false;
            if (mZipFileWorkParams.CheckAllData)
            {
                mZipFileWorkParams.FailureMessage = "Zip file failed exhaustive CRC check";
            } else {
                mZipFileWorkParams.FailureMessage = "Zip file failed quick check";
            }

            if (blnZipFileCheckAllData) {
                sngMaxExecutionTimeMinutes = Convert.ToSingle(dblFileSizeMB * MAX_THREAD_RATE_CHECK_ALL_DATA);
            } else {
                sngMaxExecutionTimeMinutes = Convert.ToSingle(dblFileSizeMB * MAX_THREAD_RATE_QUICK_CHECK);
            }

            objZipLibTest = new System.Threading.Thread(CheckZipFileWork);
            dtStartTime = DateTime.UtcNow;

            objZipLibTest.Start();
            do {
                objZipLibTest.Join(250);

                if (objZipLibTest.ThreadState == System.Threading.ThreadState.Aborted) {
                    break; // TODO: might not be correct. Was : Exit Do
                } else if (DateTime.UtcNow.Subtract(dtStartTime).TotalMinutes >= sngMaxExecutionTimeMinutes) {
                    // Execution took too long; abort
                    objZipLibTest.Abort();
                    objZipLibTest.Join(250);

                    strMessage = mZipFileWorkParams.FailureMessage + "; over " + sngMaxExecutionTimeMinutes.ToString("0.0") + " minutes have elapsed, which is longer than the expected processing time";
                    LogFileIntegrityError(mZipFileWorkParams.FilePath, strMessage);

                    break; // TODO: might not be correct. Was : Exit Do
                }
            } while (objZipLibTest.ThreadState != System.Threading.ThreadState.Stopped);

            return mZipFileWorkParams.ZipIsValid;

        }

        private void CheckZipFileWork()
        {
            bool blnThrowExceptionIfInvalid = true;
            bool blnZipIsValid = false;
            string strMessage = null;


            try {
                blnZipIsValid = CheckZipFileIntegrity(mZipFileWorkParams.FilePath, mZipFileWorkParams.CheckAllData, blnThrowExceptionIfInvalid);

                if (!blnZipIsValid) {
                    if (mZipFileWorkParams.CheckAllData) {
                        strMessage = "Zip file failed exhaustive CRC check";
                    } else {
                        strMessage = "Zip file failed quick check";
                    }

                    LogFileIntegrityError(mZipFileWorkParams.FilePath, strMessage);
                }

            } catch (Exception ex) {
                // Error reading .Zip file
                LogFileIntegrityError(mZipFileWorkParams.FilePath, ex.Message);
            }

            mZipFileWorkParams.ZipIsValid = blnZipIsValid;

        }

        /// <summary>
        /// Validate every entry in strZipFilePath
        /// </summary>
        /// <param name="strZipFilePath">Path to the zip file to validate</param>
        /// <param name="blnThrowExceptionIfInvalid">If True, then throws exceptions, otherwise simply returns True or False</param>
        /// <returns>True if the file is Valid; false if an error</returns>
        /// <remarks>Extracts each file in the zip file to a temporary file.  Will return false if you run out of disk space</remarks>
        private bool CheckZipFileIntegrity(string strZipFilePath, bool blnCheckAllData, bool blnThrowExceptionIfInvalid)
        {

            string strTempPath = string.Empty;
            bool blnZipIsValid = false;

            if (!File.Exists(strZipFilePath)) {
                // Zip file not found
                if (blnThrowExceptionIfInvalid) {
                    throw new FileNotFoundException("File not found", strZipFilePath);
                }
                return false;
            }

            try {

                if (blnCheckAllData) {
                    // Obtain a random file name
                    strTempPath = Path.GetTempFileName;

                    // Open the zip file
                    using (Ionic.Zip.ZipFile objZipFile = new Ionic.Zip.ZipFile(strZipFilePath)) {

                        // Extract each file to strTempPath
                        foreach (Ionic.Zip.ZipEntry objEntry in objZipFile.Entries) {
                            objEntry.ZipErrorAction = Ionic.Zip.ZipErrorAction.Throw;


                            if (!objEntry.IsDirectory) {
                                FileStream swTestStream = new FileStream(strTempPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                                objEntry.Extract(swTestStream);
                                swTestStream.Close();

                            }
                        }

                    }

                    blnZipIsValid = true;

                } else {
                    blnZipIsValid = Ionic.Zip.ZipFile.CheckZip(strZipFilePath);
                }


            } catch (Exception ex) {
                if (blnThrowExceptionIfInvalid) {
                    throw ex;
                }
                blnZipIsValid = false;
            } finally {
                try {
                    if (!string.IsNullOrEmpty(strTempPath) && File.Exists(strTempPath)) {
                        File.Delete(strTempPath);
                    }
                } catch (Exception ex) {
                    // Ignore errors deleting the temp file
                }
            }

            return blnZipIsValid;

        }

        /// <summary>
        /// Checks the integrity of a CSV file
        /// </summary>
        /// <param name="strFilePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckCSVFile(string strFilePath)
        {
            int intMinimumCommaCount = 0;
            string strHeaderRequired = string.Empty;

            string strFileNameLower = null;

            strFileNameLower = Path.GetFileName(strFilePath).ToLower;

            if (strFileNameLower.EndsWith("_isos.csv")) {
                // scan_num,charge,abundance,mz,fit,average_mw,monoisotopic_mw,mostabundant_mw,fwhm,signal_noise,mono_abundance,mono_plus2_abundance
                strHeaderRequired = "scan_num";
                intMinimumCommaCount = 10;

            } else if (strFileNameLower.EndsWith("_scans.csv")) {
                // scan_num,scan_time,type,bpi,bpi_mz,tic,num_peaks,num_deisotoped
                strHeaderRequired = "scan_num";
                intMinimumCommaCount = 7;
            } else {
                // Unknown CSV file; do not check it
                intMinimumCommaCount = 0;
            }

            if (intMinimumCommaCount > 0) {
                return CheckTextFileWork(strFilePath, 1, 0, intMinimumCommaCount, false, true, new string[] { strHeaderRequired }, true, false, 0);
            } else {
                return true;
            }

        }

        /// <summary>
        /// Validates the given XML file; tries to determine the expected element names based on the file name and its parent folder
        /// </summary>
        /// <param name="strFilePath">File Path</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckXMLFile(string strFilePath)
        {
            // Examine the parent folder name to determine the type of XML file strFilePath most likely is

            string strFileNameLCase = null;
            string strParentFolderName = null;

            bool blnXMLIsValid = true;

            var fiFile = new FileInfo(strFilePath);

            strFileNameLCase = fiFile.Name.ToLower();
            strParentFolderName = fiFile.Directory.Name;

            switch (strParentFolderName.Substring(0, 3).ToUpper) {
                case "SIC":
                case "DLS":
                    // MASIC or Decon2LS folder
                    if (FileIsXMLSettingsFile(fiFile.FullName)) {
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, new string[] {
                            "section",
                            "item"
                        }, new string[] {
                            "key",
                            "value"
                        });
                    } else if (FileIsDecon2LSXMLSettingsFile(fiFile.FullName)) {
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, new string[] {
                            "parameters",
                            "PeakParameters"
                        }, new string[]);
                    } else {
                        // Unknown XML file; just check for one element
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 1);
                    }

                    break;
                case "SEQ":
                    // Sequest folder
                    if (strFileNameLCase == "finnigandefsettings.xml" || FileIsXMLSettingsFile(fiFile.FullName)) {
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, new string[] {
                            "section",
                            "item"
                        }, new string[] {
                            "key",
                            "value"
                        });
                    } else {
                        // Unknown XML file; just check for one element
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 1);
                    }

                    break;
                case "XTM":
                    // Xtandem folder
                    switch (strFileNameLCase) {
                        case "default_input.xml":
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 10, new string[] {
                                "bioml",
                                "note"
                            }, new string[] {
                                "type",
                                "label"
                            });

                            break;
                        case "input.xml":
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, new string[] {
                                "bioml",
                                "note"
                            }, new string[] {
                                "type",
                                "label"
                            });

                            break;
                        case "taxonomy.xml":
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 3, new string[] {
                                "bioml",
                                "taxon"
                            }, new string[] {
                                "label",
                                "format"
                            });

                            break;
                        default:
                            if (strFileNameLCase == "iontrapdefsettings.xml" || FileIsXMLSettingsFile(fiFile.FullName)) {
                                blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, new string[] {
                                    "section",
                                    "item"
                                }, new string[] {
                                    "key",
                                    "value"
                                });
                            } else if (strFileNameLCase.StartsWith("xtandem_")) {
                                blnXMLIsValid = CheckXMLFileWork(strFilePath, 2, new string[] {
                                    "bioml",
                                    "note"
                                }, new string[]);
                            } else {
                                // Unknown XML file; just check for one element
                                blnXMLIsValid = CheckXMLFileWork(strFilePath, 1);
                            }
                            break;
                    }

                    break;
                default:
                    // Unknown XML file; just check for one element
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 1);
                    break;
            }

            return blnXMLIsValid;

        }

        /// <summary>
        /// Overloaded version of CheckXMLFileWork; takes filename and minimum element count
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        protected bool CheckXMLFileWork(string strFilePath, int intMinimumElementCount)
        {
            return CheckXMLFileWork(strFilePath, intMinimumElementCount, new string[], new string[]);
        }

        /// <summary>
        /// Validates the contents of the given XML file
        /// </summary>
        /// <param name="strFilePath">File Path</param>
        /// <param name="intMinimumElementCount">Minimum number of XML elements that must be in the file; maximum number of elements is defined by mMaximumXMLElementNodesToCheck</param>
        /// <param name="strRequiredElementNames">Optional list of element names that must be found (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
        /// <param name="strRequiredAttributeNames">Optional list of attribute names that must be found  (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        /// <remarks></remarks>
        protected bool CheckXMLFileWork(string strFilePath, int intMinimumElementCount, string[] strRequiredElementNames, string[] strRequiredAttributeNames)
        {

            bool blnCheckElementNamesThisFile = false;
            bool blnNeedToCheckElementNames = false;
            // If blnCheckElementNamesThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
            int intElementNameMatchCount = 0;
            bool[] blnElementNameFound = null;

            bool blnCheckAttributeNamesThisFile = false;
            bool blnNeedToCheckAttributeNames = false;
            // If blnCheckAttributeNamesThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
            int intAttributeNameMatchCount = 0;
            bool[] blnAttributeNameFound = null;

            int intMaximumXMLElementNodesToCheck = 0;
            int intElementsRead = 0;

            bool blnErrorLogged = false;
            string strErrorMessage = null;

            try {
                if ((strRequiredElementNames != null) && strRequiredElementNames.Length > 0) {
                    blnCheckElementNamesThisFile = true;
                    blnNeedToCheckElementNames = true;
                    blnElementNameFound = new bool[strRequiredElementNames.Length];
                } else {
                    blnElementNameFound = new bool[1];
                }

                if ((strRequiredAttributeNames != null) && strRequiredAttributeNames.Length > 0) {
                    blnCheckAttributeNamesThisFile = true;
                    blnNeedToCheckAttributeNames = true;
                    blnAttributeNameFound = new bool[strRequiredAttributeNames.Length];
                } else {
                    blnAttributeNameFound = new bool[1];
                }

                if (mMaximumXMLElementNodesToCheck <= 0) {
                    intMaximumXMLElementNodesToCheck = int.MaxValue;
                } else {
                    intMaximumXMLElementNodesToCheck = mMaximumXMLElementNodesToCheck;
                }

                if (intMaximumXMLElementNodesToCheck < 1)
                    intMaximumXMLElementNodesToCheck = 1;
                if (intMaximumXMLElementNodesToCheck < intMinimumElementCount) {
                    intMaximumXMLElementNodesToCheck = intMinimumElementCount;
                }

                try {
                    // Initialize the stream reader and the XML Text Reader
                    using (var srInFile = new StreamReader(strFilePath)) {
                        using (var objXMLReader = new System.Xml.XmlTextReader(srInFile))
                        {

                            // Read each of the nodes and examine them

                            while (objXMLReader.Read()) {
                                XMLTextReaderSkipWhitespace(objXMLReader);
                                if (objXMLReader.ReadState != System.Xml.ReadState.Interactive)
                                    break; // TODO: might not be correct. Was : Exit Do


                                if (objXMLReader.NodeType == System.Xml.XmlNodeType.Element)
                                {
                                    // Note: If needed, read the element's value using XMLTextReaderGetInnerText(objXMLReader)

                                    if (blnNeedToCheckElementNames) {
                                        FindRequiredTextInLine(ref objXMLReader.Name, ref blnNeedToCheckElementNames, ref strRequiredElementNames, ref blnElementNameFound, ref intElementNameMatchCount, true);
                                    }

                                    if (blnNeedToCheckAttributeNames && objXMLReader.HasAttributes) {
                                        if (objXMLReader.MoveToFirstAttribute) {
                                            do {
                                                FindRequiredTextInLine(ref objXMLReader.Name, ref blnNeedToCheckAttributeNames, ref strRequiredAttributeNames, ref blnAttributeNameFound, ref intAttributeNameMatchCount, true);
                                                if (!blnNeedToCheckAttributeNames)
                                                    break; // TODO: might not be correct. Was : Exit Do
                                            } while (objXMLReader.MoveToNextAttribute);

                                        }
                                    }

                                    intElementsRead += 1;
                                    if (intMaximumXMLElementNodesToCheck > 0 && intElementsRead >= MaximumXMLElementNodesToCheck) {
                                        break; // TODO: might not be correct. Was : Exit Do
                                    } else if (!blnNeedToCheckElementNames && !blnNeedToCheckAttributeNames && intElementsRead > intMinimumElementCount) {
                                        // All conditions have been met; no need to continue reading the file
                                        break; // TODO: might not be correct. Was : Exit Do
                                    }

                                }

                            }

                        }
                        // objXMLReader
                    }
                    // srInFile

                    // Make sure that all of the required element names were found; log an error if any were missing
                    ValidateRequiredTextFound(strFilePath, "XML elements", blnCheckElementNamesThisFile, blnNeedToCheckElementNames, ref strRequiredElementNames, ref blnElementNameFound, strRequiredElementNames.Length, ref blnErrorLogged);

                    // Make sure that all of the required attribute names were found; log an error if any were missing
                    ValidateRequiredTextFound(strFilePath, "XML attributes", blnCheckAttributeNamesThisFile, blnNeedToCheckAttributeNames, ref strRequiredAttributeNames, ref blnAttributeNameFound, strRequiredAttributeNames.Length, ref blnErrorLogged);

                    if (!blnErrorLogged && intElementsRead < intMinimumElementCount) {
                        strErrorMessage = "File contains " + intElementsRead.ToString + " XML elements, but the required minimum is " + intMinimumElementCount.ToString;
                        LogFileIntegrityError(strFilePath, strErrorMessage);
                        blnErrorLogged = true;
                    }

                } catch (Exception ex) {
                    // Error opening file or stepping through file
                    LogFileIntegrityError(strFilePath, ex.Message);
                    blnErrorLogged = true;
                }

            } catch (Exception ex) {
                LogErrors("CheckXMLFileWork", "Error opening XML file: " + strFilePath, ex);
                blnErrorLogged = true;
            }

            return !blnErrorLogged;

        }


        /// <summary>
        /// Checks the integrity of each file in the given folder (provided the extension is recognized)
        /// Will populate udtFolderStats with stats on the files in this folder
        /// Will populate udtFileDetails with the name of each file parsed, plus details on the files
        /// </summary>
        /// <param name="strFolderPath">Folder to examine</param>
        /// <param name="udtFolderStats">Stats on the folder, including number of files and number of files that failed the integrity check</param>
        /// <param name="udtFileStats">Details on each file checked; use udtFolderStatsType.FileCount to determine the number of entries in udtFileStats </param>
        /// <param name="filesToIgnore">List of files to skip; can be file names or full file paths</param>
        /// <returns>Returns True if all files pass the integrity checks; otherwise, returns False</returns>
        /// <remarks>Note that udtFileStats will never be shrunk in size; only increased as needed</remarks>
        public bool CheckIntegrityOfFilesInFolder(
            string strFolderPath, 
            out udtFolderStatsType udtFolderStats, 
            out List<udtFileStatsType> udtFileStats, 
            List<string> filesToIgnore)
        {
            var datasetFileInfo = new clsDatasetFileInfo();

            bool blnUseIgnoreList = false;

            udtFolderStats = new udtFolderStatsType();
            udtFileStats = new List<udtFileStatsType>();

            try
            {
                var filesToIgnoreSorted = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);

                if ((filesToIgnore != null) && filesToIgnore.Count > 0)
                {
                    foreach (var item in filesToIgnore)
                    {
                        if (!filesToIgnoreSorted.Contains(item))
                            filesToIgnoreSorted.Add(item);
                    }

                    blnUseIgnoreList = true;
                }

                var diFolderInfo = new DirectoryInfo(strFolderPath);

                udtFolderStats = GetNewFolderStats(diFolderInfo.FullName);

                foreach (var fiFile in diFolderInfo.GetFiles()) {

                    try {
                        // Assume True for now
                        var blnPassedIntegrityCheck = true;
                        var blnSkipFile = false;

                        if (blnUseIgnoreList) {
                            if (filesToIgnoreSorted.Contains(fiFile.FullName) || filesToIgnoreSorted.Contains(fiFile.Name))
                            {
                                blnSkipFile = true;
                            }
                        }


                        if (!blnSkipFile)
                        {
                            iMSFileInfoProcessor objMSInfoScanner;

                            switch (fiFile.Extension.ToUpper()) {
                                case FILE_EXTENSION_TXT:
                                case FILE_EXTENSION_LOG:
                                    blnPassedIntegrityCheck = CheckTextFile(fiFile.FullName);

                                    break;

                                case FILE_EXTENSION_PARAMS:
                                    blnPassedIntegrityCheck = CheckParamsFile(fiFile.FullName);

                                    break;
                                case FILE_EXTENSION_DAT:
                                    break;
                                // ToDo: Possibly check these files (Decon2LS DAT files)

                                case FILE_EXTENSION_TIC:
                                    blnPassedIntegrityCheck = CheckTICFile(fiFile.FullName);

                                    break;
                                case FILE_EXTENSION_ZIP:
                                    blnPassedIntegrityCheck = CheckZIPFile(fiFile.FullName);

                                    break;
                                case FILE_EXTENSION_CSV:
                                    blnPassedIntegrityCheck = CheckCSVFile(fiFile.FullName);

                                    break;
                                case FILE_EXTENSION_XML:
                                    blnPassedIntegrityCheck = CheckXMLFile(fiFile.FullName);

                                    break;
                                case FINNIGAN_RAW_FILE_EXTENSION:
                                    // File was not in strFileIgnoreList
                                    // Re-check using clsFinniganRawFileInfoScanner

                                    objMSInfoScanner = new clsFinniganRawFileInfoScanner();
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, false);
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, false);
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, false);

                                    blnPassedIntegrityCheck = objMSInfoScanner.ProcessDataFile(fiFile.FullName, datasetFileInfo);

                                    break;
                                case AGILENT_TOF_OR_QTRAP_FILE_EXTENSION:
                                    // File was not in strFileIgnoreList
                                    // Re-check using clsAgilentTOFOrQTRAPWiffFileInfoScanner

                                    objMSInfoScanner = new clsAgilentTOFOrQStarWiffFileInfoScanner();
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, false);
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, false);
                                    objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, false);

                                    blnPassedIntegrityCheck = objMSInfoScanner.ProcessDataFile(fiFile.FullName, datasetFileInfo);

                                    break;
                                case ".":
                                    // No extension
                                    blnPassedIntegrityCheck = CheckExtensionFreeFile(fiFile.FullName);

                                    break;
                                default:
                                    break;
                                // Do not check this file (but add it to udtFileStats anyway)

                            }
                        }

                        var udtNewFile = new udtFileStatsType
                        {
                            FileName = fiFile.FullName,
                            ModificationDate = fiFile.LastWriteTime,
                            SizeBytes = fiFile.Length,
                            FailIntegrity = !blnPassedIntegrityCheck
                        };
                        
                        if (mComputeFileHashes) {
                            //.FileHash = MD5CalcFile(fiFile.FullName)
                            udtNewFile.FileHash = Sha1CalcFile(fiFile.FullName);
                        }

                        udtFileStats.Add(udtNewFile);

                        udtFolderStats.FileCount += 1;
                        if (!blnPassedIntegrityCheck) {
                            udtFolderStats.FileCountFailIntegrity += 1;
                        }

                    } catch (Exception ex) {
                        LogErrors("CheckIntegrityOfFilesInFolder", "Error checking file " + fiFile.FullName, ex);
                    }

                }

            } catch (Exception ex) {
                LogErrors("CheckIntegrityOfFilesInFolder", "Error in CheckIntegrityOfFilesInFolder", ex)
            }

            if (udtFolderStats.FileCountFailIntegrity == 0) {
                return true;
            }

            return false;
        }

        protected int CountChars(string strText, char chSearchChar)
        {
            int intCharCount = 0;
            int intMatchIndex = -1;

            do {
                intMatchIndex = strText.IndexOf(chSearchChar, intMatchIndex + 1);
                if (intMatchIndex >= 0) {
                    intCharCount += 1;
                } else {
                    break; // TODO: might not be correct. Was : Exit Do
                }
            } while (true);

            return intCharCount;
        }

        /// <summary>
        /// Searches strLineToSearch for each of the items in strRequiredText; if blnMatchStart = True, then only checks the start of the line
        /// </summary>
        /// <param name="strLineToSearch">Text to search</param>
        /// <param name="blnNeedToCheckRequiredText">True until all items in strRequiredText() have been found</param>
        /// <param name="strRequiredText">List of items to look for</param>
        /// <param name="blnRequiredTextFound">Set to True when each item is found</param>
        /// <param name="intRequiredTextMatchCount">Total number of items that have been matched; equivalent to the number of True entries in blnRequiredTextFound</param>
        /// <param name="blnMatchStart"></param>
        /// <remarks></remarks>

        protected void FindRequiredTextInLine(
            string strLineToSearch, 
            ref bool blnNeedToCheckRequiredText, 
            string[] strRequiredText, 
            bool[] blnRequiredTextFound, 
            ref int intRequiredTextMatchCount, 
            bool blnMatchStart)
        {
            if (strRequiredText.Length <= 0)
            {
                return;
            }

            for (var intIndex = 0; intIndex <= blnRequiredTextFound.Length - 1; intIndex++)
            {
                if (blnRequiredTextFound[intIndex])
                {
                    continue;
                }

                if (blnMatchStart) {
                    if (strLineToSearch.StartsWith(strRequiredText[intIndex])) {
                        blnRequiredTextFound[intIndex] = true;
                        intRequiredTextMatchCount += 1;
                        break;
                    }
                } else {
                    if (strLineToSearch.Contains(strRequiredText[intIndex])) {
                        blnRequiredTextFound[intIndex] = true;
                        intRequiredTextMatchCount += 1;
                        break;
                    }
                }
            }

            if (intRequiredTextMatchCount >= blnRequiredTextFound.Length) {
                // All required headers have been matched
                // Do not need to check for line headers any more
                blnNeedToCheckRequiredText = false;
            }
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements parameters and PeakParameters
        /// </summary>
        /// <returns>True if this file contains the XML elements that indicate this is an Decon2LS XML settings file</returns>
        /// <remarks></remarks>
        protected bool FileIsDecon2LSXMLSettingsFile(string strFilePath)
        {
            return XMLFileContainsElements(strFilePath, new string[] {
                "<parameters>",
                "<peakparameters>"
            });
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements "sections" and "section"
        /// </summary>
        /// <param name="strFilePath">File to examine</param>
        /// <returns>True if this file contains the XML elements that indicate this is an XML settings file</returns>
        /// <remarks></remarks>
        protected bool FileIsXMLSettingsFile(string strFilePath)
        {
            return XMLFileContainsElements(strFilePath, new string[] {
                "<sections>",
                "<section",
                "<item"
            });
        }

        /// <summary>
        /// Overloaded form of XMLFileContainsElements; assumes intMaximumTextFileLinesToCheck = 50
        /// </summary>
        /// <returns>True if this file contains the required element text</returns>
        /// <remarks></remarks>
        protected bool XMLFileContainsElements(string strFilePath, string[] strElementsToMatch)
        {
            return XMLFileContainsElements(strFilePath, strElementsToMatch, 50);
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements specified in strElementsToMatch()
        /// </summary>
        /// <param name="strFilePath">File to examine</param>
        /// <param name="strElementsToMatch">Element text to match; item text must include the desired element Less Than Signs to match; items must be all lower-case</param>
        /// <returns>True if this file contains the required element text</returns>
        /// <remarks></remarks>
        protected bool XMLFileContainsElements(string strFilePath, string[] strElementsToMatch, int intMaximumTextFileLinesToCheck)
        {

            int intLinesRead = 0;
            int intIndex = 0;

            string strLineIn = null;

            int intElementMatchCount = 0;
            bool[] blnElementFound = null;

            bool blnAllElementsFound = false;

            try {
                if (strElementsToMatch == null || strElementsToMatch.Length == 0) {
                    return false;
                }

                intElementMatchCount = 0;
                blnElementFound = new bool[strElementsToMatch.Length];

                // Read, at most, the first intMaximumTextFileLinesToCheck lines to determine if this is an XML settings file
                if (intMaximumTextFileLinesToCheck < 10) {
                    intMaximumTextFileLinesToCheck = 50;
                }

                // Open the file
                using (var srInFile = new StreamReader(new FileStream(strFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))) {

                    // Read each line and examine it
                    while (!srInFile.EndOfStream && intLinesRead < intMaximumTextFileLinesToCheck) {
                        strLineIn = srInFile.ReadLine();
                        intLinesRead += 1;

                        if ((strLineIn != null)) {
                            strLineIn = strLineIn.Trim().ToLower();

                            for (intIndex = 0; intIndex <= blnElementFound.Length - 1; intIndex++) {
                                if (!blnElementFound[intIndex]) {
                                    if (strLineIn.Trim().StartsWith(strElementsToMatch[intIndex])) {
                                        blnElementFound[intIndex] = true;
                                        intElementMatchCount += 1;

                                        if (intElementMatchCount == blnElementFound.Length) {
                                            blnAllElementsFound = true;
                                            break; // TODO: might not be correct. Was : Exit Do
                                        }

                                        break; // TODO: might not be correct. Was : Exit For
                                    }
                                }
                            }
                        }
                    }

                }

            } catch (Exception ex) {
                LogErrors("XMLFileContainsElements", "Error checking XML file for desired elements: " + strFilePath, ex);
            }

            return blnAllElementsFound;

        }

        public static udtFolderStatsType GetNewFolderStats(string strFolderPath)
        {
            var udtFolderStats = new udtFolderStatsType
            {
                FolderPath = strFolderPath,
                FileCount = 0,
                FileCountFailIntegrity = 0
            };


            return udtFolderStats;

        }

        private void InitializeLocalVariables()
        {
            mMaximumTextFileLinesToCheck = DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;
            mMaximumXMLElementNodesToCheck = DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK;

            mZipFileCheckAllData = true;

            mFastZippedSFileCheck = true;
            mZipFileLargeSizeThresholdMB = 500;

            mComputeFileHashes = false;

            mStatusMessage = string.Empty;

        }


        private void LogErrors(string strSource, string strMessage, Exception ex)
        {
            string strMessageWithoutCRLF = null;

            mStatusMessage = string.Copy(strMessage);

            strMessageWithoutCRLF = mStatusMessage.Replace(Environment.NewLine, "; ");

            if (ex == null) {
                ex = new Exception("Error");
            } else {
                if ((ex.Message != null) && ex.Message.Length > 0) {
                    strMessageWithoutCRLF += "; " + ex.Message;
                }
            }

            if (ErrorCaught != null) {
                ErrorCaught(strSource + ": " + strMessageWithoutCRLF);
            }
        }


        protected void LogFileIntegrityError(string strFilePath, string strErrorMessage)
        {
            if (FileIntegrityFailure != null) {
                FileIntegrityFailure(strFilePath, strErrorMessage);
            }
        }

        public string MD5CalcFile(string strPath)
        {
            // Calculates the MD5 hash of a given file
            // Code from Tim Hastings, at http://www.nonhostile.com/page000017.asp

            var objMD5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] arrHash = null;

            // open file (as read-only)
            var objReader = new FileStream(strPath, FileMode.Open, FileAccess.Read);

            // hash contents of this stream
            arrHash = objMD5.ComputeHash(objReader);

            // Cleanup the objects
            objReader.Close();

            // Return the hash, formatted as a string
            return ByteArrayToString(arrHash);

        }

        public string Sha1CalcFile(string strPath)
        {
            // Calculates the Sha-1 hash of a given file

            var objSha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            byte[] arrHash = null;

            // open file (as read-only)
            var objReader = new FileStream(strPath, FileMode.Open, FileAccess.Read);

            // hash contents of this stream
            arrHash = objSha1.ComputeHash(objReader);

            // Cleanup the objects
            objReader.Close();

            // Return the hash, formatted as a string
            return ByteArrayToString(arrHash);

        }

        /// <summary>
        /// If blnCheckRequiredTextThisFile = True, then makes logs an error if any of the items in blnRequiredTextFound() were not found
        /// </summary>
        /// <param name="strFilePath">File path</param>
        /// <param name="strItemDescription">Description of the types of items that were searched</param>
        /// <param name="blnCheckRequiredTextThisFile">True if checking was enabled in this file</param>
        /// <param name="blnNeedToCheckRequiredText">True if we were still checking for items when this code was reached; if True, then indicates that not all of the items were found</param>
        /// <param name="blnRequiredTextFound">True if the given item was found</param>
        /// <param name="blnErrorLogged">Set to True if any items were missing</param>
        /// <remarks></remarks>

        protected void ValidateRequiredTextFound(string strFilePath, string strItemDescription, bool blnCheckRequiredTextThisFile, bool blnNeedToCheckRequiredText, ref string[] strRequiredText, ref bool[] blnRequiredTextFound, int intRequiredTextMinMatchCount, ref bool blnErrorLogged)
        {
            string strErrorMessage = null;
            int intIndex = 0;
            int intMatchCount = 0;

            if (!blnErrorLogged && blnCheckRequiredTextThisFile) {
                if (blnNeedToCheckRequiredText) {
                    strErrorMessage = "File did not contain all of the expected " + strItemDescription;
                    for (intIndex = 0; intIndex <= blnRequiredTextFound.Length - 1; intIndex++) {
                        if (blnRequiredTextFound[intIndex]) {
                            intMatchCount += 1;
                        } else {
                            strErrorMessage += "; missing '" + strRequiredText[intIndex] + "'";
                        }
                    }

                    if (intRequiredTextMinMatchCount > 0 && intMatchCount >= intRequiredTextMinMatchCount) {
                        // Not all of the items in strRequiredText() were matched, but at least intRequiredTextMinMatchCount were, so all is fine
                    } else {
                        LogFileIntegrityError(strFilePath, strErrorMessage);
                        blnErrorLogged = true;
                    }
                }
            }
        }

        private string XMLTextReaderGetInnerText(System.Xml.XmlTextReader objXMLReader)
        {
            string strValue = string.Empty;
            bool blnSuccess = false;

            if (objXMLReader.NodeType == System.Xml.XmlNodeType.Element)
            {
                // Advance the reader so that we can read the value
                blnSuccess = objXMLReader.Read();
            } else {
                blnSuccess = true;
            }

            if (blnSuccess && objXMLReader.NodeType != System.Xml.XmlNodeType.Whitespace & objXMLReader.HasValue)
            {
                strValue = objXMLReader.Value;
            }

            return strValue;
        }

        private void XMLTextReaderSkipWhitespace(System.Xml.XmlTextReader objXMLReader)
        {
            if (objXMLReader.NodeType == System.Xml.XmlNodeType.Whitespace)
            {
                // Whitspace; read the next node
                objXMLReader.Read();
            }
        }

    }
}
