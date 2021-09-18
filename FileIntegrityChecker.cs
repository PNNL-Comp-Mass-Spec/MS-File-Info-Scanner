using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using MSFileInfoScanner.DatasetStats;
using MSFileInfoScanner.Readers;
using MSFileInfoScannerInterfaces;
using PRISM;

namespace MSFileInfoScanner
{
    /// <summary>
    /// Check the integrity of files in a given directory
    /// </summary>
    /// <remarks>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2008</remarks>
    public class FileIntegrityChecker : EventNotifier
    {
        // ReSharper disable CommentTypo

        // Ignore Spelling: Acetyl, acqu, acqus, amu, bioml, Cn, Const, deisotoped, dta, extract_msn
        // Ignore Spelling: fht, Filt, frag, ftms, fwhm, Hyperscore, IodoAcet, lcq, mostabundant_mw, mw
        // Ignore Spelling: Orbitrap, Oxy, PeakScanStart, Prot, Proteomics, Scannum, SEQUEST, sptype, StatMomentsDataCountUsed
        // Ignore Spelling: taxon, Tid, ver, Wiff, Xc, Xcorr,

        // ReSharper restore CommentTypo

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options"></param>
        public FileIntegrityChecker(InfoScannerOptions options)
        {
            InitializeLocalVariables();
            MaximumTextFileLinesToCheck = options.MaximumTextFileLinesToCheck;
            MaximumXMLElementNodesToCheck = options.MaximumXMLElementNodesToCheck;
        }

        #region "Constants and Enums"

        public const string FILE_EXTENSION_TXT = ".TXT";
        public const string FILE_EXTENSION_LOG = ".LOG";
        public const string FILE_EXTENSION_PARAMS = ".PARAMS";
        public const string FILE_EXTENSION_DAT = ".DAT";
        public const string FILE_EXTENSION_TIC = ".TIC";
        public const string FILE_EXTENSION_ZIP = ".ZIP";
        public const string FILE_EXTENSION_CSV = ".CSV";

        public const string FILE_EXTENSION_XML = ".XML";

        public const string THERMO_RAW_FILE_EXTENSION = ".RAW";

        public const string AGILENT_TOF_OR_QTRAP_FILE_EXTENSION = ".WIFF";

        #endregion

        #region "Structures"

        public struct DirectoryStatsType
        {
            public string DirectoryPath;
            public int FileCount;
            public int FileCountFailIntegrity;
        }

        public struct FileStatsType
        {
            public string FileName;
            public long SizeBytes;
            public DateTime ModificationDate;
            public bool FailIntegrity;

            public string FileHash;

            public void Clear()
            {
                FileName = string.Empty;
                SizeBytes = 0;
                ModificationDate = DateTime.MinValue;
                FailIntegrity = false;
                FileHash = string.Empty;
            }
        }

        private struct ZipFileWorkParamsType
        {
            public string FilePath;
            public bool CheckAllData;
            public bool ZipIsValid;
            public string FailureMessage;
        }

        #endregion

        #region "Class wide variables"

        private int mMaximumTextFileLinesToCheck;

        private int mMaximumXMLElementNodesToCheck;
        private float mZipFileLargeSizeThresholdMB;

        private bool mFastZippedSFileCheck;
        private ZipFileWorkParamsType mZipFileWorkParams;

        public event FileIntegrityFailureEventHandler FileIntegrityFailure;
        public delegate void FileIntegrityFailureEventHandler(string filePath, string message);

        #endregion

        #region "Processing Options and Interface Functions"

        /// <summary>
        /// When True, then computes an MD5 hash on every file
        /// </summary>
        public bool ComputeFileHashes { get; set; }

        public int MaximumTextFileLinesToCheck
        {
            get => mMaximumTextFileLinesToCheck;
            set
            {
                if (value < 0)
                    mMaximumTextFileLinesToCheck = 0;
                else
                    mMaximumTextFileLinesToCheck = value;
            }
        }

        public int MaximumXMLElementNodesToCheck
        {
            get => mMaximumXMLElementNodesToCheck;
            set
            {
                if (value < 0)
                    mMaximumXMLElementNodesToCheck = 0;
                else
                    mMaximumXMLElementNodesToCheck = value;
            }
        }

        public string StatusMessage { get; private set; }

        /// <summary>
        /// When True, then performs an exhaustive CRC check of each Zip file; otherwise, performs a quick test
        /// </summary>
        public bool ZipFileCheckAllData { get; set; }

        #endregion

        private string ByteArrayToString(byte[] arrInput)
        {
            // Converts a byte array into a hex string

            var hexBuilder = new System.Text.StringBuilder(arrInput.Length);

            for (var i = 0; i < arrInput.Length; i++)
            {
                hexBuilder.Append(arrInput[i].ToString("X2"));
            }

            return hexBuilder.ToString().ToLower();
        }

        /// <summary>
        /// Checks the integrity of a text file
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName == null)
            {
                return false;
            }

            var fileNameLower = fileName.ToLower();

            // Analysis Manager Summary File
            if (fileNameLower == "AnalysisSummary.txt".ToLower())
            {
                // Free form text file
                // Example contents:
                //  Job Number	306839
                //  Date	5/16/2008 7:49:00 PM
                //  Processor	SeqCluster2
                //  Tool	Sequest
                return CheckTextFileWork(filePath, 10, 0, new List<string>
                {
                    "Job",
                    "Date",
                    "FileVersion:",
                    "ProductVersion:"
                }, false, true, 2);

                // DEX Manager Summary File
            }

            if (fileNameLower == "DataExtractionSummary.txt".ToLower())
            {
                // Free form text file
                // Example contents:
                //  Job Number: 306839
                //  Date: 5/16/2008 7:53:50 PM
                //  Processor: Mash-01
                return CheckTextFileWork(filePath, 5, 0, new List<string>
                {
                    "Job",
                    "Date",
                    "FileVersion:",
                    "ProductVersion:"
                }, false, true, 2);

                // Analysis Manager MetaData file
            }

            if (fileNameLower == "metadata.txt")
            {
                // Free form text file
                // Example contents (I'm not sure if this file always looks like this):
                //  Proteomics
                //  Mass spectrometer
                //  OU_CN32_002_run3_3Apr08_Draco_07-12-25
                //  Apr  4 2008 10:01AM
                //  LTQ_Orb_1
                return CheckTextFileWork(filePath, 2, 0, "Proteomics", false, false);

                // MASIC
            }

            if (fileNameLower.EndsWith("_ScanStats.txt".ToLower()))
            {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Dataset	ScanNumber	ScanTime	ScanType	TotalIonIntensity	BasePeakIntensity	BasePeakMZ	BasePeakSignalToNoiseRatio	IonCount	IonCountRaw
                //  113591	1	0.00968	1	145331	12762	531.0419	70.11	3147	3147
                return CheckTextFileWork(filePath, 1, 6, true);

                // MASIC
            }

            if (fileNameLower.EndsWith("_ScanStatsConstant.txt".ToLower()))
            {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Setting	Value
                //  AGC	On
                return CheckTextFileWork(filePath, 1, 1, true);

                // MASIC
            }

            if (fileNameLower.EndsWith("_ScanStatsEx.txt".ToLower()))
            {
                // Note: Header line could be missing, but the file should always contain data
                // Example contents:
                //  Dataset	ScanNumber	Ion Injection Time (ms)	Scan Segment	Scan Event	Master Index	Elapsed Scan Time (sec)	Charge State	Monoisotopic M/Z	MS2 Isolation Width	FT Analyzer Settings	FT Analyzer Message	FT Resolution	Conversion Parameter B	Conversion Parameter C	Conversion Parameter D	Conversion Parameter E	Collision Mode	Scan Filter Text
                //  113591	1	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0
                return CheckTextFileWork(filePath, 1, 1, true);

                // MASIC
            }

            if (fileNameLower.EndsWith("_MSMethod.txt".ToLower()))
            {
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
                return CheckTextFileWork(filePath, 10, 0, new List<string>
                {
                    "Instrument",
                    "Creator"
                }, false);

                // MASIC
            }

            if (fileNameLower.EndsWith("_SICStats.txt".ToLower()))
            {
                // Note: Header line could be missing, but the file will usually (but not always) contain data
                // Example contents:
                //  Dataset	ParentIonIndex	MZ	SurveyScanNumber	FragScanNumber	OptimalPeakApexScanNumber	PeakApexOverrideParentIonIndex	CustomSICPeak	PeakScanStart	PeakScanEnd	PeakScanMaxIntensity	PeakMaxIntensity	PeakSignalToNoiseRatio	FWHMInScans	PeakArea	ParentIonIntensity	PeakBaselineNoiseLevel	PeakBaselineNoiseStDev	PeakBaselinePointsUsed	StatMomentsArea	CenterOfMassScan	PeakStDev	PeakSkew	PeakKSStat	StatMomentsDataCountUsed
                //  113591	0	445.12	8	9	133	79	0	3	86	66	14881	0.4267	78	906920	11468	34870	22736	768	293248	68	6.36	-0.16	0.4162	5
                return CheckTextFileWork(filePath, 0, 10, true);

                // SEQUEST
            }

            if (fileNameLower.StartsWith("cat_log"))
            {
                // Example contents:
                //  5/16/2008 7:41:55 PM, 14418 'dta' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_dta.txt', Normal,
                //  5/16/2008 7:48:47 PM, 14418 'out' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_out.txt', Normal,
                return CheckTextFileWork(filePath, 1, 0, new List<string>
                {
                    "were concatenated",
                    "_dta.txt",
                    "_out.txt"
                }, false, false, 1);

                // SEQUEST
            }

            if (fileNameLower.EndsWith("_fht.txt"))
            {
                // ReSharper disable CommentTypo
                // Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
                // Example contents:
                //  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
                //  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
                return CheckTextFileWork(filePath, 0, 10, true, true);

                // SEQUEST
            }

            if (fileNameLower.EndsWith("_syn.txt"))
            {
                // Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
                // Example contents:
                //  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
                //  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
                return CheckTextFileWork(filePath, 0, 10, true, true);

                // SEQUEST
            }

            if (fileNameLower.EndsWith("_fht_prot.txt"))
            {
                // Header line should always be present
                // Example contents:
                //  RankXc	ScanNum	ChargeState	MultiProteinID	Reference
                //  1	9	1	1	CN32_0001
                return CheckTextFileWork(filePath, 1, 4, "RankXc", true);

                // SEQUEST
            }

            if (fileNameLower.EndsWith("_irr.txt"))
            {
                // Header line should always be present
                // Example contents:
                //  Scannum	CS	RankXc	ObservedIons	PossibleIons
                //  9	1	1	4	6

                // ReSharper disable once StringLiteralTypo
                return CheckTextFileWork(filePath, 1, 4, "Scannum", true);

                // SEQUEST
            }

            if (fileNameLower.EndsWith("_nli.txt"))
            {
                // Note: Header line could be missing
                // Example contents:
                //  Scannum	NL1_Intensity	NL2_Intensity	NL3_Intensity
                //  9	0	0	0
                return CheckTextFileWork(filePath, 1, 3, true);

                // X!Tandem
            }

            if (fileNameLower.EndsWith("_xt.txt"))
            {
                // Header line should always be present
                // Example contents:
                //  Result_ID	Group_ID	Scan	Charge	Peptide_MH	Peptide_Hyperscore	Peptide_Expectation_Value_Log(e)	Multiple_Protein_Count	Peptide_Sequence	DeltaCn2	y_score	y_ions	b_score	b_ions	Delta_Mass	Peptide_Intensity_Log(I)
                //  1	3125	3541	2	1990.0049	74.4	-10.174	0	R.TDMESALPVTVLSAEDIAK.T	0.6949	12.9	11	11.7	11	-0.0054	6.22
                return CheckTextFileWork(filePath, 1, 10, true);

                // ReSharper restore CommentTypo

            }

            if (fileNameLower == "lcq_dta.txt")
            {
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
                return CheckTextFileWork(filePath, 6, 0, new List<string>
                {
                    "group scan",
                    "mass range",
                    "mass:",
                    "Charge"
                }, false, false, 1);
            }

            if (fileNameLower == "lcq_profile.txt")
            {
                // Example contents:
                //  Datafile FullScanSumBP FullScanMaxBP ZoomScanSumBP ZoomScanMaxBP SumTIC MaxTIC
                //  OU_CN32_002_run3_3Apr08_Draco_07-12-25.9.9.1.dta 11861 11861 0 0 13482 13482
                return CheckTextFileWork(filePath, 1, 0, "Datafile", false);
            }

            if (fileName.Equals("XTandem_Processing_Log.txt", StringComparison.OrdinalIgnoreCase))
            {
                // Example contents:
                //  2008-05-16 10:48:19	X! Tandem starting
                //  2008-05-16 10:48:19	loading spectra
                //  2008-05-16 10:48:23	.
                return CheckTextFileWork(filePath, 1, 1, true, true);
            }

            if (fileNameLower == "mass_correction_tags.txt")
            {
                // Example contents:
                //  6C13    	6.02013	-
                //  6C132N15	8.0143	-
                return CheckTextFileWork(filePath, 1, 1, true);
            }

            if (fileNameLower.EndsWith("_ModDefs.txt".ToLower()))
            {
                // Note: File could be empty
                // Example contents:
                //  *	15.9949	M	D	Plus1Oxy
                //  #	42.0106	<	D	Acetyl
                //  @	57.0215	C	D	IodoAcet
                //  &	-17.026549	Q	D	NH3_Loss
                //  $	-18.0106	E	D	MinusH2O
                return CheckTextFileWork(filePath, 0, 4, true);

                // PHRP
            }

            if (fileNameLower.EndsWith("_ModDetails.txt".ToLower()))
            {
                // Example contents:
                //  Unique_Seq_ID	Mass_Correction_Tag	Position
                return CheckTextFileWork(filePath, 1, 2, "Unique_Seq_ID", true);

                // PHRP
            }

            if (fileNameLower.EndsWith("_ModSummary.txt".ToLower()))
            {
                // Example contents:
                //  Modification_Symbol	Modification_Mass	Target_Residues	Modification_Type	Mass_Correction_Tag	Occurrence_Count
                return CheckTextFileWork(filePath, 1, 4, "Modification_Symbol", true);

                // PHRP
            }

            if (fileNameLower.EndsWith("_ResultToSeqMap.txt".ToLower()))
            {
                // Example contents:
                //  Result_ID	Unique_Seq_ID
                //  1	1
                return CheckTextFileWork(filePath, 1, 1, "Result_ID", true);

                // PHRP
            }

            if (fileNameLower.EndsWith("_SeqInfo.txt".ToLower()))
            {
                // Example contents:
                //  Unique_Seq_ID	Mod_Count	Mod_Description	Monoisotopic_Mass
                //  1	0		2617.3685121
                //
                // OR
                //
                // Row_ID	Unique_Seq_ID	Cleavage_State	Terminus_State	Mod_Count	Mod_Description	Monoisotopic_Mass
                // 1	1	2	0	2	IodoAcet:3,IodoAcet:30	4436.0728061

                return CheckTextFileWork(filePath, 1, 3, "Unique_Seq_ID", true, false);

                // PHRP
            }

            if (fileNameLower.EndsWith("_SeqToProteinMap.txt".ToLower()))
            {
                // Example contents:
                //  Unique_Seq_ID	Cleavage_State	Terminus_State	Protein_Name	Protein_Expectation_Value_Log(e)	Protein_Intensity_Log(I)
                //  1	2	0	P005|G3P_RABIT
                return CheckTextFileWork(filePath, 1, 5, "Unique_Seq_ID", true);

                // Peptide Prophet
            }

            if (fileNameLower.EndsWith("_PepProphet.txt".ToLower()))
            {
                // Example contents:
                //  HitNum	FScore	Probability	negOnly
                //  1	9.5844	1	0
                return CheckTextFileWork(filePath, 1, 3, "HitNum", true);
            }

            if (fileNameLower == "PeptideProphet_Coefficients.txt")
            {
                // Example contents:
                //  CS	Xcorr	DeltaCn2	RankSp	DelM	Const
                //  1	5.49	4.643	-0.455	-0.84	0.646
                return CheckTextFileWork(filePath, 1, 5, "CS", true);
            }

            if (fileNameLower == "sequest.log")
            {
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
                return CheckTextFileWork(filePath, 5, 0);
            }

            return true;
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, and minimum tab count
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, false, false,
                                     new List<string>(), true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, and requireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       bool requireEqualTabsPerLine)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, new List<string>(), true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, requireEqualTabsPerLine, and charCountSkipsBlankLines
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       bool requireEqualTabsPerLine, bool charCountSkipsBlankLines)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, new List<string>(), true, charCountSkipsBlankLines, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, and requireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       string requiredTextLineHeader, bool requireEqualTabsPerLine)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, new List<string> { requiredTextLineHeader }, true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, requireEqualTabsPerLine, and requiredTextMatchesLineStart
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       string requiredTextLineHeader, bool requireEqualTabsPerLine,
                                       bool requiredTextMatchesLineStart)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, new List<string> { requiredTextLineHeader },
                                     requiredTextMatchesLineStart, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, and requireEqualTabsPerLine
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       IReadOnlyCollection<string> requiredTextLineHeaders, bool requireEqualTabsPerLine)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, requiredTextLineHeaders, true, false, 0);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, requireEqualTabsPerLine, and requiredTextMatchesLineStart
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount, int minimumTabCount,
                                       IReadOnlyCollection<string> requiredTextLineHeaders, bool requireEqualTabsPerLine,
                                       bool requiredTextMatchesLineStart, int requiredTextMinMatchCount)
        {
            return CheckTextFileWork(filePath, minimumLineCount, minimumTabCount, 0, requireEqualTabsPerLine,
                                     false, requiredTextLineHeaders, requiredTextMatchesLineStart, false,
                                     requiredTextMinMatchCount);
        }

        /// <summary>
        /// Overloaded form of CheckTextFileWork; takes filename, minimum line count, and array of required text line headers
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(string filePath, int minimumLineCount,
                                       IReadOnlyCollection<string> requiredTextLineHeaders, bool requiredTextMatchesLineStart)
        {
            return CheckTextFileWork(filePath, minimumLineCount, 0, 0, false, false, requiredTextLineHeaders,
                                     requiredTextMatchesLineStart, false, 0);
        }

        /// <summary>
        /// Checks the integrity of a text file
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <param name="minimumLineCount">Minimum number of lines to examine; maximum number of lines is defined by mMaximumTextFileLinesToCheck</param>
        /// <param name="minimumTabCount">Minimum number of tabs to require in each line</param>
        /// <param name="minimumCommaCount">Minimum number of commas to require in each line</param>
        /// <param name="requireEqualTabsPerLine">If True, then requires that every line have an equal number of Tab characters</param>
        /// <param name="requireEqualCommasPerLine">If True, then requires that every line have an equal number of commas</param>
        /// <param name="requiredTextLineHeaders">Optional list of text that must be found at the start of any of the text lines (within the first mMaximumTextFileLinesToCheck lines); the search text is case-sensitive</param>
        /// <param name="requiredTextMatchesLineStart">When True, then only examine the start of the line for the text in requiredTextLineHeaders</param>
        /// <param name="charCountSkipsBlankLines"></param>
        /// <param name="requiredTextMinMatchCount"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTextFileWork(
            string filePath,
            int minimumLineCount,
            int minimumTabCount,
            int minimumCommaCount,
            bool requireEqualTabsPerLine,
            bool requireEqualCommasPerLine,
            IReadOnlyCollection<string> requiredTextLineHeaders,
            bool requiredTextMatchesLineStart,
            bool charCountSkipsBlankLines,
            int requiredTextMinMatchCount)
        {
            // Open the text file and read the text line-by-line
            // Check for a minimum number of lines being present, a minimum number of tab characters, and a minimum number of commas
            // Additionally, look for lines that start with the text defined in requiredTextLineHeaders()
            // File will fail the check if all of these conditions are not met

            // This counts the number of line headers that have been found
            // Using a variable for speed (vs. checking all of the items in the dictionary over and over)
            var lineHeaderMatchCount = 0;

            // Keys in this dictionary are line headers to find
            // Values are set to true when the line header is found
            var textLineHeaders = ConvertTextListToDictionary(requiredTextLineHeaders);

            // This is set to true if requiredTextLineHeaders has data
            // However, once all of the expected headers are found, it is changed to false
            var needToCheckLineHeaders = textLineHeaders.Count > 0;

            var linesRead = 0;
            int maximumTextFileLinesToCheck;

            var blankLineRead = false;

            var expectedTabCount = 0;
            var expectedCommaCount = 0;

            string errorMessage;
            var errorLogged = false;

            if (mMaximumTextFileLinesToCheck <= 0)
            {
                maximumTextFileLinesToCheck = int.MaxValue;
            }
            else
            {
                maximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck;
            }

            if (maximumTextFileLinesToCheck < 1)
                maximumTextFileLinesToCheck = 1;
            if (maximumTextFileLinesToCheck < minimumLineCount)
            {
                maximumTextFileLinesToCheck = minimumLineCount;
            }

            try
            {
                // '' FHT protein files may have an extra tab at the end of the header line; need to account for this
                //'If filePath.EndsWith("_fht_prot.txt") Then
                //'    fhtProtFile = True
                //'End If

                // Open the file
                using (var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    // Read each line and examine it
                    while (!reader.EndOfStream && linesRead < maximumTextFileLinesToCheck)
                    {
                        var dataLine = reader.ReadLine();
                        linesRead++;

                        bool success;
                        if (charCountSkipsBlankLines && dataLine.Trim().Length == 0)
                        {
                            success = true;
                        }
                        else
                        {
                            if (minimumTabCount > 0)
                            {
                                // Count the number of tabs
                                success = CheckTextFileCountChars(dataLine, ref blankLineRead, linesRead,
                                                                  ref expectedTabCount, '\t', "Tab",
                                                                  minimumTabCount, requireEqualTabsPerLine,
                                                                  out errorMessage);

                                if (!success)
                                {
                                    LogFileIntegrityError(filePath, errorMessage);
                                    errorLogged = true;
                                    break;
                                }
                            }

                            if (minimumCommaCount > 0)
                            {
                                // Count the number of commas
                                success = CheckTextFileCountChars(dataLine, ref blankLineRead, linesRead,
                                                                  ref expectedCommaCount, ',', "Comma",
                                                                  minimumCommaCount, requireEqualCommasPerLine,
                                                                  out errorMessage);

                                if (!success)
                                {
                                    LogFileIntegrityError(filePath, errorMessage);
                                    errorLogged = true;
                                    break;
                                }
                            }

                            if (needToCheckLineHeaders)
                            {
                                FindRequiredTextInLine(dataLine, ref needToCheckLineHeaders, textLineHeaders, ref lineHeaderMatchCount, requiredTextMatchesLineStart);
                            }
                            else if (minimumTabCount == 0 && minimumCommaCount == 0 && linesRead > minimumLineCount)
                            {
                                // All conditions have been met; no need to continue reading the file
                                break;
                            }
                        }
                    }
                }

                if (textLineHeaders.Count > 0 && !errorLogged)
                {
                    // Make sure that all of the required line headers were found; log an error if any were missing
                    ValidateRequiredTextFound(filePath, "line headers", needToCheckLineHeaders, textLineHeaders,
                                              requiredTextMinMatchCount, ref errorLogged);
                }

                if (!errorLogged && linesRead < minimumLineCount)
                {
                    errorMessage = "File contains " + linesRead + " lines of text, but the required minimum is " +
                                      minimumLineCount;
                    LogFileIntegrityError(filePath, errorMessage);
                    errorLogged = true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Error checking file: " + filePath + "; " + ex.Message;
                LogFileIntegrityError(filePath, errorMessage);
                errorLogged = true;
            }

            return !errorLogged;
        }

        /// <summary>
        /// Counts the number of occurrences of a given character in dataLine
        /// </summary>
        /// <param name="dataLine">Line to check</param>
        /// <param name="blankLineRead">Set to true if a blank line is read; however, if already true, and a non-blank line with an insufficient number of characters is read, then this function will return an error</param>
        /// <param name="linesRead">Number of lines that have been read; when first calling this function for a new file, set this to 1 so that expectedCharCount will be initialized </param>
        /// <param name="expectedCharCount">The number of occurrences of the given character in the previous line; used when requireEqualCharsPerLine is True</param>
        /// <param name="charToCount">The character to look for</param>
        /// <param name="charDescription">A description of the character (used to populate message when an error occurs)</param>
        /// <param name="minimumCharCount">Minimum character count</param>
        /// <param name="requireEqualCharsPerLine">If True, then each line must contain an equal occurrence count of the given character (based on the first line in the file)</param>
        /// <param name="errorMessage">Error message</param>
        /// <returns>True if the line is valid; otherwise False; when False, then updates errorMessage</returns>
        private bool CheckTextFileCountChars(string dataLine, ref bool blankLineRead, int linesRead,
                                             ref int expectedCharCount, char charToCount, string charDescription,
                                             int minimumCharCount, bool requireEqualCharsPerLine,
                                             out string errorMessage)
        {
            var lineIsValid = true;
            errorMessage = string.Empty;

            // Count the number of charToCount characters
            var charCount = CountChars(dataLine, charToCount);

            if (dataLine.EndsWith(charToCount.ToString()) && charCount > 1 && charCount > minimumCharCount)
            {
                // Decrement the char count by one since the line ends in the character we're counting
                charCount--;
            }

            if (charCount < minimumCharCount)
            {
                // Character not found the minimum number of times

                if (dataLine.Length == 0 && !blankLineRead)
                {
                    blankLineRead = true;
                }
                else if (blankLineRead && dataLine.Length > 0)
                {
                    // Previously read a blank line; now found a line that's not blank
                    errorMessage = "Line " + linesRead + " has " + charCount + " " + charDescription +
                                      "s, but the required minimum is " + minimumCharCount;
                    lineIsValid = false;
                }
                else
                {
                    errorMessage = "Line " + linesRead + " has " + charCount + " " + charDescription +
                                      "s, but the required minimum is " + minimumCharCount;
                    lineIsValid = false;
                }
            }

            if (!lineIsValid || !requireEqualCharsPerLine)
            {
                return lineIsValid;
            }

            if (linesRead <= 1)
            {
                expectedCharCount = charCount;
            }
            else
            {
                if (charCount != expectedCharCount)
                {
                    if (dataLine.Length > 0)
                    {
                        errorMessage = "Line " + linesRead + " has " + charCount + " " + charDescription +
                                          "s, but previous line has " + expectedCharCount + " tabs";
                        lineIsValid = false;
                    }
                }
            }

            return lineIsValid;
        }

        /// <summary>
        /// Checks the integrity of files without an extension
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckExtensionFreeFile(string filePath)
        {
            var lineIsValid = true;

            switch (Path.GetFileNameWithoutExtension(filePath)?.ToLower())
            {
                case "acqu":
                case "acqus":
                    lineIsValid = CheckTextFileWork(filePath, 50, new List<string>
                    {
                        "##TITLE",
                        "##DATA"
                    }, true);
                    break;

                case "lock":
                    lineIsValid = CheckTextFileWork(filePath, 1, new List<string> { "ftms" }, true);
                    break;

                case "sptype":
                    // Skip this file
                    break;
            }

            return lineIsValid;
        }

        /// <summary>
        /// Checks the integrity of a Sequest Params file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckParamsFile(string filePath)
        {
            const string MASS_TOLERANCE_LINE = "peptide_mass_tolerance";
            const string FRAGMENT_TOLERANCE_LINE = "fragment_ion_tolerance";
            const int MAX_LINES_TO_READ = 50;

            var linesRead = 0;

            var massToleranceFound = false;
            var fragmentToleranceFound = false;
            var fileIsValid = false;

            try
            {
                // Open the file
                using var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                // Read each line in the file and look for the expected parameter lines
                while (!reader.EndOfStream && linesRead < MAX_LINES_TO_READ)
                {
                    var dataLine = reader.ReadLine();
                    linesRead++;

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    if (dataLine.StartsWith(MASS_TOLERANCE_LINE))
                    {
                        massToleranceFound = true;
                    }

                    if (dataLine.StartsWith(FRAGMENT_TOLERANCE_LINE))
                    {
                        fragmentToleranceFound = true;
                    }

                    if (massToleranceFound && fragmentToleranceFound)
                    {
                        fileIsValid = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogFileIntegrityError(filePath,
                                      "Error checking Sequest params file: " + filePath + "; " + ex.Message);
                fileIsValid = false;
            }

            return fileIsValid;
        }

        /// <summary>
        /// Checks the integrity of an ICR-2LS TIC file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckTICFile(string filePath)
        {
            const string ICR2LS_LINE_START = "ICR-2LS";
            const string VERSION_LINE_START = "VERSION";

            var linesRead = 0;

            // Assume True for now
            var fileIsValid = true;

            try
            {
                // Open the file
                using var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                // Confirm that the first two lines look like:
                //  ICR-2LS Data File (GA Anderson & JE Bruce); output from MASIC by Matthew E Monroe
                //  Version 2.4.2974.38283; February 22, 2008

                while (!reader.EndOfStream && linesRead < 2)
                {
                    var dataLine = reader.ReadLine();
                    linesRead++;

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    if (linesRead == 1)
                    {
                        if (!dataLine.ToUpper().StartsWith(ICR2LS_LINE_START))
                        {
                            fileIsValid = false;
                            break;
                        }
                    }
                    else if (linesRead == 2)
                    {
                        if (!dataLine.ToUpper().StartsWith(VERSION_LINE_START))
                        {
                            fileIsValid = false;
                            break;
                        }
                    }
                    else
                    {
                        // This code shouldn't be reached
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogFileIntegrityError(filePath, "Error checking TIC file: " + filePath + "; " + ex.Message);
                fileIsValid = false;
            }

            return fileIsValid;
        }

        /// <summary>
        /// Opens the given zip file and uses Ionic Zip's .TestArchive function to validate that it is valid
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckZIPFile(string filePath)
        {
            // minutes/MB
            const float MAX_THREAD_RATE_CHECK_ALL_DATA = 0.25f;

            // minutes/MB
            const float MAX_THREAD_RATE_QUICK_CHECK = 0.125f;

            float maxExecutionTimeMinutes;

            var zipFileCheckAllData = ZipFileCheckAllData;

            // Either run a fast check, or entirely skip this .Zip file if it's too large
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / 1024.0 / 1024.0;

            if (zipFileCheckAllData && mZipFileLargeSizeThresholdMB > 0)
            {
                if (fileSizeMB > mZipFileLargeSizeThresholdMB)
                {
                    zipFileCheckAllData = false;
                }
            }

            if (zipFileCheckAllData && mFastZippedSFileCheck)
            {
                var fileName = Path.GetFileName(filePath);

                var fileNameLCase = fileName.ToLower();
                if (fileNameLCase == "0.ser.zip")
                {
                    // Do not run a full check on 0.ser.zip files
                    zipFileCheckAllData = false;
                }
                else if (fileNameLCase.Length > 2 && fileNameLCase.StartsWith("s") &&
                         char.IsNumber(fileNameLCase[1]))
                {
                    // Run a full check on s001.zip but not the other s*.zip files
                    if (fileNameLCase != "s001.zip")
                    {
                        zipFileCheckAllData = false;
                    }
                }
            }

            mZipFileWorkParams.FilePath = filePath;
            mZipFileWorkParams.CheckAllData = zipFileCheckAllData;
            mZipFileWorkParams.ZipIsValid = false;
            if (mZipFileWorkParams.CheckAllData)
            {
                mZipFileWorkParams.FailureMessage = "Zip file failed exhaustive CRC check";
            }
            else
            {
                mZipFileWorkParams.FailureMessage = "Zip file failed quick check";
            }

            if (zipFileCheckAllData)
            {
                maxExecutionTimeMinutes = (float)(fileSizeMB * MAX_THREAD_RATE_CHECK_ALL_DATA);
            }
            else
            {
                maxExecutionTimeMinutes = (float)(fileSizeMB * MAX_THREAD_RATE_QUICK_CHECK);
            }

            var zipLibTest = new Thread(CheckZipFileWork);
            var startTime = DateTime.UtcNow;

            zipLibTest.Start();
            do
            {
                zipLibTest.Join(250);

                if (zipLibTest.ThreadState == ThreadState.Aborted)
                {
                    break;
                }

                if (DateTime.UtcNow.Subtract(startTime).TotalMinutes >= maxExecutionTimeMinutes)
                {
                    // Execution took too long; abort
                    zipLibTest.Abort();
                    zipLibTest.Join(250);

                    var message = mZipFileWorkParams.FailureMessage + "; over " +
                                     maxExecutionTimeMinutes.ToString("0.0") +
                                     " minutes have elapsed, which is longer than the expected processing time";
                    LogFileIntegrityError(mZipFileWorkParams.FilePath, message);

                    break;
                }
            } while (zipLibTest.ThreadState != ThreadState.Stopped);

            return mZipFileWorkParams.ZipIsValid;
        }

        private void CheckZipFileWork()
        {
            var zipIsValid = false;

            try
            {
                zipIsValid = CheckZipFileIntegrity(mZipFileWorkParams.FilePath, mZipFileWorkParams.CheckAllData,
                                                      throwExceptionIfInvalid: true);

                if (!zipIsValid)
                {
                    string message;
                    if (mZipFileWorkParams.CheckAllData)
                    {
                        message = "Zip file failed exhaustive CRC check";
                    }
                    else
                    {
                        message = "Zip file failed quick check";
                    }

                    LogFileIntegrityError(mZipFileWorkParams.FilePath, message);
                }
            }
            catch (Exception ex)
            {
                // Error reading .Zip file
                LogFileIntegrityError(mZipFileWorkParams.FilePath, ex.Message);
            }

            mZipFileWorkParams.ZipIsValid = zipIsValid;
        }

        /// <summary>
        /// Validate every entry in zipFilePath
        /// </summary>
        /// <param name="zipFilePath">Path to the zip file to validate</param>
        /// <param name="checkAllData"></param>
        /// <param name="throwExceptionIfInvalid">If True, then throws exceptions, otherwise simply returns True or False</param>
        /// <returns>True if the file is Valid; false if an error</returns>
        /// <remarks>Extracts each file in the zip file to a temporary file.  Will return false if you run out of disk space</remarks>
        private bool CheckZipFileIntegrity(string zipFilePath, bool checkAllData, bool throwExceptionIfInvalid)
        {
            var tempPath = string.Empty;
            bool zipIsValid;

            if (!File.Exists(zipFilePath))
            {
                // Zip file not found
                if (throwExceptionIfInvalid)
                {
                    throw new FileNotFoundException("File not found", zipFilePath);
                }
                return false;
            }

            try
            {
                if (checkAllData)
                {
                    // Obtain a random file name
                    tempPath = Path.GetTempFileName();

                    // Open the zip file
                    using (var zipFile = new Ionic.Zip.ZipFile(zipFilePath))
                    {
                        // Extract each file to tempPath
                        foreach (var fileEntry in zipFile.Entries)
                        {
                            fileEntry.ZipErrorAction = Ionic.Zip.ZipErrorAction.Throw;

                            if (!fileEntry.IsDirectory)
                            {
                                var testStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                                                                FileShare.ReadWrite);
                                fileEntry.Extract(testStream);
                                testStream.Close();
                            }
                        }
                    }

                    zipIsValid = true;
                }
                else
                {
                    zipIsValid = Ionic.Zip.ZipFile.CheckZip(zipFilePath);
                }
            }
            catch (Exception) when (!throwExceptionIfInvalid)
            {
                zipIsValid = false;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors deleting the temp file
                }
            }

            return zipIsValid;
        }

        /// <summary>
        /// Checks the integrity of a CSV file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckCSVFile(string filePath)
        {
            int minimumCommaCount;
            var headerRequired = string.Empty;

            var fileNameLower = Path.GetFileName(filePath)?.ToLower();

            if (string.IsNullOrWhiteSpace(fileNameLower))
            {
                OnWarningEvent("Could not extract the filename from path sent to CheckCSVFile: " + filePath);
                return false;
            }

            if (fileNameLower.EndsWith("_isos.csv"))
            {
                // scan_num,charge,abundance,mz,fit,average_mw,monoisotopic_mw,mostabundant_mw,fwhm,signal_noise,mono_abundance,mono_plus2_abundance
                headerRequired = "scan_num";
                minimumCommaCount = 10;
            }
            else if (fileNameLower.EndsWith("_scans.csv"))
            {
                // scan_num,scan_time,type,bpi,bpi_mz,tic,num_peaks,num_deisotoped
                headerRequired = "scan_num";
                minimumCommaCount = 7;
            }
            else
            {
                // Unknown CSV file; do not check it
                minimumCommaCount = 0;
            }

            if (minimumCommaCount > 0)
            {
                return CheckTextFileWork(filePath, 1, 0, minimumCommaCount, false, true,
                                         new List<string> { headerRequired }, true, false, 0);
            }

            return true;
        }

        /// <summary>
        /// Validates the given XML file; tries to determine the expected element names based on the file name and its parent directory
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckXMLFile(string filePath)
        {
            // Examine the parent directory name to determine the type of XML the file most likely is

            bool xmlIsValid;

            var file = new FileInfo(filePath);

            var fileName = file.Name;
            if (file.Directory == null)
            {
                const string errorMessage = "Unable to determine the parent directory of the XML file";
                LogFileIntegrityError(filePath, errorMessage);
                return false;
            }

            var parentFolderName = file.Directory.Name;

            switch (parentFolderName.Substring(0, 3).ToUpper())
            {
                case "SIC":
                case "DLS":
                    // MASIC or DeconTools directory
                    if (FileIsXMLSettingsFile(file.FullName))
                    {
                        xmlIsValid = CheckXMLFileWork(filePath, 5, new List<string>
                        {
                            "section",
                            "item"
                        }, new List<string>
                        {
                            "key",
                            "value"
                        });
                    }
                    else if (FileIsDeconToolsXMLSettingsFile(file.FullName))
                    {
                        xmlIsValid = CheckXMLFileWork(filePath, 5, new List<string>
                        {
                            "parameters",
                            "PeakParameters"
                        }, new List<string>());
                    }
                    else
                    {
                        // Unknown XML file; just check for one element
                        xmlIsValid = CheckXMLFileWork(filePath, 1);
                    }
                    break;

                case "SEQ":
                    // Sequest directory
                    if (fileName.Equals("FinniganDefSettings.xml", StringComparison.OrdinalIgnoreCase) || FileIsXMLSettingsFile(file.FullName))
                    {
                        xmlIsValid = CheckXMLFileWork(filePath, 5, new List<string>
                        {
                            "section",
                            "item"
                        }, new List<string>
                        {
                            "key",
                            "value"
                        });
                    }
                    else
                    {
                        // Unknown XML file; just check for one element
                        xmlIsValid = CheckXMLFileWork(filePath, 1);
                    }
                    break;

                case "XTM":
                    // XTandem directory
                    switch (fileName.ToLower())
                    {
                        case "default_input.xml":
                            xmlIsValid = CheckXMLFileWork(filePath, 10, new List<string>
                            {
                                "bioml",
                                "note"
                            }, new List<string>
                            {
                                "type",
                                "label"
                            });
                            break;

                        case "input.xml":
                            xmlIsValid = CheckXMLFileWork(filePath, 5, new List<string>
                            {
                                "bioml",
                                "note"
                            }, new List<string>
                            {
                                "type",
                                "label"
                            });
                            break;

                        case "taxonomy.xml":
                            xmlIsValid = CheckXMLFileWork(filePath, 3, new List<string>
                            {
                                "bioml",
                                "taxon"
                            }, new List<string>
                            {
                                "label",
                                "format"
                            });
                            break;

                        default:
                            if (fileName.Equals("IonTrapDefSettings.xml", StringComparison.OrdinalIgnoreCase) || FileIsXMLSettingsFile(file.FullName))
                            {
                                xmlIsValid = CheckXMLFileWork(filePath, 5, new List<string>
                                {
                                    "section",
                                    "item"
                                }, new List<string>
                                {
                                    "key",
                                    "value"
                                });
                            }
                            else if (fileName.StartsWith("XTandem_", StringComparison.OrdinalIgnoreCase))
                            {
                                xmlIsValid = CheckXMLFileWork(filePath, 2, new List<string>
                                {
                                    "bioml",
                                    "note"
                                }, new List<string>());
                            }
                            else
                            {
                                // Unknown XML file; just check for one element
                                xmlIsValid = CheckXMLFileWork(filePath, 1);
                            }
                            break;
                    }
                    break;

                default:
                    // Unknown XML file; just check for one element
                    xmlIsValid = CheckXMLFileWork(filePath, 1);
                    break;
            }

            return xmlIsValid;
        }

        /// <summary>
        /// Overloaded version of CheckXMLFileWork; takes filename and minimum element count
        /// </summary>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckXMLFileWork(string filePath, int minimumElementCount)
        {
            return CheckXMLFileWork(filePath, minimumElementCount, new List<string>(), new List<string>());
        }

        /// <summary>
        /// Validates the contents of the given XML file
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <param name="minimumElementCount">Minimum number of XML elements that must be in the file; maximum number of elements is defined by mMaximumXMLElementNodesToCheck</param>
        /// <param name="requiredElementNames">Optional list of element names that must be found (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
        /// <param name="requiredAttributeNames">Optional list of attribute names that must be found (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
        /// <returns>True if the file passes the integrity check; otherwise False</returns>
        private bool CheckXMLFileWork(
            string filePath,
            int minimumElementCount,
            IReadOnlyCollection<string> requiredElementNames,
            IReadOnlyCollection<string> requiredAttributeNames)
        {
            var elementsRead = 0;

            var errorLogged = false;

            try
            {
                // Keys in this dictionary are element names to find
                // Values are set to true when the element is found
                var requiredElements = ConvertTextListToDictionary(requiredElementNames);

                // This is set to true if requiredElementNames has data
                // However, once all of the elements have been found, it is changed to false
                var needToCheckElementNames = requiredElements.Count > 0;

                // Keys in this dictionary are attribute names to find
                // Values are set to true when the element is found
                var requiredAttributes = ConvertTextListToDictionary(requiredAttributeNames);

                // This is set to true if requiredAttributeNames has data
                // However, once all of the attributes have been found, it is changed to false
                var needToCheckAttributeNames = requiredAttributes.Count > 0;

                var maximumXMLElementNodesToCheck = mMaximumXMLElementNodesToCheck <= 0 ? int.MaxValue : mMaximumXMLElementNodesToCheck;

                if (maximumXMLElementNodesToCheck < 1)
                    maximumXMLElementNodesToCheck = 1;

                if (maximumXMLElementNodesToCheck < minimumElementCount)
                {
                    maximumXMLElementNodesToCheck = minimumElementCount;
                }

                try
                {
                    // Initialize the stream reader and the XML Text Reader
                    using (var reader = new StreamReader(filePath))
                    {
                        // Read each of the nodes and examine them
                        using var xmlReader = new XmlTextReader(reader);

                        while (xmlReader.Read())
                        {
                            XMLTextReaderSkipWhitespace(xmlReader);
                            if (xmlReader.ReadState != ReadState.Interactive)
                                break;

                            if (xmlReader.NodeType != XmlNodeType.Element)
                            {
                                continue;
                            }

                            // Note: If needed, read the element's value using XMLTextReaderGetInnerText(xmlReader)

                            if (needToCheckElementNames)
                            {
                                var totalMatchesInList = FindItemNameInList(xmlReader.Name, requiredElements);
                                if (totalMatchesInList == requiredElements.Count)
                                    needToCheckElementNames = false;
                            }

                            if (needToCheckAttributeNames && xmlReader.HasAttributes)
                            {
                                while (xmlReader.MoveToNextAttribute())
                                {
                                    var totalMatchesInList = FindItemNameInList(xmlReader.Name, requiredAttributes);
                                    if (totalMatchesInList == requiredAttributes.Count)
                                    {
                                        needToCheckAttributeNames = false;
                                        break;
                                    }
                                }
                            }

                            elementsRead++;
                            if (elementsRead >= MaximumXMLElementNodesToCheck)
                            {
                                break;
                            }

                            if (!needToCheckElementNames && !needToCheckAttributeNames && elementsRead > minimumElementCount)
                            {
                                // All conditions have been met; no need to continue reading the file
                                break;
                            }
                        }

                        // xmlReader
                    }
                    // reader

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (requiredElements.Count > 0 && !errorLogged)
                    {
                        // Make sure that all of the required element names were found; log an error if any were missing
                        ValidateRequiredTextFound(filePath, "XML elements", needToCheckElementNames,
                                                  requiredElements, requiredElements.Count, ref errorLogged);
                    }

                    if (requiredAttributes.Count > 0 && !errorLogged)
                    {
                        // Make sure that all of the required attribute names were found; log an error if any were missing
                        ValidateRequiredTextFound(filePath, "XML attributes", needToCheckAttributeNames,
                                                  requiredAttributes, requiredAttributes.Count, ref errorLogged);
                    }

                    if (!errorLogged && elementsRead < minimumElementCount)
                    {
                        var errorMessage = "File contains " + elementsRead + " XML elements, but the required minimum is " + minimumElementCount;
                        LogFileIntegrityError(filePath, errorMessage);
                        errorLogged = true;
                    }
                }
                catch (Exception ex)
                {
                    // Error opening file or stepping through file
                    LogFileIntegrityError(filePath, ex.Message);
                    errorLogged = true;
                }
            }
            catch (Exception ex)
            {
                LogErrors("CheckXMLFileWork", "Error opening XML file: " + filePath, ex);
                errorLogged = true;
            }

            return !errorLogged;
        }

        /// <summary>
        /// Checks the integrity of each file in the given directory (provided the extension is recognized)
        /// Will populate directoryStats with stats on the files in this directory
        /// Will populate fileDetails with the name of each file parsed, plus details on the files
        /// </summary>
        /// <param name="directoryPath">Directory to examine</param>
        /// <param name="directoryStats">Stats on the directory, including number of files and number of files that failed the integrity check</param>
        /// <param name="fileStats">Details on each file checked; use folderStatsType.FileCount to determine the number of entries in fileStats </param>
        /// <param name="filesToIgnore">List of files to skip; can be file names or full file paths</param>
        /// <returns>Returns True if all files pass the integrity checks; otherwise, returns False</returns>
        /// <remarks>Note that fileStats will never be shrunk in size; only increased as needed</remarks>
        public bool CheckIntegrityOfFilesInDirectory(
            string directoryPath,
            out DirectoryStatsType directoryStats,
            out List<FileStatsType> fileStats,
            List<string> filesToIgnore)
        {
            var datasetFileInfo = new DatasetFileInfo();

            var useIgnoreList = false;

            directoryStats = new DirectoryStatsType();
            fileStats = new List<FileStatsType>();

            try
            {
                var filesToIgnoreSorted = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);

                if (filesToIgnore?.Count > 0)
                {
                    foreach (var item in filesToIgnore)
                    {
                        if (!filesToIgnoreSorted.Contains(item))
                            filesToIgnoreSorted.Add(item);
                    }

                    useIgnoreList = true;
                }

                var directoryInfo = new DirectoryInfo(directoryPath);

                directoryStats = GetNewDirectoryStats(directoryInfo.FullName);

                foreach (var dataFile in directoryInfo.GetFiles())
                {
                    try
                    {
                        // Assume True for now
                        var passedIntegrityCheck = true;
                        var skipFile = false;

                        if (useIgnoreList)
                        {
                            if (filesToIgnoreSorted.Contains(dataFile.FullName) ||
                                filesToIgnoreSorted.Contains(dataFile.Name))
                            {
                                skipFile = true;
                            }
                        }

                        if (!skipFile)
                        {
                            MSFileInfoProcessorBaseClass msInfoScanner;

                            switch (dataFile.Extension.ToUpper())
                            {
                                case FILE_EXTENSION_TXT:
                                case FILE_EXTENSION_LOG:
                                    passedIntegrityCheck = CheckTextFile(dataFile.FullName);
                                    break;

                                case FILE_EXTENSION_PARAMS:
                                    passedIntegrityCheck = CheckParamsFile(dataFile.FullName);
                                    break;

                                case FILE_EXTENSION_DAT:
                                    // ToDo: Possibly check these files (Decon2LS DAT files)
                                    break;

                                case FILE_EXTENSION_TIC:
                                    passedIntegrityCheck = CheckTICFile(dataFile.FullName);
                                    break;

                                case FILE_EXTENSION_ZIP:
                                    passedIntegrityCheck = CheckZIPFile(dataFile.FullName);
                                    break;

                                case FILE_EXTENSION_CSV:
                                    passedIntegrityCheck = CheckCSVFile(dataFile.FullName);
                                    break;

                                case FILE_EXTENSION_XML:
                                    passedIntegrityCheck = CheckXMLFile(dataFile.FullName);
                                    break;

                                case THERMO_RAW_FILE_EXTENSION:
                                    // File was not in fileIgnoreList
                                    // Re-check using clsThermoRawFileInfoScanner

                                    msInfoScanner = new ThermoRawFileInfoScanner();
                                    msInfoScanner.Options.SaveTICAndBPIPlots = false;
                                    msInfoScanner.Options.ComputeOverallQualityScores = false;
                                    msInfoScanner.Options.CreateDatasetInfoFile = false;

                                    passedIntegrityCheck = msInfoScanner.ProcessDataFile(dataFile.FullName, datasetFileInfo);
                                    break;

                                case AGILENT_TOF_OR_QTRAP_FILE_EXTENSION:
                                    // File was not in fileIgnoreList
                                    // Re-check using clsAgilentTOFOrQTRAPWiffFileInfoScanner

                                    msInfoScanner = new AgilentTOFOrQStarWiffFileInfoScanner();
                                    msInfoScanner.Options.SaveTICAndBPIPlots = false;
                                    msInfoScanner.Options.ComputeOverallQualityScores = false;
                                    msInfoScanner.Options.CreateDatasetInfoFile = false;

                                    passedIntegrityCheck = msInfoScanner.ProcessDataFile(dataFile.FullName, datasetFileInfo);
                                    break;

                                case ".":
                                    // No extension
                                    passedIntegrityCheck = CheckExtensionFreeFile(dataFile.FullName);
                                    break;

                                default:
                                    // Do not check this file (but add it to fileStats anyway)
                                    break;
                            }
                        }

                        var newFile = new FileStatsType()
                        {
                            FileName = dataFile.FullName,
                            ModificationDate = dataFile.LastWriteTime,
                            SizeBytes = dataFile.Length,
                            FailIntegrity = !passedIntegrityCheck
                        };

                        if (ComputeFileHashes)
                        {
                            newFile.FileHash = Sha1CalcFile(dataFile.FullName);
                        }

                        fileStats.Add(newFile);

                        directoryStats.FileCount++;
                        if (!passedIntegrityCheck)
                        {
                            directoryStats.FileCountFailIntegrity++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogErrors("CheckIntegrityOfFilesInDirectory", "Error checking file " + dataFile.FullName, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogErrors("CheckIntegrityOfFilesInFolder", "Error in CheckIntegrityOfFilesInDirectory", ex);
            }

            return directoryStats.FileCountFailIntegrity == 0;
        }

        private Dictionary<string, bool> ConvertTextListToDictionary(IReadOnlyCollection<string> requiredTextItems)
        {
            var requiredTextDictionary = new Dictionary<string, bool>();

            if (requiredTextItems?.Count > 0)
            {
                foreach (var lineHeader in requiredTextItems)
                {
                    requiredTextDictionary.Add(lineHeader, false);
                }
            }

            return requiredTextDictionary;
        }

        private int CountChars(string text, char searchChar)
        {
            var charCount = 0;
            var matchIndex = -1;

            while (true)
            {
                matchIndex = text.IndexOf(searchChar, matchIndex + 1);
                if (matchIndex >= 0)
                {
                    charCount++;
                }
                else
                {
                    break;
                }
            }

            return charCount;
        }

        /// <summary>
        /// Searches lineToSearch for each of the items in requiredText; if matchStart = True, then only checks the start of the line
        /// </summary>
        /// <param name="textToSearch">Text to search</param>
        /// <param name="needToCheckItems">True if we have not yet found all of the items</param>
        /// <param name="requiredTextItems">List of items to look for; values are set to True as each item is found</param>
        /// <param name="requiredTextMatchCount">Total number of items that have been matched; equivalent to the number of True entries in textLineHeaders</param>
        /// <param name="matchStart"></param>
        private void FindRequiredTextInLine(
            string textToSearch,
            ref bool needToCheckItems,
            IDictionary<string, bool> requiredTextItems,
            ref int requiredTextMatchCount,
            bool matchStart)
        {
            if (!needToCheckItems || requiredTextItems.Count == 0)
            {
                return;
            }

            foreach (var item in requiredTextItems)
            {
                if (item.Value)
                {
                    // The item has already been found
                    continue;
                }

                if (matchStart)
                {
                    if (textToSearch.StartsWith(item.Key))
                    {
                        requiredTextItems[item.Key] = true;
                        requiredTextMatchCount++;
                        break;
                    }
                }
                else
                {
                    if (textToSearch.Contains(item.Key))
                    {
                        requiredTextItems[item.Key] = true;
                        requiredTextMatchCount++;
                        break;
                    }
                }
            }

            if (requiredTextMatchCount >= requiredTextItems.Count)
            {
                // All required text has been found
                // No need to continue checking additional lines
                needToCheckItems = false;
            }
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements parameters and PeakParameters
        /// </summary>
        /// <returns>True if this file contains the XML elements that indicate this is a Decon2LS XML settings file</returns>
        private bool FileIsDeconToolsXMLSettingsFile(string filePath)
        {
            return XMLFileContainsElements(filePath, new[]
            {
                "<parameters>",
                "<PeakParameters>"
            });
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements "sections" and "section"
        /// </summary>
        /// <param name="filePath">File to examine</param>
        /// <returns>True if this file contains the XML elements that indicate this is an XML settings file</returns>
        private bool FileIsXMLSettingsFile(string filePath)
        {
            // Note that section and item only have a less than sign because XMLFileContainsElements uses a simple stream reader and not an XmlTextReader
            return XMLFileContainsElements(filePath, new[]
            {
                "<sections>",
                "<section",
                "<item"
            });
        }

        /// <summary>
        /// Look for currentItemName in requiredItemNames; if found, set the entry to true
        /// </summary>
        /// <param name="currentItemName"></param>
        /// <param name="requiredItemNames"></param>
        /// <returns>
        /// If requiredItemNames contains currentItemName, returns the number of items in requiredItemNames that are true
        /// If not found, returns 0
        /// </returns>
        private int FindItemNameInList(string currentItemName, IDictionary<string, bool> requiredItemNames)
        {
            var matchFound = false;

            foreach (var item in requiredItemNames)
            {
                if (item.Key.Equals(currentItemName))
                {
                    requiredItemNames[item.Key] = true;
                    matchFound = true;
                    break;
                }
            }

            if (!matchFound)
                return 0;

            var nameMatchCount = 0;
            foreach (var requiredItem in requiredItemNames)
            {
                if (requiredItem.Value)
                    nameMatchCount++;
            }

            return nameMatchCount;
        }

        /// <summary>
        /// Opens the file using a text reader and looks for XML elements specified in elementsToMatch()
        /// This method intentionally uses a StreamReader and not an XmlTextReader
        /// </summary>
        /// <param name="filePath">File to examine</param>
        /// <param name="elementsToMatch">Element names to match; item text must start with a less than sign followed by the element name</param>
        /// <param name="maximumTextFileLinesToCheck"></param>
        /// <param name="caseSensitiveElementNames">True if element names should be case sensitive</param>
        /// <returns>True if this file contains the required element text</returns>
        /// <remarks>Requires that the element names to match are at the start of the line in the text file</remarks>
        private bool XMLFileContainsElements(string filePath,
                                             IReadOnlyList<string> elementsToMatch,
                                             int maximumTextFileLinesToCheck = 50,
                                             bool caseSensitiveElementNames = false)
        {
            var linesRead = 0;

            var allElementsFound = false;

            try
            {
                if (elementsToMatch == null || elementsToMatch.Count == 0)
                {
                    return false;
                }

                var elementMatchCount = 0;
                var elementFound = new bool[elementsToMatch.Count];

                // Read, at most, the first maximumTextFileLinesToCheck lines to determine if this is an XML settings file
                if (maximumTextFileLinesToCheck < 10)
                {
                    maximumTextFileLinesToCheck = 50;
                }

                var comparisonType = caseSensitiveElementNames ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                // Open the file
                using var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));

                // Read each line and examine it
                while (!reader.EndOfStream && linesRead < maximumTextFileLinesToCheck)
                {
                    var dataLine = reader.ReadLine();
                    linesRead++;

                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    dataLine = dataLine.Trim().ToLower();

                    for (var index = 0; index < elementFound.Length; index++)
                    {
                        if (elementFound[index])
                        {
                            continue;
                        }

                        if (dataLine.IndexOf(elementsToMatch[index], comparisonType) < 0)
                        {
                            continue;
                        }

                        elementFound[index] = true;
                        elementMatchCount++;

                        if (elementMatchCount == elementFound.Length)
                        {
                            allElementsFound = true;
                        }
                        break;
                    }

                    if (allElementsFound)
                        break;
                }
            }
            catch (Exception ex)
            {
                LogErrors("XMLFileContainsElements", "Error checking XML file for desired elements: " + filePath, ex);
            }

            return allElementsFound;
        }

        public static DirectoryStatsType GetNewDirectoryStats(string directoryPath)
        {
            var directoryStats = new DirectoryStatsType
            {
                DirectoryPath = directoryPath,
                FileCount = 0,
                FileCountFailIntegrity = 0
            };

            return directoryStats;
        }

        private void InitializeLocalVariables()
        {
            mMaximumTextFileLinesToCheck = InfoScannerOptions.DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK;
            mMaximumXMLElementNodesToCheck = InfoScannerOptions.DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK;

            ZipFileCheckAllData = true;

            mFastZippedSFileCheck = true;
            mZipFileLargeSizeThresholdMB = 500;

            ComputeFileHashes = false;

            StatusMessage = string.Empty;
        }

        private void LogErrors(string source, string message, Exception ex)
        {
            StatusMessage = string.Copy(message);

            var messageWithoutCRLF = StatusMessage.Replace(Environment.NewLine, "; ");

            if (string.IsNullOrEmpty(source))
                source = "Unknown_Source";

            if (ex == null)
            {
                OnErrorEvent(source + ": " + messageWithoutCRLF);
            }
            else
            {
                OnErrorEvent(source + ": " + messageWithoutCRLF, ex);
            }
        }

        private void LogFileIntegrityError(string filePath, string errorMessage)
        {
            FileIntegrityFailure?.Invoke(filePath, errorMessage);
        }

        public string MD5CalcFile(string path)
        {
            // Calculates the MD5 hash of a given file
            // Code from Tim Hastings, at http://www.nonhostile.com/page000017.asp

            var md5Hasher = new System.Security.Cryptography.MD5CryptoServiceProvider();

            using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Hash contents of this stream
            var arrHash = md5Hasher.ComputeHash(reader);

            // Return the hash, formatted as a string
            return ByteArrayToString(arrHash);
        }

        public string Sha1CalcFile(string path)
        {
            // Calculates the SHA-1 hash of a given file

            var sha1Hasher = new System.Security.Cryptography.SHA1CryptoServiceProvider();

            using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Hash contents of this stream
            var arrHash = sha1Hasher.ComputeHash(reader);

            // Return the hash, formatted as a string
            return ByteArrayToString(arrHash);
        }

        /// <summary>
        /// If needToCheckItem is True, logs an error if any of the items in requiredItemNames() were not found
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="itemDescription">Description of the types of items that were searched</param>
        /// <param name="needToCheckItem">True if we were still checking for items when this code was reached; if True, then indicates that not all of the items were found</param>
        /// <param name="requiredItemNames">Names to find; values are True if found</param>
        /// <param name="requiredItemMatchCountMinimum">Minimum number of items in requiredItemNames that needed to be found; 0 to require all items to be found</param>
        /// <param name="errorLogged">Set to True if any items were missing</param>
        private void ValidateRequiredTextFound(
            string filePath,
            string itemDescription,
            bool needToCheckItem,
            Dictionary<string, bool> requiredItemNames,
            int requiredItemMatchCountMinimum,
            ref bool errorLogged)
        {
            var matchCount = 0;

            if (!needToCheckItem)
            {
                return;
            }

            var errorMessage = "File did not contain all of the expected " + itemDescription;
            foreach (var requiredItem in requiredItemNames)
            {
                if (requiredItem.Value)
                {
                    matchCount++;
                }
                else
                {
                    errorMessage += "; missing '" + requiredItem.Key + "'";
                }
            }

            if (requiredItemMatchCountMinimum > 0 && matchCount >= requiredItemMatchCountMinimum)
            {
                // Not all of the items in requiredItemNames were matched, but at least requiredTextMinMatchCount were, so all is fine
            }
            else
            {
                LogFileIntegrityError(filePath, errorMessage);
                errorLogged = true;
            }
        }

        private void XMLTextReaderSkipWhitespace(XmlReader xmlReader)
        {
            if (xmlReader.NodeType == XmlNodeType.Whitespace)
            {
                // Whitespace; read the next node
                xmlReader.Read();
            }
        }
    }
}
