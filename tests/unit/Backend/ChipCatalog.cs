using System.Text.RegularExpressions;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// The chip files in the PyMCU stdlib, read the way a real build reads them.
///
/// A real build gets the geometry in the .mir, put there by the frontend from the
/// device_info() call in lib/src/pymcu/chips/&lt;chip&gt;.py. Unit tests hand-build a
/// ProgramIR and never run the frontend, so they need the same numbers from the same
/// place. Reading the chip files, rather than keeping a table here, is the point: a
/// table here would be one more copy of exactly the data this change removed from the
/// backend.
/// </summary>
public static class ChipCatalog
{
    public static string Dir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "hatch_build.py")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var chips = Path.Combine(Path.GetDirectoryName(dir)!, "pymcu",
                                 "lib", "src", "pymcu", "chips");
        Assert.True(Directory.Exists(chips), $"chip catalog not found at {chips}");
        return chips;
    }

    private static int? Constant(string text, string name)
    {
        var m = Regex.Match(text, $@"^{name}\s*=\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.Multiline);
        if (!m.Success) return null;
        var raw = m.Groups[1].Value;
        return Convert.ToInt32(raw, raw.StartsWith("0x") ? 16 : 10);
    }

    /// <summary>Geometry declared for <paramref name="chip"/>, as the frontend would carry it.</summary>
    public static DeviceGeometry For(string chip)
    {
        var path = Path.Combine(Dir(), chip.ToLowerInvariant() + ".py");
        Assert.True(File.Exists(path), $"no chip file for '{chip}' at {path}");
        var text = File.ReadAllText(path);

        return new DeviceGeometry
        {
            Chip       = chip.ToLowerInvariant(),
            RamSize    = Constant(text, "RAM_SIZE"),
            FlashSize  = Constant(text, "FLASH_SIZE"),
            EepromSize = Constant(text, "EEPROM_SIZE"),
        };
    }

    /// <summary>Every AVR chip file, as (name, RAM_START, RAM_SIZE, FLASH_SIZE).</summary>
    public static IEnumerable<(string Chip, int RamStart, int RamSize, int FlashSize)> AvrChips()
    {
        foreach (var file in Directory.GetFiles(Dir(), "*.py").OrderBy(f => f, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            if (!Regex.IsMatch(text, @"device_info\([^)]*arch\s*=\s*""avr""")) continue;

            var chip = Path.GetFileNameWithoutExtension(file);
            var start = Constant(text, "RAM_START");
            var size = Constant(text, "RAM_SIZE");
            var flash = Constant(text, "FLASH_SIZE");
            Assert.True(start.HasValue && size.HasValue && flash.HasValue,
                $"{Path.GetFileName(file)} declares no RAM_START/RAM_SIZE/FLASH_SIZE");

            yield return (chip, start!.Value, size!.Value, flash!.Value);
        }
    }

    /// <summary>
    /// Give a hand-built test program the geometry a real .mir would carry.
    /// Tests that are ABOUT the contract build their own ProgramIR and must not use this.
    /// </summary>
    public static ProgramIR WithGeometry(this ProgramIR program, string chip)
    {
        program.Device = For(chip);
        return program;
    }
}
