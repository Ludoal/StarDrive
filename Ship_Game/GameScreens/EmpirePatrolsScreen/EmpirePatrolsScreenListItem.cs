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
    // data and its two action buttons, sized to their own text
    public sealed class EmpirePatrolsScreenListItem : ScrollListItem<EmpirePatrolsScreenListItem>
    {
        public readonly FleetPatrol FleetPatrol;
        // icons, not text buttons - the same pencil/bin language the build queues speak
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
            // white, not the empire colour: only ONE empire's plans ever list here, so the
            // race tint carries no information - gray stays for plans no fleet runs
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

            // the pencil and the bin sit right of the name, using the build queues' own
            // icon language. The lit art is the resting state here; the base icons read
            // darker than the Colonies pair.
            RenamePatrol ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_build_edit_hover1", "NewUI/icon_build_edit_hover2", "NewUI/icon_build_edit_hover2"), Vector2.Zero, "")
            {
                Tooltip = "Rename this patrol plan",
                OnClick = OnRenamePatrolClicked,
            };
            DeletePatrol ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2", "NewUI/icon_queue_delete_hover2"), Vector2.Zero, "")
            {
                Tooltip = "Delete this patrol plan",
                OnClick = OnDeletePatrolClicked,
                IconTint = Color.Red, // destruction reads red
            };
            SubTexture editTex = ResourceManager.Texture("NewUI/icon_build_edit_hover1");
            SubTexture delTex = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            // Ludoal fork: the pencil and bin live in their own Actions column (the last one),
            // centred as a pair within its cell.
            Rectangle actions = cols[cols.Length - 1].Rect;
            int pairW = editTex.Width + 8 + delTex.Width;
            int editX = actions.X + (actions.Width - pairW) / 2;
            int delX  = editX + editTex.Width + 8;
            RenamePatrol.Rect = new Rectangle(editX, y + h / 2 - editTex.Height / 2, editTex.Width, editTex.Height);
            DeletePatrol.Rect = new Rectangle(delX, y + h / 2 - delTex.Height / 2, delTex.Width, delTex.Height);

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
            RenamePatrol.Draw(batch, elapsed);
            DeletePatrol.Draw(batch, elapsed);
        }

        public override bool HandleInput(InputState input)
        {
            // actions live on the buttons' OnClick; a consumed press stops the row underneath
            if (RenamePatrol.HandleInput(input))
                return true;
            if (DeletePatrol.HandleInput(input))
                return true;
            return base.HandleInput(input);
        }

        void OnDeletePatrolClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new MessageBoxScreen(Screen, "This will permanently remove the Patrol Plan from your Empire's database and from any fleets assigned to it as well.")
            {
                Accepted = () => Screen.DeletePatrol(FleetPatrol)
            });
        }

        void OnRenamePatrolClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new RenamePatrolPlanScreen(Screen, FleetPatrol));
        }
    }
}
