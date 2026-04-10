# Playnite-OculusLibrary

Plugin for Playnite to add support for importing games from the Oculus store.

> [!CAUTION]
> This can cause crashes due to some hard-to-pinpoint bug in the Playnite browser component.
> If you're encountering crashes using this plugin, they can likely be resolved by pressing `main menu > Settings > Advanced > Clear web cache`. If that doesn't work, open an issue to let me know.

## Installation ##

In Playnite, go to `main menu > Add-ons > Browse > Libraries` and install from there.
Alternatively, download the latest release zip file and extract to your Playnite/Extensions folder. This method won't notify you of updates from within Playnite though.

## Configuration ##

The plugin should pickup the installation location from the Windows registry and automatically scan for your installed games. If you also want to import Quest/Gear VR/Oculus Go games and uninstalled Rift games, go to `main menu > Add-ons > Settings > Extension settings > Libraries > Meta/Oculus`.
There, you can also configure:
- If you want to run games via Revive.
- Set your branding to Meta or Oculus (default is Meta)