using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITMartinSuite.Maui.Models;
using ITMartinSuite.Maui.Services;

namespace ITMartinSuite.Maui.ViewModels;

public partial class TaskBoardViewModel : ObservableObject
{
    private readonly FamilieApiService _api;

    public TaskBoardViewModel(FamilieApiService api)
    {
        _api = api;
    }

    public ObservableCollection<FamilyTaskDto> Tasks { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var tasks = await _api.GetTodayAsync();
            Tasks.Clear();
            foreach (var t in tasks)
                Tasks.Add(t);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClaimAsync(FamilyTaskDto task)
    {
        var name = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(name)) return;
        await _api.ClaimAsync(task.Id, name);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CompleteAsync(FamilyTaskDto task)
    {
        await _api.CompleteAsync(task.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        await Shell.Current.GoToAsync("//familie/create");
    }

    public string GetPhotoUrl(string? photoPath) =>
        _api.GetPhotoUrl(photoPath);
}
