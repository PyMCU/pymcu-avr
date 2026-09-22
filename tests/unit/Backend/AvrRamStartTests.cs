using System.Text.RegularExpressions;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// Absolute slot/array accesses must be based on the chip's RAMSTART.
///
/// CompileArrayLoad/CompileArrayStore/LoadIntoReg/StoreRegInto and the ArrayBase
/// materialisation emitted `0x0100 + offset` for every access past the Y+63
/// displacement window. That is only right on parts whose SRAM really starts at
/// 0x0100: on the ATmega2560 (RAMSTART 0x0200) every one of those stores lands
/// 0x100 low, inside the extended-I/O register range, and the matching load
/// reads the register back -- a silent wrong-code bug on every Mega build.
/// ATtiny parts are wrong the other way (RAMSTART 0x0060).
///
/// The Y+q forms are untouched: they were always correct and stay correct.
/// </summary>
public class AvrRamStartTests
{
    // A program whose static data crosses the Y+63 boundary twice:
    //   DATA[64..]      -- a module array indexed past the window (CompileArrayLoad/Store)
    //   buf[4]          -- a function-local array above 100 bytes of globals (same paths,
    //                      local region)
    //   call helper(&buf) -- the ArrayBase materialisation for a slot above the window
    //
    // Layout: DATA 0..99, then main's locals t@100, buf@101, u@109.
    private static ProgramIR Prog()
    {
        var prog = new ProgramIR();
        prog.GlobalArrays["DATA"] = 100;
        prog.Functions.Add(new Function
        {
            Name = "helper",
            Params = ["p"],
            Body = [new Return(new NoneVal())],
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body =
            [
                new ArrayStore("DATA", new Constant(64), new Constant(0x5A), DataType.UINT8, 100),
                new ArrayLoad("DATA", new Constant(90), new Temporary("t"), DataType.UINT8, 100),
                new ArrayStore("DATA", new Constant(91), new Temporary("t"), DataType.UINT8, 100),
                new ArrayStore("buf", new Constant(4), new Constant(7), DataType.UINT8, 8),
                new Call("helper", [new ArrayBase("buf")], new NoneVal()),
            ],
        });
        return prog;
    }

    private static string Compile(string chip)
    {
        var sw = new StringWriter();
        new AvrCodeGen(new DeviceConfig { TargetChip = chip, Arch = "avr" })
            .Compile(Prog().WithGeometry(chip), sw);
        return sw.ToString();
    }

    private static IEnumerable<int> AbsAddresses(string asm)
        => Regex.Matches(asm, @"\t(?:STS\t0x|LDS\t\w+,\s*0x)([0-9A-F]{4})")
                .Select(m => Convert.ToInt32(m.Groups[1].Value, 16));

    [Fact]
    public void AbsoluteAccesses_OnAtmega2560_StartAt0x0200()
    {
        var asm = Compile("atmega2560");

        // DATA at _stack_base + 0: element 64 -> 0x0240, element 90 -> 0x025A.
        // buf at _stack_base + 101: element 4 -> 0x0269, its address -> lo8(0x0265).
        Assert.Contains("\tSTS\t0x0240", asm);
        Assert.Contains("\tLDS\tR24, 0x025A", asm);
        Assert.Contains("\tSTS\t0x0269", asm);
        Assert.Contains("lo8(0x0265)", asm);

        // The whole point: no absolute access may land below the part's RAMSTART.
        Assert.DoesNotContain(AbsAddresses(asm), a => a < 0x0200);
    }

    [Fact]
    public void AbsoluteAccesses_OnAtmega328p_AreUnchanged()
    {
        var asm = Compile("atmega328p");

        // RAMSTART 0x0100: the same offsets land at 0x0140 / 0x015A / 0x0169.
        Assert.Contains("\tSTS\t0x0140", asm);
        Assert.Contains("\tLDS\tR24, 0x015A", asm);
        Assert.Contains("\tSTS\t0x0169", asm);
        Assert.Contains("lo8(0x0165)", asm);
    }

    [Fact]
    public void AbsoluteAccesses_OnAttiny85_StartAt0x0060()
    {
        var asm = Compile("attiny85");

        // RAMSTART 0x0060 -- the old constant was wrong upward here, not downward.
        Assert.Contains("\tSTS\t0x00A0", asm);        // 0x60 + 64
        Assert.Contains("\tLDS\tR24, 0x00BA", asm);   // 0x60 + 90
        Assert.Contains("\tSTS\t0x00C9", asm);        // 0x60 + 101 + 4
        Assert.Contains("lo8(0x00C5)", asm);          // buf base: 0x60 + 101
        Assert.DoesNotContain(AbsAddresses(asm), a => a < 0x0060);
    }
}
