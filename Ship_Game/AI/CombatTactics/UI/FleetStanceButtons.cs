using SDUtils;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.AI.CombatTactics.UI
{
    public class FleetStanceButtons : StanceButtons
    {
        Array<FleetDataNode> SelectedNodes = new Array<FleetDataNode>();
        public FleetStanceButtons(GameScreen screen, Vector2 position) : base(screen, position){}
        
        public void ResetButtons(FleetDataNode node)
        {
            ResetButtons(new Array<FleetDataNode>() { node });
        }

        public void ResetButtons(Array<FleetDataNode> nodes)
        {
            SelectedNodes = nodes;
            if (nodes.IsEmpty)
                Reset(new CombatState[0]);
            else
                Reset(nodes.Select(n => n.CombatState));

        }

        protected override void ApplyStance(CombatState stance)
        {
            // the stance is written on the ships' AI, so it goes through the simulation thread,
            // as the ship stance buttons do; a node with no ship is design data and is written here
            var nodes = SelectedNodes;
            Ship first = null;
            foreach (var node in nodes)
            {
                if (node.Ship != null) { first = node.Ship; break; }
            }
            if (first?.Universe.Screen == null)
            {
                foreach (var node in nodes)
                    node.SetCombatStance(stance);
                return;
            }
            first.Universe.Screen.RunOnSimThread(() =>
            {
                foreach (var node in nodes)
                    node.SetCombatStance(stance);
            });
        }

        protected override void OnOrderButtonHovered(OrdersToggleButton b) {}
    }
}