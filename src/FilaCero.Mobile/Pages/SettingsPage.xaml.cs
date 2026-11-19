using FilaCero.Mobile.Services;

namespace FilaCero.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly AppState _state;
    private readonly FilaCeroApi _api;

    public SettingsPage(AppState state, FilaCeroApi api)
    {
        InitializeComponent();
        _state = state;
        _api = api;
        _state.Changed += (_, _) => Draw();
        Draw();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _state.InitializeAsync();
        Draw();
        await CheckServiceAsync();
    }

    private void Draw()
    {
        SavedPassLabel.Text = _state.Turn is null
            ? "Todavía no hay un pase guardado en este teléfono."
            : $"Pase {_state.Turn.Code} guardado en el teléfono. Podrás mostrarlo sin conexión.";
    }

    private async void CheckServiceClicked(object sender, EventArgs e)
        => await CheckServiceAsync();

    private async Task CheckServiceAsync()
    {
        CheckButton.IsEnabled = false;
        try
        {
            if (string.IsNullOrWhiteSpace(_state.ServerUrl))
            {
                HealthLabel.Text = "El servicio aún no está habilitado. Tu último pase seguirá disponible sin conexión.";
                return;
            }

            HealthLabel.Text = "Comprobando disponibilidad del servicio...";
            await _api.HealthAsync();
            HealthLabel.Text = "Servicio disponible. Ya puedes utilizar FilaCero.";
        }
        catch (Exception)
        {
            HealthLabel.Text = "No se pudo conectar en este momento. Comprueba tu internet e inténtalo de nuevo.";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void ClearClicked(object sender, EventArgs e)
    {
        if (!await DisplayAlert("Eliminar datos locales",
            "¿Deseas eliminar la sesión y el último pase guardado en este teléfono?",
            "Eliminar", "Cancelar")) return;

        await _state.ClearAsync();
        (Shell.Current as MainTabsPage)?.Reconnect();
        Draw();
        await DisplayAlert("Datos eliminados",
            "Se borraron los datos locales. La información del servidor no cambió.", "Aceptar");
    }
}
