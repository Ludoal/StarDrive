using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Audio;
using SDGraphics;
using SDGraphics.Input; // InputState
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
        // icons, not text buttons (Lek's review + maintainer, bench 305) - the same
        // pencil/bin language the build queues speak
        TexturedButton RenamePatrol;
        TexturedButton DeletePatrol;
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

            // the pencil and the bin RIGHT OF THE NAME (maintainer, bench 305) - the build
            // queues' own icon language, and the Actions column retired with its width
            // the LIT art as the resting state (maintainer bench 307): the base icons read
            // darker here than the Colonies pair
            RenamePatrol = new TexturedButton(new Rectangle(), "NewUI/icon_build_edit_hover1", "NewUI/icon_build_edit_hover2", "NewUI/icon_build_edit_hover2");
            RenamePatrol.Tooltip = "Rename this patrol plan";
            DeletePatrol = new TexturedButton(new Rectangle(), "NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2", "NewUI/icon_queue_delete_hover2");
            DeletePatrol.Tooltip = "Delete this patrol plan";
            DeletePatrol.BaseColor = Color.Red; // destruction reads red (maintainer bench 305)
            SubTexture editTex = ResourceManager.Texture("NewUI/icon_build_edit_hover1");
            SubTexture delTex = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            // Ludoal fork (maintainer feedback): the pencil and bin sit at the right end of the
            // columns but LEFT of the slider lane - the icons used to run under the scrollbar. The
            // bin hugs the last column's right edge, the pencil to its left, both stepping inward.
            int delX  = Screen.Table.TableRect.Right - delTex.Width - 4;
            int editX = delX - 8 - editTex.Width;
            RenamePatrol.r = new Rectangle(editX, y + h / 2 - editTex.Height / 2, editTex.Width, editTex.Height);
            DeletePatrol.r = new Rectangle(delX, y + h / 2 - delTex.Height / 2, delTex.Width, delTex.Height);

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

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            RenamePatrol.Draw(batch);
            DeletePatrol.Draw(batch);
        }

        public override bool HandleInput(InputState input)
        {
            if (RenamePatrol.HandleInput(input))
            {
                OnRenamePatrolClicked(null);
                return true;
            }
            if (DeletePatrol.HandleInput(input))
            {
                OnDeletePatrolClicked(null);
                return true;
            }
            return base.HandleInput(input);
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
