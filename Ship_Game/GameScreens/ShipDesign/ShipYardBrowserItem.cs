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
                batch.Draw(Design.Icon, new Rectangle((int)X - 2, (int)Y - 2, h + 4, h + 4), Color.White);

                Color nameColor = CanBeBuilt ? Color.White : Color.Gray;
                batch.DrawString(Fonts.Arial12Bold, Design.Name, X + h + 6, Y + 2, nameColor);

                // role badge: the modules-derived role, in the warm pill the game's
                // other tables use for separators
                string role = Localizer.GetRole(Design.Role, Player);
                DrawBadge(batch, role, X + h + 8, Y + 16);

                if (IsWIP)
                    DrawBadge(batch, "WIP", X + h + 12 + Fonts.Arial8Bold.TextWidth(role), Y + 16, Color.DarkOrange);
            }
            else if (IsBareHull)
            {
                // The group header already names the hull, in its own large font — drawing
                // anything of ours on a header row lands on top of it. So a header gets
                // nothing from us, and the bare-hull row needs no name either: it is the
                // carcass of the group it sits in. The hull's own type stays out too; the
                // role only earns its place on design rows, where it varies line to line.
                batch.Draw(Hull.Icon, new Rectangle((int)X - 2, (int)Y - 2, h + 4, h + 4), Color.White);
                DrawBadge(batch, "empty hull", X + h + 6, Y + 6, Color.Gray);
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
