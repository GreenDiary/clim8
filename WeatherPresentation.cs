namespace Clim8.Windows;

public sealed record WeatherDescription(string Description, string IconName);

public static class WeatherPresentation
{
    public static WeatherDescription Describe(string weatherType, bool isDaytime, string? description = null)
    {
        var type = weatherType.Trim().ToUpperInvariant();
        var dayNight = isDaytime ? "day" : "night";
        var icon = type switch
        {
            "CLEAR" => $"clear-{dayNight}",
            "MOSTLY_CLEAR" => $"cloudy-1-{dayNight}",
            "PARTLY_CLOUDY" => $"cloudy-1-{dayNight}",
            "MOSTLY_CLOUDY" => $"cloudy-2-{dayNight}",
            "CLOUDY" or "OVERCAST" => "cloudy",
            "FOG" or "FREEZING_FOG" => "fog",
            "HAZE" or "SMOKE" or "SMOG" => $"haze-{dayNight}",
            "DRIZZLE" or "LIGHT_RAIN" => "rainy-1",
            "RAIN" or "RAIN_SHOWERS" or "SCATTERED_SHOWERS" or "LIGHT_RAIN_SHOWERS" => "rainy-2",
            "HEAVY_RAIN" or "HEAVY_RAIN_SHOWERS" => "rainy-3",
            "WINDY" => "wind",
            "WIND_AND_RAIN" => "rainy-2",
            "LIGHT_SNOW" or "FLURRIES" => "snowy-1",
            "SNOW" or "SNOW_SHOWERS" => "snowy-2",
            "HEAVY_SNOW" or "HEAVY_SNOW_SHOWERS" => "snowy-3",
            "SLEET" or "FREEZING_RAIN" or "LIGHT_FREEZING_RAIN" => "rain-and-sleet-mix",
            "ICE_PELLETS" or "HAIL" => "hail",
            "LIGHT_THUNDERSTORM" or "THUNDERSTORM" or "THUNDERSHOWER" => $"isolated-thunderstorms-{dayNight}",
            "SCATTERED_THUNDERSTORMS" => $"scattered-thunderstorms-{dayNight}",
            "HEAVY_THUNDERSTORM" or "SEVERE_THUNDERSTORM" => "severe-thunderstorm",
            "RAIN_AND_SNOW" => "rain-and-snow-mix",
            "DUST" or "SAND" => "dust",
            "FROST" or "FREEZING_DRIZZLE" => "frost",
            "TROPICAL_STORM" => "tropical-storm",
            "HURRICANE" => "hurricane",
            "TORNADO" => "tornado",
            _ => "cloudy"
        };

        var fallbackDescription = type.Replace('_', ' ').ToLowerInvariant();
        if (fallbackDescription.Length > 0)
        {
            fallbackDescription = char.ToUpperInvariant(fallbackDescription[0]) + fallbackDescription[1..];
        }

        return new WeatherDescription(
            string.IsNullOrWhiteSpace(description) || description == "Weather unavailable"
                ? fallbackDescription
                : description,
            icon);
    }

    public static string FormatTemperature(double temperature) =>
        $"{Math.Round(temperature, MidpointRounding.AwayFromZero):0}°";

    public static string FormatHour(DateTimeOffset time) =>
        time.ToString("htt", System.Globalization.CultureInfo.CurrentCulture);
}
