// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Collections.ObjectModel;
using System.Windows;
using PsGuiBuilder.Core.Building;
using PsGuiBuilder;
using PsGuiBuilder.Core.Cascade;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Discovery;
using PsGuiBuilder.Core.Execution;
using PsGuiBuilder.Execution;

namespace PsGuiBuilder.ViewModels;

public sealed class OutputChunkEventArgs : EventArgs
{
    public OutputChunkEventArgs(string text, bool isError)
    {
        Text = text;
        IsError = isError;
    }

    public string Text { get; }

    public bool IsError { get; }
}

public sealed class MainViewModel : ObservableObject
{
    private readonly CascadeSession _session;
    private readonly ICommandExecutor _executor;
    private readonly CommandMetadataProbe _probe;
    private readonly ICredentialPrompt _signIn;
    private readonly IModuleInstallGate? _modules;
    private readonly IUserConfirmation? _confirmation;
    private readonly OutputAdapter _output;
    private readonly List<string> _redact = new();
    private readonly List<FieldSegmentViewModel> _fields = new();
    private CommandDefinition? _formCommand;
    private CommandBuildResult? _lastBuild;
    private CancellationTokenSource? _runCts;
    private string _previewText = "";
    private string _validationText = "";
    private string _statusText = "";
    private string _commandSummary = "";
    private string _elevationHint = "";
    private string _executionText = "";
    private string _pipelineText = "";
    private string? _snapshotJson;
    private string? _selectedParameterSet;
    private bool _updatingSets;
    private bool _pipelineReady = true;
    private bool _runElevated;
    private bool _inProcess;
    private bool _isRunning;
    private bool _hasCompletedRun;
    private bool _showBrowser;
    private CommandDefinition? _genericCommand;
    private string _cmdletQuery = "";

    public MainViewModel(
        CommandTree tree,
        ICommandExecutor executor,
        CommandMetadataProbe probe,
        ICredentialPrompt signIn,
        IModuleInstallGate? modules = null,
        IUserConfirmation? confirmation = null,
        IDiscoverySource? discovery = null)
    {
        _session = new CascadeSession(tree);
        _executor = executor;
        _probe = probe;
        _signIn = signIn;
        _modules = modules;
        _confirmation = confirmation;
        _output = new OutputAdapter(this);
        Browser = new CommandBrowserViewModel(discovery ?? new DiscoverySession(), ApplyDiscoveredAsync);
        Levels.Add(CreateLevel(0));
        RunCommand = new AsyncRelayCommand(RunAsync, () => CanRun);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        StartOverCommand = new RelayCommand(StartOver, () => !IsRunning);
        LookupCommand = new AsyncRelayCommand(LookupAsync, () => IsIdle && !string.IsNullOrWhiteSpace(CmdletQuery));
        StatusText = "Choose a favorite, or browse installed commands.";
    }

    public static MainViewModel CreateDefault(CommandTree tree) =>
        new(tree, new CommandExecutionService(), new CommandMetadataProbe(), new SignInDialog(), new ModuleInstallCoordinator(), new MessageBoxConfirmation());

    public ObservableCollection<CascadeLevelViewModel> Levels { get; } = new();

    public ObservableCollection<SegmentViewModel> Segments { get; } = new();

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand StartOverCommand { get; }

    public AsyncRelayCommand LookupCommand { get; }

    public CommandBrowserViewModel Browser { get; }

    public ObservableCollection<string> ParameterSetNames { get; } = new();

    public bool ShowBrowser
    {
        get => _showBrowser;
        set
        {
            if (!SetProperty(ref _showBrowser, value))
                return;

            OnPropertyChanged(nameof(ShowFavorites));
            if (value)
                _ = Browser.OpenAsync();
        }
    }

    public bool ShowFavorites
    {
        get => !_showBrowser;
        set
        {
            if (value)
                ShowBrowser = false;
        }
    }

    public bool HasParameterSets => ParameterSetNames.Count > 0;

    public string? SelectedParameterSet
    {
        get => _selectedParameterSet;
        set
        {
            if (!SetProperty(ref _selectedParameterSet, value))
                return;

            if (_updatingSets || _snapshotJson is null || string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                var shell = _formCommand?.Shell ?? PowerShellEdition.PowerShell7;
                var view = CommandMetadataMapper.Read(_snapshotJson, value, shell);
                ShowGeneric(view.Command);
            }
            catch (ArgumentException ex)
            {
                ValidationText = ex.Message;
            }
        }
    }

    public string PipelineText
    {
        get => _pipelineText;
        set
        {
            if (SetProperty(ref _pipelineText, value ?? ""))
                UpdatePreview();
        }
    }

    public string CmdletQuery
    {
        get => _cmdletQuery;
        set
        {
            if (!SetProperty(ref _cmdletQuery, value ?? ""))
                return;

            LookupCommand.RaiseCanExecuteChanged();
        }
    }

    public event EventHandler<OutputChunkEventArgs>? OutputAppended;

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string ValidationText
    {
        get => _validationText;
        private set => SetProperty(ref _validationText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CommandSummary
    {
        get => _commandSummary;
        private set => SetProperty(ref _commandSummary, value);
    }

    public string ElevationHint
    {
        get => _elevationHint;
        private set => SetProperty(ref _elevationHint, value);
    }

    public bool HasCommand => _formCommand is not null;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value))
                return;

            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(CanRunInProcess));
            OnPropertyChanged(nameof(CanRun));
            RefreshCommands();
        }
    }

    public bool IsIdle => !IsRunning;

    public bool HasCompletedRun
    {
        get => _hasCompletedRun;
        private set
        {
            if (SetProperty(ref _hasCompletedRun, value))
                OnPropertyChanged(nameof(RunButtonText));
        }
    }

    public string RunButtonText => HasCompletedRun ? "Run again" : "Run";

    public bool CanRun => !IsRunning && _pipelineReady && _lastBuild is { IsComplete: true };

    public bool RunElevated
    {
        get => _runElevated;
        set
        {
            if (!SetProperty(ref _runElevated, value))
                return;

            if (value && _inProcess)
            {
                _inProcess = false;
                OnPropertyChanged(nameof(InProcess));
            }

            OnPropertyChanged(nameof(CanRunInProcess));
        }
    }

    public bool InProcess
    {
        get => _inProcess;
        set
        {
            if (value && RunElevated)
                return;

            SetProperty(ref _inProcess, value);
        }
    }

    public bool CanRunInProcess =>
        !RunElevated
        && !IsRunning
        && (_formCommand?.Shell ?? PowerShellEdition.PowerShell7) == PowerShellEdition.PowerShell7;

    public void Cancel() => _runCts?.Cancel();

    private CascadeLevelViewModel CreateLevel(int index) =>
        new(_session, index, OnStructureChanged);

    private void OnStructureChanged()
    {
        _genericCommand = null;
        ClearMetadata();
        while (Levels.Count > _session.Levels.Count)
            Levels.RemoveAt(Levels.Count - 1);

        while (Levels.Count < _session.Levels.Count)
            Levels.Add(CreateLevel(Levels.Count));

        UpdateForm();
        RefreshCommands();
    }

    private void UpdateForm()
    {
        var command = _genericCommand ?? _session.SelectedCommand;
        if (ReferenceEquals(command, _formCommand))
        {
            OnPropertyChanged(nameof(HasCommand));
            return;
        }

        HasCompletedRun = false;
        _formCommand = command;
        Segments.Clear();
        _fields.Clear();
        if (command is null)
        {
            CommandSummary = "";
            ElevationHint = "";
            PreviewText = "";
            ValidationText = "";
            _lastBuild = null;
            RunElevated = false;
            OnPropertyChanged(nameof(HasCommand));
            OnPropertyChanged(nameof(CanRun));
            return;
        }

        RunElevated = command.RequiresAdmin;
        if (command.Shell == PowerShellEdition.WindowsPowerShell5)
            InProcess = false;

        CommandSummary = command.PathDisplay + "    ·    " + command.Summary
            + (string.IsNullOrWhiteSpace(command.Description) ? "" : Environment.NewLine + command.Description);
        ElevationHint = DescribeCommand(command);
        OnPropertyChanged(nameof(CanRunInProcess));

        var seeds = ParameterValues.Seed(command);
        foreach (var segment in SentenceForm.Parse(command).Segments)
        {
            switch (segment)
            {
                case SentenceSegment.Text text:
                    Segments.Add(new TextSegmentViewModel(text.Value));
                    break;
                case SentenceSegment.Field field:
                    var viewModel = FieldSegmentViewModel.Create(field.Parameter, seeds[field.Parameter.Name]);
                    viewModel.ValueChanged += (_, _) => UpdatePreview();
                    _fields.Add(viewModel);
                    Segments.Add(viewModel);
                    break;
            }
        }

        OnPropertyChanged(nameof(HasCommand));
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_formCommand is null)
        {
            PreviewText = "";
            ValidationText = "";
            _lastBuild = null;
            OnPropertyChanged(nameof(CanRun));
            RefreshCommands();
            return;
        }

        var values = new Dictionary<string, ParameterValue>(StringComparer.Ordinal);
        foreach (var field in _fields)
            values[field.Parameter.Name] = ReadValue(field);

        _lastBuild = PowerShellCommandBuilder.Build(_formCommand, values);
        var errors = new List<string>(_lastBuild.Errors);
        if (!ScriptComposer.TryApplyPipeline(_lastBuild.CommandText, PipelineText, out var previewLine, out var pipelineError)
            || !ScriptComposer.TryApplyPipeline(_lastBuild.ExecutionText, PipelineText, out var executionLine, out _))
        {
            _pipelineReady = false;
            _executionText = _lastBuild.ExecutionText;
            PreviewText = ScriptComposer.ComposePreview(_formCommand, _lastBuild.CommandText);
            if (pipelineError is not null)
                errors.Add(pipelineError);
        }
        else
        {
            _pipelineReady = true;
            _executionText = executionLine;
            PreviewText = ScriptComposer.ComposePreview(_formCommand, previewLine);
        }

        ValidationText = string.Join(Environment.NewLine, errors);
        OnPropertyChanged(nameof(CanRun));
        RefreshCommands();
    }

    private async Task RunAsync()
    {
        if (_formCommand is null || _lastBuild is not { IsComplete: true })
            return;

        var command = _formCommand;
        var executionText = _executionText;
        var commandSecrets = new Dictionary<string, string>(_lastBuild.Secrets, StringComparer.Ordinal);
        _redact.Clear();
        var preview = PreviewText;
        var credentialSignIn = command.SignIn?.Mode == SignInMode.Credential;
        var interactiveSignIn = command.SignIn?.Mode == SignInMode.Interactive;
        var elevate = RunElevated;
        var inProcess = InProcess && !elevate && command.Shell == PowerShellEdition.PowerShell7;
        PowerShellShell? shell = null;
        ElevatedConnection? connection = null;
        _runCts = new CancellationTokenSource();
        IsRunning = true;
        HasCompletedRun = false;
        ExternalWindows.Hold();

        try
        {
            if (_modules is not null && !string.IsNullOrWhiteSpace(command.Module))
            {
                StatusText = "Checking whether " + command.Module + " is installed…";
                var plan = await _modules.FindMissingAsync(command.Module, command.Shell, _runCts.Token).ConfigureAwait(true);
                if (plan is not null)
                {
                    if (_confirmation is null || !_confirmation.Confirm(plan.Summary))
                    {
                        StatusText = "Install was not confirmed. Nothing was installed, and the command was not run.";
                        return;
                    }

                    StatusText = plan.RequiresAdmin
                        ? "Waiting for administrator approval to install " + plan.ModuleName + "…"
                        : "Installing " + plan.ModuleName + "…";
                    await _modules.InstallAsync(plan, _runCts.Token).ConfigureAwait(true);
                }
            }

            var outcome = await LaunchDriver.RunAsync(
                credentialSignIn,
                interactiveSignIn,
                elevate,
                command.SignIn,
                commandSecrets,
                new StageGate(),
                _signIn.Ask,
                _ =>
                {
                    if (inProcess)
                        return Task.FromResult<string?>(null);

                    var resolution = PowerShellLocator.Resolve(command.Shell);
                    if (resolution.Error is not null || resolution.Shell is null)
                        return Task.FromResult<string?>(resolution.Error ?? "PowerShell was not found.");

                    shell = resolution.Shell;
                    if (resolution.Notice is not null)
                        Emit(resolution.Notice + Environment.NewLine, isError: true);

                    return Task.FromResult<string?>(null);
                },
                async cancellationToken => connection = await new ElevatedCommandRunner().ConnectAsync(cancellationToken),
                (username, secrets, cancellationToken) =>
                {
                    _redact.Clear();
                    foreach (var secret in secrets.Values)
                    {
                        if (secret.Length >= 4)
                            _redact.Add(secret);
                    }

                    Emit(Environment.NewLine + "PS> " + preview + Environment.NewLine, isError: false);
                    var script = ScriptComposer.ComposeExecution(command, executionText, username);
                    var request = new ScriptRequest(script, secrets, interactiveSignIn);
                    if (connection is not null)
                        return connection.InvokeAsync(request, _output, cancellationToken);

                    return _executor.RunAsync(request, elevated: false, inProcess, shell, _output, cancellationToken);
                },
                _runCts.Token,
                stage =>
                {
                    StatusText = stage switch
                    {
                        LaunchStage.CredentialPrompt => "Signing in to " + command.SignIn!.Service + "…",
                        LaunchStage.Elevation => "Waiting for administrator approval…",
                        LaunchStage.SignInWindow => "Signing in to " + command.SignIn!.Service + "…",
                        _ => elevate ? "Running elevated…" : "Running…"
                    };
                });

            if (outcome.Status == LaunchStatus.Cancelled)
            {
                StatusText = "Sign-in was cancelled.";
                return;
            }

            if (outcome.Status == LaunchStatus.Failed)
            {
                Emit(Environment.NewLine + outcome.Error + Environment.NewLine, isError: true);
                StatusText = outcome.Error ?? "Failed.";
                return;
            }

            Emit(Environment.NewLine + "[exit " + outcome.ExitCode + "]" + Environment.NewLine, outcome.ExitCode != 0);
            StatusText = outcome.ExitCode == 0 ? "Completed. Run again, or start over." : "Completed with exit code " + outcome.ExitCode + ".";
            HasCompletedRun = true;
        }
        catch (OperationCanceledException)
        {
            Emit(Environment.NewLine + "[cancelled]" + Environment.NewLine, isError: true);
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            Emit(Environment.NewLine + ex.Message + Environment.NewLine, isError: true);
            StatusText = "Failed.";
        }
        finally
        {
            commandSecrets.Clear();
            if (connection is not null)
                await connection.DisposeAsync();

            _runCts.Dispose();
            _runCts = null;
            _redact.Clear();
            IsRunning = false;
            var deferred = ExternalWindows.Release();
            if (!string.IsNullOrEmpty(deferred))
                System.Windows.MessageBox.Show(deferred, ProductName.Name);
        }
    }

    private async Task LookupAsync()
    {
        var name = CmdletQuery.Trim();
        if (name.Length == 0)
            return;

        IsRunning = true;
        StatusText = "Looking up " + name + "…";
        try
        {
            var result = await _probe.LookupAsync(name, CancellationToken.None).ConfigureAwait(true);
            if (result.Command is null || result.SnapshotJson is null)
            {
                StatusText = result.Error ?? "Lookup failed.";
                return;
            }

            var view = CommandMetadataMapper.Read(result.SnapshotJson);
            ShowMetadata(result.SnapshotJson, view);
            StatusText = result.Notice is null
                ? "Loaded " + result.Command.Cmdlet + " from Get-Command."
                : result.Notice;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void StartOver()
    {
        _genericCommand = null;
        ClearMetadata();
        _session.Reset();
        Levels.Clear();
        Levels.Add(CreateLevel(0));
        UpdateForm();
        StatusText = "Choose a favorite, or browse installed commands.";
        RefreshCommands();
    }

    private async Task ApplyDiscoveredAsync(DiscoveredCommand command)
    {
        var qualified = string.IsNullOrWhiteSpace(command.Module)
            ? command.Name
            : command.Module + "\\" + command.Name;
        IsRunning = true;
        StatusText = "Reading " + command.Name + "…";
        try
        {
            var result = await _probe.LookupAsync(qualified, Browser.Edition, CancellationToken.None).ConfigureAwait(true);
            if (result.Command is null || result.SnapshotJson is null)
            {
                StatusText = result.Error ?? "Lookup failed.";
                return;
            }

            var view = CommandMetadataMapper.Read(result.SnapshotJson, null, Browser.Edition);
            ShowMetadata(result.SnapshotJson, view);
            StatusText = result.Notice ?? "Loaded " + view.Command.Cmdlet + " from " + Browser.EditionLabel + ".";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ShowMetadata(string snapshotJson, CommandMetadataView view)
    {
        _snapshotJson = snapshotJson;
        _updatingSets = true;
        ParameterSetNames.Clear();
        foreach (var name in view.ParameterSets)
            ParameterSetNames.Add(name);

        _selectedParameterSet = view.ParameterSet;
        OnPropertyChanged(nameof(SelectedParameterSet));
        OnPropertyChanged(nameof(HasParameterSets));
        _updatingSets = false;
        ShowGeneric(view.Command);
    }

    private void ShowGeneric(CommandDefinition command)
    {
        _session.Reset();
        Levels.Clear();
        Levels.Add(CreateLevel(0));
        _genericCommand = command;
        UpdateForm();
    }

    private void ClearMetadata()
    {
        _snapshotJson = null;
        _updatingSets = true;
        ParameterSetNames.Clear();
        _selectedParameterSet = null;
        _updatingSets = false;
        OnPropertyChanged(nameof(SelectedParameterSet));
        OnPropertyChanged(nameof(HasParameterSets));
    }

    private void RefreshCommands()
    {
        RunCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        StartOverCommand.RaiseCanExecuteChanged();
        LookupCommand.RaiseCanExecuteChanged();
    }

    private static ParameterValue ReadValue(FieldSegmentViewModel field)
    {
        if (field is ArrayFieldViewModel array)
            return ParameterValue.FromItems(array.Items);

        if (field is SecureFieldViewModel secure)
            return ParameterValue.FromText(secure.Secret);

        return ParameterValue.FromText(field.Value);
    }

    private static string DescribeCommand(CommandDefinition command)
    {
        var parts = new List<string>();
        parts.Add(command.RequiresAdmin
            ? "This command is marked requiresAdmin. Local elevation does not sign you in to Exchange or Active Directory."
            : "This command can run as the current user.");
        parts.Add(command.Shell == PowerShellEdition.WindowsPowerShell5
            ? "It runs in Windows PowerShell 5.1."
            : "It runs in PowerShell 7 (pwsh). If pwsh is not installed, the app falls back to Windows PowerShell 5.1 and says so.");
        if (command.SignIn is not null)
        {
            parts.Add(command.SignIn.Prompt);
            parts.Add(command.SignIn.Mode == SignInMode.Credential
                ? "Each run asks for credentials again and does not reuse the previous password."
                : "The sign-in window stays up by itself until it closes.");
        }

        return string.Join(" ", parts);
    }

    private void Emit(string text, bool isError)
    {
        foreach (var secret in _redact)
            text = text.Replace(secret, "***", StringComparison.Ordinal);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            OutputAppended?.Invoke(this, new OutputChunkEventArgs(text, isError));
            return;
        }

        dispatcher.Invoke(() => OutputAppended?.Invoke(this, new OutputChunkEventArgs(text, isError)));
    }

    private sealed class OutputAdapter : IExecutionOutput
    {
        private readonly MainViewModel _owner;

        public OutputAdapter(MainViewModel owner) => _owner = owner;

        public void WriteStdout(string text) => _owner.Emit(text, false);

        public void WriteStderr(string text) => _owner.Emit(text, true);
    }
}
