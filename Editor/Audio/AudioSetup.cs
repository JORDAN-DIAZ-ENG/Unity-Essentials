using System.IO;
using UnityEditor;
using UnityEngine;

namespace Essential.Audio.Editor
{
    /// <summary>
    /// One-click setup, so bringing the package into a new jam project is a menu
    /// item rather than a checklist. Creates the bank where the runtime expects it,
    /// creates the source folders if they are missing, and does the first build.
    /// </summary>
    public static class AudioSetup
    {
        const string BankAssetPath = "Assets/Resources/" + AudioBank.ResourcePath + ".asset";

        [MenuItem("Tools/Essential/Audio/Set Up Audio", priority = 0)]
        public static void SetUp()
        {
            var bank = GetOrCreateBank();

            AudioBankGenerator.EnsureAssetFolder(bank._sfxSourceFolder);
            AudioBankGenerator.EnsureAssetFolder(bank._bgmSourceFolder);

            AudioBankGenerator.Rebuild(bank);
            AudioBankLocator.Invalidate();

            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);
        }

        [MenuItem("Tools/Essential/Audio/Rebuild Bank %#r", priority = 1)]
        public static void RebuildActiveBank()
        {
            var bank = AudioBankLocator.Find();
            if (bank == null)
            {
                Debug.LogWarning("[Essential.Audio] No AudioBank in the project yet. " +
                                 "Run Tools > Essential > Audio > Set Up Audio first.");
                return;
            }

            AudioBankGenerator.Rebuild(bank);
        }

        [MenuItem("Tools/Essential/Audio/Select Bank", priority = 2)]
        public static void SelectBank()
        {
            var bank = AudioBankLocator.Find();
            if (bank == null)
            {
                SetUp();
                return;
            }

            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);
        }

        static AudioBank GetOrCreateBank()
        {
            var existing = AudioBankLocator.Find();
            if (existing != null)
            {
                var path = AssetDatabase.GetAssetPath(existing);

                // A bank that already exists somewhere else gets moved rather than
                // duplicated, so a project never ends up with two rival banks.
                if (path != BankAssetPath)
                {
                    AudioBankGenerator.EnsureAssetFolder(Path.GetDirectoryName(BankAssetPath).Replace('\\', '/'));
                    var error = AssetDatabase.MoveAsset(path, BankAssetPath);

                    if (string.IsNullOrEmpty(error))
                        Debug.Log($"[Essential.Audio] Moved the existing bank to {BankAssetPath}.", existing);
                    else
                        Debug.LogWarning($"[Essential.Audio] Left the bank at {path}: {error}", existing);
                }

                return existing;
            }

            AudioBankGenerator.EnsureAssetFolder(Path.GetDirectoryName(BankAssetPath).Replace('\\', '/'));

            var bank = ScriptableObject.CreateInstance<AudioBank>();
            AssetDatabase.CreateAsset(bank, BankAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Essential.Audio] Created a new AudioBank at {BankAssetPath}.", bank);
            return bank;
        }
    }
}
