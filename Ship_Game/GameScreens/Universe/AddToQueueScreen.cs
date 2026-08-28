using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.GameScreens;
using Ship_Game.Ships;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (maintainer, bench 526): "Add to Construction Queue" - a build order given
    // once and posted to every colony that can take it. The maintainer's own words for it:
    // a blueprint built on the fly.
    //
    // ⚠ PROTOTYPE. It carries its own lists rather than borrowing the colony screen's: those
    // are welded to that screen - each row holds a reference to it - and prying them loose is
    // the expensive half of this feature. The behaviour is what wants judging first; the rows
    // can be brought onto the colony screen's own once it is.
    //
    // The one thing that is NOT provisional is what Apply writes - see ApplyToTargets.
    public sealed class AddToQueueScreen : PopupWindow
    {
        readonly UniverseScreen Universe;
        Empire Player => Universe.Player;

        // ⚠ this popup IS a frame, and the report it summons has to centre on it. Without this
        // the base returns the whole display, PageFrameCentre reads that as "no frame at all"
        // and hands back null, so the message box lands in the middle of the screen instead of
        // in the middle of the window that asked for it (bench 528).
        public override Rectangle PageFrame => Rect;

        // What the picker is aiming at. The two negatives are the targets that are not a
        // governor type: every colony, and the ones that have no governor at all - the young
        // ones, which is the case this button was asked for.
        const int TargetAll = -1;
        const int TargetNoGovernor = -2;
        int Target = TargetAll;

        enum Tab { Buildings, Troops, Ships }
        Tab CurrentTab = Tab.Buildings;

        // One basket line = one copy on each colony that can take it. The same thing may be
        // added several times, which is how "five troops on every planet" is said - the colony
        // screen says it the same way, by clicking five times.
        class Entry
        {
            public Building Building;
            public string TroopType;
            public IShipDesign Ship;
            public string Name => Building?.Name ?? TroopType ?? Ship?.Name ?? "";
        }

        readonly List<Entry> Basket = new();

        ScrollList<SourceItem> SourceList;
        ScrollList<BasketItem> BasketList;
        UIButton ApplyButton;

        const int PopupW = 720, PopupH = 560;
        const int RowH = 22, ListTop = 118, ListH = 320;

        // ⚠ the SUMMONER is the parent, not the universe: PopupWindow centres itself on its
        // parent's page frame, and handed the fullscreen universe it centred on the display
        // instead of on the page that opened it (bench 528)
        public AddToQueueScreen(GameScreen summoner, UniverseScreen u) : base(summoner, PopupW, PopupH)
        {
            Universe = u;
            TitleText = Localizer.Token(GameText.AddToQueueTitle);
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        // ── the colonies the picker is aiming at ────────────────────────────────────────────
        Planet[] Targets()
        {
            IReadOnlyList<Planet> owned = Player.GetPlanets();
            return Target switch
            {
                TargetAll        => owned.ToArray(),
                TargetNoGovernor => owned.Where(p => !p.HasLaborGovernor).ToArray(),
                _                => owned.Where(p => (int)p.CType == Target).ToArray(),
            };
        }

        // The UNION of what the targeted colonies can build. A colony's buildable list depends on
        // its own ground and on what already stands there, so there is no such thing as "what the
        // empire can build" - the union is the closest honest answer, and Apply lets each colony
        // take from it what it can.
        Building[] BuildableUnion()
        {
            var seen = new HashSet<int>();
            var union = new List<Building>();
            foreach (Planet p in Targets())
                foreach (Building b in p.GetBuildingsCanBuild())
                    if (seen.Add(b.BID))
                        union.Add(b);

            return union.OrderBy(b => b.Name).ToArray();
        }

        public override void LoadContent()
        {
            base.LoadContent();

            // ⚠ in TWO pieces, and the foot is inset past the corner blocks. A single panel
            // carried down to the bottom rule paints over the outer 28px columns where the
            // hand-drawn corner arcs live, and they read as unfinished. This is the exact shape
            // fixed in the refit popup this morning, rewritten here from memory of the OLD one.
            const int CornerW = 28;
            Color fill = ScreenGroups.GroupFrameFill;
            Add(new UIPanel(BottomBigFill, fill));
            Add(new UIPanel(new Rectangle(Rect.X + CornerW, BottomBigFill.Bottom,
                                          Rect.Width - 2 * CornerW,
                                          Rect.Bottom - PopupFrame.BottomLine - BottomBigFill.Bottom), fill));

            float pickerY = BodyTop + 8;
            Add(new UILabel(GameText.AddToQueueTargetLabel, Fonts.Arial12Bold, Colors.Cream))
                .Pos = new Vector2(Rect.X + 20, pickerY + 2);

            var tabsRect = new RectF(Rect.X + 16, Rect.Y + ListTop - 26, PopupW / 2 - 30, ListH + 26);
            var tabs = Add(new Submenu(tabsRect, new LocalizedText[]
            {
                GameText.Buildings, GameText.Troops, GameText.Ships,
            }));
            tabs.OnTabChange = i => { CurrentTab = (Tab)i; RefreshSource(); };

            var sourceRect = new RectF(tabsRect.X + 8, Rect.Y + ListTop + 6, tabsRect.W - 16, ListH - 12);
            SourceList = Add(new ScrollList<SourceItem>(sourceRect, RowH));
            SourceList.EnableItemHighlight = true;
            SourceList.OnClick = item =>
            {
                // a building is had or not had: a colony cannot raise the same one twice, so a
                // second copy in the basket could only ever be dropped on arrival. Troops and
                // ships DO repeat - that is how a count is expressed here (bench 528).
                if (item.Building != null && Basket.Any(e => e.Building?.BID == item.Building.BID))
                    return;
                Basket.Add(item.Make());
                RefreshBasket();
            };

            var basketFrame = new RectF(Rect.X + PopupW / 2 + 6, Rect.Y + ListTop - 26,
                                        PopupW / 2 - 30, ListH + 26);
            Add(new Submenu(basketFrame, GameText.AddToQueueBasket));
            var basketRect = new RectF(basketFrame.X + 8, Rect.Y + ListTop + 6, basketFrame.W - 16, ListH - 12);
            BasketList = Add(new ScrollList<BasketItem>(basketRect, RowH));
            BasketList.EnableItemHighlight = true;
            // clicking a line takes ONE copy back: the basket counts, so removing must count too
            BasketList.OnClick = item => { Basket.Remove(item.Entry); RefreshBasket(); };

            // no Close button: the frame carries its own cross, and a second way out of the same
            // room is the double door we removed from the blueprint picker this morning
            float buttonsY = Rect.Y + ListTop + ListH + 20;
            ApplyButton = Button(ButtonStyle.Default, Rect.X + PopupW - 200, buttonsY,
                                 GameText.AddToQueueApply, click: _ => ApplyToTargets());
            ApplyButton.Tooltip = GameText.AddToQueueApplyTip;
            ApplyButton.Enabled = false;

            // ⚠ added LAST, and that is its whole placement rule: add order is draw order, so a
            // picker seated before the tabs opened its list UNDERNEATH them (bench 528)
            var targetList = Add(new DropOptions<int>(220, 18));
            targetList.AddOption(option: GameText.AddToQueueAllColonies, TargetAll);
            targetList.AddOption(option: GameText.AddToQueueNoGovernor, TargetNoGovernor);
            targetList.AddOption(option: GameText.Core,         (int)Planet.ColonyType.Core);
            targetList.AddOption(option: GameText.Industrial,   (int)Planet.ColonyType.Industrial);
            targetList.AddOption(option: GameText.Agricultural, (int)Planet.ColonyType.Agricultural);
            targetList.AddOption(option: GameText.Research,     (int)Planet.ColonyType.Research);
            targetList.AddOption(option: GameText.Military,     (int)Planet.ColonyType.Military);
            targetList.ActiveValue = Target;
            targetList.Pos = new Vector2(Rect.X + 150, pickerY);
            targetList.OnValueChange = t => { Target = t; RefreshSource(); };

            RefreshSource();
        }

        // ⚠ a colony's buildable list is a CACHE. It is refreshed when the colony is governed or
        // when its screen is opened - so on a save just loaded and left paused, colonies that
        // have not ticked yet report NOTHING, and this union came out shorter or longer depending
        // on where the player had been. Refreshed on the simulation thread first; the rows are
        // rebuilt on the next update, which costs one frame and nothing else (bench 529).
        void RefreshSource()
        {
            if (CurrentTab == Tab.Buildings)
            {
                Planet[] targets = Targets();
                Universe.RunOnSimThread(() =>
                {
                    foreach (Planet p in targets)
                        p.RefreshBuildingsWeCanBuildHere();
                    PendingSourceRefresh = true;
                });
            }
            BuildSourceItems();
        }

        void BuildSourceItems()
        {
            var items = new Array<SourceItem>();
            switch (CurrentTab)
            {
                case Tab.Buildings:
                    foreach (Building b in BuildableUnion())
                        items.Add(new SourceItem { Building = b });
                    break;
                case Tab.Troops:
                    foreach (string t in Player.GetTroopsWeCanBuild().OrderBy(t => t))
                        items.Add(new SourceItem { TroopType = t });
                    break;
                case Tab.Ships:
                    // the same rule the colony screen uses, plus the one this screen owns:
                    // platforms and stations are raised from the deep-space build window, never
                    // from a colony queue, so they have no place here (bench 528/529)
                    foreach (IShipDesign s in Player.ShipsWeCanBuildSnapshot
                                                    .Where(s => Empire.IsPlayerQueueableShip(s, Player)
                                                             && !s.IsPlatformOrStation)
                                                    .OrderBy(s => s.Name))
                        items.Add(new SourceItem { Ship = s });
                    break;
            }
            SourceList.SetItems(items);
        }

        void RefreshBasket()
        {
            BasketList.SetItems(Basket.Select(e => new BasketItem { Entry = e }));
            ApplyButton.Enabled = Basket.Count > 0;
        }

        // ── what Apply actually writes ─────────────────────────────────────────────────────
        //
        // ⚠ THE LINE THAT CARRIES THIS FEATURE: buildings go in with playerAdded TRUE. The
        // parameter is optional and defaults to FALSE, so the obvious call would post, in the
        // player's own name, queue items the game believes the governor put there - and the
        // governor would then be free to scrap them, sieve them through a blueprint, or let the
        // terraformer walk over them. Seven places read that flag and every one is about
        // buildings, which is also why the troop and ship calls neither take it nor need it:
        // nothing scraps a queued ship.
        //
        // Troops and ships go only where they can be built at all - no spaceport, no ships. That
        // is their only gate: one basket line is one copy per eligible colony, which is exactly
        // how "five troops everywhere" is asked for.
        void ApplyToTargets()
        {
            Planet[] targets = Targets();
            var blocked = new List<string>();

            Universe.RunOnSimThread(() =>
            {
                foreach (Planet p in targets)
                {
                    foreach (Entry e in Basket)
                    {
                        if (e.Building != null)
                        {
                            // already standing or already queued: not a failure, nothing to do
                            if (p.BuildingBuiltOrQueued(e.Building))
                                continue;
                            if (!p.Construction.Enqueue(e.Building, playerAdded: true))
                                blocked.Add($"{p.Name} - {e.Building.Name}");
                        }
                        else if (e.TroopType != null)
                        {
                            if (p.CanBuildInfantry)
                                p.Construction.Enqueue(ResourceManager.GetTroopTemplate(e.TroopType),
                                                       QueueItemType.Troop);
                        }
                        else if (e.Ship != null && p.HasSpacePort)
                        {
                            // the queue type comes from the one classifier the colony screen uses,
                            // so the same ship is filed the same way from either end
                            p.Construction.Enqueue(e.Ship, QueueItem.PlayerQueueTypeFor(e.Ship));
                        }
                    }
                }

                // A report of failures alone says nothing at all when everything worked, which is
                // exactly when the button most looks broken. The nominal case gets its own line.
                //
                // ⚠ handed BACK to the UI rather than shown from here: this runs on the simulation
                // thread, and pushing a screen onto the stack from there reaches into what the
                // draw loop is walking.
                PendingReport = blocked.Count == 0
                    ? Localizer.Token(GameText.AddToQueueAllPassed)
                    : Localizer.Token(GameText.AddToQueueBlocked) + "\n\n" + string.Join("\n", blocked);
            });
        }

        // the sim thread writes these, the UI picks them up on its own beat
        volatile string PendingReport;
        volatile bool PendingSourceRefresh;

        public override void Update(float fixedDeltaTime)
        {
            if (PendingSourceRefresh)
            {
                PendingSourceRefresh = false;
                BuildSourceItems();
            }

            string report = PendingReport;
            if (report != null)
            {
                PendingReport = null;
                ScreenManager.AddScreen(new MessageBoxScreen(this, report, MessageBoxButtons.Ok));
            }
            base.Update(fixedDeltaTime);
        }

        // ── the two lists' rows ────────────────────────────────────────────────────────────
        class SourceItem : ScrollListItem<SourceItem>
        {
            public Building Building;
            public string TroopType;
            public IShipDesign Ship;

            public Entry Make() => new() { Building = Building, TroopType = TroopType, Ship = Ship };

            // ⚠ not "Label": UIElementContainer already has Label(...) further up the chain, and
            // this repo compiles CS0108 as an error. A name in a derived class is checked against
            // the whole base chain, not just against its own file.
            string RowText => Building?.Name ?? TroopType ?? Ship?.Name ?? "";

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                batch.DrawString(Fonts.Arial12, RowText, new Vector2(X + 4, Y + 2), Colors.Cream);
            }
        }

        class BasketItem : ScrollListItem<BasketItem>
        {
            public Entry Entry;

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                batch.DrawString(Fonts.Arial12, Entry.Name, new Vector2(X + 4, Y + 2), Colors.Cream);
            }
        }
    }
}
