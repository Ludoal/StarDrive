using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.UI;
using Ship_Game.Universe;

namespace Ship_Game.GameScreens.NewGame
{
    // Ludoal fork: a PopupWindow. It was a floating title bar plus a separate list frame, two
    // pieces of furniture for one window - and both were see-through over Race Design behind it
    // (maintainer observation). One frame, opaque, titled, with the close cross the class supplies.
    public class SelectOpponentsScreen : PopupWindow
    {
        public readonly UniverseParams Params;
        ScrollList<SelectOpponentListItem> ChooseRaceList;
        readonly IEmpireData PlayerData;
        UILabel RandomOpponentsCount;

        // 450x620: the list's own width, and a height that held the same rows the old
        // ScreenHeight*0.6 gave at 900 - a constant now, so the window does not resize with the
        // display while the rows inside it stay 135 tall.
        public SelectOpponentsScreen(GameScreen parent, UniverseParams p, IEmpireData selectedData)
            : base(parent, 500, 620)
        {
            TitleText = "Select Opponents";
            TransitionOnTime = 0.75f;
            TransitionOffTime = 0.25f;
            Params = p;
            PlayerData = selectedData;
        }

        public override void LoadContent()
        {
            // ⚠ base.LoadContent() lays the frame out and calls RemoveAll(), so it goes FIRST -
            // this method used to call it last, which was harmless with hand-built frames and is
            // not with a PopupWindow: everything below would be discarded.
            base.LoadContent();

            // the window IS the frame now: no title bar of its own, no list frame, no close cross
            Rectangle rect = Rect;
            int top = PopupFrame.ContentTop(rect);
            RectF background = new(rect.X + 12, top + 30, rect.Width - 24, rect.Bottom - 20 - (top + 30));
            ChooseRaceList = Add(new ScrollList<SelectOpponentListItem>(background, 135));
            ChooseRaceList.OnClick = OnRaceItemSelected;
            ChooseRaceList.OnDoubleClick = OnRaceItemSelected;
            // the count sits between the title bar and the list, in the strip left for it
            RandomOpponentsCount = Add(new UILabel(
                               new Rectangle(rect.X + 30, top + 2, 200, 30),
                                              "", Fonts.Arial20Bold, Color.White));
            IEmpireData[] majorRaces = ResourceManager.MajorRaces.Filter(
                                data => data.ArchetypeName != PlayerData.ArchetypeName);
            foreach (IEmpireData e in majorRaces)
                ChooseRaceList.AddItem(new SelectOpponentListItem(this, e));
        }

        public override bool HandleInput(InputState input)
        {
            if (input.RightMouseClick && ChooseRaceList.HitTest(input.CursorPosition))
                return true;

            return base.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // base.Draw opens and closes its own batch (PopupWindow draws the frame there); this
            // screen adds nothing on top - its list and labels are children.
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }

        public override void Update(float fixedDeltaTime)
        {
            RandomOpponentsCount.Text = $"Random Opponents: {Params.NumOpponents - Params.SelectedOpponents.Count}";
            RandomOpponentsCount.Color = Params.SelectedOpponents.Count == Params.NumOpponents ? Color.Gray : Color.Green;
            base.Update(fixedDeltaTime);
        }

        private void OnRaceItemSelected(SelectOpponentListItem item)
        {
            if (Params.SelectedOpponents.Remove(item.EmpireData))
                return;


            if (Params.SelectedOpponents.Count >= Params.NumOpponents)
                GameAudio.NegativeClick();
            else
                Params.SelectedOpponents.Add(item.EmpireData);
        }
    }
}