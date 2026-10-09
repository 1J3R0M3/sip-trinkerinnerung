// Erzeugt die App-Icons (Wassertropfen) ohne externe Abhängigkeiten.
// Portiert aus scripts/gen-icons.js des Electron-Prototyps.
// Aufruf im Repo-Wurzelverzeichnis: dotnet run tools/gen-icons.cs
// Ohne eigene .exe (läuft über dotnet.exe), damit Smart App Control das Skript nicht blockiert.
#:property UseAppHost=false
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

var root = Directory.GetCurrentDirectory();
var assets = Path.Combine(root, "src", "App", "Assets");
Directory.CreateDirectory(assets);

byte[] blue = [33, 136, 229];
byte[] Square(int size, double fill = 1) => Render(size, size, (int)Math.Round(size * fill), blue);

void Write(string name, byte[] png)
{
    File.WriteAllBytes(Path.Combine(assets, name), png);
    Console.WriteLine($"geschrieben: {Path.GetRelativePath(root, Path.Combine(assets, name))}");
}

// MSIX-Grafiken in allen Skalierungen.
foreach (var scale in new[] { 100, 125, 150, 200, 400 })
{
    var f = scale / 100.0;
    int S(double v) => (int)Math.Round(v * f);
    Write($"StoreLogo.scale-{scale}.png", Square(S(50), 0.9));
    Write($"Square44x44Logo.scale-{scale}.png", Square(S(44), 0.8));
    Write($"SmallTile.scale-{scale}.png", Square(S(71), 0.6));
    Write($"Square150x150Logo.scale-{scale}.png", Square(S(150), 0.5));
    Write($"LargeTile.scale-{scale}.png", Square(S(310), 0.5));
    Write($"Wide310x150Logo.scale-{scale}.png", Render(S(310), S(150), S(75), blue));
    Write($"SplashScreen.scale-{scale}.png", Render(S(620), S(300), S(150), blue));
}

// Taskleiste/Startmenü: ohne farbige Kachel ("unplated") in festen Pixelgrößen.
foreach (var size in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
{
    var png = Square(size, 0.9);
    Write($"Square44x44Logo.targetsize-{size}.png", png);
    Write($"Square44x44Logo.targetsize-{size}_altform-unplated.png", png);
    Write($"Square44x44Logo.targetsize-{size}_altform-lightunplated.png", png);
}

// Tray- und Fenster-Icon als .ico mit mehreren Größen (PNG-kodiert).
var icoSizes = new[] { 16, 20, 24, 32, 40, 48, 64, 256 };
var images = icoSizes.Select(s => Square(s, 0.95)).ToArray();
using (var ico = new MemoryStream())
using (var w = new BinaryWriter(ico))
{
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Length);
    var offset = 6 + 16 * images.Length;
    for (var i = 0; i < images.Length; i++)
    {
        var s = icoSizes[i];
        w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((ushort)1); w.Write((ushort)32);
        w.Write(images[i].Length); w.Write(offset);
        offset += images[i].Length;
    }
    foreach (var img in images) w.Write(img);
    w.Flush();
    File.WriteAllBytes(Path.Combine(assets, "AppIcon.ico"), ico.ToArray());
    Console.WriteLine("geschrieben: src/App/Assets/AppIcon.ico");
}

// Tropfen = Kreis unten + Dreieck mit Spitze oben (normierte Koordinaten 0..1).
static bool InsideDrop(double x, double y)
{
    const double cx = 0.5, cy = 0.62, r = 0.32, tipY = 0.06;
    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) return true;
    // Tangentenpunkte vom Scheitel an den Kreis
    var d = cy - tipY;
    var ty = cy - r * r / d;
    if (y < tipY || y > ty) return false;
    var halfW = Math.Sqrt(d * d - r * r) * r / d * ((y - tipY) / (ty - tipY));
    return Math.Abs(x - cx) <= halfW;
}

static bool InsideHighlight(double x, double y)
{
    const double hx = 0.39, hy = 0.66, rx = 0.06, ry = 0.13;
    return Math.Pow((x - hx) / rx, 2) + Math.Pow((y - hy) / ry, 2) <= 1;
}

// Zeichnet den Tropfen zentriert auf eine (auch nicht quadratische) Fläche.
// dropSize: Kantenlänge des Quadrats, in das der Tropfen gezeichnet wird.
static byte[] Render(int width, int height, int dropSize, byte[] color)
{
    const int ss = 4;
    var rgba = new byte[width * height * 4];
    var ox = (width - dropSize) / 2.0;
    var oy = (height - dropSize) / 2.0;
    for (var py = 0; py < height; py++)
    for (var px = 0; px < width; px++)
    {
        int cov = 0, hl = 0;
        for (var sy = 0; sy < ss; sy++)
        for (var sx = 0; sx < ss; sx++)
        {
            var x = (px - ox + (sx + 0.5) / ss) / dropSize;
            var y = (py - oy + (sy + 0.5) / ss) / dropSize;
            if (!InsideDrop(x, y)) continue;
            cov++;
            if (dropSize >= 32 && InsideHighlight(x, y)) hl++;
        }
        var i = (py * width + px) * 4;
        var h = cov > 0 ? (double)hl / cov : 0;
        for (var c = 0; c < 3; c++)
            rgba[i + c] = (byte)Math.Round(color[c] + (255 - color[c]) * h * 0.7);
        rgba[i + 3] = (byte)Math.Round(cov / (double)(ss * ss) * 255);
    }
    return EncodePng(width, height, rgba);
}

static byte[] EncodePng(int width, int height, byte[] rgba)
{
    var ihdr = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
    BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
    ihdr[8] = 8; // Bittiefe
    ihdr[9] = 6; // RGBA
    var stride = width * 4 + 1;
    var raw = new byte[height * stride];
    for (var y = 0; y < height; y++)
        Array.Copy(rgba, y * width * 4, raw, y * stride + 1, width * 4); // Filtertyp 0 = "None"
    using var idat = new MemoryStream();
    using (var z = new ZLibStream(idat, CompressionLevel.SmallestSize)) z.Write(raw);

    using var png = new MemoryStream();
    png.Write([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
    Chunk(png, "IHDR", ihdr);
    Chunk(png, "IDAT", idat.ToArray());
    Chunk(png, "IEND", []);
    return png.ToArray();
}

static void Chunk(Stream s, string type, byte[] data)
{
    Span<byte> buf = stackalloc byte[4];
    BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
    s.Write(buf);
    var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
    s.Write(td);
    BinaryPrimitives.WriteUInt32BigEndian(buf, Crc32(td));
    s.Write(buf);
}

static uint Crc32(byte[] data)
{
    var c = 0xFFFFFFFFu;
    foreach (var b in data)
    {
        c ^= b;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
    }
    return c ^ 0xFFFFFFFFu;
}
