// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Windows;
using System.Windows.Controls;

namespace PsGuiBuilder.Controls;

public static class SecretBox
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text",
        typeof(string),
        typeof(SecretBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    private static readonly DependencyProperty HookedProperty = DependencyProperty.RegisterAttached(
        "Hooked",
        typeof(bool),
        typeof(SecretBox),
        new PropertyMetadata(false));

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value ?? "");

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not PasswordBox box)
            return;

        Hook(box);
        var next = args.NewValue as string ?? "";
        if (!string.Equals(box.Password, next, StringComparison.Ordinal))
            box.Password = next;
    }

    private static void Hook(PasswordBox box)
    {
        if ((bool)box.GetValue(HookedProperty))
            return;

        box.SetValue(HookedProperty, true);
        box.PasswordChanged += (_, _) =>
        {
            var password = box.Password;
            if (!string.Equals(GetText(box), password, StringComparison.Ordinal))
                SetText(box, password);
        };
    }
}
