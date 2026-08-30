using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Audio;
using System;
using Ship_Game.GameScreens;
using Ship_Game.GameScreens.Universe.Debug;
using Ship_Game.Graphics; // RenderStates: the frame scissor
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using System.Linq;

namespace Ship_Game
{
    public sealed class ResearchScreenNew : GameScreen
    {
        public readonly UniverseScreen Universe;
        public readonly Empire Player;
        public Camera2D camera = new();

        readonly Map<string, RootNode> RootNodes = new(StringComparer.OrdinalIgnoreCase);
        public Map<string, TreeNode> SubNodes = new(StringComparer.OrdinalIgnoreCase);

        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;
        UIButton Search;
        // Ludoal fork: the slot the Search / Hide Queue pair sits in. UIButton stretches to
        // whatever rect it is given, so the width is pinned rather than taken from the texture.
        public const int ResearchButtonW = 150;
        // where the Search / Hide Queue pair sits, shared with ResearchQueueUIComponent so the two
        // buttons cannot drift apart. Ludoal fork.
        public float ResearchButtonY;
        public float ResearchButtonsRight;
        public const int ResearchButtonH = 24;
        Rectangle MainArea; // the frame's client rect - the node grids derive from it
        public EmpireUIOverlay empireUI;

        Vector2 MainMenuOffset;

        public ResearchQueueUIComponent Queue;

        int GridWidth  = 175;
        int GridHeight = 100;

        readonly Array<Vector2> ClaimedSpots = new();

        ResearchDebugUnlocks DebugUnlocks;

        public Color ApplyCurrentAlphaColor(Color color) => ApplyCurrentAlphaToColor(color, 100 / 255f);

        public ResearchScreenNew(GameScreen parent, UniverseScreen u, EmpireUIOverlay empireUi)
            : base(parent, toPause: u)
        {
            Universe = u;
            Player = u.Player;
            empireUI = empireUi;
            // IsPopup lets the map draw underneath, like every table/design screen.
            IsPopup = true;
            CanEscapeFromScreen = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        public override void LoadContent()
        {
            camera = new Camera2D { Pos = new Vector2(Viewport.Width, Viewport.Height) / 2f };
            // Ludoal fork: the Research tab of the Empire group. The node grids derive from
            // MainArea.Height, so they compress on their own down to the 900px floor.
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), ScreenGroups.TabIndexOf(this),
                                                    OnEmpireTabChanged, out Rectangle frame);
            RectF client = EmpireTabs.ClientArea;
            var main = new Rectangle((int)client.X, (int)client.Y, (int)client.W, (int)client.H);
            MainArea = main;
            // the client sits 9px inside the frame border, so +1 puts the nodes 10px off the
            // BORDER. The Y here only serves the ROOT column - the tech rows' Y is re-derived
            // from the row count in SubGridHeight (margins mirror the inter-row gap).
            MainMenuOffset = new Vector2(main.X + 1, main.Y + 24);

            RootNodes.Clear();
            SubNodes.Clear();

            int numDiscoveredRoots = Player.TechEntries.Count(t => t.IsRoot && t.Discovered);

            // roots pitch: first anchor at the offset, the last one's 76px body ends 2px
            // above the floor - the pitch divides what remains between the anchors, and never
            // compresses below the body plus a breath (bench 408): the frame scissor clips
            // what overruns instead
            GridHeight = numDiscoveredRoots > 1 ? Math.Max((main.Height - 102) / (numDiscoveredRoots - 1), 80)
                                                : main.Height - 102;

            Vector2 nodePos = Vector2.Zero;

            var rootTechs = Player.TechEntries.Filter(t => t.IsRoot && t.Discovered);
            // sort the techs
            rootTechs = rootTechs.Sorted(t => t.Tech.RootNode);

            foreach (TechEntry tech in rootTechs)
            {
                nodePos.X = 0f;
                nodePos.Y = FindDeepestY() + 1;
                SetRootNode(tech, ref nodePos);
            }

            GridHeight = SubGridHeight(6);

            if (!RootNodes.TryGetValue(Universe.UState.ResearchRootUIDToDisplay, out RootNode root))
                root = RootNodes.Values.FirstOrDefault() ?? throw new("ResearchScreen has no RootNodes");

            PopulateNodesFromRoot(root);

            // Create queue once all techs are populated. 34 down: the frame's close cross sits
            // on the client's first lane. The button pair closes 10px above the frame BORDER -
            // the client bottom is 9px inside it - and the queue stretches down to meet the buttons.
            var queue = new Rectangle(main.X + main.Width - 340, main.Y + 34, 330,
                                      main.Height - 34 - 8 - ResearchButtonH - 1);
            // Ludoal fork: both buttons hang off the QUEUE rect, one on each of its edges, so they
            // stay level and evenly spaced. UIButton stretches to its rect, so the width is pinned.
            // ⚠ set BEFORE the queue component is built: it reads them in its own constructor.
            ResearchButtonY = queue.Bottom + 8;
            ResearchButtonsRight = queue.Right;
            Queue = Add(new ResearchQueueUIComponent(this, queue));
            Search = Add(new UIButton(ButtonStyle.WideActive,
                                      new Vector2(queue.X, ResearchButtonY), "Search"));
            Search.Rect = new RectF(queue.X, ResearchButtonY, ResearchButtonW, ResearchButtonH);
            Search.OnClick = OnSearchButtonClicked;

            DebugUnlocks = Add(new ResearchDebugUnlocks(Universe, () =>
            {
                Universe.UState.ResearchRootUIDToDisplay = GetCurrentlySelectedRootNode().Entry.UID;
                ReloadContent();
            }));
            DebugUnlocks.AxisAlign = Align.BottomRight;
            DebugUnlocks.SetLocalPos(-Queue.Width - 50, -25);

            base.LoadContent();
        }

        public override void Update(float fixedDeltaTime)
        {
            DebugUnlocks.Visible = Universe.Debug || Universe is DeveloperUniverse;
            base.Update(fixedDeltaTime);
        }

        public void OnSearchButtonClicked(UIButton button)
        {
            ScreenManager.AddScreen(new SearchTechScreen(this));
        }

        // Ludoal fork: the other tabs live in their own screen, so leaving Research hands over to
        // it. Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // The universe shows behind Research (IsPopup), so the tech tree needs an opaque
            // backdrop to stay legible - fill the FRAME only, not the whole screen. Opaque
            // (14,12,9), not the 0.92 GroupFrameFill, for crispness.

            batch.SafeBegin();
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), new Color(14, 12, 9));
            batch.SafeEnd();

            // the tree clips to the frame (bench 408): with the pan unclamped from the row
            // grid, nodes can sit past the frame edge and must not draw over the map below
            RenderStates.EnableScissorTest(batch.GraphicsDevice, ScreenGroups.GroupFrameFillRect(EmpireTabs));
            batch.SafeBegin(SpriteBlendMode.AlphaBlend, sortImmediate:false, RenderStates.ScissorEnabled, camera.Transform);
            {
                DrawConnectingLines(batch);

                foreach (RootNode rootNode in RootNodes.Values)
                {
                    rootNode.Draw(batch);
                }

                // the hovered tech draws last so it rides above its neighbours - at the 900p
                // floor the nodes pack tight enough to overlap
                TreeNode hovered = null;
                foreach (TreeNode treeNode in SubNodes.Values)
                {
                    if (treeNode.State == NodeState.Hover)
                        hovered = treeNode;
                    else
                        treeNode.Draw(batch);
                }
                hovered?.Draw(batch);
            }
            batch.SafeEnd();

            batch.SafeBegin();
            base.Draw(batch, elapsed);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            if (ScreenHeight > 720)
                empireUI.Draw(batch); // Ludoal fork: live top bar (paused indicator included)
            batch.SafeEnd();
        }

        static Vector2 CenterBetweenPoints(Vector2 left, Vector2 right)
        {
            return left.LerpTo(right, 0.5f).Rounded();
        }

        RootNode GetCurrentlySelectedRootNode()
        {
            foreach (RootNode root in RootNodes.Values)
                if (root.nodeState == NodeState.Press)
                    return root;
            return null;
        }

        // the center-right connector point of the parent node
        Vector2 GetParentConnectorPoint(Node parent)
        {
            return (parent is RootNode root) ? root.RightPoint : ((TreeNode)parent).RightPoint;
        }

        Vector2 GetBranchMidPoint(Node parent)
        {
            Vector2 parentNode = GetParentConnectorPoint(parent);

            // for the ROOT nodes, the midpoint is a bit closer
            if (parent is RootNode)
                return new(parentNode.X + (int)(GridWidth / 3), parentNode.Y);
            return new(parentNode.X + (int)(GridWidth / 2), parentNode.Y);
        }

        void DrawLinesFromParentToChild(SpriteBatch batch, Node parent, TechEntry child)
        {
            if (SubNodes.TryGetValue(child.UID, out TreeNode node))
            {
                Vector2 branchMidPoint = GetBranchMidPoint(parent);
                Vector2 verticalEnd = new(branchMidPoint.X, node.BaseRect.CenterY - 10);
                Vector2 endPos = new(node.BaseRect.X + 13f, verticalEnd.Y);

                // draw the vertical line which connects us from branch middle junction towards the child tech
                DrawResearchLineVertical(batch, branchMidPoint, verticalEnd, child.Unlocked);

                // draw the final horizontal connection from middle junction to endPos
                DrawResearchLineHorizontal(batch, verticalEnd, endPos, child.Unlocked, gradient: true);
            }
        }

        void DrawLineFromParentToBranchMiddle(SpriteBatch batch, Node parent, bool anyTechsComplete)
        {
            // from parent node to the middle of the branch junction
            Vector2 parentNode = GetParentConnectorPoint(parent);
            Vector2 branchMidPoint = GetBranchMidPoint(parent);
            DrawResearchLineHorizontal(batch, parentNode, branchMidPoint, anyTechsComplete, gradient:false);
        }

        void DrawConnectingLinesFromParentToChildren(SpriteBatch batch, Node parent)
        {
            bool anyTechsComplete = false;
            bool discoveredAny = false;
            foreach (TechEntry maybeUndiscovered in parent.Entry.Children)
            {
                // scan from `maybeUndiscovered` (inclusive) until we find a discovered tech
                // this would skip over any secret techs in the middle 
                TechEntry toTech = maybeUndiscovered.FindNextDiscoveredTech(Player);
                if (toTech != null)
                {
                    discoveredAny = true;
                    anyTechsComplete |= toTech.Unlocked;
                    DrawLinesFromParentToChild(batch, parent, toTech);
                }
            }

            // from parent tech to the middle of the branch junction
            if (discoveredAny)
            {
                DrawLineFromParentToBranchMiddle(batch, parent, anyTechsComplete);
            }
        }

        void DrawConnectingLines(SpriteBatch batch)
        {
            RootNode root = GetCurrentlySelectedRootNode();

            DrawConnectingLinesFromParentToChildren(batch, root);
            foreach (TreeNode from in SubNodes.Values)
            {
                DrawConnectingLinesFromParentToChildren(batch, from);
            }
        }

        static void DrawResearchLineHorizontal(SpriteBatch batch, Vector2 left, Vector2 right, bool complete, bool gradient)
        {
            if (left.X > right.X) // top must have lower X
                Vectors.Swap(ref left, ref right);

            SubTexture texture;
            if (gradient)
            {
                texture = ResourceManager.Texture(complete
                        ? "ResearchMenu/grid_horiz_gradient_complete"
                        : "ResearchMenu/grid_horiz_gradient");
            }
            else
            {
                texture = ResourceManager.Texture(complete
                        ? "ResearchMenu/grid_horiz_complete"
                        : "ResearchMenu/grid_horiz");
            }

            RectF r = new(left.X + 5, left.Y - 2, (right.X - left.X) - 5, 5);
            //batch.Draw(texture, r, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);
            batch.Draw(texture, r, Color.White);

            // fill a small rectangle at the beginning of the research line
            // to cover up some stupid artifacts caused by XNA transparent sprite renderer
            batch.FillRectangle(new Rectangle((int)left.X, (int)left.Y, 5, 1), (complete ? new(110, 171, 227) : new(194, 194, 194)));
        }

        static void DrawResearchLineVertical(SpriteBatch batch, Vector2 top, Vector2 bottom, bool complete)
        {
            if (top.Y > bottom.Y) // top must have lower Y
                Vectors.Swap(ref top, ref bottom);

            SubTexture texture = ResourceManager.Texture(complete
                               ? "ResearchMenu/grid_vert_complete"
                               : "ResearchMenu/grid_vert");

            // shift the line down a bit to avoid overlapping transparency artifacts
            int offsetY = 1;
            RectF r = new(top.X - texture.CenterX, top.Y + offsetY, texture.Width, (bottom.Y - top.Y) - offsetY);
            //batch.Draw(texture, r, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);
            batch.Draw(texture, r, Color.White);
        }


        public override void ExitScreen()
        {
            Universe.UState.ResearchRootUIDToDisplay = GetCurrentlySelectedRootNode().Entry.UID;
            base.ExitScreen();
        }

        int FindDeepestY()
        {
            int deepest = 0;
            foreach (RootNode root in RootNodes.Values)
                if (root.NodePosition.Y > deepest)
                    deepest = (int) root.NodePosition.Y;
            return deepest;
        }

        int FindDeepestYSubNodes()
        {
            int deepest = 0;
            foreach (TreeNode node in SubNodes.Values)
                if (node.NodePosition.Y > deepest)
                    deepest = (int)node.NodePosition.Y;
            return deepest;
        }

        public override bool HandleInput(InputState input)
        {
            if (ScreenHeight > 720 && empireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            // the tree drag stops at the page frame - a middle-drag in the band pans the MAP.
            // Pan clamps to the tree's real extent (bench 408): the neutral seat is the low
            // bound (the tree never pans right/down of its origin), and the far bound is what
            // overruns the frame, zero when everything already fits.
            if (input.MiddleMouseHeld() && PageFrame.HitTest(input.CursorPosition))
            {
                float maxX = 0, maxY = 0;
                foreach (TreeNode n in SubNodes.Values)
                {
                    maxX = Math.Max(maxX, n.BaseRect.Right);
                    maxY = Math.Max(maxY, n.BaseRect.Bottom);
                }
                var overrun = new Vector2(Math.Max(0, maxX + 10 - MainArea.Right),
                                          Math.Max(0, maxY + 10 - MainArea.Bottom));
                camera.MoveClamped(input.CursorVelocity, ScreenCenter, ScreenCenter + overrun);
            }

            // maintainer bench 444: the wheel scrolls the tree vertically when it overruns
            // the frame - the same clamped camera move as the middle-drag, vertical axis only
            if (PageFrame.HitTest(input.CursorPosition) && !Queue.HitTest(input.CursorPosition)
                && (input.ScrollIn || input.ScrollOut))
            {
                float wheelMaxY = 0;
                foreach (TreeNode n in SubNodes.Values)
                    wheelMaxY = Math.Max(wheelMaxY, n.BaseRect.Bottom);
                float overrunY = Math.Max(0, wheelMaxY + 10 - MainArea.Bottom);
                if (overrunY > 0)
                {
                    camera.MoveClamped(new Vector2(0, input.ScrollIn ? -70 : 70),
                                       ScreenCenter, ScreenCenter + new Vector2(0, overrunY));
                    return true;
                }
            }

            foreach (RootNode root in RootNodes.Values)
            {
                if (root.HandleInput(input,camera))
                {
                    GameAudio.ResearchSelect();
                    PopulateNodesFromRoot(root);
                    return true;
                }
            }

            foreach (TreeNode node in SubNodes.Values)
            {
                if (node.HandleInput(input, ScreenManager, camera, Universe))
                {
                    if (input.LeftMouseClick && !input.RightMouseClick)
                    {
                        OnTechNodeClicked(node.Entry);
                    }
                    return true; // input captured
                }
            }

            if (!Queue.HitTest(input.CursorPosition) && (input.ResearchExitScreen || input.RightMouseClick))
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            return base.HandleInput(input);
        }

        void OnTechNodeClicked(TechEntry tech)
        {
            if (!tech.CanBeResearched)
            {
                // this tech cannot be researched
                GameAudio.NegativeClick();
                return;
            }
            
            bool added = false;
            
            if (!Player.Research.IsQueued(tech.UID))
            {
                GameAudio.ResearchSelect();
                Player.Research.AddTechToQueue(tech.UID);
                added = true;
            }
            
            // if ctrl is held down, move tech to top of queue ALWAYS, even if it was already in queue (imo good UX)
            if(GameBase.ScreenManager.input != null && GameBase.ScreenManager.input.IsCtrlKeyDown)
            {
                int index = Player.Research.IndexInQueue(tech.UID);
                int moved = Player.Research.MoveToTopWithPreReqs(index);
                if (moved == 0)
                {
                    GameAudio.NegativeClick();
                }
            }
            
            // if CTRL was not held down, and tech is in queue (but not added right now), remove it
            else
            {
                if (!added)
                {
                    Player.Research.RemoveTechFromQueue(tech.UID);
                }
            }
            
            Queue.ReloadResearchQueue();
        }

        Vector2 GridSize => new(GridWidth, GridHeight);

        // the tree PANS, so rows never compress (bench 408): the grid step keeps at least
        // MinGap between rows and the tree simply overruns the frame on small displays -
        // panning reaches the rest. Top/bottom margins mirror the inter-row gap.
        // ⚠ the node's VISIBLE height is 108, not its 120 rect: the base texture carries
        // ~12px of transparent padding at its foot (92x90 png, content stops at row 79).
        // ⚠ this also OWNS MainMenuOffset.Y - the first anchor rides the margin.
        int SubGridHeight(int rows)
        {
            rows = Math.Max(1, rows);
            const int NodeVisible = 108; // 22 title plate + 86 of visible body art
            const int MinGap = 4;
            int gh;
            if (rows == 1)
                gh = MainArea.Height - 104; // one row: the step only seats the anchor
            else
                gh = Math.Max((MainArea.Height + NodeVisible) / (rows + 1), // margin == inter-row gap
                              NodeVisible + MinGap);
            int margin = Math.Max(MinGap, gh - NodeVisible);
            if (rows == 1)
                margin = 2;
            MainMenuOffset.Y = MainArea.Y + margin + 22;
            return gh;
        }

        Vector2 GetCurrentCursorOffset(in Vector2 cursorPos, float yOffset = 0)
        {
            var cursor = new Vector2(cursorPos.X, cursorPos.Y + yOffset);
            return (MainMenuOffset + cursor*GridSize).Rounded();
        }

        void PopulateNodesFromRoot(RootNode root)
        {
            foreach (RootNode node in RootNodes.Values)
                node.nodeState = (node == root) ? NodeState.Press : NodeState.Normal;

            int rows = 1;
            int cols = CalculateTreeDimensionsFromRoot(root.Entry, ref rows, 0, 0);
            GridHeight = SubGridHeight(Math.Min(rows, 9));

            if (cols > 0 && cols < 9) GridWidth = (MainArea.Width - 330) / cols;
            else                      GridWidth = 165;

            BuildSubNodes(root);

            // Ludoal fork: the row ESTIMATE overcounts branches that merge back into the
            // main line, so some tabs squeezed toward the top with dead space below.
            // Measure the rows actually laid out and rebuild once at the exact height.
            // +1: the deepest node needs its own height below its anchor.
            int actualRows = Math.Max(1, FindDeepestYSubNodes());
            int wantRows = Math.Min(actualRows + 1, 9);
            if (wantRows != Math.Min(rows, 9))
            {
                GridHeight = SubGridHeight(wantRows);
                BuildSubNodes(root);
            }
        }

        void BuildSubNodes(RootNode root)
        {
            SubNodes.Clear();
            ClaimedSpots.Clear();

            var nodePos = new Vector2(1f, 1f);
            bool first = true;

            foreach (TechEntry child in root.Entry.Children)
            {
                if (!child.Discovered)
                    continue;

                nodePos.X = root.NodePosition.X + 1f;
                nodePos.Y = first ? FindDeepestYSubNodes()
                                  : FindFreeRowFor(child, 0, (int)nodePos.X); // scan from the TAB top — the root's own Y is its slot in the left category list, not a row of this canvas
                if (first) first = false;

                if (!SubNodes.ContainsKey(child.UID)) // only ever add unique entries
                {
                    var newNode = new TreeNode(GetCurrentCursorOffset(nodePos), child, this) { NodePosition = nodePos };
                    SubNodes[newNode.Entry.UID] = newNode;
                    PopulateNodesFromSubNode(newNode, ref nodePos);
                }
            }
        }

        void PopulateNodesFromSubNode(Node node, ref Vector2 nodePos)
        {
            UpdateCursorAndClaimedSpots(ref nodePos, node.Entry.Discovered);

            bool first = true;
            foreach (TechEntry child in node.Entry.Children)
            {
                nodePos.X = node.NodePosition.X + 1f;
                nodePos.Y = first ? FindDeepestYSubNodes()
                                  : FindFreeRowFor(child, (int)node.NodePosition.Y, (int)nodePos.X);
                if (first) first = false;

                if (child.Discovered && !SubNodes.ContainsKey(child.UID))
                {
                    var newNode = new TreeNode(GetCurrentCursorOffset(nodePos), child, this) { NodePosition = nodePos };
                    SubNodes[newNode.Entry.UID] = newNode;
                    PopulateNodesFromSubNode(newNode, ref nodePos);
                }
            }
        }

        void SetRootNode(TechEntry tech, ref Vector2 nodePos)
        {
            UpdateCursorAndClaimedSpots(ref nodePos, true);

            RootNodes[tech.UID] = new RootNode(GetCurrentCursorOffset(nodePos, -1), tech)
            {
                NodePosition = nodePos,
                isResearched = tech.Unlocked
            };
        }
        
        void UpdateCursorAndClaimedSpots(ref Vector2 nodePos, bool addToClaimed)
        {
            if (PositionIsClaimed(nodePos))
                nodePos.Y += 1f;
            else if (addToClaimed)
                ClaimedSpots.Add(nodePos);
        }
        
        bool PositionIsClaimed(Vector2 position) => ClaimedSpots.Any(p => p.AlmostEqual(position));

        // Ludoal fork: a branch takes the first row at or below its parent where its
        // whole rectangle (own rows x own columns) is free.
        int FindFreeRowFor(TechEntry branch, int parentY, int col)
        {
            int bRows = 1;
            int bCols = CalculateTreeDimensionsFromRoot(branch, ref bRows, 0, 0);
            for (int y = parentY; ; ++y)
            {
                bool freeRect = true;
                for (int dy = 0; dy < bRows && freeRect; ++dy)
                    for (int dx = 0; dx < bCols && freeRect; ++dx)
                        if (PositionIsClaimed(new Vector2(col + dx, y + dy)))
                            freeRect = false;
                if (freeRect)
                    return y;
            }
        }

        //Added by McShooterz: find size of tech tree before it is built
        int CalculateTreeDimensionsFromRoot(TechEntry techEntry, ref int rows, int cols, int colmax)
        {
            cols++;
            if (cols > colmax)
                colmax = cols;

            TechEntry[] children = techEntry.Children;

            // look for branches and make space for them
            if (children.Length > 0)
            {
                int rowCount = 0;
                // don't count the main branch. use the branch that starts here.
                for (int i = 1; i < children.Length; i++)
                {
                    var discovered = children[i].FindNextDiscoveredTech(Player);
                    if (discovered != null)
                        rowCount++;
                }
                rows += rowCount;
            }

            foreach (TechEntry maybeUndiscovered in children)
            {
                // TODO: not sure why this pattern is used here?
                // scan from `maybeUndiscovered` (inclusive) until we find a discovered tech
                var discovered = maybeUndiscovered.FindNextDiscoveredTech(Player);
                if (discovered != null)
                {
                    int max = CalculateTreeDimensionsFromRoot(discovered, ref rows, cols, colmax);
                    if (max > colmax)
                        colmax = max;
                }
                else
                {
                    CalculateTreeDimensionsFromRoot(maybeUndiscovered, ref rows, cols, colmax);
                }
            }
            return colmax;
        }
    }
}