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
        // (maintainer feedback)
        private readonly Graphics.Font NameFont  = Fonts.Arial14Bold;
        private readonly Graphics.Font ClassFont = Fonts.Arial12;
        private readonly Graphics.Font SmallFont  = Fonts.Arial12Bold;
        private readonly Graphics.Font TinyFont   = Fonts.Arial8Bold;
        private readonly Color PlanetStatColor;
        private Color EmpireColor;   // recomputed on owner change (live refresh)
        private Empire LastSeenOwner;
        private int LastSeenBuildings;

        private Rectangle ShipIconRect;
        // (maintainer feedback) the two order buttons are a compact icon lane (Ships-list
        // style). ColonizeIcon: click to colonise, RED to cancel. TroopIcon: left = Send Troops,
        // right = cancel one inbound landing (Recall reuses it on our own worlds). Rects are laid
        // in PerformLayout and hit-tested in HandleInput - no UIButton, so the icon can go red.
        private Rectangle ColonizeIconRect;
        private Rectangle TroopIconRect;
        private bool ShowColonizeIcon;
        private bool ShowTroopIcon;
        UIPanel ColonizePanel;
        UIPanel TroopPanel;
        UILabel TroopsLabel;
        float LiveRefreshTimer;
        // the widest the "En route: N  Deployed: M" counter column ever gets, for auto-size
        public const string TroopsCounterMeasure = "En route: 99   Deployed: 99";
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
            LastSeenOwner     = planet.Owner;
            LastSeenBuildings = planet.NumBuildings;
            CanSendTroops   = canSendTroops;

            foreach (Goal g in planet.Universe.Player.AI.Goals)
            {
                if (g.IsColonizationGoal(planet))
                    MarkedForColonization = true;
            }
        }

        public override void PerformLayout()
        {
            int y = (int)Y;
            int h = (int)Height;
            RemoveAll();

            // two dedicated slots (Colonize left, Send Troops right), fixed whether the other
            // shows or not. Colonize on a colonisable unowned world; the troop icon whenever a
            // troop action is available (send, cancel an inbound landing, or recall from our own).
            UITable.Column[] cols = Screen.Table.Columns;
            Rectangle ordersCol = cols[9].Rect;
            ShipIconRect = new Rectangle(cols[1].Rect.X + UITable.PadX, y + h / 2 - 16, 32, 32);

            // the two slots are fixed and centred as a PAIR, so a row with only one icon keeps it
            // in its own column (Colonize left / Send Troops right) - the Ships-list convention
            const int IconSize = 22, IconGap = 8;
            int laneW = 2 * IconSize + IconGap;
            int slotX = ordersCol.X + (ordersCol.Width - laneW) / 2;
            int iy = y + h / 2 - IconSize / 2;
            ColonizeIconRect = new Rectangle(slotX, iy, IconSize, IconSize);
            TroopIconRect    = new Rectangle(slotX + IconSize + IconGap, iy, IconSize, IconSize);

            // the widgets are built once; RefreshLiveState owns visibility, tints, tooltips and
            // the counter - the sim moves troops and colonisation goals while the list is open.
            // (icon_troop_shipUI: the plain icon_troop washes out at this size)
            ColonizePanel = Panel(ColonizeIconRect, Color.White, ResourceManager.Texture("UI/ColonizeIcon"));
            // the _bright variant (bench 410): the stock art peaks at ~35 of luminance, so a
            // Red tint - the hostile-owner state - was near-black. Normalized to 230.
            TroopPanel    = Panel(TroopIconRect, Color.White, ResourceManager.Texture("UI/icon_troop_shipUI_bright"));
            TroopsLabel   = Label(UITable.CellPos(SmallFont, cols[10].Rect, Y, Height, "", TableAlign.Left),
                                  "", SmallFont, Cream);
            RefreshLiveState();

            AddSystemName();
            AddPlanetName();
            AddPlanetTextureAndStatus();
            AddPlanetStats();
            AddHostileWarning();
            base.PerformLayout();
        }

        // the sim keeps moving while the list is open - refresh the live cells on a small
        // throttle (a per-frame OwnedShips scan for every row would be too dear)
        public override void Update(float fixedDeltaTime)
        {
            LiveRefreshTimer -= fixedDeltaTime;
            if (LiveRefreshTimer <= 0f)
            {
                LiveRefreshTimer = 0.5f;
                RefreshLiveState();
            }
            base.Update(fixedDeltaTime);
        }

        void RefreshLiveState()
        {
            // the row's static cells (planet icon flag, owner colours, class) rebuild when the
            // owner changes - a colonisation completing must show without a re-sort
            if (Planet.Owner != LastSeenOwner || Planet.NumBuildings != LastSeenBuildings)
            {
                LastSeenOwner     = Planet.Owner;
                LastSeenBuildings = Planet.NumBuildings;
                EmpireColor       = Planet.Owner?.EmpireColor ?? new Color(255, 239, 208);
                PerformLayout(); // the layout re-runs this refresh; Features follow the buildings
                return;
            }
            // colonisation marks placed elsewhere (the cartouche button, an auto-colonize goal)
            // colour the icon too - the constructor snapshot went stale
            bool marked = false;
            foreach (Goal g in Player.AI.Goals)
                if (g.IsColonizationGoal(Planet)) { marked = true; break; }
            MarkedForColonization = marked;

            TryGetIncomingTroops(out int incoming, out _);
            bool canRecall = Planet.NumTroopsCanLaunchFor(Player) > 0;
            ShowColonizeIcon = Planet.Owner == null && Planet.Habitable;
            ShowTroopIcon = (Planet.Habitable && CanSendTroops && !Player.IsNAPactWith(Planet.Owner))
                          || incoming > 0 || canRecall;

            if (ColonizePanel != null)
            {
                ColonizePanel.Visible = ShowColonizeIcon;
                ColonizePanel.Color   = MarkedForColonization ? Color.Red : Color.White;
                ColonizePanel.Tooltip = MarkedForColonization ? GameText.CancelColonize : GameText.Colonize;
            }
            if (TroopPanel != null)
            {
                TroopPanel.Visible = ShowTroopIcon;
                TroopPanel.Color   = Planet.Owner != null && Planet.Owner != Player ? Color.Red : Color.White;
                TroopPanel.Tooltip = Planet.Owner == Player ? GameText.RecallAllTroopsBasedOn
                                                            : GameText.SendAvailableTroopsToThis;
            }
            if (TroopsLabel != null)
                TroopsLabel.Text = TroopsCounterText(incoming);
        }

        // "En route: N   Deployed: M" - N inbound (travelling, not yet landed), M garrisoned
        string TroopsCounterText(int incoming)
        {
            int deployed = Planet.CountEmpireTroops(Player);
            var parts = new Array<string>();
            if (incoming > 0) parts.Add($"En route: {incoming}");
            if (deployed > 0) parts.Add($"Deployed: {deployed}");
            return string.Join("   ", parts.ToArray());
        }

        public override bool HandleInput(InputState input)
        {
            if (ShowColonizeIcon && ColonizeIconRect.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                OnColonizeClicked(null);
                return true;
            }
            if (ShowTroopIcon && TroopIconRect.HitTest(input.CursorPosition))
            {
                if (input.RightMouseClick)
                {
                    OnSendTroopsRightClick();
                    return true;
                }
                if (input.LeftMouseClick)
                {
                    // our own world: the icon RECALLS; anyone else's (or unowned): it SENDS
                    if (Planet.Owner == Player) OnRecallTroopsClicked(null);
                    else                        OnSendTroopsClicked(null);
                    return true;
                }
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
            // plain regular, uncoloured and without the richness - it has its own column.
            // The environment multiplier stays on the class line (maintainer feedback).
            var namePos = new Vector2(ShipIconRect.Right + 8, Y + Height / 2 - (NameFont.LineSpacing + ClassFont.LineSpacing + 2) / 2);
            Label(namePos, Planet.Name, NameFont, EmpireColor);
            namePos.Y += NameFont.LineSpacing + 2;
            // class WITH its richness word ("Terran Ultra Rich") - only the numeric
            // values left for their own column; the mineable variant appends " (8.2)",
            // stripped here too
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
            // an unowned habitable world shows NOTHING for owner (maintainer feedback:
            // "None" read like a race); an uninhabitable one keeps its Impossible
            string owner = Planet.Owner != null ? Planet.Owner.data.Traits.Singular
                         : Planet.Habitable ? "" : Localizer.Token(GameText.Impossible);

            UITable.Column[] cols = Screen.Table.Columns;
            void Cell(int col, string text, Color color)
                => Label(UITable.CellPos(SmallFont, cols[col].Rect, Y, Height, text, cols[col].Align), text, SmallFont, color);

            // pop grows, terraforming and mining move fertility/richness - these four cells
            // are LIVE labels, right-aligned in their box (a Func label re-measures itself,
            // so CellPos' width math cannot place it)
            UILabel DynCell(int col, System.Func<UILabel, string> getText)
            {
                var l = new UILabel(getText, SmallFont) { Color = PlanetStatColor, TextAlign = TextAlign.Right };
                Rectangle r = cols[col].Rect;
                l.Pos  = new Vector2(r.X, (int)(Y + Height / 2f - SmallFont.LineSpacing / 2f));
                l.Size = new Vector2(r.Width - UITable.PadX, SmallFont.LineSpacing);
                Add(l);
                return l;
            }

            AddFeatures(cols[2].Rect);
            // Distance is measured from the nearest player colony, so a colony of your own is
            // its own reference point: the scale has nothing to say about it. A dash reads as
            // "not applicable", where a blank cell read as a bug (maintainer feedback).
            if (Planet.Owner == Player)
            {
                Cell(3, "---", Color.Gray);
            }
            else
            {
                DistanceDisplay dd = new DistanceDisplay(Distance);
                Cell(3, dd.Text, dd.Color);
            }
            // fixed one decimal: right-aligned + constant fraction = aligned on the point
            DynCell(4, l => Planet.FertilityFor(Player).ToString("0.0", CultureInfo.InvariantCulture));
            DynCell(5, l => Planet.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture));
            // Max Pop splits: the figure in its column, the percentage in Fill's.
            // ⚠ the percentage is computed against the player's own max - the string's
            // built-in ratio divides by the BASE max, so a racial max-pop bonus reads over
            // 100%. Clamped: a brief overshoot is the sim's business, not the table's.
            DynCell(6, l =>
            {
                string ps = Planet.PopulationStringForPlayer;
                int paren = ps.IndexOf(" (");
                return paren < 0 ? ps : ps.Substring(0, paren);
            });
            DynCell(7, l =>
            {
                float maxPop = Planet.MaxPopulationBillionFor(Player);
                return maxPop > 0f && Planet.PopulationBillion > 0f
                     ? (Planet.PopulationBillion / maxPop * 100f).UpperBound(100f).String() + "%" : "";
            });
            Cell(8, owner, EmpireColor);
        }

        // the Features column: the terrain/event buildings grouped by icon, each with
        // its count - "{Rock Field}(3) {Dormant Volcano}(2) {Volcano}"
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
        // sit on a line, the rest wrap to a second one (maintainer feedback)
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
            // 16px icons vertically centred in the cell, level with the Features column's icons
            var statusIcons = new Vector2(nameCol.Right, (int)Y + (int)Height / 2 - 8);
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
            // bench 393: the troop icon's visibility is decided in PerformLayout now - re-lay the
            // row so it follows the new free-troop count at once (every row shares the count).
            PerformLayout();
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
                PerformLayout(); // bench 393: recolour/re-show the troop icon
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
            // Ludoal fork: cancelling an inbound landing frees its troops again, and every row's
            // Send Troops button hides itself on CanSendTroops - refreshing this row alone would
            // leave the others hiding on a stale count.
            Screen.RefreshSendTroopButtonsVisibility();
            PerformLayout(); // bench 393
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
                MarkedForColonization = true;
                PerformLayout(); // bench 393: the colonize icon goes red (cancel)
                return;
            }

            Planet.Universe.Player.AI.CancelColonization(Planet);
            MarkedForColonization = false;
            PerformLayout(); // bench 393: back to white (order)
        }
    }
}
