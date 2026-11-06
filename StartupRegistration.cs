using Microsoft.Win32;
using System.Reflection;

namespace Clim8.Windows;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "Clim8";

    public static void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            runKey.SetValue(AppValueName, GetStartupCommand(), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(AppValueName, throwOnMissingValue: false);
        }
    }

    private static string GetStartupCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Windows could not determine the Clim8 executable path.");
        var entryAssembly = Assembly.GetEntryAssembly()?.Location;

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entryAssembly))
        {
            return $"{Quote(processPath)} {Quote(entryAssembly)} --background";
        }

        return $"{Quote(processPath)} --background";
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
