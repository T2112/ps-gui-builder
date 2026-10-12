// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Collections.ObjectModel;
using System.Windows;
using PsGuiBuilder.Core.Building;
using PsGuiBuilder.Core.Cascade;
using PsGuiBuilder.Core.CommandTree;
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
    private readonly OutputAdapter _output;
    private readonly List<FieldSegmentViewModel> _fields = new();
    private CommandDefinition? _formCommand;
    private CommandBuildResult? _lastBuild;
    private CancellationTokenSource? _runCts;
    private string _previewText = "";
    private string _validationText = "";
    private string _statusText = "";
    private string _commandSummary = "";
    private string _elevationHint = "";
    private bool _runElevated;
    private bool _inProcess;
    private bool _isRunning;
    private bool _hasCompletedRun;

    public MainViewModel(CommandTree tree, ICommandExecutor executor)
    {
        _session = new CascadeSession(tree);
        _executor = executor;
        _output = new OutputAdapter(this);
        Levels.Add(CreateLevel(0));
        RunCommand = new AsyncRelayCommand(RunAsync, () => CanRun);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        StartOverCommand = new RelayCommand(StartOver, () => !IsRunning);
        StatusText = "Choose a command.";
    }

    public ObservableCollection<CascadeLevelViewModel> Levels { get; } = new();

    public ObservableCollection<SegmentViewModel> Segments { get; } = new();

    public AsyncRelayCommand RunCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand StartOverCommand { get; }

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

    public bool CanRun => !IsRunning && _lastBuild is { IsComplete: true };

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

    public bool CanRunInProcess => !RunElevated && !IsRunning;

    public void Cancel() => _runCts?.Cancel();

    private CascadeLevelViewModel CreateLevel(int index) =>
        new(_session, index, OnStructureChanged);

    private void OnStructureChanged()
    {
        while (Levels.Count > _session.Levels.Count)
            Levels.RemoveAt(Levels.Count - 1);

        while (Levels.Count < _session.Levels.Count)
            Levels.Add(CreateLevel(Levels.Count));

        UpdateForm();
        RefreshCommands();
    }

    private void UpdateForm()
    {
        var command = _session.SelectedCommand;
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
        InProcess = false;
        CommandSummary = command.PathDisplay + "    ·    " + command.Summary;
        ElevationHint = command.RequiresAdmin
            ? "This command is marked requiresAdmin. Local elevation does not sign you in to Exchange or Active Directory."
            : "This command can run as the current user.";

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

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in _fields)
            values[field.Parameter.Name] = field.Value;

        _lastBuild = PowerShellCommandBuilder.Build(_formCommand, values);
        PreviewText = ScriptComposer.ComposePreview(_formCommand, _lastBuild.CommandText);
        ValidationText = string.Join(Environment.NewLine, _lastBuild.Errors);
        OnPropertyChanged(nameof(CanRun));
        RefreshCommands();
    }

    private async Task RunAsync()
    {
        if (_formCommand is null || _lastBuild is not { IsComplete: true })
            return;

        var script = ScriptComposer.ComposeExecution(_formCommand, _lastBuild.CommandText);
        var preview = PreviewText;
        _runCts = new CancellationTokenSource();
        IsRunning = true;
        HasCompletedRun = false;
        StatusText = RunElevated ? "Waiting for administrator approval…" : "Running…";
        Emit(Environment.NewLine + "PS> " + preview + Environment.NewLine, isError: false);

        try
        {
            var code = await _executor.RunAsync(script, RunElevated, InProcess && !RunElevated, _output, _runCts.Token);
            Emit(Environment.NewLine + "[exit " + code + "]" + Environment.NewLine, code != 0);
            StatusText = code == 0 ? "Completed. Run again, or start over." : "Completed with exit code " + code + ".";
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
            _runCts.Dispose();
            _runCts = null;
            IsRunning = false;
        }
    }

    private void StartOver()
    {
        _session.Reset();
        Levels.Clear();
        Levels.Add(CreateLevel(0));
        UpdateForm();
        StatusText = "Choose a command.";
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        RunCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        StartOverCommand.RaiseCanExecuteChanged();
    }

    private void Emit(string text, bool isError)
    {
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
