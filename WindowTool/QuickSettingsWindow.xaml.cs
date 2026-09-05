using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool;

public partial class QuickSettingsWindow : UserControl {
    private static readonly Regex NonDigitRegex = new("[^0-9]+", RegexOptions.Compiled);

    private IProcessService _processService = null!;
    private ProcessInfo? _selectedProcess;
    private bool _isLoadingSettings;
    private bool _hasPendingVolumeChange;
    private bool _isApplyingVolume;
    private int _selectionVersion;
    private readonly System.Windows.Threading.DispatcherTimer _durationSaveTimer;

    public QuickSettingsWindow() {
        InitializeComponent();
        _durationSaveTimer = new System.Windows.Threading.DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _durationSaveTimer.Tick += DurationSaveTimer_Tick;
    }

    public QuickSettingsWindow(IProcessService processService) : this() {
        Initialize(processService);
    }

    public void Initialize(IProcessService processService) {
        _processService = processService;
    }

    public event Action<int>? SettingsChanged;
    public event Action<string>? StatusMessageChanged;

    public void ShowProcess(ProcessInfo process) {
        LoadProcess(process);
        Visibility = Visibility.Visible;
    }

    public void UpdateProcessIfVisible(ProcessInfo process) {
        if (Visibility == Visibility.Visible && _selectedProcess?.Id != process.Id) {
            LoadProcess(process);
        }
    }

    public void HideForMissingProcess() {
        FlushPendingDurationSettings();
        _selectionVersion++;
        _selectedProcess = null;
        Visibility = Visibility.Collapsed;
    }

    public void ClosePermanently() {
        FlushPendingDurationSettings();
        _selectionVersion++;
        _selectedProcess = null;
    }

    private void LoadProcess(ProcessInfo process) {
        FlushPendingDurationSettings();
        int selectionVersion = ++_selectionVersion;
        _selectedProcess = process;
        _isLoadingSettings = true;
        try {
            SelectedTitleText.Text = process.MainWindowTitle;
            SelectedProcessText.Text = $"{process.Name}  ·  PID {process.Id}";
            SelectedIcon.Source = null;
            SelectedIcon.Visibility = Visibility.Collapsed;
            SelectedIconFallback.Visibility = Visibility.Visible;
            SelectedIconFallback.Child = new TextBlock {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Text = string.IsNullOrWhiteSpace(process.Name)
                    ? "?"
                    : process.Name[..1].ToUpper(CultureInfo.CurrentCulture)
            };
            VolumeSlider.Value = Math.Round(process.TargetVolume * 100);
            UpdateVolumeText();
            _hasPendingVolumeChange = false;

            EnableUnfocusMuteCheckBox.IsChecked = process.EnableUnfocusMute;
            EnableTopMostCheckBox.IsChecked = process.ShouldBeTopMost;
            UnfocusDelayTextBox.Text = process.UnfocusMuteDurationSec.ToString(CultureInfo.InvariantCulture);
            FocusDelayTextBox.Text = process.FocusUnmuteDurationSec.ToString(CultureInfo.InvariantCulture);
            FadeOutTextBox.Text = process.FadeMuteDurationSec.ToString(CultureInfo.InvariantCulture);
            FadeInTextBox.Text = process.FadeUnmuteDurationSec.ToString(CultureInfo.InvariantCulture);
            MuteOptionsPanel.IsEnabled = process.EnableUnfocusMute;
        }
        finally {
            _isLoadingSettings = false;
        }

        _ = LoadRuntimeStateAsync(process, selectionVersion);
    }

    private async Task LoadRuntimeStateAsync(ProcessInfo process, int selectionVersion) {
        try {
            Task<ImageSource?> iconTask = WindowIconHelper.GetIconAsync(process);
            Task<(bool Found, float Volume)> volumeTask = Task.Run(() => {
                bool found = AudioHelper.TryGetVolume(process, out float volume);
                return (found, volume);
            });

            ImageSource? icon = await iconTask;
            if (selectionVersion != _selectionVersion || _selectedProcess?.Id != process.Id) return;

            SelectedIcon.Source = icon;
            SelectedIcon.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
            SelectedIconFallback.Visibility = icon == null ? Visibility.Visible : Visibility.Collapsed;

            (bool found, float currentVolume) = await volumeTask;
            if (selectionVersion != _selectionVersion
                || _selectedProcess?.Id != process.Id
                || _hasPendingVolumeChange
                || _isApplyingVolume) {
                return;
            }

            if (found && !process.HasTargetVolume) {
                process.TargetVolume = currentVolume;
                process.HasTargetVolume = true;
                _isLoadingSettings = true;
                VolumeSlider.Value = Math.Round(currentVolume * 100);
                UpdateVolumeText();
                _isLoadingSettings = false;
            }
        }
        catch (Exception) {
            // The process or its audio session can disappear while loading.
            // Keep the saved value and fallback icon in that case.
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        if (VolumeValueText == null) return;
        UpdateVolumeText();
        if (_isLoadingSettings || _selectedProcess == null) return;
        _hasPendingVolumeChange = true;
    }

    private void UpdateVolumeText() {
        VolumeValueText.Text = $"{Math.Round(VolumeSlider.Value):0}%";
    }

    private async void VolumeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        await ApplyPendingVolumeAsync();
    }

    private async void VolumeSlider_PreviewKeyUp(object sender, KeyEventArgs e) {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.PageUp or Key.PageDown or Key.Home or Key.End) {
            await ApplyPendingVolumeAsync();
        }
    }

    private async Task ApplyPendingVolumeAsync() {
        if (!_hasPendingVolumeChange || _isApplyingVolume || _selectedProcess == null) return;

        _isApplyingVolume = true;
        int applySelectionVersion = _selectionVersion;
        try {
            while (_hasPendingVolumeChange
                && _selectedProcess != null
                && applySelectionVersion == _selectionVersion) {
                _hasPendingVolumeChange = false;
                ProcessInfo process = _selectedProcess;
                double volumePercent = VolumeSlider.Value;
                bool updated = await _processService.SetProcessVolumeAsync(
                    process,
                    (float)(volumePercent / 100.0));

                if (_selectedProcess?.Id != process.Id) continue;
                string message = updated
                    ? $"已將「{process.MainWindowTitle}」音量調整為 {Math.Round(volumePercent):0}%"
                    : $"已保存音量；「{process.MainWindowTitle}」目前沒有音訊工作階段";
                StatusMessageChanged?.Invoke(message);
                SettingsChanged?.Invoke(process.Id);
            }
        }
        catch (Exception ex) {
            if (applySelectionVersion == _selectionVersion) {
                _hasPendingVolumeChange = true;
                StatusMessageChanged?.Invoke($"調整音量失敗：{ex.Message}");
            }
        }
        finally {
            _isApplyingVolume = false;
        }
    }

    private async void EnableUnfocusMuteCheckBox_Changed(object sender, RoutedEventArgs e) {
        if (MuteOptionsPanel == null) return;
        bool enabled = EnableUnfocusMuteCheckBox.IsChecked == true;
        MuteOptionsPanel.IsEnabled = enabled;
        if (_isLoadingSettings || _selectedProcess == null) return;

        ProcessInfo process = _selectedProcess;
        process.EnableUnfocusMute = enabled;
        _processService.SaveSettings(process);
        try {
            if (enabled) {
                await _processService.AddToMonitorListAsync(process);
                await _processService.MonitorProcessAsync();
            }
            else {
                await _processService.RemoveFromMonitorListAsync(process);
            }

            SettingsChanged?.Invoke(process.Id);
            StatusMessageChanged?.Invoke(enabled
                ? $"已啟用「{process.MainWindowTitle}」的失焦靜音"
                : $"已停用「{process.MainWindowTitle}」的失焦靜音");
        }
        catch (Exception ex) {
            StatusMessageChanged?.Invoke($"變更失焦靜音失敗：{ex.Message}");
        }
    }

    private void EnableTopMostCheckBox_Changed(object sender, RoutedEventArgs e) {
        if (_isLoadingSettings || _selectedProcess == null) return;

        ProcessInfo process = _selectedProcess;
        bool requestedTopMost = EnableTopMostCheckBox.IsChecked == true;
        process.ShouldBeTopMost = requestedTopMost;
        if (!ApplyTopMostSetting(process)) {
            process.ShouldBeTopMost = !requestedTopMost;
            _isLoadingSettings = true;
            EnableTopMostCheckBox.IsChecked = !requestedTopMost;
            _isLoadingSettings = false;
            StatusMessageChanged?.Invoke($"無法將「{process.MainWindowTitle}」設為最上層。請確認視窗仍然存在。");
            return;
        }

        _processService.SaveSettings(process);
        SettingsChanged?.Invoke(process.Id);
        StatusMessageChanged?.Invoke(process.ShouldBeTopMost
            ? $"已將「{process.MainWindowTitle}」設為最上層"
            : $"已解除「{process.MainWindowTitle}」的最上層狀態");
    }

    private void DurationTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        if (_isLoadingSettings || _selectedProcess == null) return;
        _durationSaveTimer.Stop();
        if (!TryReadAllDurations(out int unfocusDelay, out int focusDelay, out int fadeOut, out int fadeIn)) {
            StatusMessageChanged?.Invoke("時間必須是 0 到 3600 之間的整數。");
            return;
        }

        _durationSaveTimer.Start();
    }

    private void DurationSaveTimer_Tick(object? sender, EventArgs e) {
        _durationSaveTimer.Stop();
        SaveDurationSettings();
    }

    private void FlushPendingDurationSettings() {
        if (!_durationSaveTimer.IsEnabled) return;
        _durationSaveTimer.Stop();
        SaveDurationSettings();
    }

    private void SaveDurationSettings() {
        if (_isLoadingSettings || _selectedProcess == null
            || !TryReadAllDurations(out int unfocusDelay, out int focusDelay, out int fadeOut, out int fadeIn)) {
            return;
        }

        ProcessInfo process = _selectedProcess;
        process.UnfocusMuteDurationSec = unfocusDelay;
        process.FocusUnmuteDurationSec = focusDelay;
        process.FadeMuteDurationSec = fadeOut;
        process.FadeUnmuteDurationSec = fadeIn;
        _processService.SaveSettings(process);
        StatusMessageChanged?.Invoke($"已自動保存「{process.MainWindowTitle}」的時間設定");
    }

    private bool TryReadAllDurations(
        out int unfocusDelay,
        out int focusDelay,
        out int fadeOut,
        out int fadeIn) {
        bool unfocusValid = TryReadDuration(UnfocusDelayTextBox, out unfocusDelay);
        bool focusValid = TryReadDuration(FocusDelayTextBox, out focusDelay);
        bool fadeOutValid = TryReadDuration(FadeOutTextBox, out fadeOut);
        bool fadeInValid = TryReadDuration(FadeInTextBox, out fadeIn);
        return unfocusValid && focusValid && fadeOutValid && fadeInValid;
    }

    private static bool ApplyTopMostSetting(ProcessInfo process) =>
        TopMostStateManager.Apply(process, process.ShouldBeTopMost);

    private static bool TryReadDuration(TextBox textBox, out int value) {
        bool valid = int.TryParse(textBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value is >= 0 and <= 3600;
        textBox.BorderBrush = valid
            ? (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"]
            : System.Windows.Media.Brushes.IndianRed;
        return valid;
    }

    private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
        e.Handled = NonDigitRegex.IsMatch(e.Text);
    }

}
