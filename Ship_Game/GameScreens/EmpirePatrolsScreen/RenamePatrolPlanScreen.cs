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
        UIButton CancelButton;
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
            PatrolNameEntry = Add(new UITextEntry(inner.X + 15, inner.Y + 12, 200, Fonts.Arial20Bold, FleetPatrol.Name));
            PatrolNameEntry.AutoCaptureOnHover = true;
            PatrolNameEntry.AutoCaptureOnKeys = true;
            PatrolNameEntry.MaxCharacters = 40;
            PatrolNameEntry.OnTextChanged = OnPatrolNameTextChanged;
            PatrolNameEntry.Background = new Submenu(new RectF(PatrolNameEntry.X-10, PatrolNameEntry.Y-3, PatrolNameEntry.Width+220, PatrolNameEntry.Height+6));
            NameAlreadyExistsLabel = Add(new UILabel(inner.X + 15, PatrolNameEntry.Y + 40, GameText.PatrolNameAlreadyExists));
            NameAlreadyExistsLabel.Color = Color.Red;
            NameAlreadyExistsLabel.Visible = false;
            RenameButton = ButtonMedium(inner.X + 15, inner.Bottom - 40, GameText.RenamePatrol, OnRenameClicked);
            CancelButton = ButtonBigDip(inner.X + 165, inner.Bottom - 40, GameText.RenamePatrol, OnCancelClicked);
            RenameButton.Enabled = false;
            RenameButton.Text = "Rename";
            CancelButton.Text = "Cancel";
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

        void OnCancelClicked(UIButton b)
        {
            ExitScreen();
        }
    }
}
