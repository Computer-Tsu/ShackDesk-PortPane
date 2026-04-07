using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using PortPane.Models;
using Serilog;

namespace PortPane.Services;

public interface IAudioService
{
    IReadOnlyList<AudioDeviceInfo> GetAllDevices();
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();
    string? GetDefaultPlaybackId();
    string? GetDefaultCaptureId();
    bool SetDefaultDevice(string deviceId);
}

public sealed record AudioDeviceInfo(
    string   Id,
    string   FriendlyName,
    DataFlow Flow,
    bool     IsDefault,
    bool     IsUsb,
    bool     IsRadioInterface);

public sealed class AudioService : IAudioService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly UsbDeviceDatabase  _usbDb;

    // USB keyword classification — matches spec requirements
    private static readonly string[] UsbKeywords =
    [
        "usb", "codec", "c-media", "cm108", "cm119", "cm119a",
        "digirig", "signalink", "rigblaster", "usb audio",
        "usb pnp sound device", "usb sound device",
        "microphone (usb)", "speakers (usb)"
    ];

    public AudioService(UsbDeviceDatabase usbDb) => _usbDb = usbDb;

    public IReadOnlyList<AudioDeviceInfo> GetAllDevices()      => GetDevices(DataFlow.All);
    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices() => GetDevices(DataFlow.Render);
    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()  => GetDevices(DataFlow.Capture);

    public string? GetDefaultPlaybackId() => TryGetDefault(DataFlow.Render)?.ID;
    public string? GetDefaultCaptureId()  => TryGetDefault(DataFlow.Capture)?.ID;

    private IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow flow)
    {
        try
        {
            string? defaultPlaybackId = GetDefaultPlaybackId();
            string? defaultCaptureId  = GetDefaultCaptureId();

            return _enumerator
                .EnumerateAudioEndPoints(flow, DeviceState.Active)
                .Select(d =>
                {
                    bool isDefault = d.ID == defaultPlaybackId || d.ID == defaultCaptureId;
                    bool isUsb     = ClassifyAsUsb(d.FriendlyName, d.Properties);
                    bool isRadio   = isUsb && IsRadioInterface(d.FriendlyName);

                    Log.Debug("Audio device: {Name} usb={Usb} radio={Radio}", d.FriendlyName, isUsb, isRadio);

                    return new AudioDeviceInfo(
                        Id:              d.ID,
                        FriendlyName:    d.FriendlyName,
                        Flow:            d.DataFlow,
                        IsDefault:       isDefault,
                        IsUsb:           isUsb,
                        IsRadioInterface: isRadio);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate audio devices");
            return [];
        }
    }

    /// <summary>
    /// Sets the Windows default audio endpoint for the given device ID.
    /// Uses NAudio's PolicyConfig COM interface — no admin rights required.
    /// </summary>
    public bool SetDefaultDevice(string deviceId)
    {
        try
        {
            PolicyConfigClient.SetDefaultEndpoint(deviceId, Role.Multimedia);
            PolicyConfigClient.SetDefaultEndpoint(deviceId, Role.Communications);
            Log.Information("Default audio device set: {DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set default audio device {DeviceId}", deviceId);
            return false;
        }
    }

    private MMDevice? TryGetDefault(DataFlow flow)
    {
        try { return _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia); }
        catch { return null; }
    }

    private static bool ClassifyAsUsb(string name, PropertyStore properties)
    {
        string lower = name.ToLowerInvariant();
        return UsbKeywords.Any(k => lower.Contains(k));
    }

    private bool IsRadioInterface(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("signalink")
            || lower.Contains("rigblaster")
            || lower.Contains("digirig")
            || lower.Contains("codec")
            || lower.Contains("cm108")
            || lower.Contains("cm119");
    }

    public void Dispose() => _enumerator.Dispose();
}

/// <summary>
/// Direct COM interop for IPolicyConfig — the Windows undocumented interface used to set
/// default audio endpoints. This approach is used by SoundSwitch, AudioDeviceCmdlets, and
/// other open-source audio tools. No admin rights required; works on Windows 10/11.
/// </summary>
[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
file interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
    [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
    [PreserveSig] int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
    [PreserveSig] int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
    [PreserveSig] int GetShareMode(string pszDeviceName, IntPtr pMode);
    [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
    [PreserveSig] int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetDefaultEndpoint(string pszDeviceName, uint eRole);
    [PreserveSig] int SetEndpointVisibility(string pszDeviceName, bool bVisible);
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
file class PolicyConfigComObject { }

file static class PolicyConfigClient
{
    public static void SetDefaultEndpoint(string deviceId, Role role)
    {
        var config = (IPolicyConfig)new PolicyConfigComObject();
        Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, (uint)role));
    }
}
