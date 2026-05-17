using System.Windows.Media;

namespace SageRage.UI.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    // ── Proxy Status ──────────────────────────────────────────────
    private bool _proxyRunning;
    public bool ProxyRunning
    {
        get => _proxyRunning;
        set
        {
            Set(ref _proxyRunning, value);
            OnPropertyChanged(nameof(ProxyStatusText));
            OnPropertyChanged(nameof(ProxyStatusBrush));
        }
    }

    public string ProxyStatusText => ProxyRunning ? "RUNNING" : "STOPPED";

    public Brush ProxyStatusBrush => ProxyRunning
        ? new SolidColorBrush(Color.FromRgb(0xD6, 0xA2, 0x4A))   // gold
        : new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));  // rage-red

    private string _proxyUpstream = "Not configured";
    public string ProxyUpstream { get => _proxyUpstream; set => Set(ref _proxyUpstream, value); }

    private int _proxyPort = 9443;
    public int ProxyPort { get => _proxyPort; set => Set(ref _proxyPort, value); }

    private string _proxyUptime = "—";
    public string ProxyUptime { get => _proxyUptime; set => Set(ref _proxyUptime, value); }

    // ── Pipeline Stats ───────────────────────────────────────────
    private int _totalRequests;
    public int TotalRequests { get => _totalRequests; set => Set(ref _totalRequests, value); }

    private int _flaggedRequests;
    public int FlaggedRequests { get => _flaggedRequests; set => Set(ref _flaggedRequests, value); }

    public string FlagRate => TotalRequests == 0
        ? "0%"
        : $"{(double)FlaggedRequests / TotalRequests * 100:F1}%";

    // ── VAM State ────────────────────────────────────────────────
    private double _valence = 0.5;
    public double Valence { get => _valence; set { Set(ref _valence, value); OnPropertyChanged(nameof(VamGlyphBrush)); } }

    private double _activation = 0.3;
    public double Activation { get => _activation; set => Set(ref _activation, value); }

    private double _malice;
    public double Malice
    {
        get => _malice;
        set
        {
            Set(ref _malice, value);
            OnPropertyChanged(nameof(VamGlyphBrush));
            OnPropertyChanged(nameof(MaliceText));
        }
    }

    public string MaliceText => Malice < 0.3 ? "CLEAR" : Malice < 0.65 ? "ELEVATED" : "CRITICAL";

    public Brush VamGlyphBrush => Malice < 0.3
        ? new SolidColorBrush(Color.FromRgb(0xD6, 0xA2, 0x4A))   // gold — healthy
        : Malice < 0.65
            ? new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)) // amber — elevated
            : new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)); // rage-red — critical

    // ── Active Persona ───────────────────────────────────────────
    private string _activePersona = "None";
    public string ActivePersona { get => _activePersona; set => Set(ref _activePersona, value); }

    // ── Secrets Status ───────────────────────────────────────────
    private bool _secretsConfigured;
    public bool SecretsConfigured
    {
        get => _secretsConfigured;
        set
        {
            Set(ref _secretsConfigured, value);
            OnPropertyChanged(nameof(SecretsStatusText));
            OnPropertyChanged(nameof(SecretsStatusBrush));
        }
    }

    public string SecretsStatusText => SecretsConfigured ? "CONFIGURED" : "NOT CONFIGURED";

    public Brush SecretsStatusBrush => SecretsConfigured
        ? new SolidColorBrush(Color.FromRgb(0xD6, 0xA2, 0x4A))
        : new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C));

    private string _secretsProfile = "—";
    public string SecretsProfile { get => _secretsProfile; set => Set(ref _secretsProfile, value); }

    // ── Status Bar Feed ──────────────────────────────────────────
    public string StatusFeed => ProxyRunning
        ? $"Proxy running on :{ProxyPort}  ·  {TotalRequests} requests  ·  {FlaggedRequests} flags"
        : "Proxy stopped  ·  No active session";
}
