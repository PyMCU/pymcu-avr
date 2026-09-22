using PyMCU.Backend.Targets.AVR;
using PyMCU.IR;
using Xunit;

namespace PyMCU.UnitTests;

/// <summary>
/// The call-tree overlay is only sound while every array in a frame dies with the
/// call that made it. AvrEscapingArrays promotes the ones that do not into
/// ProgramIR.GlobalArrays before StackAllocator runs, so a sibling subtree can
/// never reuse their bytes.
///
/// These tests pin the promotion rules directly on ProgramIR: which references
/// mean "object lifetime" (named by two functions, address stored where a frame
/// cannot take it back) and which stay "call lifetime" (one body only, or passed
/// to a callee that never stashes the pointer).
/// </summary>
public class AvrEscapingArraysTests
{
    private static ArrayStore Init(string name, int count)
        => new(name, new Constant(0), new Constant(0), DataType.UINT8, count);

    private static Function Fn(string name, params Instruction[] body)
        => new() { Name = name, Body = body.ToList() };

    [Fact]
    public void ArrayNamedByTwoFunctions_IsPromoted()
    {
        // The Life driver shape: __init__ makes the array, a second body names it.
        var prog = new ProgramIR();
        prog.Functions.Add(Fn("main", Init("dev_buf", 64)));
        prog.Functions.Add(Fn("use", new ArrayLoad("dev_buf", new Constant(0), new Temporary("t"), DataType.UINT8, 64)));

        var promoted = AvrEscapingArrays.Apply(prog);

        Assert.Contains("dev_buf", promoted);
        Assert.Equal(64, prog.GlobalArrays["dev_buf"]);
    }

    [Fact]
    public void ArrayUsedByOneFunctionOnly_StaysLocal()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(Fn("main",
            Init("scratch", 64),
            new ArrayLoad("scratch", new Constant(0), new Temporary("t"), DataType.UINT8, 64)));

        var promoted = AvrEscapingArrays.Apply(prog);

        Assert.DoesNotContain("scratch", promoted);
        Assert.False(prog.GlobalArrays.ContainsKey("scratch"));
    }

    [Fact]
    public void AddressStoredToAGlobal_IsPromoted()
    {
        var prog = new ProgramIR();
        prog.Globals.Add(new Variable("saved"));
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new Copy(new ArrayBase("arr"), new Variable("saved"))));

        var promoted = AvrEscapingArrays.Apply(prog);

        Assert.Contains("arr", promoted);
    }

    [Fact]
    public void AddressStoredThroughAPointer_IsPromoted()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new StoreIndirect(new ArrayBase("arr"), new Variable("slot"))));

        Assert.Contains("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void AddressReturnedFromItsMaker_IsPromoted()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(Fn("make",
            Init("arr", 32),
            new Return(new ArrayBase("arr"))));

        Assert.Contains("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void AddressPassedToACalleeThatDoesNotStash_StaysLocal()
    {
        // The callee only writes through the pointer during its own frame: the
        // array's call lifetime already covers it, promotion would waste SRAM.
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "fill",
            Params = ["b"],
            Body = [new BytearrayStore("b", new Constant(0), new Constant(1))],
        });
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new Call("fill", [new ArrayBase("arr")], new NoneVal())));

        var promoted = AvrEscapingArrays.Apply(prog);

        Assert.DoesNotContain("arr", promoted);
    }

    [Fact]
    public void AddressPassedToACalleeThatStashes_IsPromoted()
    {
        var prog = new ProgramIR();
        prog.Globals.Add(new Variable("saved"));
        prog.Functions.Add(new Function
        {
            Name = "stash",
            Params = ["b"],
            Body = [new Copy(new Variable("b"), new Variable("saved"))],
        });
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new Call("stash", [new ArrayBase("arr")], new NoneVal())));

        Assert.Contains("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void AddressForwardedThroughAStashingChain_IsPromoted()
    {
        // middle(b) forwards its parameter to stash(b); the leak must propagate
        // back through the call chain to reach main's array.
        var prog = new ProgramIR();
        prog.Globals.Add(new Variable("saved"));
        prog.Functions.Add(new Function
        {
            Name = "stash",
            Params = ["b"],
            Body = [new Copy(new Variable("b"), new Variable("saved"))],
        });
        prog.Functions.Add(new Function
        {
            Name = "middle",
            Params = ["b"],
            Body = [new Call("stash", [new Variable("b")], new NoneVal())],
        });
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new Call("middle", [new ArrayBase("arr")], new NoneVal())));

        Assert.Contains("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void AddressPassedToAnUnknownCallee_IsPromoted()
    {
        var prog = new ProgramIR();
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new IndirectCall(new Variable("fp"), [new ArrayBase("arr")], new NoneVal())));

        Assert.Contains("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void ParameterReturnedUnmodified_DoesNotLeakIt()
    {
        // Returning a parameter hands it to the frame that already owns the
        // array; nothing about the array's lifetime changes, so nothing is
        // promoted -- the SRAM stays overlayable.
        var prog = new ProgramIR();
        prog.Functions.Add(new Function
        {
            Name = "ident",
            Params = ["b"],
            Body = [new Return(new Variable("b"))],
        });
        prog.Functions.Add(Fn("main",
            Init("arr", 32),
            new Call("ident", [new ArrayBase("arr")], new Temporary("r"))));

        Assert.DoesNotContain("arr", AvrEscapingArrays.Apply(prog));
    }

    [Fact]
    public void ArrayAlreadyGlobal_IsUntouched()
    {
        var prog = new ProgramIR();
        prog.GlobalArrays["cells"] = 256;
        prog.Functions.Add(Fn("main", Init("cells", 256)));
        prog.Functions.Add(Fn("step", Init("cells", 256)));

        var promoted = AvrEscapingArrays.Apply(prog);

        Assert.DoesNotContain("cells", promoted);
        Assert.Equal(256, prog.GlobalArrays["cells"]);
    }
}
