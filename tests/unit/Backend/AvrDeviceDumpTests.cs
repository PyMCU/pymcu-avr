using System.Text.Json;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;
using IrBinaryOp = PyMCU.IR.BinaryOp;

namespace PyMCU.UnitTests;

public class AvrDeviceDumpTests
{
    public record Row(string Chip, int RamStart, bool HasJmpCall);

    private static List<Row> Dump()
    {
        var rows = new List<Row>();
        using var doc = JsonDocument.Parse(AvrDevices.ToJson());
        foreach (var e in doc.RootElement.EnumerateArray())
            rows.Add(new Row(
                e.GetProperty("Chip").GetString()!,
                e.GetProperty("RamStart").GetInt32(),
                e.GetProperty("HasJmpCall").GetBoolean()));
        return rows;
    }

    private static string Compile(string chip, params Instruction[] body)
    {
        var prog = new ProgramIR().WithGeometry(chip);
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
        => Dump().Select(r => new object[] { r.Chip, r.RamStart, r.HasJmpCall });

    [Fact]
    public void TheDumpIsNotEmpty()
    {
        Assert.True(Dump().Count >= 15, $"the published catalog has only {Dump().Count} chips");
    }

    // The catalog carries what device_info() does NOT declare, and nothing else. RamSize
    // and FlashSize used to be columns here; the .mir carries them now, and a second copy
    // in the catalog is how the ATmega2560 came to choose LPM over ELPM from a list of
    // chip names in the first place.
    [Fact]
    public void TheDumpPublishesNoGeometry()
    {
        using var doc = JsonDocument.Parse(AvrDevices.ToJson());
        var first = doc.RootElement.EnumerateArray().First();

        Assert.False(first.TryGetProperty("RamSize", out _),
            "SRAM size belongs to the chip file and travels in the .mir, not in this catalog");
        Assert.False(first.TryGetProperty("FlashSize", out _),
            "flash size belongs to the chip file and travels in the .mir, not in this catalog");
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheSramBaseTheCodegenEmits(string chip, int ramStart, bool _)
    {
        Assert.Contains($".equ RAMSTART, 0x{ramStart:X4}", Blink(chip));
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheVectorSlotsTheCodegenEmits(string chip, int _, bool hasJmpCall)
    {
        var asm = Blink(chip).Replace("\r\n", "\n");

        Assert.Contains(hasJmpCall ? ".org 0x0002\n\tRJMP" : ".org 0x0001\n\tRJMP", asm);
    }

    // The call-stack floor used to be a flat 64 bytes, which is the whole SRAM of an
    // ATtiny13, so no program with a temporary compiled for it and this test asserted the
    // refusal to keep the hole visible. The floor is min(64, SRAM / 2) now, so the two
    // 64-byte parts compile like every other part without JMP/CALL. The list is kept, empty,
    // as the place a part that genuinely cannot fit would go.
    private static readonly string[] NoRoomForATemporary = [];

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheDumpMatchesTheCallFormTheCodegenEmits(string chip, int _, bool hasJmpCall)
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
    public void TheDumpListsEveryChipTheBackendAccepts()
    {
        foreach (var r in Dump())
            Blink(r.Chip);

        var prog = new ProgramIR { Device = new DeviceGeometry { Chip = "atmega1284p", RamSize = 16384, FlashSize = 131072 } };
        prog.Functions.Add(new Function { Name = "main", Body = [] });
        var ex = Assert.Throws<Exception>(
            () => new AvrCodeGen(new DeviceConfig { TargetChip = "atmega1284p", Arch = "avr" })
                .Compile(prog, new StringWriter()));
        Assert.Contains("atmega1284p", ex.Message);
    }

    // Every chip the backend accepts must have a chip file declaring both sizes, or the
    // .mir has nothing to carry and the build fails on the first flash table it meets.
    [Theory]
    [MemberData(nameof(Rows))]
    public void EveryChipInTheCatalogHasAChipFileDeclaringBothSizes(string chip, int _, bool __)
    {
        var geo = ChipCatalog.For(chip);

        Assert.NotNull(geo.RamSize);
        Assert.NotNull(geo.FlashSize);
    }
}
