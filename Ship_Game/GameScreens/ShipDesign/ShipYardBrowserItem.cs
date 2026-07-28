using System;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics; // Premultiplied() lives here (MathExt)
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Ships;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork: one row of the merged shipyard browser, which replaces the old
    // "SELECT HULL" list plus the separate "SELECT DESIGN" popup by a single list
    // grouped by hull. A row is exactly one of three kinds:
    //
    //   Header    the hull group header       (hull icon + hull name + hull role)
    //   BareHull  the empty hull itself       — always row #1 inside its own group
    //   Design    a design built on that hull — carries a role badge
    //
    // The two roles are distinct fields of the data model and never disagree:
    // ShipHull.Role / IShipDesign.HullRole is the role of the carcass (Cruiser),
    // IShipDesign.Role is the role expressed by the modules actually fitted
    // (Carrier, Colony, Scout) — the badge shows the latter.
    public class ShipYardBrowserItem : ScrollListItem<ShipYardBrowserItem>
    {
        readonly Empire Player;

        public readonly ShipHull Hull;         // set on Header and BareHull rows
        public readonly IShipDesign Design;    // set on Design rows
        public readonly bool IsBareHull;
        public readonly bool IsWIP;
        // Ludoal fork (bench 46.152): the badge carries what the GROUP HEADING does not
        // say. Grouped by hull, the heading is the hull class, so the badge shows the role;
        // grouped by role, the heading is the role, so it shows the hull instead. Same pill,
        // no repetition either way (Ludo).
        public bool ShowHullInBadge;
        public readonly bool CanBeBuilt;

        public bool IsDesign => Design != null;

        // the hull group header
        public ShipYardBrowserItem(Empire player, ShipHull hull, string headerText) : base(headerText)
        {
            Player = player;
            Hull = hull;
        }

        // the bare hull, first row of its group: loading it starts a design from scratch
        public ShipYardBrowserItem(Empire player, ShipHull hull)
        {
            Player = player;
            Hull = hull;
            IsBareHull = true;
            CanBeBuilt = true;
        }

        // a design. Delete / research affordances are passed in so this row stays
        // independent of whichever screen hosts the list.
        public ShipYardBrowserItem(Empire player, IShipDesign design, bool isWIP,
                                   Action onDelete = null, Action onResearch = null,
                                   Action onDeleteAllWipVersions = null)
        {
            Player = player;
            Design = design;
            IsWIP = isWIP;
            CanBeBuilt = player.WeCanBuildThis(design);

            if (isWIP)
            {
                if (onDelete != null)
                    AddCancel(new(-30, -45), "Delete this WIP Design", onDelete);
                if (onDeleteAllWipVersions != null)
                    AddDelete(new(-30, 15), "Delete all related versions of this WIP Design", onDeleteAllWipVersions);
            }
            else
            {
                if (onDelete != null && !design.IsReadonlyDesign && !design.IsFromSave)
                    AddCancel(new(-30, 0), "Delete this Ship Design", onDelete);
                if (onResearch != null && !CanBeBuilt)
                    AddResearch(new(-50, 0), "Research This Ship", onResearch);
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            int h = (int)Height;
            if (Design != null)
            {
                // Ludoal fork (bench 46.152): the gesture hint belongs to the ROW. Hung off the
                // list's rect it appeared wherever the cursor first entered and stayed there,
                // and it fired over the class headers too - those carry no Design, so they no
                // longer qualify. The comparison line only shows when the feature is on.
                if (HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GlobalStats.ShipyardComparison
                        ? "Double-click to load\nShift-click to pin for comparison"
                        : "Double-click to load");

                batch.Draw(Design.Icon, new Rectangle((int)X - 2, (int)Y - 2, h + 4, h + 4), Color.White);

                // Ludoal fork: red for obsolete, the same code the module list uses, and it wins
                // over the not-buildable grey — a design you have retired is a stronger statement
                // than one you cannot afford yet (Ludo).
                Color nameColor = Player.IsDesignObsolete(Design.Name) ? Color.Red
                                : CanBeBuilt ? Color.White : Color.Gray;
                batch.DrawString(Fonts.Arial12Bold, Design.Name, X + h + 6, Y + 2, nameColor);

                // role badge: the modules-derived role, in the warm pill the game's
                // other tables use for separators
                string tag = ShowHullInBadge
                           ? (ResourceManager.Hull(Design.Hull, out ShipHull h2) ? h2.VisibleName : Design.Hull)
                           : Localizer.GetRole(Design.Role, Player);
                DrawBadge(batch, tag, X + h + 8, Y + 16);

                if (IsWIP)
                    DrawBadge(batch, "WIP", X + h + 12 + Fonts.Arial8Bold.TextWidth(tag), Y + 16, Color.DarkOrange);
            }
            else if (IsBareHull)
            {
                // The group heading is the hull CLASS (Fighter, Corvette, Frigate — the
                // tech-tree ones), so this row is where the hull states its own name, e.g.
                // "Fang Fighter". No class tag here: the heading above already said it, and
                // it would be the same on every row of the group. A header row itself gets
                // nothing drawn by us — the base class renders its title in a larger font
                // and anything of ours lands on top of it.
                // Ludoal fork (bench 46.157): the bare hull rows had no hint at all — the tooltip
                // lived in the Design branch, and a hull carries no Design (Ludo). No comparison
                // line here: pinning compares designs, and an empty hull is not one.
                if (HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip("Double-click to start a new design on this hull");

                batch.Draw(Hull.Icon, new Rectangle((int)X - 2, (int)Y - 2, h + 4, h + 4), Color.White);
                batch.DrawString(Fonts.Arial12Bold, Hull.VisibleName, X + h + 6, Y + 2);
                DrawBadge(batch, "empty hull", X + h + 8, Y + 16, Color.Gray);
            }
        }

        void DrawBadge(SpriteBatch batch, string text, float x, float y, Color? fill = null)
        {
            int w = (int)Fonts.Arial8Bold.TextWidth(text);
            int lh = Fonts.Arial8Bold.LineSpacing;
            var pill = new Rectangle((int)x - 3, (int)y, w + 6, lh);
            batch.FillRectangle(pill, (fill ?? new Color(118, 102, 67)).Premultiplied());
            batch.DrawString(Fonts.Arial8Bold, text, x, y, Colors.Cream);
        }
    }
}
