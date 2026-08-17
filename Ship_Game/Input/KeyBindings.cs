using System;
using System.IO;
using System.Reflection;
using SDGraphics.Input; // the game's own Keys enum - InputState.KeyPressed consumes THIS type
using SDUtils;

namespace Ship_Game
{
    // Ludoal fork (wishlist): the rebindable game hotkeys. Defaults mirror the historical
    // layout; a player override lives in {AppData}/StarDrive/Hotkeys.yaml as plain
    // "ActionName: KeyName" lines (XNA key names, e.g. "PlanetListScreen: F10").
    // A typo in either half is reported and skipped - it must never eat a binding silently.
    public static class KeyBindings
    {
        // map overlays
        public static Keys InfluenceOverlay    = Keys.F2;
        public static Keys VisionOverlay       = Keys.F3;
        public static Keys FTLOverlay          = Keys.F4;
        public static Keys GravityWellOverlay  = Keys.F5;
        public static Keys RangeOverlay        = Keys.F6;

        // screens and windows
        public static Keys ImportantEventsScreen = Keys.F7;
        public static Keys ColonyOverviewScreen  = Keys.F8;
        public static Keys DeepSpaceBuildWindow  = Keys.B;
        public static Keys TroopListScreen       = Keys.C;
        public static Keys BlueprintsScreen      = Keys.F;
        public static Keys ExoticListScreen      = Keys.G;
        public static Keys AutomationWindow      = Keys.H;
        public static Keys FleetDesignScreen     = Keys.J;
        public static Keys ShipListScreen        = Keys.K;
        public static Keys PlanetListScreen      = Keys.L;
        public static Keys ExoticBonusesWindow   = Keys.M;
        public static Keys FreighterUtilWindow   = Keys.N;
        public static Keys EmpirePatrolsScreen   = Keys.P;
        public static Keys ShipPieMenu           = Keys.Q;

        // game commands
        public static Keys QuickSave     = Keys.F9;
        public static Keys CinematicMode = Keys.F11;

        // the display name a tooltip announces - ONE source, so a remapped key can
        // never lie on screen (the "F3" literal trap)
        public static string Name(Keys key) => key.ToString();

        static string OverrideFile => Path.Combine(Dir.StarDriveAppData, "Hotkeys.yaml");

        static FieldInfo[] BindingFields() => typeof(KeyBindings)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Filter(f => f.FieldType == typeof(Keys));

        public static void Load()
        {
            try
            {
                string file = OverrideFile;
                if (!File.Exists(file))
                {
                    WriteTemplate(file);
                    return;
                }
                foreach (string rawLine in File.ReadAllLines(file))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    int colon = line.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    string action = line.Substring(0, colon).Trim();
                    string keyName = line.Substring(colon + 1).Trim();
                    FieldInfo field = BindingFields().Find(f => f.Name == action);
                    if (field == null)
                    {
                        Log.Warning($"Hotkeys.yaml: unknown action '{action}', line skipped");
                        continue;
                    }
                    if (!Enum.TryParse(keyName, ignoreCase: true, out Keys key))
                    {
                        Log.Warning($"Hotkeys.yaml: unknown key '{keyName}' for '{action}', default kept");
                        continue;
                    }
                    field.SetValue(null, key);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Hotkeys.yaml load failed, defaults kept: {e.Message}");
            }
        }

        // first run: write the full current layout, commented, so the file documents itself
        static void WriteTemplate(string file)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# StarDrive hotkey overrides - edit a line and restart the game.");
                sb.AppendLine("# Format: ActionName: KeyName (XNA key names: A..Z, F1..F12, OemTilde...)");
                sb.AppendLine("# A line starting with # is a comment; unknown names are reported and skipped.");
                foreach (FieldInfo f in BindingFields())
                    sb.AppendLine($"{f.Name}: {(Keys)f.GetValue(null)}");
                File.WriteAllText(file, sb.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"Hotkeys.yaml template write failed: {e.Message}");
            }
        }
    }
}
