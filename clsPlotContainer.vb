
Imports System.IO
Imports OxyPlot
Imports OxyPlot.Axes

Public Class clsPlotContainer

    Protected Enum ImageFileFormat
        PNG
        JPG
    End Enum

    Public Property AnnotationBottomLeft As String

    Public Property AnnotationBottomRight As String

    Public ReadOnly Property Plot As OxyPlot.PlotModel
        Get
            Return mPlot
        End Get
    End Property

    Public Property FontSizeBase As Integer

    Public ReadOnly Property SeriesCount As Integer
        Get
            If mPlot Is Nothing Then
                Return 0
            End If
            Return mPlot.Series.Count
        End Get
    End Property

    Public Property PlottingDeisotopedData As Boolean


    Protected mPlot As OxyPlot.PlotModel
    Protected mColorGradients As Dictionary(Of String, OxyPalette)

    ''' <summary>
    ''' Constructor
    ''' </summary>
    ''' <param name="thePlot"></param>
    ''' <remarks></remarks>
    Public Sub New(thePlot As OxyPlot.PlotModel)
        mPlot = thePlot
        FontSizeBase = 16
    End Sub

    ''' <summary>
    ''' Save the plot, along with any defined annotations, to a png file
    ''' </summary>
    ''' <param name="pngFilePath">Output file path</param>
    ''' <param name="width">PNG file width, in pixels</param>
    ''' <param name="height">PNG file height, in pixels</param>
    ''' <param name="resolution">Image resolution, in dots per inch</param>
    ''' <remarks></remarks>
    Public Sub SaveToPNG(pngFilePath As String, width As Integer, height As Integer, resolution As Integer)
        If String.IsNullOrWhiteSpace(pngFilePath) Then
            Throw New ArgumentOutOfRangeException(pngFilePath, "Filename cannot be empty")
        End If

        If Path.GetExtension(pngFilePath).ToLower() <> ".png" Then
            pngFilePath &= ".png"
        End If

        SaveToFileLoop(pngFilePath, ImageFileFormat.PNG, width, height, resolution)

    End Sub

    Public Sub SaveToJPG(jpgFilePath As String, width As Integer, height As Integer, resolution As Integer)
        If String.IsNullOrWhiteSpace(jpgFilePath) Then
            Throw New ArgumentOutOfRangeException(jpgFilePath, "Filename cannot be empty")
        End If

        If Path.GetExtension(jpgFilePath).ToLower() <> ".png" Then
            jpgFilePath &= ".jpg"
        End If

        SaveToFileLoop(jpgFilePath, ImageFileFormat.JPG, width, height, resolution)

    End Sub
    
    Protected Sub SaveToFileLoop(imageFilePath As String, fileFormat As ImageFileFormat, width As Integer, height As Integer, resolution As Integer)

        If mColorGradients Is Nothing OrElse mColorGradients.Count = 0 Then
            SaveToFile(imageFilePath, fileFormat, width, height, resolution)
            Return
        End If

        For Each colorGradient In mColorGradients
            Dim matchFound As Boolean

            For Each axis In mPlot.Axes
                Dim newAxis = TryCast(axis, LinearColorAxis)

                If newAxis Is Nothing Then
                    Continue For
                End If

                matchFound = True
                newAxis.Palette = colorGradient.Value
                newAxis.IsAxisVisible = True

                Dim fiBaseImageFile = New FileInfo(imageFilePath)

                Dim newFileName = Path.GetFileNameWithoutExtension(fiBaseImageFile.Name) & "_Gradient_" & colorGradient.Key & fiBaseImageFile.Extension
                newFileName = Path.Combine(fiBaseImageFile.DirectoryName, newFileName)

                SaveToFile(newFileName, fileFormat, width, height, resolution)
            Next

            If Not matchFound Then
                SaveToFile(imageFilePath, fileFormat, width, height, resolution)
                Return
            End If
        Next

    End Sub

    Protected Sub SaveToFile(imageFilePath As String, fileFormat As ImageFileFormat, width As Integer, height As Integer, resolution As Integer)

        Console.WriteLine("Saving " & Path.GetFileName(imageFilePath))

        ' Note that this operation can be slow if there are over 100,000 data points
        Dim plotBitmap = OxyPlot.Wpf.PngExporter.ExportToBitmap(mPlot, width, height, OxyPlot.OxyColors.White, resolution)

        Dim drawVisual = New DrawingVisual()
        Using drawContext = drawVisual.RenderOpen()

            Dim myCanvas = New Rect(0, 0, width, height)

            drawContext.DrawImage(plotBitmap, myCanvas)

            ' Add a frame
            Dim rectPen = New System.Windows.Media.Pen()
            rectPen.Brush = New SolidColorBrush(Colors.Black)
            rectPen.Thickness = 2

            drawContext.DrawRectangle(Nothing, rectPen, myCanvas)

            If Not String.IsNullOrWhiteSpace(AnnotationBottomLeft) Then
                AddText(AnnotationBottomLeft, drawContext, width, height, Windows.HorizontalAlignment.Left, Windows.VerticalAlignment.Bottom, 5)
            End If

            If Not String.IsNullOrWhiteSpace(AnnotationBottomRight) Then
                AddText(AnnotationBottomRight, drawContext, width, height, Windows.HorizontalAlignment.Right, Windows.VerticalAlignment.Bottom, 5)
            End If

            If PlottingDeisotopedData Then
                AddDeisotopedDataLegend(drawContext, width, height, 10, -30, 25)
            End If
        End Using

        Const DPI = 96

        Dim target = New RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Default)
        target.Render(drawVisual)

        Dim encoder As BitmapEncoder = Nothing

        Select Case fileFormat
            Case ImageFileFormat.PNG
                encoder = New PngBitmapEncoder()

            Case ImageFileFormat.JPG
                encoder = New JpegBitmapEncoder()
            Case Else
                Throw New ArgumentOutOfRangeException(fileFormat, "Unrecognized value: " + fileFormat.ToString())
        End Select

        If encoder IsNot Nothing Then
            encoder.Frames.Add(BitmapFrame.Create(target))

            Using outputStream = New FileStream(imageFilePath, FileMode.Create, FileAccess.Write)
                encoder.Save(outputStream)
            End Using
        End If

    End Sub

    Public Sub AddGradients(colorGradients As Dictionary(Of String, OxyPalette))
        mColorGradients = colorGradients
    End Sub

    Protected Sub AddDeisotopedDataLegend(
      drawContext As DrawingContext,
      canvasWidth As Integer,
      canvasHeight As Integer,
      offsetLeft As Integer,
      offsetTop As Integer,
      spacing As Integer)

        Const CHARGE_START = 1
        Const CHARGE_END = 6

        Dim usCulture = Globalization.CultureInfo.GetCultureInfo("en-us")
        ' Dim fontTypeface = New Typeface(New FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal)
        Dim fontTypeface = New Typeface("Arial")

        Dim fontSizeEm = FontSizeBase + 3

        ' Write out the text 1+  2+  3+  4+  5+  6+  

        ' Create a box for the legend
        Dim rectPen = New System.Windows.Media.Pen()
        rectPen.Brush = New SolidColorBrush(Colors.Black)
        rectPen.Thickness = 1

        For chargeState = CHARGE_START To CHARGE_END

            Dim newBrush = New SolidColorBrush(GetColorByCharge(chargeState))

            Dim newText = New FormattedText(chargeState & "+", usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, newBrush)

            Dim textRect = New Rect(0, 0, canvasWidth, canvasHeight)
            Dim position = textRect.Location

            position.X = mPlot.PlotArea.Left + offsetLeft + (chargeState - 1) * (newText.Width + spacing)
            position.Y = mPlot.PlotArea.Top + offsetTop

            If chargeState = CHARGE_START Then
                Dim legendBox = New Rect(position.X - 10, position.Y, CHARGE_END * (newText.Width + spacing) - spacing / 4, newText.Height)
                drawContext.DrawRectangle(Nothing, rectPen, legendBox)
            End If

            drawContext.DrawText(newText, position)
        Next

    End Sub

    Protected Sub AddText(
      textToAdd As String,
      drawContext As DrawingContext,
      canvasWidth As Integer,
      canvasHeight As Integer,
      hAlign As Windows.HorizontalAlignment,
      vAlign As Windows.VerticalAlignment,
      padding As Integer)

        Dim usCulture = Globalization.CultureInfo.GetCultureInfo("en-us")
        ' Dim fontTypeface = New Typeface(New FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal)
        Dim fontTypeface = New Typeface("Arial")

        Dim fontSizeEm = FontSizeBase + 1

        Dim newText = New FormattedText(textToAdd, usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, Brushes.Black)

        Dim textRect = New Rect(0, 0, canvasWidth, canvasHeight)
        Dim position = textRect.Location

        Select Case hAlign
            Case Windows.HorizontalAlignment.Left
                position.X += padding

            Case Windows.HorizontalAlignment.Center
                position.X += (textRect.Width - newText.Width) / 2

            Case Windows.HorizontalAlignment.Right
                position.X += textRect.Width - newText.Width - padding
        End Select

        Select Case vAlign
            Case Windows.VerticalAlignment.Top
                position.Y += padding

            Case Windows.VerticalAlignment.Center
                position.Y += (textRect.Height - newText.Height) / 2

            Case Windows.VerticalAlignment.Bottom
                position.Y += textRect.Height - newText.Height - padding
        End Select

        drawContext.DrawText(newText, position)
    End Sub

    Public Shared Function GetColorByCharge(charge As Integer) As Color
        Dim seriesColor As Color
        Select Case charge
            Case 1 : seriesColor = Colors.MediumBlue
            Case 2 : seriesColor = Colors.Red
            Case 3 : seriesColor = Colors.Green
            Case 4 : seriesColor = Colors.Magenta
            Case 5 : seriesColor = Colors.SaddleBrown
            Case 6 : seriesColor = Colors.Indigo
            Case 7 : seriesColor = Colors.LimeGreen
            Case 8 : seriesColor = Colors.CornflowerBlue
            Case Else : seriesColor = Colors.Gray
        End Select

        Return seriesColor

    End Function

    Protected Function PointSizeToEm(fontSizePoints As Integer) As Double
        Dim fontSizeEm = fontSizePoints / 12
        Return fontSizeEm
    End Function

    Protected Function PointSizeToPixels(fontSizePoints As Integer) As Integer
        Dim fontSizePixels = fontSizePoints * 1.33
        Return CInt(Math.Round(fontSizePixels, 0))
    End Function

End Class
