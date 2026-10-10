# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-10-10

### Added

- `Essential.Juice` assembly with a static `Juice` API that needs no scene
  object. The runtime builds itself on first use and survives scene loads.
- Trauma-based screen shake: `Shake(ShakeStrength)`, `AddTrauma`, and the
  distance-faded `ShakeAt`/`AddTraumaAt`. Orthographic and perspective cameras
  are detected automatically and shaken appropriately for each.
- `Kick`, a directional camera push on a damped spring, for recoil and hit
  direction.
- Hitstop with `Hitstop(HitstopStrength)` or `Hitstop(seconds)`. Overlapping
  hitstops extend instead of adding up.
- `Juice.TimeScale`, which owns `Time.timeScale` so a hitstop can never undo a
  pause or slow motion.
- `ShakeIntensity` and `HitstopEnabled` accessibility controls. `ShakeIntensity`
  also scales the screen effects.
- The shake offset is applied only while the camera renders, so it never conflicts
  with follow scripts or Cinemachine. Works with URP, HDRP and the built-in
  pipeline.
- Optional `JuiceSettings` asset for tuning, created by
  `Tools > Essential > Juice > Set Up Juice`. Built-in defaults apply without it.
- `SlowMotion` and `EndSlowMotion`: drop time, hold it, then ease back to full
  speed. Layers with hitstop and with the game's own `TimeScale`.
- `Flash`, which turns an object's mesh and sprite renderers into a solid
  silhouette for a moment. Works with any shader, because it swaps materials
  rather than setting a property.
- `PunchScale`, `Squash`, `Stretch`, `PunchPosition` and `PunchRotation`:
  spring-driven punches on any transform. They are applied only between the
  game's last script and the next frame's first one, so they never fight the
  game's own movement.
- `Essential.Juice.URP`, an optional assembly that pulses chromatic aberration,
  vignette and lens distortion with trauma. It compiles only when URP is
  installed.
- `JuiceTrigger` component for firing shake, kick, hitstop, slow motion, flash
  and scale punches from lifecycle events or UnityEvents without writing a
  script.

## [0.1.1] - 2026-10-03

### Fixed

- With domain reload disabled, volume changes from one play session could carry
  into the next when no bank was loaded. `AudioManager` now resets every static
  field on entering play mode, not just the bank reference.

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
