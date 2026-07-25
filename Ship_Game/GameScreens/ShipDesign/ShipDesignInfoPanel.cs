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
    /// Displays STATS information of the currently active design
    /// </summary>
    public class ShipDesignInfoPanel : UIElementContainer
    {
        Ship S;
        ShipDesignStats Ds;
        bool UpdateDesignStats = false;

        UIList StatsList;
        float ItemHeight = 11;
        float ValueWidth = 80;
        float TitleWidth;
        Graphics.Font StatsFont = Fonts.Arial11Bold;

        Array<(UIElementV2, Func<bool>)> DynamicVisibility = new Array<(UIElementV2, Func<bool>)>();

        public ShipDesignInfoPanel(in Rectangle rect) : base(rect)
        {
        }

        public void SetActiveDesign(Ship ship, ShipDesignStats ds = null)
        {
            Elements.Clear();
            DynamicVisibility.Clear();

            // block-heading state is per rebuild, not per panel: without this reset the second
            // design loaded would open with a stray spacer above its first heading
            CurrentHeader = null;
            CurrentHeaderSpacer = null;
            CurrentHeaderLines = new Array<Func<bool>>();
            HasBlocks = false;

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
                CreateElements();
            }
        }

        void CreateElements()
        {
            // Ludoal fork: two columns, like the module panel. Grouping by block added eight
            // headings and their spacers, which made a single column taller than its rect —
            // the content spilled onto the issues panel below. Splitting halves the height.
            ColWidth = Width * 0.5f - 4;
            ValueWidth = 60; // the value column narrows with the column itself
            TitleWidth = ColWidth - ValueWidth;

            LeftList = Add(new UIList(Pos, new Vector2(ColWidth, Height)));
            LeftList.Padding = Vector2.Zero;
            LeftList.SetLocalPos(0, 0);

            RightList = Add(new UIList(Pos, new Vector2(ColWidth, Height)));
            RightList.Padding = Vector2.Zero;
            RightList.SetLocalPos(ColWidth + 8, 0);

            StatsList = LeftList;

            Color good = Color.LightGreen;
            Color energy = Color.LightSkyBlue;
            Color protect = Color.Goldenrod;
            Color engines = Color.DarkSeaGreen;
            Color ordnance = Color.IndianRed;

            // Ludoal fork — grouped by DECISION, per Lek's spec v3. Every line of the old flat
            // list is preserved, only moved: she ruled "fold, don't prune", because the panel
            // already hides its null lines, so the real worst case is a station and nobody
            // loses a tuning tool. Block titles and order do the readability work.
            // Two named calls of hers: costs FIRST (at the shipyard the first question is how
            // much), and FTL Time moved to MOBILITY (it reads as travel endurance, next to the
            // speeds, not as a power figure).

            Header("CONSTRUCTION", Color.Wheat);
            Val(() => S.GetCost(), GT.ProductionCost, GT.TT_ProductionCost, Tint.Pos);
            Val(() => S.GetMaintCost(), GT.UpkeepCost, GT.TT_UpkeepCost, Tint.Pos);
            Val(() => S.SurfaceArea, GT.TotalModuleSlots, GT.TT_TotalModuleSlots, Tint.Pos);
            Val(() => S.Mass, GT.Mass, GT.TT_Mass, Tint.Pos);

            Header("ENERGY", energy);
            Val(() => Ds.PowerCapacity, GT.PowerCapacity, GT.TT_PowerCapacity, Tint.No, energy, col: ColGreater(() => Ds.PowerConsumed));
            Val(() => Ds.PowerRecharge, GT.PowerRecharge, GT.TT_PowerRecharge, Tint.Pos, energy);
            Val(() => Ds.ChargeAtWarp, GT.RechargeAtWarp, GT.TT_RechargeAtWarp, Tint.Pos, energy, vis: Ds.IsWarpCapable);

            Val(() => -Ds.PowerConsumed, GT.ExcessWpnPwrDrain, GT.TT_ExcessWpnPwrDrain, Tint.No, energy, vis: Ds.HasEnergyWepsPositive);
            Val(() => Ds.EnergyDuration, GT.WpnFirePowerTime, GT.TT_WpnFirePowerTime, Tint.Two, energy, vis: Ds.HasEnergyWepsPositive);
            Val("INF", GT.WpnFirePowerTime, GT.TT_WpnFirePowerTime, Tint.No, energy, good, vis: Ds.HasEnergyWepsNegative);

            Val(() => -Ds.PowerConsumedWithBeams, GT.BurstWpnPwrDrain, GT.TT_BurstWpnPwerDrain, Tint.No, energy, vis: Ds.HasBeams);
            Val(() => Ds.BurstEnergyDuration, GT.BurstWpnPwrTime, GT.TT_BurstWpnPwrTime, Tint.Bad, energy, vis: Ds.HasBeamDurationNegative);
            Val("INF", GT.BurstWpnPwrTime, GT.TT_BurstWpnPwrTime, Tint.No, energy, good, vis: Ds.HasBeamDurationPositive);

            Header("DEFENCE", protect);
            Val(() => S.Health, GT.TotalHitpoints, GT.TT_HitPoints, Tint.Pos, protect);
            Val(() => S.ShieldMax, GT.ShieldPower, GT.TT_ShieldPower, Tint.Pos, protect, vis: Ds.HasRegularShields);
            Val(() => S.ShieldMax, GT.ShieldPower, GT.TT_ShieldPower, Tint.Pos, Color.Gold, vis: Ds.HasAmplifiedMains);
            ValNZ(() => (int)S.Stats.ShieldAmplifyPerShield, GT.ShieldAmplify, GT.TT_ShieldAmplify, Tint.Pos, protect);
            ValNZ(() => S.RepairRate, GT.RepairRate, GT.TT_RepairRate, Tint.Pos, protect);
            // the tooltip promises the TOTAL protection of the design, and the load-list
            // overlay already shows EmpTolerance - show the same effective value here
            Val(() => S.EmpTolerance, GT.EmpProtection, GT.TT_EmpProtection, Tint.Pos, protect);
            ValNZ(() => S.ECMValue, GT.Ecm3, GT.TT_Ecm3, Tint.Pos, protect);

            SecondColumn();
            Header("MOBILITY", engines);
            Val(() => S.MaxFTLSpeed, GT.FtlSpeed, GT.TT_FtlSpeed, Tint.No, engines, vis: Ds.IsWarpCapable, col: ColGreater(20_000));
            Val(() => S.MaxSTLSpeed, GT.SublightSpeed, GT.TT_SublightSpeed, Tint.No, engines, col: ColGreater(50));
            Val(() => S.RotationRadsPerSecond.ToDegrees(), GT.TurnRate, GT.TT_TurnRate, Tint.No, engines, col: ColGreater(15));
            if (!S.IsPlatformOrStation)
            {
                Val(() => Ds.WarpTime, GT.FtlTime, GT.TT_FtlTime, Tint.Pos, engines, vis: Ds.HasFiniteWarp);
                Val("INF", GT.FtlTime, GT.TT_FtlTime, Tint.No, engines, good, vis: Ds.HasInfiniteWarp);
            }

            // the ordnance family was missing from the inventory I sent her, so its placement
            // is mine: it sits with the guns it feeds, which is also where her v1 put ammo time
            Header("COMBAT & FIRE CONTROL", Color.Orange);
            ValNZ(() => S.OrdAddedPerSecond, GT.OrdnanceCreated, GT.TT_OrdnanceCreated, Tint.No, ordnance);
            Val(() => S.OrdinanceMax, GT.OrdnanceCapacity, GT.TT_OrdnanceCap, Tint.No, ordnance, vis: Ds.HasOrdnance);
            Val(() => Ds.AmmoTime, GT.AmmoTime, GT.TT_AmmoTime, Tint.No, ordnance, vis: Ds.HasOrdFinite, col: ColGreater(30));
            Val("INF", GT.AmmoTime, GT.TT_AmmoTime, Tint.No, ordnance, good, vis: Ds.HasOrdInfinite);
            ValNZ(() => S.TargetingAccuracy, GT.FireControl, GT.TT_FireControl);
            ValNZ(() => S.TrackingPower, GT.FcsPower, GT.TT_FcsPower);
            ValNZ(() => S.SensorRange, GT.SensorRange3, GT.TT_SensorRange3);

            Header("PAYLOAD", ordnance);
            ValNZ(() => S.TroopCapacity, GT.TroopCapacity, GT.TT_TroopCapacity, Tint.No, ordnance);
            ValNZ(() => S.CargoSpaceMax, GT.CargoSpace, GT.TT_CargoSpace);

            Header("STATION", Color.MediumPurple);
            ValNZ(() => S.ResearchPerTurn, GT.ResearchPerTurn, GT.ResearchPerTurnStatTip);
            Val(Ds.ResearchTime, GT.ResearchStationResearchTimeStat, GT.ResearchStationResearchTimeStatTip,
                Tint.No, Color.White, vis: Ds.ProducesResearch, col: ColGreater(ShipResupply.NumTurnsForGoodResearchSupply));

            ValNZ(() => S.TotalRefining, GT.RefinningPerTurnStat, GT.RefiningPerTurnStatTip);
            Val(Ds.RefiningTime, GT.MiningStationRefiningTimeStat, GT.MiningStationRefiningTimeStatTip,
                Tint.No, Color.White, vis: Ds.RefinesResources, col: ColGreater(ShipResupply.NumTurnsForGoodRefiningSupply-0.01f));

            // her closing block: the verdict reads last, like a signature
            Header("ASSESSMENT", Color.White);
            ValNZ(() => Ds.Strength, GT.ShipOffense, GT.TT_ShipOffense);
            ValNZ(() => Ds.RelativeStrength, GT.RelativeStrength, GT.TT_RelativeStrength);

            CloseHeader(); // the last block has no successor to close it
        }

        // Ludoal fork: block titles. A block whose every line is hidden would otherwise leave
        // a bare heading behind — a fighter has no STATION and no PAYLOAD — so a heading (and
        // the spacer above it) is visible only while at least one of its own lines is.
        UIList LeftList, RightList;
        float ColWidth;
        UILabel CurrentHeader;
        UI.UISpacer CurrentHeaderSpacer;
        Array<Func<bool>> CurrentHeaderLines = new Array<Func<bool>>();
        bool HasBlocks;

        void Header(in LocalizedText text, Color color)
        {
            CloseHeader();

            if (HasBlocks) // no spacer above the very first block
            {
                CurrentHeaderSpacer = new UI.UISpacer(ColWidth, ItemHeight - 3);
                StatsList.Add(CurrentHeaderSpacer);
            }
            HasBlocks = true;

            CurrentHeader = new UILabel(Vector2.Zero, text, StatsFont, color)
            {
                Width = ColWidth,
                Height = ItemHeight + 2,
            };
            StatsList.Add(CurrentHeader);
            CurrentHeaderLines = new Array<Func<bool>>();
        }

        // Ludoal fork: everything built after this call lands in the right-hand column.
        // Closing the pending heading first is what keeps the left column's last block
        // able to hide itself.
        void SecondColumn()
        {
            CloseHeader();
            StatsList = RightList;
            HasBlocks = false; // no spacer above the column's first heading
        }

        void CloseHeader()
        {
            if (CurrentHeader == null)
                return;

            Func<bool>[] lines = CurrentHeaderLines.ToArray();
            bool AnyLineVisible()
            {
                for (int i = 0; i < lines.Length; ++i)
                    if (lines[i]())
                        return true;
                return false;
            }

            DynamicVisibility.Add((CurrentHeader, AnyLineVisible));
            if (CurrentHeaderSpacer != null)
                DynamicVisibility.Add((CurrentHeaderSpacer, AnyLineVisible));

            CurrentHeader = null;
            CurrentHeaderSpacer = null;
        }

        UI.UIKeyValueLabel Val(Func<float> dynamicValue, LocalizedText title, LocalizedText tooltip, 
                                 Tint tint = Tint.No, Color? titleColor = null,  Color? valueColor = null,
                                 Func<float, Color> col = null, Func<bool> vis = null, LocalizedText? valueText = null)
        {
            var lbl = new UI.UIKeyValueLabel(title, valueText ?? "11.11k", titleColor, valueColor)
            {
                Separator = ":     ",
                Width = ColWidth,
                Split = TitleWidth,
                DynamicValue = dynamicValue,
                Tooltip = tooltip,
                Color = col ?? (tint != Tint.No ? Tinted(tint) : null),
                Height = ItemHeight,
            };

            lbl.Key.TextAlign = TextAlign.Right;
            lbl.Key.Width = TitleWidth;
            lbl.Key.Font = lbl.Value.Font = StatsFont;
            if (vis != null)
                DynamicVisibility.Add((lbl, vis));

            // a line always visible counts as visible for its block heading
            CurrentHeaderLines.Add(vis ?? (() => true));
            //lbl.DebugDraw = true;
            StatsList.Add(lbl);
            return lbl;
        }

        UI.UIKeyValueLabel Val(LocalizedText valueText, LocalizedText title, LocalizedText tooltip, 
                               Tint tint = Tint.No, Color? titleColor = null,  Color? valueColor = null,
                               Func<float, Color> col = null, Func<bool> vis = null)
        {
            return Val(null, title, tooltip, tint, titleColor, valueColor, col, vis, valueText);
        }

        // Displays the dynamicValue if it's Greater than 0
        UI.UIKeyValueLabel ValNZ(Func<float> dynamicValue, LocalizedText title, LocalizedText tooltip, 
                                      Tint tint = Tint.No, Color? titleColor = null,  Color? valueColor = null,
                                      Func<float, Color> col = null, LocalizedText? valueText = null)
        {
            Func<bool> vis = () => dynamicValue() > 0;
            return Val(dynamicValue, title, tooltip, tint, titleColor, valueColor, col, vis, valueText);
        }

        void Line()
        {
            StatsList.Add(new UI.UISpacer(ColWidth, ItemHeight - 3));
        }

        enum Tint
        {
            No, // no tint
            Bad, // this value is bad
            Pos, // value must be positive
            One, // must be greater than 1
            Two, // must be greater than 2
        }

        Func<float, Color> Tinted(Tint tint)
        {
            return (v) =>
            {
                switch (tint)
                {
                    default: case Tint.No:   return Color.White;
                    case Tint.Bad: return Color.LightPink;
                    case Tint.Pos: return v > 0f ? Color.LightGreen : Color.LightPink;
                    case Tint.One: return v > 1f ? Color.LightGreen : Color.LightPink;
                    case Tint.Two: return v > 2f ? Color.LightGreen : Color.LightPink;
                }
            };
        }

        // value must be greater than compareValue()
        Func<float, Color> ColGreater(Func<float> compareValue)
        {
            return (v) => v > compareValue() ? Color.LightGreen : Color.LightPink;
        }

        Func<float, Color> ColGreater(float compareValue)
        {
            return (v) => v > compareValue ? Color.LightGreen : Color.LightPink;
        }

        public override void Update(float fixedDeltaTime)
        {
            if (UpdateDesignStats)
                Ds.Update(S.Universe.Player);

            // Toggle which items are visible
            foreach ((UIElementV2 item, Func<bool> visibility) in DynamicVisibility)
            {
                bool visible = visibility();
                if (item.Visible != visible)
                {
                    item.Visible = visible;
                    StatsList.RequiresLayout = true;
                }
            }
            
            base.Update(fixedDeltaTime);
        }
    }
}
