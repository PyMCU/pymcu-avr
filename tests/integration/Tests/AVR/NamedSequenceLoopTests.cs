using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/named-sequence-loop (PyMCU#77).
///
/// "Declare the pins, then walk them" is how an Arduino or MicroPython program starts, and
/// it had no spelling: bound to a name, the loop compiled at run time and the loop variable
/// was not a constant, so Pin(p) rejected it -- while the same literal written inline at the
/// `for` unrolled and compiled. A named tuple failed even earlier.
///
/// Asserted on the port registers, because "it compiles" would pass on an unrolled loop that
/// drove the wrong pins, and on both a list and a tuple, since they failed differently.
/// </summary>
[TestFixture]
public class NamedSequenceLoopTests
{
    private const int PortBAddr = 0x25;
    private const int PortDAddr = 0x2B;
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("named-sequence-loop"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void NamedList_DrivesEveryPinInIt()
    {
        var uno = Boot();
        (uno.Data[PortBAddr] & 0x03).Should().Be(0x03, "pins 8 and 9 are PB0 and PB1");
    }

    [Test]
    public void NamedTuple_DrivesEveryPinInIt()
    {
        var uno = Boot();
        (uno.Data[PortDAddr] & 0xE0).Should().Be(0xE0, "pins 5, 6 and 7 are PD5, PD6 and PD7");
    }

    [Test]
    public void EnumerateOverANamedSequence_Unrolls()
    {
        Boot().Data[Gpior0Addr].Should().Be(1, "the last index of a two-element list is 1");
    }
}
