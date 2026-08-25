using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// The .mir is where the backend learns the chip's flash and SRAM sizes.
///
/// It did not used to be anywhere. `cfg.FlashSize` was never filled, so LargeFlash was
/// permanently false and reaching a flash table on an ATmega2560 emitted plain LPM,
/// which cannot address past byte 0xFFFF. The workaround was a per-chip table inside the
/// backend. These tests pin the replacement: the number comes from the .mir, the backend
/// keeps no copy of it, and both ways of not having it stop the build.
/// </summary>
public class AvrGeometryContractTests
{
    private static ProgramIR ReadsAFlashTable(DeviceGeometry? geometry)
    {
        var prog = new ProgramIR { Device = geometry };
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = [new ArrayLoadFlash("table", new Constant(0), new Variable("x"))],
        });
        return prog;
    }

    private static string Compile(string chip, ProgramIR prog)
    {
        var sw = new StringWriter();
        new AvrCodeGen(new DeviceConfig { TargetChip = chip, Arch = "avr" }).Compile(prog, sw);
        return sw.ToString();
    }

    private static DeviceGeometry Geometry(string chip, int? ram, int? flash)
        => new() { Chip = chip, RamSize = ram, FlashSize = flash };

    // -----------------------------------------------------------------------
    // the number in the .mir is the one that decides
    // -----------------------------------------------------------------------

    // Deliberately mismatched: the chip named on the command line is the 2560, but the
    // geometry says 32 KB. The output must follow the geometry. Were the backend still
    // deciding from a table keyed by chip name, this would emit ELPM and fail.
    [Fact]
    public void ANarrowFlashInTheMir_EmitsLpm_EvenForAChipNamedAtmega2560()
    {
        var asm = Compile("atmega2560", ReadsAFlashTable(Geometry("atmega2560", 8192, 32768)));

        Assert.Contains("\tLPM\t", asm);
        Assert.DoesNotContain("\tELPM\t", asm);
    }

    // And the converse: a chip the old table sized at 32 KB, told in the .mir that it has
    // more than 64 KB, must reach for RAMPZ + ELPM.
    [Fact]
    public void AWideFlashInTheMir_EmitsElpm_EvenForAChipNamedAtmega328p()
    {
        var asm = Compile("atmega328p", ReadsAFlashTable(Geometry("atmega328p", 2048, 262144)));

        Assert.Contains("\tELPM\t", asm);
        Assert.Contains("OUT\t0x3B", asm);
    }

    [Fact]
    public void RamendFollowsTheSramSizeInTheMir_NotAnyTableInTheBackend()
    {
        // atmega328p: RAMSTART 0x100 comes from the backend catalog (device_info does not
        // declare it), the size comes from the .mir. 0x100 + 1024 - 1 = 0x04FF.
        var prog = new ProgramIR { Device = Geometry("atmega328p", 1024, 32768) };
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = [new Copy(new Constant(1), new MemoryAddress(0x25))],
        });

        Assert.Contains(".equ RAMEND, 0x04FF", Compile("atmega328p", prog));
    }

    // -----------------------------------------------------------------------
    // not having the number stops the build
    // -----------------------------------------------------------------------

    [Fact]
    public void AMirFromACompilerWithoutTheContract_IsRefused()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Compile("atmega328p", ReadsAFlashTable(null)));

        Assert.Contains("no device geometry", ex.Message);
        Assert.Contains("Rebuild", ex.Message);
    }

    [Fact]
    public void AChipFileThatDeclaresNoFlashSize_IsRefusedByName()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Compile("atmega328p", ReadsAFlashTable(Geometry("atmega328p", 2048, null))));

        Assert.Contains("atmega328p", ex.Message);
        Assert.Contains("flash_size", ex.Message);
        Assert.Contains("LPM", ex.Message);
    }

    [Fact]
    public void AChipFileThatDeclaresNoRamSize_IsRefusedByName()
    {
        var prog = new ProgramIR { Device = Geometry("attiny85", null, 8192) };
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = [new Copy(new Constant(1), new MemoryAddress(0x25))],
        });

        var ex = Assert.Throws<InvalidOperationException>(() => Compile("attiny85", prog));

        Assert.Contains("attiny85", ex.Message);
        Assert.Contains("ram_size", ex.Message);
    }

    // -----------------------------------------------------------------------
    // what the chip files actually say
    // -----------------------------------------------------------------------

    // The one part in the catalog whose flash is past the 16-bit Z, and the reason the
    // whole LPM/ELPM branch exists. Read from the chip file, not from a literal here.
    [Fact]
    public void TheAtmega2560ChipFile_StillPutsItsFlashPastTheSixteenBitZ()
    {
        var asm = Compile("atmega2560", ReadsAFlashTable(ChipCatalog.For("atmega2560")));

        Assert.True(ChipCatalog.For("atmega2560").FlashSize > 0x10000);
        Assert.Contains("\tELPM\t", asm);
    }

    [Theory]
    [InlineData("atmega328p")]
    [InlineData("attiny85")]
    [InlineData("atmega48")]
    public void EveryOtherPartStaysOnPlainLpm(string chip)
    {
        var asm = Compile(chip, ReadsAFlashTable(ChipCatalog.For(chip)));

        Assert.Contains("\tLPM\t", asm);
        Assert.DoesNotContain("\tELPM\t", asm);
    }
}
