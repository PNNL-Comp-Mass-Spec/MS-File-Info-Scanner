Option Strict On

' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Copyright 2005, Battelle Memorial Institute.  All Rights Reserved.
'
' Last modified December 09, 2005

Public Class clsAgilentTOFOrQStarWiffFileInfoScanner
    Inherits clsMSFileInfoProcessorBaseClass

    ' Note: The extension must be in all caps
    Public Const AGILENT_TOF_OR_QSTAR_FILE_EXTENSION As String = ".WIFF"

    Protected Enum DeconToolsFileTypeConstants As Integer
        BRUKER = 0
        IONSPEC
        MIDAS
        FINNIGAN
        SUNEXTREL
        AGILENT_TOF
        ICR2LSRAWDATA
        MICROMASSRAWDATA
    End Enum

	Public Overrides Function GetDatasetNameViaPath(ByVal strDataFilePath As String) As String
		' The dataset name is simply the file name without .wiff
		Try
			Return System.IO.Path.GetFileNameWithoutExtension(strDataFilePath)
		Catch ex As System.Exception
			Return String.Empty
		End Try
	End Function

	Public Overrides Function ProcessDatafile(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Returns True if success, False if an error

		Dim ioFileInfo As System.IO.FileInfo
		Dim blnSuccess As Boolean = False

		Dim dblAcquisitionLengthMinutes As Double = 0

		Dim intTotalScanCount As Integer = 0

		' blnSuccess = ProcessDatafileClearCore(strDataFilePath, udtFileInfo)

		If Not blnSuccess Then
			' Obtain the full path to the file
			ioFileInfo = New System.IO.FileInfo(strDataFilePath)

			With udtFileInfo
				.FileSystemCreationTime = ioFileInfo.CreationTime
				.FileSystemModificationTime = ioFileInfo.LastWriteTime

				' Using the file system modification time as the acquisition end time
				' Using an arbitrary, fixed length of 2 minutes for each dataset
				.AcqTimeStart = .FileSystemModificationTime.AddMinutes(-2)
				.AcqTimeEnd = .FileSystemModificationTime

				.DatasetID = 0
				.DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
				.FileExtension = ioFileInfo.Extension
				.FileSizeBytes = ioFileInfo.Length
			End With

			' Copy over the updated filetime info and scan info from udtFileInfo to mDatasetFileInfo
			With mDatasetStatsSummarizer.DatasetFileInfo
				.DatasetName = String.Copy(udtFileInfo.DatasetName)
				.FileExtension = String.Copy(udtFileInfo.FileExtension)
				.AcqTimeStart = udtFileInfo.AcqTimeStart
				.AcqTimeEnd = udtFileInfo.AcqTimeEnd
				.ScanCount = udtFileInfo.ScanCount
			End With

			blnSuccess = True

		End If

		Return blnSuccess

	End Function


	''' <summary>
	''' The following was an attempt to read the data using ClearCore2 DLLs
	''' However, this only works if you have a license key
	''' </summary>
	''' <param name="strDataFilePath"></param>
	''' <param name="udtFileInfo"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Private Function ProcessDatafileClearCore(ByVal strDataFilePath As String, ByRef udtFileInfo As iMSFileInfoProcessor.udtFileInfoType) As Boolean
		' Returns True if success, False if an error

		Dim ioFileInfo As System.IO.FileInfo
		Dim blnSuccess As Boolean

		Dim dblAcquisitionLengthMinutes As Double = 0

		Dim intTotalScanCount As Integer = 0

		' Obtain the full path to the file
		ioFileInfo = New System.IO.FileInfo(strDataFilePath)

		With udtFileInfo
			.FileSystemCreationTime = ioFileInfo.CreationTime
			.FileSystemModificationTime = ioFileInfo.LastWriteTime

			' The acquisition times will get updated below to more accurate values
			.AcqTimeStart = .FileSystemModificationTime
			.AcqTimeEnd = .FileSystemModificationTime

			.DatasetID = 0
			.DatasetName = System.IO.Path.GetFileNameWithoutExtension(ioFileInfo.Name)
			.FileExtension = ioFileInfo.Extension
			.FileSizeBytes = ioFileInfo.Length
		End With

		' Use Clearcore2 DLLs to read the .Wiff file

		' Open a handle to the data file
		'Try
		'	Dim oProvider As Clearcore2.Data.AnalystDataProvider.AnalystWiffDataProvider
		'	Dim oBatch As Clearcore2.Data.DataAccess.SampleData.Batch

		'	oProvider = New Clearcore2.Data.AnalystDataProvider.AnalystWiffDataProvider()
		'	oBatch = Clearcore2.Data.AnalystDataProvider.AnalystDataProviderFactory.CreateBatch(strDataFilePath, oProvider)

		'	blnSuccess = True

		'	If blnSuccess Then

		'		Dim lstSampleNames As New System.Collections.Generic.List(Of String)
		'		Dim lstExperimentTypes As New System.Collections.Generic.Dictionary(Of Clearcore2.Data.DataAccess.SampleData.ExperimentType, Integer)

		'		lstSampleNames = GetSampleNames(oBatch)

		'		' Process each sample
		'		Dim intSampleIndex As Integer = 0
		'		For Each strSampleName As String In lstSampleNames

		'			Dim oSample As Clearcore2.Data.DataAccess.SampleData.Sample
		'			Dim oMSSample As Clearcore2.Data.DataAccess.SampleData.MassSpectrometerSample

		'			oSample = oBatch.GetSample(intSampleIndex)

		'			' Note that the .AcqTimeEnd values will get updated below using dblRT
		'			If intSampleIndex = 0 Then
		'				udtFileInfo.AcqTimeStart = oSample.Details.AcquisitionDateTime
		'				udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart
		'			Else
		'				If oSample.Details.AcquisitionDateTime > System.DateTime.MinValue Then
		'					If oSample.Details.AcquisitionDateTime < udtFileInfo.AcqTimeStart Then
		'						udtFileInfo.AcqTimeStart = oSample.Details.AcquisitionDateTime
		'					End If
		'					udtFileInfo.AcqTimeEnd = oSample.Details.AcquisitionDateTime
		'				End If
		'			End If


		'			If oSample.HasMassSpectrometerData Then
		'				oMSSample = oSample.MassSpectrometerSample()

		'				Dim intExperimentCount As Integer
		'				intExperimentCount = oMSSample.ExperimentCount

		'				For intExperimentIndex As Integer = 0 To intExperimentCount - 1

		'					Dim oMSExperiment As Clearcore2.Data.DataAccess.SampleData.MSExperiment
		'					oMSExperiment = oMSSample.GetMSExperiment(intExperimentIndex)

		'					' Update the total scan count
		'					Dim intScanCount As Integer
		'					intScanCount = oMSExperiment.Details.NumberOfScans
		'					intTotalScanCount += intScanCount

		'					' Update the list of experiment types used
		'					Dim eExperimentType As Clearcore2.Data.DataAccess.SampleData.ExperimentType
		'					Dim intExperimentTypeUsageCount As Integer = 0
		'					eExperimentType = oMSExperiment.Details.ExperimentType

		'					If lstExperimentTypes.TryGetValue(eExperimentType, intExperimentTypeUsageCount) Then
		'						lstExperimentTypes(eExperimentType) = intExperimentTypeUsageCount + 1
		'					Else
		'						lstExperimentTypes.Add(eExperimentType, 1)
		'					End If

		'					If intScanCount > 0 Then
		'						' Update the total acquisition time

		'						Dim dblRT As Double = 0
		'						Dim blnScantimeLookupSuccess As Boolean = False
		'						Dim intScanEnd As Integer = intScanCount				'  ToDo: Possibly use intScanCount-1

		'						Do
		'							Try
		'								dblRT = oMSExperiment.GetRTFromExperimentScanIndex(intScanEnd)
		'								blnScantimeLookupSuccess = True
		'							Catch ex As System.Exception
		'								intScanEnd -= 1
		'								If intScanEnd < 0 Then
		'									Exit Do
		'								End If
		'							End Try
		'						Loop While Not blnScantimeLookupSuccess


		'						If blnScantimeLookupSuccess Then

		'							' ToDo: determine if we should add dblRT to dblAcquisitionLengthMinutes or just keep the maximum
		'							' To add, we would do this: dblAcquisitionLengthMinutes += dblRT

		'							' For safety, just keeping the maximum
		'							If dblRT > dblAcquisitionLengthMinutes Then
		'								dblAcquisitionLengthMinutes = dblRT
		'							End If
		'						End If


		'					End If

		'				Next
		'			End If

		'			intSampleIndex += 1

		'		Next

		'		' Possibly update .AcqTimeEnd
		'		If udtFileInfo.AcqTimeStart.AddMinutes(dblAcquisitionLengthMinutes) > udtFileInfo.AcqTimeEnd Then
		'			udtFileInfo.AcqTimeEnd = udtFileInfo.AcqTimeStart.AddMinutes(dblAcquisitionLengthMinutes)
		'		End If

		'		' We could use the file modification time as the acquisition end time, but that's not as accurate
		'		' udtFileInfo.AcqTimeEnd = udtFileInfo.FileSystemModificationTime

		'		udtFileInfo.ScanCount = intTotalScanCount


		'	End If

		'Catch ex As System.Exception
		'	blnSuccess = False
		'End Try

		Return blnSuccess
	End Function

	'Private Function GetSampleNames(ByRef oBatch As Clearcore2.Data.DataAccess.SampleData.Batch) As System.Collections.Generic.List(Of String)

	'	Dim lstSampleNames As System.Collections.Generic.List(Of String) = New System.Collections.Generic.List(Of String)()
	'	Dim dctDuplicateTracker As System.Collections.Generic.Dictionary(Of String, Integer) = New System.Collections.Generic.Dictionary(Of String, Integer)(StringComparer.CurrentCultureIgnoreCase)
	'	Dim intCount As Integer

	'	' Obtain the sample names defined in oBatch
	'	' If duplicate sample names exist, we will append the duplicate number for the 2nd, 3rd, etc. occurrences
	'	For Each strSampleName As String In oBatch.GetSampleNames()

	'		If dctDuplicateTracker.TryGetValue(strSampleName, intCount) Then
	'			' Duplicate
	'			intCount += 1
	'			dctDuplicateTracker(strSampleName) = intCount
	'			lstSampleNames.Add(strSampleName & " (" & intCount.ToString() & ")")
	'		Else
	'			' Not a duplicate
	'			dctDuplicateTracker.Add(strSampleName, 1)
	'			lstSampleNames.Add(strSampleName)
	'		End If

	'	Next

	'	Return lstSampleNames

	'End Function

End Class
