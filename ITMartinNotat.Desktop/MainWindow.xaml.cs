using System.IO;
using System.Windows;
using System.Windows.Controls;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using WpfButton = System.Windows.Controls.Button;

namespace ITMartinNotat;

public partial class MainWindow : Window
{
    private Pseudonymizer _pseudo = new();
    private CancellationTokenSource? _cts;

    // Made-up example (no real person) so she can try the program before using it on real notes.
    private const string ExampleNames = "Grethe Madsen, Ole Madsen";
    private const string ExampleKeywords = """
        Grethe Madsen 0304421234
        besøg 12.10.2026 kl 9
        træt, sov dårligt, vågen 3x nat
        smerter hø knæ 6/10, panodil 1g x3 virker lidt
        BT 145/85, p 78
        spist halv havregrød, drukket 2 glas vand
        mand Ole ringet 22 33 44 55 - bekymret, vil gerne have læge ser knæ
        plan: ringe læge i dag, obs fald, ny vurdering i morgen
        """;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var s in NoteWriter.Styles) NoteStyle.Items.Add(s);
        NoteStyle.SelectedIndex = 0;
        ApplyMode();
    }

    /// <summary>Local AI (default): nothing leaves the PC, so no hiding/check step. Cloud: hide + check before sending.</summary>
    private void ApplyMode()
    {
        var cloud = AppMode.Cloud;
        Subtitle.Text = cloud
            ? "Lav dine stikord om til læsbar journaltekst. Sky-AI: navne, CPR, datoer og adresser fjernes på din PC, før noget sendes – og sættes ind igen bagefter."
            : "Lav dine stikord om til læsbar journaltekst. 🔒 Lokal AI: alt bliver på din PC.";
        Step2Title.Text = cloud ? "2. Tjek hvad der sendes" : "2. Vælg form";
        Step2Help.Visibility = Outgoing.Visibility = Checked.Visibility = NamesPanel.Visibility = cloud ? Visibility.Visible : Visibility.Collapsed;
        LocalBox.Visibility = cloud ? Visibility.Collapsed : Visibility.Visible;
        DownloadPanel.Visibility = !cloud && LocalWriter.FindModel() is null ? Visibility.Visible : Visibility.Collapsed;
        RefreshOutgoing();
        Status.Text = cloud
            ? KeyStore.Load() is null ? "Sky-AI: tryk ⚙ Indstillinger og indsæt API-nøglen." : "Sky-AI er valgt. Træk en fil ind, eller skriv dine stikord i felt 1."
            : LocalWriter.FindModel() is null ? "Første gang: tryk ⬇ Hent AI-model i felt 2." : "Klar. Træk en fil ind i vinduet, eller skriv/indsæt dine stikord i felt 1.";
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        DownloadBtn.IsEnabled = false;
        DownloadBar.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            DownloadBar.Value = p;
            Status.Text = $"Henter AI-model … {p:P0} ({p * LocalWriter.ModelBytes / 1e9:F1} af {LocalWriter.ModelBytes / 1e9:F1} GB). Du kan godt bruge PC'en imens.";
        });
        try
        {
            await LocalWriter.DownloadAsync(progress, CancellationToken.None);
            ApplyMode();
            Status.Text = "✅ AI-modellen er hentet. Fra nu af virker programmet uden internet.";
        }
        catch (Exception ex)
        {
            Status.Text = $"Kunne ikke hente AI-modellen: {ex.Message} – arbejdspladsen kan blokere store downloads; IT kan lægge filen {LocalWriter.ModelFile} ved siden af programmet.";
        }
        finally
        {
            DownloadBtn.IsEnabled = true;
            DownloadBar.Visibility = Visibility.Collapsed;
        }
    }

    // ── Step 1: input ──
    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vælg dine stikord",
            Filter = "Stikord (Word, tekst, billeder)|*.docx;*.txt;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Alle filer|*.*",
        };
        if (dlg.ShowDialog(this) == true) await LoadFileAsync(dlg.FileName);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            foreach (var f in files) await LoadFileAsync(f, append: files.Length > 1);
    }

    private async Task LoadFileAsync(string path, bool append = false)
    {
        try
        {
            var isImage = InputReader.ImageExt.Contains(Path.GetExtension(path).ToLowerInvariant());
            Status.Text = isImage ? "Læser billedet på din PC …" : "Læser filen …";
            var text = await InputReader.ReadAsync(path);
            Keywords.Text = append && Keywords.Text.Length > 0 ? Keywords.Text + Environment.NewLine + text : text;
            Status.Text = isImage
                ? "Billedet er læst på din PC (intet er sendt). Håndskrift bliver ofte læst forkert – ret teksten i felt 1."
                : $"Indlæst: {Path.GetFileName(path)}. Skriv navnene der skal skjules nederst i felt 1.";
        }
        catch (Exception ex)
        {
            Status.Text = "Kunne ikke læse filen: " + ex.Message;
        }
    }

    private void Example_Click(object sender, RoutedEventArgs e)
    {
        Names.Text = ExampleNames;
        Keywords.Text = ExampleKeywords;
        Status.Text = "Eksemplet er opdigtet. Se i felt 2, hvordan navne, CPR, dato og telefon er skjult.";
    }

    // ── Step 2: what will be sent ──
    private void Input_Changed(object sender, TextChangedEventArgs e) => RefreshOutgoing();

    private void RefreshOutgoing()
    {
        if (Outgoing is null) return;
        _pseudo = new Pseudonymizer();
        var names = Names.Text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Outgoing.Text = _pseudo.Hide(Keywords.Text, names);
        Checked.IsChecked = false;

        MaybeNames.Children.Clear();
        // Local AI: nothing is sent, so there is nothing to hide.
        foreach (var word in AppMode.Cloud ? Pseudonymizer.PossibleNames(Outgoing.Text).Take(30) : [])
        {
            var b = new WpfButton { Content = "🙈 " + word, Tag = word, Style = (System.Windows.Style)FindResource("Plain"), Margin = new Thickness(0, 0, 6, 6), FontSize = 14, Padding = new Thickness(10, 4, 10, 4) };
            b.Click += (_, _) => { Names.Text = string.IsNullOrWhiteSpace(Names.Text) ? (string)b.Tag : Names.Text.TrimEnd(' ', ',') + ", " + b.Tag; };
            MaybeNames.Children.Add(b);
        }
        MaybeHeader.Visibility = MaybeNames.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateWriteButton();
    }

    private void Checked_Changed(object sender, RoutedEventArgs e) => UpdateWriteButton();

    private void UpdateWriteButton() =>
        WriteBtn.IsEnabled = _cts is null && Keywords.Text.Trim().Length > 0 &&
                             (AppMode.Cloud ? Checked.IsChecked == true : LocalWriter.FindModel() is not null);

    private async void Write_Click(object sender, RoutedEventArgs e)
    {
        var cloud = AppMode.Cloud;
        string? key = null;
        if (cloud)
        {
            key = KeyStore.Load();
            if (key is null) { Settings_Click(sender, e); key = KeyStore.Load(); if (key is null) return; }
        }

        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(cloud ? 3 : 10));
        UpdateWriteButton();
        WriteBtn.Content = "⏳ Skriver …";
        Status.Text = cloud
            ? "AI'en skriver notatet (typisk 10-30 sekunder) …"
            : "Den lokale AI skriver på din PC (typisk 20-90 sekunder, første gang lidt længere). Teksten dukker op i felt 3 undervejs …";
        CopyBtn.IsEnabled = SaveBtn.IsEnabled = false;
        try
        {
            List<string> missing = [];
            if (cloud)
            {
                var draft = await NoteWriter.WriteAsync(key!, Outgoing.Text, (string)NoteStyle.SelectedItem, Extra.Text, _cts.Token);
                Result.Text = _pseudo.Restore(draft, out missing);
            }
            else
            {
                Result.Clear();
                var note = await LocalWriter.WriteAsync(Keywords.Text, (string)NoteStyle.SelectedItem, Extra.Text,
                    piece => Dispatcher.BeginInvoke(() => { Result.AppendText(piece); Result.ScrollToEnd(); }), _cts.Token);
                // Let the streamed pieces land first, then show the cleaned-up final text.
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                Result.Text = note;
            }
            CopyBtn.IsEnabled = SaveBtn.IsEnabled = true;
            Status.Text = missing.Count == 0
                ? "✅ Færdigt. Læs notatet igennem, ret hvis nødvendigt, og tryk 📋 Kopiér → indsæt i journalen (Ctrl+V)."
                : "⚠ Færdigt, men disse oplysninger kom ikke med i notatet – tjek om de mangler: " + string.Join("; ", missing);
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Det tog for lang tid. Prøv igen.";
        }
        catch (Exception ex)
        {
            Status.Text = "❌ " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            WriteBtn.Content = "✨ Lav notat";
            UpdateWriteButton();
        }
    }

    // ── Step 3: result ──
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Result.Text);
        Status.Text = "📋 Kopieret. Gå til journalen og tryk Ctrl+V.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = "Gem notat", Filter = "Word-dokument|*.docx", FileName = $"Notat {DateTime.Now:yyyy-MM-dd HH.mm}.docx" };
        if (dlg.ShowDialog(this) != true) return;
        using (var doc = WordprocessingDocument.Create(dlg.FileName, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var body = new Body();
            foreach (var line in Result.Text.Replace("\r", "").Split('\n'))
                body.Append(new Paragraph(new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve })));
            main.Document = new Document(body);
        }
        Status.Text = "💾 Gemt: " + dlg.FileName + " – husk at filen indeholder patientdata. Slet den, når den er overført.";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Keywords.Clear(); Names.Clear(); Extra.Clear(); Result.Clear();
        CopyBtn.IsEnabled = SaveBtn.IsEnabled = false;
        Clipboard.Clear();
        Status.Text = "Alt er ryddet (også udklipsholderen). Programmet gemmer intet om patienter.";
    }

    // ── Settings + help ──
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var local = new RadioButton
        {
            FontSize = 15, Margin = new Thickness(0, 8, 0, 4), IsChecked = !AppMode.Cloud,
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "🔒 Lokal AI på denne PC (anbefalet) – intet forlader PC'en. Langsommere og enklere sprog." },
        };
        var cloudRb = new RadioButton
        {
            FontSize = 15, Margin = new Thickness(0, 4, 0, 4), IsChecked = AppMode.Cloud,
            Content = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "☁ Sky-AI (Anthropic) – bedre og hurtigere, men KUN hvis din arbejdsplads har godkendt det (databehandleraftale)." },
        };
        var box = new PasswordBox { FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 12) };
        var ok = new WpfButton { Content = "Gem", Style = (System.Windows.Style)FindResource("Big"), HorizontalAlignment = HorizontalAlignment.Left, IsDefault = true };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Hvilken AI skal skrive notatet?", FontSize = 18, FontWeight = FontWeights.Bold });
        panel.Children.Add(local);
        panel.Children.Add(cloudRb);
        panel.Children.Add(new TextBlock { Text = "API-nøgle (kun til sky-AI)", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 0) });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 6, 0, 0),
            Text = "Lad feltet stå tomt ved lokal AI. Nøglen får du af den, der har sat programmet op (din arbejdsplads' aftale med Anthropic). " +
                   "Den gemmes krypteret, så kun din Windows-bruger kan læse den.",
        });
        panel.Children.Add(box);
        panel.Children.Add(ok);
        var win = new Window
        {
            Title = "Indstillinger", Owner = this, Content = panel, Width = 520, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("Bg"),
        };
        ok.Click += (_, _) =>
        {
            if (box.Password.Length > 0)
            {
                if (box.Password.Trim().Length < 20) { MessageBox.Show(win, "Det ligner ikke en API-nøgle.", "Indstillinger"); return; }
                KeyStore.Save(box.Password);
            }
            if (cloudRb.IsChecked == true && KeyStore.Load() is null) { MessageBox.Show(win, "Sky-AI kræver en API-nøgle.", "Indstillinger"); return; }
            AppMode.Cloud = cloudRb.IsChecked == true;
            win.DialogResult = true;
        };
        if (win.ShowDialog() == true) ApplyMode();
    }

    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, """
        Sådan laver du et notat (lokal AI):

        1. Første gang: tryk ⬇ Hent AI-model i felt 2 (2,5 GB, kun én gang). Derefter virker det uden internet.
        2. Træk din fil med stikord ind i vinduet (Word, tekstfil eller billede af dine noter) – eller skriv/indsæt dem i felt 1.
        3. Vælg form i felt 2 (fx Kort resumé) og tryk ✨ Lav notat.
        4. Vent 20-90 sekunder. Teksten dukker op i felt 3, mens den skrives.
        5. Læs notatet ord for ord, og ret det. Den lokale AI kan bytte rundt på, hvem der gjorde hvad, eller tilføje ting, der ikke stod i stikordene.
        6. Tryk 📋 Kopiér og indsæt i journalen med Ctrl+V. Tryk 🧹 Ryd alt, når du er færdig.

        Ulemper ved lokal AI
        • Langsom: 20-90 sekunder pr. notat, og PC'en arbejder hårdt imens (blæseren kan gå i gang).
        • Kræver ca. 4 GB fri hukommelse (RAM) og 3 GB diskplads.
        • Enklere sprog og flere fejl end sky-AI – du skal altid læse efter.
        • Billeder af håndskrift læses ofte forkert – ret teksten i felt 1 først.

        Hvad gemmer programmet?
        Intet om patienter. Stikord og notater forsvinder, når du lukker programmet eller trykker 🧹 Ryd alt.

        Må jeg bruge det på min arbejds-PC?
        Spørg din leder eller IT, om du må installere programmer. Selve teksten bliver på PC'en.

        AI-modellen er Google Gemma 3 (vilkår: ai.google.dev/gemma/terms).
        """, "Sådan gør du");
}
