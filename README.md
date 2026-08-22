# Unity Essentials

Reusable game systems that survive between projects. Built for game jams: drop the
package in, point it at a folder of sounds, and start calling things.

- **Essential.Audio** — sound effects and music, addressed by name, with free
  randomisation, pitch variation, voice pooling and music crossfades.
- More modules to come (juice, scene management).

Each system is its own assembly, so a project only compiles the parts it uses.

## Install

In the target project, open **Window > Package Manager > + > Install package from
git URL** and paste:

```
https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git
```

To pin a version, append a tag:

```
https://github.com/JORDAN-DIAZ-ENG/Unity-Essentials.git#v0.1.0
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
