using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class AutostartService : IAutostartService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationName = "PDFOrtnerSorter";
    private readonly ILoggerService _logger;

    public AutostartService(ILoggerService logger)
    {
        _logger = logger;
    }

    public Task<bool> IsEnabledAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                var value = key?.GetValue(ApplicationName) as string;
                var currentExePath = GetExecutablePath();
                
                return !string.IsNullOrEmpty(value) && value.Equals(currentExePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to check autostart status", ex);
                return false;
            }
        });
    }

    public Task EnableAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var exePath = GetExecutablePath();
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                
                if (key == null)
                {
                    _logger.LogError("Failed to open registry key for autostart");
                    return;
                }

                key.SetValue(ApplicationName, exePath);
                _logger.LogInfo($"Autostart enabled: {exePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to enable autostart", ex);
                throw;
            }
        });
    }

    public Task DisableAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                
                if (key == null)
                {
                    _logger.LogError("Failed to open registry key for autostart");
                    return;
                }

                if (key.GetValue(ApplicationName) != null)
                {
                    key.DeleteValue(ApplicationName, false);
                    _logger.LogInfo("Autostart disabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to disable autostart", ex);
                throw;
            }
        });
    }

    private static string GetExecutablePath()
    {
        // Get the actual executable path
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            return processPath;
        }

        // Fallback to AppContext.BaseDirectory (works with single-file deployment)
        var baseDir = AppContext.BaseDirectory;
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        return Path.Combine(baseDir, $"{assemblyName}.exe");
    }
}
