﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OxyPlot;
using OxyPlot.Wpf;
using LinearColorAxis = OxyPlot.Axes.LinearColorAxis;

// ReSharper disable RedundantNameQualifier

namespace MSFileInfoScanner.Plotting
{
    /// <summary>
    /// OxyPlot container
    /// </summary>
    internal class PlotContainer : PlotContainerBase
    {
        // Ignore Spelling: Arial, Jpg, png, OxyPlot

        private const double PIXELS_PER_DIP = 1.25;

        public const int DEFAULT_BASE_FONT_SIZE = 16;

        private enum ImageFileFormat
        {
            PNG,
            JPG
        }

        /// <summary>
        /// The OxyPlot plot model
        /// </summary>
        /// <remarks>Type OxyPlot.PlotModel</remarks>
        public PlotModel Plot { get; }

        public int FontSizeBase { get; set; }

        public override int SeriesCount
        {
            get
            {
                if (Plot == null)
                {
                    return 0;
                }
                return Plot.Series.Count;
            }
        }

        private Dictionary<string, OxyPalette> mColorGradients;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="thePlot">The plot (type OxyPlot.PlotModel)</param>
        /// <param name="writeDebug">When true, create a debug file that tracks processing steps</param>
        /// <param name="dataSource">Data source</param>
        public PlotContainer(
            PlotModel thePlot,
            bool writeDebug = false,
            string dataSource = "") : base(writeDebug, dataSource)
        {
            Plot = thePlot;
            FontSizeBase = DEFAULT_BASE_FONT_SIZE;
        }

        /// <summary>
        /// Save the plot, along with any defined annotations, to a png file
        /// </summary>
        /// <param name="pngFile">Output PNG file</param>
        /// <param name="width">PNG file width, in pixels</param>
        /// <param name="height">PNG file height, in pixels</param>
        /// <param name="resolution">Image resolution, in dots per inch</param>
        public override bool SaveToPNG(FileInfo pngFile, int width, int height, int resolution)
        {
            if (pngFile == null)
                throw new ArgumentNullException(nameof(pngFile), "PNG file instance cannot be blank");

            bool success;

            if (!pngFile.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                success = SaveToFileLoop(MSFileInfoScanner.GetFileInfo(pngFile.FullName + ".png"), ImageFileFormat.PNG, width, height, resolution);
            }
            else
            {
                success = SaveToFileLoop(pngFile, ImageFileFormat.PNG, width, height, resolution);
            }

            return success;
        }

        // ReSharper disable once UnusedMember.Global
        public bool SaveToJPG(FileInfo jpgFile, int width, int height, int resolution)
        {
            if (jpgFile == null)
                throw new ArgumentNullException(nameof(jpgFile), "JPG file instance cannot be blank");

            bool success;

            if (!jpgFile.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                success = SaveToFileLoop(MSFileInfoScanner.GetFileInfo(jpgFile.FullName + ".jpg"), ImageFileFormat.JPG, width, height, resolution);
            }
            else
            {
                success = SaveToFileLoop(jpgFile, ImageFileFormat.JPG, width, height, resolution);
            }

            return success;
        }

        private bool SaveToFileLoop(FileInfo imageFile, ImageFileFormat fileFormat, int width, int height, int resolution)
        {
            var successOverall = false;

            if (mColorGradients == null || mColorGradients.Count == 0)
            {
                var success = SaveToFile(imageFile, fileFormat, width, height, resolution);
                return success;
            }

            foreach (var colorGradient in mColorGradients)
            {
                var matchFound = false;

                foreach (var axis in Plot.Axes)
                {
                    if (axis is not LinearColorAxis newAxis)
                    {
                        continue;
                    }

                    matchFound = true;
                    newAxis.Palette = colorGradient.Value;
                    newAxis.IsAxisVisible = true;

                    var newFileName = Path.GetFileNameWithoutExtension(imageFile.Name) + "_Gradient_" + colorGradient.Key + imageFile.Extension;
                    FileInfo newFileInfo;

                    if (imageFile.DirectoryName != null)
                    {
                        newFileInfo = MSFileInfoScanner.GetFileInfo(Path.Combine(imageFile.DirectoryName, newFileName));
                    }
                    else
                    {
                        newFileInfo = MSFileInfoScanner.GetFileInfo(newFileName);
                    }

                    var success = SaveToFile(newFileInfo, fileFormat, width, height, resolution);

                    if (success)
                        successOverall = true;
                }

                if (!matchFound)
                {
                    return SaveToFile(imageFile, fileFormat, width, height, resolution);
                }
            }

            return successOverall;
        }

        private bool SaveToFile(FileInfo imageFile, ImageFileFormat fileFormat, int width, int height, int resolution)
        {
            if (imageFile == null)
                throw new ArgumentNullException(nameof(imageFile), "Image file instance cannot be blank");

            Console.WriteLine("Saving " + Path.GetFileName(imageFile.FullName));

            // Note that this operation can be slow if there are over 100,000 data points
            var plotBitmap = ExportToBitMap(Plot, width, height, resolution);

            var drawVisual = new DrawingVisual();
            using (var drawContext = drawVisual.RenderOpen())
            {
                var myCanvas = new Rect(0, 0, width, height);

                drawContext.DrawImage(plotBitmap, myCanvas);

                // Add a frame
                var rectPen = new Pen
                {
                    Brush = new SolidColorBrush(Colors.Black),
                    Thickness = 2
                };

                drawContext.DrawRectangle(null, rectPen, myCanvas);

                if (!string.IsNullOrWhiteSpace(AnnotationBottomLeft))
                {
                    AddText(AnnotationBottomLeft, drawContext, width, height, HorizontalAlignment.Left, VerticalAlignment.Bottom, 5);
                }

                if (!string.IsNullOrWhiteSpace(AnnotationBottomRight))
                {
                    AddText(AnnotationBottomRight, drawContext, width, height, HorizontalAlignment.Right, VerticalAlignment.Bottom, 5);
                }

                if (PlottingDeisotopedData)
                {
                    AddDeisotopedDataLegend(drawContext, width, height, 10, -30, 25);
                }
            }

            const int DPI = 96;

            var target = new RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Default);
            target.Render(drawVisual);

            BitmapEncoder encoder = fileFormat switch
            {
                ImageFileFormat.PNG => new PngBitmapEncoder(),
                ImageFileFormat.JPG => new JpegBitmapEncoder(),
                _ => throw new ArgumentOutOfRangeException(fileFormat.ToString(), "Unrecognized value: " + fileFormat)
            };

            // ReSharper disable once UnusedVariable
            var bitMap = BitmapFrame.Create(target);

            encoder.Frames.Add(BitmapFrame.Create(target));

            using var outputStream = new FileStream(imageFile.FullName, FileMode.Create, FileAccess.Write);
            encoder.Save(outputStream);

            return true;
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

            for (var chargeState = CHARGE_START; chargeState <= CHARGE_END; chargeState++)
            {
                var newBrush = new SolidColorBrush(GetColorByCharge(chargeState));

                var newText = new FormattedText(chargeState + "+", usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, newBrush, null, PIXELS_PER_DIP);

                var textRect = new Rect(0, 0, canvasWidth, canvasHeight);
                var position = textRect.Location;

                position.X = Plot.PlotArea.Left + offsetLeft + (chargeState - 1) * (newText.Width + spacing);
                position.Y = Plot.PlotArea.Top + offsetTop;

                if (chargeState == CHARGE_START)
                {
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

            switch (hAlign)
            {
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

            switch (vAlign)
            {
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

        private BitmapSource ExportToBitMap(IPlotModel plot, int width, int height, int resolution)
        {
            var exporter = new PngExporter
            {
                Width = width,
                Height = height,
                Resolution = resolution
            };

            return exporter.ExportToBitmap(plot);
        }

        public static Color GetColorByCharge(int charge)
        {
            return charge switch
            {
                1 => Colors.MediumBlue,
                2 => Colors.Red,
                3 => Colors.Green,
                4 => Colors.Magenta,
                5 => Colors.SaddleBrown,
                6 => Colors.DarkViolet,
                7 => Colors.LimeGreen,
                8 => Colors.CornflowerBlue,
                _ => Colors.Indigo
            };
        }

        // ReSharper disable once UnusedMember.Local
        private double PointSizeToEm(int fontSizePoints)
        {
            return fontSizePoints / 12.0;
        }

        // ReSharper disable once UnusedMember.Local
        private int PointSizeToPixels(int fontSizePoints)
        {
            var fontSizePixels = fontSizePoints * 1.33;
            return (int)Math.Round(fontSizePixels, 0);
        }
    }
}
