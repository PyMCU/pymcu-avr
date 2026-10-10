using PyMCU.Backend.Targets.AVR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// Unit tests for AvrPeephole.Optimize() — all patterns are tested directly
/// against the static method without going through the full AVR code-gen
/// pipeline, so failures pinpoint individual peephole rules.
/// </summary>
public class AvrPeepholeTests
{
    private static List<AvrAsmLine> Opt(params AvrAsmLine[] lines)
        => AvrPeephole.Optimize(lines.ToList());

    // ─── Redundant LDI ───────────────────────────────────────────────────────

    [Fact]
    public void RedundantLDI_SameRegAndValue_SecondEliminated()
    {
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R16", "5"),
            AvrAsmLine.MakeInstruction("LDI", "R16", "5"));

        int count = result.Count(l =>
            l.Type == AvrAsmLine.LineType.Instruction &&
            l.Mnemonic == "LDI" && l.Op1 == "R16" && l.Op2 == "5");
        Assert.Equal(1, count);
    }

    [Fact]
    public void RedundantLDI_DifferentValues_BothKept()
    {
        // Use R16 between the two loads so the first LDI is live (a dead first load would be
        // removed by dead-store elimination). The redundant-LDI dedup must not collapse two
        // different values into one.
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R16", "1"),
            AvrAsmLine.MakeInstruction("OUT", "0x05", "R16"),
            AvrAsmLine.MakeInstruction("LDI", "R16", "2"));

        Assert.Contains(result, l => l.Mnemonic == "LDI" && l.Op2 == "1");
        Assert.Contains(result, l => l.Mnemonic == "LDI" && l.Op2 == "2");
    }

    [Fact]
    public void RedundantLDI_DifferentRegisters_BothKept()
    {
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R16", "7"),
            AvrAsmLine.MakeInstruction("LDI", "R17", "7"));

        Assert.Contains(result, l => l.Mnemonic == "LDI" && l.Op1 == "R16");
        Assert.Contains(result, l => l.Mnemonic == "LDI" && l.Op1 == "R17");
    }

    // ─── Redundant MOV ────────────────────────────────────────────────────────

    [Fact]
    public void RedundantMOV_SameAliases_SecondEliminated()
    {
        // After LDI R24, 5 and MOV R4, R24, the alias for R4 matches R24's alias.
        // A second MOV R4, R24 is redundant.
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R24", "5"),
            AvrAsmLine.MakeInstruction("MOV", "R4", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R4", "R24"));

        int count = result.Count(l =>
            l.Type == AvrAsmLine.LineType.Instruction &&
            l.Mnemonic == "MOV" && l.Op1 == "R4" && l.Op2 == "R24");
        Assert.Equal(1, count);
    }

    [Fact]
    public void MOV_AfterArithmetic_NotEliminated()
    {
        // ADD modifies R24's alias; the following MOV must be retained.
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R24", "1"),
            AvrAsmLine.MakeInstruction("MOV", "R4", "R24"),
            AvrAsmLine.MakeInstruction("ADD", "R24", "R4"),
            AvrAsmLine.MakeInstruction("MOV", "R4", "R24"));

        // Final MOV must still be present
        int lastMovCount = result.Count(l =>
            l.Type == AvrAsmLine.LineType.Instruction &&
            l.Mnemonic == "MOV" && l.Op1 == "R4" && l.Op2 == "R24");
        Assert.True(lastMovCount >= 1);
    }

    // ─── Dead Label Elimination ───────────────────────────────────────────────

    [Fact]
    public void DeadLabel_Unreferenced_L_Prefix_Eliminated()
    {
        // L_0 is not referenced by any branch — must be removed.
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDI", "R24", "1"),
            AvrAsmLine.MakeLabel("L_0"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.DoesNotContain(result, l =>
            l.Type == AvrAsmLine.LineType.Label && l.LabelText == "L_0");
    }

    [Fact]
    public void DeadLabel_Unreferenced_LDot_Prefix_Eliminated()
    {
        var result = Opt(
            AvrAsmLine.MakeInstruction("RET"),
            AvrAsmLine.MakeLabel("L.unreachable"));

        Assert.DoesNotContain(result, l =>
            l.Type == AvrAsmLine.LineType.Label && l.LabelText == "L.unreachable");
    }

    [Fact]
    public void DeadLabel_ReferencedByBranch_Kept()
    {
        // L_target is referenced; an intervening instruction prevents RJMP-to-next elimination.
        var result = Opt(
            AvrAsmLine.MakeInstruction("RJMP", "L_target"),
            AvrAsmLine.MakeInstruction("LDI", "R24", "0"),   // instruction between RJMP and label
            AvrAsmLine.MakeLabel("L_target"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.Contains(result, l =>
            l.Type == AvrAsmLine.LineType.Label && l.LabelText == "L_target");
    }

    [Fact]
    public void DeadLabel_NonL_Prefix_Kept()
    {
        // Labels not starting with L. / L_ (e.g. function names) must never be removed.
        var result = Opt(
            AvrAsmLine.MakeLabel("main"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.Contains(result, l =>
            l.Type == AvrAsmLine.LineType.Label && l.LabelText == "main");
    }

    // ─── Pattern A: STD Y+N, Rx followed immediately by LDD Ry, Y+N ─────────

    [Fact]
    public void StdLdd_SameOffsetAndReg_LddEliminated()
    {
        // STD Y+2, R20 ; LDD R20, Y+2 → LDD is dead (R20 already holds the value)
        var result = Opt(
            AvrAsmLine.MakeInstruction("STD", "Y+2", "R20"),
            AvrAsmLine.MakeInstruction("LDD", "R20", "Y+2"));

        Assert.DoesNotContain(result, l => l.Mnemonic == "LDD");
    }

    [Fact]
    public void StdLdd_SameOffsetDifferentReg_LddReplacedWithMov()
    {
        // STD Y+2, R20 ; LDD R22, Y+2 → MOV R22, R20 (avoids a memory round-trip)
        var result = Opt(
            AvrAsmLine.MakeInstruction("STD", "Y+2", "R20"),
            AvrAsmLine.MakeInstruction("LDD", "R22", "Y+2"));

        Assert.DoesNotContain(result, l => l.Mnemonic == "LDD");
        Assert.Contains(result, l =>
            l.Mnemonic == "MOV" && l.Op1 == "R22" && l.Op2 == "R20");
    }

    [Fact]
    public void StdLdd_DifferentOffsets_LddKept()
    {
        // Offsets differ → no optimisation should occur.
        var result = Opt(
            AvrAsmLine.MakeInstruction("STD", "Y+2", "R20"),
            AvrAsmLine.MakeInstruction("LDD", "R22", "Y+4"));

        Assert.Contains(result, l => l.Mnemonic == "LDD" && l.Op2 == "Y+4");
    }

    // ─── Pattern B: LDD Rx, Y+N followed immediately by STD Y+N, Rx ─────────

    [Fact]
    public void LddStd_SameOffsetAndReg_StdEliminated()
    {
        // LDD R20, Y+3 ; STD Y+3, R20 → STD is a no-op (memory unchanged)
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDD", "R20", "Y+3"),
            AvrAsmLine.MakeInstruction("STD", "Y+3", "R20"));

        Assert.DoesNotContain(result, l =>
            l.Mnemonic == "STD" && l.Op1 == "Y+3" && l.Op2 == "R20");
    }

    [Fact]
    public void LddStd_DifferentReg_StdKept()
    {
        // LDD R20, Y+3 ; STD Y+3, R22 → R22 ≠ R20, so STD is NOT a no-op.
        var result = Opt(
            AvrAsmLine.MakeInstruction("LDD", "R20", "Y+3"),
            AvrAsmLine.MakeInstruction("STD", "Y+3", "R22"));

        Assert.Contains(result, l =>
            l.Mnemonic == "STD" && l.Op1 == "Y+3" && l.Op2 == "R22");
    }

    // ─── RJMP to immediately following label ─────────────────────────────────

    [Fact]
    public void RjmpToNextLabel_Eliminated()
    {
        // RJMP L_end ; L_end: → the jump is to the very next label (no-op)
        var result = Opt(
            AvrAsmLine.MakeInstruction("RJMP", "L_end"),
            AvrAsmLine.MakeLabel("L_end"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.DoesNotContain(result, l => l.Mnemonic == "RJMP" && l.Op1 == "L_end");
    }

    [Fact]
    public void RjmpToNextLabel_WithInterveningComments_Eliminated()
    {
        // Comments between RJMP and its target label must not block the optimisation.
        var result = Opt(
            AvrAsmLine.MakeInstruction("RJMP", "L_skip"),
            AvrAsmLine.MakeComment("some comment"),
            AvrAsmLine.MakeLabel("L_skip"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.DoesNotContain(result, l => l.Mnemonic == "RJMP" && l.Op1 == "L_skip");
    }

    [Fact]
    public void RjmpToNonNextLabel_Kept()
    {
        // RJMP main targets a label that is not the immediately following one.
        var result = Opt(
            AvrAsmLine.MakeInstruction("RJMP", "main"),
            AvrAsmLine.MakeInstruction("LDI", "R24", "1"),
            AvrAsmLine.MakeLabel("main"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.Contains(result, l => l.Mnemonic == "RJMP" && l.Op1 == "main");
    }

    // ─── Park/unpark round trip across a runtime call ───────────────────────

    [Fact]
    public void ParkReadAgainAfterCall_ParkKept()
    {
        // `(s + 3) ** 3`: the base is parked in R16:R17 and read by both multiplies. The call
        // MAY clobber R16 but is not a redefinition, so the park stays live past it. Treating
        // the CALL as the kill rewrote the park into `MOV R18,R24` and the second multiply read
        // whatever R16 held (2295 instead of 27).
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R17", "R25"),
            AvrAsmLine.MakeInstruction("MOV", "R18", "R16"),
            AvrAsmLine.MakeInstruction("MOV", "R19", "R17"),
            AvrAsmLine.MakeInstruction("CALL", "__mul32"),
            AvrAsmLine.MakeInstruction("MOV", "R18", "R16"),
            AvrAsmLine.MakeInstruction("MOV", "R19", "R17"),
            AvrAsmLine.MakeInstruction("CALL", "__mul32"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.Contains(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
        Assert.Contains(result, l => l.Mnemonic == "MOV" && l.Op1 == "R17" && l.Op2 == "R25");
    }

    [Fact]
    public void ParkRedefinedAfterCall_RoundTripCollapsed()
    {
        // Past the call the park is overwritten before any read, so it is dead and the
        // round trip still collapses into a direct move.
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R18", "R16"),
            AvrAsmLine.MakeInstruction("CALL", "__mul32"),
            AvrAsmLine.MakeInstruction("LDI", "R16", "5"),
            AvrAsmLine.MakeInstruction("STS", "0x0100", "R16"),
            AvrAsmLine.MakeInstruction("RET"));

        Assert.Contains(result, l => l.Mnemonic == "MOV" && l.Op1 == "R18" && l.Op2 == "R24");
        Assert.DoesNotContain(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
    }

    [Fact]
    public void ParkPastCallToAFunction_RoundTripCollapsed()
    {
        // The allocator never keeps a temp across an IR Call, so the codegen names its targets
        // and a CALL to one of them is the park's redefinition: the collapse stays free there.
        var lines = new List<AvrAsmLine>
        {
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R30", "R16"),
            AvrAsmLine.MakeInstruction("CALL", "user_fn"),
            AvrAsmLine.MakeInstruction("MOV", "R18", "R24"),
            AvrAsmLine.MakeInstruction("RET"),
        };

        var collapsed = AvrPeephole.Optimize(lines, clobberingCallTargets: new HashSet<string> { "user_fn" });
        Assert.Contains(collapsed, l => l.Mnemonic == "MOV" && l.Op1 == "R30" && l.Op2 == "R24");

        // Without the name the same CALL is only a may-write: nothing past it proves the park
        // dead, so it is kept.
        var kept = AvrPeephole.Optimize(lines);
        Assert.Contains(kept, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
    }

    [Fact]
    public void ParkPastCallToAnOutlinedRegion_ParkKept()
    {
        // An outlined region is a lifted piece of the caller: it may read the temp the caller
        // left in R16, so the park stays even when a later write would otherwise prove it dead.
        var lines = new List<AvrAsmLine>
        {
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R30", "R16"),
            AvrAsmLine.MakeInstruction("RCALL", "_pymcu_outline_0"),
            AvrAsmLine.MakeInstruction("LDI", "R16", "5"),
            AvrAsmLine.MakeInstruction("STS", "0x0100", "R16"),
            AvrAsmLine.MakeInstruction("RET"),
            AvrAsmLine.MakeLabel("_pymcu_outline_0"),
            AvrAsmLine.MakeInstruction("STS", "0x0101", "R16"),
            AvrAsmLine.MakeInstruction("RET"),
        };

        var result = AvrPeephole.Optimize(lines, outlinedSubroutines: new HashSet<string> { "_pymcu_outline_0" });
        Assert.Contains(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
    }

    // ─── Dead temp-register (R16/R17) move elimination vs. call arguments ───

    [Fact]
    public void DeadTempMove_NeverReadBeforeReturn_Removed()
    {
        // Baseline: a MOV into R16 that is never read again before the function returns
        // is dead regardless of calls, and the pass still removes it.
        var result = AvrPeephole.Optimize(new List<AvrAsmLine>
        {
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("RET"),
        });

        Assert.DoesNotContain(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
    }

    [Fact]
    public void DeadTempMove_FeedsAClobberingCallTargetsArgument_Kept()
    {
        // _f32_emit_digits(out, pos, xd, nd, start, count): six parameters, the fifth
        // (start) assigned to R16 by AssignArgLocations/ArgBaseRegs once a function has
        // five or more <=2-byte register arguments. The first call below passes a
        // compile-time literal for `start` (LDI, which this pass never touches); the
        // second passes a variable living in its allocator home R12 (a MOV into R16). A
        // CALL to a clobberingCallTargets member -- every IR Call target, see AvrCodeGen
        // CompileCall -- may read R16/R17 as that argument, so the second MOV must survive
        // past the call. Before the fix, this pass only recognised outlined regions as
        // able to read the temps and treated every other CALL/RCALL as not reading them,
        // so it saw no read of R16 between the MOV and the function's end and deleted it --
        // dropping the second call's `start` argument.
        var lines = new List<AvrAsmLine>
        {
            AvrAsmLine.MakeInstruction("LDI", "R16", "0"),            // call 1: start = 0 (literal)
            AvrAsmLine.MakeInstruction("RCALL", "_f32_emit_digits"),
            AvrAsmLine.MakeInstruction("MOV", "R16", "R12"),           // call 2: start = decpt (home R12)
            AvrAsmLine.MakeInstruction("RCALL", "_f32_emit_digits"),
            AvrAsmLine.MakeInstruction("RET"),
        };

        var result = AvrPeephole.Optimize(lines,
            clobberingCallTargets: new HashSet<string> { "_f32_emit_digits" });

        Assert.Contains(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R12");
    }

    [Fact]
    public void DeadTempMove_OnlyFeedsAFixedAbiRuntimeCall_StillRemoved()
    {
        // The fixed-ABI math runtime (__mul32, __div8/16/32, ...) never takes an R16/R17
        // argument and preserves them across the call, so it is NOT a clobberingCallTargets
        // member. A MOV into R16 that only precedes such a call, and is never read again,
        // is still genuinely dead and must still be removed -- the fix must not make every
        // CALL/RCALL conservative, only the ones that can actually reach R16/R17.
        var result = AvrPeephole.Optimize(new List<AvrAsmLine>
        {
            AvrAsmLine.MakeInstruction("MOV", "R16", "R24"),
            AvrAsmLine.MakeInstruction("CALL", "__mul32"),
            AvrAsmLine.MakeInstruction("RET"),
        });

        Assert.DoesNotContain(result, l => l.Mnemonic == "MOV" && l.Op1 == "R16" && l.Op2 == "R24");
    }

    // ─── 3-window: MOV Ra, Rb ; OP Ra ; MOV Rb, Ra → OP Rb ; MOV Ra, Rb ─────

    [Fact]
    public void MovIncMov_CollapsedToIncOnSource()
    {
        // MOV R24, R4 ; INC R24 ; MOV R4, R24 → INC R4 ; MOV R24, R4
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R24", "R4"),
            AvrAsmLine.MakeInstruction("INC", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R4", "R24"));

        // INC must now target R4 (the original source register)
        Assert.Contains(result, l => l.Mnemonic == "INC" && l.Op1 == "R4");
        // The original copy-out (MOV R4, R24) must be gone — it was replaced by MOV R24, R4
        Assert.DoesNotContain(result, l =>
            l.Mnemonic == "MOV" && l.Op1 == "R4" && l.Op2 == "R24");
    }

    [Fact]
    public void MovDecMov_CollapsedToDecOnSource()
    {
        // MOV R24, R5 ; DEC R24 ; MOV R5, R24 → DEC R5 ; MOV R24, R5
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R24", "R5"),
            AvrAsmLine.MakeInstruction("DEC", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R5", "R24"));

        Assert.Contains(result, l => l.Mnemonic == "DEC" && l.Op1 == "R5");
    }

    [Fact]
    public void MovComMov_CollapsedToComOnSource()
    {
        // COM (bitwise complement)
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R24", "R6"),
            AvrAsmLine.MakeInstruction("COM", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R6", "R24"));

        Assert.Contains(result, l => l.Mnemonic == "COM" && l.Op1 == "R6");
    }

    [Fact]
    public void MovNegMov_CollapsedToNegOnSource()
    {
        var result = Opt(
            AvrAsmLine.MakeInstruction("MOV", "R24", "R7"),
            AvrAsmLine.MakeInstruction("NEG", "R24"),
            AvrAsmLine.MakeInstruction("MOV", "R7", "R24"));

        Assert.Contains(result, l => l.Mnemonic == "NEG" && l.Op1 == "R7");
    }

    // ─── Idempotency & edge cases ─────────────────────────────────────────────

    [Fact]
    public void EmptyInput_ReturnsEmpty()
        => Assert.Empty(AvrPeephole.Optimize(new List<AvrAsmLine>()));

    [Fact]
    public void CommentsAndRaw_PassThrough()
    {
        var result = Opt(
            AvrAsmLine.MakeComment("hello"),
            AvrAsmLine.MakeRaw(".equ RAMSTART, 0x0100"));

        Assert.Contains(result, l => l.Type == AvrAsmLine.LineType.Comment);
        Assert.Contains(result, l => l.Type == AvrAsmLine.LineType.Raw);
    }

    [Fact]
    public void AvrAsmLine_ToString_Instruction_NoOps()
    {
        var line = AvrAsmLine.MakeInstruction("RET");
        Assert.Equal("\tRET", line.ToString());
    }

    [Fact]
    public void AvrAsmLine_ToString_Instruction_OneOp()
    {
        var line = AvrAsmLine.MakeInstruction("RJMP", "main");
        Assert.Equal("\tRJMP\tmain", line.ToString());
    }

    [Fact]
    public void AvrAsmLine_ToString_Instruction_TwoOps()
    {
        var line = AvrAsmLine.MakeInstruction("LDI", "R24", "42");
        Assert.Equal("\tLDI\tR24, 42", line.ToString());
    }

    [Fact]
    public void AvrAsmLine_ToString_Label()
    {
        var line = AvrAsmLine.MakeLabel("main");
        Assert.Equal("main:", line.ToString());
    }

    [Fact]
    public void AvrAsmLine_ToString_Comment()
    {
        var line = AvrAsmLine.MakeComment("Generated by pymcuc");
        Assert.Equal("; Generated by pymcuc", line.ToString());
    }

    [Fact]
    public void AvrAsmLine_ToString_Empty_IsEmptyString()
    {
        var line = AvrAsmLine.MakeEmpty();
        Assert.Equal("", line.ToString());
    }
}
