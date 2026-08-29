using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public enum MessageBoxButtons
    {
        Default, // Ok / Cancel
        Ok, // only OK
    }

    public class MessageBoxScreen : GameScreen
    {
        readonly UIButton Ok;
        readonly UIButton Cancel;

        float Timer;
        readonly bool Timed;
        
        string Message;
        readonly string Original;
        string ToAppend;
        readonly int BoxWidth;

        public Action Accepted;
        public Action Cancelled;
        public Vector2? CenterOn; // Ludoal fork (bench 362): centre on a frame instead of the display
        readonly GameScreen Summoner; // bench 407: default CenterOn source (the summoner's page frame)

        public MessageBoxScreen(GameScreen parent, string message,
                                MessageBoxButtons buttons = MessageBoxButtons.Default, int width = 320)
            : this(parent, message, Localizer.Token(GameText.Ok), Localizer.Token(GameText.Cancel), buttons, width)
        {
        }

        public MessageBoxScreen(GameScreen parent, GameText message, string okText, string cancelText)
            : this(parent, Localizer.Token(message), okText, cancelText)
        {
        }

        public MessageBoxScreen(GameScreen parent, string message, float timer,
                                MessageBoxButtons buttons = MessageBoxButtons.Default)
            : this(parent, message, Localizer.Token(GameText.Ok), Localizer.Token(GameText.Cancel), buttons)
        {
            Timed = true;
            Timer = timer;
        }

        public MessageBoxScreen(GameScreen parent, string message, string okText, string cancelText,
                                MessageBoxButtons buttons = MessageBoxButtons.Default, int width = 320)
            : base(parent, toPause: UniverseToPause(parent))
        {
            Summoner = parent;
            Original = message;
            Message = message;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            BoxWidth = width;

            // bench 363 (maintainer): the confirm is a neutral Wide, the cancel a red WideHostile -
            // the same meaning-coloured pair the Shipyard's own rows use. bench 364: at the SMALL
            // button's size - every style is nine-sliced, so the meaning colour and the compact
            // format compose freely.
            Ok = Button(ButtonStyle.Wide, 0f, 0f, okText, click: OnOkClicked);
            Ok.SetAbsSize(96, 26);
            if (buttons == MessageBoxButtons.Default)
            {
                Cancel = Button(ButtonStyle.WideHostile, 0f, 0f, cancelText, click: OnCancelClicked);
                Cancel.SetAbsSize(96, 26);
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);

            Message = Fonts.Arial12Bold.ParseText(Original + ToAppend, 250f);
            Vector2 msgSize = Fonts.Arial12Bold.MeasureString(Message);
            // Ludoal fork (bench 362): a box summoned by a frame-bound screen centres on that frame
            Vector2 c = CenterOn ?? Summoner?.PageFrameCentre() ?? new Vector2(ScreenWidth / 2f, ScreenHeight / 2f);
            var r = new Rectangle((int)c.X - BoxWidth/2, (int)c.Y - (int)(msgSize.Y + 40f) / 2,
                                  BoxWidth, (int)(msgSize.Y + 40f) + 30); // bench 363: buttons breathe off the edge

            var textPosition = new Vector2(r.X + r.Width / 2 - Fonts.Arial12Bold.MeasureString(Message).X / 2f, r.Y + 10);

            // the pair straddles the centre; ALONE, Ok takes the centre itself - it was sitting
            // where its half of a couple would be, beside a Cancel that is not there (bench 528)
            Ok.SetAbsPos(Cancel != null ? r.X + r.Width / 2 + 6
                                        : r.X + r.Width / 2 - 48, r.Y + r.Height - 38);
            Cancel?.SetAbsPos(r.X + r.Width / 2 - 102, r.Y + r.Height - 38);

            batch.SafeBegin();
            batch.FillRectangle(r, Color.Black);
            batch.DrawRectangle(r, Color.Orange);
            batch.DrawString(Fonts.Arial12Bold, Message, textPosition, Color.White);
            base.Draw(batch, elapsed);
            batch.SafeEnd();
        }

        void OnOkClicked(UIButton b)
        {
            Accepted?.Invoke();
            GameAudio.AffirmativeClick();
            ExitScreen();
        }

        void OnCancelClicked(UIButton b)
        {
            Cancelled?.Invoke();
            ExitScreen();
        }

        public override bool HandleInput(InputState input)
        {
            if (input.MenuSelect)
            {
                Accepted?.Invoke();
                ExitScreen();
                return true;
            }
            if (input.MenuCancel)
            {
                Cancelled?.Invoke();
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            Timer -= fixedDeltaTime;
            if (Timed && !IsExiting)
            {
                ToAppend = string.Concat(" ", Timer.String(0), " ", Localizer.Token(GameText.Seconds));
                if (Timer <= 0f)
                {
                    Cancelled?.Invoke();
                    ExitScreen();
                }
            }
            base.Update(fixedDeltaTime);
        }
    }
}
