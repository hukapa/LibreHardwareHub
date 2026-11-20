// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using DiskInfoToolkit.Devices;
using DiskInfoToolkit.Smart;
using StorageDeviceDIT = DiskInfoToolkit.Devices.StorageDevice;

namespace LibreHardwareMonitor.Windows.Forms.UI.DiskHealth;

/// <summary>Overall verdict shown on a drive card.</summary>
public enum DiskHealthLevel
{
    Unknown,
    Good,
    Caution,
    Bad
}

/// <summary>Severity of a single finding. <see cref="Info" /> explains something without changing the verdict.</summary>
public enum DiskFindingSeverity
{
    Info,
    Caution,
    Bad
}

public readonly record struct DiskFinding(DiskFindingSeverity Severity, string Text);

/// <summary>Plain snapshot of one drive, built on the UI thread from the data the background poller already read.</summary>
public sealed class DiskHealthReport
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DiskHealthLevel Level { get; init; }
    public IReadOnlyList<DiskFinding> Findings { get; init; } = Array.Empty<DiskFinding>();
    public int? LifePercent { get; init; }
    public ulong? PowerOnHours { get; init; }
    public ulong? PowerOnCount { get; init; }
    public ulong? HostWritesBytes { get; init; }
    public ulong? HostReadsBytes { get; init; }

    /// <summary>True when <see cref="HostWritesBytes" /> is extrapolated by the app, not a counter kept by the drive.</summary>
    public bool WritesEstimated { get; init; }
    public int? TemperatureC { get; init; }
    public bool IsAsleep { get; init; }

    /// <summary>Spinning disk: there is no wear percentage, health comes from the sector counters.</summary>
    public bool IsHdd { get; init; }

    /// <summary>Windows disk number ("Disco 0"), used for ordering like Disk Management.</summary>
    public uint? DiskNumber { get; init; }

    /// <summary>The raw S.M.A.R.T. values behind the verdict; null when the drive exposes none.</summary>
    public DiskSmartTable Smart { get; init; }
}

/// <summary>
/// Turns S.M.A.R.T. / NVMe health data into a verdict with reasons in plain language.
/// The drive's own summarized status (DiskInfoToolkit, same method as CrystalDiskInfo) is the baseline;
/// the rules here only escalate it and explain why. Nothing here touches the device.
/// </summary>
public static class DiskHealthEvaluator
{
    // ATA attribute IDs (vendor independent meaning for these ones).
    private const byte AtaSpinUpTime = 0x03;
    private const byte AtaReallocatedSectors = 0x05;
    private const byte AtaSpinRetry = 0x0A;
    private const byte AtaReportedUncorrectable = 0xBB;
    private const byte AtaPendingSectors = 0xC5;
    private const byte AtaOfflineUncorrectable = 0xC6;
    private const byte AtaUdmaCrcErrors = 0xC7;
    private const byte AtaTotalLbasWritten = 0xF1;
    private const byte AtaTotalLbasRead = 0xF2;
    private const ulong AtaSectorBytes = 512;

    // NVMe SMART / Health Information log page (0x02) offsets.
    private const int NvmeCriticalWarning = 0;
    private const int NvmeAvailableSpare = 3;
    private const int NvmeSpareThreshold = 4;
    private const int NvmePercentageUsed = 5;
    private const int NvmeMediaErrors = 160;
    private const int NvmeLogMinLength = 176;

    /// <param name="disk">Drive data read by the background poller.</param>
    /// <param name="bytesWrittenSinceBoot">Windows' write total for this disk since boot, used to estimate lifetime writes when the drive keeps none.</param>
    /// <param name="uptime">Time since Windows started.</param>
    public static DiskHealthReport Evaluate(StorageDeviceDIT disk, long bytesWrittenSinceBoot = 0, TimeSpan uptime = default)
    {
        List<DiskFinding> findings = new();
        DiskHealthLevel level = FromToolkit(disk.HealthStatus, out DiskFinding? toolkitFinding);
        SmartAttributeEntry[] attributes = Snapshot(disk.SmartAttributes);
        bool isNvme = disk.SmartAttributeProfile == SmartAttributeProfile.NVMe || disk.TransportKind == StorageTransportKind.Nvme;
        bool asleep = disk.IsDevicePowerOn == false;
        bool isHdd = !isNvme && IsSpinningDisk(attributes);

        if (isNvme)
            EvaluateNvme(disk.Nvme?.SmartLogData, findings);
        else
            EvaluateAta(attributes, findings);

        int? lifePercent = isHdd ? HddHealthScore(attributes) : disk.Health;

        // NVMe: read "Percentage Used" straight from the health log, so the figure is exactly what the drive says.
        byte[] nvmeLog = isNvme ? disk.Nvme?.SmartLogData : null;
        if (nvmeLog is { Length: > NvmePercentageUsed })
            lifePercent = 100 - Math.Min(100, (int)nvmeLog[NvmePercentageUsed]);
        if (lifePercent is int life && !isHdd)
        {
            if (life <= 10)
                findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"Vida restante baixa ({life}%). Faça cópia dos dados e planeie a substituição."));

            AddWearEstimate(life, disk.PowerOnHours, findings);
        }

        if (disk.Temperature is int temp && temp > 0)
        {
            if (disk.TemperatureCritical is int critical && critical > 0 && temp >= critical)
                findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"Temperatura de {temp} °C atingiu o limite crítico do fabricante ({critical} °C)."));
            else if (disk.TemperatureWarning is int warning && warning > 0 && temp >= warning)
                findings.Add(new DiskFinding(DiskFindingSeverity.Info, $"Temperatura de {temp} °C acima do aviso do fabricante ({warning} °C). Verifique a ventilação."));
        }

        bool hasData = disk.HealthStatus.HasValue || attributes.Length > 0 || lifePercent.HasValue;
        if (!hasData)
        {
            level = DiskHealthLevel.Unknown;
            findings.Clear();
            findings.Add(new DiskFinding(DiskFindingSeverity.Info, disk.IsUsbConnected
                ? "A caixa ou adaptador USB não deixa ler os dados SMART deste disco."
                : "O disco não expõe dados SMART (controlador RAID, disco virtual ou modo não suportado)."));
        }
        else
        {
            foreach (DiskFinding f in findings)
                level = Escalate(level, f.Severity);

            // The drive's own verdict is only spelled out when no rule above already explains it.
            if (toolkitFinding is DiskFinding generic && !findings.Exists(f => f.Severity >= generic.Severity))
                findings.Insert(0, generic);
        }

        ulong? written = disk.HostWrites ?? (isHdd ? LbaCounterBytes(attributes, AtaTotalLbasWritten) : null);
        bool writesEstimated = false;
        if (!written.HasValue && hasData && EstimateWrites(bytesWrittenSinceBoot, uptime, disk.PowerOnHours) is ulong estimate)
        {
            written = estimate;
            writesEstimated = true;
            findings.Add(new DiskFinding(DiskFindingSeverity.Info,
                $"Dados escritos estimados: o disco não guarda este total. Média desde o arranque do Windows ({DiskHealthFormat.Data((ulong)bytesWrittenSinceBoot)} em {DiskHealthFormat.Hours((ulong)uptime.TotalHours)}) × tempo ligado. Pode variar muito."));
        }

        if (asleep)
            findings.Add(new DiskFinding(DiskFindingSeverity.Info, "Disco em repouso: valores da última leitura (não é acordado para ler o SMART)."));

        return new DiskHealthReport
        {
            Name = string.IsNullOrWhiteSpace(disk.ProductName) ? "Disco" : disk.ProductName.Trim(),
            Description = Describe(disk, isHdd),
            Level = level,
            Findings = findings,
            LifePercent = lifePercent,
            IsHdd = isHdd,
            DiskNumber = disk.StorageDeviceNumber,
            PowerOnHours = disk.PowerOnHours,
            PowerOnCount = disk.PowerOnCount,
            HostWritesBytes = written,
            WritesEstimated = writesEstimated,
            HostReadsBytes = disk.HostReads ?? (isHdd ? LbaCounterBytes(attributes, AtaTotalLbasRead) : null),
            TemperatureC = disk.Temperature is > 0 ? disk.Temperature : null,
            IsAsleep = asleep,
            Smart = (isNvme ? DiskSmartTableBuilder.FromNvme(nvmeLog) : null) ?? DiskSmartTableBuilder.FromAta(attributes, AttributeName)
        };
    }

    private static DiskHealthLevel FromToolkit(StorageHealthStatus? status, out DiskFinding? finding)
    {
        finding = null;
        switch (status)
        {
            case StorageHealthStatus.Caution:
                finding = new DiskFinding(DiskFindingSeverity.Caution, "O próprio disco reporta um estado a vigiar.");
                return DiskHealthLevel.Caution;
            case StorageHealthStatus.Warning:
                finding = new DiskFinding(DiskFindingSeverity.Bad, "O próprio disco reporta um estado crítico, perto de falhar.");
                return DiskHealthLevel.Bad;
            case StorageHealthStatus.Bad:
                finding = new DiskFinding(DiskFindingSeverity.Bad, "O próprio disco reporta falha ou falha iminente.");
                return DiskHealthLevel.Bad;
            default:
                return DiskHealthLevel.Good;
        }
    }

    private static void EvaluateAta(SmartAttributeEntry[] attributes, List<DiskFinding> findings)
    {
        foreach (SmartAttributeEntry a in attributes)
        {
            // Low 32 bits: some vendors pack extra counters in the upper bytes of the raw value.
            ulong raw = a.RawValue & 0xFFFFFFFF;

            switch (a.ID)
            {
                case AtaReallocatedSectors when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{raw} setores realocados (zonas danificadas já substituídas)."));
                    break;
                case AtaPendingSectors when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{raw} setores pendentes (ilegíveis, à espera de realocação)."));
                    break;
                case AtaOfflineUncorrectable when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{raw} setores incorrigíveis."));
                    break;
                case AtaReportedUncorrectable when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{raw} erros de leitura incorrigíveis reportados."));
                    break;
                case AtaSpinRetry when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{raw} tentativas repetidas de arranque do motor."));
                    break;
                case AtaUdmaCrcErrors when raw > 0:
                    findings.Add(new DiskFinding(DiskFindingSeverity.Info, $"{raw} erros de transferência (CRC). Normalmente é o cabo SATA ou a ligação, não o disco."));
                    break;
            }

            // Normalized value at or below the vendor threshold is the classic S.M.A.R.T. failure condition.
            if (a.ThresholdValue > 0 && a.CurrentValue > 0 && a.CurrentValue <= a.ThresholdValue)
                findings.Add(new DiskFinding(DiskFindingSeverity.Bad, $"\"{AttributeName(a)}\" está abaixo do limite do fabricante ({a.CurrentValue} de {a.ThresholdValue})."));
        }
    }

    private static void EvaluateNvme(byte[] log, List<DiskFinding> findings)
    {
        if (log == null || log.Length < NvmeLogMinLength)
            return;

        byte warning = log[NvmeCriticalWarning];
        if ((warning & 0x01) != 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Bad, "A reserva de células está abaixo do limite."));
        if ((warning & 0x02) != 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Caution, "O disco assinalou temperatura fora dos limites."));
        if ((warning & 0x04) != 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Bad, "O disco assinalou fiabilidade degradada por erros de memória."));
        if ((warning & 0x08) != 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Bad, "O disco passou a modo só de leitura."));
        if ((warning & 0x10) != 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Caution, "Falhou a proteção de energia da memória volátil."));

        byte spare = log[NvmeAvailableSpare];
        byte spareThreshold = log[NvmeSpareThreshold];
        if (spareThreshold > 0 && spare <= spareThreshold && (warning & 0x01) == 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Bad, $"Reserva disponível de {spare}% no limite ({spareThreshold}%)."));

        ulong mediaErrors = BitConverter.ToUInt64(log, NvmeMediaErrors);
        if (mediaErrors > 0)
            findings.Add(new DiskFinding(DiskFindingSeverity.Caution, $"{mediaErrors} erros de integridade de dados registados."));
    }

    /// <summary>
    /// Explains the life figure: it is the wear the drive itself reports (NVMe "Percentage Used" or the SSD wear
    /// attribute against the rated write endurance), and at the pace so far, how much powered-on time is left.
    /// </summary>
    private static void AddWearEstimate(int life, ulong? powerOnHours, List<DiskFinding> findings)
    {
        int used = 100 - Math.Clamp(life, 0, 100);
        string text = $"Desgaste de {used}% reportado pelo próprio disco (escritas feitas face à resistência prevista pelo fabricante).";

        // Needs a month of use and some wear before the pace means anything.
        if (powerOnHours is ulong hours && hours >= 24 * 30 && used >= 2 && life > 0)
        {
            ulong remaining = (ulong)(hours / (double)used * life);
            text += $" Ao ritmo atual ({used}% em {DiskHealthFormat.Hours(hours)} ligado) restam cerca de {DiskHealthFormat.Hours(remaining)} de uso.";
        }

        findings.Add(new DiskFinding(DiskFindingSeverity.Info, text));
    }

    /// <summary>
    /// Estimated health of a hard disk, 0-100. Hard disks report no wear figure, so this condenses the counters
    /// that predict failure: every damaged or unreadable sector and every attribute past its vendor threshold
    /// takes points off, with a cap per counter so one noisy attribute cannot zero the score on its own.
    /// CRC errors are left out on purpose (cable, not disk). It is an estimate in the spirit of tools such as
    /// Hard Disk Sentinel, not their exact formula.
    /// </summary>
    private static int HddHealthScore(SmartAttributeEntry[] attributes)
    {
        double penalty = 0;
        int failing = 0;
        foreach (SmartAttributeEntry a in attributes)
        {
            ulong raw = a.RawValue & 0xFFFFFFFF;
            penalty += a.ID switch
            {
                AtaReallocatedSectors => Math.Min(40, raw * 0.5),
                AtaPendingSectors => Math.Min(40, raw * 2.0),
                AtaOfflineUncorrectable => Math.Min(40, raw * 2.0),
                AtaReportedUncorrectable => Math.Min(30, raw * 0.1),
                AtaSpinRetry => Math.Min(20, raw * 5.0),
                _ => 0
            };

            if (a.ThresholdValue > 0 && a.CurrentValue > 0 && a.CurrentValue <= a.ThresholdValue)
                failing++;
        }

        double score = Math.Clamp(100 - penalty, penalty > 0 ? 1 : 0, 100);

        // An attribute past its vendor threshold already makes the verdict "Mau"; the figure must not look fine.
        if (failing > 0)
            score = Math.Min(score, 50);

        return (int)Math.Round(score);
    }

    /// <summary>
    /// Rough lifetime writes for drives that keep no counter: the write pace since Windows started, applied to
    /// the drive's whole powered-on time. Needs a couple of hours of uptime so one burst does not dominate.
    /// </summary>
    private static ulong? EstimateWrites(long bytesSinceBoot, TimeSpan uptime, ulong? powerOnHours)
    {
        if (bytesSinceBoot <= 0 || uptime.TotalHours < 2 || powerOnHours is not ulong hours || hours == 0)
            return null;

        double perHour = bytesSinceBoot / uptime.TotalHours;
        return (ulong)(perHour * hours);
    }

    /// <summary>
    /// Lifetime totals of hard disks that keep them (Seagate and some others): F1/F2 count 512-byte sectors.
    /// Only used for hard disks; SSD vendors give these IDs other units and the toolkit already decodes them.
    /// </summary>
    private static ulong? LbaCounterBytes(SmartAttributeEntry[] attributes, byte id)
    {
        foreach (SmartAttributeEntry a in attributes)
        {
            if (a.ID != id)
                continue;

            // 48-bit raw value; zero means the counter is not maintained.
            ulong lbas = a.RawValue & 0xFFFFFFFFFFFF;
            return lbas > 0 ? lbas * AtaSectorBytes : null;
        }

        return null;
    }

    /// <summary>
    /// ATA drives do not say whether they spin. Spin-up time and spin retry counters only exist on hard disks;
    /// SSD profiles reuse neither ID.
    /// </summary>
    private static bool IsSpinningDisk(SmartAttributeEntry[] attributes)
    {
        bool spinUp = false;
        bool spinRetry = false;
        foreach (SmartAttributeEntry a in attributes)
        {
            spinUp |= a.ID == AtaSpinUpTime;
            spinRetry |= a.ID == AtaSpinRetry;
        }

        return spinUp && spinRetry;
    }

    private static DiskHealthLevel Escalate(DiskHealthLevel level, DiskFindingSeverity severity) => severity switch
    {
        DiskFindingSeverity.Bad => DiskHealthLevel.Bad,
        DiskFindingSeverity.Caution when level == DiskHealthLevel.Good => DiskHealthLevel.Caution,
        _ => level
    };

    private static string Describe(StorageDeviceDIT disk, bool isHdd)
    {
        List<string> parts = new();
        if (disk.StorageDeviceNumber is uint number)
            parts.Add($"Disco {number}");
        parts.Add(TransportName(disk, isHdd));
        if (disk.DiskSizeBytes is ulong size && size > 0)
            parts.Add(DiskHealthFormat.Capacity(size));
        parts.AddRange(Volumes(disk));
        return string.Join(" · ", parts);
    }

    /// <summary>Drive letters with their volume labels, e.g. "G: Games".</summary>
    private static IEnumerable<string> Volumes(StorageDeviceDIT disk)
    {
        DiskInfoToolkit.Partitions.StoragePartitionInfo[] partitions;
        try
        {
            partitions = disk.Partitions?.ToArray() ?? Array.Empty<DiskInfoToolkit.Partitions.StoragePartitionInfo>();
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var partition in partitions)
        {
            if (partition.DriveLetter is not char letter)
                continue;

            string label = null;
            try
            {
                label = new System.IO.DriveInfo($"{letter}:\\").VolumeLabel;
            }
            catch (Exception)
            {
                // Locked (BitLocker) or vanished volumes just show the letter.
            }

            yield return string.IsNullOrWhiteSpace(label) ? $"{letter}:" : $"{letter}: {label.Trim()}";
        }
    }

    private static string TransportName(StorageDeviceDIT disk, bool isHdd)
    {
        if (disk.IsUsbConnected)
            return isHdd ? "USB · HDD" : "USB";

        return disk.TransportKind switch
        {
            StorageTransportKind.Nvme => "NVMe SSD",
            StorageTransportKind.Ata or StorageTransportKind.Ahci => isHdd ? "SATA HDD" : disk.Health.HasValue ? "SATA SSD" : "SATA",
            StorageTransportKind.Sas => "SAS",
            StorageTransportKind.Scsi => "SCSI",
            StorageTransportKind.Raid => "RAID",
            StorageTransportKind.Sd or StorageTransportKind.Mmc => "Cartão SD/MMC",
            StorageTransportKind.Virtual => "Virtual",
            _ => "Disco"
        };
    }

    private static string AttributeName(SmartAttributeEntry a)
    {
        string name = null;
        try
        {
            name = a.GetDisplayName();
        }
        catch
        {
            // Display names come from localized resources; fall back to the raw name.
        }

        name = string.IsNullOrWhiteSpace(name) ? a.Name : name;
        return string.IsNullOrWhiteSpace(name) ? $"Atributo {a.ID:X2}" : name;
    }

    // The poller replaces entries on a background thread; copy defensively.
    private static SmartAttributeEntry[] Snapshot(List<SmartAttributeEntry> list)
    {
        if (list == null)
            return Array.Empty<SmartAttributeEntry>();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return list.ToArray();
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return Array.Empty<SmartAttributeEntry>();
    }
}

/// <summary>Number formatting shared by the evaluator and the cards.</summary>
public static class DiskHealthFormat
{
    public static string Capacity(ulong bytes)
    {
        double gb = bytes / 1e9;
        return gb >= 1000 ? $"{gb / 1000:0.#} TB" : $"{gb:0} GB";
    }

    public static string Data(ulong bytes)
    {
        double gb = bytes / 1e9;
        if (gb >= 1000)
            return $"{gb / 1000:0.0} TB";
        return $"{gb:0} GB";
    }

    public static string Hours(ulong hours)
    {
        if (hours < 48)
            return $"{hours} h";

        double days = hours / 24d;
        return days >= 365 ? $"{days / 365.25:0.0} anos" : $"{days:0} dias";
    }
}
