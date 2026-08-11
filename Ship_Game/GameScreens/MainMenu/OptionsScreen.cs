using System;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using NAudio.CoreAudioApi;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens.MainMenu;
using SynapseGaming.LightingSystem.Core;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class GraphicsSettings
    {
        public WindowMode Mode;
        public int Width, Height;
        public int AntiAlias;
        public int MaxAnisotropy;
        public int TextureSampling;
        public int TextureQuality;
        public int ShadowDetail; // 0=High, 1=Medium, 2=Low, 3=Off (DetailPreference enum)
        public int EffectDetail;
        public bool VSync;

        public static GraphicsSettings FromGlobalStats()
        {
            var settings = new GraphicsSettings();
            settings.LoadGlobalStats();
            return settings;
        }

        public GraphicsSettings GetClone() => (GraphicsSettings)MemberwiseClone();

        public void LoadGlobalStats()
        {
            Mode            = GlobalStats.WindowMode;
            Width           = GlobalStats.XRES;
            Height          = GlobalStats.YRES;
            AntiAlias       = GlobalStats.AntiAlias;
            MaxAnisotropy   = GlobalStats.MaxAnisotropy;
            TextureSampling = GlobalStats.TextureSampling;
            TextureQuality  = GlobalStats.TextureQuality;
            ShadowDetail    = GlobalStats.ShadowDetail;
            EffectDetail    = GlobalStats.EffectDetail;
            VSync           = GlobalStats.VSync;
        }

        void SetGlobalStats()
        {
            GlobalStats.WindowMode      = Mode;
            GlobalStats.XRES            = Width;
            GlobalStats.YRES            = Height;
            GlobalStats.AntiAlias       = AntiAlias;
            GlobalStats.MaxAnisotropy   = MaxAnisotropy;
            GlobalStats.TextureSampling = TextureSampling;
            GlobalStats.TextureQuality  = TextureQuality;
            GlobalStats.EffectDetail    = EffectDetail;
            GlobalStats.VSync           = VSync;
            GlobalStats.SetShadowDetail(ShadowDetail);
        }

        public void ApplyChanges()
        {
            // @note This MAY trigger StarDriveGame.UnloadContent() and LoadContent() !!!
            //       Only if graphics device reset fails and a new device must be created
            SetGlobalStats();
            bool deviceChanged = StarDriveGame.Instance.ApplyGraphics(this);
            
            // if device changed, then all game screens were already reloaded
            if (deviceChanged)
                return; // nothing to do here!

            // reload all screens, this is specific to StarDriveGame
            // NOTE: The game content should already be unloaded because of Device.Dispose()
            ScreenManager.Instance.LoadContent(deviceWasReset:true);
        }

        public bool Equals(GraphicsSettings other)
        {
            if (this == other) return true;
            return Mode            == other.Mode 
                && Width           == other.Width 
                && Height          == other.Height 
                && AntiAlias       == other.AntiAlias 
                && MaxAnisotropy   == other.MaxAnisotropy 
                && TextureSampling == other.TextureSampling 
                && TextureQuality  == other.TextureQuality 
                && ShadowDetail    == other.ShadowDetail
                && EffectDetail    == other.EffectDetail
                && VSync           == other.VSync;
        }
    }

    public sealed class OptionsScreen : PopupWindow
    {
        readonly bool Fade = true;
        readonly UniverseScreen Universe; // null when opened from the main menu
        DropOptions<DisplayMode> ResolutionDropDown;
        DropOptions<MMDevice> SoundDevices;
        DropOptions<Language> CurrentLanguage;

        GraphicsSettings Original; // default starting options and those we have applied with success
        GraphicsSettings New;

        FloatSlider MusicVolumeSlider;
        FloatSlider EffectsVolumeSlider;
        FloatSlider EffectsInfluenceNodeAlpha;
        AudioHandle EffectSound = new();

        FloatSlider IconSize;
        FloatSlider StationIconSize; // Ludoal fork (maintainer spec): stations scale separately
        FloatSlider AsteroidSize;
        FloatSlider MinimapSize;
        FloatSlider AutoSaveYears; // Ludoal fork: autosave counts in star-years now
        UICheckBox AutoPauseColonyBox; // Ludoal fork (bench 392): greyed when its parent option is off

        FloatSlider SimulationFps;
        FloatSlider MaxDynamicLightSources;

        public OptionsScreen(MainMenuScreen mainMenu) : base(mainMenu, 1100, 660)
        {
            IsPopup           = true;
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;
            TitleText         = Localizer.Token(GameText.Options);
            Original = GraphicsSettings.FromGlobalStats();
            New = Original.GetClone();
        }

        public OptionsScreen(UniverseScreen universe) : base(universe, 1100, 660)
        {
            Universe          = universe;
            Fade              = false;
            IsPopup           = true;
            TransitionOnTime  = 0f;
            TransitionOffTime = 0f;
            TitleText         = Localizer.Token(GameText.Options);
            Original = GraphicsSettings.FromGlobalStats();
            New = Original.GetClone();
        }

        string AntiAliasString()
        {
            if (New.AntiAlias == 0)
                return "No AA";
            return New.AntiAlias + "x MSAA";
        }

        string TextureFilterString()
        {
            if (New.MaxAnisotropy == 0)
                return new[]{"Bilinear", "Trilinear"}[New.TextureSampling];
            return "Anisotropic x" + New.MaxAnisotropy;
        }

        static string QualityString(int parameter)
        {
            return (uint)parameter <= 3 ? new[]{ "High", "Normal", "Low", "Ultra-Low" }[parameter] : "None";
        }

        static string ShadowQualStr(int parameter)
        {
            return ((DetailPreference)parameter).ToString();
        }

        void AntiAliasing_OnClick(UILabel label)
        {
            New.AntiAlias = New.AntiAlias == 0 ? 2 : New.AntiAlias * 2;
            if (New.AntiAlias > 8)
                New.AntiAlias = 0;
        }

        void TextureQuality_OnClick(UILabel label)
        {
            New.TextureQuality = New.TextureQuality == 3 ? 0 : New.TextureQuality + 1;
        }

        void TextureFiltering_OnClick(UILabel label)
        {
            New.TextureSampling += 1;
            if (New.TextureSampling >= 2)
            {
                New.MaxAnisotropy  += 1;
                New.TextureSampling = 2;
            }
            if (New.MaxAnisotropy > 4)
            {
                New.MaxAnisotropy   = 0;
                New.TextureSampling = 0;
            }
        }

        void ShadowQuality_OnClick(UILabel label)
        {
            // 0=High, 1=Medium, 2=Low, 3=Off
            New.ShadowDetail = New.ShadowDetail >= 3 ? 0 : New.ShadowDetail + 1;
        }

        void Fullscreen_OnClick(UILabel label)
        {
            ++New.Mode;
            if (New.Mode > WindowMode.Borderless)
                New.Mode = WindowMode.Fullscreen;
        }

        void EffectsQuality_OnClick(UILabel label)
        {
            New.EffectDetail = New.EffectDetail == 3 ? 0 : New.EffectDetail + 1;
        }

        void Add(UIList graphics, LocalizedText title, Func<UILabel, string> getText, Action<UILabel> onClick, float splitOffset = 0)
        {
            graphics.AddSplit(new UILabel($"{title.Text}:"), new UILabel(getText, onClick))
                .Split = graphics.Width*0.4f + splitOffset;
        }

        void Add(UIList graphics, LocalizedText title, UIElementV2 second, float splitOffset = 0)
        {
            graphics.AddSplit(new UILabel($"{title.Text}:"), second)
                .Split = graphics.Width*0.4f + splitOffset;
        }

        // Ludoal fork (maintainer spec): five themed boxes in the Automation grammar, all
        // visible at once - an options panel is a place you SEARCH, not a place you live in.
        // Graphics keeps the Apply button (device settings need explicit confirmation);
        // everything else applies the moment it is changed.
        UIList NewBox(in RectF r, LocalizedText title)
        {
            var box = Add(new Submenu(r, new[] { title }));
            box.PerformLayout();
            UIList list = AddList(new Vector2(box.ClientArea.X + 12, box.ClientArea.Y + 10),
                                  new Vector2(r.W - 36, r.H - 40));
            list.Padding = new Vector2(2f, 8f);
            // NOTE: ReverseZOrder is a one-shot gesture on existing rows, so lists that hold a
            // dropdown call it themselves AFTER their rows are added - here it would be a no-op
            return list;
        }

        void InitScreen()
        {
            Rectangle inner = PopupFrame.ContentArea(Rect);
            const float BoxW = 340f, BoxGap = 10f;
            const float GraphicsBoxH = 260, AudioBoxH = 160, VisualsBoxH = 260, GameplayBoxH = 210, UIBoxH = 310;
            float x0 = inner.X + 16, x1 = x0 + BoxW + BoxGap, x2 = x1 + BoxW + BoxGap;
            float top = inner.Y + 10;

            // ⚠ within a column the LOWER box is added FIRST: an open dropdown's list spills
            // below its own row, and add order is draw order - the spill must land on top
            // of the neighbour, not under it.

            // ---- column 1: Graphics (Apply-gated device settings) over Audio
            UIList audio = NewBox(new RectF(x0, top + GraphicsBoxH + BoxGap, BoxW, AudioBoxH), "Audio");
            SoundDevices = new DropOptions<MMDevice>(190, 18);
            audio.AddSplit(new UILabel(GameText.SoundDevice), SoundDevices).Split = 90;
            MusicVolumeSlider   = audio.Add(new FloatSlider(SliderStyle.Percent, 288f, 36f, GameText.MusicVolume, 0f, 1f, GlobalStats.MusicVolume));
            EffectsVolumeSlider = audio.Add(new FloatSlider(SliderStyle.Percent, 288f, 36f, GameText.EffectsVolume, 0f, 1f, GlobalStats.EffectsVolume));
            audio.ReverseZOrder(); // the device dropdown draws over the sliders below it

            UIList graphics = NewBox(new RectF(x0, top, BoxW, GraphicsBoxH), "Graphics");
            graphics.Padding = new Vector2(2f, 6f);
            ResolutionDropDown = new DropOptions<DisplayMode>(126, 18);

            // graphics rows get +30px between the setting name and its option
            Add(graphics, GameText.Resolution, ResolutionDropDown, 30);
            Add(graphics, GameText.ScreenMode,   l => New.Mode.ToString(),               Fullscreen_OnClick, 30);
            Add(graphics, GameText.AntiAliasing, l => AntiAliasString(),                 AntiAliasing_OnClick, 30);
            Add(graphics, GameText.TextureQuality, l => QualityString(New.TextureQuality), TextureQuality_OnClick, 30);
            Add(graphics, GameText.TextureFiltering, l => TextureFilterString(),             TextureFiltering_OnClick, 30);
            Add(graphics, GameText.ShadowQuality, l => ShadowQualStr(New.ShadowDetail),   ShadowQuality_OnClick, 30);
            Add(graphics, GameText.EffectsQuality, l => QualityString(New.EffectDetail),   EffectsQuality_OnClick, 30);
            graphics.ReverseZOrder(); // @todo This is a hacky workaround to zorder limitations
            graphics.ZOrder = 10;

            // Apply lives INSIDE the Graphics box: it is the device settings' button, no one else's
            var apply = Add(new UIButton(ButtonStyle.Default, new Vector2(x0 + 12, top + GraphicsBoxH - 44), GameText.ApplySettings));
            apply.OnClick = button => RunOnNextFrame(ApplyOptions);
            // Ludoal fork: say what this button is actually for. It applies the DISPLAY settings
            // and nothing else - everything else on this screen takes effect the moment you
            // change it and is saved when you leave. A button called "Apply Settings" sitting
            // among settings that apply themselves is a fair way to be misread (maintainer feedback).
            apply.Tooltip = "For the display settings only: resolution and screen mode.\n"
                          + "You get 10 seconds to confirm, or they revert.\n\n"
                          + "Everything else applies as you change it,\n"
                          + "and is saved when you leave this screen.";

            // ---- column 2: Visuals over Gameplay
            UIList gameplay = NewBox(new RectF(x1, top + VisualsBoxH + BoxGap, BoxW, GameplayBoxH), "Gameplay");
            SimulationFps = gameplay.Add(new FloatSlider(SliderStyle.Decimal, 288f, 36f, GameText.SimulationFps, 10, 120, GlobalStats.SimulationFramesPerSecond));
            AutoSaveYears = gameplay.Add(new FloatSlider(SliderStyle.Decimal, 288f, 36f, "Autosave every X years", 1, 20, GlobalStats.AutoSaveYears));
            gameplay.AddCheckbox(() => GlobalStats.PauseOnNotification,          title: GameText.PauseOnNotifications, tooltip: GameText.PausesGameOnNotificationsClearing);
            gameplay.AddCheckbox(() => GlobalStats.NotifyEnemyInSystemAfterLoad, title: GameText.AlertEnemyPresenceAfterLoad, tooltip: GameText.AddNotificationsRegardingEnemiesIn);
            gameplay.AddCheckbox(() => GlobalStats.RouteAroundGravityWells,      title: GameText.Pathfinder, tooltip: GameText.PathfinderTip);
            gameplay.AddCheckbox(() => GlobalStats.AutoErrorReport,              title: GameText.AutomaticErrorReport, tooltip: GameText.SendAutomaticErrorReportsTo);

            UIList visuals = NewBox(new RectF(x1, top, BoxW, VisualsBoxH), "Visuals");
            // Bloom applies instantly now (lazy component allocation) - it left the Apply pack
            visuals.AddCheckbox(() => GlobalStats.RenderBloom,        title: GameText.Bloom, tooltip: GameText.DisablingBloomEffectWillIncrease);
            visuals.AddCheckbox(() => GlobalStats.EnableEngineTrails, title: GameText.EngineTrails, tooltip: GameText.TT_EngineTrails);
            visuals.AddCheckbox(() => GlobalStats.DisableAsteroids,   title: GameText.DisableAsteroids, tooltip: GameText.ThisWillPreventAsteroidsFrom);
            // Ludoal fork: bring back the explored-system fog discs for those who miss them
            visuals.AddCheckbox(() => GlobalStats.FogOfWarMemory, title: "Fog Of War Memory",
                                tooltip: "Ships permanently paint their sensor coverage on the fog of war as they travel - the classic map memory. Off: the map stays dark and only live sensor coverage lights it.");
            AsteroidSize = visuals.Add(new FloatSlider(SliderStyle.Percent, 288f, 36f, "Asteroid Size", 0.25f, 1f, GlobalStats.AsteroidSizeMult));
            EffectsInfluenceNodeAlpha = visuals.Add(new FloatSlider(SliderStyle.Percent, 288f, 36f, GameText.GameOptionsInfluenceAlpha, 0f, 1f, GlobalStats.InfluenceNodeAlpha));
            EffectsInfluenceNodeAlpha.Tip = GameText.GameOptionsInfluenceAlphaTip;
            MaxDynamicLightSources = visuals.Add(new FloatSlider(SliderStyle.Decimal, 288f, 36f, GameText.MaxDynamicLightSources, 0, 1000, GlobalStats.MaxDynamicLightSources));

            // ---- column 3: UI, with Reset below it
            UIList ui = NewBox(new RectF(x2, top, BoxW, UIBoxH), "UI");
            IconSize        = ui.Add(new FloatSlider(SliderStyle.Decimal, 288f, 36f, "Ship Icon Sizes", 1, 30, GlobalStats.IconSize));
            StationIconSize = ui.Add(new FloatSlider(SliderStyle.Decimal, 288f, 36f, "Station Icon Sizes", 1, 30, GlobalStats.StationIconSize));
            MinimapSize     = ui.Add(new FloatSlider(SliderStyle.Percent, 288f, 36f, "Minimap Size", 1f, 2f, GlobalStats.MinimapSizeMult));
            ui.AddCheckbox(() => GlobalStats.ZoomTracking,         title: GameText.ToggleZoomTracking, tooltip: GameText.ZoomWillCenterOnSelected);
            ui.AddCheckbox(() => GlobalStats.DisableScreenPanning, title: GameText.DisableScreenPanningOption, tooltip: GameText.DisableScreenPanningOptionTip);
            ui.AddCheckbox(() => GlobalStats.AltArcControl,        title: GameText.KeyboardFireArcLocking, tooltip: GameText.WhenActiveArcsInThe);
            ui.AddCheckbox(() => GlobalStats.PauseOnPageOpen, title: "Auto-pause on page opening",
                           tooltip: "Opening a screen pauses the simulation. Untick to let the universe run behind your pages - manual pause still works. The Shipyard's Full Screen mode always pauses.");
            // bench 392 (maintainer): the Colony panel opts OUT of auto-pause by default (its
            // original behaviour). Subordinate to the option above - the setter refuses and the
            // label greys when auto-pause is off, and it is indented under it.
            AutoPauseColonyBox = ui.AddCheckbox(
                () => GlobalStats.AutoPauseColonyPanel,
                b => { if (GlobalStats.PauseOnPageOpen) GlobalStats.AutoPauseColonyPanel = b; },
                title: "Auto-pause Colony panel",
                tooltip: "When Auto-pause on page opening is on, also pause for the Colony panel. Off (default): the colony runs live while you read it.");
            AutoPauseColonyBox.Indent = 20; // indented under its parent option (bench 392)
            CurrentLanguage = new DropOptions<Language>(126, 18);
            ui.AddSplit(new UILabel(GameText.Language), CurrentLanguage).Split = 90;
            ui.ReverseZOrder();

            var reset = Add(new UIButton(ButtonStyle.Medium, new Vector2(x2 + 12, top + UIBoxH + BoxGap + 4), "Reset to Defaults"));
            reset.Tooltip = "Reset every option except the display settings (Graphics),\n"
                          + "the language and the sound device to its default value.";
            reset.OnClick = b => RunOnNextFrame(ResetToDefaults);

            MusicVolumeSlider.OnChange = (s) => GlobalStats.MusicVolume = s.AbsoluteValue;
            EffectsVolumeSlider.OnChange = (s) => SetEffectsVolume(s.AbsoluteValue);
            EffectsInfluenceNodeAlpha.OnChange = (s) => GlobalStats.InfluenceNodeAlpha = s.AbsoluteValue;
            MaxDynamicLightSources.OnChange = (s) => GlobalStats.MaxDynamicLightSources = (int)s.AbsoluteValue;
            IconSize.OnChange = (s) => GlobalStats.IconSize = (int)s.AbsoluteValue;
            StationIconSize.OnChange = (s) => GlobalStats.StationIconSize = (int)s.AbsoluteValue;
            AsteroidSize.OnChange = (s) => GlobalStats.AsteroidSizeMult = s.AbsoluteValue;
            MinimapSize.OnChange = (s) => { GlobalStats.MinimapSizeMult = s.AbsoluteValue; Universe?.SeatMinimap(); };
            AutoSaveYears.OnChange = (s) => GlobalStats.AutoSaveYears = (int)s.AbsoluteValue;
            SimulationFps.OnChange = (s) => GlobalStats.SimulationFramesPerSecond = (int)s.AbsoluteValue;

            MaxDynamicLightSources.Tip = GameText.TT_MaxDynamicLightSources;
            AutoSaveYears.Tip = "How many star-years between autosaves. A year is 10 turns; the clock does not advance while paused.";
            SimulationFps.Tip = GameText.ChangesTheSimulationFrequencyLower;
            AsteroidSize.Tip = "Visual size of asteroids. Takes effect immediately, cosmetic only.";
            MinimapSize.Tip = "Size of the minimap widget in the corner of the map.";
            IconSize.Tip = "Extra size in pixels for ship tactical icons.";
            StationIconSize.Tip = "Extra size in pixels for station and platform icons.";

            RefreshZOrder();
            PerformLayout();
            CreateResolutionDropOptions();
            CreateSoundDevicesDropOptions();
            CreateLanguageDropOptions();
        }

        // Ludoal fork (maintainer spec): back to stock for everything that applies instantly.
        // The Apply-gated display settings, the language and the sound device keep their values.
        void ResetToDefaults()
        {
            GlobalStats.MusicVolume   = 0.7f;
            GlobalStats.EffectsVolume = 1f;

            GlobalStats.RenderBloom            = true;
            GlobalStats.EnableEngineTrails     = true;
            GlobalStats.DisableAsteroids       = false;
            GlobalStats.FogOfWarMemory         = false;
            GlobalStats.AsteroidSizeMult       = 1f;
            GlobalStats.InfluenceNodeAlpha     = 1f;
            GlobalStats.MaxDynamicLightSources = 100;

            GlobalStats.IconSize             = 1;
            GlobalStats.StationIconSize      = 1;
            GlobalStats.MinimapSizeMult      = 1f;
            GlobalStats.ZoomTracking         = false;
            GlobalStats.DisableScreenPanning = false;
            GlobalStats.AltArcControl        = false;
            GlobalStats.PauseOnPageOpen      = true;
            GlobalStats.AutoPauseColonyPanel = false;

            GlobalStats.SimulationFramesPerSecond    = 60;
            GlobalStats.AutoSaveYears                = 5;
            GlobalStats.PauseOnNotification          = false;
            GlobalStats.NotifyEnemyInSystemAfterLoad = true;
            GlobalStats.RouteAroundGravityWells      = true;
            GlobalStats.AutoErrorReport              = true;

            GameAudio.ConfigureAudioSettings(GlobalStats.MusicVolume, GlobalStats.EffectsVolume);
            Universe?.SeatMinimap();
            GlobalStats.SaveSettings();
            LoadContent(); // rebuild the panel so every control re-reads its value
        }

        void CreateResolutionDropOptions()
        {
            int screenWidth  = ScreenWidth;
            int screenHeight = ScreenHeight;

            DisplayModeCollection displayModes = GraphicsAdapter.DefaultAdapter.SupportedDisplayModes;
            foreach (DisplayMode mode in displayModes)
            {
                if (mode.Width < 1280)
                    continue;
                if (ResolutionDropDown.Contains(existing => mode.Width == existing.Width && mode.Height == existing.Height))
                    continue;

                ResolutionDropDown.AddOption($"{mode.Width} x {mode.Height}", mode);

                if (mode.Width == screenWidth && mode.Height == screenHeight)
                    ResolutionDropDown.ActiveIndex = ResolutionDropDown.Count-1;
            }
        }

        void CreateSoundDevicesDropOptions()
        {
            MMDevice defaultDevice = GameAudio.Devices?.DefaultDevice;
            Array<MMDevice> devices = GameAudio.Devices?.Devices;

            SoundDevices.Clear();

            if (devices is {Count: > 0})
            {
                SoundDevices.AddOption("Default", null/*because it might change*/);
                foreach (MMDevice device in devices)
                {
                    string isDefault = (device.ID == defaultDevice?.ID) ? "* " : "";
                    SoundDevices.AddOption($"{isDefault}{device.FriendlyName}", device);
                    if (!GameAudio.Devices.UserPrefersDefaultDevice && device.ID == GameAudio.Devices.CurrentDevice.ID)
                        SoundDevices.ActiveIndex = devices.IndexOf(device) + 1;
                }
                SoundDevices.OnValueChange = OnAudioDeviceDropDownChange;
            }
            else
            {
                SoundDevices.AddOption("Not Available", null);
                SoundDevices.OnValueChange = null;
            }
        }

        void CreateLanguageDropOptions()
        {
            foreach (Language language in (Language[]) Enum.GetValues(typeof(Language)))
            {
                CurrentLanguage.AddOption(language.ToString(), language);
            }
            CurrentLanguage.ActiveValue = GlobalStats.Language;
            CurrentLanguage.OnValueChange = OnLanguageDropDownChange;
        }

        void OnAudioDeviceDropDownChange(MMDevice newDevice)
        {
            newDevice ??= GameAudio.Devices.DefaultDevice;

            GameAudio.Devices.SetUserPreference(newDevice);
            GameAudio.ReloadAfterDeviceChange(newDevice);

            GameAudio.SmallServo();
            GameAudio.TacticalPause();
        }

        void OnLanguageDropDownChange(Language newLanguage)
        {
            if (GlobalStats.Language != newLanguage)
            {
                GlobalStats.Language = newLanguage;
                ResourceManager.LoadLanguage(newLanguage);
                Fonts.LoadFonts(ResourceManager.RootContent, newLanguage);
                LoadContent(); // reload the options screen to update the text
            }
        }

        public override void LoadContent()
        {
            base.LoadContent();
            InitScreen();
        }

        void ApplyOptions()
        {
            try
            {
                GameAudio.ConfigureAudioSettings(GlobalStats.MusicVolume, GlobalStats.EffectsVolume);
                New.Width  = ResolutionDropDown.ActiveValue.Width;
                New.Height = ResolutionDropDown.ActiveValue.Height;
                New.ApplyChanges();

                if (Original.Equals(New))
                {
                    AcceptChanges(); // auto-accept
                }
                else
                {
                    ScreenManager.AddScreen(new MessageBoxScreen(this, Localizer.Token(GameText.KeepChangesRevertingIn), 10f)
                    {
                        Accepted = () => RunOnNextFrame(AcceptChanges),
                        Cancelled = () => RunOnNextFrame(CancelChanges)
                    });
                }
            }
            catch
            {
                RunOnNextFrame(CancelChanges);
            }
        }

        void AcceptChanges()
        {
            Original = New.GetClone(); // accepted!
            GlobalStats.SaveSettings();

            EffectsVolumeSlider.RelativeValue       = GlobalStats.EffectsVolume;
            MusicVolumeSlider.RelativeValue         = GlobalStats.MusicVolume;
            EffectsInfluenceNodeAlpha.RelativeValue = GlobalStats.InfluenceNodeAlpha;
        }

        void CancelChanges()
        {
            New = Original.GetClone(); // back to default!
            New.ApplyChanges();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (Fade) ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            // bench 392: the Colony sub-option greys out live when its parent option is off
            if (AutoPauseColonyBox != null)
                AutoPauseColonyBox.Greyed = !GlobalStats.PauseOnPageOpen;
            base.Draw(batch, elapsed);
        }

        public override void ExitScreen()
        {
            GlobalStats.SaveSettings();
            base.ExitScreen();
        }

        public override bool HandleInput(InputState input)
        {
            if (base.HandleInput(input))
            {
                GameAudio.ConfigureAudioSettings(GlobalStats.MusicVolume, GlobalStats.EffectsVolume);
                return true;
            }
            return false;
        }

        public void SetEffectsVolume(float volume)
        {
            if (GlobalStats.EffectsVolume != volume)
                EffectSound.PlaySfxAsync("sd_weapon_bigcannon_01", emitter: null, replayTimeout: 0.5f);

            GlobalStats.EffectsVolume = volume;
        }
    }
}
