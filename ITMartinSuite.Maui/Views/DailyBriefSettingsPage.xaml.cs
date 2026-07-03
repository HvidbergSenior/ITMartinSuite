using ITMartinSuite.Maui.Models;
using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class DailyBriefSettingsPage : ContentPage
{
    private readonly DailyBriefViewModel _vm;
    private int _selectedCount;

    public DailyBriefSettingsPage(DailyBriefViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        _selectedCount = Preferences.Default.Get("brief_max_items", 10);
        SourcesView.ItemsSource = vm.Sources;
        HighlightCountButton(_selectedCount);
    }

    private void OnCountClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Text, out var n))
        {
            _selectedCount = n;
            HighlightCountButton(n);
        }
    }

    private void HighlightCountButton(int count)
    {
        var active   = Color.FromArgb("#3730A3");
        var inactive = Color.FromArgb("#1E2030");
        Btn5.BackgroundColor  = count == 5  ? active : inactive;
        Btn10.BackgroundColor = count == 10 ? active : inactive;
        Btn15.BackgroundColor = count == 15 ? active : inactive;
    }

    private void OnAddSourceClicked(object sender, EventArgs e)
    {
        var name = CustomNameEntry.Text?.Trim();
        var url  = CustomUrlEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) return;

        var source = new FeedSource { Name = name, RssUrl = url, Enabled = true };
        _vm.Sources.Add(source);
        SourcesView.ItemsSource = null;
        SourcesView.ItemsSource = _vm.Sources;

        CustomNameEntry.Text = "";
        CustomUrlEntry.Text  = "";
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        Preferences.Default.Set("brief_max_items", _selectedCount);

        // Save enabled state for each source
        foreach (var s in _vm.Sources)
            Preferences.Default.Set($"brief_source_{s.Id}", s.Enabled);

        // Save custom sources as JSON
        var customs = _vm.Sources
            .Where(s => !s.IsPreset)
            .Select(s => new { id = s.Id, name = s.Name, url = s.RssUrl })
            .ToList();
        Preferences.Default.Set("brief_custom_sources",
            System.Text.Json.JsonSerializer.Serialize(customs));

        // Reload feed with new settings
        await _vm.LoadCommand.ExecuteAsync(null);
        await Navigation.PopAsync();
    }
}
