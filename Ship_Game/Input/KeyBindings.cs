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
        // Ludoal fork (maintainer feedback): H moved to Policies - the standing orders are
        // opened far more often than the automation preferences, and every letter was taken.
        // Automation ships UNBOUND rather than displacing another screen; it is a mouse page.
        public static Keys AutomationWindow      = Keys.None;
        public static Keys PoliciesWindow        = Keys.H;
        public static Keys FleetDesignScreen     = Keys.J;
        public static Keys ShipListScreen        = Keys.K;
        public static Keys PlanetListScreen      = Keys.L;
        public static Keys ExoticBonusesWindow   = Keys.M;
        public static Keys FreighterUtilWindow   = Keys.N;
        public static Keys EmpirePatrolsScreen   = Keys.P;
        public static Keys ShipPieMenu           = Keys.Q;

        // the main pages (bench 427: they join the table like everyone else)
        public static Keys OpenResearch  = Keys.R;
        public static Keys OpenEconomy   = Keys.T;
        public static Keys OpenShipyard  = Keys.Y;
        public static Keys OpenEmpire    = Keys.U;
        public static Keys OpenDiplomacy = Keys.I;
        public static Keys OpenEspionage = Keys.E;

        // map & camera (bench 434): deliberate zooming joins the table
        public static Keys ZoomToSelection = Keys.PageUp;
        public static Keys ZoomOut         = Keys.PageDown;

        // Shipyard: open/close the design issues screen (T was its historical close key)
        public static Keys DesignIssues    = Keys.T;

        // colony navigation (bench 427): leaf through the empire without the mouse
        public static Keys PrevColony   = Keys.OemComma;  // bench 428: brackets need AltGr on AZERTY - dead keys on the maintainer's layout
        public static Keys NextColony   = Keys.OemPeriod;
        public static Keys GoToCapital  = Keys.Home;

        // game commands
        public static Keys QuickSave       = Keys.F9;
        public static Keys CinematicMode   = Keys.F11;
        public static Keys RealisticLights = Keys.F10; // bench 434: was Shift+F5, single-key until combos land

        // the display name a tooltip announces - ONE source, so a remapped key can
        // never lie on screen (the "F3" literal trap)
        public static string Name(Keys key) => key switch
        {
            Keys.None      => Localizer.Token(GameText.HkUnbound),
            Keys.OemComma  => ",",
            Keys.OemPeriod => ".",
            Keys.OemTilde  => "~",
            Keys.OemPlus   => "+",
            Keys.OemMinus  => "-",
            _ => key.ToString()
        };

        static string OverrideFile => Path.Combine(Dir.StarDriveAppData, "Hotkeys.yaml");

        static FieldInfo[] BindingFields() => typeof(KeyBindings)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Filter(f => f.FieldType == typeof(Keys));

        // the shipped layout, captured before Load applies the player's overrides -
        // the per-row and reset-all restores read from here
        static readonly System.Collections.Generic.Dictionary<string, Keys> Defaults = new();
        static KeyBindings()
        {
            foreach (FieldInfo f in BindingFields())
                Defaults[f.Name] = (Keys)f.GetValue(null);
        }

        public static Keys Get(string bind) => (Keys)(BindingFields().Find(f => f.Name == bind)?.GetValue(null) ?? Keys.None);
        public static Keys DefaultOf(string bind) => Defaults.TryGetValue(bind, out Keys k) ? k : Keys.None;

        public static void Set(string bind, Keys key)
        {
            FieldInfo f = BindingFields().Find(x => x.Name == bind);
            if (f == null) return;
            f.SetValue(null, key);
            Save();
        }

        // rebinding is the save - no save button anywhere (the themes' own doctrine)
        public static void Save()
        {
            try { WriteTemplate(OverrideFile); }
            catch (Exception e) { Log.Warning($"Hotkeys.yaml save failed: {e.Message}"); }
        }

        // Ludoal fork: a few bindings live in their OWN input context (the frame captures the
        // mouse/keys independently), so their key never collides with the general namespace - e.g.
        // Design Issues (T) inside the Shipyard coexists with T in Economy. These are excluded from
        // conflict detection: they neither hold a general key nor bump a general holder.
        // extend the condition if another scoped binding is added later
        public static bool IsScoped(string bind) => bind == nameof(DesignIssues);

        // the field currently holding this key, if any - the editor's conflict/swap check. Scoped
        // binds are skipped: assigning their key elsewhere must not unbind them, and vice versa.
        public static string HolderOf(Keys key, string except = null)
        {
            if (key == Keys.None) return null;
            FieldInfo f = BindingFields().Find(x => x.Name != except && !IsScoped(x.Name) && (Keys)x.GetValue(null) == key);
            return f?.Name;
        }

        public static void ResetAll()
        {
            foreach (FieldInfo f in BindingFields())
                if (Defaults.TryGetValue(f.Name, out Keys k))
                    f.SetValue(null, k);
            Save();
        }

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
                string[] lines = File.ReadAllLines(file);
                // bench 429: a v1 file listed EVERY binding as an explicit override, which
                // froze the layout of the build that wrote it - new defaults never landed.
                // v2 files list only the player's actual overrides; a legacy file is
                // regenerated (its lines were the old template, not player choices).
                if (!Array.Exists(lines, l => l.Contains(FileMarker)))
                {
                    WriteTemplate(file);
                    return;
                }
                foreach (string rawLine in lines)
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

        const string FileMarker = "hotkeys-v2"; // overrides-only format, see Load()

        // the file carries ONLY the bindings that differ from the shipped defaults;
        // everything else is documented as a comment, so future default changes land
        static void WriteTemplate(string file)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# StarDrive hotkey overrides ({FileMarker}) - uncomment/edit a line and restart the game.");
                sb.AppendLine("# Format: ActionName: KeyName (XNA key names: A..Z, F1..F12, OemTilde...)");
                sb.AppendLine("# Only uncommented lines override; the commented ones document the defaults.");
                foreach (FieldInfo f in BindingFields())
                {
                    var current = (Keys)f.GetValue(null);
                    string prefix = current != DefaultOf(f.Name) ? "" : "# ";
                    sb.AppendLine($"{prefix}{f.Name}: {current}");
                }
                File.WriteAllText(file, sb.ToString());
            }
            catch (Exception e)
            {
                Log.Warning($"Hotkeys.yaml template write failed: {e.Message}");
            }
        }
    }
}
