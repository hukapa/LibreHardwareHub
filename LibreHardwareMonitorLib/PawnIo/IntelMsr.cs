using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.PawnIo;

public class IntelMsr
{
    private readonly long[] _inArray = new long[1];
    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelMsr).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIo.IntelMSR.bin");

    public bool ReadMsr(uint index, out ulong value)
    {
        _inArray[0] = index;
        value = 0;
        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_msr", _inArray, 1);
            value = (ulong)outArray[0];
        }
        catch
        {
            return false;
        }

        return true;
    }

    public bool ReadMsr(uint index, out uint eax, out uint edx)
    {
        _inArray[0] = index;
        eax = 0;
        edx = 0;
        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_msr", _inArray, 1);
            eax = (uint)outArray[0];
            edx = (uint)(outArray[0] >> 32);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public bool ReadMsr(uint index, out uint eax, out uint edx, GroupAffinity affinity)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool result = ReadMsr(index, out eax, out edx);
        ThreadAffinity.Set(previousAffinity);
        return result;
    }

    public bool WriteMsr(uint index, ulong value)
    {
        try
        {
            long[] inArray = [(long)index, (long)value];
            _pawnIO.Execute("ioctl_write_msr", inArray, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool WriteMsr(uint index, uint eax, uint edx)
    {
        ulong value = ((ulong)edx << 32) | eax;
        return WriteMsr(index, value);
    }

    public bool WriteMsr(uint index, ulong value, GroupAffinity affinity)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool result = WriteMsr(index, value);
        ThreadAffinity.Set(previousAffinity);
        return result;
    }

    public bool IsLoaded => _pawnIO.IsLoaded;

    public void Close() => _pawnIO.Close();
}
