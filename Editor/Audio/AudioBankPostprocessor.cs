using System;
using UnityEditor;
using UnityEngine;

namespace Essential.Audio.Editor
{
    /// <summary>
    /// Keeps the bank in step with the folders on disk. Dropping a new sound into
    /// the source folder should be all it takes for <c>Sfx.NewSound</c> to exist,
    /// with no menu item to remember.
    ///
    /// The rebuild only runs when something actually changed under a watched folder,
    /// so importing unrelated assets costs nothing.
    /// </summary>
    sealed class AudioBankPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] movedTo, string[] movedFrom)
        {
            var bank = AudioBankLocator.Find();
            if (bank == null || !bank._rebuildOnAudioImport) return;

            if (!TouchesWatchedFolder(bank, imported)
                && !TouchesWatchedFolder(bank, deleted)
                && !TouchesWatchedFolder(bank, movedTo)
                && !TouchesWatchedFolder(bank, movedFrom))
            {
                return;
            }

            if (!AudioBankGenerator.NeedsRebuild(bank)) return;

            // Deferred, because rebuilding writes assets and that is not safe from
            // inside the import callback itself.
            EditorApplication.delayCall += () =>
            {
                if (bank == null) return;
                AudioBankGenerator.Rebuild(bank, logResult: false);
                Debug.Log("[Essential.Audio] Bank updated to match the audio folders.", bank);
            };
        }

        static bool TouchesWatchedFolder(AudioBank bank, string[] paths)
        {
            foreach (var path in paths)
            {
                var normalized = path.Replace('\\', '/');

                if (IsUnder(normalized, bank._sfxSourceFolder)) return true;
                if (IsUnder(normalized, bank._bgmSourceFolder)) return true;
            }

            return false;
        }

        static bool IsUnder(string path, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return false;

            folder = folder.Replace('\\', '/').TrimEnd('/');
            return path.StartsWith(folder + "/", StringComparison.Ordinal);
        }
    }
}
