using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class SoundEffectEntryEditorDialog : Window
{
    private static readonly string[] AllowedRepeatModes = ["None", "RepeatCount", "RepeatForDuration", "UntilCanceled"];
    private static readonly string[] AllowedReplayPolicies = ["PlayAgain", "CancelPreviousAtNextPlay", "IgnoreIfAlreadyPlaying"];
    private static readonly System.Windows.Media.Brush PreviewStatusNeutralBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x55, 0x63));
    private static readonly System.Windows.Media.Brush PreviewStatusActiveBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1D, 0x4E, 0x89));
    private static readonly System.Windows.Media.Brush PreviewStatusSuccessBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0x65, 0x34));
    private static readonly System.Windows.Media.Brush PreviewStatusWarningBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB4, 0x53, 0x09));
    private static readonly System.Windows.Media.Brush PreviewStatusErrorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB9, 0x1C, 0x1C));
    private const string DefaultCategory = "General";
    private const int DefaultLoopPreviewDurationMs = 4000;
    private const int DefaultPlaybackWindowMs = 2000;
    private const int MaxPreviewSessionMs = 15000;
    private const int PreviewTailGuardMs = 80;

    private CancellationTokenSource? _previewCancellation;
    private bool _isPreviewPlaying;
    private int _assetMetadataRequestVersion;
    private int? _resolvedAssetDurationMs;
    private MediaPlayer? _previewPlayer;
    private string? _previewPlayerPath;
    private readonly Action<string, string>? _previewStatusSink;

    internal readonly record struct RepeatModeEditorState(
        bool UsesRepeatSettings,
        bool UsesCount,
        bool UsesDuration,
        bool UsesIntervalAndCooldown);

    private enum PreviewStatusSeverity
    {
        Neutral,
        Active,
        Success,
        Warning,
        Error
    }

    public SoundEffectEntryEditorDialog(
        SoundEffectLibraryEntry initialEntry,
        IReadOnlyList<string>? categorySuggestions = null,
        Action<string, string>? previewStatusSink = null)
    {
        InitializeComponent();
        _previewStatusSink = previewStatusSink;

        SoundEffectIdTextBox.Text = (initialEntry.SoundEffectId == Guid.Empty ? Guid.NewGuid() : initialEntry.SoundEffectId).ToString("D");
        SoundEffectKeyTextBox.Text = initialEntry.SoundEffectKey ?? string.Empty;
        DisplayNameTextBox.Text = initialEntry.DisplayName ?? string.Empty;
        var categories = BuildCategorySuggestions(initialEntry.Category, categorySuggestions);
        CategoryComboBox.ItemsSource = categories;
        CategoryComboBox.Text = ResolveCategoryForSave(initialEntry.Category);
        AssetRefTextBox.Text = initialEntry.AssetRef ?? string.Empty;
        SoundEffectLaneComboBox.ItemsSource = Enum.GetValues<SoundEffectLane>();
        SoundEffectLaneComboBox.SelectedItem = initialEntry.SoundEffectLane;
        RepeatModeComboBox.ItemsSource = AllowedRepeatModes;
        ReplayPolicyComboBox.ItemsSource = AllowedReplayPolicies;
        var normalizedRepeatMode = NormalizeRepeatMode(initialEntry.RepeatMode);
        RepeatModeComboBox.SelectedItem = AllowedRepeatModes.Contains(normalizedRepeatMode, StringComparer.OrdinalIgnoreCase)
            ? normalizedRepeatMode
            : "None";
        var normalizedReplayPolicy = NormalizeReplayPolicy(initialEntry.ReplayPolicy);
        ReplayPolicyComboBox.SelectedItem = AllowedReplayPolicies.Contains(normalizedReplayPolicy, StringComparer.OrdinalIgnoreCase)
            ? normalizedReplayPolicy
            : "PlayAgain";
        BaseVolumeDbTextBox.Text = FormatNullableDouble(initialEntry.BaseVolumeDb);
        FadeInMsTextBox.Text = FormatNullableInt(initialEntry.FadeInMs);
        FadeOutMsTextBox.Text = FormatNullableInt(initialEntry.FadeOutMs);
        RepeatCountTextBox.Text = FormatNullableInt(initialEntry.RepeatCount);
        StartDelayMsTextBox.Text = FormatNullableInt(initialEntry.StartDelayMs);
        RepeatIntervalMsTextBox.Text = FormatNullableInt(initialEntry.RepeatIntervalMs);
        MaxPlayDurationMsTextBox.Text = FormatNullableInt(initialEntry.MaxPlayDurationMs);
        RepeatDurationMsTextBox.Text = FormatNullableInt(initialEntry.RepeatDurationMs);
        RepeatCooldownMsTextBox.Text = FormatNullableInt(initialEntry.RepeatCooldownMs);
        _resolvedAssetDurationMs = NormalizeDurationMs(initialEntry.DurationMs);
        ConcurrencyGroupTextBox.Text = initialEntry.ConcurrencyGroup ?? string.Empty;
        ConcurrencyGroupImportanceTextBox.Text = FormatNullableInt(initialEntry.ConcurrencyGroupImportance);
        ImportanceTextBox.Text = FormatNullableInt(initialEntry.Importance);
        RefreshRepeatModeEditorState();
        ResetPreviewProgress();
        _ = RefreshAssetDurationAsync();
        _ = PreparePreviewPlayerFromCurrentAssetAsync();
    }

    public SoundEffectLibraryEntry Entry { get; private set; } = new();

    private async void Preview_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isPreviewPlaying)
        {
            return;
        }

        var assetRef = AssetRefTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(assetRef))
        {
            SetPreviewUiState(isPlaying: false, statusText: "Select an asset path before previewing.", PreviewStatusSeverity.Warning);
            return;
        }

        if (!TryResolveExistingAudioPath(assetRef, out var resolvedPath))
        {
            SetPreviewUiState(isPlaying: false, statusText: "Asset file was not found. Use Browse to select an existing audio file.", PreviewStatusSeverity.Warning);
            return;
        }

        if (!TryReadPreviewSettings(out var settings, out var errorMessage))
        {
            SetPreviewUiState(isPlaying: false, statusText: errorMessage, PreviewStatusSeverity.Warning);
            return;
        }

        _previewCancellation = new CancellationTokenSource();
        SetPreviewUiState(isPlaying: true, statusText: "Playing...", PreviewStatusSeverity.Active);

        try
        {
            await RunPreviewSequenceAsync(resolvedPath, settings, _previewCancellation.Token);
            SetPreviewUiState(isPlaying: false, statusText: "Preview complete", PreviewStatusSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            SetPreviewUiState(isPlaying: false, statusText: "Preview stopped", PreviewStatusSeverity.Warning);
        }
        catch (Exception ex)
        {
            SetPreviewUiState(isPlaying: false, statusText: $"Preview failed: {ex.Message}", PreviewStatusSeverity.Error);
        }
        finally
        {
            _previewCancellation?.Dispose();
            _previewCancellation = null;
        }
    }

    private void StopPreview_OnClick(object sender, RoutedEventArgs e)
    {
        CancelPreview();
    }

    private void AssetRefTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ = RefreshAssetDurationAsync();
        _ = PreparePreviewPlayerFromCurrentAssetAsync();
    }

    private void RepeatModeComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshRepeatModeEditorState();
    }

    private void BrowseAssetRef_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Wave files (*.wav)|*.wav|Audio files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Select Sound Effect Asset"
        };

        var currentPath = AssetRefTextBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            try
            {
                if (File.Exists(currentPath))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                    dialog.FileName = Path.GetFileName(currentPath);
                }
            }
            catch
            {
                // Ignore malformed paths and let the picker use defaults.
            }
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AssetRefTextBox.Text = AssetSourcePathResolver.NormalizeForPersistence(dialog.FileName, projectRootFolder: null);
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!Guid.TryParse(SoundEffectIdTextBox.Text.Trim(), out var soundEffectId) || soundEffectId == Guid.Empty)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid non-empty GUID for SoundEffectId.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var soundEffectKey = SoundEffectKeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(soundEffectKey))
        {
            System.Windows.MessageBox.Show(this, "SoundEffectKey is required.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var displayName = DisplayNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            System.Windows.MessageBox.Show(this, "Display Name is required.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var category = ResolveCategoryForSave(CategoryComboBox.Text);

        var assetRef = AssetSourcePathResolver.NormalizeForPersistence(AssetRefTextBox.Text, projectRootFolder: null);
        if (string.IsNullOrWhiteSpace(assetRef))
        {
            System.Windows.MessageBox.Show(this, "Asset Ref is required.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var repeatMode = NormalizeRepeatMode(RepeatModeComboBox.SelectedItem as string);
        if (!AllowedRepeatModes.Contains(repeatMode, StringComparer.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this, "Repeat Mode is invalid.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var replayPolicy = NormalizeReplayPolicy(ReplayPolicyComboBox.SelectedItem as string);
        if (!AllowedReplayPolicies.Contains(replayPolicy, StringComparer.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this, "Replay Policy is invalid.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SoundEffectLaneComboBox.SelectedItem is not SoundEffectLane soundEffectLane)
        {
            System.Windows.MessageBox.Show(this, "Lane is invalid.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryParseNullableDouble(BaseVolumeDbTextBox.Text, out var baseVolumeDb)
            || !TryParseNullableInt(FadeInMsTextBox.Text, out var fadeInMs)
            || !TryParseNullableInt(FadeOutMsTextBox.Text, out var fadeOutMs)
            || !TryParseNullableInt(RepeatCountTextBox.Text, out var repeatCount)
            || !TryParseNullableInt(StartDelayMsTextBox.Text, out var startDelayMs)
            || !TryParseNullableInt(RepeatIntervalMsTextBox.Text, out var repeatIntervalMs)
            || !TryParseNullableInt(MaxPlayDurationMsTextBox.Text, out var maxPlayDurationMs)
            || !TryParseNullableInt(RepeatDurationMsTextBox.Text, out var repeatDurationMs)
            || !TryParseNullableInt(RepeatCooldownMsTextBox.Text, out var repeatCooldownMs)
            || !TryParseNullableInt(ConcurrencyGroupImportanceTextBox.Text, out var concurrencyGroupImportance)
            || !TryParseNullableInt(ImportanceTextBox.Text, out var importance))
        {
            System.Windows.MessageBox.Show(this, "One or more numeric fields are invalid.", "Sound Effect Entry", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        NormalizeRepeatFieldsForSave(
            repeatMode,
            ref repeatCount,
            ref repeatDurationMs,
            ref repeatIntervalMs,
            ref repeatCooldownMs);

        Entry = new SoundEffectLibraryEntry
        {
            SoundEffectId = soundEffectId,
            SoundEffectKey = soundEffectKey,
            DisplayName = displayName,
            Category = category,
            AssetRef = assetRef,
            SoundEffectLane = soundEffectLane,
            RepeatMode = repeatMode,
            ReplayPolicy = replayPolicy,
            BaseVolumeDb = baseVolumeDb,
            FadeInMs = fadeInMs,
            FadeOutMs = fadeOutMs,
            RepeatCount = repeatCount,
            StartDelayMs = startDelayMs,
            RepeatIntervalMs = repeatIntervalMs,
            DurationMs = ResolveDurationMsForPersistence(assetRef),
            MaxPlayDurationMs = maxPlayDurationMs,
            RepeatDurationMs = repeatDurationMs,
            RepeatCooldownMs = repeatCooldownMs,
            ConcurrencyGroup = NormalizeNullableText(ConcurrencyGroupTextBox.Text),
            ConcurrencyGroupImportance = concurrencyGroupImportance,
            Importance = importance
        };

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        CancelPreview();
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelPreview();
        ClosePreviewPlayer();
        base.OnClosed(e);
    }

    private static string FormatNullableInt(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatNullableDouble(double? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static bool TryParseNullableInt(string text, out int? value)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return true;
        }

        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseNullableDouble(string text, out double? value)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = null;
            return true;
        }

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = null;
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? NormalizeNullableText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    internal static string ResolveCategoryForSave(string? category)
    {
        var trimmed = category?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed) ? DefaultCategory : trimmed;
    }

    internal static void NormalizeRepeatFieldsForSave(
        string repeatMode,
        ref int? repeatCount,
        ref int? repeatDurationMs,
        ref int? repeatIntervalMs,
        ref int? repeatCooldownMs)
    {
        var normalizedMode = NormalizeRepeatMode(repeatMode);

        if (string.Equals(normalizedMode, "None", StringComparison.OrdinalIgnoreCase))
        {
            repeatCount = null;
            repeatDurationMs = null;
            repeatIntervalMs = null;
            repeatCooldownMs = null;
            return;
        }

        if (string.Equals(normalizedMode, "RepeatCount", StringComparison.OrdinalIgnoreCase))
        {
            repeatDurationMs = null;
            return;
        }

        if (string.Equals(normalizedMode, "RepeatForDuration", StringComparison.OrdinalIgnoreCase))
        {
            repeatCount = null;
            return;
        }

        if (string.Equals(normalizedMode, "UntilCanceled", StringComparison.OrdinalIgnoreCase))
        {
            repeatCount = null;
            repeatDurationMs = null;
        }
    }

    private void RefreshRepeatModeEditorState()
    {
        var state = ResolveRepeatModeEditorState(RepeatModeComboBox.SelectedItem as string);

        RepeatSettingsBorder.Opacity = state.UsesRepeatSettings ? 1.0 : 0.65;

        SetRepeatFieldState(RepeatCountLabel, RepeatCountTextBox, state.UsesCount);
        SetRepeatFieldState(RepeatIntervalLabel, RepeatIntervalMsTextBox, state.UsesIntervalAndCooldown);
        SetRepeatFieldState(RepeatDurationLabel, RepeatDurationMsTextBox, state.UsesDuration);
        SetRepeatFieldState(RepeatCooldownLabel, RepeatCooldownMsTextBox, state.UsesIntervalAndCooldown);
    }

    internal static RepeatModeEditorState ResolveRepeatModeEditorState(string? repeatMode)
    {
        var normalizedMode = NormalizeRepeatMode(repeatMode);
        var usesRepeatSettings = !string.Equals(normalizedMode, "None", StringComparison.OrdinalIgnoreCase);
        var usesCount = string.Equals(normalizedMode, "RepeatCount", StringComparison.OrdinalIgnoreCase);
        var usesDuration = string.Equals(normalizedMode, "RepeatForDuration", StringComparison.OrdinalIgnoreCase);
        return new RepeatModeEditorState(
            usesRepeatSettings,
            usesCount,
            usesDuration,
            usesRepeatSettings);
    }

    private static void SetRepeatFieldState(System.Windows.Controls.TextBlock label, System.Windows.Controls.TextBox textBox, bool enabled)
    {
        label.IsEnabled = enabled;
        textBox.IsEnabled = enabled;
    }

    internal static IReadOnlyList<string> BuildCategorySuggestions(
        string? currentCategory,
        IReadOnlyList<string>? categorySuggestions)
    {
        var values = new List<string>();

        void Add(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (values.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            values.Add(normalized);
        }

        Add(DefaultCategory);
        Add(currentCategory);

        foreach (var suggestion in categorySuggestions ?? Array.Empty<string>())
        {
            Add(suggestion);
        }

        return values
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => string.Equals(value, DefaultCategory, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
    }

    private async Task RunPreviewSequenceAsync(string path, PreviewPlaybackSettings settings, CancellationToken cancellationToken)
    {
        var player = await EnsurePreviewPlayerReadyAsync(path, cancellationToken);
        var naturalDurationMs = player.NaturalDuration.HasTimeSpan
            ? Math.Max(1, (int)Math.Round(player.NaturalDuration.TimeSpan.TotalMilliseconds))
            : (int?)null;
        var wavDurationMs = TryGetWavHeaderDurationMs(path);
        var sourceDurationMs = ResolvePreviewSourceDurationMs(naturalDurationMs, wavDurationMs);
        var passDurationMs = ResolvePassDurationMs(settings, sourceDurationMs);
        var overallTargetMs = ResolveOverallTargetDurationMs(settings, passDurationMs);
        var repeatMode = NormalizeRepeatMode(settings.RepeatMode);
        var baseVolume = ConvertDbToLinearVolume(settings.BaseVolumeDb);

        UpdatePassProgress(0, passDurationMs);
        UpdateOverallProgress(0, overallTargetMs);

        if (settings.StartDelayMs > 0)
        {
            PreviewStatusTextBlock.Text = $"Starting in {settings.StartDelayMs} ms...";
            await DelayWithProgressAsync(
                settings.StartDelayMs,
                elapsedInSegment => UpdateOverallProgress(elapsedInSegment, overallTargetMs),
                cancellationToken);
        }

        var totalElapsedMs = settings.StartDelayMs;
        var iterations = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            iterations++;
            PreviewStatusTextBlock.Text = $"Playing pass {iterations}...";

            var (iterationFadeInMs, iterationFadeOutMs) = ResolvePreviewIterationFade(
                repeatMode,
                iterations,
                settings.RepeatCount,
                settings.FadeInMs,
                settings.FadeOutMs);

            var playbackMs = await PlaySinglePassAsync(
                player,
                baseVolume,
                passDurationMs,
                iterationFadeInMs,
                iterationFadeOutMs,
                cancellationToken,
                passElapsedMs =>
                {
                    UpdatePassProgress(passElapsedMs, passDurationMs);
                    UpdateOverallProgress(totalElapsedMs + passElapsedMs, overallTargetMs);
                });

            UpdatePassProgress(passDurationMs, passDurationMs);
            totalElapsedMs += playbackMs;
            UpdateOverallProgress(totalElapsedMs, overallTargetMs);

            if (!ShouldContinuePlayback(settings, iterations, totalElapsedMs))
            {
                break;
            }

            var gapMs = ResolvePreviewGapMs(
                settings.RepeatIntervalMs,
                settings.RepeatCooldownMs,
                iterations);

            if (gapMs > 0)
            {
                PreviewStatusTextBlock.Text = $"Waiting {gapMs} ms...";
                var cooldownStart = totalElapsedMs;
                await DelayWithProgressAsync(
                    gapMs,
                    elapsedInSegment => UpdateOverallProgress(cooldownStart + elapsedInSegment, overallTargetMs),
                    cancellationToken);
                totalElapsedMs += gapMs;
                UpdateOverallProgress(totalElapsedMs, overallTargetMs);
            }
        }
    }

    private static bool ShouldContinuePlayback(PreviewPlaybackSettings settings, int iterations, int elapsedMs)
    {
        switch (settings.RepeatMode)
        {
            case "None":
                return false;
            case "RepeatCount":
                return iterations < settings.RepeatCount;
            case "RepeatForDuration":
                return elapsedMs < settings.RepeatDurationMs;
            case "UntilCanceled":
                return elapsedMs < settings.RepeatDurationMs;
            default:
                return false;
        }
    }

    private static async Task<int> PlaySinglePassAsync(
        MediaPlayer player,
        double baseVolume,
        int passDurationMs,
        int fadeInMs,
        int fadeOutMs,
        CancellationToken cancellationToken,
        Action<int> onElapsed)
    {
        var effectivePassMs = Math.Max(1, passDurationMs);
        player.Stop();
        player.Position = TimeSpan.Zero;
        player.Volume = fadeInMs > 0 ? 0d : baseVolume;

        var mediaEnded = false;
        EventHandler endedHandler = (_, _) => mediaEnded = true;
        player.MediaEnded += endedHandler;

        player.Play();

        try
        {
            var tickMs = 40;
            var elapsedMs = 0;
            var safetyWatch = Stopwatch.StartNew();
            var maxSafetyMs = effectivePassMs + 600;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var positionMs = (int)Math.Max(0, Math.Round(player.Position.TotalMilliseconds));
                elapsedMs = Math.Clamp(positionMs, 0, effectivePassMs);

                var fadeMultiplier = ComputeFadeMultiplier(
                    elapsedMs,
                    effectivePassMs,
                    fadeInMs,
                    fadeOutMs);

                player.Volume = Math.Clamp(baseVolume * fadeMultiplier, 0d, 1d);
                onElapsed(elapsedMs);

                if (mediaEnded)
                {
                    break;
                }

                if (elapsedMs >= effectivePassMs)
                {
                    break;
                }

                if (safetyWatch.ElapsedMilliseconds >= maxSafetyMs)
                {
                    break;
                }

                var remaining = effectivePassMs - elapsedMs;
                var delay = Math.Min(tickMs, Math.Max(1, remaining));
                await Task.Delay(delay, cancellationToken);
            }

            player.Stop();
            return effectivePassMs;
        }
        finally
        {
            player.MediaEnded -= endedHandler;
        }
    }

    private static double ConvertDbToLinearVolume(double? baseVolumeDb)
    {
        if (!baseVolumeDb.HasValue)
        {
            return 1d;
        }

        var db = Math.Clamp(baseVolumeDb.Value, -60d, 12d);
        var linear = Math.Pow(10d, db / 20d);
        return Math.Clamp(linear, 0d, 1d);
    }

    private static async Task<MediaPlayer> OpenPreviewPlayerAsync(string path, CancellationToken cancellationToken)
    {
        var player = new MediaPlayer();
        var openedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler openedHandler = (_, _) => openedTcs.TrySetResult(true);
        EventHandler<ExceptionEventArgs> failedHandler = (_, args) => openedTcs.TrySetException(args.ErrorException ?? new InvalidOperationException("Failed to open audio."));

        player.MediaOpened += openedHandler;
        player.MediaFailed += failedHandler;

        try
        {
            player.Open(new Uri(path, UriKind.Absolute));
            await openedTcs.Task.WaitAsync(cancellationToken);
            return player;
        }
        catch
        {
            player.Close();
            throw;
        }
        finally
        {
            player.MediaOpened -= openedHandler;
            player.MediaFailed -= failedHandler;
        }
    }

    private async Task PreparePreviewPlayerFromCurrentAssetAsync()
    {
        var requestVersion = _assetMetadataRequestVersion;
        var assetRef = AssetRefTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(assetRef))
        {
            ClosePreviewPlayer();
            return;
        }

        if (!TryResolveExistingAudioPath(assetRef, out var resolvedPath))
        {
            ClosePreviewPlayer();
            return;
        }

        if (requestVersion != _assetMetadataRequestVersion)
        {
            return;
        }

        try
        {
            await EnsurePreviewPlayerReadyAsync(resolvedPath, CancellationToken.None);
        }
        catch
        {
            // Best effort prewarm; preview click path handles user-facing errors.
        }
    }

    private async Task<MediaPlayer> EnsurePreviewPlayerReadyAsync(string path, CancellationToken cancellationToken)
    {
        if (_previewPlayer is not null
            && string.Equals(_previewPlayerPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return _previewPlayer;
        }

        ClosePreviewPlayer();
        var prepared = await OpenPreviewPlayerAsync(path, cancellationToken);
        _previewPlayer = prepared;
        _previewPlayerPath = path;
        return prepared;
    }

    private void ClosePreviewPlayer()
    {
        var player = _previewPlayer;
        _previewPlayer = null;
        _previewPlayerPath = null;

        if (player is null)
        {
            return;
        }

        try
        {
            player.Stop();
        }
        catch
        {
            // Best effort stop before close.
        }

        player.Close();
    }

    private static bool TryResolveExistingAudioPath(string candidate, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            if (AssetSourcePathResolver.TryResolveConfiguredPath(candidate, projectRootFolder: null, out var configuredPath)
                && File.Exists(configuredPath))
            {
                fullPath = Path.GetFullPath(configuredPath);
                return true;
            }

            var cwdPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, candidate));
            if (File.Exists(cwdPath))
            {
                fullPath = cwdPath;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryReadPreviewSettings(out PreviewPlaybackSettings settings, out string errorMessage)
    {
        settings = new PreviewPlaybackSettings();
        errorMessage = string.Empty;

        var repeatMode = NormalizeRepeatMode(RepeatModeComboBox.SelectedItem as string);
        if (!AllowedRepeatModes.Contains(repeatMode, StringComparer.OrdinalIgnoreCase))
        {
            errorMessage = "Repeat Mode is invalid.";
            return false;
        }

        if (!TryParseNullableInt(StartDelayMsTextBox.Text, out var startDelayMs)
            || !TryParseNullableInt(FadeInMsTextBox.Text, out var fadeInMs)
            || !TryParseNullableInt(FadeOutMsTextBox.Text, out var fadeOutMs)
            || !TryParseNullableInt(RepeatCountTextBox.Text, out var repeatCount)
            || !TryParseNullableInt(RepeatIntervalMsTextBox.Text, out var repeatIntervalMs)
            || !TryParseNullableInt(RepeatDurationMsTextBox.Text, out var repeatDurationMs)
            || !TryParseNullableInt(RepeatCooldownMsTextBox.Text, out var repeatCooldownMs)
            || !TryParseNullableInt(MaxPlayDurationMsTextBox.Text, out var maxPlayDurationMs)
            || !TryParseNullableDouble(BaseVolumeDbTextBox.Text, out var baseVolumeDb))
        {
            errorMessage = "One or more preview numeric fields are invalid.";
            return false;
        }

        settings = new PreviewPlaybackSettings
        {
            RepeatMode = repeatMode,
            StartDelayMs = Math.Max(0, startDelayMs ?? 0),
            RepeatCount = Math.Max(1, repeatCount ?? 1),
            RepeatIntervalMs = Math.Max(0, repeatIntervalMs ?? 0),
            RepeatDurationMs = Math.Max(250, repeatDurationMs ?? DefaultLoopPreviewDurationMs),
            RepeatCooldownMs = Math.Max(0, repeatCooldownMs ?? 0),
            MaxPlayDurationMs = Math.Max(0, maxPlayDurationMs ?? 0),
            FadeInMs = Math.Max(0, fadeInMs ?? 0),
            FadeOutMs = Math.Max(0, fadeOutMs ?? 0),
            BaseVolumeDb = baseVolumeDb
        };

        // Keep preview bounded even when RepeatDuration is large or omitted.
        settings.RepeatDurationMs = Math.Min(settings.RepeatDurationMs, MaxPreviewSessionMs);
        return true;
    }

    private void SetPreviewUiState(bool isPlaying, string statusText, PreviewStatusSeverity severity = PreviewStatusSeverity.Neutral)
    {
        _isPreviewPlaying = isPlaying;
        PreviewButton.IsEnabled = !isPlaying;
        StopPreviewButton.IsEnabled = isPlaying;
        PreviewStatusTextBlock.Text = statusText;
        PreviewStatusTextBlock.Foreground = ResolvePreviewStatusBrush(severity);
        _previewStatusSink?.Invoke(MapPreviewSeverityToDiagnosticLevel(severity), statusText);

        if (!isPlaying)
        {
            ResetPreviewProgress();
        }
    }

    private void CancelPreview()
    {
        _previewCancellation?.Cancel();
    }

    private static System.Windows.Media.Brush ResolvePreviewStatusBrush(PreviewStatusSeverity severity)
    {
        return severity switch
        {
            PreviewStatusSeverity.Active => PreviewStatusActiveBrush,
            PreviewStatusSeverity.Success => PreviewStatusSuccessBrush,
            PreviewStatusSeverity.Warning => PreviewStatusWarningBrush,
            PreviewStatusSeverity.Error => PreviewStatusErrorBrush,
            _ => PreviewStatusNeutralBrush
        };
    }

    internal static string MapPreviewSeverityToDiagnosticLevel(string severity)
    {
        if (string.Equals(severity, nameof(PreviewStatusSeverity.Warning), StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (string.Equals(severity, nameof(PreviewStatusSeverity.Error), StringComparison.OrdinalIgnoreCase))
        {
            return "Error";
        }

        return "Info";
    }

    private static string MapPreviewSeverityToDiagnosticLevel(PreviewStatusSeverity severity)
    {
        return MapPreviewSeverityToDiagnosticLevel(severity.ToString());
    }

    private async Task RefreshAssetDurationAsync()
    {
        var requestVersion = ++_assetMetadataRequestVersion;
        var assetRef = AssetRefTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(assetRef))
        {
            _resolvedAssetDurationMs = null;
            AssetDurationTextBlock.Text = "Unknown";
            return;
        }

        if (!TryResolveExistingAudioPath(assetRef, out var resolvedPath))
        {
            _resolvedAssetDurationMs = null;
            AssetDurationTextBlock.Text = "File not found";
            return;
        }

        AssetDurationTextBlock.Text = "Reading...";
        try
        {
            var durationMs = await TryGetAudioDurationMsAsync(resolvedPath, CancellationToken.None);
            if (requestVersion != _assetMetadataRequestVersion)
            {
                return;
            }

            _resolvedAssetDurationMs = NormalizeDurationMs(durationMs);

            AssetDurationTextBlock.Text = durationMs.HasValue
                ? $"{durationMs.Value} ms"
                : "Unknown";
        }
        catch
        {
            if (requestVersion == _assetMetadataRequestVersion)
            {
                _resolvedAssetDurationMs = null;
                AssetDurationTextBlock.Text = "Unavailable";
            }
        }
    }

    private int? ResolveDurationMsForPersistence(string assetRef)
    {
        if (_resolvedAssetDurationMs.HasValue && _resolvedAssetDurationMs.Value > 0)
        {
            return _resolvedAssetDurationMs.Value;
        }

        if (!TryResolveExistingAudioPath(assetRef, out var resolvedPath))
        {
            return _resolvedAssetDurationMs;
        }

        var wavHeaderDurationMs = TryGetWavHeaderDurationMs(resolvedPath);
        return NormalizeDurationMs(wavHeaderDurationMs) ?? _resolvedAssetDurationMs;
    }

    private static int? NormalizeDurationMs(int? durationMs)
    {
        return durationMs is > 0 ? durationMs : null;
    }

    private static async Task<int?> TryGetAudioDurationMsAsync(string path, CancellationToken cancellationToken)
    {
        var player = new MediaPlayer();
        var openedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler openedHandler = (_, _) => openedTcs.TrySetResult(true);
        EventHandler<ExceptionEventArgs> failedHandler = (_, args) => openedTcs.TrySetException(args.ErrorException ?? new InvalidOperationException("Failed to open audio."));

        player.MediaOpened += openedHandler;
        player.MediaFailed += failedHandler;

        try
        {
            player.Open(new Uri(path, UriKind.Absolute));
            await openedTcs.Task.WaitAsync(cancellationToken);

            if (!player.NaturalDuration.HasTimeSpan)
            {
                return null;
            }

            return Math.Max(1, (int)Math.Round(player.NaturalDuration.TimeSpan.TotalMilliseconds));
        }
        finally
        {
            player.MediaOpened -= openedHandler;
            player.MediaFailed -= failedHandler;
            player.Close();
        }
    }

    private static int ResolvePassDurationMs(PreviewPlaybackSettings settings, int sourceDurationMs)
    {
        if (settings.MaxPlayDurationMs > 0)
        {
            return Math.Max(1, Math.Min(settings.MaxPlayDurationMs, sourceDurationMs));
        }

        return Math.Max(1, sourceDurationMs);
    }

    internal static int ResolvePreviewSourceDurationMs(int? naturalDurationMs, int? wavHeaderDurationMs)
    {
        var natural = naturalDurationMs.GetValueOrDefault();
        var wav = wavHeaderDurationMs.GetValueOrDefault();

        var bestKnownDurationMs = Math.Max(natural, wav);
        if (bestKnownDurationMs <= 0)
        {
            bestKnownDurationMs = DefaultPlaybackWindowMs;
        }

        // Leave a small safety margin so timing jitter/metadata rounding does not clip audible tails.
        return Math.Max(1, bestKnownDurationMs + PreviewTailGuardMs);
    }

    internal static int? TryGetWavHeaderDurationMs(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

            if (stream.Length < 12)
            {
                return null;
            }

            var riff = reader.ReadBytes(4);
            _ = reader.ReadUInt32();
            var wave = reader.ReadBytes(4);

            if (!riff.SequenceEqual("RIFF"u8.ToArray()) || !wave.SequenceEqual("WAVE"u8.ToArray()))
            {
                return null;
            }

            int? byteRate = null;
            uint? dataChunkBytes = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = reader.ReadBytes(4);
                var chunkSize = reader.ReadUInt32();

                if (chunkId.SequenceEqual("fmt "u8.ToArray()))
                {
                    if (chunkSize < 16 || stream.Position + chunkSize > stream.Length)
                    {
                        return null;
                    }

                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt32();
                    byteRate = (int)reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();

                    var remainingFmtBytes = chunkSize - 16;
                    if (remainingFmtBytes > 0)
                    {
                        stream.Position += remainingFmtBytes;
                    }
                }
                else if (chunkId.SequenceEqual("data"u8.ToArray()))
                {
                    dataChunkBytes = chunkSize;
                    stream.Position += chunkSize;
                }
                else
                {
                    if (stream.Position + chunkSize > stream.Length)
                    {
                        return null;
                    }

                    stream.Position += chunkSize;
                }

                if ((chunkSize & 1u) == 1u && stream.Position < stream.Length)
                {
                    stream.Position += 1;
                }

                if (byteRate.HasValue && byteRate.Value > 0 && dataChunkBytes.HasValue)
                {
                    break;
                }
            }

            if (!byteRate.HasValue || byteRate.Value <= 0 || !dataChunkBytes.HasValue)
            {
                return null;
            }

            var durationMs = (int)Math.Round((double)dataChunkBytes.Value * 1000d / byteRate.Value);
            return Math.Max(1, durationMs);
        }
        catch
        {
            return null;
        }
    }

    private static int ResolveOverallTargetDurationMs(PreviewPlaybackSettings settings, int passDurationMs)
    {
        var startDelay = Math.Max(0, settings.StartDelayMs);

        if (string.Equals(settings.RepeatMode, "None", StringComparison.OrdinalIgnoreCase))
        {
            return startDelay + passDurationMs;
        }

        if (string.Equals(settings.RepeatMode, "RepeatCount", StringComparison.OrdinalIgnoreCase))
        {
            var count = Math.Max(1, settings.RepeatCount);
            var gapAfterFirst = Math.Max(0, settings.RepeatIntervalMs);
            var gapAfterSubsequent = settings.RepeatCooldownMs > 0
                ? settings.RepeatCooldownMs
                : gapAfterFirst;

            var gaps = 0;
            if (count > 1)
            {
                gaps += gapAfterFirst;
            }

            if (count > 2)
            {
                gaps += (count - 2) * gapAfterSubsequent;
            }

            return startDelay + (count * passDurationMs) + gaps;
        }

        return startDelay + Math.Max(250, settings.RepeatDurationMs);
    }

    internal static string NormalizeRepeatMode(string? repeatMode)
    {
        return repeatMode?.Trim() switch
        {
            "Count" => "RepeatCount",
            "Duration" => "RepeatForDuration",
            "Loop" => "RepeatForDuration",
            "UntilCancelled" => "UntilCanceled",
            "UntilCancel" => "UntilCanceled",
            "None" => "None",
            "RepeatCount" => "RepeatCount",
            "RepeatForDuration" => "RepeatForDuration",
            "UntilCanceled" => "UntilCanceled",
            _ => "None"
        };
    }

    internal static string NormalizeReplayPolicy(string? replayPolicy)
    {
        return replayPolicy?.Trim() switch
        {
            "CancelPreviousAtNextPlay" => "CancelPreviousAtNextPlay",
            "IgnoreIfAlreadyPlaying" => "IgnoreIfAlreadyPlaying",
            _ => "PlayAgain"
        };
    }

    internal static (int FadeInMs, int FadeOutMs) ResolvePreviewIterationFade(
        string? repeatMode,
        int iteration,
        int repeatCount,
        int fadeInMs,
        int fadeOutMs)
    {
        var normalizedMode = NormalizeRepeatMode(repeatMode);
        var normalizedIteration = Math.Max(1, iteration);
        var normalizedRepeatCount = Math.Max(1, repeatCount);
        var normalizedFadeIn = Math.Max(0, fadeInMs);
        var normalizedFadeOut = Math.Max(0, fadeOutMs);

        if (string.Equals(normalizedMode, "RepeatCount", StringComparison.OrdinalIgnoreCase))
        {
            return (
                FadeInMs: normalizedIteration == 1 ? normalizedFadeIn : 0,
                FadeOutMs: normalizedIteration == normalizedRepeatCount ? normalizedFadeOut : 0);
        }

        if (string.Equals(normalizedMode, "RepeatForDuration", StringComparison.OrdinalIgnoreCase))
        {
            return (
                FadeInMs: normalizedIteration == 1 ? normalizedFadeIn : 0,
                FadeOutMs: 0);
        }

        if (string.Equals(normalizedMode, "UntilCanceled", StringComparison.OrdinalIgnoreCase))
        {
            return (
                FadeInMs: normalizedIteration == 1 ? normalizedFadeIn : 0,
                FadeOutMs: 0);
        }

        return (normalizedFadeIn, normalizedFadeOut);
    }

    internal static int ResolvePreviewGapMs(int repeatIntervalMs, int repeatCooldownMs, int completedIterations)
    {
        var intervalMs = Math.Max(0, repeatIntervalMs);
        var cooldownMs = Math.Max(0, repeatCooldownMs);
        if (cooldownMs <= 0)
        {
            return 0;
        }

        var iteration = Math.Max(1, completedIterations);
        return iteration == 1 ? intervalMs : cooldownMs;
    }

    private static async Task DelayWithProgressAsync(int durationMs, Action<int> onElapsed, CancellationToken cancellationToken)
    {
        if (durationMs <= 0)
        {
            onElapsed(0);
            return;
        }

        var tickMs = 40;
        var elapsedMs = 0;
        while (elapsedMs < durationMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = durationMs - elapsedMs;
            var delay = Math.Min(tickMs, remaining);
            await Task.Delay(delay, cancellationToken);
            elapsedMs += delay;
            onElapsed(elapsedMs);
        }
    }

    private void ResetPreviewProgress()
    {
        UpdatePassProgress(0, 1);
        UpdateOverallProgress(0, 1);
    }

    private void UpdatePassProgress(int elapsedMs, int totalMs)
    {
        var boundedTotal = Math.Max(1, totalMs);
        var boundedElapsed = Math.Clamp(elapsedMs, 0, boundedTotal);
        PassProgressBar.Maximum = boundedTotal;
        PassProgressBar.Value = boundedElapsed;
        PassProgressTextBlock.Text = $"{boundedElapsed} / {boundedTotal} ms";
    }

    private void UpdateOverallProgress(int elapsedMs, int totalMs)
    {
        var boundedTotal = Math.Max(1, totalMs);
        var boundedElapsed = Math.Clamp(elapsedMs, 0, boundedTotal);
        OverallProgressBar.Maximum = boundedTotal;
        OverallProgressBar.Value = boundedElapsed;
        OverallProgressTextBlock.Text = $"{boundedElapsed} / {boundedTotal} ms";
    }

    private static double ComputeFadeMultiplier(int elapsedMs, int totalMs, int fadeInMs, int fadeOutMs)
    {
        if (totalMs <= 0)
        {
            return 1d;
        }

        var multiplier = 1d;

        if (fadeInMs > 0)
        {
            var inProgress = Math.Clamp((double)elapsedMs / fadeInMs, 0d, 1d);
            multiplier = Math.Min(multiplier, inProgress);
        }

        if (fadeOutMs > 0)
        {
            var remainingMs = totalMs - elapsedMs;
            var outProgress = Math.Clamp((double)remainingMs / fadeOutMs, 0d, 1d);
            multiplier = Math.Min(multiplier, outProgress);
        }

        return multiplier;
    }

    private sealed class PreviewPlaybackSettings
    {
        public string RepeatMode { get; set; } = "None";
        public int StartDelayMs { get; set; }
        public int RepeatCount { get; set; } = 1;
        public int RepeatIntervalMs { get; set; }
        public int RepeatDurationMs { get; set; } = DefaultLoopPreviewDurationMs;
        public int RepeatCooldownMs { get; set; }
        public int MaxPlayDurationMs { get; set; }
        public int FadeInMs { get; set; }
        public int FadeOutMs { get; set; }
        public double? BaseVolumeDb { get; set; }
    }
}