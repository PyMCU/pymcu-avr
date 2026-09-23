using System.Text.Json;
using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// Unit tests for the PGO register-priority consumer (pymcuc-avr --profile):
/// AvrRegisterAllocator orders the R2-R15 named-variable homes by each
/// variable's uses weighted with the profiled execution count of the MIR block
/// containing them, instead of the static IR use count. Only the order changes;
/// eligibility (size, GC/funcref, address-taken) is untouched.
/// </summary>
public class AvrRegisterPriorityTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    // 'cold' collects 7 uses in the entry block (runs once); 'hot' collects 4
    // uses inside L_1. Statically cold wins the lower home; a profile where
    // L_1 ran 1000 times flips the order.
    private static ProgramIR MakeProgram()
    {
        var prog = new ProgramIR();
        var cold = new Variable("cold");
        var hot = new Variable("hot");
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body =
            {
                new Copy(new Constant(1), cold),
                new Copy(new Constant(2), hot),
                new Binary(BinaryOp.Add, cold, new Constant(1), cold),
                new Binary(BinaryOp.Add, cold, new Constant(1), cold),
                new Binary(BinaryOp.Add, cold, new Constant(1), cold),
                new Label("L_1"),
                new Binary(BinaryOp.Add, hot, new Constant(1), hot),
                new JumpIfNotZero(hot, "L_1"),
                new Return(new Constant(0)),
            },
        });
        return prog;
    }

    private static Dictionary<string, ulong> Counts(params (string label, ulong count)[] blocks) =>
        blocks.ToDictionary(b => b.label, b => b.count);

    [Fact]
    public void NoProfile_StaticOrderAssigns()
    {
        var homes = AvrRegisterAllocator.Allocate(MakeProgram());
        Assert.Equal("R2", homes["cold"]);   // 7 uses
        Assert.Equal("R3", homes["hot"]);    // 4 uses
    }

    [Fact]
    public void Profile_DynamicOrderAssigns()
    {
        // L_1 ran 1000x: hot's 3 in-block uses outweigh cold's 7 entry-block uses.
        var homes = AvrRegisterAllocator.Allocate(MakeProgram(), Counts(("L_1", 1000)));
        Assert.Equal("R2", homes["hot"]);
        Assert.Equal("R3", homes["cold"]);
    }

    [Fact]
    public void Profile_UnknownLabels_FallBackToStatic()
    {
        // A foreign profile names no block this program contains: every block
        // keeps weight 1 and the allocation is byte-identical to static order.
        var foreign = AvrRegisterAllocator.Allocate(MakeProgram(),
            Counts(("L_NOPE", 99999), ("someone_else", 42)));
        var plain = AvrRegisterAllocator.Allocate(MakeProgram());
        Assert.Equal(plain, foreign);
    }

    [Fact]
    public void Profile_UnprofiledBlocksKeepWeightOne()
    {
        // A variable seen only in an unprofiled block still competes: 'cold' is
        // entirely in the entry block, which L_1-only profiles never mention,
        // and it must still win a home over nothing.
        var homes = AvrRegisterAllocator.Allocate(MakeProgram(), Counts(("L_1", 3)));
        Assert.True(homes.ContainsKey("cold"));
        Assert.True(homes.ContainsKey("hot"));
        // hot = 1 (entry) + 3 in-block uses x 3 = 10 > cold's 7 -- the profiled
        // block still decides the order at small counts.
        Assert.Equal("R2", homes["hot"]);
    }

    [Fact]
    public void GcRefSeenAsUint16_NeverRegisterHomed()
    {
        // A GC_REF variable is also viewed as UINT16 by the address arithmetic
        // the IR emits for element loads (`lst + i*2`). If the u16 view is the
        // last one CountVal records, the variable becomes eligible for an
        // R2-R15 home -- and a register-cached heap pointer goes stale the
        // moment compaction moves the object. The GC_REF view must stick.
        var prog = new ProgramIR();
        var lst = new Variable("lst", DataType.GC_REF);
        prog.Functions.Add(new Function
        {
            Name = "main",
            Body =
            {
                new Copy(new Constant(0), lst),                                  // GC_REF view
                new Binary(BinaryOp.Add,
                    new Variable("lst", DataType.UINT16),
                    new Constant(2), new Temporary("t0", DataType.UINT16)), // u16 view wins last write
                new Return(new Constant(0)),
            },
        });
        var homes = AvrRegisterAllocator.Allocate(prog);
        Assert.False(homes.ContainsKey("lst"));
    }

    [Fact]
    public void Compile_ProfilePath_ChangesEmittedAsm()
    {
        var asmPlain = Compile(null);
        var asmPgo = Compile(WriteProfile(Counts(("L_1", 1000))));
        Assert.NotEqual(asmPlain, asmPgo);
    }

    [Fact]
    public void Compile_ForeignProfile_EmitsIdenticalAsm()
    {
        var asmPlain = Compile(null);
        var asmForeign = Compile(WriteProfile(Counts(("L_FOREIGN", 99999))));
        Assert.Equal(asmPlain, asmForeign);
    }

    private static string WriteProfile(Dictionary<string, ulong> counts)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pgo-prof-{Guid.NewGuid():N}.json");
        var doc = new
        {
            format = 1,
            chip = "atmega328p",
            freq = 16_000_000UL,
            blocks = counts.ToDictionary(kv => kv.Key, kv => new { count = kv.Value, cycles = kv.Value * 4UL }),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(doc));
        return path;
    }

    private static string Compile(string? profilePath)
    {
        var prog = MakeProgram();
        prog.Device = ChipCatalog.For(Atmega328p.Chip);
        var codegen = new AvrCodeGen(Atmega328p) { ProfilePath = profilePath };
        var sw = new StringWriter();
        codegen.Compile(prog, sw);
        return sw.ToString();
    }
}
