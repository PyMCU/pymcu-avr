using System.Text.Json;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;
using IrBinaryOp = PyMCU.IR.BinaryOp;

namespace PyMCU.UnitTests;

public class AvrDeviceDumpTests
{
    public record Row(string Chip, int RamStart, int RamSize, int RamEnd, int FlashSize, bool HasJmpCall);

    private static List<Row> Dump()
    {
        var rows = new List<Row>();
        using var doc = JsonDocument.Parse(AvrDevices.ToJson());
        foreach (var e in doc.RootElement.EnumerateArray())
            rows.Add(new Row(
                e.GetProperty("Chip").GetString()!,
                e.GetProperty("RamStart").GetInt32(),
                e.GetProperty("RamSize").GetInt32(),
                e.GetProperty("RamEnd").GetInt32(),
                e.GetProperty("FlashSize").GetInt32(),
                e.GetProperty("HasJmpCall").GetBoolean()));
        return rows;
    }

    private static string Compile(string chip, params Instruction[] body)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        var sw = new StringWriter();
        new AvrCodeGen(new DeviceConfig { TargetChip = chip, Arch = "avr" }).Compile(prog, sw);
        return sw.ToString();
    }

    private static string Blink(string chip)
        => Compile(chip, new Copy(new Constant(1), new MemoryAddress(0x25)));

    private static string Divides(string chip)
        => Compile(chip, new Binary(IrBinaryOp.Div,
            new Variable("a", DataType.UINT16),
            new Variable("b", DataType.UINT16),
            new Variable("c", DataType.UINT16)));

    public static IEnumerable<object[]> Rows()
        => Dump().Select(r => new object[] { r.Chip, r.RamStart, r.RamEnd, r.HasJmpCall });

    public static IEnumerable<object[]> FlashRows()
        => Dump().Select(r => new object[] { r.Chip, r.FlashSize });

    private static string ReadsAFlashTable(string chip)
        => Compile(chip, new ArrayLoadFlash("table", new Constant(0), new Variable("x")));

    // Reaching a table above byte address 0xFFFF needs RAMPZ + ELPM; below it, plain LPM.
    // The chip's flash size decides, and the catalog is where that size now lives.
    [Theory]
    [MemberData(nameof(FlashRows))]
    public void TheDumpDecidesHowFarTheCodegenCanReachIntoFlash(string chip, int flashSize)
    {
        var asm = ReadsAFlashTable(chip);
        var far = flashSize > 0x10000;

        Assert.Equal(far, asm.Contains("\tELPM\t"));
        Assert.Equal(far, asm.Contains("OUT\t0x3B"));
        Assert.Equal(!far, asm.Contains("\tLPM\t"));
    }

    [Fact]
    public void TheDumpIsNotEmpty()
    {
        Assert.True(Dump().Count >= 15, $"the published catalog has only {Dump().Count} chips");
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheSramTheCodegenEmits(string chip, int ramStart, int ramEnd, bool _)
    {
        var asm = Blink(chip);

        Assert.Contains($".equ RAMSTART, 0x{ramStart:X4}", asm);
        Assert.Contains($".equ RAMEND, 0x{ramEnd:X4}", asm);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheVectorSlotsTheCodegenEmits(string chip, int _, int __, bool hasJmpCall)
    {
        var asm = Blink(chip).Replace("\r\n", "\n");

        Assert.Contains(hasJmpCall ? ".org 0x0002\n\tRJMP" : ".org 0x0001\n\tRJMP", asm);
    }

    // The 64-byte call-stack floor is the whole SRAM of an ATtiny13, so no program
    // with a temporary compiles for it at all. Asserting the refusal keeps the hole
    // visible: the day the floor is fixed, this test says so instead of staying green.
    private static readonly string[] NoRoomForATemporary = ["attiny13", "attiny13a"];

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheCallFormTheCodegenEmits(string chip, int _, int __, bool hasJmpCall)
    {
        if (NoRoomForATemporary.Contains(chip))
        {
            var refused = Assert.Throws<InvalidOperationException>(() => Divides(chip));
            Assert.Contains("bytes of SRAM", refused.Message);
            return;
        }

        Assert.Contains(hasJmpCall ? "\tCALL\t__div16" : "\tRCALL\t__div16", Divides(chip));
    }

    [Fact]
    public void TheDumpIsInternallyConsistent()
    {
        foreach (var r in Dump())
            Assert.Equal(r.RamStart + r.RamSize - 1, r.RamEnd);
    }

    [Fact]
    public void TheDumpListsEveryChipTheBackendAccepts()
    {
        foreach (var r in Dump())
            Blink(r.Chip);

        var ex = Assert.Throws<Exception>(() => Blink("atmega1284p"));
        Assert.Contains("atmega1284p", ex.Message);
    }
}
