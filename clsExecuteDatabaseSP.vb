Option Strict On

' Simplified version of clsDBTask.vb, which was written by Dave Clark for the DMS Analysis Manager in 2007

'Imports System.Collections.Specialized
'Imports System.Data.SqlClient
'Imports System.Xml.XPath
'Imports System.Xml
'Imports System.IO

Public Class clsExecuteDatabaseSP

#Region "Constants"
    Public Const RET_VAL_OK As Integer = 0
    Public Const RET_VAL_EXCESSIVE_RETRIES As Integer = -5           ' Timeout expired
    Public Const RET_VAL_DEADLOCK As Integer = -4                    ' Transaction (Process ID 143) was deadlocked on lock resources with another process and has been chosen as the deadlock victim

    Public Const DEFAULT_SP_RETRY_COUNT As Integer = 3
    Public Const DEFAULT_SP_RETRY_DELAY_SEC As Integer = 20

    Public Const DEFAULT_SP_TIMEOUT_SEC As Integer = 30

#End Region

#Region "Module variables"

    Protected m_ConnStr As String
    Protected mTimeoutSeconds As Integer = DEFAULT_SP_TIMEOUT_SEC

    Public Event DBErrorEvent(ByVal Message As String)

#End Region

#Region "Properties"
    Public ReadOnly Property DBConnectionString() As String
        Get
            Return m_ConnStr
        End Get
    End Property

    Public Property TimeoutSeconds() As Integer
        Get
            Return mTimeoutSeconds
        End Get
        Set(ByVal value As Integer)
            mTimeoutSeconds = value
        End Set
    End Property
#End Region

#Region "Methods"
    ''' <summary>
    ''' Constructor
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub New(ByVal ConnectionString As String)

        m_ConnStr = String.Copy(ConnectionString)

    End Sub

    ''' <summary>
    ''' Event handler for InfoMessage event
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="args"></param>
    ''' <remarks>Errors and warnings from SQL Server are caught here</remarks>
    Private Sub OnInfoMessage(ByVal sender As Object, ByVal args As System.Data.SqlClient.SqlInfoMessageEventArgs)

        Dim err As System.Data.SqlClient.SqlError
        Dim s As String

        For Each err In args.Errors
            s = "Message: " & err.Message & _
                ", Source: " & err.Source & _
                ", Class: " & err.Class & _
                ", State: " & err.State & _
                ", Number: " & err.Number & _
                ", LineNumber: " & err.LineNumber & _
                ", Procedure:" & err.Procedure & _
                ", Server: " & err.Server

            RaiseEvent DBErrorEvent(s)
        Next

    End Sub

    ''' <summary>
    ''' Method for executing a db stored procedure, assuming no data table is returned; will retry the call to the procedure up to DEFAULT_SP_RETRY_COUNT=3 times
    ''' </summary>
    ''' <param name="SpCmd">SQL command object containing stored procedure params</param>
    ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
    ''' <remarks></remarks>
    Public Function ExecuteSP(ByRef SpCmd As System.Data.SqlClient.SqlCommand) As Integer

        Return ExecuteSP(SpCmd, Nothing, DEFAULT_SP_RETRY_COUNT, DEFAULT_SP_RETRY_DELAY_SEC)

    End Function

    ''' <summary>
    ''' Method for executing a db stored procedure, assuming no data table is returned
    ''' </summary>
    ''' <param name="SpCmd">SQL command object containing stored procedure params</param>
    ''' <param name="MaxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
    ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
    ''' <remarks></remarks>
    Public Function ExecuteSP(ByRef SpCmd As System.Data.SqlClient.SqlCommand, _
                              ByVal MaxRetryCount As Integer) As Integer

        Return ExecuteSP(SpCmd, Nothing, MaxRetryCount, DEFAULT_SP_RETRY_DELAY_SEC)

    End Function

    ''' <summary>
    ''' Method for executing a db stored procedure, assuming no data table is returned
    ''' </summary>
    ''' <param name="SpCmd">SQL command object containing stored procedure params</param>
    ''' <param name="MaxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
    ''' <param name="RetryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
    ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
    ''' <remarks></remarks>
    Public Function ExecuteSP(ByRef SpCmd As System.Data.SqlClient.SqlCommand, _
                              ByVal MaxRetryCount As Integer, _
                              ByVal RetryDelaySeconds As Integer) As Integer

        Return ExecuteSP(SpCmd, Nothing, MaxRetryCount, RetryDelaySeconds)

    End Function

    ''' <summary>
    ''' Method for executing a db stored procedure if a data table is to be returned; will retry the call to the procedure up to DEFAULT_SP_RETRY_COUNT=3 times
    ''' </summary>
    ''' <param name="SpCmd">SQL command object containing stored procedure params</param>
    ''' <param name="OutTable">NOTHING when called; if SP successful, contains data table on return</param>
    ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
    ''' <remarks></remarks>
    Public Function ExecuteSP(ByRef SpCmd As System.Data.SqlClient.SqlCommand, _
                              ByRef OutTable As DataTable) As Integer
        Return ExecuteSP(SpCmd, OutTable, DEFAULT_SP_RETRY_COUNT, DEFAULT_SP_RETRY_DELAY_SEC)
    End Function

    ''' <summary>
    ''' Method for executing a db stored procedure if a data table is to be returned
    ''' </summary>
    ''' <param name="SpCmd">SQL command object containing stored procedure params</param>
    ''' <param name="OutTable">NOTHING when called; if SP successful, contains data table on return</param>
    ''' <param name="MaxRetryCount">Maximum number of times to attempt to call the stored procedure</param>
    ''' <param name="RetryDelaySeconds">Number of seconds to wait between retrying the call to the procedure</param>
    ''' <returns>Result code returned by SP; -1 if unable to execute SP</returns>
    ''' <remarks></remarks>
    Public Function ExecuteSP(ByRef SpCmd As System.Data.SqlClient.SqlCommand, _
                              ByRef OutTable As DataTable, _
                              ByVal MaxRetryCount As Integer, _
                              ByVal RetryDelaySeconds As Integer) As Integer

        Dim ResCode As Integer = -9999  'If this value is in error msg, then exception occurred before ResCode was set
        Dim ErrMsg As String
        Dim MyTimer As New System.Diagnostics.Stopwatch
        Dim RetryCount As Integer = MaxRetryCount
        Dim blnDeadlockOccurred As Boolean

        If RetryCount < 1 Then
            RetryCount = 1
        End If

        If RetryDelaySeconds < 1 Then
            RetryDelaySeconds = 1
        End If

        While RetryCount > 0    'Multiple retry loop for handling SP execution failures
            blnDeadlockOccurred = False
            Try
                Using Cn As System.Data.SqlClient.SqlConnection = New System.Data.SqlClient.SqlConnection(m_ConnStr)
                    AddHandler Cn.InfoMessage, New System.Data.SqlClient.SqlInfoMessageEventHandler(AddressOf OnInfoMessage)
                    Using Da As System.Data.SqlClient.SqlDataAdapter = New System.Data.SqlClient.SqlDataAdapter(), Ds As DataSet = New DataSet
                        'NOTE: The connection has to be added here because it didn't exist at the time the command object was created
                        SpCmd.Connection = Cn
                        'Change command timeout from 30 second default in attempt to reduce SP execution timeout errors
                        SpCmd.CommandTimeout = mTimeoutSeconds
                        Da.SelectCommand = SpCmd
                        MyTimer.Start()
                        Da.Fill(Ds)
                        MyTimer.Stop()
                        ResCode = CInt(Da.SelectCommand.Parameters("@Return").Value)
                        If OutTable IsNot Nothing Then OutTable = Ds.Tables(0)
                    End Using  'Ds
                    RemoveHandler Cn.InfoMessage, AddressOf OnInfoMessage
                End Using  'Cn

                Exit While
            Catch ex As System.Exception
                MyTimer.Stop()
                RetryCount -= 1
                ErrMsg = "Exception filling data adapter for " & SpCmd.CommandText & ": " & ex.Message
                ErrMsg &= "; ResCode = " & ResCode.ToString & "; Retry count = " & RetryCount.ToString

                RaiseEvent DBErrorEvent(ErrMsg)

                If ex.Message.StartsWith("Could not find stored procedure " & SpCmd.CommandText) Then
                    Exit While
                ElseIf ex.Message.Contains("was deadlocked") Then
                    blnDeadlockOccurred = True
                End If

            Finally
                MyTimer.Reset()
            End Try

            If RetryCount > 0 Then
                System.Threading.Thread.Sleep(RetryDelaySeconds * 1000) 'Wait 20 seconds before retrying
            End If
        End While

        If RetryCount < 1 Then
            'Too many retries, log and return error
            ErrMsg = "Excessive retries"
            If blnDeadlockOccurred Then
                ErrMsg &= " (including deadlock)"
            End If
            ErrMsg = " executing SP " & SpCmd.CommandText

            RaiseEvent DBErrorEvent(ErrMsg)

            If blnDeadlockOccurred Then
                Return RET_VAL_DEADLOCK
            Else
                Return RET_VAL_EXCESSIVE_RETRIES
            End If
        End If

        Return ResCode

    End Function

#End Region

End Class
