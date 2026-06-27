using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class TaskBoardPage : ContentPage
{
    private readonly TaskBoardViewModel _vm;

    public TaskBoardPage(TaskBoardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var name = Preferences.Get("UserName", "");
        if (string.IsNullOrEmpty(name))
        {
            await Shell.Current.GoToAsync("onboarding");
            return;
        }

        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
