using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITMartinSuite.Maui.Models;
using ITMartinSuite.Maui.Services;

namespace ITMartinSuite.Maui.ViewModels;

public partial class DailyBriefViewModel : ObservableObject
{
    private readonly FeedService _feed;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _hasError;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _summary      = "";

    public ObservableCollection<FeedItem> Items { get; } = [];

    // Loaded from preferences
    public List<FeedSource> Sources { get; private set; } = [];
    public int              MaxItems { get; private set; } = 10;

    public DailyBriefViewModel(FeedService feed) => _feed = feed;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError  = false;
        Items.Clear();

        LoadPreferences();

        try
        {
            var items = await _feed.GetItemsAsync(Sources, MaxItems);
            foreach (var item in items)
                Items.Add(item);

            var enabledCount = Sources.Count(s => s.Enabled);
            Summary = $"{items.Count} artikler fra {enabledCount} {(enabledCount == 1 ? "kilde" : "kilder")}";
        }
        catch (Exception ex)
        {
            HasError     = true;
            ErrorMessage = "Kunne ikke hente nyheder: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task OpenArticleAsync(FeedItem item)
    {
        if (!string.IsNullOrEmpty(item.Url))
            await Browser.Default.OpenAsync(item.Url, BrowserLaunchMode.SystemPreferred);
    }

    public void LoadPreferences()
    {
        MaxItems = Preferences.Default.Get("brief_max_items", 10);

        var presets = FeedSource.Presets;
        Sources = presets.Select(p =>
        {
            p.Enabled = Preferences.Default.Get($"brief_source_{p.Id}", true);
            return p;
        }).ToList();

        // Load any custom sources saved as JSON
        var customJson = Preferences.Default.Get("brief_custom_sources", "");
        if (!string.IsNullOrEmpty(customJson))
        {
            try
            {
                var customs = System.Text.Json.JsonSerializer.Deserialize<List<CustomSourceDto>>(customJson);
                if (customs is not null)
                    foreach (var c in customs)
                        Sources.Add(new FeedSource
                        {
                            Id      = c.Id,
                            Name    = c.Name,
                            RssUrl  = c.Url,
                            Color   = Colors.DimGray,
                            Enabled = Preferences.Default.Get($"brief_source_{c.Id}", true),
                        });
            }
            catch { /* ignore corrupt prefs */ }
        }
    }

    private record CustomSourceDto(string Id, string Name, string Url);
}
