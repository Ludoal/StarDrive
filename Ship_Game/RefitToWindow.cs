using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Commands.Goals;
using Ship_Game.Audio;
using Ship_Game.Fleets;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI;

namespace Ship_Game
{
    // Ludoal fork (design review, bench 462): rebuilt on the PopupWindow charte - the
    // frame's height derives from the candidate count (no more empty cathedral), it
    // centres on the summoner's page frame (PopupWindow does that by itself), the
    // selection is a sticky gold rectangle that IS the "what am I refitting to"
    // reminder, and the foot (Rush toggle + buttons) lives INSIDE the frame.
    public sealed class RefitToWindow : PopupWindow
    {
        readonly ShipListScreen Screen;
        readonly Ship ShipToRefit;
        Empire Player => ShipToRefit.Universe.Player;
        ScrollList<RefitShipListItem> RefitShipList;
        UIButton RefitOne;
        UIButton RefitAll;
        UIButton RefitInFleet;
        UICheckBox RushRefit;
        IShipDesign RefitTo;
        ShipInfoOverlayComponent ShipInfoOverlay;
        bool Rush;

        const int PopupW = 420;
        const int RowH = 40, MaxRows = 8;
        const int FootToggleH = 26, FootButtonsH = 36; // two stable foot rows: toggle line, then buttons

        public RefitToWindow(ShipListScreen screen, ShipListScreenItem item) : this((GameScreen)screen, item.Ship)
        {
            Screen = screen;
        }

        public RefitToWindow(UniverseScreen parent, Ship ship) : this((GameScreen)parent, ship)
        {
        }

        RefitToWindow(GameScreen parent, Ship ship) : base(parent, PopupW, HeightFor(ship))
        {
            ShipToRefit = ship;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Rush = false; // the checkbox writes it via its expression; this appeases CS0649
        }

        // the same filter the list uses - counted at construction so the frame's
        // height derives from the content
        static Array<IShipDesign> CandidatesFor(Ship ship)
        {
            var designs = new Array<IShipDesign>();
            if (ship.IsSubspaceProjector)
                return designs;
            foreach (IShipDesign design in ship.Loyalty.ShipsWeCanBuildSnapshot)
            {
                if ((design.Hull == ship.ShipData.Hull || ship.IsResearchStation || ship.IsMiningStation)
                    && design != ship.ShipData
                    && !design.ShipRole.Protected
                    && ship.IsResearchStation == design.IsResearchStation
                    && ship.IsMiningStation == design.IsMiningStation)
                {
                    designs.Add(design);
                }
            }
            return designs;
        }

        static int HeightFor(Ship ship)
        {
            int rows = Math.Max(1, Math.Min(CandidatesFor(ship).Count, MaxRows));
            return PopupFrame.TitleBarTop + PopupFrame.TitleBarHeight
                 + rows * RowH + 12
                 + FootToggleH + FootButtonsH
                 + PopupFrame.BorderBottom;
        }

        class RefitShipListItem : ScrollListItem<RefitShipListItem>
        {
            readonly RefitToWindow Screen;
            public readonly IShipDesign Design;

            public RefitShipListItem(RefitToWindow screen, IShipDesign design)
            {
                Screen = screen;
                Design = design;
            }
            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                // sticky selection: the gold rectangle is the "refit to WHAT" reminder
                if (Screen.RefitTo == Design)
                    batch.DrawRectangle(Rect.Bevel(2, 1), Color.Gold);

                batch.Draw(Design.Icon, new Rectangle((int)X, (int)Y, 29, 30), Color.White);

                var tCursor = new Vector2(X + 40f, Y + 3f);
                batch.DrawString(Fonts.Arial12Bold, Design.Name, tCursor, Color.White);
                tCursor.Y += Fonts.Arial12Bold.LineSpacing;
                batch.DrawString(Fonts.Arial12Bold, Design.GetRole(), tCursor, Color.Orange);

                // the cost on the numbers lane, right-aligned against the row's edge
                int refitCost = Screen.ShipToRefit.RefitCost(Design);
                string cost = refitCost.ToString();
                float costW = Fonts.Arial12Bold.TextWidth(cost);
                var moneyRect = new Rectangle((int)(Right - 34 - costW - 25), (int)Y, 21, 20);
                batch.Draw(ResourceManager.Texture("NewUI/icon_production"), moneyRect, Color.White);
                batch.DrawString(Fonts.Arial12Bold, cost, new Vector2(Right - 34 - costW, Y + 2), Color.White);
            }
        }

        public override void LoadContent()
        {
            TitleText = $"Refit {ShipToRefit.Name}";
            base.LoadContent(); // seats the frame - centred on the summoner's page frame

            // bench 464: the frame texture is see-through by design - the old window's
            // list carried its own dark fill, this one seats it explicitly UNDER the rows
            // (added first = drawn first) or the summoner's page bleeds through the body
            Add(new UIPanel(PopupFrame.ContentArea(Rect), new Color(0, 0, 0, 230)));

            Array<IShipDesign> designs = CandidatesFor(ShipToRefit);
            int rows = Math.Max(1, Math.Min(designs.Count, MaxRows));
            var listRect = new RectF(Rect.X + 12, BodyTop, Rect.Width - 24, rows * RowH + 8);
            RefitShipList = Add(new ScrollList<RefitShipListItem>(listRect, RowH));
            RefitShipList.EnableItemHighlight = true;
            RefitShipList.OnClick = OnRefitShipItemClicked;
            RefitShipList.OnDoubleClick = item => { OnRefitShipItemClicked(item); OnRefitOneClicked(null); };
            foreach (IShipDesign design in designs)
                RefitShipList.AddItem(new RefitShipListItem(this, design));

            // the foot, INSIDE the frame: the Rush line, then the buttons line
            float toggleY  = listRect.Bottom + 4;
            float buttonsY = toggleY + FootToggleH;
            RushRefit = Add(new UICheckBox(() => Rush, Fonts.Arial12Bold,
                title: GameText.RushRefit, tooltip: GameText.RushRefitTip));
            RushRefit.TextColor = Color.Gray;
            RushRefit.CheckedTextColor = Color.Red;
            RushRefit.Pos = new Vector2(Rect.X + 16, toggleY);

            // right-aligned trio; Refit In Fleet only exists for a fleet ship. All greyed
            // until the sticky selection arms them - the selection is the transaction's WHAT.
            int shipsOfDesign = 0;
            foreach (Ship s in Player.OwnedShips)
                if (s.Name == ShipToRefit.Name)
                    ++shipsOfDesign;

            float x = Rect.Right - 11;
            x -= 128;
            RefitAll = ButtonMedium(x, buttonsY, text: Localizer.Token(GameText.RefitAll) + $" ({shipsOfDesign})", click: OnRefitAllClicked);
            RefitAll.Tooltip = GameText.RefitAllShipsOfThis;
            if (ShipToRefit.Fleet != null)
            {
                x -= 130;
                RefitInFleet = ButtonMedium(x, buttonsY, text: GameText.RefitInFleet, click: OnRefitFleetClicked);
                RefitInFleet.Tooltip = GameText.RefitInFleetTip;
            }
            x -= 130;
            RefitOne = ButtonMedium(x, buttonsY, text: GameText.RefitOne, click: OnRefitOneClicked);
            RefitOne.Tooltip = GameText.RefitOnlyThisShipTo;

            RefitOne.Enabled = RefitAll.Enabled = false;
            if (RefitInFleet != null)
                RefitInFleet.Enabled = false;

            ShipInfoOverlay = Add(new ShipInfoOverlayComponent(this, ShipToRefit.Universe));
            RefitShipList.OnHovered = (item) =>
            {
                ShipInfoOverlay.ShowToLeftOf(item?.Pos ?? Vector2.Zero, item?.Design);
            };
        }

        void OnRefitShipItemClicked(RefitShipListItem item)
        {
            RefitTo = item.Design;
            RefitOne.Enabled = RefitAll.Enabled = RefitTo != null;
            if (RefitInFleet != null)
                RefitInFleet.Enabled = RefitTo != null;
        }

        public override void ExitScreen()
        {
            Screen?.ResetStatus();
            base.ExitScreen();
        }

        void OnRefitOneClicked(UIButton b)
        {
            if (RefitTo == null)
                return;
            Player.AI.AddGoalAndEvaluate(GetRefitGoal(ShipToRefit));
            GameAudio.EchoAffirmative();
            ExitScreen();
        }

        void OnRefitAllClicked(UIButton b)
        {
            if (RefitTo == null)
                return;
            RefitAllShips();
            foreach (Fleet fleet in Player.AllFleets)
                fleet.RefitNodeName(ShipToRefit.Name, RefitTo.Name);

            GameAudio.EchoAffirmative();
            ExitScreen();
        }

        void RefitAllShips(Fleet specificFleet = null)
        {
            var ships = Player.OwnedShips;
            foreach (Ship ship in ships)
            {
                if (ship.Name == ShipToRefit.Name && (specificFleet == null || ship.Fleet == specificFleet))
                    Player.AI.AddGoalAndEvaluate(GetRefitGoal(ship));
            }

            foreach (Planet planet in Player.GetPlanets())
                planet.Construction.RefitShipsBeingBuilt(ShipToRefit, RefitTo);
        }

        void OnRefitFleetClicked(UIButton b)
        {
            if (RefitTo == null)
                return;
            ShipToRefit.Fleet?.RefitNodeName(ShipToRefit.Name, RefitTo.Name);
            RefitAllShips(ShipToRefit.Fleet);
            GameAudio.EchoAffirmative();
            ExitScreen();
        }

        Goal GetRefitGoal(Ship ship)
        {
            Goal refitShip;
            if (ShipToRefit.IsPlatformOrStation)
                refitShip = new RefitOrbital(ship, RefitTo, Player, Rush);
            else
                refitShip = new RefitShip(ship, RefitTo, Player, Rush);

            return refitShip;
        }
    }
}
