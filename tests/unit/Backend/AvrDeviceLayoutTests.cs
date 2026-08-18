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
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        var codegen = new AvrCodeGen(new DeviceConfig { TargetChip = chip, Arch = "avr" });
        var sw = new StringWriter();
        codegen.Compile(prog, sw);
        return sw.ToString();
    }

    private static string Blink(string chip)
        => Compile(chip, new Copy(new Constant(1), new MemoryAddress(0x25)));

    private static string CatalogDir()
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

    public static IEnumerable<object[]> AvrChips()
    {
        foreach (var file in Directory.GetFiles(CatalogDir(), "*.py").OrderBy(f => f))
        {
            var text = File.ReadAllText(file);
            if (!Regex.IsMatch(text, @"device_info\([^)]*arch\s*=\s*""avr""")) continue;
            var start = Regex.Match(text, @"^RAM_START\s*=\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.Multiline);
            var size = Regex.Match(text, @"^RAM_SIZE\s*=\s*(0x[0-9A-Fa-f]+|\d+)", RegexOptions.Multiline);
            Assert.True(start.Success && size.Success,
                $"{Path.GetFileName(file)} declares no RAM_START/RAM_SIZE");
            yield return new object[]
            {
                Path.GetFileNameWithoutExtension(file),
                Convert.ToInt32(start.Groups[1].Value, start.Groups[1].Value.StartsWith("0x") ? 16 : 10),
                Convert.ToInt32(size.Groups[1].Value, size.Groups[1].Value.StartsWith("0x") ? 16 : 10),
            };
        }
    }

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
        var ex = Assert.Throws<Exception>(() => Blink("atmega1284p"));

        Assert.Contains("atmega1284p", ex.Message);
    }
}
