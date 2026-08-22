using System;
using UnityEngine;

namespace Essential.Audio
{
    /// <summary>
    /// A serializable reference to a sound effect in the bank.
    ///
    /// This exists so components can expose a sound in the inspector without the
    /// package needing to know about the generated <c>Sfx</c> constants, which live
    /// in the game's own assembly. The editor draws it as a searchable dropdown of
    /// the bank's keys; in code it converts to and from a plain string, so
    /// <c>PlaySfx(Sfx.Heal)</c> and <c>PlaySfx(someRef)</c> both compile.
    /// </summary>
    [Serializable]
    public struct SfxRef : IEquatable<SfxRef>
    {
        [SerializeField] string _key;

        public SfxRef(string key) => _key = key;

        public string Key => _key;
        public bool IsEmpty => string.IsNullOrEmpty(_key);

        public static implicit operator SfxRef(string key) => new SfxRef(key);
        public static implicit operator string(SfxRef reference) => reference._key;

        public bool Equals(SfxRef other) => _key == other._key;
        public override bool Equals(object obj) => obj is SfxRef other && Equals(other);
        public override int GetHashCode() => _key == null ? 0 : _key.GetHashCode();
        public override string ToString() => _key;
    }

    /// <summary>
    /// A serializable reference to a music track in the bank.
    /// The music-side twin of <see cref="SfxRef"/>; kept as a separate type purely
    /// so the inspector dropdown knows which list of keys to offer.
    /// </summary>
    [Serializable]
    public struct BgmRef : IEquatable<BgmRef>
    {
        [SerializeField] string _key;

        public BgmRef(string key) => _key = key;

        public string Key => _key;
        public bool IsEmpty => string.IsNullOrEmpty(_key);

        public static implicit operator BgmRef(string key) => new BgmRef(key);
        public static implicit operator string(BgmRef reference) => reference._key;

        public bool Equals(BgmRef other) => _key == other._key;
        public override bool Equals(object obj) => obj is BgmRef other && Equals(other);
        public override int GetHashCode() => _key == null ? 0 : _key.GetHashCode();
        public override string ToString() => _key;
    }
}
