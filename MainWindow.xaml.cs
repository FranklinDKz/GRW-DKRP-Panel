using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GRW_DKRP_Panel;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _watcher = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _bridgeLogTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly ObservableCollection<string> _logs = [];
    private readonly HashSet<string> _seenBridgeLines = [];
    private Process? _gameProcess;
    private string? _gamePath;
    private string? _ceLogPath;
    private string? _runtimeTablePath;
    private CEBridge? _bridge;
    private bool _busy;
    private bool _bridgeReady;

    private static readonly Feature[] PlayerFeaturesList =
    [
        new(930001, "Invencível", "Ignora dano e morte."),
        new(930002, "Invisível", "Reduz a detecção visual dos inimigos."),
        new(910001, "Teleporte", "Novo marcador · NUMPAD 0 · etapas com carregamento."),
        new(930004, "Supervelocidade", "Multiplicador ajustável na tabela CE."),
        new(930005, "Superpulo", "Elevação adicional ajustável na tabela CE."),
        new(930006, "NoClip experimental", "WASD · Espaço sobe · Ctrl desce. Sem colisão validada.")
    ];

    private static readonly Feature[] UtilityFeaturesList =
    [
        new(930007, "Munição e itens", "Ativa o script correspondente na tabela."),
        new(930009, "Sem recuo", "Ativa o script correspondente na tabela."),
        new(930010, "Precisão máxima", "Ativa o script correspondente na tabela."),
        new(930011, "Bateria do drone", "Autonomia ampliada."),
        new(930012, "Alcance do drone", "Ativa o script correspondente na tabela."),
        new(930013, "Tiro sincronizado", "Remove espera entre disparos da equipe.")
    ];

    public MainWindow()
    {
        InitializeComponent();
        LogItems.ItemsSource = _logs;
        BuildFeatureCards(PlayerFeatures, PlayerFeaturesList);
        BuildFeatureCards(UtilityFeatures, UtilityFeaturesList);
        _watcher.Tick += (_, _) => DetectGame();
        _watcher.Start();
        _bridgeLogTimer.Tick += (_, _) => ReadBridgeLog();
        _bridgeLogTimer.Start();
        AddLog("Painel iniciado. Uso restrito à campanha solo/offline.");
        AddLog("A atualização oficial mais recente consultada é Last Rites (agosto de 2026). Build local não confirmada.");
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
            var toggle = new CheckBox { Tag = feature.Id, IsEnabled = false, IsChecked = false, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Conecte e valide a tabela antes de alterar este recurso." };
            toggle.Checked += FeatureToggle_Changed;
            toggle.Unchecked += FeatureToggle_Changed;
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(stack);
            grid.Children.Add(toggle);
            card.Child = grid;
            host.Children.Add(card);
        }
    }

    private void FeatureToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not int id || !_bridgeReady || _bridge is null) return;
        _ = ToggleFeatureAsync(checkBox, id, checkBox.IsChecked == true);
    }

    private async Task ToggleFeatureAsync(CheckBox control, int id, bool enabled)
    {
        control.IsEnabled = false;
        try
        {
            var ok = await Task.Run(() => _bridge?.SetRecordActive(id, enabled) == true);
            AddLog(ok ? $"Recurso {id}: {(enabled ? "ativado" : "desativado")}." : $"Não foi possível {(enabled ? "ativar" : "desativar")} o recurso {id}; confira o log do Cheat Engine.");
            if (!ok) control.IsChecked = !enabled;
        }
        catch (Exception ex)
        {
            control.IsChecked = !enabled;
            AddLog($"Falha no recurso {id}: {ex.Message}");
        }
        finally { control.IsEnabled = _bridgeReady; }
    }

    private void DetectGame()
    {
        try
        {
            var processes = Process.GetProcessesByName("GRW");
            var current = processes.FirstOrDefault(p => !p.HasExited);
            if (current is not null)
            {
                if (_gameProcess?.Id != current.Id)
                {
                    _gameProcess = current;
                    try { _gamePath = current.MainModule?.FileName ?? _gamePath; } catch { }
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(212, 220, 228));
                    StatusText.Text = "GRW.exe detectado";
                    GamePathText.Text = _gamePath ?? $"PID {current.Id}";
                    AddLog($"Processo detectado (PID {current.Id}).");
                    _ = ReadGameMetadataAsync(_gamePath);
                }
            }
            else if (_gameProcess is not null)
            {
                _gameProcess = null;
                _bridgeReady = false;
                SetFeatureAvailability(false);
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(123, 129, 136));
                StatusText.Text = "Jogo não detectado";
                AddLog("O processo do jogo foi encerrado; controles desabilitados.");
            }
        }
        catch (Exception ex) { AddLog("Falha na leitura do processo: " + ex.Message); }
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
                var bytes = await SHA256.HashDataAsync(stream);
                return Convert.ToHexString(bytes);
            });
            await Dispatcher.InvokeAsync(() =>
            {
                BuildText.Text = string.IsNullOrWhiteSpace(version) ? "Versão PE sem metadados publicados" : "Versão informada pelo arquivo: " + version;
                HashText.Text = "SHA-256: " + hash + "\nUbisoft não publica hash oficial para comparar; compatibilidade não confirmada.";
            });
        }
        catch (Exception ex) { await Dispatcher.InvokeAsync(() => AddLog("Não foi possível ler versão/hash: " + ex.Message)); }
    }

    private void ChooseGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Selecione o executável oficial do jogo", Filter = "Ghost Recon Wildlands (GRW.exe)|GRW.exe|Executáveis (*.exe)|*.exe", CheckFileExists = true, FileName = "GRW.exe" };
        if (dialog.ShowDialog(this) != true) return;
        if (!string.Equals(Path.GetFileName(dialog.FileName), "GRW.exe", StringComparison.OrdinalIgnoreCase))
        {
            AddLog("Arquivo recusado: selecione o executável GRW.exe.");
            return;
        }
        _gamePath = dialog.FileName;
        GamePathText.Text = _gamePath;
        AddLog("Executável selecionado: " + _gamePath);
        _ = ReadGameMetadataAsync(_gamePath);
        var running = Process.GetProcessesByName("GRW").FirstOrDefault(p => !p.HasExited);
        if (running is null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_gamePath) { UseShellExecute = true });
                AddLog("Jogo iniciado; aguardando GRW.exe aparecer para conectar.");
            }
            catch (Exception ex) { AddLog("Não foi possível iniciar o jogo: " + ex.Message); }
        }
    }

    private async void Inject_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InjectButton.IsEnabled = false;
        try
        {
            var process = Process.GetProcessesByName("GRW").FirstOrDefault(p => !p.HasExited);
            if (process is null)
            {
                ChooseGame_Click(sender, e);
                AddLog("Abra o jogo selecionado e pressione INJETAR novamente quando o menu principal estiver carregado.");
                return;
            }
            _gameProcess = process;
            try { _gamePath = process.MainModule?.FileName ?? _gamePath; } catch { }
            if (string.IsNullOrWhiteSpace(_gamePath) || !File.Exists(_gamePath))
            {
                AddLog("Caminho do GRW.exe indisponível. Use Escolher GRW.exe.");
                return;
            }

            var cePath = FindCheatEngine();
            if (cePath is null)
            {
                AddLog("Cheat Engine x64 não encontrado. Instale uma versão oficial 64-bit e tente novamente.");
                return;
            }
            var tableSource = Path.Combine(AppContext.BaseDirectory, "Cheats", "GRW_Atualizado_PTBR.CT");
            if (!File.Exists(tableSource))
            {
                AddLog("Tabela não encontrada ao lado do painel: " + tableSource);
                return;
            }

            _bridgeReady = false;
            SetFeatureAvailability(false);
            var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DKRP", "Wildlands");
            Directory.CreateDirectory(work);
            _ceLogPath = Path.Combine(work, "cheat-engine.log");
            File.WriteAllText(_ceLogPath, "", Encoding.UTF8);
            _runtimeTablePath = Path.Combine(work, "GRW_DKRP_runtime.CT");
            PrepareRuntimeTable(tableSource, _runtimeTablePath, process.Id, _ceLogPath);
            AddLog("Abrindo Cheat Engine com tabela temporária e assinatura protegida.");
            var ceProcess = Process.Start(new ProcessStartInfo(cePath) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal, ArgumentList = { _runtimeTablePath } });
            if (ceProcess is null) { AddLog("O Windows não iniciou o Cheat Engine."); return; }
            AddLog("Se o Cheat Engine pedir confirmação para executar o Lua da tabela, aceite para conectar o painel.");
            AddLog("A verificação da assinatura do jogo é executada pela tabela; em divergência, os controles permanecem indisponíveis.");
            var clientPath = Path.Combine(Path.GetDirectoryName(cePath)!, "luaclient-x86_64.dll");
            if (!File.Exists(clientPath)) { AddLog("LuaClient x64 não encontrado ao lado do Cheat Engine."); return; }
            _bridge = new CEBridge(clientPath);
            var pipe = "DKRP_GRW_" + process.Id;
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline && !_bridgeReady)
            {
                if (ceProcess.HasExited) { AddLog("Cheat Engine foi encerrado antes de conectar."); return; }
                try
                {
                    if (_bridge.Connect(pipe))
                    {
                        var check = _bridge.Execute("return (GRWPanelReady and 1 or 0)");
                        if (check == 1)
                        {
                            var activate = _bridge.Execute("local r=getAddressList().getMemoryRecordByID(23931); if not r then return 0 end; local ok=pcall(function() r.Active=true end); return (ok and r.Active and 1 or 0)");
                            if (activate == 1)
                            {
                                _bridgeReady = true;
                                SetFeatureAvailability(true);
                                AddLog("Conexão do painel validada. Controles habilitados.");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex) { AddLog("Aguardando o Cheat Engine: " + ex.Message); }
                await Task.Delay(700);
            }
            if (!_bridgeReady) AddLog("Tempo esgotado aguardando o Lua do Cheat Engine. Confirme o script na janela do CE e pressione INJETAR novamente.");
        }
        catch (Exception ex) { AddLog("Falha controlada na injeção: " + ex.Message); }
        finally { _busy = false; InjectButton.IsEnabled = true; }
    }

    private static string? FindCheatEngine()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cheat Engine", "cheatengine-x86_64-SSE4-AVX2.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cheat Engine", "cheatengine-x86_64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Cheat Engine", "cheatengine-x86_64.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static void PrepareRuntimeTable(string source, string destination, int pid, string logPath)
    {
        var document = XDocument.Load(source, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidDataException("A tabela Cheat Engine está sem raiz XML.");
        var escapedLog = logPath.Replace("\\", "\\\\").Replace("'", "\\'");
        var pipe = "DKRP_GRW_" + pid;
        var lua = $$"""
            local function GRWPanelLog(message)
              local f=io.open('{{escapedLog}}','a')
              if f then f:write(os.date('%H:%M:%S')..' | '..tostring(message)..'\n'); f:close() end
            end
            GRWPanelLog('Lua da tabela aceito; preparando conexao')
            local targetPid={{pid}}
            if getOpenedProcessID()~=targetPid then openProcess(targetPid) end
            openLuaServer('{{pipe}}')
            local readyTimer=createTimer(nil,false)
            readyTimer.Interval=400
            readyTimer.OnTimer=function(timer)
              if getOpenedProcessID()~=targetPid then GRWPanelLog('ERRO: processo conectado mudou'); timer.destroy(); return end
              local record=getAddressList().getMemoryRecordByID(23931)
              if not record then GRWPanelLog('ERRO: entrada INICIALIZAR ausente'); timer.destroy(); return end
              if not record.Active then
                local ok,err=pcall(function() record.Active=true end)
                if not ok then GRWPanelLog('ERRO_INIT: '..tostring(err)); return end
              end
              if record.Active then
                GRWPanelReady=true
                GRWPanelLog('Inicializacao ativa; aguardando validacao do painel')
                timer.destroy()
              end
            end
            readyTimer.Enabled=true
            """;
        root.Element("LuaScript")?.Remove();
        root.Add(new XElement("LuaScript", lua));
        foreach (var script in root.Descendants("AssemblerScript"))
        {
            if (script.Value.Contains("showMessage(", StringComparison.Ordinal))
                script.Value = script.Value.Replace("showMessage(", "GRWPanelLog(", StringComparison.Ordinal);
        }
        document.Save(destination, SaveOptions.DisableFormatting);
    }

    private void SetFeatureAvailability(bool ready)
    {
        foreach (var host in new[] { PlayerFeatures, UtilityFeatures })
            foreach (var child in host.Children)
                if (child is Border { Child: Grid grid } && grid.Children.OfType<CheckBox>().FirstOrDefault() is { } check)
                    check.IsEnabled = ready;
    }

    private void ReadBridgeLog()
    {
        if (string.IsNullOrWhiteSpace(_ceLogPath) || !File.Exists(_ceLogPath)) return;
        try
        {
            foreach (var line in File.ReadLines(_ceLogPath, Encoding.UTF8))
                if (_seenBridgeLines.Add(line)) AddLog("CE · " + line);
        }
        catch { }
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
    private void OpenTable_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Cheats", "GRW_Atualizado_PTBR.CT");
        if (!File.Exists(path)) { AddLog("Tabela não encontrada: " + path); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); AddLog("Tabela aberta no aplicativo associado ao .CT."); }
        catch (Exception ex) { AddLog("Não foi possível abrir a tabela: " + ex.Message); }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e) => _logs.Clear();
    private void CopyPix_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText("dkpixscr@gmail.com"); AddLog("Chave Pix copiada para a área de transferência."); }
        catch (Exception ex) { AddLog("Falha ao copiar chave Pix: " + ex.Message); }
    }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) { _bridge?.Dispose(); Close(); }

    private sealed record Feature(int Id, string Name, string Hint);

    private sealed class CEBridge : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate int InitializeDelegate([MarshalAs(UnmanagedType.LPStr)] string name);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)] private delegate nuint ExecuteDelegate([MarshalAs(UnmanagedType.LPStr)] string code, nuint parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool WaitNamedPipe(string pipeName, uint timeout);
        private readonly IntPtr _library;
        private readonly InitializeDelegate _initialize;
        private readonly ExecuteDelegate _execute;
        private string? _connectedPipe;
        public CEBridge(string libraryPath)
        {
            _library = NativeLibrary.Load(libraryPath);
            _initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(NativeLibrary.GetExport(_library, "CELUA_Initialize"));
            _execute = Marshal.GetDelegateForFunctionPointer<ExecuteDelegate>(NativeLibrary.GetExport(_library, "CELUA_ExecuteFunction"));
        }
        public bool Connect(string pipe)
        {
            if (_connectedPipe == pipe) return true;
            if (!WaitNamedPipe(@"\\.\pipe\" + pipe, 30)) return false;
            if (_initialize(pipe) == 0) return false;
            _connectedPipe = pipe;
            return true;
        }
        public nuint Execute(string code)
        {
            if (_connectedPipe is null) throw new InvalidOperationException("Ponte Lua não conectada.");
            return _execute(code, 0);
        }
        public bool SetRecordActive(int id, bool active)
        {
            var desired = active ? "true" : "false";
            var code = $"local r=getAddressList().getMemoryRecordByID({id}); if not r then return 0 end; local ok,err=pcall(function() r.Active={desired} end); if not ok then GRWPanelLog('ERRO_RECURSO_{id}: '..tostring(err)); return 0 end; return (r.Active=={desired} and 1 or 0)";
            return Execute(code) == 1;
        }
        public void Dispose() { if (_library != IntPtr.Zero) NativeLibrary.Free(_library); }
    }
}


