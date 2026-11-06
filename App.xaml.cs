using System.Diagnostics;
using WinForms = System.Windows.Forms;
using Wpf = System.Windows;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;

namespace Clim8.Windows;

public partial class App : Wpf.Application
{
    private readonly SettingsStore _settingsStore = new();
    private readonly WeatherService _weatherService = new();
    private readonly DeviceLocationService _locationService = new();
    private AppSettings _settings = new();
    private WinForms.NotifyIcon? _trayIcon;
    private DispatcherTimer? _traySingleClickTimer;
    private MainWindow? _window;
    private SettingsWindow? _settingsWindow;
    private TrayMenuWindow? _trayMenuWindow;
    private bool _locationRequestInProgress;
    private double? _weatherWindowLeft;
    private double? _weatherWindowTop;

    protected override void OnStartup(Wpf.StartupEventArgs e)
    {
        base.OnStartup(e);

        var isFirstRun = !_settingsStore.Exists;
        _settings = _settingsStore.Load();

        if (isFirstRun)
        {
            _settings.StartWithWindows = true;
            try
            {
                StartupRegistration.SetEnabled(true);
            }
            catch
            {
                _settings.StartWithWindows = false;
            }

            SaveSettings();
        }
        else if (_settings.StartWithWindows)
        {
            try
            {
                StartupRegistration.SetEnabled(true);
            }
            catch
            {
                _settings.StartWithWindows = false;
                SaveSettings();
            }
        }

        CreateTrayIcon();

        var isBackgroundLaunch = e.Args.Any(
            argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase));
        if (!isBackgroundLaunch)
        {
            ShowWeatherWindow();
        }
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "Clim8",
            Visible = true
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;
        _trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

        _traySingleClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(300, WinForms.SystemInformation.DoubleClickTime))
        };
        _traySingleClickTimer.Tick += (_, _) =>
        {
            _traySingleClickTimer.Stop();
            ShowWeatherWindow();
        };
    }

    private void TrayIcon_MouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Right)
        {
            _traySingleClickTimer?.Stop();
            ShowTrayMenuWindow();
            return;
        }

        if (e.Button != WinForms.MouseButtons.Left)
        {
            return;
        }

        if (e.Clicks > 1 || _traySingleClickTimer?.IsEnabled == true)
        {
            _traySingleClickTimer?.Stop();
            ShowTrayMenuWindow();
            return;
        }

        _traySingleClickTimer?.Stop();
        _traySingleClickTimer?.Start();
    }

    private void TrayIcon_MouseDoubleClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            _traySingleClickTimer?.Stop();
            ShowTrayMenuWindow();
        }
    }

    private void ShowTrayMenuWindow()
    {
        if (_trayMenuWindow is null)
        {
            _trayMenuWindow = new TrayMenuWindow(() => ShowWeatherWindow(), ShowSettingsWindow, Shutdown);
            _trayMenuWindow.Closed += (_, _) => _trayMenuWindow = null;
        }

        _trayMenuWindow.ShowNear(WinForms.Cursor.Position);
    }

    private void CloseTrayMenuWindow()
    {
        if (_trayMenuWindow is not null)
        {
            _trayMenuWindow.Close();
            _trayMenuWindow = null;
        }
    }

    private void ShowWeatherWindow(bool startLocationLookup = true)
    {
        CloseTrayMenuWindow();
        CloseSettingsWindow();

        if (_window is null)
        {
            var weatherWindow = new MainWindow(_settings, _weatherService, SaveSettings, SetUnits);
            _window = weatherWindow;
            if (_weatherWindowLeft is double left && _weatherWindowTop is double top)
            {
                weatherWindow.Left = left;
                weatherWindow.Top = top;
            }
            else
            {
                weatherWindow.PositionNearTray();
            }

            weatherWindow.Closed += (_, _) =>
            {
                _weatherWindowLeft = weatherWindow.Left;
                _weatherWindowTop = weatherWindow.Top;
                if (ReferenceEquals(_window, weatherWindow))
                {
                    _window = null;
                }
            };
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.Activate();
        _window.RefreshIfNeeded();
        if (startLocationLookup)
        {
            StartLocationLookupIfNeeded();
        }
    }

    private void StartLocationLookupIfNeeded()
    {
        if (!_settings.HasDeviceLocation && !_locationRequestInProgress)
        {
            _ = RefreshDeviceLocationAsync(openPrivacySettingsIfDenied: false);
        }
    }

    private void ShowSettingsWindow()
    {
        CloseTrayMenuWindow();
        CloseWeatherWindow();

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(
                _settings,
                RefreshDeviceLocation,
                SetUnits,
                SetStartupEnabled,
                RefreshWeatherNow);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.PositionNearTray();
        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        _settingsWindow.RefreshValues();
        _settingsWindow.Activate();
    }

    private void RefreshDeviceLocation()
    {
        CloseSettingsWindow();
        ShowWeatherWindow(startLocationLookup: false);
        _ = RefreshDeviceLocationAsync(openPrivacySettingsIfDenied: true);
    }

    private async Task RefreshDeviceLocationAsync(bool openPrivacySettingsIfDenied)
    {
        if (_locationRequestInProgress)
        {
            return;
        }

        _locationRequestInProgress = true;
        _window?.SetLocationStatus("Requesting device location…");
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            var hasMoved = !_settings.HasDeviceLocation
                || Math.Abs(location.Latitude - _settings.Latitude) > 0.01
                || Math.Abs(location.Longitude - _settings.Longitude) > 0.01;

            _settings.City = "Current location";
            _settings.Latitude = location.Latitude;
            _settings.Longitude = location.Longitude;
            _settings.HasDeviceLocation = true;
            if (hasMoved)
            {
                _settings.CachedWeather = null;
                _settings.CachedWeatherAt = null;
            }

            SaveSettings();
            _settingsWindow?.RefreshValues();
            if (_window?.IsVisible == true)
            {
                _window.SetLocationStatus("Location updated · loading weather");
                await _window.RefreshWeatherAsync();
            }
        }
        catch (LocationAccessDeniedException)
        {
            _window?.SetLocationStatus("Allow location in Windows Settings, then refresh");
            if (openPrivacySettingsIfDenied)
            {
                OpenWindowsLocationSettings();
            }
        }
        catch (Exception)
        {
            _window?.SetLocationStatus("Device location unavailable · try again in Settings");
        }
        finally
        {
            _locationRequestInProgress = false;
        }
    }

    private static void OpenWindowsLocationSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void RefreshWeatherNow()
    {
        CloseSettingsWindow();
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            ShowWeatherWindow();
            if (_window is not null)
            {
                await _window.RefreshWeatherAsync();
            }
        }));
    }

    private void CloseWeatherWindow()
    {
        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }
    }

    private void CloseSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
            _settingsWindow = null;
        }
    }

    private void SetUnits(bool isImperial)
    {
        if (_settings.IsImperial == isImperial)
        {
            return;
        }

        _settings.IsImperial = isImperial;
        SaveSettings();
        _settingsWindow?.RefreshValues();
        _window?.RefreshForUnitChange();
    }

    private bool SetStartupEnabled(bool enabled)
    {
        try
        {
            StartupRegistration.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            SaveSettings();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (IOException)
        {
            // Settings are optional; a restricted profile must not prevent the app from opening.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep the current session usable when the profile cannot be written.
        }
    }

    protected override void OnExit(Wpf.ExitEventArgs e)
    {
        _traySingleClickTimer?.Stop();
        CloseTrayMenuWindow();
        CloseWeatherWindow();
        CloseSettingsWindow();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon.Icon?.Dispose();
            _trayIcon = null;
        }

        base.OnExit(e);
    }
}
