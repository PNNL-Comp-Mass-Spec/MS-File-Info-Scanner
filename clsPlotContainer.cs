using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

using System.IO;
using OxyPlot;
using OxyPlot.Axes;

public class clsPlotContainer
{

	protected enum ImageFileFormat
	{
		PNG,
		JPG
	}

	public string AnnotationBottomLeft { get; set; }

	public string AnnotationBottomRight { get; set; }

	public OxyPlot.PlotModel Plot {
		get { return mPlot; }
	}

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


	protected OxyPlot.PlotModel mPlot;

	protected Dictionary<string, OxyPalette> mColorGradients;
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


	protected void SaveToFileLoop(string imageFilePath, ImageFileFormat fileFormat, int width, int height, int resolution)
	{
		if (mColorGradients == null || mColorGradients.Count == 0) {
			SaveToFile(imageFilePath, fileFormat, width, height, resolution);
			return;
		}

		foreach (void colorGradient_loopVariable in mColorGradients) {
			colorGradient = colorGradient_loopVariable;
			bool matchFound = false;

			foreach (void axis_loopVariable in mPlot.Axes) {
				axis = axis_loopVariable;
				dynamic newAxis = axis as LinearColorAxis;

				if (newAxis == null) {
					continue;
				}

				matchFound = true;
				newAxis.Palette = colorGradient.Value;
				newAxis.IsAxisVisible = true;

				dynamic fiBaseImageFile = new FileInfo(imageFilePath);

				dynamic newFileName = Path.GetFileNameWithoutExtension(fiBaseImageFile.Name) + "_Gradient_" + colorGradient.Key + fiBaseImageFile.Extension;
				newFileName = Path.Combine(fiBaseImageFile.DirectoryName, newFileName);

				SaveToFile(newFileName, fileFormat, width, height, resolution);
			}

			if (!matchFound) {
				SaveToFile(imageFilePath, fileFormat, width, height, resolution);
				return;
			}
		}

	}


	protected void SaveToFile(string imageFilePath, ImageFileFormat fileFormat, int width, int height, int resolution)
	{
		Console.WriteLine("Saving " + Path.GetFileName(imageFilePath));

		// Note that this operation can be slow if there are over 100,000 data points
		dynamic plotBitmap = OxyPlot.Wpf.PngExporter.ExportToBitmap(mPlot, width, height, OxyPlot.OxyColors.White, resolution);

		dynamic drawVisual = new DrawingVisual();
		using (drawContext == drawVisual.RenderOpen()) {

			dynamic myCanvas = new Rect(0, 0, width, height);

			drawContext.DrawImage(plotBitmap, myCanvas);

			// Add a frame
			dynamic rectPen = new System.Windows.Media.Pen();
			rectPen.Brush = new SolidColorBrush(Colors.Black);
			rectPen.Thickness = 2;

			drawContext.DrawRectangle(null, rectPen, myCanvas);

			if (!string.IsNullOrWhiteSpace(AnnotationBottomLeft)) {
				AddText(AnnotationBottomLeft, drawContext, width, height, Windows.HorizontalAlignment.Left, Windows.VerticalAlignment.Bottom, 5);
			}

			if (!string.IsNullOrWhiteSpace(AnnotationBottomRight)) {
				AddText(AnnotationBottomRight, drawContext, width, height, Windows.HorizontalAlignment.Right, Windows.VerticalAlignment.Bottom, 5);
			}

			if (PlottingDeisotopedData) {
				AddDeisotopedDataLegend(drawContext, width, height, 10, -30, 25);
			}
		}

		const dynamic DPI = 96;

		dynamic target = new RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Default);
		target.Render(drawVisual);

		BitmapEncoder encoder = null;

		switch (fileFormat) {
			case ImageFileFormat.PNG:
				encoder = new PngBitmapEncoder();

				break;
			case ImageFileFormat.JPG:
				encoder = new JpegBitmapEncoder();
				break;
			default:
				throw new ArgumentOutOfRangeException(fileFormat, "Unrecognized value: " + fileFormat.ToString());
		}

		if (encoder != null) {
			encoder.Frames.Add(BitmapFrame.Create(target));

			using (outputStream == new FileStream(imageFilePath, FileMode.Create, FileAccess.Write)) {
				encoder.Save(outputStream);
			}
		}

	}

	public void AddGradients(Dictionary<string, OxyPalette> colorGradients)
	{
		mColorGradients = colorGradients;
	}


	protected void AddDeisotopedDataLegend(DrawingContext drawContext, int canvasWidth, int canvasHeight, int offsetLeft, int offsetTop, int spacing)
	{
		const dynamic CHARGE_START = 1;
		const dynamic CHARGE_END = 6;

		dynamic usCulture = Globalization.CultureInfo.GetCultureInfo("en-us");
		// Dim fontTypeface = New Typeface(New FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal)
		dynamic fontTypeface = new Typeface("Arial");

		dynamic fontSizeEm = FontSizeBase + 3;

		// Write out the text 1+  2+  3+  4+  5+  6+  

		// Create a box for the legend
		dynamic rectPen = new System.Windows.Media.Pen();
		rectPen.Brush = new SolidColorBrush(Colors.Black);
		rectPen.Thickness = 1;


		for (chargeState = CHARGE_START; chargeState <= CHARGE_END; chargeState++) {
			dynamic newBrush = new SolidColorBrush(GetColorByCharge(chargeState));

			dynamic newText = new FormattedText(chargeState + "+", usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, newBrush);

			dynamic textRect = new Rect(0, 0, canvasWidth, canvasHeight);
			dynamic position = textRect.Location;

			position.X = mPlot.PlotArea.Left + offsetLeft + (chargeState - 1) * (newText.Width + spacing);
			position.Y = mPlot.PlotArea.Top + offsetTop;

			if (chargeState == CHARGE_START) {
				dynamic legendBox = new Rect(position.X - 10, position.Y, CHARGE_END * (newText.Width + spacing) - spacing / 4, newText.Height);
				drawContext.DrawRectangle(null, rectPen, legendBox);
			}

			drawContext.DrawText(newText, position);
		}

	}


	protected void AddText(string textToAdd, DrawingContext drawContext, int canvasWidth, int canvasHeight, Windows.HorizontalAlignment hAlign, Windows.VerticalAlignment vAlign, int padding)
	{
		dynamic usCulture = Globalization.CultureInfo.GetCultureInfo("en-us");
		// Dim fontTypeface = New Typeface(New FontFamily("Arial"), FontStyles.Normal, System.Windows.FontWeights.Normal, FontStretches.Normal)
		dynamic fontTypeface = new Typeface("Arial");

		dynamic fontSizeEm = FontSizeBase + 1;

		dynamic newText = new FormattedText(textToAdd, usCulture, FlowDirection.LeftToRight, fontTypeface, fontSizeEm, Brushes.Black);

		dynamic textRect = new Rect(0, 0, canvasWidth, canvasHeight);
		dynamic position = textRect.Location;

		switch (hAlign) {
			case Windows.HorizontalAlignment.Left:
				position.X += padding;

				break;
			case Windows.HorizontalAlignment.Center:
				position.X += (textRect.Width - newText.Width) / 2;

				break;
			case Windows.HorizontalAlignment.Right:
				position.X += textRect.Width - newText.Width - padding;
				break;
		}

		switch (vAlign) {
			case Windows.VerticalAlignment.Top:
				position.Y += padding;

				break;
			case Windows.VerticalAlignment.Center:
				position.Y += (textRect.Height - newText.Height) / 2;

				break;
			case Windows.VerticalAlignment.Bottom:
				position.Y += textRect.Height - newText.Height - padding;
				break;
		}

		drawContext.DrawText(newText, position);
	}

	public static Color GetColorByCharge(int charge)
	{
		Color seriesColor = default(Color);
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

	protected double PointSizeToEm(int fontSizePoints)
	{
		dynamic fontSizeEm = fontSizePoints / 12;
		return fontSizeEm;
	}

	protected int PointSizeToPixels(int fontSizePoints)
	{
		dynamic fontSizePixels = fontSizePoints * 1.33;
		return Convert.ToInt32(Math.Round(fontSizePixels, 0));
	}

}

