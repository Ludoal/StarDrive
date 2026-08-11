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
    /// ModuleSelection — rows are data, the draw walks them with a cursor per column, and a row
    /// that must not show is simply never drawn. No gaps, no layout passes.
    ///
    /// Content and order: eight blocks by decision, costs first, FTL time with the speeds,
    /// block headings hidden along with their block.
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
            public Func<string> TextFn;       // a LIVE text value (compact W.Range) - no delta
            public Func<float, Color> Tint;   // value colour from the value; null => white
            public Func<bool> Visible;        // null => always visible
            public bool NonZeroOnly;          // hidden while the value is zero
            public string Icon;               // Ludoal fork: optional inline icon, left of the title
            public Color IconColor;           // its own colour, the load popup's
            public bool Gap;                  // headingless block break (compact) - air, no text
        }

        readonly Array<Row> Rows = new Array<Row>();

        // Ludoal fork: the pinned design has no frame of its own — it lives here, as a shadow
        // row set whose only job is to produce the delta after each of THIS panel's values. Rows
        // are matched by their title, not by index, because the two designs hide different
        // lines. Null when nothing is pinned.
        public ShipDesignInfoPanel CompareAgainst;
        string ComparedName;
        // where the "x" that drops the comparison sits; empty when nothing is pinned
        public RectF CancelCompareRect;

        // Ludoal fork: zero. The panel is already inset 10px inside its frame, so any shift here
        // is added ON TOP of that - ten all round.
        public float Col0Shift = 0f;
        // shared with FrameWidthFor, which is static: the placement and the width read the SAME
        // number, they do not each carry their own copy of it
        public const float Col1ShiftConst = 10f;
        public float Col1Shift = Col1ShiftConst;

        // Ludoal fork (spec v4): the in-frame title takes this much height before the rows
        // start. Fixed, so every design puts its first row on the same line.
        const float TitleBandHeight = 30f;
        // compact: the "vs <name>" moves to a SECOND line (the narrow frame has no room
        // beside the name), and the line is RESERVED on both frames whether or not a
        // comparison runs - the two row sets stay level
        const float CompactTitleBand = 46f;

        // Ludoal fork: three levels of value, no per-family colour. Depth comes from brightness
        // instead: block headings stay cream and carry the eye down the page, labels drop to
        // grey and become the background, and only the numbers keep full white. The column of
        // figures stands out on its own, and the pink of a bad value is the single colour left
        // on the panel, so it lands hard.
        static readonly Color LabelGrey = new Color(168, 172, 178);


        // Room a delta needs after a value: the offset it is drawn at, plus the widest delta
        // string expected ("(+157.2k)"). The delta is drawn LEFT-aligned at spacing +
        // DeltaLaneOffset, growing RIGHTWARD from its anchor, so the lane must be offset + text
        // width, not a guessed constant — the lane also has to clear where the value itself
        // ends (cursor.X + spacing, running its own width), or the two overlap.
        static float DeltaLaneOffset => ValueRoom + 8f;
        // MEASURED, not guessed. "(-27.35k)" is the widest delta the formatter can produce:
        // sign, four significant digits, a decimal point and the k suffix, in brackets.
        static float WidestDelta => Fonts.Arial12Bold.TextWidth("(-27.35k)");
        public static float DeltaLaneWidth => DeltaLaneOffset + WidestDelta;

        // ★ THE MODULE PANEL'S GEOMETRY, copied. Two brute constants, ONE per régime — no
        // derivation, no measurement, nothing divided out of an available width. The module
        // panel is WideColStep = 210 / TightColStep = 152; a design row's titles are longer
        // ("Total Module Slots" against "Complexity"), so ours are larger, but they are
        // constants exactly as its are. A step computed from its contents moves whenever the
        // contents change, which is why it is not derived that way.
        //
        // Each constant is the sum of its parts:
        //   title room (the longest label plus its gap) + the value + , when comparing,
        //   the delta lane it is followed by + the gap that separates the two columns
        //
        // The titles are drawn RIGHT-ALIGNED: DrawStatText places them via
        // FontSpace(cursor.X + spacing, -20, ...), i.e. at
        //
        //     titleLeft = cursor.X + spacing - 20 - textWidth(title)
        //
        // so cursor.X anchors the VALUE, never the title, and the block's visible left edge is
        // wherever the LONGEST label lands. The title room constant must therefore already
        // include the -20 and the longest label's own width, or the whole block reads as
        // shifted left regardless of ContentLeft.
        //
        // The module panel escapes this because its spacing is a fraction of its frame
        // (panel.Width * 0.27f), which happens to land right for its shorter labels.
        //
        //   135 = 10 (left margin) + 20 (FontSpace's own inset) + 115 (longest label) - 10 (Inset,
        //   already paid by the inner rect the cursor starts at)
        // Measured, so if a longer label is ever added to the rows the column follows it instead
        // of quietly clipping — and a font change cannot silently break the layout.
        // ⚠ measured over EVERY label this panel can draw, not a chosen specimen: "Excess Wpn Pwr
        // Drain" and "Processing Time (turns)" are wider than "Total Module Slots", and since the
        // titles are RIGHT-aligned a label longer than the reserve runs off the left edge of the
        // frame. Cached: the widths cannot change while the game runs, and this is read every frame.
        static float LongestTitleCache;
        static float LongestTitle
        {
            get
            {
                if (LongestTitleCache <= 0f)
                {
                    foreach (GT t in RowTitleKeys)
                    {
                        float w = Fonts.Arial12Bold.TextWidth(Localizer.Token(t));
                        if (w > LongestTitleCache)
                            LongestTitleCache = w;
                    }
                    // ⚠ and the rows written as raw strings, which carry no GameText key and
                    // would otherwise never be measured.
                    foreach (string t in RawRowTitles)
                    {
                        float w = Fonts.Arial12Bold.TextWidth(t);
                        if (w > LongestTitleCache)
                            LongestTitleCache = w;
                    }
                }
                return LongestTitleCache;
            }
        }

        // every label BuildRows can put in the title column. A row added without its key here
        // simply is not measured, and a too-long one would overhang - so keep the two in step.
        // titles with no GameText key, spelled out in BuildRows
        static readonly string[] RawRowTitles = { "Weapons", "Max Wpn Range", "DPS" };

        static readonly GT[] RowTitleKeys =
        {
            GT.AmmoTime, GT.BurstWpnPwrDrain, GT.BurstWpnPwrTime, GT.CargoSpace, GT.Ecm3,
            GT.EmpProtection, GT.ExcessWpnPwrDrain, GT.FcsPower, GT.FireControl, GT.FtlSpeed,
            GT.FtlTime, GT.Mass, GT.MiningStationRefiningTimeStat, GT.OrdnanceCapacity,
            GT.OrdnanceCreated, GT.PowerCapacity, GT.PowerRecharge, GT.ProductionCost,
            GT.RechargeAtWarp, GT.RefinningPerTurnStat, GT.RelativeStrength, GT.RepairRate,
            GT.ResearchPerTurn, GT.ResearchStationResearchTimeStat, GT.SensorRange3,
            GT.ShieldAmplify, GT.ShieldPower, GT.ShipOffense, GT.SublightSpeed,
            GT.TotalHitpoints, GT.TotalModuleSlots, GT.TroopCapacity, GT.TurnRate,
            GT.UpkeepCost, GT.WpnFirePowerTime
        };
        static float TitleColumn => 10f + 20f + LongestTitle - Inset;

        // ⚠ NOT + DeltaLaneOffset + DeltaLaneWidth: the width ALREADY contains the offset (it
        // is offset + text). Adding both counts the lead-in twice, widening the comparing frame
        // beyond its own content.
        static float WideColumnStep  => TitleColumn + DeltaLaneWidth + MidGap;
        static float TightColumnStep => TitleColumn + ValueRoom + MidGap;
        float ColumnStep => HasDeltaLanes ? WideColumnStep : TightColumnStep;
        static float StepFor(bool withDeltas) => withDeltas ? WideColumnStep : TightColumnStep;
        // air between the two columns. It belongs to the STEP, so the gap opens between the
        // columns rather than just pushing column 2 further right.
        const float MidGap = 10f;
        // Measured like the other two — and it feeds DeltaLaneOffset, so a wrong guess here
        // would put the delta back on top of the value.
        static float ValueRoom => Fonts.Arial12Bold.TextWidth("107.1k");
        // ⚠ the RIGHT margin only. The left one is the inner rect's own inset, which the frame
        // does not pay for again — since the frame is anchored on its right edge and grows
        // leftwards, double-counting it opens a gap down the left of the whole block. Content
        // sits Inset in from the frame's left edge and SidePad in from its right: same 10 both
        // sides, counted once each.
        const float SidePad = 10f;

        // What a frame must measure to hold two of those columns — the screen asks this rather
        // than sizing the frame from whatever space is going spare and hoping the columns fit.
        // ⚠ ONE arithmetic for two blocks that must agree: the frame is derived from the SAME
        // expression the draw uses — column 1's origin plus what one column needs — so the two
        // cannot drift apart (a frame computed independently from where the draw actually places
        // column 1 will clip that column's values against the border).
        public static float FrameWidthFor(bool withDeltas, bool withPlan)
        {
            // Include the left margin the draw starts at: content begins at ContentLeft, i.e.
            // Inset px in from the frame's edge, not at the frame's edge.
            // The frame, read left edge to right edge exactly as the draw consumes it:
            //
            //   [Inset] [column 0] [Col1Shift] [column 1] [SidePad]
            //
            // ⚠ the two margins are NOT interchangeable and both have to be here once. Content
            // starts at ContentLeft — Inset in from the frame — so Inset is the LEFT margin and
            // SidePad the right; double-counting Inset (once here, once via ContentLeft) opens a
            // gap down the left of the whole block, since the frame is anchored on its RIGHT
            // edge and grows leftwards.
            float col1Origin = Inset + StepFor(withDeltas) + Col1ShiftConst;  // where the draw puts it
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
        // computed FROM the plan, so deriving the plan back from the width would be circular.
        // What reads as margin around the picture is mostly the plan's own empty space — a ship
        // rarely fills its square — so the way to recover it is to shrink the square, not to
        // chase a margin that is not there.
        const float PlanSide = 220f;
        const float PlanGap = 6f;

        // How far this panel is inset inside its frame. The screen builds the inner rect, so it
        // tells us here rather than us guessing a constant that would have to stay in step.
        // ⚠ THE inset, and the only one — callers read this constant instead of each carrying
        // their own; separate copies for Active and Hover can silently drift apart.
        public const float Inset = 10f;
        public float InnerInset = Inset;

        // Where content starts: the inner rect's own left edge, full stop. The rect is already
        // inset inside the frame, so that IS the margin — nothing to back out and nothing to add.
        // One edge for the title, the columns and the ship plan, which also draws at X — any
        // other origin here puts the text on a different left edge from the plan.
        float ContentLeft => X;

        // Whether this frame reserves room for delta lanes. A property of the FRAME, not of the
        // moment: the Active cartouche always keeps the room whether or not a design is pinned,
        // exactly as the module panel does, so pinning never moves a single number. The Hover
        // frame never compares, so it gives the space to its columns instead.
        public bool HasDeltaLanes;

        // Ludoal fork: the Hover cartouche draws the design's MODULE PLAN at its far left — the
        // picture the flying overlay used to give, and the reason to keep a hover frame at all.
        // Same call the overlay made: RenderOverlay with the modules and the hull background.
        // Only the hover frame turns this on.
        public bool ShowShipPlan;

        // ── COMPACT ────────────────────────────────────────────────
        // The flying overlay's own inventory instead of Lek's eight blocks: one column, no
        // headings, the headline five with their icons then the verbose list. The Active
        // cartouche wears it at the browser list's width; the Hover one keeps its plan.
        public bool Compact;

        // measured over the compact titles exactly as LongestTitle is over the full set.
        // Localized lazily (LowerIsBetterCache's pattern): the set now speaks the full
        // rows' own labels - the ETM/OTM dialect stays with the micro overlay only.
        static string[] CompactTitlesCache;
        static string[] CompactTitles => CompactTitlesCache ??= new[]
        {
            Localizer.Token(GT.ShipOffense), "DPS", "Weapons", "Hangars", "Bomb Bays",
            "Max Wpn Range",
            Localizer.Token(GT.WpnFirePowerTime), Localizer.Token(GT.AmmoTime),
            Localizer.Token(GT.TroopCapacity), Localizer.Token(GT.CargoSpace),
            Localizer.Token(GT.TotalHitpoints), Localizer.Token(GT.ShieldPower),
            Localizer.Token(GT.RepairRate), Localizer.Token(GT.EmpProtection),
            Localizer.Token(GT.FtlSpeed), Localizer.Token(GT.FtlTime),
            Localizer.Token(GT.SublightSpeed), Localizer.Token(GT.TurnRate),
        };
        static float LongestCompactCache;
        static float LongestCompactTitle
        {
            get
            {
                if (LongestCompactCache <= 0f)
                    foreach (string t in CompactTitles)
                    {
                        float w = Fonts.Arial12Bold.TextWidth(t);
                        if (w > LongestCompactCache)
                            LongestCompactCache = w;
                    }
                return LongestCompactCache;
            }
        }
        static float CompactTitleColumn => 10f + 20f + LongestCompactTitle - Inset;

        // what a compact frame needs when it is free to hug (the hover frame) - the Active
        // one is pinned to the browser list's width by the screen instead
        public static float CompactFrameWidthFor(bool withPlan)
            => Inset + CompactTitleColumn + DeltaLaneWidth + SidePad
             + (withPlan ? PlanSide + PlanGap - 10f : 0f); // hover window trimmed 10

        // re-derive the rows after a Compact flip - the shadow follows, or the delta lookup
        // would match titles across two different row sets and find nothing
        public void RebuildRows()
        {
            if (S != null)
                SetActiveDesign(S, UpdateDesignStats ? null : Ds);
            if (CompareAgainst != null)
            {
                CompareAgainst.Compact = Compact;
                CompareAgainst.RebuildRows();
            }
        }

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
            shadow.Compact = Compact; // same row set, or the title lookup finds nothing
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

            }
        }

        public override void Update(float fixedDeltaTime)
        {
            if (UpdateDesignStats && S != null)
                Ds.Update(S.Universe.Player);

            base.Update(fixedDeltaTime);
        }

        // ── the compact set: the FULL rows' own definitions, filtered - same titles, same
        // lambdas, the densities only select. One headingless column in the operational order:
        // what the ship inflicts, what it takes, how it moves - three blocks split by air. The
        // ETM/OTM dialect retires to the micro overlay, where a two-letter label is the right coin.
        void BuildCompactRows()
        {
            Color good = Color.White;
            Color energy = Color.LightSkyBlue;
            Color protect = Color.Goldenrod;
            Color engines = Color.DarkSeaGreen;
            Color ordnance = Color.IndianRed;

            // COMBAT - the two verdicts first, then the guns, then the firing reserves.
            // One merged fire-power line: a beam boat reads its burst duration (the old
            // compact's own rule), under the full row's label.
            Stat(GT.ShipOffense, () => Ds.Strength, GT.TT_ShipOffense, nonZero: true);
            Stat("DPS", () => S.TotalDps, GT.TT_ShipOffense, nonZero: true, icon: "UI/icon_offense", iconColor: Color.OrangeRed);
            Stat("Weapons", () => S.Weapons.Count, GT.TT_ShipOffense, nonZero: true);
            // the carrier and bomber armament, right under the guns
            Stat("Hangars", () => S.Carrier.AllFighterHangars.Length, GT.TT_ShipOffense, nonZero: true);
            Stat("Bomb Bays", () => S.BombBays.Count, GT.TT_ShipOffense, nonZero: true);
            Stat("Max Wpn Range", () => S.WeaponsMaxRange, GT.TT_ShipOffense, nonZero: true);
            Stat(GT.WpnFirePowerTime, () => Ds.HasBeams() ? Ds.BurstEnergyDuration : Ds.EnergyDuration, GT.TT_WpnFirePowerTime, energy,
                 vis: () => Ds.HasEnergyWeapons && (Ds.HasBeams() ? Ds.HasBeamDurationNegative() : Ds.HasEnergyWepsPositive()),
                 icon: "UI/lightningBolt", iconColor: Color.LightGoldenrodYellow);
            Word(GT.WpnFirePowerTime, "INF", GT.TT_WpnFirePowerTime, energy, good,
                 vis: () => Ds.HasEnergyWeapons && (Ds.HasBeams() ? Ds.HasBeamDurationPositive() : Ds.HasEnergyWepsNegative()),
                 icon: "UI/lightningBolt", iconColor: Color.LightGoldenrodYellow);
            Stat(GT.AmmoTime, () => Ds.AmmoTime, GT.TT_AmmoTime, ordnance, tint: Above(30f), vis: Ds.HasOrdFinite, icon: "Modules/Ordnance", iconColor: Color.Khaki);
            Word(GT.AmmoTime, "INF", GT.TT_AmmoTime, ordnance, good, vis: Ds.HasOrdInfinite, icon: "Modules/Ordnance", iconColor: Color.Khaki);

            Gap();

            // DEFENCE - the full set's rows, verbatim
            Stat(GT.TotalHitpoints, () => S.Health, GT.TT_HitPoints, protect, tint: Positive, icon: "UI/icon_shield", iconColor: Color.CadetBlue);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, protect, tint: Positive, vis: Ds.HasRegularShields, icon: "Modules/Shield_1KW", iconColor: Color.AliceBlue);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, Color.Gold, tint: Positive, vis: Ds.HasAmplifiedMains, icon: "Modules/Shield_1KW", iconColor: Color.AliceBlue);
            Stat(GT.RepairRate, () => S.RepairRate, GT.TT_RepairRate, protect, tint: Positive, nonZero: true);
            Stat(GT.EmpProtection, () => S.EmpTolerance, GT.TT_EmpProtection, protect, tint: Positive);

            Gap();

            // MOBILITY - a platform or station shows none of it, and the gap folds with it
            Stat(GT.FtlSpeed, () => S.MaxFTLSpeed, GT.TT_FtlSpeed, engines, tint: Above(20_000f), vis: Ds.IsWarpCapable);
            if (!S.IsPlatformOrStation)
            {
                Stat(GT.FtlTime, () => Ds.WarpTime, GT.TT_FtlTime, engines, tint: Positive, vis: Ds.HasFiniteWarp);
                Word(GT.FtlTime, "INF", GT.TT_FtlTime, engines, good, vis: Ds.HasInfiniteWarp);
            }
            Stat(GT.SublightSpeed, () => S.MaxSTLSpeed, GT.TT_SublightSpeed, engines, tint: Above(50f),
                 vis: () => !S.IsPlatformOrStation);
            Stat(GT.TurnRate, () => S.RotationRadsPerSecond.ToDegrees(), GT.TT_TurnRate, engines, tint: Above(15f),
                 vis: () => !S.IsPlatformOrStation);

            Gap();

            // PAYLOAD, role-adaptive: a warship carries neither, so the whole block - air
            // included - folds away on its own
            Stat(GT.TroopCapacity, () => S.TroopCapacity, GT.TT_TroopCapacity, ordnance, nonZero: true);
            Stat(GT.CargoSpace, () => S.CargoSpaceMax, GT.TT_CargoSpace, nonZero: true);
        }

        void Gap() => Rows.Add(new Row { Gap = true });

        void TextRow(in LocalizedText title, Func<string> text, in LocalizedText tip, Func<bool> vis = null)
            => Rows.Add(new Row { Title = title, Tip = tip, TextFn = text, Color = LabelGrey, Visible = vis });

        // ── the content, per Lek's spec v3 ────────────────────────────────────────────────
        void BuildRows()
        {
            if (Compact)
            {
                BuildCompactRows();
                return;
            }

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
            // Ludoal fork: the five figures the load popup marks with an icon get the same icon
            // here, inline and scaled to the line. Both variants of a row carry it, or it would
            // blink away on the INF case.
            Stat(GT.WpnFirePowerTime, () => Ds.EnergyDuration, GT.TT_WpnFirePowerTime, energy, tint: Above(2f), vis: Ds.HasEnergyWepsPositive, icon: "UI/lightningBolt", iconColor: Color.LightGoldenrodYellow);
            Word(GT.WpnFirePowerTime, "INF", GT.TT_WpnFirePowerTime, energy, good, vis: Ds.HasEnergyWepsNegative, icon: "UI/lightningBolt", iconColor: Color.LightGoldenrodYellow);
            Stat(GT.BurstWpnPwrDrain, () => -Ds.PowerConsumedWithBeams, GT.TT_BurstWpnPwerDrain, energy, vis: Ds.HasBeams);
            Stat(GT.BurstWpnPwrTime, () => Ds.BurstEnergyDuration, GT.TT_BurstWpnPwrTime, energy, tint: _ => Color.LightPink, vis: Ds.HasBeamDurationNegative);
            Word(GT.BurstWpnPwrTime, "INF", GT.TT_BurstWpnPwrTime, energy, good, vis: Ds.HasBeamDurationPositive);

            // Ludoal fork: MOBILITY before DEFENCE. Reading order follows the columns — the left
            // one runs CONSTRUCTION, ENERGY, MOBILITY, which is what the ship IS, while the
            // right one carries what it does in a fight.
            Head("MOBILITY");
            Stat(GT.FtlSpeed, () => S.MaxFTLSpeed, GT.TT_FtlSpeed, engines, tint: Above(20_000f), vis: Ds.IsWarpCapable);
            // FTL time right under the speed it belongs to
            if (!S.IsPlatformOrStation)
            {
                Stat(GT.FtlTime, () => Ds.WarpTime, GT.TT_FtlTime, engines, tint: Positive, vis: Ds.HasFiniteWarp);
                Word(GT.FtlTime, "INF", GT.TT_FtlTime, engines, good, vis: Ds.HasInfiniteWarp);
            }
            // Ludoal fork: a platform or a station has no engine, so these two are a pair of
            // zeroes and the whole block goes with them - a heading with nothing under it is
            // never drawn, so hiding the rows hides MOBILITY itself.
            Stat(GT.SublightSpeed, () => S.MaxSTLSpeed, GT.TT_SublightSpeed, engines, tint: Above(50f),
                 vis: () => !S.IsPlatformOrStation);
            Stat(GT.TurnRate, () => S.RotationRadsPerSecond.ToDegrees(), GT.TT_TurnRate, engines, tint: Above(15f),
                 vis: () => !S.IsPlatformOrStation);

            // Ludoal fork: STATION and PAYLOAD sit in the LEFT column, in that order, because
            // STATION BELONGS WITH MOBILITY - a station is a ship that does not move
            // (IsResearchStation requires IsPlatformOrStation, so a real one has no engine at
            // all), and what it refines and what it carries answer the same question its speed
            // does. Reading them one under the other says that.
            // ⚠ these two blocks show on PRODUCTION, not on the station role: nothing stops a
            // research lab going on a mobile hull, so they are not proof the ship is a station.
            // The right column loses its two rarest blocks and stops overflowing as a bonus.
            Head("STATION");
            Stat(GT.ResearchPerTurn, () => S.ResearchPerTurn, GT.ResearchPerTurnStatTip, nonZero: true);
            Stat(GT.ResearchStationResearchTimeStat, () => Ds.ResearchTime(), GT.ResearchStationResearchTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodResearchSupply), vis: Ds.ProducesResearch);
            Stat(GT.RefinningPerTurnStat, () => S.TotalRefining, GT.RefiningPerTurnStatTip, nonZero: true);
            Stat(GT.MiningStationRefiningTimeStat, () => Ds.RefiningTime(), GT.MiningStationRefiningTimeStatTip,
                 tint: Above(ShipResupply.NumTurnsForGoodRefiningSupply - 0.01f), vis: Ds.RefinesResources);

            Head("PAYLOAD");
            Stat(GT.TroopCapacity, () => S.TroopCapacity, GT.TT_TroopCapacity, ordnance, nonZero: true);
            Stat(GT.CargoSpace, () => S.CargoSpaceMax, GT.TT_CargoSpace, nonZero: true);

            Head("DEFENCE");
            Stat(GT.TotalHitpoints, () => S.Health, GT.TT_HitPoints, protect, tint: Positive, icon: "UI/icon_shield", iconColor: Color.CadetBlue);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, protect, tint: Positive, vis: Ds.HasRegularShields, icon: "Modules/Shield_1KW", iconColor: Color.AliceBlue);
            Stat(GT.ShieldPower, () => S.ShieldMax, GT.TT_ShieldPower, Color.Gold, tint: Positive, vis: Ds.HasAmplifiedMains, icon: "Modules/Shield_1KW", iconColor: Color.AliceBlue);
            Stat(GT.ShieldAmplify, () => (int)S.Stats.ShieldAmplifyPerShield, GT.TT_ShieldAmplify, protect, tint: Positive, nonZero: true);
            Stat(GT.RepairRate, () => S.RepairRate, GT.TT_RepairRate, protect, tint: Positive, nonZero: true);
            // the tooltip promises the TOTAL protection of the design, and the load-list
            // overlay already shows EmpTolerance - show the same effective value here
            Stat(GT.EmpProtection, () => S.EmpTolerance, GT.TT_EmpProtection, protect, tint: Positive);
            Stat(GT.Ecm3, () => S.ECMValue, GT.TT_Ecm3, protect, tint: Positive, nonZero: true);

            // the ordnance family was missing from the inventory sent to Lek, so its placement
            // is mine: it sits with the guns it feeds, which is where her v1 put ammo time
            // Ludoal fork: split into ORDNANCE and FCS - the two halves answer different
            // questions, how long can it keep shooting versus how well does it aim, and reading
            // them as one list made neither obvious. Sensor Range sits with FCS rather than
            // earning a third heading: it is detection, not fire control, but a block of its own
            // would cost a line and the air around it for a single row.
            Head("ORDNANCE");
            Stat(GT.OrdnanceCreated, () => S.OrdAddedPerSecond, GT.TT_OrdnanceCreated, ordnance, nonZero: true);
            Stat(GT.OrdnanceCapacity, () => S.OrdinanceMax, GT.TT_OrdnanceCap, ordnance, vis: Ds.HasOrdnance);
            Stat(GT.AmmoTime, () => Ds.AmmoTime, GT.TT_AmmoTime, ordnance, tint: Above(30f), vis: Ds.HasOrdFinite, icon: "Modules/Ordnance", iconColor: Color.Khaki);
            Word(GT.AmmoTime, "INF", GT.TT_AmmoTime, ordnance, good, vis: Ds.HasOrdInfinite, icon: "Modules/Ordnance", iconColor: Color.Khaki);

            Head("FCS");
            Stat(GT.FireControl, () => S.TargetingAccuracy, GT.TT_FireControl, nonZero: true);
            Stat(GT.FcsPower, () => S.TrackingPower, GT.TT_FcsPower, nonZero: true);
            Stat(GT.SensorRange3, () => S.SensorRange, GT.TT_SensorRange3, nonZero: true);

            // the closing block: the verdict reads last, like a signature
            // Ludoal fork: COMBAT takes in the three figures the load popup showed that this
            // panel otherwise would not - a design cartouche should say whether the ship shoots.
            // Raw strings: these three have no GameText key, exactly as the load overlay wrote
            // them. Max range only, not the avg..max pair - on a ship mixing a short-range laser
            // with a long-range cannon that pair describes neither of them.
            Head("COMBAT");
            Stat("Weapons", () => S.Weapons.Count, GT.TT_ShipOffense, nonZero: true);
            Stat("Max Wpn Range", () => S.WeaponsMaxRange, GT.TT_ShipOffense, nonZero: true);
            Stat("DPS", () => S.TotalDps, GT.TT_ShipOffense, nonZero: true, icon: "UI/icon_offense", iconColor: Color.OrangeRed);
            Stat(GT.ShipOffense, () => Ds.Strength, GT.TT_ShipOffense, nonZero: true);
            Stat(GT.RelativeStrength, () => Ds.RelativeStrength, GT.TT_RelativeStrength, nonZero: true);
        }

        // Ludoal fork: block headings are all cream. They are structure, not content —
        // colouring each one after its family would make the panel read as eight unrelated
        // lists instead of one. The per-block colour still tints the stat TITLES, which is
        // where it carries meaning.
        void Head(string heading) => Rows.Add(new Row { Heading = heading, Color = Colors.Cream });

        void Stat(in LocalizedText title, Func<float> value, in LocalizedText tip, Color? titleColor = null,
                  Func<float, Color> tint = null, Func<bool> vis = null, bool nonZero = false,
                  string icon = null, Color? iconColor = null)
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
                Icon = icon, IconColor = iconColor ?? Color.White,
            });
        }

        void Word(in LocalizedText title, string text, in LocalizedText tip, Color titleColor,
                  Color valueColor, Func<bool> vis = null, string icon = null, Color? iconColor = null)
        {
            Rows.Add(new Row
            {
                Title = title, Tip = tip, Text = text,
                Color = LabelGrey, Tint = _ => valueColor, Visible = vis,
                Icon = icon, IconColor = iconColor ?? Color.White,
            });
        }

        // Ludoal fork: colour is an ATTENTION GETTER, not a status light. A value that is fine
        // reads white; only a value below its threshold — or negative where it should not be —
        // goes pink. A permanent green would say nothing and compete with the delta lane, which
        // uses the same two colours to mean something else entirely.
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

        // Ludoal fork: no comparison on an element a design does not have - a row either exists
        // on this design or it is not drawn at all.
        bool IsDrawn(int index) => IsVisible(Rows[index]);

        // ── the draw, two columns, split on a block boundary ──────────────────────────────
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            if (S == null)
                return;

            // Ludoal fork: the design name sits INSIDE the frame, on the module panel's pattern.
            // The pinned design has no frame to name it, so its name follows, one size down: the
            // delta lane must never come from an anonymous source.
            if (S.Name.NotEmpty())
            {
                Graphics.Font nameFont = Fonts.Arial20Bold;
                if (nameFont.TextWidth(S.Name) + 40f > Width)
                    nameFont = Fonts.Arial14Bold;

                // The module frame draws its title at (frame.X + 10). This panel is the INNER
                // rect, already inset by 10, so drawing at X would put the name 20px off the
                // frame — twice the module's margin. Back out the inset so the two agree.
                // Ludoal fork: same reasoning vertically as horizontally above. The module panel
                // draws its title at frame.Y + 35; this inner rect starts at frame.Y + 26, so the
                // offset here is derived from the module's number, not re-guessed.
                const float ModuleTitleFromFrame = 35f;
                const float InnerTopInset = 26f;
                var namePos = new Vector2(ContentLeft + (ShowShipPlan ? PlanSide + PlanGap : 0f),
                                          Y + (ModuleTitleFromFrame - InnerTopInset));
                // compact hover: the name centres on the WHOLE frame, image band included
                if (Compact && ShowShipPlan)
                    namePos.X = X + (Width - nameFont.TextWidth(S.Name)) * 0.5f;
                batch.DrawString(nameFont, S.Name, namePos, Color.White);

                if (ComparedName.NotEmpty())
                {
                    Graphics.Font vsFont = Fonts.Arial12Bold;
                    // compact: line 2, under the name - beside it there is no room
                    var vsPos = Compact
                        ? new Vector2(ContentLeft + (ShowShipPlan ? PlanSide + PlanGap : 0f),
                                      namePos.Y + nameFont.LineSpacing + 2f)
                        : new Vector2(namePos.X + nameFont.TextWidth(S.Name) + 10f,
                                      namePos.Y + nameFont.LineSpacing - vsFont.LineSpacing - 2f);
                    string vs = "vs " + ComparedName;
                    batch.DrawString(vsFont, vs, vsPos, Colors.Cream);

                    // Ludoal fork: a way out of the comparison that does not require finding the
                    // pinned design again to shift-click it a second time. It lives on the "vs"
                    // line because that is where the comparison announces itself, it costs no
                    // layout, and it only exists while there is something to cancel.
                    CancelCompareRect = new RectF(vsPos.X + vsFont.TextWidth(vs) + 6f, vsPos.Y,
                                                  vsFont.LineSpacing, vsFont.LineSpacing);
                    bool hot = CancelCompareRect.HitTest(Screen.Input.CursorPosition);
                    batch.DrawString(vsFont, "x",
                                     new Vector2(CancelCompareRect.X + 3f, CancelCompareRect.Y),
                                     hot ? Color.White : Color.Gray);
                    if (hot)
                        ToolTip.CreateTooltip("Cancel the comparison");
                }
                else
                {
                    CancelCompareRect = default;
                }
            }

            // Column origins are hand-tuned. Titles are right-aligned and grow leftward, which
            // is why the longest ones ("Excess Wpn Pwr Drain") need the slack.
            // The rows now start below the in-frame title, at a FIXED offset so every design —
            // long name or short, compared or not — puts its first row on the same line.
            float rowsY = Y + (Compact ? CompactTitleBand : TitleBandHeight);

            // the ship plan owns a square band down the frame's left edge; the stat columns
            // share whatever is left of the width
            float planW = 0f;
            if (ShowShipPlan)
            {
                // fixed square, but never taller than the frame can hold (a windowed player can
                // squash the screen well below the supported floor)
                float side = Math.Min(PlanSide, Height - TitleBandHeight - 10f);
                planW = PlanSide + PlanGap; // the columns keep their place even if the square shrinks
                // the plan rides 30px right - the frame's left margin would otherwise be dead surface
                const float PlanShift = 30f;
                S.RenderOverlay(batch, new Rectangle((int)(X + PlanShift), (int)rowsY, (int)side, (int)side),
                                showModules: true, drawHullBackground: true,
                                moduleHealthColor: false, markLockedModules: true);

                // Ludoal fork: the design's own two settings, under the picture and WITHOUT
                // labels, exactly as the load popup states them - "Civilian, Evade" says itself.
                // They belong to the hover frame only: the shipyard already shows them as
                // controls for the design on the workbench.
                if (S.ShipData != null)
                {
                    string settings = $"{S.ShipData.ShipCategory}, {S.ShipData.DefaultCombatState}";
                    // centred under the picture and in white: grey reads as disabled, and left
                    // aligned it would float away from the square it belongs to
                    float w = Fonts.Arial12Bold.TextWidth(settings);
                    batch.DrawString(Fonts.Arial12Bold, settings,
                                     new Vector2(X + PlanShift + (side - w) * 0.5f, rowsY + side + 6f), Color.White);
                }
            }

            // The delta lane is ALWAYS reserved, pinned or not — the module panel's principle,
            // and the reason its columns never move. Reserving it only while a comparison runs
            // makes the layout shift on every pin, and squeezes the deltas onto the right-hand
            // column's labels.
            // BOTH columns carry a delta lane, so both must be paid for — subtracting one lane
            // for two columns leaves the second overlapping its neighbour's labels. The first
            // column looks right regardless, because it starts at the left edge and absorbs none
            // of the error.
            // The FIRST column keeps its place and its title room whether or not deltas are on:
            // its geometry comes from the frame's half-width, exactly as it did before lanes
            // existed. Only the SECOND column moves right, past the first one's delta lane.
            // The MODULE panel's geometry, copied instead of re-derived: fixed steps, not
            // divisions. There, column 0 starts at the frame's left margin and column 1 a
            // constant step further right; the title room is a fraction of the FRAME, never of
            // the column; and the delta lane is simply the space left between the step and the
            // next column, neither reserved nor subtracted. A panel that divides the available
            // width instead moves something else each time that width changes.
            float colStep = ColumnStep;
            // ⚠ the columns start where the TITLE starts, and the title is measured from the
            // FRAME (X - InnerInset + 10), not from this inner rect. Same expression as
            // namePos, so the two cannot part company.
            float col0X = ContentLeft + planW + Col0Shift;
            // the compact column rides right - 30px when a delta lane is in use, 40px when
            // single (hover included)
            if (Compact)
                col0X += CompareAgainst != null ? 30f : 40f;
            float col1X = col0X + colStep + Col1Shift - Col0Shift;
            // the compact set is ONE headingless column with its own, tighter title room
            float spacing = Compact ? CompactTitleColumn : TitleColumn;
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

            // Ludoal fork: the split is FIXED by block, not balanced by line count. Balancing
            // would put MOBILITY left on one design and right on the next, since a design with
            // no shields or no ordnance shifts the halfway mark - the reader would have to find
            // each block again on every ship.
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
            const int FixedSplitBlock = 5;   // CONSTRUCTION ENERGY MOBILITY STATION PAYLOAD
            int splitBlock = Math.Min(FixedSplitBlock, blockVisible.Count);

            var cursor = new Vector2(col0X, rowsY);
            int block = -1;
            bool firstHeadingOfColumn = true;
            bool pendingGap = false, anyRowDrawn = false; // headingless block breaks (compact)
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
                    // titles share one vertical line
                    float headRight = cursor.X + spacing - 20f;
                    batch.DrawString(headFont, r.Heading,
                                     new Vector2(headRight - headFont.TextWidth(r.Heading), cursor.Y), r.Color);
                    continue;
                }

                // a headingless block break: the air is paid only when a later row draws,
                // so a fully hidden block (MOBILITY on a station) folds its gap with it
                if (r.Gap)
                {
                    pendingGap = true;
                    continue;
                }

                // block < 0: the compact set has no headings at all
                if ((block >= 0 && blockVisible[block] == 0) || !IsDrawn(i))
                    continue;

                if (pendingGap && anyRowDrawn)
                    cursor.Y += headFont.LineSpacing * 0.5f; // same air as between headed blocks
                pendingGap = false;
                anyRowDrawn = true;

                if (r.TextFn != null)
                {
                    Screen.DrawStatText(ref cursor, r.Title, r.TextFn(), r.Color, r.Tip, spacing,
                                        valueColor: null, icon: r.Icon, iconColor: r.IconColor);
                }
                else if (r.Text != null)
                {
                    Screen.DrawStatText(ref cursor, r.Title, r.Text, r.Color, r.Tip, spacing,
                                        valueColor: r.Tint?.Invoke(0f), icon: r.Icon, iconColor: r.IconColor);
                }
                else
                {
                    float v = r.Value();
                    Screen.DrawStatText(ref cursor, r.Title, v.GetNumberString(), r.Color, r.Tip, spacing,
                                        valueColor: r.Tint?.Invoke(v), icon: r.Icon, iconColor: r.IconColor);

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
