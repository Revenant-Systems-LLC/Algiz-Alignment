using System.Windows;
using System.Windows.Input;
using SageRage.UI.Services;
using SageRage.UI.Views;

namespace SageRage.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetCircuitBackground();
        ContentFrame.Navigate(new DashboardView());
    }

    private void SetCircuitBackground()
    {
        CircuitLayer.Background = CircuitBackgroundService.GetRandomBrush();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;

        SetCircuitBackground();

        var page = btn.Tag?.ToString() switch
        {
            "Dashboard" => (System.Windows.Controls.Page)new DashboardView(),
            "Profiles"  => new ProfilesView(),
            "Trace"     => new TraceView(),
            "Proxy"     => new ProxyView(),
            "Settings"  => new SettingsView(),
            _           => new DashboardView(),
        };

        ContentFrame.Navigate(page);
        UpdateNavHighlight(btn.Tag?.ToString() ?? "Dashboard");
    }

    private void UpdateNavHighlight(string active)
    {
        var tabs = new[]
        {
            (NavDashboard, "Dashboard"),
            (NavProfiles,  "Profiles"),
            (NavTrace,     "Trace"),
            (NavProxy,     "Proxy"),
            (NavSettings,  "Settings"),
        };

        foreach (var (btn, tag) in tabs)
        {
            btn.Style = tag == active
                ? (System.Windows.Style)FindResource("NavTabActive")
                : (System.Windows.Style)FindResource("NavTab");
        }
    }
}
