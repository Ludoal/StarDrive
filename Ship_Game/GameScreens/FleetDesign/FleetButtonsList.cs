using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Fleets;

namespace Ship_Game.GameScreens.FleetDesign;

/// <summary>
/// List of fleet buttons on the Left side of the screen
/// </summary>
public class FleetButtonsList : UIList
{
    readonly bool IsFleetDesigner;
    bool IsUniverse => !IsFleetDesigner;
    readonly UniverseScreen Us;
    readonly Empire Player;
    readonly Array<FleetButton> Buttons = new();

    // Ludoal fork (maintainer design): the column shows TEN slots, at every resolution - the
    // window does not run to the display foot by design, so twenty never fit, not even at 1200.
    // A button in the column's head swaps the banks. No keyboard shortcut for now: Alt is taken
    // by the 11-20 fleet keys, and a hotkey the tooltip cannot name is a door with no handle.
    const int BankSize = 10;
    const int SlotPitch = 50;   // one button plus the padding the list adds
    public static float BarHeight => BankSize * SlotPitch;
    bool SecondBank;            // false: slots 1-10, true: 11-20
    bool InCurrentBank(int key) => SecondBank ? key > BankSize : key <= BankSize;
    UIButton BankSwap;

    public FleetButtonsList(RectF rect, GameScreen parent, UniverseScreen us,
                            Action<FleetButton> onClick,
                            Action<FleetButton> onHotKey,
                            Func<FleetButton, bool> isSelected)
        : base(rect, Color.Transparent)
    {
        Us = us;
        Player = us.Player;
        LayoutStyle = ListLayoutStyle.Clip;
        IsFleetDesigner = parent is FleetDesignScreen;

        Vector2 buttonSize = new(52, 48);
        for (int key = Empire.FirstFleetKey; key <= Empire.LastFleetKey; ++key)
        {
            FleetButton b = new(us, key, buttonSize)
            {
                FleetDesigner = IsFleetDesigner,
                OnClick = onClick,
                OnHotKey = onHotKey,
                IsSelected = isSelected
            };
            Buttons.Add(b);
            base.Add(b);
        }

        if (IsFleetDesigner)
        {
            // the list's Header sits above the items, which is exactly where a bank switch
            // belongs. DynamicText so the label follows the bank without a second source.
            BankSwap = new UIButton(ButtonStyle.Small, "1-10")
            {
                DynamicText = () => BankLabel,
                Tooltip = "Switch between fleets 1-10 and 11-20",
                OnClick = _ => ToggleBank()
            };
            Header = BankSwap;
        }

        base.PerformLayout();
        // Ludoal fork: no slide-in/slide-out on the fleet buttons. They appear where the layout puts them.
    }

    // In some conditions, the fleet buttons should automatically be disabled
    bool ShouldHideInUniverse => Us.LookingAtPlanet;
    bool ShouldHide => (IsUniverse && ShouldHideInUniverse)
                    || Us.DefiningAO // FB dont show fleet list when selected AOs and Trade Routes
                    || Us.DefiningTradeRoutes;

    bool IsInputDisabled => (IsUniverse && Us.pieMenu.Visible);

    string BankLabel => SecondBank ? "11-20" : "1-10";

    void ToggleBank()
    {
        SecondBank = !SecondBank;
        RequiresLayout = true;
    }

    // Ludoal fork: no display gate here any more (maintainer decision). Every key answers;
    // what the resolution decides is how many slots the column SHOWS at once, not how many
    // fleets the player may command.
    static int InputFleetSelection(InputState input)
    {
        if (input.Fleet1)  return 1;
        if (input.Fleet2)  return 2;
        if (input.Fleet3)  return 3;
        if (input.Fleet4)  return 4;
        if (input.Fleet5)  return 5;
        if (input.Fleet6)  return 6;
        if (input.Fleet7)  return 7;
        if (input.Fleet8)  return 8;
        if (input.Fleet9)  return 9;

        {
            if (input.Fleet10) return 10;
            if (input.Fleet11) return 11;
            if (input.Fleet12) return 12;
            if (input.Fleet13) return 13;
            if (input.Fleet14) return 14;
            if (input.Fleet15) return 15;
            if (input.Fleet16) return 16;
            if (input.Fleet17) return 17;
            if (input.Fleet18) return 18;
            if (input.Fleet19) return 19;
            if (input.Fleet20) return 20;
        }

        return -1;
    }

    public override bool HandleInput(InputState input)
    {
        if (ShouldHide || IsInputDisabled)
            return false;

        foreach (FleetButton b in Buttons)
        {
            // always handle hotkeys, since they can be used to create new fleets
            if (InputFleetSelection(input) == b.FleetKey)
            {
                b.OnHotKey?.Invoke(b);
                return true;
            }
        }

        return base.HandleInput(input);
    }

    public override void Update(float fixedDeltaTime)
    {
        if (ShouldHide)
            return;

        foreach (FleetButton b in Buttons)
        {
            Fleet f = Player.GetFleetOrNull(b.FleetKey);
            // Ludoal fork: the workshop shows the SLOTS, empty ones included - an empty slot
            // is how a fleet is created, and one that draws nothing is a door with no handle.
            // The map keeps showing only fleets that exist, and that carry ships.
            bool visible = IsFleetDesigner ? InCurrentBank(b.FleetKey)
                                           : f != null && f.CountShips > 0;

            // make sure to do layout if any visibility changes
            RequiresLayout |= (visible != b.Visible);
            b.Visible = visible;
        }

        base.Update(fixedDeltaTime);
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        if (ShouldHide)
            return;

        base.Draw(batch, elapsed);
    }
}
