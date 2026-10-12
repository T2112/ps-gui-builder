// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PsGuiBuilder.Core.Cascade;

namespace PsGuiBuilder.Controls;

public partial class SearchableComboBox : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SearchableComboBox),
        new PropertyMetadata(""));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(SearchableComboBox),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SearchableComboBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(SearchableComboBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private int _quiet;
    private bool _highlightExplicit;

    public SearchableComboBox()
    {
        InitializeComponent();
        List.ItemsSource = ItemsSource;
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (SearchableComboBox)sender;
        control._highlightExplicit = false;
        if (control.List is null || control.OptionsPopup is null)
            return;

        control.List.ItemsSource = args.NewValue as IEnumerable;
        control.List.SelectedIndex = -1;
        if (control.OptionsPopup.IsOpen)
            control.OpenOptionsPopup();
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (SearchableComboBox)sender;
        if (control._quiet > 0 || control.Input is null || !control.Input.IsKeyboardFocusWithin)
            return;

        control._highlightExplicit = false;
        control.OpenOptionsPopup();
    }

    private void Input_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => OpenOptionsPopup();

    private void Input_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsWithinPopup(e.NewFocus as DependencyObject))
            return;

        CommitResolved();
        OptionsPopup.IsOpen = false;
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var items = Snapshot();
        if (e.Key == Key.Down)
        {
            OpenOptionsPopup();
            if (items.Count == 0)
                return;

            var next = Math.Min(List.SelectedIndex + 1, items.Count - 1);
            if (List.SelectedIndex < 0)
                next = 0;

            List.SelectedIndex = next;
            List.ScrollIntoView(List.SelectedItem);
            _highlightExplicit = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (items.Count == 0)
                return;

            var next = Math.Max(List.SelectedIndex - 1, 0);
            List.SelectedIndex = next;
            List.ScrollIntoView(List.SelectedItem);
            _highlightExplicit = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (_highlightExplicit && List.SelectedItem is CascadeOption highlighted)
                Commit(highlighted, returnFocus: true);
            else
                CommitResolved();

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            OptionsPopup.IsOpen = false;
            _highlightExplicit = false;
            e.Handled = true;
        }
    }

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) is ListBoxItem item
            && item.DataContext is CascadeOption option)
        {
            Commit(option, returnFocus: true);
            e.Handled = true;
        }
    }

    private void CommitResolved()
    {
        if (_quiet > 0)
            return;

        var items = Snapshot();
        var text = (Text ?? "").Trim();
        if (text.Length == 0)
        {
            if (items.Count == 1)
                Commit(items[0], returnFocus: false);

            return;
        }

        var exact = items.Where(option => string.Equals(option.Name, text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            Commit(exact[0], returnFocus: false);
            return;
        }

        if (items.Count == 1)
            Commit(items[0], returnFocus: false);
    }

    private void Commit(CascadeOption option, bool returnFocus)
    {
        _quiet++;
        try
        {
            SelectedItem = option;
            Text = option.Name;
            _highlightExplicit = false;
            OptionsPopup.IsOpen = false;
        }
        finally
        {
            _quiet--;
        }

        if (!returnFocus)
            return;

        Input.Focus();
        Input.CaretIndex = Input.Text?.Length ?? 0;
    }

    private void OpenOptionsPopup()
    {
        if (Input is null || OptionsPopup is null)
            return;

        var items = Snapshot();
        EmptyHint.Visibility = items.Count == 0 && !string.IsNullOrWhiteSpace(Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
        List.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        OptionsPopup.Width = Math.Max(Input.ActualWidth, 240);
        OptionsPopup.IsOpen = items.Count > 0 || !string.IsNullOrWhiteSpace(Text);
    }

    private List<CascadeOption> Snapshot()
    {
        var items = new List<CascadeOption>();
        if (ItemsSource is null)
            return items;

        foreach (var item in ItemsSource)
        {
            if (item is CascadeOption option)
                items.Add(option);
        }

        return items;
    }

    private bool IsWithinPopup(DependencyObject? node)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, OptionsPopup) || ReferenceEquals(node, OptionsPopup.Child))
                return true;

            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }

        return false;
    }
}
