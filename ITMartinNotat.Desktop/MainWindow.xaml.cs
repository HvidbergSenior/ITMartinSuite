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
        if (KeyStore.Load() is null)
            Status.Text = "Første gang: tryk ⚙ Indstillinger og indsæt API-nøglen. Du kan stadig prøve trin 1 og 2 uden.";
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
        foreach (var word in Pseudonymizer.PossibleNames(Outgoing.Text).Take(30))
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
        WriteBtn.IsEnabled = Checked.IsChecked == true && Outgoing.Text.Trim().Length > 0 && _cts is null;

    private async void Write_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyStore.Load();
        if (key is null) { Settings_Click(sender, e); key = KeyStore.Load(); if (key is null) return; }

        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        UpdateWriteButton();
        WriteBtn.Content = "⏳ Skriver …";
        Status.Text = "AI'en skriver notatet (typisk 10-30 sekunder) …";
        try
        {
            var draft = await NoteWriter.WriteAsync(key, Outgoing.Text, (string)NoteStyle.SelectedItem, Extra.Text, _cts.Token);
            Result.Text = _pseudo.Restore(draft, out var missing);
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
        var box = new PasswordBox { FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 12) };
        var ok = new WpfButton { Content = "Gem", Style = (System.Windows.Style)FindResource("Big"), HorizontalAlignment = HorizontalAlignment.Left, IsDefault = true };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "API-nøgle til AI-tjenesten (Anthropic)", FontSize = 18, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 6, 0, 0),
            Text = "Nøglen får du af den, der har sat programmet op (din arbejdsplads' aftale med Anthropic). " +
                   "Den gemmes krypteret, så kun din Windows-bruger kan læse den.",
        });
        panel.Children.Add(box);
        panel.Children.Add(ok);
        var win = new Window
        {
            Title = "Indstillinger", Owner = this, Content = panel, Width = 480, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            Background = (System.Windows.Media.Brush)FindResource("Bg"),
        };
        ok.Click += (_, _) =>
        {
            if (box.Password.Trim().Length < 20) { MessageBox.Show(win, "Det ligner ikke en API-nøgle.", "Indstillinger"); return; }
            KeyStore.Save(box.Password);
            win.DialogResult = true;
        };
        if (win.ShowDialog() == true) Status.Text = "🔑 Nøglen er gemt.";
    }

    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, """
        Sådan laver du et notat:

        1. Træk din fil med stikord ind i vinduet (Word, tekstfil eller billede af dine noter) – eller skriv/indsæt dem i felt 1.
        2. Skriv navnene på patient, pårørende og kolleger i feltet "Navne der skal skjules", adskilt med komma.
        3. Kig felt 2 igennem: det er PRÆCIS den tekst, der sendes. CPR, telefon, datoer, adresser og navne er byttet ud med fx [NAVN1].
           Klik på 🙈-knapperne, hvis programmet har fundet ord, der kan være navne.
        4. Sæt flueben i "Jeg har tjekket teksten" og tryk ✨ Lav notat.
        5. Læs notatet i felt 3, ret det hvis nødvendigt, tryk 📋 Kopiér og indsæt i journalen med Ctrl+V.
        6. Tryk 🧹 Ryd alt, når du er færdig.

        Hvad gemmer programmet?
        Intet om patienter. Kun API-nøglen gemmes (krypteret). Billeder læses af Windows på din egen PC.

        Må jeg bruge det på rigtige patienter?
        Kun når din arbejdsplads har godkendt det (databehandleraftale og risikovurdering). Indtil da: brug 🧪 eksemplet.
        """, "Sådan gør du");
}
