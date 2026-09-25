using EarTrumpet.DataModel.Audio;
using EarTrumpet.Interop;
using EarTrumpet.UI.ViewModels;
using System;
using System.IO;
using System.Text;

namespace EarTrumpet.UI.Helpers
{
    // Identifies an app for AppSettings.HiddenApps by its executable file name (e.g. "steam.exe"),
    // so an app stays hidden across restarts and process ids. Packaged apps use their process
    // executable too, rather than their versioned package install path.
    public static class HiddenAppKey
    {
        public const string SystemSounds = "#systemsounds";

        public static string For(IAudioDeviceSession session) =>
            For(session.ProcessId, session.IsSystemSoundsSession, session.IsDesktopApp, session.ExeName);

        public static string For(IAppItemViewModel app) =>
            For(app.ProcessId, app.ProcessId == 0, app.IsDesktopApp, app.ExeName);

        public static string GetDisplayName(string key) =>
            key == SystemSounds ? Properties.Resources.SystemSoundsDisplayName : key;

        private static string For(int processId, bool isSystemSounds, bool isDesktopApp, string exeName)
        {
            if (isSystemSounds)
            {
                return SystemSounds;
            }

            var imagePath = GetProcessImagePath(processId);
            if (!string.IsNullOrEmpty(imagePath))
            {
                return Path.GetFileName(imagePath).ToLowerInvariant();
            }

            // The process is gone; desktop apps still carry their exe name (without extension).
            return isDesktopApp && !string.IsNullOrWhiteSpace(exeName) ? (exeName + ".exe").ToLowerInvariant() : null;
        }

        private static string GetProcessImagePath(int processId)
        {
            if (processId <= 0)
            {
                return null;
            }

            var handle = Kernel32.OpenProcess(Kernel32.ProcessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var buffer = new StringBuilder(1024);
                uint length = (uint)buffer.Capacity;
                return Kernel32.QueryFullProcessImageName(handle, 0, buffer, ref length) != 0 ? buffer.ToString() : null;
            }
            finally
            {
                Kernel32.CloseHandle(handle);
            }
        }
    }
}
