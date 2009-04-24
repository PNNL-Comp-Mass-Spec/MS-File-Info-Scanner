Option Strict On

' This class can be used to read or write settings in an Xml settings file
' Based on a class from the DMS Analysis Manager software written by Dave Clark and Gary Kiebel (PNNL, Richland, WA)
' Additional features added by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in October 2003
' Copyright 2005, Battelle Memorial Institute
'
' Updated in October 2004 to truly be case-insensitive if IsCaseSensitive = False when calling LoadSettings()
' Updated in August 2007 to remove the PRISM.Logging functionality and to include class IniFileReader inside class XmlSettingsFileAccessor

Public Class XmlSettingsFileAccessor

    Public Sub New()
        mCaseSensitive = False
        htSectionNames = New Hashtable

        With mCachedSection
            .SectionName = String.Empty
            .htKeys = New Hashtable
        End With
    End Sub

    Private Structure udtRecentSectionType
        Public SectionName As String            ' Stores the section name whose keys are cached; the section name is capitalized identically to that actually present in the Xml file
        Public htKeys As Hashtable
    End Structure

    ' Ini file reader
    ' Call LoadSettings to initialize, even if simply saving settings
    Private m_IniFilePath As String = ""
    Private WithEvents m_iniFileAccessor As IniFileReader

    Private mCaseSensitive As Boolean

    ' When mCaseSensitive = False, then htSectionNames stores mapping between lowercase section name and actual section name stored in file
    '   If section is present more than once in file, then only grabs the last occurence of the section
    ' When mCaseSensitive = True, then the mappings in htSectionNames are effectively not used
    Private htSectionNames As Hashtable
    Private mCachedSection As udtRecentSectionType

    Public Event InformationMessage(ByVal msg As String)

    ''' <summary>
    ''' Loads the settings for the defined Xml Settings File.  Assumes names are not case sensitive
    ''' </summary>
    ''' <return>The function returns a boolean that shows if the file was successfully loaded.</return>
    Public Function LoadSettings() As Boolean
        Return LoadSettings(m_IniFilePath, False)
    End Function

    ''' <summary>
    ''' Loads the settings for the defined Xml Settings File.   Assumes names are not case sensitive
    ''' </summary>
    ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
    ''' <return>The function returns a boolean that shows if the file was successfully loaded.</return>
    Public Function LoadSettings(ByVal XmlSettingsFilePath As String) As Boolean
        Return LoadSettings(XmlSettingsFilePath, False)
    End Function

    ''' <summary>
    ''' Loads the settings for the defined Xml Settings File
    ''' </summary>
    ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
    ''' <param name="IsCaseSensitive">Case sensitive names if True.  Non-case sensitive if false.</param>
    Public Function LoadSettings(ByVal XmlSettingsFilePath As String, ByVal IsCaseSensitive As Boolean) As Boolean
        mCaseSensitive = IsCaseSensitive

        m_IniFilePath = XmlSettingsFilePath

        ' Note: Always set IsCaseSensitive = True for IniFileReader's constructor since this class handles 
        '       case sensitivity mapping internally
        m_iniFileAccessor = New IniFileReader(m_IniFilePath, True)
        If m_iniFileAccessor Is Nothing Then
            Return False
        ElseIf m_iniFileAccessor.Initialized Then
            CacheSectionNames()
            Return True
        Else
            Return False
        End If

    End Function

    ''' <summary>
    ''' Saves the settings for the defined Xml Settings File.  Note that you must call LoadSettings to initialize the class prior to setting any values.
    ''' </summary>
    ''' <return>The function returns a boolean that shows if the file was successfully saved.</return>
    Public Function SaveSettings() As Boolean

        If m_iniFileAccessor Is Nothing Then
            Return False
        ElseIf m_iniFileAccessor.Initialized Then
            m_iniFileAccessor.OutputFilename = m_IniFilePath
            m_iniFileAccessor.Save()
            Return True
        Else
            Return False
        End If

    End Function

    ''' <summary>Checks if a section is present in the settings file.</summary>
    ''' <param name="sectionName">The name of the section to look for.</param>
    ''' <return>The function returns a boolean that shows if the section is present.</return>
    Public Function SectionPresent(ByVal sectionName As String) As Boolean
        Dim strSections As System.Collections.Specialized.StringCollection
        Dim intIndex As Integer

        strSections = m_iniFileAccessor.AllSections

        For intIndex = 0 To strSections.Count - 1
            If SetNameCase(strSections.Item(intIndex)) = SetNameCase(sectionName) Then Return True
        Next intIndex

        Return False

    End Function

    Private Function CacheKeyNames(ByVal sectionName As String) As Boolean
        ' Looks up the Key Names for the given section, storing them in mCachedSection
        ' This is done so that this class will know the correct capitalization for the key names

        Dim strKeys As System.Collections.Specialized.StringCollection
        Dim intIndex As Integer

        Dim sectionNameInFile As String
        Dim strKeyNameToStore As String

        ' Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
        sectionNameInFile = GetCachedSectionName(sectionName)
        If sectionNameInFile.Length = 0 Then Return False

        Try
            ' Grab the keys for sectionName
            strKeys = m_iniFileAccessor.AllKeysInSection(sectionNameInFile)
        Catch ex As System.Exception
            ' Invalid section name; do not update anything
            Return False
        End Try

        If strKeys Is Nothing Then
            Return False
        End If

        ' Update mCachedSection with the key names for the given section
        With mCachedSection
            .SectionName = sectionNameInFile
            .htKeys.Clear()

            For intIndex = 0 To strKeys.Count - 1
                If mCaseSensitive Then
                    strKeyNameToStore = String.Copy(strKeys.Item(intIndex))
                Else
                    strKeyNameToStore = String.Copy(strKeys.Item(intIndex).ToLower)
                End If

                If Not .htKeys.Contains(strKeyNameToStore) Then
                    .htKeys.Add(strKeyNameToStore, strKeys.Item(intIndex))
                End If

            Next intIndex
        End With

        Return True

    End Function

    Private Sub CacheSectionNames()
        ' Looks up the Section Names in the XML file
        ' This is done so that this class will know the correct capitalization for the section names

        Dim strSections As System.Collections.Specialized.StringCollection
        Dim strSectionNameToStore As String

        Dim intIndex As Integer

        strSections = m_iniFileAccessor.AllSections

        htSectionNames.Clear()

        For intIndex = 0 To strSections.Count - 1
            If mCaseSensitive Then
                strSectionNameToStore = String.Copy(strSections.Item(intIndex))
            Else
                strSectionNameToStore = String.Copy(strSections.Item(intIndex).ToLower)
            End If

            If Not htSectionNames.Contains(strSectionNameToStore) Then
                htSectionNames.Add(strSectionNameToStore, strSections.Item(intIndex))
            End If

        Next intIndex

    End Sub

    Private Function GetCachedKeyName(ByVal sectionName As String, ByVal keyName As String) As String
        ' Looks up the correct capitalization for key keyName in section sectionName
        ' Returns String.Empty if not found

        Dim blnSuccess As Boolean
        Dim sectionNameInFile As String
        Dim keyNameToFind As String

        ' Lookup the correct capitalization for sectionName (only truly important if mCaseSensitive = False)
        sectionNameInFile = GetCachedSectionName(sectionName)
        If sectionNameInFile.Length = 0 Then Return String.Empty

        If mCachedSection.SectionName = sectionNameInFile Then
            blnSuccess = True
        Else
            ' Update the keys for sectionName
            blnSuccess = CacheKeyNames(sectionName)
        End If

        If blnSuccess Then
            With mCachedSection
                keyNameToFind = SetNameCase(keyName)
                If .htKeys.ContainsKey(keyNameToFind) Then
                    Return CStr(.htKeys(keyNameToFind))
                Else
                    Return String.Empty
                End If
            End With
        Else
            Return String.Empty
        End If
    End Function

    Private Function GetCachedSectionName(ByVal sectionName As String) As String
        ' Looks up the correct capitalization for sectionName
        ' Returns String.Empty if not found

        Dim sectionNameToFind As String

        sectionNameToFind = SetNameCase(sectionName)
        If htSectionNames.ContainsKey(sectionNameToFind) Then
            Return CStr(htSectionNames(sectionNameToFind))
        Else
            Return String.Empty
        End If

    End Function

    Private Function SetNameCase(ByVal aName As String) As String
        ' Changes aName to lowercase if mCaseSensitive = False

        If mCaseSensitive Then
            Return aName
        Else
            Return aName.ToLower()
        End If
    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as a String.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As String, Optional ByRef valueNotPresent As Boolean = False) As String
        Dim strResult As String
        Dim sectionNameInFile As String
        Dim keyNameInFile As String

        If mCaseSensitive Then
            strResult = m_iniFileAccessor.GetIniValue(sectionName, keyName)
        Else
            sectionNameInFile = GetCachedSectionName(sectionName)
            If sectionNameInFile.Length > 0 Then
                keyNameInFile = GetCachedKeyName(sectionName, keyName)
                If keyNameInFile.Length > 0 Then
                    strResult = m_iniFileAccessor.GetIniValue(sectionNameInFile, keyNameInFile)
                End If
            End If
        End If

        If strResult Is Nothing Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Return strResult
        End If
    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns boolean True if the "value" attribute is "true".  Otherwise, returns boolean False.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Boolean, Optional ByRef valueNotPresent As Boolean = False) As Boolean
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            If strResult.ToLower = "true" Then
                Return True
            Else
                Return False
            End If
        End If
    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as a Short.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Short, Optional ByRef valueNotPresent As Boolean = False) As Short
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Try
                If IsNumeric(strResult) Then
                    Return CShort(strResult)
                ElseIf strResult.ToLower = "true" Then
                    Return -1
                ElseIf strResult.ToLower = "false" Then
                    Return 0
                Else
                    valueNotPresent = True
                    Return valueIfMissing
                End If
            Catch ex As System.Exception
                valueNotPresent = True
                Return valueIfMissing
            End Try
        End If

    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as an Integer.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Integer, Optional ByRef valueNotPresent As Boolean = False) As Integer
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Try
                If IsNumeric(strResult) Then
                    Return CInt(strResult)
                ElseIf strResult.ToLower = "true" Then
                    Return -1
                ElseIf strResult.ToLower = "false" Then
                    Return 0
                Else
                    valueNotPresent = True
                    Return valueIfMissing
                End If
            Catch ex As System.Exception
                valueNotPresent = True
                Return valueIfMissing
            End Try
        End If

    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as a Long.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Long, Optional ByRef valueNotPresent As Boolean = False) As Long
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Try
                If IsNumeric(strResult) Then
                    Return CLng(strResult)
                ElseIf strResult.ToLower = "true" Then
                    Return -1
                ElseIf strResult.ToLower = "false" Then
                    Return 0
                Else
                    valueNotPresent = True
                    Return valueIfMissing
                End If
            Catch ex As System.Exception
                valueNotPresent = True
                Return valueIfMissing
            End Try
        End If

    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as a Single.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Single, Optional ByRef valueNotPresent As Boolean = False) As Single
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Try
                If IsNumeric(strResult) Then
                    Return CSng(strResult)
                ElseIf strResult.ToLower = "true" Then
                    Return -1
                ElseIf strResult.ToLower = "false" Then
                    Return 0
                Else
                    valueNotPresent = True
                    Return valueIfMissing
                End If
            Catch ex As System.Exception
                valueNotPresent = True
                Return valueIfMissing
            End Try
        End If

    End Function

    ''' <summary>
    ''' The function gets the name of the "value" attribute in section "sectionName".
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="valueIfMissing">Value to return if "sectionName" or "keyName" is missing.</param>
    ''' <param name="valueNotPresent">Set to True if "sectionName" or "keyName" is missing.  Returned ByRef.</param>
    ''' <return>The function returns the name of the "value" attribute as a Double.  If "value" is "true" returns -1.  If "value" is "false" returns 0.</return>
    Public Function GetParam(ByVal sectionName As String, ByVal keyName As String, ByVal valueIfMissing As Double, Optional ByRef valueNotPresent As Boolean = False) As Double
        Dim strResult As String
        Dim blnNotFound As Boolean = False

        strResult = Me.GetParam(sectionName, keyName, valueIfMissing.ToString, blnNotFound)
        If strResult Is Nothing OrElse blnNotFound Then
            valueNotPresent = True
            Return valueIfMissing
        Else
            valueNotPresent = False
            Try
                If IsNumeric(strResult) Then
                    Return CDbl(strResult)
                ElseIf strResult.ToLower = "true" Then
                    Return -1
                ElseIf strResult.ToLower = "false" Then
                    Return 0
                Else
                    valueNotPresent = True
                    Return valueIfMissing
                End If
            Catch ex As System.Exception
                valueNotPresent = True
                Return valueIfMissing
            End Try
        End If

    End Function

    ''' <summary>
    ''' The function sets the path to the Xml Settings File.
    ''' </summary>
    ''' <param name="XmlSettingsFilePath">The path to the XML settings file.</param>
    Public Sub SetIniFilePath(ByVal XmlSettingsFilePath As String)
        m_IniFilePath = XmlSettingsFilePath
    End Sub

    ''' <summary>
    ''' The function sets a new String value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As String) As Boolean
        Dim sectionNameInFile As String
        Dim keyNameInFile As String

        If Not mCaseSensitive Then
            sectionNameInFile = GetCachedSectionName(sectionName)
            If sectionNameInFile.Length > 0 Then
                keyNameInFile = GetCachedKeyName(sectionName, keyName)
                If keyNameInFile.Length > 0 Then
                    ' Section and Key are present; update them
                    Return m_iniFileAccessor.SetIniValue(sectionNameInFile, keyNameInFile, newValue)
                Else
                    ' Section is present, but the Key isn't; add teh key
                    Return m_iniFileAccessor.SetIniValue(sectionNameInFile, keyName, newValue)
                End If
            End If
        End If

        ' If we get here, then either mCaseSensitive = True or the section and key weren't found
        Return m_iniFileAccessor.SetIniValue(sectionName, keyName, newValue)

    End Function

    ''' <summary>
    ''' The function sets a new Boolean value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Boolean) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function sets a new Short value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Short) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function sets a new Integer value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Integer) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function sets a new Long value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Long) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function sets a new Single value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Single) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function sets a new Double value for the "value" attribute.
    ''' </summary>
    ''' <param name="sectionName">The name of the section.</param>
    ''' <param name="keyName">The name of the key.</param>
    ''' <param name="newValue">The new value for the "value".</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function SetParam(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As Double) As Boolean
        Return Me.SetParam(sectionName, keyName, CStr(newValue))
    End Function

    ''' <summary>
    ''' The function renames a section.
    ''' </summary>
    ''' <param name="sectionNameOld">The name of the old ini section name.</param>
    ''' <param name="sectionNameNew">The new name for the ini section.</param>
    ''' <return>The function returns a boolean that shows if the change was done.</return>
    Public Function RenameSection(ByVal sectionNameOld As String, ByVal sectionNameNew As String) As Boolean

        Dim strSectionName As String

        If Not mCaseSensitive Then
            strSectionName = GetCachedSectionName(sectionNameOld)
            If strSectionName.Length > 0 Then
                Return m_iniFileAccessor.SetIniSection(strSectionName, sectionNameNew)
            End If
        End If

        ' If we get here, then either mCaseSensitive = True or the section wasn't found using GetCachedSectionName
        Return m_iniFileAccessor.SetIniSection(sectionNameOld, sectionNameNew)

    End Function

    Private Sub FileAccessorInfoMessageEvent(ByVal msg As String) Handles m_iniFileAccessor.InformationMessage
        RaiseEvent InformationMessage(msg)
    End Sub


    ''' <summary>
    ''' Tools to manipulates INI files.
    ''' </summary>
    Protected Class IniFileReader

        Enum IniItemTypeEnum
            GetKeys = 0
            GetValues = 1
            GetKeysAndValues = 2
        End Enum

        Private m_IniFilename As String
        Private m_XmlDoc As System.Xml.XmlDocument
        Private unattachedComments As ArrayList = New ArrayList
        Private sections As System.Collections.Specialized.StringCollection = New System.Collections.Specialized.StringCollection
        Private m_CaseSensitive As Boolean = False
        Private m_SaveFilename As String
        Private m_initialized As Boolean = False

        Public NotifyOnEvent As Boolean
        Public NotifyOnException As Boolean

        Public Event InformationMessage(ByVal msg As String)

        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="IniFileName">The name of the ini file.</param>
        Public Sub New(ByVal IniFilename As String)
            NotifyOnException = False
            InitIniFileReader(IniFilename, False)
        End Sub
        ''' <summary>Initializes a new instance of the IniFileReader.</summary>
        ''' <param name="IniFileName">The name of the ini file.</param>
        ''' <param name="IsCaseSensitive">Case sensitive as boolean.</param>
        Public Sub New(ByVal IniFilename As String, ByVal IsCaseSensitive As Boolean)
            NotifyOnException = True
            InitIniFileReader(IniFilename, IsCaseSensitive)
        End Sub
        ''' <summary>
        ''' This routine is called by each of the constructors to make the actual assignments.
        ''' </summary>
        Private Sub InitIniFileReader(ByVal IniFilename As String, ByVal IsCaseSensitive As Boolean)
            Dim fi As System.IO.FileInfo
            Dim s As String
            Dim tr As System.IO.TextReader
            m_CaseSensitive = IsCaseSensitive
            m_XmlDoc = New System.Xml.XmlDocument

            If ((IniFilename Is Nothing) OrElse (IniFilename.Trim() = "")) Then
                Return
            End If
            ' try to load the file as an XML file
            Try
                m_XmlDoc.Load(IniFilename)
                UpdateSections()
                m_IniFilename = IniFilename
                m_initialized = True

            Catch
                ' load the default XML
                m_XmlDoc.LoadXml("<?xml version=""1.0"" encoding=""UTF-8""?><sections></sections>")
                Try
                    fi = New System.IO.FileInfo(IniFilename)
                    If (fi.Exists) Then
                        tr = fi.OpenText
                        s = tr.ReadLine()
                        Do While Not s Is Nothing
                            ParseLineXml(s, m_XmlDoc)
                            s = tr.ReadLine()
                        Loop
                        m_IniFilename = IniFilename
                        m_initialized = True
                    Else
                        m_XmlDoc.Save(IniFilename)
                        m_IniFilename = IniFilename
                        m_initialized = True
                    End If
                Catch e As System.Exception
                    If NotifyOnException Then
                        Throw New System.Exception("Failed to read INI file.")
                    End If
                Finally
                    If (Not tr Is Nothing) Then
                        tr.Close()
                    End If
                End Try
            End Try
        End Sub
        ''' <summary>
        ''' This routine returns the name of the ini file.
        ''' </summary>
        ''' <return>The function returns the name of ini file.</return>
        Public ReadOnly Property IniFilename() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return (m_IniFilename)
            End Get
        End Property
        ''' <summary>
        ''' This routine returns a boolean showing if the file was initialized or not.
        ''' </summary>
        ''' <return>The function returns a Boolean.</return>
        Public ReadOnly Property Initialized() As Boolean
            Get
                Return m_initialized
            End Get
        End Property
        ''' <summary>
        ''' This routine returns a boolean showing if the name is case sensitive or not.
        ''' </summary>
        ''' <return>The function returns a Boolean.</return>
        Public ReadOnly Property CaseSensitive() As Boolean
            Get
                Return m_CaseSensitive
            End Get
        End Property

        ''' <summary>
        ''' This routine sets a name.
        ''' </summary>
        ''' <param name="aName">The name to be set.</param>
        ''' <return>The function returns a string.</return>
        Private Function SetNameCase(ByVal aName As String) As String
            If (CaseSensitive) Then
                Return aName
            Else
                Return aName.ToLower()
            End If
        End Function
        ''' <summary>
        ''' TBD.
        ''' </summary>
        Private Function GetRoot() As System.Xml.XmlElement
            Return m_XmlDoc.DocumentElement
        End Function

        ''' <summary>
        ''' The function gets the last section.
        ''' </summary>
        ''' <return>The function returns the last section as System.Xml.XmlElement.</return>
        Private Function GetLastSection() As System.Xml.XmlElement
            If sections.Count = 0 Then
                Return GetRoot()
            Else
                Return GetSection(sections(sections.Count - 1))
            End If
        End Function
        ''' <summary>
        ''' The function gets a section as System.Xml.XmlElement.
        ''' </summary>
        ''' <param name="sectionName">The name of a section.</param>
        ''' <return>The function returns a section as System.Xml.XmlElement.</return>
        Private Function GetSection(ByVal sectionName As String) As System.Xml.XmlElement
            If (Not (sectionName = Nothing)) AndAlso (sectionName <> "") Then
                sectionName = SetNameCase(sectionName)
                Return CType(m_XmlDoc.SelectSingleNode("//section[@name='" & sectionName & "']"), System.Xml.XmlElement)
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function gets an item.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <return>The function returns a XML element.</return>
        Private Function GetItem(ByVal sectionName As String, ByVal keyName As String) As System.Xml.XmlElement
            Dim section As System.Xml.XmlElement
            If (Not keyName Is Nothing) AndAlso (keyName <> "") Then
                keyName = SetNameCase(keyName)
                section = GetSection(sectionName)
                If (Not section Is Nothing) Then
                    Return CType(section.SelectSingleNode("item[@key='" + keyName + "']"), System.Xml.XmlElement)
                End If
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function sets the ini section name.
        ''' </summary>
        ''' <param name="oldSection">The name of the old ini section name.</param>
        ''' <param name="newSection">The new name for the ini section.</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniSection(ByVal oldSection As String, ByVal newSection As String) As Boolean
            Dim section As System.Xml.XmlElement
            If Not Initialized Then
                Throw New IniFileReaderNotInitializedException
            End If
            If (Not newSection Is Nothing) AndAlso (newSection <> "") Then
                section = GetSection(oldSection)
                If (Not (section Is Nothing)) Then
                    section.SetAttribute("name", SetNameCase(newSection))
                    UpdateSections()
                    Return True
                End If
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function sets a new value for the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "value".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniValue(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As String) As Boolean
            Dim item As System.Xml.XmlElement
            Dim section As System.Xml.XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            section = GetSection(sectionName)
            If section Is Nothing Then
                If CreateSection(sectionName) Then
                    section = GetSection(sectionName)
                    ' exit if keyName is Nothing or blank
                    If (keyName Is Nothing) OrElse (keyName = "") Then
                        Return True
                    End If
                Else
                    ' can't create section
                    Return False
                End If
            End If
            If keyName Is Nothing Then
                ' delete the section
                Return DeleteSection(sectionName)
            End If

            item = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                If newValue Is Nothing Then
                    ' delete this item
                    Return DeleteItem(sectionName, keyName)
                Else
                    ' add or update the value attribute
                    item.SetAttribute("value", newValue)
                    Return True
                End If
            Else
                ' try to create the item
                If (keyName <> "") AndAlso (Not newValue Is Nothing) Then
                    ' construct a new item (blank values are OK)
                    item = m_XmlDoc.CreateElement("item")
                    item.SetAttribute("key", SetNameCase(keyName))
                    item.SetAttribute("value", newValue)
                    section.AppendChild(item)
                    Return True
                End If
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function deletes a section in the file.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a boolean that shows if the delete was completed.</return>
        Private Function DeleteSection(ByVal sectionName As String) As Boolean
            Dim section As System.Xml.XmlElement = GetSection(sectionName)
            If Not section Is Nothing Then
                section.ParentNode.RemoveChild(section)
                UpdateSections()
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function deletes a item in a specific section.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <return>The function returns a boolean that shows if the delete was completed.</return>
        Private Function DeleteItem(ByVal sectionName As String, ByVal keyName As String) As Boolean
            Dim item As System.Xml.XmlElement = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                item.ParentNode.RemoveChild(item)
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function sets a new value for the "key" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value for the "key".</param>
        ''' <return>The function returns a boolean that shows if the change was done.</return>
        Public Function SetIniKey(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As String) As Boolean
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim item As System.Xml.XmlElement = GetItem(sectionName, keyName)
            If Not item Is Nothing Then
                item.SetAttribute("key", SetNameCase(newValue))
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The function gets the name of the "value" attribute.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        '''<return>The function returns the name of the "value" attribute.</return>
        Public Function GetIniValue(ByVal sectionName As String, ByVal keyName As String) As String
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim N As System.Xml.XmlNode = GetItem(sectionName, keyName)
            If Not N Is Nothing Then
                Return (N.Attributes.GetNamedItem("value").Value)
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' The function gets the comments for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        '''<return>The function returns a string collection with comments</return>
        Public Function GetIniComments(ByVal sectionName As String) As System.Collections.Specialized.StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim sc As System.Collections.Specialized.StringCollection = New System.Collections.Specialized.StringCollection
            Dim target As System.Xml.XmlNode
            Dim nodes As System.Xml.XmlNodeList
            Dim N As System.Xml.XmlNode
            If sectionName Is Nothing Then
                target = m_XmlDoc.DocumentElement
            Else
                target = GetSection(sectionName)
            End If
            If Not target Is Nothing Then
                nodes = target.SelectNodes("comment")
                If nodes.Count > 0 Then
                    For Each N In nodes
                        sc.Add(N.InnerText)
                    Next
                End If
            End If
            Return sc
        End Function

        ''' <summary>
        ''' The function sets a the comments for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="comments">A string collection.</param>
        '''<return>The function returns a Boolean that shows if the change was done.</return>
        Public Function SetIniComments(ByVal sectionName As String, ByVal comments As System.Collections.Specialized.StringCollection) As Boolean
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Dim target As System.Xml.XmlNode
            Dim nodes As System.Xml.XmlNodeList
            Dim N As System.Xml.XmlNode
            Dim s As String
            Dim NLastComment As System.Xml.XmlElement
            If sectionName Is Nothing Then
                target = m_XmlDoc.DocumentElement
            Else
                target = GetSection(sectionName)
            End If
            If Not target Is Nothing Then
                nodes = target.SelectNodes("comment")
                For Each N In nodes
                    target.RemoveChild(N)
                Next
                For Each s In comments
                    N = m_XmlDoc.CreateElement("comment")
                    N.InnerText = s
                    NLastComment = CType(target.SelectSingleNode("comment[last()]"), System.Xml.XmlElement)
                    If NLastComment Is Nothing Then
                        target.PrependChild(N)
                    Else
                        target.InsertAfter(N, NLastComment)
                    End If
                Next
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' The subroutine updades the sections.
        ''' </summary>
        Private Sub UpdateSections()
            sections = New System.Collections.Specialized.StringCollection
            Dim N As System.Xml.XmlElement
            For Each N In m_XmlDoc.SelectNodes("sections/section")
                sections.Add(N.GetAttribute("name"))
            Next
        End Sub
        ''' <summary>
        ''' The subroutine gets the sections.
        ''' </summary>
        ''' <return>The subroutine returns a strin collection of sections.</return>
        Public ReadOnly Property AllSections() As System.Collections.Specialized.StringCollection
            Get
                If Not Initialized Then
                    Throw New IniFileReaderNotInitializedException
                End If
                Return sections
            End Get
        End Property

        ''' <summary>
        ''' The function gets a collection of items for a section name.
        ''' </summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="itemType">Item type.</param>
        ''' <return>The function returns a string colection of items in a section.</return>
        Private Function GetItemsInSection(ByVal sectionName As String, ByVal itemType As IniItemTypeEnum) As System.Collections.Specialized.StringCollection
            Dim nodes As System.Xml.XmlNodeList
            Dim items As System.Collections.Specialized.StringCollection = New System.Collections.Specialized.StringCollection
            Dim section As System.Xml.XmlNode = GetSection(sectionName)
            Dim N As System.Xml.XmlNode
            If section Is Nothing Then
                Return Nothing
            Else
                nodes = section.SelectNodes("item")
                If nodes.Count > 0 Then
                    For Each N In nodes
                        Select Case itemType
                            Case IniItemTypeEnum.GetKeys
                                items.Add(N.Attributes.GetNamedItem("key").Value)
                            Case IniItemTypeEnum.GetValues
                                items.Add(N.Attributes.GetNamedItem("value").Value)
                            Case IniItemTypeEnum.GetKeysAndValues
                                items.Add(N.Attributes.GetNamedItem("key").Value & "=" & _
                                N.Attributes.GetNamedItem("value").Value)
                        End Select
                    Next
                End If
                Return items
            End If
        End Function
        ''' <summary>The funtions gets a collection of keys in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the keys in a section.</return>
        Public Function AllKeysInSection(ByVal sectionName As String) As System.Collections.Specialized.StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return GetItemsInSection(sectionName, IniItemTypeEnum.GetKeys)
        End Function
        ''' <summary>The funtions gets a collection of values in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the values in a section.</return>
        Public Function AllValuesInSection(ByVal sectionName As String) As System.Collections.Specialized.StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return GetItemsInSection(sectionName, IniItemTypeEnum.GetValues)
        End Function
        ''' <summary>The funtions gets a collection of items in a section.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <return>The function returns a string colection of all the items in a section.</return>
        Public Function AllItemsInSection(ByVal sectionName As String) As System.Collections.Specialized.StringCollection
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Return (GetItemsInSection(sectionName, IniItemTypeEnum.GetKeysAndValues))
        End Function
        ''' <summary>The funtions gets a custom attribute name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="attributeName">The name of the attribute.</param>
        ''' <return>The function returns a string.</return>
        Public Function GetCustomIniAttribute(ByVal sectionName As String, ByVal keyName As String, ByVal attributeName As String) As String
            Dim N As System.Xml.XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If (Not attributeName Is Nothing) AndAlso (attributeName <> "") Then
                N = GetItem(sectionName, keyName)
                If Not N Is Nothing Then
                    attributeName = SetNameCase(attributeName)
                    Return N.GetAttribute(attributeName)
                End If
            End If
            Return Nothing
        End Function
        ''' <summary>The funtions sets a custom attribute name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="attributeName">The name of the attribute.</param>
        ''' <param name="attributeValue">The value of the attribute.</param>
        ''' <return>The function returns a Boolean.</return>
        Public Function SetCustomIniAttribute(ByVal sectionName As String, ByVal keyName As String, ByVal attributeName As String, ByVal attributeValue As String) As Boolean
            Dim N As System.Xml.XmlElement
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If attributeName <> "" Then
                N = GetItem(sectionName, keyName)
                If Not N Is Nothing Then
                    Try
                        If attributeValue Is Nothing Then
                            ' delete the attribute
                            N.RemoveAttribute(attributeName)
                            Return True
                        Else
                            attributeName = SetNameCase(attributeName)
                            N.SetAttribute(attributeName, attributeValue)
                            Return True
                        End If

                    Catch e As System.Exception
                        If NotifyOnException Then
                            Throw New System.Exception("Failed to create item.")
                        End If
                    End Try
                End If
                Return False
            End If
        End Function
        ''' <summary>The funtions creates a section name.</summary>
        ''' <param name="sectionName">The name of the section to be created.</param>
        ''' <return>The function returns a Boolean.</return>
        Private Function CreateSection(ByVal sectionName As String) As Boolean
            Dim N As System.Xml.XmlElement
            Dim Natt As System.Xml.XmlAttribute
            If (Not sectionName Is Nothing) AndAlso (sectionName <> "") Then
                sectionName = SetNameCase(sectionName)
                Try
                    N = m_XmlDoc.CreateElement("section")
                    Natt = m_XmlDoc.CreateAttribute("name")
                    Natt.Value = SetNameCase(sectionName)
                    N.Attributes.SetNamedItem(Natt)
                    m_XmlDoc.DocumentElement.AppendChild(N)
                    sections.Add(Natt.Value)
                    Return True
                Catch e As System.Exception
                    If NotifyOnException Then
                        Throw New System.Exception("Failed to create item.")
                    End If
                    Return False
                End Try
            End If
            Return False
        End Function
        ''' <summary>The funtions creates a section name.</summary>
        ''' <param name="sectionName">The name of the section.</param>
        ''' <param name="keyName">The name of the key.</param>
        ''' <param name="newValue">The new value to be created.</param>
        ''' <return>The function returns a Boolean.</return>
        Private Function CreateItem(ByVal sectionName As String, ByVal keyName As String, ByVal newValue As String) As Boolean
            Dim item As System.Xml.XmlElement
            Dim section As System.Xml.XmlElement
            Try
                section = GetSection(sectionName)
                If Not section Is Nothing Then
                    item = m_XmlDoc.CreateElement("item")
                    item.SetAttribute("key", keyName)
                    item.SetAttribute("newValue", newValue)
                    section.AppendChild(item)
                    Return True
                End If
                Return False
            Catch e As System.Exception
                If NotifyOnException Then
                    Throw New System.Exception("Failed to create item.")
                End If
                Return False
            End Try
        End Function
        ''' <summary>It parses a string and adds atribbutes to the System.Xml.XmlDocument.</summary>
        ''' <param name="s">The name of the string to be parse.</param>
        ''' <param name="doc">The name of the System.Xml.XmlDocument.</param>
        Private Sub ParseLineXml(ByVal s As String, ByVal doc As System.Xml.XmlDocument)
            Dim key As String
            Dim value As String
            Dim N As System.Xml.XmlElement
            Dim Natt As System.Xml.XmlAttribute
            Dim parts() As String
            s.TrimStart()

            If s.Length = 0 Then
                Return
            End If
            Select Case (s.Substring(0, 1))
                Case "["
                    ' this is a section
                    ' trim the first and last characters
                    s = s.TrimStart("["c)
                    s = s.TrimEnd("]"c)
                    ' create a new section element
                    CreateSection(s)
                Case ";"
                    ' new comment
                    N = doc.CreateElement("comment")
                    N.InnerText = s.Substring(1)
                    GetLastSection().AppendChild(N)
                Case Else
                    ' split the string on the "=" sign, if present
                    If (s.IndexOf("=") > 0) Then
                        parts = s.Split("="c)
                        key = parts(0).Trim()
                        value = parts(1).Trim()
                    Else
                        key = s
                        value = ""
                    End If
                    N = doc.CreateElement("item")
                    Natt = doc.CreateAttribute("key")
                    Natt.Value = SetNameCase(key)
                    N.Attributes.SetNamedItem(Natt)
                    Natt = doc.CreateAttribute("value")
                    Natt.Value = value
                    N.Attributes.SetNamedItem(Natt)
                    GetLastSection().AppendChild(N)
            End Select

        End Sub
        ''' <summary>It Sets or Gets the output file name.</summary>
        Public Property OutputFilename() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return m_SaveFilename
            End Get
            Set(ByVal Value As String)
                Dim fi As System.IO.FileInfo
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                fi = New System.IO.FileInfo(Value)
                If Not fi.Directory.Exists Then
                    If NotifyOnException Then
                        Throw New System.Exception("Invalid path for output file.")
                    End If
                Else
                    m_SaveFilename = Value
                End If
            End Set
        End Property
        ''' <summary>It saves the data to the Xml output file.</summary>
        Public Sub Save()
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            If Not OutputFilename Is Nothing AndAlso Not m_XmlDoc Is Nothing Then
                Dim fi As System.IO.FileInfo = New System.IO.FileInfo(OutputFilename)
                If Not fi.Directory.Exists Then
                    If NotifyOnException Then
                        Throw New System.Exception("Invalid path.")
                    End If
                    Return
                End If
                If fi.Exists Then
                    fi.Delete()
                    m_XmlDoc.Save(OutputFilename)
                Else
                    m_XmlDoc.Save(OutputFilename)
                End If
                If NotifyOnEvent Then
                    RaiseEvent InformationMessage("File save complete.")
                End If
            Else
                If NotifyOnException Then
                    Throw New System.Exception("Not Output File name specified.")
                End If
            End If
        End Sub
        ''' <summary>It transforms a XML file to an INI file.</summary>
        ''' <return>The function returns document formatted as a string.</return>
        Public Function AsIniFile() As String
            If Not Initialized Then Throw New IniFileReaderNotInitializedException
            Try
                Dim xsl As System.Xml.Xsl.XslTransform = New System.Xml.Xsl.XslTransform
                Dim resolver As System.Xml.XmlUrlResolver = New System.Xml.XmlUrlResolver
                xsl.Load("c:\\XMLToIni.xslt")
                Dim sb As System.Text.StringBuilder = New System.Text.StringBuilder
                Dim sw As System.IO.StringWriter = New System.io.StringWriter(sb)
                xsl.Transform(m_XmlDoc, Nothing, sw, resolver)
                sw.Close()
                Return sb.ToString
            Catch e As System.Exception
                If NotifyOnException Then
                    Throw New System.Exception("Error transforming XML to INI file.")
                End If
                Return Nothing
            End Try
        End Function
        ''' <summary>It gets the System.Xml.XmlDocument.</summary>
        Public ReadOnly Property XmlDoc() As System.Xml.XmlDocument
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Return m_XmlDoc
            End Get
        End Property

        ''' <summary>Converts an XML document to a string.</summary>
        ''' <return>It returns the XML document formatted as a string.</return>
        Public ReadOnly Property XML() As String
            Get
                If Not Initialized Then Throw New IniFileReaderNotInitializedException
                Dim sb As System.Text.StringBuilder = New System.Text.StringBuilder
                Dim sw As System.IO.StringWriter = New System.IO.StringWriter(sb)
                Dim xw As System.Xml.XmlTextWriter = New System.Xml.XmlTextWriter(sw)
                xw.Indentation = 3
                xw.Formatting = System.Xml.Formatting.Indented
                m_XmlDoc.WriteContentTo(xw)
                xw.Close()
                sw.Close()
                Return sb.ToString()
            End Get
        End Property

    End Class

    Public Class IniFileReaderNotInitializedException
        Inherits System.ApplicationException
        Public Overrides ReadOnly Property Message() As String
            Get
                Return "The IniFileReader instance has not been properly initialized."
            End Get
        End Property
    End Class

End Class

