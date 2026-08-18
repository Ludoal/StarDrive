using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public sealed class CloseButton : UIButton
    {
        public CloseButton(float x, float y) : base(ButtonStyle.Close, new Vector2(x, y), "")
        {
            Tooltip = GameText.CloseThisScreen;
        }

        protected override void OnButtonClicked()
        {
            if (Parent is GameScreen screen && !screen.IsExiting)
                screen.ExitScreen();
            base.OnButtonClicked();
        }
    }
}