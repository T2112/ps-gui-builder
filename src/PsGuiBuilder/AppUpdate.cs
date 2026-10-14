// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Updates;

namespace PsGuiBuilder;

public sealed class AppUpdateController
{
    private readonly Window _owner;
    private readonly GitHubUpdateClient _client;
    private int _busy;

    public AppUpdateController(Window owner)
        : this(owner, GitHubUpdateClient.Create(CurrentVersion()))
    {
    }

    public AppUpdateController(Window owner, GitHubUpdateClient client)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public static string CurrentVersion()
    {
        var assembly = typeof(AppUpdateController).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public async Task CheckOnLaunchAsync()
    {
        try
        {
            await Task.Delay(400).ConfigureAwait(true);
            if (_owner.IsLoaded)
                await CheckAsync(manual: false).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
        }
    }

    public async Task CheckAsync(bool manual)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        try
        {
            var skipped = LoadSkipped();
            var installed = IsInstalled();
            var status = await _client.CheckAsync(CurrentVersion(), skipped, installed, CancellationToken.None).ConfigureAwait(true);
            if (!_owner.IsLoaded)
                return;

            Show(status, manual);
        }
        catch (Exception ex) when (!manual)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            MessageBox.Show(_owner, ex.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public static void ShowAbout(Window owner)
    {
        MessageBox.Show(
            owner,
            ProductName.Name + " " + UpdateVersion.PublishedText(CurrentVersion())
                + Environment.NewLine + Environment.NewLine
                + "Personal side project by Thomas Schueler. Built on personal time with personal equipment. No employer resources.",
            ProductName.Name,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Show(UpdateStatus status, bool manual)
    {
        switch (UpdateRelease.PromptKind(status, manual))
        {
            case UpdatePromptKind.Quiet:
                return;
            case UpdatePromptKind.Error:
                MessageBox.Show(_owner, status.Error ?? "Could not check for updates.", ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            case UpdatePromptKind.Current:
                var message = status.Latest is not null && !VersionsEqual(status.Latest, status.Current)
                    ? "You're on " + status.Current + ". The latest published release is " + status.Latest + "."
                    : "You're on " + status.Current + ", which is the latest release.";
                MessageBox.Show(_owner, message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            default:
                ShowPrompt(status);
                return;
        }
    }

    private void ShowPrompt(UpdateStatus status)
    {
        var choice = UpdatePrompt.Ask(_owner, status);
        if (choice == UpdateChoice.Skip && status.Latest is not null)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrWhiteSpace(appData))
                    UpdateSkipStore.Save(UpdateSkipStore.PathFor(appData), status.Latest);
            }
            catch (IOException ex)
            {
                MessageBox.Show(_owner, ex.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return;
        }

        if (choice != UpdateChoice.Install)
            return;

        if (!status.CanInstall)
        {
            OpenPage(status.PageUrl);
            return;
        }

        _ = DownloadAndRunAsync(status);
    }

    private async Task DownloadAndRunAsync(UpdateStatus status)
    {
        var progress = new UpdateProgressWindow { Owner = _owner };
        progress.Show();
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "Shellmason-update-" + Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(folder, status.AssetName ?? "Shellmason-Setup.exe");
            await _client.DownloadAsync(
                status,
                destination,
                (done, total) => progress.Report(done, total),
                CancellationToken.None).ConfigureAwait(true);
            progress.Close();
            StartInstaller(destination);
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            progress.Close();
            if (_owner.IsLoaded)
                MessageBox.Show(_owner, ex.Message, ProductName.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void StartInstaller(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = string.Join(' ', UpdateInstaller.Arguments()),
            UseShellExecute = true
        });
    }

    private static void OpenPage(string url)
    {
        if (!UpdateUrls.IsGitHub(url))
            return;

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private static string? LoadSkipped()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            return null;

        return UpdateSkipStore.Load(UpdateSkipStore.PathFor(appData));
    }

    private static bool IsInstalled() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, CommandTreeLocations.InstalledMarkerName));

    private static bool VersionsEqual(string left, string right) =>
        UpdateVersion.TryParse(left, out var parsedLeft)
        && UpdateVersion.TryParse(right, out var parsedRight)
        && parsedLeft.CompareTo(parsedRight) == 0;
}

public enum UpdateChoice
{
    Later,
    Skip,
    Install
}

public static class UpdatePrompt
{
    public static UpdateChoice Ask(Window owner, UpdateStatus status)
    {
        var choice = UpdateChoice.Later;
        var notes = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(status.Notes) ? "No release notes were published." : status.Notes,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 140,
            Margin = new Thickness(0, 8, 0, 12)
        };
        var detail = status.CanInstall
            ? "You are on " + status.Current + ". Update now downloads " + status.AssetName
                + ", checks its size and checksum, and runs the installer. Windows may ask for administrator approval."
            : "You are on " + status.Current + ". This copy was started from source, so it will not run the installer. Open the release page to download it.";
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var window = new Window
        {
            Title = "Update " + ProductName.Name,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = owner
        };
        buttons.Children.Add(Button(status.CanInstall ? "Update now" : "Open release page", () =>
        {
            choice = UpdateChoice.Install;
            window.Close();
        }));
        buttons.Children.Add(Button("Later", () => window.Close()));
        buttons.Children.Add(Button("Skip this version", () =>
        {
            choice = UpdateChoice.Skip;
            window.Close();
        }));
        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock
                {
                    Text = "Version " + status.Latest + " is available",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = detail,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                    Foreground = System.Windows.Media.Brushes.DimGray
                },
                notes,
                buttons
            }
        };
        window.ShowDialog();
        return choice;
    }

    private static Button Button(string content, Action action)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(12, 6, 12, 6),
            IsDefault = content is "Update now" or "Open release page"
        };
        button.Click += (_, _) => action();
        return button;
    }
}

public sealed class UpdateProgressWindow : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _label;

    public UpdateProgressWindow()
    {
        Title = "Downloading update";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        _label = new TextBlock { Text = "Starting the download…", TextWrapping = TextWrapping.Wrap };
        _bar = new ProgressBar { Height = 16, Margin = new Thickness(0, 12, 0, 0), IsIndeterminate = true };
        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children = { _label, _bar }
        };
    }

    public void Report(long done, long total)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Report(done, total));
            return;
        }

        if (total > 0)
        {
            _bar.IsIndeterminate = false;
            _bar.Maximum = total;
            _bar.Value = done;
            _label.Text = "Downloaded " + Format(done) + " of " + Format(total);
            return;
        }

        _bar.IsIndeterminate = true;
        _label.Text = "Downloaded " + Format(done);
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024)
            return bytes + " B";

        var value = bytes / 1024d;
        var unit = "KB";
        if (value >= 1024)
        {
            value /= 1024d;
            unit = "MB";
        }

        return value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + unit;
    }
}
