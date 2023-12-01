using Playnite.Common;
using Playnite.SDK;
using System.Diagnostics;
using System.IO;

namespace OculusLibrary
{
    class OculusClient : LibraryClient
    {
        bool uninstallEntryFetched = false;
        private UninstallProgram oculusUninstallEntry;
        private readonly IPlayniteAPI playniteAPI;

        private UninstallProgram OculusUninstallEntry
        {
            get
            {
                if (uninstallEntryFetched) //don't keep trying if it's not installed
                    return oculusUninstallEntry;

                if (oculusUninstallEntry == null)
                {
                    oculusUninstallEntry = Programs.GetOculusUninstallProgram();
                    uninstallEntryFetched = true;
                }

                return oculusUninstallEntry;
            }

            set => oculusUninstallEntry = value;
        }

        public override bool IsInstalled => OculusUninstallEntry != null;
        public override string Icon => OculusLibraryPlugin.IconPath;

        public OculusClient(IPlayniteAPI playniteAPI)
        {
            this.playniteAPI = playniteAPI;
        }

        public override void Open()
        {
            if (!IsInstalled)
                return;

            var path = Path.Combine(OculusUninstallEntry.InstallLocation, @"Support\oculus-client\OculusClient.exe");
            if (!File.Exists(path))
            {
                playniteAPI.Dialogs.ShowErrorMessage($"Could not find {path}");
                return;
            }

            Process.Start(path);
        }
    }
}
