using System.Windows;
using DigItPatcher.App.ViewModels;
using DigItPatcher.App.Views;

namespace DigItPatcher.App;

internal partial class App : Application
{
    /// <summary>Reads the build's identity, builds the main window's view model and shows the window.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = new MainWindow(new MainViewModel(AppIdentityFactory.FromEntryAssembly()));
        MainWindow.Show();
    }
}
