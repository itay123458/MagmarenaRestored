# Magmarena Restored

Brings back Gorilla Tag's old Magmarena through the entrance in Forest. Works in private rooms, including modded private rooms.

## Features

- Restores the Forest entrance and tunnel to the original arena.
- Enables the original ball, teams, goals, scoreboards, and reset controls.
- Fixes the old arena's dark, patchy lighting and adjusts tunnel brightness.
- Closes the entrance and restores changed objects when you leave the private room.

## Requirements

- Gorilla Tag on Windows/Steam, build **26061329**.
- BepInEx **5** installed.
- A private room using the **PUN** networking backend.

Everyone playing, including the host, needs this mod. It cannot open the map for players who do not have it installed. Public rooms and unsupported game versions/backends are disabled.

## Install

1. Download the ZIP from [Releases](https://github.com/itay123458/MagmarenaRestored/releases).
2. Close Gorilla Tag.
3. Extract the ZIP into your Gorilla Tag folder. The DLL belongs at `BepInEx/plugins/MagmarenaPrivate/MagmarenaPrivate.dll`.
4. Start the game, join a private room, and enter Magmarena from Forest.

Use one copy of the DLL. If you renamed an older copy, remove it before installing this version.

To uninstall, close the game and remove the mod's DLL.

## Settings

The config file is `BepInEx/config/local.magmarena.private.cfg`.

| Setting | Default | What it does |
| --- | --- | --- |
| `FixArenaLighting` | `true` | Repairs the old arena's baked-lighting artifacts. |
| `TunnelBrightness` | `0.45` | Adjusts tunnel brightness, from `0.15` to `1.0`. |
| `ArenaBrightness` | `0.75` | Adjusts arena brightness, from `0.15` to `1.0`. |

Brightness changes reload while you are in a private room.

## Compatibility

The entrance, tunnel, lighting, arena loading, and travel in both directions have been checked in game. Tested with two players in a private room. Late joins and host changes have not been separately verified.

The mod uses arena content already present in the supported game build. Game updates or other map restoration mods may stop it from loading. If the entrance stays closed, check `BepInEx/LogOutput.log` for `Magmarena`.

## Building

From Windows PowerShell, with Gorilla Tag and BepInEx installed:

```powershell
./test.ps1
./build.ps1
./package.ps1
```

For a different game location, pass `-GamePath` to `build.ps1`. The build uses the Windows .NET Framework compiler and reads dependencies from your game installation. It does not include them in the release. `install.ps1` installs a local build while the game is closed and backs up this mod's previous DLL.

## Credits and license

Original map, assets, and ball game: Another Axiom. Uses [BepInEx](https://github.com/BepInEx/BepInEx) and [Harmony](https://github.com/pardeike/Harmony).

Plugin source is under the [MIT license](LICENSE). Original game assets and third-party libraries remain under their respective licenses; they are not included in this repository or release.

This product is not affiliated with Another Axiom Inc. or its videogames Gorilla Tag and Orion Drift and is not endorsed or otherwise sponsored by Another Axiom. Portions of the materials contained herein are property of Another Axiom. Copyright 2021 Another Axiom Inc.
