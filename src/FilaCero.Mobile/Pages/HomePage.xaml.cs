using FilaCero.Mobile.Services;

namespace FilaCero.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppState _state;
    public HomePage(AppState state)
    {
        InitializeComponent();_state=state;
        _state.Changed += (_,_) => Draw();
        Draw();
    }
    private void Draw()
    {
        MainTicketLabel.Text=_state.Turn is null?"Tu atención empieza aquí":$"Tu turno {_state.Turn.Code}";
        HeroStatus.Text=_state.Turn is null?"Evita filas de ventanilla y consulta tus avances.":$"{_state.Turn.Status} · {_state.Turn.Clinic}";
        ConnectionLabel.Text=$"● {_state.ConnectionStatus.ToUpperInvariant()}";
        Step1.Text=_state.Patient is null?"1. Pre-registro: pendiente":"✓ 1. Pre-registro completo";
        Step2.Text=_state.Triage is null?"2. Cuestionario: pendiente":"✓ 2. Cuestionario recibido";
        Step3.Text=_state.Turn is null?"3. Pase QR: pendiente":"✓ 3. Pase digital disponible";
        NoticeLabel.Text=_state.Notice;
    }
    private void OpenRegister(object sender, EventArgs e) => (Shell.Current as MainTabsPage)?.GoTo(1);
    private void OpenTicket(object sender, EventArgs e) => (Shell.Current as MainTabsPage)?.GoTo(3);
}
