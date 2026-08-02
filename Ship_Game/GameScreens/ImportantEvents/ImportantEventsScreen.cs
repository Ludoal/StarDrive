using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.ExtensionMethods;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    // Permanent log of Important notifications (empire defeat, merge/surrender,
    // remnant story progression), opened from the minimap. Styled after ShipDesignIssuesScreen.
    public sealed class ImportantEventsScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu GalaxyTabs;   // Ludoal fork: the Galaxy group's tab row, this screen being one tab

        void OnGalaxyTabChanged(int index)
            => GameScreens.ReworkScreens.SwitchGalaxyTab(index, self: 3, Universe, this);

        readonly Color Cream = Colors.Cream;
        readonly ImportantNotification[] Events;
        readonly ScrollList<ImportantEventListItem> EventList;
        readonly Graphics.Font LargeFont = Fonts.Arial20Bold;

        public ImportantEventsScreen(UniverseScreen screen) : base(screen, toPause: null)
        {
            Universe          = screen;
            Events            = screen.UState.GetImportantEvents();
            IsPopup           = true;
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;

            // Ludoal fork: the Events tab of the Galaxy group - the centred 1200x540 window gives
            // way to the frame and tab row its three siblings share.
            Rectangle frame = GameScreens.ReworkScreens.GroupFrame(ScreenWidth, ScreenHeight);
            GalaxyTabs = Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height),
                                         GameScreens.ReworkScreens.GalaxyTabTitles));
            GalaxyTabs.OnTabChange = OnGalaxyTabChanged;
            GalaxyTabs.PerformLayout();
            GalaxyTabs.SelectedIndex = 3;

            Vector2 closePos = GameScreens.ReworkScreens.GroupClosePos(GalaxyTabs.ClientArea);
            Add(new CloseButton(closePos.X, closePos.Y));

            RectF client = GalaxyTabs.ClientArea;
            RectF table  = GameScreens.ReworkScreens.GalaxyTable(client);
            int x  = (int)table.X;
            int y  = (int)table.Y + 20;   // the column headers sit on the line above the list
            int w  = (int)table.W;
            int h  = (int)table.H - 20;

            EventList = Add(new ScrollList<ImportantEventListItem>(new RectF(x, y, w, h), 80));
            EventList.EnableItemHighlight = true;

            UILabel starDateLabel    = Add(new UILabel("Star Date", LargeFont, Cream));
            UILabel titleLabel       = Add(new UILabel("Title", LargeFont, Cream));
            UILabel descriptionLabel = Add(new UILabel("Description", LargeFont, Cream));
            starDateLabel.Size       = new Vector2(120, 20);
            titleLabel.Size          = new Vector2(230, 20);
            descriptionLabel.Size    = new Vector2(700, 20);
            starDateLabel.Pos        = new Vector2(x + 60, y - 10);
            titleLabel.Pos           = new Vector2(x + 190, y - 10);
            descriptionLabel.Pos     = new Vector2(x + 430, y - 10);
            starDateLabel.TextAlign    = TextAlign.HorizontalCenter;
            titleLabel.TextAlign       = TextAlign.HorizontalCenter;
            descriptionLabel.TextAlign = TextAlign.HorizontalCenter;
        }

        void PopulateEvents()
        {
            // newest first
            for (int i = Events.Length - 1; i >= 0; --i)
                EventList.AddItem(new ImportantEventListItem(Events[i]));
        }

        public override void LoadContent()
        {
            // the close cross and the screen's name come from the group's tab row now
            PopulateEvents();
            base.LoadContent();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame is filled by hand before its children, the way every screen
            // in this group does - the group's frame is transparent, so the map showed through.
            batch.FillRectangle(GalaxyTabs.Rect, GameScreens.ReworkScreens.GroupFrameFill);
            base.Draw(batch, elapsed);
            Universe.EmpireUI.Draw(batch);   // the live top bar, as on its sibling tabs
            GameScreens.ReworkScreens.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.ImportantEventsScreen && !GlobalStats.TakingInput) // Ludoal fork: F7 toggles the screen
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }
            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }
    }
}
