# CelestiaVR 🌌

> A real-time stargazing experience for Meta Quest 3 — projecting a GPU-rendered star catalogue onto the celestial sphere in room-scale VR.

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.3.5f2-black?style=flat-square&logo=unity" alt="Unity 6000.3.5f2" />
  <img src="https://img.shields.io/badge/target-Meta%20Quest%203-1C1E20?style=flat-square&logo=meta" alt="Meta Quest 3" />
  <img src="https://img.shields.io/badge/OpenXR-supported-0095D5?style=flat-square" alt="OpenXR" />
  <img src="https://img.shields.io/badge/render_pipeline-URP-blueviolet?style=flat-square" alt="URP" />
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License" />
</p>

---

## See It In Action

| | |
|---|---|
| 📺 **[Demo video](https://www.youtube.com/watch?v=D05QMqR-XI0)** | The experience running on-device |
| 🎬 **[Technical presentation](https://rdx-rajat-savdekar.github.io/Celestia_Presentation/)** | Animated walkthrough of the rendering pipeline, built with Manim |

The presentation is the best place to start if you care about *how* it works — it covers each stage of the pipeline as a separate animated scene.

---

## Overview

CelestiaVR renders a star catalogue in virtual reality. Rather than placing a skybox texture around the user, it treats each star as real data — position, magnitude, and colour index — and projects it onto a celestial sphere centred on the viewer, so the sky is spatially correct rather than painted on.

The engineering problem is throughput. A full catalogue contains far more stars than a standalone headset can draw naively at a comfortable framerate, and Quest 3 is mobile hardware running a tile-based GPU with a strict per-frame budget. The pipeline exists to get from raw catalogue data to a stable, comfortable frame: filter aggressively, transform once, and push the per-star work onto the GPU.

Built on Unity 6 with OpenXR, so it targets any compliant runtime — but Quest 3 standalone is the primary platform and the one the performance work is tuned for.

---

## Rendering Pipeline

The five stages below are each covered as an animated scene in the [technical presentation](https://rdx-rajat-savdekar.github.io/Celestia_Presentation/).

### 1. Sphere Projection
Stars are effectively at infinite distance, so they're placed on a unit celestial sphere surrounding the viewer rather than at true scale. Translating the head position doesn't shift the star field — only rotation does, which is what makes the sky read as genuinely distant.

### 2. Magnitude Filtering
Apparent magnitude determines what gets drawn. Culling dim stars below a threshold cuts the draw count by orders of magnitude while removing almost nothing a human eye would resolve in a headset, which is where most of the performance headroom comes from.

### 3. Coordinate Transforms
Catalogue data arrives in equatorial coordinates — right ascension and declination. These are converted into Cartesian world-space positions Unity can render, accounting for the mapping between astronomical and engine coordinate conventions.

### 4. B-V Colour Mapping
The B-V colour index encodes a star's surface temperature. Mapping it through a blackbody approximation gives each star its real colour, so hot stars render blue-white and cool stars render orange-red instead of every star being a uniform white dot.

### 5. Spatial Search
Locating a named star or querying what's near a given direction requires a spatial structure over the catalogue — linear search across the full star list is not viable inside a frame budget.

> 📝 *These descriptions summarise the pipeline stages at a conceptual level. See the presentation for the actual implementation details and derivations.*

---

## Features

- **Real star catalogue rendering** — data-driven, not a skybox texture
- **Physically-motivated star colour** — B-V index mapped to blackbody temperature
- **Magnitude-based culling** — draw-count budget tuned for mobile GPU limits
- **Star search** — spatially indexed lookup across the catalogue
- **Room-scale VR** — OpenXR, with XR Interaction Toolkit interactions
- **Hand tracking** — controller-free interaction via XR Hands
- **Quest 3 standalone** — runs untethered on-device

---

## Built With

| Layer | Technology |
|---|---|
| Engine | Unity 6 (`6000.3.5f2`) |
| Language | C# |
| Rendering | Universal Render Pipeline (URP) |
| XR runtime | OpenXR Plugin |
| Interaction | XR Interaction Toolkit, XR Core Utilities |
| Hand tracking | XR Hands |
| Presentation | Manim |

---

## Supported Platforms

| Platform | Status |
|---|---|
| Meta Quest 3 (standalone) | ✅ Primary target |
| Meta Quest 2 / Pro | ✅ Supported |
| PC VR via OpenXR (SteamVR, WMR) | ✅ Supported |

Any OpenXR-compliant runtime should work, but performance is tuned against Quest 3.

---

## Getting Started

### Prerequisites

- **Unity Hub** with **Unity 6 (`6000.3.5f2`)** installed
  *Other Unity 6 patch versions will likely work, but the project was authored on this one.*
- **Android Build Support** module — including OpenJDK and Android SDK/NDK — required for standalone headset builds
- A **VR headset** with developer mode enabled
- **Git** (and **Git LFS** if you add large binary assets)

### Installation

```bash
git clone https://github.com/RDX-Rajat-Savdekar/CelestiaVR.git
cd CelestiaVR
```

Open the folder through **Unity Hub → Add → Add project from disk**. Unity resolves all Package Manager dependencies automatically on first launch — the initial import takes several minutes while shaders compile.

### Running in the Editor

1. Open `Assets/Scenes/CelestiaVR_Main.unity`
2. Connect your headset (Quest Link or Air Link)
3. Press **Play** — the view mirrors to the headset

### Building for Quest 3

1. **File → Build Settings → Android**, then **Switch Platform**
2. **Project Settings → XR Plug-in Management → Android** → enable **OpenXR**
3. Under **OpenXR**, add the **Oculus Touch Controller Profile** interaction profile
4. Set **Texture Compression** to **ASTC**
5. Connect the headset over USB, accept the debugging prompt, and choose **Build and Run**

---

## Project Structure

```text
CelestiaVR/
├── Assets/
│   ├── Scenes/          # CelestiaVR_Main.unity — the entry scene
│   ├── CelestiaVR/      # Star rendering, catalogue parsing, shaders, prefabs
│   ├── Settings/        # URP render pipeline assets and quality settings
│   └── Samples/         # Imported XR Interaction Toolkit sample content
├── Packages/            # Package Manager manifest and lockfile
├── ProjectSettings/     # Unity project configuration
└── README.md
```

> ℹ️ `Library/`, `Temp/`, `Obj/`, `Build/`, and `Logs/` are generated locally and should never be committed. If they aren't already ignored, add Unity's [official `.gitignore`](https://github.com/github/gitignore/blob/main/Unity.gitignore) to the repo root.

---

## Screenshots

<!-- Drop images in docs/screenshots/ and reference them here.
     Star field renders photograph well — two or three shots will do more
     for this README than any amount of prose. -->

*Coming soon.*

---

## Troubleshooting

**Headset shows a black screen when entering Play mode**
Check **Project Settings → XR Plug-in Management** and confirm OpenXR is enabled for the platform tab you're currently building to — it's a per-platform setting, so enabling it for Standalone does nothing for Android.

**Controllers render but don't track**
The matching OpenXR *interaction profile* is missing. Add it under **XR Plug-in Management → OpenXR → Interaction Profiles**.

**Pink or magenta materials after opening the project**
Materials are using Built-in pipeline shaders. Select them and switch to a URP equivalent, or run **Window → Rendering → Render Pipeline Converter**.

**Framerate drops or stutters on-device**
Lower the magnitude cutoff to reduce the number of stars drawn. Profile with **OVR Metrics Tool** rather than editor stats — editor performance is not representative of standalone.

**Build fails with an Android SDK or JDK error**
Install the Android Build Support module through Unity Hub rather than pointing Unity at a system-wide SDK — mismatched versions are the usual cause.

---

## Roadmap

- [ ] Constellation lines and labels
- [ ] Time controls — scrub the sky forward and backward through dates
- [ ] Location selection to view the sky from anywhere on Earth
- [ ] Deep-sky objects beyond the stellar catalogue
- [ ] Spatial audio pass
- [ ] Publish an APK under Releases for one-click install

---

## Acknowledgements

- Star catalogue data — *add your source here (HYG Database, Hipparcos, Yale BSC, etc.)*
- [Manim](https://www.manim.community/) — used to build the technical presentation

---

<p align="center">
  ⭐ If you found this project useful, consider giving it a star.<br />
  Built with Unity and OpenXR.
</p>
