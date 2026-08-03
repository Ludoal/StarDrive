using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
	// Ludoal fork (wishlist): a simple star cartouche — same frame as the
	// uninhabitable-planet one: the star's name, its research status, and a
	// Deploy Research Station button. The mechanics were fully native
	// (researchable suns, ProcessResearchStation over a system, a computed
	// station position) — only this UI never existed.
	public sealed class StarInfoUIElement : UIElement
	{
		// Ludoal fork: the sculpted texture spent this top band on antenna machinery; with it
		// gone the frame starts this far under the housing. Same recipe as its four siblings.
		const int FrameShave = 26;

		SolarSystem Sys;
		readonly UniverseScreen Screen;
		Empire Player => Screen.Player;
		readonly Rectangle Housing;
		readonly Rectangle DeployRect;
		readonly Rectangle SunIconRect; // Ludoal fork: the star itself, left slot — same grammar as planets
		readonly Graphics.Font Font12 = Fonts.Arial12Bold;
		readonly Color ButtonTextColor  = new Color(174, 202, 255);
		readonly Color ButtonHoverColor = new Color(88, 108, 146);

		public StarInfoUIElement(in Rectangle r, ScreenManager sm, UniverseScreen screen)
		{
			Screen = screen;
			ScreenManager = sm;
			ElementRect = r;
			Housing = r;
			TransitionOnTime = TimeSpan.FromSeconds(0.25);
			TransitionOffTime = TimeSpan.FromSeconds(0.25);
			DeployRect = new Rectangle(r.X + 183, r.Y + 130, 182, 25); // right column, like planet action buttons
			SunIconRect = new Rectangle(r.X + 20, r.Y + 85, 160, 160); // doubled per bench feedback
		}

		public void SetSystem(SolarSystem s) => Sys = s;

		bool ShowDeployButton => Sys != null && Sys.IsExploredBy(Player) && Sys.IsResearchable;

		public override void Draw(SpriteBatch batch, DrawTimes elapsed)
		{
			if (Sys == null)
				return;

			// Ludoal fork: the minimap's recipe instead of the sculpted unitselmenu texture -
			// a near-opaque flat ground and a rounded grey rule, frame shaved like its siblings
			Rectangle frame = Housing;
			frame.Y += FrameShave; frame.Height -= FrameShave;
			Rectangle plate = frame;
			plate.Inflate(-2, -2);
			batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
			UITheme.DrawPlate(batch, frame, Color.Transparent,
			                  new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
			                  ruleWidthOverride: 3);
			batch.DrawString(Fonts.Arial20Bold, Sys.Name, new Vector2(Housing.X + 41, Housing.Y + 65), Colors.Cream);

			var textPos = new Vector2(Housing.X + 183, Housing.Y + 100); // right column — the star occupies the left
			if (!Sys.IsExploredBy(Player))
			{
				batch.DrawString(Font12, "Unexplored star", textPos, Color.Gray);
				return;
			}

			if (Sys.Sun.Icon != null) // Ludoal fork: show the star (system info may join it later)
				batch.Draw(Sys.Sun.Icon, SunIconRect, Color.White);

			if (Sys.IsResearchable)
			{
				batch.DrawString(Font12, "Researchable phenomena", textPos, Colors.Cream);
				DrawDeployButton(batch);
			}
			else
			{
				batch.DrawString(Font12, "No scientific interest", textPos, Color.Gray);
			}
		}

		void DrawDeployButton(SpriteBatch batch)
		{
			if (Sys.IsResearchStationDeployedBy(Player)) // built: no button, and no Abort on an operational station
			{
				var okPos = new Vector2(DeployRect.X + 13, DeployRect.Y + 13 - Font12.LineSpacing / 2 - 2);
				batch.DrawString(Font12, "Research station operational", okPos, Color.LightGreen);
				return;
			}
			bool canBuild = Player.CanBuildResearchStations;
			batch.Draw(ResourceManager.Texture(canBuild ? "NewUI/dan_button_blue_clear" : "NewUI/dan_button_disabled"),
			           DeployRect, Color.White);

			string text = Player.AI.HasGoal(g => g.IsResearchStationGoal(Sys))
			            ? Localizer.Token(GameText.AbortDeployent)
			            : Localizer.Token(GameText.DeployResearchStation);
			var textPos = new Vector2(DeployRect.X + 13, DeployRect.Y + 13 - Font12.LineSpacing / 2 - 2);
			bool hover = DeployRect.HitTest(Screen.Input.CursorPosition);
			batch.DrawString(Font12, text, textPos,
			                 canBuild ? (hover ? ButtonTextColor : ButtonHoverColor) : Color.Gray);
		}

		public override bool HandleInput(InputState input)
		{
			if (!ShowDeployButton || Sys.IsResearchStationDeployedBy(Player))
				return false;

			if (DeployRect.HitTest(input.CursorPosition))
			{
				ToolTip.CreateTooltip(Player.CanBuildResearchStations
				                      ? GameText.DeployResearchStationTip
				                      : GameText.CannotBuildResearchStationTip);
				if (input.LeftMouseClick)
				{
					if (Player.AI.HasGoal(g => g.IsResearchStationGoal(Sys)))
					{
						GameAudio.AffirmativeClick();
						Player.AI.CancelResearchStation(Sys);
					}
					else if (Player.CanBuildResearchStations)
					{
						GameAudio.AffirmativeClick();
						Player.AI.AddDeployResearchStationGoal(Sys);
					}
					else
					{
						GameAudio.NegativeClick();
					}
					return true;
				}
			}
			return false;
		}
	}
}
