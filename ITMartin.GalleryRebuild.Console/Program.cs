using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Genopbygger det offline fotobibliotek (index.html + _Galleri) ud fra de
// filer der faktisk ligger på drevet lige nu. Kør denne når du har flyttet,
// omdøbt eller slettet billeder/videoer - så matcher galleriet igen.
//
// Peger som udgangspunkt på den mappe programmet selv ligger i, så den bare
// virker ved at blive dobbeltklikket fra drevets rodmappe. Et argument kan
// give en anden sti, hvis filen nogensinde flyttes væk fra biblioteket.

var libraryPath = args.Length > 0
    ? args[0]
    : AppContext.BaseDirectory;

libraryPath = libraryPath.TrimEnd('\\', '/');

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("========================================");
Console.WriteLine(" Genstart Galleri");
Console.WriteLine("========================================");
Console.WriteLine();
Console.WriteLine($"Bibliotek: {libraryPath}");

if (!Directory.Exists(libraryPath))
{
    Console.WriteLine();
    Console.WriteLine($"FEJL: Mappen findes ikke: {libraryPath}");
    Console.WriteLine();
    Console.WriteLine("Tryk på en tast for at lukke...");
    if (!Console.IsInputRedirected)
    {
        try { Console.ReadKey(true); } catch (InvalidOperationException) { }
    }
    return 1;
}

Console.WriteLine();
Console.WriteLine("Bygger galleriet igen ud fra de filer der ligger her nu...");
Console.WriteLine("(dette kan tage flere minutter for et stort bibliotek)");
Console.WriteLine();

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    // Kun advarsler/fejl i konsollen - ellers drukner fremdriften i en
    // logline for hver eneste fil, når biblioteket har titusindvis af dem.
    builder.SetMinimumLevel(LogLevel.Warning);
});

var configuration = new ConfigurationBuilder().Build();

services.AddMediaInfrastructureCore(configuration);
services.AddFileSorterCore();

await using var provider = services.BuildServiceProvider();
var exportService = provider.GetRequiredService<IStaticGalleryExportService>();

// MediaDateService writes a raw Console.WriteLine per file for its own
// debugging purposes - fine in a dev server's log, but it would flood this
// customer-facing console with thousands of lines. Swallow stdout for the
// duration of the export only; our own progress/summary text goes straight
// to the real console before and after via realOut.
var realOut = Console.Out;
Console.SetOut(TextWriter.Null);

// Simple heartbeat so the window doesn't look frozen during a long run -
// no real progress data available from ExportAsync, just proof it's alive.
using var heartbeat = new Timer(_ =>
{
    realOut.Write(".");
}, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

var sw = System.Diagnostics.Stopwatch.StartNew();
var result = await exportService.ExportAsync(libraryPath);
sw.Stop();

Console.SetOut(realOut);
Console.WriteLine();

Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine(" Færdig!");
Console.WriteLine("========================================");
Console.WriteLine($"Filer fundet:          {result.TotalFiles}");
Console.WriteLine($"Nye thumbnails lavet:  {result.ThumbnailsGenerated}");
Console.WriteLine($"År-sider bygget:       {result.YearsGenerated}");
Console.WriteLine($"Tid brugt:             {sw.Elapsed:mm\\:ss}");
Console.WriteLine();
Console.WriteLine("Åbn index.html i denne mappe for at se det opdaterede galleri.");
Console.WriteLine();
Console.WriteLine("Tryk på en tast for at lukke...");
if (!Console.IsInputRedirected)
{
    try { Console.ReadKey(true); } catch (InvalidOperationException) { }
}
return 0;
