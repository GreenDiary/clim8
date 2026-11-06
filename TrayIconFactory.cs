using System.Drawing;

namespace Clim8.Windows;

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "clim8.ico");
        return new Icon(iconPath);
    }
}
