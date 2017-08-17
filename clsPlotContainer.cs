using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OxyPlot;
using OxyPlot.Axes;

namespace MSFileInfoScanner
{
    using System.Globalization;

    public class clsPlotContainer
    {
        private const double PIXELS_PER_DIP = 1.25;

        private enum ImageFileFormat
        {
            PNG,
            JPG
        }

        public string AnnotationBottomLeft { get; set; }

        public string AnnotationBottomRight { get; set; }

        public OxyPlot.PlotModel Plot => mPlot;

        public int FontSizeBase { get; set; }

        public int SeriesCount {
            get {
                if (mPlot == null) {
                    return 0;
                }
                return mPlot.Series.Count;
            }
        }

        public bool PlottingDeisotopedData { get; set; }

        private readonly OxyPlot.PlotModel mPlot;

        private Dictionary<string, OxyPalette> mColorGradients;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="thePlot"></param>
        /// <remarks></remarks>
        public clsPlotContainer(OxyPlot.PlotModel thePlot)
        {
            mPlot = thePlot;
            FontSizeBase = 16;
        }

        /// <summary>
        /// Save the plot, along with any defined annotations, to a png file
        /// </summary>
        /// <param name="pngFilePath">Output file path</param>
        /// <param name="width">PNG file width, in pixels</param>
        /// <param name="height">PNG file height, in pixels</param>
        /// <param name="resolution">Image resolution, in dots per inch</param>
        /// <remarks></remarks>
        public void SaveToPNG(string pngFilePath, int width, int height, int resolution)
        {
            if (string.IsNullOrWhiteSpace(pngFilePath)) {
                throw new ArgumentOutOfRangeException(pngFilePath, "Filename cannot be empty");
            }

            if (Path.GetExtension(pngFilePath).ToLower() != ".png") {
                pngFilePath += ".png";
            }

            SaveToFileLoop(pngFilePath, ImageFileFormat.PNG, width, height, resolution);

        }

        public void SaveToJPG(string jpgFilePath, int width, int height, int resolution)
        {
            if (string.IsNullOrWhiteSpace(jpgFilePath)) {
                throw new ArgumentOutOfRangeException(jpgFilePath, "Filename cannot be empty");
            }

            if (Path.GetExtension(jpgFilePath).ToLower() != ".png") {
                jpgFilePath += ".jpg";
            }

            SaveToFileLoop(jpgFilePath, ImageFileFormat.JPG, width, height, resolution);

        }

        private void SaveToFileLoop(string imageFilePath, ImageFileFormat fileFormat, int width, int height, int resolution)
        {
            if (mColorGradients == null || mColorGradients.Count == 0) {
                SaveToFile(imageFilePath, fileFormat, width, height, resolution);
                return;
            }

            foreach (var colorGradient in mColorGradients) {
                var matchFound = false;

                foreach (var axis in mPlot.Axes) {
                    var newAxis = axis as LinearColorAxis;

                    if (newAxis == null) {
                        continue;
                    }

                    matchFound = true;
                    newAxis.Palette = colorGradient.Value;
                    newAxis.IsAxisVisible = true;

                    var fiBaseImageFile = new FileInfo(imageFilePath);

                    var newFileName = Path.GetFileNameWithoutExtension(fiBaseImageFile.Name) + "_Gradient_" + colorGradient.Key + fiBaseImageFile.Extension;
                    if (fiBaseImageFile.DirectoryName != null)
                    {
                        newFileName = Path.Combine(fiBaseImageFile.DirectoryName, newFileName);
                    }

                    SaveToFile(newFileName, fileFormat, width, height, resolution);
                }

                if (!matchFound) {
                    SaveToFile(imageFilePath, fileFormat, width, height, resolution);
                    return;
                }
            }

        }

        private void SaveToFile(string imageFilePath, ImageFileFormat fileFormat, int width, int height, int resolution)
        {
            Console.WriteLine("Saving " + Path.GetFileName(imageFilePath));

            // Note that this operation can be slow if there are over 100,000 data points
            var plotBitmap = OxyPlot.Wpf.PngExporter.ExportToBitmap(mPlot, width, height, OxyPlot.OxyColors.White, resolution);

            var drawVisual = new DrawingVisual();
            using (var drawContext = drawVisual.RenderOpen()) {

                var myCanvas = new Rect(0, 0, width, height);

                drawContext.DrawImage(plotBitmap, myCanvas);

                // Add a frame
                var rectPen = new Pen
                {
                    Brush = new SolidColorBrush(Colors.Black),
                    Thickness = 2
                };

                drawContext.DrawRectangle(null, rectPen, myCanvas);

                if (!string.IsNullOrWhiteSpace(AnnotationBottomLeft)) {
                    AddText(AnnotationBottomLeft, drawContext, width, height, HorizontalAlignment.Left, VerticalAlignment.Bottom, 5);
                }

                if (!string.IsNullOrWhiteSpace(AnnotationBottomRight)) {
                    AddText(AnnotationBottomRight, drawContext, width, height, HorizontalAlignment.Right, VerticalAlignment.Bottom, 5);
                }

                if (PlottingDeisotopedData) {
                    AddDeisotopedDataLegend(drawContext, width, height, 10, -30, 25);
                }
            }

            const int DPI = 96;

            var target = new RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Default);
            target.Render(drawVisual);

            BitmapEncoder encoder;

            switch (fileFormat) {
                case ImageFileFormat.PNG:
                    encoder = new PngBitmapEncoder();

                    break;
                case ImageFileFormat.JPG:
                    encoder = new JpegBitmapEncoder();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(fileFormat.ToString(), "Unrecognized value: " + fileFormat.ToString());
            }

            encoder.Frames.Add(BitmapFrame.Create(target));

            using (var outputStream = new FileStream(imageFilePath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(outputStream);
            }
        }

        public void AddGradients(Dictionary<string, OxyPalette> colorGradients)
        {
            mColorGradients = colorGradients;
        }

        private void AddDeisotopedDataLegend(DrawingContext drawContext, int canvasWidth, int canvasHeight, int offsetLeft, int offsetTop, int spacing)
        {
            const int CHARGE_START = 1;
            const int CHARGE_END = 6;

            var usCulture = CultureInfo.GetCultureInfo("en-us");
            // var fontTypeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal);
            var fontTypeface = new Typeface("Arial");

            var fontSizeEm = FontSizeBase + 3;

            // Write out the text 1+  2+  3+  4+  5+  6+  

            // Create a box for the legend
            var rectPen = new Pen
            {
                Brush = new SolidColorBrush(Colors.Black),
                Thickness = 1
            };

            for (var chargeState = CHARGE_START; chargeState <= CHARGE_END; chargeState++) {
                var newBrush = new SolidColorBrush(GetColorByCharge(chargeState));

                var newText = new FormattedText(chargeState + "+", usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, newBrush, null, PIXELS_PER_DIP);

                var textRect = new Rect(0, 0, canvasWidth, canvasHeight);
                var position = textRect.Location;

                position.X = mPlot.PlotArea.Left + offsetLeft + (chargeState - 1) * (newText.Width + spacing);
                position.Y = mPlot.PlotArea.Top + offsetTop;

                if (chargeState == CHARGE_START) {
                    var legendBox = new Rect(position.X - 10, position.Y, CHARGE_END * (newText.Width + spacing) - spacing / 4.0, newText.Height);
                    drawContext.DrawRectangle(null, rectPen, legendBox);
                }

                drawContext.DrawText(newText, position);
            }

        }

        private void AddText(string textToAdd, DrawingContext drawContext, int canvasWidth, int canvasHeight, HorizontalAlignment hAlign, VerticalAlignment vAlign, int padding)
        {
            var usCulture = CultureInfo.GetCultureInfo("en-us");
            // var fontTypeface = new Typeface(new FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal);
            var fontTypeface = new Typeface("Arial");

            var fontSizeEm = FontSizeBase + 1;

            var newText = new FormattedText(textToAdd, usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, Brushes.Black, null, PIXELS_PER_DIP);

            var textRect = new Rect(0, 0, canvasWidth, canvasHeight);
            var position = textRect.Location;

            switch (hAlign) {
                case HorizontalAlignment.Left:
                    position.X += padding;

                    break;
                case HorizontalAlignment.Center:
                    position.X += (textRect.Width - newText.Width) / 2;

                    break;
                case HorizontalAlignment.Right:
                    position.X += textRect.Width - newText.Width - padding;
                    break;
            }

            switch (vAlign) {
                case VerticalAlignment.Top:
                    position.Y += padding;

                    break;
                case VerticalAlignment.Middle:
                    position.Y += (textRect.Height - newText.Height) / 2;

                    break;
                case VerticalAlignment.Bottom:
                    position.Y += textRect.Height - newText.Height - padding;
                    break;
            }

            drawContext.DrawText(newText, position);
        }

        public static Color GetColorByCharge(int charge)
        {
            Color seriesColor;
            switch (charge) {
                case 1:
                    seriesColor = Colors.MediumBlue;
                    break;
                case 2:
                    seriesColor = Colors.Red;
                    break;
                case 3:
                    seriesColor = Colors.Green;
                    break;
                case 4:
                    seriesColor = Colors.Magenta;
                    break;
                case 5:
                    seriesColor = Colors.SaddleBrown;
                    break;
                case 6:
                    seriesColor = Colors.Indigo;
                    break;
                case 7:
                    seriesColor = Colors.LimeGreen;
                    break;
                case 8:
                    seriesColor = Colors.CornflowerBlue;
                    break;
                default:
                    seriesColor = Colors.Gray;
                    break;
            }

            return seriesColor;

        }

        private double PointSizeToEm(int fontSizePoints)
        {
            var fontSizeEm = fontSizePoints / 12;
            return fontSizeEm;
        }

        private int PointSizeToPixels(int fontSizePoints)
        {
            var fontSizePixels = fontSizePoints * 1.33;
            return (int)Math.Round(fontSizePixels, 0);
        }

    }
}

