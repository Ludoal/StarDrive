using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Fleets;

namespace Ship_Game
{
    public sealed class RenamePatrolPlanScreen : PopupWindow
    {
        readonly EmpirePatrolsScreen Screen;
        readonly FleetPatrol FleetPatrol;
        UITextEntry PatrolNameEntry;
        UIButton RenameButton;
        UILabel NameAlreadyExistsLabel;

        public RenamePatrolPlanScreen(EmpirePatrolsScreen screen, FleetPatrol fleetPatrol)
            : base(screen, 500, 240)
        {
            Screen = screen;
            FleetPatrol = fleetPatrol;
            TransitionOnTime = 0.25f;
        }

        public override void LoadContent()
        {
            // the window names itself in its own title bar; frame and close cross are
            // PopupWindow's - base.LoadContent goes FIRST and lays them out
            TitleText = "Change Patrol Plan Name";
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            // Ludoal fork (maintainer feedback): a little more air around the name field, and no
            // Cancel button - the frame's close cross top-right dismisses the dialog.
            PatrolNameEntry = Add(new UITextEntry(inner.X + 20, inner.Y + 22, 200, Fonts.Arial20Bold, FleetPatrol.Name));
            PatrolNameEntry.AutoCaptureOnHover = true;
            PatrolNameEntry.AutoCaptureOnKeys = true;
            PatrolNameEntry.MaxCharacters = 40;
            PatrolNameEntry.OnTextChanged = OnPatrolNameTextChanged;
            PatrolNameEntry.Background = new Submenu(new RectF(PatrolNameEntry.X-14, PatrolNameEntry.Y-8, PatrolNameEntry.Width+228, PatrolNameEntry.Height+16));
            NameAlreadyExistsLabel = Add(new UILabel(inner.X + 20, PatrolNameEntry.Y + 48, GameText.PatrolNameAlreadyExists));
            NameAlreadyExistsLabel.Color = Color.Red;
            NameAlreadyExistsLabel.Visible = false;
            RenameButton = ButtonMedium(inner.X + 20, inner.Bottom - 40, GameText.RenamePatrol, OnRenameClicked);
            RenameButton.Enabled = false;
            RenameButton.Text = "Rename";
        }

        void OnPatrolNameTextChanged(string newName)
        {
            if (FleetPatrol.Name == newName)
            {
                RenameButton.Enabled = false;
                return;
            }

            if (Screen.Player.FleetPatrols.Any(p => p.Name == newName))
            {
                NameAlreadyExistsLabel.Visible = true;
                RenameButton.Enabled = false;
            }
            else
            {
                NameAlreadyExistsLabel.Visible = false;
                RenameButton.Enabled = true;
            }
        }

        void OnRenameClicked(UIButton b)
        {
            Screen.RenamePatrol(FleetPatrol, PatrolNameEntry.Text);
            ExitScreen();
        }
    }
}
