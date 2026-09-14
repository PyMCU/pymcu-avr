using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Indexing an unannotated comprehension with a run-time value (PyMCU#64, PyMCU#317).
///
/// This was refused twice over, and each refusal was an improvement on the last. The oldest
/// message ("Array subscript must be a compile-time constant") blamed the subscript, which was
/// never the problem: the same subscript on an annotated array compiles. Then it named the
/// array and the annotation that makes it indexable.
///
/// Now it is not refused at all. The elements are constants and nothing writes them, so the
/// comprehension becomes a table in flash at the run-time subscript that needs it (PyMCU#317),
/// and the annotated spelling remains the one to write when the array IS written.
/// </summary>
[TestFixture]
public class UnrolledArrayIndexTests
{
    // PB0 is driven by the test, so `n` is decided on the chip and nothing folds. `xs` is
    // [0, 2, 4, 6], computed by CPython from the same comprehension, so the program prints 0
    // with PB0 low and 2 with PB0 high.
    private const string RuntimeIndexOfComprehension =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    n: uint8 = p.value()\n" +
        "    xs = [i * 2 for i in range(4)]\n" +
        "    print(xs[n])\n" +
        "    print(\"END\")\n";

    private const string Annotated =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    n: uint8 = p.value()\n" +
        "    xs: uint8[4] = [i * 2 for i in range(4)]\n" +
        "    print(xs[n])\n";

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildSource(RuntimeIndexOfComprehension));

    private string Transcript(bool pb0High)
    {
        var uno = _session.Reset();
        uno.PortB.SetPinValue(0, pb0High);
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    // The subscript is decided on the chip: PB0 selects element 0 or element 1 of a
    // comprehension nobody annotated and nobody writes, which now lives in flash (PyMCU#317).
    [TestCase(false, 0)]
    [TestCase(true, 2)]
    public void UnannotatedComprehension_ReadsTheRightElementAtRunTime(bool pb0High, int expected)
        => Transcript(pb0High).Should().StartWith($"{expected}\n",
            "xs is [0, 2, 4, 6] and PB0 picks the index");

    [Test]
    public void AnnotatedComprehension_StillCompiles()
    {
        PymcuCompiler.BuildSource(Annotated).Should().NotBeEmpty(
            "the annotated spelling is the one the message points at, so it must work");
    }
}
