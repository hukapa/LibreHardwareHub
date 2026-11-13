using Windows.Win32;

namespace LibreHardwareMonitor.Interop;

internal static class ByteExtensions
{
    public static byte[] ToArray(this __byte_4 bytes)
    {
        return bytes.AsReadOnlySpan().ToArray();
    }
}
