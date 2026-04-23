using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
namespace IsaacPickupScanner;
using static WinAPI;


class Program
{
    const int STRIDE = 0x540;
    const int MAX_SUBTYPE = 10000;
    const int REQUIRED_CONSECUTIVE_SLOTS = 3;
    static readonly HashSet<int> validVariants = new() { 10, 30, 100, 300, 350 };
    static string outputPath = @"X:\Bezplatformowe\The Binding of Isaac Repentance\IsaacPickupScanner.txt";

    static void Main()
    {
        Console.WriteLine("Isaac memory reader - start");

        var processes = Process.GetProcessesByName("isaac-ng");

        if (processes.Length == 0)
        {
            Console.WriteLine("Nie znaleziono procesu isaac-ng.exe");
            return;
        }

        Process isaac = processes[0];
        Console.WriteLine($"Znaleziono proces: PID = {isaac.Id}");

        IntPtr handle = WinAPI.OpenProcess(
            WinAPI.PROCESS_VM_READ | WinAPI.PROCESS_QUERY_INFORMATION,
            false,
            isaac.Id
            );

        if (handle == IntPtr.Zero)
        {
            Console.WriteLine("Nie udało się otworzyć procesu");
            return;
        }

        Console.WriteLine("Proces otwarty poprawnie");

        byte[] test = new byte[4];
        WinAPI.ReadProcessMemory(
            handle,
            isaac.MainModule!.BaseAddress,
            test,
            test.Length,
            out _
            );

        int testValue = BitConverter.ToInt32(test, 0);
        Console.WriteLine($"Testowy odczyt OK: {testValue}");

        var (anchor, regionBase, regionSize) = FindPickupAnchor(handle);

        if (anchor == IntPtr.Zero)
        {
            Console.WriteLine("Pickup anchor NIE znaleziony");
        }
        else
        {
            Console.WriteLine($"Pickup anchor znaleziony: 0x{anchor.ToInt64():X}");
        }

        var monitor = new PickupMonitor(
            handle,
            anchor,
            regionBase,
            regionSize,
            validVariants,
            STRIDE,
            outputPath,
            MAX_SUBTYPE
        );

        monitor.StartMonitoring();

        Console.WriteLine("Naciśnij Enter aby zakończyć");
        Console.ReadLine();
    }

    static (IntPtr anchor, long regionStart, int regionSize) FindPickupAnchor(IntPtr processHandle)
    {
        IntPtr addr = IntPtr.Zero;

        while (true)
        {
            WinAPI.MEMORY_BASIC_INFORMATION mbi;

            int result = WinAPI.VirtualQueryEx(
                processHandle,
                addr,
                out mbi,
                (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))
                );

            if (result == 0)
                break;

            bool isInterestingRegion =
               mbi.State == WinAPI.MEM_COMMIT &&
               mbi.Type == WinAPI.MEM_PRIVATE &&
               (
                   (mbi.Protect & WinAPI.PAGE_READWRITE) != 0 ||
                   (mbi.Protect & WinAPI.PAGE_WRITECOPY) != 0 ||
                   (mbi.Protect & WinAPI.PAGE_READONLY) != 0 ||
                   (mbi.Protect & WinAPI.PAGE_EXECUTE_READ) != 0 ||
                   (mbi.Protect & WinAPI.PAGE_EXECUTE_READWRITE) != 0
               );

            if (isInterestingRegion)
            {
                long regionStart = mbi.BaseAddress.ToInt64();
                int regionSize = (int)mbi.RegionSize.ToInt64();

                byte[] buffer = new byte[regionSize];

                if (!WinAPI.ReadProcessMemory(
                    processHandle,
                    mbi.BaseAddress,
                    buffer,
                    buffer.Length,
                    out int bytesRead) || bytesRead == 0)
                {
                    addr = new IntPtr(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
                    continue;
                }

                for (int offset = 16; offset + (REQUIRED_CONSECUTIVE_SLOTS * STRIDE) < bytesRead; offset += 4)
                {
                    if (HasConsecutivePickupSlots(buffer, offset, REQUIRED_CONSECUTIVE_SLOTS))
                    {
                        long realAddress = regionStart + offset;
                        Console.WriteLine($"Stable pickup anchor found at 0x{realAddress:X}");
                        return (new IntPtr(realAddress), regionStart, regionSize);
                    }
                }
            }

            addr = new IntPtr(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
        }

        Console.WriteLine($"proper anchor not found!");
        return (IntPtr.Zero, 0, 0);
    }

    static bool HasConsecutivePickupSlots(byte[] buffer, int startOffset, int requiredCount)
    {
        for (int i = 0; i < requiredCount; i++)
        {
            int offset = startOffset + i * STRIDE;

            if (!MemoryUtils.IsPickupSlot(buffer, offset, validVariants, MAX_SUBTYPE))
                return false;
        }

        return true;
    }

}

static class WinAPI
{
    public const int PROCESS_VM_READ = 0x0010;
    public const int PROCESS_QUERY_INFORMATION = 0x0400;

    public const uint MEM_COMMIT = 0x00001000;
    public const uint MEM_PRIVATE = 0x00020000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_WRITECOPY = 0x08;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_READONLY = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    public static extern int VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        uint dwLength
        );

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(
        int dwDesiredAccess,
        bool bInheritHandle,
        int dwProcessId
        );

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead
        ); 
}