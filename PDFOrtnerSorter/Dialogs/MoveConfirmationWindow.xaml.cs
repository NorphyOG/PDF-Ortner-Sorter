using System.Windows;

namespace PDFOrtnerSorter.Dialogs;

public partial class MoveConfirmationWindow : Window
{
    public MoveConfirmationWindow(string currentFolderName)
    {
        InitializeComponent();
        FolderName = currentFolderName;
        DataContext = this;
        Loaded += (_, _) =>
        {
            FolderNameTextBox.Focus();
            FolderNameTextBox.SelectAll();
        };
    }

    public string FolderName { get; set; }

    private void OnConfirmClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
