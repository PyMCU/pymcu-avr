using System.Text.RegularExpressions;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;
using IrBinaryOp = PyMCU.IR.BinaryOp;

namespace PyMCU.UnitTests;

public class AvrDeviceLayoutTests
{
    private static string Compile(string chip, params Instruction[] body)
    {
        var prog = new ProgramIR().WithGeometry(chip);
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        var codegen = new AvrCodeGen(new DeviceConfig { TargetChip = chip, Arch = "avr" });
        var sw = new StringWriter();
        codegen.Compile(prog, sw);
        return sw.ToString();
    }

    private static string Blink(string chip)
        => Compile(chip, new Copy(new Constant(1), new MemoryAddress(0x25)));

    public static IEnumerable<object[]> AvrChips()
        => ChipCatalog.AvrChips().Select(c => new object[] { c.Chip, c.RamStart, c.RamSize });

    [Theory]
    [MemberData(nameof(AvrChips))]
    public void SramLayout_MatchesTheChipCatalog(string chip, int ramStart, int ramSize)
    {
        var asm = Blink(chip);

        Assert.Contains($".equ RAMSTART, 0x{ramStart:X4}", asm);
        Assert.Contains($".equ RAMEND, 0x{ramStart + ramSize - 1:X4}", asm);
    }

    [Theory]
    [InlineData("atmega48")]
    [InlineData("atmega48p")]
    [InlineData("atmega88")]
    [InlineData("atmega88p")]
    [InlineData("attiny85")]
    [InlineData("attiny13")]
    public void VectorSlots_AreOneWord_WithoutJmpCall(string chip)
    {
        var asm = Blink(chip);

        Assert.Contains(".org 0x0001\n\tRJMP", asm.Replace("\r\n", "\n"));
        Assert.DoesNotContain("\tJMP\t", asm);
    }

    [Theory]
    [InlineData("atmega168")]
    [InlineData("atmega328p")]
    [InlineData("atmega2560")]
    [InlineData("atmega32u4")]
    public void VectorSlots_AreTwoWords_WithJmpCall(string chip)
    {
        var asm = Blink(chip);

        Assert.Contains(".org 0x0002\n\tRJMP", asm.Replace("\r\n", "\n"));
    }

    [Theory]
    [InlineData("atmega48")]
    [InlineData("atmega88p")]
    [InlineData("attiny85")]
    public void Calls_AreRelative_WithoutJmpCall(string chip)
    {
        var asm = Compile(chip,
            new Binary(IrBinaryOp.Div,
                new Variable("a", DataType.UINT16),
                new Variable("b", DataType.UINT16),
                new Variable("c", DataType.UINT16)));

        Assert.Contains("\tRCALL\t__div16", asm);
        Assert.DoesNotContain("\tCALL\t", asm);
    }

    [Fact]
    public void Calls_StayAbsolute_WithJmpCall()
    {
        var asm = Compile("atmega328p",
            new Binary(IrBinaryOp.Div,
                new Variable("a", DataType.UINT16),
                new Variable("b", DataType.UINT16),
                new Variable("c", DataType.UINT16)));

        Assert.Contains("\tCALL\t__div16", asm);
    }

    [Fact]
    public void UnknownChip_IsRejected_InsteadOfGuessed()
    {
        // Geometry supplied, so what is under test is the backend catalog's refusal to
        // guess a RAMSTART, not the absence of a chip file.
        var prog = new ProgramIR
        {
            Device = new DeviceGeometry
            {
                Chip = "atmega1284p", RamSize = 16384, FlashSize = 131072,
            },
        };
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = [new Copy(new Constant(1), new MemoryAddress(0x25))],
        });

        var ex = Assert.Throws<Exception>(
            () => new AvrCodeGen(new DeviceConfig { TargetChip = "atmega1284p", Arch = "avr" })
                .Compile(prog, new StringWriter()));

        Assert.Contains("atmega1284p", ex.Message);
    }
}
