using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pwmio-deinit (PyMCU#296): pwmio.PWMOut.deinit() releases the pin
/// as an input, CircuitPython's semantics, and leaves the sibling channel on the same
/// timer running. Before: TCCR0B = 0 froze both channels and D6 stayed at the level the
/// OC0A latch had, 5 V half the time on the real board.
/// </summary>
[TestFixture]
public class CompatCpPwmioDeinitTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private const int D6Bit = 6;
    private const int D5Bit = 5;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pwmio-deinit"));

    private static (byte Ddrd, byte Tccr0A, byte Tccr0B, byte PortD) Run()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        var ddrd = uno.Data[Gpior0Addr];
        var tccr0a = uno.Data[Gpior1Addr];
        var tccr0b = uno.Data[Gpior2Addr];
        uno.RunInstructions(1);
        uno.RunToBreak();
        var portd = uno.Data[Gpior0Addr];
        return (ddrd, tccr0a, tccr0b, portd);
    }

    [Test]
    public void DeinitMakesThePinAnInputWithoutPullUp()
    {
        var r = Run();
        (r.Ddrd & (1 << D6Bit)).Should().Be(0, "CircuitPython leaves a deinit'd pin as an input");
        (r.PortD & (1 << D6Bit)).Should().Be(0, "no pull-up either");
    }

    [Test]
    public void DeinitDisconnectsOnlyItsOwnCompareOutput()
    {
        var r = Run();
        ((r.Tccr0A >> 6) & 0b11).Should().Be(0b00, "OC0A off");
        ((r.Tccr0A >> 4) & 0b11).Should().Be(0b10, "OC0B still drives D5");
    }

    [Test]
    public void DeinitKeepsTimer0Running()
    {
        var r = Run();
        r.Tccr0B.Should().Be(0x03, "the sibling channel and the time base need the clock");
        (r.Ddrd & (1 << D5Bit)).Should().NotBe(0, "D5 is still an output");
    }
}
