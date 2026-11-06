using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clim8.Windows;

public sealed class WeatherIconImage : FrameworkElement
{
    private static readonly ConcurrentDictionary<string, BitmapSource> ImageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static readonly DependencyProperty IconNameProperty =
        DependencyProperty.Register(
            nameof(IconName),
            typeof(string),
            typeof(WeatherIconImage),
            new FrameworkPropertyMetadata("cloudy", FrameworkPropertyMetadataOptions.AffectsRender));

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var bitmap = ImageCache.GetOrAdd(IconName, LoadIcon);
        var ratio = Math.Min(ActualWidth / bitmap.PixelWidth, ActualHeight / bitmap.PixelHeight);
        var width = bitmap.PixelWidth * ratio;
        var height = bitmap.PixelHeight * ratio;
        drawingContext.DrawImage(
            bitmap,
            new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
    }

    private static BitmapSource LoadIcon(string iconName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "weather", $"{iconName}.png");
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "assets", "weather", "cloudy.png");
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
