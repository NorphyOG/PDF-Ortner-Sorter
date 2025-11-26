using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await _viewModel.InitializeAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Startfehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDocumentItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListViewItem item)
        {
            return;
        }

        var shouldSelect = !item.IsSelected;
        item.Focus();
        item.IsSelected = shouldSelect;
        e.Handled = true;
    }
}