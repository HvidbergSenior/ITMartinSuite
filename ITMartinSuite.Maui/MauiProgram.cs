using ITMartinSuite.Maui.Services;
using ITMartinSuite.Maui.ViewModels;
using ITMartinSuite.Maui.Views;
using Microsoft.Extensions.Logging;

namespace ITMartinSuite.Maui;

public static class MauiProgram
{
    // Android emulator uses 10.0.2.2 to reach localhost on the PC
    // Change to NAS IP (e.g. http://10.0.0.126:5110/) when running on real device
    private const string ApiBaseUrl = "http://10.0.2.2:5110/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddHttpClient<FamilieApiService>(client =>
        {
            client.BaseAddress = new Uri(ApiBaseUrl);
        });

        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<TaskBoardViewModel>();
        builder.Services.AddTransient<CreateTaskViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<DailyBriefViewModel>();

        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<TaskBoardPage>();
        builder.Services.AddTransient<CreateTaskPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<DailyBriefPage>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddSingleton<FeedService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
