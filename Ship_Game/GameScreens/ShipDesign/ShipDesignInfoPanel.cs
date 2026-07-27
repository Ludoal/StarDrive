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
        // Ludoal fork (bench 46.152): zero. The panel is already inset 10px inside its frame,
        // so any shift here is added ON TOP of that - the left margin was 20 against 10 in the
        // middle and 0 on the right. Ten all round now (Ludo).
        public float Col0Shift = 0f;
        // shared with FrameWidthFor, which is static: the placement and the width read the SAME
        // number, they do not each carry their own copy of it
        public const float Col1ShiftConst = 10f;
        public float Col1Shift = Col1ShiftConst;

        // Ludoal fork (spec v4): the in-frame title takes this much height before the rows
        // start. Fixed, so every design puts its first row on the same line.
        const float TitleBandHeight = 30f;

        // Ludoal fork: three levels of value, no colour. Removing the per-family tints left the
        // panel flat — everything was white, so nothing structured it vertically (Ludo, at the
        // bench). Depth comes from brightness instead: the block headings stay cream and carry
        // the eye down the page, the labels drop to grey and become the background, and only
        // the numbers keep full white. The column of figures then stands out on its own, and
        // the pink of a bad value is the single colour left on the panel, so it lands hard.
        static readonly Color LabelGrey = new Color(168, 172, 178);


        // room a delta needs after a value: the offset it is drawn at, plus the widest delta
        // string we can expect ("(+157.2k)")
        // the delta is drawn this far past the value, and runs to about "(-27.35k)" wide
        const float DeltaLaneOffset = 46f;
        public const float DeltaLaneWidth = DeltaLaneOffset + 14f;

        // ★ THE MODULE PANEL'S GEOMETRY, ACTUALLY COPIED THIS TIME (Ludo, said four times:
        // "ça fonctionne parfaitement côté Module... essaie de t'en inspirer"). Two brute
        // constants, ONE per régime — no derivation, no measurement, nothing divided out of an
        // available width. The module panel is WideColStep = 210 / TightColStep = 152; a design
        // row's titles are longer ("Total Module Slots" against "Complexity"), so ours are
        // larger, but they are constants exactly as its are.
        //
        // ⚠ What went wrong in 46.157, and why the derivation had to go: measuring the title
        // room to kill the dead air in the middle SHRANK the step, so column 2 walked left and
        // drew its values straight over column 1's labels. A step that is computed from its
        // contents moves whenever the contents change - which is the whole reason the module
        // panel does not compute it.
        // ⚠ 46.159 posed these as round numbers with ~20px of slack invented on top, which is
        // exactly the margin the bench then found in the wrong places: no air on the right when
        // plain, too much in the middle when comparing (Ludo). They are DERIVED from what a
        // column paints now — still constants, still not divisions of available space, but each
        // one the sum of its parts rather than a number that looked about right.
        //
        //   title room (the longest label plus its gap) + the value + , when comparing,
        //   the delta lane it is followed by + the gap that separates the two columns
        const float TitleColumn = TitleRoomFloor + TitleValueGap;  // 92 + 12
        const float ValueColumn = 52f;                             // widest value, "107.1k"
        const float WideColumnStep  = TitleColumn + DeltaLaneOffset + DeltaLaneWidth + MidGap;
        const float TightColumnStep = TitleColumn + ValueColumn + MidGap;
        float ColumnStep => HasDeltaLanes ? WideColumnStep : TightColumnStep;
        static float StepFor(bool withDeltas) => withDeltas ? WideColumnStep : TightColumnStep;
        // air between the two columns: on the 46.138 shot column 1's delta lane nearly touched
        // "MOBILITY" (Ludo). It belongs to the STEP, so the gap opens between the columns rather
        // than just pushing column 2 further right.
        const float MidGap = 10f;
        // Where the VALUE sits inside its column, measured from the rows actually present. This
        // one stays measured: it only moves the value within a column whose width is fixed
        // above, so it cannot make two columns collide — it is what killed the dead air in the
        // middle without touching the step (bench 46.154, kept).
        float MeasuredTitleRoom = TitleRoomFloor;
        const float TitleRoomFloor = 92f;
        const float TitleValueGap = 12f;   // air between the longest title and the value column

        void RemeasureTitleRoom()
        {
            float widest = 0f;
            foreach (Row r in Rows)
            {
                if (r.Heading != null)
                    continue;
                float w = Fonts.Arial12Bold.TextWidth(r.Title.Text);
                if (w > widest)
                    widest = w;
            }
            MeasuredTitleRoom = Math.Max(TitleRoomFloor, widest + TitleValueGap);
        }
        const float ValueRoom = 52f;
        const float SidePad = 10f;    // the right-hand margin; the left one is the panel's inset

        // What a frame must measure to hold two of those columns — the screen asks this rather
        // than sizing the frame from whatever space is going spare and hoping the columns fit.
        // ⚠ ONE arithmetic for two blocks that must agree. This used to compute the frame as
        // 2*step - MidGap + SidePad while the draw placed column 1 at col0 + step + Col1Shift:
        // the extra 10px of Col1Shift existed in the placement and NOT in the width, so column 2
        // started further right than the frame had been sized for and its values were clipped by
        // the border (bench 46.158). The frame is now derived from the SAME expression the draw
        // uses — column 1's origin plus what one column needs — so the two cannot drift apart.
        public static float FrameWidthFor(bool withDeltas, bool withPlan)
        {
            float col1Origin = StepFor(withDeltas) + Col1ShiftConst;   // where the draw puts it
            float w = col1Origin + ColumnContentWidth(withDeltas) + SidePad;
            if (withPlan)
                w += PlanSide + PlanGap;
            return w;
        }

        // what one column actually paints: its title room, its value, and its delta lane when
        // the frame carries one. The step adds MidGap on top of this — that gap separates the
        // columns and must NOT be counted again past the last one.
        static float ColumnContentWidth(bool withDeltas)
            => StepFor(withDeltas) - MidGap;

        // The ship plan is a FIXED square, not a fraction of the frame: the frame's width is
        // computed FROM the plan, so deriving the plan back from the width was circular and
        // left a hole between the picture and the columns (bench 46.140).
        // 250 is what 46.139 actually drew (it worked out at 32% of a 790px frame there), kept
        // as the size itself now rather than as a fraction that has to be reverse-engineered.
        // Ludoal fork (bench): trimmed from 250. What reads as margin around the picture is
        // mostly the plan's own empty space — a ship rarely fills its square — so the way to
        // recover it is to shrink the square, not to chase a margin that is not there.
        const float PlanSide = 220f;
        const float PlanGap = 6f;

        // How far this panel is inset inside its frame. The screen builds the inner rect, so it
        // tells us here rather than us guessing a constant that would have to stay in step.
        public float InnerInset = 10f;

        // Whether this frame reserves room for delta lanes. A property of the FRAME, not of the
        // moment: the Active cartouche always keeps the room whether or not a design is pinned,
        // exactly as the module panel does, so pinning never moves a single number. The Hover
        // frame never compares, so it gives the space to its columns instead.
        public bool HasDeltaLanes;

        // Ludoal fork (spec v4): the Hover cartouche draws the design's MODULE PLAN at its far
        // left (Ludo) — the picture the flying overlay used to give, and the reason to keep a
        // hover frame at all. Same call the overlay made: RenderOverlay with the modules and the
        // hull background. Only the hover frame turns this on.
        public bool ShowShipPlan;

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
                // ⚠ measured over EVERY declared row, including the ones currently hidden: a
                // row's visibility changes during a session (Shield Power appears the moment a
                // shield is fitted), and a column that shifted whenever a line came or went
                // would be worse than the dead air it saves. Space belongs to the object, not
                // to the instant.
                RemeasureTitleRoom();
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
            // "INF" is its own signal — the word says it, the colour was redundant
            Color good = Color.White;
            Color energy = Color.LightSkyBlue;
            Color protect = Color.Goldenrod;
            Color engines = Color.DarkSeaGreen;
            Color ordnance = Color.IndianRed;

            Head("CONSTRUCTION");
            Stat(GT.ProductionCost, () => S.GetCost(), GT.TT_ProductionCost);
            Stat(GT.UpkeepCost, () => S.GetMaintCost(), GT.TT_UpkeepCost);
            Stat(GT.TotalModuleSlots, () => S.SurfaceArea, GT.TT_TotalModuleSlots);
            Stat(GT.Mass, () => S.Mass, GT.TT_Mass);

            Head("ENERGY");
            Stat(GT.PowerCapacity, () => Ds.PowerCapacity, GT.TT_PowerCapacity, energy, tint: Above(() => Ds.PowerConsumed));
            Stat(GT.PowerRecharge, () => Ds.PowerRecharge, GT.TT_PowerRecharge, energy, tint: Positive);
            Stat(GT.RechargeAtWarp, () => Ds.ChargeAtWarp, GT.TT_RechargeAtWarp, energy, tint: Positive, vis: Ds.IsWarpCapable);
            Stat(GT.ExcessWpnPwrDrain, () => -Ds.PowerConsumed, GT.TT_ExcessWpnPwrDrain, energy, vis: Ds.HasEnergyWepsPositive);
            Stat(GT.WpnFirePowerTime, () => Ds.EnergyDuration, GT.TT_WpnFirePowerTime, energy, tint: Above(2f), vis: Ds.HasEnergyWepsPositive);
            Word(GT.WpnFirePowerTime, "INF", GT.TT_WpnFirePowerTime, energy, good, vis: Ds.HasEnergyWepsNegative);
            Stat(GT.BurstWpnPwrDrain, () => -Ds.PowerConsumedWithBeams, GT.TT_BurstWpnPwerDrain, energy, vis: Ds.HasBeams);
            Stat(GT.BurstWpnPwrTime, () => Ds.BurstEnergyDuration, GT.TT_BurstWpnPwrTime, energy, tint: _ => Color.LightPink, vis: Ds.HasBeamDurationNegative);
            Word(GT.BurstWpnPwrTime, "INF", GT.TT_BurstWpnPwrTime, energy, good, vis: Ds.HasBeamDurationPositive);

            // Ludoal fork: MOBILITY before DEFENCE (Ludo). Reading order follows the columns —
            // the left one now runs CONSTRUCTION, ENERGY, MOBILITY, which is what the ship IS,
            // while the right one carries what it does in a fight.
            Head("MOBILITY");
            Stat(GT.FtlSpeed, () => S.MaxFTLSpeed, GT.TT_FtlSpeed, engines, tint: Above(20_000f), vis: Ds.IsWarpCapable);
            // FTL time right under the speed it belongs to (Ludo, at the bench)
            if (!S.IsPlatformOrStation)
            {
                Stat(GT.FtlTime, () => Ds.WarpTime, GT.TT_FtlTime, engines, tint: Positive, vis: Ds.HasFiniteWarp);
                Word(GT.FtlTime, "INF", GT.TT_FtlTime, engines, good, vis: Ds.HasInfiniteWarp);
            }
            Stat(GT.SublightSpeed, () => S.MaxSTLSpeed, GT.TT_SublightSpeed, engines, tint: Above(50f));
            Stat(GT.TurnRate, () => S.RotationRadsPerSecond.ToDegrees(), GT.TT_TurnRate, engines, tint: Above(15f));

            Head("DEFENCE");
            Stat(GT.TotalHitpoints, () => S.Health, GT.TT_HitPoints, protect, tint: Positive);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, protect, tint: Positive, vis: Ds.HasRegularShields);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, Color.Gold, tint: Positive, vis: Ds.HasAmplifiedMains);
            Stat(GT.ShieldAmplify, () => (int)S.Stats.ShieldAmplifyPerShield, GT.TT_ShieldAmplify, protect, tint: Positive, nonZero: true);
            Stat(GT.RepairRate, () => S.RepairRate, GT.TT_RepairRate, protect, tint: Positive, nonZero: true);
            // the tooltip promises the TOTAL protection of the design, and the load-list
            // overlay already shows EmpTolerance - show the same effective value here
            Stat(GT.EmpProtection, () => S.EmpTolerance, GT.TT_EmpProtection, protect, tint: Positive);
            Stat(GT.Ecm3, () => S.ECMValue, GT.TT_Ecm3, protect, tint: Positive, nonZero: true);

            // the ordnance family was missing from the inventory sent to Lek, so its placement
            // is mine: it sits with the guns it feeds, which is where her v1 put ammo time
            // Ludoal fork: split from one COMBAT / FCS block (Ludo). The two halves answer
            // different questions — how long can it keep shooting, versus how well does it aim
            // — and reading them as one list made neither obvious. Sensor Range sits with FCS
            // rather than earning a third heading: it is detection, not fire control, but a
            // block of its own would cost a line and the air around it for a single row.
            Head("ORDNANCE");
            Stat(GT.OrdnanceCreated, () => S.OrdAddedPerSecond, GT.TT_OrdnanceCreated, ordnance, nonZero: true);
            Stat(GT.OrdnanceCapacity, () => S.OrdinanceMax, GT.TT_OrdnanceCap, ordnance, vis: Ds.HasOrdnance);
            Stat(GT.AmmoTime, () => Ds.AmmoTime, GT.TT_AmmoTime, ordnance, tint: Above(30f), vis: Ds.HasOrdFinite);
            Word(GT.AmmoTime, "INF", GT.TT_AmmoTime, ordnance, good, vis: Ds.HasOrdInfinite);

            Head("FCS");
            Stat(GT.FireControl, () => S.TargetingAccuracy, GT.TT_FireControl, nonZero: true);
            Stat(GT.FcsPower, () => S.TrackingPower, GT.TT_FcsPower, nonZero: true);
            Stat(GT.SensorRange3, () => S.SensorRange, GT.TT_SensorRange3, nonZero: true);

            Head("PAYLOAD");
            Stat(GT.TroopCapacity, () => S.TroopCapacity, GT.TT_TroopCapacity, ordnance, nonZero: true);
            Stat(GT.CargoSpace, () => S.CargoSpaceMax, GT.TT_CargoSpace, nonZero: true);

            Head("STATION");
            Stat(GT.ResearchPerTurn, () => S.ResearchPerTurn, GT.ResearchPerTurnStatTip, nonZero: true);
            Stat(GT.ResearchStationResearchTimeStat, () => Ds.ResearchTime(), GT.ResearchStationResearchTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodResearchSupply), vis: Ds.ProducesResearch);
            Stat(GT.RefinningPerTurnStat, () => S.TotalRefining, GT.RefiningPerTurnStatTip, nonZero: true);
            Stat(GT.MiningStationRefiningTimeStat, () => Ds.RefiningTime(), GT.MiningStationRefiningTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodRefiningSupply - 0.01f), vis: Ds.RefinesResources);

            // her closing block: the verdict reads last, like a signature
            Head("ASSESSMENT");
            Stat(GT.ShipOffense, () => Ds.Strength, GT.TT_ShipOffense, nonZero: true);
            Stat(GT.RelativeStrength, () => Ds.RelativeStrength, GT.TT_RelativeStrength, nonZero: true);
        }

        // Ludoal fork (bench 46.135): block headings are all cream (Ludo). They are structure,
        // not content — colouring each one after its family made the panel read as eight
        // unrelated lists instead of one. The per-block colour still tints the stat TITLES,
        // which is where it carries meaning.
        void Head(string heading) => Rows.Add(new Row { Heading = heading, Color = Colors.Cream });

        void Stat(in LocalizedText title, Func<float> value, in LocalizedText tip, Color? titleColor = null,
                  Func<float, Color> tint = null, Func<bool> vis = null, bool nonZero = false)
        {
            // Ludoal fork: labels are WHITE — the per-family colour they used to carry is now
            // said by the block heading above them, so tinting each label as well was a
            // duplicate, and it competed with the delta lane for the eye. The titleColor
            // argument is kept so the family is still declared at the call site (it documents
            // which block a row belongs to) but it no longer reaches the screen.
            Rows.Add(new Row
            {
                Title = title, Tip = tip, Value = value,
                Color = LabelGrey,
                Tint = tint, Visible = vis, NonZeroOnly = nonZero,
            });
        }

        void Word(in LocalizedText title, string text, in LocalizedText tip, Color titleColor,
                  Color valueColor, Func<bool> vis = null)
        {
            Rows.Add(new Row
            {
                Title = title, Tip = tip, Text = text,
                Color = LabelGrey, Tint = _ => valueColor, Visible = vis,
            });
        }

        // Ludoal fork: colour is an ATTENTION GETTER, not a status light (Ludo). A value that is
        // fine reads white; only a value that is below its threshold — or negative where it
        // should not be — goes pink. The permanent green said nothing and competed with the
        // delta lane, which uses the same two colours to mean something else entirely.
        // (Upstream player feedback, Roland-Johansen: the existing colour coding "may be
        // distracting from the comparison that you wish to show".)
        static Color Positive(float v) => v > 0f ? Color.White : Color.LightPink;
        static Func<float, Color> Above(float threshold) => v => v > threshold ? Color.White : Color.LightPink;
        static Func<float, Color> Above(Func<float> threshold) => v => v > threshold() ? Color.White : Color.LightPink;

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
        bool ShownAsMissing(int index)
        {
            Row r = Rows[index];
            if (CompareAgainst == null || r.Heading != null || r.Value == null)
                return false;
            if (!CompareAgainst.TryGetVisibleValue(r.Title.Text, out _))
                return false;

            // Several rows can share a title while being mutually exclusive by their vis()
            // predicate — Shield Power is declared twice, once plain and once amplified. Only
            // the FIRST of them may stand in as the missing row, or the panel shows the same
            // dimmed line as many times as it was declared (bench, 46.134).
            for (int i = 0; i < index; ++i)
            {
                Row o = Rows[i];
                if (o.Heading == null && o.Value != null && o.Title.Text == r.Title.Text)
                    return false;
            }
            return true;
        }

        bool IsDrawn(int index) => IsVisible(Rows[index]) || ShownAsMissing(index);

        // ── the draw, two columns, split on a block boundary ──────────────────────────────
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            if (S == null)
                return;

            // Ludoal fork (spec v4): the design name sits INSIDE the frame, on the module
            // panel's pattern (Ludo) — it used to hang off the tab row, right-aligned, which
            // read as a stray label rather than the frame's subject. The pinned design has no
            // frame to name it, so its name follows, one size down: the delta lane must never
            // come from an anonymous source.
            if (S.Name.NotEmpty())
            {
                Graphics.Font nameFont = Fonts.Arial20Bold;
                if (nameFont.TextWidth(S.Name) + 40f > Width)
                    nameFont = Fonts.Arial14Bold;

                // The module frame draws its title at (frame.X + 10). This panel is the INNER
                // rect, already inset by 10, so drawing at X put the name 20px off the frame —
                // twice the module's margin, which is what read as "pushed to the right"
                // (Ludo, three benches running). Back out the inset so the two agree.
                // Ludoal fork (bench 46.154): same reasoning vertically as horizontally above.
                // The module panel draws its title at frame.Y + 35; this inner rect starts at
                // frame.Y + 26, so +2 put the name 28px down against the module's 35 and it sat
                // too close to the tab (Ludo). Derived from the module's number, not re-guessed.
                const float ModuleTitleFromFrame = 35f;
                const float InnerTopInset = 26f;
                var namePos = new Vector2(X - InnerInset + 10f + (ShowShipPlan ? PlanSide + PlanGap : 0f),
                                          Y + (ModuleTitleFromFrame - InnerTopInset));
                batch.DrawString(nameFont, S.Name, namePos, Color.White);

                if (ComparedName.NotEmpty())
                {
                    Graphics.Font vsFont = Fonts.Arial12Bold;
                    batch.DrawString(vsFont, "vs " + ComparedName,
                                     new Vector2(namePos.X + nameFont.TextWidth(S.Name) + 10f,
                                                 namePos.Y + nameFont.LineSpacing - vsFont.LineSpacing - 2f),
                                     Colors.Cream);
                }
            }

            // Column origins, tuned at the bench by Ludo. Titles are right-aligned and grow
            // leftward, which is why the longest ones ("Excess Wpn Pwr Drain") need the slack.
            // The rows now start below the in-frame title, at a FIXED offset so every design —
            // long name or short, compared or not — puts its first row on the same line.
            float rowsY = Y + TitleBandHeight;

            // the ship plan owns a square band down the frame's left edge; the stat columns
            // share whatever is left of the width
            float planW = 0f;
            if (ShowShipPlan)
            {
                // fixed square, but never taller than the frame can hold (a windowed player can
                // squash the screen well below the supported floor)
                float side = Math.Min(PlanSide, Height - TitleBandHeight - 10f);
                planW = PlanSide + PlanGap; // the columns keep their place even if the square shrinks
                S.RenderOverlay(batch, new Rectangle((int)X, (int)rowsY, (int)side, (int)side),
                                showModules: true, drawHullBackground: true,
                                moduleHealthColor: false, markLockedModules: true);
            }

            // The delta lane is ALWAYS reserved, pinned or not — this is the module panel's
            // principle, and it is the whole reason its columns never move (Ludo: "revois bien
            // le code Active Module, il fonctionne parfaitement"). Reserving it only while a
            // comparison ran made the layout shift on every pin, and squeezed the deltas onto
            // the right-hand column's labels (bench 46.136).
            // BOTH columns carry a delta lane, so both must be paid for — subtracting one lane
            // for two columns left the second one overlapping its neighbour's labels, which is
            // the "second column too far left" the bench kept seeing (46.137). The first column
            // looked right because it starts at the left edge and absorbs none of the error.
            // The FIRST column keeps its place and its title room whether or not deltas are on:
            // its geometry comes from the frame's half-width, exactly as it did before lanes
            // existed. Only the SECOND column moves right, past the first one's delta lane
            // (46.138: taking both lanes out of the total shrank every column and dragged
            // column 1 leftwards — Ludo wanted column 2 pushed right, not column 1 pulled left).
            // The MODULE panel's geometry, copied instead of re-derived (Ludo, and he is right
            // to keep saying it): fixed steps, not divisions. There, column 0 starts at the
            // frame's left margin and column 1 a constant step further right; the title room is
            // a fraction of the FRAME, never of the column; and the delta lane is simply the
            // space left between the step and the next column, neither reserved nor subtracted.
            // Every version of this panel that divided the available width moved something else
            // each time a width changed — five benches' worth of it.
            float colStep = ColumnStep;
            float col0X = X + planW + Col0Shift;
            float col1X = col0X + colStep + Col1Shift - Col0Shift;
            float spacing = MeasuredTitleRoom; // the value sits at the cursor + spacing
            Graphics.Font headFont = Fonts.Arial12Bold;

            // a heading is only worth drawing when at least one of its own lines shows
            var blockVisible = new Array<int>(); // visible line count per block, in order
            int current = -1;
            for (int i = 0; i < Rows.Count; ++i)
            {
                if (Rows[i].Heading != null)
                {
                    blockVisible.Add(0);
                    current = blockVisible.Count - 1;
                }
                else if (current >= 0 && IsDrawn(i))
                {
                    blockVisible[current] = blockVisible[current] + 1;
                }
            }

            // Ludoal fork (bench 46.150): the split is FIXED by block, not balanced by line
            // count. Balancing meant MOBILITY sat left on one design and right on the next,
            // because a design with no shields or no ordnance shifts the halfway mark - so the
            // reader had to find each block again on every ship (Ludo).
            //
            // Left column is what the ship IS: construction, energy, mobility.
            // Right column is what happens to it or from it: defence, ordnance, fire control,
            // payload, station, verdict. A hidden block costs its side height and nothing else.
            //
            // Worst case is 21 lines left against 28 right, but that is a ship carrying
            // ordnance, a research station AND cargo at once, which does not exist. In practice
            // the right-hand blocks largely exclude each other while DEFENCE is almost always
            // full, so the two columns come out close. If a real design ever runs the right
            // column past the frame, this is the number to revisit.
            const int FixedSplitBlock = 3;
            int splitBlock = Math.Min(FixedSplitBlock, blockVisible.Count);

            var cursor = new Vector2(col0X, rowsY);
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
                        cursor = new Vector2(col1X, rowsY);
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

                if (blockVisible[block] == 0 || !IsDrawn(i))
                    continue;

                // this design hides the row, the pinned one has it: dimmed dash + its delta
                if (!IsVisible(r))
                {
                    var dim = new Color(105, 105, 105);
                    cursor.Y += headFont.LineSpacing;
                    string missTitle = r.Title.Text; // no trailing colon, same as the drawn rows
                    var missCursor = new Vector2(cursor.X + spacing, cursor.Y);
                    batch.DrawString(headFont, missTitle,
                                     new Vector2(missCursor.X - 20f - headFont.TextWidth(missTitle), missCursor.Y), dim);
                    batch.DrawString(headFont, "-", missCursor, dim);

                    if (CompareAgainst.TryGetVisibleValue(r.Title.Text, out float miss) && !miss.AlmostEqual(0f))
                    {
                        float dmiss = -miss; // this design has none: the delta is the whole of it
                        bool betterMiss = LowerIsBetter(r.Title.Text) ? dmiss < 0f : dmiss > 0f;
                        batch.DrawString(headFont, "(" + dmiss.GetNumberString() + ")",
                                         new Vector2(cursor.X + spacing + DeltaLaneOffset, cursor.Y),
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
                            batch.DrawString(headFont, ds, new Vector2(cursor.X + spacing + DeltaLaneOffset, cursor.Y),
                                             better ? Color.LightGreen : Color.LightPink);
                        }
                    }
                }
            }
        }
    }
}
