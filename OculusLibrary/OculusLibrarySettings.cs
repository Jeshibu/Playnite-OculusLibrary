using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace OculusLibrary;

public class OculusLibrarySettings : ObservableObject
{
    public bool UseOculus { get; set => SetValue(ref field, value); } = true;

    public bool UseRevive { get; set => SetValue(ref field, value); }
    public string RevivePath { get; set => SetValue(ref field, value); }

    public bool ImportOculusAppGames { get; set => SetValue(ref field, value); } = true;
    public bool ImportRiftOnline { get; set => SetOnlineImportValue(ref field, value); }
    public bool ImportQuestOnline { get; set => SetOnlineImportValue(ref field, value); }
    public bool ImportGearGoOnline { get; set => SetOnlineImportValue(ref field, value); }

    public BackgroundSource BackgroundSource { get; set => SetValue(ref field, value); } = BackgroundSource.TrailerThumbnail;

    public Branding Branding { get; set; } = Branding.Meta;

    [DontSerialize]
    public bool ImportAnyOnline => ImportRiftOnline || ImportQuestOnline || ImportGearGoOnline;

    public int Version { get; set; } = 1;

    private void SetOnlineImportValue(ref bool property, bool value)
    {
        if (property == value)
            return;

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

public enum Branding
{
    Oculus,
    Meta
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
            Settings = new OculusLibrarySettings { Version = 3 };
            SeedRevivePath();
        }
        Settings.PropertyChanged += Settings_PropertyChanged;
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
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
        errors = [];
        if (Settings.UseRevive && !File.Exists(Settings.RevivePath))
        {
            errors.Add("Invalid ReviveInjector.exe path");
            return false;
        }

        return true;
    }

    public RelayCommand<object> LoginCommand => new(a =>
    {
        Login();
    });

    private void Login()
    {
        try
        {
            string loginUrl = "https://www.meta.com/login/?next=https%3A%2F%2Fsecure.oculus.com%2Fmy%2Fprofile%2F";
            using var view = PlayniteApi.WebViews.CreateView(675, 540, Colors.Black);

            void OnViewOnLoadingChanged(object s, WebViewLoadingChangedEventArgs e)
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
            }

            view.LoadingChanged += OnViewOnLoadingChanged;

            view.DeleteDomainCookies(".oculus.com");
            view.DeleteDomainCookies(".www.meta.com");
            view.Navigate(loginUrl);
            view.OpenDialog();

            view.LoadingChanged -= OnViewOnLoadingChanged;

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
                const string profileUrl = "https://secure.oculus.com/my/profile/";
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

    public Branding[] BrandingOptions { get; } = [Branding.Meta, Branding.Oculus];

    public override void EndEdit()
    {
        OculusLibraryPlugin.UpdateYaml(Settings, Logger);
        base.EndEdit();
    }
}

public enum AuthStatus
{
    Ok,
    Checking,
    AuthRequired,
    Failed,
    Disabled,
}
