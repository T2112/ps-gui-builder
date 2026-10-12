// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PsGuiBuilder.ViewModels;

namespace PsGuiBuilder;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachOutput();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.IsRunning)
            viewModel.Cancel();

        base.OnClosing(e);
    }

    private void AttachOutput()
    {
        if (_viewModel is not null)
            _viewModel.OutputAppended -= AppendOutput;

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.OutputAppended += AppendOutput;
    }

    private void AppendOutput(object? sender, OutputChunkEventArgs args)
    {
        var paragraph = OutputBox.Document.Blocks.LastBlock as Paragraph;
        if (paragraph is null)
        {
            paragraph = new Paragraph { Margin = new Thickness(0) };
            OutputBox.Document.Blocks.Add(paragraph);
        }

        paragraph.Inlines.Add(new Run(args.Text)
        {
            Foreground = args.IsError
                ? new SolidColorBrush(Color.FromRgb(0xFB, 0x71, 0x85))
                : new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF4))
        });
        OutputBox.ScrollToEnd();
    }
}
