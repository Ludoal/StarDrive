using System;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.UI; // UITable.FitText
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
	// Ludoal fork (spec cartouches, bench 306): the star cartouche — name, class and
	// image up top, the system's planet roll with class and R/F/P, and the Research
	// Station deployment when the star is eligible. The plate is BOTTOM-ANCHORED on
	// the housing and grows UPWARD with its content: the first of the info cartouches
	// to take an adaptive height (maintainer direction).
	public sealed class StarInfoUIElement : UIElement
	{
		SolarSystem Sys;
		readonly UniverseScreen Screen;
		Empire Player => Screen.Player;
		readonly Rectangle Housing;
		Rectangle DeployRect;   // computed each draw - the plate's height moves with the content
		readonly Graphics.Font Font12 = Fonts.Arial12Bold;
		readonly Color ButtonTextColor  = new Color(174, 202, 255);
		readonly Color ButtonHoverColor = new Color(88, 108, 146);
		const int RowH = 16;

		public StarInfoUIElement(in Rectangle r, ScreenManager sm, UniverseScreen screen)
		{
			Screen = screen;
			ScreenManager = sm;
			ElementRect = r;
			Housing = r;
			TransitionOnTime = TimeSpan.FromSeconds(0.25);
			TransitionOffTime = TimeSpan.FromSeconds(0.25);
		}

		public void SetSystem(SolarSystem s) => Sys = s;

		bool ShowDeployButton => Sys != null && Sys.IsExploredBy(Player) && Sys.IsResearchable;

		// "Star Blue" from "star_blue2" - the same reading the Exotic table does
		static string StarClassName(string sunId)
		{
			string t = sunId.TrimEnd('0','1','2','3','4','5','6','7','8','9');
			if (t.StartsWith("star_"))
				t = t.Substring(5);
			string[] words = t.Split('_');
			for (int i = 0; i < words.Length; i++)
				if (words[i].Length > 0)
					words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
			return string.Join(" ", words);
		}

		public override void Draw(SpriteBatch batch, DrawTimes elapsed)
		{
			if (Sys == null)
				return;

			bool explored = Sys.IsExploredBy(Player);
			var planets = Sys.PlanetList;

			// the plate grows UPWARD from the housing's foot: header (icon, name, class),
			// then one row per explored planet under a small icon header, then the
			// research line and its button when the star earns them
			const int HeaderH = 86;
			int rows = 0;
			if (explored)
				foreach (Planet p in planets)
					if (p.IsExploredBy(Player))
						rows++;
			bool research = explored && Sys.IsResearchable;
			int listH   = rows > 0 ? RowH + rows * RowH + 6 : 0; // icon header + rows + air
			int deployH = research ? 56 : 0;
			int plateH  = explored ? HeaderH + listH + deployH + 10 : HeaderH + 10;
			int top     = Housing.Bottom - plateH;

			// the minimap's recipe: near-opaque flat ground, rounded grey rule
			var frame = new Rectangle(Housing.X, top, Housing.Width, plateH);
			Rectangle plate = frame;
			plate.Inflate(-2, -2);
			batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
			UITheme.DrawPlate(batch, frame, Color.Transparent,
			                  new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
			                  ruleWidthOverride: 3);

			// header: the star at left, name and class beside it
			if (Sys.Sun.Icon != null)
				batch.Draw(Sys.Sun.Icon, new Rectangle(frame.X + 12, top + 10, 64, 64), Color.White);
			batch.DrawString(Fonts.Arial20Bold, Sys.Name, new Vector2(frame.X + 86, top + 18), Colors.Cream);
			batch.DrawString(Fonts.Arial12, explored ? StarClassName(Sys.Sun.Id) : "Unexplored star",
			                 new Vector2(frame.X + 88, top + 18 + Fonts.Arial20Bold.LineSpacing + 2),
			                 Color.Gray);
			if (!explored)
				return;

			// the planet roll: name, class, then R / F / P on three fixed lanes
			int laneP = frame.Right - 46;   // max population
			int laneF = laneP - 42;         // fertility
			int laneR = laneF - 42;         // richness
			float y = top + HeaderH;
			if (rows > 0)
			{
				void LaneIcon(string tex, int lane, int size)
					=> batch.Draw(ResourceManager.Texture(tex),
					              new Rectangle(lane + 18 - size, (int)y, size, size), Color.White);
				LaneIcon("NewUI/icon_production", laneR, 14);
				LaneIcon("NewUI/icon_food", laneF, 14);
				LaneIcon("UI/icon_pop_22", laneP, 14);
				y += RowH;

				foreach (Planet p in planets)
				{
					if (!p.IsExploredBy(Player))
						continue;
					Color nameColor = p.Owner?.EmpireColor ?? Colors.Cream;
					batch.DrawString(Font12, p.Name, new Vector2(frame.X + 16, y), nameColor);
					batch.DrawString(Fonts.Arial12,
					                 UITable.FitText(Fonts.Arial12, p.LocalizedCategory, laneR - 24 - (frame.X + 150)),
					                 new Vector2(frame.X + 150, y), Color.Gray);
					void Lane(string v, int lane)
						=> batch.DrawString(Fonts.Arial12, v,
						                    new Vector2(lane + 18 - Fonts.Arial12.TextWidth(v), y), Color.White);
					Lane(p.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture), laneR);
					Lane(p.FertilityFor(Player).ToString("0.0", CultureInfo.InvariantCulture), laneF);
					Lane(p.MaxPopulationBillionFor(Player).ToString("0.0", CultureInfo.InvariantCulture), laneP);
					y += RowH;
				}
				y += 6;
			}

			if (research)
			{
				batch.DrawString(Font12, "Researchable phenomena", new Vector2(frame.X + 16, y + 2), Colors.Cream);
				DeployRect = new Rectangle(frame.X + 16, (int)y + 22, 182, 25);
				DrawDeployButton(batch);
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
			UIButton.DrawPlate(batch, DeployRect, canBuild ? UIButton.PlateActive : UIButton.PlateNeutral);

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
