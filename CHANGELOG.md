# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-21

First release as a package. Extracted from the Unity Essentials project, where the
audio system lived under `Assets/Packages/Essentials/Audio`.

### Added

- `Essential.Audio` assembly with a static `AudioManager` API that needs no scene
  object: the runtime builds itself on the first sound and survives scene loads.
- Voice pooling for sound effects, so overlapping sounds each get their own pitch
  instead of sharing one source.
- Music crossfades via two alternating sources, with `fadeIn` and `fadeOut` on
  `PlayBgm` and `StopBgm`.
- Per-sound volume trim, pitch range and loop default on each bank entry. Rebuilds
  preserve this tuning by name.
- `SfxRef` and `BgmRef` serializable references, drawn as a searchable dropdown of
  the bank's sounds, with missing keys flagged rather than silently reset.
- `AudioTrigger` component for wiring sounds to lifecycle events or UnityEvents
  without writing a script.
- Automatic rebuild when clips are added to or removed from the source folders.
- `Tools > Essential > Audio > Set Up Audio` for one-click setup in a new project.

### Changed

- **Sounds are addressed by name, not by index.** The generated `SfxId`/`BgmId`
  enums are replaced by `Sfx`/`Bgm` classes of string constants. Call sites read
  the same (`Sfx.Heal` in place of `SfxId.Heal`), but inserting a folder no longer
  shifts every other id and silently repoints serialized references.
- Generated constants are written to the consuming project under
  `Assets/Essential/Generated` rather than into the package, which a git install
  makes read-only.
- The bank is a `ScriptableObject` loaded from `Resources` rather than a
  `MonoBehaviour` on a prefab, so no prefab has to be placed or wired up.
- Source folders, the generated scripts folder and the generated namespace are all
  configurable on the bank instead of hardcoded to `Assets/AudioBank`.

### Fixed

- `PlayBGM`'s `loop` argument was accepted and then ignored, so music never looped.
- Random pitch on `PlayOneShot` had no effect: the pitch was restored on the shared
  source immediately after the call, which one-shots follow.
- `PlayOneShotControlled` created and destroyed a `GameObject` per sound effect.

### Removed

- `AudioBank.prefab` and `AudioManager.prefab`. Neither is needed now that the
  runtime bootstraps itself.
- The `Sound` class, which was unused.
