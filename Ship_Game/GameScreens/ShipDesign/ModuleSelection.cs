using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.Text;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.UI;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public class ModuleSelection : Submenu
    {
        readonly ShipDesignScreen Screen;
        UniverseState Universe => Screen.ParentUniverse.UState;
        Empire Player => Screen.Player;

        readonly FighterScrollList ChooseFighterSL;
        readonly SubmenuScrollList<FighterListItem> ChooseFighterSub;
        readonly ModuleSelectScrollList ModuleSelectList;
        readonly Submenu ActiveModSubMenu;
        // Ludoal fork: was the Shift-click comparison panel; spec v4 folded its delta lanes into
        // the Active frame, so it is kept only to hold its slot — the hover frame takes it next.
        readonly Submenu CompareModSubMenu;
        readonly TexturedButton Obsolete;

        public ModuleSelection(ShipDesignScreen screen, LocalPos pos, Vector2 size)
            : base(pos, size, new LocalizedText[]{ "Wpn", "Pwr", "Def", "Spc" })
        {
            Screen = screen;
            // rounded black background
            SetBackground(Colors.TransparentBlackFill);
            base.PerformLayout(); // necessary

            ModuleSelectList = base.Add(new ModuleSelectScrollList(this, Screen));

            // Ludoal fork: the Active panel carries the delta lanes now (spec v4) — it is the
            // only stat frame left, so it needs the width the Compared one used to have.
            RectF acsub = new(Rect.X, Rect.Bottom + 15, 305 + 105, Screen.Height - (Rect.Y + Rect.Height) - 100);
            ActiveModSubMenu = base.Add(new Submenu(acsub, "Active Module"));
            // rounded black background
            ActiveModSubMenu.SetBackground(Colors.TransparentBlackFill);

            // obsolete button
            int obsoleteW = ResourceManager.Texture("NewUI/icon_queue_delete").Width;
            int obsoleteH = ResourceManager.Texture("NewUI/icon_queue_delete").Height;
            var obsoletePos = new RectF(ActiveModSubMenu.X + ActiveModSubMenu.Width - obsoleteW - 10, ActiveModSubMenu.Y + 38, obsoleteW, obsoleteH);
            Obsolete = new(obsoletePos, "NewUI/icon_queue_delete", "NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2");
            Obsolete.Tooltip = GameText.MarkThisModuleAsObsolete;
            
            RectF fighterR = acsub.Move(acsub.W + 20, 0);

            // Ludoal fork: comparison panel (Shift-click in the module list). Same slot as
            // Choose Fighter (the fighter list wins it when a hangar is selected), widened
            // by 105px: its right column shifts right so each column owns a delta lane.
            RectF compareR = new(acsub.X + acsub.W + 20, acsub.Y, acsub.W + 105, acsub.H);
            CompareModSubMenu = base.Add(new Submenu(compareR, "Compared Module"));
            CompareModSubMenu.SetBackground(Colors.TransparentBlackFill);
            ChooseFighterSub = base.Add(new SubmenuScrollList<FighterListItem>(fighterR, "Choose Fighter"));
            ChooseFighterSub.SetBackground(Colors.TransparentBlackFill);
            
            ChooseFighterSL = ChooseFighterSub.Add(new FighterScrollList(ChooseFighterSub, Screen)
            {
                EnableItemHighlight = true
            });
        }

        protected override void OnTabChangedEvt(int newIndex)
        {
            ModuleSelectList.SetActiveCategory(newIndex);
            base.OnTabChangedEvt(newIndex);
        }

        public void ResetActiveCategory()
        {
            ModuleSelectList.SetActiveCategory(SelectedIndex);
        }

        float ActiveModStatSpacing => ActiveModSubMenu.Width * 0.27f;

        // Ludoal fork (spec v4): a comparison is running when a module is pinned and the Active
        // panel is up. There is no second frame any more — this is what used to be
        // CompareModSubMenu.Visible, and every test that asked for that frame really meant this.
        bool Comparing => Screen.CompareModule != null && ActiveModSubMenu.Visible;

        // ===== Ludoal fork: comparator v2 =====
        // Stats start at a fixed offset from the panel top so both panels align.
        const float StatsStartRel = 195f;

        // A stat row captured by collect-mode instead of being drawn. The existing
        // Draw* stat methods stay the single source of stat expressions; with
        // Collector set they append here (and only advance the cursor) instead of
        // drawing, so the comparison view can render an aligned union of two runs.
        class CollectedStat
        {
            public string Key;      // label text — union identity
            public string Title;
            public float Value;
            public LocalizedText Tip;
            public bool IsPercent;
            public int Tint;        // 0 = default good/bad, 1 = custom title color, 2 = bad-percent-lower-than-1
            public Color CustomColor;
            public int Column;      // 0 = left, 1 = right (the +152f jump)
        }

        List<CollectedStat> Collector;
        const float CollectColSplitX = 76f; // collect runs from X=0; the col2 jump is +152f

        // Stats where a SMALLER value is the better one (delta coloring).
        // Keys are the on-screen labels (English — the game's own stat labels).
        static readonly HashSet<string> LowerIsBetter = new HashSet<string>
        {
            "Cost", "Mass", "Delay", "Ord / Shot", "Pwr / Shot", "Complexity",
            "Imprecision", "Spawn Timer", "Ignition"
        };

        bool CollectStat(in Vector2 cursor, in LocalizedText label, float value, LocalizedText tip,
                         bool isPercent, int tint, Color custom)
        {
            if (Collector == null)
                return false;
            Collector.Add(new CollectedStat
            {
                Key = label.Text, Title = label.Text, Value = value, Tip = tip,
                IsPercent = isPercent, Tint = tint, CustomColor = custom,
                Column = cursor.X > CollectColSplitX ? 1 : 0
            });
            return true;
        }

        List<CollectedStat> CollectStats(ShipModule mod)
        {
            var list = new List<CollectedStat>();
            Collector = list;
            var cursor = new Vector2(0f, 0f);
            float strength = mod.CalculateModuleOffenseDefense(Screen.CurrentHull.SurfaceArea, forceRecalculate: mod.IsFighterHangar);
            DrawStat(ref cursor, "Offense", strength, GameText.TT_ShipOffense);
            if (mod.BombType == null && !mod.IsWeapon || mod.InstalledWeapon == null)
                DrawModuleStats(null, mod, cursor, 0f);
            else
                DrawWeaponStats(null, cursor, mod, mod.InstalledWeapon, 0f);
            Collector = null;
            return list;
        }

        // Draw both panels' stat areas as ONE union per column: same rows, same
        // heights — absent stats show a dimmed dash, the Compared panel appends
        // a colored delta after each shared value.
        void DrawComparisonStats(SpriteBatch batch)
        {
            ShipModule a = Screen.ActiveModule ?? Screen.HighlightedModule;
            ShipModule b = Screen.CompareModule;
            if (a == null || b == null)
                return;

            List<CollectedStat> ra = CollectStats(a);
            List<CollectedStat> rb = CollectStats(b);
            var aByKey = new Map<string, CollectedStat>();
            var bByKey = new Map<string, CollectedStat>();
            foreach (CollectedStat r in ra) if (!aByKey.ContainsKey(r.Key)) aByKey.Add(r.Key, r);
            foreach (CollectedStat r in rb) if (!bByKey.ContainsKey(r.Key)) bByKey.Add(r.Key, r);

            for (int col = 0; col < 2; ++col)
            {
                var union = new List<CollectedStat>();
                var seen = new HashSet<string>();
                foreach (CollectedStat r in ra) if (r.Column == col && seen.Add(r.Key)) union.Add(r);
                foreach (CollectedStat r in rb) if (r.Column == col && seen.Add(r.Key)) union.Add(r);
                if (union.Count == 0)
                    continue;
                // spec v4: ONE frame. The values shown are the ACTIVE module's; the pinned one
                // never shows its own numbers, it only sets the delta (the player who wants them
                // in clear hovers the list). Delta sign therefore reads "active vs compared":
                // a green + means the module on the workbench is the better one.
                DrawStatColumn(batch, union, aByKey, bByKey, col, ActiveModSubMenu);
            }
        }

        void DrawStatColumn(SpriteBatch batch, List<CollectedStat> union,
                            Map<string, CollectedStat> own, Map<string, CollectedStat> other,
                            int col, Submenu panel)
        {
            Graphics.Font font = Fonts.Arial12Bold;
            float spacing = ActiveModStatSpacing;
            var dim = new Color(105, 105, 105);
            // With a delta lane in play, the right column is pushed further out so column 1's
            // deltas never run into column 2's labels.
            float colStep = other != null ? 210f : 152f;
            var cursor = new Vector2(panel.X + 10 + col * colStep, panel.Y + StatsStartRel);

            foreach (CollectedStat u in union)
            {
                if (own.TryGetValue(u.Key, out CollectedStat r))
                {
                    if (r.Tint == 2)
                        Screen.DrawStatBadPercentLower1(ref cursor, r.Title, r.Value, Color.White, r.Tip, spacing);
                    else
                        Screen.DrawStat(ref cursor, r.Title, r.Value,
                                        r.Tint == 1 ? r.CustomColor : Color.White,
                                        r.Tip, spacing: spacing, isPercent: r.IsPercent);

                    // delta vs the Active module, colored by "which direction is better"
                    if (other != null && other.TryGetValue(u.Key, out CollectedStat o) && !r.Value.AlmostEqual(o.Value))
                    {
                        float dv = r.Value - o.Value;
                        bool better = LowerIsBetter.Contains(u.Key) ? dv < 0f : dv > 0f;
                        string ds = (dv > 0f ? "(+" : "(") + (r.IsPercent ? dv.ToString("P0") : dv.GetNumberString()) + ")";
                        batch.DrawString(font, ds, new Vector2(cursor.X + spacing + 46f, cursor.Y),
                                         better ? Color.LightGreen : Color.LightPink);
                    }
                }
                else
                {
                    // Absent on the active module: dimmed label + dash (Ludo's call — "the other
                    // one has a hangar and this one hasn't" is information worth a row). The
                    // delta still shows, so the row says how much is being given up.
                    cursor.Y += font.LineSpacing;
                    string title = u.Title + ":";
                    var statCursor = new Vector2(cursor.X + spacing, cursor.Y);
                    batch.DrawString(font, title, new Vector2(statCursor.X - 20 - font.TextWidth(title), statCursor.Y), dim);
                    batch.DrawString(font, "-", statCursor, dim);

                    if (other != null && other.TryGetValue(u.Key, out CollectedStat missing) && !missing.Value.AlmostEqual(0f))
                    {
                        float dv = -missing.Value; // active has none: the delta is the whole of it
                        bool better = LowerIsBetter.Contains(u.Key) ? dv < 0f : dv > 0f;
                        string ds = "(" + (missing.IsPercent ? dv.ToString("P0") : dv.GetNumberString()) + ")";
                        batch.DrawString(font, ds, new Vector2(cursor.X + spacing + 46f, cursor.Y),
                                         better ? Color.LightGreen : Color.LightPink);
                    }
                }
            }
        }
        // ===== end comparator v2 =====

        public bool HitTest(InputState input)
        {
            return base.HitTest(input.CursorPosition) || ChooseFighterSL.HitTest(input);
        }

        public override bool HandleInput(InputState input)
        {
            if (HandleObsoleteInput(input))
                return true;

            return base.HandleInput(input);
        }

        bool HandleObsoleteInput(InputState input)
        {
            if (Obsolete.HandleInput(input))
            {
                ShipModule m = Screen.ActiveModule;
                if (input.LeftMouseClick && m != null)
                {
                    if (!m.IsObsolete(Player))
                        Player.ObsoletePlayerShipModules.Add(m.UID);
                    else
                        Player.ObsoletePlayerShipModules.Remove(m.UID);

                    return true;
                }
            }

            return false;
        }

        public override void Update(float fixedDeltaTime)
        {
            if (SelectedIndex == -1)
                SelectedIndex = 0; // this will trigger OnTabChangedEvt

            ActiveModSubMenu.Visible = Screen.ActiveModule != null || Screen.HighlightedModule != null;
            ChooseFighterSub.Visible = ChooseFighterSL.GetFighterHangar() != null;
            if (!ActiveModSubMenu.Visible)
                Screen.CompareModule = null; // Ludoal fork: closing the Active panel drops the pin
            // Ludoal fork (spec v4): the Compared panel is gone — the pinned module never shows
            // its own values, it only feeds the delta lane of the Active panel. Its old slot is
            // free for the hover frame.
            CompareModSubMenu.Visible = false;

            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            // Ludoal fork: surface the comparator gesture where the modules are picked
            batch.DrawString(Fonts.Arial12Bold, "Shift-click to compare", new Vector2(Rect.X + 10, Rect.Y - 18), Color.Wheat);
            if (ActiveModSubMenu.Visible)
            {
                DrawActiveModuleData(batch);
            }
            if (Comparing) // Ludoal fork (spec v4): one frame, active values, compared deltas
            {
                DrawComparisonStats(batch);

                // The pinned module has no frame of its own any more, so its name has to be
                // said here — right-aligned on the tab row, facing the "Active Module" tab.
                // Without it the delta lane would come from an anonymous source.
                ShipModule cmp = Screen.CompareModule;
                string cmpName = "vs " + cmp.NameText.Text;
                Graphics.Font nameFont = Fonts.Arial12Bold;
                batch.DrawString(nameFont, cmpName,
                                 new Vector2(ActiveModSubMenu.Right - nameFont.TextWidth(cmpName) - 10,
                                             ActiveModSubMenu.Y - 18f), Colors.Cream);
            }
        }

        static void DrawString(SpriteBatch batch, ref Vector2 cursorPos, string text, Graphics.Font font = null)
        {
            if (font == null) 
                font = Fonts.Arial8Bold;
            batch.DrawString(font, text, cursorPos, Color.SpringGreen);
            cursorPos.X += font.TextWidth(text);
        }

        static void DrawStringRed(SpriteBatch batch, ref Vector2 cursorPos, string text, Graphics.Font font = null)
        {
            if (font == null) 
                font = Fonts.Arial10;

            cursorPos.Y += 5;
            batch.DrawString(font, text, cursorPos, Color.Red);
            cursorPos.X += font.TextWidth(text)+2;
        }

        static void DrawString(SpriteBatch batch, ref Vector2 cursorPos, string text, Color color, Graphics.Font font = null)
        {
            if (font == null) font = Fonts.Arial8Bold;
            batch.DrawString(font, text, cursorPos, color);
            cursorPos.X += font.TextWidth(text);
        }

        // Gets the tech cost of the tech which unlocks the module provided, this is for modders in debug
        float DebugGetModuleTechCost(ShipModule module)
        {
            foreach (TechEntry tech in Player.TechEntries)
            {
                if (tech.GetUnlockableModules(Player).Any(m => m.ModuleUID == module.UID))
                    return tech.TechCost;
            }

            return 0;
        }

        void DrawActiveModuleData(SpriteBatch batch)
        {
            ShipModule mod = Screen.ActiveModule ?? Screen.HighlightedModule;

            if (ActiveModSubMenu.SelectedIndex != 0 || mod == null)
                return;

            bool isObsolete = mod.IsObsolete(Player);
            Color nameColor = isObsolete ? Color.Red : Color.White;
            Obsolete.BaseColor = nameColor;
            Obsolete.Draw(batch);
            DrawModuleData(batch, mod, ActiveModSubMenu);
        }

        // Ludoal fork: rendering extracted from DrawActiveModuleData and parameterized by
        // panel so the comparison window can reuse it verbatim.
        void DrawModuleData(SpriteBatch batch, ShipModule mod, Submenu panel)
        {
            if (mod == null)
                return;
            Color nameColor = mod.IsObsolete(Player) ? Color.Red : Color.White;
            ShipModule moduleTemplate = ResourceManager.GetModuleTemplate(mod.UID);
            //Added by McShooterz: Changed how modules names are displayed for allowing longer names
            var modTitlePos = new Vector2(panel.X + 10, panel.Y + 35);

            if (Fonts.Arial20Bold.TextWidth(moduleTemplate.NameText.Text) + 40 < panel.Width)
            {
                batch.DrawString(Fonts.Arial20Bold, moduleTemplate.NameText.Text,
                    modTitlePos, nameColor);
                modTitlePos.Y += (Fonts.Arial20Bold.LineSpacing + 6);
            }
            else
            {
                batch.DrawString(Fonts.Arial14Bold, moduleTemplate.NameText.Text,
                    modTitlePos, nameColor);
                modTitlePos.Y += (Fonts.Arial14Bold.LineSpacing + 4);
            }

            if (Screen.ParentUniverse.Debug)
            {
                batch.DrawString(Fonts.Arial12, $"Debug Tech Cost: {DebugGetModuleTechCost(mod).String(1)}", modTitlePos, Color.Gold);
                modTitlePos.Y += (Fonts.Arial12.LineSpacing + 4);
            }

            string rest = "";
            switch (moduleTemplate.Restrictions)
            {
                case Restrictions.IO:  rest = "Any Slot except E"; break;
                case Restrictions.I:   rest = "I, IO, IE or IOE";  break;
                case Restrictions.O:   rest = "O, IO, OE, or IOE"; break;
                case Restrictions.E:   rest = "E, IE, OE, or IOE"; break;
                case Restrictions.IOE: rest = "Any Slot";          break;
                case Restrictions.IE:  rest = "Any Slot except O"; break;
                case Restrictions.OE:  rest = "Any Slot except I"; break;
                case Restrictions.xI:  rest = "Only I";            break;
                case Restrictions.xIO: rest = "Only IO";           break;
                case Restrictions.xO:  rest = "Only O";            break;
            }

            // Concat ship class restrictions
            string shipRest = "";
            bool specialString = false;
            bool destroyers = GlobalStats.Defaults.UseDestroyers;

            if (destroyers)
            {
                if (mod.DroneModule && mod.FighterModule && mod.CorvetteModule 
                    && mod.FrigateModule && mod.DestroyerModule && mod.CruiserModule 
                    && mod.BattleshipModule && mod.BattleshipModule && mod.PlatformModule 
                    && mod.StationModule && mod.FreighterModule)
                {
                    shipRest = "All Hulls";
                    specialString = true;
                }
            }

            if (!destroyers)
            {
                if (mod.FighterModule && mod.CorvetteModule && mod.FrigateModule 
                    && mod.CruiserModule && mod.CruiserModule && mod.CapitalModule 
                    && mod.PlatformModule && mod.StationModule && mod.FreighterModule)
                {
                    shipRest = "All Hulls";
                }
            }

            if (!specialString && !mod.DroneModule || (!mod.DestroyerModule && destroyers) 
                     || !mod.FighterModule || !mod.CorvetteModule || !mod.FrigateModule 
                     || !mod.CruiserModule || !mod.BattleshipModule  || !mod.CapitalModule 
                     || !mod.PlatformModule || !mod.StationModule || !mod.FreighterModule)
            {
                 if (mod.DroneModule)                         shipRest += "Dr ";
                 if (mod.FighterModule)                       shipRest += "Fi ";
                 if (mod.CorvetteModule)                      shipRest += "Co ";
                 if (mod.FrigateModule)                       shipRest += "Fr ";
                 if (mod.DestroyerModule && destroyers)       shipRest += "Dy ";
                 if (mod.CruiserModule)                       shipRest += "Cr ";
                 if (mod.BattleshipModule)                    shipRest += "Bs ";
                 if (mod.CapitalModule)                       shipRest += "Ca ";
                 if (mod.FreighterModule)                     shipRest += "Frt ";
                 if (mod.PlatformModule || mod.StationModule) shipRest += "Orb ";
            }

            batch.DrawString(Fonts.Arial8Bold, Localizer.Token(GameText.Restrictions)+": "+rest, modTitlePos, Color.Orange);
            modTitlePos.Y += Fonts.Arial8Bold.LineSpacing;
            batch.DrawString(Fonts.Arial8Bold, "Hulls: "+shipRest, modTitlePos, Color.LightSteelBlue);
            modTitlePos.Y += (Fonts.Arial8Bold.LineSpacing + 11);

            int startx = (int)modTitlePos.X;
            if (moduleTemplate.IsWeapon)
            {
                var sb = new StringBuilder();
                var t = ResourceManager.GetWeaponTemplate(moduleTemplate.WeaponType);
                if (t.Tag_Guided)    sb.Append("GUIDED ");
                if (t.Tag_Intercept) sb.Append("INTERCEPTABLE ");
                if (t.Tag_Energy)    sb.Append("ENERGY ");
                if (t.Tag_Plasma)    sb.Append("PLASMA ");
                if (t.Tag_Kinetic)   sb.Append("KINETIC ");
                if (t.Explodes)      sb.Append("EXPLOSIVE ");
                if (t.Tag_PD)        sb.Append("POINT DEFENSE ");
                if (t.Tag_Missile)   sb.Append("MISSILE ");
                if (t.Tag_Beam)      sb.Append("BEAM ");
                if (t.Tag_Torpedo)   sb.Append("TORPEDO ");
                if (t.Tag_Bomb)      sb.Append("BOMB ");
                if (t.Tag_Cannon)    sb.Append("CANNON ");

                DrawString(batch, ref modTitlePos, sb.ToString(), Fonts.Arial8Bold);

                modTitlePos.Y += (Fonts.Arial8Bold.LineSpacing + 5);
                modTitlePos.X = startx;
            }

            string txt = Fonts.Arial12.ParseText(moduleTemplate.DescriptionText.Text,
                                                 panel.Width - 20);

            // Ludoal fork: the header (title/restrictions/description) gets a FIXED slot
            // so the stat rows of the Active and Compared panels align row-for-row.
            // Overlong descriptions are ellipsized instead of pushing the stats down.
            int maxLines = (int)((panel.Y + StatsStartRel - 8f - modTitlePos.Y) / Fonts.Arial12.LineSpacing);
            string[] descLines = txt.Split('\n');
            if (maxLines > 0 && descLines.Length > maxLines)
                txt = string.Join("\n", descLines, 0, maxLines) + "...";

            batch.DrawString(Fonts.Arial12, txt, modTitlePos, Color.White);
            modTitlePos.Y = panel.Y + StatsStartRel;
            float starty = modTitlePos.Y;
            modTitlePos.X = panel.X + 10; // Ludoal fork: was absolute 10 — same thing for the left panel, correct for the comparison one

            if (Comparing) // Ludoal fork: comparison mode draws the stat rows as one aligned union
                return;

            float strength = mod.CalculateModuleOffenseDefense(Screen.CurrentHull.SurfaceArea, forceRecalculate: mod.IsFighterHangar);
            DrawStat(ref modTitlePos, "Offense", strength, GameText.TT_ShipOffense);

            if (mod.BombType == null && !mod.IsWeapon || mod.InstalledWeapon == null)
            {
                DrawModuleStats(batch, mod, modTitlePos, starty);
            }
            else
            {
                DrawWeaponStats(batch, modTitlePos, mod, mod.InstalledWeapon, starty);
            }
        }

        void DrawStat(ref Vector2 cursor, LocalizedText text, float stat, LocalizedText toolTipId, bool isPercent = false)
        {
            if (stat.AlmostEqual(0))
                return;
            if (CollectStat(cursor, text, stat, toolTipId, isPercent, 0, Color.White)) // Ludoal fork
            {
                cursor.Y += Fonts.Arial12Bold.LineSpacing;
                return;
            }
            Screen.DrawStat(ref cursor, text, stat, Color.White, toolTipId, spacing: ActiveModStatSpacing, isPercent: isPercent);
        }

        void DrawStatCustomColor(ref Vector2 cursor, LocalizedText text, float stat, LocalizedText toolTipId, Color color, bool isPercent = true)
        {
            if (stat.AlmostEqual(0))
                return;
            if (CollectStat(cursor, text, stat, toolTipId, isPercent, 1, color)) // Ludoal fork
            {
                cursor.Y += Fonts.Arial12Bold.LineSpacing;
                return;
            }
            Screen.DrawStat(ref cursor, text, stat, color, toolTipId, spacing: ActiveModStatSpacing, isPercent: isPercent);
        }

        void DrawModuleStats(SpriteBatch batch, ShipModule mod, Vector2 modTitlePos, float starty)
        {
            DrawStat(ref modTitlePos, GameText.Cost, mod.ActualCost(Universe), GameText.IndicatesTheProductionCostOf);
            DrawStat(ref modTitlePos, GameText.Mass2, mod.GetActualMass(Player, 1), GameText.TT_Mass);
            DrawStat(ref modTitlePos, GameText.Health, mod.ActualMaxHealth, GameText.AModulesHealthRepresentsHow);

            float powerDraw = mod.ActualPowerFlowMax - mod.PowerDraw;
            DrawStat(ref modTitlePos, GameText.Power, powerDraw, GameText.IndicatesHowMuchPowerThis);
            DrawStat(ref modTitlePos, GameText.Defense, mod.MechanicalBoardingDefense, GameText.IndicatesTheCombatStrengthAdded);
            DrawStat(ref modTitlePos, Localizer.Token(GameText.Repair)+"+", mod.ActualBonusRepairRate, GameText.IndicatesTheBonusToOutofcombat);

            float maxDepth = modTitlePos.Y;
            modTitlePos.X = modTitlePos.X + 152f;
            modTitlePos.Y = starty;

            DrawStat(ref modTitlePos, GameText.Thrust, mod.Thrust, GameText.IndicatesTheAmountOfThrust);
            DrawStat(ref modTitlePos, GameText.Warp, mod.WarpThrust, GameText.IndicatesTheAmountOfThrust2);
            DrawStat(ref modTitlePos, GameText.Turn, mod.TurnThrust, GameText.IndicatesTheAmountOfRotational);


            float shieldMax = mod.ActualShieldPowerMax;
            float amplifyShields = mod.AmplifyShields;
            DrawStat(ref modTitlePos, GameText.ShieldAmp, amplifyShields, GameText.WhenPoweredThisAmplifiesThe);

            if (mod.IsAmplified)
                DrawStatCustomColor(ref modTitlePos, GameText.ShldStr, shieldMax, GameText.IndicatesTheHitpointsOfThis, Color.Gold, isPercent: false);
            else
                DrawStat(ref modTitlePos, GameText.ShldStr, shieldMax, GameText.IndicatesTheHitpointsOfThis);

            DrawStat(ref modTitlePos, GameText.ShldSize, mod.ShieldRadius, GameText.IndicatesTheProtectiveRadiusOf);
            DrawStat(ref modTitlePos, GameText.Recharge, mod.ShieldRechargeRate, GameText.IndicatesTheNumberOfHitpoints);
            DrawStat(ref modTitlePos, GameText.Crecharge, mod.ShieldRechargeCombatRate, GameText.ThisShieldCanRechargeEven);

            // Doc: new shield resistances, UI info.
            Color shieldResistColor = Color.LightSkyBlue;
            DrawStatCustomColor(ref modTitlePos, GameText.KineticSr, mod.ShieldKineticResist, GameText.IndicatesShieldBubblesResistanceTo, shieldResistColor);
            DrawStatCustomColor(ref modTitlePos, GameText.EnergySr, mod.ShieldEnergyResist, GameText.IndicatesShieldBubblesResistanceTo2, shieldResistColor);
            DrawStatCustomColor(ref modTitlePos, GameText.ExplSr, mod.ShieldExplosiveResist, GameText.IndicatesShieldBubblesResistanceTo3, shieldResistColor);
            DrawStatCustomColor(ref modTitlePos, GameText.HybridSr, mod.ShieldPlasmaResist, GameText.IndicatesShieldBubblesResistanceTo6, shieldResistColor);
            DrawStatCustomColor(ref modTitlePos, GameText.BeamSr, mod.ShieldBeamResist, GameText.IndicatesShieldBubblesResistanceTo10, shieldResistColor);
            DrawStatCustomColor(ref modTitlePos, GameText.SdDeflect, mod.ShieldDeflection, GameText.WeaponsWhichDoLessDamage2, shieldResistColor, isPercent: false);

            DrawStat(ref modTitlePos, GameText.Regenerate, mod.Regenerate, GameText.ThisModuleHasSelfRegeneration);
            DrawStat(ref modTitlePos, GameText.Range,  mod.SensorRange, GameText.IndicatesTheAdditionalSensorRange);
            DrawStat(ref modTitlePos, GameText.Range3, mod.SensorBonus, GameText.IndicatesSensorBonusAddedBy);
            DrawStat(ref modTitlePos, GameText.Heal, mod.HealPerTurn, GameText.IndicatesTheAmountTroopsAre);
            DrawStat(ref modTitlePos, GameText.Range,  mod.TransporterRange, GameText.IndicatesTheRangeOfThis);
            DrawStat(ref modTitlePos, GameText.TransPw, mod.TransporterPower, GameText.IndicatesThePowerUsedBy);
            DrawStat(ref modTitlePos, GameText.Delay, mod.TransporterTimerConstant, GameText.IndicatesTheDelayBetweenTransports);
            DrawStat(ref modTitlePos, GameText.TransOrd, mod.TransporterOrdnance, GameText.IndicatesTheAmountOfOrdnance4);
            DrawStat(ref modTitlePos, GameText.Assault, mod.TransporterTroopAssault, GameText.IndicatesTheNumberOfTroops4);
            DrawStat(ref modTitlePos, GameText.Land, mod.TransporterTroopLanding, GameText.IndicatesTheNumberOfTroops2);
            DrawStat(ref modTitlePos, GameText.Ordnance, mod.OrdinanceCapacity, GameText.IndicatesTheAmountOfOrdnance2);
            DrawStat(ref modTitlePos, GameText.CargoSpace,  mod.CargoCapacity, GameText.TT_CargoSpace);
            DrawStat(ref modTitlePos, GameText.ResearchPerTurnModule, mod.ResearchPerTurn, GameText.ResearchPerTurnStatTip);
            DrawStat(ref modTitlePos, GameText.RefiningModule, mod.Refining, GameText.RefiningPerTurnStatTip);
            DrawStat(ref modTitlePos, GameText.Ordnances, mod.OrdnanceAddedPerSecond, GameText.TT_OrdnanceCreated);
            DrawStat(ref modTitlePos, GameText.Inhibition, mod.InhibitionRadius, GameText.IndicatesTheWarpInhibitionRange);
            DrawStat(ref modTitlePos, GameText.Troops,  mod.TroopCapacity, GameText.IndicatesTheNumberOfTroops3);
            DrawStat(ref modTitlePos, GameText.PowerStore, mod.ActualPowerStoreMax, GameText.IndicatesTheAmountOfPower2);

            // added by McShooterz: Allow Power Draw at Warp variable to show up in design screen for any module
            // FB improved it to use the Power struct
            ShipModule[] modList = { mod };
            Power modNetWarpPowerDraw = Power.Calculate(modList, Player, true);
            DrawStat(ref modTitlePos, GameText.PowerWarp, -modNetWarpPowerDraw.NetWarpPowerDraw, GameText.TheEffectivePowerDrainOf);

            if (GlobalStats.Defaults.EnableECM)
            {
                DrawStat(ref modTitlePos, GameText.Ecm2, mod.ECM, GameText.IndicatesTheChanceOfEcm, isPercent: true);
            }
            if (mod.ModuleType == ShipModuleType.Hangar)
            {
                DrawStat(ref modTitlePos, GameText.SpawnTimer, mod.HangarTimerConstant, GameText.HangarsAreCapableOfSustaning);
                DrawStat(ref modTitlePos, GameText.HangarSize, mod.MaximumHangarShipSize, GameText.ThisIsTheMaximumNumber);
            }
            if (mod.Explodes)
            {
                DrawStatCustomColor(ref modTitlePos, GameText.ExpDmg, mod.ExplosionDamage, GameText.TheDamageCausedToNearby, Color.Red, isPercent: false);
                DrawStatCustomColor(ref modTitlePos, GameText.ExpRad, mod.ExplosionRadius / 16f, GameText.TheDamageRadiusOfThis, Color.Red, isPercent: false);
            }

            DrawStat(ref modTitlePos, GameText.KineticRes, mod.KineticResist, GameText.IndicatesResistanceToKinetictypeDamage, true);
            DrawStat(ref modTitlePos, GameText.EnergyRes, mod.EnergyResist, GameText.IndicatesResistanceToEnergyWeapon,  true);
            DrawStat(ref modTitlePos, GameText.HybridRes, mod.PlasmaResist, GameText.IndicatesResistanceToHybridWeapon, isPercent: true);
            DrawStat(ref modTitlePos, GameText.BeamRes, mod.BeamResist, GameText.IndicatesResistanceToBeamWeapon, isPercent: true);
            DrawStat(ref modTitlePos, GameText.ExplRes, mod.ExplosiveResist, GameText.IndicatesResistanceToExplosiveDamage, isPercent: true);
            DrawStat(ref modTitlePos, GameText.ApRes, mod.APResist, GameText.IndicatesResistanceToArmourPiercing);
            DrawStat(ref modTitlePos, GameText.Deflection, mod.Deflection, GameText.WeaponsWhichDoLessDamage);
            DrawStat(ref modTitlePos, GameText.EmpProt, mod.EMPProtection, GameText.IndicatesTheAmountOfEmp2);
            DrawStat(ref modTitlePos, GameText.FireControl, mod.TargetingAccuracy, GameText.ThisValueRepresentsTheComplexity);
            DrawStat(ref modTitlePos, $"+{Localizer.Token(GameText.FcsPower)}", mod.TargetTracking, GameText.ThisIsABonusTo);

            if (mod.RepairDifficulty.NotZero()) 
                DrawStat(ref modTitlePos, GameText.Complexity, mod.RepairDifficulty, GameText.TheMoreComplexTheModule); // Complexity

            if (mod.NumberOfColonists.Greater(0))
                DrawStat(ref modTitlePos, "Colonists", mod.NumberOfColonists, GameText.ProsperInTerranWorldsAnd); // Number of Colonists

            if (Collector != null) // Ludoal fork: the hangar-ship block draws directly; active panel only
                return;

            if (mod.PermittedHangarRoles.Length == 0 && !mod.IsSupplyBay && !mod.IsTroopBay && !mod.IsMiningBay)
                return;

            var hangarOption  = ShipBuilder.GetDynamicHangarOptions(mod.HangarShipUID);
            string hangarShip = mod.GetHangarShipName(Player);
            Ship hs = ResourceManager.GetShipTemplate(hangarShip, false);
            if (hs != null)
            {
                Color color   = ShipBuilder.GetHangarTextColor(mod.HangarShipUID);
                modTitlePos.Y = Math.Max(modTitlePos.Y, maxDepth) + Fonts.Arial12Bold.LineSpacing;
                Vector2 shipSelectionPos = new Vector2(modTitlePos.X - 152f, modTitlePos.Y + 5);
                string name = hs.VanityName.IsEmpty() ? hs.Name : hs.VanityName;
                DrawString(batch, ref shipSelectionPos, string.Concat(hs.DesignRole.ToString().ToUpper(), " : ", name), color, Fonts.Arial12Bold);
                shipSelectionPos = new Vector2(modTitlePos.X - 152f, modTitlePos.Y-20);
                shipSelectionPos.Y += Fonts.Arial12Bold.LineSpacing * 2;
                DrawStat(ref shipSelectionPos, "Ord. Cost", hs.ShipOrdLaunchCost, "");
                DrawStat(ref shipSelectionPos, "Weapons", hs.Weapons.Count, "");
                DrawStat(ref shipSelectionPos, "Health", hs.HealthMax * (1+Player.data.Traits.ModHpModifier), "");
                float mass = hs.Stats.InitializeMass(hs.Modules, Player, hs.SurfaceArea, 1);
                DrawStat(ref shipSelectionPos, "FTL", hs.Stats.GetFTLSpeed(mass, Player), "");

                if (hangarOption != DynamicHangarOptions.Static)
                {
                    modTitlePos.Y = Math.Max(shipSelectionPos.Y, maxDepth) + Fonts.Arial10.LineSpacing + 5;
                    Vector2 bestShipSelectionPos = new Vector2(modTitlePos.X - 145f, modTitlePos.Y);
                    string bestShip = Fonts.Arial10.ParseText(GetDynamicHangarText(hangarOption), ActiveModSubMenu.Width - 20);
                    DrawString(batch, ref bestShipSelectionPos, bestShip, color, Fonts.Arial10);
                }
            }
        }

        string GetDynamicHangarText(DynamicHangarOptions hangarOption)
        {
            switch (hangarOption)
            {
                case DynamicHangarOptions.DynamicLaunch:
                    return "Hangar will launch more advanced ships, as they become available in your empire";
                case DynamicHangarOptions.DynamicInterceptor:
                    return "Hangar will launch more advanced ships which their designated ship category is 'Interceptor', " +
                           "as they become available in your empire. If no Fighters are available, the strongest ship will be launched";
                case DynamicHangarOptions.DynamicAntiShip:
                    return "Hangar will launch more advanced ships which their designated ship category is 'Anti-Ship', " +
                           "as they become available in your empire. If no Fighters are available, the strongest ship will be launched";
                default:
                    return "";
            }
        }

        void DrawWeaponStats(SpriteBatch batch, Vector2 cursor, ShipModule m, Weapon w, float startY)
        {
            IWeaponTemplate wOrMirv = w.T; // We want some stats to show warhead stats and not weapon stats
            if (wOrMirv.IsMirv)
            {
                wOrMirv = ResourceManager.GetWeaponTemplate(w.MirvWeapon);
            }

            float range = ModifiedWeaponStat(w, WeaponStat.Range);
            // DelayedIgnition is the missile's float-then-ignite phase; it does
            // NOT extend the launcher's cooldown (Weapon.cs sets CooldownTimer =
            // NetFireDelay). Adding it here would inflate "Delay" and deflate
            // DPS — and it's already shown separately below as "Ignition".
            float delay = ModifiedWeaponStat(w, WeaponStat.FireDelay) * GetHullFireRateBonus();
            float speed = ModifiedWeaponStat(w, WeaponStat.Speed);
            
            bool repair = w.IsRepairBeam;
            bool isBeam = repair || w.IsBeam;
            bool isBallistic = wOrMirv.Explodes && wOrMirv.OrdinanceRequiredToFire > 0f;
            float beamMultiplier = isBeam ? w.BeamDuration * (repair ? -60f : +60f) : 0f;

            float rawDamage       = ModifiedWeaponStat(wOrMirv, WeaponStat.Damage) * GetHullDamageBonus();
            float beamDamage      = rawDamage * beamMultiplier;
            float ballisticDamage = rawDamage + rawDamage * Player.data.OrdnanceEffectivenessBonus;
            float energyDamage    = rawDamage;

            float cost = m.ActualCost(Universe);
            float power = m.ModuleType != ShipModuleType.PowerPlant ? -m.PowerDraw : m.PowerFlowMax;

            DrawStat(ref cursor, GameText.Cost, cost, GameText.IndicatesTheProductionCostOf);
            DrawStat(ref cursor, GameText.Mass2, m.GetActualMass(Player, 1), GameText.TT_Mass);
            DrawStat(ref cursor, GameText.Health, m.ActualMaxHealth, GameText.AModulesHealthRepresentsHow);
            DrawStat(ref cursor, GameText.Power, power, GameText.IndicatesHowMuchPowerThis);
            DrawStat(ref cursor, GameText.Range, range, GameText.IndicatesTheMaximumRangeOf);
            if (!w.Tag_Guided)
            {
                float accuracy = w.BaseTargetError(Screen.DesignedShip.TargetingAccuracy);
                accuracy       = accuracy > 0 ? accuracy.LowerBound(1) / 16 : 0;
                DrawStat(ref cursor, GameText.Accuracy, -1 * accuracy, GameText.WeaponTargetError);
            }
            if (isBeam)
            {
                GameText beamText = repair ? GameText.Repair : GameText.Damage;
                DrawStat(ref cursor, beamText, beamDamage, repair ? GameText.IndicatesTheMaximumAmountOf4 : GameText.IndicatesTheMaximumAmountOf);
                DrawStat(ref cursor, "Duration", w.BeamDuration, GameText.TheDurationABeamWill);
            }
            else
            {
                DrawStat(ref cursor, GameText.Damage, isBallistic ? ballisticDamage : energyDamage, GameText.IndicatesTheMaximumAmountOf);
            }

            if (wOrMirv.Explodes)
            {
                DrawStat(ref cursor, "Blast Rad", wOrMirv.ExplosionRadius / 16, GameText.TheRadiusOfTheProjectiles);
            }

            if (wOrMirv.TerminalPhaseAttack)
            {
                DrawStat(ref cursor, "T.Range", wOrMirv.TerminalPhaseDistance, GameText.ThisMissileHasTerminalPhase);
                DrawStat(ref cursor, "T.Speed", wOrMirv.TerminalPhaseSpeedMod * speed, GameText.ThisIsTheSpeedThe);
            }

            if (w.DelayedIgnition.Greater(0))
                DrawStat(ref cursor, "Ignition", w.DelayedIgnition, GameText.ThisMissileHasDelayedIgnition);

            if (wOrMirv.ProjectileCount > 1 && w.IsMirv)
                DrawStat(ref cursor, "MIRV", wOrMirv.ProjectileCount, GameText.ThisWeaponHasMirvMeaning);

            cursor.X += 152f;
            cursor.Y = startY;

            if (!isBeam) DrawStat(ref cursor, GameText.Speed, speed, GameText.IndicatesTheDistanceAProjectile);

            if (rawDamage > 0f)
            {
                int salvos      = w.SalvoCount.LowerBound(1);
                int projectiles = w.ProjectileCount > 0 ? w.ProjectileCount : 1;
                float dps = isBeam ? (beamDamage / delay)
                                   : (salvos / delay) * w.ProjectileCount 
                                                      * (isBallistic ? ballisticDamage : energyDamage);

                if (wOrMirv.ProjectileCount > 1 && w.IsMirv)
                    dps *= wOrMirv.ProjectileCount;

                DrawStat(ref cursor, "DPS", dps, GameText.IndicatesTheMaximumDamagePer);
                if (salvos > 1) DrawStat(ref cursor, "Salvo", salvos, GameText.ThisWeaponsFireASalvo);
                if (projectiles > 1) DrawStat(ref cursor, "Projectiles", projectiles, GameText.ThisWeaponFiresMoreThan);
            }

            if (w.FireImprecisionAngle > 0)
                DrawStat(ref cursor, "Imprecision", w.FireImprecisionAngle, GameText.MaximumImprecisionAngleInDegrees);

            DrawStat(ref cursor, "Pwr/s", w.BeamPowerCostPerSecond, GameText.TheAmountOfPowerThis);
            DrawStat(ref cursor, "Delay", delay, GameText.TimeBetweenShots);
            DrawStat(ref cursor, "EMP", w.EMPDamage, GameText.IndicatesTheAmountOfEmp);

            float siphon = w.SiphonDamage + w.SiphonDamage * beamMultiplier;
            DrawStat(ref cursor, "Siphon", siphon, GameText.IndicatesTheAmountOfShields);

            float tractor = w.TractorDamage + w.TractorDamage * beamMultiplier;
            DrawStat(ref cursor, "Tractor", tractor, GameText.IndicatesTheAmountOfDrag);

            float powerDamage = w.PowerDamage + w.PowerDamage * beamMultiplier;
            DrawStat(ref cursor, "Pwr Dmg", powerDamage, GameText.IndicatesTheAmountOfPower3);
            DrawStat(ref cursor, GameText.FireArc, m.FieldOfFire.ToDegrees(), GameText.AWeaponMayOnlyFire);
            DrawStat(ref cursor, "Ord / Shot", w.OrdinanceRequiredToFire, GameText.IndicatesTheAmountOfOrdnance);
            DrawStat(ref cursor, "Pwr / Shot", w.PowerRequiredToFire, GameText.IndicatesTheAmountOfPower);

            if (w.Tag_Guided && GlobalStats.Defaults.EnableECM)
                DrawStatPercentLine(ref cursor, GameText.EcmResist, w.ECMResist, GameText.IndicatesTheResistanceOfThis);

            DrawResistancePercent(ref cursor, wOrMirv, "VS Armor", WeaponStat.Armor);
            DrawResistancePercent(ref cursor, wOrMirv, "VS Shield", WeaponStat.Shield);
            if (!wOrMirv.TruePD)
            {
                int actualArmorPen = wOrMirv.ArmorPen + (wOrMirv.Tag_Kinetic ? Player.data.ArmorPiercingBonus : 0);
                if (actualArmorPen > wOrMirv.ArmorPen)
                    DrawStatCustomColor(ref cursor, GameText.ArmorPen, actualArmorPen, GameText.ArmorPenetrationEnablesThisWeapon, Color.Gold, isPercent: false);
                else
                    DrawStat(ref cursor, "Armor Pen", actualArmorPen, GameText.ArmorPenetrationEnablesThisWeapon);

                float actualShieldPenChance = Player.data.ShieldPenBonusChance + wOrMirv.ShieldPenChance / 100;
                for (int i = 0; i < wOrMirv.ActiveWeaponTags.Length; ++i)
                {
                    CheckShieldPenModifier(wOrMirv.ActiveWeaponTags[i], ref actualShieldPenChance);
                }

                if (actualShieldPenChance.Greater(wOrMirv.ShieldPenChance / 100))
                    DrawStatCustomColor(ref cursor, GameText.ShieldPen, actualShieldPenChance.UpperBound(1), GameText.RandomChanceThisWeaponWill, Color.Gold);
                else
                    DrawStat(ref cursor, "Shield Pen", actualShieldPenChance.UpperBound(100), GameText.RandomChanceThisWeaponWill, isPercent: true);
            }
            DrawStat(ref cursor, GameText.Ordnance, m.OrdinanceCapacity, GameText.IndicatesTheAmountOfOrdnance2);
            DrawStat(ref cursor, GameText.Deflection, m.Deflection, GameText.WeaponsWhichDoLessDamage);
            if (m.RepairDifficulty > 0) DrawStat(ref cursor, GameText.Complexity, m.RepairDifficulty, GameText.TheMoreComplexTheModule); // Complexity

            if (wOrMirv.TruePD && Collector == null) // Ludoal fork: direct draw, active panel only
            {
                WriteLine(ref cursor);
                DrawStringRed(batch, ref cursor, "Cannot Target Ships");
            }
            else if (wOrMirv.ExcludesFighters || wOrMirv.ExcludesCorvettes || wOrMirv.ExcludesCapitals || wOrMirv.ExcludesStations)
            {
                WriteLine(ref cursor);
                DrawStringRed(batch, ref cursor, "Cannot Target:", Fonts.Arial8Bold);

                if (wOrMirv.ExcludesFighters)  WriteLine(batch, ref cursor, "Fighters");
                if (wOrMirv.ExcludesCorvettes) WriteLine(batch, ref cursor, "Corvettes");
                if (wOrMirv.ExcludesCapitals)  WriteLine(batch, ref cursor, "Capitals");
                if (wOrMirv.ExcludesStations)  WriteLine(batch, ref cursor, "Stations");
            }
        }

        void CheckShieldPenModifier(WeaponTag tag, ref float actualShieldPenChance)
        {
            WeaponTagModifier weaponTag = Player.data.WeaponTags[tag];
            actualShieldPenChance += weaponTag.ShieldPenetration;
        }

        void DrawStatPercentLine(ref Vector2 cursor, GameText text, float stat, LocalizedText tooltipId)
        {
            DrawStat(ref cursor, text, stat, tooltipId, isPercent: true);
            WriteLine(ref cursor);
        }

        void WriteLine(SpriteBatch batch, ref Vector2 cursor, string text)
        {
            batch.DrawString(Fonts.Arial8Bold, text, cursor, Color.Wheat);
            WriteLine(ref cursor);
        }

        static void WriteLine(ref Vector2 cursor, int lines = 1)
        {
            cursor.Y += Fonts.Arial12Bold.LineSpacing * lines;
        }

        static float GetStatForWeapon(WeaponStat stat, IWeaponTemplate weapon)
        {
            switch (stat)
            {
                case WeaponStat.Damage:    return weapon.DamageAmount;
                case WeaponStat.Range:     return weapon.BaseRange;
                case WeaponStat.Speed:     return weapon.ProjectileSpeed;
                case WeaponStat.FireDelay: return weapon.NetFireDelay;
                case WeaponStat.Armor:     return weapon.EffectVsArmor;
                case WeaponStat.Shield:    return weapon.EffectVsShields;
                default: return 0f;
            }
        }

        float ModifiedWeaponStat(IWeaponTemplate weapon, WeaponStat stat)
        {
            float value = GetStatForWeapon(stat, weapon);
            foreach (WeaponTag tag in weapon.ActiveWeaponTags)
                value += value * Player.data.GetStatBonusForWeaponTag(stat, tag);
            return value;
        }

        void DrawResistancePercent(ref Vector2 cursor, IWeaponTemplate weapon, string description, WeaponStat stat)
        {
            float effect = ModifiedWeaponStat(weapon, stat);
            if (effect.NotEqual(1))
            {
                if (CollectStat(cursor, description, effect, GameText.IndicatesAnyBonusOrPenalty, true, 2, Color.White)) // Ludoal fork
                {
                    cursor.Y += Fonts.Arial12Bold.LineSpacing;
                    return;
                }
                Screen.DrawStatBadPercentLower1(ref cursor, description, effect, Color.White, GameText.IndicatesAnyBonusOrPenalty, ActiveModStatSpacing);
            }
        }

        float GetHullDamageBonus()
        {
            if (GlobalStats.Defaults.UseHullBonuses)
                return 1f + Screen.CurrentHull.Bonuses.DamageBonus;
            return 1f;
        }

        float GetHullFireRateBonus()
        {
            if (GlobalStats.Defaults.UseHullBonuses)
                return 1f - Screen.CurrentHull.Bonuses.FireRateBonus;
            return 1f;
        }
    }
}
