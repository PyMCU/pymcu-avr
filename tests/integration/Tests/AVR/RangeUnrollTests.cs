using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `for` over a constant range unrolls (PyMCU#79).
///
/// The plain spelling files its bounds in RangeStart/RangeStop/RangeStep and leaves Iterable
/// null, and the unrolling in Iteration.cs only ran for range() as an iterable expression --
/// the shape enumerate() and zip() build. So `for p in range(11, 14)` compiled to a real loop
/// and its variable never qualified where a compile-time constant is required, while
/// `pins = [11, 12, 13]` then `for p in pins:` compiled.
///
/// Every assertion below goes through a loop variable used as a register bit index, which does
/// not compile at all unless the loop unrolled. The bits come from GPIOR0, seeded per run.
/// </summary>
[TestFixture]
public class RangeUnrollTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-unroll"));

    private byte Checkpoint(int n, byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
        }
        return uno.Data[Gpior2Addr];
    }

    [TestCase((byte)0b101, (byte)0b101)]
    [TestCase((byte)0b010, (byte)0b010)]
    [TestCase((byte)0b11111000, (byte)0b000)]
    public void AscendingRange_BindsEachIndex(byte seed, byte expected)
        => Checkpoint(1, seed).Should().Be(expected, "range(3) writes GPIOR2 bits 0-2 from seed bits 0-2");

    // Descending, so an unroll that walked the range in the wrong direction -- or only ran
    // once -- lands the bits somewhere else.
    [TestCase((byte)0b101, (byte)0b10100000)]
    [TestCase((byte)0b010, (byte)0b01000000)]
    public void DescendingRange_BindsEachIndex(byte seed, byte expected)
        => Checkpoint(2, seed).Should().Be(expected, "range(7, 4, -1) writes bits 7,6,5 from seed bits 2,1,0");

    [TestCase((byte)0b1011, (byte)0b1011)]
    [TestCase((byte)0b0110, (byte)0b0110)]
    public void NamedConstantBound_UnrollsToo(byte seed, byte expected)
        => Checkpoint(3, seed).Should().Be(expected, "range(WIDTH) with WIDTH = 4 is as constant as range(4)");

    // The shape the issue was written about: three board pins from a range, which is what
    // every "walk the pins" program types first.
    [Test]
    public void ARangeOfPinNumbers_Compiles()
        => PymcuCompiler.BuildSource(
            "from pymcu.hal.gpio import Pin\n" +
            "\n" +
            "\n" +
            "def main():\n" +
            "    for p in range(11, 14):\n" +
            "        Pin(p, Pin.OUT).value(1)\n" +
            "\n" +
            "\n" +
            "main()\n").Should().NotBeEmpty();

    // Past the cap the loop stays a loop, on purpose: unrolling copies the body once per
    // iteration, and the cap is the one a constant list literal already lives under. The
    // diagnostic a const parameter gives must say where the line falls, since both sides of
    // it are now reachable.
    [Test]
    public void ARangeLongerThanTheCap_StaysALoopAndSaysSo()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(ConstParamLoop(20)));

        ex!.Message.Should().Contain("requires a compile-time constant");
        ex.Message.Should().Contain("constant range of at most 8 steps",
            "the message has to say which ranges DO unroll, now that some of them do");
    }

    [Test]
    public void ARangeInsideTheCap_FeedsAConstParameter()
        => PymcuCompiler.BuildSource(ConstParamLoop(8)).Should().NotBeEmpty();

    // A const[uint8] parameter is the plainest thing that demands a compile-time constant,
    // and it is what the issue's Pin(pin_id) rejection came down to.
    private static string ConstParamLoop(int stop) =>
        "from pymcu.chips.atmega328p import GPIOR2\n" +
        "from pymcu.types import uint8, const, inline\n" +
        "\n" +
        "\n" +
        "@inline\n" +
        "def shifted(bit: const[uint8]) -> uint8:\n" +
        "    return 1 << bit\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        $"    for i in range({stop}):\n" +
        "        GPIOR2.value = shifted(i)\n" +
        "\n" +
        "\n" +
        "main()\n";
}
