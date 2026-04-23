namespace IsaacPickupScanner;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using static WinAPI;

internal class PickupMonitor
{
    private readonly IntPtr _processHandle;
    private readonly long _anchor;
    private readonly long _regionStart;
    private readonly int _regionSize;
    private readonly int _stride;
    private readonly string _outputPath;
    private readonly HashSet<int> _validVariants;
    private readonly int _maxSubtype;
    private readonly ItemDatabase _database;

    private readonly HashSet<int> _previousActive = new();

    public PickupMonitor(
        IntPtr processHandle,
        IntPtr anchor,
        long regionStart,
        int regionSize,
        HashSet<int> validVariants,
        int stride,
        string outputPath,
        int maxSubtype,
        ItemDatabase database)
    {
        _processHandle = processHandle;
        _anchor = anchor.ToInt64();
        _regionStart = regionStart;
        _regionSize = regionSize;
        _validVariants = validVariants;
        _stride = stride;
        _outputPath = outputPath;
        _maxSubtype = maxSubtype;
        _database = database;
    }

    public void StartMonitoring(int intervalMs = 300)
    {
        Console.WriteLine("Pickup monitoring started...");

        while (true)
        {
            var currentActive = ScanActiveItems();

            if (!currentActive.SetEquals(_previousActive))
            {
                SaveItems(currentActive);

                _previousActive.Clear();
                foreach (var item in currentActive)
                    _previousActive.Add(item);

                Console.WriteLine($"Updated items: {string.Join(", ", currentActive)}");
            }

            Thread.Sleep(intervalMs);
        }
    }

    private HashSet<int> ScanActiveItems()
    {
        var result = new HashSet<int>();

        byte[] buffer = new byte[_regionSize];

        if (!WinAPI.ReadProcessMemory(
            _processHandle,
            new IntPtr(_regionStart),
            buffer,
            buffer.Length,
            out int bytesRead) || bytesRead == 0)
        {
            return result;
        }

        int baseOffset = (int)(_anchor - _regionStart);

        for (int i = 0; ; i++)
        {
            int offset = baseOffset + i * _stride;

            if (offset - 16 < 0 || offset + 4 > bytesRead)
                break;

            if (!MemoryUtils.IsPickupSlot(buffer, offset, _validVariants, _maxSubtype))
                break;

            int subType = BitConverter.ToInt32(buffer, offset);
            int type = BitConverter.ToInt32(buffer, offset - 4);

            if (type == 100 && subType > 0)
            {
                result.Add(subType);
            }
        }

        return result;
    }

    private string FormatItem(Item item)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ID: {item.Id}");
        sb.AppendLine($"Name: {item.Name}");

        if (!string.IsNullOrWhiteSpace(item.Pickup))
            sb.AppendLine($"Pickup: {item.Pickup}");

        sb.AppendLine($"Quality: {item.Quality}");

        sb.AppendLine();

        if (item.Description != null && item.Description.Count > 0)
        {
            sb.AppendLine("Description:");

            foreach (var line in item.Description)
            {
                sb.AppendLine($"- {line}");
            }

            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(item.Type))
            sb.AppendLine($"Type: {item.Type}");

        if (!string.IsNullOrWhiteSpace(item.Pools))
            sb.AppendLine($"Pools: {item.Pools}");

        if (!string.IsNullOrWhiteSpace(item.Tags))
            sb.AppendLine($"Tags: {item.Tags}");

        sb.AppendLine("================================");

        return sb.ToString();
    }

    private void SaveItems(HashSet<int> itemIds)
    {
        var sb = new StringBuilder();

        foreach (var id in itemIds)
        {
            var items = _database.GetItems(id);

            if (items != null)
            {
                sb.AppendLine($"=== Item ID: {id} ===");
                foreach (var item in items)
                {
                    sb.AppendLine(FormatItem(item));
                }
            }
        }

        File.WriteAllText(_outputPath, sb.ToString());
    }
}