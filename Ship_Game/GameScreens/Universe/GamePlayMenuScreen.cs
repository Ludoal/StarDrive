using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using Ship_Game.Audio;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.Graphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game;

/// <summary>
/// In-Game Menu for Load/Save/Options and Exit to Windows
/// </summary>
public sealed class GamePlayMenuScreen : PopupWindow
{
    readonly UniverseScreen Universe;
    UILabel SavingText;
    UIButton SaveButton;
    UIButton LoadButton;
    UIButton ExitToMainMenu;
    UIButton ExitToWindows;

    // Ludoal fork: 200x330 -> 380x360. Width is not cosmetic here: the popup frame's gradient
    // bands are 433px wide and only shrink to fit, so a window under ~493 gets a squeezed
    // band - and at 260 it was narrower than the frame's own furniture (maintainer
    // observation). 380 carries the 168px buttons with room either side.
    public GamePlayMenuScreen(UniverseScreen screen) : base(screen, 380, 360)
    {
        Universe = screen;
        TransitionOnTime  = 0.25f;
        TransitionOffTime = 0.25f;
    }

    public override void LoadContent()
    {
        // the window names itself in its own title bar; frame and close cross are
        // PopupWindow's - base.LoadContent goes FIRST and lays them out. Escape and O
        // still close it on top of the cross.
        TitleText = "Menu";
        base.LoadContent();

        Vector2 c = ScreenCenter;
        SavingText = Add(new UILabel(GameText.Saving, Fonts.Pirulen16, Color.White));
        SavingText.Visible = false;
        SavingText.TextAlign = TextAlign.Center;
        SavingText.Pos = new Vector2(c.X - SavingText.Size.X*0.5f,
            50 + Fonts.Pirulen16.LineSpacing * 2);

        // ⚠ derived from the frame, not from screen centre: the two used to be placed
        // independently, so widening the window left the buttons where they were.
        const float btnW = 168;
        UIList buttons = AddList(new Vector2(Rect.X + (Rect.Width - btnW) / 2,
                                             PopupFrame.ContentTop(Rect) + 12));
        buttons.Padding = new Vector2(2f, 12f);
        buttons.LayoutStyle = ListLayoutStyle.ResizeList;

        SaveButton = buttons.Add(ButtonStyle.Default, GameText.Save, Save_OnClick);
        LoadButton = buttons.Add(ButtonStyle.Default, GameText.LoadGame,   Load_OnClick);
        buttons.Add(ButtonStyle.Default, GameText.Options,   Options_OnClick);
        buttons.Add(ButtonStyle.Default, GameText.ReturnToGame, Return_OnClick);
        ExitToMainMenu = buttons.Add(ButtonStyle.Default, GameText.ExitToMainMenu, ExitToMain_OnClick);
        ExitToWindows = buttons.Add(ButtonStyle.Default, GameText.ExitToWindows, Exit_OnClick);
    }

    public override bool HandleInput(InputState input)
    {
        if (input.KeyPressed(Keys.O) && !GlobalStats.TakingInput)
        {
            GameAudio.EchoAffirmative();
            ExitScreen();
            return true;
        }

        return base.HandleInput(input);
    }

    public override void Update(float fixedDeltaTime)
    {
        // enable/disable buttons based on current status
        bool buttonsEnabled = !Universe.IsSaving && !IsExiting;
        SaveButton.Enabled = buttonsEnabled;
        LoadButton.Enabled = buttonsEnabled;
        ExitToMainMenu.Enabled = buttonsEnabled;
        ExitToWindows.Enabled = buttonsEnabled;

        SavingText.Enabled = Universe.IsSaving;
        if (SavingText.Enabled)
        {
            SavingText.Color = CurrentFlashColor;
        }
        base.Update(fixedDeltaTime);
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        // base.Draw paints the window frame and every child inside its own batch
        ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
        base.Draw(batch, elapsed);
    }

    // double layer of security, the Save/Load/Exit actions must be double-checked
    bool DisallowActions()
    {
        if (Universe.IsSaving || IsExiting)
        {
            GameAudio.NegativeClick();
            return true;
        }
        return false;
    }

    void Save_OnClick(UIButton button)
    {
        if (DisallowActions()) return;

        ScreenManager.AddScreen(new SaveGameScreen(Universe));
    }

    void Load_OnClick(UIButton button)
    {
        if (DisallowActions()) return;

        ExitScreen(); // exit before opening new screen
        ScreenManager.AddScreen(new LoadSaveScreen(Universe));
    }

    void Options_OnClick(UIButton button)
    {
        ScreenManager.AddScreen(new OptionsScreen(Universe)
        {
            TitleText  = Localizer.Token(GameText.Options),
            MiddleText = Localizer.Token(GameText.ChangeAudioVideoAndGameplay)
        });
    }

    void Return_OnClick(UIButton button)
    {
        ExitScreen(); 
    }

    void ExitToMain_OnClick(UIButton button)
    {
        if (DisallowActions()) return;

        // Leaving a game to MainMenu: the in-game content cache (RootContent +
        // ResourceManager.Textures, including decompressed DXT5 atlases in both RAM and
        // VRAM) stays pinned, and MainMenu's own atlas decompress allocates on top of it.
        // On memory-tight machines running Combined Arms that OOM'd inside
        // DxtReader.DecompressDXT5 even with a healthy managed heap (OS-level pressure).
        //
        // If both system RAM and VRAM have comfortable headroom right now, keep the cache:
        // fast exit, and a same-content new game reuses it. Otherwise free it first by
        // routing through GameLoadingScreen(resetResources:true) - the same safely-sequenced
        // UnloadAllData → UnloadGraphicsResources → boot LoadContent the mod-switch flow uses
        // (so MainMenu finds ResourceManager.Blank, Beam.BeamEffect, the Textures index, etc.
        // repopulated). The two are coupled: the fast path is only valid because we keep that
        // boot content loaded.
        if (MemoryPressure.HasHeadroomToKeepContent(Device))
            ScreenManager.GoToScreen(new MainMenuScreen(), clear3DObjects:true);
        else
            ScreenManager.GoToScreen(new GameLoadingScreen(showSplash:false, resetResources:true), clear3DObjects:true);
    }

    void Exit_OnClick(UIButton button)
    {
        if (DisallowActions()) return;

        StarDriveGame.Instance.Exit();
    }
}