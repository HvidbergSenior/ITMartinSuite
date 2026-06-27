using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
