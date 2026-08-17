# Kater1EQ

<p align="center">
  <img src="Assets/Images/avatar.png" width="96" alt="Kater1EQ Icon">
</p>

<h3 align="center">Kater1EQ</h3>

<p align="center">
  A lightweight system-wide parametric equalizer for Windows.
  <br>
  Built with WPF, .NET 8 and Equalizer APO.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=flat-square&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/WPF-Desktop-0078D4?style=flat-square&logo=windows&logoColor=white">
  <img src="https://img.shields.io/badge/NAudio-2.2.1-222222?style=flat-square">
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D4?style=flat-square&logo=windows&logoColor=white">
  <img src="https://img.shields.io/badge/License-TBD-lightgrey?style=flat-square">
</p>

---

## Overview

**Kater1EQ** is a lightweight Windows desktop equalizer designed to provide a clean, fast and highly responsive interface for system-wide audio processing.

Instead of implementing the entire audio DSP pipeline inside the WPF application, Kater1EQ uses **Equalizer APO** as the real-time system audio processing engine.

The application focuses on:

- Equalizer control
- Real-time filter editing
- Preset management
- Frequency-response visualization
- Real-time waveform visualization
- System volume control
- Persistent settings
- Theme customization
- Social/developer links
- A lightweight desktop-first UI

The goal is simple:

> **A serious audio utility without the complexity and resource overhead of a full audio workstation.**

---

## ✨ Features

### 🎚️ Real-time EQ

Kater1EQ provides a multi-band parametric EQ interface with editable:

- Frequency
- Gain
- Q factor
- Filter type
- Filter slope
- Band enable/disable

Supported filter types include:

| Filter | Description |
|---|---|
| Bell | Parametric peaking filter |
| Low Shelf | Low-frequency shelving |
| High Shelf | High-frequency shelving |
| Low Pass | Low-pass filter |
| High Pass | High-pass filter |
| Notch | Band-reject filter |

Changes are written to the Equalizer APO configuration and applied to the system audio pipeline.

---

### 🎧 System-wide Audio Processing

Kater1EQ uses **Equalizer APO** as its audio engine.

The WPF application does not continuously process the full audio stream itself.

Instead, Kater1EQ generates an Equalizer APO configuration containing the active filters.

This architecture keeps the application lightweight while allowing Equalizer APO to perform the actual real-time DSP processing.

```text
Windows Audio
      │
      ▼
Equalizer APO
      │
      │  Kater1EQ filters
      ▼
Output Device
```

---

### 📈 Frequency Response Visualization

The EQ graph is calculated independently from the actual audio output.

Kater1EQ uses RBJ Audio EQ Cookbook biquad equations to calculate the expected frequency response of the active filters.

The graph supports:

- Multiple EQ bands
- Combined frequency response
- Frequency axis
- dB axis
- Interactive band points
- Selected-band visualization
- Filter-specific response calculations

The calculation layer is separated into:

```text
Services/EqCurveMath.cs
```

This keeps DSP mathematics independent from the UI rendering layer.

---

### 🌊 Real-time Waveform

Kater1EQ can monitor the system's current playback output using **WASAPI loopback capture** through NAudio.

```text
Windows Output
      │
      ▼
WASAPI Loopback
      │
      ▼
NAudio
      │
      ▼
Waveform Buffer
      │
      ▼
WPF Canvas
```

The waveform is for visualization only.

It does **not** replace Equalizer APO as the DSP engine.

The capture system is designed to remain stable when the waveform visualization is toggled repeatedly instead of constantly creating and destroying audio capture sessions.

---

### 🎛️ Auto Gain Compensation

Aggressive EQ boosts can cause the combined response to exceed 0 dB and introduce clipping.

Kater1EQ analyzes the combined frequency response and calculates the worst positive peak:

```text
Peak compensation = -max(0, combined EQ peak)
```

The resulting compensation is written as Equalizer APO preamp.

This preserves the relative shape of the EQ curve while reducing the risk of digital clipping.

Conceptually:

```text
EQ Curve
   │
   ├── Peak analysis
   │
   ▼
Auto Compensation
   │
   ▼
Equalizer APO Preamp
```

---

## 🎨 UI & Themes

Kater1EQ is designed around a desktop audio-utility aesthetic rather than a generic Windows settings interface.

Current visual direction:

```text
Clean
Dark
Pixel-inspired
High contrast
Technical
Lightweight
```

### Available Themes

- `Pixel`
- `Dark`
- `Pink`

The Pink theme provides a light pastel interface with:

- White background
- Soft pink panels
- Dark pink/purple typography
- Bright pink accent controls

Theme configuration is separated from application logic.

```text
Themes/
├── PixelTheme.xaml
├── PixelFonts.xaml
├── PixelStyles.xaml
├── DarkTheme.xaml
└── PinkTheme.xaml
```

---

## 💾 Persistent Data

Kater1EQ stores user configuration outside the application directory.

### Settings

```text
%AppData%\Kater1EQ\settings.json
```

Used for:

- Current theme
- Application settings

### Social Links

```text
%AppData%\Kater1EQ\social.json
```

Stores user-defined:

- Discord
- GitHub
- Facebook
- YouTube
- TikTok
- X / Twitter
- Website

No personal URLs are hard-coded into the application.

### Presets

User presets are persisted through `PresetService`.

The service is responsible for loading, saving and protecting built-in presets.

---

# 🧩 Architecture

The project follows a lightweight service-oriented WPF architecture.

```text
Kater1EQ/
│
├── App.xaml
├── App.xaml.cs
│
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── PromptDialog.xaml
│
├── Models/
│   ├── EqBand.cs
│   ├── EqBandState.cs
│   ├── EqPreset.cs
│   ├── Settings.cs
│   └── SocialLink.cs
│
├── Services/
│   ├── AudioLoopbackService.cs
│   ├── EqualizerApoService.cs
│   ├── EqCurveMath.cs
│   ├── PresetService.cs
│   ├── SettingsService.cs
│   ├── SystemVolumeService.cs
│   └── SocialService.cs
│
├── Themes/
│   ├── PixelTheme.xaml
│   ├── PixelFonts.xaml
│   ├── PixelStyles.xaml
│   ├── DarkTheme.xaml
│   └── PinkTheme.xaml
│
└── Assets/
    ├── Images/
    ├── Fonts/
    └── app.ico
```

---

# 🔬 DSP Architecture

One of the main design decisions of Kater1EQ is separating **DSP execution** from **DSP visualization**.

### Actual audio processing

Handled by:

```text
EqualizerApoService
        │
        ▼
Equalizer APO
```

### Frequency-response calculation

Handled by:

```text
EqCurveMath
```

### Waveform visualization

Handled by:

```text
AudioLoopbackService
        │
        ▼
WPF waveform renderer
```

This separation prevents the UI from becoming responsible for the entire audio processing pipeline.

---

## Biquad Mathematics

Kater1EQ uses formulas based on the:

**RBJ Audio EQ Cookbook**

for the frequency-response calculations.

The implementation supports:

- Peaking/Bell
- Low Shelf
- High Shelf
- Low Pass
- High Pass
- Notch

For example, low-pass and high-pass filters can be cascaded to achieve steeper slopes:

```text
12 dB/oct → 1 stage
24 dB/oct → 2 stages
36 dB/oct → 3 stages
48 dB/oct → 4 stages
```

The same mathematical model is used when calculating the displayed frequency response and gain compensation.

---

# ⚙️ Equalizer APO Integration

Kater1EQ intentionally avoids overwriting the user's existing Equalizer APO configuration.

Instead, it maintains a dedicated configuration file:

```text
Kater1EQ.txt
```

The main Equalizer APO configuration receives a single include directive:

```text
Include: Kater1EQ.txt
```

Kater1EQ then manages its own generated filters.

Example generated configuration:

```text
Preamp: -4.2 dB

Filter: ON PK Fc 100 Hz Gain 3.0 dB Q 1.00
Filter: ON PK Fc 250 Hz Gain -2.0 dB Q 1.20
Filter: ON PK Fc 1000 Hz Gain 2.5 dB Q 0.90
```

This approach keeps Kater1EQ's configuration isolated from the user's other Equalizer APO settings as much as possible.

---

# 📦 Requirements

Before running Kater1EQ, install:

### 1. Windows

Recommended:

- Windows 10
- Windows 11

### 2. .NET 8

Kater1EQ targets:

```text
.NET 8 / WPF
```

Download:

https://dotnet.microsoft.com/download/dotnet/8.0

### 3. Equalizer APO

Equalizer APO is required because it performs the actual system-wide audio processing.

Download:

https://equalizerapo.com/

During installation, make sure the correct playback device is selected.

After installation, Windows may require a restart.

---

# 🛠️ Building From Source

## Clone

```bash
git clone https://github.com/YOUR_USERNAME/Kater1EQ.git
cd Kater1EQ
```

## Open

Open:

```text
Kater1EQ.sln
```

with:

- Visual Studio 2022

Make sure the workload below is installed:

```text
.NET desktop development
```

## Build

From Visual Studio:

```text
Build → Build Solution
```

or:

```text
Ctrl + Shift + B
```

## Run

```text
F5
```

---

# 📁 Configuration Location

Kater1EQ keeps user data under:

```text
%AppData%\Kater1EQ\
```

Example:

```text
Kater1EQ/
├── settings.json
├── social.json
└── presets.json
```

The application does not require modifying source code when changing user-configurable social links or application settings.

---

# 🎮 Presets

Kater1EQ includes built-in presets for different listening scenarios.

Examples include:

```text
Flat
Bass Boost
Gaming - Footsteps
Vocal Clarity
CS2
PUBG
Valorant
Apex Legends
Warzone
Pop
Rock
EDM
Hip-Hop
Classical
Jazz
Acoustic
Movie
Podcast
Night Mode
League of Legends
Dota 2
Minecraft
Metal
Lo-fi
```

Built-in presets are protected by `PresetService`.

User-created presets can be saved independently.

---

# 🧠 Why Equalizer APO?

A major goal of Kater1EQ is to stay lightweight.

Instead of building a complete custom audio driver and DSP engine, Kater1EQ delegates system-wide audio processing to Equalizer APO.

### Kater1EQ handles

- UI
- EQ editing
- Presets
- Configuration generation
- Frequency-response visualization
- Waveform visualization
- Settings

### Equalizer APO handles

- Real-time audio processing
- System-wide routing
- Filter execution
- Audio DSP

This keeps the application architecture considerably simpler than implementing a custom Windows audio engine.

---

# ⚡ Performance Philosophy

Kater1EQ is designed as a native Windows desktop application.

It does **not** use:

- Unity
- Electron
- Chromium
- WebView-based UI

The application is built with:

```text
C#
.NET 8
WPF
NAudio
Equalizer APO
```

The UI and configuration layer are intentionally lightweight.

The actual audio DSP is delegated to Equalizer APO rather than continuously processing the complete audio stream inside the WPF application.

---

# 🗺️ Roadmap

Kater1EQ is still under active development.

### UI

- [x] Custom WPF interface
- [x] Pixel-inspired styling
- [x] Pink theme
- [x] Dark theme
- [x] Runtime theme foundation
- [x] Custom pixel font support
- [ ] Complete responsive layout pass
- [ ] Dedicated EQ graph UserControl
- [ ] Dedicated Filter Editor UserControl
- [ ] Dedicated Preset Panel UserControl
- [ ] Dedicated Settings Panel UserControl

### Equalizer

- [x] Parametric filters
- [x] Multiple filter types
- [x] Q control
- [x] Filter slope
- [x] Band enable/disable
- [x] Real-time configuration updates
- [x] Automatic gain compensation
- [ ] Additional advanced filter controls

### Visualization

- [x] Frequency-response curve
- [x] Interactive EQ bands
- [x] Real-time waveform
- [ ] Advanced spectrum analyzer
- [ ] Improved pixel-style graph rendering

### Presets

- [x] Built-in presets
- [x] User presets
- [x] Persistent storage
- [x] Default preset protection
- [ ] Preset import/export

### Application

- [x] System tray support
- [x] Persistent settings
- [x] Social links
- [ ] Global hotkeys
- [ ] Optional Windows startup
- [ ] Installer
- [ ] Release packaging
- [ ] Automatic Equalizer APO detection/setup improvements

---

# 🔐 Design Principles

Kater1EQ follows a few simple principles.

### 1. Keep DSP separate from UI

The UI should never become the audio engine.

### 2. Prefer existing system audio infrastructure

Equalizer APO already provides a reliable system-wide DSP layer.

### 3. Keep configuration isolated

Kater1EQ manages its own generated configuration rather than rewriting the user's entire EQ setup.

### 4. Keep the application lightweight

Avoid unnecessary frameworks and background services.

### 5. Make visual feedback immediate

EQ changes should be reflected immediately in:

- Controls
- Frequency curve
- Presets
- Audio configuration

### 6. Don't sacrifice technical accuracy for visual effects

The EQ graph is a visualization of the actual filter mathematics.

---

# 🧪 Development Notes

The project is currently being developed as a Windows-first desktop application.

The codebase is intentionally kept relatively small so that the relationship between:

```text
UI
│
├── Models
│
├── Services
│
└── Equalizer APO
```

remains easy to understand and maintain.

---

# 📸 Screenshots

> Screenshots will be added as the UI reaches the final design stage.

Recommended screenshots:

```text
01-main-window.png
02-eq-editor.png
03-presets.png
04-waveform.png
05-pink-theme.png
06-settings.png
```

---

# 👤 Author

**Nguyễn Đăng Quang**

Game Programming / Software Development

Vietnam 🇻🇳

Kater1EQ started as a personal project focused on exploring:

- C#
- WPF
- XAML
- Windows audio
- Equalizer APO
- DSP mathematics
- desktop UI architecture

---

# 📄 License

License information will be added before the first public release.

Until then, this repository should be considered a development project.

---

<p align="center">

**Kater1EQ**

*A lightweight EQ for Windows.*

</p>
