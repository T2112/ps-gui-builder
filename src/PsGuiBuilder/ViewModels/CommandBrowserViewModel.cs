// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Collections.ObjectModel;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Discovery;

namespace PsGuiBuilder.ViewModels;

public sealed class ShellChoice
{
    public ShellChoice(string label, PowerShellEdition edition)
    {
        Label = label;
        Edition = edition;
    }

    public string Label { get; }

    public PowerShellEdition Edition { get; }
}

public sealed class BrowserRow
{
    public BrowserRow(string group, DiscoveredCommand command)
    {
        Group = group;
        Command = command;
        Detail = command.CommandType + "  ·  " + (command.Module.Length == 0 ? "no module" : command.Module);
    }

    public string Group { get; }

    public string Title => Command.Name;

    public string Detail { get; }

    public DiscoveredCommand Command { get; }
}

public sealed class CommandBrowserViewModel : ObservableObject
{
    private readonly IDiscoverySource _source;
    private readonly Func<DiscoveredCommand, Task> _use;
    private readonly ObservableCollection<string> _modules = new();
    private readonly ObservableCollection<string> _nouns = new();
    private readonly ObservableCollection<string> _verbs = new();
    private DiscoveryCatalog? _catalog;
    private ShellChoice _selectedShell;
    private string _searchText = "";
    private string _selectedModule = "Any";
    private string _selectedNoun = "Any";
    private string _selectedVerb = "Any";
    private bool _groupByModule = true;
    private bool _includeAliases;
    private bool _busy;
    private bool _opened;
    private bool _updating;
    private int _generation;
    private string _baseStatus = "Refresh reads every installed module and command. The result is cached for this shell.";
    private string _statusText = "Refresh reads every installed module and command. The result is cached for this shell.";
    private BrowserRow? _selectedRow;

    public CommandBrowserViewModel(IDiscoverySource source, Func<DiscoveredCommand, Task> use)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _use = use ?? throw new ArgumentNullException(nameof(use));
        Shells = new[]
        {
            new ShellChoice("PowerShell 7", PowerShellEdition.PowerShell7),
            new ShellChoice("Windows PowerShell 5.1", PowerShellEdition.WindowsPowerShell5)
        };
        _selectedShell = Shells[0];
        Modules = _modules;
        Nouns = _nouns;
        Verbs = _verbs;
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(refresh: true), () => !IsBusy);
        UseCommand = new AsyncRelayCommand(UseAsync, () => !IsBusy && SelectedRow is not null);
    }

    public IReadOnlyList<ShellChoice> Shells { get; }

    public ObservableCollection<string> Modules { get; }

    public ObservableCollection<string> Nouns { get; }

    public ObservableCollection<string> Verbs { get; }

    public ObservableCollection<BrowserRow> Rows { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand UseCommand { get; }

    public PowerShellEdition Edition => _selectedShell.Edition;

    public string EditionLabel => _selectedShell.Label;

    public ShellChoice SelectedShell
    {
        get => _selectedShell;
        set
        {
            if (value is null || !SetProperty(ref _selectedShell, value))
                return;

            if (_opened)
                _ = LoadAsync(refresh: false);
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? ""))
                Rebuild();
        }
    }

    public string SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (_updating || !SetProperty(ref _selectedModule, string.IsNullOrWhiteSpace(value) ? "Any" : value))
                return;

            RefreshFacets(keepNoun: false);
            Rebuild();
        }
    }

    public string SelectedNoun
    {
        get => _selectedNoun;
        set
        {
            if (_updating || !SetProperty(ref _selectedNoun, string.IsNullOrWhiteSpace(value) ? "Any" : value))
                return;

            RefreshFacets(keepNoun: true);
            Rebuild();
        }
    }

    public string SelectedVerb
    {
        get => _selectedVerb;
        set
        {
            if (_updating || !SetProperty(ref _selectedVerb, string.IsNullOrWhiteSpace(value) ? "Any" : value))
                return;

            Rebuild();
        }
    }

    public bool GroupByModule
    {
        get => _groupByModule;
        set
        {
            if (!SetProperty(ref _groupByModule, value))
                return;

            OnPropertyChanged(nameof(GroupByNoun));
            Rebuild();
        }
    }

    public bool GroupByNoun
    {
        get => !_groupByModule;
        set
        {
            if (value)
                GroupByModule = false;
        }
    }

    public bool IncludeAliases
    {
        get => _includeAliases;
        set
        {
            if (!SetProperty(ref _includeAliases, value))
                return;

            RefreshFacets(keepNoun: true);
            Rebuild();
        }
    }

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (!SetProperty(ref _busy, value))
                return;

            RefreshCommand.RaiseCanExecuteChanged();
            UseCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public BrowserRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetProperty(ref _selectedRow, value))
                return;

            UseCommand.RaiseCanExecuteChanged();
        }
    }

    public Task OpenAsync()
    {
        _opened = true;
        if (_catalog is not null)
            return Task.CompletedTask;

        return LoadAsync(refresh: false);
    }

    private async Task LoadAsync(bool refresh)
    {
        var generation = ++_generation;
        var edition = Edition;
        IsBusy = true;
        StatusText = refresh
            ? "Reading installed modules from " + EditionLabel + ". This can take a minute."
            : "Loading the cached catalog for " + EditionLabel + "…";
        try
        {
            var loaded = await _source.LoadAsync(edition, refresh, CancellationToken.None).ConfigureAwait(true);
            if (generation != _generation)
                return;

            _catalog = loaded.Catalog;
            var when = loaded.Catalog.CapturedUtc.ToLocalTime().ToString("g");
            _baseStatus = (loaded.FromCache ? "Cached" : "Read")
                + " " + loaded.Catalog.Commands.Count + " commands from " + EditionLabel
                + " at " + when + "."
                + (loaded.Notice is null ? "" : " " + loaded.Notice);
            RefreshFacets(keepNoun: false);
            Rebuild();
        }
        catch (Exception ex)
        {
            if (generation == _generation)
                StatusText = ex.Message;
        }
        finally
        {
            if (generation == _generation)
                IsBusy = false;
        }
    }

    private async Task UseAsync()
    {
        if (SelectedRow is null)
            return;

        await _use(SelectedRow.Command).ConfigureAwait(true);
    }

    private void RefreshFacets(bool keepNoun)
    {
        if (_catalog is null)
            return;

        _updating = true;
        var module = Filter(_selectedModule);
        var noun = keepNoun ? Filter(_selectedNoun) : null;
        Replace(_modules, DiscoveryBrowser.Modules(_catalog, _includeAliases), ref _selectedModule);
        Replace(_nouns, DiscoveryBrowser.Nouns(_catalog, module, _includeAliases), ref _selectedNoun);
        if (!keepNoun)
            _selectedNoun = "Any";
        Replace(_verbs, DiscoveryBrowser.Verbs(_catalog, module, noun ?? Filter(_selectedNoun), _includeAliases), ref _selectedVerb);
        OnPropertyChanged(nameof(SelectedModule));
        OnPropertyChanged(nameof(SelectedNoun));
        OnPropertyChanged(nameof(SelectedVerb));
        _updating = false;
    }

    private void Rebuild()
    {
        Rows.Clear();
        SelectedRow = null;
        if (_catalog is null)
            return;

        var page = DiscoveryBrowser.Search(_catalog, new DiscoveryQuery
        {
            Text = _searchText,
            Module = Filter(_selectedModule),
            Noun = Filter(_selectedNoun),
            Verb = Filter(_selectedVerb),
            IncludeAliases = _includeAliases,
            GroupBy = _groupByModule ? DiscoveryGroupBy.Module : DiscoveryGroupBy.Noun
        });
        foreach (var hit in page.Hits)
            Rows.Add(new BrowserRow(hit.Group, hit.Command));

        StatusText = page.Truncated
            ? "Showing " + page.Hits.Count + " of " + page.TotalMatches + ". Narrow the search."
            : _baseStatus;
    }

    private static string? Filter(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase) ? null : value;

    private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> values, ref string selected)
    {
        var keep = selected;
        target.Clear();
        target.Add("Any");
        foreach (var value in values)
            target.Add(value);

        selected = target.Contains(keep) ? keep : "Any";
    }
}
