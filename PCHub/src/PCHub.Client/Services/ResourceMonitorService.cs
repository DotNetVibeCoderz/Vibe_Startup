using System.Diagnostics;
using System.Windows.Threading;

namespace PCHub.Client.Services;

/// <summary>
/// Service untuk monitoring resource PC client (CPU, GPU, RAM).
/// Mengirim data ke server secara periodik.
/// </summary>
public class ResourceMonitorService
{
    private readonly DispatcherTimer _timer;
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;
    private readonly ApiService? _api;
    private Guid? _pcId;

    public double CpuUsage { get; private set; }
    public double RamUsagePercent { get; private set; }
    public double AvailableRamMB { get; private set; }
    public double GpuUsage { get; private set; }

    public event Action? MetricsUpdated;

    public ResourceMonitorService(ApiService? api = null)
    {
        _api = api;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += Timer_Tick;
        InitializeCounters();
    }

    private void InitializeCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MB");
            _cpuCounter.NextValue(); // First call always returns 0
        }
        catch
        {
            _cpuCounter = null;
            _ramCounter = null;
        }
    }

    /// <summary>Mulai monitoring resource</summary>
    public void Start(Guid? pcId = null)
    {
        _pcId = pcId;
        _timer.Start();
    }

    /// <summary>Hentikan monitoring</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (_cpuCounter != null)
                CpuUsage = Math.Round(_cpuCounter.NextValue(), 1);

            if (_ramCounter != null)
            {
                AvailableRamMB = _ramCounter.NextValue();
                var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0);
                RamUsagePercent = Math.Round(((totalRam - AvailableRamMB) / totalRam) * 100, 1);
            }

            // Kirim ke server jika tersedia
            if (_api != null && _pcId.HasValue)
            {
                await _api.UpdatePcResourceAsync(_pcId.Value, CpuUsage, GpuUsage, RamUsagePercent);
            }

            MetricsUpdated?.Invoke();
        }
        catch { /* Monitoring error - ignore */ }
    }

    /// <summary>Dapatkan informasi sistem</summary>
    public static SystemInfo GetSystemInfo()
    {
        var info = new SystemInfo
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            ClrVersion = Environment.Version.ToString(),
            WorkingSet = Environment.WorkingSet / (1024 * 1024)
        };
        return info;
    }
}

/// <summary>Informasi sistem client</summary>
public class SystemInfo
{
    public string MachineName { get; set; } = "";
    public string OSVersion { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string ClrVersion { get; set; } = "";
    public long WorkingSet { get; set; } // MB
}
