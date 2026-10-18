namespace WindowSizer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 座標を物理ピクセルで扱うため、画面を作る前に DPI 対応を有効にする
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
