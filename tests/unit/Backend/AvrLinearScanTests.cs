using PyMCU.Backend.Targets.AVR;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// Unit tests for AvrLinearScan.Allocate() — the greedy linear-scan register
/// allocator that maps short-lived temporaries to R16/R17: an 8-bit temp takes
/// one slot, a 16-bit temp takes the R16:R17 pair (low in R16).
/// </summary>
public class AvrLinearScanTests
{
    private static Dictionary<string, string> Allocate(params Instruction[] body)
    {
        var func = new Function { Name = "test", Body = body.ToList() };
        return AvrLinearScan.Allocate(func);
    }

    // ─── Single temporary ─────────────────────────────────────────────────────

    [Fact]
    public void SingleTemporary_AssignedToR16()
    {
        var t1 = new Temporary("t1");
        var result = Allocate(
            new Copy(new Constant(1), t1),
            new Return(t1));

        Assert.True(result.TryGetValue("t1", out var reg));
        Assert.Equal("R16", reg);
    }

    // ─── Two non-overlapping temporaries (R16 is reused) ─────────────────────

    [Fact]
    public void TwoNonOverlapping_BothAssigned_R16Reused()
    {
        // t1 is last used at instruction 1; t2 starts at instruction 2.
        // They do not overlap, so both can share R16.
        var t1 = new Temporary("t1");
        var t2 = new Temporary("t2");
        var result = Allocate(
            new Copy(new Constant(1), t1),   // 0 — t1 def
            new Return(t1),                   // 1 — t1 last use
            new Copy(new Constant(2), t2),   // 2 — t2 def
            new Return(t2));                  // 3 — t2 last use

        Assert.True(result.ContainsKey("t1"));
        Assert.True(result.ContainsKey("t2"));
        // Non-overlapping intervals: t2 may safely reuse t1's register.
        Assert.Equal(result["t1"], result["t2"]);
    }

    // ─── Two overlapping temporaries (R16 and R17 both used) ─────────────────

    [Fact]
    public void TwoOverlapping_AssignedToR16AndR17()
    {
        var t1 = new Temporary("t1");
        var t2 = new Temporary("t2");
        var dummy = new Temporary("dummy");
        var result = Allocate(
            new Copy(new Constant(1), t1),                               // 0 — t1 def
            new Copy(new Constant(2), t2),                               // 1 — t2 def
            new Binary(BinaryOp.Add, t1, t2, dummy),                     // 2 — t1 and t2 live
            new Return(new Constant(0)));                                 // 3

        Assert.True(result.ContainsKey("t1"));
        Assert.True(result.ContainsKey("t2"));
        // Must be assigned different registers
        Assert.NotEqual(result["t1"], result["t2"]);
        var regs = new HashSet<string> { result["t1"], result["t2"] };
        Assert.Subset(new HashSet<string> { "R16", "R17" }, regs);
    }

    // ─── More than two simultaneous live temporaries ──────────────────────────

    [Fact]
    public void ThreeSimultaneous_AtMostTwoAllocated()
    {
        var t1 = new Temporary("t1");
        var t2 = new Temporary("t2");
        var t3 = new Temporary("t3");
        var r12 = new Temporary("r12");
        var r13 = new Temporary("r13");
        var result = Allocate(
            new Copy(new Constant(1), t1),                    // 0 — t1 def
            new Copy(new Constant(2), t2),                    // 1 — t2 def
            new Copy(new Constant(3), t3),                    // 2 — t3 def
            new Binary(BinaryOp.Add, t1, t2, r12),            // 3 — t1 and t2 used
            new Binary(BinaryOp.Add, t2, t3, r13),            // 4 — t2 and t3 used
            new Return(new Constant(0)));                      // 5

        int allocatedCount = new[] { "t1", "t2", "t3" }
            .Count(name => result.ContainsKey(name));
        // Only 2 registers available; at least one must spill.
        Assert.True(allocatedCount <= 2,
            $"Expected ≤ 2 allocated temporaries, got {allocatedCount}");
    }

    // ─── Temporary spanning a function call is not allocated ─────────────────

    [Fact]
    public void TemporarySpanningCall_NotAllocated()
    {
        // t1 is defined before the Call and used after — it spans the call.
        // Scratch registers R16/R17 are caller-saved by AVR-GCC convention,
        // so the allocator must not place such a temporary there.
        var t1 = new Temporary("t1");
        var result = Allocate(
            new Copy(new Constant(42), t1),                          // 0 — t1 def
            new Call("some_func", new List<Val>(), new NoneVal()),    // 1 — call
            new Return(t1));                                          // 2 — t1 last use

        // Interval for t1: Def=0, LastUse=2, spans call at index 1 → must be spilled.
        Assert.False(result.ContainsKey("t1"),
            "t1 spans a call and must not be allocated to a scratch register");
    }

    // ─── UINT16 temporary takes the R16:R17 pair ─────────────────────────────

    [Fact]
    public void Uint16Temporary_AssignedToR16Pair()
    {
        // A non-call-spanning 16-bit temporary occupies the whole R16:R17 pair,
        // homed at R16 (low byte); the codegen drives R17 via GetHighReg.
        var t1 = new Temporary("t1", DataType.UINT16);
        var result = Allocate(
            new Copy(new Constant(500), t1),
            new Return(t1));

        Assert.True(result.TryGetValue("t1", out var reg),
            "a non-call-spanning UINT16 temporary is allocated to the R16:R17 pair");
        Assert.Equal("R16", reg);
    }

    // ─── UINT16 temporary spanning a call is still spilled ────────────────────

    [Fact]
    public void Uint16Temporary_SpanningCall_NotAllocated()
    {
        var t1 = new Temporary("t1", DataType.UINT16);
        var result = Allocate(
            new Copy(new Constant(500), t1),                       // 0 — t1 def
            new Call("some_func", new List<Val>(), new NoneVal()),  // 1 — call
            new Return(t1));                                        // 2 — t1 last use

        Assert.False(result.ContainsKey("t1"),
            "a UINT16 temporary spanning a call must not be placed in caller-saved R16:R17");
    }

    // ─── Runtime routines with no IR Call ─────────────────────────────────────
    // __div32/__mod32/__divs32/__mods32 use R16 as their loop counter without saving it, and
    // the GC shadow-stack push/pop stage the depth in R16:R17, so a temp live across one of
    // them is spilled exactly as across a Call. `(s + 300) + 100000 // (s + 7)` printed 14541
    // instead of 14585 while the temp sat in the pair.

    [Theory]
    [InlineData(BinaryOp.FloorDiv)]
    [InlineData(BinaryOp.Div)]
    [InlineData(BinaryOp.Mod)]
    public void Uint16Temporary_Spanning32BitDivMod_NotAllocated(BinaryOp op)
    {
        var t1 = new Temporary("t1", DataType.UINT16);
        var q = new Temporary("q", DataType.UINT32);
        var result = Allocate(
            new Copy(new Constant(300), t1),                                               // 0 — t1 def
            new Binary(op, new Constant(100000), new Variable("d", DataType.UINT32), q),  // 1 — __div32
            new Binary(BinaryOp.Add, t1, q, new Temporary("r", DataType.UINT32)),         // 2 — t1 last use
            new Return(new NoneVal()));

        Assert.False(result.ContainsKey("t1"),
            "__div32 and __mod32 write R16, so a temp live across them cannot sit in R16:R17");
    }

    [Fact]
    public void Uint8Temporary_Spanning32BitAugDiv_NotAllocated()
    {
        var t1 = new Temporary("t1");
        var result = Allocate(
            new Copy(new Constant(20), t1),                                                // 0 — t1 def
            new AugAssign(BinaryOp.FloorDiv, new Variable("v", DataType.INT32), new Constant(7)), // 1
            new Return(t1));                                                               // 2 — t1 last use

        Assert.False(result.ContainsKey("t1"),
            "a 32-bit //= lowers to __divs32, which writes R16");
    }

    [Fact]
    public void Uint16Temporary_SpanningGcUnroot_NotAllocated()
    {
        var t1 = new Temporary("t1", DataType.UINT16);
        var result = Allocate(
            new Copy(new Constant(300), t1),                   // 0 — t1 def
            new GcUnroot(new Variable("obj", DataType.GC_REF)), // 1 — LDS/DEC/STS R16
            new Return(t1));                                    // 2 — t1 last use

        Assert.False(result.ContainsKey("t1"), "the shadow-stack pop stages the depth in R16");
    }

    // The routines that leave R16:R17 intact keep the temp in the pair: spilling there would
    // cost bytes on every 8/16-bit division and every 32-bit multiply for nothing.
    [Theory]
    [InlineData(BinaryOp.FloorDiv, DataType.UINT16)]
    [InlineData(BinaryOp.Mod, DataType.INT16)]
    [InlineData(BinaryOp.Mul, DataType.INT32)]
    public void Uint16Temporary_SpanningRoutineThatSavesThePair_StillAllocated(BinaryOp op, DataType width)
    {
        var t1 = new Temporary("t1", DataType.UINT16);
        var q = new Temporary("q", width);
        var result = Allocate(
            new Copy(new Constant(300), t1),                               // 0 — t1 def
            new Binary(op, new Variable("a", width), new Variable("b", width), q), // 1
            new Binary(BinaryOp.Add, t1, q, new Temporary("r", width)),    // 2 — t1 last use
            new Return(new NoneVal()));

        Assert.True(result.TryGetValue("t1", out var reg));
        Assert.Equal("R16", reg);
    }

    // ─── Empty function ───────────────────────────────────────────────────────

    // ─── Inline asm ───────────────────────────────────────────────────────────
    // asm() text the allocator cannot model: naming R16/R17, making a call, or taking %N
    // operands (staged through R16..R19) writes the pair, so a temp live across it is spilled.

    [Theory]
    [InlineData("ldi r16, 0x55")]
    [InlineData("LDI R17, 0x66")]
    [InlineData("movw r16, r24")]
    [InlineData("rcall helper")]
    [InlineData("call helper")]
    [InlineData("icall")]
    public void Uint16Temporary_SpanningAsmThatWritesThePair_NotAllocated(string code)
    {
        var t1 = new Temporary("t1", DataType.UINT16);
        var result = Allocate(
            new Copy(new Constant(300), t1),   // 0 — t1 def
            new InlineAsm(code),               // 1
            new Return(t1));                   // 2 — t1 last use

        Assert.False(result.ContainsKey("t1"), $"`{code}` can write R16:R17");
    }

    [Fact]
    public void Uint8Temporary_SpanningAsmWithOperands_NotAllocated()
    {
        var t1 = new Temporary("t1");
        var result = Allocate(
            new Copy(new Constant(20), t1),                                          // 0
            new InlineAsm("inc %0", new List<Val> { new Variable("x") }),            // 1 — %0 in R16
            new Return(t1));                                                         // 2

        Assert.False(result.ContainsKey("t1"), "asm() stages its %N operands through R16..R19");
    }

    [Theory]
    [InlineData("nop")]
    [InlineData("sei")]
    [InlineData("out 0x25, r24")]
    [InlineData("ldi r18, 1")]
    public void Uint16Temporary_SpanningAsmThatLeavesThePair_StillAllocated(string code)
    {
        // `r18` must not match `r1[67]`, and `r1` alone is not the pair either.
        var t1 = new Temporary("t1", DataType.UINT16);
        var result = Allocate(
            new Copy(new Constant(300), t1),
            new InlineAsm(code),
            new Return(t1));

        Assert.True(result.TryGetValue("t1", out var reg));
        Assert.Equal("R16", reg);
    }

    [Fact]
    public void EmptyFunction_ReturnsEmptyDictionary()
        => Assert.Empty(Allocate());

    // ─── Non-Temporary values are ignored ────────────────────────────────────

    [Fact]
    public void OnlyVariables_NoTemporaries_ReturnsEmpty()
    {
        var v = new Variable("x");
        var result = Allocate(
            new Copy(new Constant(1), v),
            new Return(v));

        // Variables are managed by the register allocator / stack, not linear scan.
        Assert.Empty(result);
    }

    // ─── AugAssign source and target are visited ─────────────────────────────

    [Fact]
    public void AugAssign_TemporaryInOperand_Allocated()
    {
        var t1 = new Temporary("t1");
        var target = new Variable("x");
        var result = Allocate(
            new Copy(new Constant(3), t1),            // 0 — t1 def
            new AugAssign(BinaryOp.Add, target, t1),  // 1 — t1 last use (operand)
            new Return(new Constant(0)));              // 2

        Assert.True(result.ContainsKey("t1"),
            "t1 used in AugAssign.Operand should be eligible for allocation");
    }

    // ─── Temporary used only in a comparison jump ─────────────────────────────

    [Fact]
    public void TemporaryInJumpSrc_Allocated()
    {
        var t1 = new Temporary("t1");
        var result = Allocate(
            new Copy(new Constant(5), t1),                                // 0 — t1 def
            new JumpIfEqual(t1, new Constant(5), "done"),                 // 1 — t1 last use
            new Label("done"),                                             // 2
            new Return(new Constant(0)));                                  // 3

        Assert.True(result.ContainsKey("t1"));
    }
}
