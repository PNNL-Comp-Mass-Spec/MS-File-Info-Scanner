Option Strict On

<CLSCompliant(True)>
Public Class clsSpectrumTypeClassifier

#Region "Constants and Enums"
    Public Const DEFAULT_PPM_DIFF_THRESHOLD As Integer = 50

    Public Enum eCentroidStatusConstants
        Unknown = 0
        Profile = 1
        Centroid = 2
    End Enum

#End Region
	

#Region "Module Wide Variables"
	Protected mErrorMessage As String
	Protected mPpmDiffThreshold As Integer

	' The following dictionaries keep track of the number of spectra at each MSLevel (1 for MS1, 2 for MS2, etc.)

    ''' <summary>
    ''' Centroided spectra
    ''' </summary>
    ''' <remarks></remarks>
    Protected mCentroidedSpectra As Dictionary(Of Integer, Integer)

    ''' <summary>
    ''' Centroided spectra that were mis-classified as profile mode
    ''' </summary>
    ''' <remarks></remarks>
    Protected mCentroidedSpectraClassifiedAsProfile As Dictionary(Of Integer, Integer)

    ''' <summary>
    ''' All spectra
    ''' </summary>
    ''' <remarks></remarks>
	Protected mTotalSpectra As Dictionary(Of Integer, Integer)

	Protected ReadOnly mMedianUtils As clsMedianUtilities
#End Region

#Region "Events"
	Public Event ReadingSpectra(ByVal spectraProcessed As Integer)
	Public Event ProcessingComplete(ByVal spectraProcessed As Integer)

	Public Event ErrorEvent(ByVal Message As String)
#End Region

#Region "Properties"

	Public ReadOnly Property ErrorMessage As String
		Get
			If String.IsNullOrWhiteSpace(mErrorMessage) Then Return String.Empty
			Return mErrorMessage
		End Get
	End Property

	Public Property PpmDiffThreshold As Integer
		Get
			Return mPpmDiffThreshold
		End Get
		Set(value As Integer)
			mPpmDiffThreshold = value
		End Set
	End Property

#End Region

	Public Sub New()
		mMedianUtils = New clsMedianUtilities()

        mCentroidedSpectra = New Dictionary(Of Integer, Integer)
        mCentroidedSpectraClassifiedAsProfile = New Dictionary(Of Integer, Integer)
		mTotalSpectra = New Dictionary(Of Integer, Integer)

		mPpmDiffThreshold = DEFAULT_PPM_DIFF_THRESHOLD
		Reset()

	End Sub

	Public Function CentroidedSpectra() As Integer
		Return mCentroidedSpectra.Sum(Function(item) item.Value)
	End Function

    Public Function CentroidedMS1Spectra() As Integer
        Return (From item In mCentroidedSpectra Where item.Key <= 1).Sum(Function(item) item.Value)
    End Function

    Public Function CentroidedMSnSpectra() As Integer
        Return (From item In mCentroidedSpectra Where item.Key > 1).Sum(Function(item) item.Value)
    End Function

    Public Function CentroidedMS1SpectraClassifiedAsProfile() As Integer
        Return (From item In mCentroidedSpectraClassifiedAsProfile Where item.Key <= 1).Sum(Function(item) item.Value)
    End Function

    Public Function CentroidedMSnSpectraClassifiedAsProfile() As Integer
        Return (From item In mCentroidedSpectraClassifiedAsProfile Where item.Key > 1).Sum(Function(item) item.Value)
    End Function

	Public Function FractionCentroided() As Double

        Dim total = TotalSpectra()
		If total = 0 Then
			Return 0
		Else
			Return CentroidedSpectra() / CDbl(total)
		End If

	End Function

    Public Function FractionCentroidedMSn() As Double

        Dim total = TotalMSnSpectra()
        If total = 0 Then
            Return 0
        Else
            Return CentroidedMSnSpectra() / CDbl(total)
        End If

    End Function

	Public Function TotalSpectra() As Integer
		Return mTotalSpectra.Sum(Function(item) item.Value)
	End Function

    Public Function TotalMS1Spectra() As Integer
        Return (From item In mTotalSpectra Where item.Key <= 1).Sum(Function(item) item.Value)
    End Function

    Public Function TotalMSnSpectra() As Integer
        Return (From item In mTotalSpectra Where item.Key > 1).Sum(Function(item) item.Value)
    End Function


	''' <summary>
	''' Examine the spectra in a _DTA.txt file to determine the number of centroided spectra
	''' </summary>
	''' <param name="strCDTAPath"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Public Function CheckCDTAFile(ByVal strCDTAPath As String) As Boolean

		Dim dtLastStatusTime = DateTime.UtcNow()

		Dim splitChars = New Char() {" "c}

		Reset()

		Try

			If Not File.Exists(strCDTAPath) Then
				Throw New FileNotFoundException("CDTA file not found: " & strCDTAPath)
			End If

			' Read the m/z values in the _dta.txt file
			' Using a simple text reader here for speed purposes

			Using srDtaFile = New StreamReader(New FileStream(strCDTAPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))

				Dim lstPpmDiffs = New List(Of Double)(2000)

				Dim dblPreviousMZ As Double = 0

				While srDtaFile.Peek > -1
					Dim strLineIn = srDtaFile.ReadLine

					If Not String.IsNullOrEmpty(strLineIn) Then
						If strLineIn.StartsWith("=============") Then
							' DTA header line

							' Process the data for the previous spectrum
                            CheckPPMDiffs(lstPpmDiffs, 2, eCentroidStatusConstants.Unknown)

							' Reset the previous m/z value and skip the next line
							If srDtaFile.Peek > -1 Then srDtaFile.ReadLine()
							dblPreviousMZ = 0
							lstPpmDiffs.Clear()

							If DateTime.UtcNow.Subtract(dtLastStatusTime).TotalSeconds >= 30 Then
								RaiseEvent ReadingSpectra(TotalSpectra())
								dtLastStatusTime = DateTime.UtcNow
							End If

						Else
							Dim dataColumns = strLineIn.Split(splitChars, 3)

							Dim dblMZ As Double
							If Double.TryParse(dataColumns(0), dblMZ) Then
								If dblPreviousMZ > 0 AndAlso dblMZ > dblPreviousMZ Then
									Dim delMPPM = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ
									lstPpmDiffs.Add(delMPPM)
								End If
								dblPreviousMZ = dblMZ
							End If
						End If

					End If

				End While

				' Process the data for the previous spectrum
                CheckPPMDiffs(lstPpmDiffs, 2, eCentroidStatusConstants.Unknown)

			End Using

			RaiseEvent ProcessingComplete(TotalSpectra())

			Return True

		Catch ex As Exception
			mErrorMessage = "Exception in CheckCDTAFile: " & ex.Message
			RaiseEvent ErrorEvent(mErrorMessage)
			Return False
		End Try

	End Function

	''' <summary>
	''' Examines lstPpmDiffs to determine if the data is centroided data
	''' </summary>
	''' <param name="lstPpmDiffs"></param>
	''' <param name="msLevel">1 for MS1, 2 for MS2, etc.</param>
	''' <remarks>Increments class property TotalSpectra if lstPpmDiffs is not empty; increments class property CentroidedSpectra if the data is centroided</remarks>
    Protected Sub CheckPPMDiffs(
      ByVal lstPpmDiffs As List(Of Double),
      ByVal msLevel As Integer,
      ByVal centroidingStatus As eCentroidStatusConstants)

        If lstPpmDiffs.Count > 0 Then
            IncrementDictionaryByMSLevel(mTotalSpectra, msLevel)

            Dim empiricalCentroidStatus = eCentroidStatusConstants.Profile
            If IsDataCentroided(lstPpmDiffs) Then
                empiricalCentroidStatus = eCentroidStatusConstants.Centroid
            End If

            If centroidingStatus = eCentroidStatusConstants.Centroid And empiricalCentroidStatus = eCentroidStatusConstants.Profile Then
                ' The empirical algorithm has classified a centroid spectrum as profile
                ' Change it back to centroid
                empiricalCentroidStatus = eCentroidStatusConstants.Centroid

                IncrementDictionaryByMSLevel(mCentroidedSpectraClassifiedAsProfile, msLevel)
            End If

            If empiricalCentroidStatus = eCentroidStatusConstants.Centroid Then
                IncrementDictionaryByMSLevel(mCentroidedSpectra, msLevel)
            End If
        End If

    End Sub

	Private Sub IncrementDictionaryByMSLevel(ByRef dctSpectrumCounts As Dictionary(Of Integer, Integer), ByVal msLevel As Integer)

		Dim spectraCount As Integer

		If dctSpectrumCounts.TryGetValue(msLevel, spectraCount) Then
			dctSpectrumCounts(msLevel) = spectraCount + 1
		Else
			dctSpectrumCounts(msLevel) = 1
        End If

	End Sub

	Public Sub CheckSpectrum(ByVal lstMZs As List(Of Double), ByVal msLevel As Integer)
		CheckSpectrum(lstMZs, msLevel, assumeSorted:=False)
	End Sub

    Public Sub CheckSpectrum(ByVal lstMZs As List(Of Double), ByVal msLevel As Integer, ByVal centroidingStatus As eCentroidStatusConstants)
        CheckSpectrum(lstMZs, msLevel, assumeSorted:=False, centroidingStatus:=centroidingStatus)
    End Sub

    Public Sub CheckSpectrum(
      ByVal lstMZs As List(Of Double),
      ByVal msLevel As Integer,
      ByVal assumeSorted As Boolean,
      Optional ByVal centroidingStatus As eCentroidStatusConstants = eCentroidStatusConstants.Unknown)

        If Not assumeSorted Then
            ' Check whether sorting is required
            For i As Integer = 1 To lstMZs.Count - 1
                If lstMZs(i) < lstMZs(i - 1) Then
                    lstMZs.Sort()
                    Exit For
                End If
            Next
        End If

        Dim lstPpmDiffs = New List(Of Double)(lstMZs.Count)

        For i As Integer = 1 To lstMZs.Count - 1
            Dim dblMZ = lstMZs(i)
            Dim dblPreviousMZ = lstMZs(i - 1)

            If dblPreviousMZ > 0 AndAlso dblMZ > dblPreviousMZ Then
                Dim delMppm = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ
                lstPpmDiffs.Add(delMppm)
            End If
        Next

        CheckPPMDiffs(lstPpmDiffs, msLevel, centroidingStatus)

    End Sub

	''' <summary>
	''' Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
	''' </summary>
	''' <param name="dblMZs"></param>
	''' <param name="msLevel"></param>
	''' <remarks>Assumes the ions are sorted</remarks>
	Public Sub CheckSpectrum(ByVal dblMZs As Double(), ByVal msLevel As Integer)
		CheckSpectrum(dblMZs.Count, dblMZs, msLevel)
	End Sub

	''' <summary>
	''' Step through the MZ values in array dblMZs and compute the ppm-based mass difference between adjacent points
	''' </summary>
	''' <param name="ionCount">Number of items in dblMZs; if -1, then parses all data in dblMZs</param>
	''' <param name="dblMZs"></param>
	''' <param name="msLevel"></param>
	''' <remarks>Assumes the ions are sorted</remarks>
    Public Sub CheckSpectrum(
      ByVal ionCount As Integer,
      ByVal dblMZs As Double(),
      ByVal msLevel As Integer,
      Optional ByVal centroidingStatus As eCentroidStatusConstants = eCentroidStatusConstants.Unknown)

        If ionCount < 0 Then
            ionCount = dblMZs.Count
        End If

        ' Possibly sort dblMZs
        For i As Integer = 1 To ionCount - 1
            If dblMZs(i) < dblMZs(i - 1) Then
                ' Sort required
                Array.Sort(dblMZs)
                Exit For
            End If
        Next

        Dim lstPpmDiffs = New List(Of Double)(ionCount)

        For i As Integer = 1 To ionCount - 1
            Dim dblMZ = dblMZs(i)
            Dim dblPreviousMZ = dblMZs(i - 1)

            If dblPreviousMZ > 0 AndAlso dblMZ > dblPreviousMZ Then
                Dim delMppm = 1000000.0 * (dblMZ - dblPreviousMZ) / dblMZ
                lstPpmDiffs.Add(delMppm)
            End If
        Next

        CheckPPMDiffs(lstPpmDiffs, msLevel, centroidingStatus)
    End Sub

	Public Sub Reset()
		mErrorMessage = String.Empty
		mTotalSpectra.Clear()
        mCentroidedSpectra.Clear()
        mCentroidedSpectraClassifiedAsProfile.Clear()
	End Sub

	''' <summary>
	''' Computes the median of the ppm m/z difference values in lstPpmDiffs
	''' </summary>
	''' <param name="lstPpmDiffs">List of mass difference values between adjacent data points, converted to ppm</param>
	''' <returns>True if the median is at least as large as PpmDiffThreshold</returns>
	''' <remarks></remarks>
	Public Function IsDataCentroided(ByVal lstPpmDiffs As IList(Of Double)) As Boolean

		Dim medianDelMppm = mMedianUtils.Median(lstPpmDiffs)

		If medianDelMppm < PpmDiffThreshold Then
			' Profile mode data
			Return False
		Else
			' Centroided data
			Return True
		End If

	End Function

End Class
