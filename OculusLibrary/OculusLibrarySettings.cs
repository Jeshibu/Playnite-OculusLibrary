using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows.Media;

namespace OculusLibrary
{
    public class OculusLibrarySettings : ObservableObject
    {
        private bool useRevive = false;
        private bool useOculus = true;
        private string revivePath;
        private bool importOculusApp = true;
        private bool importRiftOnline = false;
        private bool importQuestOnline = false;
        private bool importGearGoOnline = false;
        private BackgroundSource backgroundSource = BackgroundSource.TrailerThumbnail;

        public bool UseOculus { get => useOculus; set => SetValue(ref useOculus, value); }
        public bool UseRevive { get => useRevive; set => SetValue(ref useRevive, value); }
        public string RevivePath { get => revivePath; set => SetValue(ref revivePath, value); }

        public bool ImportOculusAppGames { get => importOculusApp; set => SetValue(ref importOculusApp, value); }
        public bool ImportRiftOnline { get => importRiftOnline; set => SetOnlineImportValue(ref importRiftOnline, value); }
        public bool ImportQuestOnline { get => importQuestOnline; set => SetOnlineImportValue(ref importQuestOnline, value); }
        public bool ImportGearGoOnline { get => importGearGoOnline; set => SetOnlineImportValue(ref importGearGoOnline, value); }

        public BackgroundSource BackgroundSource { get => backgroundSource; set => SetValue(ref backgroundSource, value); }

        [DontSerialize]
        public bool ImportAnyOnline => ImportRiftOnline || ImportQuestOnline || ImportGearGoOnline;

        public int Version { get; set; } = 1;

        private void SetOnlineImportValue(ref bool property, bool value)
        {
            SetValue(ref property, value);
            OnPropertyChanged(nameof(ImportAnyOnline));
        }
    }

    public enum BackgroundSource
    {
        Hero,
        TrailerThumbnail,
        Screenshots,
    }

    public class OculusLibrarySettingsViewModel : PluginSettingsViewModel<OculusLibrarySettings, OculusLibraryPlugin>
    {
        public OculusLibrarySettingsViewModel(OculusLibraryPlugin plugin, IPlayniteAPI playniteApi) : base(plugin, playniteApi)
        {
            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<OculusLibrarySettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new OculusLibrarySettings() { Version = 3 };
                SeedRevivePath();
            }
            Settings.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.ImportAnyOnline))
            {
                OnPropertyChanged(nameof(AuthStatus));
            }
        }

        public void SeedRevivePath()
        {
            if (Settings.RevivePath != null)
                return;

            var program = Programs.GetReviveUninstallProgram();
            if (program?.InstallLocation == null)
                return;

            Settings.RevivePath = Path.Combine(program.InstallLocation, "ReviveInjector.exe");
        }

        public override bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (Settings.UseRevive && !File.Exists(Settings.RevivePath))
            {
                errors.Add("Invalid ReviveInjector.exe path");
                return false;
            }

            return true;
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        private void Login()
        {
            try
            {
                string loginUrl = "https://www.meta.com/login/?next=https%3A%2F%2Fsecure.oculus.com%2Fmy%2Fprofile%2F";
                List<Cookie> cookies = new List<Cookie>();
                using (var view = PlayniteApi.WebViews.CreateView(675, 540, Colors.Black))
                {
                    view.LoadingChanged += (s, e) =>
                    {
                        try
                        {
                            if (e.IsLoading) return;

                            var address = view.GetCurrentAddress();

                            if (address == "https://secure.oculus.com/my/profile/")
                                view.Close();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error logging into Oculus");
                        }
                    };

                    view.DeleteDomainCookies(".oculus.com");
                    view.DeleteDomainCookies(".www.meta.com");
                    view.Navigate(loginUrl);
                    view.OpenDialog();
                }

                OnPropertyChanged(nameof(AuthStatus));
            }
            catch (Exception e)
            {
                PlayniteApi.Dialogs.ShowErrorMessage("Error logging in to Oculus", "");
                Logger.Error(e, "Failed to authenticate user.");
            }
        }

        public AuthStatus AuthStatus
        {
            get
            {
                if (!Settings.ImportAnyOnline)
                    return AuthStatus.Disabled;

                var view = PlayniteApi.WebViews.CreateOffscreenView();
                try
                {
                    string profileUrl = "https://secure.oculus.com/my/profile/";
                    view.NavigateAndWait(profileUrl);
                    string actualUrl = view.GetCurrentAddress(); //this will be a login URL if not authenticated

                    if (actualUrl == profileUrl)
                        return AuthStatus.Ok;
                    else
                        return AuthStatus.AuthRequired;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to check Oculus auth status.");
                    return AuthStatus.Failed;
                }
                finally
                {
                    view.Dispose();
                }
            }
        }

        public IDictionary<BackgroundSource, string> BackgroundSourceOptions { get; } = new Dictionary<BackgroundSource, string>
        {
            { BackgroundSource.Hero, "Hero (slim, wide header image)" },
            { BackgroundSource.Screenshots, "Screenshot (random selection)" },
            { BackgroundSource.TrailerThumbnail, "Trailer thumbnail (is usually key art)" },
        };
    }

    public enum AuthStatus
    {
        Ok,
        Checking,
        AuthRequired,
        Failed,
        Disabled,
    }
}
