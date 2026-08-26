using PyMCU.Backend.Targets.AVR;
using PyMCU.Common;
using PyMCU.Common.Models;
using PyMCU.IR;
using Xunit;
using IrBinaryOp = PyMCU.IR.BinaryOp;

namespace PyMCU.UnitTests;

/// <summary>
/// pymcu-avr#13. An 8-bit register read in a wider context loaded TWO bytes from the
/// register's address, so the register ONE BYTE ABOVE arrived as the high half:
/// `d: uint16 = GPIOR1.value` emitted `IN R24, 0x2A` then `LDS R25, 0x004B`, which is
/// GPIOR2, and the same shape on GPIOR0 read SREG.
///
/// The IR is not at fault. It asks for a one-byte read widened into a wider destination
/// (`mem` carries type 0, the destination type 2), which is what every other source kind
/// in LoadIntoReg already honours.
///
/// Addresses used below, on atmega328p: GPIOR1 = 0x4A (I/O 0x2A), GPIOR2 = 0x4B,
/// TCNT1 = 0x84 and is genuinely 16 bit.
/// </summary>
public class AvrRegisterWidthTests
{
    private static readonly DeviceConfig Atmega328p = new() { Chip = "atmega328p", Arch = "avr" };

    private static string Compile(params Instruction[] body)
    {
        var prog = new ProgramIR();
        prog.Functions.Add(new Function { Name = "main", Body = body.ToList() });
        prog.Device ??= ChipCatalog.For(Atmega328p.Chip);
        var sw = new StringWriter();
        new AvrCodeGen(Atmega328p).Compile(prog, sw);
        return sw.ToString();
    }

    private const string Gpior2Load = "LDS\tR25, 0x004B";

    // ─── The defect ───────────────────────────────────────────────────────

    [Fact]
    public void EightBitRegisterIntoSixteen_ClearsTheHighByteInsteadOfReadingTheNeighbour()
    {
        var asm = Compile(
            new Copy(new MemoryAddress(0x4A, DataType.UINT8), new Variable("d", DataType.UINT16)),
            new Return(new Constant(0)));

        Assert.Contains("IN\tR24, 0x2A", asm);
        Assert.Contains("CLR\tR25", asm);
        Assert.DoesNotContain(Gpior2Load, asm);
    }

    [Fact]
    public void EightBitRegisterIntoThirtyTwo_DoesNotReadAnyNeighbour()
    {
        var asm = Compile(
            new Copy(new MemoryAddress(0x4A, DataType.UINT8), new Variable("q", DataType.UINT32)),
            new Return(new Constant(0)));

        Assert.DoesNotContain(Gpior2Load, asm);
        Assert.DoesNotContain("0x004C", asm);
        Assert.DoesNotContain("0x004D", asm);
    }

    // The widening happens inside the operand load, so an arithmetic use is the same defect
    // reached by another route. `100 + GPIOR1.value` is the shape in the issue.
    [Fact]
    public void EightBitRegisterAsAnOperandOfWiderArithmetic_DoesNotReadTheNeighbour()
    {
        var asm = Compile(
            new Binary(IrBinaryOp.Add, new MemoryAddress(0x4A, DataType.UINT8),
                       new Constant(100), new Variable("w", DataType.UINT16)),
            new Return(new Constant(0)));

        Assert.DoesNotContain(Gpior2Load, asm);
    }

    // An INT8-typed register operand widens by ZERO extension, not sign extension, because
    // GetValType maps every one-byte MemoryAddress to UINT8 and the signedness is gone before
    // the widening is chosen. That predates this fix and is unchanged by it; the chip files
    // declare registers as ptr[uint8] / ptr[uint16], so no INT8 register operand is built
    // today. Pinned as the current answer, not as the desirable one: what matters here is
    // that it does not read the neighbour either way.
    [Fact]
    public void SignedEightBitRegister_DoesNotReadTheNeighbourAndZeroExtends()
    {
        var asm = Compile(
            new Copy(new MemoryAddress(0x4A, DataType.INT8), new Variable("s", DataType.INT16)),
            new Return(new Constant(0)));

        Assert.DoesNotContain(Gpior2Load, asm);
        Assert.Contains("CLR\tR25", asm);
    }

    // ─── Invariants: what must NOT change ─────────────────────────────────

    // The whole point of reading two bytes is right when the register IS two bytes. A fix
    // that simply stopped loading the high byte would break every 16-bit peripheral
    // register, and this is the test that says so.
    [Fact]
    public void GenuinelySixteenBitRegister_StillLoadsBothBytes()
    {
        var asm = Compile(
            new Copy(new MemoryAddress(0x84, DataType.UINT16), new Variable("t", DataType.UINT16)),
            new Return(new Constant(0)));

        Assert.Contains("LDS\tR24, 0x0084", asm);
        Assert.Contains("LDS\tR25, 0x0085", asm);
    }

    [Fact]
    public void EightBitRegisterIntoEightBit_IsUnchanged()
    {
        var asm = Compile(
            new Copy(new MemoryAddress(0x4A, DataType.UINT8), new Variable("t", DataType.UINT8)),
            new Return(new Constant(0)));

        Assert.Contains("IN\tR24, 0x2A", asm);
        Assert.DoesNotContain(Gpior2Load, asm);
    }

    // Writing is a separate direction and was never part of the defect; pinned so a change
    // to the load side cannot quietly narrow the store side.
    [Fact]
    public void WritingASixteenBitRegister_StillStoresBothBytes()
    {
        var asm = Compile(
            new Copy(new Constant(300), new MemoryAddress(0x84, DataType.UINT16)),
            new Return(new Constant(0)));

        Assert.Contains("STS\t0x0084", asm);
        Assert.Contains("STS\t0x0085", asm);
    }
}
