using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// RFC 0009 phase 1: a runtime-Optional return transports a union-member byte
/// beside the payload -- the register after the payload in the return run
/// (R25 for u8, R22 for u16, R20 for u32/float). These tests pin the wire shape
/// the backend emits for Return.Tag and Call.TagDst.
/// </summary>
public class OptionalTagTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    private static string Compile(ProgramIR program)
    {
        program.Device ??= ChipCatalog.For("atmega328p");
        var codegen = new AvrCodeGen(Atmega328p);
        var sw = new StringWriter();
        codegen.Compile(program, sw);
        return sw.ToString();
    }

    // ─── Return.Tag: the callee side ──────────────────────────────────────

    [Fact]
    public void TaggedReturn_U8Payload_TagGoesToR25()
    {
        // def read() -> Optional[uint8]: if ...: return None / return k
        // None path: no payload load, tag byte 1 in R25.
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT8,
            ReturnMembers = new List<string> { "uint8", "None" },
            Body = new List<Instruction>
            {
                new Return(new NoneVal(), new Constant(1)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(), new NoneVal(), new NoneVal()),
            },
        });

        var asm = Compile(prog);

        Assert.Contains("LDI\tR25, 1", asm);
    }

    [Fact]
    public void TaggedReturn_U8Payload_ValuePathLoadsBoth()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT8,
            ReturnMembers = new List<string> { "uint8", "None" },
            Body = new List<Instruction>
            {
                new Return(new Constant(42), new Constant(0)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(), new NoneVal(), new NoneVal()),
            },
        });

        var asm = Compile(prog);

        Assert.Contains("LDI\tR24, 42", asm);
        Assert.Contains("LDI\tR25, 0", asm);
    }

    [Fact]
    public void TaggedReturn_U16Payload_TagGoesToR22()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT16,
            ReturnMembers = new List<string> { "int", "None" },
            Body = new List<Instruction>
            {
                new Return(new Variable("v", DataType.UINT16), new Constant(0)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(), new NoneVal(), new NoneVal()),
            },
        });

        var asm = Compile(prog);

        Assert.Contains("LDI\tR22, 0", asm);
    }

    [Fact]
    public void TaggedReturn_U32Payload_TagGoesToR20()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT32,
            ReturnMembers = new List<string> { "uint32", "None" },
            Body = new List<Instruction>
            {
                new Return(new Variable("v", DataType.UINT32), new Constant(1)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(), new NoneVal(), new NoneVal()),
            },
        });

        var asm = Compile(prog);

        Assert.Contains("LDI\tR20, 1", asm);
    }

    // ─── Call.TagDst: the caller side ─────────────────────────────────────

    [Fact]
    public void TaggedCall_U8Callee_ReadsTagFromR25()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT8,
            ReturnMembers = new List<string> { "uint8", "None" },
            Body = new List<Instruction>
            {
                new Return(new Constant(7), new Constant(0)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(),
                    new Variable("r", DataType.UINT8),
                    new Variable("r$tag", DataType.UINT8)),
                new Return(new NoneVal()),
            },
        });

        var asm = Compile(prog);

        var callAt = asm.IndexOf("CALL\tread", StringComparison.Ordinal);
        Assert.True(callAt >= 0, "expected a CALL to read");
        // The tag byte must be collected out of R25 after the call -- before any
        // later code can clobber the register.
        var tail = asm[callAt..];
        Assert.Matches(@"MOV\tR\d+, R25|STD\tY\+\d+, R25|STS\t0x[0-9A-F]+, R25", tail);
    }

    [Fact]
    public void TaggedCall_DeadPayload_StillReadsCalleeWidth()
    {
        // `if f() is None:` binds only the tag: Dst is NoneVal, so the tag
        // register comes from the callee's declared ReturnType (u16 -> R22),
        // not from GetValType(NoneVal).
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT16,
            ReturnMembers = new List<string> { "int", "None" },
            Body = new List<Instruction>
            {
                new Return(new Variable("v", DataType.UINT16), new Constant(0)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(),
                    new NoneVal(),
                    new Variable("r$tag", DataType.UINT8)),
                new Return(new NoneVal()),
            },
        });

        var asm = Compile(prog);

        var callAt = asm.IndexOf("CALL\tread", StringComparison.Ordinal);
        Assert.True(callAt >= 0, "expected a CALL to read");
        var tail = asm[callAt..];
        Assert.Matches(@"MOV\tR\d+, R22|STD\tY\+\d+, R22|STS\t0x[0-9A-F]+, R22", tail);
    }

    [Fact]
    public void UntaggedCall_EmitsNoTagMove()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT8,
            Body = new List<Instruction>
            {
                new Return(new Constant(7)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(), new Variable("r", DataType.UINT8)),
                new Return(new NoneVal()),
            },
        });

        var asm = Compile(prog);

        var callAt = asm.IndexOf("CALL\tread", StringComparison.Ordinal);
        var haltAt = asm.IndexOf("__pymcu_halt", StringComparison.Ordinal);
        Assert.True(callAt >= 0 && haltAt > callAt);
        var between = asm[callAt..haltAt];
        // The payload lands in its home; nothing touches R25 as a tag source.
        Assert.DoesNotContain("R25", between);
    }

    // ─── the tag slot is an ordinary local to the allocators ──────────────

    [Fact]
    public void TagVariable_GetsStorageLikeAnyLocal()
    {
        // v = read(); t = v$tag -- the sibling slot must exist (stack or
        // register home) or the MOV after CALL has nowhere to go.
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "read",
            ReturnType = DataType.UINT8,
            ReturnMembers = new List<string> { "uint8", "None" },
            Body = new List<Instruction>
            {
                new Return(new Constant(7), new Constant(0)),
            },
        });
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body = new List<Instruction>
            {
                new Call("read", new List<Val>(),
                    new Variable("r", DataType.UINT8),
                    new Temporary("tmp_0", DataType.UINT8)),
                new Copy(new Temporary("tmp_0", DataType.UINT8),
                    new Variable("r$tag", DataType.UINT8)),
                new Return(new NoneVal()),
            },
        });

        // A missing allocation used to surface as a KeyNotFoundException at
        // StoreRegInto/LoadIntoReg; a successful compile is the assertion.
        var asm = Compile(prog);
        Assert.Contains("CALL\tread", asm);
    }
}
