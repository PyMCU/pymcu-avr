// SPDX-License-Identifier: MIT
namespace PyMCU.Backend.Targets.AVR;

internal readonly record struct AvrDevice(int RamStart, int RamSize, bool HasJmpCall)
{
    public int RamEnd => RamStart + RamSize - 1;
}

internal static class AvrDevices
{
    private static readonly Dictionary<string, AvrDevice> Catalog = new()
    {
        ["atmega48"]   = new(0x100, 512,  false),
        ["atmega48p"]  = new(0x100, 512,  false),
        ["atmega88"]   = new(0x100, 1024, false),
        ["atmega88p"]  = new(0x100, 1024, false),
        ["atmega168"]  = new(0x100, 1024, true),
        ["atmega168p"] = new(0x100, 1024, true),
        ["atmega328"]  = new(0x100, 2048, true),
        ["atmega328p"] = new(0x100, 2048, true),
        ["atmega32u4"] = new(0x100, 2560, true),
        ["atmega2560"] = new(0x200, 8192, true),
        ["attiny13"]   = new(0x60,  64,   false),
        ["attiny13a"]  = new(0x60,  64,   false),
        ["attiny24"]   = new(0x60,  128,  false),
        ["attiny25"]   = new(0x60,  128,  false),
        ["attiny2313"] = new(0x60,  128,  false),
        ["attiny44"]   = new(0x60,  256,  false),
        ["attiny45"]   = new(0x60,  256,  false),
        ["attiny4313"] = new(0x60,  256,  false),
        ["attiny84"]   = new(0x60,  512,  false),
        ["attiny85"]   = new(0x60,  512,  false),
    };

    public static IEnumerable<string> Chips => Catalog.Keys;

    public static bool TryGet(string chip, out AvrDevice device)
        => Catalog.TryGetValue(chip.ToLowerInvariant(), out device);
}
