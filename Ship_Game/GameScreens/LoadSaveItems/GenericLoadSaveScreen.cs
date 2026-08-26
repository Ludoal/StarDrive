using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork: a PopupWindow - this one class carries NINE screens (load/save game, race,
    // fleet design, setups, blueprints). They wear the frame Options wears.
    public abstract class GenericLoadSaveScreen : PopupWindow
    {
        protected Rectangle Window;
        // (SaveMenu is gone: the window frame is PopupWindow's now, not a Menu1 of our own)
        protected Submenu NameSave;
        protected SubmenuScrollList<SaveLoadListItem> AllSaves;
        protected Vector2 TitlePosition;
        protected UITextEntry EnterNameArea;
        protected ScrollList<SaveLoadListItem> SavesSL;
        protected UIButton DoBtn;
        protected UIButton ExportBtn;
        public enum SLMode { Load, Save }
        protected SLMode Mode;

        protected string InitText;
        protected string Title;
        protected string OverwriteText = "";
        protected string Path = "";
        protected string TabText;

        protected FileData SelectedFile;
        protected int EntryHeight = 55; // element height
        protected bool ShowSaveExport;

        protected GenericLoadSaveScreen(
            GameScreen parent, SLMode mode, string initText, string title, string tabText, bool showSaveExport = false)
            : base(parent, 680, 600) // wide enough for Export beside the Load/Save button
        {
            Mode = mode;
            InitText = initText;
            Title = title;
            TabText = tabText;
            ShowSaveExport = showSaveExport;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        protected GenericLoadSaveScreen(
            GameScreen parent, SLMode mode, string initText, string title, string tabText, string overwriteText, bool showSaveExport = false) 
            : this(parent, mode, initText, title, tabText, showSaveExport:showSaveExport)
        {
            OverwriteText = overwriteText;
        }

        protected GenericLoadSaveScreen(
            GameScreen parent, SLMode mode, string initText, string title, string tabText, int entryHeight) 
            : this(parent, mode, initText, title, tabText)
        {
            EntryHeight = entryHeight;
        }

        protected GenericLoadSaveScreen(
            GameScreen parent, SLMode mode, string initText, string title, string tabText, string overwriteText, int entryHeight) 
            : this(parent, mode, initText, title, tabText, overwriteText)
        {
            EntryHeight = entryHeight;
        }

        public virtual void DoSave()
        {
        }

        protected virtual bool DeleteFile(FileData toDelete)
        {
            try
            {
                toDelete.FileLink.Delete(); // delete the file
            } 
            catch 
            {
                GameAudio.NegativeClick();
                return false;
            }

            GameAudio.EchoAffirmative();
            SavesSL.RemoveFirstIf(item => item.Data == toDelete);
            return true;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // ⚠ base.Draw goes FIRST now: it paints the window frame, which used to be SaveMenu's
            // job here. Leaving it last would lay the frame's body over the two lists.
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);

            batch.SafeBegin();
            NameSave.Draw(batch, elapsed);
            AllSaves.Draw(batch, elapsed);
            batch.SafeEnd();
        }

        protected virtual void Load()
        {
        }

        protected abstract void InitSaveList(); // To be implemented in subclasses

        public override void LoadContent()
        {
            // ⚠ base.LoadContent() lays the frame out and calls RemoveAll(): it goes FIRST, and
            // it supplies the frame and the close cross this method used to build itself.
            // the window names itself in its own title bar
            TitleText = Title;
            base.LoadContent();

            Window = Rect;
            // ⚠ inset 28, not 20: a Submenu's tab sticks out to the LEFT of its own rect, so at
            // 20 the tabs overhung the frame's border. Bounds come from ContentArea, which knows
            // what the frame's own edges eat (11 right, 30 at the foot).
            Rectangle inner = PopupFrame.ContentArea(Window);
            // standard 10px margins + the 12px of air over the name row
            RectF sub = new(inner.X + 10, inner.Y + 16, inner.Width - 20, 80);
            NameSave = new Submenu(sub, Title);
            TitlePosition = new Vector2(sub.X + 20, sub.Y + 45);

            RectF scrollList = new(sub.X, sub.Y + 90, sub.W, inner.Bottom - (sub.Y + 90));

            AllSaves = Add(new SubmenuScrollList<SaveLoadListItem>(scrollList, TabText, EntryHeight));
            SavesSL = AllSaves.List;
            SavesSL.OnClick = OnSaveLoadItemClicked;
            SavesSL.OnDoubleClick = OnSaveLoadItemDoubleClicked;
            SavesSL.EnableItemHighlight = true;
            InitSaveList();

            EnterNameArea = Add(new UITextEntry(TitlePosition, Fonts.Arial20Bold, InitText));
            EnterNameArea.Enabled = (Mode == SLMode.Save); // Only enable name field change when saving

            string title = Mode == SLMode.Save ? "Save" : "Load";
            DoBtn = ButtonSmall(sub.X + sub.W - 88, EnterNameArea.Y - 2, title, b =>
            {
                if (Mode == SLMode.Save)
                    TrySave();
                else if (Mode == SLMode.Load)
                    Load();
            });

            if (ShowSaveExport)
            {
                // on the SAME row, left of Load/Save, and just "Export" - the window grew the
                // 80px this button needs; blue - the fork's active-control plate
                var exportBtn = Add(new UIButton(ButtonStyle.WideActive, new Vector2(sub.X + sub.W - 176, EnterNameArea.Y - 2), "Export"));
                exportBtn.OnClick = b => ExportSave();
                exportBtn.SetAbsSize(80, 24);
                exportBtn.Tooltip = GameText.ThisWillLetYouEasily;
            }
            // (no base.LoadContent() here: it ran at the top, and calling it again would
            // RemoveAll() everything this method just built)
        }

        protected virtual void OnSaveLoadItemClicked(SaveLoadListItem item)
        {
            SwitchFile(item.Data);
        }
        
        protected virtual void OnSaveLoadItemDoubleClicked(SaveLoadListItem item)
        {
            SwitchFile(item.Data);
            if (Mode == SLMode.Save)
                TrySave();
            else if (Mode == SLMode.Load)
                Load();
        }


        protected void SwitchFile(FileData file)
        {
            SelectedFile = file;
            GameAudio.AcceptClick();
            EnterNameArea.Text = file.FileName;
        }

        void OverWriteAccepted()
        {
            DoSave();
        }

        bool IsSaveOk()
        {
            foreach (SaveLoadListItem item in SavesSL.AllEntries)
                if (EnterNameArea.Text == item.Data.FileName) // check if item already exists
                    return false;
            return true;
        }

        void TrySave()
        {
            if (EnterNameArea.Text.IsEmpty())
            {
                GameAudio.NegativeClick();
                ScreenManager.AddScreen(new MessageBoxScreen(this, Localizer.Token(GameText.MmEnterFileName), MessageBoxButtons.Ok));
            }
            else if (IsSaveOk())
            {
                DoSave();
            }
            else
            {
                ScreenManager.AddScreen(new MessageBoxScreen(this, OverwriteText)
                {
                    Accepted = OverWriteAccepted
                });
            }
        }

        protected void AddItemsToSaveSL(IEnumerable<FileData> files, bool addCancel = true)
        {
            foreach (FileData data in files)
                SavesSL.AddItem(new SaveLoadListItem(this, data, addCancel));
        }

        protected void ExportSave()
        {
            if (SelectedFile == null)
            {
                GameAudio.NegativeClick();
                return;
            }

            string savedFileName = ExportSave(SelectedFile);

            string message = string.Format(Localizer.Token(GameText.MmSaveExportedToDesktop), savedFileName);
            int messageWidth = ((int)Fonts.Arial12Bold.MeasureString(savedFileName).X + 20).UpperBound(400);
            ScreenManager.AddScreen(new MessageBoxScreen(this, message, MessageBoxButtons.Ok, messageWidth));
        }
        
        string ExportSave(FileData save)
        {
            Log.FlushAllLogs();

            string fileName = save.FileName;
            var dirInfo = new DirectoryInfo(Path + "/" + fileName);
            dirInfo.Create();
            string tmpDir = dirInfo.FullName;

            save.FileLink.CopyTo($"{tmpDir}/{save.FileName}{save.FileLink.Extension}", overwrite:true);

            // also add both logfiles
            if (File.Exists(Log.LogFilePath))
                File.Copy(Log.LogFilePath, $"{tmpDir}/blackbox.log", overwrite:true);
            if (File.Exists(Log.OldLogFilePath))
                File.Copy(Log.OldLogFilePath, $"{tmpDir}/blackbox.old.log", overwrite:true);

            // include the user's colony blueprints for the current mod/BBplus context
            string modScope = BlueprintsTemplate.CurrentModName;
            string blueprintsSrc = Dir.StarDriveUserData + "/Colony Blueprints/" + modScope;
            if (Directory.Exists(blueprintsSrc))
            {
                string blueprintsDest = $"{tmpDir}/blueprints/{modScope}";
                Directory.CreateDirectory(blueprintsDest);
                foreach (FileInfo bp in Dir.GetFiles(blueprintsSrc, "yaml"))
                    bp.CopyTo($"{blueprintsDest}/{bp.Name}", overwrite:true);
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string outZip = $"{GetDebugVersionString()}_{fileName}.zip";
            HelperFunctions.CompressDir(dirInfo, $"{desktop}/{outZip}");
            dirInfo.Delete(true);

            return outZip;
        }

        static string GetDebugVersionString()
        {
            string blackBox = GlobalStats.ExtendedVersionNoHash.Replace(":", "").Replace(" ", "_").Replace("/", "_");
            string modTitle = "";
            if (GlobalStats.HasMod)
            {
                string title = GlobalStats.ModName;
                string version = GlobalStats.Defaults.Mod.Version;
                if (version.NotEmpty() && !title.Contains(version))
                    modTitle = title + "-" + version;

                modTitle = modTitle.Replace(":", "").Replace(" ", "_");
                return $"{blackBox}_{modTitle}";
            }
            return blackBox;
        }

        protected class SaveLoadListItem : ScrollListItem<SaveLoadListItem>
        {
            readonly GenericLoadSaveScreen Screen;
            public FileData Data;
            public SaveLoadListItem(GenericLoadSaveScreen screen, FileData data, bool addCencel)
            {
                Screen = screen;
                Data = data;
                if (addCencel)
                    AddCancel(new Vector2(-30, 0), GameText.MmDeleteSaveFile, OnDeleteClicked);
            }
            void OnDeleteClicked()
            {
                var toDelete = Data;
                Screen.ScreenManager.AddScreen(new MessageBoxScreen(Screen, "Confirm Delete:")
                {
                    Accepted = () => Screen.DeleteFile(toDelete)
                });
            }
            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);

                float iconHeight = (int)(Height * 0.89f);
                float iconWidth = (int)Data.Icon.GetWidthFromHeightAspect(iconHeight);
                batch.Draw(Data.Icon, Pos, new Vector2(iconWidth, iconHeight), Data.IconColor);

                var tCursor = new Vector2(X + 50f, Y);
                var mainColor = Data.Enabled ? Data.FileNameColor : Color.Gray;
                batch.DrawString(Fonts.Arial20Bold, Data.FileName, tCursor, mainColor);

                tCursor.Y += Fonts.Arial20Bold.LineSpacing;
                batch.DrawString(Fonts.Arial12Bold, Data.Info, tCursor, Data.InfoColor);

                tCursor.Y += Fonts.Arial12Bold.LineSpacing;
                batch.DrawString(Fonts.Arial12Bold, Data.ExtraInfo, tCursor, Data.InfoColor);

                if (Hovered && Data.Tooltip.NotEmpty())
                    ToolTip.CreateTooltip(Data.Tooltip, "", null, maxWidth:400);
            }
        }

        protected class FileData
        {
            public string FileName;
            public string Info;
            public string ExtraInfo;
            public string Tooltip;
            public SubTexture Icon;
            public Color IconColor;
            public FileInfo FileLink;
            public object Data;
            public bool Enabled = true; // new feature: show incompatible entries as grayed out and unselectable
            public Color InfoColor = Color.White;
            public Color FileNameColor = Color.Orange;

            public FileData(FileInfo fileLink, object data,
                string fileName, string info, string extraInfo, string tooltip, SubTexture icon, Color iconColor)
            {
                FileName = fileName;
                Info = info;
                ExtraInfo = extraInfo;
                Tooltip = tooltip;
                FileLink = fileLink;
                Data = data;
                Icon = icon ?? ResourceManager.Texture("ShipIcons/Wisp");
                IconColor = iconColor;
            }

            public static FileData FromSaveHeader(FileInfo file, HeaderData header)
            {
                string info = $"{header.PlayerName} StarDate {header.StarDate}";
                string extraInfo = header.RealDate;
                string tooltip = file.Name;

                // headers that carry the player's flag draw it directly; older headers read
                // -1 and fall back to the race-name lookup below, which shows the default
                // flag for custom and renamed races. Flag() is null if the index is not in
                // the loaded atlas (a modded save listed in the unfiltered Save dialog), and
                // the lookup is a better guess than the ctor's generic icon fallback
                SubTexture flag = header.FlagIndex >= 0 ? ResourceManager.Flag(header.FlagIndex) : null;
                if (flag != null)
                    return new(file, header, header.SaveName, info, extraInfo, tooltip,
                               flag, header.EmpireColor);

                IEmpireData empire = ResourceManager.AllRaces.FirstOrDefault(e => e.Name == header.PlayerName)
                                  ?? ResourceManager.AllRaces[0];
                // only the icon was missing, so keep the header's own colour when it has one
                Color tint = header.FlagIndex >= 0 ? header.EmpireColor : empire.Traits.Color;
                return new(file, header, header.SaveName, info, extraInfo, tooltip,
                           empire.Traits.FlagIcon, tint);
            }
        }
    }
}