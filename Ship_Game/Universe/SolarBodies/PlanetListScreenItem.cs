using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Audio;
using Ship_Game.Commands.Goals;
using Ship_Game.Ships;
using System.Linq;
using System.Globalization;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Graphics;
using Ship_Game.Universe.SolarBodies;
using Ship_Game.UI; // UITable: the shared table charte

namespace Ship_Game
{
    public sealed class PlanetListScreenItem : ScrollListItem<PlanetListScreenItem> // Moved to UI V2
    {
        public readonly Planet Planet;

        Empire Player => Planet.Universe.Player;
        private readonly Color Cream = Colors.Cream;
        // the NAME a step larger than the body text, its class a plain regular
        // (maintainer, 4 Aug - down from the old Arial20)
        private readonly Graphics.Font NameFont  = Fonts.Arial14Bold;
        private readonly Graphics.Font ClassFont = Fonts.Arial12;
        private readonly Graphics.Font SmallFont  = Fonts.Arial12Bold;
        private readonly Graphics.Font TinyFont   = Fonts.Arial8Bold;
        private readonly Color PlanetStatColor;
        private readonly Color EmpireColor;

        private Rectangle ShipIconRect;
        private UIButton Colonize;
        private UIButton SendTroops;
        private UIButton RecallTroops;
        private readonly PlanetListScreen Screen;
        private readonly float Distance;
        private bool MarkedForColonization;
        public bool CanSendTroops;

        public PlanetListScreenItem(PlanetListScreen screen, Planet planet, float distance, bool canSendTroops)
        {
            Screen   = screen;
            Planet   = planet;
            Distance = distance / 1000; // Distance from nearest player colony

            PlanetStatColor = Planet.Habitable ? Color.White : Color.LightPink;
            EmpireColor     = Planet.Owner?.EmpireColor ?? new Color(255, 239, 208);
            CanSendTroops   = canSendTroops;

            foreach (Goal g in planet.Universe.Player.AI.Goals)
            {
                if (g.IsColonizationGoal(planet))
                    MarkedForColonization = true;
            }
        }

        public override void PerformLayout()
        {
            int x = (int)X;
            int y = (int)Y;
            int w = (int)Width;
            int h = (int)Height;
            RemoveAll();

            // Ludoal fork: the NewUI dan_button family. Blue to ORDER a colonisation, red to cancel
            // one, plain for Send and Recall Troops - the convention reads action / undo / neutral.
            LocalizedText colonizeText = !MarkedForColonization ? GameText.Colonize : GameText.CancelColonize;
            ButtonStyle colonizeStyle  = MarkedForColonization ? ButtonStyle.WideHostile
                                                               : ButtonStyle.WideActive;
            Colonize   = Button(colonizeStyle, colonizeText, OnColonizeClicked);
            SendTroops = Button(ButtonStyle.Wide, "Send Troops", OnSendTroopsClicked);
            SendTroops.Tooltip = GameText.SendAvailableTroopsToThis;
            RecallTroops = Button(ButtonStyle.Wide, $"Recall Troops ({Planet.NumTroopsCanLaunchFor(Player)})", OnRecallTroopsClicked);
            RecallTroops.Tooltip = GameText.RecallAllTroopsBasedOn;

            // cells read the shared column geometry; the two button slots are sized from
            // their own texts (Colonize from its Cancel Colonize toggle - maintainer, 4 Aug).
            // Colonize and Recall Troops share the first slot: an unowned planet has no
            // troops of yours to recall, so the two never come up together.
            UITable.Column[] cols = Screen.Table.Columns;
            Rectangle ordersCol = cols[9].Rect;
            ShipIconRect = new Rectangle(cols[1].Rect.X + UITable.PadX, y + h / 2 - 16, 32, 32);

            int btnW = Screen.OrdersSlotW, btnH = 24;
            int slot1X = ordersCol.X + UITable.PadX;
            int rowY   = y + h / 2 - btnH / 2;
            Colonize.Rect      = new Rectangle(slot1X, rowY, btnW, btnH);
            RecallTroops.Rect  = new RectF(slot1X, rowY, btnW, btnH);
            SendTroops.Rect    = new RectF(slot1X + btnW + 6, rowY, btnW, btnH);

            Colonize.Visible     = Planet.Owner == null && Planet.Habitable;
            RecallTroops.Visible = Planet.Owner != Player && Planet.NumTroopsCanLaunchFor(Player) > 0;
            
            UpdateButtonSendTroops();
            AddSystemName();
            AddPlanetName();
            AddPlanetTextureAndStatus();
            AddPlanetStats();
            AddHostileWarning();
            base.PerformLayout();
        }

        public override bool HandleInput(InputState input)
        {
            if (SendTroops.HitTest(input.CursorPosition) && input.RightMouseClick)
            {
                OnSendTroopsRightClick();
                return true;
            }

            return base.HandleInput(input);
        }

        void AddSystemName()
        {
            UITable.Column c = Screen.Table.Columns[0];
            Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, Planet.System.Name, c.Align),
                  Planet.System.Name, SmallFont, Cream);
        }

        void AddPlanetName()
        {
            // two lines: the NAME a step larger, owner-coloured; the CLASS under it in
            // plain regular, uncoloured and without the richness - it has its own column
            // now. The environment multiplier stays on the class line (maintainer, 4 Aug).
            var namePos = new Vector2(ShipIconRect.Right + 8, Y + Height / 2 - (NameFont.LineSpacing + ClassFont.LineSpacing + 2) / 2);
            Label(namePos, Planet.Name, NameFont, EmpireColor);
            namePos.Y += NameFont.LineSpacing + 2;
            // class WITH its richness word ("Terran Ultra Rich") - only the numeric
            // values left for their own column; the mineable variant appends " (8.2)",
            // stripped here too (maintainer, 4 Aug)
            string category = Planet.LocalizedRichness;
            int par = category.IndexOf(" (");
            if (par >= 0) category = category.Substring(0, par);
            Label(namePos, category, ClassFont, Color.Gray);

            float fertEnvMultiplier = Player.PlayerEnvModifier(Planet.Category);
            if (!fertEnvMultiplier.AlmostEqual(1))
            {
                Color fertEnvColor       = fertEnvMultiplier.Less(1) ? Color.Pink : Color.LightGreen;
                string multiplierString  = $" (x {fertEnvMultiplier.String(2)})";
                var fertEnvMultiplierPos = new Vector2(namePos.X + ClassFont.MeasureString(category).X + 5, namePos.Y + 2);
                Label(fertEnvMultiplierPos, multiplierString, TinyFont, fertEnvColor);
            }
        }

        void AddPlanetStats()
        {
            // an unowned habitable world shows NOTHING for owner (maintainer bench 291:
            // "None" read like a race); an uninhabitable one keeps its Impossible
            string owner = Planet.Owner != null ? Planet.Owner.data.Traits.Singular
                         : Planet.Habitable ? "" : Localizer.Token(GameText.Impossible);

            UITable.Column[] cols = Screen.Table.Columns;
            void Cell(int col, string text, Color color)
                => Label(UITable.CellPos(SmallFont, cols[col].Rect, Y, Height, text, cols[col].Align), text, SmallFont, color);

            AddFeatures(cols[2].Rect);
            DistanceDisplay dd = new DistanceDisplay(Distance);
            if (Distance.Greater(0))
                Cell(3, dd.Text, dd.Color);
            // fixed one decimal: right-aligned + constant fraction = aligned on the point
            Cell(4, Planet.FertilityFor(Player).ToString("0.0", CultureInfo.InvariantCulture), PlanetStatColor);
            Cell(5, Planet.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture), PlanetStatColor);
            // Max Pop splits: the figure in its column, the percentage in Fill's.
            // ⚠ the percentage is computed HERE, against the player's own max - the string's
            // built-in ratio divides by the BASE max, so a racial max-pop bonus read as
            // "100.4%" (Lek's review, bench 305). Clamped: a brief overshoot is the sim's
            // business, not the table's.
            string popString = Planet.PopulationStringForPlayer;
            int paren = popString.IndexOf(" (");
            string popMain = paren < 0 ? popString : popString.Substring(0, paren);
            float maxPop = Planet.MaxPopulationBillionFor(Player);
            string ratio = maxPop > 0f && Planet.PopulationBillion > 0f
                         ? (Planet.PopulationBillion / maxPop * 100f).UpperBound(100f).String() + "%" : "";
            Cell(6, popMain, PlanetStatColor);
            Cell(7, ratio, PlanetStatColor);
            Cell(8, owner, EmpireColor);
        }

        // the Features column (maintainer, 4 Aug): the terrain/event buildings grouped by
        // icon, each with its count - "{Rock Field}(3) {Dormant Volcano}(2) {Volcano}"
        public static Array<(Building b, int n)> FeatureGroups(Planet p)
        {
            var groups = new Array<(Building b, int n)>();
            foreach (Building b in p.Buildings)
            {
                if (!b.ShowOnPlanetList && !(b.EventHere && (p.Owner == null || !p.Owner.IsBuildingUnlocked(b.Name))))
                    continue;
                int at = groups.FirstIndexOf(g => g.b.Icon == b.Icon);
                if (at >= 0) groups[at] = (groups[at].b, groups[at].n + 1);
                else         groups.Add((b, 1));
            }
            return groups;
        }

        // a 20px icon OCCUPIES the width of three characters - the same footprint the
        // column measures itself on and the renderer advances by; at most SIX features
        // sit on a line, the rest wrap to a second one (maintainer bench 294)
        const int FeaturesPerLine = 6;
        static int IconFootprint => (int)Fonts.Arial12Bold.TextWidth("000");

        // the synthetic string the Features column measures itself on - the first
        // line's worth of groups only, since the rest wraps
        public static string FeaturesMeasure(Planet p)
        {
            string s = "";
            int i = 0;
            foreach ((Building b, int n) in FeatureGroups(p))
            {
                if (i++ >= FeaturesPerLine)
                    break;
                s += "000" + (n > 1 ? $"({n})" : "") + " ";
            }
            return s;
        }

        void AddFeatures(in Rectangle col)
        {
            var groups = FeatureGroups(Planet);
            bool twoLines = groups.Count > FeaturesPerLine;
            int fx = col.X + UITable.PadX;
            int fy = (int)Y + (int)Height / 2 - (twoLines ? 21 : 10);
            int onLine = 0;
            foreach ((Building b, int n) in groups)
            {
                if (onLine++ == FeaturesPerLine)
                {
                    fx = col.X + UITable.PadX;
                    fy += 22;
                    onLine = 1;
                }
                var iconRect = new Rectangle(fx, fy, 20, 20);
                UIPanel icon = Panel(iconRect, ResourceManager.Texture($"Buildings/icon_{b.Icon}_48x48"));
                icon.Tooltip = $"{b.TranslatedName.Text}:\n{b.DescriptionText.Text}";
                fx += IconFootprint;
                if (n > 1)
                {
                    string count = $"({n})";
                    Label(new Vector2(fx - 4, fy + 4), count, TinyFont, Cream);
                    fx += (int)Fonts.Arial12Bold.TextWidth(count) - 4;
                }
                fx += (int)Fonts.Arial12Bold.TextWidth(" ");
            }
        }

        void AddHostileWarning()
        {
            if (Player.KnownEnemyStrengthIn(Planet.System) > 0)
            {
                Rectangle c0 = Screen.Table.Columns[0].Rect;
                SubTexture flash = ResourceManager.Texture("Ground_UI/EnemyHere");
                UIPanel enemyHere = Panel(c0.Right - 22, (int)Y + 5, flash);
                enemyHere.Tooltip = GameText.IndicatesThatHostileForcesWere;
            }
        }

        void AddPlanetTextureAndStatus()
        {
            var planetIcon = ShipIconRect;
            Add( new UIPanel(planetIcon, ResourceManager.Texture(Planet.IconPath))
            {
                Tooltip = GameText.PlanetTypeAndRichnessThe
            });

            if (Planet.Owner != null)
                Panel(planetIcon, EmpireColor, ResourceManager.Flag(Planet.Owner));

            AddPlanetStatusIcons(planetIcon);
        }

        void AddPlanetStatusIcons(Rectangle planetIcon)
        {
            Rectangle nameCol = Screen.Table.Columns[1].Rect;
            var statusIcons = new Vector2(nameCol.Right, planetIcon.Y);
            int xOffset = 0;
            int numIcons = 0;

            AddRecentCombat(statusIcons, ref xOffset, ref numIcons);
            AddTroopsIcon(statusIcons, ref xOffset);
            AddMoleIcons(statusIcons, ref xOffset, ref numIcons);
        }

        void AddRecentCombat(Vector2 statusIcons, ref int offset, ref int numIcons)
        {
            if (!Planet.RecentCombat) 
                return;

            offset += 18;
            numIcons += 1;
            var statusRect = new Rectangle((int)statusIcons.X - offset, (int)statusIcons.Y, 16, 16);
            UIPanel status = Panel(statusRect, ResourceManager.Texture("UI/icon_fighting_small"));
            status.Tooltip = GameText.IndicatesThatGroundCombatIs;
        }

        void AddMoleIcons(Vector2 statusIcons, ref int offset, ref int numIcons) // Haha, moles..
        {
            if (Player.data.MoleList.Count <= 0) 
                return;

            foreach (Mole m in Player.data.MoleList)
            {
                if (m.PlanetId == Planet.Id)
                {
                    offset += 18;
                    numIcons += 1;
                    var spyRect = new Rectangle((int)statusIcons.X - offset, (int)statusIcons.Y, 16, 16);
                    UIPanel spy = Panel(spyRect, ResourceManager.Texture("UI/icon_spy_small"));
                    spy.Tooltip = GameText.IndicatesThatAFriendlyAgent;
                    break;
                }
            }
        }

        void AddTroopsIcon(Vector2 statusIcons, ref int offset)
        {
            int troops = Planet.CountEmpireTroops(Player);
            if (troops > 0)
            {
                offset += 18;
                var troopRect = new Rectangle((int)statusIcons.X - offset, (int)statusIcons.Y, 16, 16);
                UIPanel troop = Panel(troopRect, ResourceManager.Texture("UI/icon_troop"));
                troop.Tooltip = LocalizedText.Parse($"{{Troops}}: {troops}");
            }
        }

        void UpdateButtonSendTroops()
        {
            if (TryGetIncomingTroops(out int troopsInvading, out _))
            {
                // red on a hostile target, plain otherwise - the convention of the new look
                ButtonStyle style  = Planet.Owner == Player || Planet.Owner == null
                                   ? ButtonStyle.Wide : ButtonStyle.WideHostile;
                string text        = "Invading:";

                if (Planet.Owner == Player)    text = "Rebasing:";
                else if (Planet.Owner == null) text = "Landing:";

                SendTroops.Text = $"{text} {troopsInvading}";
                SendTroops.Style = style;
                // ⚠ visible even with no free troop left: right-clicking this button is the only
                // way to cancel the landing, and a hidden button takes no click. Sending your last
                // troop would otherwise be irreversible.
                SendTroops.Visible = true;
            }
            else
            {
                SendTroops.Text    = "Send Troops";
                // ⚠ Ludoal fork: hiding this button when no troop is free is right - there is
                // nothing to send. But right-clicking it is how an inbound landing is CANCELLED,
                // and the branch above (troops on their way) is the one that keeps it visible.
                // Do not extend the hiding to that branch: a hidden button takes no click, so the
                // last free troop sent would be impossible to recall.
                SendTroops.Visible = Planet.Habitable && CanSendTroops && !Player.IsNAPactWith(Planet.Owner);
                SendTroops.Style   = Planet.Owner == Player || Planet.Owner == null
                                   ? ButtonStyle.Wide : ButtonStyle.WideHostile;
            }
        }

        bool TryGetIncomingTroops(out int incomingTroops, out Array<Ship> incomingTroopShips)
        {
            incomingTroopShips = new Array<Ship>();
            incomingTroops     = 0;
            var ships = Player.OwnedShips;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship ship = ships[i];
                ShipAI ai = ship?.AI;
                if (ai == null || ai.State == AIState.Resupply || !ship.HasOurTroops || ai.OrderQueue.IsEmpty)
                    continue;

                if (ai.OrderQueue.Any(goal => goal.TargetPlanet != null
                                              && goal.TargetPlanet == Planet
                                              && (goal.Plan == ShipAI.Plan.LandTroop || goal.Plan == ShipAI.Plan.Rebase)))
                {
                    incomingTroopShips.AddUnique(ship);
                    incomingTroops += ship.TroopCount;
                }
            }

            return incomingTroopShips.Count > 0;
        }

        public void SetCanSendTroops(bool value)
        {
            CanSendTroops = value;
            // Ludoal fork: the flag alone changed nothing on screen - Visible is only recomputed in
            // UpdateButtonSendTroops, so a row that had hidden its button kept it hidden until
            // something else redrew it. Refresh here and every row follows the new count at once.
            // Guarded: this can be called before the row has laid its buttons out.
            if (SendTroops != null)
                UpdateButtonSendTroops();
        }

        void OnSendTroopsClicked(UIButton b)
        {
            if (Player.GetTroopShipForRebase(out Ship troopShip, Planet.Position, Planet.Name))
            {
                if (Player.InvasionBlockedNotEnoughWarmup(Planet.Owner))
                {
                    ToolTip.CreateFloatingText(GameText.InvasionblockedWarmup, "", Screen.Input.CursorPosition, 3);
                    GameAudio.NegativeClick();
                    return;
                }

                GameAudio.EchoAffirmative();
                troopShip.AI.OrderLandAllTroops(Planet, clearOrders: true);
                Screen.RefreshSendTroopButtonsVisibility();
                Player.Universe.Objects.UpdateLists();
                UpdateButtonSendTroops();
            }
            else
            {
                GameAudio.NegativeClick();
            }
        }

        void OnSendTroopsRightClick() // cancel one incoming troop
        {
            if (!TryGetIncomingTroops(out _, out Array<Ship> incomingTroopShips))
                return;

            Ship ship = incomingTroopShips.Last();
            ship.AI.OrderRebaseToNearest();
            // Ludoal fork: the free-troop count has to be recomputed too. Cancelling an inbound
            // landing frees its troops again, and every row's Send Troops button hides itself on
            // CanSendTroops - refreshing this row alone left the others hiding on a stale count,
            // so one right-click made the whole column of buttons vanish.
            Screen.RefreshSendTroopButtonsVisibility();
            UpdateButtonSendTroops();
        }

        void OnRecallTroopsClicked(UIButton b)
        {
            bool troopLaunched = false;
            foreach (Troop t in Planet.Troops.GetLaunchableTroops(Player))
            {
                Ship troopTransport = t.Launch();
                if (troopTransport != null)
                {
                    troopLaunched = true;
                    troopTransport.AI.OrderRebaseToNearest();
                }
            }

            if (troopLaunched)
            {
                GameAudio.EchoAffirmative();
                PerformLayout();
            }
            else
            {
                GameAudio.NegativeClick();
            }
        }

        void OnColonizeClicked(UIButton b)
        {
            GameAudio.EchoAffirmative();
            if (!MarkedForColonization)
            {
                Player.AI.AddGoalAndEvaluate(new MarkForColonization(Planet, Planet.Universe.Player, isManual:true));
                Colonize.Text = "Cancel Colonize";
                Colonize.Style = ButtonStyle.WideHostile; // red once it undoes something
                MarkedForColonization = true;
                return;
            }

            Planet.Universe.Player.AI.CancelColonization(Planet);
            MarkedForColonization = false;
            Colonize.Text  = "Colonize";
            Colonize.Style = ButtonStyle.WideActive;
        }
    }
}
