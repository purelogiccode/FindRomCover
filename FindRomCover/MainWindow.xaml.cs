using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
{
    private static readonly HashSet<string> NonRomExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dummy", ".bat", ".url", ".lnk", ".exe", ".cmd", ".txt", ".nfo", ".xml", ".json", ".ini", ".cfg", ".log",
        ".dat"
    };

    private readonly SemaphoreSlim _findSimilarSemaphore = new(1, 1);
    private AiAssistService? _aiAssistService;
    private AiQueryHistory? _aiQueryHistory;
    private CancellationTokenSource? _aiAssistCts;
    private bool _aiBatchRunning;
    private bool _disposed;
    private CancellationTokenSource? _findSimilarCts;
    private Task? _findSimilarTask;
    private ImageFolderWatcher? _imageFolderWatcher;
    private bool _isExiting;
    private CancellationTokenSource? _loadMissingCts;
    private Task? _loadMissingTask;
    private Dictionary<string, string>? _mameLookup;
    private string _selectedRomFileName = string.Empty;
    private TrayIconManager? _systemTrayIcon;
    private string? _watchedFolderPath;
    private bool _webView2NotificationShown;
    private bool _webView2Unavailable;

    public MainWindow(SettingsManager settingsManager, string? startupImageFolder = null,
        string? startupRomFolder = null)
    {
        Settings = settingsManager;
        InitializeComponent();
        DataContext = this;

        CheckForMissingImagesCommand = new DelegateCommand(
            _ => _ = CheckForMissingImagesSafeAsync(),
            _ => BtnCheckForMissingImages?.IsEnabled ?? false);
        ExitCommand = new DelegateCommand(_ => Close());

        if (!string.IsNullOrEmpty(startupImageFolder)) TxtImageFolder.Text = startupImageFolder;

        if (!string.IsNullOrEmpty(startupRomFolder)) TxtRomFolder.Text = startupRomFolder;

        UpdateThumbnailSizeMenuChecks();
        UpdateSimilarityAlgorithmChecks();
        UpdateSimilarityThresholdChecks();
        UpdateIgnoreBracketedTextCheck();
        UpdateAccentColorChecks();
        UpdateBaseThemeMenuChecks();
        UpdateMameDescriptionCheck();

        Settings.PropertyChanged += AppSettingsManagerPropertyChangedAsync;
        SimilarImages.CollectionChanged += (_, _) =>
        {
            UpdateAiPickAvailability();
            NotifyOverlayProperties();
        };
        PanelImages.CollectionChanged += (_, _) =>
        {
            UpdateAiPickAvailability();
            NotifyOverlayProperties();
        };
        MissingImages.CollectionChanged += (_, _) => NotifyOverlayProperties();
        Closing += OnWindowClosing;
        Loaded += MainWindow_LoadedAsync;
        Closed += OnWindowClosed;
        PropertyChanged += OnWindowPropertyChanged;
        GoogleWebView.NavigationCompleted += (_, _) =>
        {
            IsSearching = false;
            StatusMessage.Text = "Google web search loaded.";
        };
        BingWebView.NavigationCompleted += (_, _) =>
        {
            IsSearching = false;
            StatusMessage.Text = "Bing web search loaded.";
        };

        InitializeNotifyIcon();

        _ = LoadMameDataAsync();
        UpdateUiStateForFolderPaths();
        UpdateAiPickAvailability();
    }

    public ObservableCollection<ImageData> SimilarImages { get; set; } = [];
    public ObservableCollection<ImageData> PanelImages { get; set; } = [];
    public ObservableCollection<MissingImageItem> MissingImages { get; set; } = [];

    public bool IsCheckingMissing
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsFindingSimilar
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            NotifyOverlayProperties();
        }
    }

    public bool HasSearchedSimilar
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            NotifyOverlayProperties();
        }
    }

    public bool IsSearching
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            NotifyOverlayProperties();
        }
    }

    public bool HasSearchedApi
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            NotifyOverlayProperties();
        }
    }

    public bool IsAiPickAvailable
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsAiPickApiAvailable
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsAiBusy
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            UpdateAiPickAvailability();
        }
    }

    // --- Computed properties replacing the WPF DataTriggers ---

    public bool EmptyLocalOverlayVisible => !IsFindingSimilar && SimilarImages.Count == 0;

    public bool ShowSelectHint => !HasSearchedSimilar;

    public bool ShowNoResults => HasSearchedSimilar;

    public bool ApiEmptyMessageVisible => !IsSearching && PanelImages.Count == 0;

    public string ApiEmptyMessage => HasSearchedApi
        ? "No results found. Try adjusting your search query or API settings."
        : "Select a missing cover from the list to see suggestions.";

    public SettingsManager Settings { get; }

    public ICommand CheckForMissingImagesCommand { get; }
    public ICommand ExitCommand { get; }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Settings.PropertyChanged -= AppSettingsManagerPropertyChangedAsync;

        _findSimilarCts?.Cancel();
        _loadMissingCts?.Cancel();
        _aiAssistCts?.Cancel();

        // NOTE: never block the UI thread on the search/scan tasks here — they post
        // continuations back to this dispatcher. Cancellation is requested above and
        // SettleBackgroundTasksAsync (invoked from OnWindowClosed) awaits them off-thread.

        _findSimilarCts?.Dispose();
        _findSimilarCts = null;
        _loadMissingCts?.Dispose();
        _loadMissingCts = null;
        _aiAssistCts?.Dispose();
        _aiAssistCts = null;
        _aiAssistService?.Dispose();
        _aiAssistService = null;

        _findSimilarSemaphore.Dispose();
        _imageFolderWatcher?.Dispose();
        _systemTrayIcon?.Dispose();

        if (CheckForMissingImagesCommand is IDisposable disposableCheckCommand)
            disposableCheckCommand.Dispose();
        if (ExitCommand is IDisposable disposableExitCommand)
            disposableExitCommand.Dispose();

        GC.SuppressFinalize(this);
    }

    private event PropertyChangedEventHandler? PropertyChangedCore;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChangedCore += value;
        remove => PropertyChangedCore -= value;
    }

    private void NotifyOverlayProperties()
    {
        OnPropertyChanged(nameof(EmptyLocalOverlayVisible));
        OnPropertyChanged(nameof(ShowSelectHint));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ApiEmptyMessageVisible));
        OnPropertyChanged(nameof(ApiEmptyMessage));
    }

    private void InitializeNotifyIcon()
    {
        try
        {
            _systemTrayIcon = new TrayIconManager();
            _systemTrayIcon.Initialize();
            _systemTrayIcon.RestoreRequested += RestoreFromTray;
            _systemTrayIcon.ExitRequested += ExitApplication;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in InitializeNotifyIcon");
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized) MinimizeToTray();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in OnWindowStateChanged");
        }
    }

    private void MinimizeToTray()
    {
        try
        {
            if (_systemTrayIcon == null) return;

            _systemTrayIcon.Visible = true;
            Hide();
            _systemTrayIcon.ShowBalloonTip("FindRomCover", "Application minimized to tray.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in MinimizeToTray");
        }
    }

    private void RestoreFromTray()
    {
        try
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _systemTrayIcon?.Visible = false;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in RestoreFromTray");
        }
    }

    private void ExitApplication()
    {
        try
        {
            _isExiting = true;
            _systemTrayIcon?.Dispose();
            _systemTrayIcon = null;
            App.ShutdownApplication();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ExitApplication");
        }
    }

    private async void MainWindow_LoadedAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            LstMissingImages.KeyDown += LstMissingImages_KeyDown;

            await InitializeWebViewsAsync();

            if (!string.IsNullOrEmpty(TxtRomFolder.Text) && !string.IsNullOrEmpty(TxtImageFolder.Text))
                await RefreshMissingImagesListAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in MainWindow_LoadedAsync");
        }
    }

    /// <summary>
    ///     NativeWebView creates its platform adapter (WebView2 / WebKit / WebKitGTK)
    ///     when it is attached to the visual tree; there is no environment to pre-create.
    /// </summary>
    private static Task InitializeWebViewsAsync()
    {
        return Task.CompletedTask;
    }

    private Task<bool> EnsureWebViewReadyAsync(NativeWebView webView, string viewName)
    {
        if (_webView2Unavailable)
            return Task.FromResult(false);

        try
        {
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"Failed to initialize the {viewName} web view");
            return Task.FromResult(false);
        }
    }

    private void NavigateWebView(NativeWebView webView, string viewName, string url)
    {
        try
        {
            webView.Navigate(new Uri(url));
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"{viewName} web view navigation failed; opening the system browser instead.");
            if (!UrlService.TryOpenUrl(url))
                LogService.Warning($"Could not open '{url}' in the system browser.");
            _webView2Unavailable = true;
            _ = NotifyWebViewUnavailableAsync(viewName);
        }
    }

    private async Task NotifyWebViewUnavailableAsync(string viewName)
    {
        if (_webView2NotificationShown)
            return;

        _webView2NotificationShown = true;

        var (runtimeName, downloadUrl) = OperatingSystem.IsWindows()
            ? ("WebView2 Runtime", "https://developer.microsoft.com/en-us/microsoft-edge/webview2/")
            : OperatingSystem.IsMacOS()
                ? ("WebKit", "https://support.apple.com/guide/safari")
                : ("WebKitGTK", "https://webkitgtk.org/releases/");

        var result = await Dialogs.ShowQuestionAsync(this,
            $"The embedded {runtimeName} is not available, so the {viewName} web search tab cannot be displayed.\n\n" +
            "Would you like to open the download page?",
            "Web View Runtime Required",
            MessageIcon.Warning);

        if (result == MessageResult.Yes && !UrlService.TryOpenUrl(downloadUrl))
            await Dialogs.ShowMessageAsync(this,
                "Could not open the download page automatically.\n\nPlease copy this address into your browser:\n" +
                downloadUrl,
                "Open Browser Failed", MessageIcon.Warning);
    }

    private async Task LoadMameDataAsync()
    {
        try
        {
            var machines = await Task.Run(MameManager.LoadFromDat);
            _mameLookup = machines
                .GroupBy(static m => m.MachineName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static g => g.Key, static g => g.First().Description, StringComparer.OrdinalIgnoreCase);
            ToggleMameDescriptions.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _mameLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ToggleMameDescriptions.IsEnabled = false;
            ToggleMameDescriptions.IsChecked = false;
            LogService.Error(ex, "Failed to load MAME data");
        }
    }

    private async void AppSettingsManagerPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            switch (e.PropertyName)
            {
                case nameof(SettingsManager.BaseTheme):
                    UpdateBaseThemeMenuChecks();
                    break;
                case nameof(SettingsManager.AccentColor):
                    UpdateAccentColorChecks();
                    break;
                case nameof(SettingsManager.ImageWidth):
                case nameof(SettingsManager.ImageHeight):
                    UpdateThumbnailSizeMenuChecks();
                    break;
                case nameof(SettingsManager.SelectedSimilarityAlgorithm):
                    UpdateSimilarityAlgorithmChecks();
                    await RefreshLocalSearchForSelectionAsync();
                    break;
                case nameof(SettingsManager.SimilarityThreshold):
                    UpdateSimilarityThresholdChecks();
                    await RefreshLocalSearchForSelectionAsync();
                    break;
                case nameof(SettingsManager.IgnoreBracketedText):
                    UpdateIgnoreBracketedTextCheck();
                    await RefreshLocalSearchForSelectionAsync();
                    break;
                case nameof(SettingsManager.UseMameDescriptions):
                    UpdateMameDescriptionCheck();
                    if (_mameLookup is { Count: > 0 })
                        await RefreshMissingImagesListAsync();
                    break;
                case nameof(SettingsManager.AiAssistEnabled):
                    UpdateAiPickAvailability();
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in AppSettings_PropertyChanged");
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChangedCore?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ChangeBaseTheme_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            var theme = string.Equals(menuItem.Name, "LightTheme", StringComparison.OrdinalIgnoreCase)
                ? "Light"
                : "Dark";
            App.ChangeTheme(theme, Settings.AccentColor);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ChangeBaseTheme_Click");
        }
    }

    private void ChangeAccentColor_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            var accent = menuItem.Name?.Replace("Accent", "") ?? "Blue";
            App.ChangeTheme(Settings.BaseTheme, accent);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ChangeAccentColor_Click");
        }
    }

    private void UpdateAccentColorChecks()
    {
        try
        {
            var currentAccent = Settings.AccentColor;
            foreach (var item in MenuAccentColors.Items)
            {
                if (item is not MenuItem menuItem) continue;

                if (menuItem.Header is StackPanel sp)
                {
                    var tb = sp.Children.OfType<TextBlock>().FirstOrDefault();
                    if (tb != null)
                        menuItem.IsChecked = string.Equals(tb.Text, currentAccent, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    menuItem.IsChecked = string.Equals(menuItem.Name?.Replace("Accent", ""), currentAccent,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateAccentColorChecks");
        }
    }

    private void UpdateBaseThemeMenuChecks()
    {
        try
        {
            LightTheme.IsChecked = string.Equals(Settings.BaseTheme, "Light", StringComparison.OrdinalIgnoreCase);
            DarkTheme.IsChecked = string.Equals(Settings.BaseTheme, "Dark", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateBaseThemeMenuChecks");
        }
    }

    private void UpdateMameDescriptionCheck()
    {
        try
        {
            ToggleMameDescriptions.IsChecked = Settings.UseMameDescriptions;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateMameDescriptionCheck");
        }
    }

    private void UpdateIgnoreBracketedTextCheck()
    {
        try
        {
            ToggleIgnoreBracketedText.IsChecked = Settings.IgnoreBracketedText;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateIgnoreBracketedTextCheck");
        }
    }

    private async Task RefreshLocalSearchForSelectionAsync()
    {
        // Toggling a matching option must re-run the similarity calculation for the
        // ROM that is already selected; SelectionChanged will not fire again for it.
        if (LstMissingImages.SelectedItem is not MissingImageItem selectedItem) return;

        _findSimilarTask = RunLocalSearchAsync(selectedItem.SearchName, selectedItem.RomName);
        await _findSimilarTask;
    }

    private void UpdateAiPickAvailability()
    {
        try
        {
            IsAiPickAvailable = Settings.AiAssistEnabled && SimilarImages.Count > 0 && !IsAiBusy;
            IsAiPickApiAvailable = Settings.AiAssistEnabled && PanelImages.Count > 0 && !IsAiBusy;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateAiPickAvailability");
        }
    }

    private void DonateButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!UrlService.TryOpenUrl("https://www.purelogiccode.com/donate"))
                Dialogs.ShowMessage(this,
                    "Could not open the donation link automatically.\n\nPlease copy this address into your browser:\nhttps://www.purelogiccode.com/donate",
                    "Open Browser Failed", MessageIcon.Warning);
        }
        catch (Exception ex)
        {
            Dialogs.ShowMessage(this, $"Unable to open the donation link: {ex.Message}", "Error", MessageIcon.Error);
            LogService.Error(ex, "Error opening donation link");
        }
    }

    private async void ShowAboutWindow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ShowAboutWindow_Click");
        }
    }

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var updateInfo = await UpdateCheckService.CheckForUpdateAsync();

            if (updateInfo is { IsUpdateAvailable: true })
            {
                var choice = await Dialogs.ShowQuestionAsync(this,
                    "A new version of FindRomCover is available!\n\n" +
                    $"Current: {updateInfo.CurrentVersion}\n" +
                    $"Latest: {updateInfo.LatestVersion}\n\n" +
                    "Would you like to go to the download page?",
                    "Update Available", MessageIcon.Information);

                if (choice == MessageResult.Yes)
                    UrlService.TryOpenUrl(updateInfo.ReleaseUrl);
            }
            else
            {
                await Dialogs.ShowMessageAsync(this,
                    $"You are running the latest version (v{updateInfo.CurrentVersion}).",
                    "No Updates Available", MessageIcon.Information);
            }
        }
        catch (Exception ex)
        {
            await Dialogs.ShowMessageAsync(this, $"Unable to check for updates: {ex.Message}", "Error",
                MessageIcon.Error);
            LogService.Error(ex, "Error checking for updates");
        }
    }

    private async void ApiSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var window = new ApiSettingsWindow(Settings);
            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ApiSettings_Click");
        }
    }

    private async void AiSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var window = new AiSettingsWindow(Settings);
            await window.ShowDialog(this);
            UpdateAiPickAvailability();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in AiSettings_Click");
        }
    }

    private void ToggleDebugWindow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (App.LogWindow == null) return;

            if (ToggleDebugWindow.IsChecked) App.LogWindow.Show();
            else App.LogWindow.Hide();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ToggleDebugWindow_Click");
        }
    }

    private void ToggleMameDescriptions_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Settings.UseMameDescriptions = ToggleMameDescriptions.IsChecked;
            Settings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ToggleMameDescriptions_Click");
        }
    }

    private void ToggleIgnoreBracketedText_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Settings.IgnoreBracketedText = ToggleIgnoreBracketedText.IsChecked;
            Settings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in ToggleIgnoreBracketedText_Click");
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting)
        {
            // Allow close when explicitly exiting
            try
            {
                _findSimilarCts?.Cancel();
            }
            catch
            {
                // ignored
            }

            try
            {
                _loadMissingCts?.Cancel();
            }
            catch
            {
                // ignored
            }

            return;
        }

        // X button or Alt+F4: cancel the raw close and use the explicit shutdown path
        e.Cancel = true;
        ExitApplication();
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            ExitApplication();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in Exit_Click");
        }
    }

    private async void BtnBrowseRomFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the folder where your ROM or ISO files are stored.",
                AllowMultiple = false
            });

            var folderPath = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(folderPath))
            {
                TxtRomFolder.Text = folderPath;
                UpdateUiStateForFolderPaths();
                if (FolderPathsAreValid()) await CheckForMissingImagesSafeAsync();
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnBrowseRomFolder_Click");
        }
    }

    private async void BtnBrowseImageFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the folder where your image files are stored.",
                AllowMultiple = false
            });

            var folderPath = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(folderPath)) return;

            TxtImageFolder.Text = folderPath;
            UpdateUiStateForFolderPaths();
            Settings.LastImageFolder = folderPath;
            Settings.SaveSettings();
            if (FolderPathsAreValid()) await CheckForMissingImagesSafeAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnBrowseImageFolder_Click");
        }
    }

    private async void BtnCheckForMissingImages_ClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshMissingImagesListAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnCheckForMissingImages_Click");
        }
    }

    private async Task CheckForMissingImagesSafeAsync()
    {
        try
        {
            await RefreshMissingImagesListAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in CheckForMissingImagesCommand");
        }
    }

    private async void BtnAiBatchFill_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!Settings.AiAssistEnabled)
            {
                await Dialogs.ShowMessageAsync(this,
                    "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                    "AI Batch Fill", MessageIcon.Information);
                return;
            }

            var imageFolderPath = GetValidatedImageFolderPath();
            if (string.IsNullOrEmpty(imageFolderPath)) return;

            if (MissingImages.Count == 0)
            {
                await Dialogs.ShowMessageAsync(this, "There are no missing covers to process.", "AI Batch Fill",
                    MessageIcon.Information);
                return;
            }

            if (_loadMissingTask is { } pendingScan) await pendingScan;

            var resolvedCount = 0;
            var window = new AiBatchWindow(
                Settings,
                MissingImages.ToList(),
                imageFolderPath,
                _imageFolderWatcher != null ? _imageFolderWatcher.PreRegisterExpectedFile : null,
                TxtExtraQuery.Text?.Trim() ?? string.Empty,
                _imageFolderWatcher != null ? _imageFolderWatcher.UnregisterExpectedFile : null);

            void OnItemResolved(string romName)
            {
                resolvedCount++;
                RemoveMissingItemByName(romName);
            }

            window.ItemResolved += OnItemResolved;
            _aiBatchRunning = true;
            try
            {
                await window.ShowDialog(this);
            }
            finally
            {
                _aiBatchRunning = false;
                window.ItemResolved -= OnItemResolved;
            }

            if (resolvedCount > 0)
                StatusMessage.Text = $"AI batch fill resolved {resolvedCount} cover(s).";

            await RefreshMissingImagesListAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnAiBatchFill_Click");
        }
    }

    private void RemoveMissingItemByName(string romName)
    {
        var index = MissingImages.ToList().FindIndex(m =>
            string.Equals(m.RomName, romName, StringComparison.OrdinalIgnoreCase));

        if (index < 0) return;

        var wasSelected = index == LstMissingImages.SelectedIndex;
        MissingImages.RemoveAt(index);

        if (MissingImages.Count > 0 && (wasSelected || LstMissingImages.SelectedItem == null))
        {
            var newIndex = Math.Min(index, MissingImages.Count - 1);
            LstMissingImages.SelectedIndex = newIndex;
            LstMissingImages.ScrollIntoView(MissingImages[newIndex]);
        }

        UpdateMissingCount();
    }

    private async Task RefreshMissingImagesListAsync()
    {
        if (_loadMissingCts != null)
        {
            await _loadMissingCts.CancelAsync();
            _loadMissingCts.Dispose();
            _loadMissingCts = null;
        }

        var cts = new CancellationTokenSource();
        _loadMissingCts = cts;
        var task = LoadMissingImagesListAsync(cts.Token);
        _loadMissingTask = task;
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error refreshing missing images list.");
        }
        finally
        {
            if (_loadMissingCts == cts) _loadMissingCts = null;
            if (ReferenceEquals(_loadMissingTask, task)) _loadMissingTask = null;
            cts.Dispose();
        }
    }

    private async Task LoadMissingImagesListAsync(CancellationToken cancellationToken = default)
    {
        if (Settings.SupportedExtensions.Count == 0)
        {
            await Dialogs.ShowMessageAsync(this, "No supported file extensions loaded. Please check settings.",
                "Warning", MessageIcon.Warning);
            return;
        }

        var romFolderPath = GetValidatedRomFolderPath();
        var imageFolderPath = GetValidatedImageFolderPath();

        if (string.IsNullOrEmpty(romFolderPath) || string.IsNullOrEmpty(imageFolderPath))
        {
            await Dialogs.ShowMessageAsync(this, "Please select both ROM and Image folders.", "Warning",
                MessageIcon.Warning);
            return;
        }

        IsCheckingMissing = true;
        try
        {
            var missingResult = await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allRomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var supportedExtensionsSet = new HashSet<string>(
                    Settings.SupportedExtensions.Select(static ext => "." + ext), StringComparer.OrdinalIgnoreCase);

                var files = Directory.EnumerateFiles(romFolderPath, "*.*",
                    new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true });

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (supportedExtensionsSet.Contains(Path.GetExtension(file)))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(fileName)) allRomNames.Add(fileName);
                    }
                }

                var missing = new List<(string RomName, string SearchName)>();
                var processedCount = 0;
                foreach (var romName in allRomNames)
                {
                    if (++processedCount % 100 == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }

                    if (CoverFileResolver.FindCover(imageFolderPath, romName) == null)
                    {
                        if (Settings.UseMameDescriptions && _mameLookup != null &&
                            _mameLookup.TryGetValue(romName, out var description) && !string.IsNullOrEmpty(description))
                            missing.Add((romName, description));
                        else
                            missing.Add((romName, romName));
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                return (missing.OrderBy(static x => x.RomName, StringComparer.OrdinalIgnoreCase).ToList(),
                    allRomNames.Count);
            }, cancellationToken);

            var (missingFiles, romCount) = missingResult;

            cancellationToken.ThrowIfCancellationRequested();
            MissingImages.Clear();
            SimilarImages.Clear();

            foreach (var item in missingFiles.Select(static mf => new MissingImageItem(mf.RomName, mf.SearchName)))
                MissingImages.Add(item);

            UpdateMissingCount();
            StatusMessage.Text = romCount == 0
                ? $"No ROM files found in \"{romFolderPath}\" matching the supported extensions."
                : $"Found {romCount} ROM file(s) matching the supported extensions; " +
                  $"{missingFiles.Count} missing cover(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dialogs.ShowMessageAsync(this, $"Error checking for missing images: {ex.Message}", "Error",
                MessageIcon.Error);
            LogService.Error(ex, "Error checking for missing images");
        }
        finally
        {
            // Only the current run may clear the flag — a stale run that was cancelled
            // by a newer scan must not stop the new run's spinner.
            if (_loadMissingCts is null || cancellationToken.Equals(_loadMissingCts.Token))
                IsCheckingMissing = false;
        }
    }

    private void RemoveSelectedItem(int? index = null)
    {
        var removeIndex = index ?? LstMissingImages.SelectedIndex;
        if (removeIndex < 0 || removeIndex >= MissingImages.Count) return;

        try
        {
            MissingImages.RemoveAt(removeIndex);
            if (MissingImages.Count > 0)
            {
                var newIndex = Math.Min(removeIndex, MissingImages.Count - 1);
                LstMissingImages.SelectedIndex = newIndex;
                LstMissingImages.ScrollIntoView(MissingImages[newIndex]);
            }
            else
            {
                LblLocalSearchQuery.Content = null;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in RemoveSelectedItem");
        }

        UpdateMissingCount();
    }

    private void UpdateMissingCount()
    {
        try
        {
            LabelMissingRoms.Content = AppConstants.Messages.MissingCoversPrefix + MissingImages.Count;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateMissingCount");
        }
    }

    private void BtnRemoveSelectedItem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            RemoveSelectedItem();
            SimilarImages.Clear();
            App.AudioService.PlayClickSound();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in BtnRemoveSelectedItem_Click");
        }
    }

    private void RemoveItemFromList_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            RemoveSelectedItem();
            App.AudioService.PlayClickSound();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in RemoveItemFromList_Click");
        }
    }

    private void CopyFileName_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (LstMissingImages.SelectedItem is MissingImageItem item)
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null) _ = clipboard.SetTextAsync(item.RomName);
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in CopyFileName_Click");
        }
    }

    private async void DeleteCorrespondingRom_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (LstMissingImages.SelectedItem is not MissingImageItem selectedItem) return;

            var romFolderPath = GetValidatedRomFolderPath();
            if (string.IsNullOrEmpty(romFolderPath)) return;

            var romFilePath = await Task.Run(() =>
                FindCorrespondingRomFile(selectedItem.RomName, romFolderPath, Settings.SupportedExtensions));
            if (romFilePath == null)
            {
                await Dialogs.ShowMessageAsync(this,
                    $"Could not find a ROM or ISO file for '{selectedItem.RomName}' in the ROM folder.",
                    "File Not Found", MessageIcon.Warning);
                return;
            }

            var result = await Dialogs.ShowQuestionAsync(this,
                $"Are you sure you want to permanently delete this file?\n\n{romFilePath}\n\nThis action cannot be undone.",
                "Confirm Delete", MessageIcon.Warning);

            if (result != MessageResult.Yes) return;

            try
            {
                File.Delete(romFilePath);
                LogService.Information($"Deleted ROM file: {romFilePath}");
                RemoveSelectedItem();
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessageAsync(this, $"Failed to delete file: {ex.Message}", "Delete Error",
                    MessageIcon.Error);
                LogService.Error(ex, $"Error deleting ROM file: {romFilePath}");
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error deleting ROM file");
        }
    }

    private static string? FindCorrespondingRomFile(string fileNameWithoutExtension, string romFolderPath,
        List<string> supportedExtensions)
    {
        var extensionsWithDot = supportedExtensions
            .Select(static ext => ext.StartsWith('.') ? ext : "." + ext)
            .Where(static ext => !NonRomExtensions.Contains(ext))
            .ToArray();

        foreach (var ext in extensionsWithDot)
        {
            var romPath = Path.Combine(romFolderPath, fileNameWithoutExtension + ext);
            if (File.Exists(romPath)) return romPath;
        }

        // Try case-insensitive search
        if (!Directory.Exists(romFolderPath)) return null;

        var extensionsSet = new HashSet<string>(extensionsWithDot, StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(romFolderPath, "*.*",
                     new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
            if (extensionsSet.Contains(Path.GetExtension(file)) &&
                string.Equals(Path.GetFileNameWithoutExtension(file), fileNameWithoutExtension,
                    StringComparison.OrdinalIgnoreCase))
                return file;

        return null;
    }

    private async void EditExtensions_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SettingsWindow(Settings);
            var saved = await dialog.ShowDialog<bool>(this);
            if (saved && FolderPathsAreValid()) await CheckForMissingImagesSafeAsync();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in EditExtensions_Click");
        }
    }

    private void SetSimilarityAlgorithm_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;

            Settings.SelectedSimilarityAlgorithm = menuItem.Header?.ToString() ?? "Jaro-Winkler Distance";
            Settings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in SetSimilarityAlgorithm_Click");
        }
    }

    private void UpdateSimilarityAlgorithmChecks()
    {
        try
        {
            foreach (var item in MenuSimilarityAlgorithms.Items)
                if (item is MenuItem menuItem)
                    menuItem.IsChecked = string.Equals(menuItem.Header?.ToString(),
                        Settings.SelectedSimilarityAlgorithm, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateSimilarityAlgorithmChecks");
        }
    }

    private void SetSimilarityThreshold_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem clickedItem) return;

            var headerText = clickedItem.Header?.ToString()?.Replace("%", "") ?? "70";
            if (double.TryParse(headerText, CultureInfo.InvariantCulture, out var rate))
            {
                Settings.SimilarityThreshold = rate;
                Settings.SaveSettings();
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in SetSimilarityThreshold_Click");
        }
    }

    private void UpdateSimilarityThresholdChecks()
    {
        try
        {
            var currentThreshold = Settings.SimilarityThreshold;
            foreach (var item in MySimilarityMenu.Items)
            {
                if (item is not MenuItem menuItem) continue;

                var thresholdString = menuItem.Header?.ToString()?.Replace("%", "") ?? "70";
                if (double.TryParse(thresholdString, NumberStyles.Any, CultureInfo.InvariantCulture,
                        out var menuItemThreshold))
                    menuItem.IsChecked = Math.Abs(menuItemThreshold - currentThreshold) < 0.001;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateSimilarityThresholdChecks");
        }
    }

    private void SetThumbnailSize_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem menuItem) return;
            if (menuItem.Tag is not int size &&
                !int.TryParse(menuItem.Tag?.ToString(), CultureInfo.InvariantCulture, out size))
                return;

            Settings.ImageWidth = size;
            Settings.ImageHeight = size;
            Settings.SaveSettings();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in SetThumbnailSize_Click");
        }
    }

    private void UpdateThumbnailSizeMenuChecks()
    {
        try
        {
            var currentWidth = Settings.ImageWidth;
            foreach (var item in ImageSizeMenu.Items)
            {
                if (item is not MenuItem menuItem) continue;

                if (menuItem.Tag is int size ||
                    int.TryParse(menuItem.Tag?.ToString(), CultureInfo.InvariantCulture, out size))
                    menuItem.IsChecked = size == currentWidth;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateThumbnailSizeMenuChecks");
        }
    }

    private void LstMissingImages_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Delete)
            {
                RemoveSelectedItem();
                App.AudioService.PlayClickSound();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in LstMissingImages_KeyDown");
        }
    }

    private void TxtRomFolder_TextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            UpdateUiStateForFolderPaths();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtRomFolder_TextChanged");
        }
    }

    private void TxtImageFolder_TextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            UpdateUiStateForFolderPaths();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtImageFolder_TextChanged");
        }
    }

    private void TxtRomFolder_LostFocus(object? sender, RoutedEventArgs e)
    {
        try
        {
            UpdateUiStateForFolderPaths();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtRomFolder_LostFocus");
        }
    }

    private void TxtImageFolder_LostFocus(object? sender, RoutedEventArgs e)
    {
        try
        {
            UpdateUiStateForFolderPaths();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtImageFolder_LostFocus");
        }
    }

    private void TxtRomFolder_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Enter)
            {
                UpdateUiStateForFolderPaths();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtRomFolder_KeyDown");
        }
    }

    private void TxtImageFolder_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.Enter)
            {
                UpdateUiStateForFolderPaths();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in TxtImageFolder_KeyDown");
        }
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key != Key.F8) return;

            var filePath = ScreenshotService.CaptureWindow(this);
            StatusMessage.Text = filePath is not null
                ? $"Screenshot saved: {filePath}"
                : "Screenshot failed. Check the log for details.";

            e.Handled = true;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in MainWindow_KeyDown");
        }
    }

    private string? GetValidatedImageFolderPath(bool showWarning = true)
    {
        return ValidateFolderPath(TxtImageFolder.Text?.Trim(), "Image", showWarning);
    }

    private string? GetValidatedRomFolderPath(bool showWarning = true)
    {
        return ValidateFolderPath(TxtRomFolder.Text?.Trim(), "ROM", showWarning);
    }

    private string? ValidateFolderPath(string? path, string folderType, bool showWarning)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Directory.Exists(path)) return path;

        if (showWarning)
            Dialogs.ShowMessage(this,
                $"The {folderType.ToLowerInvariant()} folder path '{path}' is invalid or does not exist.",
                $"Invalid {folderType} Folder", MessageIcon.Warning);

        return null;
    }

    private bool FolderPathsAreValid()
    {
        return !string.IsNullOrEmpty(TxtRomFolder.Text?.Trim()) &&
               Directory.Exists(TxtRomFolder.Text.Trim()) &&
               !string.IsNullOrEmpty(TxtImageFolder.Text?.Trim()) &&
               Directory.Exists(TxtImageFolder.Text.Trim());
    }

    private void UpdateUiStateForFolderPaths()
    {
        try
        {
            var romText = TxtRomFolder.Text?.Trim() ?? string.Empty;
            var imageText = TxtImageFolder.Text?.Trim() ?? string.Empty;
            var romPathValid = !string.IsNullOrEmpty(romText) && Directory.Exists(romText);
            var imagePathValid = !string.IsNullOrEmpty(imageText) && Directory.Exists(imageText);
            BtnCheckForMissingImages.IsEnabled = romPathValid && imagePathValid;
            LstMissingImages.IsEnabled = romPathValid && imagePathValid;
            if (!romPathValid || !imagePathValid)
            {
                MissingImages.Clear();
                SimilarImages.Clear();
                UpdateMissingCount();
            }

            StartImageFolderWatcher(imagePathValid ? imageText : null);

            if (CheckForMissingImagesCommand is DelegateCommand delegateCommand)
                delegateCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in UpdateUiStateForFolderPaths");
        }
    }

    private void StartImageFolderWatcher(string? folderPath)
    {
        var samePath = string.Equals(_watchedFolderPath, folderPath, StringComparison.OrdinalIgnoreCase);
        var watcherAlive = _imageFolderWatcher is { IsWatching: true };

        if (samePath && watcherAlive)
            return;

        _watchedFolderPath = folderPath;

        _imageFolderWatcher?.Stop();

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            LogService.Information(
                $"StartImageFolderWatcher: skipped (path='{folderPath}', exists={Directory.Exists(folderPath ?? "")})");
            return;
        }

        _imageFolderWatcher ??= new ImageFolderWatcher();
        _imageFolderWatcher.ImageFound -= OnImageFolderImageFound;
        _imageFolderWatcher.ImageFound += OnImageFolderImageFound;
        _imageFolderWatcher.ConversionFailed -= OnImageFolderConversionFailed;
        _imageFolderWatcher.ConversionFailed += OnImageFolderConversionFailed;
        _imageFolderWatcher.VerificationFailed -= OnImageFolderVerificationFailed;
        _imageFolderWatcher.VerificationFailed += OnImageFolderVerificationFailed;
        _imageFolderWatcher.VerifyImageAsync = VerifyImageWithAiAsync;
        try
        {
            if (_imageFolderWatcher.Start(folderPath))
                LogService.Information($"StartImageFolderWatcher: watcher started for '{folderPath}'");
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, $"StartImageFolderWatcher: cannot watch folder '{folderPath}' — automatic detection disabled");
        }
    }

    private void OnImageFolderImageFound(string fileNameWithoutExtension)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            var index = MissingImages.ToList().FindIndex(m =>
                string.Equals(m.RomName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                LogService.Information(
                    $"ImageFolderWatcher: image '{fileNameWithoutExtension}' does not match any missing ROM name — skipping");
                StatusMessage.Text =
                    $"Image '{fileNameWithoutExtension}.png' was saved but doesn't match any missing ROM name.";
                return;
            }

            RemoveSelectedItem(index);
            LogService.Information($"Auto-removed '{fileNameWithoutExtension}' from missing images.");
        });
    }

    private void OnImageFolderConversionFailed(string filePath, string errorMessage)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            Dialogs.ShowMessage(this,
                $"Failed to convert image:\n\n{Path.GetFileName(filePath)}\n\n{errorMessage}",
                "Image Conversion Failed",
                MessageIcon.Warning);
        });
    }

    private void OnImageFolderVerificationFailed(string filePath, string romName)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage.Text =
                $"AI thinks '{Path.GetFileName(filePath)}' is not a cover for '{romName}' — rename skipped.";
            Dialogs.ShowMessage(this,
                $"The AI model does not think this image is a cover for '{romName}'.\n\n" +
                $"The file was left as-is:\n{filePath}\n\n" +
                "You can still assign it manually or save a different image.",
                "AI Verification", MessageIcon.Warning);
        });
    }

    private async Task<bool> VerifyImageWithAiAsync(string filePath, string romName,
        CancellationToken cancellationToken)
    {
        if (_disposed || !Settings.AiAssistEnabled || !Settings.AiVerifyOnSave) return true;

        try
        {
            _aiAssistService ??= new AiAssistService(Settings);
            var result = await _aiAssistService.VerifyAsync(romName, ResolveSearchName(romName), filePath,
                cancellationToken);

            if (result is null || result.IsMatch) return true;

            LogService.Warning(
                $"AI verification rejected '{Path.GetFileName(filePath)}' for '{romName}' " +
                $"(confidence {result.Confidence:P0}): {result.Reason}");
            return false;
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI verification failed; proceeding without verification.");
            return true;
        }
    }

    private async Task<AiVerificationResult?> TryVerifyImageAsync(string romName, string imagePath)
    {
        if (_disposed || !Settings.AiAssistEnabled || !Settings.AiVerifyOnSave) return null;

        try
        {
            _aiAssistService ??= new AiAssistService(Settings);
            return await _aiAssistService.VerifyAsync(romName, ResolveSearchName(romName), imagePath,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "AI verification failed; proceeding without verification.");
            return null;
        }
    }

    private string ResolveSearchName(string romName)
    {
        if (!Settings.UseMameDescriptions || _mameLookup is not { Count: > 0 }) return romName;

        return _mameLookup.TryGetValue(romName, out var description) && !string.IsNullOrEmpty(description)
            ? description
            : romName;
    }

    private async void SearchTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (StatusSearchEngine == null) return;

            var tab = SearchTabControl.SelectedIndex;
            StatusSearchEngine.Text = tab switch
            {
                0 => "Local Files",
                1 => "Google Web",
                2 => "Bing Web",
                3 => "Google API",
                _ => "Unknown"
            };

            if (tab == 3 && string.IsNullOrWhiteSpace(Settings.GoogleKey))
            {
                await Dialogs.ShowMessageAsync(this,
                    "A Google API key is required to use the Google API search.\n\nPlease enter your API key in the settings window that will open.",
                    "API Key Required",
                    MessageIcon.Information);

                var apiSettingsWindow = new ApiSettingsWindow(Settings);
                await apiSettingsWindow.ShowDialog(this);

                if (string.IsNullOrWhiteSpace(Settings.GoogleKey))
                {
                    StatusMessage.Text = "API key not set. Google API search is unavailable.";
                    return;
                }
            }

            // If an item is already selected, trigger the search for the new tab
            if (LstMissingImages.SelectedItem is MissingImageItem selectedItem)
                TriggerActiveTabSearch(selectedItem.SearchName);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error in SearchTabControl_SelectionChanged");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Dispose();
        _ = SettleBackgroundTasksAsync();
    }

    /// <summary>
    ///     Awaits the cancelled search/scan tasks after the window closes so they do not
    ///     keep running against disposed state. Never blocks the UI thread; each task is
    ///     bounded by a timeout.
    /// </summary>
    private async Task SettleBackgroundTasksAsync()
    {
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
            if (_findSimilarTask != null) await Task.WhenAny(_findSimilarTask, timeout);
        }
        catch
        {
            /* ignored - task was cancelled */
        }

        try
        {
            if (_loadMissingTask != null) await Task.WhenAny(_loadMissingTask, timeout);
        }
        catch
        {
            /* ignored - task was cancelled */
        }
    }
}
