using System;
using System.Linq;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.GameScreens;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (maintainer feedback): an ORDER given once to every colony - a base to work
    // from, refined colony by colony afterwards. Not a policy: nothing is remembered here and a
    // colony founded later receives nothing, which is why it lives behind a button rather than
    // in the rules page.
    //
    // TWO windows, ONE mechanic: the page that opens it decides the subject, so the other half
    // would be dead weight on screen. They differ by Mode and by nothing else - a second class
    // would be the same gesture written twice, and two places to fix the day it moves.
    //
    // ⚠ THE WINDOW OWNS NO VALUE. Its sliders are pens: after an order the truth lives in the
    // colonies, and the counter on each row reads them back. A window holding a figure of its
    // own would sooner or later disagree with the eighteen colonies underneath it.
    public sealed class GovernorOrdersScreen : PopupWindow
    {
        public enum Mode { Budget, Defense }

        readonly UniverseScreen Universe;
        readonly Mode Kind;
        Empire Player => Universe.Player;

        FloatSlider CivRail, GrdRail, SpcRail, GarrisonRail;
        UICheckBox GovOrbitalsBox;
        bool WantGovOrbitals;

        const int PopupW = 520, BudgetH = 280, DefenseH = 300;
        const int RowH = 34, RailW = UITable.PurseRailWidth, LabelW = 150;

        public GovernorOrdersScreen(GameScreen summoner, UniverseScreen u, Mode kind)
            : base(summoner, PopupW, kind == Mode.Budget ? BudgetH : DefenseH)
        {
            Universe = u;
            Kind = kind;
            TitleText = kind == Mode.Budget ? "Set all colony budgets" : "Set all colony defenses";
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        // the order runs on the simulation, like every write from a page
        void ForEachColony(Action<Planet> act)
        {
            Universe.RunOnSimThread(() =>
            {
                foreach (Planet p in Player.GetPlanets())
                    act(p);
            });
        }

        // ⚠ the ceiling comes from the colonies, not from a number chosen here: a rail whose top
        // is a guess is useless on a small empire and too coarse on a large one
        float RailMax(BudgetArea area)
        {
            var owned = Player.GetPlanets();
            float top = 0f;
            for (int i = 0; i < owned.Count; ++i)
                top = Math.Max(top, owned[i].BudgetAutoTarget(area));
            return (top * 2f).LowerBound(20f);
        }

        string BudgetCount(BudgetArea area)
        {
            var owned = Player.GetPlanets();
            int manual = 0;
            for (int i = 0; i < owned.Count; ++i)
                if (owned[i].IsBudgetManual(area)) ++manual;
            return $"{owned.Count - manual} Auto · {manual} manual";
        }

        string GarrisonCount()
        {
            var owned = Player.GetPlanets();
            int auto = 0;
            for (int i = 0; i < owned.Count; ++i)
                if (owned[i].AutoBuildTroops) ++auto;
            return $"{auto} Auto · {owned.Count - auto} manual";
        }

        string GovOrbitalsCount()
        {
            var owned = Player.GetPlanets();
            int on = 0;
            for (int i = 0; i < owned.Count; ++i)
                if (owned[i].GovOrbitals) ++on;
            return $"{on} on · {owned.Count - on} off";
        }

        // one row: its name, its rail, and what the colonies say right now
        FloatSlider Row(string name, float y, float max, float seed, Func<string> count)
        {
            Add(new UILabel(new Vector2(Rect.X + 20, y + 2), name, Fonts.Arial12Bold, Colors.Cream));
            var rail = Add(new FloatSlider(SliderStyle.Decimal1,
                                           new Rectangle(Rect.X + 20 + LabelW, (int)y, RailW, 12),
                                           "", 0f, max, seed));
            rail.TrackYOffset = 0;
            rail.ValueLane = 34;
            var tally = Add(new UILabel(l => count(), Fonts.Arial12));
            tally.Pos = new Vector2(Rect.X + 20 + LabelW + RailW + 12, y + 2);
            tally.Color = Color.Gray;
            return rail;
        }

        public override void LoadContent()
        {
            base.LoadContent();
            const int CornerW = 28;
            Color fill = ScreenGroups.GroupFrameFill;
            Add(new UIPanel(BottomBigFill, fill));
            Add(new UIPanel(new Rectangle(Rect.X + CornerW, BottomBigFill.Bottom,
                                          Rect.Width - 2 * CornerW,
                                          Rect.Bottom - PopupFrame.BottomLine - BottomBigFill.Bottom), fill));

            float y = BodyTop + 14;
            if (Kind == Mode.Budget)
            {
                CivRail = Row("Civilian", y, RailMax(BudgetArea.Civilian), 0f,
                              () => BudgetCount(BudgetArea.Civilian));
                GrdRail = Row("Ground Defense", y += RowH, RailMax(BudgetArea.GroundDef), 0f,
                              () => BudgetCount(BudgetArea.GroundDef));
                SpcRail = Row("Space Defense", y += RowH, RailMax(BudgetArea.SpaceDef), 0f,
                              () => BudgetCount(BudgetArea.SpaceDef));

                float by = y + RowH + 10;
                // ⚠ "Set" says (manual) out loud because it does two things: taking a purse over
                // RESTARTS it from what the governor was allocating, so a level posed on a colony
                // still on Auto would be wiped the moment it goes manual. The button cannot pose
                // one without the other and stay honest.
                var set = Button(ButtonStyle.DefaultActive, Rect.X + 20, by, "Set all (manual)",
                                 click: _ => ApplyBudgets());
                set.Tooltip = "Applies this level to every colony and takes it off Auto. Taking manual "
                            + "control restarts from what the governor was allocating, so a level set "
                            + "while on Auto would be lost.";
                var auto = Button(ButtonStyle.Default, Rect.X + 20 + 190, by, "Auto all",
                                  click: _ => ForEachColony(p =>
                                  {
                                      p.SetBudgetManual(BudgetArea.Civilian, false);
                                      p.SetBudgetManual(BudgetArea.GroundDef, false);
                                      p.SetBudgetManual(BudgetArea.SpaceDef, false);
                                      p.Budget?.SnapToTarget();
                                  }));
                auto.Tooltip = "Hands all three purses back to the governors, on every colony.";
            }
            else
            {
                GarrisonRail = Row("Garrison", y, DefenseListItem.MaxGarrison, 0f, GarrisonCount);

                float by = y + RowH + 10;
                // ⚠ three buttons, one grandeur each, and no overlap: a garrison level is simply
                // stored and serves when auto-training is on, so unlike a purse it can be posed
                // without deciding the mode.
                var set = Button(ButtonStyle.DefaultActive, Rect.X + 20, by, "Set",
                                 click: _ => ForEachColony(p => p.GarrisonSize = (int)GarrisonRail.AbsoluteValue));
                set.Tooltip = "Applies this garrison level to every colony without changing Auto / Manual.";
                Button(ButtonStyle.Default, Rect.X + 20 + 120, by, "Auto",
                       click: _ => ForEachColony(p => p.AutoBuildTroops = true))
                    .Tooltip = "Turns auto-training on for every colony. The level each one keeps is untouched.";
                Button(ButtonStyle.Default, Rect.X + 20 + 240, by, "Manual",
                       click: _ => ForEachColony(p => p.AutoBuildTroops = false))
                    .Tooltip = "Turns auto-training off for every colony. The level each one keeps is untouched.";

                float gy = by + 44;
                WantGovOrbitals = true;
                GovOrbitalsBox = Add(new UICheckBox(Rect.X + 20, gy,
                                                    () => WantGovOrbitals, v => WantGovOrbitals = v,
                                                    Fonts.Arial12Bold, "Gov. Manages Space Defense",
                                                    GameText.DvDefenseSpaceDefTip));
                Button(ButtonStyle.Default, Rect.X + 20 + 240, gy - 4, "Set",
                       click: _ => ForEachColony(p => p.GovOrbitals = WantGovOrbitals))
                    .Tooltip = "Gives every colony the state of the box beside it.";
                var tally = Add(new UILabel(l => GovOrbitalsCount(), Fonts.Arial12));
                tally.Pos = new Vector2(Rect.X + 20 + LabelW + RailW + 12, gy);
                tally.Color = Color.Gray;
            }
        }

        void ApplyBudgets()
        {
            float civ = CivRail.AbsoluteValue, grd = GrdRail.AbsoluteValue, spc = SpcRail.AbsoluteValue;
            ForEachColony(p =>
            {
                p.SetBudgetAmount(BudgetArea.Civilian, civ);
                p.SetBudgetManual(BudgetArea.Civilian, true);
                p.SetBudgetAmount(BudgetArea.GroundDef, grd);
                p.SetBudgetManual(BudgetArea.GroundDef, true);
                p.SetBudgetAmount(BudgetArea.SpaceDef, spc);
                p.SetBudgetManual(BudgetArea.SpaceDef, true);
            });
        }
    }
}
