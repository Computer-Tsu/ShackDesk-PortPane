using System.Diagnostics;
using System.Windows;

namespace PortPane.Views.Dialogs;

/// <summary>
/// Shown after settings reset is complete.
/// Offers to relaunch PortPane or let the user do it manually.
/// Neither button is IsDefault or IsCancel — both choices are intentional.
/// Escape is inert on this dialog.
/// </summary>
public partial class ResetCompleteDialog : Window
{
    public ResetCompleteDialog()
    {
        InitializeComponent();
    }

    private void NoRelaunch_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void Relaunch_Click(object sender, RoutedEventArgs e)
    {
        // Launch new instance first, then exit this one.
        // The new instance goes through the normal single-instance Mutex gate.
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }
        Application.Current.Shutdown();
    }
}
