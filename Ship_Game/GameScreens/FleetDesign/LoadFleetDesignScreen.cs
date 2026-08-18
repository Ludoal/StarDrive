using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Data.Yaml;

namespace Ship_Game;

public sealed class LoadFleetDesignScreen : GenericLoadSaveScreen
{
    readonly FleetDesignScreen Screen;

    public LoadFleetDesignScreen(FleetDesignScreen caller) : base(caller, SLMode.Load, "", Localizer.Token(GameText.FdLoadSavedFleet), "Saved Fleets", 40)
    {
        Screen = caller;
        Path = Dir.StarDriveUserData + "/Fleet Designs/";
    }

    protected override void Load()
    {
        if (SelectedFile?.FileLink != null && SelectedFile.Enabled)
        {
            Screen.LoadFleetDesign(SelectedFile.FileLink);
        }
        else
        {
            GameAudio.NegativeClick();
        }
        ExitScreen();
    }

    bool PlayerCanBuildFleet(FleetDesign fleetDesign)
    {
        foreach (FleetDataDesignNode node in fleetDesign.Nodes)
            if (!Screen.Player.WeCanBuildThis(node.ShipName))
                return false;
        return true;
    }

    FileData CreateFleetDesignSaveItem(FileInfo info, FleetDesign design)
    {
        design.CanBuildFleet = PlayerCanBuildFleet(design);
        FileData data = new(info, info, info.NameNoExt(), "", "", "", design.Icon, Color.White)
        {
            Enabled = design.CanBuildFleet
        };
        if (!design.CanBuildFleet)
            data.Info = Localizer.Token(GameText.FdCannotBuildFleet);
        return data;
    }

    void LoadFleetDesigns(FileInfo[] designFiles)
    {
        Array<FileData> items = new();
        foreach (FileInfo info in designFiles)
        {
            var design = YamlParser.Deserialize<FleetDesign>(info);
            items.Add(CreateFleetDesignSaveItem(info, design));
        }
        AddItemsToSaveSL(items);
    }

    protected override void InitSaveList()
    {
        FileInfo[] coreDesigns = ResourceManager.GatherFilesModOrVanilla("FleetDesigns", "yaml");
        FileInfo[] playerDesigns = Dir.GetFiles(Path, "yaml");
        LoadFleetDesigns(coreDesigns);
        LoadFleetDesigns(playerDesigns);
    }
}
