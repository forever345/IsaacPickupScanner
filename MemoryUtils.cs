using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace IsaacPickupScanner;

internal class MemoryUtils
{
    public static bool IsPickupSlot(byte[] buffer, int offset, HashSet<int> validVariants, int MAX_SUBTYPE)
    {
        if (offset - 16 < 0 || offset + 4 > buffer.Length)
            return false;

        int subType = BitConverter.ToInt32(buffer, offset);
        if (subType < 0 || subType > MAX_SUBTYPE)
            return false;

        int variant = BitConverter.ToInt32(buffer, offset - 4);
        if (!validVariants.Contains(variant))
            return false;

        int entityType = BitConverter.ToInt32(buffer, offset - 8);
        if (entityType != 5)
            return false;

        int flag1 = BitConverter.ToInt32(buffer, offset - 12);
        int flag2 = BitConverter.ToInt32(buffer, offset - 16);
        if (flag1 == 0 || flag2 == 0)
            return false;

        return true;
    }

    public static int ReadInt32(IntPtr processHandle, long address)
    {
        byte[] buffer = new byte[4];
        WinAPI.ReadProcessMemory(
            processHandle,
            new IntPtr(address),
            buffer,
            buffer.Length,
            out _);

        return BitConverter.ToInt32(buffer, 0);
    }
}
