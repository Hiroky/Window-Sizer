using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowSizer;

/// <summary>一覧に表示するトップレベルウインドウの情報。最小化中は ClientSize が null。</summary>
internal sealed record WindowInfo(IntPtr Handle, string Title, string ProcessName, Size? ClientSize);

/// <summary>ウインドウの列挙と、クライアント領域サイズの変更を行う。</summary>
internal static class WindowResizer
{
    /// <summary>表示中のトップレベルウインドウを列挙する（本アプリ自身は除外）。</summary>
    public static List<WindowInfo> GetWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd) || IsCloaked(hWnd))
            {
                return true;
            }

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            if ((int)processId == Environment.ProcessId)
            {
                return true;
            }

            var title = GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title) || title == "Program Manager")
            {
                return true;
            }

            windows.Add(new WindowInfo(hWnd, title, GetProcessName(processId), GetClientSize(hWnd)));
            return true;
        }, IntPtr.Zero);

        return windows.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// 指定ウインドウのクライアント領域を <paramref name="clientSize"/> にする。
    /// 最小化・最大化中の場合は通常状態に戻してから適用する。
    /// </summary>
    /// <returns>適用後の実際のクライアント領域サイズ（アプリ側の最小・最大制限で変わることがある）。</returns>
    public static Size ResizeClientArea(IntPtr hWnd, Size clientSize)
    {
        if (!NativeMethods.IsWindow(hWnd))
        {
            throw new InvalidOperationException("ウインドウが見つかりません。一覧を更新してください。");
        }

        if (NativeMethods.IsIconic(hWnd) || NativeMethods.IsZoomed(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        }

        if (!NativeMethods.GetWindowRect(hWnd, out var windowRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!NativeMethods.GetClientRect(hWnd, out var clientRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        // 外側の矩形とクライアント領域の差が、タイトルバー・枠線・メニューバーなどの大きさ。
        // SetWindowPos は外側の矩形で指定するため、要求サイズにこの差を足して換算する。
        int frameWidth = windowRect.Width - clientRect.Width;
        int frameHeight = windowRect.Height - clientRect.Height;

        bool moved = NativeMethods.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            windowRect.Left,
            windowRect.Top,
            clientSize.Width + frameWidth,
            clientSize.Height + frameHeight,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        if (!moved)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        NativeMethods.GetClientRect(hWnd, out clientRect);
        return new Size(clientRect.Width, clientRect.Height);
    }

    private static Size? GetClientSize(IntPtr hWnd)
    {
        if (NativeMethods.IsIconic(hWnd) || !NativeMethods.GetClientRect(hWnd, out var rect))
        {
            return null;
        }

        return new Size(rect.Width, rect.Height);
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // 権限不足などで名前を取得できない場合
            return "-";
        }
    }

    // 別の仮想デスクトップなど、DWM が非表示にしているウインドウを除外する
    private static bool IsCloaked(IntPtr hWnd) =>
        NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
        && cloaked != 0;
}
