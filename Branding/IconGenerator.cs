// AutoFPS icon generator — single source of truth for the app icon ("B: flight over a smoothing frame-time line").
//
// Run from the repository root:
//     dotnet run Branding/IconGenerator.cs
//
// Writes: Branding/*.svg + Branding/png/*.png, MSFS_AutoFPS/icon.ico, MSFS_AutoFPS/AutoFPS.png,
// Installer/icon.ico, the MSFS widget images/thumbnail, and refreshes the widget's layout.json and
// manifest.json total_package_size (MSFS requires those to match the files on disk).
//
// After editing widget files only (HTML/CSS/JS), refresh just the package metadata:
//     dotnet run Branding/IconGenerator.cs -- --layout-only

#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property Nullable=disable
#:property PublishTrimmed=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

bool layoutOnly = args.Contains("--layout-only");
string[] positional = args.Where(a => !a.StartsWith("--")).ToArray();
string root = Path.GetFullPath(positional.Length > 0 ? positional[0] : Directory.GetCurrentDirectory());
if (!File.Exists(Path.Combine(root, "MSFS_AutoFPS.sln")))
{
	Console.Error.WriteLine($"Run from the repository root (MSFS_AutoFPS.sln not found in {root}).");
	return 1;
}

int exitCode = 0;
var thread = new Thread(() =>
{
	try
	{
		if (layoutOnly) Icons.RefreshWidgetLayout(Path.Combine(root, "Widget", "autofps-html-widget-panel"));
		else Icons.Generate(root);
		Console.WriteLine(layoutOnly ? "Widget layout refreshed." : "");
	}
	catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
return exitCode;

/// <summary>One drawable variant of the icon, defined in a 64×64 design space.</summary>
/// <param name="ContentScale">SVG only: shrinks the artwork about its centre to leave a margin (toolbar icon).</param>
/// <param name="ContentCenterY">SVG only: vertical centre of the artwork, moved to the canvas centre when scaling.</param>
record Variant(string Name, bool Tile, double TileRadius, string LinePoints, double LineWidth, string LineColor, double PlaneScale, string PlaneColor,
	double ContentScale = 1.0, double ContentCenterY = 32);

static class Icons
{
	const string TileColor = "#0E1A2B";
	const string Accent = "#22D3EE";

	// Plane silhouette (top view, nose up) before rotation; rotated 45° about (40,22) so it climbs to the upper right.
	const string PlanePoints = "40,8 42.2,16 53,22.5 53,25.5 42.2,22.5 41.6,30 46,33.5 46,35.5 40,34 34,35.5 34,33.5 38.4,30 37.8,22.5 27,25.5 27,22.5 37.8,16";
	const double PlaneCx = 40, PlaneCy = 22;

	// Full detail (≥ 32 px): jittery frame times on the left settling into a flat line.
	static readonly Variant Full = new("full", true, 14, "8,47 13,41 17,51 22,38 27,45 33,43 42,44 56,44", 3.5, Accent, 1.0, "#FFFFFF");
	// Simplified for 16–24 px: fewer zig-zags, heavier stroke, bigger plane.
	static readonly Variant Small = new("small", true, 12, "7,48 14,39 20,51 27,42 34,45 57,45", 5.5, Accent, 1.15, "#FFFFFF");
	// Monochrome for the MSFS toolbar (the sim draws it on its own dark button). Scaled to ~74 % with the
	// artwork centred so it has the same breathing room as the sim's own toolbar icons.
	static readonly Variant Toolbar = new("toolbar", false, 0, "6,48 12,40 17,52 23,38 29,46 35,44 44,45 58,45", 5, "#FFFFFF", 1.1, "#FFFFFF",
		ContentScale: 0.74, ContentCenterY: 29);

	static readonly int[] PngSizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256, 512 };
	static readonly int[] IcoSizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

	public static void Generate(string root)
	{
		string branding = Path.Combine(root, "Branding");
		string pngDir = Path.Combine(branding, "png");
		Directory.CreateDirectory(pngDir);

		// Sources
		WriteText(Path.Combine(branding, "autofps-icon.svg"), Svg(Full));
		WriteText(Path.Combine(branding, "autofps-icon-small.svg"), Svg(Small));
		WriteText(Path.Combine(branding, "autofps-toolbar.svg"), Svg(Toolbar));

		// PNG set
		var png = new Dictionary<int, byte[]>();
		foreach (int size in PngSizes)
		{
			png[size] = EncodePng(Render(ForSize(size), size));
			WriteBytes(Path.Combine(pngDir, $"autofps-{size}.png"), png[size]);
		}

		// Windows icon (app exe, tray, installer)
		byte[] ico = BuildIco(IcoSizes);
		WriteBytes(Path.Combine(root, "MSFS_AutoFPS", "icon.ico"), ico);
		WriteBytes(Path.Combine(root, "Installer", "icon.ico"), ico);

		// In-app header image (shown at 16 DIP, so 64 px covers up to 400 % DPI)
		WriteBytes(Path.Combine(root, "MSFS_AutoFPS", "AutoFPS.png"), png[64]);

		// MSFS widget
		string widget = Path.Combine(root, "Widget", "autofps-html-widget-panel");
		WriteBytes(Path.Combine(widget, "html_ui", "InGamePanels", "HtmlWidgetPanel", "img", "AutoFPS.png"), png[64]);
		string toolbarSvg = Svg(Toolbar);
		WriteText(Path.Combine(widget, "html_ui", "icons", "toolbar", "ICON_TOOLBAR_HTML_WIDGET.svg"), toolbarSvg);
		WriteText(Path.Combine(widget, "html_ui", "Textures", "Menu", "toolbar", "ICON_TOOLBAR_HTML_WIDGET.svg"), toolbarSvg);
		WriteBytes(Path.Combine(widget, "ContentInfo", "autofps-html-widget-panel", "Thumbnail.jpg"), RenderThumbnail());
		RefreshWidgetLayout(widget);

		Console.WriteLine("Icons generated.");
	}

	static Variant ForSize(int size) => size <= 24 ? Small : Full;

	// ---------- SVG ----------

	static string Svg(Variant v)
	{
		var sb = new StringBuilder();
		sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\" width=\"64\" height=\"64\">\n");
		sb.Append("\t<title>AutoFPS</title>\n");
		if (v.Tile)
			sb.Append($"\t<rect width=\"64\" height=\"64\" rx=\"{F(v.TileRadius)}\" fill=\"{TileColor}\"/>\n");
		bool scaled = v.ContentScale != 1.0;
		if (scaled)
			sb.Append($"\t<g transform=\"translate(32 32) scale({F(v.ContentScale)}) translate(-32 {F(-v.ContentCenterY)})\">\n");
		sb.Append($"\t<polyline points=\"{v.LinePoints}\" fill=\"none\" stroke=\"{v.LineColor}\" stroke-width=\"{F(v.LineWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
		sb.Append($"\t<polygon points=\"{PlanePoints}\" fill=\"{v.PlaneColor}\" transform=\"rotate(45 {F(PlaneCx)} {F(PlaneCy)}){(v.PlaneScale != 1.0 ? $" translate({F(PlaneCx)} {F(PlaneCy)}) scale({F(v.PlaneScale)}) translate({F(-PlaneCx)} {F(-PlaneCy)})" : "")}\"/>\n");
		if (scaled)
			sb.Append("\t</g>\n");
		sb.Append("</svg>\n");
		return sb.ToString();
	}

	// ---------- WPF rendering ----------

	static void Draw(DrawingContext dc, Variant v)
	{
		if (v.Tile)
			dc.DrawRoundedRectangle(Brush(TileColor), null, new Rect(0, 0, 64, 64), v.TileRadius, v.TileRadius);

		var pen = new Pen(Brush(v.LineColor), v.LineWidth)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round,
			LineJoin = PenLineJoin.Round,
		};
		dc.DrawGeometry(null, pen, PolyGeometry(v.LinePoints, closed: false));

		var plane = PolyGeometry(PlanePoints, closed: true);
		var t = new TransformGroup();
		t.Children.Add(new ScaleTransform(v.PlaneScale, v.PlaneScale, PlaneCx, PlaneCy));
		t.Children.Add(new RotateTransform(45, PlaneCx, PlaneCy));
		plane.Transform = t;
		dc.DrawGeometry(Brush(v.PlaneColor), null, plane);
	}

	static BitmapSource Render(Variant v, int size)
	{
		var visual = new DrawingVisual();
		RenderOptions.SetEdgeMode(visual, EdgeMode.Unspecified);
		using (var dc = visual.RenderOpen())
		{
			dc.PushTransform(new ScaleTransform(size / 64.0, size / 64.0));
			Draw(dc, v);
			dc.Pop();
		}
		var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
		bmp.Render(visual);
		bmp.Freeze();
		return bmp;
	}

	static byte[] RenderThumbnail()
	{
		const int w = 412, h = 170;
		var visual = new DrawingVisual();
		using (var dc = visual.RenderOpen())
		{
			dc.DrawRectangle(Brush("#14161A"), null, new Rect(0, 0, w, h));
			dc.PushTransform(new TranslateTransform(36, 29));
			dc.PushTransform(new ScaleTransform(112 / 64.0, 112 / 64.0));
			Draw(dc, Full);
			dc.Pop();
			dc.Pop();
			var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
			var title = new FormattedText("AutoFPS", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 40, Brushes.White, 1.0);
			dc.DrawText(title, new Point(172, 46));
			var sub = new FormattedText("In-game widget", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
				new Typeface("Segoe UI"), 18, Brush(Accent), 1.0);
			dc.DrawText(sub, new Point(174, 98));
		}
		var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
		bmp.Render(visual);
		var enc = new JpegBitmapEncoder { QualityLevel = 92 };
		enc.Frames.Add(BitmapFrame.Create(bmp));
		using var ms = new MemoryStream();
		enc.Save(ms);
		return ms.ToArray();
	}

	// ---------- ICO ----------

	static byte[] BuildIco(int[] sizes)
	{
		var images = sizes.Select(s =>
		{
			var bmp = Render(ForSize(s), s);
			// ≤ 64 px as classic 32-bit DIBs (widest compatibility, incl. System.Drawing.Icon); 256 as PNG.
			return (Size: s, Data: s >= 256 ? EncodePng(bmp) : EncodeIcoDib(bmp, s));
		}).ToList();

		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);
		bw.Write((ushort)0);            // reserved
		bw.Write((ushort)1);            // type: icon
		bw.Write((ushort)images.Count);
		int offset = 6 + 16 * images.Count;
		foreach (var img in images)
		{
			bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
			bw.Write((byte)(img.Size >= 256 ? 0 : img.Size));
			bw.Write((byte)0);          // palette colours
			bw.Write((byte)0);          // reserved
			bw.Write((ushort)1);        // planes
			bw.Write((ushort)32);       // bpp
			bw.Write(img.Data.Length);
			bw.Write(offset);
			offset += img.Data.Length;
		}
		foreach (var img in images)
			bw.Write(img.Data);
		bw.Flush();
		return ms.ToArray();
	}

	static byte[] EncodeIcoDib(BitmapSource src, int size)
	{
		var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0); // un-premultiplies alpha
		int stride = size * 4;
		var pixels = new byte[stride * size];
		bgra.CopyPixels(pixels, stride, 0);

		int maskStride = ((size + 31) / 32) * 4;
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);
		bw.Write(40);                   // BITMAPINFOHEADER size
		bw.Write(size);
		bw.Write(size * 2);             // XOR + AND mask height
		bw.Write((ushort)1);
		bw.Write((ushort)32);
		bw.Write(0);                    // BI_RGB
		bw.Write(stride * size + maskStride * size);
		bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
		for (int y = size - 1; y >= 0; y--)   // bottom-up
			bw.Write(pixels, y * stride, stride);
		bw.Write(new byte[maskStride * size]); // AND mask unused: alpha channel is authoritative
		bw.Flush();
		return ms.ToArray();
	}

	static byte[] EncodePng(BitmapSource src)
	{
		var enc = new PngBitmapEncoder();
		enc.Frames.Add(BitmapFrame.Create(src));
		using var ms = new MemoryStream();
		enc.Save(ms);
		return ms.ToArray();
	}

	// ---------- MSFS package metadata ----------

	public static void RefreshWidgetLayout(string widget)
	{
		var files = Directory.GetFiles(widget, "*", SearchOption.AllDirectories)
			.Select(f => (Full: f, Rel: Path.GetRelativePath(widget, f).Replace('\\', '/')))
			.Where(f => !f.Rel.Equals("layout.json", StringComparison.OrdinalIgnoreCase)
					 && !f.Rel.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
			.OrderBy(f => f.Rel.ToLowerInvariant(), StringComparer.Ordinal)
			.ToList();

		var sb = new StringBuilder("{\n  \"content\": [\n");
		long total = 0;
		for (int i = 0; i < files.Count; i++)
		{
			var info = new FileInfo(files[i].Full);
			total += info.Length;
			sb.Append("    {\n");
			sb.Append($"      \"path\": \"{files[i].Rel.ToLowerInvariant()}\",\n");
			sb.Append($"      \"size\": {info.Length},\n");
			sb.Append($"      \"date\": {info.LastWriteTimeUtc.ToFileTimeUtc()}\n");
			sb.Append(i < files.Count - 1 ? "    },\n" : "    }\n");
		}
		sb.Append("  ]\n}");
		WriteText(Path.Combine(widget, "layout.json"), sb.ToString());

		string manifestPath = Path.Combine(widget, "manifest.json");
		string manifest = File.ReadAllText(manifestPath);
		manifest = Regex.Replace(manifest, "\"total_package_size\":\\s*\"\\d+\"", $"\"total_package_size\": \"{total}\"");
		WriteText(manifestPath, manifest);
	}

	// ---------- helpers ----------

	static Geometry PolyGeometry(string points, bool closed)
	{
		var pts = points.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(p => p.Split(','))
			.Select(p => new Point(double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture)))
			.ToList();
		var fig = new PathFigure { StartPoint = pts[0], IsClosed = closed, IsFilled = closed };
		fig.Segments.Add(new PolyLineSegment(pts.Skip(1), true));
		var geo = new PathGeometry();
		geo.Figures.Add(fig);
		return geo;
	}

	static SolidColorBrush Brush(string hex)
	{
		var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		b.Freeze();
		return b;
	}

	static string F(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

	static void WriteText(string path, string text)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllText(path, text, new UTF8Encoding(false));
	}

	static void WriteBytes(string path, byte[] data)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		File.WriteAllBytes(path, data);
	}
}
