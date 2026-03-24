using System.Diagnostics;
using Microsoft.Win32;
using Serilog;

namespace PortPane.Services;

public interface IPuttyService
{
    bool IsPuttyAvailable { get; }
    string? PuttyPath     { get; }
    bool Launch(string portName, int baudRate);
}

public sealed class PuttyService : IPuttyService
{
    public bool    IsPuttyAvailable { get; }
    public string? PuttyPath        { get; }

    public PuttyService()
    {
        PuttyPath        = Detect();
        IsPuttyAvailable = PuttyPath is not null;
        Log.Debug("PuTTY available={Available} path={Path}", IsPuttyAvailable, PuttyPath);
    }

    /// <summary>
    /// Launches PuTTY in serial mode pre-configured for the given COM port and baud rate.
    /// Command: putty.exe -serial COMx -sercfg 9600,8,n,1,N
    /// </summary>
    public bool Launch(string portName, int baudRate)
    {
        if (PuttyPath is null || !IsPuttyAvailable) return false;

        string args = $"-serial {portName} -sercfg {baudRate},8,n,1,N";
        Log.Information("Launching PuTTY: {Path} {Args}", PuttyPath, args);

        try
        {
            Process.Start(new ProcessStartInfo(PuttyPath, args)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch PuTTY");
            return false;
        }
    }

    private static string? Detect()
    {
        // 1. Registry: HKLM\SOFTWARE\SimonTatham\PuTTY (installation marker)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SimonTatham\PuTTY");
            if (key is not null)
            {
                // Installation exists; find the exe in known locations
                string? found = FindExe();
                if (found is not null) return found;
            }
        }
        catch { /* ignore registry errors */ }

        // 2. Known filesystem paths
        return FindExe();
    }

    private static string? FindExe()
    {
        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PuTTY", "putty.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "PuTTY", "putty.exe"),
            // Scoop / portable installs
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "scoop", "shims", "putty.exe")
        ];

        foreach (string path in candidates)
            if (File.Exists(path)) return path;

        return null;
    }
}
