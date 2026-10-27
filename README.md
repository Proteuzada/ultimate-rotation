# Ultimate Rotation Guide

Dalamud plugin for Proteu. It reads one player's casts from a public FFLogs ultimate pull and shows that sequence in game. The in-game UI is English.

The guide keeps ability order and phase names. It drops downtime, Sprint, Peloton, and auto-attacks. When you are in the selected ultimate, it starts at the pull and stays on the fight clock. Leaving that zone or a wipe resets it. Outside the duty, it still advances when you use the expected action, or when you press Skip.

Automatic rotation is off by default. Turn it on with the Automatic rotation checkbox in `/guide` or on the overlay. During the pull it presses the current step for your character when the fight clock reaches that step, then moves on. If the action cannot be used, that step is skipped. It does not move you, target other players, or inject packets.

## Configure

Create your own FFLogs API v2 client at <https://www.fflogs.com/api/clients/>. In game, run `/guide` and paste the client id and secret. They are saved in the local Dalamud config and are not part of this source tree. `/guia` is an alias of `/guide`.

Then pick an ultimate and a job. The plugin loads that job's public FFLogs rDPS parses for the encounter and groups them by percentile (100, then 99, then 98, and so on). Each row shows that parse number. Pick a row to see that player's ability timeline. Enter that ultimate and the guide starts when the pull starts. It resets if you leave the zone or wipe. Leave Automatic rotation unchecked unless you want the plugin to press those steps for you.

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

`/xlplugins` → Dev Tools → Installed Dev Plugins → enable **Ultimate Rotation Guide**.

`/guide` opens the picker. `/guide overlay` shows the guide. `/guia` does the same.

## Install from Dalamud

Dalamud → `/xlsettings` → Experimental → Custom Plugin Repositories. Paste this URL:

```
https://raw.githubusercontent.com/Proteuzada/ultimate-rotation/main/repo.json
```

Then install **Ultimate Rotation Guide** from the plugin installer.

That file is the custom repository manifest. Pushing a `v*` tag, or publishing a GitHub release, builds `UltimateRotation.dll`, uploads `UltimateRotation.zip`, and updates `repo.json` with that zip.
