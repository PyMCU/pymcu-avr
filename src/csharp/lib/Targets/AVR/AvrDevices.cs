// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PyMCU.Backend.Targets.AVR;

internal readonly record struct AvrDevice(int RamStart, int RamSize, int FlashSize, bool HasJmpCall)
{
    public int RamEnd => RamStart + RamSize - 1;
}

/// <summary>One row of the catalog as published by <c>pymcuc-avr devices</c>.</summary>
public record DeviceEntry(string Chip, int RamStart, int RamSize, int RamEnd, int FlashSize, bool HasJmpCall);

[JsonSerializable(typeof(List<DeviceEntry>))]
internal partial class AvrDevicesJsonContext : JsonSerializerContext { }

public static class AvrDevices
{
    private static readonly Dictionary<string, AvrDevice> Catalog = new()
    {
        ["atmega48"]   = new(0x100, 512, 4096, false),
        ["atmega48p"]  = new(0x100, 512, 4096, false),
        ["atmega88"]   = new(0x100, 1024, 8192, false),
        ["atmega88p"]  = new(0x100, 1024, 8192, false),
        ["atmega168"]  = new(0x100, 1024, 16384, true),
        ["atmega168p"] = new(0x100, 1024, 16384, true),
        ["atmega328"]  = new(0x100, 2048, 32768, true),
        ["atmega328p"] = new(0x100, 2048, 32768, true),
        ["atmega32u4"] = new(0x100, 2560, 32768, true),
        ["atmega2560"] = new(0x200, 8192, 262144, true),
        ["attiny13"]   = new(0x60, 64, 1024, false),
        ["attiny13a"]  = new(0x60, 64, 1024, false),
        ["attiny24"]   = new(0x60, 128, 2048, false),
        ["attiny25"]   = new(0x60, 128, 2048, false),
        ["attiny2313"] = new(0x60, 128, 2048, false),
        ["attiny44"]   = new(0x60, 256, 4096, false),
        ["attiny45"]   = new(0x60, 256, 4096, false),
        ["attiny4313"] = new(0x60, 256, 4096, false),
        ["attiny84"]   = new(0x60, 512, 8192, false),
        ["attiny85"]   = new(0x60, 512, 8192, false),
    };

    internal static IEnumerable<string> Chips => Catalog.Keys;

    internal static bool TryGet(string chip, out AvrDevice device)
        => Catalog.TryGetValue(chip.ToLowerInvariant(), out device);

    // Read back through TryGet, the same accessor the codegen calls, so the published
    // catalog cannot drift from the one the compiler consults.
    public static string ToJson()
    {
        var entries = new List<DeviceEntry>();
        foreach (var chip in Chips.OrderBy(c => c, StringComparer.Ordinal))
        {
            if (!TryGet(chip, out var d)) continue;
            entries.Add(new DeviceEntry(chip, d.RamStart, d.RamSize, d.RamEnd, d.FlashSize, d.HasJmpCall));
        }
        return JsonSerializer.Serialize(entries, AvrDevicesJsonContext.Default.ListDeviceEntry);
    }
}
