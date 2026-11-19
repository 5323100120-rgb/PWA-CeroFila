using FilaCero.Mobile.Pages;
using FilaCero.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace FilaCero.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts => { });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<FilaCeroApi>();
        builder.Services.AddSingleton<QueueStream>();
        builder.Services.AddSingleton<MainTabsPage>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<RegisterPage>();
        builder.Services.AddSingleton<TriagePage>();
        builder.Services.AddSingleton<TicketPage>();
        builder.Services.AddSingleton<SettingsPage>();
        return builder.Build();
    }
}
