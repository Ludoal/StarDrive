using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens;
using Ship_Game.UI; // SplitElement (a label and its control sharing one row)
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork: the Policies tab of the Empire group - the empire's STANDING ORDERS.
    //
    // The line that decides what belongs here: Automation answers "do it for me", Policies
    // answers "when you do it, do it THIS way". A control that only delegates a chore stays
    // in Automation; a control that declares a doctrine lives here.
    //
    // Categories: ECONOMY, RESEARCH, COLONY, TRADE, CONSTRUCTION. Each wears its own one-tab
    // frame and they are ALL visible at once - the same façade as Automation, on purpose.
    //
    // No mute control: every frame says what it governs, in the tooltip of its own tab. The
    // sentence is there for the first visit and out of the way afterwards, which is how a
    // standing-orders page is read - once to learn it, then to change one line.
    public sealed class PoliciesScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;
        // this page's real frame is its tab row's rect - the band excludes exactly what the
        // page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;

        ShareRow FreighterShares;
        DropOptions<Planet.BuildMandate> EmpireBuildMandateList, EmpireScrapMandateList;
        DropOptions<string>[] BlueprintPolicyLists;
        UICheckBox RushNewColonies;
        UIPanel PriorityHost;

        // fixed box geometry - the boxes own their sizes, the columns just stack them.
        // Heights: one-tab strip (~24) + 12 top pad + 26 per row + 12 bottom pad. Each frame's
        // notice line lives in its tab's tooltip, hence 20px less.
        // BoxW2 (Trade): 370, not 450 - its widest row is a 300px rail, its value lane and a padlock (bench 617)
        const float BoxW = 320f, BoxW2 = 370f, BoxW3 = 300f, BoxGap = 10f;
        // Colony carries the two mandates AND the default-plan table: a heading plus one row per
        // governor type that can hold a plan. Written as a count times a row height, so adding a
        // governor type later moves the box by itself instead of needing a new magic number.
        const float PolicyRowH = 26f;
        const float BlueprintRows = 6f;                       // heading + 5 governor types
        // +1 Auto-terraform, then the New Colony block: its heading, three switches and a rail
        const float ColonyBoxH = 170f + PolicyRowH + BlueprintRows * PolicyRowH + 5 * PolicyRowH;
        // A number on a rail costs two rows: its own title, then the rail itself, whose 28px
        // must hold a 26px knob (bench 485). The rail is narrower than its frame: these are
        // short ranges, and the value prints past the rail's right end (bench 538).
        const float SliderRowH = 64f, SliderRailW = 300f;
        // Trade carries the food level, the priority picker, the three quantity rails and the game rule.
        // ⚠ no Economy or Research frame on this page (maintainer feedback): Auto-taxes lives on
        // the Economy screen and Auto-research on the Research screen, each over the panels it
        // governs. A frame holding one switch that is also somewhere else is a second place to
        // look, not a policy.
        // the three share rails stack under the Auto switch, each under its own caption row, on
        // the rhythm of the other rails: a caption, then a rail whose track is tucked up under it
        // (bench 617: the knob climbed onto a caption drawn on the rail itself)
        const float ShareCaptionH = 18f, ShareRailPitch = 46f, ShareRowsH = 3f * ShareRailPitch + 10f;
        // one rail left: the three freighter numbers went to Automation, each under the toggle
        // it qualifies (maintainer feedback)
        const float TradeBoxH = 126f + 26f + 1f * SliderRowH + ShareRowsH; // +26: the priority title row

        // The Prioritization rows live INSIDE the Construction frame, under its Rush row.
        // Both numbers are CONSTANTS and the frame is sized FROM them - never the
        // reverse: a host placed at a share of the space left moves every time a row is added
        // above it.
        const float PrioTopInset = 124f, PrioRowsH = 300f; // heading + Continuous Rush + the new-colony toggle
        // and below the rows, a heading of its own plus the button it names
        // the box ends with its priority rows now: the manual order moved to the Colonies tab,
        // and a reserved lane belongs to what fills it
        const float ConstructionBoxH = PrioTopInset + PrioRowsH + 18f;

        public PoliciesScreen(UniverseScreen u) : base(u, toPause: u)
        {
            Universe = u;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        public override void LoadContent()
        {
            RemoveAll();
            float col1H = ColonyBoxH;
            float col2H = TradeBoxH;
            float contentW = 9 + 10 + BoxW + BoxGap + BoxW2 + BoxGap + BoxW3 + 10 + 9;  // ClientArea insets + gutters
            float contentH = 60 + Math.Max(Math.Max(col1H, col2H), ConstructionBoxH) + 22; // tab strip + cross clearance + pads
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), ScreenGroups.TabIndexOf(this),
                                                    OnEmpireTabChanged, contentW, contentH);

            RectF client = EmpireTabs.ClientArea;
            float top = ScreenGroups.GroupContentTop(client);
            // middle column is Construction, right column is Trade (bench 538): each x is
            // derived from the width of the box actually seated to its left.
            float x0 = client.X + 10, x1 = x0 + BoxW + BoxGap, x2 = x1 + BoxW3 + BoxGap;
            Empire player = Universe.Player;

            // ⚠ within a column the LOWER box is added FIRST: an open dropdown's list spills
            // below its own row, and add order is draw order - the spill must land on top
            // of the neighbour, not under it.

            UIList colony = NewBox(new RectF(x0, top, BoxW, ColonyBoxH), "Governors", GameText.PolColonyNotice);
            // the empire's own mandates: what a colony left on Auto follows. Same picker as the
            // colony's, minus the Auto position - a policy has nowhere to defer to.
            // label and picker share a row: a label on its own line reads as a heading, and this
            // is a setting. Cream like every other label on the page.
            EmpireBuildMandateList = MandateDropdown.Make(player.EmpireBuildMandate,
                m => Universe.RunOnSimThread(() => player.EmpireBuildMandate = m), withAuto: false);
            EmpireScrapMandateList = MandateDropdown.Make(player.EmpireScrapMandate,
                m => Universe.RunOnSimThread(() => player.EmpireScrapMandate = m), withAuto: false, scrap: true);
            // Split names the picker's X outright, so the two rows cannot land a pixel apart the
            // way they would if each hugged the right edge of a label of its own width.
            const float MandateSplit = 118f;
            colony.Add(new SplitElement(new UILabel(GameText.BuildMandate, Fonts.Arial12Bold, Colors.Cream)
                { Tooltip = GameText.BuildMandateTip }, EmpireBuildMandateList) { Split = MandateSplit });
            colony.Add(new SplitElement(new UILabel(GameText.ScrapMandate, Fonts.Arial12Bold, Colors.Cream)
                { Tooltip = GameText.ScrapMandateTip }, EmpireScrapMandateList) { Split = MandateSplit });

            // the default plan per governor type. Only a colony whose Blueprint is set to Auto
            // follows its row; Custom and None are never touched from here.
            colony.Add(new UILabel(GameText.PolBlueprintsHeading, Fonts.Arial12Bold, Colors.Cream)
                { Tooltip = GameText.PolBlueprintsTip });
            BlueprintPolicyLists = new DropOptions<string>[BlueprintGovernors.Length];
            for (int i = 0; i < BlueprintGovernors.Length; ++i)
            {
                (Planet.ColonyType type, GameText label) = BlueprintGovernors[i];
                DropOptions<string> list = MakeBlueprintPolicyList(player, type);
                BlueprintPolicyLists[i] = list;
                colony.Add(new SplitElement(new UILabel(label, Fonts.Arial12, Colors.Cream), list)
                    { Split = MandateSplit });
            }
            // Auto-terraform closes the frame: it acts on a colony the empire ALREADY holds, which
            // is what this page is about - unlike auto-colonisation, which decides where the empire
            // goes next and stays with the other build automations (maintainer feedback).
            colony.AddCheckbox(() => player.AutoBuildTerraformers, title: GameText.AutoBuildTerraformers,
                               tooltip: GameText.AutoBuildTerraformersTip);

            // ★ EVERYTHING BELOW THIS LINE IS A DEFAULT FOR A COLONY NOT YET FOUNDED, and the
            // heading is what separates it from an order: the Economy and Defense tabs give
            // orders to the colonies that exist, this gives a starting point to the next one.
            // ⚠ the three settings go UNDER the heading, never above, or a default gets read as
            // an order (maintainer feedback).
            colony.Add(new UILabel("New Colony", Fonts.Arial12Bold, Colors.Cream))
                .Tooltip = "What a colony founded from now on starts with. Nothing here reaches a "
                         + "colony that already exists.";
            // ⚠ "Auto Governor" decides whether a new colony gets an ASSESSED governor -
            // see Planet_Colonize.SetupColonyType. It belongs with the rest of the newborn's kit.
            colony.AddCheckbox(() => player.AutoCoreGovernor, title: "Auto Governor",
                               tooltip: GameText.AutoGovernorTip);
            colony.AddCheckbox(() => player.NewColonyAutoTroops, title: "Auto Build Garrison",
                               tooltip: "A new colony trains its own garrison from the start.");
            SliderRow(colony, "Garrison Size", "How many troops a new colony aims to keep. Zero "
                      + "leaves it to you, which is the game's own default.",
                      0, DefenseListItem.MaxGarrison, player.NewColonyGarrison, "none",
                      v => Universe.RunOnSimThread(() => player.NewColonyGarrison = v));
            colony.AddCheckbox(() => player.NewColonyGovOrbitals,
                               title: "Governor Manages Space Defense",
                               tooltip: GameText.DvDefenseSpaceDefTip);

            colony.ReverseZOrder(); // an open list draws over the rows beneath it

            // ⚠ Automation carries a box of the same name: that one is what the empire DOES by
            // itself, this one the rules the freighters obey.
            UIList trade = NewBox(new RectF(x2, top, BoxW2, TradeBoxH), "Trade", GameText.PolTradeNotice);
            // the RIGHT is read before the doctrine that uses it (bench 538), so the permission
            // to trade abroad heads the frame. Its own tooltip says out loud that this one is a
            // GAME rule, not an empire order - it is stored with the game setup, so it does not
            // travel with the empire.
            trade.AddCheckbox(() => Universe.UState.P.AllowPlayerInterTrade,
                              title: GameText.AllowPlayerInterTradeTitle, tooltip: GameText.PolInterTradeGameRuleTip);
            // (maintainer feedback) food is the dispatch's first call; this level says for WHOM.
            // Its right stop is not a quantity: at Default every colony that orders food is served
            // first, as in the base game, and the stop names the 90% cutoff it stands on. That
            // word is wider than a number, so the rail keeps a wider value lane.
            SliderRow(trade, GameText.PolFoodFirstBelow, GameText.PolFoodFirstBelowTip,
                      0, Planet.FoodImportCutoffPct, player.FoodFirstBelowPct, default,
                      v => player.FoodFirstBelowPct = v, "%",
                      maxText: GameText.PolFoodFirstDefault, valueLane: 100);
            // (maintainer feedback) Freighter Priority is a switch and three linked shares. Auto is
            // the game's own conduct: a population-weighted roll between production and colonists
            // each turn, trade on the leftovers. Off, the free hulls of each turn are split at the
            // three percentages below, which sum to 100 and rebalance each other.
            // a title row, then the Auto switch under it (bench 616)
            trade.Add(new UILabel(GameText.FreighterPriority, Fonts.Arial12Bold, Colors.Cream)).Tooltip = GameText.PolFreighterPriorityAutoTip;
            trade.AddCheckbox(() => player.FreighterPriorityAuto,
                              title: GameText.FreighterPriorityAuto, tooltip: GameText.PolFreighterPriorityAutoTip);
            FreighterShares = trade.Add(new ShareRow(player));
            // Ludoal fork (maintainer feedback): the three quantity numbers. Automation's
            // checkboxes stay the RIGHT to build, upgrade and scrap; these three say HOW,
            // which is what puts them on this page. Every one of them is neutral at its
            // default, so a page never touched changes nothing.

            trade.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList construction = NewBox(new RectF(x1, top, BoxW3, ConstructionBoxH), "Construction", GameText.PolConstructionNotice, out Submenu constructionBox);
            // ⚠ NOT a plain checkbox: its setter marshals onto the SIMULATION thread. Copying it
            // as a bare boolean would look right and propagate nothing.
            // (bench 528) the two things this frame does, each said out loud: what it rushes, and
            // what the player posts by hand. A timed rush joins the first.
            construction.Add(new UILabel(GameText.PolRushProduction, Fonts.Arial12Bold, Colors.Cream));
            construction.AddCheckbox(() => RushConstruction, title: GameText.RushAllConstruction, tooltip: GameText.RushAllConstructionTip);

            // (bench 529) a young colony rushed for its first turns. Greyed while the continuous
            // rush is on - that one already rushes everything, everywhere.
            RushNewColonies = construction.AddCheckbox(() => player.RushNewColonies,
                                                       v => player.RushNewColonies = v,
                                                       title: GameText.PolRushNewColony,
                                                       tooltip: GameText.PolRushNewColonyTip);


            // the prioritized categories, their ORDER is the hierarchy; arrows reorder, the
            // inhibit glyph demotes, the plus promotes. Acts at queue INSERTION only
            // (SBProduction) - reordering never reshuffles queues already filled, and the
            // section's tooltip says so.
            // ⚠ the host takes its geometry from the frame's own client area, the same source the
            // list rows use (+12) - never from a second sum over x2 and BoxW3. ClientArea is
            // already inset by 9 (the corner textures' size), so a second arithmetic lands the
            // host 15px left of the rows above it.
            PriorityHost = Add(new UIPanel(new Rectangle((int)(constructionBox.ClientArea.X + 12),
                                                         (int)(top + PrioTopInset),
                                                         (int)(constructionBox.ClientArea.W - 24),
                                                         (int)PrioRowsH),
                                           new Color(0, 0, 0, 0)));
            RebuildPriorityRows();

            // Ludoal fork (maintainer feedback): the empire-wide build order has MOVED to the
            // Colonies tab, at the head of the Construction column it fills. An order is given
            // where its effect is read; this page holds standing rules, not orders. ⚠ ONE door:
            // it is not offered here as well - an order with two entrances is two behaviours to
            // keep in step.

            base.LoadContent();
        }

        // Three linked shares stacked - Production, Colonists, Trade - each a caption over a rail,
        // with a padlock at the rail's right end (bench 616: stacked, so the values line up). They
        // sum to 100: moving one rebalances the unlocked others at their current proportion, a
        // locked one is left alone. Under Auto the rails are inert, the locks drawn shut, and the
        // rails show what the pool did over the last ten turns (maintainer feedback).
        class ShareRow : UIElementV2
        {
            const float RailW = SliderRailW, RailH = 28f, LockGap = 6f;
            public const float LockSize = 16f;
            readonly Empire Player;
            readonly UILabel[] Captions = new UILabel[3];
            readonly FloatSlider[] Rails = new FloatSlider[3];
            readonly LockToggle[] Locks = new LockToggle[3];
            bool Rebalancing; // a rail set from here fires its own OnChange - not a player's move
            static readonly GameText[] CaptionText = { GameText.Production, GameText.Colonists, GameText.Trade };

            public ShareRow(Empire player)
            {
                Player = player;
                for (int i = 0; i < 3; ++i)
                {
                    int k = i;
                    Captions[i] = new UILabel(new Vector2(-200f, -200f), CaptionText[i], Fonts.Arial12Bold, Colors.Cream) { Tooltip = GameText.PolShareTip };
                    // the same rail as SliderRow builds: no text of its own, the track tucked up
                    // under the caption row
                    Rails[i] = new FloatSlider(SliderStyle.Decimal, new Vector2(RailW, RailH), "", 0, 100, Share(i))
                    {
                        Step = 1, Tip = GameText.PolShareTip, TrackYOffset = -5, ValueSuffix = "%",
                    };
                    Rails[i].OnChange = s => OnShareMoved(k, (int)s.AbsoluteValue);
                    Locks[i] = new LockToggle(Locked(i), v => SetLocked(k, v)) { Tooltip = GameText.PolShareLockTip };
                }
                Size = new Vector2(RailW + LockGap + LockSize, 3 * ShareRailPitch);
            }

            int Share(int i) => i == 0 ? Player.ShareProdPct : i == 1 ? Player.ShareColonistsPct : Player.ShareTradePct;
            bool Locked(int i) => i == 0 ? Player.ShareProdLocked : i == 1 ? Player.ShareColonistsLocked : Player.ShareTradeLocked;
            void SetShare(int i, int v)
            {
                if (i == 0) Player.ShareProdPct = v; else if (i == 1) Player.ShareColonistsPct = v; else Player.ShareTradePct = v;
            }
            void SetLocked(int i, bool v)
            {
                if (i == 0) Player.ShareProdLocked = v; else if (i == 1) Player.ShareColonistsLocked = v; else Player.ShareTradeLocked = v;
            }

            // the moved share takes its value within what the locked ones leave; the unlocked
            // others share the rest at their current proportion (evenly when both stand at nought)
            void OnShareMoved(int moved, int value)
            {
                if (Rebalancing || Player.FreighterPriorityAuto)
                    return;
                Rebalancing = true;
                int lockedSum = 0;
                var free = new Array<int>();
                for (int i = 0; i < 3; ++i)
                {
                    if (i == moved) continue;
                    if (Locked(i)) lockedSum += Share(i);
                    else free.Add(i);
                }
                int room = 100 - lockedSum;
                value = value.Clamped(0, room);
                int rest = room - value;
                int freeSum = 0;
                foreach (int i in free) freeSum += Share(i);
                int given = 0;
                for (int n = 0; n < free.Count; ++n)
                {
                    int i = free[n];
                    int part = n == free.Count - 1 ? rest - given
                             : freeSum > 0 ? rest * Share(i) / freeSum
                             : rest / free.Count;
                    SetShare(i, part);
                    given += part;
                }
                SetShare(moved, value);
                ShowShares();
                Rebalancing = false;
            }

            // what the rails print: the shares - or under Auto, in the same unit, what the pool
            // actually did over the last ten turns (runs laid down per pass, as percentages)
            void ShowShares()
            {
                int[] shown = { Share(0), Share(1), Share(2) };
                if (Player.FreighterPriorityAuto)
                {
                    // until the window holds a run, the rails keep the shares as set rather than
                    // dropping to nought at the switch (bench 617)
                    int[] runs = Player.RecentRuns;
                    int total = runs[0] + runs[1] + runs[2];
                    if (total > 0)
                    {
                        shown[0] = runs[0] * 100 / total;
                        shown[1] = runs[1] * 100 / total;
                        shown[2] = 100 - shown[0] - shown[1];
                    }
                }
                Rebalancing = true;
                for (int i = 0; i < 3; ++i)
                    if ((int)Rails[i].AbsoluteValue != shown[i])
                        Rails[i].AbsoluteValue = shown[i];
                Rebalancing = false;
            }

            public override void PerformLayout()
            {
                for (int i = 0; i < 3; ++i)
                {
                    float y = Pos.Y + i * ShareRailPitch;
                    Captions[i].Pos = new Vector2(Pos.X, y);
                    Captions[i].PerformLayout();
                    float railY = y + ShareCaptionH;
                    Rails[i].Pos = new Vector2(Pos.X, railY);
                    Rails[i].PerformLayout();
                    // the padlock sits on the track's line (RailH/2 + TrackYOffset), past the value lane
                    Locks[i].Pos = new Vector2(Pos.X + RailW + LockGap, railY + RailH / 2f - 5f - LockSize / 2f + 3f);
                    Locks[i].PerformLayout();
                }
                base.PerformLayout();
            }

            public override bool HandleInput(InputState input)
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (Locks[i].Enabled && Locks[i].HandleInput(input)) return true;
                    if (Rails[i].Enabled && Rails[i].HandleInput(input)) return true;
                    if (Captions[i].HandleInput(input)) return true;
                }
                return false;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                bool auto = Player.FreighterPriorityAuto;
                ShowShares();
                for (int i = 0; i < 3; ++i)
                {
                    bool inert = auto || Locked(i);
                    Rails[i].Enabled = !inert;
                    Rails[i].Greyed = inert;
                    Locks[i].Enabled = !auto;
                    Locks[i].Shut = auto || Locked(i);
                    Locks[i].Greyed = auto;
                    Captions[i].Color = auto ? Color.Gray : Colors.Cream;
                    Captions[i].Draw(batch, elapsed);
                    Rails[i].Draw(batch, elapsed);
                    Locks[i].Draw(batch, elapsed);
                }
            }
        }

        // a padlock: lit when shut, dim when open, darker when the whole row is inert
        class LockToggle : UIElementV2
        {
            readonly SubTexture Icon = ResourceManager.Texture("NewUI/icon_lock");
            readonly Action<bool> OnToggle;
            public bool Shut;
            public bool Greyed;
            public LocalizedText Tooltip;

            public LockToggle(bool shut, Action<bool> onToggle)
            {
                Shut = shut;
                OnToggle = onToggle;
                Size = new Vector2(ShareRow.LockSize, ShareRow.LockSize);
            }

            public override bool HandleInput(InputState input)
            {
                if (!Rect.HitTest(input.CursorPosition))
                    return false;
                if (Tooltip.IsValid)
                    ToolTip.CreateTooltip(Tooltip);
                if (input.LeftMouseClick)
                {
                    Shut = !Shut;
                    OnToggle(Shut);
                    GameAudio.AcceptClick();
                    return true;
                }
                return false;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                Color tint = Greyed ? new Color(70, 70, 70) : Shut ? Color.White : new Color(120, 120, 120);
                batch.Draw(Icon, Rect, tint);
            }
        }

        // A number set on a rail, the way the tax rate and the notification delay already work.
        // The title takes its own row: the slider prints its value at the rail's right end, so
        // a label inside would crowd it. zeroText names what the left stop MEANS when nought
        // is not a quantity; left empty the rail simply shows the number. maxText does the same
        // for the right stop. valueLane is the widest the value can be: a rail whose stop carries
        // a word gives up track for it, and the column still lines up on the last digit.
        void SliderRow(UIList box, in LocalizedText title, in LocalizedText tooltip, float min, float max,
                       int current, LocalizedText zeroText, Action<int> onChange, string suffix = "",
                       LocalizedText maxText = default, int valueLane = FloatSlider.DefaultValueLane)
        {
            box.Add(new UILabel(title, Fonts.Arial12Bold, Colors.Cream)).Tooltip = tooltip;
            var rail = box.Add(new FloatSlider(SliderStyle.Decimal, new Vector2(SliderRailW, 28),
                                               "", min, max, current)
            {
                Step = 1,
                Tip = tooltip,
                TrackYOffset = -5, // tuck the rail up under its own title
                ZeroString = zeroText,
                MaxString = maxText,
                ValueSuffix = suffix,
                ValueLane = valueLane,
            });
            rail.OnChange = s => onChange((int)s.AbsoluteValue);
        }

        // ⚠ FIVE rows, not seven. ColonyType has seven members, but Colony and TradeHub can hold
        // no plan at all: switching a colony to either WIPES its blueprints (GovernorDetails-
        // Component.OnColonyTypeChanged), and no template can even be saved as TradeHub - the
        // The five governor rows. Colony and TradeHub have none: the constructor folds TradeHub
        // into Colony, and Colony is what the editor writes for a plan with NO governor - such a
        // plan belongs in every list rather than in one of its own (see MakeBlueprintPolicyList).
        static readonly (Planet.ColonyType Type, GameText Label)[] BlueprintGovernors =
        {
            (Planet.ColonyType.Core,         GameText.Core),
            (Planet.ColonyType.Industrial,   GameText.Industrial),
            (Planet.ColonyType.Agricultural, GameText.Agricultural),
            (Planet.ColonyType.Research,     GameText.Research),
            (Planet.ColonyType.Military,     GameText.Military),
        };

        // One picker: the plans of THIS category, plus the empty position. The empty position is a
        // real value - a row left unset means an Auto colony of that type simply has no plan, so
        // the table can ship blank and change nothing.
        DropOptions<string> MakeBlueprintPolicyList(Empire player, Planet.ColonyType type)
        {
            var list = new DropOptions<string>(170, 18);
            list.AddOption(option: "--", "");
            // Ludoal fork (maintainer feedback): a plan of THIS governor's type, or a GENERIC one.
            // ⚠ the blueprint editor maps its "--" position to ColonyType.Colony, which is how a
            // plan says it has no governor: matching on the type alone would hide every generic
            // plan from every list. A plan without a governor is the universal candidate.
            // An exclusive plan is marked with the lock the game already uses for it - the picker
            // says which plans forbid anything outside their list, without opening each one.
            SubTexture lockIcon = ResourceManager.Texture("NewUI/icon_lock");
            foreach (BlueprintsTemplate t in ResourceManager.GetAllBlueprints())
                if (t.ColonyType == type || t.ColonyType == Planet.ColonyType.Colony)
                    list.AddOption(option: t.Name, t.Name, t.Exclusive ? lockIcon : null);

            string current = player.GetBlueprintPolicy(type);
            // a plan whose file was deleted or renamed outside the game: say so on the row rather
            // than snapping the picker back to blank, which would look like the player never set it.
            if (current.NotEmpty() && !ResourceManager.TryGetBlueprints(current, out _))
                list.AddOption(option: $"{current} ({Localizer.Token(GameText.BlueprintNotFound)})", current);

            list.ActiveValue = current;
            list.OnValueChange = name => OnBlueprintPolicyChanged(player, type, list, name);
            return list;
        }

        // Assigning an EXCLUSIVE plan is confirmed: it is the one value here that can make a
        // governor scrap what the plan does not name, and a table is exactly where that gets
        // clicked past without reading.
        void OnBlueprintPolicyChanged(Empire player, Planet.ColonyType type,
                                      DropOptions<string> list, string name)
        {
            string previous = player.GetBlueprintPolicy(type);
            if (name == previous)
                return;

            if (name.NotEmpty()
                && ResourceManager.TryGetBlueprints(name, out BlueprintsTemplate template)
                && template.Exclusive)
            {
                var confirm = new MessageBoxScreen(this, Localizer.Token(GameText.BlueprintExclusiveWarn));
                confirm.Accepted  = () => Universe.RunOnSimThread(() => player.SetBlueprintPolicy(type, name));
                confirm.Cancelled = () => list.ActiveValue = previous;
                ScreenManager.AddScreen(confirm);
                return;
            }

            Universe.RunOnSimThread(() => player.SetBlueprintPolicy(type, name));
        }

        // the categories the queue insertion knows, in display order (keys match SBProduction)
        static readonly (string Key, string Label)[] PriorityCategories =
        {
            ("Explorers",        "Explorers"),
            ("Colonizers",       "Colonizers"),
            ("Projectors",       "Projectors"),
            ("ResearchStations", "Research Stations"),
            ("MiningStations",   "Mining Stations"),
            ("Freighters",       "Freighters"),
            ("Troops",           "Troops"),
            ("MilitaryShips",    "Military Ships"),
        };

        static string LabelOf(string key)
        {
            foreach ((string k, string label) in PriorityCategories)
                if (k == key) return label;
            return key;
        }

        UIButton IconBtn(string normal, string hover, in Rectangle r, LocalizedText tip, Action onClick)
        {
            var b = new UIButton(new UIButton.StyleTextures(normal, hover, hover), Vector2.Zero, "")
            {
                Tooltip = tip,
                OnClick = _ => onClick(),
                ClickSfx = "sd_ui_accept_alt3",
            };
            b.Rect = r;
            return b;
        }

        void RebuildPriorityRows()
        {
            PriorityHost.RemoveAll();
            var prio = Universe.UState.P.ConstructionPriorities;
            int x = (int)PriorityHost.X, w = (int)PriorityHost.Width;
            int y = (int)PriorityHost.Y;
            const int RowH = 26, Icon = 20;

            void Section(string title, GameText tip = 0)
            {
                // the explainer rides the section label, not the tab title
                PriorityHost.Add(tip != 0
                    ? new UILabel(new Vector2(x, y + 3), title, Fonts.Arial12Bold, Colors.Cream, tip)
                    : new UILabel(new Vector2(x, y + 3), title, Fonts.Arial12Bold, Colors.Cream));
                y += RowH - 2;
            }
            UILabel Row(string key)
            {
                var l = new UILabel(new Vector2(x + 8, y + 4), LabelOf(key), Fonts.Arial12Bold, Color.White);
                PriorityHost.Add(l);
                return l;
            }
            Rectangle Slot(int fromRight) => new(x + w - fromRight, y + (RowH - Icon) / 2, Icon, Icon);

            Section("Prioritize", GameText.PrioritizationHeaderTip);
            for (int i = 0; i < prio.Count; i++)
            {
                string key = prio[i];
                Row(key);
                int idx = i;
                if (i > 0)
                    PriorityHost.Add(IconBtn("NewUI/icon_queue_arrow_up", "NewUI/icon_queue_arrow_up_hover1",
                                             Slot(72), "Higher priority", () => MoveCategory(idx, -1)));
                if (i < prio.Count - 1)
                    PriorityHost.Add(IconBtn("NewUI/icon_queue_arrow_down", "NewUI/icon_queue_arrow_down_hover1",
                                             Slot(48), "Lower priority", () => MoveCategory(idx, +1)));
                PriorityHost.Add(IconBtn("NewUI/icon_queue_delete", "NewUI/icon_queue_delete_hover1",
                                         Slot(24), "Stop prioritizing this category", () => DemoteCategory(key)));
                y += RowH;
            }
            if (prio.Count == 0)
            {
                PriorityHost.Add(new UILabel(new Vector2(x + 8, y + 4), "Nothing prioritized", Fonts.Arial12, Color.Gray));
                y += RowH;
            }

            y += 6;
            Section("Do not prioritize");
            foreach ((string key, string _) in PriorityCategories)
            {
                if (prio.Contains(key))
                    continue;
                Row(key);
                PriorityHost.Add(IconBtn("NewUI/icon_build_add", "NewUI/icon_build_add_hover1",
                                         Slot(24), "Prioritize this category", () => PromoteCategory(key)));
                y += RowH;
            }
        }

        void MoveCategory(int index, int delta)
        {
            var prio = Universe.UState.P.ConstructionPriorities;
            string key = prio[index];
            prio.RemoveAt(index);
            prio.Insert(index + delta, key);
            RebuildPriorityRows();
        }

        void DemoteCategory(string key)
        {
            Universe.UState.P.ConstructionPriorities.Remove(key);
            RebuildPriorityRows();
        }

        void PromoteCategory(string key)
        {
            Universe.UState.P.ConstructionPriorities.Add(key);
            RebuildPriorityRows();
        }

        // one category box: a one-tab frame bearing the category's name, with its rows inside
        UIList NewBox(in RectF r, LocalizedText title, LocalizedText tooltip) => NewBox(r, title, tooltip, out _);

        UIList NewBox(in RectF r, LocalizedText title, LocalizedText tooltip, out Submenu box)
        {
            box = Add(new Submenu(r, new[] { title }));
            // what the frame governs, carried by the tab itself: these boxes wear a single
            // tab, so the tab IS the frame's title and the only part of it left to hover.
            box.Tabs[0].Tooltip = tooltip;
            box.PerformLayout();
            UIList list = AddList(new Vector2(box.ClientArea.X + 12, box.ClientArea.Y + 12));
            list.Padding = new Vector2(2f, 10f);
            return list;
        }

        public override void Update(float fixedDeltaTime)
        {
            if (RushNewColonies != null)
            {
                // greyed AND inert while the empire-wide rush is on: it already rushes everything
                RushNewColonies.Greyed = RushConstruction;
                RushNewColonies.Enabled = !RushConstruction;
            }
            base.Update(fixedDeltaTime);
        }

        void OnEmpireTabChanged(int index)
            => ScreenGroups.SwitchEmpireTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // live top bar - the popup veil must not grey it
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            // H closes what H opened; right-click closes like every table screen of the group
            if ((input.PoliciesWindow && !GlobalStats.TakingInput) || input.RightMouseClick)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // live top bar
                return true;

            return base.HandleInput(input);
        }

        // ⚠ NOT a plain boolean: writing it marshals the switch onto the SIMULATION thread.
        bool RushConstruction
        {
            get => Universe.Player.RushAllConstruction;
            set
            {
                Universe.RunOnSimThread(() =>
                {
                    Universe.Player.RushAllConstruction = value;
                    Universe.Player.SwitchRushAllConstruction(value);
                });
            }
        }
    }
}
