using Windows.Devices.Geolocation;

namespace Clim8.Windows;

public sealed record DeviceLocation(double Latitude, double Longitude);

public sealed class LocationAccessDeniedException() : InvalidOperationException("Location access is not allowed.");

public sealed class DeviceLocationService
{
    public async Task<DeviceLocation> GetCurrentLocationAsync()
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
        {
            throw new LocationAccessDeniedException();
        }

        var locator = new Geolocator { DesiredAccuracyInMeters = 100 };
        var position = await locator.GetGeopositionAsync(
            TimeSpan.FromMinutes(2),
            TimeSpan.FromSeconds(20));
        var coordinates = position.Coordinate.Point.Position;
        return new DeviceLocation(coordinates.Latitude, coordinates.Longitude);
    }
}
