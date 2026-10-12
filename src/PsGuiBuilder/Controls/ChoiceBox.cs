// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Windows;
using System.Windows.Controls;

namespace PsGuiBuilder.Controls;

public static class ChoiceBox
{
    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.RegisterAttached(
        "SelectedValue",
        typeof(string),
        typeof(ChoiceBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(ChoiceBox),
        new PropertyMetadata(false));

    public static string? GetSelectedValue(DependencyObject element) => (string?)element.GetValue(SelectedValueProperty);

    public static void SetSelectedValue(DependencyObject element, string? value) => element.SetValue(SelectedValueProperty, value);

    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ComboBox box)
            return;

        Hook(box);
        Apply(box);
    }

    private static void Hook(ComboBox box)
    {
        if ((bool)box.GetValue(HookedProperty))
            return;

        box.SetValue(HookedProperty, true);
        box.SelectionChanged += OnSelectionChanged;
        box.Loaded += (_, _) => Apply(box);
        box.ItemContainerGenerator.StatusChanged += (_, _) => Apply(box);
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        var box = (ComboBox)sender;
        if (box.SelectedItem is string selected && !string.Equals(selected, GetSelectedValue(box), StringComparison.Ordinal))
            SetSelectedValue(box, selected);
    }

    private static void Apply(ComboBox box)
    {
        var value = GetSelectedValue(box);
        if (string.IsNullOrEmpty(value) || Equals(box.SelectedItem, value))
            return;

        if (!box.Items.Contains(value))
            return;

        box.SelectedItem = value;
    }
}
