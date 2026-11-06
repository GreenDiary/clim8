using WinForms = System.Windows.Forms;
using Wpf = System.Windows;

namespace Clim8.Windows;

public partial class SettingsWindow : Wpf.Window
{
    private readonly AppSettings _settings;
    private readonly Action _refreshDeviceLocation;
    private readonly Action<bool> _setUnits;
    private readonly Func<bool, bool> _setStartupEnabled;
    private readonly Action _refreshWeather;
    private bool _updatingValues;

    public SettingsWindow(
        AppSettings settings,
        Action refreshDeviceLocation,
        Action<bool> setUnits,
        Func<bool, bool> setStartupEnabled,
        Action refreshWeather)
    {
        InitializeComponent();
        _settings = settings;
        _refreshDeviceLocation = refreshDeviceLocation;
        _setUnits = setUnits;
        _setStartupEnabled = setStartupEnabled;
        _refreshWeather = refreshWeather;
        RefreshValues();
    }

    public void PositionNearTray()
    {
        var screen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        var area = screen.WorkingArea;
        Left = Math.Max(
            DpiCompat.ToDip(area.Left) + DpiCompat.ToDip(4),
            DpiCompat.ToDip(area.Right) - Width - DpiCompat.ToDip(8));
        Top = Math.Max(
            DpiCompat.ToDip(area.Top) + DpiCompat.ToDip(4),
            DpiCompat.ToDip(area.Bottom) - Height - DpiCompat.ToDip(8));
    }

    public void RefreshValues()
    {
        LocationValue.Text = _settings.HasDeviceLocation ? "Current location" : "Not set";
        UnitsValue.Text = _settings.IsImperial ? "°F" : "°C";

        _updatingValues = true;
        StartupCheckBox.IsChecked = _settings.StartWithWindows;
        _updatingValues = false;
    }

    private void CloseButton_Click(object sender, Wpf.RoutedEventArgs e) => Close();

    private void LocationButton_Click(object sender, Wpf.RoutedEventArgs e) => _refreshDeviceLocation();

    private void UnitsButton_Click(object sender, Wpf.RoutedEventArgs e)
    {
        _setUnits(!_settings.IsImperial);
        RefreshValues();
    }

    private void StartupCheckBox_Changed(object sender, Wpf.RoutedEventArgs e)
    {
        if (_updatingValues)
        {
            return;
        }

        var requestedValue = StartupCheckBox.IsChecked == true;
        if (!_setStartupEnabled(requestedValue))
        {
            RefreshValues();
        }
    }

    private void RefreshButton_Click(object sender, Wpf.RoutedEventArgs e) => _refreshWeather();
}
