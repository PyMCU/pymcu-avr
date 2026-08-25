using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A list field in a class (PyMCU#106).
///
/// Both spellings used to fail with a message about something the program had not written:
/// `self.buf: list[uint8] = [0, 0, 0]` reported "Array size 'uint8' is not a compile-time
/// constant" (the bracket in list[T] holds the element type, not a size), and the bare
/// `self.buf = [0, 0, 0]` reported "Unknown Expression type: ListExpr". The same list as a
/// function local compiled and ran, so the restriction was on the field position.
///
/// A list field is the fixed array its literal describes. The element type comes from the
/// annotation, or -- unannotated -- from the widest literal, which is why the uint16 case
/// asserts BOTH bytes: an element type picked as uint8 would drop the high one.
///
/// Values are seeded into GPIOR0/GPIOR1 before each run, so what is measured is the field.
/// </summary>
[TestFixture]
public class ListFieldTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("list-field"));

    // Runs to the n-th BREAK and returns what the program left in GPIOR2 there.
    private byte Checkpoint(int n, byte seed0, byte seed1)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed0;
        uno.Data[Gpior1Addr] = seed1;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
        }
        return uno.Data[Gpior2Addr];
    }

    [TestCase((byte)7, (byte)200)]
    [TestCase((byte)200, (byte)7)]
    public void AnnotatedListField_KeepsWhatWasStoredInIt(byte seed0, byte seed1)
    {
        Checkpoint(1, seed0, seed1).Should().Be(seed0, "self.buf: list[uint8] element 0");
        Checkpoint(2, seed0, seed1).Should().Be((byte)(seed1 + 1), "element 2, so the field is at least three long");
    }

    [TestCase((byte)7, (byte)200)]
    [TestCase((byte)200, (byte)7)]
    public void UnannotatedListField_KeepsWhatWasStoredInIt(byte seed0, byte seed1)
        => Checkpoint(3, seed0, seed1).Should().Be(seed1, "self.buf = [0, 0, 0] element 1");

    // 300 + seed does not fit in a byte. Both halves are asserted because a field that
    // silently became uint8 would return the low byte and look right in a one-byte check.
    [TestCase((byte)7)]
    [TestCase((byte)200)]
    public void Uint16ListField_KeepsBothBytes(byte seed0)
    {
        int expected = 300 + seed0;
        Checkpoint(4, seed0, 0).Should().Be((byte)(expected >> 8), "high byte of 300 + seed");
        Checkpoint(5, seed0, 0).Should().Be((byte)(expected & 0xFF), "low byte of 300 + seed");
    }

    // The size has to come from somewhere, and list[T] has no room for it. Without a literal
    // the declaration is refused by name instead of by an internal one.
    [Test]
    public void AListFieldWithNoLiteral_SaysWhereItsLengthWouldComeFrom()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "from pymcu.types import uint8\n" +
            "\n" +
            "\n" +
            "class A:\n" +
            "    def __init__(self):\n" +
            "        self.buf: list[uint8]\n" +
            "\n" +
            "\n" +
            "def main():\n" +
            "    a = A()\n" +
            "\n" +
            "\n" +
            "main()\n"));

        ex!.Message.Should().Contain("self.buf: list[uint8]", "the message must quote what was written");
        ex.Message.Should().Contain("[0, 0, 0]", "and show the spelling that has a length");
    }
}
