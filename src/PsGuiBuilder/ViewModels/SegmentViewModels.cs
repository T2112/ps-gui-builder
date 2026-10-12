// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

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

            // A choice ComboBox reports an empty selection while its items are still loading.
            // Keep a value that is already in the choice list.
            if (next.Length == 0
                && Parameter.Type == ParameterType.Choice
                && Parameter.Choices.Contains(_value))
                return;

            _value = next;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMissing));
            OnPropertyChanged(nameof(ShowPlaceholder));
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsMissing => Parameter.Required && string.IsNullOrWhiteSpace(_value);

    public bool ShowPlaceholder => string.IsNullOrEmpty(_value) && !string.IsNullOrEmpty(Parameter.Placeholder);

    public event EventHandler? ValueChanged;

    public static FieldSegmentViewModel Create(CommandParameter parameter, string initial) =>
        parameter.Type == ParameterType.Choice
            ? new ChoiceFieldViewModel(parameter, initial)
            : new StringFieldViewModel(parameter, initial);

    private static string BuildMeta(CommandParameter parameter)
    {
        var kind = parameter.Type == ParameterType.Choice ? "choice" : "string";
        var need = parameter.Required ? "required" : "optional";
        if (!string.IsNullOrEmpty(parameter.Default))
            return kind + " · " + need + " · default " + parameter.Default;

        return kind + " · " + need;
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
