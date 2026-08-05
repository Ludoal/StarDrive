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

		// the roll's rows are clickable (maintainer bench 315): a click selects the
		// planet and glides the camera onto it - the arrows' spatial walk, from the list
		struct RollRow { public Rectangle Rect; public Planet P; }
		readonly Array<RollRow> RollRows = new Array<RollRow>();
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

		public void SetSystem(SolarSystem s)
		{
			Sys = s;
			RollRows.Clear();
		}

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

			// the planet cartouche's fixed frame (maintainer bench 313): the adaptive
			// plate retires, every info cartouche wears the same dimensions
			int top = Housing.Y + PlanetInfoUIElement.FrameShave;
			var frame = new Rectangle(Housing.X, top,
			                          Housing.Width - PlanetInfoUIElement.RightTrim,
			                          Housing.Height - PlanetInfoUIElement.FrameShave);
			Rectangle plate = frame;
			plate.Inflate(-2, -2);
			batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
			UITheme.DrawPlate(batch, frame, Color.Transparent,
			                  new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
			                  ruleWidthOverride: 3);

			// the planet grammar: the star in the sprite box, its name bottom-aligned on
			// the top text line centred over it, the class caption under it
			Rectangle iconBox = PlanetInfoUIElement.SpriteBox(Housing);
			iconBox.X -= 60; // maintainer benches 314-315: the star's name/image/class block rides left
			if (Sys.Sun.Icon != null)
				batch.Draw(Sys.Sun.Icon, iconBox, Color.White);

			string name = Sys.Name;
			Graphics.Font nameFont = Fonts.Arial20Bold; // fixed, like the planet pages (bench 314)
			int spriteCX = iconBox.CenterX();
			float topTextY = Housing.Y + PlanetInfoUIElement.TopLineIconY + 13 - Font12.LineSpacing / 2;
			batch.DrawString(nameFont, name,
			                 new Vector2(spriteCX - nameFont.TextWidth(name) / 2f,
			                             topTextY + Font12.LineSpacing - nameFont.LineSpacing),
			                 Colors.Cream);
			string cls = explored ? StarClassName(Sys.Sun.Id) : "Unexplored star";
			batch.DrawString(Font12, cls,
			                 new Vector2(spriteCX - Font12.TextWidth(cls) / 2f, iconBox.Bottom + 5), tColor);
			if (!explored)
				return;

			// the system's planet roll rides the right column, where the planet pages
			// keep their sliders: name then R / F / P lanes; a zero reads as a gray dash
			// (a gas giant has no ground to rate)
			int listX = iconBox.Right + 30;
			// bench 315: the roll widens 30px (10 left with the block, 20 on its right edge)
			// and F and R swap - food first, as on the colony sliders
			int laneP = frame.Right - 50, laneR = laneP - 42, laneF = laneR - 42;
			int blockRight = laneP + 18;
			float y = Housing.Y + PlanetInfoUIElement.TopLineIconY - 3; // the icon header rides the name line
			RollRows.Clear();
			int rows = 0;
			foreach (Planet p in planets)
				if (p.IsExploredBy(Player))
					rows++;
			if (rows > 0)
			{
				void LaneIcon(string tex, int lane, int size)
					=> batch.Draw(ResourceManager.Texture(tex),
					              new Rectangle(lane + 18 - size, (int)y, size, size), Color.White);
				// standard 22px icons, as the planet pages wear them (bench 314)
				LaneIcon("NewUI/icon_food", laneF, 22);
				LaneIcon("NewUI/icon_production", laneR, 22);
				LaneIcon("UI/icon_pop_22", laneP, 22);
				y += 24; // the icon header is taller than a text row

				foreach (Planet p in planets)
				{
					if (!p.IsExploredBy(Player))
						continue;
					Color nameColor = p.Owner?.EmpireColor ?? Colors.Cream;
					string pn = UITable.FitText(Font12, p.Name, laneF - 24 - listX);
					batch.DrawString(Font12, pn, new Vector2(listX, y), nameColor);
					// the class squeezes in after the name in small type, when it fits
					int clsRoom = laneF - 24 - listX - (int)Font12.TextWidth(pn) - 6;
					if (clsRoom > 20)
						batch.DrawString(Fonts.Arial10,
						                 UITable.FitText(Fonts.Arial10, p.LocalizedCategory, clsRoom),
						                 new Vector2(listX + Font12.TextWidth(pn) + 6, y + 2), Color.Gray);
					void Lane(float v, int lane)
					{
						bool zero = v < 0.05f;
						string s = zero ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);
						batch.DrawString(Fonts.Arial12, s,
						                 new Vector2(lane + 18 - Fonts.Arial12.TextWidth(s), y),
						                 zero ? Color.Gray : Color.White);
					}
					Lane(p.FertilityFor(Player), laneF);
					Lane(p.MineralRichness, laneR);
					Lane(p.MaxPopulationBillionFor(Player), laneP);
					RollRows.Add(new RollRow
					{
						Rect = new Rectangle(listX, (int)y - 2, blockRight - listX, RowH),
						P = p
					});
					y += RowH;
				}
			}

			if (Sys.IsResearchable)
			{
				// the deploy button under the roll, centred on it - no label, the button says it all
				int by = ((int)y + 8).UpperBound(Housing.Bottom - 33);
				DeployRect = new Rectangle(listX + (blockRight - listX - 182) / 2, by, 182, 25);
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
			// a click on a roll row selects that planet - the arrows' spatial walk
			if (input.LeftMouseClick)
			{
				foreach (RollRow r in RollRows)
				{
					if (r.Rect.HitTest(input.CursorPosition))
					{
						GameAudio.AcceptClick();
						Screen.SetSelectedPlanet(r.P);
						Screen.SnapViewTo(new(r.P.Position.X, r.P.Position.Y,
							Screen.GetZfromScreenState(UniverseScreen.UnivScreenState.PlanetView)), 5f, 2f);
						return true;
					}
				}
			}

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
