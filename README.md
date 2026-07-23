![banner](https://repository-images.githubusercontent.com/576058391/90061a19-c54d-447e-95cd-e633f4ec8146)

[![Patch Build](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml/badge.svg?branch=main)](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml)

# About this fork (Ludoal)

Quality-of-life patches on top of the official **Jupiter 1.60.00046** release, made by a
new player still learning the game, with an AI assistant doing the heavy lifting on code
archaeology. **No gameplay changes**: UI fixes and comfort only. The point of publishing
is simple: the BlackBox team is welcome to take whatever they find worth keeping — patch
46 already integrated a number of them. Thanks!

- **Install**: grab the latest `BlackBox_Jupiter_Patch_*.exe` from
  [Releases](https://github.com/Ludoal/StarDrive/releases) and run it over an official
  Jupiter 1.60.00046 install, game closed. Cumulative, saves unaffected.
- **Revert**: the patch only touches files the official patch also ships, so re-applying
  the official 46 patch is a complete revert. No kit needed.
- **Versioning**: public releases are lettered (`46-a`, `46-b`, ...); the embedded build
  version stays numeric (`1.60.00046.N`), so the in-game updater will still offer the
  next official patch (47+) normally.
- **Branch**: [`qol-46`](https://github.com/Ludoal/StarDrive/tree/qol-46), the official
  46 tag plus these changes. Not based on upstream `main`/`develop`, so it stays
  save-compatible with the official 46.

## What's in it

**Overlays & map**
- One overlay = one function, freely combined: **F2** influence zones, **F3** vision
  (sensor coverage, spies and projectors included), **F4** subspace projection,
  **F5** gravity wells, **F6** weapons range.
- Optional influence colors on the main map and minimap (Options toggle, default = vanilla look).
- Fog of war stays dark: no permanent ship wakes, no explored-system halos — live
  sensors carry current vision.
- Dimmed nebulae on the main map.
- Minimap: colonized systems boxed with race color, contested systems (several races)
  in grey; the patch-46 Important Events button on the button row.

**Top bar & navigation**
- Live top bar on every full-screen panel (Empire, Espionage, Budget, Diplomacy,
  Shipyard, Fleets, the Array screens, Patrols, Blueprints, Research).
- **PAUSED** indicator on the bar: white = the screen's automatic pause, yellow = your
  pause (it survives closing the screen).
- The key that opens a screen closes it (C Troops, F Blueprints, P Patrols, **F7**
  Important Events log — new hotkey for the patch-46 screen).
- Troops Array: dedicated screen with status, grouping and strength columns.

**Screens**
- Research: integrated frame under the top bar, compact branch packing, standard title.
- Colony and Blueprints: panels aligned with the bar, wider right blocks.
- Planet View removed: double-click opens Colony view, Combat view, or just snaps the
  camera; the selection cartouches carry all the info.

**Notifications & camera**
- Notification clicks go where they mean: diplomacy opens the Diplomacy panel,
  espionage snaps to the planet, colony and ground-battle panels open in place —
  the camera only moves when it stays visible, and planet/station snaps stop at
  a sane zoom instead of nose-on.
- Autosave measured in game years (slider, 0 = legacy wall-clock).

**Texts & units** *(work in progress)*
- Money flows labeled `BC/turn` everywhere (they are all per-turn), population rates
  per billion colonists; a game-term capitalization pass is ongoing.

**Battle Arena** *(work in progress)*
- Practice arena from the main menu: pick enemies per race grouped by hull class,
  full-tech sim empires (carrier hangars work), combat stances honored at contact.
  Planned: spawn distance control, encounter-filtered enemy list.

**Bug fixes not yet upstream**
- The fixes still waiting in open PRs on the official repo (governor blueprint and
  scrap behaviour, space-road projector count and priority, troop-transport selection,
  wreck-site conquest gate, per-entry scroll-wheel steps, and more) ride in this patch
  in the meantime; details in the release notes.

*Everything below is the original BlackBox README.*

---

# Stardrive BlackBox
This is the 15b version of StarDrive.exe originally decompiled from CIL and almost completely rewritten by the BlackBox team.
The current release is **BlackBox - Jupiter (1.60)** and the upcoming version is **BlackBox - Saturn**.

Notice: We have StarDrive developer's [publicly and privately stated approval](http://steamcommunity.com/app/252450/discussions/0/385428458177062745/#c365163686048069513) for modifying the game for educational purposes but this software is still under the steam license restrictions.
Do not use this for immoral or personal financial gain, donation requests are ok but can not be demanded or required.
Do not attempt to circumvent game DRM. Be reasonably respectful of the dev and the original software and steam.

# System Requirements

* **OS**: Windows 10 version 1803 (April 2018 Update, build 17134) or later — including Windows 11. Older Windows 10 builds are missing per-thread DPI APIs that MonoGame 3.8 requires and the game will fail to start.
* **Architecture**: 64-bit (x64)
* **.NET runtime**: bundled with the installer (.NET 8)

# Downloads

The canonical distribution channel is **itch.io**:

* **[Jupiter 1.60 on itch.io](https://stardriveteam.itch.io/jupiter-160)** — major installer (~690 MB, bundles the .NET 8 runtime and ships the Combined Arms mod alongside the game)

Major versions are big, 600-700 MB installs. Patches are relatively small and are always cumulative — install the major version, then the game's in-app updater picks up the latest patch on first launch. Hover the prompt to see the changelog.

[Per-patch artefacts](https://github.com/TeamStarDrive/StarDrive/releases) are also published as GitHub Releases for reference; the canonical first-time install path remains itch.io.

# Mods
The mods currently supported on BlackBox are:
* [Combined Arms](https://github.com/TeamStarDrive/CombinedArms) — a huge content mod. Jupiter 1.60 compatible.
* [Star Trek: Shattered Alliance](https://github.com/TeamStarDrive/StarTrekShatteredAlliance) — vanilla races plus Star Trek races and ships.

# Community
Feel free to drop in for questions, bug reports, requests and what not.

* [Discord Discussion](https://discord.gg/dfvnfH4)
* [Patreon](https://www.patreon.com/stardriveblackbox)
* [GitHub Issues](https://github.com/TeamStarDrive/StarDrive/issues) for reporting all types of bugs
* [For information on older versions, visit the ModDB page](http://www.moddb.com/mods/deveks-mod)

# BlackBox - Jupiter (current)
What Jupiter 1.60 delivers, building on the Mars line:
* **64-bit engine** — the 32-bit ~2 GB process ceiling is gone; late-game crashes with large empires or Combined Arms are fixed
* **MonoGame 3.8 renderer** replaces the discontinued XNA + SunBurn stack; no more XNA 3.1 redistributable requirement
* **.NET 8 runtime** bundled with the installer (was .NET Framework 4.8)
* Restored visual effects, skinned/animated meshes, material maps, post-process passes, basic shadow maps
* Save format partitioned (`SaveGameVersion = 21`) so Jupiter coexists side-by-side with a Mars 1.51 install

# BlackBox - Mars (legacy)
What the Mars line delivered (1.50 / 1.51), now preserved on the [`mars-1.51`](https://github.com/TeamStarDrive/StarDrive/tree/mars-1.51) branch for back-port hotfixes:
* Huge performance improvements
* Huge stability improvements - especially got rid of most OutOfMemory errors
* Racial planet preferences
* Research Stations
* Mining Ops
* Multi Level Research for bonuses/upgrades
* New mesh, texture and shader loading system
* Auto Update for BlackBox versions and mods

# How do I get set up for Development?

* Install [Visual Studio 2022 Community](https://visualstudio.microsoft.com/vs/community/).
    * Workloads Module: `.NET desktop development` with **.NET 8 SDK**
    * Workloads Module: `Desktop development with C++` with `MSVC v143`
    * Workloads Module: `Game development with C++` with `Windows 10 SDK`
* Install [SourceTree](https://www.sourcetreeapp.com/) or some other GIT client.
    * Configure SourceTree: Tools->Options->Git: [v] Perform submodule actions recursively _(Important!!!)_
* [Clone](https://confluence.atlassian.com/sourcetreekb/clone-a-repository-into-sourcetree-780870050.html) this repository to a local directory, for example: C:/Projects/BlackBox
    * Advanced Options When cloning: [v] Recurse submodules _(Important!!!)_
* The active development branch is `main` (post-migration Jupiter line). The Mars-line legacy branch is `mars-1.51`.
* Launch Visual Studio, any required DLL references should be in `BlackBox/game` directory.
* Launch a full build (Build -> Build Solution) in `Release|x64` configuration to produce the BlackBox StarDrive executable.
    * If you get this build error: "Windows 10 SDK is not installed", then you need to go back to Visual Studio installer and enable Desktop development with C++
    * If you get this build error: ".. Cannot open include file: 'corecrt.h': No such file or directory ..", then you are also missing Desktop development with C++

* Install [JetBrains ReSharper](https://www.jetbrains.com/resharper/download/) to enjoy enhanced refactoring capabilities.
* Please NOTE: if the **default** Release and Debug configurations *do not work* for you then your setup is incorrect. Contact us in Discord #general-discussion.

### Contribution Guidelines

* Utilize Discord for chat discussions on ideas and refactoring.
* Use [GitHub Issues](https://github.com/TeamStarDrive/StarDrive/issues) to propose new ideas.
* Creating feature branches is always allowed and Pull Requests will be reviewed by the team.
* Comment your code so people can see what you are changing. Non-documented code will not pass review.
* Write clean code, following current best software practices. #DRY #CleanCode

### Who do I talk to?

* In Discord: @RedFox and @Fat_Bastard can provide guidance of this codebase.
* If you have a bug report, post an issue or post a bug in our Discord channel.
* For other feature ideas, you can join our Discord chat and talk with the team!

# Modding
BlackBox has greatly improved modding capabilities over the original game,
contact us in [Discord](https://discord.gg/dfvnfH4) for more information on modding.

## What is moddable?
* Globals.yaml provides access to all global game settings, more detailed than previously available.
* All textures can be replaced, PNG and DDS are supported. The old XNB textures are no longer recommended.
* All meshes can be replaced, we support OBJ and FBX meshes.
* Audio can be modded
* Custom stars
* Custom planets
* Some UI layouts can be modded, mostly MainMenu for now
* All YAML files can be modded and are hotloaded while the game is running, so you can do interactive tweaking
* Feel free to ask for more details in [Discord](https://discord.gg/dfvnfH4)

# Development Cycle
## For new features, refactors, old bug fixes  (feature)
* Create a new feature branch from `main`.
* Always add NEW feature unit tests and playtest your changes.
* Create a pull request and wait for review. Be ready to make a few tweaks! It is easy to create unintentional bugs in this legacy codebase.
## If bugs are found in main branch (hotfix)
* Create an issue or mark existing issue as a "Blocker" for current release.
* Post the issue in the dev channel of discord.
* If you can quickly fix it, help us by creating a hotfix pull request.

# Command Line Arguments
BlackBox provides a CLI for running certain utilities from Command Prompt.
Many of these are developer oriented and not very useful for regular users.
```
C:\Projects\BlackBox\game>StarDrive.exe --help
13:50:43.698ms: Loaded App Settings
13:50:43.768ms:
 ======================================================
 ==== Jupiter : 1.60.00009 jupiter-1.60            ====
 ==== UTC: 05/11/2026 13:50:43                     ====
 ======================================================

13:50:43.769ms: StarDrive BlackBox Command Line Interface (CLI)
13:50:43.769ms:   --help             Shows this help message
13:50:43.769ms:   --mod="<mod>"    Load the game with the specified <mod>, eg: --mod="Combined Arms"
13:50:43.769ms:   --export-textures  Exports all texture files as PNG and DDS to game/ExportedTextures
13:50:43.769ms:   --export-meshes=obj Exports all mesh files and textures, options: fbx obj fbx+obj
13:50:43.769ms:   --generate-hulls   Generates new .hull files from old XML hulls
13:50:43.769ms:   --generate-ships   Generates new ship .design files from old XML ships
13:50:43.769ms:   --fix-roles        Fixes Role and Category for all .design ships
13:50:43.769ms:   --run-localizer=[0-2] Run localization tool to merge missing translations and generate id-s
13:50:43.769ms:                         0: disabled  1: generate with YAML NameIds  2: generate with C# NameIds
13:50:43.769ms:   --resource-debug   Debug logs all resource loading, mainly for Mods to ensure their assets are loaded
13:50:43.769ms:   --asset-debug      Debug logs all asset load events, useful for analyzing the order of assets being loaded
13:50:43.769ms:   --console          Enable the Debug Console which mirrors blackbox.log
13:50:43.769ms:   --continue         After running CLI tasks, continue to game as normal
13:50:43.769ms: The game exited normally.
13:50:43.769ms: RunCleanupAndExit(0)
```

To convert all legacy XNB textures, you can run `--export-textures`
```
C:\Projects\BlackBox\game>StarDrive.exe --export-textures
```
