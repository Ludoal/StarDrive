using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Data.Yaml;
using Ship_Game.Data.YamlSerializer;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game;

public class SaveLoadBlueprintsScreen : GenericLoadSaveScreen
{
    readonly BlueprintsScreen BlueprintsScreen;
    public static SubTexture BlueprintsIcon = ResourceManager.Texture("NewUI/blueprints");
    readonly BlueprintsTemplate BlueprintsToSave;
    const int ListItemHeight = 60;

    public SaveLoadBlueprintsScreen(BlueprintsScreen parent, BlueprintsTemplate blueprintsToSave) 
        : base(parent, SLMode.Save, blueprintsToSave.Name, Localizer.Token(GameText.BpSaveBlueprintsAs), "Colony Blueprints", Localizer.Token(GameText.BpSavedBlueprintsOverwrite), ListItemHeight)
    {
        BlueprintsScreen = parent;
        BlueprintsToSave = blueprintsToSave;
        InitPath();
    }

    public SaveLoadBlueprintsScreen(BlueprintsScreen parent) : base(parent, SLMode.Load, "", Localizer.Token(GameText.BpLoadBlueprints), "Saved Blueprints", ListItemHeight)
    {
        BlueprintsScreen = parent;
        InitPath();
    }

    // Load blueprints to a colony from the colonyscreen.
    public SaveLoadBlueprintsScreen(GameScreen parent, string planetName)
        : base(parent, SLMode.Load, "", string.Format(Localizer.Token(GameText.BpLoadBlueprintsTo), planetName), "Saved Blueprints", ListItemHeight)
    {
        InitPath();
    }

    // Link blueprints to other Blueprints.
    public SaveLoadBlueprintsScreen(string blueprintsName, BlueprintsScreen parent)
        : base(parent, SLMode.Load, "", string.Format(Localizer.Token(GameText.BpLinkBlueprintsTo), blueprintsName), "Saved Blueprints", ListItemHeight)
    {
        BlueprintsScreen = parent;
        InitPath();
    }

    // "Save" would promise the wrong thing next to a Rename: this one keeps the old plan.
    protected override string SaveButtonTitle => Mode == SLMode.Save ? "Save as copy" : "Load";

    protected override void AddExtraButtons(float x, float y)
    {
        if (Mode != SLMode.Save || BlueprintsScreen == null)
            return;

        var rename = Add(new UIButton(ButtonStyle.WideActive, new Vector2(x, y), Localizer.Token(GameText.BpRename)));
        rename.OnClick = b => TryRename();
        rename.SetAbsSize(80, 24);
        rename.Tooltip = GameText.BpRenameTip;
    }

    // A rename must not offer to overwrite: the plan under that name would go, and every colony
    // carrying it would land on this one without being asked. A taken name is a refusal - and a
    // name another plan used to carry is taken too, or the reattachment would aim at the wrong plan.
    void TryRename()
    {
        string oldName = BlueprintsToSave.Name;
        string newName = EnterNameArea.Text;
        if (!RenameRefused(oldName, newName, out string why))
        {
            int colonies = BlueprintsScreen.Player.CountPlanetsWithBlueprints(oldName);
            int governors = BlueprintsScreen.Player.CountBlueprintPolicyRows(oldName);
            int chains = CountChainsTo(oldName);
            string text = string.Format(Localizer.Token(GameText.BpRenameConfirm),
                                        oldName, newName, colonies, governors, chains);
            ScreenManager.AddScreen(new MessageBoxScreen(this, text) { Accepted = () => DoRename(oldName, newName) });
            return;
        }

        GameAudio.NegativeClick();
        ScreenManager.AddScreen(new MessageBoxScreen(this, why, MessageBoxButtons.Ok));
    }

    bool RenameRefused(string oldName, string newName, out string why)
    {
        why = "";
        if (newName.IsEmpty())
            why = Localizer.Token(GameText.MmEnterFileName);
        else if (newName == oldName)
            why = Localizer.Token(GameText.BpRenameSameName);
        else if (newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            why = Localizer.Token(GameText.BpRenameBadChars);
        else if (NotDrawable(newName))
            why = Localizer.Token(GameText.BpRenameBadChars);
        else if (NameIsTaken(oldName, newName))
            why = string.Format(Localizer.Token(GameText.BpRenameTaken), newName);
        return why.NotEmpty();
    }

    // the game's fonts stop at Latin-1: anything past it draws as a question mark
    static bool NotDrawable(string name)
    {
        foreach (char c in name)
            if (c > 0xFF)
                return true;
        return false;
    }

    static bool NameIsTaken(string oldName, string newName)
    {
        foreach (BlueprintsTemplate t in ResourceManager.GetAllBlueprints())
            if (t.Name != oldName && t.KnownAs(newName))
                return true;
        return false;
    }

    int CountChainsTo(string name)
    {
        int n = 0;
        foreach (BlueprintsTemplate t in ResourceManager.GetAllBlueprints())
            if (t.LinkTo == name)
                ++n;
        return n;
    }

    void DoRename(string oldName, string newName)
    {
        try
        {
            BlueprintsToSave.RecordFormerName(oldName);
            BlueprintsToSave.Name = newName;
            YamlSerializer.SerializeOne(Path + newName + ".yaml", BlueprintsToSave);

            string oldFile = Path + oldName + ".yaml";
            if (File.Exists(oldFile))
                File.Delete(oldFile);

            ResourceManager.BlueprintsTemplatesDict.Remove(oldName);
            ResourceManager.AddBlueprintsTemplate(BlueprintsToSave);

            // the chains name their target, so they are rewritten on disk like a deletion does -
            // clearing there, retargeting here
            string modName = BlueprintsTemplate.CurrentModName;
            foreach (FileInfo info in Dir.GetFiles(Path, "yaml"))
            {
                var other = YamlParser.DeserializeOne<BlueprintsTemplate>(info);
                if (modName == other.ModName && other.LinkTo == oldName && other.Name != newName)
                {
                    other.LinkTo = newName;
                    YamlSerializer.SerializeOne(Path + other.Name + ".yaml", other);
                    ResourceManager.AddBlueprintsTemplate(other);
                }
            }

            BlueprintsScreen.AfterBlueprintsRename(oldName, BlueprintsToSave);
        }
        catch (Exception e)
        {
            Log.Error(e, "Rename Blueprints Failed");
        }
        finally
        {
            ExitScreen();
        }
    }

    void InitPath()
    {
        Path = Dir.StarDriveUserData + "/Colony Blueprints/" + BlueprintsTemplate.CurrentModName + "/";
        if (!Directory.Exists(Path))
            Directory.CreateDirectory(Path);
    }

    public override void DoSave()
    {
        if (BlueprintsScreen == null)
        {
            Log.Error("Cannot save Blueprints if BlueprintsScreen is null");
            ExitScreen();
            return;
        }

        try
        {
            string name = EnterNameArea.Text;
            string path = Path + name + ".yaml";
            BlueprintsToSave.Name = name;
            if (BlueprintsToSave.LinkTo == name)
                BlueprintsToSave.LinkTo = ""; // avoid cyclic link for new blueprints

            YamlSerializer.SerializeOne(path, BlueprintsToSave);
            ResourceManager.AddBlueprintsTemplate(BlueprintsToSave);
            BlueprintsScreen.AfterBluprintsSave(BlueprintsToSave);
        }
        catch (Exception e)
        {
            Log.Error(e, "Save Blueprints Failed");
        }
        finally
        {
            ExitScreen();
        }
    }

    protected override void Load()
    {
        if (SelectedFile?.FileLink != null && SelectedFile.Enabled)
        {
            var blueprints = YamlParser.DeserializeOne<BlueprintsTemplate>(SelectedFile?.FileLink);
            BlueprintsScreen.LoadBlueprintsTemplate(blueprints);
            ExitScreen();
        }
        else
        {
            GameAudio.NegativeClick();
        }
    }

    protected override bool DeleteFile(FileData toDelete)
    {

        if (!base.DeleteFile(toDelete))
            return false;

        var deleteBluprints = (BlueprintsTemplate)toDelete.Data;
        string deletedName = deleteBluprints.Name;
        BlueprintsScreen.AfterBluprintsDelete(deleteBluprints);

        string modName = BlueprintsTemplate.CurrentModName;
        foreach (FileInfo info in Dir.GetFiles(Path, "yaml"))
        {
            var blueprints = YamlParser.DeserializeOne<BlueprintsTemplate>(info);
            if (modName == blueprints.ModName && blueprints.LinkTo == deletedName)
            {
                blueprints.LinkTo = "";
                string path = Path + blueprints.Name + ".yaml";
                YamlSerializer.SerializeOne(path, blueprints);
                ResourceManager.AddBlueprintsTemplate(blueprints);
                BlueprintsScreen.RemoveAllBlueprintsLinkTo(blueprints);
            }
        }

        SavesSL.Reset();
        InitSaveList();
        return true;
    }

    FileData CreateBlueprintsSaveItem(FileInfo info, BlueprintsTemplate blueprints)
    {
        string title1;
        Color infoColor = Color.White;
        if (blueprints.Validated)
        {
            title1 = blueprints.Exclusive ? Localizer.Token(GameText.ExclusiveBlueprints) : "";
            if (blueprints.ColonyType != Planet.ColonyType.Colony)
                title1 = title1.NotEmpty() ? $"{title1} | Switch to: {blueprints.ColonyType}"
                                           : $"Switch to: {blueprints.ColonyType}";
        }
        else
        {
            title1 = Localizer.Token(GameText.BpMissingBuildings);
            infoColor = Color.Red;
        }

        string title2 = blueprints.Validated && blueprints.LinkTo.NotEmpty() ? $"Linked to: {blueprints.LinkTo}" : "";
        Color color = BlueprintsScreen.GetBlueprintsIconColor(blueprints.ColonyType);
        return new(info, blueprints, blueprints.Name, title1, title2, "", BlueprintsIcon, color)
        { Enabled = blueprints.Validated, InfoColor = infoColor, FileNameColor = color };
    }

    protected override void InitSaveList()
    {
        Array<FileData> items = new();
        string modName = BlueprintsTemplate.CurrentModName;
        foreach (FileInfo info in Dir.GetFiles(Path, "yaml"))
        {
            var blueprints = YamlParser.DeserializeOne<BlueprintsTemplate>(info);
            if (modName == blueprints.ModName)
                items.Add(CreateBlueprintsSaveItem(info, blueprints));
        }

        AddItemsToSaveSL(items);
    }
}
