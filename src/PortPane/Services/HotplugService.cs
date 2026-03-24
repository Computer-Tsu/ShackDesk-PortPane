using System.Management;
using Serilog;

namespace PortPane.Services;

/// <summary>
/// Monitors USB device hotplug events via WMI event subscriptions.
/// Raises DeviceArrived / DeviceRemoved when USB devices connect or disconnect.
/// </summary>
public interface IHotplugService : IDisposable
{
    event EventHandler<HotplugEventArgs>? DeviceArrived;
    event EventHandler<HotplugEventArgs>? DeviceRemoved;
    void Start();
    void Stop();
}

public sealed record HotplugEventArgs(string DeviceId, string? Description, DateTime Timestamp);

public sealed class HotplugService : IHotplugService
{
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;

    public event EventHandler<HotplugEventArgs>? DeviceArrived;
    public event EventHandler<HotplugEventArgs>? DeviceRemoved;

    public void Start()
    {
        try
        {
            _arrivalWatcher = CreateWatcher("__InstanceCreationEvent", "Win32_USBHub");
            _arrivalWatcher.EventArrived += OnArrival;
            _arrivalWatcher.Start();

            _removalWatcher = CreateWatcher("__InstanceDeletionEvent", "Win32_USBHub");
            _removalWatcher.EventArrived += OnRemoval;
            _removalWatcher.Start();

            Log.Debug("HotplugService started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start HotplugService");
        }
    }

    public void Stop()
    {
        _arrivalWatcher?.Stop();
        _removalWatcher?.Stop();
        Log.Debug("HotplugService stopped");
    }

    private static ManagementEventWatcher CreateWatcher(string eventClass, string targetClass)
    {
        string query = $"SELECT * FROM {eventClass} WITHIN 2 WHERE TargetInstance ISA '{targetClass}'";
        return new ManagementEventWatcher(new WqlEventQuery(query));
    }

    private void OnArrival(object sender, EventArrivedEventArgs e)
    {
        var args = BuildArgs(e);
        Log.Information("USB device arrived: {DeviceId}", args.DeviceId);
        DeviceArrived?.Invoke(this, args);
    }

    private void OnRemoval(object sender, EventArrivedEventArgs e)
    {
        var args = BuildArgs(e);
        Log.Information("USB device removed: {DeviceId}", args.DeviceId);
        DeviceRemoved?.Invoke(this, args);
    }

    private static HotplugEventArgs BuildArgs(EventArrivedEventArgs e)
    {
        var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        return new HotplugEventArgs(
            DeviceId:    instance["DeviceID"]?.ToString() ?? string.Empty,
            Description: instance["Description"]?.ToString(),
            Timestamp:   DateTime.Now);
    }

    public void Dispose()
    {
        Stop();
        _arrivalWatcher?.Dispose();
        _removalWatcher?.Dispose();
    }
}
