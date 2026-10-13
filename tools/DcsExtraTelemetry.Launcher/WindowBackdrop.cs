using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DcsExtraTelemetry.Launcher
{
    // Real translucency for the glass redesign.
    //
    // WPF has no backdrop blur of its own. On Windows 11 22H2+ the compositor can do it for
    // us via DWMWA_SYSTEMBACKDROP_TYPE (Mica / Acrylic); the window must be transparent
    // where we want the backdrop to show, which is why the glass palette's WindowBackground
    // is only used as the fallback fill.
    //
    // Usage in MainWindow.xaml.cs:
    //
    //     protected override void OnSourceInitialized(EventArgs e)
    //     {
    //         base.OnSourceInitialized(e);
    //         WindowBackdrop.Apply(this, ((App)Application.Current).IsDarkTheme);
    //     }
    //
    // and again from the ThemeChanged handler. Apply() is safe to call on Windows 10 and
    // older builds: it returns false and leaves the window with the solid fallback fill.
    internal static class WindowBackdrop
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaSystemBackdropType = 38;
        private const int DwmwaWindowCornerPreference = 33;

        // DWM_SYSTEMBACKDROP_TYPE
        private const int BackdropAuto = 0;
        private const int BackdropNone = 1;
        private const int BackdropMainWindow = 2;      // Mica
        private const int BackdropTransientWindow = 3; // Acrylic â€” heavier blur, what the mockups show
        private const int BackdropTabbedWindow = 4;

        // DWM_WINDOW_CORNER_PREFERENCE
        private const int CornerRound = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OsVersionInfo versionInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OsVersionInfo
        {
            public int Size;
            public int Major;
            public int Minor;
            public int Build;
            public int PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;
        }

        public static bool Apply(Window window, bool dark)
        {
            return Apply(window, dark, true);
        }

        // acrylic: true  -> DWMSBT_TRANSIENTWINDOW, the strong blur used in the mockups.
        // acrylic: false -> DWMSBT_MAINWINDOW (Mica), cheaper and calmer for always-open windows.
        public static bool Apply(Window window, bool dark, bool acrylic)
        {
            if (window == null) return false;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return false;      // call from OnSourceInitialized or later
            if (!IsBackdropSupported()) return false;

            // The HWND background must be transparent for the backdrop to composite through.
            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null) source.CompositionTarget.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;

            int darkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            int corner = CornerRound;
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            int backdrop = acrylic ? BackdropTransientWindow : BackdropMainWindow;
            return DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0;
        }

        public static void Clear(Window window)
        {
            if (window == null) return;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int backdrop = BackdropNone;
            DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }

        // Windows 11 build 22621 (22H2) is the first build with DWMWA_SYSTEMBACKDROP_TYPE.
        public static bool IsBackdropSupported()
        {
            try
            {
                var version = new OsVersionInfo { Size = Marshal.SizeOf(typeof(OsVersionInfo)) };
                return RtlGetVersion(ref version) == 0
                    && (version.Major > 10 || (version.Major == 10 && version.Build >= 22621));
            }
            catch
            {
                return false;
            }
        }
    }
}
