using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Clim8.Windows;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly WeatherService _weatherService;
    private readonly Action _saveSettings;
    private readonly Action<bool> _setUnits;
    private readonly CancellationTokenSource _windowLifetime = new();
    private readonly DispatcherTimer _weatherRefreshTimer;
    private bool _refreshInProgress;

    public MainWindow(
        AppSettings settings,
        WeatherService weatherService,
        Action saveSettings,
        Action<bool> setUnits)
    {
        InitializeComponent();
        _settings = settings;
        _weatherService = weatherService;
        _saveSettings = saveSettings;
        _setUnits = setUnits;
        Icon = BitmapFrame.Create(new Uri(
            Path.Combine(AppContext.BaseDirectory, "assets", "clim8.ico"),
            UriKind.Absolute));
        Card.AddHandler(
            MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Card_MouseLeftButtonDown),
            handledEventsToo: true);
        _weatherRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _weatherRefreshTimer.Tick += WeatherRefreshTimer_Tick;
        _weatherRefreshTimer.Start();

        CityButton.Content = _settings.City;
        if (_settings.HasDeviceLocation && _settings.CachedWeather is not null)
        {
            RenderWeather(_settings.CachedWeather);
        }
        else
        {
            ShowLoadingState();
        }
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

    public void RefreshIfNeeded()
    {
        if (!_settings.HasDeviceLocation)
        {
            SetLocationStatus("Allow device location to load weather");
            return;
        }

        var cacheIsFresh = _settings.CachedWeather is not null
            && _settings.CachedWeatherAt is not null
            && DateTimeOffset.Now - _settings.CachedWeatherAt.Value < TimeSpan.FromMinutes(10);

        if (!_refreshInProgress && !cacheIsFresh)
        {
            _ = RefreshWeatherAsync();
        }
    }

    public async Task RefreshWeatherAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        if (!_settings.HasDeviceLocation)
        {
            SetLocationStatus("Allow device location to load weather");
            return;
        }

        _refreshInProgress = true;
        StatusText.Text = "Updating weather…";
        if (_settings.CachedWeather is null)
        {
            SummaryText.Text = "Updating…";
        }

        try
        {
            var weather = await _weatherService.GetWeatherAsync(
                _settings.Latitude,
                _settings.Longitude,
                _settings.IsImperial,
                _windowLifetime.Token);

            _settings.CachedWeather = weather;
            _settings.CachedWeatherAt = DateTimeOffset.Now;
            _saveSettings();
            RenderWeather(weather);
        }
        catch (WeatherConfigurationException ex)
        {
            if (_settings.CachedWeather is null)
            {
                SummaryText.Text = "Weather setup needed";
            }
            StatusText.Text = ex.Message;
        }
        catch (HttpRequestException)
        {
            ShowRefreshError();
        }
        catch (TaskCanceledException)
        {
            ShowRefreshError();
        }
        catch (InvalidOperationException)
        {
            ShowRefreshError();
        }
        catch (Exception)
        {
            ShowRefreshError();
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    public void RefreshForUnitChange()
    {
        if (_settings.CachedWeather is not null)
        {
            RenderWeather(_settings.CachedWeather);
        }

        _ = RefreshWeatherAsync();
    }

    public void SetLocationStatus(string text) => StatusText.Text = text;

    private async void CityButton_Click(object sender, RoutedEventArgs e) => await RefreshWeatherAsync();

    private void TemperatureButton_Click(object sender, RoutedEventArgs e) =>
        _setUnits(!_settings.IsImperial);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _weatherRefreshTimer.Stop();
        _weatherRefreshTimer.Tick -= WeatherRefreshTimer_Tick;
        _windowLifetime.Cancel();
        _windowLifetime.Dispose();
        base.OnClosed(e);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
        e.Handled = true;
    }

    private static bool IsInteractiveElement(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase)
            {
                return true;
            }

            element = element switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(element),
                FrameworkContentElement content => content.Parent,
                _ => LogicalTreeHelper.GetParent(element)
            };
        }

        return false;
    }

    private async void WeatherRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            await RefreshWeatherAsync();
        }
    }

    private void ShowLoadingState()
    {
        CityButton.Content = _settings.City;
        DateTimeText.Text = "—";
        TemperatureText.Text = "—°";
        TemperatureUnitText.Text = _settings.IsImperial ? "F" : "C";
        HeaderIcon.IconName = "cloudy";
        SummaryText.Text = _settings.HasDeviceLocation ? "Loading weather…" : "Getting your device location…";
        HourlyPanel.Children.Clear();
        DailyPanel.Children.Clear();
        StatusText.Text = _settings.HasDeviceLocation
            ? "Google Weather"
            : "Location permission is needed";
    }

    private void ShowRefreshError()
    {
        if (_settings.CachedWeather is null)
        {
            SummaryText.Text = "Weather is unavailable";
            StatusText.Text = "Check your connection and refresh";
            return;
        }

        var updated = _settings.CachedWeatherAt?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        StatusText.Text = updated is null ? "Offline · saved forecast" : $"Offline · updated {updated}";
    }

    private void RenderWeather(WeatherSnapshot weather)
    {
        CityButton.Content = _settings.City;
        DateTimeText.Text = weather.Current.Time.ToString("ddd · HH:mm", CultureInfo.CurrentCulture);
        TemperatureText.Text = WeatherPresentation.FormatTemperature(
            ConvertTemperature(weather.Current.Temperature, weather.IsImperial));
        TemperatureUnitText.Text = _settings.IsImperial ? "F" : "C";

        var description = WeatherPresentation.Describe(
            weather.Current.WeatherType,
            weather.Current.IsDaytime,
            weather.Current.Description);
        HeaderIcon.IconName = description.IconName;
        var high = WeatherPresentation.FormatTemperature(
            ConvertTemperature(weather.Today.High, weather.IsImperial));
        var low = WeatherPresentation.FormatTemperature(
            ConvertTemperature(weather.Today.Low, weather.IsImperial));
        SummaryText.Text = $"{description.Description} · H: {high}  L: {low}";

        RenderHours(weather.Hours, weather.IsImperial);
        RenderDays(weather.Days, weather.IsImperial);

        var updated = _settings.CachedWeatherAt?.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        StatusText.Text = updated is null ? "Google Weather" : $"Google Weather · {updated}";
    }

    private void RenderHours(IReadOnlyList<HourlyForecast> hours, bool cachedAsImperial)
    {
        HourlyPanel.Children.Clear();
        foreach (var (hour, index) in hours.Take(6).Select((hour, index) => (hour, index)))
        {
            var description = WeatherPresentation.Describe(hour.WeatherType, hour.IsDaytime);
            var column = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            column.Children.Add(new TextBlock
            {
                Text = index == 0 ? "Now" : WeatherPresentation.FormatHour(hour.Time),
                FontSize = 8,
                Foreground = (Brush)Resources["SecondaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center
            });
            column.Children.Add(new WeatherIconImage
            {
                IconName = description.IconName,
                Width = 26,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            column.Children.Add(new TextBlock
            {
                Text = WeatherPresentation.FormatTemperature(ConvertTemperature(hour.Temperature, cachedAsImperial)),
                FontSize = 9,
                Foreground = (Brush)Resources["PrimaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center
            });
            HourlyPanel.Children.Add(column);
        }
    }

    private void RenderDays(IReadOnlyList<DailyForecast> days, bool cachedAsImperial)
    {
        DailyPanel.Children.Clear();
        foreach (var day in days.Take(4))
        {
            var row = new Grid { Height = 22 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dayName = new TextBlock
            {
                Text = day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd", CultureInfo.CurrentCulture),
                FontSize = 10,
                Foreground = (Brush)Resources["SecondaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dayName, 0);
            row.Children.Add(dayName);

            var weather = WeatherPresentation.Describe(day.WeatherType, true);
            var icon = new WeatherIconImage
            {
                IconName = weather.IconName,
                Width = 24,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            row.Children.Add(icon);

            var temperatures = new TextBlock
            {
                Text = $"{WeatherPresentation.FormatTemperature(ConvertTemperature(day.Low, cachedAsImperial))}  {WeatherPresentation.FormatTemperature(ConvertTemperature(day.High, cachedAsImperial))}",
                FontSize = 9,
                Foreground = (Brush)Resources["SecondaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(temperatures, 2);
            row.Children.Add(temperatures);

            DailyPanel.Children.Add(row);
        }
    }

    private double ConvertTemperature(double value, bool storedAsImperial)
    {
        if (storedAsImperial == _settings.IsImperial)
        {
            return value;
        }

        return _settings.IsImperial ? value * 9 / 5 + 32 : (value - 32) * 5 / 9;
    }
}
