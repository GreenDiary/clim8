using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Clim8.Windows;

public partial class TrayMenuWindow : Window
{
    private readonly Action _openWeather;
    private readonly Action _openSettings;
    private readonly Action _quit;

    public TrayMenuWindow(Action openWeather, Action openSettings, Action quit)
    {
        InitializeComponent();
        _openWeather = openWeather;
        _openSettings = openSettings;
        _quit = quit;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() =>
            {
                if (IsVisible)
                {
                    Close();
                }
            }));
    }

    public void ShowNear(System.Drawing.Point cursorPosition)
    {
        var area = WinForms.Screen.FromPoint(cursorPosition).WorkingArea;
        var padding = DpiCompat.ToDip(6);
        var minLeft = DpiCompat.ToDip(area.Left) + padding;
        var minTop = DpiCompat.ToDip(area.Top) + padding;
        var maxLeft = Math.Max(minLeft, DpiCompat.ToDip(area.Right) - Width - padding);
        var maxTop = Math.Max(minTop, DpiCompat.ToDip(area.Bottom) - Height - padding);
        var left = Math.Clamp(DpiCompat.ToDip(cursorPosition.X) - Width - padding, minLeft, maxLeft);
        var top = Math.Clamp(DpiCompat.ToDip(cursorPosition.Y) - Height - padding, minTop, maxTop);

        Left = left;
        Top = top;
        if (!IsVisible)
        {
            Show();
        }

        Activate();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) => RunAction(_openWeather);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => RunAction(_openSettings);

    private void QuitButton_Click(object sender, RoutedEventArgs e) => RunAction(_quit);

    private void RunAction(Action action)
    {
        Close();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, action);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

