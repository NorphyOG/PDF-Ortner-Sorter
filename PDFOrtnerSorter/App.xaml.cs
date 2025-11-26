using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PDFOrtnerSorter.Services.Abstractions;
using PDFOrtnerSorter.Services.Implementations;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter;

public partial class App : Application
{
	private IHost? _host;
	private Mutex? _instanceMutex;
	private ILoggerService? _logger;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		_instanceMutex = new Mutex(true, "PDFOrtnerSorter.SingleInstance", out var isNewInstance);
		if (!isNewInstance)
		{
			Shutdown();
			return;
		}

		_host = Host.CreateDefaultBuilder()
			.ConfigureServices(ConfigureServices)
			.Build();

		_host.Start();
		_logger = _host.Services.GetRequiredService<ILoggerService>();
		HookGlobalExceptionHandlers();

		var mainWindow = _host.Services.GetRequiredService<MainWindow>();
		mainWindow.Show();
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		services.AddSingleton<IFileService, FileService>();
		services.AddSingleton<IPreviewService, PreviewService>();
		services.AddSingleton<IMoveService, MoveService>();
		services.AddSingleton<ISettingsService, SettingsService>();
		services.AddSingleton<ISettingsDialogService, SettingsDialogService>();
		services.AddSingleton<ICatalogStore, CatalogStore>();
		services.AddSingleton<IFolderPicker, FolderPickerService>();
		services.AddSingleton<IMoveConfirmationService, MoveConfirmationService>();
		services.AddSingleton<ILoggerService, FileLoggerService>();
		services.AddSingleton<IBackgroundJobQueue>(_ => new BackgroundJobQueue(maxConcurrency: 4));

		services.AddSingleton<MainViewModel>();
		services.AddSingleton<MainWindow>();
	}

	private void HookGlobalExceptionHandlers()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		_logger?.LogError("Dispatcher exception", e.Exception);
		ShowCrashDialog(e.Exception);
		e.Handled = true;
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			_logger?.LogError("Unhandled exception", ex);
			ShowCrashDialog(ex);
		}
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		_logger?.LogError("Unobserved task exception", e.Exception);
		ShowCrashDialog(e.Exception);
		e.SetObserved();
	}

	private void ShowCrashDialog(Exception exception)
	{
		var message = new StringBuilder()
			.AppendLine("Es ist ein unerwarteter Fehler aufgetreten.")
			.AppendLine()
			.AppendLine(exception.Message)
			.AppendLine()
			.Append("Logdatei: ").Append(_logger?.LogFilePath ?? "unbekannt");

		MessageBox.Show(message.ToString(), "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
	}

	protected override async void OnExit(ExitEventArgs e)
	{
		if (_host is not null)
		{
			await _host.StopAsync();
			if (_host.Services.GetService<IBackgroundJobQueue>() is { } queue)
			{
				await queue.DisposeAsync();
			}

			if (_logger is IDisposable disposableLogger)
			{
				disposableLogger.Dispose();
			}

			_host.Dispose();
		}

		_instanceMutex?.Dispose();
		base.OnExit(e);
	}
}

