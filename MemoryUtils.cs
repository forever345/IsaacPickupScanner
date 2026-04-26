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
        return AnalyzePickupSlot(buffer, offset, validVariants, MAX_SUBTYPE).IsValid;
    }

    public static PickupSlotResult AnalyzePickupSlot(byte[] buffer, int offset, HashSet<int> validVariants, int MAX_SUBTYPE)
    {
        if (offset - 16 < 0 || offset + 4 > buffer.Length)
            return new PickupSlotResult { IsValid = false, Reason = "Out of bounds" };

        int subType = BitConverter.ToInt32(buffer, offset);
        int variant = BitConverter.ToInt32(buffer, offset - 4);
        int entityType = BitConverter.ToInt32(buffer, offset - 8);
        int flag1 = BitConverter.ToInt32(buffer, offset - 12);
        int flag2 = BitConverter.ToInt32(buffer, offset - 16);

        if (subType < 0 || subType > MAX_SUBTYPE)
            return new PickupSlotResult { IsValid = false, Reason = "Invalid subTYpe", SubType = subType, Variant = variant, EntityType = entityType, Flag1 = flag1, Flag2 = flag2 };

        if (!validVariants.Contains(variant))
            return new PickupSlotResult { IsValid = false, Reason = "Invalid variant", SubType = subType, Variant = variant, EntityType = entityType, Flag1 = flag1, Flag2 = flag2 };

        if (entityType != 5)
            return new PickupSlotResult { IsValid = false, Reason = "Invalid entityType", SubType = subType, Variant = variant, EntityType = entityType, Flag1 = flag1, Flag2 = flag2 };

        if (flag1 < 0 || flag2 < 0 || flag1 > MAX_SUBTYPE || flag2 > MAX_SUBTYPE)
            return new PickupSlotResult{IsValid = false, Reason = "Invalid flags", SubType = subType, Variant = variant, EntityType = entityType, Flag1 = flag1, Flag2 = flag2};

        return new PickupSlotResult
        {
            IsValid = true,
            Reason = "OK",
            SubType = subType,
            Variant = variant,
            EntityType = entityType,
            Flag1 = flag1,
            Flag2 = flag2
        };
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

public class PickupSlotResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; }

    public int SubType { get; set; }
    public int Variant { get; set; }
    public int EntityType { get; set; }
    public int Flag1 { get; set; }
    public int Flag2 { get; set; }
}
