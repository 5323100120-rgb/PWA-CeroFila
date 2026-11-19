using FilaCero.Domain;
using FilaCero.Mobile.Services;
using QRCoder;

namespace FilaCero.Mobile.Pages;

public partial class TicketPage : ContentPage
{
    private readonly AppState _state;
    private readonly FilaCeroApi _api;
    private string? _lastQr;
    public TicketPage(AppState state,FilaCeroApi api)
    {
        InitializeComponent();_state=state;_api=api;
        _state.Changed += (_,_) => Draw();
        Draw();
    }
    private void Draw()
    {
        var turn=_state.Turn;
        CachedBadge.Text=turn is null?"Aún no tienes un pase":_state.OfflinePass?"ÚLTIMO PASE GUARDADO • SIN CONEXIÓN":"PASE DIGITAL GUARDADO";
        CodeLabel.Text=turn?.Code??"---";
        StatusLabel.Text=turn?.Status??"Sin turno";
        ClinicLabel.Text=turn?.Clinic??"Completa registro y cuestionario";
        RoomLabel.Text=turn?.Room??"";
        QueueLabel.Text=turn is null?"":$"Personas delante: {turn.QueueAhead}";
        IssueButton.IsVisible=turn is null;
        IssueButton.Text=_state.Patient is null?"Primero regístrate":_state.Triage is null?"Primero responde triaje":"Generar turno QR";
        IssueButton.IsEnabled=_state.Patient is not null && _state.Triage is not null && _state.Triage.Color!="rojo";
        RefreshButton.IsVisible=turn is not null;
        RefreshButton.IsEnabled=!string.IsNullOrWhiteSpace(_state.SessionToken);
        QrImage.IsVisible=turn is not null;
        if(turn is not null && turn.QrPayload!=_lastQr)
        {
            using var generator=new QRCodeGenerator();
            using var code=generator.CreateQrCode(turn.QrPayload,QRCodeGenerator.ECCLevel.M);
            var png=new PngByteQRCode(code).GetGraphic(12);
            QrImage.Source=ImageSource.FromStream(() => new MemoryStream(png));
            _lastQr=turn.QrPayload;
        }
    }
    private async void IssueClicked(object sender,EventArgs e)
    {
        IssueButton.IsEnabled=false;
        try
        {
            var response=await _api.IssueTurnAsync();
            if(response.Turn is null) throw new InvalidOperationException("El servidor no pudo generar el turno.");
            await _state.SetTicketAsync(response.Turn);
            _state.SetNotice("Se generó y guardó tu pase digital.");
        }
        catch(Exception ex) {await DisplayAlert("Turno",ex.Message,"Aceptar");}
        finally {Draw();}
    }
    private async void RefreshClicked(object sender,EventArgs e)
    {
        RefreshButton.IsEnabled=false;
        try
        {
            var response=await _api.GetTurnAsync();
            if(response.Turn is null) throw new InvalidOperationException("Aún no tienes un turno en el servidor.");
            await _state.SetTicketAsync(response.Turn);
            _state.SetNotice("Turno actualizado.");
        }
        catch(Exception ex)
        {
            _state.SetNotice("No se pudo actualizar: el QR guardado permanece disponible.");
            await DisplayAlert("Sin actualización",ex.Message,"Aceptar");
        }
        finally {Draw();}
    }
}
