using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-rotaryio (pymcu-circuitpython#12): rotaryio.IncrementalEncoder says
/// where a two-track knob has turned to. Eight line changes are two detents of a common
/// knob, and the position has to read 2.
///
/// This is also the regression test for PyMCU#328. The first version of the HAL decoded
/// correctly and still read zero for ever, because a global shared between an interrupt
/// handler and the main program was allocated in the callee-saved pool R2-R15 and every
/// handler epilogue restored it, undoing the write the handler had just made.
/// </summary>
[TestFixture]
public class CompatCpRotaryioTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const byte ABit = 2;   // D2, the encoder's A line
    private const byte BBit = 3;   // D3, the encoder's B line

    // One detent, one line changing at a time. A knob idles with both lines released, which
    // with the pull-ups on reads high, so a detent starts and ends at (true, true).
    private static readonly (bool A, bool B)[] Forward =
    {
        (true, false), (false, false), (false, true), (true, true),
    };

    private static readonly (bool A, bool B)[] Backward =
    {
        (false, true), (false, false), (true, false), (true, true),
    };

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-rotaryio"));

    /// <summary>
    /// Turns the knob `forward` detents, reads the position, turns it `backward` detents and
    /// reads it again.
    /// </summary>
    private static (short First, short Second) Run(int forward, int backward)
    {
        var uno = _session.Reset();
        // Both lines released before the firmware starts, which is what the decoder primes
        // itself from.
        uno.PortD.SetPinValue(ABit, true);
        uno.PortD.SetPinValue(BBit, true);
        uno.RunToBreak(20_000_000);

        Turn(uno, Forward, forward);
        uno.RunInstructions(1);
        uno.RunToBreak(20_000_000);
        var first = Read(uno);

        Turn(uno, Backward, backward);
        uno.RunInstructions(1);
        uno.RunToBreak(20_000_000);
        return (first, Read(uno));
    }

    private static void Turn(ArduinoUnoSimulation uno, (bool A, bool B)[] sequence, int detents)
    {
        for (var i = 0; i < detents; i++)
        {
            foreach (var (a, b) in sequence)
            {
                uno.PortD.SetPinValue(ABit, a);
                uno.PortD.SetPinValue(BBit, b);
                uno.RunCycles(1600);
            }
        }
    }

    private static short Read(ArduinoUnoSimulation uno) =>
        (short)(uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8));

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(7, 7)]
    public void EveryDetentMovesThePositionByOne(int detents, int expected)
    {
        // The measurement the module exists for: four line changes per detent, and a
        // position that actually moves.
        Run(detents, 0).First.Should().Be((short)expected);
    }

    [Test]
    public void AKnobThatIsNotTouchedStaysWhereItWas()
    {
        Run(0, 0).First.Should().Be(0);
    }

    [Test]
    public void TurningItBackTakesItBack()
    {
        Run(3, 3).Second.Should().Be(0);
    }

    [Test]
    public void TurningItPastWhereItStartedCountsNegative()
    {
        var (first, second) = Run(2, 3);
        first.Should().Be(2);
        second.Should().Be(-1);
    }
}
