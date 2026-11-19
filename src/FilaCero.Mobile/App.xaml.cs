using FilaCero.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FilaCero.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly QueueStream _stream;

    public App(IServiceProvider services, QueueStream stream)
    {
        // La aplicación carga sus ResourceDictionary antes de construir páginas XAML.
        InitializeComponent();
        _services = services;
        _stream = stream;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolver las pestañas aquí evita que se creen durante la activación de App
        // y antes de que BigTitle/SubText/etc. estén disponibles en App.Resources.
        var tabs = _services.GetRequiredService<MainTabsPage>();
        var window = new Window(tabs);
        window.Activated += (_, _) => _stream.Start();
        window.Deactivated += (_, _) => _stream.Stop();
        return window;
    }
}
