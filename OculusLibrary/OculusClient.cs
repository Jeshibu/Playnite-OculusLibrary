using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary
{
    class OculusClient : LibraryClient
    {
        bool uninstallEntryFetched = false;
        private UninstallProgram oculusUninstallEntry;

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

        public override void Open()
        {
            if (!IsInstalled)
                return;

            var path = Path.Combine(OculusUninstallEntry.InstallLocation, "OculusClient.exe");
            Process.Start(path);
        }
    }
}
