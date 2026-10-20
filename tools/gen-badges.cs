// Erzeugt die Erfolgs-Badges: Form je Kategorie, Farbe je Stufe, Symbol je Erfolg;
// gesperrte Variante in Grau mit Schloss. Gezeichnet als SVG, ausgegeben als PNG (256 px)
// nach src/App/Assets/Achievements/<id>.png und <id>.locked.png.
// Die Ids müssen zu Core/Achievements/AchievementCatalog passen (ein Test prüft die Dateien).
// Aufruf im Repo-Wurzelverzeichnis: dotnet run tools/gen-badges.cs [--svg <Ordner>]
// Mit --svg werden zusätzlich die SVG-Quellen geschrieben (zum Ansehen im Browser).
#:package Svg.Skia
#:property UseAppHost=false
using System.Globalization;
using System.Text;
using SkiaSharp;
using Svg.Skia;

const int Size = 256;
var root = Directory.GetCurrentDirectory();
var outDir = Path.Combine(root, "src", "App", "Assets", "Achievements");
Directory.CreateDirectory(outDir);
var svgDir = args is ["--svg", var dir] ? Path.GetFullPath(dir) : null;
if (svgDir is not null) Directory.CreateDirectory(svgDir);

// Farbstufen: hell, mittel, dunkel, tief (Banner, Zahlen).
var blue     = new Palette("#9AD4FF", "#3A9BEA", "#1C64C8", "#12458F");
var bronze   = new Palette("#F6C79A", "#D6884B", "#9E5428", "#6E3818");
var silver   = new Palette("#F4F7FA", "#B4BFCA", "#7A8794", "#4E5A66");
var gold     = new Palette("#FFE89C", "#F5BC3C", "#C98512", "#835406");
var aqua     = new Palette("#B8F4FB", "#3FC6DD", "#168AAE", "#0B5873");
var amethyst = new Palette("#DFCBFF", "#9C6DF2", "#6A3CCB", "#43218C");
var ruby     = new Palette("#FFC3CE", "#F0557A", "#B81E4B", "#7A0F31");
var emerald  = new Palette("#B6F1D1", "#34C27E", "#138A55", "#0A5634");
var indigo   = new Palette("#C9D4FF", "#6C86F0", "#3B52C9", "#25348A");
var sunrise  = new Palette("#FFDBAA", "#FF9A52", "#E0582E", "#983113");
var gray     = new Palette("#E9ECEF", "#B7BDC4", "#8A929B", "#5C646D");

Badge[] badges =
[
    new("first-glass",       Shape.Circle,  blue,     p => Glass(p)),
    new("first-daily-goal",  Shape.Circle,  blue,     p => Target(p)),
    new("first-weekly-goal", Shape.Circle,  blue,     p => Calendar(p)),
    new("volume-10",         Shape.Hexagon, bronze,   p => Volume(p, "10 L")),
    new("volume-50",         Shape.Hexagon, silver,   p => Volume(p, "50 L")),
    new("volume-100",        Shape.Hexagon, gold,     p => Volume(p, "100 L")),
    new("volume-250",        Shape.Hexagon, aqua,     p => Volume(p, "250 L")),
    new("volume-500",        Shape.Hexagon, amethyst, p => Volume(p, "500 L")),
    new("volume-1000",       Shape.Hexagon, ruby,     p => Volume(p, "1000 L")),
    new("streak-3",          Shape.Shield,  bronze,   p => Flame(p, "3")),
    new("streak-7",          Shape.Shield,  silver,   p => Flame(p, "7")),
    new("streak-14",         Shape.Shield,  gold,     p => Flame(p, "14")),
    new("streak-30",         Shape.Shield,  aqua,     p => Flame(p, "30")),
    new("streak-100",        Shape.Shield,  amethyst, p => Flame(p, "100")),
    new("perfect-week",      Shape.Rosette, emerald,  p => Week(p)),
    new("on-cue",            Shape.Rosette, indigo,   p => Bell(p)),
    new("early-bird",        Shape.Rosette, sunrise,  p => Sunrise(p)),
];

foreach (var b in badges)
{
    Write($"{b.Id}", Render(b, b.Palette, b.Id));
    Write($"{b.Id}.locked", Render(b, gray, b.Id + "-l", locked: true));
}
Console.WriteLine($"{badges.Length * 2} Badges geschrieben nach {Path.GetRelativePath(root, outDir)}");

void Write(string name, string svgText)
{
    if (svgDir is not null) File.WriteAllText(Path.Combine(svgDir, name + ".svg"), svgText);

    using var svg = new SKSvg();
    using (var source = new MemoryStream(Encoding.UTF8.GetBytes(svgText))) svg.Load(source);
    using var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
    using (var canvas = new SKCanvas(bitmap))
    {
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);
    }
    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    using var file = File.Create(Path.Combine(outDir, name + ".png"));
    data.SaveTo(file);
}

static string Render(Badge b, Palette p, string id, bool locked = false)
{
    var s = new StringBuilder();
    s.Append("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">""");
    s.Append($"""
        <defs>
        <linearGradient id="{id}-ring" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{p.Light}"/><stop offset="1" stop-color="{p.Dark}"/></linearGradient>
        <linearGradient id="{id}-face" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{p.Dark}"/><stop offset="1" stop-color="{p.Mid}"/></linearGradient>
        <linearGradient id="{id}-gloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".32"/><stop offset=".5" stop-color="#fff" stop-opacity="0"/></linearGradient>
        <filter id="{id}-shadow" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur in="SourceAlpha" stdDeviation="2.5"/><feOffset dy="3" result="b"/><feFlood flood-color="{p.Deep}" flood-opacity=".55"/><feComposite in2="b" operator="in"/><feMerge><feMergeNode/><feMergeNode in="SourceGraphic"/></feMerge></filter>
        </defs>
        """);
    var (outer, inner) = Outline(b.Shape);
    s.Append($"""<path d="{outer}" fill="url(#{id}-ring)"/>""");
    s.Append($"""<path d="{inner}" fill="url(#{id}-face)"/>""");
    s.Append($"""<path d="{inner}" fill="none" stroke="#fff" stroke-opacity=".35" stroke-width="3"/>""");
    s.Append($"""<path d="{inner}" fill="url(#{id}-gloss)"/>""");
    s.Append($"""<g filter="url(#{id}-shadow)"{(locked ? " opacity=\".8\"" : "")}>{b.Symbol(p)}</g>""");
    if (locked) s.Append(Lock(p));
    s.Append("</svg>");
    return s.ToString();
}

// Umriss außen (Rahmen) und innen (Fläche).
static (string Outer, string Inner) Outline(Shape shape) => shape switch
{
    Shape.Circle => (Circle(128, 128, 118), Circle(128, 128, 98)),
    Shape.Hexagon => (Polygon(6, 120, -90, 14), Polygon(6, 100, -90, 10)),
    Shape.Shield => (
        "M128 8 L226 40 Q232 42 232 48 L232 118 C232 182 190 226 128 250 C66 226 24 182 24 118 L24 48 Q24 42 30 40 Z",
        "M128 28 L212 55 L212 118 C212 170 178 206 128 228 C78 206 44 170 44 118 L44 55 Z"),
    _ => (Rosette(128, 128, 112, 8, 12), Circle(128, 128, 90)),
};

// Symbole (weiß, Details in der Stufenfarbe).
static string Glass(Palette p) => $"""
    <path d="M84 68 L172 68 L160 182 Q159 190 151 190 L105 190 Q97 190 96 182 Z" fill="none" stroke="#fff" stroke-width="10" stroke-linejoin="round"/>
    <path d="M95 122 C108 112 116 112 128 120 C140 128 148 128 161 120 L155 178 Q154 182 150 182 L106 182 Q102 182 101 178 Z" fill="#fff"/>
    <path d="M114 140 Q113 158 120 168" fill="none" stroke="{p.Mid}" stroke-width="6" stroke-linecap="round"/>
    <path d="M192 44 Q195 59 210 62 Q195 65 192 80 Q189 65 174 62 Q189 59 192 44 Z" fill="#fff"/>
    <path d="M62 92 Q64 100 72 102 Q64 104 62 112 Q60 104 52 102 Q60 100 62 92 Z" fill="#fff"/>
    """;

static string Target(Palette p) => $"""
    <circle cx="128" cy="128" r="62" fill="none" stroke="#fff" stroke-width="12"/>
    <circle cx="128" cy="128" r="36" fill="none" stroke="#fff" stroke-width="12"/>
    <circle cx="128" cy="128" r="11" fill="#fff"/>
    <path d="M136 120 L186 70" stroke="{p.Dark}" stroke-width="20" stroke-linecap="round"/>
    <path d="M136 120 L186 70" stroke="#fff" stroke-width="9" stroke-linecap="round"/>
    <path d="M130 126 L154 118 L138 102 Z" fill="#fff" stroke="{p.Dark}" stroke-width="4" stroke-linejoin="round"/>
    <path d="M186 70 L206 66 M186 70 L190 50 M176 80 L196 76 M176 80 L180 60" stroke="#fff" stroke-width="8" stroke-linecap="round"/>
    """;

static string Calendar(Palette p) => $"""
    <rect x="72" y="76" width="112" height="108" rx="16" fill="none" stroke="#fff" stroke-width="10"/>
    <path d="M72 108 L184 108 L184 92 Q184 76 168 76 L88 76 Q72 76 72 92 Z" fill="#fff"/>
    <path d="M100 62 L100 88 M156 62 L156 88" stroke="#fff" stroke-width="10" stroke-linecap="round"/>
    <path d="M102 146 L120 164 L156 126" fill="none" stroke="#fff" stroke-width="13" stroke-linecap="round" stroke-linejoin="round"/>
    """;

static string Volume(Palette p, string label) => $"""
    <path d="M128 46 C116 66 92 92 92 116 A36 36 0 0 0 164 116 C164 92 140 66 128 46 Z" fill="#fff"/>
    <path d="M109 112 A20 20 0 0 0 122 136" fill="none" stroke="{p.Mid}" stroke-width="7" stroke-linecap="round"/>
    {Banner(p, label)}
    """;

static string Flame(Palette p, string label) => $"""
    <path d="M122 38 C150 62 150 88 162 104 C166 92 166 84 164 74 C182 94 184 124 180 148 C176 180 154 202 128 202 C98 202 76 180 76 150 C76 126 90 110 100 96 C102 110 106 118 114 124 C108 92 112 62 122 38 Z" fill="#fff"/>
    <text x="128" y="{(label.Length > 2 ? 174 : 178)}" text-anchor="middle" font-family="Segoe UI Variable Display, Segoe UI, sans-serif" font-weight="800" font-size="{(label.Length > 2 ? 38 : 50)}" fill="{p.Deep}">{label}</text>
    """;

static string Week(Palette p)
{
    var dots = string.Concat(Enumerable.Range(0, 7).Select(i =>
        $"""<circle cx="{F(128 + (i - 3) * 21)}" cy="174" r="7.5" fill="#fff"/>"""));
    return $"""
        <path d="{Star(128, 106, 46, 20)}" fill="#fff" stroke="#fff" stroke-width="6" stroke-linejoin="round"/>
        {dots}
        """;
}

static string Bell(Palette p) => $"""
    <circle cx="124" cy="58" r="8" fill="#fff"/>
    <path d="M124 62 C148 62 162 82 162 104 L162 132 L178 156 L70 156 L86 132 L86 104 C86 82 100 62 124 62 Z" fill="#fff" stroke="#fff" stroke-width="6" stroke-linejoin="round"/>
    <path d="M110 166 A14 14 0 0 0 138 166 Z" fill="#fff"/>
    <path d="M58 92 Q50 108 58 124 M190 92 Q198 108 190 124" fill="none" stroke="#fff" stroke-width="7" stroke-linecap="round"/>
    <circle cx="170" cy="164" r="24" fill="#fff" stroke="{p.Dark}" stroke-width="5"/>
    <path d="M159 164 L167 172 L182 156" fill="none" stroke="{p.Dark}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
    """;

static string Sunrise(Palette p)
{
    var rays = string.Concat(new[] { 180, 216, 252, 288, 324 }.Select(a =>
    {
        var r = (a + 18) * Math.PI / 180;
        var (c, sn) = (Math.Cos(r), Math.Sin(r));
        return $"""<path d="M{F(128 + 54 * c)} {F(150 + 54 * sn)} L{F(128 + 70 * c)} {F(150 + 70 * sn)}" stroke="#fff" stroke-width="9" stroke-linecap="round"/>""";
    }));
    return $"""
        <path d="M86 150 A42 42 0 0 1 170 150 Z" fill="#fff"/>
        {rays}
        <path d="M60 150 L196 150" stroke="#fff" stroke-width="9" stroke-linecap="round"/>
        <path d="M82 174 Q94 166 106 174 T130 174 T154 174 T178 174" fill="none" stroke="#fff" stroke-width="7" stroke-linecap="round"/>
        <path d="M150 64 Q158 56 166 64 Q174 56 182 64" fill="none" stroke="#fff" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>
        """;
}

static string Banner(Palette p, string label) => $"""
    <path d="M30 172 L52 172 L52 212 L30 212 L42 192 Z M226 172 L204 172 L204 212 L226 212 L214 192 Z" fill="{p.Dark}"/>
    <path d="M44 204 L52 212 L52 204 Z M212 204 L204 212 L204 204 Z" fill="{p.Deep}"/>
    <rect x="44" y="164" width="168" height="40" rx="4" fill="{p.Deep}"/>
    <text x="128" y="194" text-anchor="middle" font-family="Segoe UI Variable Display, Segoe UI, sans-serif" font-weight="800" font-size="27" letter-spacing="1" fill="#fff">{label}</text>
    """;

static string Lock(Palette p) => $"""
    <circle cx="196" cy="196" r="34" fill="{p.Deep}" stroke="#fff" stroke-width="5"/>
    <path d="M184 192 L184 182 A12 12 0 0 1 208 182 L208 192" fill="none" stroke="#fff" stroke-width="6" stroke-linecap="round"/>
    <rect x="176" y="190" width="40" height="28" rx="6" fill="#fff"/>
    <circle cx="196" cy="203" r="4" fill="{p.Deep}"/>
    """;

// Geometrie
static string Circle(double cx, double cy, double r) =>
    $"M{F(cx - r)} {F(cy)} A{F(r)} {F(r)} 0 1 1 {F(cx + r)} {F(cy)} A{F(r)} {F(r)} 0 1 1 {F(cx - r)} {F(cy)} Z";

// Regelmäßiges Vieleck mit abgerundeten Ecken.
static string Polygon(int n, double r, double startDeg, double round)
{
    var pts = Enumerable.Range(0, n).Select(i =>
    {
        var a = (startDeg + 360.0 * i / n) * Math.PI / 180;
        return (X: 128 + r * Math.Cos(a), Y: 128 + r * Math.Sin(a));
    }).ToArray();
    var s = new StringBuilder();
    for (var i = 0; i < n; i++)
    {
        var (prev, cur, next) = (pts[(i + n - 1) % n], pts[i], pts[(i + 1) % n]);
        var a = Toward(cur, prev, round);
        var b = Toward(cur, next, round);
        s.Append(i == 0 ? $"M{F(a.X)} {F(a.Y)}" : $" L{F(a.X)} {F(a.Y)}");
        s.Append($" Q{F(cur.X)} {F(cur.Y)} {F(b.X)} {F(b.Y)}");
    }
    return s.Append(" Z").ToString();
}

static (double X, double Y) Toward((double X, double Y) from, (double X, double Y) to, double d)
{
    var (dx, dy) = (to.X - from.X, to.Y - from.Y);
    var len = Math.Sqrt(dx * dx + dy * dy);
    return (from.X + dx / len * d, from.Y + dy / len * d);
}

// Rosette: Kreis mit gewellten Rand.
static string Rosette(double cx, double cy, double r, double wave, int bumps)
{
    var s = new StringBuilder();
    const int steps = 240;
    for (var i = 0; i <= steps; i++)
    {
        var a = 2 * Math.PI * i / steps;
        var rr = r + wave * Math.Cos(bumps * a);
        s.Append(i == 0 ? "M" : " L").Append($"{F(cx + rr * Math.Cos(a))} {F(cy + rr * Math.Sin(a))}");
    }
    return s.Append(" Z").ToString();
}

static string Star(double cx, double cy, double outer, double inner)
{
    var s = new StringBuilder();
    for (var i = 0; i < 10; i++)
    {
        var a = (-90 + 36 * i) * Math.PI / 180;
        var r = i % 2 == 0 ? outer : inner;
        s.Append(i == 0 ? "M" : " L").Append($"{F(cx + r * Math.Cos(a))} {F(cy + r * Math.Sin(a))}");
    }
    return s.Append(" Z").ToString();
}

static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);

enum Shape { Circle, Hexagon, Shield, Rosette }
record Palette(string Light, string Mid, string Dark, string Deep);
record Badge(string Id, Shape Shape, Palette Palette, Func<Palette, string> Symbol);
