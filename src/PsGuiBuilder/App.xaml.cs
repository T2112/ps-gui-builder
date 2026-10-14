// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Windows;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.ViewModels;

namespace PsGuiBuilder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            if (ExternalWindows.TryDefer(args.Exception.Message))
            {
                args.Handled = true;
                return;
            }

            MessageBox.Show(args.Exception.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        try
        {
            var path = CommandTreeLocator.Find(e.Args);
            var tree = CommandTreeLoader.LoadFile(path);
            var window = new MainWindow
            {
                LoadedTreePath = path,
                DataContext = MainViewModel.CreateDefault(tree)
            };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
