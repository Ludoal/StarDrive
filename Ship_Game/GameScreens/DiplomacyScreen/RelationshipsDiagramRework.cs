using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Gameplay;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.GameScreens; // InfiltrationScreenRework, sibling tab of this group

namespace Ship_Game.GameScreens.DiplomacyScreen
{
    public sealed class RelationshipsDiagramRework : GameScreen
    {
        private readonly Menu2 Window;
        readonly Array<Peer> Peers = new Array<Peer>();
        readonly Vector2 WeightCenter; // Offset from window center for circle of empires
        UILabel Title;

        // Ludoal fork: one filter per treaty type (replaces the old two toggles),
        // ordered War → Peace → Alliance → NA → Open Borders → Trade, all on by default
        readonly bool[] ShowTreaty = { true, true, true, true, true, true };
        UICheckBox[] Filters;

        // treaty rows, in the fixed display/filter order (index = row in ShowTreaty[])
        enum T { War = 0, Peace = 1, Alliance = 2, Nap = 3, Borders = 4, Trade = 5 }

        readonly Color ColorWar     = Color.Red;
        readonly Color ColorPeace   = Color.White;
        readonly Color ColorAlly    = Color.Green;
        readonly Color ColorNap     = Color.DeepSkyBlue.Alpha(0.85f);
        readonly Color ColorBorders = Color.Violet; // brighter than MediumPurple, opaque
        readonly Color ColorTrade   = Color.Yellow.Alpha(0.85f);

        readonly Array<EmpireAndIntelLevel> EmpiresAndIntel;
        readonly Graphics.Font LegendFont = Fonts.Arial14Bold;

        Empire Player;
        Empire SelectedEmpire;

        readonly UniverseScreen Universe;
        Submenu GroupTabs; // Ludoal fork: the Diplomacy group's tab row, this screen being one tab

        public RelationshipsDiagramRework(UniverseScreen us, Array<EmpireAndIntelLevel> empiresAndIntel)
            : base(us, toPause: us)
        {
            Universe = us;
            Player = us.Player;
            IsPopup           = true;
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;

            // Ludoal fork: the Relationships tab of the Diplomacy group. Same frame as its three
            // siblings - right under the top bar's button row (Y=64 on a 24px texture), 10px clear
            // of the screen edges - rather than the 1000x768 popup this was as a stacked screen.
            const int tabRowY = 64 + 24;
            const int margin = 10;
            var frame = new Rectangle(margin, tabRowY, ScreenWidth - 2 * margin,
                                      ScreenHeight - tabRowY - margin);
            GroupTabs = Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height),
                                        new LocalizedText[]
            {
                "Intelligence", "Bonuses", "Relationships", "Espionage"
            }));
            GroupTabs.OnTabChange = OnGroupTabChanged;
            GroupTabs.PerformLayout();
            GroupTabs.SelectedIndex = (int)MainDiplomacyScreenRework.Tab.Relationships;

            // NOT Add()ed: the tab frame is the delimiter now, so this only serves as the geometry
            // the diagram lays itself out against - adding it would draw the brass surround back.
            Window                = new Menu2(frame);
            RectF client          = GroupTabs.ClientArea;
            WeightCenter          = new Vector2(client.X + client.W / 2 + 100, client.Y + client.H / 2);
            EmpiresAndIntel       = empiresAndIntel;
            AddPeers();
        }

        // Ludoal fork: the other three tabs live on their own screen, so leaving Relationships
        // hands over to it. Relationships itself is a no-op: we are already here.
        void OnGroupTabChanged(int index)
        {
            var tab = (MainDiplomacyScreenRework.Tab)index;
            if (tab == MainDiplomacyScreenRework.Tab.Relationships)
                return;
            ExitScreen();
            if (tab == MainDiplomacyScreenRework.Tab.Espionage)
                ScreenManager.AddScreen(new InfiltrationScreenRework(Universe));
            else
                ScreenManager.AddScreen(new MainDiplomacyScreenRework(Universe, tab));
        }

        Color RowColor(T t) => t switch
        {
            T.War     => ColorWar,
            T.Peace   => ColorPeace,
            T.Alliance=> ColorAlly,
            T.Nap     => ColorNap,
            T.Borders => ColorBorders,
            _         => ColorTrade,
        };

        static LocalizedText RowText(T t) => t switch
        {
            T.War     => GameText.AtWar,
            T.Peace   => GameText.PeaceTreaty,
            T.Alliance=> GameText.Alliance,
            T.Nap     => GameText.NonaggressionPact3,
            T.Borders => GameText.OpenBordersTreaty2,
            _         => GameText.TradeTreaty,
        };

        const float LineThickness = 1f; // uniform across all treaty types

        public override void LoadContent()
        {
            CloseButton(Window.Menu.Right - 40, Window.Menu.Y + 20);
            Title = Add(new UILabel(GameText.EmpireRelationships, Fonts.Arial20Bold, Color.Wheat));

            // one checkbox + legend line per treaty type, in the fixed order.
            // explicit getter/setter so the array element is writable (a lambda
            // over a captured local doesn't build a settable expression Ref)
            var list = AddList(new Vector2(Window.X + 25, Window.Y + 80), new Vector2(260, 400));
            Filters = new UICheckBox[6];
            for (int i = 0; i < 6; ++i)
            {
                var t = (T)i;
                int idx = i; // capture
                var cb = new UICheckBox(0, 0, () => ShowTreaty[idx], v => ShowTreaty[idx] = v, LegendFont,
                                        title: RowText(t), tooltip: RowText(t));
                cb.TextColor = Color.Gray;
                cb.CheckedTextColor = RowColor(t);
                var ln = new UILine(new Vector2(70, LegendFont.LineSpacing + 2), 0.8f, LineThickness, RowColor(t));
                list.Add(new SplitElement(cb, ln));
                Filters[i] = cb;
            }

            base.LoadContent();
        }

        public override void PerformLayout()
        {
            Title.Pos = new Vector2(Window.X + 25, Window.Y + 30);
        }

        void AddPeers()
        {
            int angle = 360 / EmpiresAndIntel.Count;
            int peerAngle = 0;
            foreach (EmpireAndIntelLevel empireAndIntelLevel in EmpiresAndIntel)
            {
                Peer peer = new Peer(WeightCenter, Window.Rect, peerAngle, empireAndIntelLevel);
                Peers.Add(peer);
                peerAngle += angle;
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            base.Draw(batch, elapsed); // window
            DrawRelations(batch); // links and then portraits
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar, like its sibling tabs
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            HandleSelectedEmpire(input);
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;
            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                return true;
            }

            return base.HandleInput(input);
        }

        void HandleSelectedEmpire(InputState input)
        {
            SelectedEmpire = null;
            foreach (Peer peer in Peers)
            {
                if (peer.Rect.HitTest(input.CursorPosition))
                {
                    SelectedEmpire = peer.Empire;
                    break;
                }
            }
        }

        void DrawRelations(SpriteBatch batch)
        {
            Peer[] knownPeers = Peers.Filter(p => p.IntelLevel > 0);
            foreach (Peer us in knownPeers)
                foreach (Peer peer in Peers)
                    if (ShowPeer(us.Empire, peer.Empire))
                        DrawTreatyPeerLines(batch, us, peer);

            foreach (Peer empire in Peers)
            {
                batch.Draw(empire.Portrait, empire.Rect);
                batch.DrawRectangle(empire.Rect, Player.IsKnown(empire.Empire) || empire.Empire.isPlayer ? empire.Empire.EmpireColor : Color.Gray,
                    SelectedEmpire == empire.Empire ? 3 : 1);
            }
        }

        bool ShowPeer(Empire us, Empire peer)
        {
            return us != peer && Player.IsKnown(peer)
                              && (SelectedEmpire == null || SelectedEmpire == us || SelectedEmpire == peer);
        }

        void DrawTreatyPeerLines(SpriteBatch batch, Peer us, Peer peer)
        {
            Relationship rel = us.Empire.GetRelationsOrNull(peer.Empire);
            if (rel == null)
                return;

            // Ludoal fork: each treaty is a distinct parallel chord, offset perpendicular
            // to the A→B line by its row index — the six no longer stack on one line.
            // The relation is symmetric here, so pair the chord to a stable ordering
            // (lower empire id = "left") so both directions land on the same offset lane.
            Vector2 a = us.LinkPos, b = peer.LinkPos;
            Vector2 dir = a.DirectionToTarget(b);
            Vector2 perp = dir.LeftVector();
            if (us.Empire.Id > peer.Empire.Id) perp = -perp; // stable side per pair
            const float Lane = 5f; // px between adjacent treaty lanes

            void Chord(T t)
            {
                if (!ShowTreaty[(int)t]) return;
                // lanes fan out from the center line: War at 0, others stepping outward
                float lane = ((int)t) * Lane;
                Vector2 off = perp * lane;
                batch.DrawLine(a + off, b + off, RowColor(t), LineThickness);
            }

            if (rel.AtWar)               Chord(T.War);
            if (rel.Treaty_Peace)        Chord(T.Peace);
            if (rel.Treaty_Alliance)     Chord(T.Alliance);
            if (rel.Treaty_NAPact)       Chord(T.Nap);
            if (rel.Treaty_OpenBorders)  Chord(T.Borders);
            if (rel.Treaty_Trade)        Chord(T.Trade);
        }

        readonly struct Peer
        {
            public readonly Rectangle Rect;
            public readonly int IntelLevel;
            public readonly Vector2 LinkPos;
            public readonly Empire Empire;
            public readonly SubTexture Portrait;

            public Peer(Vector2 weightedCenter, Rectangle window, int angle, EmpireAndIntelLevel empireAndIntel)
            {
                Vector2 center = weightedCenter.PointFromAngle(angle, window.Height/2f - 80);
                Rect       = new Rectangle((int)center.X - 47, (int)center.Y - 55, 94, 111);
                Empire     = empireAndIntel.Empire;
                IntelLevel = empireAndIntel.IntelLevel;
                LinkPos    = center.PointFromAngle(180 + angle, 45);
                Portrait = Empire.Universe.Player.IsKnown(empireAndIntel.Empire) || empireAndIntel.Empire.isPlayer
                            ? ResourceManager.Texture("Portraits/" + Empire.data.PortraitName)
                            : ResourceManager.Texture("Portraits/unknown");
            }
        }
    }
}
