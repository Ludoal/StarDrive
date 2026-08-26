![banner](https://repository-images.githubusercontent.com/576058391/90061a19-c54d-447e-95cd-e633f4ec8146)

[![Patch Build](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml/badge.svg?branch=main)](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml)

# About this fork (Ludoal)

Quality-of-life patches on top of the official **Jupiter 1.60.00047** release, made by a new player still learning the game, with an AI assistant doing the heavy lifting on code archaeology. **No gameplay changes**: UI fixes and comfort only. The point of publishing is simple: the BlackBox team is welcome to take whatever they find worth keeping, and patches 46 and 47 already integrated a number of them. Thanks!

- **Install**: grab the latest `BlackBox_Jupiter_Patch_*.exe` from [Releases](https://github.com/Ludoal/StarDrive/releases) and run it over an official Jupiter 1.60.00047 install, game closed. Cumulative, saves unaffected. **Legacy espionage saves are unusable** (they load, but espionage reads INF everywhere).
- **Revert**: the patch only touches files the official patch also ships, so re-applying the official 47 patch is a complete revert. No kit needed.
- **Versioning**: public releases are lettered (`47-a`, `47-b`, ...); the embedded build version stays numeric, so the in-game updater will still offer the next official patch (48+) normally.
- **Branch**: [`qol-47`](https://github.com/Ludoal/StarDrive/tree/qol-47), the official 47 tag plus these changes. Not based on upstream `main`/`develop`, so it stays save-compatible with the official 47.

## What's in it

### Design choices

**Gameplay**
- The one departure from the no-gameplay rule: the legacy espionage system is removed. Jupiter's infiltration is the only regime left, hence the save-compatibility note above.

**Look**
- The whole UI moved to one consistent flat "painted plate" look: a thin frame replacing the old sculpted textures on buttons, windows, popups and panels - drawn from one shared theme whose palette lives in a single theme file, so the look is re-skinnable without touching code.
- Resolution charter: table and list screens cap at a fixed width rather than stretch on ultra-wide displays, and at 1200px of display height and above, frames reserve the info-panel corner so the ship/planet cartouches stay visible beside an open screen.
- The universe shows, and can **run**, behind every panel: every table and readout stays live while the universe runs.

**UI**
- The four new screen groups (Galaxy / Empire / Diplomacy / Design) share one tab-row frame; every major table rebuilt on one shared component: sortable headers with remembered sort, auto-sizing columns, consistent colour rules.
- Sort, column and filter choices persist for the whole session across every table and the Shipyard browser.
- List rows activate on double-click in all panels, so no more accidental exits.

**Texts & units** *(work in progress)*
- Money flows labeled `BC/turn` everywhere (they are all per-turn), population rates per billion colonists; a game-term capitalization pass is ongoing.

### Top bar & navigation
- Live top bar on every panel, game speed − / + buttons matching the hotkeys.
- The **PAUSED** indicator tells an automatic pause (orange) from a manual one (red).
- The four screen-group buttons (EMPIRE, GALAXY, DIPLOMACY, DESIGN) live in the top bar itself, painted as plates at its centre; a group tab button closes its own group on a second press, from any tab inside it.
- The research readout on the bar shows the current tech in progress: its progress against cost right by the science icon.
- Every key that opens a screen closes it, from inside any screen - and **F8** opens the last viewed colony (the Homeworld at first).
- **Colony is a hosted tab now**: opened from a list, a page or the map, it takes a seat on that group's tab rail, and closing walks back to where you came from - right-click or Escape, either one.
- While a page is open, the **visible band of the map stays alive**: the minimap and its buttons work, the wheel zooms, middle-mouse pans, ships and planets select (info cartouches and their order buttons included) and box-select. On displays 1920px and wider, the map viewport recentres into the band beside the page instead of hiding under it.
- Double-click a planet on the map opens its Colony panel (owned or infiltrated); a single click on any table row selects on the map and pans to it, keeping the current zoom.
- Notification clicks go where they mean: diplomacy opens the relevant Diplomacy panel, espionage snaps to the planet, colony and ground-battle panels open in place. The camera only moves when it stays visible, and planet/station snaps stop at a sane zoom instead of nose-on.
- Chase camera fixed (it wasn't actually working): Ctrl+Middle-click follows the selected ship, Follow button in the ship info panel; panning or deselecting decouples.

### Map & overlays
- One overlay = one function, freely combined: **F2** influence zones, **F3** vision (sensor coverage, spies and projectors included), **F4** subspace projection, **F5** gravity wells, **F6** weapons range.
- Overlay choices are saved with the game.
- Fog of war is lighter at rest and returns to full darkening under the vision overlay (**F3**); your own ships are no longer painted dark outside a sensor bubble. An Options toggle (Fog Of War Memory) brings back the classic painted map.
- Nebulae slightly dimmed on the main map. Planet View removed: double-click opens Colony view, Combat view, or just snaps the camera. Exploded system view: stats only for the hovered planet, drawn on top.
- Deep-space builds: selectable while paused, real Cancel Construction button.
- Minimap reskinned; overlay and navigation buttons regrouped into two bands, redundant page/zoom buttons dropped; click-to-jump is exact; colonized systems boxed with race colour, contested systems in grey.
- **One icon per minable planet**: the resource icon alone (it also says which resource), going out once a mining station is deployed - the generic pickaxe doubled it.

### Empire group

**Colonies (the former Empire screen)**
- Renamed from Empire. Fertility / Richness / Max Population columns added.
- On wide displays (1680px+), two more columns: Pop Growth, and a sortable **Governor** column.
- A new EMPIRE totals tab in the bottom band: colony count, total population, total per-turn growth. The planet flavour text moved into the planet-icon tooltip.

**Ships**
- Military Ships filter (every combat-capable role in one view).
- A Proximity column (sorts closest-first) and a live Patrol column.

**Troops**
- **NEW** Troops Array: every ground troop you own in one screen: system, location, status (garrison, deployed, in transport, stationed), type, count and strength. Rows group by location and troop type; double-click jumps to the ship or opens the colony.
- A status filter dropdown (All / Deployed / Garrison / Transport / Stationed).

**Economy**
- Fully reworked: the whole budget on one screen, planet by planet. Double-click a row to open that colony.
- A **Governor Budget** panel: a Governor Spending slider throttles what governors may spend of their automatic allocations (manual colony budgets are exempt), and the Colony / Defense / Space Roads split is three linked sliders with padlocks - a locked share holds while the others renormalize to 100%. An Auto split toggle pins the stock 55/25/20.

**Research**
- Integrated frame under the top bar.
- Rows keep a fixed minimum gap instead of compressing to fit the frame; on small displays the tree overruns and panning reaches the rest.

**Automation**
- Automation moved off the map into its own Empire-group tab, five categories all visible at once; notification toggles in positive phrasing (checked = you get the alert); Auto Governor decoupled from Autocolonize.
- Auto Pick per design dropdown (Explorer, Colony Ship, Constructors, Freighter, Research/Mining Station): one checkbox each, manual pick or auto.
- A **Prioritization** column replaces the scattered Prioritize toggles: an ordered list of build categories (arrows to reorder); a prioritized item enters a colony's queue above anything of a lower or unranked category. Saved with the game.

### Galaxy group

**Planets**
- A separate Features column, a Proximity filter, an Owner filter, a split-out Pop % column.

**Exotic Systems**
- The number of deployed Mining Stations is displayed.

**Events**
- The Important Events log joins the Galaxy group as a tab (**F7**).

### Diplomacy group

**Diplomacy**
- Faction portraits are clickable on all tabs to jump straight into negotiation (the old separate Contact button is gone).
- Every empire in columns, showing what they know, what they want, and what you have signed.

**Intelligence**
- Trust / Anger / Threat bars are displayed; the treaty-matrix icons carry hover tooltips naming the treaty and the empire.

**Bonuses**
- You can hover a trait to read its description.

**Relationships**
- One filter per treaty type instead of two blanket toggles, and each treaty draws as its own parallel chord rather than all six stacking on the same line between two empires.

**Espionage**
- Infiltration laid out by level, so you can see what an operation costs before you commit to it.
- The "Limit Level" is a slider; your own column carries an INFILTRATION block - the planets your moles sit on, clickable, opening that colony in mole vision and coming back to Espionage on close.

### Design group

**Fleets**
- Wider magnetic grid steps.
- A box-selected fleet group gets a working stance bar; mouse zoom and pan belong to the frame, like the Shipyard.

**Shipyard**
- **NEW** Designs load straight from the Shipyard: the hull list and the load popup are one browser. Filter by name or hull, show only your designs or include locked ones, group **By Hull** (a carcass and what you built on it) or **By Role** (every carrier you own, wherever it is built). Double-click loads.
- **NEW** Designs can be marked obsolete, like modules: a button in the Active Design frame, the name in red in the browser, a **Hide obsolete** filter. Saved with your game, per empire.
- **NEW** Hovering a module or a design shows its full stats (and its module plan) without loading it, on a second panel beside the active one; a **Pin Active** toggle turns that into one panel at a time.
- **NEW** Comparator: Shift-click a module or a design to pin it, and the panel shows how it compares, row for row, with (+x)/(-x) deltas, green = better, pink = worse.
- Hovering a module fitted on the hull shows it like a list row; the orange highlight follows the cursor off the hull.
- Compact toggle: denser, heading-less stat columns matching the in-flight ship overlay, grouped into Combat / Defence / Mobility blocks that hide when empty; the floating overlay on other screens (Colony, Fleets) reworked to match.
- A **Full Screen** toggle by the hull name: the workbench spans the whole display (always pauses, dims the universe, and only then switches the music); the design in progress survives the flip.
- Mouse gestures follow the cursor: inside the frame they belong to the design (zoom, pan, module pick/remove), outside it a right-click closes the screen. Symmetric design starts OFF for new games.

**Battle Arena (work in progress)**
- Practice arena from the Shipyard: pick enemies per race grouped by hull class, full-tech sim empires, combat stances honored at contact. Group fights with an aggregated battle report.

**Blueprints**
- A Stats+ tab of its own (the default): the plan's budget broken down (colonist income + building income = gross, less upkeep = net) and a per-source yields grid, same layout as the Colony one.

### Other screens

**New Game / Race Design**
- Rebuilt: a fixed 1440×900 window, Environment enabled, Race and Opponents as two tabs sharing one column, and Rule Options moved into the Galaxy tab.
- Rule Options: a Reset button restores the stock defaults, and your house rules are captured on exit and seeded into the next new game.

**Colony**
- Panels aligned with the bar; Stats+ tab (the default) with the full production accounting.
- The full live universe map (and minimap) shows behind the colony panel; opening a Colony from a list keeps the list dimmed behind it, telegraphing where right-click sends you back.
- The **Budget** tab reworked: one Auto toggle, a monetary Governor Spending slider, and per-area % share sliders with padlocks beside the progress bars. On Auto everything mirrors the governor's live allocation (raw target in parentheses); re-ticking Auto snaps to it. Without a governor the tab shows the same bars.

**Options**
- Autosave measured in game years (slider, 0 = legacy wall-clock).
- The panel rebuilt as **five themed boxes** - Graphics (with its Apply button, device settings only), Audio, Visuals, Gameplay, Interface - everything visible at once, plus a **Reset to Defaults** button (display, language and sound device untouched).
- New knobs: Ship and Station icon sizes split in two, an Asteroid Size slider (visual, immediate), a Minimap Size slider (the utility overlays follow), and Bloom applies the moment it is ticked.
- Auto-pause on page opening (ON by default, with a Colony sub-option): untick it to let the universe run behind your pages.

**Misc**
- A Hotkeys reference popup in the in-game menu, every binding by category (remapping is planned).

### Bug fixes not yet upstream
- Fixes for **reported issues**, still waiting in open PRs on the official repo, ride in this patch in the meantime: carrier settings lost on save/reload, governor biosphere and no-scrap behaviour, space-road projector gaps, box-select swallowing troop transports, crash sites resolving before the conquest, DSB platforms too close to a sun, colonist import/export overlap, starving planets exporting food, fleet requisition and scrap-order refreshes, Dedicated Carrier hangar requirement, diplomatic demands and open-borders trust, and more; details in the release notes.
- Fixes for bugs **we ran into ourselves**: research-tree branch packing, per-entry scroll-wheel steps, camera chase cleared every frame, a UI draw failure starving the simulation thread, the diplomacy subtitle hardcoded to 1920, money units labelled per turn, a click threshold 50ms shorter than the input layer's, WIP designs stacking `_v1_WIP` onto their own name.
- Newer ones in this range: ship-design deletion silently a no-op after a save migration; the Shipyard's music restarting every frame until the mixer drowned; the main-menu planet leaving a bare band on displays wider than 16:9; shared tooltips parked at the first hover position (and covering the cursor near the display foot); open filter lists drawn under the table chrome; a Hide Owned toggle duplicating the Owner filter's Unowned option.

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
