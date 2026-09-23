using PyMCU.Backend.Targets.AVR;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// Regression tests for the widest-type register-home rule: the same storage
/// name can legitimately appear at different widths inside one body (a tagged
/// union payload is read at member width under tag dispatch -- RFC 0009 --
/// but the IR contract allows it for any name). Both allocators used to keep
/// only one observed type per name: AvrRegisterAllocator kept the LAST and
/// AvrLinearScan kept the FIRST. Either way the assigned home could be
/// narrower than another access in the same body, so a wider store overflowed
/// the neighbouring variable's registers -- silent corruption with no
/// diagnostic. The reproducers below are plain names at two widths; no union
/// machinery is involved.
/// </summary>
public class AvrWidestHomeTests
{
    // ─── AvrRegisterAllocator (named vars -> R2-R15 homes) ───────────────────

    private static ProgramIR VarProgram(params Instruction[] body)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        return prog;
    }

    [Fact]
    public void VarSeenFloatThenInt16_NeverWinsATwoByteHome()
    {
        // `a` is written by a call at FLOAT width and later used at INT16. The
        // last-seen type (INT16) made it eligible for a 2-byte R2-R15 home, so
        // the 4-register float store after the call spilled over the next
        // variable's registers. Keeping the widest type makes it ineligible --
        // it takes a stack slot instead, which is sized for four bytes.
        var aF = new Variable("a", DataType.FLOAT);
        var aW = new Variable("a", DataType.INT16);
        var b = new Variable("b", DataType.INT16);
        var homes = AvrRegisterAllocator.Allocate(VarProgram(
            new Call("pick", new List<Val>(), aF),            // a at FLOAT
            new Binary(BinaryOp.Add, aW, new Constant(1), aW),// a at INT16
            new Copy(new Constant(3), b),
            new Binary(BinaryOp.Add, b, new Constant(1), b),
            new Return(aW)));

        Assert.False(homes.ContainsKey("a"));   // 4-byte name in a <=2-byte pool
        Assert.Equal("R2", homes["b"]);         // first eligible var, uncontended
    }

    [Fact]
    public void VarSeenInt16ThenUint8_KeepsTheWiderHome()
    {
        // `a` at INT16 then UINT8: last-write-wins recorded a byte, so `a` took
        // one register and `b` took the next -- straight into the high byte a
        // 16-bit access uses. With the widest type kept, `a` takes R2:R3 and
        // `b` starts at R4.
        var aW = new Variable("a", DataType.INT16);
        var aB = new Variable("a", DataType.UINT8);
        var b = new Variable("b", DataType.UINT8);
        var homes = AvrRegisterAllocator.Allocate(VarProgram(
            new Copy(new Constant(1), aW),
            new Binary(BinaryOp.Add, aW, new Constant(1), aW),
            new Copy(new Constant(2), b),
            new Binary(BinaryOp.Add, b, new Constant(1), b),
            new Copy(new Constant(0), aB),                     // a last seen UINT8
            new Return(aB),
            new Return(b)));

        Assert.Equal("R2", homes["a"]);   // INT16 home: R2:R3
        Assert.Equal("R4", homes["b"]);   // not R3 -- that byte belongs to a
    }

    // ─── AvrLinearScan (temps -> R16/R17 scratch homes) ─────────────────────

    private static Dictionary<string, string> TempAllocate(params Instruction[] body) =>
        AvrLinearScan.Allocate(new Function { Name = "test", Body = body.ToList() });

    [Fact]
    public void TempSeenUint8ThenInt16_KeepsTheWholePair()
    {
        // `t` is first seen at UINT8 (one slot) and later at INT16 (the pair).
        // First-write-wins kept it a byte temp: an overlapping second temp then
        // took R17 -- the very register the 16-bit access drives as the high
        // byte. With the widest type kept, `t` owns R16:R17 and the overlapping
        // temp spills instead.
        var tB = new Temporary("t", DataType.UINT8);
        var tW = new Temporary("t", DataType.INT16);
        var u = new Temporary("u", DataType.UINT8);
        var homes = TempAllocate(
            new Copy(new Constant(1), tB),                     // 0: t def UINT8
            new Copy(new Constant(2), u),                      // 1: u def, overlaps t
            new Binary(BinaryOp.Add, tW, new Constant(1), tW), // 2: t at INT16
            new Binary(BinaryOp.Add, u, new Constant(1), u),   // 3: u still live
            new Return(tW),                                    // 4: t last use
            new Return(u));                                    // 5: u last use

        Assert.Equal("R16", homes["t"]);        // owns the pair R16:R17
        Assert.False(homes.ContainsKey("u"));   // cannot share either half
    }
}
