using System.Windows.Forms;

namespace MarioClone2;

// Application bootstrap and WinForms entry point.
internal static class Program
{
    [STAThread]
    // Configures WinForms and starts the main game window.
    private static void Main()
    {
        // Applies default WinForms app configuration.
        ApplicationConfiguration.Initialize();
        // Launches the main game form.
        Application.Run(new GameForm());
    }
}
