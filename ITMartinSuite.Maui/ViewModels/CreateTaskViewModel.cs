using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITMartinSuite.Maui.Services;

namespace ITMartinSuite.Maui.ViewModels;

public partial class CreateTaskViewModel : ObservableObject
{
    private readonly FamilieApiService _api;

    public CreateTaskViewModel(FamilieApiService api)
    {
        _api = api;
    }

    [ObservableProperty]
    private ImageSource? _photoPreview;

    [ObservableProperty]
    private string _selectedType = "Task";

    [ObservableProperty]
    private string? _note;

    [ObservableProperty]
    private bool _isBusy;

    private FileResult? _photoFile;

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        var result = await MediaPicker.CapturePhotoAsync();
        if (result is null) return;

        _photoFile = result;
        await using var stream = await result.OpenReadAsync();
        PhotoPreview = ImageSource.FromStream(() =>
        {
            var s = result.OpenReadAsync().GetAwaiter().GetResult();
            return s;
        });
    }

    [RelayCommand]
    private void SetType(string type)
    {
        SelectedType = type;
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        var name = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(name)) return;

        IsBusy = true;
        try
        {
            Stream? stream = null;
            string? fileName = null;

            if (_photoFile is not null)
            {
                stream = await _photoFile.OpenReadAsync();
                fileName = _photoFile.FileName;
            }

            await _api.CreateTaskAsync(SelectedType, Note, stream, fileName, name);
            stream?.Dispose();

            Note = null;
            PhotoPreview = null;
            _photoFile = null;
            SelectedType = "Task";

            await Shell.Current.GoToAsync("//familie/board");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
