using System.Globalization;
using System.Text.Json;

namespace Clim8.Windows;

public sealed class WeatherService
{
    private static readonly HttpClient Client = CreateClient();

    public async Task<WeatherSnapshot> GetWeatherAsync(
        double latitude,
        double longitude,
        bool isImperial,
        CancellationToken cancellationToken = default)
    {
        var key = Environment.GetEnvironmentVariable("CLIM8_GOOGLE_WEATHER_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new WeatherConfigurationException(
                "Set the CLIM8_GOOGLE_WEATHER_API_KEY environment variable and restart Clim8.");
        }

        var lat = latitude.ToString("R", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("R", CultureInfo.InvariantCulture);
        var units = isImperial ? "IMPERIAL" : "METRIC";
        var sharedQuery = $"location.latitude={lat}&location.longitude={lon}&unitsSystem={units}&languageCode=en";

        var currentTask = GetJsonAsync(
            $"https://weather.googleapis.com/v1/currentConditions:lookup?{sharedQuery}", key, cancellationToken);
        var hoursTask = GetJsonAsync(
            $"https://weather.googleapis.com/v1/forecast/hours:lookup?{sharedQuery}&hours=6&pageSize=6", key, cancellationToken);
        var daysTask = GetJsonAsync(
            $"https://weather.googleapis.com/v1/forecast/days:lookup?{sharedQuery}&days=5&pageSize=5", key, cancellationToken);

        await Task.WhenAll(currentTask, hoursTask, daysTask);
        using var currentDocument = await currentTask;
        using var hoursDocument = await hoursTask;
        using var daysDocument = await daysTask;

        var currentRoot = currentDocument.RootElement;
        var hoursRoot = hoursDocument.RootElement;
        var daysRoot = daysDocument.RootElement;
        var timeZone = GetLocationTimeZone(currentRoot);
        var currentTime = ConvertToLocationTime(
            GetTimestamp(currentRoot, "currentTime", DateTimeOffset.Now),
            timeZone);
        var currentCondition = GetCondition(currentRoot);
        var current = new CurrentConditions
        {
            Time = currentTime,
            Temperature = GetDegrees(currentRoot, "temperature"),
            WeatherType = currentCondition.Type,
            Description = currentCondition.Description,
            IsDaytime = GetBoolean(currentRoot, "isDaytime")
        };

        var hours = GetArray(hoursRoot, "forecastHours")
            .Take(6)
            .Select(hour =>
            {
                var condition = GetCondition(hour);
                return new HourlyForecast
                {
                    Time = ConvertToLocationTime(
                        GetTimestamp(GetObject(hour, "interval"), "startTime", currentTime),
                        timeZone),
                    Temperature = GetDegrees(hour, "temperature"),
                    WeatherType = condition.Type,
                    IsDaytime = GetBoolean(hour, "isDaytime")
                };
            })
            .ToList();

        var days = GetArray(daysRoot, "forecastDays")
            .Take(5)
            .Select(day =>
            {
                var daytime = GetObject(day, "daytimeForecast");
                var nighttime = GetObject(day, "nighttimeForecast");
                var fullDay = GetObject(day, "fullDayForecast");
                var conditionSource = !daytime.ValueKind.Equals(JsonValueKind.Undefined)
                    ? daytime
                    : !fullDay.ValueKind.Equals(JsonValueKind.Undefined) ? fullDay : nighttime;

                return new DailyForecast
                {
                    Date = GetDate(day, currentTime),
                    WeatherType = GetCondition(conditionSource).Type,
                    High = GetDegrees(day, "maxTemperature", current.Temperature),
                    Low = GetDegrees(day, "minTemperature", current.Temperature)
                };
            })
            .ToList();

        var today = days.Count > 0
            ? days[0]
            : new DailyForecast { Date = DateOnly.FromDateTime(currentTime.LocalDateTime), High = current.Temperature, Low = current.Temperature };

        return new WeatherSnapshot
        {
            IsImperial = isImperial,
            Current = current,
            Today = today,
            Hours = hours,
            Days = days.Skip(1).Take(4).ToList()
        };
    }

    private static async Task<JsonDocument> GetJsonAsync(
        string uri,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        using var response = await Client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static JsonElement GetObject(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
                ? value
                : default;

    private static IEnumerable<JsonElement> GetArray(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().ToArray()
                : [];

    private static double GetDegrees(JsonElement source, string name, double fallback = 0)
    {
        var value = GetObject(source, name);
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("degrees", out var degrees)
            && degrees.TryGetDouble(out var result)
                ? result
                : fallback;
    }

    private static int GetInt(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var result) ? result : 0;

    private static bool GetBoolean(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static string GetString(JsonElement source, string name, string fallback = "") =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static DateTimeOffset GetTimestamp(JsonElement source, string name, DateTimeOffset fallback)
    {
        var text = GetString(source, name);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : fallback;
    }

    private static DateOnly GetDate(JsonElement source, DateTimeOffset fallback)
    {
        var date = GetObject(source, "displayDate");
        var year = GetInt(date, "year");
        var month = GetInt(date, "month");
        var day = GetInt(date, "day");
        return year > 0 && month is >= 1 and <= 12 && day is >= 1 and <= 31
            ? new DateOnly(year, month, day)
            : DateOnly.FromDateTime(fallback.LocalDateTime);
    }

    private static (string Type, string Description) GetCondition(JsonElement source)
    {
        var condition = GetObject(source, "weatherCondition");
        var description = GetObject(condition, "description");
        return (
            GetString(condition, "type", "CLOUDY"),
            GetString(description, "text", "Weather unavailable"));
    }

    private static TimeZoneInfo? GetLocationTimeZone(JsonElement source)
    {
        var id = GetString(GetObject(source, "timeZone"), "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static DateTimeOffset ConvertToLocationTime(DateTimeOffset time, TimeZoneInfo? timeZone) =>
        timeZone is null ? time.ToLocalTime() : TimeZoneInfo.ConvertTime(time, timeZone);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(18) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Clim8-Windows/1.0");
        return client;
    }
}

public sealed class WeatherConfigurationException(string message) : InvalidOperationException(message);
