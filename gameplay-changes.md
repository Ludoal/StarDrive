# Gameplay changes — Ludoal fork

Every change this fork makes to **what the game does**, as opposed to how it looks.
UI work, tooltips, translations, layout, crash fixes and serialization fixes are deliberately
out of scope: this file exists so that a player (or an upstream reviewer) can tell at a glance
whether their game plays differently, and why.

**The test:** a gameplay change is what a player who touches nothing feels as different from
vanilla. A repaired bug makes behaviour what it was *meant* to be — that is a correction. A
lever left at its shipped default changes nothing until someone moves it; that is an option.

Three sections, ordered by how much they actually move the game:

- **Balance changes** — the simulation behaves differently, with no setting touched. This is
  the short list, and the one an upstream reviewer or a returning player should read first.
- **New levers** — new controls, neutral at their default. They widen what the player may do,
  they do not change what the game does on its own.
- **Formula corrections** — a formula was demonstrably wrong and now is not, or the game is
  *read* differently while playing the same.

Each entry names its **source** — where the change came from, not a category:

- **#NNN** — an issue on the TeamStarDrive tracker. The fork is fixing a reported bug.
- **maintainer** — this fork's own bench testing and design.

Community feedback that leads to a change gets named the same way, by its author.

Baseline: fork diverged from upstream at `70055a69`. Each entry cites its commit so the diff
can be read rather than trusted.

---

## Balance changes

### Auto-upgrade fires on any idle freighter for the player
`bbb60d6d` — `Empire_Trade.cs` (`TriggerFreightersRefit`, `CheckForRefitFreighter`)

**Source: maintainer.**

Vanilla gated freighter refits behind fleet-fill thresholds: no refit at all until the freighter
fleet was over 75% of its cap, and each individual refit needed the fleet over 50%. **Both gates
are removed for the player** — a refit now fires whenever a better model exists, spread out only
by the existing 20% dice roll per idle freighter. AI empires keep both caps, since they manage
their own war economy.

### Governor no longer demolishes civilian buildings on its own
`Planet_BuildDefenses.cs`, `Planet_EvaluateBuildings.cs` (Build and Scrap Mandates)

**Source: maintainer.**

What the governor may build, and what it may demolish, are now two commands of their own
rather than a side effect of the budget. The scrap mandate ships as **None**: left alone, a
governor builds but never tears anything down.

Vanilla demolished civilian buildings when a colony ran over budget, so a returning player
will see over-budget colonies stay as they are until they intervene. Military buildings are
unaffected: vanilla never demolished those either unless Gov. Manages Ground Defense was
ticked, which was off by default.

Old saves keep their conduct: the former toggles map onto the mandates on load.

### A blueprint no longer overrides the build and scrap mandates
`Planet_BuildDefenses.cs`, `Planet_EvaluateBuildings.cs`

**Source: maintainer.**

A colony following a blueprint used to build and demolish regardless of its mandates: the
plan granted the right on its own. The mandate is the right now, and the plan only directs
what gets built inside it.

A player who wants a colony to follow its plan but tear nothing down sets its Scrap Mandate
to None and it holds - before, that setting did nothing on a colony that had a plan.

Returning players will feel it where a colony has both a plan and a restrictive mandate: it
now obeys the mandate. The shipped defaults are unchanged, so a colony whose mandates were
never touched behaves as it did.

---

### Colonies stop ordering food into a nearly full store, and Specialized Trade-Hub is gone
`Planet_Trade.cs`, `Planet_Govern.cs`, `Planet_ConstructionQueue.cs`

**Source: maintainer.**

A colony without a governor already refused food deliveries once its store passed 90%. A
governed one did not, unless the Specialized Trade-Hub box was ticked. That rule is now
every colony's, and the box is gone.

The box had nothing else left to do. Its advertised job - trading on hub thresholds instead
of its governor's - stopped happening when supply states were decoupled from governor type,
and its two other effects had already been handed back to the controls they belonged to.

A governed colony near its storage cap now imports about ten percent less margin than it
did, and the freighters it frees go elsewhere. Stores drain as they are eaten, so slots
reopen on their own, and a starving colony still outranks every other in the dispatch order.

Loading a blueprint no longer resets a colony's build and scrap mandates to All. That line
existed because a blueprint used to buy past the mandates; it no longer does, and the reset
would have undone the player's own restriction the moment they loaded a plan.

---

---

## New levers

### An empire-wide build order
`2c4c00b4` — `AddToQueueScreen.cs`, `PoliciesScreen.cs`

**Source: maintainer.**

Policies gains "Add to Construction Queue": a list built once — buildings, troops, ships — and
posted to every colony you aim it at. The target is all colonies, those without a governor, or
one governor type. A building already standing or already queued on a colony is skipped there; a
colony with no free tile is named in the report. Troops and ships go only where they can be built
at all, and a line added twice is queued twice. Everything it posts is marked as yours, so the
governor treats it the way it treats anything you queued by hand.

### Labor gets an Auto toggle
`36a3bba6` — `Planet_Govern.cs`, `Planet_WorkDistribution.cs`, `AssignLaborComponent.cs`

**Source: maintainer.**

Whether a colony's labour split was yours to set was deduced from its governor type and said
nowhere: a governed colony's sliders simply did not answer. That rule is now a toggle above the
three locks. Auto means the split is managed for you — by the governor if there is one, by the
sustenance pilot if there is not. The pilot holds only what keeps the colony alive and rescales
what you own to fill the rest, keeping the ratio you set between production and research. What
counts as sustenance follows the species: food for most, production for the cybernetic, whose
Food row — inert for them until now — becomes the gauge of the production held back, with the
row below showing what is left for you. Off,
the sliders and their locks are yours even under a governor, which keeps its build queues either
way. The setting is stored as three states, the third being "never set", so a colony from an
older save follows exactly the rule it followed before, and a choice you make outlives a change
of governor.

### A standing refit order can be cancelled from the ship list
`4b8cccc2` — `ShipListScreenItem.cs` (`OnRefitClicked`)

**Source: maintainer.**

Ordering a refit was one-way: the button reopened the chooser and there was no way back short of
scrapping the ship. It now toggles like the scrap icon beside it — a second click drops the refit
goal and clears the ship's orders — and the icon stays lit in gold while the order stands, so the
list says which ships are on their way to a yard.

### A default colony plan per governor type
`c5f1f9a5` — `Empire.cs`, `PoliciesScreen.cs`, `GovernorDetailsComponent.cs`

**Source: maintainer.**

Policies > Colony gains one row per governor type naming the blueprint a colony of that type
follows when its Blueprint picker is set to Auto; Custom and None are never touched from here.
The table ships empty and an unset row means no plan, so nothing changes until the player sets
a row. Auto is new on the colony's own picker, and loading or editing a plan by hand leaves it —
the explicit gesture outranks the doctrine. A colony the player founds is born on Auto; a
conquered world is not, since a plan let loose on it would scrap the spoils the turn they were
won. Assigning an Exclusive plan to a row is confirmed first. Older saves read the flag as false
everywhere: no existing colony changes conduct.

*Neutral at default: leave these alone and the game plays as vanilla.*

### Colony budget: pooled shares + Governor Spending tap
`8735cf95` — `AI/EmpireAI/Budget.cs`, `AI/EmpireAI/EmpireAI.RunEconomicPlanner.cs`

**Source: maintainer.**

Defense, SSP and Colony budgets used to be computed independently. For the **player** they are
now pooled and split by adjustable shares (`DefenseBudgetShare` / `SSPBudgetShare` /
`ColonyBudgetShare`), and a `GovernorSpendingRatio` tap limits what governors may actually
spend of their automatic allocations, the treasury keeping the rest. Manual per-colony budgets
bypass the tap: an explicit order is not throttled. AI empires are unaffected.

### Supply flows decoupled from governor type
`414a25ad` — `Planet_Govern.cs` (`ManageSupplyStates`), `Planet_Resources.cs`

**Source: maintainer.**

Import/export thresholds used to be applied only by an assigned governor, and a colony with no
governor had no automatic flow management at all — the `TradeHub` role was the de-facto
workaround. TradeHub is retired; `ManageSupplyStates` now runs for every colony, with a
per-resource Manual/Auto flag so a player's explicit choice is no longer overwritten each tick.
Per-type numeric thresholds are unchanged.

### Continuous Rush per colony
`d2a8d200` — `Universe/SolarBodies/SBProduction.cs`

**Source: maintainer.**

Rush spending was all-or-nothing at empire scale. A per-planet `RushConstruction` flag now
triggers it alongside the existing global one.

### Ordered construction priorities
`6ab38759` — `Universe/SolarBodies/SBProduction.cs`, `Universe/UniverseParams.cs`

**Source: maintainer.**

Vanilla had a single `PrioitizeProjectors` toggle. It is replaced by `ConstructionPriorities`:
an ordered list of categories whose sequence *is* the hierarchy, so the player ranks what jumps
the build queue.

### Player-settable freighter priority under shortage
`ab9ec04d` (renamed in `ae7a0149`) — `Empire_Trade.cs`

**Source: maintainer.**

Under a freighter shortage, whether production or colonists got served first was a
population-weighted dice roll (`Random.RollDice(productionFirstChance)`). The player can now
fix that order (`Auto` / `Production First` / `Colonists First`). AI empires keep the vanilla
dice behaviour.

### Auto Governor on new colonies
`3c1457d3` — `Planet_Colonize.cs` (`SetupColonyType`)

**Source: maintainer.**

Manually colonized planets defaulted to `ColonyType.Colony`, i.e. ungoverned. With Auto
Governor on, a new colony instead gets a governor fit to its needs via `AssessColonyNeeds()`.
Off, the player governs by hand; AI empires always self-govern.
*(Stored as `AutoCoreGovernor` for save compatibility.)*

### Governor no longer cancels or replaces player-placed buildings
`cb58a580`, `b60d0ba9` — `Planet_EvaluateBuildings.cs`

**Source: #303**, plus a companion hole found while fixing it.

Two separate holes, both closed. `TryCancelOverBudgetCivilianBuilding` cancelled any
over-budget civilian building without checking `IsPlayerAdded`. And `ReplaceBuilding` never
consulted `DontScrapBuildings`, with its player-built guard sitting after an early `return`
where it could never run. A building you queued yourself is now safe from both paths.

### Biospheres are capacity, not investment
`dd79dd44` — `Planet_EvaluateBuildings.cs` (`TryBuildBiospheres`)

**Source: #313, #321.**

Biosphere build/scrap decisions were judged on *profitability*, keyed on the empire's live tax
rate — raising taxes made governors build them, lowering taxes made them tear them down. They
are now a pure **capacity** decision, independent of any tax rate: built under population
pressure, scrapped only under genuine excess capacity (population would sit below 60% of the
cap without one).

A colony budget that covers the upkeep still reads as an explicit *"I am paying, keep them"*
(#313), but only protects the **last** biosphere — any surplus beyond it is scrapped regardless
(`Planet_EvaluateBuildings.cs:849-859`).

### Colonization goal dropped on explicit player redirect
`0fc67a01` — `ShipMoveCommands.cs` (`CancelAbandonedColonizationGoal`)

**Source: #324.**

A direct player order that pulls a colony ship off its manual `MarkForColonization` target now
also drops the empire goal, so automation stops sending replacement colony ships there. The
trigger is the explicit order, never a missing state.

### Deep space build sites can be dragged
`7260db6a` — `DeepSpaceBuildGoal.cs` (`MoveBuildPos`)

**Source: maintainer.**

New player gesture: an in-progress deep space construction site can be moved. The position is
rebased, the pathfinding detour chain invalidated, and a constructor already in flight re-routed.

### Manually queued ships carry their real role
`5718bb10` — `ColonyScreen_Build.cs`

**Source: maintainer.**

Manually queued ships were typed by a freighter/combat binary, so scouts and colony ships
entered the queue as `CombatShip`. The build priority list then ranked auto-built colony ships
above an Explorer the player had queued by hand. The type now derives from the design's actual
role. *(Lives in a GameScreens file, but the change is to real queue ordering, not display.)*

---

## Formula corrections

### Cybernetic colonies stop losing workers into a sterile row
`dc26300d` — `ColonySlider.cs` (`Pinned`)

**Source: maintainer.**

The food row of a cybernetic colony refuses the click - those people eat production and farm
nothing - but the labour solver still reached for it first when it needed somewhere to put the
difference. Dragging production by hand therefore parked workers in a row that yields nothing at
all, and the bar showed it filling. A row that cannot be dragged is now off limits to the solver
as well, so the difference goes to research, which can use it.

### Refit sends a ship to the nearest prioritized port, not the emptiest
`aa4af6be` — `Empire_RallyPlanets.cs` (`FindPlanetToRefitAt`)

**Source: Roland-Johansen.**

Refit normally minimises `TurnsUntilQueueComplete + travel time` (travel counted twice when the
ship flies back). A player with at least one prioritized port fell through to the *construction*
path instead, which minimises queue time alone: the travel term disappeared and refits were
dispatched to whichever prioritized yard was idle, however far. Restricting candidates to the
prioritized ports was the intended half and stays; the selector is restored. The same shortcut
also relaxed port quality from 1 to 0.5, widening the candidate list, and that goes with it.
Players who have marked prioritized ports will see refit traffic regroup around nearby yards.

### Lighter-materials research no longer weakens mass-reduction devices
`19c5f666` — `Ships/ShipStats.cs` (`InitializeMass`)

**Source: #341.**

`mass *= loyalty.data.MassModifier` applied to the *signed* sum of module masses, so a -20%
modifier also weakened inertial dampeners: a -100 reduction became -80. The modifier now
applies to positive mass only — `positiveMass * modifier + negativeMass` — so the hull gets
lighter while reduction devices keep their full effect.

### Building profitability judged at a nominal tax rate
`188e1ee5` — `ColonyResource.cs`

**Source: #321.**

Revenue-bearing buildings were judged profitable or not at the empire's *live* tax rate, tying
build/scrap decisions to fiscal policy. They are now judged at a fixed `NominalTaxRate = 0.25f`,
so moving the tax slider no longer rewrites what governors think is worth building.
*(Biospheres no longer consult this estimator at all — see the capacity entry above.)*

### Budget allocation tail snapped to zero
`e826cb9d` — `AI/EmpireAI/Budget.cs` (`SnapSpentTail`)

**Source: maintainer.**

Per-colony allocations are EMA-smoothed, so an allocation heading for zero approaches it
geometrically and never lands: a colony set to spend nothing kept reporting a 0.03 budget for
dozens of turns, the panels rounding the shrinking tail to a figure that looked frozen. Below
`MinMeaningfulAlloc` (0.05 BC/turn) the allocation is now snapped to zero. The cut applies to
the automatic side only — an explicit manual budget stands however small.

### Unmet empires accrue no infiltration
`63c7fb36`, `a753376f` — `Espionage.cs`, `Empire.cs`

**Source: maintainer.**

An un-met faction was meant to be excluded from espionage progression — otherwise infiltration
advances and eventually notifies, revealing a faction the player has not discovered. That guard
was repeated in each caller and still leaked: `Update` kept toggling passive effects and
operations for factions never met. The rule now sits at the source — `EffectiveWeight` is zero
until the relation is `Known` — and un-met empires are skipped outright.

### Governor builds biospheres again on ex-volcano worlds
`ef366e9e` — `Planet_EvaluateBuildings.cs` (`GetPreferredTile`)

**Source: #312.**

`GetPreferredTile` elected tiles already carrying a building or a queued item, which `Enqueue`
then rejected on every pass, while valid terraformable tiles stayed excluded from preference:
the governor stalled forever. Manual placement worked, since it validates the actual tile. The
preferred tile is now enqueue-valid or null, and null falls through to random assignment, which
accepts terraformables.

### Treasury warning only on a real deficit
`2f03aec0` — `Empire.cs` (`TakeTurn`)

**Source: maintainer.**

The alert fired every turn whenever `Money / AllSpending < 2`, regardless of whether the empire
was actually losing money. Now requires `NetIncome < 0` as well, and fires at most once per
StarDate year.
