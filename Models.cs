namespace Clim8.Windows;

public sealed class AppSettings
{
    public string City { get; set; } = "Current location";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool HasDeviceLocation { get; set; }
    public bool IsImperial { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public WeatherSnapshot? CachedWeather { get; set; }
    public DateTimeOffset? CachedWeatherAt { get; set; }
}

public sealed class WeatherSnapshot
{
    public bool IsImperial { get; set; }
    public CurrentConditions Current { get; set; } = new();
    public DailyForecast Today { get; set; } = new();
    public List<HourlyForecast> Hours { get; set; } = [];
    public List<DailyForecast> Days { get; set; } = [];
}

public sealed class CurrentConditions
{
    public DateTimeOffset Time { get; set; }
    public double Temperature { get; set; }
    public string WeatherType { get; set; } = "CLOUDY";
    public string Description { get; set; } = "Weather unavailable";
    public bool IsDaytime { get; set; }
}

public sealed class HourlyForecast
{
    public DateTimeOffset Time { get; set; }
    public double Temperature { get; set; }
    public string WeatherType { get; set; } = "CLOUDY";
    public bool IsDaytime { get; set; }
}

public sealed class DailyForecast
{
    public DateOnly Date { get; set; }
    public string WeatherType { get; set; } = "CLOUDY";
    public double High { get; set; }
    public double Low { get; set; }
}
