using System.IO;
using UnityEditor;
using UnityEngine;

namespace Essential.Audio.Editor
{
    /// <summary>
    /// Inspector for the bank. Beyond the rebuild button it exists to catch the two
    /// setup mistakes that produce silence with no error: a bank that is not in a
    /// Resources folder, and source folders that point nowhere.
    /// </summary>
    [CustomEditor(typeof(AudioBank))]
    public sealed class AudioBankEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var bank = (AudioBank)target;

            DrawResourcesWarning(bank);
            DrawFolderWarnings(bank);

            DrawDefaultInspector();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(
                $"{bank.SfxEntries.Count} sound effects, {bank.BgmEntries.Count} music tracks",
                EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Rebuild From Folders", GUILayout.Height(28)))
                {
                    AudioBankGenerator.Rebuild(bank);
                }
            }

            EditorGUILayout.HelpBox(
                "Rebuilding rescans the source folders, refreshes the clip lists and regenerates " +
                "the Sfx and Bgm constants. Per-sound volume, pitch and loop settings are kept.",
                MessageType.None);
        }

        /// <summary>
        /// The runtime finds the bank through Resources.Load, so a bank saved
        /// anywhere else silently does nothing in a build.
        /// </summary>
        void DrawResourcesWarning(AudioBank bank)
        {
            var path = AssetDatabase.GetAssetPath(bank);
            var expected = "Assets/Resources/" + AudioBank.ResourcePath + ".asset";

            if (string.IsNullOrEmpty(path)) return;
            if (IsAtResourcePath(path)) return;

            EditorGUILayout.HelpBox(
                "This bank is not where the runtime looks for it, so audio calls will find nothing.\n\n" +
                $"Expected: {expected}",
                MessageType.Warning);

            if (GUILayout.Button("Move To Resources"))
            {
                MoveToResources(bank, path, expected);
            }

            EditorGUILayout.Space();
        }

        static bool IsAtResourcePath(string assetPath)
        {
            // Any Resources folder works, not just the one at the project root, so
            // match on the tail of the path rather than the whole thing.
            var normalized = assetPath.Replace('\\', '/');
            return normalized.EndsWith("/Resources/" + AudioBank.ResourcePath + ".asset",
                                       System.StringComparison.Ordinal);
        }

        static void MoveToResources(AudioBank bank, string from, string to)
        {
            AudioBankGenerator.EnsureAssetFolder(Path.GetDirectoryName(to).Replace('\\', '/'));

            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[Essential.Audio] Could not move the bank: {error}", bank);
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Essential.Audio] Moved the bank to {to}.", bank);
        }

        void DrawFolderWarnings(AudioBank bank)
        {
            var missing = "";

            if (!AssetDatabase.IsValidFolder(bank._sfxSourceFolder))
                missing += "\nSound effects: " + Quote(bank._sfxSourceFolder);

            if (!AssetDatabase.IsValidFolder(bank._bgmSourceFolder))
                missing += "\nMusic: " + Quote(bank._bgmSourceFolder);

            if (missing.Length == 0) return;

            EditorGUILayout.HelpBox(
                "A source folder does not exist, so nothing will be found there:" + missing,
                MessageType.Warning);
            EditorGUILayout.Space();
        }

        static string Quote(string value)
        {
            return string.IsNullOrEmpty(value) ? "(not set)" : "'" + value + "'";
        }
    }
}
