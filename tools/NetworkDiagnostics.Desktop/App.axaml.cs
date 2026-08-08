using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;

namespace NetworkDiagnostics.Desktop;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(new StyleInclude(new Uri("avares://NetworkDiagnosticsDesktop/"))
        {
            Source = new Uri("avares://NetworkDiagnosticsDesktop/Styles/ComponentPolish.axaml")
        });
        Styles.Add(new StyleInclude(new Uri("avares://NetworkDiagnosticsDesktop/"))
        {
            Source = new Uri("avares://NetworkDiagnosticsDesktop/Styles/DiagnosticConfiguratorPolish.axaml")
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
