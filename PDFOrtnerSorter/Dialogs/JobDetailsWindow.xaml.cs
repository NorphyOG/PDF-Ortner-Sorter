using System.Windows;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter.Dialogs;

public partial class JobDetailsWindow : Window
{
    public JobDetailsWindow(JobDetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
