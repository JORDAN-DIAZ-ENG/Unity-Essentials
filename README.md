# Unity Essentials

Reusable game systems that survive between projects. Built for game jams: drop the
package in, point it at a folder of sounds, and start calling things.

- **Essential.Audio** — sound effects and music, addressed by name, with free
  randomisation, pitch variation, voice pooling and music crossfades.
- **Essential.Juice** — screen shake, camera kicks, hitstop, slow motion, hit
  flashes, and punch/squash/stretch on any transform, plus trauma-driven post
  effects on URP. Accessibility controls are built in.
- More modules to come (scene management).

Each system is its own assembly, so a project only compiles the parts it uses.

## Install

In the target project, open **Window > Package Manager > + > Install package from
git URL** and paste:

```
https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git
```

To pin a version, append a tag:

```
https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git#v0.2.0
```

Or add it straight to `Packages/manifest.json`:

```json
"com.jordan-diaz-eng.essentials": "https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git"
```

## Audio

### Setup

Run **Tools > Essential > Audio > Set Up Audio** once. It creates the bank at
`Assets/Resources/Essential/AudioBank.asset`, creates the source folders if they
are missing, and does the first build.

Then organise sounds like this:

```
Assets/AudioBank/
├── SFX/
│   ├── Heal/                  <- one folder per sound
│   │   └── heal.wav
│   └── ShieldCrack/           <- several clips = a random variant each play
│       ├── crack_01.wav
│       ├── crack_02.wav
│       └── crack_03.wav
└── BGM/
    └── BossTheme/
        └── boss.mp3
```

A folder becomes one named sound holding every clip inside it. Playing that sound
picks one clip at random, so variation costs nothing but dragging files into a
folder. A clip sitting loose in `SFX/` also becomes a sound, named after the file,
for one-offs that do not deserve a folder.

Adding or removing clips rebuilds the bank automatically. To force it, press the
**Rebuild From Folders** button on the bank, or **Ctrl+Shift+R**.

### Playing things

Folder names become compile-time constants in `Assets/Essential/Generated`, so
call sites autocomplete:

```csharp
using Essential.Audio;

AudioManager.PlaySfx(Sfx.Heal);
AudioManager.PlaySfx(Sfx.ShieldCrack, volume: 0.6f);

// Positioned in the world, so it pans and falls off with distance.
AudioManager.PlaySfxAt(Sfx.FootstepsGrass, transform.position);

// Looping effects hand back the source so you can stop them.
var hum = AudioManager.PlaySfxLooping(Sfx.EngineHum);
AudioManager.StopSfx(hum);
```

Music loops by default and crossfades if you ask it to:

```csharp
AudioManager.PlayBgm(Bgm.BossTheme, fadeIn: 1f, fadeOut: 1f);
AudioManager.StopBgm(fadeOut: 2f);

// Stingers report when they finish, so tracks can chain.
AudioManager.PlayBgm(Bgm.MicrogameWon, loop: false).Finished += () =>
    AudioManager.PlayBgm(Bgm.NextMicrogame);
```

Volume sliders write to the manager, never to the asset:

```csharp
AudioManager.MasterVolume = slider.value;
AudioManager.SfxVolume = 0.8f;
AudioManager.BgmVolume = 0.5f;
```

Nothing needs to be placed in a scene. The runtime builds itself the first time a
sound plays and survives scene loads.

### From the inspector

For sounds picked by designers rather than by code, use `SfxRef` or `BgmRef`. They
draw as a searchable dropdown of everything in the bank:

```csharp
public class Pickup : MonoBehaviour
{
    [SerializeField] SfxRef _collectSound;

    void OnTriggerEnter(Collider other) => AudioManager.PlaySfx(_collectSound);
}
```

There is also an `AudioTrigger` component for the no-code case: add it, pick a
sound, and either set it to fire on a lifecycle event or wire `Play()` to a
button's `onClick`.

### Per-sound tuning

Each entry on the bank carries its own volume trim, pitch range and loop default,
so a sound that is always too loud gets fixed once instead of at every call site.
**Rebuilding preserves this tuning** — it is matched by name, not by position.

## Juice

Juice works with no setup. The first call builds what it needs, nothing goes in
the scene, and every value starts from a sensible default.

### Shake

Shake is trauma-based. Each hit adds trauma (0 to 1), trauma drains over time,
and the visible shake is trauma squared times smooth noise. Hits in quick
succession build into a bigger shake instead of restarting a small one.

```csharp
using Essential.Juice;

Juice.Shake(ShakeStrength.Light);
Juice.Shake(ShakeStrength.Heavy);

// Weaker the farther it is from the camera, so a distant explosion only rumbles.
Juice.ShakeAt(ShakeStrength.Heavy, explosion.position);

// Raw trauma, for shake scaled by something like damage dealt.
Juice.AddTrauma(damage / 100f);

// One push that springs back, rather than a rattle. Use it for recoil or hit direction.
Juice.Kick(-aimDirection, 0.15f);

Juice.StopShake();   // e.g. before a cutscene
```

Orthographic cameras shake in the view plane and roll, scaled by
`orthographicSize` so the shake looks the same at any zoom. Perspective cameras
also pitch and yaw. The shake runs on real time, so it keeps going through a
hitstop.

The offset is applied only while the camera renders and is removed straight
afterwards. Follow scripts, Cinemachine and raycasts from the camera never see
it, and nothing fights over the camera's transform. The shaken camera is
`Camera.main` by default; set `Juice.Camera` to use a different one.

### Hitstop

```csharp
Juice.Hitstop(HitstopStrength.Light);
Juice.Hitstop(0.12f);                  // seconds of real time
```

Overlapping hitstops extend to whichever ends last instead of adding up, so ten
hits in one frame freeze once.

**Juice owns `Time.timeScale`.** Pause and slow the game through Juice, not by
writing `Time.timeScale` yourself:

```csharp
Juice.TimeScale = 0f;     // pause
Juice.TimeScale = 0.3f;   // slow motion
```

Hitstop and slow motion multiply on top of this value and give it back intact
when they end. That avoids the classic bug where a hitstop finishing under a
pause menu unpauses the game.

### Slow motion

```csharp
Juice.SlowMotion(0.2f, holdSeconds: 0.6f);        // drop, hold, ease back
Juice.SlowMotion(0.1f, 1f, recoverSeconds: 1.5f); // let a final kill linger
Juice.EndSlowMotion(0.3f);                        // cut it short
```

Slow motion drops time at once, holds it for real-time seconds, then eases back
to full speed. A new call replaces the one running. It layers with hitstop, so
**hitstop, then slow-mo** is the classic final-blow combo. For smooth physics
while time is slowed, turn on interpolation on the Rigidbodies that matter.

### Hit flash

```csharp
Juice.Flash(enemy.gameObject);                    // settings color and length
Juice.Flash(enemy.gameObject, Color.red, 0.15f);
```

Flash turns every mesh and sprite renderer on the object and its children into a
solid silhouette, then puts the original materials back. Particles, trails and
lines are left alone. Because it swaps materials instead of setting a shader
property, it works with any shader. Flashing an object that is already lit
changes the color and extends the flash.

### Punch, squash and stretch

```csharp
Juice.PunchScale(coin, 0.3f);                     // pop 30% bigger and wobble back
Juice.Squash(player.visual, 0.3f);                // landing
Juice.Stretch(player.visual, 0.3f);               // jump
Juice.PunchPosition(enemy.visual, hitDirection, 0.3f);
Juice.PunchRotation(button, new Vector3(0, 0, 15));
```

Punches are damped springs, so they overshoot and settle instead of easing in a
straight line, and several punches on one transform layer together. The offset
is added after every `LateUpdate` and removed before the next frame's physics and
scripts. Game code always reads and writes the real pose, so an enemy can be
knocked while its own AI moves it.

Point punches at a **visual child**, not at an object with a Rigidbody, and
not at a transform an Animator also drives.

### Screen effects (URP)

With URP installed, every shake also pulses chromatic aberration and vignette
(and lens distortion, if you turn it on in the settings), scaled by trauma. Hits
smear the screen in proportion to how hard they shake it, with no extra calls.
The effects run on Juice's own high-priority Volume, so your volumes are never
edited. The camera needs **Post Processing** ticked. This part is a separate
assembly that only compiles when URP is present.

### Accessibility

```csharp
Juice.ShakeIntensity = 0.5f;   // 0 turns shake, kicks and screen effects off
Juice.HitstopEnabled = false;
```

As with the audio volumes, these live at runtime and never touch the settings
asset. Persisting them is the game's job.

### From the inspector

The `JuiceTrigger` component fires any mix of shake, kick, hitstop, slow motion,
flash and a scale pop, squash or stretch. Either set it to fire on a lifecycle
event, or wire `Play()` to a UnityEvent. An explosion prefab with
**Play On: Start** shakes, flashes and pops with no code at all. Give it a
**Visual** to flash and punch a child instead of the object itself.

### Tuning

Run **Tools > Essential > Juice > Set Up Juice** to create
`Assets/Resources/Essential/JuiceSettings.asset`. It holds everything that is
not passed in a call: the trauma and hitstop length for each preset, decay
speed, maximum offsets and angles, the `ShakeAt` falloff distances, the kick and
punch springs, the flash color and length, the slow-motion recovery time and the
screen-effect strengths. Without the asset, the same values are used as built-in
defaults.

## Adding a module

Modules follow one shape:

```
Runtime/<Module>/Essential.<Module>.asmdef      name: Essential.<Module>
Editor/<Module>/Essential.<Module>.Editor.asmdef  references: Essential.<Module>
```

Two rules keep a module packageable:

1. **Never generate code into the package.** A package installed from git lives in
   `Library/PackageCache` and is read-only. Generated code goes under `Assets` in
   the consuming project.
2. **Never reference generated types from package code.** The package assembly
   cannot see the game's assembly. Pass values across that boundary — strings,
   ints, ScriptableObject references — not generated types.

## Developing it

This repository is the package folder itself, so clone it directly into a project's
`Packages` folder. Name the destination after the package rather than the repo, so
the folder name matches the id in `package.json`:

```bash
git clone https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git "Packages/com.jordan-diaz-eng.essentials"
```

Unity then treats it as an embedded package: fully editable, listed in the Package
Manager under **In Project**, and committable straight from that folder. A project
can have it embedded this way *or* installed from the git URL, never both — two
copies of the same package id conflict.

## License

MIT. See [LICENSE.md](LICENSE.md).
