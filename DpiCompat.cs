using System.Runtime.InteropServices;

namespace Clim8.Windows;

internal static class DpiCompat
{
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForSystem();

    public static double ToDip(double pixels)
    {
        var dpi = GetDpiForSystem();
        return dpi == 0 ? pixels : pixels * 96d / dpi;
    }
}

