using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Data.Yaml;
using Ship_Game.Utils;

namespace Ship_Game
{
    public sealed class ModManager : PopupWindow
    {
        readonly MainMenuScreen MainMenu;
        SubmenuScrollList<ModsListItem> AllSaves;
        Vector2 TitlePosition;
        UITextEntry EnterNameArea;
        UIButton UnloadMod;
        UIButton CurrentButton;

        ScrollList<ModsListItem> ModsList;
        ModEntry SelectedMod;

        public ModManager(MainMenuScreen mainMenu) : base(mainMenu, 850, 600)
        {
            MainMenu = mainMenu;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        class ModsListItem : ScrollListItem<ModsListItem>
        {
            public readonly ModEntry Mod;
            public ModsListItem(ModEntry mod)
            {
                Mod = mod;
            }
            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                Mod.DrawListElement(batch, Rect);
            }
        }

        public override void LoadContent()
        {
            // the window names itself in its own title bar; frame and close cross are
            // PopupWindow's - base.LoadContent goes FIRST and lays them out
            // Ludoal fork (maintainer feedback): the three headers used to share one token and
            // all read "Load Modification"; the window is MODS, the loader tab LOAD MOD, the
            // list MOD - three distinct labels, so they carry literals now.
            TitleText = "MODS";
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            // Ludoal fork (maintainer feedback): a little air above the LOAD MOD tab
            RectF sub = new(inner.X + 25, inner.Y + 18, inner.Width - 50, 80);
            Add(new Submenu(sub, "LOAD MOD"));

            RectF scrollList = new(sub.X, sub.Y + 90, sub.W, inner.Bottom - (sub.Y + 90));
            LoadMods(scrollList);

            TitlePosition = new Vector2(sub.X + 20, sub.Y + 45);
            EnterNameArea = Add(new UITextEntry(TitlePosition, Fonts.Arial12Bold, ""));
            EnterNameArea.SetColors(Color.Orange, Color.White);

            ButtonSmall(sub.X + sub.W - 88, EnterNameArea.Y - 2, text:GameText.Load, click: OnLoadClicked);
            // Ludoal fork (maintainer feedback): the "Load Mods (Web)" button did nothing, so it is
            // gone. Unload sits just left of Load in the LOAD MOD tab and reads as the one hostile
            // action here, so it takes the red (hostile) plate over the Small button's size.
            UnloadMod = ButtonSmall(sub.X + sub.W - 88 - 68 - 8, EnterNameArea.Y - 2, "Unload", click:OnUnloadModClicked);
            UnloadMod.DefaultColor = UIButton.PlateHostile;
            UnloadMod.HoverColor   = UITheme.Hover(UIButton.PlateHostile);
            UnloadMod.PressColor   = UITheme.Press(UIButton.PlateHostile);
            UnloadMod.Enabled = GlobalStats.HasMod;
        }

        void LoadMods(RectF scrollList)
        {
            AllSaves = Add(new SubmenuScrollList<ModsListItem>(scrollList, "MOD", 140));
            ModsList = AllSaves.List;
            ModsList.EnableItemHighlight = true;
            ModsList.OnClick = OnModItemClicked;

            Array<ModsListItem> mods = new();

            foreach (DirectoryInfo info in Dir.GetDirs("Mods", SearchOption.TopDirectoryOnly))
            {
                string modFile = $"Mods/{info.Name}/Globals.yaml";
                try
                {
                    var file = new FileInfo(modFile);
                    GamePlayGlobals modSettings = GamePlayGlobals.Deserialize(file);
                    var e = new ModEntry(modSettings);
                    e.LoadPortrait(MainMenu);
                    mods.Add(new(e));
                }
                catch (Exception ex)
                {
                    Log.Warning($"Load error in file {modFile}: {ex.Message}");
                    ex.Data.Add("Load Error in file", modFile);
                }
            }

            // sort mods so that same mods with higher version are always at top
            mods.Sort((a, b) => -string.Compare(a.Mod.Name+a.Mod.Mod.Version, b.Mod.Name+b.Mod.Mod.Version, StringComparison.OrdinalIgnoreCase));
            ModsList.SetItems(mods);
        }

        void OnModItemClicked(ModsListItem item)
        {
            SelectedMod = item.Mod;
            EnterNameArea.Text = SelectedMod.Mod.Name;
        }

        public override bool HandleInput(InputState input)
        {
            if (CurrentButton == null && (input.Escaped || input.RightMouseClick))
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (IsExiting)
                return;
            // base.Draw paints the window frame and every child inside its own batch
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }

        void OnLoadClicked(UIButton b)
        {
            if (SelectedMod == null || !SelectedMod.IsSupported)
            {
                GameAudio.NegativeClick();
                return;
            }
            CurrentButton = b;
            b.Text = "Loading";
            LoadModTask();
        }

        void OnUnloadModClicked(UIButton b)
        {
            ClearMods();
        }

        void ClearMods()
        {
            if (!GlobalStats.HasMod)
                return;

            Log.Info("ModManager.ClearMods");
            GlobalStats.SetActiveModNoSave(null);
            // reload the whole game
            ScreenManager.GoToScreen(new GameLoadingScreen(showSplash: false, resetResources: true), clear3DObjects: true);
        }

        void LoadModTask()
        {
            Log.Info($"ModManager.LoadMod {SelectedMod.Mod.Path}");
            GlobalStats.SetActiveModNoSave(SelectedMod);
            // reload the whole game
            ScreenManager.GoToScreen(new GameLoadingScreen(showSplash: false, resetResources: true), clear3DObjects: true);
        }
    }
}
