![banner](https://repository-images.githubusercontent.com/576058391/90061a19-c54d-447e-95cd-e633f4ec8146)

[![Patch Build](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml/badge.svg?branch=main)](https://github.com/TeamStarDrive/StarDrive/actions/workflows/patch-build.yml)

# About this fork (Ludoal)

Quality-of-life patches on top of the official **Jupiter 1.60.00048** release, made by a new player still learning the game, with an AI assistant doing the heavy lifting on code archaeology. The point of publishing is simple: the BlackBox team is welcome to take whatever they find worth keeping, and patches 46 to 48 already integrated a number of them. Thanks!

Visually, the whole interface has been rebuilt: a flat, themable "painted plate" look on every screen (palette in one theme file), the UI organized into four screen groups (Galaxy / Empire / Diplomacy / Design) on a single live top bar, every major table rebuilt on one shared component (sortable headers, remembered sort and filters, double-click to open), a layout charter from 1440x900 up (tables cap at a readable width on ultra-wide displays, info cartouches stay visible on tall ones), and the universe showing, and able to run, behind every panel.

- **Branch & versioning**: [`qol-48`](https://github.com/Ludoal/StarDrive/tree/qol-48), the official 48 tag plus these changes; not based on upstream `main`, so it stays save-compatible with the official 48. Public releases are lettered (`48-a`, `48-b`, ...); the embedded build version stays numeric, so the in-game updater still offers the next official patch normally.
- **Install**: grab the latest `BlackBox_Jupiter_Patch_*.exe` from [Releases](https://github.com/Ludoal/StarDrive/releases) and run it over an official Jupiter 1.60.00048 install, game closed. Cumulative; re-applying the official 48 patch is a complete revert.
- **Saves**: unaffected. **Legacy espionage saves are unusable** (they load, but espionage reads INF everywhere).

## What's in it

### Design choices

**Gameplay**
- The fork aims at UI and comfort, not at rebalancing: the game is meant to play like the official 48. The deliberate exceptions, each with its rationale, are tracked in [`gameplay-changes.md`](gameplay-changes.md).
- Where the fork touches a mechanic, the intent is always the same: clarify what a switch actually does, decouple settings that were tied together, and give the player finer control. Defaults reproduce stock conduct.
- The largest one: the legacy espionage system is removed. Jupiter's infiltration is the only regime left, hence the save note above.

**Languages** *(work in progress)*
- The language system is being overhauled, but a good deal of text is still hardcoded.
- A French translation has been created and grows screen by screen. Other translations will be updated.

**Texts & units** *(work in progress)*
- Money flows labeled `BC/turn` everywhere (they are all per-turn), population rates per billion colonists; a game-term capitalization pass is ongoing.

### Top bar & navigation
- Live top bar on every panel, game speed − / + buttons matching the hotkeys.
- The *PAUSED* indicator tells an automatic pause (orange) from a manual one (red).
- The research readout on the bar shows the current tech in progress: its progress against cost right by the science icon.
- The four screen-group buttons (EMPIRE, GALAXY, DIPLOMACY, DESIGN) live in the top bar itself, painted as plates at its centre; a group tab button closes its own group on a second press, from any tab inside it.
- Every key that opens a screen closes it, from inside any screen - and **F8** opens the last viewed colony (the Homeworld at first).
- Colony is a hosted tab now: opened from a list, a page or the map, it takes a seat on that group's tab rail, and closing walks back to where you came from - right-click or Escape, either one.
- While a page is open, the visible band of the map stays alive: the minimap and its buttons work, the wheel zooms, middle-mouse pans, ships and planets select (info cartouches and their order buttons included) and box-select. On displays 1920px and wider, the map viewport recentres into the band beside the page instead of hiding under it.
- Double-click a planet on the map opens its Colony panel (owned or infiltrated); a single click on any table row selects on the map and pans to it, keeping the current zoom.
- Notification clicks go where they mean: diplomacy opens the relevant Diplomacy panel, espionage snaps to the planet, colony and ground-battle panels open in place. The camera only moves when it stays visible, and planet/station snaps stop at a sane zoom instead of nose-on.

### Map & overlays
- Nebulae slightly dimmed on the main map.
- One overlay = one function, freely combined: **F2** influence zones, **F3** vision (sensor coverage, spies and projectors included), **F4** subspace projection, **F5** gravity wells, **F6** weapons range.
- **NEW** Route overlays: freighter traffic and colonization runs drawn on the map, one line per pair of colonies and per cargo, each with its own colour and icon. Food, Production and Population routes each own one dash slot in three, so overlapping routes interleave instead of blending into one colour.
- Overlay choices are saved with the game.
- Gravity wells (**F5**) read as a solid, desaturated disc instead of a bright wash.
- Fog of war is lighter at rest and returns to full darkening under the vision overlay (**F3**); your own ships are no longer painted dark outside a sensor bubble. An Options toggle (*Fog Of War Memory*) brings back the classic painted map. It now reaches past the world bounds, so no border square shows at the map edge.
- Planet View removed: double-click opens Colony view, Combat view, or just snaps the camera. Exploded system view: stats only for the hovered planet, drawn on top.
- Deep-space builds: selectable while paused, real *Cancel Construction* button. They can now be dragged to a new spot while under construction.
- Minimap reskinned; overlay and navigation buttons regrouped into two bands, redundant page/zoom buttons dropped; colonized systems boxed with race colour, contested systems in grey. The minimap viewport frame no longer trembles as the camera moves.
- One icon per minable planet: the resource icon alone (it also says which resource), going out once a mining station is deployed - the generic pickaxe doubled it.

### Empire group

**Colonies (the former Empire screen)**
- Renamed from Empire. Fertility / Richness / Max Population columns added.
- On wide displays (1680px+), two more columns: Pop Growth, and a sortable Governor column.
- Each labor value shows beside it the maximum that colony would reach at 100% labor.
- Space Port and Military Outpost icons sit right of the colony name on wide displays.
- The supply columns are three-position lists: in Auto the live pick shows greyed, and clicks only bite in manual.
- **NEW** EMPIRE totals tab in the bottom band: colony count, total population, total per-turn growth, global food and production stock. The planet flavour text moved into the planet-icon tooltip.

**Ships**
- Military Ships filter (every combat-capable role in one view).
- A Proximity column (sorts closest-first) and a live Patrol column.

**Troops**
- **NEW** Troops Array: every ground troop you own in one screen: system, location, status (garrison, deployed, in transport, stationed), type, count and strength. Rows group by location and troop type; double-click jumps to the ship or opens the colony.
- A status filter dropdown (All / Deployed / Garrison / Transport / Stationed).

**Economy**
- Fully reworked: the whole budget on one screen, planet by planet. Double-click a row to open that colony.
- A Governor Budget panel: a *Governor Spending* slider throttles what governors may spend of their automatic allocations (manual colony budgets are exempt), and the Colony / Defense / Space Roads split is three linked sliders with padlocks - a locked share holds while the others renormalize to 100%. An *Auto split* toggle pins the stock 55/25/20.

**Research**
- Integrated frame under the top bar.
- Rows keep a fixed minimum gap instead of compressing to fit the frame; on small displays the tree overruns and panning reaches the rest. The tree can be panned with the mouse-wheel.

**Automation**
- Automation moved off the map into its own Empire-group tab, five categories all visible at once; notification toggles in positive phrasing (checked = you get the alert); Auto Governor decoupled from Autocolonize.
- Auto Pick per design dropdown (Explorer, Colony Ship, Constructors, Freighter, Research/Mining Station): one checkbox each, manual pick or auto.
- A Prioritization column replaces the scattered Prioritize toggles: an ordered list of build categories (arrows to reorder); a prioritized item enters a colony's queue above anything of a lower or unranked category. Saved with the game.
- The Trade box is dissected: one shared *Freighter Model* picker, then three plain toggles - auto-build, auto-upgrade, auto-scrap idle - each doing one thing.
- Auto-upgrade of freighters no longer depends on Automatic Trade being on.
- *Freighter Priority*: which cargo is served first when cargo is scarce (Auto, Production first, Colonists first). It only bites under a shortage.
- Auto-explore is split in two as well: build new scouts, and send idle scouts out to explore.
- Notifications by category: every notification belongs to one of nine families, each with its own *show* switch.
- *Show oldest Notification text*: the notification at the head of the queue displays its text in the clear; the others keep their hover tooltip.
- *Auto-clear oldest*: the head of the queue ages out after an adjustable delay, and the next one takes its place; the pile drains in order.
- Story and event popups always pause the game, whatever the auto-pause setting says, and auto-clear never ages a paused game.

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
- **NEW** Trends tab: one curve per domain, with ranks unlocking per domain. No retroactive history is displayed - a curve starts the day its domain opens.

**Bonuses**
- You can hover a trait to read its description.

**Relationships**
- One filter per treaty type instead of two blanket toggles, and each treaty draws as its own parallel chord rather than all six stacking on the same line between two empires.

**Espionage**
- Infiltration laid out by level, so you can see what an operation costs before you commit to it.
- The *Limit Level* is a slider; your own column carries an INFILTRATION block - the planets your moles sit on, clickable, opening that colony in mole vision and coming back to Espionage on close.
- Uprise and Rebellion notifications point at the planet they targeted, not at the Espionage panel.

### Design group

**Fleets**
- Fleet slots show even when empty: the first ten always visible, a 1-10 / 11-20 switch reaches the rest, and every fleet key works at any resolution.
- Wider magnetic grid steps.
- A box-selected fleet group gets a working stance bar.

**Shipyard**
- **NEW** Designs load straight from the Shipyard: the hull list and the load popup are one browser. Filter by name or hull, show only your designs or include locked ones, group *By Hull* (a carcass and what you built on it) or *By Role* (every carrier you own, wherever it is built). Double-click loads.
- **NEW** Designs can be marked obsolete, like modules: a button in the Active Design frame, the name in red in the browser, a *Hide obsolete* filter. Saved with your game, per empire.
- **NEW** Hovering a module or a design shows its full stats (and its module plan) without loading it, on a second panel beside the active one; a *Pin Active* toggle turns that into one panel at a time.
- **NEW** Comparator: Shift-click a module or a design to pin it, and the panel shows how it compares, row for row, with (+x)/(-x) deltas, green = better, pink = worse.
- *Compact* toggle: denser, heading-less stat columns matching the in-flight ship overlay, grouped into Combat / Defense / Mobility blocks that hide when empty; the floating overlay on other screens (Colony, Fleets) reworked to match.
- A *Full Screen* toggle: the workbench spans the whole display (always pauses, dims the universe, and only then switches the music); the design in progress survives the flip.

**Battle Arena (work in progress)**
- Practice arena from the Shipyard: pick enemies per race grouped by hull class, full-tech sim empires, combat stances honored at contact. Group fights with an aggregated battle report.

**Blueprints**
- A Stats+ tab of its own (the default): the plan's budget broken down (colonist income + building income = gross, less upkeep = net) and a per-source yields grid, same layout as the Colony one.

### Other screens

**New Game / Race Design**
- Rebuilt: a fixed 1440×900 window, Environment enabled, Race and Opponents as two tabs sharing one column, and Rule Options moved into the Galaxy tab.
- Rule Options: a *Reset* button restores the stock defaults, and your house rules are captured on exit and seeded into the next new game.

**Colony**
- **NEW** Stats+ tab (the default) with the full production accounting.
- Build Mandate: what the governor may raise on this colony (All / Economic only / Defense only / None).
- Scrap Mandate: what it may raze, as its own separate list. Blocking a family by zeroing its budget destroyed your figures; a mandate suspends the right and leaves them stored.
- Both mandates replace *Gov. Manages Ground Defense* and *Governor Will Not Scrap Buildings*, and their defaults reproduce stock conduct.
- **NEW** LIST sub-tab beside MAP: every constructed building on the colony, classified by category rather than hidden, with the net yield the simulation itself uses. In that list, hovering previews a building, clicking pins it, and the wheel reads its long description.
- Supply (former Storage) panel: population bar, colonist freighter line, one word for stock and one for flow. Colonist migration is under your control.
- Each colony carries its own *Continuous Rush* toggle, beside the empire-wide one.
- The five governor portraits describe what each type actually does, with real numbers.
- The Specialized Trade Hub only carries the trade regime now: it no longer halts construction, and its tooltip states its thresholds.

**Options**
- Autosave measured in game years (slider, 0 = legacy wall-clock).
- The panel rebuilt as five themed boxes - Graphics (with its Apply button, device settings only), Audio, Visuals, Gameplay, Interface - everything visible at once, plus a *Reset to Defaults* button (display, language and sound device untouched).
- New knobs: Ship and Station icon sizes split in two, an *Asteroid Size* slider (visual, immediate), a *Minimap Size* slider (the utility overlays follow), and Bloom applies the moment it is ticked.
- Auto-pause on page opening (ON by default, with a Colony sub-option): untick it to let the universe run behind your pages.

**Misc**
- **NEW** Hotkeys are rebindable: the reference screen became the editor, reachable from the main menu too, with conflicts flagged as you bind.

### Performance
- Hull meshes, weapon animations and space-station meshes preload with the rest of the world models instead of stuttering in on first sight.
- Racial diplomacy videos are cached rather than re-read from disk every time the screen opens.

### Bug fixes not yet upstream
- Fixes for reported issues, still waiting in open PRs on the official repo, ride in this patch in the meantime: carrier settings lost on save/reload, governor biosphere and no-scrap behaviour, space-road projector gaps, box-select swallowing troop transports, crash sites resolving before the conquest, DSB platforms too close to a sun, starving planets exporting food, fleet requisition and scrap-order refreshes, Dedicated Carrier hangar requirement, diplomatic demands and open-borders trust, and more; details in the release notes.
- The governor cancelled its own over-budget building plans less and less the tighter the budget got: the check sat on the branch that only runs when the colony is within budget.
- Random events, and buildings raised by a volcano, no longer announce themselves on planets you have never explored.

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
