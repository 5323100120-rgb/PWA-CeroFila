using Microsoft.Maui.Storage;
using System.Text.RegularExpressions;
using FilaCero.Domain;
using FilaCero.Mobile.Services;

namespace FilaCero.Mobile.Pages;

public partial class RegisterPage : ContentPage
{
    private sealed record ClinicCardInfo(string ApiName, string DisplayName, string Address, string Distance, string WaitTime);

    private readonly FilaCeroApi _api;
    private readonly AppState _state;
    private DocumentUpload? _document;
    private string _selectedClinic = ClinicNames.All[0];

    private readonly List<ClinicCardInfo> _clinics =
    [
        new(ClinicNames.All[0], "Consultorio Similares Centro", "Calle Guerrero #412, Zona Centro", "0.6 km", "~10 min espera"),
        new(ClinicNames.All[1], "Consultorio Similares Norte", "Blvd. Rodolfo Elías Calles #1205, Norte", "1.9 km", "~15 min espera"),
        new(ClinicNames.All[2], "Consultorio Similares Sur", "Calz. Francisco Villanueva #890, Sur", "2.8 km", "~5 min espera")
    ];

    public RegisterPage(AppState state, FilaCeroApi api)
    {
        InitializeComponent();
        _state = state;
        _api = api;

        BirthDatePicker.MaximumDate = DateTime.Today;
        BirthDatePicker.Date = new DateTime(2005, 1, 1);

        RenderClinics("");
    }

    private void OnClinicSearchChanged(object? sender, TextChangedEventArgs e)
    {
        RenderClinics(e.NewTextValue ?? "");
    }

    private void RenderClinics(string filter)
    {
        ClinicsListContainer.Children.Clear();
        var query = filter.Trim();

        var filtered = string.IsNullOrEmpty(query)
            ? _clinics
            : _clinics.Where(c =>
                c.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Address.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.ApiName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            ClinicsListContainer.Children.Add(new Label
            {
                Text = "No se encontraron consultorios con esa búsqueda.",
                Style = (Style)Application.Current!.Resources["SubText"]
            });
            return;
        }

        foreach (var clinic in filtered)
        {
            bool isSelected = clinic.ApiName == _selectedClinic;

            var card = new Border
            {
                BackgroundColor = isSelected ? Color.FromArgb("#DFF8EF") : Color.FromArgb("#F7FAFA"),
                Stroke = isSelected ? Color.FromArgb("#0A8479") : Color.FromArgb("#E8EDF1"),
                StrokeThickness = isSelected ? 2 : 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 12)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto)
                },
                RowSpacing = 4,
                ColumnSpacing = 8
            };

            var titleLabel = new Label
            {
                Text = (isSelected ? "✓ " : "") + clinic.DisplayName,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#152943")
            };

            var distBadge = new Label
            {
                Text = clinic.Distance,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = Color.FromArgb("#0A8479"),
                HorizontalOptions = LayoutOptions.End
            };

            var addressLabel = new Label
            {
                Text = $"{clinic.Address} · {clinic.WaitTime}",
                FontSize = 12,
                TextColor = Color.FromArgb("#68798B")
            };

            grid.Add(titleLabel, 0, 0);
            grid.Add(distBadge, 1, 0);
            grid.Add(addressLabel, 0, 1);
            Grid.SetColumnSpan(addressLabel, 2);

            card.Content = grid;

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _selectedClinic = clinic.ApiName;
                RenderClinics(ClinicSearchEntry.Text ?? "");
            };
            card.GestureRecognizers.Add(tap);

            ClinicsListContainer.Children.Add(card);
        }
    }

    private async void ChooseDocument(object sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync();
            if (file is null) return;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mime = ext switch { ".pdf" => "application/pdf", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", _ => null };
            if (mime is null) throw new InvalidOperationException("Solo se aceptan archivos PDF, JPG y PNG.");
            await using var input = await file.OpenReadAsync();
            if (input.CanSeek && input.Length > 1_048_576) throw new InvalidOperationException("El archivo supera 1 MB.");
            using var data = new MemoryStream();
            await input.CopyToAsync(data);
            if (data.Length > 1_048_576) throw new InvalidOperationException("El archivo supera 1 MB.");
            var safeName = Regex.Replace(file.FileName, @"[^a-zA-Z0-9_\- .()]", "_");
            _document = new DocumentUpload(safeName, mime, Convert.ToBase64String(data.ToArray()));
            DocumentLabel.Text = safeName;
        }
        catch (Exception ex) { await DisplayAlert("Documento", ex.Message, "Aceptar"); }
    }

    private async void RegisterClicked(object sender, EventArgs e)
    {
        RegisterButton.IsEnabled = false;
        try
        {
            // Se envía la fecha de nacimiento formateada a 11 dígitos para mantener compatibilidad con la API
            var birthFolio = (BirthDatePicker.Date ?? new DateTime(2005, 1, 1)).ToString("yyyyMMdd") + "001";

            var input = RegistrationRules.Normalize(new RegistrationRequest(
                FullNameEntry.Text ?? "",
                "",
                birthFolio,
                "",
                _selectedClinic,
                _document));

            var issues = RegistrationRules.Validate(input);
            if (issues.Count > 0)
            {
                await DisplayAlert("Revisa tus datos", string.Join("\n", issues.Values), "Aceptar");
                return;
            }

            var result = await _api.RegisterAsync(input);
            await _state.SetRegisteredAsync(result);
            await DisplayAlert("Consultorio asignado", "Ahora responde tu pre-triaje rápido.", "Continuar");
            (Shell.Current as MainTabsPage)?.Reconnect();
            (Shell.Current as MainTabsPage)?.GoTo(2);
        }
        catch (Exception ex) { await DisplayAlert("Registro", ex.Message, "Aceptar"); }
        finally { RegisterButton.IsEnabled = true; }
    }
}