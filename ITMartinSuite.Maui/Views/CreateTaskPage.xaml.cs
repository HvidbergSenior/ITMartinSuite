using ITMartinSuite.Maui.ViewModels;

namespace ITMartinSuite.Maui.Views;

public partial class CreateTaskPage : ContentPage
{
    public CreateTaskPage(CreateTaskViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
