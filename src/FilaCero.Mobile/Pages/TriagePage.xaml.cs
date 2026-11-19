using FilaCero.Domain;
using FilaCero.Mobile.Services;

namespace FilaCero.Mobile.Pages;

public partial class TriagePage : ContentPage
{
    private readonly AppState _state;
    private readonly FilaCeroApi _api;
    public TriagePage(AppState state,FilaCeroApi api)
    {
        InitializeComponent();_state=state;_api=api;
        _state.Changed += (_,_) => Draw();
        Draw();
    }
    private void Draw()
    {
        SendButton.IsEnabled=_state.Patient is not null && _state.Turn is null;
        TriageResult.Text=_state.Triage is null?"El personal de la clínica debe verificar toda prioridad.":
            $"{_state.Triage.Title}: {_state.Triage.Message}";
    }
    private async void SendClicked(object sender,EventArgs e)
    {
        SendButton.IsEnabled=false;
        try
        {
            int? ReadPressure(string? s)
            {
                if(string.IsNullOrWhiteSpace(s)) return null;
                if(int.TryParse(s.Trim(),out var n)) return n;
                throw new ArgumentException("Introduce números válidos para la presión arterial.");
            }
            var answers=new TriageAnswers(ChestPain.IsToggled,Breathing.IsToggled,Unconscious.IsToggled,
                Fever.IsToggled,SeverePain.IsToggled,Dizziness.IsToggled,
                ReadPressure(Systolic.Text),ReadPressure(Diastolic.Text),Description.Text);
            var triage=(await _api.EvaluateAsync(answers)).Triage;
            _state.SetTriage(triage);
            await DisplayAlert(triage.Title,triage.Message,"Aceptar");
            if(triage.Color!="rojo") (Shell.Current as MainTabsPage)?.GoTo(3);
        }
        catch(Exception ex) {await DisplayAlert("Triaje",ex.Message,"Aceptar");}
        finally {Draw();}
    }
}
