# Stops EarTrumpet without leaving dead icons in the notification area.
# This fork exits cleanly when asked (and removes its own icon). Anything still running after that
# (e.g. the Store build, which can't be asked) is killed, and the notification area is then swept
# with mouse-move messages so Explorer drops icons whose owner is gone. The real cursor is not moved.
param([switch]$KeepStore)

try {
    $exitRequest = [System.Threading.EventWaitHandle]::OpenExisting('Local\EarTrumpet.ExitRequest')
    $exitRequest.Set() | Out-Null
    $exitRequest.Dispose()
} catch [System.Threading.WaitHandleCannotBeOpenedException] {
    # Not running, or a build without the exit request.
}

$targets = Get-Process EarTrumpet -ErrorAction SilentlyContinue |
    Where-Object { -not ($KeepStore -and $_.Path -like '*\WindowsApps\*') }
$targets | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
$leftover = $targets | Where-Object { -not $_.HasExited }
if ($leftover) {
    $leftover | Stop-Process -Force
    $leftover | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

if (-not ('EarTrumpetTraySweep' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EarTrumpetTraySweep {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    struct RECT { public int Left, Top, Right, Bottom; }
    static void Sweep(IntPtr toolbar) {
        RECT rc;
        if (toolbar == IntPtr.Zero || !GetClientRect(toolbar, out rc)) return;
        for (int y = 0; y < rc.Bottom; y += 8)
            for (int x = 0; x < rc.Right; x += 8) {
                IntPtr ignored;
                SendMessageTimeout(toolbar, 0x0200 /* WM_MOUSEMOVE */, IntPtr.Zero, (IntPtr)((y << 16) | (x & 0xFFFF)), 0x0002 /* SMTO_ABORTIFHUNG */, 100, out ignored);
            }
    }
    public static void Run() {
        var tray = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
        var pager = FindWindowEx(FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null), IntPtr.Zero, "SysPager", null);
        Sweep(FindWindowEx(pager, IntPtr.Zero, "ToolbarWindow32", null));
        Sweep(FindWindowEx(FindWindowEx(IntPtr.Zero, IntPtr.Zero, "NotifyIconOverflowWindow", null), IntPtr.Zero, "ToolbarWindow32", null));
    }
}
'@
}
[EarTrumpetTraySweep]::Run()
