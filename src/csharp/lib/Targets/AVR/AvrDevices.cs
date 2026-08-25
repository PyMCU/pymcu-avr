// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PyMCU.Backend.Targets.AVR;

/// <summary>
/// What the backend knows about a part that <c>device_info()</c> does NOT declare.
///
/// The SRAM and flash SIZES used to live here too. They were a workaround: the chip
/// files declared them, the frontend parsed them, and nothing carried them to a
/// backend, so <c>cfg.FlashSize</c> was permanently 0 and LargeFlash picked LPM over
/// ELPM from a hardcoded list of chip names. They now travel in the .mir as
/// <c>ProgramIR.Device</c> and are read from there; keeping a copy here would put the
/// ATmega2560's flash size in two places again.
///
/// RamStart and HasJmpCall stay because they are core layout, not geometry: no
/// device_info() argument declares either one.
/// </summary>
internal readonly record struct AvrDevice(int RamStart, bool HasJmpCall);

/// <summary>One row of the catalog as published by <c>pymcuc-avr devices</c>.</summary>
public record DeviceEntry(string Chip, int RamStart, bool HasJmpCall);

[JsonSerializable(typeof(List<DeviceEntry>))]
internal partial class AvrDevicesJsonContext : JsonSerializerContext { }

public static class AvrDevices
{
    private static readonly Dictionary<string, AvrDevice> Catalog = new()
    {
        ["atmega48"]   = new(0x100, false),
        ["atmega48p"]  = new(0x100, false),
        ["atmega88"]   = new(0x100, false),
        ["atmega88p"]  = new(0x100, false),
        ["atmega168"]  = new(0x100, true),
        ["atmega168p"] = new(0x100, true),
        ["atmega328"]  = new(0x100, true),
        ["atmega328p"] = new(0x100, true),
        ["atmega32u4"] = new(0x100, true),
        ["atmega2560"] = new(0x200, true),
        ["attiny13"]   = new(0x60, false),
        ["attiny13a"]  = new(0x60, false),
        ["attiny24"]   = new(0x60, false),
        ["attiny25"]   = new(0x60, false),
        ["attiny2313"] = new(0x60, false),
        ["attiny44"]   = new(0x60, false),
        ["attiny45"]   = new(0x60, false),
        ["attiny4313"] = new(0x60, false),
        ["attiny84"]   = new(0x60, false),
        ["attiny85"]   = new(0x60, false),
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
            entries.Add(new DeviceEntry(chip, d.RamStart, d.HasJmpCall));
        }
        return JsonSerializer.Serialize(entries, AvrDevicesJsonContext.Default.ListDeviceEntry);
    }
}
