using System.IO;
using UnityEditor;
using UnityEngine;

namespace Essential.Juice.Editor
{
    /// <summary>
    /// Creates the settings asset where the runtime expects it. Juice works
    /// without one, on built-in defaults, so this is only needed to start tuning.
    /// </summary>
    public static class JuiceSetup
    {
        const string SettingsAssetPath = "Assets/Resources/" + JuiceSettings.ResourcePath + ".asset";

        [MenuItem("Tools/Essential/Juice/Set Up Juice", priority = 100)]
        public static void SetUp()
        {
            var settings = GetOrCreateSettings();

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        static JuiceSettings GetOrCreateSettings()
        {
            var existing = FindSettings();
            if (existing != null)
            {
                var path = AssetDatabase.GetAssetPath(existing);

                // Settings that already exist somewhere else get moved rather than
                // duplicated, since the runtime only ever loads the one in Resources.
                if (path != SettingsAssetPath)
                {
                    EnsureAssetFolder(Path.GetDirectoryName(SettingsAssetPath).Replace('\\', '/'));
                    var error = AssetDatabase.MoveAsset(path, SettingsAssetPath);

                    if (string.IsNullOrEmpty(error))
                        Debug.Log($"[Essential.Juice] Moved the existing settings to {SettingsAssetPath}.", existing);
                    else
                        Debug.LogWarning($"[Essential.Juice] Left the settings at {path}: {error}", existing);
                }

                return existing;
            }

            EnsureAssetFolder(Path.GetDirectoryName(SettingsAssetPath).Replace('\\', '/'));

            var settings = ScriptableObject.CreateInstance<JuiceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Essential.Juice] Created JuiceSettings at {SettingsAssetPath}.", settings);
            return settings;
        }

        static JuiceSettings FindSettings()
        {
            var atExpectedPath = AssetDatabase.LoadAssetAtPath<JuiceSettings>(SettingsAssetPath);
            if (atExpectedPath != null) return atExpectedPath;

            var guids = AssetDatabase.FindAssets("t:" + nameof(JuiceSettings));
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<JuiceSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
