# Guia de Rotação Ultimate

Dalamud plugin for Proteu. It reads one player's casts from a public FFLogs ultimate pull and shows that sequence in game. The in-game UI is Brazilian Portuguese.

The guide keeps ability order and phase names. It drops exact timestamps, downtime, Sprint, Peloton, and auto-attacks. It advances when you use the expected action, or when you press Pular. It never presses a button for you.

## Configure

Create your own FFLogs API v2 client at <https://www.fflogs.com/api/clients/>. In game, run `/guia` and paste the client id and secret. They are saved in the local Dalamud config and are not part of this source tree.

Then pick an ultimate, load a speed ranking or paste a report code, pick a pull and a player, and press Montar guia.

## Build and load

Requires the .NET 10 SDK. `Dalamud.NET.Sdk` 15 targets `net10.0-windows` and API level 15. It compiles against the Dalamud assemblies XIVLauncher already downloaded:

- Windows: `%AppData%\XIVLauncher\addon\Hooks\dev\`
- Linux: `~/.xlcore/dalamud/Hooks/dev/`
- or set `DALAMUD_HOME` to that folder

From this directory:

```bash
dotnet build UltimateRotation.sln -c Release
```

The dev-plugin folder is `UltimateRotation/bin/Release/`. It must contain `UltimateRotation.dll`, `UltimateRotation.Core.dll`, and `UltimateRotation.json`.

Dalamud → `/xlsettings` → Experimental → Dev Plugin Locations. Add the full path to `UltimateRotation.dll`.

`/xlplugins` → Dev Tools → Installed Dev Plugins → enable **Guia de Rotação Ultimate**.

`/guia` opens the picker. `/guia overlay` shows the guide.

## Install from Dalamud

Dalamud → `/xlsettings` → Experimental → Custom Plugin Repositories. Paste this URL:

```
https://raw.githubusercontent.com/Proteuzada/ultimate-rotation/main/repo.json
```

Then install **Guia de Rotação Ultimate** from the plugin installer.

That file is the custom repository manifest. Pushing a `v*` tag, or publishing a GitHub release, builds `UltimateRotation.dll`, uploads `UltimateRotation.zip`, and updates `repo.json` with that zip.
