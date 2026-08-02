using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
	public sealed class DanButton
	{
		public Rectangle r;
	    public readonly Vector2 Pos;

        private readonly string Text;
		public string ToggledText;

		public bool IsToggle;

		public bool Toggled;
	    private bool Hover;

		private readonly Vector2 TextPos;

		public DanButton(Vector2 pos, string text)
		{
			Pos = pos;
			r = new Rectangle((int)pos.X, (int)pos.Y, 182, 25);
			Text = text;
			TextPos = new Vector2(r.X + 20, r.Y + 12 - Fonts.Arial12Bold.LineSpacing / 2 - 2);
			ToggledText = text;
		}

		public void Draw(ScreenManager screenManager)
		{
		    Color color;
			Vector2 pos = TextPos;
			UIButton.DrawPlate(screenManager.SpriteBatch, r, UIButton.PlateNeutral);
			SpriteBatch spriteBatch = screenManager.SpriteBatch;
			Graphics.Font arial12Bold = Fonts.Arial12Bold;
			string str = (Toggled ? ToggledText : Text);
			Vector2 vector2 = pos;
			if (Hover)
			{
				color = new Color(255, 255, 255, 150).Premultiplied();
			}
			else
			{
				color = (Toggled ? new Color(121, 98, 75) : Color.White);
			}
			spriteBatch.DrawString(arial12Bold, str, vector2, color);
		}

		public void Draw(ScreenManager screenManager, Rectangle rect)
		{
		    Color color;
			UIButton.DrawPlate(screenManager.SpriteBatch, rect, UIButton.PlateNeutral);
			var tPos = new Vector2(rect.X + 25, rect.Y + 12 - Fonts.Arial12Bold.LineSpacing / 2);
			Vector2 pos = tPos;
			SpriteBatch spriteBatch = screenManager.SpriteBatch;
			Graphics.Font arial12Bold = Fonts.Arial12Bold;
			string str = (Toggled ? ToggledText : Text);
			Vector2 vector2 = pos;
			if (Hover)
			{
				color = new Color(255, 255, 255, 150).Premultiplied();
			}
			else
			{
				color = (Toggled ? new Color(121, 98, 75) : Color.White);
			}
			spriteBatch.DrawString(arial12Bold, str, vector2, color);
		}

		public void DrawBlue(SpriteBatch batch)
		{
		    Color color;
			Vector2 pos = TextPos;
            UIButton.DrawPlate(batch, r, UIButton.PlateActive);
			string str = (Toggled ? ToggledText : Text);
			if (Hover)
			{
				color = new Color(174, 202, 255);
			}
			else
			{
				color = (Toggled ? Color.White : new Color(88, 108, 146));
			}
            batch.DrawString(Fonts.Arial12Bold, str, pos, color);
		}

		public bool HandleInput(InputState input)
		{
			if (!r.HitTest(input.CursorPosition))
			{
				Hover = false;
			}
			else
			{
				Hover = true;
				if (input.InGameSelect)
				{
					GameAudio.EchoAffirmative();
					if (IsToggle)
					{
						Toggled = !Toggled;
					}
					return true;
				}
			}
			return false;
		}
	}
}