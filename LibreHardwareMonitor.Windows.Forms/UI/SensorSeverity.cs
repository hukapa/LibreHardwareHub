using System;
using System.Drawing;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public enum SeverityLevel
{
    None,
    Normal,
    Warm,
    Hot
}

/// <summary>
/// Classifies sensor readings into visual severity levels and normalized fill ratios,
/// based on real electrical specifications (ATX ±5%, CR2032 battery) and thermal baselines.
/// Prevents false positive alarms when components are operating normally at scale.
/// </summary>
public static class SensorSeverity
{
    public static SeverityLevel Classify(ISensor sensor)
    {
        if (sensor == null || !sensor.Value.HasValue || float.IsNaN(sensor.Value.Value))
            return SeverityLevel.None;

        return Classify(sensor.SensorType, sensor.Name, sensor.Value.Value);
    }

    public static SeverityLevel Classify(SensorType type, float? value)
    {
        if (!value.HasValue || float.IsNaN(value.Value))
            return SeverityLevel.None;

        return Classify(type, string.Empty, value.Value);
    }

    public static SeverityLevel Classify(SensorType type, string name, float v)
    {
        name ??= string.Empty;

        return type switch
        {
            SensorType.Temperature => ClassifyTemperature(name, v),
            SensorType.Voltage => ClassifyVoltage(name, v),
            // Carga (Load) a 100% e normal durante trabalho ou jogos - nunca deve alarmar a vermelho
            SensorType.Load => SeverityLevel.Normal,
            _ => SeverityLevel.None
        };
    }

    private static SeverityLevel ClassifyTemperature(string name, float temp)
    {
        // CPU e GPU operam normalmente sob carga ate 78-82 °C; alerta térmico real apenas > 85 °C / 90 °C
        if (name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return temp >= 90f ? SeverityLevel.Hot : temp >= 80f ? SeverityLevel.Warm : SeverityLevel.Normal;
        }

        if (name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return temp >= 85f ? SeverityLevel.Hot : temp >= 78f ? SeverityLevel.Warm : SeverityLevel.Normal;
        }

        // Motherboard, discos SSD, VRM, ambiente
        return temp >= 75f ? SeverityLevel.Hot : temp >= 65f ? SeverityLevel.Warm : SeverityLevel.Normal;
    }

    private static SeverityLevel ClassifyVoltage(string name, float v)
    {
        // 1. Linha ATX +12V (Norma ATX ±5%: 11.40 V a 12.60 V)
        if (name.IndexOf("12V", StringComparison.OrdinalIgnoreCase) >= 0 || v >= 8.0f)
        {
            if (v < 11.10f || v > 12.90f)
                return SeverityLevel.Hot;
            if (v < 11.40f || v > 12.60f)
                return SeverityLevel.Warm;
            return SeverityLevel.Normal;
        }

        // 2. Linha ATX +5V (Norma ATX ±5%: 4.75 V a 5.25 V)
        if (name.IndexOf("5V", StringComparison.OrdinalIgnoreCase) >= 0 || (v >= 4.2f && v < 6.0f))
        {
            if (v < 4.50f || v > 5.50f)
                return SeverityLevel.Hot;
            if (v < 4.75f || v > 5.25f)
                return SeverityLevel.Warm;
            return SeverityLevel.Normal;
        }

        // 3. Pilha da Motherboard CMOS / CR2032 (Nominal 3.0V, nova 3.10-3.35V; o perigo e descarga)
        if (name.IndexOf("CMOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("VBAT", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (v < 2.65f || v > 3.65f)
                return SeverityLevel.Hot; // Bateria gasta ou falha do circuito
            if (v < 2.80f)
                return SeverityLevel.Warm; // A descarregar
            return SeverityLevel.Normal; // 2.80 V a 3.40 V e saude excelente
        }

        // 4. Linhas ATX +3.3V, AVCC, +3V Standby / 3VSB (Norma ATX ±5%: 3.135 V a 3.465 V)
        if (name.IndexOf("3.3V", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("3V", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("AVCC", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Standby", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (v >= 2.6f && v < 3.8f))
        {
            if (v < 3.00f || v > 3.60f)
                return SeverityLevel.Hot;
            if (v < 3.135f || v > 3.465f)
                return SeverityLevel.Warm;
            return SeverityLevel.Normal;
        }

        // 5. CPU VCore / VID / DRAM (Tensao de nucleos e memoria)
        if (v >= 1.50f)
            return SeverityLevel.Hot;
        if (v >= 1.42f)
            return SeverityLevel.Warm;

        return SeverityLevel.Normal;
    }

    /// <summary>Returns a 0..1 fill ratio for sensors based on real electrical and scale ranges, or null.</summary>
    public static float? Ratio(ISensor sensor, float? value)
    {
        if (sensor == null || !value.HasValue || float.IsNaN(value.Value))
            return null;

        return Ratio(sensor.SensorType, sensor.Name, value.Value);
    }

    public static float? Ratio(SensorType type, float? value)
    {
        if (!value.HasValue || float.IsNaN(value.Value))
            return null;

        return Ratio(type, string.Empty, value.Value);
    }

    public static float? Ratio(SensorType type, string name, float v)
    {
        name ??= string.Empty;

        float ratio = type switch
        {
            SensorType.Load or SensorType.Level or SensorType.Control or SensorType.Humidity => v / 100f,
            SensorType.Temperature => v / 100f,
            SensorType.Voltage => CalculateVoltageRatio(name, v),
            SensorType.Fan => v / 2500f,
            _ => float.NaN
        };

        if (float.IsNaN(ratio))
            return null;

        return Math.Max(0f, Math.Min(1f, ratio));
    }

    private static float CalculateVoltageRatio(string name, float v)
    {
        // 12V: escala de 10.0 V a 14.0 V (12V fica centrado nos 50%)
        if (name.IndexOf("12V", StringComparison.OrdinalIgnoreCase) >= 0 || v >= 8.0f)
            return (v - 10.0f) / 4.0f;

        // 5V: escala de 4.0 V a 6.0 V (5V fica centrado nos 50%)
        if (name.IndexOf("5V", StringComparison.OrdinalIgnoreCase) >= 0 || (v >= 4.2f && v < 6.0f))
            return (v - 4.0f) / 2.0f;

        // CMOS Battery: escala de saude de 2.0 V a 3.3 V (3.136V fica saudavel a ~87%)
        if (name.IndexOf("CMOS", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("VBAT", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) >= 0)
            return (v - 2.0f) / 1.3f;

        // 3.3V / AVCC / 3VSB: escala de 2.8 V a 3.8 V (3.3V fica centrado nos 50%)
        if (name.IndexOf("3.3V", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("3V", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("AVCC", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Standby", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (v >= 2.6f && v < 3.8f))
            return (v - 2.8f) / 1.0f;

        // CPU VCore / Sub-2V: escala de 0.0 V a 1.6 V
        return v / 1.6f;
    }

    public static Color ColorFor(SeverityLevel level, Theme theme, Color normal)
    {
        return level switch
        {
            SeverityLevel.Warm => theme.StateWarmColor,
            SeverityLevel.Hot => theme.StateHotColor,
            _ => normal
        };
    }
}
