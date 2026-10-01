using System.IO;
using System.Net.Http;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace ITMartinNotat;

/// <summary>
/// The AI that runs on this PC (llama.cpp via LLamaSharp + Google Gemma 3 4B). Nothing leaves the machine;
/// the internet is only used once, to download the model file.
/// </summary>
public static class LocalWriter
{
    public const string ModelFile = "gemma-3-4b-it-Q4_K_M.gguf";
    public const string ModelUrl = "https://huggingface.co/bartowski/google_gemma-3-4b-it-GGUF/resolve/main/google_gemma-3-4b-it-Q4_K_M.gguf";
    public const long ModelBytes = 2_489_758_112;

    public static readonly string ModelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Notatskriver", "model");

    private static LLamaWeights? _weights;
    private static ModelParams? _params;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The model next to the program (IT can place it there) wins over the downloaded copy.</summary>
    public static string? FindModel()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, ModelDir })
        {
            var p = Path.Combine(dir, ModelFile);
            if (File.Exists(p) && new FileInfo(p).Length > 1_000_000_000) return p;
        }
        return null;
    }

    public static async Task DownloadAsync(IProgress<double> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(ModelDir);
        var target = Path.Combine(ModelDir, ModelFile);
        var part = target + ".part";
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var resp = await http.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? ModelBytes;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(part))
        {
            var buf = new byte[1 << 20];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                done += n;
                progress.Report((double)done / total);
            }
        }
        File.Move(part, target, overwrite: true);
    }

    // Small local models invent and swap who-did-what easily - hence the strict rules and the worked example.
    private const string Instructions = """
        Omskriv stikord fra en dansk sundhedsfaglig medarbejder til journaltekst.

        Regler:
        - Brug KUN oplysninger fra stikordene. Tilføj intet: ingen steder (fx "klinikken"), årsager, vurderinger, risici eller aftaler, der ikke står der.
        - Bevar hvem der gør hvad. "X ringet" betyder at X har ringet. Den der er bekymret, er den stikordet nævner.
        - Bevar navne, tal, CPR, telefonnumre, enheder, medicin og doser nøjagtigt som skrevet. Fortolk ikke CPR som fødselsdato.
        - Er noget uklart, så skriv det tæt på stikordet og sæt [?] efter.
        - Lav stikordene om til HELE, korte, neutrale sætninger på dansk med udsagnsord. Skriv almindelige forkortelser ud (hø = højre, ve = venstre, p = puls).
        - Kun selve notatet: gentag ikke formens navn, ingen indledning, ingen stjerner eller #.
        - Overskrift-afsnit uden indhold i stikordene skrives som "Intet noteret".

        Eksempel på stikord:
        hjemmebesøg, svimmel ved opstand, ve fod hævet, p 88, datter Mette ringet - vil have status
        Eksempel på løbende tekst:
        Hjemmebesøg. Patienten er svimmel, når hun rejser sig. Venstre fod er hævet. Puls 88. Datteren Mette har ringet og ønsker en status.
        """;

    /// <summary>Writes the note and streams the text piece by piece (onPiece runs for every new bit).</summary>
    public static async Task<string> WriteAsync(string keywords, string style, string extra, Action<string> onPiece, CancellationToken ct)
    {
        var path = FindModel() ?? throw new InvalidOperationException("AI-modellen er ikke hentet endnu. Tryk ⬇ Hent AI-model.");
        await Gate.WaitAsync(ct);
        try
        {
            if (_weights is null)
            {
                // Loading takes a few seconds the first time; the model then stays in memory while the program is open.
                _params = new ModelParams(path) { ContextSize = 8192, GpuLayerCount = 0, Threads = int.TryParse(Environment.GetEnvironmentVariable("NOTAT_THREADS"), out var t) ? t : null };
                _weights = await Task.Run(() => LLamaWeights.LoadFromFile(_params), ct);
            }

            // Gemma has no system role - instructions go in the user turn (Gemma 3 chat template).
            var prompt = new StringBuilder()
                .Append("<start_of_turn>user\n")
                .AppendLine(Instructions)
                .AppendLine($"Skriv nu: {style}. Hold længden som formen siger.")
                .AppendLine(string.IsNullOrWhiteSpace(extra) ? "" : $"Ekstra ønske: {extra.Trim()}")
                .AppendLine("Stikord:")
                .AppendLine(keywords.Trim())
                .Append("<end_of_turn>\n<start_of_turn>model\n")
                .ToString();

            var executor = new StatelessExecutor(_weights, _params!);
            var inference = new InferenceParams
            {
                MaxTokens = 1200,
                AntiPrompts = ["<end_of_turn>"],
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.1f },
            };
            var sb = new StringBuilder();
            await foreach (var piece in executor.InferAsync(prompt, inference, ct))
            {
                sb.Append(piece);
                onPiece(piece);
            }
            var note = sb.ToString().Replace("<end_of_turn>", "").Replace("**", "").Trim();
            // The small model sometimes repeats the chosen form as a first line ("Journalnotat – løbende tekst.") - drop it.
            var firstLine = note.Split('\n')[0].Trim().TrimEnd('.', ':');
            var styleName = style.Split(" (")[0].Trim();
            if (firstLine.Length <= style.Length + 2 && styleName.StartsWith(firstLine, StringComparison.OrdinalIgnoreCase))
                note = note[(note.IndexOf('\n') is var i and >= 0 ? i + 1 : note.Length)..].TrimStart();
            return note;
        }
        finally
        {
            Gate.Release();
        }
    }
}
