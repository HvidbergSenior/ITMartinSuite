using ITMartinSuite.Maui.Views;

namespace ITMartinSuite.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("familie/create", typeof(CreateTaskPage));
        Routing.RegisterRoute("onboarding", typeof(OnboardingPage));
    }
}
