Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2007, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified December 19, 2007

Public MustInherit Class clsMSFileInfoProcessorBaseClass
    Implements iMSFileInfoProcessor

    Public Sub New()
        InitializeTICandBPI()
    End Sub

#Region "Member variables"
    Protected mCreateTICAndBPI As Boolean
    Protected mComputeOverallQualityScores As Boolean

    Protected mBPI As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType
    Protected mTIC As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType
#End Region

#Region "Properties"
    Public ReadOnly Property BPI() As iMSFileInfoProcessor.udtChromatogramInfoType Implements iMSFileInfoProcessor.BPI
        Get
            Return mBPI
        End Get
    End Property

    Public ReadOnly Property TIC() As iMSFileInfoProcessor.udtChromatogramInfoType Implements iMSFileInfoProcessor.TIC
        Get
            Return mTIC
        End Get
    End Property
#End Region
  
    Public Function GetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions) As Boolean Implements iMSFileInfoProcessor.GetOption
        Select Case eOption
            Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
                Return mCreateTICAndBPI
            Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
                Return mComputeOverallQualityScores
        End Select
    End Function

    Public Sub SetOption(ByVal eOption As iMSFileInfoProcessor.ProcessingOptions, ByVal blnValue As Boolean) Implements iMSFileInfoProcessor.SetOption
        Select Case eOption
            Case iMSFileInfoProcessor.ProcessingOptions.CreateTICAndBPI
                mCreateTICAndBPI = blnValue
            Case iMSFileInfoProcessor.ProcessingOptions.ComputeOverallQualityScores
                mComputeOverallQualityScores = blnValue
        End Select

    End Sub

    Protected Sub AddChromatogramPoint(ByRef udtChromatogram As MSFileInfoScanner.iMSFileInfoProcessor.udtChromatogramInfoType, ByVal intScanNumber As Integer, ByVal dblIntensity As Double, ByVal intMSLevel As Integer)

        With udtChromatogram
            If .ScanCount >= .ScanNum.Length Then
                ReDim Preserve .ScanNum(.ScanNum.Length * 2 - 1)
                ReDim Preserve .ScanIntensity(.ScanNum.Length - 1)
                ReDim Preserve .ScanMSLevel(.ScanNum.Length - 1)
            End If

            .ScanNum(.ScanCount) = intScanNumber
            .ScanIntensity(.ScanCount) = dblIntensity
            .ScanMSLevel(.ScanCount) = intMSLevel

            .ScanCount += 1
        End With
    End Sub

    Protected Sub InitializeTICandBPI()
        ' Initialize mBPI
        mBPI.Initialize()
        mTIC.Initialize()
    End Sub

    Public MustOverride Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean Implements iMSFileInfoProcessor.ProcessDatafile
    Public MustOverride Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String Implements iMSFileInfoProcessor.GetDatasetNameViaPath

End Class
