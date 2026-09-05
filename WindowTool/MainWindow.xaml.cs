using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowTool.Model;
using WindowTool.Service;

namespace WindowTool;

public partial class MainWindow : Window {
    private readonly IProcessService _processService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private readonly HashSet<(int ProcessId, IntPtr Handle)> _initializedWindows = new();
    private readonly ObservableCollection<ProcessListItem> _processItems = new();
    private readonly Dictionary<int, ProcessListItem> _processItemsById = new();
    private readonly ICollectionView _processView;
    private readonly bool _usesLiveProcessViewUpdates;
    private ProcessInfo? _selectedProcess;
    private string _searchQuery = string.Empty;
    private bool _isRefreshing;
    private bool _isClosing;
    private bool _shutdownComplete;

    public MainWindow(IProcessService processService) {
        InitializeComponent();
        _processService = processService;
        _processView = CollectionViewSource.GetDefaultView(_processItems);
        _processView.Filter = MatchesSearch;
        _processView.SortDescriptions.Add(new SortDescription(
            nameof(ProcessListItem.WindowTitle),
            ListSortDirection.Ascending));
        if (_processView is ICollectionViewLiveShaping liveView
            && liveView.CanChangeLiveSorting
            && liveView.CanChangeLiveFiltering) {
            liveView.LiveSortingProperties.Add(nameof(ProcessListItem.WindowTitle));
            liveView.LiveFilteringProperties.Add(nameof(ProcessListItem.WindowTitle));
            liveView.LiveFilteringProperties.Add(nameof(ProcessListItem.ProcessName));
            liveView.IsLiveSorting = true;
            liveView.IsLiveFiltering = true;
            _usesLiveProcessViewUpdates = true;
        }
        ProcessDataGrid.ItemsSource = _processView;
        QuickSettingsPanel.Initialize(processService);
        QuickSettingsPanel.SettingsChanged += QuickSettingsWindow_SettingsChanged;
        QuickSettingsPanel.StatusMessageChanged += QuickSettingsWindow_StatusMessageChanged;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += SearchTimer_Tick;
    }

    protected override void OnSourceInitialized(EventArgs e) {
        base.OnSourceInitialized(e);
        EnableDarkTitleBar(this);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) {
        try {
            _processService.StartMonitoring();
            await _processService.RefreshWindowProcessListAsync(_shutdownCancellation.Token);
            if (_isClosing) return;

            await ApplySavedSettingsAsync();
            if (_isClosing) return;

            BindProcessList();
            _refreshTimer.Start();
            UpdateStatus();
        }
        catch (ObjectDisposedException) when (_isClosing) {
        }
        catch (OperationCanceledException) when (_isClosing) {
        }
    }

    private async Task ApplySavedSettingsAsync() {
        foreach (ProcessInfo process in _processService.WindowProcessList.ToList()) {
            if (_isClosing) return;

            var identity = (process.Id, process.MainWindowHandle);
            if (!_initializedWindows.Add(identity)) continue;

            if (process.ShouldBeTopMost && !ProcessHelper.IsTopMost(process.MainWindowHandle)) {
                ApplyTopMostSetting(process);
            }

            if (process.EnableUnfocusMute) {
                await Task.Run(() => _processService.AddToMonitorListAsync(process));
            }
        }

        if (!_isClosing) {
            await Task.Run(_processService.MonitorProcessAsync);
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e) {
        await RefreshProcessesAsync();
    }

    private async Task RefreshProcessesAsync() {
        if (_isRefreshing || _isClosing) return;

        _isRefreshing = true;
        try {
            int? selectedPid = _selectedProcess?.Id;
            bool changed = await _processService.RefreshWindowProcessListAsync(
                _shutdownCancellation.Token);
            if (_isClosing) return;

            if (changed) {
                await ApplySavedSettingsAsync();
            }
            if (_isClosing) return;

            if (changed) {
                BindProcessList(selectedPid);
            }

            if (changed && _selectedProcess != null) {
                ProcessInfo? current = _processService.WindowProcessList
                    .FirstOrDefault(process => process.Id == _selectedProcess.Id);
                if (current == null) {
                    ClearSelection();
                }
                else {
                    _selectedProcess = current;
                }
            }

            UpdateStatus();
        }
        catch (ObjectDisposedException) when (_isClosing) {
        }
        catch (OperationCanceledException) when (_isClosing) {
        }
        catch (Exception ex) {
            StatusText.Text = $"重新整理失敗：{ex.Message}";
        }
        finally {
            _isRefreshing = false;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        if (!IsLoaded) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e) {
        _searchTimer.Stop();
        _searchQuery = SearchTextBox.Text.Trim();
        _processView.Refresh();
        UpdateVisibleWindowCount();
        RestoreSelection(_selectedProcess?.Id);
    }

    private void BindProcessList(int? selectedPid = null) {
        HashSet<int> monitoredIds = _processService.MonitorWindowProcessList
            .Select(process => process.Id)
            .ToHashSet();
        HashSet<int> currentProcessIds = _processService.WindowProcessList
            .Select(process => process.Id)
            .ToHashSet();

        bool viewKeyChanged = false;
        for (int index = _processItems.Count - 1; index >= 0; index--) {
            int processId = _processItems[index].Process.Id;
            if (!currentProcessIds.Contains(processId)) {
                _processItems.RemoveAt(index);
                _processItemsById.Remove(processId);
            }
        }

        foreach (ProcessInfo process in _processService.WindowProcessList) {
            bool isMonitored = monitoredIds.Contains(process.Id);
            if (_processItemsById.TryGetValue(process.Id, out ProcessListItem? item)) {
                viewKeyChanged |= item.Update(process, isMonitored);
            }
            else {
                var newItem = new ProcessListItem(process, isMonitored);
                _processItemsById.Add(process.Id, newItem);
                _processItems.Add(newItem);
            }
        }

        // WPF's default ListCollectionView supports per-item live shaping. A
        // custom view may not, so retain a safe refresh fallback only when a
        // property used by sorting/filtering actually changed.
        if (viewKeyChanged && !_usesLiveProcessViewUpdates) _processView.Refresh();

        UpdateVisibleWindowCount();
        RestoreSelection(selectedPid);
    }

    private bool MatchesSearch(object item) {
        if (item is not ProcessListItem processItem) return false;
        return string.IsNullOrEmpty(_searchQuery)
            || processItem.WindowTitle.Contains(_searchQuery, StringComparison.CurrentCultureIgnoreCase)
            || processItem.ProcessName.Contains(_searchQuery, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateVisibleWindowCount() {
        WindowCountText.Text = $"{_processView.Cast<object>().Count()} 個";
    }

    private void RestoreSelection(int? selectedPid) {
        if (selectedPid == null) return;

        if (_processItemsById.TryGetValue(selectedPid.Value, out ProcessListItem? selectedItem)
            && MatchesSearch(selectedItem)) {
            ProcessDataGrid.SelectedItem = selectedItem;
            ProcessDataGrid.ScrollIntoView(selectedItem);
        }
        else if (_selectedProcess?.Id == selectedPid.Value) {
            ClearSelection();
        }
    }

    private void ProcessDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (ProcessDataGrid.SelectedItem is not ProcessListItem item) {
            return;
        }

        _selectedProcess = item.Process;
        SystemSettingsPanel.Visibility = Visibility.Collapsed;
        QuickSettingsPanel.Visibility = Visibility.Visible;
        SettingsPanelBorder.Visibility = Visibility.Visible;
        QuickSettingsPanel.ShowProcess(item.Process);
    }

    private void ClearSelection() {
        _selectedProcess = null;
        ProcessDataGrid.SelectedItem = null;
        QuickSettingsPanel.HideForMissingProcess();
        SystemSettingsPanel.Visibility = Visibility.Collapsed;
        SettingsPanelBorder.Visibility = Visibility.Collapsed;
    }

    private void SystemSettingsButton_Click(object sender, RoutedEventArgs e) {
        _selectedProcess = null;
        ProcessDataGrid.SelectedItem = null;
        QuickSettingsPanel.HideForMissingProcess();
        QuickSettingsPanel.Visibility = Visibility.Collapsed;
        SystemSettingsPanel.Visibility = Visibility.Visible;
        SettingsPanelBorder.Visibility = Visibility.Visible;
        ThemeComboBox.SelectedValue = ThemeManager.LoadTheme();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!IsLoaded || ThemeComboBox.SelectedValue is not string theme) return;
        ThemeManager.Apply(Application.Current, theme);
        ThemeManager.SaveTheme(theme);
    }

    private void QuickSettingsWindow_SettingsChanged(int processId) {
        ProcessInfo? process = _processService.WindowProcessList
            .FirstOrDefault(candidate => candidate.Id == processId);
        if (process != null && _processItemsById.TryGetValue(processId, out ProcessListItem? item)) {
            bool isMonitored = _processService.MonitorWindowProcessList
                .Any(monitored => monitored.Id == processId);
            bool viewKeyChanged = item.Update(process, isMonitored);
            if (viewKeyChanged && !_usesLiveProcessViewUpdates) _processView.Refresh();
        }
        else {
            BindProcessList(processId);
        }
        UpdateStatus();
    }

    private void QuickSettingsWindow_StatusMessageChanged(string message) {
        StatusText.Text = message;
    }

    private void UpdateStatus() {
        int monitoredCount = _processService.MonitorWindowProcessList.Count;
        int topMostCount = _processService.WindowProcessList.Count(process => process.IsTopMost);
        MonitoredCountText.Text = $"{monitoredCount} 個監控中";
        TopMostCountText.Text = $"{topMostCount} 個置頂";
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e) {
        if (_shutdownComplete) return;

        e.Cancel = true;
        if (_isClosing) return;

        _isClosing = true;
        _shutdownCancellation.Cancel();
        IsEnabled = false;
        _refreshTimer.Stop();
        _searchTimer.Stop();
        QuickSettingsPanel.ClosePermanently();
        StatusText.Text = "正在還原視窗狀態…";

        try {
            await _processService.ShutdownAsync();
            TopMostStateManager.RestoreAll();
        }
        finally {
            _shutdownComplete = true;
            _shutdownCancellation.Dispose();
            // Let the current (cancelled) Closing event return before asking
            // WPF to close again; calling Close inline while the event is
            // still active throws InvalidOperationException.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Close));
        }
    }

    private static void ApplyTopMostSetting(ProcessInfo process) {
        _ = TopMostStateManager.Apply(process, process.ShouldBeTopMost);
    }

    internal static void EnableDarkTitleBar(Window window) {
        int useDarkMode = 1;
        IntPtr handle = new WindowInteropHelper(window).Handle;
        int result = DwmSetWindowAttribute(handle, 20, ref useDarkMode, sizeof(int));
        if (result != 0) {
            DwmSetWindowAttribute(handle, 19, ref useDarkMode, sizeof(int));
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private sealed class ProcessListItem : INotifyPropertyChanged {
        private ImageSource? _icon;
        private string _windowTitle;
        private string _processName;
        private bool _isMonitored;
        private bool _isMuted;
        private bool _isTopMost;
        private IntPtr _windowHandle;
        private int _iconLoadVersion;

        public ProcessListItem(ProcessInfo process, bool isMonitored) {
            Process = process;
            _windowTitle = process.MainWindowTitle;
            _processName = process.Name;
            _isMonitored = isMonitored;
            _isMuted = process.IsMuted;
            _isTopMost = process.IsTopMost;
            _windowHandle = process.MainWindowHandle;
            _ = LoadIconAsync();
        }

        public ProcessInfo Process { get; private set; }
        public ImageSource? Icon {
            get => _icon;
            private set {
                if (ReferenceEquals(_icon, value)) return;
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }
        public string WindowTitle => _windowTitle;
        public string ProcessName => _processName;
        public string ProcessInitial => string.IsNullOrWhiteSpace(_processName)
            ? "?"
            : _processName[..1].ToUpper(CultureInfo.CurrentCulture);

        public string StatusText {
            get {
                var states = new List<string>();
                if (_isMuted) states.Add("已靜音");
                else if (_isMonitored) states.Add("監控中");
                if (_isTopMost) states.Add("最上層");
                return states.Count == 0 ? "—" : string.Join(" · ", states);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool Update(ProcessInfo process, bool isMonitored) {
            bool iconIdentityChanged = Process.Id != process.Id
                || _windowHandle != process.MainWindowHandle;
            Process = process;
            _windowHandle = process.MainWindowHandle;

            bool windowTitleChanged = SetField(
                ref _windowTitle,
                process.MainWindowTitle,
                nameof(WindowTitle));
            bool processNameChanged = SetField(ref _processName, process.Name, nameof(ProcessName));
            if (processNameChanged) {
                OnPropertyChanged(nameof(ProcessInitial));
            }

            bool statusChanged = _isMonitored != isMonitored
                || _isMuted != process.IsMuted
                || _isTopMost != process.IsTopMost;
            _isMonitored = isMonitored;
            _isMuted = process.IsMuted;
            _isTopMost = process.IsTopMost;
            if (statusChanged) OnPropertyChanged(nameof(StatusText));

            if (iconIdentityChanged) {
                Icon = null;
                _ = LoadIconAsync();
            }

            return windowTitleChanged || processNameChanged;
        }

        private async Task LoadIconAsync() {
            int version = ++_iconLoadVersion;
            ProcessInfo processSnapshot = Process;
            try {
                ImageSource? icon = await WindowIconHelper.GetIconAsync(processSnapshot);
                if (version == _iconLoadVersion) Icon = icon;
            }
            catch (Exception) {
                // The target may close while its icon is being loaded. The
                // initial-letter fallback remains visible in that case.
            }
        }

        private bool SetField(ref string field, string value, string propertyName) {
            if (string.Equals(field, value, StringComparison.Ordinal)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
