using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.ExtensionMethods;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens.ShipDesign
{
    public sealed class ShipDesignIssuesScreen : GameScreen
    {
        private readonly Rectangle Window;
        private readonly Color Cream = Colors.Cream;
        private readonly Array<DesignIssueDetails> DesignIssues;
        private readonly ScrollList<ShipDesignIssuesListItem> IssueList;
        private readonly Graphics.Font LargeFont = Fonts.Arial20Bold;

        public ShipDesignIssuesScreen(GameScreen screen, Array<DesignIssueDetails> issues) : base(screen, toPause: null)
        {
            DesignIssues      = issues;
            IsPopup           = true;
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;

            // Centred on the Shipyard FRAME, not the screen - at wide resolutions (or Full Screen
            // off) the two centres diverge.
            Rectangle frame = GameScreens.ScreenGroups.GroupFrame(ScreenWidth, ScreenHeight, ShipDesignScreen.FullScreenDesign);
            Window = new Rectangle(frame.CenterX() - 600, frame.CenterY() - 270, 1200, 540);
            int x  = (int)Window.X + 20;
            int y  = (int)Window.Y + 70;
            int w  = (int)Window.Width - 30;
            int h  = (int)Window.Height - 80;

            IssueList = Add(new ScrollList<ShipDesignIssuesListItem>(new RectF(x, y, w, h), 80));
            IssueList.EnableItemHighlight = true;
            //IssueList.DebugDrawScrollList = true;
            //IssueList.DebugDraw = true;

            UILabel designIssueLabel = Add(new UILabel(GameText.SyDesignIssue, LargeFont, Cream));
            UILabel descriptionLabel = Add(new UILabel(GameText.SyIssueDescription, LargeFont, Cream));
            UILabel remediationLabel = Add(new UILabel(GameText.SyRemediation, LargeFont, Cream));
            designIssueLabel.Size    = new Vector2(230, 20);
            descriptionLabel.Size    = new Vector2(470, 20);
            remediationLabel.Size    = new Vector2(470, 20);
            designIssueLabel.Pos     = new Vector2(x, y - 10);
            descriptionLabel.Pos     = new Vector2(x + 180, y - 10);
            remediationLabel.Pos     = new Vector2(x + 650, y - 10);
            designIssueLabel.TextAlign   = TextAlign.HorizontalCenter;
            descriptionLabel.TextAlign   = TextAlign.HorizontalCenter;
            remediationLabel.TextAlign   = TextAlign.HorizontalCenter;
        }

        void PopulateIssues()
        {
            foreach (DesignIssueDetails details in DesignIssues)
            {
                var d = new ShipDesignIssuesListItem(details);
                IssueList.AddItem(d);
            }

            IssueList.SortDescending(item => item.IssueDetails.Severity);
        }

        public override void LoadContent()
        {
            CloseButton(Window.Right - 40, Window.Y + 20);
            //Screen Title
            // Default popup title font, ALL CAPS, centred VERTICALLY in the title bar by the
            // bar's own metrics.
            string title    = "CURRENT SHIP ISSUES";
            float titleY    = Window.Y + PopupFrame.TitleBarTop + (PopupFrame.TitleBarHeight - UITheme.WindowTitle.LineSpacing) / 2f;
            Vector2 menuPos = new Vector2(Window.CenterTextX(title, UITheme.WindowTitle), titleY);
            Label(menuPos, title, UITheme.WindowTitle, Cream);
            PopulateIssues();
            base.LoadContent();
        }

         public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // the popup window's surface, drawn in place before the children
            var frame = new PopupFrame(Window);
            frame.DrawFill(batch, Window);
            frame.Draw(batch);
            base.Draw(batch, elapsed);
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (input.DesignIssues && !GlobalStats.TakingInput) // rebindable (bench 434)
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