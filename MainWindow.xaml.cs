using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GRW_DKRP_Panel;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _watcher = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _trainerTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly ObservableCollection<string> _logs = [];
    private Process? _gameProcess;
    private string? _gamePath;
    private WildlandsTrainer? _trainer;
    private bool _busy;
    private bool _teleportKeyDown;
    private bool _suppressFeatureEvents;

    private static readonly Feature[] PlayerFeaturesList =
    [
        new("god", "Invencível", "Protege contra dano. Requer a captura do jogador."),
        new("stealth", "Invisível", "Patch com validação de bytes da build."),
        new("teleport", "Teleporte", "Crie um marcador novo · NUMPAD 0 · rota em etapas."),
        new("speed", "Supervelocidade", "Amplifica deslocamento do jogador."),
        new("jump", "Superpulo", "Salto vertical acionado ao pressionar Espaço."),
        new("noclip", "NoClip experimental", "WASD · Espaço sobe · Ctrl desce; colisão não validada.")
    ];

    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) =>
        {
            if (_trainer is not null) DetachTrainer("Painel fechado; hooks e patches revertidos quando os bytes ainda correspondem.");
        };
        LogItems.ItemsSource = _logs;
        BuildFeatureCards(PlayerFeatures, PlayerFeaturesList);
        _watcher.Tick += (_, _) => DetectGame();
        _watcher.Start();
        _trainerTimer.Tick += (_, _) => TrainerTick();
        _trainerTimer.Start();
        AddLog("Painel independente iniciado. Não usa nem abre o Cheat Engine.");
        AddLog("O botão INJETAR só funciona após confirmar campanha solo/offline.");
        AddLog("O painel não certifica a versão do jogo; confira o Console LOG para validações de compatibilidade.");
        DetectGame();
    }

    private void BuildFeatureCards(Panel host, IEnumerable<Feature> features)
    {
        foreach (var feature in features)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(16), CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Background = new SolidColorBrush(Color.FromArgb(204, 23, 25, 28)), MinHeight = 92
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = feature.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
            stack.Children.Add(new TextBlock { Text = feature.Hint, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(158, 163, 170)), Margin = new Thickness(0, 5, 12, 0) });
            Grid.SetColumn(stack, 0);
            var toggle = new CheckBox { Tag = feature.Key, IsEnabled = false, IsChecked = false, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Conecte o jogo e confirme a campanha solo/offline." };
            toggle.Checked += FeatureToggle_Changed;
            toggle.Unchecked += FeatureToggle_Changed;
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(stack); grid.Children.Add(toggle); card.Child = grid; host.Children.Add(card);
        }
    }

    private async void FeatureToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressFeatureEvents || sender is not CheckBox check || check.Tag is not string key || _trainer is null) return;
        var trainer = _trainer;
        var enabled = check.IsChecked == true;
        check.IsEnabled = false;
        try
        {
            await Task.Run(() => trainer.SetEnabled(key, enabled));
            AddLog($"{key}: {(enabled ? "ativado" : "desativado")}");
        }
        catch (Exception ex)
        {
            _suppressFeatureEvents = true;
            check.IsChecked = !enabled;
            _suppressFeatureEvents = false;
            AddLog($"Não foi possível alterar {key}: {ex.Message}");
        }
        finally { check.IsEnabled = _trainer is not null; }
    }

    private void DetectGame()
    {
        try
        {
            var process = Process.GetProcessesByName("GRW").FirstOrDefault(p => !p.HasExited);
            if (process is not null)
            {
                if (_gameProcess?.Id != process.Id)
                {
                    if (_trainer is not null) DetachTrainer("O processo mudou; patches anteriores restaurados.");
                    _gameProcess = process;
                    try { _gamePath = process.MainModule?.FileName ?? _gamePath; } catch { }
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(212, 220, 228));
                    StatusText.Text = "GRW.exe detectado";
                    GamePathText.Text = _gamePath ?? $"PID {process.Id}";
                    AddLog($"Processo detectado (PID {process.Id}).");
                    _ = ReadGameMetadataAsync(_gamePath);
                }
            }
            else if (_gameProcess is not null)
            {
                if (_trainer is not null) DetachTrainer("Jogo encerrado; recursos desligados.");
                _gameProcess = null;
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(123, 129, 136));
                StatusText.Text = "Jogo não detectado";
            }
        }
        catch (Exception ex) { AddLog("Falha ao verificar GRW.exe: " + ex.Message); }
    }

    private async Task ReadGameMetadataAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var version = string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;
            var hash = await Task.Run(async () =>
            {
                await using var stream = File.OpenRead(path);
                return Convert.ToHexString(await SHA256.HashDataAsync(stream));
            });
            await Dispatcher.InvokeAsync(() =>
            {
                BuildText.Text = string.IsNullOrWhiteSpace(version) ? "Versão não informada no arquivo PE" : "Versão indicada: " + version;
                HashText.Text = "SHA-256: " + hash + "\nA Ubisoft não publica hash oficial para confirmar esta build.";
            });
        }
        catch (Exception ex) { await Dispatcher.InvokeAsync(() => AddLog("Falha ao ler metadados do jogo: " + ex.Message)); }
    }

    private void ChooseGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Selecione GRW.exe", Filter = "Ghost Recon Wildlands (GRW.exe)|GRW.exe|Executáveis (*.exe)|*.exe", CheckFileExists = true, FileName = "GRW.exe" };
        if (dialog.ShowDialog(this) != true) return;
        if (!string.Equals(Path.GetFileName(dialog.FileName), "GRW.exe", StringComparison.OrdinalIgnoreCase))
        { AddLog("Selecione o executável GRW.exe."); return; }
        _gamePath = dialog.FileName;
        GamePathText.Text = _gamePath;
        AddLog("Executável selecionado: " + _gamePath);
        _ = ReadGameMetadataAsync(_gamePath);
        if (!Process.GetProcessesByName("GRW").Any(p => !p.HasExited))
        {
            try { Process.Start(new ProcessStartInfo(_gamePath) { UseShellExecute = true }); AddLog("Jogo iniciado; aguarde o menu e clique INJETAR novamente."); }
            catch (Exception ex) { AddLog("Não foi possível iniciar o jogo: " + ex.Message); }
        }
    }

    private async void Inject_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (OfflineConfirm.IsChecked != true)
        { AddLog("Ativação cancelada: confirme que está na campanha solo/offline."); return; }
        _busy = true; InjectButton.IsEnabled = false;
        try
        {
            var process = Process.GetProcessesByName("GRW").FirstOrDefault(p => !p.HasExited);
            if (process is null)
            {
                ChooseGame_Click(sender, e);
                AddLog("Abra a campanha solo, confirme o modo offline e clique INJETAR novamente.");
                return;
            }
            if (_trainer is not null) { AddLog("Painel já conectado ao processo atual."); return; }
            _gameProcess = process;
            try { _gamePath = process.MainModule?.FileName ?? _gamePath; } catch { }
            AddLog("Validando assinaturas executáveis antes de qualquer patch…");
            var trainer = await Task.Run(() => new WildlandsTrainer(process.Id));
            trainer.Log += AddLog;
            _trainer = trainer;
            SetFeatureAvailability(true);
            AddLog("Conexão nativa pronta. Cheat Engine não foi iniciado nem é necessário.");
        }
        catch (Exception ex) { AddLog("Ativação recusada com segurança: " + ex.Message); }
        finally { _busy = false; InjectButton.IsEnabled = true; }
    }

    private void TrainerTick()
    {
        if (_trainer is null) return;
        try
        {
            _trainer.Tick();
            var down = IsKeyDown(0x11) && IsKeyDown(0x12);
            if (down)
            {
                if (IsKeyDown(0x75)) ToggleHotkey("god");
                if (IsKeyDown(0x76)) ToggleHotkey("stealth");
                if (IsKeyDown(0x77)) ToggleHotkey("noclip");
            }
            var teleportDown = IsKeyDown(0x60);
            if (teleportDown && !_teleportKeyDown && _trainerFeatureOn("teleport")) _ = RunTeleportAsync();
            _teleportKeyDown = teleportDown;
        }
        catch (Exception ex) { AddLog("Loop interrompido: " + ex.Message); }
    }

    private bool _hotkeyGod, _hotkeyStealth, _hotkeyNoclip;
    private void ToggleHotkey(string key)
    {
        var field = key switch { "god" => _hotkeyGod, "stealth" => _hotkeyStealth, _ => _hotkeyNoclip };
        if (field) return;
        var toggle = FindToggle(key);
        if (toggle is not null) toggle.IsChecked = toggle.IsChecked != true;
        if (key == "god") _hotkeyGod = true;
        else if (key == "stealth") _hotkeyStealth = true;
        else _hotkeyNoclip = true;
        _ = Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
        {
            if (key == "god") _hotkeyGod = false;
            else if (key == "stealth") _hotkeyStealth = false;
            else _hotkeyNoclip = false;
        }));
    }

    private CheckBox? FindToggle(string key) => PlayerFeatures.Children
        .OfType<Border>().Select(b => b.Child).OfType<Grid>().SelectMany(g => g.Children.OfType<CheckBox>())
        .FirstOrDefault(c => c.Tag as string == key);

    private bool _trainerFeatureOn(string key) => FindToggle(key)?.IsChecked == true;

    private async Task RunTeleportAsync()
    {
        if (_trainer is null) return;
        try { await _trainer.TeleportToMarkerAsync(); }
        catch (Exception ex) { AddLog("Teleporte cancelado: " + ex.Message); }
    }

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private void SetFeatureAvailability(bool ready)
    {
        foreach (var toggle in PlayerFeatures.Children.OfType<Border>().Select(b => b.Child).OfType<Grid>().SelectMany(g => g.Children.OfType<CheckBox>()))
        {
            toggle.IsEnabled = ready;
            if (!ready) toggle.IsChecked = false;
        }
    }

    private void DetachTrainer(string message)
    {
        var trainer = _trainer;
        _trainer = null;
        SetFeatureAvailability(false);
        try { trainer?.Dispose(); } catch (Exception ex) { AddLog("Falha ao restaurar alterações: " + ex.Message); }
        AddLog(message);
    }

    private void AddLog(string message)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => AddLog(message)); return; }
        _logs.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (_logs.Count > 600) _logs.RemoveAt(0);
        LogScroll?.ScrollToEnd();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page }) return;
        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        ModsPage.Visibility = page == "Mods" ? Visibility.Visible : Visibility.Collapsed;
        LogsPage.Visibility = page == "Logs" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;
        if (page == "Mods") ModsScroll.ScrollToTop();
    }
    private void ModsShortcut_Click(object sender, RoutedEventArgs e) => Nav_Click(new Button { Tag = "Mods" }, e);
    private void ClearLogs_Click(object sender, RoutedEventArgs e) => _logs.Clear();
    private void CopyPix_Click(object sender, RoutedEventArgs e)
    { try { Clipboard.SetText("dkpixscr@gmail.com"); AddLog("Chave Pix copiada."); } catch (Exception ex) { AddLog("Falha ao copiar chave Pix: " + ex.Message); } }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private sealed record Feature(string Key, string Name, string Hint);

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
