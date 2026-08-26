using PyMCU.Backend.Targets.AVR;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;
using IrBinaryOp = PyMCU.IR.BinaryOp;

namespace PyMCU.UnitTests;

/// <summary>
/// pymcu-avr#5, second half. A backend refusal reached the user as bare text with no source
/// position, and for a multiply rewritten into a shift it read "Float comparison op LShift
/// not supported": the word comparison for something that was a multiply, and an operator the
/// program never contained.
///
/// The CLI prints only the exception message unless -v is passed, so everything the reader
/// needs has to be in the message. It now carries the last non-inline source position and
/// says which pass to suspect.
/// </summary>
public class AvrRefusalDiagnosticTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    private static string Refusal(params Instruction[] body)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        prog.Device ??= ChipCatalog.For(Atmega328p.Chip);
        var ex = Assert.ThrowsAny<Exception>(() =>
            new AvrCodeGen(Atmega328p).Compile(prog, new StringWriter()));
        return ex.Message;
    }

    // A float shift is only ever produced by a pass rewriting a float multiply, so the
    // message has to send the reader at that pass rather than at their own arithmetic.
    [Fact]
    public void AFloatShift_IsRefusedWithTheSourcePositionAndTheLikelyCause()
    {
        var msg = Refusal(
            new DebugLine(8, "    v = fb * 2", "main.py"),
            new Binary(IrBinaryOp.LShift, new Variable("fb", DataType.FLOAT),
                       new Constant(1), new Variable("v", DataType.FLOAT)),
            new Return(new Constant(0)));

        Assert.Contains("main.py:8", msg);
        Assert.Contains("LShift", msg);
        Assert.Contains("rewrote a float operation", msg);
        Assert.DoesNotContain("comparison", msg);
    }

    // Without a source position the message still has to say what happened; it just cannot
    // say where. The old text could not say either.
    [Fact]
    public void WithNoDebugLine_TheRefusalStillNamesTheOperator()
    {
        var msg = Refusal(
            new Binary(IrBinaryOp.LShift, new Variable("fb", DataType.FLOAT),
                       new Constant(1), new Variable("v", DataType.FLOAT)),
            new Return(new Constant(0)));

        Assert.Contains("LShift", msg);
        Assert.DoesNotContain(":0:", msg);
    }

    // An inline expansion's line points into the stdlib, so it must not overwrite the
    // position the reader can actually open.
    [Fact]
    public void AnInlineDebugLine_DoesNotReplaceTheUsersPosition()
    {
        var msg = Refusal(
            new DebugLine(8, "    v = fb * 2", "main.py"),
            new DebugLine(214, "    return x", "uart.py") { IsInline = true },
            new Binary(IrBinaryOp.LShift, new Variable("fb", DataType.FLOAT),
                       new Constant(1), new Variable("v", DataType.FLOAT)),
            new Return(new Constant(0)));

        Assert.Contains("main.py:8", msg);
        Assert.DoesNotContain("uart.py", msg);
    }
}
