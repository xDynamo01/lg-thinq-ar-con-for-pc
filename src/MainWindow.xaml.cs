using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinQDesktop;
public partial class MainWindow : Window
{
    AirflowAnimation? mainAirAnimation;
    ThinQClient? client;
    Device? active;
    List<Device> available = new();
    JsonNode? profile, state;
    List<Capability> capabilities = new();
    bool busy, fresh, recovering, closed, recoveryBlocked;
    readonly List<Button> choices = new();
    readonly System.Windows.Threading.DispatcherTimer stateTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    bool polling, stateStale; long stateVersion; DateTimeOffset lastRead;
    event Action? StateUpdated;
    static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
    static Brush Color(string hex) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
    static string Format(decimal? value) => value?.ToString("0.#", Pt) ?? "—";
    string? Value(string resource, string field) => state?[resource]?[field]?.ToString();
    decimal? Number(string resource, string field) => decimal.TryParse(Value(resource, field), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    decimal? Target => Number("temperature", "targetTemperature") ?? ReadTemperature("targetTemperature");
    decimal? ReadTemperature(string field)
    {
        var item = (state?["temperatureInUnits"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => x["unit"]?.ToString() == "C");
        return decimal.TryParse(item?[field]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
    decimal? ControlTarget => Target is decimal value ? decimal.Round(value,0,MidpointRounding.AwayFromZero) : null;
    Capability? Find(string resource, string field) => capabilities.FirstOrDefault(c => c.Resource == resource && c.Field == field);
    Capability? Temperature => capabilities.FirstOrDefault(c => c.Resource == "temperatureInUnits" && c.Unit == "C" && c.Field == (Value("airConJobMode", "currentJobMode") switch { "COOL" => "coolTargetTemperature", "HEAT" => "heatTargetTemperature", "AUTO" => "autoTargetTemperature", _ => "targetTemperature" }))
        ?? capabilities.FirstOrDefault(c => c.Resource == "temperatureInUnits" && c.Unit == "C" && c.Field == "targetTemperature");

    public MainWindow(bool connect, bool capture)
    {
        InitializeComponent(); mainAirAnimation=new AirflowAnimation(MainAirStream,MainAirVane); SetEnabled();
        Closed += (_, _) => { closed=true; mainAirAnimation?.Dispose(); stateTimer.Stop(); compact?.Close(); client?.Dispose(); };
        if(connect && !capture) {
            stateTimer.Tick+=async(_,_)=>{if(IsVisible || compact?.IsVisible==true)await PollState();};
            stateTimer.Start();
            Activated+=async(_,_)=>{if(DateTimeOffset.UtcNow-lastRead>TimeSpan.FromSeconds(10))await PollState();};
        }
        bool initialized=false;
        Loaded += async (_, _) => {
            if(initialized)return;initialized=true;
            if (connect) await Run(async () => {
                var saved = AccountStore.Load();
                if (saved is null) { ConnectionLabel.Text = "●  Sem conexão"; StatusText.Text = "Configure sua conta no botão de engrenagem."; return; }
                await Connect(saved, AccountStore.Country);
            });
            else { ConnectionLabel.Text = "●  Sem conexão"; StatusText.Text = "Prévia da interface · nenhuma leitura carregada"; }
            if (capture) {
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(this); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var f = File.Create(Path.Combine(AppContext.BaseDirectory, "preview.png"))) encoder.Save(f);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"preview-status.txt"), fresh ? "CONNECTED_STATE_RENDERED" : "NO_LIVE_STATE");
                if(fresh) { CaptureDialog(FanDialog(),"preview-fan.png"); CaptureDialog(FlowDialog(),"preview-flow.png"); }
                CaptureDialog(SettingsDialog(),"preview-settings.png"); CaptureDialog(BuildCompact(),"preview-compact.png");
                Close();
            }
        };
    }
    void CaptureDialog(Window dialog,string filename)
    {
        dialog.Show(); dialog.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(dialog);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var f=File.Create(Path.Combine(AppContext.BaseDirectory,filename)))encoder.Save(f);
        dialog.Close();
    }
    async Task Run(Func<Task> action)
    {
        if (busy) return; stateVersion++; busy = true; SetEnabled();
        try { await action(); if(fresh)recoveryBlocked=false; }
        catch (Exception e) { if(state is not null && active is not null && CanRecover(e)) { stateStale=true; fresh=true; ConnectionLabel.Text="●  Última leitura · tentando atualizar"; StatusText.Text="A leitura demorou. O último estado continua disponível."; } else { fresh=false; ConnectionLabel.Text = "●  Leitura indisponível"; StatusText.Text = e.Message; StatusText.Foreground = Color("#F2B387"); } LogConnectionFailure(e); recoveryBlocked=!CanRecover(e); if(!recoveryBlocked) _=RecoverConnection(); }
        finally { busy = false; SetEnabled(); }
    }
    internal static bool CanRecover(Exception e) => e is not ThinQApiException { StatusCode: 401 or 403 or 429 };
    static void LogConnectionFailure(Exception e) {
        try { var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ThinQDesktop"); Directory.CreateDirectory(folder); var path=Path.Combine(folder,"connection.log"); if(File.Exists(path)&&new FileInfo(path).Length>65536)File.WriteAllText(path,""); File.AppendAllText(path,DateTimeOffset.Now.ToString("O")+" "+e.GetType().Name+(e is ThinQApiException api?" HTTP "+api.StatusCode:"")+Environment.NewLine); } catch { }
    }
    async Task RecoverConnection() {
        if(recovering || closed)return; recovering=true;
        try { foreach(int seconds in new[]{5,15,30}) {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            // During Windows login there is no active device yet. In that
            // case retry the initial connection using the protected token.
            if(closed || (active is not null && !stateStale) || recoveryBlocked)return; if(busy)continue;
            try { if(AccountStore.Load() is null)return; } catch { return; }
            ConnectionLabel.Text="●  Reconectando…";
            await Run(Refresh);
        }} finally { recovering=false; }
    }
    void SetEnabled()
    {
        bool ready = !busy && fresh && active is not null;
        TimerButton.IsEnabled = ready && TimerCommand.All.Where(t=>t.Resource=="timer").Any(t=>t.Supported(profile) || t.CanCancel(profile));

        SleepButton.IsEnabled = ready && TimerCommand.All.Where(t=>t.Resource=="sleepTimer").Any(t=>t.Supported(profile) || t.CanCancel(profile));
        FanSettingsButton.IsEnabled = FlowSettingsButton.IsEnabled = ready;
        var fan = Find("airFlow","windStep"); var level = Number("airFlow","windStep");
        FanMinusButton.IsEnabled = ready && fan is not null && level.HasValue && level - 1 >= fan.Min && !fan.Excluded.Contains(level.Value - 1);
        FanPlusButton.IsEnabled = ready && fan is not null && level.HasValue && level + 1 <= fan.Max && !fan.Excluded.Contains(level.Value + 1);
        PowerButton.IsEnabled = ready && Find("operation","airConOperationMode") is not null;
        SwingButton.IsEnabled = ready && Find("windDirection","rotateUpDown") is not null && Value("windDirection","rotateUpDown") is not null;
        LightButton.IsEnabled = ready && Find("display","light") is not null && Value("display","light") is not null;
        var t = Temperature;
        MinusButton.IsEnabled = ready && t is not null && ControlTarget is decimal v && v - 1m >= t.Min && !t.Excluded.Contains(v - 1m);
        PlusButton.IsEnabled = ready && t is not null && ControlTarget is decimal v2 && v2 + 1m <= t.Max && !t.Excluded.Contains(v2 + 1m);
        foreach (var b in choices) b.IsEnabled = ready;
        RefreshButton.IsEnabled = !busy; SettingsButton.IsEnabled = !busy; mainAirAnimation?.Update(fresh && Value("operation","airConOperationMode")=="POWER_ON", Value("airConJobMode","currentJobMode"), (int)(Number("airFlow","windStep")??3)); updateCompact?.Invoke(); StateUpdated?.Invoke();
    }
    async Task Connect(string token, string country)
    {
        StatusText.Text = "Conectando à sua conta LG…";
        client?.Dispose(); client = null; active = null; state = profile = null; fresh = false; stateStale=false; capabilities.Clear(); RenderEmpty();
        var candidate = new ThinQClient(token,country);
        try {
            available = Device.Parse(await candidate.Get("devices"));
            AccountStore.Save(token,country); client = candidate;
        } catch { candidate.Dispose(); throw; }
        if (available.Count == 0) { ConnectionLabel.Text = "●  Conta conectada"; StatusText.Text = "Nenhum ar-condicionado encontrado. Confira a conta e o país em Configurações."; return; }
        await Load(available[0]);
    }
    async Task Load(Device device)
    {
        active = null; fresh = false; stateStale=false; state = profile = null; capabilities.Clear(); RenderEmpty();
        var id = Uri.EscapeDataString(device.Id);
        var p = await client!.Get($"devices/{id}/profile");
        var s = await client.Get($"devices/{id}/state");
        if (s is not JsonObject) throw new Exception("O aparelho não retornou um estado válido. Tente atualizar.");
        active = device; profile = p; state = s; capabilities = Capabilities.Read(profile).ToList(); fresh = true; stateStale=false; lastRead=DateTimeOffset.UtcNow; Render();
    }
    async Task PollState()
    {
        if(polling || busy || recovering || recoveryBlocked || closed || client is null || active is null)return;
        polling=true;long version=stateVersion;var device=active;var api=client;
        try {
            var response=await api.Get($"devices/{Uri.EscapeDataString(device.Id)}/state");
            if(closed || busy || version!=stateVersion || active!=device)return;
            if(response is not JsonObject)throw new Exception("A LG não retornou um estado válido.");
            state=response;fresh=true;stateStale=false;lastRead=DateTimeOffset.UtcNow;Render();
        } catch(Exception e) {
            if(closed || busy || version!=stateVersion)return;
            // A transient cloud/network error must not erase the last valid
            // state or disable every control. Keep the device usable while a
            // background read retries; only auth errors are terminal.
            stateStale=true;ConnectionLabel.Text="●  Última leitura · tentando atualizar";StatusText.Text="A leitura demorou. O último estado continua disponível.";LogConnectionFailure(e);recoveryBlocked=!CanRecover(e);SetEnabled();if(!recoveryBlocked)_=RecoverConnection();
        } finally {polling=false;}
    }
    async Task Refresh()
    {
        if (client is null || active is null) {
            var saved = AccountStore.Load(); if (saved is null) throw new Exception("Configure sua conta na engrenagem.");
            await Connect(saved,AccountStore.Country); return;
        }
        var response = await client.Get($"devices/{Uri.EscapeDataString(active.Id)}/state");
        if (response is not JsonObject) throw new Exception("Leitura indisponível. Os dados exibidos são da última atualização.");
        state = response; fresh = true; stateStale=false; lastRead=DateTimeOffset.UtcNow; Render();
    }
    void RenderEmpty()
    {
        AmbientValue.Text = "—"; RemoteTarget.Text = "—°"; TargetCard.Text = "— °C"; FanCard.Text = SwingCard.Text = "—";
        DeviceName.Text = "Seu ar-condicionado"; ModeSummary.Text = "Aguardando leitura do aparelho"; HealthValue.Text = "Estado não consultado";
        PowerLabel.Text = "Sem leitura"; FanStep.Text = "Sem leitura"; FilterValue.Text = "Aguardando contadores do aparelho"; LightValue.Text = "Display · —";
        choices.Clear(); ModesPanel.Children.Clear(); UpdatedText.Text = "";
    }
    void Render()
    {
        DeviceName.Text = active!.Name;
        AmbientValue.Text = Format(Number("temperature","currentTemperature") ?? ReadTemperature("currentTemperature"));
        TargetCard.Text = Format(Target) + " °C"; RemoteTarget.Text = Format(ControlTarget) + "°"; RemoteTarget.ToolTip = "Leitura recebida: " + Format(Target) + " °C. Controle em graus inteiros.";
        bool on = Value("operation","airConOperationMode") == "POWER_ON";
        PowerLabel.Text = on ? "Ligado · pronto para ajustar" : "Desligado";
        PowerButton.Background = Color(on ? "#D76522" : "#6F7D88");
        PowerButton.Foreground = Brushes.White;
        PowerButton.BorderBrush = Color(on ? "#F2A064" : "#83919D");
        ModeSummary.Text = (on ? "Ligado" : "Desligado") + "  ·  " + Translate(Value("airConJobMode","currentJobMode"));
        var health = Value("runState","currentState"); HealthValue.Text = health == "NORMAL" ? "●  Funcionamento normal" : health == "ERROR" ? "●  Aparelho informa erro" : "Estado: " + (health ?? "indisponível");
        HealthValue.Foreground = Color(health == "ERROR" ? "#FFC09A" : "#B6F4DE");
        FanCard.Text = Translate(Value("airFlow","windStrength")); FanStep.Text = "Nível " + (Value("airFlow","windStep") ?? "—") + " de ventilação";
        bool swing = Value("windDirection","rotateUpDown") == "true";
        SwingCard.Text = Value("windDirection","rotateUpDown") is null ? "—" : swing ? "Ativa" : "Parada";
        Accent(SwingButton,swing); Accent(LightButton,Value("display","light") == "ON");
        LightValue.Text = "Display · " + (Value("display","light") == "ON" ? "aceso" : Value("display","light") == "OFF" ? "apagado" : "—");
        FilterValue.Text = $"Contador de uso: {Value("filterInfo","usedTime") ?? "—"}  /  referência: {Value("filterInfo","filterLifetime") ?? "—"}";
        FilterValue.ToolTip = "Contadores retornados pela LG. A unidade e a condição física do filtro não foram confirmadas.";
        var t = Temperature; RangeLabel.Text = t is null ? "Ajuste indisponível neste modo" : $"{Format(t.Min)}–{Format(t.Max)} °C  ·  botões de 1 °C";
        choices.Clear(); ModesPanel.Children.Clear();
        ChoiceButtons(ModesPanel,Find("airConJobMode","currentJobMode"),Value("airConJobMode","currentJobMode"));
        FanSettingsButton.Content = Value("airFlow","windStrength") == "AUTO" ? "Auto · gráfico ↗" : "Nível " + (Value("airFlow","windStep") ?? "—") + " ↗";
        ConnectionLabel.Text = "●  Conectado à LG"; UpdatedText.Text = "Última leitura · " + DateTime.Now.ToString("HH:mm:ss");
        StatusText.Foreground = Color("#9AAEC8"); StatusText.Text = "Atualização automática a cada 30 segundos enquanto o painel está aberto.";
        SetEnabled();
    }
    static void Accent(Button button, bool selected) { button.Background = Color(selected ? "#254756" : "#23324B"); button.BorderBrush = Color(selected ? "#58A6B5" : "#344562"); }
    void ChoiceButtons(Panel panel, Capability? cap, string? current)
    {
        if (cap is null) { panel.Children.Add(new TextBlock { Text = "Não disponível", Foreground = Color("#7488A6") }); return; }
        foreach (var option in cap.Options) {
            var b = new Button { Content = Translate(option), Margin = new Thickness(0,0,5,5), Padding = new Thickness(9,11,9,11), FontSize = 12, MinWidth = 58 };
            Accent(b,option == current); choices.Add(b); panel.Children.Add(b);
            b.Click += async (_,_) => await Run(() => Send(cap,option));
        }
    }
    static string Translate(string? text) => text switch { "COOL" => "Resfriar", "FAN" => "Ventilar", "AIR_DRY" => "Secar", "HEAT" => "Aquecer", "LOW" => "Baixa", "MID" => "Média", "HIGH" => "Alta", "AUTO" => "Auto", "NATURE" => "Natural", _ => text ?? "—" };
    async Task Send(Capability cap, string value)
    {
        if (client is null || active is null || !fresh) throw new Exception("Atualize a conexão antes de enviar um comando.");
        StatusText.Foreground = Color("#FFCA85"); StatusText.Text = "Enviando comando…";
        var result = await ControlFlow.SendAndConfirm(client,active.Id,cap,value);
        state = result.State; fresh = result.State is not null;
        if (fresh) Render();
        StatusText.Text = result.Confirmed ? "✓  Alteração confirmada pelo aparelho." : "Comando aceito pela LG; confirmação ainda pendente. Clique em Atualizar.";
        StatusText.Foreground = Color(result.Confirmed ? "#8CE3BA" : "#FFCA85");
    }
    async void FanMinusClick(object s,RoutedEventArgs e) => await Run(() => Send(Find("airFlow","windStep")!, (Number("airFlow","windStep")!.Value - 1).ToString(CultureInfo.InvariantCulture)));
    async void FanPlusClick(object s,RoutedEventArgs e) => await Run(() => Send(Find("airFlow","windStep")!, (Number("airFlow","windStep")!.Value + 1).ToString(CultureInfo.InvariantCulture)));
    async void RefreshClick(object s,RoutedEventArgs e) => await Run(Refresh);
    async void PowerClick(object s,RoutedEventArgs e) => await Run(() => Send(Find("operation","airConOperationMode")!,Value("operation","airConOperationMode") == "POWER_ON" ? "POWER_OFF" : "POWER_ON"));
    async void SwingClick(object s,RoutedEventArgs e) => await Run(() => Send(Find("windDirection","rotateUpDown")!,Value("windDirection","rotateUpDown") == "true" ? "false" : "true"));
    async void LightClick(object s,RoutedEventArgs e) => await Run(() => Send(Find("display","light")!,Value("display","light") == "ON" ? "OFF" : "ON"));
    async void MinusClick(object s,RoutedEventArgs e) => await Run(() => Send(Temperature!, (ControlTarget!.Value - 1m).ToString(CultureInfo.InvariantCulture)));
    async void PlusClick(object s,RoutedEventArgs e) => await Run(() => Send(Temperature!, (ControlTarget!.Value + 1m).ToString(CultureInfo.InvariantCulture)));
    Window Dialog(string title, UIElement content, double width = 540, double height = 420)
    {
        var dialog = new Window { Title=title, Owner=this, Content=content, Width=width, Height=height, Background=Color("#172237"), Foreground=Brushes.White, FontFamily=FontFamily, FontSize=14, WindowStartupLocation=WindowStartupLocation.CenterOwner };
        foreach (var type in new[]{typeof(ComboBox),typeof(ComboBoxItem),typeof(PasswordBox),typeof(TextBox),typeof(Button)}) {
            var style=new Style(type);
            style.Setters.Add(new Setter(Control.ForegroundProperty,Color("#172237")));
            style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.White));
            style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8)));
            dialog.Resources[type]=style;
        }
        return dialog;
    }
    void SettingsClick(object s,RoutedEventArgs e) => SettingsDialog().ShowDialog();
    Window SettingsDialog()
    {
        var panel = new StackPanel { Margin=new Thickness(24) };
        panel.Children.Add(new TextBlock { Text="Conexão LG", FontSize=24, Foreground=Brushes.White });
        panel.Children.Add(new TextBlock { Text="Sua credencial fica protegida pelo Windows.", Foreground=Color("#A9BCD5"), Margin=new Thickness(0,8,0,18) });
        panel.Children.Add(Description(fresh ? "Conectado · " + active?.Name : "Reconecte usando a credencial salva."));
        var selector = new ComboBox { ItemsSource=available, DisplayMemberPath="Name", SelectedItem=active, Margin=new Thickness(0,0,0,12) }; panel.Children.Add(selector);
        panel.Children.Add(Description("Novo token LG (opcional; vazio mantém a conta salva)"));
        var box = new PasswordBox { Padding=new Thickness(8), ToolTip="Novo token pessoal LG (opcional)" }; panel.Children.Add(box);
        var region = new ComboBox { ItemsSource=new[]{"BR","US","PT","KR"}, SelectedItem=AccountStore.Country, Margin=new Thickness(0,10,0,12) }; panel.Children.Add(region);
        var row = new WrapPanel(); panel.Children.Add(row); var dialog=Dialog("Configurações",panel,580,500);
        var connect=new Button { Content="Conectar / trocar", Margin=new Thickness(0,0,8,8) }; row.Children.Add(connect);
        connect.Click += async (_,_) => { var secret=box.Password.Trim(); var code=region.SelectedItem?.ToString() ?? "BR"; var chosen=selector.SelectedItem as Device; dialog.Close(); await Run(async () => { if (secret.Length>0) await Connect(secret,code); else if (chosen is not null && client is not null) await Load(chosen); else { var saved=AccountStore.Load(); if(saved is null) throw new Exception("Informe um token LG nas configurações."); await Connect(saved,code); } }); };
        var forget=new Button { Content="Desconectar", Margin=new Thickness(0,0,8,8) }; row.Children.Add(forget);
        forget.Click += (_,_) => { AccountStore.Forget(); client?.Dispose(); client=null; active=null; available.Clear(); capabilities.Clear(); state=profile=null; fresh=false; RenderEmpty(); SetEnabled(); ConnectionLabel.Text="●  Desconectado"; StatusText.Text="Credencial salva removida."; dialog.Close(); };
        var site=new Button { Content="Gerenciar token LG" }; row.Children.Add(site); site.Click += (_,_) => Process.Start(new ProcessStartInfo("https://connect-pat.lgthinq.com/tokens"){UseShellExecute=true});
        return dialog;
    }
    void DiagnosticsClick(object s,RoutedEventArgs e)
    {
        if (state is null) { StatusText.Text="Carregue um aparelho primeiro."; return; }
        var json=new JsonObject { ["device"]=active?.Info.DeepClone(), ["profile"]=profile?.DeepClone(), ["state"]=state.DeepClone() }.ToJsonString(new(){WriteIndented=true});
        var dock=new DockPanel { Margin=new Thickness(18) }; var save=new Button { Content="Exportar diagnóstico", Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(save,Dock.Top); dock.Children.Add(save);
        dock.Children.Add(new TextBox { Text=json, IsReadOnly=true, FontFamily=new FontFamily("Consolas"), Background=Color("#101A2B"), Foreground=Color("#C9D8ED"), BorderThickness=new Thickness(0), AcceptsReturn=true, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto });
        save.Click += (_,_) => { var picker=new Microsoft.Win32.SaveFileDialog { FileName="diagnostico-ar-condicionado.json", Filter="JSON|*.json" }; if(picker.ShowDialog()==true) File.WriteAllText(picker.FileName,json); };
        Dialog("Dados completos do aparelho",dock,850,650).ShowDialog();
    }
}












