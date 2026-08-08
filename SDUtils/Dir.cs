using System;
using System.IO;

namespace SDUtils;

public static class Dir
{
    static readonly FileInfo[]      NoFiles = new FileInfo[0];
    static readonly DirectoryInfo[] NoDirs  = new DirectoryInfo[0];

    // Added by RedFox - this is a safe wrapper to DirectoryInfo.GetFiles which assumes 
    //                   dir is optional and if it doesn't exist, returns empty file list
    public static FileInfo[] GetFiles(string dir, string pattern, SearchOption option)
    {
        try
        {
            var info = new DirectoryInfo(dir);
            return info.Exists ? info.GetFiles(pattern, option) : NoFiles;
        }
        catch { return NoFiles; }
    }
    public static FileInfo[] GetFiles(string dir)
    {
        return GetFiles(dir, "*.*", SearchOption.AllDirectories);
    }
    public static FileInfo[] GetFiles(string dir, string ext)
    {
        return GetFiles(dir, "*."+ext, SearchOption.AllDirectories);
    }
    public static FileInfo[] GetFilesNoSub(string dir)
    {
        return GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
    }
    public static FileInfo[] GetFilesNoSub(string dir, string ext)
    {
        return GetFiles(dir, "*."+ext, SearchOption.TopDirectoryOnly);
    }

    // Finds all subdirectories
    public static DirectoryInfo[] GetDirs(string dir, SearchOption option = SearchOption.AllDirectories)
    {
        try
        {
            var info = new DirectoryInfo(dir);
            return info.Exists ? info.GetDirectories("*", option) : NoDirs;
        }
        catch { return NoDirs; }
    }

    public static void CopyDir(string sourceDirName, string destDirName, bool copySubDirs)
    {
        var dir = new DirectoryInfo(sourceDirName);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");

        var dirs = dir.GetDirectories();

        if (!Directory.Exists(destDirName))
            Directory.CreateDirectory(destDirName);

        foreach (FileInfo file in dir.GetFiles())
            file.CopyTo(Path.Combine(destDirName, file.Name), true);

        if (!copySubDirs)
            return;

        foreach (DirectoryInfo subdir in dirs)
            CopyDir(subdir.FullName, Path.Combine(destDirName, subdir.Name), true);
    }

    static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        .NormalizedFilePath();

    // {AppData}/StarDrive
    // This is where all the saved games and cache files are stored
    //
    // Ludoal fork: a side-by-side install can move this with the STARDRIVE_APPDATA environment
    // variable, either as a full path or as a bare suffix ("Dev" -> {AppData}/StarDrive_Dev).
    // Without it, nothing changes.
    //
    // Why: this folder holds far more than saves — the user config (resolution, options), ship
    // designs, fleet designs, saved setups and the logs. The UI chantier build gets its own; the
    // QoL build stays compatible with stock BlackBox and keeps the default folder.
    public static readonly string StarDriveAppData = ResolveAppDataDir();

    // Ludoal fork: the PLAYER'S OWN WORK is shared across side-by-side installs, whatever
    // STARDRIVE_APPDATA says - saves, ship and fleet designs, blueprints, setups, races. Copying
    // those between installs by hand was the whole friction (maintainer feedback). What stays
    // per-install is the plumbing: StarDrive.user.config and the logs, which are about THIS build
    // and would otherwise fight over resolution and options.
    //
    // ⚠ The accepted price: a save written by a build with changed serialization may fail to load
    // in another. That is the trade - one folder of work rather than three copies of it.
    public static readonly string StarDriveUserData = AppData + "/StarDrive";

    static string ResolveAppDataDir()
    {
        string custom = Environment.GetEnvironmentVariable("STARDRIVE_APPDATA");
        if (custom == null || custom.Trim().Length == 0)
            return AppData + "/StarDrive";

        custom = custom.Trim();
        // a full path if it looks like one, otherwise a suffix on the usual folder
        bool isFullPath = custom.Contains(":") || custom.StartsWith("/") || custom.StartsWith("\\");
        return isFullPath ? custom.NormalizedFilePath()
                          : AppData + "/StarDrive_" + custom;
    }
}