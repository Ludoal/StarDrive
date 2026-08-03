using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.ExtensionMethods;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI;

namespace Ship_Game
{
    public sealed class SearchTechScreen : PopupWindow
    {
        readonly ResearchScreenNew Screen;
        ScrollList<SearchTechItem> TechList;
        UITextEntry SearchTech;

        public SearchTechScreen(ResearchScreenNew screen) : base(screen, 400, (int)(GameBase.ScreenHeight * 0.8f))
        {
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;
            Screen = screen;
        }

        public override void LoadContent()
        {
            // the window names itself in its own title bar; the frame and the close cross are
            // PopupWindow's - base.LoadContent goes FIRST and lays them out
            Rect = new Rectangle(Rect.X, Rect.Y, 400, (int)(ScreenHeight * 0.8f));
            TitleText = Localizer.Token(GameText.SearchTechnology);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);

            RectF techList = new(inner.X + 10, inner.Y + 36, inner.Width - 20, inner.Bottom - (inner.Y + 36));
            TechList = Add(new SubmenuScrollList<SearchTechItem>(techList, 125, ListStyle.Blue)).List;
            TechList.OnClick = (item) => ResearchToTech(item.Tech);

            Rectangle rect = new RectF(inner.X + 10, inner.Y + 6, inner.Width - 20, 20);
            SearchTech = Add(new UITextEntry(rect.Bevel(-4, -2), Fonts.Arial12Bold,
                                             GameText.StartTypingToFindTechs));
            SearchTech.Background = new Submenu(rect, SubmenuStyle.Blue);
            SearchTech.Color = Color.AliceBlue;
            SearchTech.MaxCharacters = 14;
            SearchTech.OnTextChanged = (text) => PopulateTechs(text.ToLower());
            SearchTech.AutoCaptureOnKeys = true;
            SearchTech.AutoClearTextOnInputCapture = true;

            PopulateTechs("");
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // base.Draw paints the window frame and every child inside its own batch
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }

        void PopulateTechs(string keyword)
        {
            TechList.Reset();
            var items = new Array<SearchTechItem>();

            foreach (TechEntry entry in Screen.Universe.Player.TechEntries)
            {
                TreeNode node = new(Vector2.Zero, entry, Screen);
                if (entry.Discovered && !entry.IsRoot &&
                    (keyword.IsEmpty() || node.TechName.ToLower().Contains(keyword)))
                {
                    items.Add(CreateQueueItem(node));
                }
            }

            TechList.SetItems(items);
            TechList.RequiresLayout = true;
        }

        SearchTechItem CreateQueueItem(TreeNode node)
        {
            var defaultPos = new Vector2(Rect.X + 5, Rect.Y);
            return new(Screen, node, defaultPos) { List = TechList };
        }

        void ResearchToTech(TechEntry entry)
        {
            if (!entry.CanBeResearched || Screen.Player.Research.IsQueued(entry.UID))
            {
                GameAudio.NegativeClick();
                return;
            }

            var entries = new Array<TechEntry> {entry};
            while (!entry.IsRoot)
            {
                TechEntry parent = entry.GetPreReq(Screen.Player);
                if (parent.Unlocked || Screen.Player.Research.IsQueued(entry.UID))
                    break;

                if (!parent.IsRoot)
                    entries.Add(parent);

                entry = parent;
            }

            for (int i = entries.Count-1; i >= 0; i--)
            {
                TechEntry te = entries[i];
                Screen.Queue.AddToResearchQueue(te);
            }
        }
    }
}
