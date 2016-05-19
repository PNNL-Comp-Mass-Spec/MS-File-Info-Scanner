Option Strict On

''' <summary>
''' Set of utilities for computing the median value given a list of numbers
''' Also supports the NthOrderStatistic (return the Nth smallest item from a list)
''' The algorithm is more efficient than performing a full sort on the list of numbers
''' </summary>
''' <remarks>From http://stackoverflow.com/questions/4140719/i-need-c-sharp-function-that-will-calculate-median </remarks>
<CLSCompliant(True)>
Public Class clsMedianUtilities

	Protected ReadOnly mRandom As Random

	Public Enum eEventListCountBehaviorType
		ReportMidpointAverage = 0
		ReportNearest = 1
	End Enum

	Public Property EvenNumberedListCountBehavior As eEventListCountBehaviorType

	''' <summary>
	''' Constructor
	''' </summary>
	Public Sub New()
		mRandom = New Random
		EvenNumberedListCountBehavior = eEventListCountBehaviorType.ReportMidpointAverage
	End Sub

	''' <summary>
	''' Partitions the given list around a pivot element such that all elements on left of pivot are less than or equal to pivot
	''' and the ones at thr right are greater than pivot. This method can be used for sorting, N-order statistics such as
	''' as median finding algorithms.
	''' Pivot is selected ranodmly if random number generator is supplied else its selected as last element in the list.
	''' Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 171
	''' </summary>	
	Private Function Partition(ByVal lstData As IList(Of Double), ByVal startIndex As Integer, ByVal endIndex As Integer, ByVal oRandom As Random) As Integer
		If oRandom IsNot Nothing Then
			Swap(lstData, endIndex, oRandom.Next(startIndex, endIndex))
		End If

		Dim pivot = lstData(endIndex)
		Dim lastLow = startIndex - 1
		For i As Integer = startIndex To endIndex - 1
			If lstData(i).CompareTo(pivot) <= 0 Then
				lastLow += 1
				Swap(lstData, i, lastLow)
			End If
		Next
		lastLow += 1
		Swap(lstData, endIndex, lastLow)
		Return lastLow

	End Function

	''' <summary>
	''' Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
	''' Note: specified list will be mutated in the process.
	''' Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
	''' </summary>
	Public Function NthOrderStatistic(ByVal lstData As IList(Of Double), ByVal n As Integer) As Double
		Return NthOrderStatistic(lstData, n, 0, lstData.Count - 1, mRandom)
	End Function

	''' <summary>
	''' Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
	''' Note: specified list will be mutated in the process.
	''' Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
	''' </summary>
	Public Function NthOrderStatistic(ByVal lstData As IList(Of Double), ByVal n As Integer, ByVal oRandom As Random) As Double
		Return NthOrderStatistic(lstData, n, 0, lstData.Count - 1, oRandom)
	End Function

	''' <summary>
	''' Returns Nth smallest element from the list. Here n starts from 0 so that n=0 returns minimum, n=1 returns 2nd smallest element etc.
	''' Note: specified list will be mutated in the process.
	''' Reference: Introduction to Algorithms 3rd Edition, Corman et al, pp 216
	''' </summary>
	Private Function NthOrderStatistic(ByVal lstData As IList(Of Double), ByVal n As Integer, ByVal startIndex As Integer, ByVal endIndex As Integer, ByVal oRandom As Random) As Double
		While True
			Dim pivotIndex = Partition(lstData, startIndex, endIndex, oRandom)
			If pivotIndex = n Then
				Return lstData(pivotIndex)
			End If

			If n < pivotIndex Then
				endIndex = pivotIndex - 1
			Else
				startIndex = pivotIndex + 1
			End If
		End While

		Throw New Exception("This code should not be reached")
	End Function

	''' <summary>
	''' Swap two items in a list
	''' </summary>
	Public Sub Swap(ByVal lstData As IList(Of Double), ByVal i As Integer, ByVal j As Integer)
		If i = j Then
			' Swap is not required
			Return
		End If
		Dim temp = lstData(i)
		lstData(i) = lstData(j)
		lstData(j) = temp
	End Sub

	''' <summary>
	''' Compute the median of the values in lstData
	''' </summary>
	''' <remarks>lstData will be mutated (changed) when determining the median</remarks>
	Public Function Median(ByVal lstData As IList(Of Double)) As Double

		If lstData Is Nothing OrElse lstData.Count < 1 Then
			' List is empty
			Return 0
		ElseIf lstData.Count <= 1 Then
			' Only 1 item; the median is the value
			Return lstData.First		
		End If

		Dim midPoint1 = CInt(Math.Floor((lstData.Count - 1) / 2))
		Dim median1 = NthOrderStatistic(lstData, midPoint1)

		If lstData.Count Mod 2 > 0 OrElse EvenNumberedListCountBehavior = eEventListCountBehaviorType.ReportNearest Then
			Return median1
		End If

		' List contains an even number of elements
		Dim midPoint2 = CInt(lstData.Count / 2)
		Dim median2 = NthOrderStatistic(lstData, midPoint2)

		' Median is the average of the two middle points
		Return (median1 + median2) / 2.0

	End Function

	''' <summary>
	''' Compute the median of a subset of lstData, selected using getValue
	''' </summary>
	Public Function Median(ByVal lstData As IEnumerable(Of Double), ByVal getValue As Func(Of Double, Double)) As Double
		Dim lstDataSubset = lstData.Select(getValue).ToList()
		Return Median(lstDataSubset)
	End Function

End Class
