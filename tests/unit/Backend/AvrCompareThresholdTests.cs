using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

// `x <= C` is lowered on its false edge to "jump away if x > C", and the backend may emit
// nothing at all for that jump when no value of the type can exceed C. Deciding that needs
// the maximum of the TYPE. For a 4-byte type it asked int.MaxValue, which is the largest
// SIGNED 32-bit value, so for a uint32 the comparison was dropped across the entire upper
// half of the range and the branch was never taken. See fixtures/uint32-compare-max, which
// catches the same defect by running it.
//
// The 8- and 16-bit paths ask 0xFF and 0xFFFF and are right, because those are values an
// int can hold. 32 is the one width where the type outgrows the int the threshold sits in,
// which is why exactly one of the three was wrong.
//
// WHAT DISCRIMINATES: AtSignedMaxOnAnUnsignedType_StillCompares. Against the unfixed backend
// the function emits no compare and no branch whatsoever.
//
// WHAT IS INVARIANT, and here on purpose: the three cases that SHOULD emit nothing. A fix
// that stopped clamping altogether would make them emit a compare against a threshold no
// value can reach, and the tests below would not notice unless they are asked.
public class AvrCompareThresholdTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    private static string GreaterThan(DataType type, int threshold)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new JumpIfGreaterThan(new Variable("x", type), new Constant(threshold), "L_far"),
                new Label("L_far"),
                new Return(new NoneVal()),
            },
        });
        prog.Device ??= ChipCatalog.For("atmega328p");
        var sw = new StringWriter();
        new AvrCodeGen(Atmega328p).Compile(prog, sw);
        return sw.ToString();
    }

    private static bool Compares(string asm) => asm.Contains("CP\tR24, R18");
    private static bool Branches(string asm) => asm.Contains("BRSH") || asm.Contains("BRGE");

    // --- what is wrong ----------------------------------------------------------

    [Fact]
    public void AtSignedMaxOnAnUnsignedType_StillCompares()
    {
        // A uint32 runs to 4294967295, so `x > 2147483647` is a real question for half the
        // range. Emitting nothing answers "never" for all of it.
        var asm = GreaterThan(DataType.UINT32, int.MaxValue);

        Assert.True(Compares(asm), "the threshold must be compared against:\n" + asm);
        Assert.True(Branches(asm), "and the branch must be emitted:\n" + asm);
    }

    // --- invariants: these must keep emitting nothing ----------------------------

    [Fact]
    public void AtSignedMaxOnASignedType_EmitsNothing()
    {
        // No int32 exceeds int.MaxValue, so there is nothing to compare.
        var asm = GreaterThan(DataType.INT32, int.MaxValue);

        Assert.False(Compares(asm), "an int32 cannot exceed its own maximum:\n" + asm);
        Assert.False(Branches(asm), "so no branch belongs here either:\n" + asm);
    }

    [Fact]
    public void AtItsMaximum_AByteEmitsNothing()
    {
        var asm = GreaterThan(DataType.UINT8, 0xFF);

        Assert.False(Branches(asm), "no uint8 exceeds 255:\n" + asm);
    }

    [Fact]
    public void AtItsMaximum_ASixteenBitValueEmitsNothing()
    {
        var asm = GreaterThan(DataType.UINT16, 0xFFFF);

        Assert.False(Branches(asm), "no uint16 exceeds 65535:\n" + asm);
    }

    [Fact]
    public void BelowItsMaximum_AThirtyTwoBitValueStillCompares()
    {
        var asm = GreaterThan(DataType.UINT32, 1500000000);

        Assert.True(Compares(asm), "an ordinary 32-bit threshold must be compared:\n" + asm);
        Assert.True(Branches(asm), "and branched on:\n" + asm);
    }
}
