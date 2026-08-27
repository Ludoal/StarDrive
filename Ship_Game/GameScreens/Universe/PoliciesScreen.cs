using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens;
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
    // No mute control: every order carries a notice saying what it governs. This is the page
    // where the player must understand what they are ordering, so the doctrine is on screen
    // rather than hidden behind a hover.
    public sealed class PoliciesScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;
        // this page's real frame is its tab row's rect - the band excludes exactly what the
        // page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;

        DropOptions<CargoPriority> FreighterPriorityDropDown;
        DropOptions<Planet.BuildMandate> EmpireBuildMandateList, EmpireScrapMandateList;
        UIPanel PriorityHost;

        // fixed box geometry - the boxes own their sizes, the columns just stack them.
        // Heights: one-tab strip (~24) + 12 top pad + 20 notice + 26 per row + 12 bottom pad.
        const float BoxW = 320f, BoxW2 = 450f, BoxW3 = 300f, BoxGap = 10f;
        const float EconomyBoxH = 94f, ResearchBoxH = 94f, ColonyBoxH = 172f, TradeBoxH = 146f;

        // The Prioritization rows live INSIDE the Construction frame, under its notice and its
        // Rush row. Both numbers are CONSTANTS and the frame is sized FROM them - never the
        // reverse: a host placed at a share of the space left moves every time a row is added
        // above it.
        const float PrioTopInset = 92f, PrioRowsH = 300f;
        const float ConstructionBoxH = PrioTopInset + PrioRowsH + 12f;

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
            float col1H = EconomyBoxH + BoxGap + ResearchBoxH + BoxGap + ColonyBoxH;
            float col2H = TradeBoxH;
            float contentW = 9 + 10 + BoxW + BoxGap + BoxW2 + BoxGap + BoxW3 + 10 + 9;  // ClientArea insets + gutters
            float contentH = 60 + Math.Max(Math.Max(col1H, col2H), ConstructionBoxH) + 22; // tab strip + cross clearance + pads
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), 6,
                                                    OnEmpireTabChanged, contentW, contentH);

            RectF client = EmpireTabs.ClientArea;
            float top = ScreenGroups.GroupContentTop(client);
            float x0 = client.X + 10, x1 = x0 + BoxW + BoxGap, x2 = x1 + BoxW2 + BoxGap;
            Empire player = Universe.Player;

            // ⚠ within a column the LOWER box is added FIRST: an open dropdown's list spills
            // below its own row, and add order is draw order - the spill must land on top
            // of the neighbour, not under it.

            UIList colony = NewBox(new RectF(x0, top + EconomyBoxH + BoxGap + ResearchBoxH + BoxGap, BoxW, ColonyBoxH), "Colony");
            Notice(colony, GameText.PolColonyNotice);
            // ⚠ "Auto Governor" decides whether a new colony gets an ASSESSED governor -
            // see Planet_Colonize.SetupColonyType.
            colony.AddCheckbox(() => player.AutoCoreGovernor, title: "Auto Governor", tooltip: GameText.AutoGovernorTip);
            // the empire's own mandates: what a colony left on Auto follows. Same picker as the
            // colony's, minus the Auto position - a policy has nowhere to defer to.
            colony.Add(new UILabel(GameText.BuildMandate, Fonts.Arial12Bold, Color.White) { Tooltip = GameText.BuildMandateTip });
            EmpireBuildMandateList = colony.Add(MandateDropdown.Make(player.EmpireBuildMandate,
                m => Universe.RunOnSimThread(() => player.EmpireBuildMandate = m), withAuto: false));
            colony.Add(new UILabel(GameText.ScrapMandate, Fonts.Arial12Bold, Color.White) { Tooltip = GameText.ScrapMandateTip });
            EmpireScrapMandateList = colony.Add(MandateDropdown.Make(player.EmpireScrapMandate,
                m => Universe.RunOnSimThread(() => player.EmpireScrapMandate = m), withAuto: false));
            colony.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList research = NewBox(new RectF(x0, top + EconomyBoxH + BoxGap, BoxW, ResearchBoxH), "Research");
            Notice(research, GameText.PolResearchNotice);
            research.AddCheckbox(() => player.AutoResearch, title: GameText.AutoResearch, tooltip: GameText.YourEmpireWillAutomaticallySelect);

            UIList economy = NewBox(new RectF(x0, top, BoxW, EconomyBoxH), "Economy");
            Notice(economy, GameText.PolEconomyNotice);
            economy.AddCheckbox(() => player.AutoTaxes, title: GameText.AutoTaxes, tooltip: GameText.YourEmpireWillAutomaticallyManage3);

            UIList trade = NewBox(new RectF(x1, top, BoxW2, TradeBoxH), "Trade");
            Notice(trade, GameText.PolTradeNotice);
            FreighterPriorityDropDown = trade.Add(new LabeledDropdown<CargoPriority>())
                .Create(GameText.FreighterPriority, GameText.FreighterPriorityTip);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityAuto, CargoPriority.Auto);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityProductionFirst, CargoPriority.ProductionFirst);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityColonistsFirst, CargoPriority.ColonistsFirst);
            FreighterPriorityDropDown.ActiveValue = player.CargoPriority;
            FreighterPriorityDropDown.OnValueChange = v => player.CargoPriority = v;
            // its own tooltip says out loud that this one is a GAME rule, not an empire order -
            // it is stored with the game setup, so it does not travel with the empire.
            trade.AddCheckbox(() => Universe.UState.P.AllowPlayerInterTrade,
                              title: GameText.AllowPlayerInterTradeTitle, tooltip: GameText.PolInterTradeGameRuleTip);
            trade.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList construction = NewBox(new RectF(x2, top, BoxW3, ConstructionBoxH), "Construction", out Submenu constructionBox);
            Notice(construction, GameText.PolConstructionNotice);
            // ⚠ NOT a plain checkbox: its setter marshals onto the SIMULATION thread. Copying it
            // as a bare boolean would look right and propagate nothing.
            construction.AddCheckbox(() => RushConstruction, title: GameText.RushAllConstruction, tooltip: GameText.RushAllConstructionTip);

            // the prioritized categories, their ORDER is the hierarchy; arrows reorder, the
            // inhibit glyph demotes, the plus promotes. Acts at queue INSERTION only
            // (SBProduction) - reordering never reshuffles queues already filled, and the
            // section's tooltip says so.
            // The host takes its geometry from the frame's own client area, the same source the
            // list rows use (+12) - never from a second sum over x2 and BoxW3. ClientArea is
            // already inset by 9 (the corner textures' size), so two arithmetics that have to
            // agree end up disagreeing: this one was 15px left of the rows above it.
            PriorityHost = Add(new UIPanel(new Rectangle((int)(constructionBox.ClientArea.X + 12),
                                                         (int)(top + PrioTopInset),
                                                         (int)(constructionBox.ClientArea.W - 24),
                                                         (int)PrioRowsH),
                                           new Color(0, 0, 0, 0)));
            RebuildPriorityRows();

            base.LoadContent();
        }

        // the one-line doctrine that heads a category - what this box governs, in the clear
        void Notice(UIList box, LocalizedText text) => box.Add(new UILabel(text, Fonts.Arial12, Colors.Cream));

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
        UIList NewBox(in RectF r, LocalizedText title) => NewBox(r, title, out _);

        UIList NewBox(in RectF r, LocalizedText title, out Submenu box)
        {
            box = Add(new Submenu(r, new[] { title }));
            box.PerformLayout();
            UIList list = AddList(new Vector2(box.ClientArea.X + 12, box.ClientArea.Y + 12));
            list.Padding = new Vector2(2f, 10f);
            return list;
        }

        void OnEmpireTabChanged(int index)
            => ScreenGroups.SwitchEmpireTab(index, self: 6, Universe, this);

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
                Universe.Player.RushAllConstruction = value;
                Universe.RunOnSimThread(() => Universe.Player.SwitchRushAllConstruction(value));
            }
        }
    }
}
