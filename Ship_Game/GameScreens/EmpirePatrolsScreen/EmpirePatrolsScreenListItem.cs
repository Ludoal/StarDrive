using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Audio;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI; // UITable: the shared table charte
using Ship_Game.Fleets;

namespace Ship_Game
{
    // one patrol plan = one row, on the shared table charte: the row only carries its
    // data and its two action buttons, sized to their own text (maintainer, 4 Aug)
    public sealed class EmpirePatrolsScreenListItem : ScrollListItem<EmpirePatrolsScreenListItem>
    {
        public readonly FleetPatrol FleetPatrol;
        UIButton RenamePatrol;
        UIButton DeletePatrol;
        readonly EmpirePatrolsScreen Screen;
        readonly Empire Player;

        public EmpirePatrolsScreenListItem(EmpirePatrolsScreen screen, FleetPatrol fleetPatrol, Empire player)
        {
            Screen = screen;
            FleetPatrol = fleetPatrol;
            Player = player;
        }

        public override void PerformLayout()
        {
            int y = (int)Y;
            int h = (int)Height;
            RemoveAll();

            UITable.Column[] cols = Screen.Table.Columns;
            Array<string> fleetsAssigned = GetFleetsAssignedText();
            // white, not the empire colour (maintainer bench 290): only ONE empire's plans
            // ever list here, the race tint carried no information - gray stays for the
            // plans no fleet runs
            Color color = fleetsAssigned.Count == 0 ? Color.Gray : Color.White;

            Cell(cols[0], FleetPatrol.Name, color);
            Cell(cols[1], FleetPatrol.WayPoints.Count.ToString(), color);
            Cell(cols[2], fleetsAssigned.Count.ToString(), color);
            // foldable: cut to the column, the tooltip carries the full list
            string joined = string.Join(", ", fleetsAssigned);
            string shown = UITable.FitText(Fonts.Arial12Bold, joined, cols[3].Rect.Width - 2 * UITable.PadX);
            var fleetsLbl = Cell(cols[3], shown, color);
            if (shown != joined)
                fleetsLbl.Tooltip = joined;

            // the Actions lane: both buttons sized to their text, side by side
            RenamePatrol = Button(ButtonStyle.Default, "Rename", OnRenamePatrolClicked);
            DeletePatrol = Button(ButtonStyle.Military, "Delete", OnDeletePatrolClicked);
            const int BtnH = 24;
            int bx = cols[4].Rect.X + UITable.PadX;
            RenamePatrol.Rect = new RectF(bx, y + h / 2 - BtnH / 2, Screen.RenameBtnW, BtnH);
            DeletePatrol.Rect = new RectF(bx + Screen.RenameBtnW + 8, y + h / 2 - BtnH / 2, Screen.DeleteBtnW, BtnH);

            base.PerformLayout();
        }

        UILabel Cell(UITable.Column c, string text, Color color)
        {
            return Label(UITable.CellPos(Fonts.Arial12Bold, c.Rect, Y, Height, text, c.Align),
                         text, Fonts.Arial12Bold, color);
        }

        Array<string> GetFleetsAssignedText()
        {
            Array<string> fleets = new();
            foreach (Fleet fleet in Player.AllFleets)
            {
                if (fleet.HasPatrolPlan && fleet.Patrol.Name == FleetPatrol.Name)
                    fleets.Add(fleet.Name);
            }

            return fleets;
        }

        void OnDeletePatrolClicked(UIButton b)
        {
            GameAudio.EchoAffirmative();
            Screen.ScreenManager.AddScreen(new MessageBoxScreen(Screen, "This will permanently remove the Patrol Plan from your Empire's database and from any fleets assigned to it as well.")
            {
                Accepted = () => Screen.DeletePatrol(FleetPatrol)
            });
        }

        void OnRenamePatrolClicked(UIButton b)
        {
            GameAudio.EchoAffirmative();
            Screen.ScreenManager.AddScreen(new RenamePatrolPlanScreen(Screen, FleetPatrol));
        }
    }
}
