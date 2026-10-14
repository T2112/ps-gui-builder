// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.ViewModels;

namespace PsGuiBuilder;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private AppUpdateController? _updates;
    private bool _launchCheckStarted;

    public string LoadedTreePath { get; set; } = "";

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachOutput();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_launchCheckStarted)
            return;

        _launchCheckStarted = true;
        _updates ??= new AppUpdateController(this);
        await _updates.CheckOnLaunchAsync();
    }

    private async void CheckForUpdates(object sender, RoutedEventArgs e)
    {
        _updates ??= new AppUpdateController(this);
        await _updates.CheckAsync(manual: true);
    }

    private void ShowAbout(object sender, RoutedEventArgs e) =>
        AppUpdateController.ShowAbout(this);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.IsRunning)
            viewModel.Cancel();

        base.OnClosing(e);
    }

    private void AttachOutput()
    {
        if (_viewModel is not null)
            _viewModel.OutputAppended -= AppendOutput;

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.OutputAppended += AppendOutput;
    }

    private void AppendOutput(object? sender, OutputChunkEventArgs args)
    {
        var paragraph = OutputBox.Document.Blocks.LastBlock as Paragraph;
        if (paragraph is null)
        {
            paragraph = new Paragraph { Margin = new Thickness(0) };
            OutputBox.Document.Blocks.Add(paragraph);
        }

        paragraph.Inlines.Add(new Run(args.Text)
        {
            Foreground = args.IsError
                ? new SolidColorBrush(Color.FromRgb(0xFB, 0x71, 0x85))
                : new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF4))
        });
        OutputBox.ScrollToEnd();
    }

    private void LoadNewDefaultCommands(object sender, RoutedEventArgs e)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
            {
                ShowTreeMessage("The personal command tree folder is not available.");
                return;
            }

            var userFile = CommandTreeLocations.UserFile(appData);
            var defaultFile = Path.Combine(AppContext.BaseDirectory, CommandTreeLocations.FileName);
            if (!File.Exists(defaultFile))
            {
                ShowTreeMessage("The default command tree was not found next to this app.");
                return;
            }

            if (!File.Exists(userFile))
            {
                ShowTreeMessage("There is no personal command tree in %APPDATA%\\Shellmason yet. This window is already using the default commands. After that file exists, Load new default commands adds missing entries there and leaves your edits alone.");
                return;
            }

            var result = CommandTreeMerger.MergeFiles(userFile, defaultFile);
            if (result.CommandsAdded == 0)
            {
                ShowTreeMessage("No new default commands were missing. Your edits were left as they are.");
                return;
            }

            var preview = result.AddedNames.Take(12).ToArray();
            var list = string.Join(Environment.NewLine, preview);
            var extra = result.CommandsAdded - preview.Length;
            if (extra > 0)
                list += Environment.NewLine + "and " + extra + " more";

            ShowTreeMessage(
                "Added " + result.CommandsAdded + " commands from the default tree. Your existing entries were not changed."
                + Environment.NewLine + Environment.NewLine + list);

            if (string.Equals(Path.GetFullPath(LoadedTreePath), Path.GetFullPath(userFile), StringComparison.OrdinalIgnoreCase))
                ReloadTree(userFile);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadTree(string path)
    {
        var tree = CommandTreeLoader.LoadFile(path);
        OutputBox.Document.Blocks.Clear();
        DataContext = MainViewModel.CreateDefault(tree);
    }

    private void ShowTreeMessage(string message) =>
        MessageBox.Show(this, message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Information);
}
