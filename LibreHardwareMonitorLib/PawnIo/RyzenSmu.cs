using System;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.PawnIo;

public class RyzenSmu
{
    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelMsr).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIo.RyzenSMU.bin");

    public uint GetSmuVersion()
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        uint version;

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_get_smu_version", [], 1);
            version = (uint)outArray[0];
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }

        return version;
    }

    public long GetCodeName()
    {
        long[] outArray = _pawnIO.Execute("ioctl_get_code_name", [], 1);
        return outArray[0];
    }

    public long[] ReadPmTable(int size)
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_pm_table", [], size);
            return outArray;
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    public void UpdatePmTable()
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            _pawnIO.Execute("ioctl_update_pm_table", [], 0);
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    public void ResolvePmTable(out uint version, out uint tableBase)
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_resolve_pm_table", [], 2);
            version = (uint)outArray[0];
            tableBase = (uint)outArray[1];
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    public bool SendSmuCommand(uint command, params uint[] args)
    {
        if (!Mutexes.WaitPciBus(5000))
            return false;

        try
        {
            long[] inArray = new long[1 + (args?.Length ?? 0)];
            inArray[0] = command;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                    inArray[1 + i] = args[i];
            }

            _pawnIO.Execute("ioctl_send_smu_command", inArray, 0);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    public bool IsLoaded => _pawnIO.IsLoaded;

    public void Close() => _pawnIO.Close();
}
