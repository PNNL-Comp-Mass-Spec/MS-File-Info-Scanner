Option Strict On

' This class computes aggregate stats for a dataset
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started May 7, 2009
' Ported from clsMASICScanStatsParser to clsDatasetStatsSummarizer in February 2010
'
' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://omics.pnl.gov
' -------------------------------------------------------------------------------
' 
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute, 
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
' Department of Energy (DOE).  All rights in the computer software are reserved 
' by DOE on behalf of the United States Government and the Contractor as 
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
' SOFTWARE.  This notice including this sentence must appear on any copies of 
' this computer software.

Namespace DSSummarizer

    Public Class clsDatasetStatsSummarizer

        Public Sub New()
            mFileDate = "February 1, 2010"
            InitializeLocalVariables()
        End Sub

#Region "Constants and Enums"
        Public Const SCANTYPE_STATS_SEPCHAR As String = ":::"
#End Region

#Region "Structures"

        Public Structure udtDatasetFileInfoType
            Public FileSystemCreationTime As DateTime
            Public FileSystemModificationTime As DateTime
            Public DatasetID As Integer
            Public DatasetName As String
            Public FileExtension As String
            Public AcqTimeStart As DateTime
            Public AcqTimeEnd As DateTime
            Public ScanCount As Integer
            Public FileSizeBytes As Long

            Public Sub Clear()
                FileSystemCreationTime = System.DateTime.MinValue
                FileSystemModificationTime = System.DateTime.MinValue
                DatasetID = 0
                DatasetName = String.Empty
                FileExtension = String.Empty
                AcqTimeStart = System.DateTime.MinValue
                AcqTimeEnd = System.DateTime.MinValue
                ScanCount = 0
                FileSizeBytes = 0
            End Sub
        End Structure

#End Region

#Region "Classwide Variables"
        Protected mFileDate As String
        Protected mDatasetStatsSummaryFileName As String
        Protected mErrorMessage As String = String.Empty
#End Region

#Region "Properties"

        Public Property DatasetStatsSummaryFileName() As String
            Get
                Return mDatasetStatsSummaryFileName
            End Get
            Set(ByVal value As String)
                If Not value Is Nothing Then
                    mDatasetStatsSummaryFileName = value
                End If
            End Set
        End Property

        Public ReadOnly Property ErrorMessage() As String
            Get
                Return mErrorMessage
            End Get
        End Property

        Public ReadOnly Property FileDate() As String
            Get
                FileDate = mFileDate
            End Get
        End Property

#End Region

        ''' <summary>
        ''' Summarizes the scan info in objScanStats()
        ''' </summary>
        ''' <param name="objScanStats">ScanStats data to parse</param>
        ''' <param name="objSummaryStats">Stats output</param>
        ''' <returns>>True if success, false if error</returns>
        ''' <remarks></remarks>
        Public Function ComputeScanStatsSummary(ByRef objScanStats As System.Collections.Generic.List(Of clsScanStatsEntry), _
                                                ByRef objSummaryStats As clsDatasetSummaryStats) As Boolean

            Dim intScanStatsCount As Integer
            Dim objEntry As clsScanStatsEntry

            Dim strScanTypeKey As String

            Dim blnSuccess As Boolean = False

            Dim dblTICListMS() As Double
            Dim intTICListMSCount As Integer = 0

            Dim dblTICListMSn() As Double
            Dim intTICListMSnCount As Integer = 0

            Dim dblBPIListMS() As Double
            Dim intBPIListMSCount As Integer = 0

            Dim dblBPIListMSn() As Double
            Dim intBPIListMSnCount As Integer = 0

            Try
                
                If objScanStats Is Nothing Then
                    mErrorMessage = "objScanStats is Nothing; unable to continue"
                    Return False
                Else
                    mErrorMessage = ""
                End If

                intScanStatsCount = objScanStats.Count

                ' Initialize objSummaryStats
                If objSummaryStats Is Nothing Then
                    objSummaryStats = New clsDatasetSummaryStats
                Else
                    objSummaryStats.Clear()
                End If

                ' Initialize the TIC and BPI List arrays
                ReDim dblTICListMS(intScanStatsCount - 1)
                ReDim dblBPIListMS(intScanStatsCount - 1)

                ReDim dblTICListMSn(intScanStatsCount - 1)
                ReDim dblBPIListMSn(intScanStatsCount - 1)

                For Each objEntry In objScanStats

                    If objEntry.ScanType > 1 Then
                        ' MSn spectrum
                        ComputeScanStatsUpdateDetails(objEntry, objSummaryStats.ElutionTimeMax, _
                                                      objSummaryStats.MSnStats, _
                                                      dblTICListMSn, intTICListMSnCount, _
                                                      dblBPIListMSn, intBPIListMSnCount)
                    Else
                        ' MS spectrum
                        ComputeScanStatsUpdateDetails(objEntry, objSummaryStats.ElutionTimeMax, _
                                                      objSummaryStats.MSStats, _
                                                      dblTICListMS, intTICListMSCount, _
                                                      dblBPIListMS, intBPIListMSCount)
                    End If

                    strScanTypeKey = objEntry.ScanTypeName & SCANTYPE_STATS_SEPCHAR & objEntry.ScanFilterText
                    If objSummaryStats.objScanTypeStats.ContainsKey(strScanTypeKey) Then
                        objSummaryStats.objScanTypeStats.Item(strScanTypeKey) += 1
                    Else
                        objSummaryStats.objScanTypeStats.Add(strScanTypeKey, 1)
                    End If
                Next

                objSummaryStats.MSStats.TICMedian = ComputeMedian(dblTICListMS, intTICListMSCount)
                objSummaryStats.MSStats.BPIMedian = ComputeMedian(dblBPIListMS, intBPIListMSCount)

                objSummaryStats.MSnStats.TICMedian = ComputeMedian(dblTICListMSn, intTICListMSnCount)
                objSummaryStats.MSnStats.BPIMedian = ComputeMedian(dblBPIListMSn, intBPIListMSnCount)

                blnSuccess = True

            Catch ex As System.Exception
                mErrorMessage = "Error in ComputeScanStatsSummary: " & ex.Message
            End Try

            Return blnSuccess

        End Function

        Protected Sub ComputeScanStatsUpdateDetails(ByRef objScanStats As clsScanStatsEntry, _
                                                    ByRef dblElutionTimeMax As Double, _
                                                    ByRef udtSummaryStatDetails As clsDatasetSummaryStats.udtSummaryStatDetailsType, _
                                                    ByRef dblTICList() As Double, _
                                                    ByRef intTICListCount As Integer, _
                                                    ByRef dblBPIList() As Double, _
                                                    ByRef intBPIListCount As Integer)

            Dim dblElutionTime As Double
            Dim dblTIC As Double
            Dim dblBPI As Double

            If objScanStats.ElutionTime <> Nothing AndAlso objScanStats.ElutionTime.Length > 0 Then
                If Double.TryParse(objScanStats.ElutionTime, dblElutionTime) Then
                    If dblElutionTime > dblElutionTimeMax Then
                        dblElutionTimeMax = dblElutionTime
                    End If
                End If
            End If

            If Double.TryParse(objScanStats.TotalIonIntensity, dblTIC) Then
                If dblTIC > udtSummaryStatDetails.TICMax Then
                    udtSummaryStatDetails.TICMax = dblTIC
                End If

                dblTICList(intTICListCount) = dblTIC
                intTICListCount += 1
            End If

            If Double.TryParse(objScanStats.BasePeakIntensity, dblBPI) Then
                If dblBPI > udtSummaryStatDetails.BPIMax Then
                    udtSummaryStatDetails.BPIMax = dblBPI
                End If

                dblBPIList(intBPIListCount) = dblBPI
                intBPIListCount += 1
            End If

            udtSummaryStatDetails.ScanCount += 1

        End Sub

        Protected Function ComputeMedian(ByRef dblList() As Double, ByVal intItemCount As Integer) As Double

            Dim intMidpointIndex As Integer
            Dim blnAverage As Boolean

            If dblList Is Nothing OrElse dblList.Length < 1 OrElse intItemCount < 1 Then
                ' List is empty (or intItemCount = 0)
                Return 0
            ElseIf intItemCount <= 1 Then
                ' Only 1 item; the median is the value
                Return dblList(0)
            Else
                ' Sort dblList ascending, then find the midpoint
                Array.Sort(dblList, 0, intItemCount)

                If intItemCount Mod 2 = 0 Then
                    ' Even number
                    intMidpointIndex = CInt(Math.Floor(intItemCount / 2)) - 1
                    blnAverage = True
                Else
                    ' Odd number
                    intMidpointIndex = CInt(Math.Floor(intItemCount / 2))
                End If

                If intMidpointIndex > intItemCount Then intMidpointIndex = intItemCount - 1
                If intMidpointIndex < 0 Then intMidpointIndex = 0

                If blnAverage Then
                    ' Even number of items
                    ' Return the average of the two middle points
                    Return (dblList(intMidpointIndex) + dblList(intMidpointIndex + 1)) / 2
                Else
                    ' Odd number of items
                    Return dblList(intMidpointIndex)
                End If

                Return dblList(intMidpointIndex)
            End If

        End Function

        Public Function CreateDatasetInfoFile(ByVal strDatasetName As String, _
                                              ByVal strDatasetInfoFilePath As String, _
                                              ByRef objScanStats As System.Collections.Generic.List(Of clsScanStatsEntry), _
                                              ByRef udtDatasetFileInfo As udtDatasetFileInfoType) As Boolean

            Dim objDSInfo As System.Xml.XmlTextWriter
            Dim objEnum As System.Collections.Generic.Dictionary(Of String, Integer).Enumerator

            Dim objMASICScanStatsParser As DSSummarizer.clsDatasetStatsSummarizer
            Dim objSummaryStats As DSSummarizer.clsDatasetSummaryStats

            Dim intIndexMatch As Integer
            Dim strScanType As String
            Dim strScanFilterText As String

            Dim blnSuccess As Boolean

            Try
                If objScanStats Is Nothing Then
                    mErrorMessage = "objScanStats is Nothing; unable to continue"
                    Return False
                Else
                    mErrorMessage = ""
                End If

                objMASICScanStatsParser = New DSSummarizer.clsDatasetStatsSummarizer()
                objSummaryStats = New DSSummarizer.clsDatasetSummaryStats

                ' Parse the data in mScanStats to compute the bulk values
                objMASICScanStatsParser.ComputeScanStatsSummary(objScanStats, objSummaryStats)

                objDSInfo = New System.Xml.XmlTextWriter(strDatasetInfoFilePath, System.Text.Encoding.UTF8)
                objDSInfo.Formatting = System.Xml.Formatting.Indented
                objDSInfo.Indentation = 2

                objDSInfo.WriteStartDocument(True)

                'Write the beginning of the "Root" element.
                objDSInfo.WriteStartElement("DatasetInfo")

                objDSInfo.WriteElementString("Dataset", strDatasetName)

                objDSInfo.WriteStartElement("ScanTypes")

                objEnum = objSummaryStats.objScanTypeStats.GetEnumerator
                Do While objEnum.MoveNext

                    strScanType = objEnum.Current.Key
                    intIndexMatch = strScanType.IndexOf(clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR)

                    If intIndexMatch >= 0 Then
                        strScanFilterText = strScanType.Substring(intIndexMatch + clsDatasetStatsSummarizer.SCANTYPE_STATS_SEPCHAR.Length)
                        If intIndexMatch > 0 Then
                            strScanType = strScanType.Substring(0, intIndexMatch)
                        Else
                            strScanType = String.Empty
                        End If
                    Else
                        strScanFilterText = String.Empty
                    End If

                    objDSInfo.WriteStartElement("ScanType")
                    objDSInfo.WriteAttributeString("ScanCount", objEnum.Current.Value.ToString)
                    objDSInfo.WriteAttributeString("ScanFilterText", strScanFilterText)
                    objDSInfo.WriteString(strScanType)
                    objDSInfo.WriteEndElement()     ' ScanType
                Loop

                objDSInfo.WriteEndElement()       ' ScanTypes

                objDSInfo.WriteStartElement("AcquisitionInfo")

                objDSInfo.WriteElementString("ScanCount", (objSummaryStats.MSStats.ScanCount + objSummaryStats.MSnStats.ScanCount).ToString)
                objDSInfo.WriteElementString("ScanCountMS", objSummaryStats.MSStats.ScanCount.ToString)
                objDSInfo.WriteElementString("ScanCountMSn", objSummaryStats.MSnStats.ScanCount.ToString)
                objDSInfo.WriteElementString("Elution_Time_Max", objSummaryStats.ElutionTimeMax.ToString)

                objDSInfo.WriteElementString("AcqTimeMinutes", udtDatasetFileInfo.AcqTimeEnd.Subtract(udtDatasetFileInfo.AcqTimeStart).TotalMinutes.ToString("0.00"))
                objDSInfo.WriteElementString("StartTime", udtDatasetFileInfo.AcqTimeStart.ToString("yyyy-MM-dd hh:mm:ss tt"))
                objDSInfo.WriteElementString("EndTime", udtDatasetFileInfo.AcqTimeEnd.ToString("yyyy-MM-dd hh:mm:ss tt"))

                objDSInfo.WriteElementString("FileSizeBytes", udtDatasetFileInfo.FileSizeBytes.ToString)
                objDSInfo.WriteEndElement()       ' AcquisitionInfo

                objDSInfo.WriteStartElement("TICInfo")
                objDSInfo.WriteElementString("TIC_Max_MS", ValueToString(objSummaryStats.MSStats.TICMax, 5))
                objDSInfo.WriteElementString("TIC_Max_MSn", ValueToString(objSummaryStats.MSnStats.TICMax, 5))
                objDSInfo.WriteElementString("BPI_Max_MS", ValueToString(objSummaryStats.MSStats.BPIMax, 5))
                objDSInfo.WriteElementString("BPI_Max_MSn", ValueToString(objSummaryStats.MSnStats.BPIMax, 5))
                objDSInfo.WriteElementString("TIC_Median_MS", ValueToString(objSummaryStats.MSStats.TICMedian, 5))
                objDSInfo.WriteElementString("TIC_Median_MSn", ValueToString(objSummaryStats.MSnStats.TICMedian, 5))
                objDSInfo.WriteElementString("BPI_Median_MS", ValueToString(objSummaryStats.MSStats.BPIMedian, 5))
                objDSInfo.WriteElementString("BPI_Median_MSn", ValueToString(objSummaryStats.MSnStats.BPIMedian, 5))
                objDSInfo.WriteEndElement()       ' TICInfo

                objDSInfo.WriteEndElement()  'End the "Root" element (DatasetInfo)
                objDSInfo.WriteEndDocument() 'End the document

                objDSInfo.Close()
                objDSInfo = Nothing

                blnSuccess = True

            Catch ex As System.Exception
                blnSuccess = False
                mErrorMessage = "Error in CreateDatasetInfoFile: " & ex.Message
            End Try

            Return blnSuccess

        End Function

        Private Sub InitializeLocalVariables()
            mErrorMessage = String.Empty
        End Sub

        Public Shared Function ValueToString(ByVal sngValue As Single, ByVal intDigitsOfPrecision As Integer, Optional ByVal sngScientificNotationThreshold As Single = 1000000) As String
            Return ValueToString(CDbl(sngValue), intDigitsOfPrecision, CDbl(sngScientificNotationThreshold))
        End Function

        Public Shared Function ValueToString(ByVal dblValue As Double, ByVal intDigitsOfPrecision As Integer, Optional ByVal dblScientificNotationThreshold As Double = 1000000) As String
            Dim strFormatString As String
            Dim strValue As String
            Dim strMantissa As String

            Dim dblNewValue As Double

            Dim intDigitsAfterDecimal As Integer

            If intDigitsOfPrecision < 1 Then intDigitsOfPrecision = 1

            Try
                strMantissa = "0." & New String("0"c, Math.Max(intDigitsOfPrecision - 1, 1)) & "E+00"

                If dblValue = 0 Then
                    strValue = "0"
                ElseIf Math.Abs(dblValue) < 1 Then
                    strFormatString = "0." & New String("0"c, intDigitsOfPrecision)
                    strValue = dblValue.ToString(strFormatString)
                    dblNewValue = Double.Parse(strValue)

                    If dblNewValue = 0 Then
                        strValue = dblValue.ToString(strMantissa)
                    Else
                        strValue = strValue.TrimEnd("0"c)
                    End If
                Else
                    intDigitsAfterDecimal = intDigitsOfPrecision - CInt(Math.Ceiling(Math.Log10(Math.Abs(dblValue))))

                    If dblValue >= dblScientificNotationThreshold Then
                        strValue = dblValue.ToString(strMantissa)
                    Else
                        If intDigitsAfterDecimal > 0 Then
                            strValue = dblValue.ToString("0." & New String("0"c, intDigitsAfterDecimal))
                            strValue = strValue.TrimEnd("0"c)
                            strValue = strValue.TrimEnd("."c)
                        Else
                            strValue = dblValue.ToString("0")
                        End If
                    End If
                End If

                Return strValue

            Catch ex As System.Exception
                Console.WriteLine("Error in clsDatasetStatsSummarizer->ValueToString: " & ex.Message)
                Return dblValue.ToString
            End Try

        End Function

    End Class

    Public Class clsScanStatsEntry
        Public ScanNumber As Integer
        Public ScanType As Integer              ' 1 for MS, 2 for MS2, 3 for MS3

        Public ScanFilterText As String        ' Example values: "FTMS + p NSI Full ms [400.00-2000.00]" or "ITMS + c ESI Full ms [300.00-2000.00]" or "ITMS + p ESI d Z ms [1108.00-1118.00]" or "ITMS + c ESI d Full ms2 342.90@cid35.00"
        Public ScanTypeName As String          ' Example values: MS, HMS, Zoom, CID-MSn, or PQD-MSn

        ' The following are strings to prevent the number formatting from changing
        Public ElutionTime As String
        Public TotalIonIntensity As String
        Public BasePeakIntensity As String
        Public BasePeakMZ As String
        Public BasePeakSignalToNoiseRatio As String

        Public IonCount As Integer
        Public IonCountRaw As Integer

        Public Sub Clear()
            ScanNumber = 0
            ScanType = 0

            ScanFilterText = String.Empty
            ScanTypeName = String.Empty

            ElutionTime = "0"
            TotalIonIntensity = "0"
            BasePeakIntensity = "0"
            BasePeakMZ = "0"
            BasePeakSignalToNoiseRatio = "0"

            IonCount = 0
            IonCountRaw = 0
        End Sub

        Public Sub New()
            Me.Clear()
        End Sub
    End Class

    Public Class clsDatasetSummaryStats

        Public ElutionTimeMax As Double
        Public MSStats As udtSummaryStatDetailsType
        Public MSnStats As udtSummaryStatDetailsType

        ' The following collection keeps track of each ScanType in the dataset, along with the number of scans of this type
        ' Example scan types:  FTMS + p NSI Full ms" or "ITMS + c ESI Full ms" or "ITMS + p ESI d Z ms" or "ITMS + c ESI d Full ms2 @cid35.00"
        Public objScanTypeStats As System.Collections.Generic.Dictionary(Of String, Integer)

        Public Structure udtSummaryStatDetailsType
            Public ScanCount As Integer
            Public TICMax As Double
            Public BPIMax As Double
            Public TICMedian As Double
            Public BPIMedian As Double
        End Structure

        Public Sub Clear()

            ElutionTimeMax = 0

            With MSStats
                .ScanCount = 0
                .TICMax = 0
                .BPIMax = 0
                .TICMedian = 0
                .BPIMedian = 0
            End With

            With MSnStats
                .ScanCount = 0
                .TICMax = 0
                .BPIMax = 0
                .TICMedian = 0
                .BPIMedian = 0
            End With

            If objScanTypeStats Is Nothing Then
                objScanTypeStats = New System.Collections.Generic.Dictionary(Of String, Integer)
            Else
                objScanTypeStats.Clear()
            End If

        End Sub

        Public Sub New()
            Me.Clear()
        End Sub
    End Class

End Namespace
