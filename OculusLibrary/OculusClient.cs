using Playnite.Common;
using Playnite.SDK;
using System.Diagnostics;
using System.IO;

namespace OculusLibrary;

internal class OculusClient(IPlayniteAPI playniteApi, string iconPath) : LibraryClient
{
    private bool _uninstallEntryFetched = false;
    private UninstallProgram _oculusUninstallEntry;

    private UninstallProgram OculusUninstallEntry
    {
        get
        {
            if (_uninstallEntryFetched || _oculusUninstallEntry != null) //don't keep trying if it's not installed, or return cached value
                return _oculusUninstallEntry;

            _uninstallEntryFetched = true;
            return _oculusUninstallEntry = Programs.GetOculusUninstallProgram();
        }

        set => _oculusUninstallEntry = value;
    }

    public override bool IsInstalled => OculusUninstallEntry != null;
    public override string Icon => iconPath;

    public override void Open()
    {
        if (!IsInstalled)
            return;

        var path = Path.Combine(OculusUninstallEntry.InstallLocation, @"Support\oculus-client\OculusClient.exe");
        if (!File.Exists(path))
        {
            playniteApi.Dialogs.ShowErrorMessage($"Could not find {path}");
            return;
        }

        Process.Start(path);
    }
}
