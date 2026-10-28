using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DKRP.GRW.Panel.Setup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Run(new SetupWindow());
    }
}

internal sealed class SetupWindow : Window
{
    private readonly TextBlock _status;
    private static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "DK RP", "Wildlands Panel");
    private static string AppPath => Path.Combine(InstallDirectory, "DKRP-GRW-Panel.exe");

    public SetupWindow()
    {
        Title = "Instalador · DK RP Wildlands Panel";
        Width = 520;
        Height = 340;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(14, 15, 17));
        Foreground = Brushes.White;

        var root = new StackPanel { Margin = new Thickness(30) };
        root.Children.Add(new TextBlock
        {
            Text = "DK RP  /  GHOST RECON WILDLANDS",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 177, 185)),
            FontSize = 11, FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = "Instalar painel",
            FontSize = 30, FontWeight = FontWeights.Light,
            Margin = new Thickness(0, 12, 0, 6)
        });
        root.Children.Add(new TextBlock
        {
            Text = "A instalação é feita somente no seu perfil do Windows. Não requer Cheat Engine nem permissões de administrador.",
            Foreground = new SolidColorBrush(Color.FromRgb(171, 176, 183)),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
        });
        root.Children.Add(new TextBlock
        {
            Text = InstallDirectory, FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(215, 219, 224)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20)
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var install = MakeButton("INSTALAR", true);
        install.Click += (_, _) => Install();
        var remove = MakeButton("REMOVER", false);
        remove.Margin = new Thickness(10, 0, 0, 0);
        remove.Click += (_, _) => Uninstall();
        buttons.Children.Add(install);
        buttons.Children.Add(remove);
        root.Children.Add(buttons);

        _status = new TextBlock
        {
            Text = "By: FranklinDKz (DK RP)",
            Foreground = new SolidColorBrush(Color.FromRgb(139, 145, 153)),
            FontSize = 10, Margin = new Thickness(0, 22, 0, 0), TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_status);
        Content = root;
    }

    private static Button MakeButton(string label, bool primary) => new()
    {
        Content = label,
        Width = primary ? 160 : 120,
        Height = 42,
        Foreground = Brushes.White,
        Background = new SolidColorBrush(primary ? Color.FromRgb(67, 77, 90) : Color.FromRgb(34, 36, 39)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private void Install()
    {
        try
        {
            Directory.CreateDirectory(InstallDirectory);
            using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("DKRP.GRW.Panel.Payload.exe")
                ?? throw new InvalidDataException("O instalador não contém o executável do painel.");
            using (var output = File.Create(AppPath)) input.CopyTo(output);
            var shortcutStatus = "Atalhos criados na área de trabalho e no menu Iniciar.";
            try
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DK RP Wildlands Panel.lnk"));
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DK RP Wildlands Panel.lnk"));
            }
            catch (Exception ex) { shortcutStatus = "O painel foi instalado, mas não consegui criar os atalhos: " + ex.Message; }
            _status.Text = "Instalação concluída em " + InstallDirectory + ". " + shortcutStatus;
            if (MessageBox.Show(this, "Painel instalado. Deseja abri-lo agora?", "DK RP",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _status.Text = "Falha na instalação: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Não foi possível instalar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Uninstall()
    {
        try
        {
            if (File.Exists(AppPath)) File.Delete(AppPath);
            if (Directory.Exists(InstallDirectory) && !Directory.EnumerateFileSystemEntries(InstallDirectory).Any())
                Directory.Delete(InstallDirectory);
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DK RP Wildlands Panel.lnk"));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DK RP Wildlands Panel.lnk"));
            _status.Text = "Painel e atalhos removidos deste perfil do Windows.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao remover. Feche o painel antes de tentar novamente: " + ex.Message;
        }
    }

    private static void CreateShortcut(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("O Windows Script Host não está disponível para criar atalhos.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = AppPath;
        shortcut.WorkingDirectory = InstallDirectory;
        shortcut.Description = "DK RP · Ghost Recon Wildlands Panel";
        shortcut.Save();
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }

    private static void DeleteShortcut(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
