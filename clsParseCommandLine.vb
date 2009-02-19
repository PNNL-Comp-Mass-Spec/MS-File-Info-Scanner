Option Strict On

' This class can be used to parse the text following the program name when a 
'  program is started from the command line
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started November 8, 2003

' E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
' Website: http://ncrr.pnl.gov/ or http://www.sysbio.org/resources/staff/
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

'
' Last modified August 29, 2007

Public Class clsParseCommandLine

    Private mSwitches As Hashtable
    Private mNonSwitchParameters() As String

    Private mShowHelp As Boolean

    Public ReadOnly Property NeedToShowHelp() As Boolean
        Get
            Return mShowHelp
        End Get
    End Property

    Public ReadOnly Property ParameterCount() As Integer
        Get
            If mSwitches Is Nothing Then
                Return 0
            Else
                Return mSwitches.Count
            End If
        End Get
    End Property

    Public ReadOnly Property NonSwitchParameterCount() As Integer
        Get
            If mNonSwitchParameters Is Nothing Then
                Return 0
            Else
                Return mNonSwitchParameters.Length
            End If
        End Get
    End Property

    Public Function InvalidParametersPresent(ByVal strParameterList() As String, Optional ByVal blnCaseSensitive As Boolean = False) As Boolean
        ' Returns true if any of the parameters are not present in strParameterList()

        Dim intIndex As Integer
        Dim blnMatchFound As Boolean

        Try
            Dim iEnum As System.Collections.IDictionaryEnumerator = mSwitches.GetEnumerator()

            Do While iEnum.MoveNext()
                blnMatchFound = False
                For intIndex = 0 To strParameterList.Length - 1
                    If blnCaseSensitive Then
                        If CStr(iEnum.Key) = strParameterList(intIndex) Then
                            blnMatchFound = True
                            Exit For
                        End If
                    Else
                        If CStr(iEnum.Key).ToUpper = strParameterList(intIndex).ToUpper Then
                            blnMatchFound = True
                            Exit For
                        End If
                    End If
                Next intIndex

                If Not blnMatchFound Then Return True
            Loop

        Catch ex As System.Exception
            Throw New System.Exception("Error in InvalidParametersPresent", ex)
        End Try

    End Function

    Public Function ParseCommandLine(Optional ByVal strSwitchStartChar As Char = "/"c, Optional ByVal strSwitchParameterChar As Char = ":"c) As Boolean
        ' Returns True if any command line parameters were found
        ' Otherwise, returns false
        '
        ' If /? or /help is found, then returns False and sets mShowHelp to True

        Dim strCmdLine As String
        Dim strKey As String, strValue As String

        Dim intCharLoc As Integer
        Dim intNonSwitchParameterCount As Integer

        Dim intIndex As Integer
        Dim strParameters() As String

        Dim blnSwitchParam As Boolean

        mSwitches = New Hashtable

        intNonSwitchParameterCount = 0
        ReDim mNonSwitchParameters(9)

        Try
            Try
                ' This command will fail if the program is called from a network share
                strCmdLine = System.Environment.CommandLine()
                strParameters = System.Environment.GetCommandLineArgs()
            Catch ex As System.Exception
                Windows.Forms.MessageBox.Show("This program cannot be run from a network share.  Please map a drive to the network share you are currently accessing or copy the program files and required DLL's to your local computer.", "Error", Windows.Forms.MessageBoxButtons.OK, Windows.Forms.MessageBoxIcon.Exclamation)
                mShowHelp = True
                Return False
            End Try

            If strCmdLine Is Nothing OrElse strCmdLine.Length = 0 Then
                Return False
            ElseIf strCmdLine.IndexOf(strSwitchStartChar & "?") > 0 Or strCmdLine.ToLower.IndexOf(strSwitchStartChar & "help") > 0 Then
                mShowHelp = True
                Return False
            End If

            ' Parse the command line
            mSwitches.Clear()

            ' Note that strParameters(0) is the path to the Executable for the calling program
            For intIndex = 1 To strParameters.Length - 1

                If strParameters(intIndex).Length > 0 Then
                    ' Note that .NET will strip out the starting and ending double quote if the user provides a parameter like this:
                    ' MyProgram.exe "C:\Program Files\FileToProcess"

                    strKey = strParameters(intIndex).TrimStart(" "c)
                    strValue = String.Empty

                    If strKey.StartsWith(strSwitchStartChar) Then
                        blnSwitchParam = True
                    ElseIf strKey.StartsWith("-"c) OrElse strKey.StartsWith("/"c) Then
                        blnSwitchParam = True
                    Else
                        ' Parameter doesn't start with strSwitchStartChar or / or -
                        blnSwitchParam = False
                    End If

                    If blnSwitchParam Then
                        ' Look for strSwitchParameterChar in strParameters(intIndex)
                        intCharLoc = strParameters(intIndex).IndexOf(strSwitchParameterChar)

                        If intCharLoc >= 0 Then
                            ' Parameter is of the form /I:MyParam or /I:"My Parameter" or -I:"My Parameter" or /MyParam:Setting
                            strValue = strKey.Substring(intCharLoc + 1).Trim

                            ' Remove any starting and ending quotation marks
                            strValue = strValue.Trim(""""c)

                            strKey = strKey.Substring(0, intCharLoc)
                        Else
                            ' Parameter is of the form /S or -S
                        End If

                        ' Remove the switch character from strKey
                        strKey = strKey.Substring(1).Trim

                        ' Note: .Item() will add strKey if it doesn't exist (which is normally the case)
                        mSwitches.Item(strKey) = strValue
                    Else
                        ' Non-switch parameter since strSwitchParameterChar was not found and does not start with strSwitchStartChar

                        ' Remove any starting and ending quotation marks
                        strKey = strKey.Trim(""""c)

                        If intNonSwitchParameterCount >= mNonSwitchParameters.Length Then
                            ReDim Preserve mNonSwitchParameters(mNonSwitchParameters.Length * 2 - 1)
                        End If
                        mNonSwitchParameters(intNonSwitchParameterCount) = String.Copy(strKey)
                        intNonSwitchParameterCount += 1
                    End If

                End If
            Next intIndex

        Catch ex As System.Exception
            Throw New System.Exception("Error in ParseCommandLine", ex)
        Finally
            If intNonSwitchParameterCount < 0 Then intNonSwitchParameterCount = 0
            ReDim Preserve mNonSwitchParameters(intNonSwitchParameterCount - 1)
        End Try

        If mSwitches.Count + intNonSwitchParameterCount > 0 Then
            Return True
        Else
            Return False
        End If

    End Function

    Public Function RetrieveNonSwitchParameter(ByVal intParameterIndex As Integer) As String
        Dim strValue As String = String.Empty

        If Not mNonSwitchParameters Is Nothing Then
            If intParameterIndex < mNonSwitchParameters.Length Then
                strValue = mNonSwitchParameters(intParameterIndex)
            End If
        End If

        If strValue Is Nothing Then
            strValue = String.Empty
        End If

        Return strValue

    End Function

    Public Function RetrieveParameter(ByVal intParameterIndex As Integer, ByRef strKey As String, ByRef strValue As String) As Boolean
        ' Returns True if the parameter exists; returns false otherwise

        Dim intIndex As Integer

        Try
            strKey = ""
            strValue = ""
            If intParameterIndex < mSwitches.Count Then
                Dim iEnum As System.Collections.IDictionaryEnumerator = mSwitches.GetEnumerator()

                intIndex = 0
                Do While iEnum.MoveNext()
                    If intIndex = intParameterIndex Then
                        strKey = CStr(iEnum.Key)
                        strValue = CStr(iEnum.Value)
                        Return True
                    End If
                    intIndex += 1
                Loop
            Else
                Return False
            End If
        Catch ex As System.Exception
            Throw New System.Exception("Error in RetrieveParameter", ex)
        End Try

    End Function

    Public Function RetrieveValueForParameter(ByVal strKey As String, ByRef strValue As String, Optional ByVal blnCaseSensitive As Boolean = False) As Boolean
        ' Returns True if the parameter exists; returns false otherwise

        Try
            strValue = ""
            If blnCaseSensitive Then
                If mSwitches.ContainsKey(strKey) Then
                    strValue = CStr(mSwitches(strKey))
                    Return True
                Else
                    Return False
                End If
            Else
                Dim iEnum As System.Collections.IDictionaryEnumerator = mSwitches.GetEnumerator()

                Do While iEnum.MoveNext()
                    If CStr(iEnum.Key).ToUpper = strKey.ToUpper Then
                        strValue = CStr(mSwitches(iEnum.Key))
                        Return True
                    End If
                Loop
                Return False
            End If
        Catch ex As System.Exception
            Throw New System.Exception("Error in RetrieveValueForParameter", ex)
        End Try

    End Function

End Class
