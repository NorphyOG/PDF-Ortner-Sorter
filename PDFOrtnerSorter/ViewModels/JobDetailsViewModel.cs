using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class JobDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private Job _job;

    public bool HasErrors => Job?.HasErrors ?? false;
    public bool HasNoErrors => !HasErrors;
    public bool CanRetry => HasErrors && Job?.Status == JobStatus.Completed;

    public event EventHandler<Job>? RetryRequested;

    public JobDetailsViewModel(Job job)
    {
        _job = job;
    }

    [RelayCommand]
    private void RetryFailedFiles()
    {
        if (Job != null)
        {
            RetryRequested?.Invoke(this, Job);
        }
    }

    partial void OnJobChanged(Job value)
    {
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasNoErrors));
        OnPropertyChanged(nameof(CanRetry));
    }
}
