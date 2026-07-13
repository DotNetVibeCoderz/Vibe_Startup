using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;

namespace PCHub.Shared.Services;

/// <summary>
/// IoT Integration Service - smart lamp, AC, lock controller.
/// Dapat berkomunikasi via MQTT atau Serial Port (COM/USB).
/// </summary>
public class IoTService
{
    private readonly List<IoTDeviceDto> _devices = [];
    private bool _mqttConnected;

    public IoTService()
    {
        // Register simulated devices
        _devices.Add(new IoTDeviceDto("lamp-01", "Smart Lamp 1", IoTDeviceType.SmartLamp, false, "Offline"));
        _devices.Add(new IoTDeviceDto("lamp-02", "Smart Lamp 2", IoTDeviceType.SmartLamp, false, "Offline"));
        _devices.Add(new IoTDeviceDto("ac-01", "Smart AC 1", IoTDeviceType.SmartAC, false, "Offline"));
        _devices.Add(new IoTDeviceDto("lock-01", "Main Door Lock", IoTDeviceType.SmartLock, false, "Offline"));
        _devices.Add(new IoTDeviceDto("relay-01", "PC Power Relay", IoTDeviceType.Relay, false, "Offline"));
    }

    /// <summary>Dapatkan semua perangkat IoT</summary>
    public Task<List<IoTDeviceDto>> GetDevicesAsync()
    {
        return Task.FromResult(_devices.ToList());
    }

    /// <summary>Kirim command ke perangkat IoT</summary>
    public async Task<bool> SendCommandAsync(IoTCommandRequest command)
    {
        var device = _devices.FirstOrDefault(d => d.DeviceId == command.DeviceId);
        if (device == null) return false;

        // Update status simulasi
        var status = command.Command.ToLower() switch
        {
            "on" => "On",
            "off" => "Off",
            "toggle" => (device.Status == "On" ? "Off" : "On"),
            "lock" => "Locked",
            "unlock" => "Unlocked",
            _ => device.Status
        };

        var idx = _devices.FindIndex(d => d.DeviceId == command.DeviceId);
        if (idx >= 0)
        {
            _devices[idx] = device with { Status = status, IsOnline = true };
        }

        await Task.Delay(200); // Simulasi latency
        return true;
    }

    /// <summary>Auto-control: nyalakan lampu saat PC aktif</summary>
    public async Task AutoControlOnPcActive(bool pcActive)
    {
        if (pcActive)
        {
            await SendCommandAsync(new IoTCommandRequest("lamp-01", "on", null));
            await SendCommandAsync(new IoTCommandRequest("ac-01", "on", null));
        }
        else
        {
            await SendCommandAsync(new IoTCommandRequest("lamp-01", "off", null));
            await SendCommandAsync(new IoTCommandRequest("ac-01", "off", null));
        }
    }

    /// <summary>Simulasi MQTT connect</summary>
    public async Task<bool> ConnectMqttAsync(string brokerUrl = "localhost", int port = 1883)
    {
        await Task.Delay(500);
        _mqttConnected = true;
        foreach (var i in Enumerable.Range(0, _devices.Count))
        {
            _devices[i] = _devices[i] with { IsOnline = true };
        }
        return true;
    }

    public Task DisconnectMqttAsync()
    {
        _mqttConnected = false;
        return Task.CompletedTask;
    }
}
