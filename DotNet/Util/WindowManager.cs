using ADLib.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace ADLib.Util;

using HWND = IntPtr;

public static class WindowManager
{
    private const uint SWP_NOMOVE = 0x0002;

    private const uint SWP_NOSIZE = 0x0001;

    public static bool BringWindowToFront(string title)
    {
        return BringWindowToFront(GetWindow(title));
    }

    private static HWND? GetWindow(string title)
    {
        foreach (var (handle, foundTitle) in OpenWindowGetter.GetOpenWindows())
        {
            if (string.IsNullOrEmpty(title) || title != foundTitle)
            {
                continue;
            }

            return handle;
        }

        return null;
    }

    //public bool IsClientInForeground()
    //{
    //    var handle = GetWindow();
    //    if (handle == null)
    //    {
    //        return false;
    //    }

    //    return handle.Value == GetForegroundWindow();
    //}

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static bool BringWindowToFront(HWND? handle)
    {
        if (handle == null)
        {
            GenLog.Error("Table not found, cannot perform any action");
            return false;
        }

        SetForegroundWindow(handle.Value);
        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("User32.dll")]
    private static extern int SetForegroundWindow(HWND point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

    [DllImport("USER32.DLL")]
    private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("USER32.DLL")]
    private static extern int GetWindowTextLength(HWND hWnd);

    [DllImport("USER32.DLL")]
    private static extern bool IsWindowVisible(HWND hWnd);

    [DllImport("USER32.DLL")]
    private static extern HWND GetShellWindow();

    private static class OpenWindowGetter
    {
        public static IDictionary<HWND, string> GetOpenWindows()
        {
            var shellWindow = GetShellWindow();
            var windows = new Dictionary<HWND, string>();

            EnumWindows(
                (hWnd, _) =>
                {
                    if (hWnd == shellWindow)
                    {
                        return true;
                    }

                    if (!IsWindowVisible(hWnd))
                    {
                        return true;
                    }

                    var length = GetWindowTextLength(hWnd);
                    if (length == 0)
                    {
                        return true;
                    }

                    var builder = new StringBuilder(length);
                    GetWindowText(hWnd, builder, length + 1);

                    windows[hWnd] = builder.ToString();
                    return true;
                },
                0);

            return windows;
        }
    }

    private delegate bool EnumWindowsProc(HWND hWnd, int lParam);
}