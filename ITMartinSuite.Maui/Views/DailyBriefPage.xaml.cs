using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class DailyBriefPage : ContentPage
{
    private readonly DailyBriefViewModel _vm;

    public DailyBriefPage(DailyBriefViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_vm.Items.Any())
            await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new DailyBriefSettingsPage(_vm));
    }
}
