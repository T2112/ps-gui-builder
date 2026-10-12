// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Collections.ObjectModel;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.ViewModels;

public abstract class SegmentViewModel : ObservableObject
{
}

public sealed class TextSegmentViewModel : SegmentViewModel
{
    public TextSegmentViewModel(string text) => Text = text;

    public string Text { get; }
}

public class FieldSegmentViewModel : SegmentViewModel
{
    private string _value;

    protected FieldSegmentViewModel(CommandParameter parameter, string initial)
    {
        Parameter = parameter;
        _value = initial;
        MetaText = BuildMeta(parameter);
    }

    public CommandParameter Parameter { get; }

    public string MetaText { get; }

    public string? Placeholder => Parameter.Placeholder;

    public bool IsRequired => Parameter.Required;

    public IReadOnlyList<string> Choices => Parameter.Choices;

    public string Value
    {
        get => _value;
        set
        {
            var next = value ?? "";
            if (_value == next)
                return;

            if (next.Length == 0
                && Parameter.Type == ParameterType.Choice
                && Parameter.Choices.Contains(_value))
                return;

            _value = next;
            NotifyValue();
        }
    }

    public virtual bool IsMissing => Parameter.Required && string.IsNullOrWhiteSpace(_value);

    public bool ShowPlaceholder => string.IsNullOrEmpty(_value) && !string.IsNullOrEmpty(Parameter.Placeholder);

    public event EventHandler? ValueChanged;

    public static FieldSegmentViewModel Create(CommandParameter parameter, string initial) =>
        parameter.Type switch
        {
            ParameterType.Choice => new ChoiceFieldViewModel(parameter, initial),
            ParameterType.Switch => new SwitchFieldViewModel(parameter, initial),
            ParameterType.Bool => new BoolFieldViewModel(parameter, initial),
            ParameterType.SecureString => new SecureFieldViewModel(parameter),
            ParameterType.Array => new ArrayFieldViewModel(parameter, initial),
            _ => new StringFieldViewModel(parameter, initial)
        };

    protected void NotifyValue()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(ShowPlaceholder));
        RaiseValueChanged();
    }

    protected void RaiseValueChanged() => ValueChanged?.Invoke(this, EventArgs.Empty);

    private static string BuildMeta(CommandParameter parameter)
    {
        var kind = parameter.Type switch
        {
            ParameterType.Choice => "choice",
            ParameterType.Switch => "switch",
            ParameterType.Bool => "bool",
            ParameterType.Int => "int",
            ParameterType.Number => "number",
            ParameterType.SecureString => "secure string",
            ParameterType.Array => "array",
            _ => "string"
        };
        var need = parameter.Required ? "required" : "optional";
        if (parameter.Type == ParameterType.SecureString || string.IsNullOrEmpty(parameter.Default))
            return kind + " · " + need;

        return kind + " · " + need + " · default " + parameter.Default;
    }
}

public sealed class StringFieldViewModel : FieldSegmentViewModel
{
    public StringFieldViewModel(CommandParameter parameter, string initial)
        : base(parameter, initial)
    {
    }
}

public sealed class ChoiceFieldViewModel : FieldSegmentViewModel
{
    public ChoiceFieldViewModel(CommandParameter parameter, string initial)
        : base(parameter, initial)
    {
    }
}

public sealed class SwitchFieldViewModel : FieldSegmentViewModel
{
    public SwitchFieldViewModel(CommandParameter parameter, string initial)
        : base(parameter, string.Equals(initial, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false")
    {
    }

    public bool IsChecked
    {
        get => string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "true" : "false";
    }
}

public sealed class BoolOption
{
    public BoolOption(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public string Value { get; }
}

public sealed class BoolFieldViewModel : FieldSegmentViewModel
{
    public BoolFieldViewModel(CommandParameter parameter, string initial)
        : base(parameter, initial)
    {
        var options = new List<BoolOption>();
        if (!parameter.Required)
            options.Add(new BoolOption("leave unset", ""));

        options.Add(new BoolOption("true", "true"));
        options.Add(new BoolOption("false", "false"));
        Options = options;
    }

    public IReadOnlyList<BoolOption> Options { get; }
}

public sealed class SecureFieldViewModel : FieldSegmentViewModel
{
    private string _secret = "";

    public SecureFieldViewModel(CommandParameter parameter)
        : base(parameter, "")
    {
    }

    public string Secret
    {
        get => _secret;
        set
        {
            var next = value ?? "";
            if (_secret == next)
                return;

            _secret = next;
            OnPropertyChanged(nameof(IsMissing));
            RaiseValueChanged();
        }
    }

    public override bool IsMissing => Parameter.Required && _secret.Length == 0;
}

public sealed class ArrayRow : ObservableObject
{
    private string _text;

    public ArrayRow(string text, Action changed)
    {
        _text = text;
        Changed = changed;
    }

    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? ""))
                return;

            Changed();
        }
    }

    private Action Changed { get; }
}

public sealed class ArrayFieldViewModel : FieldSegmentViewModel
{
    public ArrayFieldViewModel(CommandParameter parameter, string initial)
        : base(parameter, "")
    {
        Rows = new ObservableCollection<ArrayRow>();
        if (!string.IsNullOrEmpty(initial))
            Rows.Add(new ArrayRow(initial, NotifyRows));
        else
            Rows.Add(new ArrayRow("", NotifyRows));

        AddRowCommand = new RelayCommand(() => Rows.Add(new ArrayRow("", NotifyRows)));
        RemoveRowCommand = new RelayCommand<ArrayRow>(row =>
        {
            if (row is null)
                return;

            Rows.Remove(row);
            if (Rows.Count == 0)
                Rows.Add(new ArrayRow("", NotifyRows));

            NotifyRows();
        });
    }

    public ObservableCollection<ArrayRow> Rows { get; }

    public RelayCommand AddRowCommand { get; }

    public RelayCommand<ArrayRow> RemoveRowCommand { get; }

    public IReadOnlyList<string> Items =>
        Rows.Select(row => row.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();

    public override bool IsMissing => Parameter.Required && Items.Count == 0;

    private void NotifyRows()
    {
        OnPropertyChanged(nameof(IsMissing));
        RaiseValueChanged();
    }
}
