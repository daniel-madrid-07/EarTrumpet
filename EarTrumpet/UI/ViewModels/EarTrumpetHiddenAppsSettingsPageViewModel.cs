using EarTrumpet.UI.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace EarTrumpet.UI.ViewModels
{
    public class EarTrumpetHiddenAppsSettingsPageViewModel : SettingsPageViewModel
    {
        public class HiddenAppViewModel
        {
            public string DisplayName { get; set; }
            public ICommand Restore { get; set; }
        }

        public ObservableCollection<HiddenAppViewModel> HiddenApps { get; } = new ObservableCollection<HiddenAppViewModel>();
        public Visibility ListVisibility => HiddenApps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EmptyVisibility => HiddenApps.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        private readonly AppSettings _settings;

        public EarTrumpetHiddenAppsSettingsPageViewModel(AppSettings settings) : base(null)
        {
            _settings = settings;
            Title = Properties.Resources.HiddenAppsSettingsPageText;
            Glyph = "\xED1A";

            WeakEventManager<AppSettings, EventArgs>.AddHandler(_settings, nameof(AppSettings.HiddenAppsChanged), OnHiddenAppsChanged);
            Refresh();
        }

        // An instance method, not a lambda: the weak event would let a lambda's closure be collected.
        private void OnHiddenAppsChanged(object sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            HiddenApps.Clear();
            foreach (var key in _settings.HiddenApps)
            {
                HiddenApps.Add(new HiddenAppViewModel
                {
                    DisplayName = AppKey.GetDisplayName(key),
                    Restore = new RelayCommand(() => _settings.UnhideApp(key)),
                });
            }

            RaisePropertyChanged(nameof(ListVisibility));
            RaisePropertyChanged(nameof(EmptyVisibility));
        }
    }
}
