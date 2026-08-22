using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Essential.Audio.Editor
{
    /// <summary>
    /// Turns a folder of audio files into a populated <see cref="AudioBank"/> plus a
    /// pair of constant classes in the game's own scripts folder.
    ///
    /// Two rules shape the design:
    ///
    /// The generated constants are written under Assets, never into the package.
    /// A package installed from git is read-only, so generated code cannot live
    /// there, and it belongs to the game anyway.
    ///
    /// Sounds are addressed by name rather than by position. The previous
    /// index-based enum meant that adding a folder alphabetically ahead of the
    /// others silently repointed every serialized reference in the project.
    /// </summary>
    public static class AudioBankGenerator
    {
        /// <summary>
        /// Rescans both source folders and rewrites the bank and the constant
        /// classes. Existing per-sound tuning is preserved by key.
        /// </summary>
        /// <param name="bank">The bank to rebuild.</param>
        /// <param name="logResult">False when running from an automatic import, to keep the console quiet.</param>
        public static void Rebuild(AudioBank bank, bool logResult = true)
        {
            if (bank == null)
            {
                Debug.LogError("[Essential.Audio] Cannot rebuild a null AudioBank.");
                return;
            }

            var sfx = ScanFolder(bank._sfxSourceFolder, bank._sfx, isMusic: false);
            var bgm = ScanFolder(bank._bgmSourceFolder, bank._bgm, isMusic: true);

            bank._sfx = sfx;
            bank._bgm = bgm;
            bank.InvalidateLookups();

            EditorUtility.SetDirty(bank);

            WriteConstants("Sfx", sfx, bank, "sound effects");
            WriteConstants("Bgm", bgm, bank, "music tracks");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logResult)
            {
                Debug.Log($"[Essential.Audio] Rebuilt '{bank.name}': " +
                          $"{sfx.Length} sound effects, {bgm.Length} music tracks.", bank);
            }
        }

        /// <summary>
        /// True when the bank no longer matches what is on disk. Used by the import
        /// hook so that unrelated asset imports do not churn the generated files.
        /// </summary>
        public static bool NeedsRebuild(AudioBank bank)
        {
            if (bank == null) return false;

            return !Matches(bank._sfx, ScanFolder(bank._sfxSourceFolder, bank._sfx, isMusic: false))
                || !Matches(bank._bgm, ScanFolder(bank._bgmSourceFolder, bank._bgm, isMusic: true));
        }

        static bool Matches(AudioEntry[] current, AudioEntry[] fresh)
        {
            if (current == null) return fresh.Length == 0;
            if (current.Length != fresh.Length) return false;

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == null) return false;
                if (current[i]._key != fresh[i]._key) return false;

                var currentClips = current[i]._clips ?? Array.Empty<AudioClip>();
                if (!currentClips.SequenceEqual(fresh[i]._clips)) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Scanning
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds entries from a source folder. Each subfolder becomes one sound
        /// holding every clip inside it, which is what gives free randomisation:
        /// drop three shield-crack recordings in one folder and they become
        /// variants of a single sound. Clips sitting loose in the root become
        /// single-clip sounds named after the file, so a one-off does not need a
        /// folder of its own.
        /// </summary>
        static AudioEntry[] ScanFolder(string folder, AudioEntry[] existing, bool isMusic)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Array.Empty<AudioEntry>();

            var previous = BuildPreviousLookup(existing);
            var entries = new List<AudioEntry>();

            foreach (var subfolder in AssetDatabase.GetSubFolders(folder).OrderBy(NameOf, StringComparer.Ordinal))
            {
                var clips = LoadClipsIn(subfolder, recursive: true);
                if (clips.Length == 0) continue;

                entries.Add(MakeEntry(NameOf(subfolder), clips, previous, isMusic));
            }

            foreach (var clip in LoadClipsIn(folder, recursive: false))
            {
                var key = clip.name;
                if (entries.Any(e => e._key == key)) continue;

                entries.Add(MakeEntry(key, new[] { clip }, previous, isMusic));
            }

            return entries
                .OrderBy(e => e._key, StringComparer.Ordinal)
                .ToArray();
        }

        static Dictionary<string, AudioEntry> BuildPreviousLookup(AudioEntry[] existing)
        {
            var lookup = new Dictionary<string, AudioEntry>();
            if (existing == null) return lookup;

            foreach (var entry in existing)
            {
                if (entry != null && !string.IsNullOrEmpty(entry._key))
                    lookup[entry._key] = entry;
            }

            return lookup;
        }

        /// <summary>
        /// Creates an entry, carrying over hand-authored tuning from the matching
        /// old entry. Volume and pitch tweaks are the expensive part of setting up
        /// audio, so a rebuild must never throw them away.
        ///
        /// This deliberately builds a fresh entry rather than mutating the old one,
        /// so that <see cref="NeedsRebuild"/> can scan without side effects.
        /// </summary>
        static AudioEntry MakeEntry(string key, AudioClip[] clips,
                                    Dictionary<string, AudioEntry> previous, bool isMusic)
        {
            if (previous.TryGetValue(key, out var carried))
            {
                return new AudioEntry
                {
                    _key = key,
                    _clips = clips,
                    _volume = carried._volume,
                    _pitchRange = carried._pitchRange,
                    _loop = carried._loop
                };
            }

            // Effects get a touch of pitch variation so repeats do not sound
            // machine-gunned; music is left at its authored pitch and loops.
            return new AudioEntry
            {
                _key = key,
                _clips = clips,
                _volume = 1f,
                _pitchRange = isMusic ? new Vector2(1f, 1f) : new Vector2(0.95f, 1.05f),
                _loop = isMusic
            };
        }

        static AudioClip[] LoadClipsIn(string folder, bool recursive)
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });

            var clips = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => recursive || NormalizeDirectory(path) == folder)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(clip => clip != null)
                .ToArray();

            return clips;
        }

        static string NormalizeDirectory(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath);
            return directory == null ? string.Empty : directory.Replace('\\', '/');
        }

        static string NameOf(string assetPath)
        {
            return assetPath.Substring(assetPath.LastIndexOf('/') + 1);
        }

        // ------------------------------------------------------------------
        // Constant generation
        // ------------------------------------------------------------------

        /// <summary>
        /// Writes a class of string constants, one per sound. Constants rather than
        /// an enum because the generated file lives in the game's assembly while the
        /// bank lives in the package: a constant carries its own value across that
        /// boundary, where an enum type would need a reference the package cannot
        /// have. Call sites read the same either way.
        /// </summary>
        static void WriteConstants(string className, AudioEntry[] entries, AudioBank bank, string description)
        {
            var folder = bank._generatedScriptFolder;
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogError("[Essential.Audio] The bank has no generated scripts folder set.", bank);
                return;
            }

            if (!folder.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) && folder != "Assets")
            {
                Debug.LogError($"[Essential.Audio] Generated scripts folder must be under Assets, got '{folder}'. " +
                               "Packages installed from git are read-only, so generated code cannot live inside them.", bank);
                return;
            }

            EnsureAssetFolder(folder);

            var hasNamespace = !string.IsNullOrWhiteSpace(bank._generatedNamespace);
            var indent = hasNamespace ? "        " : "    ";

            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated>");
            builder.AppendLine($"//     Generated from {bank._sfxSourceFolder} and {bank._bgmSourceFolder}");
            builder.AppendLine("//     by Essential > Audio. Edits will be overwritten on the next rebuild.");
            builder.AppendLine("// </auto-generated>");
            builder.AppendLine();

            if (hasNamespace)
            {
                builder.AppendLine($"namespace {bank._generatedNamespace.Trim()}");
                builder.AppendLine("{");
            }

            builder.AppendLine($"{(hasNamespace ? "    " : "")}/// <summary>Every one of this project's {description}, by name.</summary>");
            builder.AppendLine($"{(hasNamespace ? "    " : "")}public static class {className}");
            builder.AppendLine($"{(hasNamespace ? "    " : "")}{{");

            var used = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                var identifier = MakeUniqueIdentifier(entry._key, used);
                builder.AppendLine($"{indent}public const string {identifier} = \"{Escape(entry._key)}\";");
            }

            builder.AppendLine();
            builder.AppendLine($"{indent}/// <summary>All {description} in the bank, in the order they appear.</summary>");
            builder.AppendLine($"{indent}public static readonly string[] All =");
            builder.AppendLine($"{indent}{{");
            foreach (var entry in entries)
            {
                builder.AppendLine($"{indent}    \"{Escape(entry._key)}\",");
            }
            builder.AppendLine($"{indent}}};");

            builder.AppendLine($"{(hasNamespace ? "    " : "")}}}");

            if (hasNamespace) builder.AppendLine("}");

            var path = Path.Combine(folder, className + ".cs").Replace('\\', '/');
            var contents = builder.ToString();

            // Skip identical writes so an automatic rebuild does not trigger a
            // pointless script recompile every time an unrelated asset imports.
            if (File.Exists(path) && File.ReadAllText(path) == contents) return;

            File.WriteAllText(path, contents);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>
        /// Turns a folder name into a legal C# identifier. Folder names with spaces
        /// or dashes are common and should not be a reason to rename assets, so
        /// "Email Sent" becomes the identifier Email_Sent while its value stays the
        /// original "Email Sent" that the bank is keyed by.
        /// </summary>
        static string MakeUniqueIdentifier(string key, Dictionary<string, string> used)
        {
            var identifier = Sanitize(key);

            if (used.TryGetValue(identifier, out var owner))
            {
                // Two different folder names collapsing onto one identifier would not
                // compile, so disambiguate and say why.
                Debug.LogWarning($"[Essential.Audio] '{key}' and '{owner}' both sanitise to '{identifier}'. " +
                                 "Rename one of the folders to keep call sites readable.");

                int suffix = 2;
                while (used.ContainsKey(identifier + "_" + suffix)) suffix++;
                identifier += "_" + suffix;
            }

            used[identifier] = key;
            return identifier;
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var builder = new StringBuilder(name.Length);
            foreach (var character in name)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            var result = builder.ToString();
            if (char.IsDigit(result[0])) result = "_" + result;

            // Contextual keywords are fine as identifiers, reserved words are not.
            return IsReservedWord(result) ? "@" + result : result;
        }

        static readonly HashSet<string> ReservedWords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        static bool IsReservedWord(string identifier) => ReservedWords.Contains(identifier);

        static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Creates a folder and any missing parents through the AssetDatabase, so
        /// Unity registers them straight away. Creating them on disk alone would
        /// leave the following ImportAsset call pointing at a folder Unity has not
        /// noticed yet.
        /// </summary>
        internal static void EnsureAssetFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var path = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                var next = path + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
