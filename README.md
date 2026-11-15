# LibreHardwareHub

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-blue.svg)](https://opensource.org/licenses/MPL-2.0)
[![Target: .NET 8.0 LTS](https://img.shields.io/badge/.NET-8.0_LTS-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform: Windows x64](https://img.shields.io/badge/Platform-Windows_x64-0078D6.svg)](https://www.microsoft.com/windows)

**LibreHardwareHub** is a modern, high-performance hardware monitoring, active fan control, hardware tuning, and in-game HUD overlay suite for Windows.

Built as an advanced fork of LibreHardwareMonitor, LibreHardwareHub unifies the codebase on **.NET 8.0 LTS**, introduces a responsive **Bento Card Dashboard**, an anti-cheat safe **In-Game Overlay (HUD)** with transparent layered rendering, a safety-oriented **Fan Control Engine**, **CPU/GPU Tuning**, a modernized **Remote Web Server**, and a **100% Autonomous Portable Package** requiring zero runtime installations.

---

## What is Included?

| Component | Description | Target Framework |
|---|---|---|
| **LibreHardwareHub** | Desktop graphical interface, Bento dashboard, in-game overlay, fan control, tuning, and embedded web server | `.NET 8.0 LTS (net8.0-windows)` |
| **LibreHardwareMonitorLib** | Core library providing low-level hardware detection, sensor polling, and PawnIO driver communication | `.NET 8.0 LTS (net8.0)` |
| **Aga.Controls** | TreeViewAdv multi-column sensor tree component optimized for .NET 8.0 | `.NET 8.0 LTS (net8.0-windows)` |

---

## Key Features

### 1. Responsive Bento Card Dashboard
* **Dynamic Grid Layout:** High-density Bento cards for CPU, GPU, Memory, Storage, and Motherboard/Network with uniform row height.
* **Full-Row Expansion:** Clicking any card smoothly expands it across the full width of the window, displaying all detailed sensors grouped by type without disrupting page flow.
* **CPU Core Matrix:** Real-time per-core load matrix and frequencies.
* **Unified Sensor Formatting:** Centralized lookup via `SensorLookup` with zero LINQ allocations on the telemetry path.

### 2. In-Game Overlay HUD (`Alt + R`)
* **Anti-Cheat Safe & Zero DLL Injection:** Rendered as an external layered window through the Windows Desktop Window Manager (DWM) using native Win32 `UpdateLayeredWindow` and persistent `CreateDIBSection` buffers. Works seamlessly in borderless and windowed games without triggering VAC, EasyAntiCheat, or BattlEye.
* **HUD Studio:** Live preview matching the in-game display, free drag-and-drop repositioning with magnetic 8px grid snapping, quick alignment presets, and per-metric custom colors.
* **Dynamic Thermal Colors:** Automatic green/yellow/red color shifts based on configurable temperature and load severity thresholds.
* **Universal Sensor Pinning:** Add any motherboard, CPU, GPU, or storage sensor directly to the HUD from the context menu.

### 3. Active Fan Control Engine
* **Custom PWM Curves:** Interactive multi-point curve editor with linear interpolation and asymmetrical hysteresis (+1.5 C spin-up, -3.0 C spin-down).
* **Hardware Fail-Safes:** Hard Thermal Override automatically forces 100% PWM when the target temperature reaches or exceeds 90 C.
* **Dead-Man's Switch:** Reverts all fan channels to BIOS defaults on process exit, crash, or Windows session termination.

### 4. Safe Hardware Tuning (CPU & GPU)
* **CPU Tuning:** Intel RAPL package power limits (PL1/PL2) and universal Windows processor performance boost policies.
* **GPU Tuning:** Core clock offsets, memory clock offsets, and power targets (NVIDIA NVML / AMD Overdrive) with strict hardware clamping.
* **Dual Watchdog Protection:** 15-second user confirmation test watchdog and 120-second dirty-boot watchdog that automatically restores safe factory defaults in the event of instability.

### 5. Modern Remote Web Dashboard (Port 8085)
* Zero external dependencies (vanilla ES2022 JavaScript, semantic HTML5, and responsive CSS).
* Automatic light, dark, and OLED black theme support (`light-dark()`).
* Real-time sensor streaming via `/data.json` with instant search and filtering.

### 6. Zero Allocation & Low-Power Tray Mode
* Adaptive tiered sensor polling (critical sensors polled frequently, SMART storage and battery polled conservatively).
* Full UI rendering suspension and memory footprint minimization (`EmptyWorkingSet` and `GC.Collect`) when minimized to the System Tray.

---

## Downloads

Official releases are available on GitHub:
* [Download Latest Release](https://github.com/hukapa/LibreHardwareHub/releases)

### Package Variants
1. **Portable Version (`LibreHardwareHub-v<version>-Portable.zip`):**
   * Fully self-contained single-file executable with embedded .NET 8.0 runtime and compression.
   * Runs out of the box on Windows 10/11 x64 without needing .NET installed.
   * Stores all settings locally in `LibreHardwareHub.config` beside the executable (enabled by `portable.txt`).
2. **Installer Version (`LibreHardwareHub-v<version>-Setup.exe`):**
   * Installs to `Program Files\LibreHardwareHub`.
   * Creates Start Menu and Desktop shortcuts.
   * Manages Windows startup via Task Scheduler with elevated rights (no UAC prompt on boot).
   * Stores configuration in `%AppData%\LibreHardwareHub\`.

---

## Quick Start (Portable)

1. Download **`LibreHardwareHub-v<version>-Portable.zip`** from the releases page.
2. Extract all files to a folder of your choice (e.g. `C:\Tools\LibreHardwareHub`).
3. Double-click **`LibreHardwareHub.exe`**. The application automatically requests Administrator elevation via UAC.
4. On first launch on a clean system, accept the installation of the included **PawnIO** driver to enable low-level sensor access (MSR, Super I/O, SMBus).

---

## System Requirements & Supported Targets

* **Operating System:** Windows 10 x64 (1809 or higher) / Windows 11 x64
* **Runtime:** None required when using the standalone self-contained executable. .NET 8.0 SDK is required only for building from source.
* **Driver:** PawnIO low-level driver (included in the distribution package).

| Project | Target Framework | Architecture | Mode |
|---|---|---|---|
| LibreHardwareHub | `net8.0-windows` | `win-x64` | Self-Contained Single-File |
| LibreHardwareMonitorLib | `net8.0` | `x64` | Class Library |
| Aga.Controls | `net8.0-windows` | `x64` | WinForms Component |

---

## Developer Information

### Integrating LibreHardwareMonitorLib in Your Project

1. Reference the library or add the `LibreHardwareMonitorLib` project to your solution.
2. Use the sample code below:

```csharp
using System;
using LibreHardwareMonitor.Hardware;

Computer computer = new()
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true,
    IsMotherboardEnabled = true,
    IsControllerEnabled = true,
    IsNetworkEnabled = true,
    IsStorageEnabled = true,
    IsPowerMonitorEnabled = true
};

computer.Open();
computer.Accept(new UpdateVisitor());

foreach (IHardware hardware in computer.Hardware)
{
    Console.WriteLine("Hardware: {0}", hardware.Name);

    foreach (IHardware subHardware in hardware.SubHardware)
    {
        Console.WriteLine("\tSubhardware: {0}", subHardware.Name);

        foreach (ISensor sensor in subHardware.Sensors)
            Console.WriteLine("\t\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
    }

    foreach (ISensor sensor in hardware.Sensors)
        Console.WriteLine("\tSensor: {0}, value: {1}", sensor.Name, sensor.Value);
}

computer.Close();

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
```

### Administrator Rights
Low-level hardware access (reading Super I/O registers, CPU MSRs, and SMBus SPD data) requires elevated administrative privileges. Run your IDE or application as Administrator, or embed an `app.manifest` specifying `requestedExecutionLevel level="requireAdministrator"` or elevate via `Process.Start` with `Verb = "runas"`.

---

## Command Line Options

| Argument | Description |
|---|---|
| `--exit` | Signals running instance via named event to save settings, revert fans to BIOS, and exit cleanly |
| `--register-startup` | Registers elevated Windows startup task via Task Scheduler |
| `--unregister-startup` | Removes Windows startup task |
| `--elevate` | Restarts the application with administrative elevation |

---

## License

LibreHardwareHub is free and open-source software licensed under the **Mozilla Public License 2.0 (MPL 2.0)**.
See [LICENSE](LICENSE) for details.
