namespace HyperJav.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
        {
            // 直开设置窗体（调试/核对 ini 键位用）。
            Application.Run(new SettingsForm());
            return;
        }
        Application.Run(new MainForm());
    }
}
