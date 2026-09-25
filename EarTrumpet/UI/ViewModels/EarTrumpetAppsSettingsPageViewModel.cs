using EarTrumpet.Interop;
using EarTrumpet.UI.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace EarTrumpet.UI.ViewModels
{
    // Settings > Apps: the apps in the mixer now, hidden apps and custom icons.
    public class EarTrumpetAppsSettingsPageViewModel : SettingsPageViewModel
    {
        public class AppRowViewModel
        {
            public string DisplayName { get; set; }
            public string Detail { get; set; }
            public IAppIconSource Icon { get; set; }
            public ICommand Hide { get; set; }
            public ICommand Restore { get; set; }
            public ICommand ChangeIcon { get; set; }
            public ICommand ResetIcon { get; set; }

            public Visibility DetailVisibility => string.IsNullOrEmpty(Detail) ? Visibility.Collapsed : Visibility.Visible;
            public Visibility HideVisibility => Hide == null ? Visibility.Collapsed : Visibility.Visible;
            public Visibility RestoreVisibility => Restore == null ? Visibility.Collapsed : Visibility.Visible;
            public Visibility ChangeIconVisibility => ChangeIcon == null ? Visibility.Collapsed : Visibility.Visible;
            public Visibility ResetIconVisibility => ResetIcon == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private class IconFile : IAppIconSource
        {
            public string IconPath { get; set; }
            public bool IsDesktopApp => true;
        }

        public ObservableCollection<AppRowViewModel> CurrentApps { get; } = new ObservableCollection<AppRowViewModel>();
        public ObservableCollection<AppRowViewModel> HiddenApps { get; } = new ObservableCollection<AppRowViewModel>();
        public ObservableCollection<AppRowViewModel> IconOverrides { get; } = new ObservableCollection<AppRowViewModel>();
        public Visibility CurrentAppsEmptyVisibility => CurrentApps.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility HiddenAppsEmptyVisibility => HiddenApps.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility IconOverridesEmptyVisibility => IconOverrides.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        public ICommand HideProgram { get; }

        private readonly AppSettings _settings;
        private readonly DeviceCollectionViewModel _devices;
        private readonly HashSet<INotifyCollectionChanged> _watchedAppLists = new HashSet<INotifyCollectionChanged>();

        public EarTrumpetAppsSettingsPageViewModel(AppSettings settings, DeviceCollectionViewModel devices) : base(null)
        {
            _settings = settings;
            _devices = devices;
            Title = Properties.Resources.AppsSettingsPageText;
            Glyph = "\xE71D";
            HideProgram = new RelayCommand(OnHideProgram);

            // Weak: the settings and device lists outlive this page. Handlers are instance methods,
            // since the weak events would let a lambda's closure be collected.
            WeakEventManager<AppSettings, EventArgs>.AddHandler(_settings, nameof(AppSettings.HiddenAppsChanged), OnChanged);
            WeakEventManager<AppSettings, EventArgs>.AddHandler(_settings, nameof(AppSettings.IconOverridesChanged), OnChanged);
            if (_devices != null)
            {
                CollectionChangedEventManager.AddHandler(_devices.AllDevices, OnChanged);
            }

            Refresh();
        }

        private void OnChanged(object sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            var current = new List<AppRowViewModel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in _devices?.AllDevices ?? Enumerable.Empty<DeviceViewModel>())
            {
                if (_watchedAppLists.Add(device.Apps))
                {
                    CollectionChangedEventManager.AddHandler(device.Apps, OnChanged);
                }

                foreach (var app in device.Apps)
                {
                    var key = AppKey.For(app);
                    if (key != null && seen.Add(key))
                    {
                        current.Add(new AppRowViewModel
                        {
                            DisplayName = app.DisplayName,
                            Detail = key == AppKey.SystemSounds ? null : key,
                            Icon = app,
                            Hide = new RelayCommand(() => _settings.HideApp(key)),
                            ChangeIcon = new RelayCommand(() => PickIcon(key)),
                            ResetIcon = _settings.GetIconOverride(key) == null ? null : new RelayCommand(() => _settings.SetIconOverride(key, null)),
                        });
                    }
                }
            }
            Replace(CurrentApps, current.OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase));

            Replace(HiddenApps, _settings.HiddenApps.Select(key => new AppRowViewModel
            {
                DisplayName = AppKey.GetDisplayName(key),
                Icon = IconFor(key),
                Restore = new RelayCommand(() => _settings.UnhideApp(key)),
            }));

            Replace(IconOverrides, _settings.IconOverrides.Where(o => !string.IsNullOrEmpty(o.App)).Select(o => new AppRowViewModel
            {
                DisplayName = AppKey.GetDisplayName(o.App),
                Icon = new IconFile { IconPath = o.IconPath },
                ChangeIcon = new RelayCommand(() => PickIcon(o.App)),
                ResetIcon = new RelayCommand(() => _settings.SetIconOverride(o.App, null)),
            }));

            RaisePropertyChanged(nameof(CurrentAppsEmptyVisibility));
            RaisePropertyChanged(nameof(HiddenAppsEmptyVisibility));
            RaisePropertyChanged(nameof(IconOverridesEmptyVisibility));
        }

        private static void Replace(ObservableCollection<AppRowViewModel> list, IEnumerable<AppRowViewModel> rows)
        {
            list.Clear();
            foreach (var row in rows)
            {
                list.Add(row);
            }
        }

        private IAppIconSource IconFor(string key)
        {
            var path = _settings.GetIconOverride(key);
            return path == null ? null : new IconFile { IconPath = path };
        }

        // The shell's Change Icon dialog: browses .ico, .exe and .dll files and returns the icon's position.
        private void PickIcon(string key)
        {
            var path = new StringBuilder(_settings.GetIconOverride(key) ?? @"%SystemRoot%\System32\imageres.dll", 1024);
            var index = Math.Max(0, Shlwapi.PathParseIconLocationW(path));
            if (Shell32.PickIconDlg(GetOwner(), path, (uint)path.Capacity, ref index) != 0)
            {
                var file = path.ToString();
                _settings.SetIconOverride(key, file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ? file : $"{file},{index}");
            }
        }

        private void OnHideProgram()
        {
            var dialog = new OpenFileDialog
            {
                Title = Properties.Resources.HideProgramDialogTitle,
                Filter = $"{Properties.Resources.ProgramsFileFilterText}|*.exe",
            };
            if (dialog.ShowDialog(Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)) == true)
            {
                _settings.HideApp(Path.GetFileName(dialog.FileName).ToLowerInvariant());
            }
        }

        private static IntPtr GetOwner()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            return window == null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        }
    }
}
