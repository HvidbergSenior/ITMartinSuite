using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ITMartinSuite.Maui.ViewModels;

public partial class OnboardingViewModel : ObservableObject
{
    public List<string> FamilyMembers { get; } =
    [
        "Martin", "Vibz", "Julius", "Eigil", "Bertil"
    ];

    [RelayCommand]
    private async Task SelectNameAsync(string name)
    {
        Preferences.Set("UserName", name);
        await Shell.Current.GoToAsync("//familie/board");
    }
}
