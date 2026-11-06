// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public interface IGpuTuningDevice
{
    string Name { get; }

    string DeviceId { get; }

    HardwareType GpuType { get; }

    IHardware Hardware { get; }

    GpuTuningCapabilities Capabilities { get; }

    GpuTuningState CurrentState { get; }

    GpuTuningTelemetry ReadTelemetry();

    bool ApplyTuning(GpuTuningState state, out string errorMessage);

    bool ResetToStock(out string errorMessage);
}
