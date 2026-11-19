using FilaCero.Mobile.Pages;
using FilaCero.Mobile.Services;

namespace FilaCero.Mobile;

/// <summary>
/// Navegación inferior nativa de MAUI Shell. Cada Tab contiene un solo
/// ShellContent para evitar pestañas adicionales en la parte superior.
/// </summary>
public sealed class MainTabsPage : Shell
{
    private readonly AppState _state;
    private readonly FilaCeroApi _api;
    private readonly QueueStream _realtime;
    private readonly TabBar _bottomTabs;
    private bool _initialized;

    public MainTabsPage(AppState state, FilaCeroApi api, QueueStream realtime,
        HomePage home, RegisterPage register, TriagePage triage, TicketPage ticket, SettingsPage settings)
    {
        _state = state;
        _api = api;
        _realtime = realtime;

        // Shell usa tabs inferiores en Android e iOS. Ocultamos su barra superior.
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarBackgroundColor(this, Colors.White);
        Shell.SetTabBarForegroundColor(this, Color.FromArgb("#0A8479"));
        Shell.SetTabBarTitleColor(this, Color.FromArgb("#0A8479"));
        Shell.SetTabBarUnselectedColor(this, Color.FromArgb("#68798B"));

        _bottomTabs = new TabBar { Route = "principal" };
        _bottomTabs.Items.Add(CreateTab("Inicio", "tab_inicio.png", "inicio", home));
        _bottomTabs.Items.Add(CreateTab("Registro", "tab_registro.png", "registro", register));
        _bottomTabs.Items.Add(CreateTab("Triaje", "tab_triaje.png", "triaje", triage));
        _bottomTabs.Items.Add(CreateTab("Mi turno", "tab_turno.png", "turno", ticket));
        _bottomTabs.Items.Add(CreateTab("Ajustes", "tab_ajustes.png", "ajustes", settings));
        Items.Add(_bottomTabs);
    }

    private static Tab CreateTab(string title, string icon, string route, ContentPage page)
    {
        // La barra de navegación del encabezado está desactivada en todas las pantallas.
        Shell.SetNavBarIsVisible(page, false);
        return new Tab
        {
            Title = title,
            Route = route,
            Icon = icon,
            Items =
            {
                new ShellContent { Title = title, Route = route + "_contenido", Content = page }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        await _state.InitializeAsync();
        await _state.RefreshAsync(_api);
        _realtime.Start();
    }

    public void GoTo(int index)
    {
        if (index >= 0 && index < _bottomTabs.Items.Count)
            _bottomTabs.CurrentItem = _bottomTabs.Items[index];
    }

    public void Reconnect() => _realtime.Start();
}
