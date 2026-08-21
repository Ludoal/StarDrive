using System;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game.GameScreens
{
    // Ludoal fork (Wishlist, trends spec): the Trends tab of the Diplomacy group -
    // progression curves per race in the four intel domains, drawn from the StatTracker
    // snapshots the replay already records. A curve only exists where its datum is known:
    // each domain unlocks with its intel level, and a third party's series starts at the
    // StarDate the level was reached - espionage does not invent the past. The player
    // sees their own full history. Legend names toggle a race's curve.
    public sealed class TrendsScreen : GameScreen
    {
        public readonly UniverseScreen Universe;
        readonly Empire Player;

        Submenu GroupTabs; // the Diplomacy group's tab row, this screen being one tab
        // this page's real frame is its tab row's rect - the band excludes exactly
        // what the page occupies (same law as its sibling tabs)
        public override Rectangle PageFrame => GroupTabs?.Rect ?? base.PageFrame;
        Rectangle LeftRect;

        // bench 459 (maintainer): Population by default, and the screen re-opens on the
        // domain it last showed - a static carries it across openings for the session
        static Espionage.IntelDomain Domain = Espionage.IntelDomain.Population;
        static readonly (Espionage.IntelDomain D, string Label)[] Domains =
        {
            (Espionage.IntelDomain.Population, "Population"),
            (Espionage.IntelDomain.Military,   "Military"),
            (Espionage.IntelDomain.Economy,    "Economy"),
            (Espionage.IntelDomain.Science,    "Science"),
        };

        // one series per empire, sorted by StarDate once at load
        class EmpireSeries
        {
            public Empire E;
            public Array<(float Date, float[] Values)> Points = new(); // Values indexed by IntelDomain
            public bool Hidden; // legend toggle, session-local
        }
        readonly Array<EmpireSeries> Series = new();

        // frame-harvested click zones (the draw lays them out, HandleInput reads them)
        readonly Array<(Rectangle Rect, Espionage.IntelDomain D)> DomainTabs = new();
        readonly Array<(Rectangle Rect, EmpireSeries S)> LegendRows = new();
        Rectangle ChartRect;

        Font Font12Bold = Fonts.Arial12Bold;
        Font Font14Bold = Fonts.Arial14Bold;

        public TrendsScreen(UniverseScreen parent) : base(parent, toPause: parent)
        {
            Universe = parent;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Player = Universe.Player;
        }

        public override void LoadContent()
        {
            // bench 448 (rature): the RELATIONSHIPS gabarit - the 900-class window, built
            // by hand exactly like that sibling
            Rectangle frame = ScreenGroups.GroupFrame900(ScreenWidth, ScreenHeight);
            GroupTabs = Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height),
                                        ScreenGroups.LiveTitles(ScreenGroups.Group.Diplomacy, Universe)));
            GroupTabs.OnTabChange = OnGroupTabChanged;
            GroupTabs.PerformLayout();
            GroupTabs.SelectedIndex = (int)MainDiplomacyScreen.Tab.Trends;
            Vector2 closePos = ScreenGroups.GroupClosePos(GroupTabs.ClientArea);
            CloseButton(closePos.X, closePos.Y);
            LeftRect = frame;

            // maintainer option B: level acquired before the feature = the curve starts
            // at the FIRST LOOK, never in a past the empire did not record
            foreach (Empire e in Universe.UState.ActiveMajorEmpires)
                if (!e.isPlayer)
                    Player.GetRelations(e).Espionage?.StampLegacyHoles();

            HarvestSeries();
        }

        int HarvestedTurns = -1;

        // bench 450: the sim keeps running under the open page - re-harvest whenever the
        // tracker grows a turn (the UI asks the sim, it never caches it)
        public override void Update(float fixedDeltaTime)
        {
            if (Universe.UState.Stats.NumRecordedTurns != HarvestedTurns)
                HarvestSeries();
            base.Update(fixedDeltaTime);
        }

        void HarvestSeries()
        {
            HarvestedTurns = Universe.UState.Stats.NumRecordedTurns;
            Empire[] majors = Universe.UState.ActiveMajorEmpires;
            var hidden = new Map<Empire, bool>();
            foreach (EmpireSeries old in Series)
                hidden[old.E] = old.Hidden; // the legend toggles survive the refresh
            Series.Clear();
            // harvest the snapshots: (date, one value per domain) per empire
            var byEmpire = new Map<Empire, EmpireSeries>();
            foreach (var perDate in Universe.UState.Stats.SnapshotsMap.Values)
            {
                foreach (var kv in perDate)
                {
                    if (kv.Key < 0 || kv.Key >= Universe.UState.Empires.Count)
                        continue;
                    Empire e = Universe.UState.Empires[kv.Key];
                    if (e.IsFaction || e.IsDefeated)
                        continue;
                    Snapshot snap = kv.Value;
                    if (!byEmpire.TryGetValue(e, out EmpireSeries s))
                        byEmpire[e] = s = new EmpireSeries { E = e };
                    s.Points.Add((snap.StarDate, new[]
                    {
                        snap.Population,
                        snap.MilitaryStrength,
                        snap.GrossIncome,
                        snap.ScientificStrength,
                    }));
                }
            }
            foreach (Empire e in majors) // stable order: the majors row's own
            {
                if (byEmpire.TryGetValue(e, out EmpireSeries s))
                {
                    s.Points.Sort((a, b) => a.Date.CompareTo(b.Date));
                    // bench 461 (maintainer): smooth every domain but Science - Economy
                    // especially carries one-turn windfalls (excess-goods sales) that spike
                    // 10x over the baseline. Science stays raw: its level steps ARE the point.
                    for (int d = 0; d < 3; ++d)
                        SmoothDomain(s.Points, d);
                    if (hidden.TryGetValue(e, out bool h))
                        s.Hidden = h;
                    Series.Add(s);
                }
            }
        }

        // One WIDE centred average (window 15, maintainer call at bench 464): the
        // sale-burst income is REAL revenue, so instead of clamping it away the average
        // redistributes each burst over its neighbourhood - the curve reads as the true
        // mean income. Lone spikes are absorbed the same way. Holes (v<=0, the
        // unmet-intel gaps) are preserved and never smeared - only positive neighbours
        // enter the window.
        static void SmoothDomain(Array<(float Date, float[] Values)> pts, int d)
        {
            int n = pts.Count;
            if (n < 5)
                return;
            float[] src = new float[n];
            for (int i = 0; i < n; ++i)
                src[i] = pts[i].Values[d];

            const int Half = 7; // window 15
            for (int i = 0; i < n; ++i)
            {
                if (src[i] <= 0f)
                    continue; // a hole stays a hole
                float sum = 0f; int cnt = 0;
                for (int j = Math.Max(0, i - Half); j <= Math.Min(n - 1, i + Half); ++j)
                    if (src[j] > 0f) { sum += src[j]; ++cnt; }
                pts[i].Values[d] = sum / cnt; // Values is a shared array - the write lands
            }
        }

        void OnGroupTabChanged(int index)
        {
            if (ScreenGroups.IsHostedTab(ScreenGroups.Group.Diplomacy, index, Universe))
            {
                ExitScreen();
                Universe.OpenHostedTabPanel?.Invoke();
                return;
            }
            var tab = (MainDiplomacyScreen.Tab)index;
            if (tab == MainDiplomacyScreen.Tab.Trends)
                return;
            ExitScreen();
            if (tab == MainDiplomacyScreen.Tab.Espionage)
                ScreenManager.AddScreen(new InfiltrationScreen(Universe));
            else
                ScreenManager.AddScreen(new MainDiplomacyScreen(Universe, tab));
        }

        // a third party's curve exists from its unlock date; the player's always, in full.
        // Legacy holes are stamped at screen open (option B), so an open gate always has
        // a date - no full-history fallback remains.
        bool SeriesVisible(Empire e, out float clipFrom)
        {
            clipFrom = 0f;
            if (e.isPlayer)
                return true;
            if (!Player.IsKnown(e))
                return false; // bench 447: an unmet race has no curve and no name
            Espionage esp = Player.GetRelations(e).Espionage;
            if (esp == null)
                return false;
            clipFrom = esp.DomainUnlockDate(Domain);
            bool gateOpen = Domain switch
            {
                Espionage.IntelDomain.Population => esp.CanViewPopRank,
                Espionage.IntelDomain.Military   => esp.CanViewMilitaryRank,
                Espionage.IntelDomain.Economy    => esp.CanViewEconomyRank,
                _                                => esp.CanViewScienceRank,
            };
            return gateOpen || clipFrom > 0f; // a level that fell back keeps what was noted
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GroupTabs), ScreenGroups.GroupFrameFill);

            RectF client = GroupTabs.ClientArea;
            int pad = 16;

            // domain selector row - gold is the selected one (charter)
            DomainTabs.Clear();
            float dx = client.X + pad, dy = client.Y + 10;
            foreach ((Espionage.IntelDomain d, string label) in Domains)
            {
                int w = (int)Font14Bold.TextWidth(label) + 20;
                var r = new Rectangle((int)dx, (int)dy, w, Font14Bold.LineSpacing + 8);
                bool sel = d == Domain;
                if (sel)
                    batch.FillRectangle(r, new Color(128, 87, 43, 60).Premultiplied());
                batch.DrawRectangle(r, sel ? Colors.Cream : new Color(95, 82, 56));
                batch.DrawString(Font14Bold, label, new Vector2(r.X + 10, r.Y + 4),
                                 sel ? Colors.Cream : Color.Gray);
                DomainTabs.Add((r, d));
                dx += w + 10;
            }

            // legend column on the right; the chart takes the rest
            int legendW = 170;
            ChartRect = new Rectangle((int)client.X + pad, (int)dy + Font14Bold.LineSpacing + 20,
                                      (int)client.W - pad * 2 - legendW - 10,
                                      (int)(client.Bottom - dy - Font14Bold.LineSpacing - 30 - pad));
            batch.DrawRectangle(ChartRect, new Color(95, 82, 56));

            // visible, clipped points; shared scale
            int di = (int)Domain;
            float minDate = float.MaxValue, maxDate = Universe.UState.StarDate, maxVal = 0f;
            var drawn = new Array<(EmpireSeries S, Array<(float Date, float Val)> Pts)>();
            foreach (EmpireSeries s in Series)
            {
                if (s.Hidden || !SeriesVisible(s.E, out float clipFrom))
                    continue;
                var pts = new Array<(float, float)>();
                foreach ((float date, float[] values) in s.Points)
                {
                    if (date < clipFrom)
                        continue;
                    if (values[di] <= 0f)
                        continue; // bench 448: a missing sample is a HOLE to skip, never a zero to draw
                    pts.Add((date, values[di]));
                    if (date < minDate) minDate = date;
                    if (values[di] > maxVal) maxVal = values[di];
                }
                if (pts.Count > 0)
                    drawn.Add((s, pts));
            }

            if (drawn.Count == 0 || maxDate <= minDate)
            {
                string none = "No recorded history in this domain yet";
                batch.DrawString(Font12Bold, none,
                    new Vector2(ChartRect.X + (ChartRect.Width - Font12Bold.TextWidth(none)) / 2, ChartRect.Y + ChartRect.Height / 2),
                    Color.Gray);
            }
            else
            {
                if (maxVal <= 0f) maxVal = 1f;
                float Sx(float date) => ChartRect.X + 4 + (date - minDate) / (maxDate - minDate) * (ChartRect.Width - 8);
                float Sy(float val)  => ChartRect.Bottom - 4 - val / maxVal * (ChartRect.Height - 8);

                foreach ((EmpireSeries s, var pts) in drawn)
                {
                    Vector2 prev = new Vector2(Sx(pts[0].Date), Sy(pts[0].Val));
                    for (int i = 1; i < pts.Count; ++i)
                    {
                        var cur = new Vector2(Sx(pts[i].Date), Sy(pts[i].Val));
                        batch.DrawLine(prev, cur, s.E.EmpireColor, 2f);
                        prev = cur;
                    }
                }

                // axis marks: first date, today, and the domain's peak value
                batch.DrawString(Font12Bold, minDate.StarDateString(),
                                 new Vector2(ChartRect.X + 4, ChartRect.Bottom + 4), Color.Gray);
                string end = maxDate.StarDateString();
                batch.DrawString(Font12Bold, end,
                                 new Vector2(ChartRect.Right - Font12Bold.TextWidth(end) - 4, ChartRect.Bottom + 4), Color.Gray);
                batch.DrawString(Font12Bold, maxVal.GetNumberString(),
                                 new Vector2(ChartRect.X + 4, ChartRect.Y + 2), Color.Gray); // inside: above, it sat on the buttons
            }

            // legend: every major, its curve's colour; hidden or locked ones grayed.
            // A name click toggles the curve (drawn ones only - a locked row says why).
            LegendRows.Clear();
            float ly = ChartRect.Y;
            float lx = ChartRect.Right + 14;
            foreach (EmpireSeries s in Series)
            {
                bool known = s.E.isPlayer || Player.IsKnown(s.E);
                bool visible = SeriesVisible(s.E, out _);
                var row = new Rectangle((int)lx, (int)ly, legendW - 4, Font12Bold.LineSpacing + 4);
                var swatch = new Rectangle(row.X, row.Y + 3, 10, 10);
                batch.FillRectangle(swatch, visible && !s.Hidden ? s.E.EmpireColor : new Color(70, 70, 70));
                // bench 447: an unmet race shows "?", nothing else; a locked one names its
                // price, and the name truncates so the tag never collides
                string req = !known ? "" : visible ? "" : "Lvl " + Domain switch
                {
                    Espionage.IntelDomain.Population => 1,
                    Espionage.IntelDomain.Military   => 2,
                    _                                => 3,
                };
                string name = known ? s.E.data.Traits.Name : "?";
                float nameRoom = row.Width - 16 - (req.Length > 0 ? Font12Bold.TextWidth(req) + 6 : 0);
                while (name.Length > 2 && Font12Bold.TextWidth(name) > nameRoom)
                    name = name.Substring(0, name.Length - 1); // plain cut: the font draws the ellipsis glyph as "?" 
                Color c = !visible ? new Color(105, 105, 105)
                        : s.Hidden ? Color.Gray
                        : Colors.Cream;
                batch.DrawString(Font12Bold, name, new Vector2(row.X + 16, row.Y), c);
                if (req.Length > 0)
                    batch.DrawString(Font12Bold, req,
                        new Vector2(row.Right - Font12Bold.TextWidth(req), row.Y), new Color(105, 105, 105));
                LegendRows.Add((row, s));
                ly += row.Height + 2;
            }

            base.Draw(batch, elapsed);
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // live top bar, like every sibling
                return true;
            if (input.LeftMouseClick)
            {
                foreach ((Rectangle r, Espionage.IntelDomain d) in DomainTabs)
                {
                    if (r.HitTest(input.CursorPosition) && d != Domain)
                    {
                        GameAudio.AcceptClick();
                        Domain = d;
                        return true;
                    }
                }
                foreach ((Rectangle r, EmpireSeries s) in LegendRows)
                {
                    if (r.HitTest(input.CursorPosition) && SeriesVisible(s.E, out _))
                    {
                        GameAudio.AcceptClick();
                        s.Hidden = !s.Hidden;
                        return true;
                    }
                }
            }
            return base.HandleInput(input);
        }
    }
}
