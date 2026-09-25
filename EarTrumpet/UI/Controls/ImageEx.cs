using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.Helpers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EarTrumpet.UI.Controls
{
    public class ImageEx : Image
    {
        public IAppIconSource SourceEx { get => (IAppIconSource)GetValue(SourceExProperty); set => SetValue(SourceExProperty, value); }
        public static readonly DependencyProperty SourceExProperty = DependencyProperty.Register(
          "SourceEx", typeof(IAppIconSource), typeof(ImageEx), new PropertyMetadata(null, new PropertyChangedCallback(OnSourceExChanged)));

        private uint _dpi;
        private static readonly string _windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        private static readonly string _systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);

        public ImageEx()
        {
            DpiChanged += OnDpiChanged;
            Loaded += (_, __) => OnSourceExChanged();
        }

        private void OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (IsLoaded)
            {
                var nextDpi = GetWindowDpi();
                if (nextDpi != _dpi)
                {
                    _dpi = nextDpi;
                    OnSourceExChanged();
                }
            }
        }

        private void OnSourceExChanged()
        {
            if (SourceEx != null && IsLoaded)
            {
                Source = LoadImage(SourceEx.IconPath, SourceEx.IsDesktopApp);
            }
        }

        private ImageSource LoadImage(string path, bool isDesktopApp)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    path = Environment.ExpandEnvironmentVariables(path.TrimStart('@'));

                    var scale = GetWindowDpi() / (double)96;
                    // Packaged apps normally carry an AppUserModelId, but a session can set its own
                    // icon file (e.g. "C:\app\logo.ico,0"), which must be loaded like a desktop icon.
                    if (!isDesktopApp && !IsFilePath(path))
                    {
                        return LoadShellIcon(path, isDesktopApp, (int)(Width * scale), (int)(Height * scale));
                    }
                    else
                    {
                        var iconPath = new StringBuilder(path);
                        int iconIndex = Shlwapi.PathParseIconLocationW(iconPath);

                        if (iconIndex != 0)
                        {
                            using (var icon = IconHelper.LoadIconResource(iconPath.ToString(), Math.Abs(iconIndex), (int)(Width * scale), (int)(Height * scale)))
                            {
                                Trace.WriteLine($"ImageEx LoadImage {icon?.Size.Width}x{icon?.Size.Height} {path}");
                                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            }
                        }
                        else
                        {
                            // An explicit ",0" index: load the file itself.
                            if (!File.Exists(path) && File.Exists(iconPath.ToString()))
                            {
                                path = iconPath.ToString();
                            }

                            // libmpv-based applications, like Plex, may set an invalid indirect icon path
                            // https://github.com/mpv-player/mpv/issues/7269
                            // (e.g. C:\Program Files\Plex\Plex.exe,-IDI_ICON1)
                            //
                            // The legacy volume mixer falls back to enumerating icons in the image and
                            // selecting an icon that 'best fits the current display device'. We will
                            // mimic this behavior by stripping off the invalid resource identifier and
                            // asking the shell for an appropriate icon.

                            if (path.Contains(",-"))
                            {
                                path = path.Remove(path.LastIndexOf(",-"));
                            }
                            return LoadShellIcon(path, isDesktopApp, (int)(Width * scale), (int)(Height * scale));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"ImageEx LoadImage Failed: {path} {ex}");
                }
            }
            return null;
        }

        public static ImageSource LoadShellIcon(string path, bool isDesktopApp, int cx, int cy)
        {
            path = CanonicalizePath(path);

            IShellItem2 shellItem;
            try
            {
                shellItem = Shell32.SHCreateItemInKnownFolder(FolderIds.AppsFolder, Shell32.KF_FLAG_DONT_VERIFY, path, typeof(IShellItem2).GUID);
            }
            catch(Exception)
            {
                if (!isDesktopApp)
                {
                    Trace.WriteLine($"ImageEx LoadShellIcon SHCreateItemInKnownFolder failed for non-desktop app ({path}).");
                }
                shellItem = Shell32.SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItem2).GUID);
            }

            ((IShellItemImageFactory)shellItem).GetImage(new SIZE { cx = cx, cy = cy }, SIIGBF.SIIGBF_RESIZETOFIT, out var bmp);
            try
            {
                var ret = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                Trace.WriteLine($"ImageEx LoadShellIcon {cx}x{cy} {path}");
                return ret;
            }
            finally
            {
                Gdi32.DeleteObject(bmp);
            }
        }

        private static string CanonicalizePath(string path)
        {
            if (Path.GetDirectoryName(path).StartsWith(_systemPath, StringComparison.InvariantCultureIgnoreCase))
            {
                path = Path.Combine(_windowsPath, "sysnative", path.Substring(_systemPath.Length + 1));
            }

            //
            // Microsoft includes garbage CortanaUI app assets (\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\Cortana.UI\Assets\App)
            // so replace the appid with one that has better (than nothing) assets.
            //
            // Ref: https://github.com/File-New-Project/EarTrumpet/issues/1259
            //
            if (path.Equals("MicrosoftWindows.Client.CBS_cw5n1h2txyewy!CortanaUI", StringComparison.InvariantCultureIgnoreCase))
            {
                path = "MicrosoftWindows.Client.CBS_cw5n1h2txyewy!PackageMetadata";
            }

            return path;
        }

        private static bool IsFilePath(string path)
        {
            try
            {
                var iconPath = new StringBuilder(path);
                Shlwapi.PathParseIconLocationW(iconPath);
                return Path.IsPathRooted(iconPath.ToString());
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void OnSourceExPropertyChanged(object sender, PropertyChangedEventArgs e) =>
            Dispatcher.BeginInvoke((Action)OnSourceExChanged);

        private uint GetWindowDpi() => User32.GetDpiForWindow(((HwndSource)PresentationSource.FromVisual(this)).Handle);
        private static void OnSourceExChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = (ImageEx)d;

            // Sessions often set their icon after they appear, so follow IconPath changes (weakly).
            if (e.OldValue is INotifyPropertyChanged oldSource)
            {
                PropertyChangedEventManager.RemoveHandler(oldSource, image.OnSourceExPropertyChanged, nameof(IAppIconSource.IconPath));
            }
            if (e.NewValue is INotifyPropertyChanged newSource)
            {
                PropertyChangedEventManager.AddHandler(newSource, image.OnSourceExPropertyChanged, nameof(IAppIconSource.IconPath));
            }

            image.OnSourceExChanged();
        }
    }
}
