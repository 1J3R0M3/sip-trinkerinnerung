// Bereitet die Benachrichtigungstöne auf: MP3 aus artifacts/sounds-raw → src/App/Assets/Sounds/<id>.wav.
// Mono, Stille vorn und hinten abgeschnitten, höchstens 3 s, Lautstärke angeglichen, kurz ein- und
// ausgeblendet, 16 Bit PCM (spielt Windows für Benachrichtigungen zuverlässig ab).
// Herkunft und Lizenz der Töne: src/App/Assets/Sounds/LICENSES.md.
// Aufruf im Repo-Wurzelverzeichnis: dotnet run tools/process-sounds.cs
// Ohne eigene .exe (läuft über dotnet.exe), damit Smart App Control das Skript nicht blockiert.
#:property UseAppHost=false
// NAudio dekodiert MP3 über Media Foundation (COM); Skripte schalten COM sonst ab.
#:property PublishAot=false
#:property BuiltInComInteropSupport=true
#:package NAudio
using NAudio.Wave;

var root = Directory.GetCurrentDirectory();
var input = Path.Combine(root, "artifacts", "sounds-raw");
var output = Path.Combine(root, "src", "App", "Assets", "Sounds");
Directory.CreateDirectory(output);

const double SilenceDb = -45;   // leiser gilt als Stille (relativ zum lautesten Punkt)
const double TargetRmsDb = -18; // angestrebte Lautheit des hörbaren Teils
const double PeakDb = -1;       // Spitzen nie lauter als das
const double MaxSeconds = 3.0;
const double FadeInSeconds = 0.005;
const double FadeOutSeconds = 0.15;

var files = Directory.GetFiles(input, "*.mp3").Order().ToArray();
if (files.Length == 0)
{
    Console.Error.WriteLine($"Keine MP3-Dateien in {Path.GetRelativePath(root, input)}.");
    return 1;
}

foreach (var file in files)
{
    var id = Path.GetFileNameWithoutExtension(file);
    var (samples, rate) = ReadMono(file);

    // Stille abschneiden; 10 ms davor bleiben stehen, damit der Anschlag nicht abgehackt klingt.
    var peak = samples.Max(Math.Abs);
    var threshold = peak * Db(SilenceDb);
    var first = Array.FindIndex(samples, s => Math.Abs(s) > threshold);
    var last = Array.FindLastIndex(samples, s => Math.Abs(s) > threshold);
    first = Math.Max(0, first - rate / 100);
    var length = Math.Min(last - first + 1, (int)(MaxSeconds * rate));
    var clip = samples.AsSpan(first, length).ToArray();

    // Lautstärke: Ziel-RMS über den hörbaren Teil, aber die Spitze bleibt unter -1 dBFS.
    var rms = Math.Sqrt(clip.Average(s => (double)s * s));
    var gain = Math.Min(Db(TargetRmsDb) / rms, Db(PeakDb) / clip.Max(Math.Abs));

    var fadeIn = (int)(FadeInSeconds * rate);
    var fadeOut = Math.Min((int)(FadeOutSeconds * rate), clip.Length / 3);
    for (var i = 0; i < clip.Length; i++)
    {
        var envelope = 1.0;
        if (i < fadeIn) envelope = (double)i / fadeIn;
        var fromEnd = clip.Length - 1 - i;
        if (fromEnd < fadeOut) envelope *= (double)fromEnd / fadeOut;
        clip[i] = (float)(clip[i] * gain * envelope);
    }

    var target = Path.Combine(output, id + ".wav");
    using (var writer = new WaveFileWriter(target, new WaveFormat(rate, 16, 1)))
        writer.WriteSamples(clip, 0, clip.Length);

    Console.WriteLine($"{id,-12} {clip.Length / (double)rate,5:0.00} s  Verstärkung {20 * Math.Log10(gain),6:+0.0;-0.0} dB  {new FileInfo(target).Length / 1024,4} KB");
}
return 0;

static double Db(double db) => Math.Pow(10, db / 20);

static (float[] Samples, int Rate) ReadMono(string path)
{
    using var reader = new AudioFileReader(path);
    var channels = reader.WaveFormat.Channels;
    var all = new List<float>();
    var buffer = new float[reader.WaveFormat.SampleRate * channels];
    int read;
    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        for (var i = 0; i + channels <= read; i += channels)
        {
            var sum = 0f;
            for (var c = 0; c < channels; c++) sum += buffer[i + c];
            all.Add(sum / channels);
        }
    return ([.. all], reader.WaveFormat.SampleRate);
}
