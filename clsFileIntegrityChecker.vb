Option Strict On

' This class will check the integrity of files in a given folder
'
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2008, Battelle Memorial Institute.  All Rights Reserved.
' Started May 16, 2008

Public Class clsFileIntegrityChecker

    Public Sub New()
        mFileDate = "5/26/2008"
        InitializeLocalVariables()
    End Sub

#Region "Constants and Enums"
    Public Const DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK As Integer = 500
    Public Const DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK As Integer = 500

    Public Const FILE_EXTENSION_TXT As String = ".TXT"
    Public Const FILE_EXTENSION_LOG As String = ".LOG"
    Public Const FILE_EXTENSION_PARAMS As String = ".PARAMS"
    Public Const FILE_EXTENSION_DAT As String = ".DAT"
    Public Const FILE_EXTENSION_TIC As String = ".TIC"
    Public Const FILE_EXTENSION_ZIP As String = ".ZIP"
    Public Const FILE_EXTENSION_CSV As String = ".CSV"
    Public Const FILE_EXTENSION_XML As String = ".XML"

    Public Const FINNIGAN_RAW_FILE_EXTENSION As String = ".RAW"
    Public Const AGILENT_TOF_OR_QSTAR_FILE_EXTENSION As String = ".WIFF"

#End Region

#Region "Structures"

    Public Structure udtFolderStatsType
        Public FolderPath As String
        Public FileCount As Integer
        Public FileCountFailIntegrity As Integer
    End Structure

    Public Structure udtFileStatsType
        Public FileName As String
        Public SizeBytes As Long
        Public ModificationDate As DateTime
        Public FailIntegrity As Boolean
        Public FileHash As String

        Public Sub Initialize()
            FileName = String.Empty
            SizeBytes = 0
            ModificationDate = DateTime.MinValue
            FailIntegrity = False
            FileHash = String.Empty
        End Sub
    End Structure

    Protected Structure udtZipFileWorkParamsType
        Public FilePath As String
        Public CheckAllData As Boolean
        Public ZipIsValid As Boolean
        Public FailureMessage As String
    End Structure
#End Region

#Region "Classwide Variables"
    Protected mFileDate As String
    Protected mStatusMessage As String

    Protected mMaximumTextFileLinesToCheck As Integer
    Protected mMaximumXMLElementNodesToCheck As Integer

    Protected mZipFileCheckAllData As Boolean
    Protected mZipFileLargeSizeThresholdMB As Single
    Protected mFastZippedSFileCheck As Boolean

    Protected mComputeFileHashes As Boolean

    Protected mZipFileWorkParams As udtZipFileWorkParamsType

    Public Event ErrorCaught(ByVal strMessage As String)
    Public Event FileIntegrityFailure(ByVal strFilePath As String, ByVal strMessage As String)

#End Region

#Region "Processing Options and Interface Functions"

    ''' <summary>
    ''' When True, then computes an MD5 hash on every file
    ''' </summary>
    Public Property ComputeFileHashes() As Boolean
        Get
            Return mComputeFileHashes
        End Get
        Set(ByVal value As Boolean)
            mComputeFileHashes = value
        End Set
    End Property

    Public Property MaximumTextFileLinesToCheck() As Integer
        Get
            Return mMaximumTextFileLinesToCheck
        End Get
        Set(ByVal value As Integer)
            If value < 0 Then value = 0
            mMaximumTextFileLinesToCheck = value
        End Set
    End Property

    Public Property MaximumXMLElementNodesToCheck() As Integer
        Get
            Return mMaximumXMLElementNodesToCheck
        End Get
        Set(ByVal value As Integer)
            If value < 0 Then value = 0
            mMaximumXMLElementNodesToCheck = value
        End Set
    End Property

    Public ReadOnly Property StatusMessage() As String
        Get
            Return mStatusMessage
        End Get
    End Property

    ''' <summary>
    ''' When True, then performs an exhaustive CRC check of each Zip file; otherwise, performs a quick test
    ''' </summary>
    Public Property ZipFileCheckAllData() As Boolean
        Get
            Return mZipFileCheckAllData
        End Get
        Set(ByVal value As Boolean)
            mZipFileCheckAllData = value
        End Set
    End Property
#End Region

    Private Function ByteArrayToString(ByVal arrInput() As Byte) As String
        ' Converts a byte array into a hex string

        Dim strOutput As New System.Text.StringBuilder(arrInput.Length)

        For i As Integer = 0 To arrInput.Length - 1
            strOutput.Append(arrInput(i).ToString("X2"))
        Next

        Return strOutput.ToString().ToLower

    End Function

    ''' <summary>
    ''' Checks the integrity of a text file
    ''' </summary>
    ''' <param name="strFilePath">File path to check</param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckTextFile(ByVal strFilePath As String) As Boolean

        Dim strFileNameLower As String
        Dim blnFileIsValid As Boolean = True

        strFileNameLower = System.IO.Path.GetFileName(strFilePath).ToLower

        If strFileNameLower = "analysissummary.txt" Then                    ' Analysis Manager Summary File
            ' Free form text file
            ' Example contents:
            '  Job Number	306839
            '  Date	5/16/2008 7:49:00 PM
            '  Processor	SeqCluster2
            '  Tool	Sequest
            blnFileIsValid = CheckTextFileWork(strFilePath, 10, 0, New String() {"Job", "Date", "FileVersion:", "ProductVersion:"}, False, True, 2)

        ElseIf strFileNameLower = "dataextractionsummary.txt" Then              ' DEX Manager Summary File
            ' Free form text file
            ' Example contents:
            '  Job Number: 306839
            '  Date: 5/16/2008 7:53:50 PM
            '  Processor: Mash-01
            blnFileIsValid = CheckTextFileWork(strFilePath, 5, 0, New String() {"Job", "Date", "FileVersion:", "ProductVersion:"}, False, True, 2)

        ElseIf strFileNameLower = "metadata.txt" Then                           ' Analysis Manager MetaData file
            ' Free form text file
            ' Example contents (I'm not sure if this file always looks like this):
            '  Proteomics
            '  Mass spectrometer
            '  OU_CN32_002_run3_3Apr08_Draco_07-12-25
            '  Apr  4 2008 10:01AM
            '  LTQ_Orb_1
            blnFileIsValid = CheckTextFileWork(strFilePath, 2, 0, "Proteomics", False, False)

        ElseIf strFileNameLower.EndsWith("_scanstats.txt") Then                     ' MASIC
            ' Note: Header line could be missing, but the file should always contain data
            ' Example contents:
            '  Dataset	ScanNumber	ScanTime	ScanType	TotalIonIntensity	BasePeakIntensity	BasePeakMZ	BasePeakSignalToNoiseRatio	IonCount	IonCountRaw
            '  113591	1	0.00968	1	145331	12762	531.0419	70.11	3147	3147
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 6, True)

        ElseIf strFileNameLower.EndsWith("_scanstatsconstant.txt") Then         ' MASIC
            ' Note: Header line could be missing, but the file should always contain data
            ' Example contents:
            '  Setting	Value
            '  AGC	On
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, True)

        ElseIf strFileNameLower.EndsWith("_scanstatsex.txt") Then               ' MASIC
            ' Note: Header line could be missing, but the file should always contain data
            ' Example contents:
            '  Dataset	ScanNumber	Ion Injection Time (ms)	Scan Segment	Scan Event	Master Index	Elapsed Scan Time (sec)	Charge State	Monoisotopic M/Z	MS2 Isolation Width	FT Analyzer Settings	FT Analyzer Message	FT Resolution	Conversion Parameter B	Conversion Parameter C	Conversion Parameter D	Conversion Parameter E	Collision Mode	Scan Filter Text
            '  113591	1	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0	0
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, True)

        ElseIf strFileNameLower.EndsWith("_msmethod.txt") Then                  ' MASIC
            ' Free form text file
            ' Example contents:
            '  Instrument model: LTQ Orbitrap
            '  Instrument name: LTQ Orbitrap
            '  Instrument description: 
            '  Instrument serial number: SN1006B
            '
            '  Creator: LTQ
            '  Last modified: 12/10/2007 by LTQ
            '
            '  MS Run Time (min): 99.50
            blnFileIsValid = CheckTextFileWork(strFilePath, 10, 0, New String() {"Instrument", "Creator"}, False)

        ElseIf strFileNameLower.EndsWith("_sicstats.txt") Then                  ' MASIC
            ' Note: Header line could be missing, but the file will usually (but not always) contain data
            ' Example contents:
            '  Dataset	ParentIonIndex	MZ	SurveyScanNumber	FragScanNumber	OptimalPeakApexScanNumber	PeakApexOverrideParentIonIndex	CustomSICPeak	PeakScanStart	PeakScanEnd	PeakScanMaxIntensity	PeakMaxIntensity	PeakSignalToNoiseRatio	FWHMInScans	PeakArea	ParentIonIntensity	PeakBaselineNoiseLevel	PeakBaselineNoiseStDev	PeakBaselinePointsUsed	StatMomentsArea	CenterOfMassScan	PeakStDev	PeakSkew	PeakKSStat	StatMomentsDataCountUsed
            '  113591	0	445.12	8	9	133	79	0	3	86	66	14881	0.4267	78	906920	11468	34870	22736	768	293248	68	6.36	-0.16	0.4162	5
            blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, True)

        ElseIf strFileNameLower.StartsWith("cat_log") Then                      ' SEQUEST
            ' Example contents:
            '  5/16/2008 7:41:55 PM, 14418 'dta' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_dta.txt', Normal, 
            '  5/16/2008 7:48:47 PM, 14418 'out' files were concatenated to 'D:\DMS_Work\OU_CN32_002_run3_3Apr08_Draco_07-12-25_out.txt', Normal, 
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 0, New String() {"were concatenated", "_dta.txt", "_out.txt"}, False, False, 1)

        ElseIf strFileNameLower.EndsWith("_fht.txt") Then                       ' SEQUEST
            ' Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
            ' Example contents:
            '  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
            '  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
            blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, True, True)

        ElseIf strFileNameLower.EndsWith("_syn.txt") Then                       ' SEQUEST
            ' Note: Header line could be missing, but the file should always contain data (unless no data was above the XCorr threshold (rare, but happens))
            ' Example contents:
            '  HitNum	ScanNum	ScanCount	ChargeState	MH	XCorr	DelCn	Sp	Reference	MultiProtein	Peptide	DelCn2	RankSp	RankXc	DelM	XcRatio	PassFilt	MScore	NumTrypticEnds
            '  1	10221	1	3	2618.37592	8.4305	0.0000	3751.6	P005|G3P_RABIT	+1	K.VIHDHFGIVEGLMTTVHAITATQK.T	0.5745	1	1	-0.00040	1.000	1	12.02	2
            blnFileIsValid = CheckTextFileWork(strFilePath, 0, 10, True, True)

        ElseIf strFileNameLower.EndsWith("_fht_prot.txt") Then                  ' SEQUEST
            ' Header line should always be present
            ' Example contents:
            '  RankXc	ScanNum	ChargeState	MultiProteinID	Reference	
            '  1	9	1	1	CN32_0001
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "RankXc", True)

        ElseIf strFileNameLower.EndsWith("_irr.txt") Then                       ' SEQUEST
            ' Header line should always be present
            ' Example contents:
            '  Scannum	CS	RankXc	ObservedIons	PossibleIons	
            '  9	1	1	4	6	
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "Scannum", True)

        ElseIf strFileNameLower.EndsWith("_nli.txt") Then                       ' SEQUEST
            ' Note: Header line could be missing
            ' Example contents:
            '  Scannum	NL1_Intensity	NL2_Intensity	NL3_Intensity	
            '  9	0	0	0	
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, True)

        ElseIf strFileNameLower.EndsWith("_xt.txt") Then                        ' X!Tandem
            ' Header line should always be present
            ' Example contents:
            '  Result_ID	Group_ID	Scan	Charge	Peptide_MH	Peptide_Hyperscore	Peptide_Expectation_Value_Log(e)	Multiple_Protein_Count	Peptide_Sequence	DeltaCn2	y_score	y_ions	b_score	b_ions	Delta_Mass	Peptide_Intensity_Log(I)
            '  1	3125	3541	2	1990.0049	74.4	-10.174	0	R.TDMESALPVTVLSAEDIAK.T	0.6949	12.9	11	11.7	11	-0.0054	6.22
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 10, True)

        ElseIf strFileNameLower = "lcq_dta.txt" Then
            ' Free form text file
            ' Example contents:
            '  extract_msn ver 4.0, Copyright 1997-2007
            '  Licensed to Thermo Fisher Scientific Inc.
            '  OU_CN32_002_run3_3Apr08_Draco_07-12-25  05/16/08, 07:39 PM
            '
            '  group scan          = 1
            '  min group count     = 1
            '  min ion threshold   = 35
            '  intensity threshold = 0
            '  precursor tolerance = 3.0000 amu
            '  mass range          = 200.0000 - 5000.0000
            '  scan range          = 1 - 15240
            '
            '     #     Scan   MasterScan   Precursor   Charge     (M+H)+  
            '  ------  ------  ----------  -----------  ------  -----------
            blnFileIsValid = CheckTextFileWork(strFilePath, 6, 0, New String() {"group scan", "mass range", "mass:", "Charge"}, False, False, 1)

        ElseIf strFileNameLower = "lcq_profile.txt" Then
            ' Example contents:
            '  Datafile FullScanSumBP FullScanMaxBP ZoomScanSumBP ZoomScanMaxBP SumTIC MaxTIC
            '  OU_CN32_002_run3_3Apr08_Draco_07-12-25.9.9.1.dta 11861 11861 0 0 13482 13482
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 0, "Datafile", False)

        ElseIf strFileNameLower = "xtandem_processing_log.txt" Then
            ' Example contents:
            '  2008-05-16 10:48:19	X! Tandem starting
            '  2008-05-16 10:48:19	loading spectra
            '  2008-05-16 10:48:23	.
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, True, True)

        ElseIf strFileNameLower = "mass_correction_tags.txt" Then
            ' Example contents:
            '  6C13    	6.02013	-
            '  6C132N15	8.0143	-
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, True)

        ElseIf strFileNameLower.EndsWith("_moddefs.txt") Then
            ' Note: File could be empty
            ' Example contents:
            '  *	15.9949	M	D	Plus1Oxy
            '  #	42.0106	<	D	Acetyl
            '  @	57.0215	C	D	IodoAcet
            '  &	-17.026549	Q	D	NH3_Loss
            '  $	-18.0106	E	D	MinusH2O
            blnFileIsValid = CheckTextFileWork(strFilePath, 0, 4, True)

        ElseIf strFileNameLower.EndsWith("_moddetails.txt") Then                ' PHRP
            ' Example contents:
            '  Unique_Seq_ID	Mass_Correction_Tag	Position
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 2, "Unique_Seq_ID", True)

        ElseIf strFileNameLower.EndsWith("_modsummary.txt") Then                ' PHRP
            ' Example contents:
            '  Modification_Symbol	Modification_Mass	Target_Residues	Modification_Type	Mass_Correction_Tag	Occurence_Count
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 4, "Modification_Symbol", True)

        ElseIf strFileNameLower.EndsWith("_resulttoseqmap.txt") Then            ' PHRP
            ' Example contents:
            '  Result_ID	Unique_Seq_ID
            '  1	1
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 1, "Result_ID", True)

        ElseIf strFileNameLower.EndsWith("_seqinfo.txt") Then                   ' PHRP
            ' Example contents:
            '  Unique_Seq_ID	Mod_Count	Mod_Description	Monoisotopic_Mass
            '  1	0		2617.3685121
            '
            ' OR
            '
            ' Row_ID	Unique_Seq_ID	Cleavage_State	Terminus_State	Mod_Count	Mod_Description	Monoisotopic_Mass
            ' 1	1	2	0	2	IodoAcet:3,IodoAcet:30	4436.0728061

            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, "Unique_Seq_ID", True, False)

        ElseIf strFileNameLower.EndsWith("_seqtoproteinmap.txt") Then           ' PHRP
            ' Example contents:
            '  Unique_Seq_ID	Cleavage_State	Terminus_State	Protein_Name	Protein_Expectation_Value_Log(e)	Protein_Intensity_Log(I)
            '  1	2	0	P005|G3P_RABIT		
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 5, "Unique_Seq_ID", True)

        ElseIf strFileNameLower.EndsWith("_pepprophet.txt") Then                ' Peptide Prophet
            ' Example contents:
            '  HitNum	FScore	Probability	negOnly
            '  1	9.5844	1	0
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 3, "HitNum", True)

        ElseIf strFileNameLower = "PeptideProphet_Coefficients.txt" Then
            ' Example contents:
            '  CS	Xcorr	DeltaCn2	RankSp	DelM	Const
            '  1	5.49	4.643	-0.455	-0.84	0.646
            blnFileIsValid = CheckTextFileWork(strFilePath, 1, 5, "CS", True)

        ElseIf strFileNameLower = "sequest.log" Then
            ' Free form text file
            ' Example contents:
            '  TurboSEQUEST - PVM Master v.27 (rev. 12), (c) 1998-2005
            '  Molecular Biotechnology, Univ. of Washington, J.Eng/S.Morgan/J.Yates
            '  Licensed to Thermo Electron Corp.
            ' 
            '  NumHosts = 10, NumArch = 1
            ' 
            '    Arch:WIN32  CPU:1  Tid:40000  Name:p1
            '    Arch:LINUXI386  CPU:4  Tid:80000  Name:node18
            blnFileIsValid = CheckTextFileWork(strFilePath, 5, 0)

        End If

        Return blnFileIsValid

    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, and minimum tab count
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, False, False, New String() {}, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, and blnRequireEqualTabsPerLine
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal blnRequireEqualTabsPerLine As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, New String() {}, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, blnRequireEqualTabsPerLine, and blnCharCountSkipsBlankLines
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal blnRequireEqualTabsPerLine As Boolean, ByVal blnCharCountSkipsBlankLines As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, New String() {}, True, blnCharCountSkipsBlankLines, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, and blnRequireEqualTabsPerLine
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal strRequiredTextLineHeader As String, ByVal blnRequireEqualTabsPerLine As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, New String() {strRequiredTextLineHeader}, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, single required text line header, blnRequireEqualTabsPerLine, and blnRequiredTextMatchesLineStart
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal strRequiredTextLineHeader As String, ByVal blnRequireEqualTabsPerLine As Boolean, ByVal blnRequiredTextMatchesLineStart As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, New String() {strRequiredTextLineHeader}, blnRequiredTextMatchesLineStart, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, and blnRequireEqualTabsPerLine
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal strRequiredTextLineHeaders() As String, ByVal blnRequireEqualTabsPerLine As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, strRequiredTextLineHeaders, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, array of required text line headers, blnRequireEqualTabsPerLine, and blnRequiredTextMatchesLineStart
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal strRequiredTextLineHeaders() As String, ByVal blnRequireEqualTabsPerLine As Boolean, ByVal blnRequiredTextMatchesLineStart As Boolean, ByVal intRequiredTextMinMatchCount As Integer) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, 0, blnRequireEqualTabsPerLine, False, strRequiredTextLineHeaders, blnRequiredTextMatchesLineStart, False, intRequiredTextMinMatchCount)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, and minimum comma count
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal intMinimumCommaCount As Integer) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, intMinimumCommaCount, False, False, New String() {}, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, minimum tab count, minimum comma count, blnRequireEqualTabsPerLine, and blnRequireEqualCommasPerLine
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal intMinimumTabCount As Integer, ByVal intMinimumCommaCount As Integer, ByVal blnRequireEqualTabsPerLine As Boolean, ByVal blnRequireEqualCommasPerLine As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, intMinimumTabCount, intMinimumCommaCount, blnRequireEqualTabsPerLine, blnRequireEqualCommasPerLine, New String() {}, True, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, and single required text line header
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal strRequiredTextLineHeader As String, ByVal blnRequiredTextMatchesLineStart As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, 0, 0, False, False, New String() {strRequiredTextLineHeader}, blnRequiredTextMatchesLineStart, False, 0)
    End Function

    ''' <summary>
    ''' Overloaded form of CheckTextFileWork; takes filename, minimum line count, and array of required text line headers
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, ByVal intMinimumLineCount As Integer, ByVal strRequiredTextLineHeaders() As String, ByVal blnRequiredTextMatchesLineStart As Boolean) As Boolean
        Return CheckTextFileWork(strFilePath, intMinimumLineCount, 0, 0, False, False, strRequiredTextLineHeaders, blnRequiredTextMatchesLineStart, False, 0)
    End Function

    ''' <summary>
    ''' Checks the integrity of a text file
    ''' </summary>
    ''' <param name="strFilePath">File path to check</param>
    ''' <param name="intMinimumLineCount">Minimum number of lines to examine; maximum number of lines is defined by mMaximumTextFileLinesToCheck</param>
    ''' <param name="intMinimumTabCount">Minimum number of tabs to require in each line</param>
    ''' <param name="intMinimumCommaCount">Minimum number of commas to require in each line</param>
    ''' <param name="blnRequireEqualTabsPerLine">If True, then requires that every line have an equal number of Tab characters</param>
    ''' <param name="blnRequireEqualCommasPerLine">If True, then requires that every line have an equal number of commas</param>
    ''' <param name="strRequiredTextLineHeaders">Optional list of text that must be found at the start of any of the text lines (within the first mMaximumTextFileLinesToCheck lines); the search text is case-sensitive</param>
    ''' <param name="blnRequiredtextMatchesLineStart">When True, then only examine the start of the line for the text in strRequiredTextLineHeaders</param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckTextFileWork(ByVal strFilePath As String, _
                                         ByVal intMinimumLineCount As Integer, _
                                         ByVal intMinimumTabCount As Integer, _
                                         ByVal intMinimumCommaCount As Integer, _
                                         ByVal blnRequireEqualTabsPerLine As Boolean, _
                                         ByVal blnRequireEqualCommasPerLine As Boolean, _
                                         ByVal strRequiredTextLineHeaders() As String, _
                                         ByVal blnRequiredTextMatchesLineStart As Boolean, _
                                         ByVal blnCharCountSkipsBlankLines As Boolean, _
                                         ByVal intRequiredTextMinMatchCount As Integer) As Boolean
        ' Open the text file and read the text line-by-line
        ' Check for a minimum number of lines being present, a minimum number of tab characters, and a minimum number of commas
        ' Additionally, look for lines that start with the text defined in strRequiredTextLineHeaders()
        ' File will fail the check if all of these conditions are not met

        Dim blnCheckLineHeadersForThisFile As Boolean = False
        Dim blnNeedToCheckLineHeaders As Boolean = False          ' If blnCheckLineHeadersForThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
        Dim intLineHeaderMatchCount As Integer = 0
        Dim blnLineHeaderFound() As Boolean

        Dim blnSuccess As Boolean

        Dim intLinesRead As Integer = 0
        Dim intMaximumTextFileLinesToCheck As Integer

        Dim strLineIn As String
        Dim blnBlankLineRead As Boolean = False

        Dim intExpectedTabCount As Integer = 0
        Dim intExpectedCommaCount As Integer = 0

        Dim strErrorMessage As String
        Dim blnErrorLogged As Boolean = False

        Dim ioInStream As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim blnFhtProtFile As Boolean = False

        If Not strRequiredTextLineHeaders Is Nothing AndAlso strRequiredTextLineHeaders.Length > 0 Then
            blnCheckLineHeadersForThisFile = True
            blnNeedToCheckLineHeaders = True
            ReDim blnLineHeaderFound(strRequiredTextLineHeaders.Length - 1)
        End If

        If mMaximumTextFileLinesToCheck <= 0 Then
            intMaximumTextFileLinesToCheck = Integer.MaxValue
        Else
            intMaximumTextFileLinesToCheck = mMaximumTextFileLinesToCheck
        End If

        If intMaximumTextFileLinesToCheck < 1 Then intMaximumTextFileLinesToCheck = 1
        If intMaximumTextFileLinesToCheck < intMinimumLineCount Then
            intMaximumTextFileLinesToCheck = intMinimumLineCount
        End If

        Try
            ' '' FHT protein files may have an extra tab at the end of the header line; need to account for this
            ''If strFilePath.EndsWith("_fht_prot.txt") Then
            ''    blnFhtProtFile = True
            ''End If

            ' Open the file
            ioInStream = New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            srInFile = New System.IO.StreamReader(ioInStream)

            ' Read each line and examine it
            Do While srInFile.Peek >= 0 AndAlso intLinesRead < intMaximumTextFileLinesToCheck
                strLineIn = srInFile.ReadLine
                intLinesRead += 1

                If blnCharCountSkipsBlankLines AndAlso strLineIn.Trim.Length = 0 Then
                    blnSuccess = True
                Else

                    If intMinimumTabCount > 0 Then
                        ' Count the number of tabs
                        blnSuccess = CheckTextFileCountChars(strLineIn, blnBlankLineRead, intLinesRead, intExpectedTabCount, ControlChars.Tab, "Tab", intMinimumTabCount, blnRequireEqualTabsPerLine, strErrorMessage)

                        If Not blnSuccess Then
                            LogFileIntegrityError(strFilePath, strErrorMessage)
                            blnErrorLogged = True
                            Exit Do
                        End If

                    End If

                    If intMinimumCommaCount > 0 Then
                        ' Count the number of commas
                        blnSuccess = CheckTextFileCountChars(strLineIn, blnBlankLineRead, intLinesRead, intExpectedCommaCount, ","c, "Comma", intMinimumCommaCount, blnRequireEqualCommasPerLine, strErrorMessage)

                        If Not blnSuccess Then
                            LogFileIntegrityError(strFilePath, strErrorMessage)
                            blnErrorLogged = True
                            Exit Do
                        End If

                    End If


                    If blnNeedToCheckLineHeaders Then
                        FindRequiredTextInLine(strLineIn, blnNeedToCheckLineHeaders, strRequiredTextLineHeaders, blnLineHeaderFound, intLineHeaderMatchCount, blnRequiredTextMatchesLineStart)
                    ElseIf intMinimumTabCount = 0 AndAlso intMinimumCommaCount = 0 AndAlso intLinesRead > intMinimumLineCount Then
                        ' All conditions have been met; no need to continue reading the file
                        Exit Do
                    End If

                End If

            Loop

            ' Make sure that all of the required line headers were found; log an error if any were missing
            ValidateRequiredTextFound(strFilePath, "line headers", blnCheckLineHeadersForThisFile, blnNeedToCheckLineHeaders, strRequiredTextLineHeaders, blnLineHeaderFound, intRequiredTextMinMatchCount, blnErrorLogged)

            If Not blnErrorLogged AndAlso intLinesRead < intMinimumLineCount Then
                strErrorMessage = "File contains " & intLinesRead.ToString & " lines of text, but the required minimum is " & intMinimumLineCount.ToString
                LogFileIntegrityError(strFilePath, strErrorMessage)
                blnErrorLogged = True
            End If

        Catch ex As System.Exception
            strErrorMessage = "Error checking file: " & strFilePath & "; " & ex.Message
            LogFileIntegrityError(strFilePath, strErrorMessage)
            blnErrorLogged = True
        Finally
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If
        End Try

        Return Not blnErrorLogged

    End Function

    ''' <summary>
    ''' Counts the number of occurrences of a given character in strLineIn
    ''' </summary>
    ''' <param name="strLineIn">Line to check</param>
    ''' <param name="blnBlankLineRead">Set to true if a blank line is read; however, if already true, and a non-blank line with an insufficient number of characters is read, then this function will return an error</param>
    ''' <param name="intLinesRead">Number of lines that have been read; when first calling this function for a new file, set this to 1 so that intExpectedCharCount will be initialized </param>
    ''' <param name="intExpectedCharCount">The number of occurrences of the given character in the previous line; used when blnRequireEqualCharsPerLine is True</param>
    ''' <param name="chCharToCount">The character to look for</param>
    ''' <param name="strCharDescription">A description of the character (used to populate strMessage when an error occurs)</param>
    ''' <param name="intMinimumCharCount">Minimum character count</param>
    ''' <param name="blnRequireEqualCharsPerLine">If True, then each line must contain an equal occurrence count of the given character (based on the first line in the file)</param>
    ''' <param name="strErrorMessage">Error message</param>
    ''' <returns>True if the line is valid; otherwise False; when False, then updates strErrorMessage</returns>
    ''' <remarks></remarks>
    Protected Function CheckTextFileCountChars(ByRef strLineIn As String, _
                                               ByRef blnBlankLineRead As Boolean, _
                                               ByVal intLinesRead As Integer, _
                                               ByRef intExpectedCharCount As Integer, _
                                               ByVal chCharToCount As Char, _
                                               ByVal strCharDescription As String, _
                                               ByVal intMinimumCharCount As Integer, _
                                               ByVal blnRequireEqualCharsPerLine As Boolean, _
                                               ByRef strErrorMessage As String) As Boolean

        Dim intCharCount As Integer

        Dim blnLineIsValid As Boolean = True

        ' Count the number of chCharToCount characters
        intCharCount = CountChars(strLineIn, chCharToCount)

        If strLineIn.EndsWith(chCharToCount) AndAlso intCharCount > 1 AndAlso intCharCount > intMinimumCharCount Then
            ' Decrement the char count by one since the line ends in the character we're counting
            intCharCount -= 1
        End If

        If intCharCount < intMinimumCharCount Then
            ' Character not found the minimum number of times

            If strLineIn.Length = 0 AndAlso Not blnBlankLineRead Then
                blnBlankLineRead = True
            ElseIf blnBlankLineRead AndAlso Not strLineIn.Length = 0 Then
                ' Previously read a blank line; now found a line that's not blank
                strErrorMessage = "Line " & intLinesRead.ToString & " has " & intCharCount.ToString & " " & strCharDescription & "s, but the required minimum is " & intMinimumCharCount.ToString
                blnLineIsValid = False
            Else
                strErrorMessage = "Line " & intLinesRead.ToString & " has " & intCharCount.ToString & " " & strCharDescription & "s, but the required minimum is " & intMinimumCharCount.ToString
                blnLineIsValid = False
            End If
        End If

        If blnLineIsValid AndAlso blnRequireEqualCharsPerLine Then
            If intLinesRead <= 1 Then
                intExpectedCharCount = intCharCount
            Else
                If intCharCount <> intExpectedCharCount Then
                    If strLineIn.Length > 0 Then
                        strErrorMessage = "Line " & intLinesRead.ToString & " has " & intCharCount.ToString & " " & strCharDescription & "s, but previous line has " & intExpectedCharCount.ToString & " tabs"
                        blnLineIsValid = False
                    End If
                End If
            End If
        End If

        Return blnLineIsValid

    End Function

    ''' <summary>
    ''' Checks the integrity of files without an extension
    ''' </summary>
    ''' <param name="strFilePath"></param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckExtensionFreeFile(ByVal strFilePath As String) As Boolean
        Dim blnLineIsValid As Boolean = True

        Select Case System.IO.Path.GetFileNameWithoutExtension(strFilePath).ToLower
            Case "acqu", "acqus"
                blnLineIsValid = CheckTextFileWork(strFilePath, 50, New String() {"##TITLE", "##DATA"}, True)

            Case "lock"
                blnLineIsValid = CheckTextFileWork(strFilePath, 1, New String() {"ftms"}, True)

            Case "sptype"
                ' Skip this file
                blnLineIsValid = True
        End Select

        Return blnLineIsValid
    End Function

    ''' <summary>
    ''' Checks the integrity of a Sequest Params file
    ''' </summary>
    ''' <param name="strFilePath"></param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckParamsFile(ByVal strFilePath As String) As Boolean

        Const MASS_TOLERANCE_LINE As String = "peptide_mass_tolerance"
        Const FRAGMENT_TOLERANCE_LINE As String = "fragment_ion_tolerance"
        Const MAX_LINES_TO_READ As Integer = 50

        Dim ioInStream As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim intLinesRead As Integer = 0

        Dim strLineIn As String

        Dim blnMassToleranceFound As Boolean = False
        Dim blnFragmentToleranceFound As Boolean = False
        Dim blnFileIsValid As Boolean = False

        Try
            ' Open the file
            ioInStream = New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            srInFile = New System.IO.StreamReader(ioInStream)

            ' Read each line in the file and look for the expected parameter lines
            Do While srInFile.Peek >= 0 AndAlso intLinesRead < MAX_LINES_TO_READ
                strLineIn = srInFile.ReadLine
                intLinesRead += 1

                If strLineIn.StartsWith(MASS_TOLERANCE_LINE) Then
                    blnMassToleranceFound = True
                End If

                If strLineIn.StartsWith(FRAGMENT_TOLERANCE_LINE) Then
                    blnFragmentToleranceFound = True
                End If

                If blnMassToleranceFound AndAlso blnFragmentToleranceFound Then
                    blnFileIsValid = True
                    Exit Do
                End If
            Loop

        Catch ex As System.Exception
            LogFileIntegrityError(strFilePath, "Error checking Sequest params file: " & strFilePath & "; " & ex.Message)
            blnFileIsValid = False
        Finally
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If
        End Try

        Return blnFileIsValid

    End Function

    ''' <summary>
    ''' Checks the integrity of an ICR-2LS TIC file
    ''' </summary>
    ''' <param name="strFilePath"></param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckTICFile(ByVal strFilePath As String) As Boolean

        Const ICR2LS_LINE_START As String = "ICR-2LS"
        Const VERSION_LINE_START As String = "VERSION"

        Dim ioInStream As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim intLinesRead As Integer = 0

        Dim strLineIn As String

        ' Assume True for now
        Dim blnFileIsValid As Boolean = True

        Try
            ' Open the file
            ioInStream = New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            srInFile = New System.IO.StreamReader(ioInStream)

            ' Confirm that the first two lines look like:
            '  ICR-2LS Data File (GA Anderson & JE Bruce); output from MASIC by Matthew E Monroe
            '  Version 2.4.2974.38283; February 22, 2008

            Do While srInFile.Peek >= 0 AndAlso intLinesRead < 2
                strLineIn = srInFile.ReadLine
                intLinesRead += 1

                If intLinesRead = 1 Then
                    If Not strLineIn.ToUpper.StartsWith(ICR2LS_LINE_START) Then
                        blnFileIsValid = False
                        Exit Do
                    End If
                ElseIf intLinesRead = 2 Then
                    If Not strLineIn.ToUpper.StartsWith(VERSION_LINE_START) Then
                        blnFileIsValid = False
                        Exit Do
                    End If
                Else
                    ' This code shouldn't be reached
                    Exit Do
                End If

            Loop

        Catch ex As System.Exception
            LogFileIntegrityError(strFilePath, "Error checking TIC file: " & strFilePath & "; " & ex.Message)
            blnFileIsValid = False
        Finally
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If
        End Try

        Return blnFileIsValid

    End Function

    ''' <summary>
    ''' Opens the given zip file and uses SharpZipLib's .TestArchive function to validate that it is valid
    ''' </summary>
    ''' <param name="strFilePath">File path to check</param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckZIPFile(ByVal strFilePath As String) As Boolean

        Const MAX_THREAD_RATE_CHECK_ALL_DATA As Single = 0.25  ' minutes/MB
        Const MAX_THREAD_RATE_QUICK_CHECK As Single = 0.125    ' minutes/MB

        Dim strFileNameLCase As String
        Dim objFileInfo As System.IO.FileInfo
        Dim dblFileSizeMB As Double

        Dim blnZipIsValid As Boolean = False
        Dim strMessage As String

        Dim objSharpZipLibTest As System.Threading.Thread
        Dim dtStartTime As DateTime
        Dim sngMaxExecutionTimeMinutes As Single

        Dim blnZipFileCheckAllData As Boolean
        blnZipFileCheckAllData = mZipFileCheckAllData

        ' Either run a fast check, or entirely skip this .Zip file if it's too large
        objFileInfo = New System.IO.FileInfo(strFilePath)
        dblFileSizeMB = objFileInfo.Length / 1024.0 / 1024.0

        If blnZipFileCheckAllData AndAlso mZipFileLargeSizeThresholdMB > 0 Then
            If dblFileSizeMB > mZipFileLargeSizeThresholdMB Then
                blnZipFileCheckAllData = False
            End If
        End If

        If dblFileSizeMB > 2040 Then
            ' File is too large for SharpZipLib; log an error but return true
            strMessage = "File is too large for SharpZipLib: " & Math.Round(dblFileSizeMB / 1024.0, 2).ToString & " GB"
            LogFileIntegrityError(strFilePath, strMessage)
            Return True
        End If

        If blnZipFileCheckAllData AndAlso mFastZippedSFileCheck Then
            strFileNameLCase = System.IO.Path.GetFileName(strFilePath).ToLower
            If strFileNameLCase = "0.ser.zip" Then
                ' Do not run a full check on 0.ser.zip files
                blnZipFileCheckAllData = False

            ElseIf strFileNameLCase.Length > 2 AndAlso strFileNameLCase.StartsWith("s") AndAlso Char.IsNumber(strFileNameLCase.Chars(1)) Then
                ' Run a full check on s001.zip but not the other s*.zip files
                If strFileNameLCase <> "s001.zip" Then
                    blnZipFileCheckAllData = False
                End If
            End If
        End If

        With mZipFileWorkParams
            .FilePath = strFilePath
            .CheckAllData = blnZipFileCheckAllData
            .ZipIsValid = False
            If .CheckAllData Then
                .FailureMessage = "Zip file failed exhaustive CRC check"
            Else
                .FailureMessage = "Zip file failed quick check"
            End If
        End With

        If blnZipFileCheckAllData Then
            sngMaxExecutionTimeMinutes = CSng(dblFileSizeMB * MAX_THREAD_RATE_CHECK_ALL_DATA)
        Else
            sngMaxExecutionTimeMinutes = CSng(dblFileSizeMB * MAX_THREAD_RATE_QUICK_CHECK)
        End If

        objSharpZipLibTest = New System.Threading.Thread(AddressOf CheckZipFileWork)
        dtStartTime = System.DateTime.Now

        objSharpZipLibTest.Start()
        Do
            objSharpZipLibTest.Join(250)

            If objSharpZipLibTest.ThreadState = Threading.ThreadState.Aborted Then
                Exit Do
            ElseIf System.DateTime.Now.Subtract(dtStartTime).TotalMinutes >= sngMaxExecutionTimeMinutes Then
                ' Execution took too long; abort
                objSharpZipLibTest.Abort()
                objSharpZipLibTest.Join(250)

                strMessage = mZipFileWorkParams.FailureMessage & "; over " & sngMaxExecutionTimeMinutes.ToString("0.0") & " minutes have elapsed, which is longer than the expected processing time"
                LogFileIntegrityError(mZipFileWorkParams.FilePath, strMessage)

                Exit Do
            End If
        Loop While objSharpZipLibTest.ThreadState <> Threading.ThreadState.Stopped

        Return mZipFileWorkParams.ZipIsValid

    End Function

    Private Sub CheckZipFileWork()
        Dim objZipFile As ICSharpCode.SharpZipLib.Zip.ZipFile
        Dim blnZipIsValid As Boolean = False
        Dim strMessage As String

        Try
            objZipFile = New ICSharpCode.SharpZipLib.Zip.ZipFile(mZipFileWorkParams.FilePath)

            blnZipIsValid = objZipFile.TestArchive(mZipFileWorkParams.CheckAllData)

            If Not blnZipIsValid Then
                If mZipFileWorkParams.CheckAllData Then
                    strMessage = "Zip file failed exhaustive CRC check"
                Else
                    strMessage = "Zip file failed quick check"
                End If

                LogFileIntegrityError(mZipFileWorkParams.FilePath, strMessage)
            End If

        Catch ex As System.Exception
            ' Error opening .Zip file
            LogFileIntegrityError(mZipFileWorkParams.FilePath, ex.Message)
        End Try

        mZipFileWorkParams.ZipIsValid = blnZipIsValid

    End Sub

    ''' <summary>
    ''' Checks the integrity of a CSV file
    ''' </summary>
    ''' <param name="strFilePath"></param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckCSVFile(ByVal strFilePath As String) As Boolean
        Dim intMinimumCommaCount As Integer
        Dim strHeaderRequired As String = String.Empty

        Dim strFileNameLower As String

        strFileNameLower = System.IO.Path.GetFileName(strFilePath).ToLower

        If strFileNameLower.EndsWith("_isos.csv") Then
            ' scan_num,charge,abundance,mz,fit,average_mw,monoisotopic_mw,mostabundant_mw,fwhm,signal_noise,mono_abundance,mono_plus2_abundance
            strHeaderRequired = "scan_num"
            intMinimumCommaCount = 10

        ElseIf strFileNameLower.EndsWith("_scans.csv") Then
            ' scan_num,scan_time,type,bpi,bpi_mz,tic,num_peaks,num_deisotoped
            strHeaderRequired = "scan_num"
            intMinimumCommaCount = 7
        Else
            ' Unknown CSV file; do not check it
            intMinimumCommaCount = 0
        End If

        If intMinimumCommaCount > 0 Then
            Return CheckTextFileWork(strFilePath, 1, 0, intMinimumCommaCount, False, True, New String() {strHeaderRequired}, True, False, 0)
        Else
            Return True
        End If

    End Function

    ''' <summary>
    ''' Validates the given XML file; tries to determine the expected element names based on the file name and its parent folder
    ''' </summary>
    ''' <param name="strFilePath">File Path</param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckXMLFile(ByVal strFilePath As String) As Boolean
        ' Examine the parent folder name to determine the type of XML file strFilePath most likely is

        Dim ioFile As System.IO.FileInfo

        Dim strFileNameLCase As String
        Dim strParentFolderName As String

        Dim blnXMLIsValid As Boolean = True

        ioFile = New System.IO.FileInfo(strFilePath)

        strFileNameLCase = ioFile.Name.ToLower
        strParentFolderName = ioFile.Directory.Name

        Select Case strParentFolderName.Substring(0, 3).ToUpper
            Case "SIC", "DLS"
                ' MASIC or Decon2LS folder
                If FileIsXMLSettingsFile(ioFile.FullName) Then
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, New String() {"section", "item"}, New String() {"key", "value"})
                ElseIf FileIsDecon2LSXMLSettingsFile(ioFile.FullName) Then
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, New String() {"parameters", "PeakParameters"}, New String() {})
                Else
                    ' Unknown XML file; just check for one element
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 1)
                End If

            Case "SEQ"
                ' Sequest folder
                If strFileNameLCase = "finnigandefsettings.xml" OrElse FileIsXMLSettingsFile(ioFile.FullName) Then
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, New String() {"section", "item"}, New String() {"key", "value"})
                Else
                    ' Unknown XML file; just check for one element
                    blnXMLIsValid = CheckXMLFileWork(strFilePath, 1)
                End If

            Case "XTM"
                ' Xtandem folder
                Select Case strFileNameLCase
                    Case "default_input.xml"
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 10, New String() {"bioml", "note"}, New String() {"type", "label"})

                    Case "input.xml"
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, New String() {"bioml", "note"}, New String() {"type", "label"})

                    Case "taxonomy.xml"
                        blnXMLIsValid = CheckXMLFileWork(strFilePath, 3, New String() {"bioml", "taxon"}, New String() {"label", "format"})

                    Case Else
                        If strFileNameLCase = "iontrapdefsettings.xml" OrElse FileIsXMLSettingsFile(ioFile.FullName) Then
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 5, New String() {"section", "item"}, New String() {"key", "value"})
                        ElseIf strFileNameLCase.StartsWith("xtandem_") Then
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 2, New String() {"bioml", "note"}, New String() {})
                        Else
                            ' Unknown XML file; just check for one element
                            blnXMLIsValid = CheckXMLFileWork(strFilePath, 1)
                        End If
                End Select

            Case Else
                ' Unknown XML file; just check for one element
                blnXMLIsValid = CheckXMLFileWork(strFilePath, 1)
        End Select

        Return blnXMLIsValid

    End Function

    ''' <summary>
    ''' Overloaded version of CheckXMLFileWork; takes filename and minimum element count
    ''' </summary>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    Protected Function CheckXMLFileWork(ByVal strFilePath As String, ByVal intMinimumElementCount As Integer) As Boolean
        Return CheckXMLFileWork(strFilePath, intMinimumElementCount, New String() {}, New String() {})
    End Function

    ''' <summary>
    ''' Validates the contents of the given XML file
    ''' </summary>
    ''' <param name="strFilePath">File Path</param>
    ''' <param name="intMinimumElementCount">Minimum number of XML elements that must be in the file; maximum number of elements is defined by mMaximumXMLElementNodesToCheck</param>
    ''' <param name="strRequiredElementNames">Optional list of element names that must be found (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
    ''' <param name="strRequiredAttributeNames">Optional list of attribute names that must be found  (within the first mMaximumXMLElementNodesToCheck elements); the names are case-sensitive</param>
    ''' <returns>True if the file passes the integrity check; otherwise False</returns>
    ''' <remarks></remarks>
    Protected Function CheckXMLFileWork(ByVal strFilePath As String, _
                                        ByVal intMinimumElementCount As Integer, _
                                        ByVal strRequiredElementNames() As String, _
                                        ByVal strRequiredAttributeNames() As String) As Boolean

        Dim srInFile As System.IO.StreamReader
        Dim objXMLReader As System.Xml.XmlTextReader

        Dim blnCheckElementNamesThisFile As Boolean = False
        Dim blnNeedToCheckElementNames As Boolean = False           ' If blnCheckElementNamesThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
        Dim intElementNameMatchCount As Integer = 0
        Dim blnElementNameFound() As Boolean

        Dim blnCheckAttributeNamesThisFile As Boolean = False
        Dim blnNeedToCheckAttributeNames As Boolean = False         ' If blnCheckAttributeNamesThisFile is True, then this will be set to True.  However, once all of the expected headers are found, this is set to False
        Dim intAttributeNameMatchCount As Integer = 0
        Dim blnAttributeNameFound() As Boolean

        Dim intMaximumXMLElementNodesToCheck As Integer
        Dim intElementsRead As Integer = 0

        Dim blnErrorLogged As Boolean = False
        Dim strErrorMessage As String

        Try
            If Not strRequiredElementNames Is Nothing AndAlso strRequiredElementNames.Length > 0 Then
                blnCheckElementNamesThisFile = True
                blnNeedToCheckElementNames = True
                ReDim blnElementNameFound(strRequiredElementNames.Length - 1)
            End If

            If Not strRequiredAttributeNames Is Nothing AndAlso strRequiredAttributeNames.Length > 0 Then
                blnCheckAttributeNamesThisFile = True
                blnNeedToCheckAttributeNames = True
                ReDim blnAttributeNameFound(strRequiredAttributeNames.Length - 1)
            End If

            If mMaximumXMLElementNodesToCheck <= 0 Then
                intMaximumXMLElementNodesToCheck = Integer.MaxValue
            Else
                intMaximumXMLElementNodesToCheck = mMaximumXMLElementNodesToCheck
            End If

            If intMaximumXMLElementNodesToCheck < 1 Then intMaximumXMLElementNodesToCheck = 1
            If intMaximumXMLElementNodesToCheck < intMinimumElementCount Then
                intMaximumXMLElementNodesToCheck = intMinimumElementCount
            End If

            Try
                ' Initialize the stream reader and the XML Text Reader
                srInFile = New System.IO.StreamReader(strFilePath)
                objXMLReader = New System.Xml.XmlTextReader(srInFile)

                ' Read each of the nodes and examine them
                Do While objXMLReader.Read()

                    XMLTextReaderSkipWhitespace(objXMLReader)
                    If Not objXMLReader.ReadState = Xml.ReadState.Interactive Then Exit Do


                    If objXMLReader.NodeType = Xml.XmlNodeType.Element Then
                        ' Note: If needed, read the element's value using XMLTextReaderGetInnerText(objXMLReader)

                        If blnNeedToCheckElementNames Then
                            FindRequiredTextInLine(objXMLReader.Name, blnNeedToCheckElementNames, strRequiredElementNames, blnElementNameFound, intElementNameMatchCount, True)
                        End If

                        If blnNeedToCheckAttributeNames AndAlso objXMLReader.HasAttributes Then
                            If objXMLReader.MoveToFirstAttribute Then
                                Do
                                    FindRequiredTextInLine(objXMLReader.Name, blnNeedToCheckAttributeNames, strRequiredAttributeNames, blnAttributeNameFound, intAttributeNameMatchCount, True)
                                    If Not blnNeedToCheckAttributeNames Then Exit Do
                                Loop While objXMLReader.MoveToNextAttribute

                            End If
                        End If

                        intElementsRead += 1
                        If intMaximumXMLElementNodesToCheck > 0 AndAlso intElementsRead >= MaximumXMLElementNodesToCheck Then
                            Exit Do
                        ElseIf Not blnNeedToCheckElementNames AndAlso Not blnNeedToCheckAttributeNames AndAlso intElementsRead > intMinimumElementCount Then
                            ' All conditions have been met; no need to continue reading the file
                            Exit Do
                        End If

                    End If

                Loop

                ' Make sure that all of the required element names were found; log an error if any were missing
                ValidateRequiredTextFound(strFilePath, "XML elements", blnCheckElementNamesThisFile, blnNeedToCheckElementNames, strRequiredElementNames, blnElementNameFound, strRequiredElementNames.Length, blnErrorLogged)

                ' Make sure that all of the required attribute names were found; log an error if any were missing
                ValidateRequiredTextFound(strFilePath, "XML attributes", blnCheckAttributeNamesThisFile, blnNeedToCheckAttributeNames, strRequiredAttributeNames, blnAttributeNameFound, strRequiredAttributeNames.Length, blnErrorLogged)

                If Not blnErrorLogged AndAlso intElementsRead < intMinimumElementCount Then
                    strErrorMessage = "File contains " & intElementsRead.ToString & " XML elements, but the required minimum is " & intMinimumElementCount.ToString
                    LogFileIntegrityError(strFilePath, strErrorMessage)
                    blnErrorLogged = True
                End If

            Catch ex As System.Exception
                ' Error opening file or stepping through file
                LogFileIntegrityError(strFilePath, ex.Message)
                blnErrorLogged = True
            Finally
                If Not objXMLReader Is Nothing Then
                    objXMLReader.Close()
                End If

                If Not srInFile Is Nothing Then
                    srInFile.Close()
                End If
            End Try

        Catch ex As System.Exception
            LogErrors("CheckXMLFileWork", "Error opening XML file: " & strFilePath, ex)
            blnErrorLogged = True
        End Try

        Return Not blnErrorLogged

    End Function


    ''' <summary>
    ''' Checks the integrity of each file in the given folder (provided the extension is recognized)
    ''' Will populate udtFolderStats with stats on the files in this folder
    ''' Will populate udtFileDetails with the name of each file parsed, plus details on the files
    ''' </summary>
    ''' <param name="strFolderPath">Folder to examine</param>
    ''' <param name="udtFolderStats">Stats on the folder, including number of files and number of files that failed the integrity check</param>
    ''' <param name="udtFileStats">Details on each file checked; use udtFolderStatsType.FileCount to determine the number of entries in udtFileStats </param>
    ''' <param name="strFileIgnoreList">List of files to skip; can be file names or full file paths</param>
    ''' <returns>Returns True if all files pass the integrity checks; otherwise, returns False</returns>
    ''' <remarks>Note that udtFileStats will never be shrunk in size; only increased as needed</remarks>
    Public Function CheckIntegrityOfFilesInFolder(ByVal strFolderPath As String, _
                                                  ByRef udtFolderStats As udtFolderStatsType, _
                                                  ByRef udtFileStats() As udtFileStatsType, _
                                                  ByRef strFileIgnoreList() As String) As Boolean

        Dim objMSInfoScanner As MSFileInfoScanner.iMSFileInfoProcessor
        Dim udtFileInfo As MSFileInfoScanner.iMSFileInfoProcessor.udtFileInfoType

        Dim ioFolderInfo As System.IO.DirectoryInfo
        Dim ioFile As System.IO.FileInfo

        Dim intIndex As Integer
        Dim blnPassedIntegrityCheck As Boolean

        Dim blnUseIgnoreList As Boolean = False
        Dim blnSkipFile As Boolean

        Try
            If udtFileStats Is Nothing Then
                ReDim udtFileStats(9)
            End If

            For intIndex = 0 To udtFileStats.Length - 1
                udtFileStats(intIndex).Initialize()
            Next

            If Not strFileIgnoreList Is Nothing AndAlso strFileIgnoreList.Length > 0 Then
                ' Assure strFileIgnoreList is sorted and that the entries are lowercase
                blnUseIgnoreList = True
                Array.Sort(strFileIgnoreList)

                For intIndex = 0 To strFileIgnoreList.Length - 1
                    If strFileIgnoreList.Length > 0 Then
                        strFileIgnoreList(intIndex) = strFileIgnoreList(intIndex).ToLower
                    End If
                Next
            End If

            ioFolderInfo = New System.IO.DirectoryInfo(strFolderPath)

            udtFolderStats = GetNewFolderStats(ioFolderInfo.FullName)

            For Each ioFile In ioFolderInfo.GetFiles()

                Try

                    ' Assume True for now
                    blnPassedIntegrityCheck = True
                    blnSkipFile = False

                    If blnUseIgnoreList Then
                        If Array.BinarySearch(strFileIgnoreList, ioFile.FullName.ToLower) >= 0 Then
                            blnSkipFile = True
                        ElseIf Array.BinarySearch(strFileIgnoreList, ioFile.Name.ToLower) >= 0 Then
                            blnSkipFile = True
                        End If
                    End If

                    If Not blnSkipFile = True Then

                        Select Case ioFile.Extension.ToUpper
                            Case FILE_EXTENSION_TXT, FILE_EXTENSION_LOG
                                blnPassedIntegrityCheck = CheckTextFile(ioFile.FullName)


                            Case FILE_EXTENSION_PARAMS
                                blnPassedIntegrityCheck = CheckParamsFile(ioFile.FullName)

                            Case FILE_EXTENSION_DAT
                                ' ToDo: Possibly check these files (Decon2LS DAT files)

                            Case FILE_EXTENSION_TIC
                                blnPassedIntegrityCheck = CheckTICFile(ioFile.FullName)

                            Case FILE_EXTENSION_ZIP
                                blnPassedIntegrityCheck = CheckZIPFile(ioFile.FullName)

                            Case FILE_EXTENSION_CSV
                                blnPassedIntegrityCheck = CheckCSVFile(ioFile.FullName)

                            Case FILE_EXTENSION_XML
                                blnPassedIntegrityCheck = CheckXMLFile(ioFile.FullName)

                            Case FINNIGAN_RAW_FILE_EXTENSION
                                ' File was not in strFileIgnoreList
                                ' Re-check using clsFinniganRawFileInfoScanner

                                objMSInfoScanner = New clsFinniganRawFileInfoScanner
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, False)
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, False)
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, False)

                                blnPassedIntegrityCheck = objMSInfoScanner.ProcessDatafile(ioFile.FullName, udtFileInfo)

                            Case AGILENT_TOF_OR_QSTAR_FILE_EXTENSION
                                ' File was not in strFileIgnoreList
                                ' Re-check using clsAgilentTOFOrQStarWiffFileInfoScanner

                                objMSInfoScanner = New clsAgilentTOFOrQStarWiffFileInfoScanner
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI, False)
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores, False)
                                objMSInfoScanner.SetOption(iMSFileInfoProcessor.ProcessingOptions.CreateDatasetInfoFile, False)

                                blnPassedIntegrityCheck = objMSInfoScanner.ProcessDatafile(ioFile.FullName, udtFileInfo)

                            Case "."
                                ' No extension
                                blnPassedIntegrityCheck = CheckExtensionFreeFile(ioFile.FullName)

                            Case Else
                                ' Do not check this file (but add it to udtFileStats anyway)

                        End Select
                    End If

                    If udtFolderStats.FileCount >= udtFileStats.Length Then
                        ' Reserve more space in udtFileStats
                        ReDim Preserve udtFileStats(udtFileStats.Length * 2 - 1)
                    End If

                    With udtFileStats(udtFolderStats.FileCount)
                        .FileName = ioFile.FullName
                        .ModificationDate = ioFile.LastWriteTime
                        .SizeBytes = ioFile.Length
                        .FailIntegrity = Not blnPassedIntegrityCheck

                        If mComputeFileHashes Then
                            '.FileHash = MD5CalcFile(ioFile.FullName)
                            .FileHash = Sha1CalcFile(ioFile.FullName)
                        End If
                    End With

                    udtFolderStats.FileCount += 1
                    If Not blnPassedIntegrityCheck Then
                        udtFolderStats.FileCountFailIntegrity += 1
                    End If

                Catch ex As System.Exception
                    LogErrors("CheckIntegrityOfFilesInFolder", "Error checking file " & ioFile.FullName, ex)
                End Try

            Next ioFile

        Catch ex As System.Exception
            LogErrors("CheckIntegrityOfFilesInFolder", "Error in CheckIntegrityOfFilesInFolder", ex)
        End Try

        If udtFolderStats.FileCountFailIntegrity = 0 Then
            Return True
        Else
            Return False
        End If

    End Function

    Protected Function CountChars(ByVal strText As String, ByVal chSearchChar As Char) As Integer
        Dim intCharCount As Integer = 0
        Dim intMatchIndex As Integer = -1

        Do
            intMatchIndex = strText.IndexOf(chSearchChar, intMatchIndex + 1)
            If intMatchIndex >= 0 Then
                intCharCount += 1
            Else
                Exit Do
            End If
        Loop

        Return intCharCount
    End Function

    ''' <summary>
    ''' Searches strLineToSearch for each of the items in strRequiredText; if blnMatchStart = True, then only checks the start of the line
    ''' </summary>
    ''' <param name="strLineToSearch">Text to search</param>
    ''' <param name="blnNeedToCheckRequiredText">True until all items in strRequiredText() have been found</param>
    ''' <param name="strRequiredText">List of items to look for</param>
    ''' <param name="blnRequiredTextFound">Set to True when each item is found</param>
    ''' <param name="intRequiredTextMatchCount">Total number of items that have been matched; equivalent to the number of True entries in blnRequiredTextFound</param>
    ''' <param name="blnMatchStart"></param>
    ''' <remarks></remarks>
    Protected Sub FindRequiredTextInLine(ByRef strLineToSearch As String, _
                                         ByRef blnNeedToCheckRequiredText As Boolean, _
                                         ByRef strRequiredText() As String, _
                                         ByRef blnRequiredTextFound() As Boolean, _
                                         ByRef intRequiredTextMatchCount As Integer, _
                                         ByVal blnMatchStart As Boolean)

        Dim intIndex As Integer

        If strRequiredText.Length > 0 Then
            For intIndex = 0 To blnRequiredTextFound.Length - 1
                If Not blnRequiredTextFound(intIndex) Then
                    If blnMatchStart Then
                        If strLineToSearch.StartsWith(strRequiredText(intIndex)) Then
                            blnRequiredTextFound(intIndex) = True
                            intRequiredTextMatchCount += 1
                            Exit For
                        End If
                    Else
                        If strLineToSearch.Contains(strRequiredText(intIndex)) Then
                            blnRequiredTextFound(intIndex) = True
                            intRequiredTextMatchCount += 1
                            Exit For
                        End If
                    End If
                End If
            Next intIndex

            If intRequiredTextMatchCount >= blnRequiredTextFound.Length Then
                ' All required headers have been matched
                ' Do not need to check for line headers any more
                blnNeedToCheckRequiredText = False
            End If
        End If

    End Sub

    ''' <summary>
    ''' Opens the file using a text reader and looks for XML elements parameters and PeakParameters
    ''' </summary>
    ''' <returns>True if this file contains the XML elements that indicate this is an Decon2LS XML settings file</returns>
    ''' <remarks></remarks>
    Protected Function FileIsDecon2LSXMLSettingsFile(ByVal strFilePath As String) As Boolean
        Return XMLFileContainsElements(strFilePath, New String() {"<parameters>", "<peakparameters>"})
    End Function

    ''' <summary>
    ''' Opens the file using a text reader and looks for XML elements "sections" and "section"
    ''' </summary>
    ''' <param name="strFilePath">File to examine</param>
    ''' <returns>True if this file contains the XML elements that indicate this is an XML settings file</returns>
    ''' <remarks></remarks>
    Protected Function FileIsXMLSettingsFile(ByVal strFilePath As String) As Boolean
        Return XMLFileContainsElements(strFilePath, New String() {"<sections>", "<section", "<item"})
    End Function

    ''' <summary>
    ''' Overloaded form of XMLFileContainsElements; assumes intMaximumTextFileLinesToCheck = 50
    ''' </summary>
    ''' <returns>True if this file contains the required element text</returns>
    ''' ''' <remarks></remarks>
    Protected Function XMLFileContainsElements(ByVal strFilePath As String, ByVal strElementsToMatch() As String) As Boolean
        Return XMLFileContainsElements(strFilePath, strElementsToMatch, 50)
    End Function

    ''' <summary>
    ''' Opens the file using a text reader and looks for XML elements specified in strElementsToMatch()
    ''' </summary>
    ''' <param name="strFilePath">File to examine</param>
    ''' <param name="strElementsToMatch">Element text to match; item text must include the desired element Less Than Signs to match; items must be all lower-case</param>
    ''' <returns>True if this file contains the required element text</returns>
    ''' <remarks></remarks>
    Protected Function XMLFileContainsElements(ByVal strFilePath As String, ByVal strElementsToMatch() As String, ByVal intMaximumTextFileLinesToCheck As Integer) As Boolean

        Dim ioInStream As System.IO.FileStream
        Dim srInFile As System.IO.StreamReader

        Dim intLinesRead As Integer = 0
        Dim intIndex As Integer

        Dim strLineIn As String

        Dim intElementMatchCount As Integer
        Dim blnElementFound() As Boolean

        Dim blnAllElementsFound As Boolean = False

        Try
            If strElementsToMatch Is Nothing OrElse strElementsToMatch.Length = 0 Then
                Return False
            End If

            intElementMatchCount = 0
            ReDim blnElementFound(strElementsToMatch.Length - 1)

            ' Read, at most, the first intMaximumTextFileLinesToCheck lines to determine if this is an XML settings file
            If intMaximumTextFileLinesToCheck < 10 Then
                intMaximumTextFileLinesToCheck = 50
            End If

            ' Open the file
            ioInStream = New System.IO.FileStream(strFilePath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
            srInFile = New System.IO.StreamReader(ioInStream)

            ' Read each line and examine it
            Do While srInFile.Peek >= 0 AndAlso intLinesRead < intMaximumTextFileLinesToCheck
                strLineIn = srInFile.ReadLine
                intLinesRead += 1

                If Not strLineIn Is Nothing Then
                    strLineIn = strLineIn.Trim.ToLower

                    For intIndex = 0 To blnElementFound.Length - 1
                        If Not blnElementFound(intIndex) Then
                            If strLineIn.Trim.StartsWith(strElementsToMatch(intIndex)) Then
                                blnElementFound(intIndex) = True
                                intElementMatchCount += 1

                                If intElementMatchCount = blnElementFound.Length Then
                                    blnAllElementsFound = True
                                    Exit Do
                                End If

                                Exit For
                            End If
                        End If
                    Next
                End If
            Loop

        Catch ex As System.Exception
            LogErrors("XMLFileContainsElements", "Error checking XML file for desired elements: " & strFilePath, ex)
        Finally
            If Not srInFile Is Nothing Then
                srInFile.Close()
            End If
        End Try

        Return blnAllElementsFound

    End Function

    Public Shared Function GetNewFolderStats(ByVal strFolderPath As String) As udtFolderStatsType
        Dim udtFolderStats As udtFolderStatsType

        With udtFolderStats
            .FolderPath = strFolderPath
            .FileCount = 0
            .FileCountFailIntegrity = 0
        End With

        Return udtFolderStats

    End Function

    Private Sub InitializeLocalVariables()
        mMaximumTextFileLinesToCheck = DEFAULT_MAXIMUM_TEXT_FILE_LINES_TO_CHECK
        mMaximumXMLElementNodesToCheck = DEFAULT_MAXIMUM_XML_ELEMENT_NODES_TO_CHECK

        mZipFileCheckAllData = True

        mFastZippedSFileCheck = True
        mZipFileLargeSizeThresholdMB = 500

        mComputeFileHashes = False

        mStatusMessage = String.Empty

    End Sub

    Private Sub LogErrors(ByVal strSource As String, ByVal strMessage As String, ByVal ex As System.Exception)

        Dim strMessageWithoutCRLF As String

        mStatusMessage = String.Copy(strMessage)

        strMessageWithoutCRLF = mStatusMessage.Replace(ControlChars.NewLine, "; ")

        If ex Is Nothing Then
            ex = New System.Exception("Error")
        Else
            If Not ex.Message Is Nothing AndAlso ex.Message.Length > 0 Then
                strMessageWithoutCRLF &= "; " & ex.Message
            End If
        End If

        RaiseEvent ErrorCaught(strSource & ": " & strMessageWithoutCRLF)
    End Sub

    Protected Sub LogFileIntegrityError(ByVal strFilePath As String, ByVal strErrorMessage As String)

        RaiseEvent FileIntegrityFailure(strFilePath, strErrorMessage)
    End Sub

    Public Function MD5CalcFile(ByVal strPath As String) As String
        ' Calculates the MD5 hash of a given file
        ' Code from Tim Hastings, at http://www.nonhostile.com/page000017.asp

        Dim objReader As System.IO.Stream
        Dim objMD5 As New System.Security.Cryptography.MD5CryptoServiceProvider
        Dim arrHash() As Byte

        ' open file (as read-only)
        objReader = New System.IO.FileStream(strPath, IO.FileMode.Open, IO.FileAccess.Read)

        ' hash contents of this stream
        arrHash = objMD5.ComputeHash(objReader)

        ' Cleanup the objects
        objReader.Close()
        objReader = Nothing
        objMD5 = Nothing

        ' Return the hash, formatted as a string
        Return ByteArrayToString(arrHash)

    End Function

    Public Function Sha1CalcFile(ByVal strPath As String) As String
        ' Calculates the Sha-1 hash of a given file

        Dim objReader As System.IO.Stream
        Dim objSha1 As New System.Security.Cryptography.SHA1CryptoServiceProvider
        Dim arrHash() As Byte

        ' open file (as read-only)
        objReader = New System.IO.FileStream(strPath, IO.FileMode.Open, IO.FileAccess.Read)

        ' hash contents of this stream
        arrHash = objSha1.ComputeHash(objReader)

        ' Cleanup the objects
        objReader.Close()
        objReader = Nothing
        objSha1 = Nothing

        ' Return the hash, formatted as a string
        Return ByteArrayToString(arrHash)

    End Function

    ''' <summary>
    ''' If blnCheckRequiredTextThisFile = True, then makes logs an error if any of the items in blnRequiredTextFound() were not found
    ''' </summary>
    ''' <param name="strFilePath">File path</param>
    ''' <param name="strItemDescription">Description of the types of items that were searched</param>
    ''' <param name="blnCheckRequiredTextThisFile">True if checking was enabled in this file</param>
    ''' <param name="blnNeedToCheckRequiredText">True if we were still checking for items when this code was reached; if True, then indicates that not all of the items were found</param>
    ''' <param name="blnRequiredTextFound">True if the given item was found</param>
    ''' <param name="blnErrorLogged">Set to True if any items were missing</param>
    ''' <remarks></remarks>
    Protected Sub ValidateRequiredTextFound(ByVal strFilePath As String, _
                                            ByVal strItemDescription As String, _
                                            ByVal blnCheckRequiredTextThisFile As Boolean, _
                                            ByVal blnNeedToCheckRequiredText As Boolean, _
                                            ByRef strRequiredText() As String, _
                                            ByRef blnRequiredTextFound() As Boolean, _
                                            ByVal intRequiredTextMinMatchCount As Integer, _
                                            ByRef blnErrorLogged As Boolean)

        Dim strErrorMessage As String
        Dim intIndex As Integer
        Dim intMatchCount As Integer = 0

        If Not blnErrorLogged AndAlso blnCheckRequiredTextThisFile Then
            If blnNeedToCheckRequiredText Then
                strErrorMessage = "File did not contain all of the expected " & strItemDescription
                For intIndex = 0 To blnRequiredTextFound.Length - 1
                    If blnRequiredTextFound(intIndex) Then
                        intMatchCount += 1
                    Else
                        strErrorMessage &= "; missing '" & strRequiredText(intIndex) & "'"
                    End If
                Next intIndex

                If intRequiredTextMinMatchCount > 0 AndAlso intMatchCount >= intRequiredTextMinMatchCount Then
                    ' Not all of the items in strRequiredText() were matched, but at least intRequiredTextMinMatchCount were, so all is fine
                Else
                    LogFileIntegrityError(strFilePath, strErrorMessage)
                    blnErrorLogged = True
                End If
            End If
        End If
    End Sub

    Private Function XMLTextReaderGetInnerText(ByRef objXMLReader As System.Xml.XmlTextReader) As String
        Dim strValue As String = String.Empty
        Dim blnSuccess As Boolean

        If objXMLReader.NodeType = Xml.XmlNodeType.Element Then
            ' Advance the reader so that we can read the value
            blnSuccess = objXMLReader.Read()
        Else
            blnSuccess = True
        End If

        If blnSuccess AndAlso Not objXMLReader.NodeType = Xml.XmlNodeType.Whitespace And objXMLReader.HasValue Then
            strValue = objXMLReader.Value
        End If

        Return strValue
    End Function

    Private Sub XMLTextReaderSkipWhitespace(ByRef objXMLReader As System.Xml.XmlTextReader)
        If objXMLReader.NodeType = Xml.XmlNodeType.Whitespace Then
            ' Whitspace; read the next node
            objXMLReader.Read()
        End If
    End Sub

End Class
