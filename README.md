# Kater1EQ

<p align="center">
  <img src="Assets/Images/avatar.png?v=1" width="96" alt="Kater1EQ Icon">
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

## 👨‍💻 Lead Developer & Author

**Nguyễn Đăng Quang (Kater1)**  
*Software Engineer | VR & Game Development | Vietnam 🇻🇳*

**Kater1EQ** was architected and developed as a specialized software project exploring the intersection of modern desktop UI architecture and real-time audio processing. The project demonstrates a strong technical focus on:

- **Systems Engineering:** C#, .NET 8, WPF, and XAML.
- **Audio Programming:** Windows audio pipeline, NAudio (WASAPI loopback), and Equalizer APO integration.
- **DSP Mathematics:** Implementing RBJ Audio EQ Cookbook biquad equations for frequency-response calculations and auto-gain compensation.
- **Software Architecture:** Clean, lightweight, service-oriented design separating UI rendering from DSP execution.

---

## 📖 Overview

**Kater1EQ** is a lightweight Windows desktop equalizer designed to provide a clean, fast, and highly responsive interface for system-wide audio processing.

Instead of implementing the entire audio DSP pipeline inside the WPF application, Kater1EQ uses **Equalizer APO** as the real-time system audio processing engine. 

The application focuses on:
- Equalizer control & Real-time filter editing
- Preset management
- Frequency-response & Real-time waveform visualization
- System volume control
- Persistent settings & Theme customization
- A lightweight desktop-first UI

The goal is simple:
> **A serious audio utility without the complexity and resource overhead of a full audio workstation.**

---

## ✨ Features

### 🎚️ Real-time EQ
Kater1EQ provides a multi-band parametric EQ interface with editable:
- Frequency, Gain, Q factor
- Filter type & Filter slope
- Band enable/disable

Supported filter types include:
| Filter | Description |
|---|---|
| **Bell** | Parametric peaking filter |
| **Low Shelf** | Low-frequency shelving |
| **High Shelf** | High-frequency shelving |
| **Low Pass** | Low-pass filter |
| **High Pass** | High-pass filter |
| **Notch** | Band-reject filter |

Changes are written to the Equalizer APO configuration and applied to the system audio pipeline in real-time.

### 🎧 System-wide Audio Processing
Kater1EQ uses **Equalizer APO** as its audio engine. The WPF application does not continuously process the full audio stream itself. Instead, it generates an Equalizer APO configuration containing the active filters, keeping the application lightweight.

```text
Windows Audio
      │
      ▼
Equalizer APO
      │
      │  Kater1EQ filters
      ▼
Output Device
