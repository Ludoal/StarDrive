using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
	public sealed class VariableUIElement : UIElement
	{
		// the cartouche family's shave, read from ONE constant so this one cannot drift
		const int FrameShave = PlanetInfoUIElement.FrameShave;

		private UniverseScreen screen;

		public Rectangle LeftRect;

		public Rectangle RightRect;

		public Rectangle Housing;

		public Rectangle Power;

		public Rectangle Shields;

		public Rectangle Ordnance;

		public VariableUIElement(Rectangle r, ScreenManager sm, UniverseScreen screen)
		{
			this.screen = screen;
			ScreenManager = sm;
			ElementRect = r;
			TransitionOnTime = TimeSpan.FromSeconds(0.25);
			TransitionOffTime = TimeSpan.FromSeconds(0.25);
			Housing = r;
			LeftRect = new Rectangle(r.X, r.Y + 44, 180, r.Height - 44);
			RightRect = new Rectangle(LeftRect.X + LeftRect.Width, LeftRect.Y, 220, LeftRect.Height);
		}

		public override void Draw(SpriteBatch batch, DrawTimes elapsed)
		{
		}

		public void Draw(string TitleText, string BodyText)
		{
			0f.SmoothStep(1f, TransitionPosition);
			// Ludoal fork: the minimap's recipe - a near-opaque flat ground and a rounded grey
			// rule, and the SAME frame as the four cartouche siblings to the pixel: same shave,
			// same right trim, same Submenu furniture
			SpriteBatch batch = ScreenManager.SpriteBatch;
			Rectangle frame = Housing;
			frame.Y += FrameShave; frame.Height -= FrameShave;
			frame.Width -= PlanetInfoUIElement.RightTrim;
			Submenu.DrawFrameWithGround(batch, new RectF(frame));
			Vector2 NamePos = new Vector2(Housing.X + 41, Housing.Y + 69); // 8px under the frame top
			ScreenManager.SpriteBatch.DrawString(Fonts.Arial20Bold, TitleText, NamePos, tColor);
			Vector2 BodyPos = new Vector2(NamePos.X, Housing.Y + 115);
			ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, BodyText, BodyPos, tColor);
		}
	}
}