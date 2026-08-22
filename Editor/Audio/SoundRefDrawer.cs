using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Essential.Audio.Editor
{
    /// <summary>
    /// Draws <see cref="SfxRef"/> and <see cref="BgmRef"/> as a searchable dropdown
    /// of the bank's sounds instead of a raw text field.
    ///
    /// This is what buys back the one thing an enum gave for free. A key that is no
    /// longer in the bank still shows, marked as missing, rather than silently
    /// resetting to the first sound in the list.
    /// </summary>
    public abstract class SoundRefDrawer : PropertyDrawer
    {
        const string NoneLabel = "(None)";

        protected abstract IEnumerable<AudioEntry> EntriesIn(AudioBank bank);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keyProperty = property.FindPropertyRelative("_key");
            if (keyProperty == null)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var bank = AudioBankLocator.Find();
            if (bank == null)
            {
                // No bank to offer choices from, so fall back to a plain text field
                // rather than showing an empty dropdown the user cannot escape.
                EditorGUI.PropertyField(position, keyProperty, label);
                return;
            }

            var keys = EntriesIn(bank)
                .Select(entry => entry.Key)
                .Where(key => !string.IsNullOrEmpty(key))
                .ToList();

            var current = keyProperty.stringValue;
            var missing = !string.IsNullOrEmpty(current) && !keys.Contains(current);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            var buttonLabel = string.IsNullOrEmpty(current)
                ? NoneLabel
                : missing ? current + "  (missing)" : current;

            var previousColor = GUI.color;
            if (missing) GUI.color = new Color(1f, 0.6f, 0.6f);

            var pressed = EditorGUI.DropdownButton(position, new GUIContent(buttonLabel), FocusType.Keyboard);

            GUI.color = previousColor;

            if (!pressed) return;

            // The selection callback runs after this GUI frame has ended, by which
            // point the SerializedProperty may no longer be valid, so look it up
            // again by path instead of capturing it.
            var serialized = keyProperty.serializedObject;
            var path = keyProperty.propertyPath;

            // AdvancedDropdown rather than a plain Popup: a jam project can easily
            // reach a hundred sounds, and a flat menu that long is unusable.
            var dropdown = new SoundKeyDropdown(new AdvancedDropdownState(), keys, chosen =>
            {
                if (serialized == null || serialized.targetObject == null) return;

                serialized.Update();
                var resolved = serialized.FindProperty(path);
                if (resolved == null) return;

                resolved.stringValue = chosen;
                serialized.ApplyModifiedProperties();
            });

            dropdown.Show(position);
        }
    }

    /// <summary>
    /// The searchable list of sound keys. Unity's AdvancedDropdown gives the search
    /// field and keyboard navigation for free once the items are handed over.
    /// </summary>
    internal sealed class SoundKeyDropdown : AdvancedDropdown
    {
        readonly List<string> _keys;
        readonly System.Action<string> _onPicked;

        internal SoundKeyDropdown(AdvancedDropdownState state, List<string> keys, System.Action<string> onPicked)
            : base(state)
        {
            _keys = keys;
            _onPicked = onPicked;
            minimumSize = new Vector2(240f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Sounds");

            // Id 0 is reserved for the clear option so the index maths below stays
            // aligned with the key list.
            root.AddChild(new AdvancedDropdownItem("(None)") { id = 0 });
            root.AddSeparator();

            for (int i = 0; i < _keys.Count; i++)
            {
                root.AddChild(new AdvancedDropdownItem(_keys[i]) { id = i + 1 });
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            _onPicked(item.id == 0 ? string.Empty : _keys[item.id - 1]);
        }
    }

    [CustomPropertyDrawer(typeof(SfxRef))]
    public sealed class SfxRefDrawer : SoundRefDrawer
    {
        protected override IEnumerable<AudioEntry> EntriesIn(AudioBank bank) => bank.SfxEntries;
    }

    [CustomPropertyDrawer(typeof(BgmRef))]
    public sealed class BgmRefDrawer : SoundRefDrawer
    {
        protected override IEnumerable<AudioEntry> EntriesIn(AudioBank bank) => bank.BgmEntries;
    }

    /// <summary>
    /// Finds the bank for editor tooling. Prefers the one the runtime will actually
    /// load, so the dropdown never offers sounds that would be missing at runtime,
    /// and falls back to any bank in the project while one is still being set up.
    /// </summary>
    internal static class AudioBankLocator
    {
        static AudioBank _cached;

        internal static AudioBank Find()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<AudioBank>(AudioBank.ResourcePath);
            if (_cached != null) return _cached;

            var guids = AssetDatabase.FindAssets("t:" + nameof(AudioBank));
            if (guids.Length == 0) return null;

            _cached = AssetDatabase.LoadAssetAtPath<AudioBank>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return _cached;
        }

        internal static void Invalidate() => _cached = null;
    }
}
