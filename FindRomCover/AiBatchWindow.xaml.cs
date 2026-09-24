using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;

namespace FindRomCover;

public partial class AiBatchWindow
{
    private const int DefaultMaxItems = 25;
    private const int HardMaxItems = 500;

    private readonly string? _extraQuery;
    private readonly string _imageFolderPath;
    private readonly List<MissingImageItem> _items;
    private readonly Action<string>? _preRegisterExpectedFile;
    private readonly Action<string>? _unregisterExpectedFile;
    private readonly AiQueryHistory _queryHistory = new();
    private readonly SettingsManager _settings;
    private CancellationTokenSource? _cts;
    private bool _running;

    public AiBatchWindow(
        SettingsManager settings,
        IEnumerable<MissingImageItem> items,
        string imageFolderPath,
        Action<string>? preRegisterExpectedFile = null,
        string? extraQuery = null,
        Action<string>? unregisterExpectedFile = null)
    {
        InitializeComponent();

        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        _imageFolderPath = imageFolderPath;
        _preRegisterExpectedFile = preRegisterExpectedFile;
        _extraQuery = extraQuery;
        _unregisterExpectedFile = unregisterExpectedFile;

        Closing += AiBatchWindow_Closing;

        var hasGoogleKey = !string.IsNullOrWhiteSpace(settings.GoogleKey);
        ChkUseApiFallback.IsChecked = hasGoogleKey;
        ChkUseApiFallback.IsEnabled = hasGoogleKey;
        ChkSkipQueried.IsChecked = settings.AiSkipPreviouslyQueried;

        Progress.Maximum = Math.Max(1, _items.Count);

        var queriedPaths = _queryHistory.GetQueriedPathSet();
        var alreadyQueried = _items.Count(item =>
            queriedPaths.Contains(AiQueryHistory.NormalizeKey(TargetPathFor(item))));
        TxtSummary.Text = alreadyQueried == 0
            ? $"{_items.Count} missing cover(s) available."
            : $"{_items.Count} missing cover(s) available ({alreadyQueried} already queried).";
    }

    public event Action<string>? ItemResolved;

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_running) return;

            if (!_settings.AiAssistEnabled)
            {
                MessageBox.Show(
                    "AI Assist is disabled.\n\nEnable it in Settings > AI Settings... to use this feature.",
                    "AI Batch Fill", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var items = _items.Take(ParseMaxItems()).ToList();
            if (items.Count == 0) return;

            _running = true;
            BtnStart.IsEnabled = false;
            BtnClose.IsEnabled = false;
            BtnCancel.IsEnabled = true;
            LstResults.Items.Clear();
            Progress.Value = 0;

            _cts = new CancellationTokenSource();
            var cancellationToken = _cts.Token;

            try
            {
                using var aiAssist = new AiAssistService(_settings);
                var service = new AiBatchFillService(_settings, aiAssist, _preRegisterExpectedFile, null,
                    _unregisterExpectedFile);
                var progress = new Progress<AiBatchItemResult>(OnItemCompleted);

                var results = await service.RunAsync(
                    items,
                    _imageFolderPath,
                    ChkUseApiFallback.IsChecked == true,
                    _extraQuery,
                    progress,
                    cancellationToken,
                    ChkSkipQueried.IsChecked == true);

                ShowSummary(results);
            }
            catch (OperationCanceledException)
            {
                TxtSummary.Text = "Batch canceled. Already saved covers were kept.";
            }
            catch (Exception ex)
            {
                LogService.Warning(ex, "AI batch fill failed.");
                MessageBox.Show(ex.Message, "AI Batch Fill", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _running = false;
                _cts?.Dispose();
                _cts = null;
                BtnStart.IsEnabled = true;
                BtnClose.IsEnabled = true;
                BtnCancel.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Error in method BtnStart_Click");
        }
    }

    private void OnItemCompleted(AiBatchItemResult result)
    {
        Progress.Value = Math.Min(Progress.Maximum, Progress.Value + 1);

        LstResults.Items.Add($"{result.RomName}: {result.Outcome} — {result.Message}");
        if (LstResults.Items.Count > 0) LstResults.ScrollIntoView(LstResults.Items[^1]);

        if (result.Outcome is AiBatchOutcome.FilledFromLocal or AiBatchOutcome.FilledFromApi
            or AiBatchOutcome.SkippedAlreadyExists)
            ItemResolved?.Invoke(result.RomName);
    }

    private void ShowSummary(List<AiBatchItemResult> results)
    {
        var filled = results.Count(static r =>
            r.Outcome is AiBatchOutcome.FilledFromLocal or AiBatchOutcome.FilledFromApi);
        var skipped = results.Count(static r =>
            r.Outcome is AiBatchOutcome.SkippedLowConfidence or AiBatchOutcome.SkippedAlreadyExists
                or AiBatchOutcome.SkippedAlreadyQueried or AiBatchOutcome.NoCandidates);
        var failed = results.Count(static r => r.Outcome == AiBatchOutcome.Failed);

        TxtSummary.Text = $"Done: {filled} filled, {skipped} skipped, {failed} failed.";
        LogService.Information($"AI batch fill finished: {filled} filled, {skipped} skipped, {failed} failed.");
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cts?.Cancel();
            TxtSummary.Text = "Canceling after the current item...";
        }
        catch (Exception ex)
        {
            LogService.Warning(ex, "Error canceling AI batch fill.");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;

        Close();
    }

    private void AiBatchWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Closing the window (X button, Alt+F4) while a batch is running must stop the
        // batch — otherwise downloads/saves keep running against a dead window. Keep the
        // window open until the current item finishes so the caller does not rescan
        // while a cover is still being written.
        if (!_running) return;

        e.Cancel = true;
        _cts?.Cancel();
        BtnClose.IsEnabled = false;
        TxtSummary.Text = "Canceling after the current item...";
    }

    private string TargetPathFor(MissingImageItem item)
    {
        return Path.Combine(_imageFolderPath, SearchQueryHelper.SanitizeFileName(item.RomName) + ".png");
    }

    private int ParseMaxItems()
    {
        if (!int.TryParse(TxtMaxItems.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            value = DefaultMaxItems;

        return Math.Clamp(value, 1, HardMaxItems);
    }
}
