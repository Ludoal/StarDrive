using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens.ShipDesign
{
    using GT = GameText;

    /// <summary>
    /// Displays STATS information of the currently active design.
    ///
    /// Ludoal fork: drawn in IMMEDIATE MODE, on the same pattern as the module comparator in
    /// ModuleSelection (which is our own code) — rows are data, the draw walks them with a
    /// cursor per column, and a row that must not show is simply never drawn. That is why
    /// there are no gaps and no layout passes here. The previous element-based version (a
    /// UIList of labels toggled visible) cost two bugs in one evening and, worse, left two
    /// different architectures for the same kind of panel in the same screen — which is what
    /// makes a contribution hard for the upstream devs to take.
    ///
    /// Content and order come from Lek's spec v3: eight blocks by decision, costs first,
    /// FTL time with the speeds, block headings hidden along with their block.
    /// </summary>
    public class ShipDesignInfoPanel : UIElementContainer
    {
        readonly ShipDesignScreen Screen;
        Ship S;
        ShipDesignStats Ds;
        bool UpdateDesignStats;

        // A row is either a block heading or one stat line.
        struct Row
        {
            public string Heading;            // non-null => this row is a block heading
            public Color Color;               // heading colour, or the title colour of a stat
            public LocalizedText Title;
            public LocalizedText Tip;
            public Func<float> Value;         // null when Text is used instead
            public string Text;               // a word value, e.g. "INF"
            public Func<float, Color> Tint;   // value colour from the value; null => white
            public Func<bool> Visible;        // null => always visible
            public bool NonZeroOnly;          // hidden while the value is zero
        }

        readonly Array<Row> Rows = new Array<Row>();

        // Ludoal fork (spec v4): the pinned design has no frame of its own — it lives here, as a
        // shadow row set whose only job is to produce the delta after each of THIS panel's
        // values. Rows are matched by their title, not by index, because the two designs hide
        // different lines. Null when nothing is pinned.
        public ShipDesignInfoPanel CompareAgainst;
        string ComparedName;

        // column offsets, bench values
        public float Col0Shift = 20f;
        public float Col1Shift = 10f;

        // Ludoal fork (spec v4): take a pinned design as the delta source. It is held as a
        // detached panel — never added to the screen, never drawn — because its row set is
        // exactly what TryGetVisibleValue needs to answer with.
        public void SetComparedDesign(Ship ship, string name)
        {
            if (ship == null)
            {
                CompareAgainst = null;
                ComparedName = null;
                return;
            }

            // The shadow is never updated after this: a pinned design does not change while it
            // is pinned, so its stats are captured once and stay valid.
            var shadow = new ShipDesignInfoPanel(Screen, Rect);
            shadow.SetActiveDesign(ship);
            CompareAgainst = shadow;
            ComparedName = name;
        }

        // Stats where LESS is better. Lek's list also had TurnRate and the power drains; both are
        // wrong and the code proves it: the game already tints a turn rate GREEN above 15, and the
        // drains are displayed negated, so closer to zero is already the higher number.
        static string[] LowerIsBetterCache;
        static bool LowerIsBetter(string title)
        {
            // GameText is an enum: it converts implicitly to LocalizedText when assigned to a
            // field, but the resolved string has to be asked for explicitly
            LowerIsBetterCache ??= new[]
            {
                Localizer.Token(GT.ProductionCost),
                Localizer.Token(GT.UpkeepCost),
                Localizer.Token(GT.Mass),
            };
            for (int i = 0; i < LowerIsBetterCache.Length; ++i)
                if (LowerIsBetterCache[i] == title)
                    return true;
            return false;
        }

        // the compared panel asks the active one for the same row, by title
        public bool TryGetVisibleValue(string title, out float value)
        {
            for (int i = 0; i < Rows.Count; ++i)
            {
                Row r = Rows[i];
                if (r.Heading == null && r.Value != null && r.Title.Text == title && IsVisible(r))
                {
                    value = r.Value();
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        public ShipDesignInfoPanel(ShipDesignScreen screen, in Rectangle rect) : base(rect)
        {
            Screen = screen;
        }

        public void SetActiveDesign(Ship ship, ShipDesignStats ds = null)
        {
            Rows.Clear();

            if (ship == null)
            {
                S = null;
                Ds = null;
                UpdateDesignStats = false;
            }
            else
            {
                S = ship;
                Ds = ds ?? new ShipDesignStats(ship, ship.Universe.Player);
                UpdateDesignStats = ds == null;
                BuildRows();
            }
        }

        public override void Update(float fixedDeltaTime)
        {
            if (UpdateDesignStats && S != null)
                Ds.Update(S.Universe.Player);

            base.Update(fixedDeltaTime);
        }

        // ── the content, per Lek's spec v3 ────────────────────────────────────────────────
        void BuildRows()
        {
            Color good = Color.LightGreen;
            Color energy = Color.LightSkyBlue;
            Color protect = Color.Goldenrod;
            Color engines = Color.DarkSeaGreen;
            Color ordnance = Color.IndianRed;

            Head("CONSTRUCTION", Color.Wheat);
            Stat(GT.ProductionCost, () => S.GetCost(), GT.TT_ProductionCost);
            Stat(GT.UpkeepCost, () => S.GetMaintCost(), GT.TT_UpkeepCost);
            Stat(GT.TotalModuleSlots, () => S.SurfaceArea, GT.TT_TotalModuleSlots);
            Stat(GT.Mass, () => S.Mass, GT.TT_Mass);

            Head("ENERGY", energy);
            Stat(GT.PowerCapacity, () => Ds.PowerCapacity, GT.TT_PowerCapacity, energy, tint: Above(() => Ds.PowerConsumed));
            Stat(GT.PowerRecharge, () => Ds.PowerRecharge, GT.TT_PowerRecharge, energy, tint: Positive);
            Stat(GT.RechargeAtWarp, () => Ds.ChargeAtWarp, GT.TT_RechargeAtWarp, energy, tint: Positive, vis: Ds.IsWarpCapable);
            Stat(GT.ExcessWpnPwrDrain, () => -Ds.PowerConsumed, GT.TT_ExcessWpnPwrDrain, energy, vis: Ds.HasEnergyWepsPositive);
            Stat(GT.WpnFirePowerTime, () => Ds.EnergyDuration, GT.TT_WpnFirePowerTime, energy, tint: Above(2f), vis: Ds.HasEnergyWepsPositive);
            Word(GT.WpnFirePowerTime, "INF", GT.TT_WpnFirePowerTime, energy, good, vis: Ds.HasEnergyWepsNegative);
            Stat(GT.BurstWpnPwrDrain, () => -Ds.PowerConsumedWithBeams, GT.TT_BurstWpnPwerDrain, energy, vis: Ds.HasBeams);
            Stat(GT.BurstWpnPwrTime, () => Ds.BurstEnergyDuration, GT.TT_BurstWpnPwrTime, energy, tint: _ => Color.LightPink, vis: Ds.HasBeamDurationNegative);
            Word(GT.BurstWpnPwrTime, "INF", GT.TT_BurstWpnPwrTime, energy, good, vis: Ds.HasBeamDurationPositive);

            Head("DEFENCE", protect);
            Stat(GT.TotalHitpoints, () => S.Health, GT.TT_HitPoints, protect, tint: Positive);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, protect, tint: Positive, vis: Ds.HasRegularShields);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, Color.Gold, tint: Positive, vis: Ds.HasAmplifiedMains);
            Stat(GT.ShieldAmplify, () => (int)S.Stats.ShieldAmplifyPerShield, GT.TT_ShieldAmplify, protect, tint: Positive, nonZero: true);
            Stat(GT.RepairRate, () => S.RepairRate, GT.TT_RepairRate, protect, tint: Positive, nonZero: true);
            // the tooltip promises the TOTAL protection of the design, and the load-list
            // overlay already shows EmpTolerance - show the same effective value here
            Stat(GT.EmpProtection, () => S.EmpTolerance, GT.TT_EmpProtection, protect, tint: Positive);
            Stat(GT.Ecm3, () => S.ECMValue, GT.TT_Ecm3, protect, tint: Positive, nonZero: true);

            Head("MOBILITY", engines);
            Stat(GT.FtlSpeed, () => S.MaxFTLSpeed, GT.TT_FtlSpeed, engines, tint: Above(20_000f), vis: Ds.IsWarpCapable);
            // FTL time right under the speed it belongs to (Ludo, at the bench)
            if (!S.IsPlatformOrStation)
            {
                Stat(GT.FtlTime, () => Ds.WarpTime, GT.TT_FtlTime, engines, tint: Positive, vis: Ds.HasFiniteWarp);
                Word(GT.FtlTime, "INF", GT.TT_FtlTime, engines, good, vis: Ds.HasInfiniteWarp);
            }
            Stat(GT.SublightSpeed, () => S.MaxSTLSpeed, GT.TT_SublightSpeed, engines, tint: Above(50f));
            Stat(GT.TurnRate, () => S.RotationRadsPerSecond.ToDegrees(), GT.TT_TurnRate, engines, tint: Above(15f));

            // the ordnance family was missing from the inventory sent to Lek, so its placement
            // is mine: it sits with the guns it feeds, which is where her v1 put ammo time
            Head("COMBAT / FCS", Color.Orange);
            Stat(GT.OrdnanceCreated, () => S.OrdAddedPerSecond, GT.TT_OrdnanceCreated, ordnance, nonZero: true);
            Stat(GT.OrdnanceCapacity, () => S.OrdinanceMax, GT.TT_OrdnanceCap, ordnance, vis: Ds.HasOrdnance);
            Stat(GT.AmmoTime, () => Ds.AmmoTime, GT.TT_AmmoTime, ordnance, tint: Above(30f), vis: Ds.HasOrdFinite);
            Word(GT.AmmoTime, "INF", GT.TT_AmmoTime, ordnance, good, vis: Ds.HasOrdInfinite);
            Stat(GT.FireControl, () => S.TargetingAccuracy, GT.TT_FireControl, nonZero: true);
            Stat(GT.FcsPower, () => S.TrackingPower, GT.TT_FcsPower, nonZero: true);
            Stat(GT.SensorRange3, () => S.SensorRange, GT.TT_SensorRange3, nonZero: true);

            Head("PAYLOAD", ordnance);
            Stat(GT.TroopCapacity, () => S.TroopCapacity, GT.TT_TroopCapacity, ordnance, nonZero: true);
            Stat(GT.CargoSpace, () => S.CargoSpaceMax, GT.TT_CargoSpace, nonZero: true);

            Head("STATION", Color.MediumPurple);
            Stat(GT.ResearchPerTurn, () => S.ResearchPerTurn, GT.ResearchPerTurnStatTip, nonZero: true);
            Stat(GT.ResearchStationResearchTimeStat, () => Ds.ResearchTime(), GT.ResearchStationResearchTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodResearchSupply), vis: Ds.ProducesResearch);
            Stat(GT.RefinningPerTurnStat, () => S.TotalRefining, GT.RefiningPerTurnStatTip, nonZero: true);
            Stat(GT.MiningStationRefiningTimeStat, () => Ds.RefiningTime(), GT.MiningStationRefiningTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodRefiningSupply - 0.01f), vis: Ds.RefinesResources);

            // her closing block: the verdict reads last, like a signature
            Head("ASSESSMENT", Color.White);
            Stat(GT.ShipOffense, () => Ds.Strength, GT.TT_ShipOffense, nonZero: true);
            Stat(GT.RelativeStrength, () => Ds.RelativeStrength, GT.TT_RelativeStrength, nonZero: true);
        }

        void Head(string heading, Color color) => Rows.Add(new Row { Heading = heading, Color = color });

        void Stat(in LocalizedText title, Func<float> value, in LocalizedText tip, Color? titleColor = null,
                  Func<float, Color> tint = null, Func<bool> vis = null, bool nonZero = false)
        {
            Rows.Add(new Row
            {
                Title = title, Tip = tip, Value = value,
                Color = titleColor ?? Color.White,
                Tint = tint, Visible = vis, NonZeroOnly = nonZero,
            });
        }

        void Word(in LocalizedText title, string text, in LocalizedText tip, Color titleColor,
                  Color valueColor, Func<bool> vis = null)
        {
            Rows.Add(new Row
            {
                Title = title, Tip = tip, Text = text,
                Color = titleColor, Tint = _ => valueColor, Visible = vis,
            });
        }

        static Color Positive(float v) => v > 0f ? Color.LightGreen : Color.LightPink;
        static Func<float, Color> Above(float threshold) => v => v > threshold ? Color.LightGreen : Color.LightPink;
        static Func<float, Color> Above(Func<float> threshold) => v => v > threshold() ? Color.LightGreen : Color.LightPink;

        bool IsVisible(in Row r)
        {
            if (r.Heading != null)
                return true; // decided by its block, in Draw
            if (r.Visible != null && !r.Visible())
                return false;
            return !r.NonZeroOnly || (r.Value != null && r.Value() > 0f);
        }

        // Ludoal fork (spec v4): while a design is pinned, a row this design hides but the
        // pinned one shows is still drawn — dimmed, with a dash and its delta (Ludo's call:
        // "the other one has hangars and this one hasn't" is worth a row). Outside a
        // comparison the row simply does not exist.
        bool ShownAsMissing(in Row r)
        {
            return CompareAgainst != null && r.Heading == null && r.Value != null
                && CompareAgainst.TryGetVisibleValue(r.Title.Text, out _);
        }

        bool IsDrawn(in Row r) => IsVisible(r) || ShownAsMissing(r);

        // ── the draw, two columns, split on a block boundary ──────────────────────────────
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            if (S == null)
                return;

            // the design's own name, on the frame's tab row. Right-aligned rather than hugging
            // the tab: the two frames have tabs of different widths ("Active" vs "Compared"),
            // so a fixed offset would collide on one of them.
            if (S.Name.NotEmpty())
            {
                Graphics.Font nameFont = Fonts.Arial12Bold;
                // Ludoal fork (spec v4): the pinned design has no frame to name it, so its name
                // follows this one — the delta lane must never come from an anonymous source.
                string shown = ComparedName.NotEmpty() ? S.Name + "  vs " + ComparedName : S.Name;
                batch.DrawString(nameFont, shown,
                                 new Vector2(Right - nameFont.TextWidth(shown), Y - 25f), Colors.Cream);
            }

            // Column origins, tuned at the bench by Ludo — and NOT the same on the two frames:
            // the compared one carries a delta after each value, so its columns sit further left
            // to leave that lane room. Titles are right-aligned and grow leftward, which is why
            // the longest ones ("Excess Wpn Pwr Drain") need the slack.
            float colStep = (Width - 10f) * 0.5f;
            float col0X = X + Col0Shift;
            float col1X = X + colStep + Col1Shift;
            float spacing = colStep * 0.72f; // title room; the value sits at the cursor + spacing
            Graphics.Font headFont = Fonts.Arial12Bold;

            // a heading is only worth drawing when at least one of its own lines shows
            int visibleTotal = 0;
            var blockVisible = new Array<int>(); // visible line count per block, in order
            int current = -1;
            for (int i = 0; i < Rows.Count; ++i)
            {
                if (Rows[i].Heading != null)
                {
                    blockVisible.Add(0);
                    current = blockVisible.Count - 1;
                }
                else if (current >= 0 && IsDrawn(Rows[i]))
                {
                    blockVisible[current] = blockVisible[current] + 1;
                    ++visibleTotal;
                }
            }

            // Split on the block edge NEAREST half the visible lines, not the first edge past
            // it: the greedy version put 16 lines against 9 and overflowed the frame.
            int half = (visibleTotal + 1) / 2;
            int splitBlock = blockVisible.Count;
            int bestDiff = int.MaxValue;
            int running = 0;
            for (int b = 0; b < blockVisible.Count; ++b)
            {
                if (running > 0) // never split at the first edge: the left column would be empty
                {
                    int diff = Math.Abs(running - half);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        splitBlock = b;
                    }
                }
                running += blockVisible[b];
            }

            var cursor = new Vector2(col0X, Y);
            int block = -1;
            bool firstHeadingOfColumn = true;
            for (int i = 0; i < Rows.Count; ++i)
            {
                Row r = Rows[i];
                if (r.Heading != null)
                {
                    ++block;

                    // the column switch happens BEFORE the hidden-block test: if the split
                    // block happened to be entirely hidden, skipping first meant the switch
                    // never fired and everything piled into one column
                    if (block == splitBlock)
                    {
                        cursor = new Vector2(col1X, Y);
                        firstHeadingOfColumn = true;
                    }

                    if (blockVisible[block] == 0)
                        continue; // whole block hidden, heading included

                    if (!firstHeadingOfColumn)
                        cursor.Y += headFont.LineSpacing * 0.5f; // air between blocks
                    firstHeadingOfColumn = false;

                    // same convention as a stat row: advance first, then draw — drawing first
                    // put every heading one line above its own block
                    cursor.Y += headFont.LineSpacing;
                    // right-aligned on the very edge the stat titles end at, so heading and
                    // titles share one vertical line (Ludo's call at the bench)
                    float headRight = cursor.X + spacing - 20f;
                    batch.DrawString(headFont, r.Heading,
                                     new Vector2(headRight - headFont.TextWidth(r.Heading), cursor.Y), r.Color);
                    continue;
                }

                if (blockVisible[block] == 0 || !IsDrawn(r))
                    continue;

                // this design hides the row, the pinned one has it: dimmed dash + its delta
                if (!IsVisible(r))
                {
                    var dim = new Color(105, 105, 105);
                    cursor.Y += headFont.LineSpacing;
                    string missTitle = r.Title.Text + ":";
                    var missCursor = new Vector2(cursor.X + spacing, cursor.Y);
                    batch.DrawString(headFont, missTitle,
                                     new Vector2(missCursor.X - 20f - headFont.TextWidth(missTitle), missCursor.Y), dim);
                    batch.DrawString(headFont, "-", missCursor, dim);

                    if (CompareAgainst.TryGetVisibleValue(r.Title.Text, out float miss) && !miss.AlmostEqual(0f))
                    {
                        float dmiss = -miss; // this design has none: the delta is the whole of it
                        bool betterMiss = LowerIsBetter(r.Title.Text) ? dmiss < 0f : dmiss > 0f;
                        batch.DrawString(headFont, "(" + dmiss.GetNumberString() + ")",
                                         new Vector2(cursor.X + spacing + 46f, cursor.Y),
                                         betterMiss ? Color.LightGreen : Color.LightPink);
                    }
                    continue;
                }

                if (r.Text != null)
                {
                    Screen.DrawStatText(ref cursor, r.Title, r.Text, r.Color, r.Tip, spacing,
                                        valueColor: r.Tint?.Invoke(0f));
                }
                else
                {
                    float v = r.Value();
                    Screen.DrawStatText(ref cursor, r.Title, v.GetNumberString(), r.Color, r.Tip, spacing,
                                        valueColor: r.Tint?.Invoke(v));

                    // Delta against the PINNED design, in its own lane, coloured by which
                    // direction is better for that row. The subtraction reads "this panel minus
                    // the other one", and this panel is the active design (spec v4), so a green
                    // + means the design on the workbench is the better one.
                    if (CompareAgainst != null)
                    {
                        string title = r.Title.Text;
                        if (CompareAgainst.TryGetVisibleValue(title, out float ov) && !v.AlmostEqual(ov))
                        {
                            float dv = v - ov;
                            bool better = LowerIsBetter(title) ? dv < 0f : dv > 0f;
                            string ds = (dv > 0f ? "(+" : "(") + dv.GetNumberString() + ")";
                            batch.DrawString(headFont, ds, new Vector2(cursor.X + spacing + 46f, cursor.Y),
                                             better ? Color.LightGreen : Color.LightPink);
                        }
                    }
                }
            }
        }
    }
}
