using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary
{
    public class OculusLibrarySettings : ObservableObject
    {
        private bool useRevive = false;
        private bool useOculus = true;
        private string revivePath;

        public bool UseOculus { get => useOculus; set => SetValue(ref useOculus, value); }
        public bool UseRevive { get => useRevive; set => SetValue(ref useRevive, value); }
        public string RevivePath { get => revivePath; set => SetValue(ref revivePath, value); }
        public int Version { get; set; } = 1;
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
                Settings = new OculusLibrarySettings();
                SeedRevivePath();
            }
        }

        public void SeedRevivePath()
        {
            if (Settings.RevivePath != null)
                return;

            var program = Programs.GetReviveUninstallProgram();
            if (program == null)
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
    }
}
