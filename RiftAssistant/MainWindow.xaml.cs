using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RiftAssistant.Core;
using RiftAssistant.Models;
using RiftAssistant.Services;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

namespace RiftAssistant
{
    public partial class MainWindow : Window
    {
        private readonly LockfileService _lockfileService;
        private readonly LcuClient _lcuClient;
        private readonly ChampSelectService _champSelectService;
        private readonly ChampionService _championService;
        private List<Champion> _champions = new();

        private bool _isConnected;
        private bool _connectionLoopStarted;
        private CancellationTokenSource? _connectionLoopCancellation;
        private bool _autoAcceptEnabled = true;

        // ReadyCheck kontrolü
        private CancellationTokenSource? _readyCheckCancellation;
        private bool _readyCheckHandled;
        private bool _autoBanEnabled = false;
        private bool _autoPickEnabled = false;
        private bool _autoPickRunning = false;
        private long? _autoPickHandledActionId;
        private bool _autoHoverRunning = false;
        private long? _lastAutoHoverActionId;
        private int _lastAutoHoverChampionId;
        private long? _autoBanHandledActionId;
        private readonly SettingsService _settingsService;
        private bool _autoBanRunning;
        private AppSettings _settings = new();
        private bool _isLoadingSettings;
        private bool _uiReady = false;
        private readonly BanDecisionService _banDecisionService = new();
        private string _stableTimerPhase = string.Empty;
        private long _stableTimerActionId = -1;

        private double _stableRemainingSeconds = 0;
        private long _stableTimerLastTick;
        private enum LcuBadgeState
        {
            Waiting,
            Connecting,
            Connected,
            Disconnected
        }

        private void SetLcuBadge(
            LcuBadgeState state)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    () => SetLcuBadge(state)
                );

                return;
            }

            string text;
            string background;
            string border;
            string dot;
            string foreground;

            switch (state)
            {
                case LcuBadgeState.Connected:
                    text = "Bağlı";
                    background = "#18251B";
                    border = "#315537";
                    dot = "#55D66B";
                    foreground = "#A9E8B3";
                    break;

                case LcuBadgeState.Connecting:
                    text = "Bağlanıyor";
                    background = "#2B2417";
                    border = "#5B4B24";
                    dot = "#C89B3C";
                    foreground = "#E6C56A";
                    break;

                case LcuBadgeState.Disconnected:
                    text = "Bağlantı yok";
                    background = "#2B1919";
                    border = "#603333";
                    dot = "#E05A5A";
                    foreground = "#F0A4A4";
                    break;

                default:
                    text = "Bekleniyor";
                    background = "#222831";
                    border = "#3A4654";
                    dot = "#8B98A8";
                    foreground = "#C7D0DA";
                    break;
            }

            LcuStatusBadgeText.Text =
                text;

            LcuStatusBadge.Background =
                (Brush)new BrushConverter()
                    .ConvertFromString(background)!;

            LcuStatusBadge.BorderBrush =
                (Brush)new BrushConverter()
                    .ConvertFromString(border)!;

            LcuStatusDot.Fill =
                (Brush)new BrushConverter()
                    .ConvertFromString(dot)!;

            LcuStatusBadgeText.Foreground =
                (Brush)new BrushConverter()
                    .ConvertFromString(foreground)!;
        }

        private static string GetDisplayVersion()
        {
            string version =
                typeof(MainWindow).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion
                ?? "0.9.0-beta.1";

            // .NET/Git bazı build'lerde +commitHash ekleyebilir.
            int metadataIndex = version.IndexOf('+');

            if (metadataIndex >= 0)
            {
                version =
                    version[..metadataIndex];
            }

            return $"v{version}";
        }

        private double GetStableRemainingSeconds(
    string phase,
    long actionId,
    double rawRemainingSeconds)
        {
            long now = Stopwatch.GetTimestamp();

            bool newTimer =
                _stableTimerLastTick == 0 ||
                !string.Equals(
                    _stableTimerPhase,
                    phase,
                    StringComparison.OrdinalIgnoreCase) ||
                _stableTimerActionId != actionId;

            if (newTimer)
            {
                _stableTimerPhase = phase;
                _stableTimerActionId = actionId;

                _stableRemainingSeconds =
                    Math.Max(0, rawRemainingSeconds);

                _stableTimerLastTick = now;

                return _stableRemainingSeconds;
            }

            double elapsedSeconds =
                (now - _stableTimerLastTick) /
                (double)Stopwatch.Frequency;

            _stableTimerLastTick = now;

            // Bilgisayar saatine göre sürekli azalt.
            _stableRemainingSeconds =
                Math.Max(
                    0,
                    _stableRemainingSeconds - elapsedSeconds
                );

            // LCU daha düşük ve güncel bir değer verdiyse onu kabul et.
            // Ama LCU aynı eski değeri tekrar gönderirse timer'ı geriye
            // doğru yükseltmiyoruz.
            if (rawRemainingSeconds >= 0 &&
                rawRemainingSeconds < _stableRemainingSeconds)
            {
                _stableRemainingSeconds =
                    rawRemainingSeconds;
            }

            return _stableRemainingSeconds;
        }

        private void ResetStableTimer()
        {
            _stableTimerPhase = string.Empty;
            _stableTimerActionId = -1;
            _stableRemainingSeconds = 0;
            _stableTimerLastTick = 0;
        }

        private void ResetAutoHoverState()
        {
            _autoHoverRunning = false;
            _lastAutoHoverActionId = null;
            _lastAutoHoverChampionId = 0;
        }

        private void ResetAutoPickState()
        {
            _autoPickRunning = false;
            _autoPickHandledActionId = null;
        }

        private int GetAutoBanDelaySeconds()
        {
            if (AutoBanDelayComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Content?.ToString(), out int seconds))
            {
                return seconds;
            }

            return 5;
        }
        private int GetAutoPickDelaySeconds()
        {
            if (AutoPickDelayComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Content?.ToString(), out int seconds))
            {
                return seconds;
            }

            return 5;
        }

        // Daha sonra ayarlar ekranından değiştireceğiz.
        private void LoadSettings()
        {
            _isLoadingSettings = true;

            try
            {
                _settings = _settingsService.Load();

                // AUTO ACCEPT
                _autoAcceptEnabled = _settings.AutoAcceptEnabled;
                AutoAcceptCheckBox.IsChecked = _settings.AutoAcceptEnabled;

                AutoAcceptStatusText.Text =
                    _settings.AutoAcceptEnabled
                        ? "Auto Accept hazır."
                        : "Auto Accept kapalı.";

                int acceptDelay =
                    Math.Clamp(
                        _settings.AutoAcceptDelaySeconds,
                        0,
                        9
                    );

                AutoAcceptDelayComboBox.SelectedIndex =
                    acceptDelay;

                // AUTO BAN
                _autoBanEnabled = _settings.AutoBanEnabled;
                AutoBanCheckBox.IsChecked = _settings.AutoBanEnabled;

                int banDelay =
                    Math.Clamp(
                        _settings.AutoBanAtSeconds,
                        1,
                        10
                    );

                AutoBanDelayComboBox.SelectedIndex =
                    banDelay - 1;

                // AUTO PICK
                _autoPickEnabled = _settings.AutoPickEnabled;
                AutoPickCheckBox.IsChecked = _settings.AutoPickEnabled;

                AutoHoverPickCheckBox.IsChecked =
                    _settings.AutoHoverPickEnabled;
int pickDelay =
                    Math.Clamp(
                        _settings.AutoPickAtSeconds,
                        1,
                        10
                    );

                AutoPickDelayComboBox.SelectedIndex =
                    pickDelay - 1;

                // TOP MOST
                TopMostCheckBox.IsChecked = _settings.TopMost;
                Topmost = _settings.TopMost;
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private double GetHoverFallbackAtSeconds()
        {
            int autoBanAtSeconds =
                Math.Clamp(
                    _settings.AutoBanAtSeconds,
                    1,
                    10
                );

            return autoBanAtSeconds / 2.0;
        }
        private void SaveSettings()
        {
            if (!_uiReady || _isLoadingSettings)
                return;

            if (PrimaryBanChampionComboBox == null ||
                SecondaryBanChampionComboBox == null ||
                TertiaryBanChampionComboBox == null ||
                PrimaryPickChampionComboBox == null ||
                SecondaryPickChampionComboBox == null ||
                TertiaryPickChampionComboBox == null ||
                AutoAcceptDelayComboBox == null ||
                AutoBanDelayComboBox == null ||
                AutoPickDelayComboBox == null)
            {
                return;
            }

            // AUTO ACCEPT
            _settings.AutoAcceptEnabled =
                _autoAcceptEnabled;

            if (AutoAcceptDelayComboBox.SelectedItem
                is ComboBoxItem acceptDelayItem &&
                int.TryParse(
                    acceptDelayItem.Content?.ToString(),
                    out int acceptDelay))
            {
                _settings.AutoAcceptDelaySeconds =
                    acceptDelay;
            }

            // AUTO BAN
            _settings.AutoBanEnabled =
                _autoBanEnabled;

            if (PrimaryBanChampionComboBox.SelectedValue is int primaryBanId)
                _settings.PrimaryBanChampionId = primaryBanId;

            if (SecondaryBanChampionComboBox.SelectedValue is int secondaryBanId)
                _settings.SecondaryBanChampionId = secondaryBanId;

            if (TertiaryBanChampionComboBox.SelectedValue is int tertiaryBanId)
                _settings.TertiaryBanChampionId = tertiaryBanId;

            if (AutoBanDelayComboBox.SelectedItem
                is ComboBoxItem banDelayItem &&
                int.TryParse(
                    banDelayItem.Content?.ToString(),
                    out int banDelay))
            {
                _settings.AutoBanAtSeconds =
                    banDelay;
            }

            // AUTO PICK
            _settings.AutoPickEnabled =
                _autoPickEnabled;

            if (PrimaryPickChampionComboBox.SelectedValue is int primaryPickId)
                _settings.PrimaryPickChampionId = primaryPickId;

            if (SecondaryPickChampionComboBox.SelectedValue is int secondaryPickId)
                _settings.SecondaryPickChampionId = secondaryPickId;

            if (TertiaryPickChampionComboBox.SelectedValue is int tertiaryPickId)
                _settings.TertiaryPickChampionId = tertiaryPickId;

            if (AutoPickDelayComboBox.SelectedItem
                is ComboBoxItem pickDelayItem &&
                int.TryParse(
                    pickDelayItem.Content?.ToString(),
                    out int pickDelay))
            {
                _settings.AutoPickAtSeconds =
                    pickDelay;
            }

            _settings.AutoHoverPickEnabled =
                AutoHoverPickCheckBox.IsChecked == true;
// APPLICATION
            _settings.TopMost =
                Topmost;

            _settingsService.Save(_settings);
        }

        private void AutoBanDelayComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e)
        {
            SaveSettings();
        }
        private void AutoAcceptCheckBox_Checked(
    object sender,
    RoutedEventArgs e)
        {
            _autoAcceptEnabled = true;

            if (_uiReady)
                AutoAcceptStatusText.Text = "Auto Accept hazır.";

            SaveSettings();
        }

        private void AutoAcceptCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            _autoAcceptEnabled = false;

            CancelAutoAccept();

            if (_uiReady)
                AutoAcceptStatusText.Text = "Auto Accept kapalı.";

            SaveSettings();
        }
        private void AutoBanCheckBox_Checked(
    object sender,
    RoutedEventArgs e)
        {
            _autoBanEnabled = true;

            SaveSettings();
        }

        private void AutoBanCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            _autoBanEnabled = false;

            SaveSettings();
        }
        private void TopMostCheckBox_Checked(
    object sender,
    RoutedEventArgs e)
        {
            Topmost = true;

            SaveSettings();
        }

        private void TopMostCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            Topmost = false;

            SaveSettings();
        }
        private void AutoAcceptDelayComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private async void CheckUpdatesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;

            string originalText =
                CheckUpdatesButton.Content?.ToString()
                ?? "Güncellemeleri kontrol et";

            CheckUpdatesButton.Content =
                "Kontrol ediliyor...";

            try
            {
                await UpdateService.CheckForUpdatesAsync(
                    showStatusMessages: true
                );
            }
            finally
            {
                CheckUpdatesButton.Content =
                    originalText;

                CheckUpdatesButton.IsEnabled =
                    true;
            }
        }

        private void AboutButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string version =
                GetDisplayVersion();

            MessageBox.Show(
                $"RiftAssistant\n\n" +
                $"Sürüm: {version}\n" +
                $"Developed by Kazım Karakaya with AI\n\n" +
                "Özellikler:\n" +
                "• Auto Accept\n" +
                "• Auto Ban\n" +
                "• Auto Hover\n" +
                "• Auto Pick\n" +
                "• Otomatik Güncelleme\n\n" +
                "GitHub: KazimKarakaya/RiftAssistant",
                "RiftAssistant Hakkında",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public MainWindow()
        {
            _settingsService = new SettingsService();

            // XAML oluşturulurken Checked/TextChanged/SelectionChanged eventleri
            // tetiklenebilir. Bu sırada ayar kaydetmeye çalışma.
            _isLoadingSettings = true;
            _uiReady = false;

            InitializeComponent();

            SetLcuBadge(
                LcuBadgeState.Waiting
            );

            VersionSignatureText.Text =
                $"Developed by Kazım Karakaya with AI • {GetDisplayVersion()}";

            _lockfileService = new LockfileService();
            _lcuClient = new LcuClient();

            _champSelectService =
                new ChampSelectService(_lcuClient);
            _championService =
                new ChampionService(_lcuClient);

            LoadSettings();
            _uiReady = true;
        }
      
        private async Task LoadChampionsAsync()
        {
            try
            {
                _champions =
                    await _championService.GetChampionsAsync();

                bool oldLoadingState =
                    _isLoadingSettings;

                _isLoadingSettings = true;

                try
                {
                    // AUTO BAN
                    PrimaryBanChampionComboBox.ItemsSource = _champions;
                    SecondaryBanChampionComboBox.ItemsSource = _champions;
                    TertiaryBanChampionComboBox.ItemsSource = _champions;

                    // AUTO PICK
                    PrimaryPickChampionComboBox.ItemsSource = _champions;
                    SecondaryPickChampionComboBox.ItemsSource = _champions;
                    TertiaryPickChampionComboBox.ItemsSource = _champions;

                    // Kayıtlı ban tercihleri
                    if (_settings.PrimaryBanChampionId > 0)
                        PrimaryBanChampionComboBox.SelectedValue =
                            _settings.PrimaryBanChampionId;

                    if (_settings.SecondaryBanChampionId > 0)
                        SecondaryBanChampionComboBox.SelectedValue =
                            _settings.SecondaryBanChampionId;

                    if (_settings.TertiaryBanChampionId > 0)
                        TertiaryBanChampionComboBox.SelectedValue =
                            _settings.TertiaryBanChampionId;

                    // Kayıtlı pick tercihleri
                    if (_settings.PrimaryPickChampionId > 0)
                        PrimaryPickChampionComboBox.SelectedValue =
                            _settings.PrimaryPickChampionId;

                    if (_settings.SecondaryPickChampionId > 0)
                        SecondaryPickChampionComboBox.SelectedValue =
                            _settings.SecondaryPickChampionId;

                    if (_settings.TertiaryPickChampionId > 0)
                        TertiaryPickChampionComboBox.SelectedValue =
                            _settings.TertiaryPickChampionId;
                }
                finally
                {
                    _isLoadingSettings =
                        oldLoadingState;
                }

                AutoBanStatusText.Text =
                    $"{_champions.Count} şampiyon yüklendi.";

                AutoPickStatusText.Text =
                    $"{_champions.Count} şampiyon yüklendi.";
            }
            catch (Exception ex)
            {
                AutoBanStatusText.Text =
                    $"Şampiyon listesi alınamadı: {ex.Message}";

                AutoPickStatusText.Text =
                    $"Şampiyon listesi alınamadı: {ex.Message}";
            }
        }

        private void BanChampionComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_uiReady || _isLoadingSettings)
                return;

            SaveSettings();
        }

        private void PickChampionComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!_uiReady || _isLoadingSettings)
                return;

            SaveSettings();
        }

        private void AutoPickCheckBox_Checked(
            object sender,
            RoutedEventArgs e)
        {
            _autoPickEnabled = true;
            SaveSettings();
        }

        private void AutoPickCheckBox_Unchecked(
            object sender,
            RoutedEventArgs e)
        {
            _autoPickEnabled = false;
            SaveSettings();
        }

        private void AutoPickDelayComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            SaveSettings();
        }

        private void AutoHoverPickCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            SaveSettings();
        }

        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            WriteDebugLog("=== RiftAssistant açıldı ===");

            if (_connectionLoopStarted)
                return;

            _connectionLoopStarted = true;
            _connectionLoopCancellation =
                new CancellationTokenSource();

            Closed += (_, _) =>
            {
                _connectionLoopCancellation?.Cancel();
                _connectionLoopCancellation?.Dispose();
                _connectionLoopCancellation = null;
            };

            await MaintainLeagueConnectionAsync(
                _connectionLoopCancellation.Token
            );
        }

        private async Task MaintainLeagueConnectionAsync(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectToLeagueAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _isConnected = false;

                    SetLcuBadge(
                        LcuBadgeState.Disconnected
                    );

                    StatusText.Text =
                        $"LCU yeniden bağlanma hatası: {ex.Message}";

                    WriteDebugLog(
                        $"[LCU RECONNECT] HATA | {ex}"
                    );
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(2),
                        cancellationToken
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ConnectToLeagueAsync()
        {
            try
            {
                StatusText.Text =
                    "League Client aranıyor...";

                string lockfilePath =
                    @"C:\Riot Games\League of Legends\lockfile";

                var lockfile =
                    _lockfileService.Read(lockfilePath);

                SetLcuBadge(
                    LcuBadgeState.Connecting
                );

                StatusText.Text =
                    "League Client bulundu, bağlanılıyor...";

                _lcuClient.Connect(
                    lockfile.Port,
                    lockfile.Password,
                    lockfile.Protocol
                );
                await LoadChampionsAsync();
                _isConnected = true;

                SetLcuBadge(
                    LcuBadgeState.Connected
                );

                StatusText.Text = "LCU Bağlandı ✓";

                await MonitorGameflowAsync();
            }
            catch (FileNotFoundException)
            {
                _isConnected = false;

                SetLcuBadge(
                    LcuBadgeState.Waiting
                );

                StatusText.Text =
                    "League Client bekleniyor...";

                WriteDebugLog(
                    "[LCU RECONNECT] League Client bekleniyor."
                );
            }
            catch (Exception ex)
            {
                _isConnected = false;

                SetLcuBadge(
                    LcuBadgeState.Disconnected
                );

                StatusText.Text =
                    $"Bağlantı hatası: {ex.Message}";

                WriteDebugLog(
                    $"[LCU RECONNECT] Bağlantı başarısız | {ex.Message}"
                );
            }
        }

        private async Task MonitorGameflowAsync()
        {
            while (_isConnected)
            {
                try
                {
                    string phase = await GetCurrentPhaseAsync();

                    if (phase == "ReadyCheck")
                    {
                        ResetStableTimer();
                        ResetAutoHoverState();
                        ResetAutoPickState();

                        if (_autoAcceptEnabled)
                        {
                            if (!_readyCheckHandled &&
                                _readyCheckCancellation == null)
                            {
                                StartAutoAccept();
                            }
                        }
                        else
                        {
                            CancelAutoAccept();

                            StatusText.Text =
                                "ReadyCheck | Auto Accept kapalı";
                        }
                    }
                    else if (phase == "ChampSelect")
                    {
                        CancelAutoAccept();

                        _readyCheckHandled = false;

                        await UpdateChampSelectStatusAsync();
                    }
                    else
                    {
                        CancelAutoAccept();
                        ResetStableTimer();
                        ResetAutoHoverState();
                        ResetAutoPickState();

                        _readyCheckHandled = false;
                        _autoBanHandledActionId = null;
                        StatusText.Text =
                            $"LCU Bağlandı ✓ | Durum: {phase}";
                    }
                }
                catch (Exception ex)
                {
                    CancelAutoAccept();

                    SetLcuBadge(
                        LcuBadgeState.Disconnected
                    );

                    StatusText.Text =
                        $"LCU bağlantısı kesildi. Yeniden bağlanılacak...";

                    _isConnected = false;

                    ResetStableTimer();
                    ResetAutoHoverState();
                    ResetAutoPickState();
                    _autoBanHandledActionId = null;
                    _readyCheckHandled = false;

                    WriteDebugLog(
                        $"[LCU RECONNECT] Bağlantı koptu | {ex.Message}"
                    );

                    break;
                }

                await Task.Delay(300);
            }
        }

        private void StartAutoAccept()
        {
            var cancellation =
                new CancellationTokenSource();

            _readyCheckCancellation = cancellation;

            WriteDebugLog(
                $"[AUTO ACCEPT] Başlatıldı | Delay={GetAutoAcceptDelaySeconds()}"
            );

            // Await etmiyoruz.
            // MonitorGameflowAsync çalışmaya devam edecek.
            _ = AutoAcceptAfterDelayAsync(cancellation);
        }

        private async Task AutoAcceptAfterDelayAsync(
    CancellationTokenSource cancellation)
        {
            try
            {
                int delaySeconds = GetAutoAcceptDelaySeconds();

                for (int remaining = delaySeconds;
                     remaining > 0;
                     remaining--)
                {
                    cancellation.Token.ThrowIfCancellationRequested();

                    StatusText.Text =
                        $"Oyun bulundu | {remaining} sn sonra kabul edilecek...";

                    AutoAcceptStatusText.Text =
                        $"{remaining} sn sonra otomatik kabul edilecek...";

                    await Task.Delay(
                        1000,
                        cancellation.Token
                    );
                }

                string currentPhase =
                    await GetCurrentPhaseAsync();

                if (currentPhase != "ReadyCheck")
                    return;

                if (!_autoAcceptEnabled)
                    return;

                using var response =
                    await _lcuClient.PostAsync(
                        "/lol-matchmaking/v1/ready-check/accept"
                    );

                response.EnsureSuccessStatusCode();

                _readyCheckHandled = true;

                StatusText.Text =
                    "Oyun otomatik kabul edildi ✓";

                AutoAcceptStatusText.Text =
                    "Maç otomatik kabul edildi ✓";

                WriteDebugLog(
                    "[AUTO ACCEPT] Tamamlandı ✓"
                );
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    $"Auto Accept hatası: {ex.Message}";

                AutoAcceptStatusText.Text =
                    $"Hata: {ex.Message}";
            }
            finally
            {
                if (ReferenceEquals(
                    _readyCheckCancellation,
                    cancellation))
                {
                    _readyCheckCancellation = null;
                }

                cancellation.Dispose();
            }
        }

        private void CancelAutoAccept()
        {
            var cancellation =
                _readyCheckCancellation;

            if (cancellation == null)
                return;

            _readyCheckCancellation = null;

            cancellation.Cancel();
        }
        private int GetAutoAcceptDelaySeconds()
        {
            if (AutoAcceptDelayComboBox.SelectedItem is ComboBoxItem selectedItem &&
                int.TryParse(selectedItem.Content?.ToString(), out int seconds))
            {
                return seconds;
            }

            return 5;
        }
        private async Task UpdateChampSelectStatusAsync()
        {
            try
            {
                Task<ChampSelectSession?> sessionTask =
                    _champSelectService.GetSessionAsync();

                Task<ChampSelectTimer?> timerTask =
                    _champSelectService.GetTimerAsync();

                await Task.WhenAll(sessionTask, timerTask);

                var session = await sessionTask;
                var timer = await timerTask;

                if (session == null || timer == null)
                {
                    ResetStableTimer();
                    StatusText.Text = "ChampSelect verisi alınamadı.";
                    return;
                }

                string phase = timer.Phase ?? string.Empty;

                // LCU'nun ham timer değeri. Auto Ban kararında stable sayaç
                // kullanacağız; çünkü raw değer bazı anlarda uzun süre sabit kalabiliyor.
                double rawRemainingSeconds =
                    timer.RemainingSeconds;

                // AUTO HOVER:
                // Auto Pick açık + Auto Hover açık ise, hazırlık/ban-pick
                // boyunca ilk uygun pick tercihini takımımıza göster.
                if (_autoPickEnabled &&
                    _settings.AutoHoverPickEnabled &&
                    (phase.Equals(
                        "PLANNING",
                        StringComparison.OrdinalIgnoreCase) ||
                     phase.Equals(
                        "BAN_PICK",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    await TryAutoHoverPickAsync(session);
                }

                if (phase.Equals(
                    "PLANNING",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ResetStableTimer();

                    StatusText.Text =
                        $"ChampSelect | Hazırlık | {rawRemainingSeconds:F1} sn";

                    return;
                }

                if (phase.Equals(
                    "FINALIZATION",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ResetStableTimer();

                    StatusText.Text =
                        $"ChampSelect | Son hazırlık | {rawRemainingSeconds:F1} sn";

                    return;
                }

                if (!phase.Equals(
                    "BAN_PICK",
                    StringComparison.OrdinalIgnoreCase))
                {
                    ResetStableTimer();

                    StatusText.Text =
                        $"ChampSelect | {phase} | {rawRemainingSeconds:F1} sn";

                    return;
                }

                var banAction = session.Actions
                    .SelectMany(group => group)
                    .FirstOrDefault(action =>
                        action.ActorCellId == session.LocalPlayerCellId &&
                        action.Type.Equals(
                            "ban",
                            StringComparison.OrdinalIgnoreCase) &&
                        action.IsInProgress &&
                        !action.Completed
                    );

                if (banAction != null)
                {
                    // Stable timer'ı eşik altına düşünce değil, ban sırası
                    // başladığı andan itibaren her poll'da güncelliyoruz.
                    double remainingSeconds =
                        GetStableRemainingSeconds(
                            phase,
                            banAction.Id,
                            rawRemainingSeconds
                        );

                    StatusText.Text =
                        $"BAN SIRASI SENDE | Action: {banAction.Id} | " +
                        $"Kalan: {remainingSeconds:F1} sn";

                    int autoBanAtSeconds =
                        GetAutoBanDelaySeconds();

                    if (_autoBanEnabled &&
                        remainingSeconds <= autoBanAtSeconds)
                    {
                        WriteDebugLog(
                            $"[AUTO BAN] EŞİK | " +
                            $"Phase={phase} | " +
                            $"Action={banAction.Id} | " +
                            $"Raw={rawRemainingSeconds:F2} | " +
                            $"Stable={remainingSeconds:F2} | " +
                            $"Target={autoBanAtSeconds}"
                        );

                        _ = TryAutoBanAsync(
                            banAction,
                            remainingSeconds
                        );
                    }

                    return;
                }

                var pickAction = session.Actions
                    .SelectMany(group => group)
                    .FirstOrDefault(action =>
                        action.ActorCellId == session.LocalPlayerCellId &&
                        action.Type.Equals(
                            "pick",
                            StringComparison.OrdinalIgnoreCase) &&
                        action.IsInProgress &&
                        !action.Completed
                    );

                if (pickAction != null)
                {
                    // Auto Ban'da kullandığımız stable timer mantığını
                    // Auto Pick için de aynen kullanıyoruz.
                    double remainingSeconds =
                        GetStableRemainingSeconds(
                            phase,
                            pickAction.Id,
                            rawRemainingSeconds
                        );

                    StatusText.Text =
                        $"PICK SIRASI SENDE | Action: {pickAction.Id} | " +
                        $"Kalan: {remainingSeconds:F1} sn";

                    int autoPickAtSeconds =
                        GetAutoPickDelaySeconds();

                    if (_autoPickEnabled &&
                        remainingSeconds <= autoPickAtSeconds)
                    {
                        WriteDebugLog(
                            $"[AUTO PICK] EŞİK | " +
                            $"Phase={phase} | " +
                            $"Action={pickAction.Id} | " +
                            $"Raw={rawRemainingSeconds:F2} | " +
                            $"Stable={remainingSeconds:F2} | " +
                            $"Target={autoPickAtSeconds}"
                        );

                        _ = TryAutoPickAsync(
                            pickAction,
                            remainingSeconds
                        );
                    }

                    return;
                }

                // Aktif ban/pick action yoksa eski sayaç bir sonraki
                // sıraya taşınmasın.
                ResetStableTimer();

                StatusText.Text =
                    $"ChampSelect | Sıra bekleniyor | {rawRemainingSeconds:F1} sn";
            }
            catch (Exception ex)
            {
                ResetStableTimer();

                StatusText.Text =
                    $"ChampSelect hatası: {ex.Message}";
            }
        }

        private HashSet<int> GetUnavailablePickChampions(
            ChampSelectSession session)
        {
            HashSet<int> unavailable =
                GetAlreadyBannedChampions(session);

            // Kilitlenmiş pick'ler de artık seçilemez.
            foreach (var action in session.Actions.SelectMany(x => x))
            {
                if (action.Type.Equals(
                        "pick",
                        StringComparison.OrdinalIgnoreCase) &&
                    action.Completed &&
                    action.ChampionId > 0)
                {
                    unavailable.Add(action.ChampionId);
                }
            }

            return unavailable;
        }

        private int? GetBestAutoHoverChampion(
            ChampSelectSession session)
        {
            int[] preferences =
            {
                _settings.PrimaryPickChampionId,
                _settings.SecondaryPickChampionId,
                _settings.TertiaryPickChampionId
            };

            HashSet<int> unavailable =
                GetUnavailablePickChampions(session);

            foreach (int championId in preferences.Distinct())
            {
                if (championId <= 0)
                    continue;

                if (unavailable.Contains(championId))
                    continue;

                return championId;
            }

            return null;
        }

        private async Task TryAutoHoverPickAsync(
            ChampSelectSession session)
        {
            if (!_autoPickEnabled ||
                !_settings.AutoHoverPickEnabled ||
                _autoHoverRunning)
            {
                return;
            }

            // Hover için pick sıramızın gelmesini beklemiyoruz.
            // Kendi tamamlanmamış pick action'ımızı bulup completed=false
            // olarak championId güncelliyoruz.
            var pickAction = session.Actions
                .SelectMany(group => group)
                .FirstOrDefault(action =>
                    action.ActorCellId == session.LocalPlayerCellId &&
                    action.Type.Equals(
                        "pick",
                        StringComparison.OrdinalIgnoreCase) &&
                    !action.Completed
                );

            if (pickAction == null)
                return;

            int? desiredChampionId =
                GetBestAutoHoverChampion(session);

            if (!desiredChampionId.HasValue)
            {
                AutoPickStatusText.Text =
                    "Auto Hover: uygun pick tercihi bulunamadı.";

                return;
            }

            int championId =
                desiredChampionId.Value;

            // LCU session içinde zaten istediğimiz champion görünüyorsa
            // tekrar istek atmaya gerek yok. Cache'e tek başına güvenmiyoruz;
            // client hover'ı sıfırlarsa veya kullanıcı değiştirirse tekrar uygularız.
            int currentIntent =
                session.MyTeam
                    .FirstOrDefault(member =>
                        member.CellId == session.LocalPlayerCellId)
                    ?.ChampionPickIntent ?? 0;

            if (currentIntent == championId ||
                pickAction.ChampionId == championId)
            {
                _lastAutoHoverActionId = pickAction.Id;
                _lastAutoHoverChampionId = championId;

                AutoPickStatusText.Text =
                    $"Auto Hover aktif: {GetChampionName(championId)}";

                return;
            }

            _autoHoverRunning = true;

            try
            {
                string endpoint =
                    $"/lol-champ-select/v1/session/actions/{pickAction.Id}";

                // Hover: championId değişir ama action TAMAMLANMAZ.
                pickAction.ChampionId = championId;
                pickAction.Completed = false;

                string json =
                    JsonSerializer.Serialize(pickAction);

                using var response =
                    await _lcuClient.PatchJsonAsync(
                        endpoint,
                        json
                    );

                string body =
                    await response.Content.ReadAsStringAsync();

                WriteDebugLog(
                    $"[AUTO HOVER] PATCH | " +
                    $"Action={pickAction.Id} | " +
                    $"Champion={championId} | " +
                    $"Status={(int)response.StatusCode} " +
                    $"{response.StatusCode} | body={body}"
                );

                if (!response.IsSuccessStatusCode)
                {
                    AutoPickStatusText.Text =
                        $"Auto Hover başarısız: {(int)response.StatusCode}";

                    return;
                }

                _lastAutoHoverActionId =
                    pickAction.Id;

                _lastAutoHoverChampionId =
                    championId;

                string championName =
                    GetChampionName(championId);

                AutoPickStatusText.Text =
                    $"Gösteriliyor: {championName}";

                WriteDebugLog(
                    $"[AUTO HOVER] OK | " +
                    $"Action={pickAction.Id} | " +
                    $"Champion={championId} ({championName}) | " +
                    $"Completed=False"
                );
            }
            catch (Exception ex)
            {
                AutoPickStatusText.Text =
                    $"Auto Hover hatası: {ex.Message}";

                WriteDebugLog(
                    $"[AUTO HOVER] HATA | {ex}"
                );
            }
            finally
            {
                _autoHoverRunning = false;
            }
        }

        private async Task<HashSet<int>> GetPickableChampionIdsAsync()
        {
            try
            {
                string json =
                    await _lcuClient.GetStringAsync(
                        "/lol-champ-select/v1/pickable-champion-ids"
                    );

                List<int>? ids =
                    JsonSerializer.Deserialize<List<int>>(json);

                return ids?
                    .Where(id => id > 0)
                    .ToHashSet()
                    ?? new HashSet<int>();
            }
            catch (Exception ex)
            {
                // Endpoint geçici olarak veri vermezse Auto Pick'i tamamen
                // durdurmuyoruz. Banlı/picklenmiş kontrolüne göre devam ederiz.
                WriteDebugLog(
                    $"[AUTO PICK] Pickable listesi alınamadı | {ex.Message}"
                );

                return new HashSet<int>();
            }
        }

        private async Task<int?> GetBestPickChampionAsync(
            ChampSelectSession session)
        {
            int[] preferences =
            {
                _settings.PrimaryPickChampionId,
                _settings.SecondaryPickChampionId,
                _settings.TertiaryPickChampionId
            };

            HashSet<int> unavailable =
                GetUnavailablePickChampions(session);

            HashSet<int> pickable =
                await GetPickableChampionIdsAsync();

            WriteDebugLog(
                $"[AUTO PICK] INPUT | " +
                $"Prefs=[{string.Join(",", preferences)}] | " +
                $"PickableCount={pickable.Count} | " +
                $"Unavailable=[{string.Join(",", unavailable)}]"
            );

            foreach (int championId in preferences.Distinct())
            {
                if (championId <= 0)
                    continue;

                if (unavailable.Contains(championId))
                    continue;

                // Pickable endpoint doluysa onu doğrulama olarak kullan.
                // Boş dönerse LCU'nun geçici/stale cevabı yüzünden sistemi
                // kilitlememek için tercihi yine deneyebiliriz.
                if (pickable.Count > 0 &&
                    !pickable.Contains(championId))
                {
                    continue;
                }

                return championId;
            }

            return null;
        }

        private async Task TryAutoPickAsync(
            ChampSelectAction pickAction,
            double remainingSeconds)
        {
            if (!_autoPickEnabled)
                return;

            int autoPickAtSeconds =
                GetAutoPickDelaySeconds();

            if (remainingSeconds > autoPickAtSeconds)
                return;

            if (_autoPickRunning)
                return;

            if (_autoPickHandledActionId == pickAction.Id)
                return;

            // Birden fazla 300 ms loop'un aynı anda pick denememesi için
            // network çağrılarından ÖNCE kilitliyoruz.
            _autoPickRunning = true;

            try
            {
                var latestSession =
                    await _champSelectService.GetSessionAsync();

                if (latestSession == null)
                {
                    AutoPickStatusText.Text =
                        "Auto Pick: session alınamadı.";

                    return;
                }

                // Eski action nesnesine güvenmek yerine en güncel action'ı al.
                var latestPickAction =
                    latestSession.Actions
                        .SelectMany(x => x)
                        .FirstOrDefault(action =>
                            action.Id == pickAction.Id &&
                            action.ActorCellId ==
                                latestSession.LocalPlayerCellId &&
                            action.Type.Equals(
                                "pick",
                                StringComparison.OrdinalIgnoreCase) &&
                            action.IsInProgress &&
                            !action.Completed
                        );

                if (latestPickAction == null)
                {
                    // Action tamamlandıysa veya sıra geçtiyse tekrar denemeyelim.
                    return;
                }

                int? selectedChampionId =
                    await GetBestPickChampionAsync(
                        latestSession
                    );

                if (!selectedChampionId.HasValue)
                {
                    AutoPickStatusText.Text =
                        "Auto Pick: uygun tercih bulunamadı.";

                    WriteDebugLog(
                        $"[AUTO PICK] KARAR | Champion=YOK | " +
                        $"Remaining={remainingSeconds:F2}"
                    );

                    return;
                }

                int championId =
                    selectedChampionId.Value;

                string championName =
                    GetChampionName(championId);

                AutoPickStatusText.Text =
                    $"Auto Pick tetiklendi: {championName} | " +
                    $"{remainingSeconds:F1} sn";

                WriteDebugLog(
                    $"[AUTO PICK] DENEME | " +
                    $"Action={latestPickAction.Id} | " +
                    $"Champion={championId} ({championName}) | " +
                    $"Remaining={remainingSeconds:F2}"
                );

                string endpoint =
                    $"/lol-champ-select/v1/session/actions/{latestPickAction.Id}";

                // Pick lock: championId + completed=true.
                latestPickAction.ChampionId =
                    championId;

                latestPickAction.Completed =
                    true;

                string json =
                    JsonSerializer.Serialize(
                        latestPickAction
                    );

                using var patchResponse =
                    await _lcuClient.PatchJsonAsync(
                        endpoint,
                        json
                    );

                string patchBody =
                    await patchResponse.Content.ReadAsStringAsync();

                WriteDebugLog(
                    $"[AUTO PICK] PATCH {endpoint} -> " +
                    $"{(int)patchResponse.StatusCode} " +
                    $"{patchResponse.StatusCode}; " +
                    $"body={patchBody}"
                );

                if (!patchResponse.IsSuccessStatusCode)
                {
                    AutoPickStatusText.Text =
                        $"Auto Pick PATCH hatası: " +
                        $"{(int)patchResponse.StatusCode}";

                    return;
                }

                await Task.Delay(75);

                var verifySession =
                    await _champSelectService.GetSessionAsync();

                var verifyAction =
                    verifySession?.Actions
                        .SelectMany(x => x)
                        .FirstOrDefault(x =>
                            x.Id == latestPickAction.Id);

                // Tamamlanmış action bazı durumlarda listeden kaybolabilir.
                if (verifyAction == null)
                {
                    _autoPickHandledActionId =
                        latestPickAction.Id;

                    AutoPickStatusText.Text =
                        $"Auto Pick tamamlandı ✓ | {championName}";

                    WriteDebugLog(
                        $"[AUTO PICK] TAMAMLANDI ✓ | " +
                        $"Action={latestPickAction.Id} | " +
                        $"Champion={championId}"
                    );

                    return;
                }

                WriteDebugLog(
                    $"[AUTO PICK] PATCH sonrası | " +
                    $"Action={verifyAction.Id} | " +
                    $"ChampionId={verifyAction.ChampionId} | " +
                    $"Completed={verifyAction.Completed}"
                );

                if (verifyAction.Completed)
                {
                    _autoPickHandledActionId =
                        latestPickAction.Id;

                    AutoPickStatusText.Text =
                        $"Auto Pick tamamlandı ✓ | {championName}";

                    WriteDebugLog(
                        $"[AUTO PICK] TAMAMLANDI ✓ | " +
                        $"Action={latestPickAction.Id} | " +
                        $"Champion={championId}"
                    );

                    return;
                }

                // PATCH championId'yi yazdı ama action hâlâ tamamlanmadıysa
                // ban tarafındaki çalışan fallback'i burada da kullan.
                if (verifyAction.ChampionId == championId)
                {
                    WriteDebugLog(
                        "[AUTO PICK] ChampionId yazıldı, " +
                        "Completed=False | /complete deneniyor"
                    );

                    using var completeResponse =
                        await _lcuClient.PostAsync(
                            $"{endpoint}/complete"
                        );

                    string completeBody =
                        await completeResponse.Content.ReadAsStringAsync();

                    WriteDebugLog(
                        $"[AUTO PICK] POST {endpoint}/complete -> " +
                        $"{(int)completeResponse.StatusCode} " +
                        $"{completeResponse.StatusCode}; " +
                        $"body={completeBody}"
                    );

                    if (!completeResponse.IsSuccessStatusCode)
                    {
                        AutoPickStatusText.Text =
                            $"Auto Pick complete hatası: " +
                            $"{(int)completeResponse.StatusCode}";

                        return;
                    }

                    await Task.Delay(75);

                    var finalSession =
                        await _champSelectService.GetSessionAsync();

                    var finalAction =
                        finalSession?.Actions
                            .SelectMany(x => x)
                            .FirstOrDefault(x =>
                                x.Id == latestPickAction.Id);

                    if (finalAction == null ||
                        finalAction.Completed)
                    {
                        _autoPickHandledActionId =
                            latestPickAction.Id;

                        AutoPickStatusText.Text =
                            $"Auto Pick tamamlandı ✓ | {championName}";

                        WriteDebugLog(
                            $"[AUTO PICK] TAMAMLANDI ✓ | " +
                            $"Action={latestPickAction.Id} | " +
                            $"Champion={championId}"
                        );

                        return;
                    }

                    WriteDebugLog(
                        $"[AUTO PICK] COMPLETE sonrası | " +
                        $"ChampionId={finalAction.ChampionId} | " +
                        $"Completed={finalAction.Completed}"
                    );

                    AutoPickStatusText.Text =
                        "Pick kilitlenmedi; tekrar denenecek.";

                    return;
                }

                AutoPickStatusText.Text =
                    "Pick action'a yazılamadı; tekrar denenecek.";

                WriteDebugLog(
                    $"[AUTO PICK] ChampionId değişmedi | " +
                    $"LCU={verifyAction.ChampionId} | " +
                    $"Beklenen={championId}"
                );
            }
            catch (TaskCanceledException)
            {
                AutoPickStatusText.Text =
                    "Auto Pick isteği zaman aşımına uğradı.";

                WriteDebugLog(
                    "[AUTO PICK] TIMEOUT"
                );
            }
            catch (Exception ex)
            {
                AutoPickStatusText.Text =
                    $"Auto Pick hatası: {ex.Message}";

                WriteDebugLog(
                    $"[AUTO PICK] HATA | {ex}"
                );
            }
            finally
            {
                _autoPickRunning = false;
            }
        }

        private async Task TryAutoBanAsync(
    ChampSelectAction banAction,
    double remainingSeconds)
        {
            if (!_autoBanEnabled)
                return;
            int autoBanAtSeconds = GetAutoBanDelaySeconds();

            if (remainingSeconds > autoBanAtSeconds)
                return;

            if (_autoBanRunning)
                return;

            if (_autoBanHandledActionId == banAction.Id)
                return;

            var latestSession =
    await _champSelectService.GetSessionAsync();

            if (latestSession == null)
            {
                AutoBanStatusText.Text =
                    "Champ Select session alınamadı.";

                return;
            }

            int? selectedChampionId =
                await GetBestBanChampionAsync(
                    latestSession,
                    remainingSeconds
                );

            if (!selectedChampionId.HasValue)
            {
                // ÖNEMLİ:
                // burada handled yapmıyoruz.
                //
                // Hover değişebilir.
                // Sonraki ~250 ms loop tekrar deneyecek.
                return;
            }

            int championId =
                selectedChampionId.Value;

            _autoBanRunning = true;

            try
            {
                AutoBanStatusText.Text =
                    $"Auto Ban tetiklendi | {remainingSeconds:F1} sn";

                WriteDebugLog(
                    $"[AUTO BAN] DENEME | Action={banAction.Id} | " +
                    $"Champion={championId} | " +
                    $"Remaining={remainingSeconds:F2}"
                );

                string endpoint =
                    $"/lol-champ-select/v1/session/actions/{banAction.Id}";

                // Elimizdeki gerçek action'ı kullan.
                // championId + completed=true birlikte gönder.
                banAction.ChampionId = championId;
                banAction.Completed = true;

                string json =
                    JsonSerializer.Serialize(banAction);

                using var patchResponse =
                    await _lcuClient.PatchJsonAsync(
                        endpoint,
                        json
                    );

                string patchBody =
                    await patchResponse.Content.ReadAsStringAsync();

                WriteDebugLog(
                    $"[AUTO BAN] PATCH {endpoint} -> " +
                    $"{(int)patchResponse.StatusCode} " +
                    $"{patchResponse.StatusCode}; " +
                    $"body={patchBody}"
                );

                if (!patchResponse.IsSuccessStatusCode)
                {
                    AutoBanStatusText.Text =
                        $"PATCH hatası: {(int)patchResponse.StatusCode}";

                    return;
                }

                await Task.Delay(75);

                // İlk doğrulama
                var verifySession =
                    await _champSelectService.GetSessionAsync();

                var verifyAction =
                    verifySession?.Actions
                        .SelectMany(x => x)
                        .FirstOrDefault(x =>
                            x.Id == banAction.Id);

                if (verifyAction == null)
                {
                    // Action tamamlanınca listeden kaybolmuş olabilir.
                    _autoBanHandledActionId = banAction.Id;

                    AutoBanStatusText.Text =
                        $"Auto Ban tamamlandı ✓ | ID: {championId}";

                    return;
                }

                WriteDebugLog(
                    $"[AUTO BAN] PATCH sonrası | Action={verifyAction.Id} | " +
                    $"ChampionId={verifyAction.ChampionId} | " +
                    $"Completed={verifyAction.Completed}"
                );

                // PATCH zaten tamamladıysa iş bitti.
                if (verifyAction.Completed)
                {
                    _autoBanHandledActionId =
                        banAction.Id;

                    AutoBanStatusText.Text =
                        $"Auto Ban tamamlandı ✓ | ID: {championId}";

                    return;
                }

                // Champion seçilmiş fakat completed false.
                // Logunda tam olarak bu olmuş.
                if (verifyAction.ChampionId == championId)
                {
                    WriteDebugLog(
                        "[AUTO BAN] ChampionId yazıldı fakat completed=false. " +
                        "/complete deneniyor."
                    );

                    using var completeResponse =
                        await _lcuClient.PostAsync(
                            $"{endpoint}/complete"
                        );

                    string completeBody =
                        await completeResponse.Content.ReadAsStringAsync();

                    WriteDebugLog(
                        $"[AUTO BAN] POST {endpoint}/complete -> " +
                        $"{(int)completeResponse.StatusCode} " +
                        $"{completeResponse.StatusCode}; " +
                        $"body={completeBody}"
                    );

                    if (!completeResponse.IsSuccessStatusCode)
                    {
                        AutoBanStatusText.Text =
                            $"Complete hatası: " +
                            $"{(int)completeResponse.StatusCode}";

                        return;
                    }

                    await Task.Delay(75);

                    // Complete gerçekten oldu mu?
                    var finalSession =
                        await _champSelectService.GetSessionAsync();

                    var finalAction =
                        finalSession?.Actions
                            .SelectMany(x => x)
                            .FirstOrDefault(x =>
                                x.Id == banAction.Id);

                    // Tamamlanınca action kaybolabilir.
                    if (finalAction == null)
                    {
                        _autoBanHandledActionId =
                            banAction.Id;

                        AutoBanStatusText.Text =
                            $"Auto Ban tamamlandı ✓ | ID: {championId}";

                        return;
                    }

                    WriteDebugLog(
                        $"[AUTO BAN] COMPLETE sonrası | " +
                        $"ChampionId={finalAction.ChampionId} | " +
                        $"Completed={finalAction.Completed}"
                    );

                    if (finalAction.Completed)
                    {
                        _autoBanHandledActionId =
                            banAction.Id;

                        AutoBanStatusText.Text =
                            $"Auto Ban tamamlandı ✓ | ID: {championId}";

                        return;
                    }

                    // ÖNEMLİ:
                    // completed hâlâ false ise handled yapmıyoruz.
                    // Böylece bir sonraki loop tekrar deneyebilir.
                    AutoBanStatusText.Text =
                        "Champion seçildi ama ban kilitlenmedi; tekrar deneniyor...";

                    WriteDebugLog(
                        "[AUTO BAN] Ban kilitlenmedi. handled ayarlanmadı."
                    );

                    return;
                }

                AutoBanStatusText.Text =
                    "Champion action'a yazılamadı; tekrar deneniyor...";

                WriteDebugLog(
                    $"[AUTO BAN] ChampionId değişmedi. " +
                    $"LCU={verifyAction.ChampionId}, Beklenen={championId}"
                );
            }
            catch (TaskCanceledException)
            {
                AutoBanStatusText.Text =
                    "Auto Ban isteği zaman aşımına uğradı.";

                WriteDebugLog(
                    "[AUTO BAN] TIMEOUT"
                );
            }
            catch (Exception ex)
            {
                AutoBanStatusText.Text =
                    $"Auto Ban hatası: {ex.Message}";

                WriteDebugLog(
                    ex.ToString()
                );
            }
            finally
            {
                _autoBanRunning = false;
            }
        }
        private HashSet<int> GetCurrentTeammateProtectedChampions(
    ChampSelectSession session)
        {
            var protectedChampions = new HashSet<int>();

            foreach (var member in session.MyTeam)
            {
                // Kendimizi hesaba katma.
                if (member.CellId == session.LocalPlayerCellId)
                    continue;

                // Pick intent / gösterilen şampiyon.
                if (member.ChampionPickIntent > 0)
                {
                    protectedChampions.Add(
                        member.ChampionPickIntent
                    );
                }

                // Bazı champ-select durumlarında championId üzerinden
                // de güncel seçim görülebilir. Güvenli tarafta kalıyoruz.
                if (member.ChampionId > 0)
                {
                    protectedChampions.Add(
                        member.ChampionId
                    );
                }
            }

            return protectedChampions;
        }
        private HashSet<int> GetAlreadyBannedChampions(
    ChampSelectSession session)
        {
            var banned = new HashSet<int>();

            foreach (int championId in session.Bans.MyTeamBans)
            {
                if (championId > 0)
                    banned.Add(championId);
            }

            foreach (int championId in session.Bans.TheirTeamBans)
            {
                if (championId > 0)
                    banned.Add(championId);
            }

            // Ek güvence:
            // completed ban action'larını da kontrol et.
            foreach (var action in session.Actions.SelectMany(x => x))
            {
                if (action.Type.Equals(
                        "ban",
                        StringComparison.OrdinalIgnoreCase) &&
                    action.Completed &&
                    action.ChampionId > 0)
                {
                    banned.Add(action.ChampionId);
                }
            }

            return banned;
        }
        private string GetChampionName(int championId)
        {
            return _champions
                       .FirstOrDefault(x => x.Id == championId)
                       ?.Name
                   ?? $"ID {championId}";
        }
        private async Task<int?> GetBestBanChampionAsync(
    ChampSelectSession session,
    double remainingSeconds)
        {
            int[] preferences =
            {
        _settings.PrimaryBanChampionId,
        _settings.SecondaryBanChampionId,
        _settings.TertiaryBanChampionId
    };

            HashSet<int> bannableChampions =
                await _champSelectService
                    .GetBannableChampionIdsAsync();

            HashSet<int> bannedChampions =
                GetAlreadyBannedChampions(session);

            HashSet<int> teammateHoveredChampions =
                GetCurrentTeammateProtectedChampions(session);

            double hoverFallbackAtSeconds =
                GetHoverFallbackAtSeconds();
            WriteDebugLog(
    $"[AUTO BAN] INPUT | " +
    $"Prefs=[{string.Join(",", preferences)}] | " +
    $"BannableCount={bannableChampions.Count} | " +
    $"Banned=[{string.Join(",", bannedChampions)}] | " +
    $"Hovered=[{string.Join(",", teammateHoveredChampions)}] | " +
    $"P1Bannable={bannableChampions.Contains(preferences[0])} | " +
    $"P2Bannable={bannableChampions.Contains(preferences[1])} | " +
    $"P3Bannable={bannableChampions.Contains(preferences[2])}"
);
            BanDecision decision =

                _banDecisionService.Decide(
                    preferences,
                    bannableChampions,
                    bannedChampions,
                    teammateHoveredChampions,
                    _settings.AvoidTeammateHover,
                    remainingSeconds,
                    hoverFallbackAtSeconds
                );

            WriteDebugLog(
                $"[AUTO BAN] KARAR | " +
                $"Remaining={remainingSeconds:F2} | " +
                $"FallbackAt={hoverFallbackAtSeconds:F2} | " +
                $"Champion={decision.ChampionId?.ToString() ?? "YOK"} | " +
                $"Tercih={decision.PreferenceNumber?.ToString() ?? "YOK"} | " +
                $"Wait={decision.ShouldWait} | " +
                $"Reason={decision.Reason}"
            );

            if (decision.ShouldWait)
            {
                AutoBanStatusText.Text =
                    $"Bekleniyor: {decision.Reason}";

                return null;
            }

            if (!decision.HasChampion)
            {
                AutoBanStatusText.Text =
                    decision.Reason;

                return null;
            }

            string championName =
                GetChampionName(
                    decision.ChampionId!.Value
                );

            AutoBanStatusText.Text =
                $"{decision.PreferenceNumber}. tercih kullanılacak: {championName}";

            return decision.ChampionId.Value;
        }
        private async Task<bool> SendBanActionAsync(
            string endpointBase,
            long actionId,
            int championId)
        {
            string endpoint = $"{endpointBase}/{actionId}";

            string patchJson =
                JsonSerializer.Serialize(new
                {
                    championId
                });

            using var patchResponse =
                await _lcuClient.PatchJsonAsync(
                    endpoint,
                    patchJson
                );

            string patchBody =
                await patchResponse.Content.ReadAsStringAsync();

            WriteDebugLog(
                $"PATCH {endpoint} -> {(int)patchResponse.StatusCode} " +
                $"{patchResponse.StatusCode}; body={patchBody}"
            );

            if (!patchResponse.IsSuccessStatusCode)
                return false;

            using var completeResponse =
                await _lcuClient.PostAsync(
                    $"{endpoint}/complete"
                );

            string completeBody =
                await completeResponse.Content.ReadAsStringAsync();

            WriteDebugLog(
                $"[AUTO BAN] POST {endpoint}/complete -> " +
                $"{(int)completeResponse.StatusCode} " +
                $"{completeResponse.StatusCode}; body={completeBody}"
            );

            if (!completeResponse.IsSuccessStatusCode)
                return false;

            await Task.Delay(100);

            var verifySession =
                await _champSelectService.GetSessionAsync();

            var verifyAction = verifySession?.Actions
                .SelectMany(x => x)
                .FirstOrDefault(x => x.Id == actionId);

            if (verifyAction == null)
            {
                WriteDebugLog(
                    $"Doğrulama: Action {actionId} bulunamadı."
                );
                return true;
            }

            bool changed =
                verifyAction.Completed ||
                verifyAction.ChampionId == championId;

            WriteDebugLog(
                $"Doğrulama: Action={actionId}, " +
                $"ChampionId={verifyAction.ChampionId}, " +
                $"Completed={verifyAction.Completed}, " +
                $"Beklenen={championId}"
            );

            return changed;
        }

        private void WriteDebugLog(string message)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "RiftAssistant"
                );

                Directory.CreateDirectory(folder);

                string path = Path.Combine(
                    folder,
                    "debug.log"
                );

                // Log dosyasının sınırsız büyümesini engelle.
                const long maxLogSizeBytes =
                    2L * 1024L * 1024L;

                if (File.Exists(path))
                {
                    var info = new FileInfo(path);

                    if (info.Length >= maxLogSizeBytes)
                    {
                        string oldPath = Path.Combine(
                            folder,
                            "debug.old.log"
                        );

                        if (File.Exists(oldPath))
                            File.Delete(oldPath);

                        File.Move(
                            path,
                            oldPath
                        );
                    }
                }

                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}" +
                    Environment.NewLine
                );
            }
            catch
            {
            }
        }

        private async Task<string> GetCurrentPhaseAsync()
        {
            string phase =
                await _lcuClient.GetStringAsync(
                    "/lol-gameflow/v1/gameflow-phase"
                );

            return phase.Trim('"');
        }
    }
}