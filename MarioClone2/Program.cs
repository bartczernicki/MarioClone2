using System.Windows.Forms;

namespace MarioClone2;

// Application bootstrap for the WinForms client.
// This type is intentionally minimal so startup order is explicit.
internal static class Program
{
    [STAThread]
    // STA is required by WinForms/COM interop features used by UI controls.
    // Main sets up framework defaults, then enters the window message loop.
    private static void Main()
    {
        // Applies .NET WinForms defaults (DPI, default font behavior, rendering setup).
        ApplicationConfiguration.Initialize();
        // Starts the blocking UI loop; process exits when GameForm closes.
        Application.Run(new GameForm());
    }
}
