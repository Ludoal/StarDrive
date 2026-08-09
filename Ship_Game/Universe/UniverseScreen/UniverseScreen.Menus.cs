using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.Ships;

namespace Ship_Game
{
    public partial class UniverseScreen
    {
        void CreatePieMenu()
        {
            pieMenu = new PieMenu();
            planetMenu = new PieMenuNode();
            shipMenu = new PieMenuNode();
        }

        void LoadPieMenuNodesForPlanet(Planet p)
        {
            planetMenu.Children?.Clear();
            var viewPlanet = ResourceManager.Texture("UI/viewPlanetIcon");
            planetMenu.Add(new(GameText.ViewPlanet, viewPlanet, () => SnapViewColony(p, false)));

            if (p.Habitable)
            {
                var colonize = ResourceManager.Texture("UI/ColonizeIcon");
                if (p.Owner == null)
                {
                    planetMenu.Add(new(GameText.MarkForColonization, colonize, () => MarkForColonization(p)));
                }
                planetMenu.Add(new(GameText.TacticalView, colonize, () => OpenCombatMenu(p)));
            }

            pieMenu.Show(planetMenu);
        }

        void LoadPieMenuShipNodes(Ship s)
        {
            if (s is {CanBeScrapped: false})
                return;

            shipMenu.Children?.Clear();
            if (s.Loyalty.isPlayer)
            {
                var orders = new PieMenuNode(GameText.Orders2, ResourceManager.Texture("UI/OrdersIcon"), null);
                orders.Add(new(GameText.GoExploring, ResourceManager.Texture("UI/marketIcon"), () => DoExplore(s)));
                //orders.Add(new("Empire Defense", ResourceManager.Texture("UI/PatrolIcon"), () => DoDefense(s)));
                shipMenu.Add(orders);

                var followIcon = ResourceManager.Texture("UI/FollowIcon");
                var holdPosition = ResourceManager.Texture("UI/HoldPositionIcon");
                var other = new PieMenuNode(GameText.Other, followIcon, null);
                if (s is {IsPlatformOrStation: false, IsSubspaceProjector: false, CanBeScrapped: true})
                {
                    other.Add(new(GameText.RefitTo, followIcon, () => RefitTo(s)));
                }
                if (s is {IsPlatformOrStation: true})
                {
                    other.Add(new("Scuttle", holdPosition, () => OrderScuttle(s)));
                }
                else if (s.ShipData.Role < RoleName.construction)
                {
                    other.Add(new(GameText.OrderScrap, holdPosition, () => OrderScrap(s)));
                }
                shipMenu.Add(other);
            }
            else
            {
                var viewPlanet = ResourceManager.Texture("UI/viewPlanetIcon");
                shipMenu.Add(new(GameText.ContactLeader, viewPlanet, () => ContactLeader(s)));
            }

            pieMenu.Show(shipMenu);
        }

        Vector2 GetPieMenuPosition()
        {
            if (SelectedShip != null)
                return ProjectToScreenPosition(SelectedShip.Position).ToVec2f();
            if (SelectedPlanet != null)
                return ProjectToScreenPosition(SelectedPlanet.Position3D).ToVec2f();
            return ScreenManager.ScreenCenter;
        }

        public void OpenCombatMenu(Planet planet)
        {
            bool doReturnToShip = ViewingShip;
            SetSelectedPlanet(planet);
            returnToShip = doReturnToShip;

            workersPanel = new CombatScreen(this, planet);

            // no camera snap: the ground battle view covers the map (Ludoal fork);
            // anchor the close-restore to the current camera (see SnapViewColony)
            transitionStartPosition = CamPos;
            CamDestination = CamPos;
            LookingAtPlanet = true;
        }

        // Ludoal fork: returns the flipped state instead of taking a ref - the overlay flags
        // are UState-backed properties now, and a property cannot be passed by ref.
        bool ToggleUIComponent(string audioCue, bool current)
        {
            GameAudio.PlaySfxAsync(audioCue);
            return !current;
        }

        public void InputOpenDeepSpaceBuildWindow()
        {
            if (!DeepSpaceBuildWindow.Visible)
            {
                GameAudio.AcceptClick();
                DeepSpaceBuildWindow.InitializeAndShow();
            }
            else
            {
                DeepSpaceBuildWindow.Hide();
            }
        }
    }
}
