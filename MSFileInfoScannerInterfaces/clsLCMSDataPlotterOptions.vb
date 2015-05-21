''' <summary>
''' Options class for clsLCMSDatPlotter
''' </summary>
''' <remarks></remarks>
Public Class clsLCMSDataPlotterOptions

    Public Const DEFAULT_MAX_POINTS_TO_PLOT As Integer = 200000
    Public Const DEFAULT_MIN_POINTS_PER_SPECTRUM As Integer = 2

    Public Const DEFAULT_LCMS2D_OVERVIEW_PLOT_DIVISOR As Integer = 10

    Public Const DEFAULT_MZ_RESOLUTION As Single = 0.4
    Public Const DEFAULT_MIN_INTENSITY As Single = 0

    Protected Const DEFAULT_MS1_PLOT_TITLE As String = "MS Spectra"
    Protected Const DEFAULT_MS2_PLOT_TITLE As String = "MS2 Spectra"

    Public Const DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT As Double = 12000
    Public Const DEFAULT_MAX_MONO_MASS_FOR_ZOOMED_DEISOTOPED_PLOT As Double = 4000

    Protected mMaxPointsToPlot As Integer
    Protected mMinPointsPerSpectrum As Integer

    Protected mMZResolution As Single
    Protected mMinIntensity As Single

    Protected mMS1PlotTitle As String
    Protected mMS2PlotTitle As String

    ' The following is only used when PlottingDeisotopedData is true
    Protected mMaxMonoMass As Double

    Public Property MaxPointsToPlot() As Integer
        Get
            Return mMaxPointsToPlot
        End Get
        Set(ByVal value As Integer)
            If value < 10 Then value = 10
            mMaxPointsToPlot = value
        End Set
    End Property

    Public Property MinPointsPerSpectrum() As Integer
        Get
            Return mMinPointsPerSpectrum
        End Get
        Set(ByVal value As Integer)
            If value < 0 Then value = 0
            mMinPointsPerSpectrum = value
        End Set
    End Property

    Public Property MS1PlotTitle() As String
        Get
            Return mMS1PlotTitle
        End Get
        Set(value As String)
            If String.IsNullOrEmpty(value) Then
                value = DEFAULT_MS1_PLOT_TITLE
            End If
            mMS1PlotTitle = value
        End Set
    End Property

    Public Property MS2PlotTitle() As String
        Get
            Return mMS2PlotTitle
        End Get
        Set(value As String)
            If String.IsNullOrEmpty(value) Then
                value = DEFAULT_MS2_PLOT_TITLE
            End If
            mMS2PlotTitle = value
        End Set
    End Property

    Public Property MZResolution() As Single
        Get
            Return mMZResolution
        End Get
        Set(ByVal value As Single)
            If value < 0 Then value = 0
            mMZResolution = value
        End Set
    End Property

    Public Property MinIntensity() As Single
        Get
            Return mMinIntensity
        End Get
        Set(ByVal value As Single)
            If value < 0 Then value = 0
            mMinIntensity = value
        End Set
    End Property

    Public Property MaxMonoMassForDeisotopedPlot() As Double
        Get
            Return mMaxMonoMass
        End Get
        Set(value As Double)
            If value < 100 Then value = 100
            mMaxMonoMass = value
        End Set
    End Property

    Public Property PlottingDeisotopedData() As Boolean

    ''' <summary>
    ''' Set to True to print out a series of 2D plots, each using a different color scheme
    ''' </summary>
    Public Property TestGradientColorSchemes As Boolean     

    Public Property UseObservedMinScan() As Boolean


    Public Function Clone() As clsLCMSDataPlotterOptions
        Dim objClone As New clsLCMSDataPlotterOptions

        With objClone
            .MaxPointsToPlot = MaxPointsToPlot
            .MinPointsPerSpectrum = MinPointsPerSpectrum

            .MZResolution = MZResolution
            .MinIntensity = MinIntensity

            .MS1PlotTitle = MS1PlotTitle
            .MS2PlotTitle = MS2PlotTitle

            .PlottingDeisotopedData = PlottingDeisotopedData
            .UseObservedMinScan = UseObservedMinScan

            .MaxMonoMassForDeisotopedPlot = MaxMonoMassForDeisotopedPlot
        End With

        Return objClone

    End Function

    Public Sub New()
        mMaxPointsToPlot = DEFAULT_MAX_POINTS_TO_PLOT
        mMinPointsPerSpectrum = DEFAULT_MIN_POINTS_PER_SPECTRUM

        mMZResolution = DEFAULT_MZ_RESOLUTION
        mMinIntensity = DEFAULT_MIN_INTENSITY

        mMS1PlotTitle = DEFAULT_MS1_PLOT_TITLE
        mMS2PlotTitle = DEFAULT_MS2_PLOT_TITLE

        mMaxMonoMass = DEFAULT_MAX_MONO_MASS_FOR_DEISOTOPED_PLOT

        PlottingDeisotopedData = False
        UseObservedMinScan = False
    End Sub


End Class
