// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Cascade;

namespace PsGuiBuilder.ViewModels;

public sealed class CascadeLevelViewModel : ObservableObject
{
    private readonly CascadeSession _session;
    private readonly int _index;
    private readonly Action _onStructureChanged;
    private string _text = "";
    private CascadeOption? _selected;
    private bool _suppress;

    public CascadeLevelViewModel(CascadeSession session, int index, Action onStructureChanged)
    {
        _session = session;
        _index = index;
        _onStructureChanged = onStructureChanged;
        Prompt = session.Levels[index].Prompt;
        FilteredOptions = session.Levels[index].FilteredOptions.ToArray();
    }

    public string Prompt { get; }

    public IReadOnlyList<CascadeOption> FilteredOptions { get; private set; }

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? "";
            if (_text == next)
                return;

            _text = next;
            OnPropertyChanged();
            if (_suppress)
                return;

            _session.SetFilter(_index, next);
            RefreshFilter();
            var selected = _session.Levels[_index].SelectedKey;
            if (selected is not null && !string.Equals(selected, next, StringComparison.Ordinal))
            {
                _session.ClearSelection(_index);
                if (_selected is not null)
                {
                    _selected = null;
                    OnPropertyChanged(nameof(Selected));
                }

                _onStructureChanged();
            }
        }
    }

    public CascadeOption? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value))
                return;

            if (value is null)
            {
                var selectedKey = _session.Levels[_index].SelectedKey;
                if (selectedKey is not null && string.Equals(selectedKey, _text, StringComparison.Ordinal))
                    return;

                _selected = null;
                OnPropertyChanged();
                return;
            }

            _selected = value;
            OnPropertyChanged();
            _suppress = true;
            Text = value.Name;
            _suppress = false;
            _session.Select(_index, value.Name);
            RefreshFilter();
            _onStructureChanged();
        }
    }

    private void RefreshFilter()
    {
        FilteredOptions = _session.Levels[_index].FilteredOptions.ToArray();
        OnPropertyChanged(nameof(FilteredOptions));
    }
}
