using System.Windows;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter.Dialogs;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
