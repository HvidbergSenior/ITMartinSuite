using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
