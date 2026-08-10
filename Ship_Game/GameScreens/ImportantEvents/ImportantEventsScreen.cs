using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using System;
using Ship_Game.Audio;
using Ship_Game.ExtensionMethods;
using Ship_Game.UI; // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    // Permanent log of Important notifications (empire defeat, merge/surrender,
    // remnant story progression), opened from the minimap. On the shared table charte.
    public sealed class ImportantEventsScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu GalaxyTabs;   // Ludoal fork: the Galaxy group's tab row, this screen being one tab
        // Ludoal fork (bench 387): this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => GalaxyTabs?.Rect ?? base.PageFrame;

        void OnGalaxyTabChanged(int index)
            => GameScreens.ScreenGroups.SwitchGalaxyTab(index, self: 3, Universe, this);

        readonly ImportantNotification[] Events;
        readonly ScrollList<ImportantEventListItem> EventList;
        public readonly UITable Table; // the shared table charte owns geometry, headers and rules

        public ImportantEventsScreen(UniverseScreen screen) : base(screen, toPause: null)
        {
            Universe          = screen;
            Events            = screen.UState.GetImportantEvents();
            IsPopup           = true;
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;

            // Ludoal fork: the Events tab of the Galaxy group, content-sized on the shared
            // table charte - the star dates and titles size their columns on the data
            Table = new UITable(new[]
            {
                new UITable.Column { Title = "Star Date", Width = 90, Align = TableAlign.Number },
                new UITable.Column { Title = "Title", Width = 200 },
                new UITable.Column { Title = "Description", Width = 700 },
            });
            var dates = new Array<string>(); var titles = new Array<string>();
            foreach (ImportantNotification ev in Events)
            {
                dates.Add(ev.StarDate.StarDateString());
                titles.Add(ev.Title);
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, dates);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, titles);
            Table.Columns[1].Width += 48; // the faction flag rides left of the title
            Table.FitToWidth((int)(Math.Min(ScreenWidth, GameScreens.ScreenGroups.MaxFrameWidth) - 2 * GameScreens.ScreenGroups.FrameMargin) - 66);

            float fullAvail = GameScreens.ScreenGroups.FullTableHeight(ScreenHeight); // bench 343: capped at 1080p
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, Events.Length), 84, fullAvail);
            GalaxyTabs = GameScreens.ScreenGroups.AddGroupTabs(this, GameScreens.ScreenGroups.LiveTitles(GameScreens.ScreenGroups.Group.Galaxy, Universe), 3,
                                                               OnGalaxyTabChanged, Table.ContentWidth, contentH);
            RectF client = GalaxyTabs.ClientArea;
            Table.RowPitch = 84;
            Table.Layout(client, client.Y + 10, client.Bottom - 5);

            EventList = Add(new ScrollList<ImportantEventListItem>(Table.ListRect, 80));
            EventList.EnableItemHighlight = true;
            Table.ApplyHighlightTo(EventList);
        }

        void PopulateEvents()
        {
            // newest first
            for (int i = Events.Length - 1; i >= 0; --i)
                EventList.AddItem(new ImportantEventListItem(Table, Events[i]));
        }

        public override void LoadContent()
        {
            // the close cross and the screen's name come from the group's tab row now
            PopulateEvents();
            base.LoadContent();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame is filled by hand before its children, the way every screen
            // in this group does - the group's frame is transparent, so the map showed through.
            batch.FillRectangle(GameScreens.ScreenGroups.GroupFrameFillRect(GalaxyTabs), GameScreens.ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);
            // the shared charte draws the headers, the rule and the separators
            // no chrome on an empty log (maintainer bench 307): the hint speaks alone
            if (EventList.NumEntries > 0)
                Table.DrawChrome(batch);

            if (EventList.NumEntries == 0)
            {
                const string hint = "Nothing to report yet.";
                Graphics.Font font = Fonts.Arial12Bold;
                var pos = new Vector2(Table.TableRect.X + (Table.TableRect.Width - font.TextWidth(hint)) / 2f,
                                      Table.ListRect.Y + 40);
                batch.DrawString(font, hint, pos.Rounded(), Color.Gray);
            }

            Universe.EmpireUI.Draw(batch);   // the live top bar, as on its sibling tabs
            GameScreens.ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.ImportantEventsScreen && !GlobalStats.TakingInput) // Ludoal fork: F7 toggles the screen
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
