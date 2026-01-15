using System.Windows;
using System.Windows.Input;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter.Dialogs;

public partial class LabelPrintDialog : Window
{
    public LabelPrintDialog(LabelPrintDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Generate initial preview
        Loaded += async (s, e) => await viewModel.GeneratePreviewCommand.ExecuteAsync(null);
    }

    private async void OnPrintClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is LabelPrintDialogViewModel viewModel)
        {
            await viewModel.PrintCommand.ExecuteAsync(null);
            DialogResult = true;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>
    /// Restrict TextBox input to numbers only
    /// </summary>
    private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits
        e.Handled = !char.IsDigit(e.Text, 0);
    }
}
