using System.Collections.ObjectModel;
using Avalonia.Threading;
using ProjectManager.Avalonia.Helpers;
using ProjectManager.Avalonia.Models;
using ProjectManager.Avalonia.Services;

namespace ProjectManager.Avalonia.ViewModels.Pages;

public partial class PerformanceViewModel : ViewModelBase
{
    private readonly IPerformanceMonitorService _performanceMonitorService;
    private readonly IErrorDisplayService _errorDisplayService;
    private readonly ILanguageService _languageService;
    private CancellationTokenSource? _monitoringCts;

    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(1);

    [ObservableProperty]
    private ObservableCollection<ProjectPerformanceSnapshot> _projectMetrics = new();

    [ObservableProperty]
    private DateTime _lastUpdated = DateTime.MinValue;

    [ObservableProperty]
    private bool _isMonitoringActive;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _totalCpuUsage;

    [ObservableProperty]
    private double? _totalGpuUsage;

    [ObservableProperty]
    private double _totalMemoryUsageMb;

    [ObservableProperty]
    private int _runningProjects;

    [ObservableProperty]
    private int _totalProjects;

    [ObservableProperty]
    private bool _hasGpuData;

    public PerformanceViewModel(
        IPerformanceMonitorService performanceMonitorService,
        IErrorDisplayService errorDisplayService,
        ILanguageService languageService)
    {
        _performanceMonitorService = performanceMonitorService;
        _errorDisplayService = errorDisplayService;
        _languageService = languageService;

        _languageService.LanguageChanged += (s, e) => UpdateStatusMessage();
        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        if (!IsMonitoringActive && _monitoringCts == null)
            StatusMessage = _languageService.GetString("Performance_Status_Waiting");
        else if (!IsMonitoringActive && _monitoringCts != null)
            StatusMessage = _languageService.GetString("Performance_Status_Paused");
        else
            StatusMessage = RunningProjects > 0
                ? string.Format(_languageService.GetString("Performance_Status_Monitoring"), RunningProjects, TotalProjects)
                : _languageService.GetString("Performance_Status_NoRunning");
    }

    public void OnNavigatedTo() => StartMonitoringLoop();
    public void OnNavigatedFrom() => StopMonitoringLoop();
    public Task OnNavigatedToAsync() { StartMonitoringLoop(); return Task.CompletedTask; }
    public Task OnNavigatedFromAsync() { StopMonitoringLoop(); return Task.CompletedTask; }

    [RelayCommand]
    private async Task RefreshNow()
    {
        try
        {
            await RefreshMetricsAsync(_monitoringCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await _errorDisplayService.ShowErrorAsync(
                $"{_languageService.GetString("Error_Performance_RefreshFailed")}: {ex.Message}",
                _languageService.GetString("Error_Performance_Error"));
        }
    }

    [RelayCommand]
    private void OpenInExplorer(ProjectPerformanceSnapshot? snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.LocalPath) || !Directory.Exists(snapshot.LocalPath))
            return;

        try
        {
            ProcessInterop.OpenInFileManager(snapshot.LocalPath);
        }
        catch (Exception ex)
        {
            _ = _errorDisplayService.ShowErrorAsync(
                $"{_languageService.GetString("Error_Performance_OpenExplorerFailed")}: {ex.Message}",
                _languageService.GetString("Error_Performance_OpenPathFailed"));
        }
    }

    private void StartMonitoringLoop()
    {
        if (_monitoringCts != null) return;
        _monitoringCts = new CancellationTokenSource();
        _ = MonitorLoopAsync(_monitoringCts.Token);
    }

    private void StopMonitoringLoop()
    {
        if (_monitoringCts == null) return;
        _monitoringCts.Cancel();
        _monitoringCts.Dispose();
        _monitoringCts = null;
        IsMonitoringActive = false;
        UpdateStatusMessage();
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        IsMonitoringActive = true;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await RefreshMetricsAsync(token);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                await _errorDisplayService.ShowErrorAsync(
                    $"{_languageService.GetString("Error_Performance_MonitoringFailed")}: {ex.Message}",
                    _languageService.GetString("Error_Performance_Error"));
            }

            try { await Task.Delay(_refreshInterval, token); }
            catch (TaskCanceledException) { break; }
        }
        IsMonitoringActive = false;
    }

    private async Task RefreshMetricsAsync(CancellationToken token)
    {
        var snapshots = await _performanceMonitorService.GetProjectPerformanceAsync(token);
        token.ThrowIfCancellationRequested();

        var ordered = snapshots
            .OrderByDescending(s => s.IsRunning)
            .ThenByDescending(s => s.CpuUsagePercent)
            .ThenBy(s => s.ProjectName)
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() => ApplySnapshots(ordered), DispatcherPriority.Background);
    }

    private void ApplySnapshots(IList<ProjectPerformanceSnapshot> ordered)
    {
        var newIds = new HashSet<string>(ordered.Select(m => m.ProjectId));

        for (int i = ProjectMetrics.Count - 1; i >= 0; i--)
        {
            if (!newIds.Contains(ProjectMetrics[i].ProjectId))
                ProjectMetrics.RemoveAt(i);
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            var snapshot = ordered[i];

            if (snapshot.GpuUsagePercent.HasValue)
            {
                var v = snapshot.GpuUsagePercent.Value;
                if (v <= 1d) v *= 100d;
                v = Math.Clamp(v, 0d, 100d);
                snapshot.GpuUsagePercent = Math.Round(v, 1);
            }

            var existingIndex = -1;
            for (int j = 0; j < ProjectMetrics.Count; j++)
            {
                if (ProjectMetrics[j].ProjectId == snapshot.ProjectId)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
                ProjectMetrics[existingIndex] = snapshot;
            else
                ProjectMetrics.Add(snapshot);
        }

        TotalProjects = ordered.Count;
        RunningProjects = ordered.Count(s => s.IsRunning);
        TotalCpuUsage = Math.Round(ordered.Where(s => s.IsRunning).Sum(s => s.CpuUsagePercent), 1);
        var gpuSum = ordered.Where(s => s.GpuUsagePercent.HasValue).Sum(s => s.GpuUsagePercent!.Value);
        TotalGpuUsage = Math.Round(gpuSum, 1);
        HasGpuData = ordered.Any(s => s.GpuUsagePercent.HasValue) && gpuSum > 0;

        var totalMem = ordered.Where(s => s.IsRunning).Sum(s => s.MemoryUsageMb);
        TotalMemoryUsageMb = Math.Round(totalMem, 1);
        LastUpdated = DateTime.Now;
        UpdateStatusMessage();
    }
}
