using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Ship_Game.Data.Yaml;

namespace Ship_Game.Tools.Localization
{
    /// <summary>
    /// Converts StarDrive GameText into C# enums
    /// </summary>
    public static class LocalizationTool
    {
        public static bool UseYAMLFileAsSource = true;

        static IEnumerable<TextToken> GetGameText(string lang, string path)
        {
            Log.Write(ConsoleColor.Cyan, $"GetGameText: {lang} {path}");
            var ser = new XmlSerializer(typeof(LocalizationFile));
            var loc = (LocalizationFile)ser.Deserialize(File.OpenRead(path));
            return loc.TokenList.Select(t => new TextToken(lang, t.Index, null, t.Text));
        }

        static LocalizationDB CreateGameTextEnum(int mode, string bbContent, string gameContent, string modContent, string outputDir)
        {
            string enumFile = $"{outputDir}\\GameText.cs";
            string yamlFile = $"{gameContent}\\GameText.yaml";

            var db = new LocalizationDB("Ship_Game", "GameText", mode, gameContent, modContent);
            db.LoadIdentifiers(enumFile, yamlFile);

            if (UseYAMLFileAsSource)
            {
                if (db.AddFromYaml(yamlFile))
                {
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.RUS.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.SPA.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.UKR.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.GER.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.PTB.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.POL.yaml", logMerge:true);
                    db.AddFromYaml($"{gameContent}\\GameText.Missing.FRA.yaml", logMerge:true); // Ludoal fork
                }
            }
            if (db.NumLocalizations == 0)
            {
                db.AddLocalizations(GetGameText("ENG", $"{gameContent}\\Localization\\English\\GameText_EN.xml"));
                db.AddLocalizations(GetGameText("RUS", $"{gameContent}\\Localization\\Russian\\GameText_RU.xml"));
                db.AddLocalizations(GetGameText("SPA", $"{gameContent}\\Localization\\Spanish\\GameText.xml"));
                db.AddLocalizations(GetGameText("UKR", $"{gameContent}\\Localization\\Ukrainian\\GameText_UKR.xml"));
                db.AddLocalizations(GetGameText("GER", $"{gameContent}\\Localization\\German\\GameText_GER.xml"));
            }

            if (Directory.Exists(outputDir))
                db.ExportCsharp(enumFile);

            db.ExportYaml(yamlFile);
            if (Directory.Exists(bbContent)) // if content exists, copy to our dev folder
            {
                File.Copy(yamlFile, $"{bbContent}\\GameText.yaml", overwrite: true);
            }
            db.ExportMissingTranslationsYaml("RUS", $"{gameContent}\\GameText.Missing.RUS.yaml");
            db.ExportMissingTranslationsYaml("SPA", $"{gameContent}\\GameText.Missing.SPA.yaml");
            db.ExportMissingTranslationsYaml("UKR", $"{gameContent}\\GameText.Missing.UKR.yaml");
            db.ExportMissingTranslationsYaml("GER", $"{gameContent}\\GameText.Missing.GER.yaml");
            db.ExportMissingTranslationsYaml("PTB", $"{gameContent}\\GameText.Missing.PTB.yaml");
            db.ExportMissingTranslationsYaml("POL", $"{gameContent}\\GameText.Missing.POL.yaml");
            db.ExportMissingTranslationsYaml("FRA", $"{gameContent}\\GameText.Missing.FRA.yaml"); // Ludoal fork

            if (Directory.Exists(modContent))
            {
                if (UseYAMLFileAsSource)
                {
                    if (db.AddFromModYaml($"{modContent}\\GameText.yaml"))
                    {
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.RUS.yaml", logMerge:true);
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.SPA.yaml", logMerge:true);
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.UKR.yaml", logMerge:true);
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.GER.yaml", logMerge:true);
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.PTB.yaml", logMerge:true);
                        db.AddFromModYaml($"{modContent}\\GameText.Missing.POL.yaml", logMerge:true);
                    }
                }
                if (db.NumModLocalizations == 0)
                {
                    db.AddModLocalizations(GetGameText("ENG", $"{modContent}\\Localization\\English\\GameText_EN.xml"));
                    db.AddModLocalizations(GetGameText("RUS", $"{modContent}\\Localization\\Russian\\GameText_RU.xml"));
                    db.AddModLocalizations(GetGameText("UKR", $"{modContent}\\Localization\\Ukrainian\\GameText_UKR.xml"));
                    db.AddModLocalizations(GetGameText("GER", $"{modContent}\\Localization\\German\\GameText_GER.xml"));
                }
                db.FinalizeModLocalization();
                db.ExportModYaml($"{modContent}\\GameText.yaml");
                db.ExportMissingModYaml("RUS", $"{modContent}\\GameText.Missing.RUS.yaml");
                db.ExportMissingModYaml("SPA", $"{modContent}\\GameText.Missing.SPA.yaml");
                db.ExportMissingModYaml("UKR", $"{modContent}\\GameText.Missing.UKR.yaml");
                db.ExportMissingModYaml("GER", $"{modContent}\\GameText.Missing.GER.yaml");
                db.ExportMissingModYaml("PTB", $"{modContent}\\GameText.Missing.PTB.yaml");
                db.ExportMissingModYaml("POL", $"{modContent}\\GameText.Missing.POL.yaml");
            }
            return db;
        }

        static void UpgradeGameXmls(string contentDir, LocalizationDB db, bool mod)
        {
            UpgradeXmls(db, mod, $"{contentDir}/Buildings", 
                             "NameTranslationIndex", "DescriptionIndex", "ShortDescriptionIndex");
        }

        static void UpgradeXmls(LocalizationDB db, bool mod, string contentFolder, params string[] tags)
        {
            string[] xmls = Directory.GetFiles(contentFolder, "*.xml");
            foreach (string xmlFile in xmls)
                UpgradeXml(db, mod, xmlFile, tags);
        }

        static void UpgradeXml(LocalizationDB db, bool mod, string xmlFile, string[] tags)
        {
            if (!File.Exists(xmlFile))
                return;

            Log.Write(ConsoleColor.Blue, $"Upgrading XML Localizations: {xmlFile}");
            string[] lines = File.ReadAllLines(xmlFile);
            int modified = 0;
            Regex[] patterns = tags.Select(tag => new Regex($"<{tag}>.+\\d+.+<\\/{tag}>"));
            Regex numberMatcher = new Regex("\\d+");
            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];
                foreach (Regex pattern in patterns)
                {
                    if (pattern.Match(line).Success)
                    {
                        // replace number with the new id
                        int id = int.Parse(numberMatcher.Match(line).Value);
                        string nameId = mod ? db.GetModNameId(id) : db.GetNameId(id);
                        string replacement = numberMatcher.Replace(line, nameId);
                        Log.Write(ConsoleColor.Cyan, $"replace {id} => {nameId}");
                        ++modified;
                        lines[i] = replacement;
                        break;
                    }
                }
            }

            if (modified > 0)
            {
                //Log.Write(ConsoleColor.Green, $"Modified {modified} entries");
                //File.WriteAllLines(xmlFile, lines);
            }
        }

        static void ReplaceCsharpTokens(string codeDir, LocalizationDB db)
        {
            string[] codeFiles = Directory.GetFiles(codeDir, "*.cs", SearchOption.AllDirectories);
            foreach (string fileName in codeFiles)
            {
                ReplaceInCsharpFile(fileName, db);
            }
        }

        static void ReplaceInCsharpFile(string fileName, LocalizationDB db)
        {
            string[] lines = File.ReadAllLines(fileName);
            int modified = 0;
            int i = 0;
            void ModifyCurrentLine(string newValue)
            {
                lines[i] = newValue;
                ++modified;
                --i; // after we modify current line, skip back to reprocess this line
            }

            var mInteger = new Regex("\\d+");
            var mLocToken = new Regex("Localizer\\.Token\\(\\d+\\)");
            var mLocText  = new Regex("new LocalizedText\\(\\d+\\)");
            Func<string, string> rLocToken = (nameId) => $"Localizer.Token(GameText.{nameId})";
            Func<string, string> rLocText = (nameId) => $"GameText.{nameId}";

            bool ReplaceIntWithNameId(string line, Regex matcher, Func<string, string> replacement)
            {
                var m = matcher.Match(line);
                if (m.Success)
                {
                    var intM = mInteger.Match(m.Value);
                    if (intM.Success && int.TryParse(intM.Value, out int id))
                    {
                        string nameId = db.GetNameId(id);
                        string replaceWith = replacement(nameId);
                        ModifyCurrentLine(line.Replace(m.Value, replaceWith));
                        return true;
                    }
                }
                return false;
            }

            for (; i < lines.Length; ++i)
            {
                if (ReplaceIntWithNameId(lines[i], mLocToken, rLocToken)) continue;
                if (ReplaceIntWithNameId(lines[i], mLocText, rLocText)) continue;
            }

            if (modified > 0)
            {
                Log.Write(ConsoleColor.Green, $"Modified  {fileName}  ({modified})");
                File.WriteAllLines(fileName, lines);
            }
        }

        // Translations-only merge (CLI: --merge-translations).
        // For each Content/GameText.Missing.<LANG>.yaml it inserts the "<LANG>: ..." row of every
        // matching entry into Content/GameText.yaml, leaving every other byte unchanged. Unlike
        // Run(), it does NOT regenerate the GameText.cs enum, rewrite C# token references, normalize
        // the file, or regenerate the Missing files. Rows whose NameId isn't already present in
        // GameText.yaml are skipped and reported (out-of-scope translations are never injected).
        public static void MergeMissingTranslations()
        {
            string starDrive = Directory.GetCurrentDirectory();
            string gameContent = $"{starDrive}/Content";
            if (!Directory.Exists(gameContent))
                throw new Exception($"Could not find StarDrive/Content at: {gameContent}");

            string yamlFile = $"{gameContent}/GameText.yaml";
            if (!File.Exists(yamlFile))
                throw new Exception($"Could not load base localization: {yamlFile}");

            string[] langs = { "RUS", "SPA", "UKR", "GER", "PTB", "POL", "FRA" };
            int totalMerged = 0, totalSkipped = 0;
            foreach (string lang in langs)
            {
                string missingFile = $"{gameContent}/GameText.Missing.{lang}.yaml";
                if (!File.Exists(missingFile))
                    continue;

                (int merged, int skipped) = MergeMissingLang(yamlFile, missingFile, lang);
                totalMerged  += merged;
                totalSkipped += skipped;
                Log.Write(ConsoleColor.Cyan, $"{lang}: merged {merged}, skipped {skipped} out-of-scope row(s)");
            }

            Log.Write(ConsoleColor.Cyan, $"Translations-only merge complete: {totalMerged} merged, " +
                                         $"{totalSkipped} skipped. {yamlFile} updated; GameText.cs and C# sources untouched.");
        }

        // Reads the two files, runs the pure MergeLangIntoYaml transform, VERIFIES the result still
        // parses as valid GameText YAML, then writes it back (UTF-8 with BOM, matching the tool's
        // other YAML output). If verification fails the original file is left untouched and the
        // problem is reported - so a malformed merge is caught here, not at game load. See
        // MergeLangIntoYaml.
        static (int merged, int skipped) MergeMissingLang(string yamlFile, string missingFile, string lang)
        {
            string result = MergeLangIntoYaml(File.ReadAllText(yamlFile), File.ReadAllText(missingFile),
                                              lang, out int merged, out int skipped);
            VerifyParsesOrThrow(result, yamlFile, lang);
            File.WriteAllText(yamlFile, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return (merged, skipped);
        }

        // Parses the merged YAML with the same reader the game uses (YamlParser -> LangToken) and
        // confirms it still deserializes into at least as many tokens as before. Throws (without
        // writing) if the merge produced anything the game could not load.
        static void VerifyParsesOrThrow(string mergedYaml, string yamlFile, string lang)
        {
            int before;
            try
            {
                before = YamlParser.DeserializeArray<LangToken>(new FileInfo(yamlFile)).Count;
            }
            catch (Exception e)
            {
                throw new Exception($"Base {yamlFile} does not parse before merging {lang}: {e.Message}");
            }

            string tmp = Path.Combine(Path.GetTempPath(), $"GameText.merge.{lang}.yaml");
            File.WriteAllText(tmp, mergedYaml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            try
            {
                int after = YamlParser.DeserializeArray<LangToken>(new FileInfo(tmp)).Count;
                if (after < before)
                    throw new Exception($"token count dropped {before} -> {after}");
            }
            catch (Exception e)
            {
                throw new Exception($"Merged {lang} YAML failed verification - {yamlFile} left unchanged. " +
                                    $"Cause: {e.Message}");
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* best-effort cleanup */ }
            }
        }

        // Pure text transform (no file IO, so it is unit-testable): inserts each " <lang>: ..." row
        // from `missingText` into the matching NameId entry of `yamlText`, appended after that
        // entry's existing rows. Every other byte is preserved - newline style, ordering, trailing
        // newline. NameIds not present in `yamlText` are skipped+counted; entries that already carry
        // the language are left unchanged (so the merge is idempotent).
        // A YAML entry header is a non-indented, non-comment line introducing "NameId:" - the
        // NameId is everything before the first ':'. Shared by both parse passes so they agree.
        static bool IsEntryHeader(string line)
            => line.Length > 0 && line[0] != ' ' && line[0] != '\t' && line[0] != '#' && line.IndexOf(':') >= 0;

        static string HeaderNameId(string line) => line.Substring(0, line.IndexOf(':')).Trim();

        public static string MergeLangIntoYaml(string yamlText, string missingText, string lang,
                                               out int merged, out int skipped)
        {
            string prefix = lang + ":";

            // 1) NameId -> the bare "<lang>: ..." text, read verbatim from the missing text.
            //    The indent is applied at insertion time to match the target entry's own fields.
            var langLineByName = new Dictionary<string, string>();
            string current = null;
            foreach (string raw in missingText.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (IsEntryHeader(line))
                {
                    current = HeaderNameId(line);
                }
                else if (current != null)
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                        langLineByName[current] = trimmed;
                }
            }

            // 2) Walk yaml and append the lang row at the end of each matching entry
            string nl = yamlText.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = yamlText.Split(new[] { nl }, StringSplitOptions.None);

            var outLines = new List<string>(lines.Length + langLineByName.Count);
            var yamlNames = new HashSet<string>();
            int mergedCount = 0;
            string curName = null;
            bool curHasLang = false;
            string curIndent = " "; // indentation of the current entry's fields (default 1 space)

            void FlushEntry()
            {
                if (curName != null && !curHasLang && langLineByName.TryGetValue(curName, out string add))
                {
                    // Match the entry's own field indentation. Most entries use a single space,
                    // but a few legacy entries use two - inserting at the wrong depth corrupts the
                    // YAML structure (a stray dedent), so we mirror what the entry already uses.
                    outLines.Add(curIndent + add);
                    ++mergedCount;
                }
                curName = null; // entry handled; don't append twice
            }

            foreach (string line in lines)
            {
                bool isHeader = IsEntryHeader(line);
                bool isBlank = line.Trim().Length == 0;

                // Close the current entry (appending the lang row) before a new header OR any
                // blank/separator line - never after a trailing blank, so the row lands as the
                // entry's last field and the file's trailing newline is preserved.
                if (isHeader || isBlank)
                    FlushEntry();

                if (isHeader)
                {
                    curName = HeaderNameId(line);
                    curHasLang = false;
                    curIndent = " ";
                    yamlNames.Add(curName);
                }
                else if (curName != null && (line[0] == ' ' || line[0] == '\t'))
                {
                    // a field line of the current entry: remember its indentation
                    curIndent = line.Substring(0, line.Length - line.TrimStart().Length);
                    if (line.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
                        curHasLang = true; // entry already has this language
                }
                outLines.Add(line);
            }
            FlushEntry(); // last entry (no trailing blank case)

            int skippedCount = 0;
            foreach (KeyValuePair<string, string> kv in langLineByName)
                if (!yamlNames.Contains(kv.Key))
                    ++skippedCount;

            merged = mergedCount;
            skipped = skippedCount;
            return string.Join(nl, outLines);
        }

        // modPath: "Mods/My Mod/"
        public static void Run(string modPath, int mode)
        {
            string starDrive = Directory.GetCurrentDirectory();
            string gameContent = $"{starDrive}/Content";
            string modContent = modPath.NotEmpty() ? $"{starDrive}/{modPath}" : "";
            
            if (!Directory.Exists(gameContent))
                throw new Exception($"Could not find StarDrive/Content at: {gameContent}");
            
            if (modPath.NotEmpty() && !Directory.Exists(modContent))
                throw new Exception($"Could not find Mod at: {modContent}");

            string solutionDir = Path.GetFullPath($"{starDrive}/..");
            string bbContent = $"{solutionDir}/Content"; // OPTIONAL
            string codeDir = $"{solutionDir}/Ship_Game"; // OPTIONAL
            string outputDir = $"{codeDir}/Data"; // OPTIONAL

            LocalizationDB db = CreateGameTextEnum(mode, bbContent, gameContent, modContent, outputDir);

            if (Directory.Exists(bbContent))
                UpgradeGameXmls(bbContent, db, mod:false);

            if (modPath.NotEmpty())
                UpgradeGameXmls(modContent, db, mod:true);

            if (Directory.Exists(codeDir))
                ReplaceCsharpTokens(codeDir, db);
        }
    }
}
