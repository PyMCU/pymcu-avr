using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/isr-global-survives-return (PyMCU#328): a global an interrupt writes has to
/// survive the handler returning.
///
/// R2-R15 is the callee-saved home pool the register allocator hands out, and every ISR
/// prologue pushes that whole range and the epilogue pops it. A global homed there was
/// restored to its pre-interrupt value on RETI, so the handler ran, the write happened, and
/// the value never moved. The program compiled clean and said nothing; a quadrature encoder
/// counted every edge and reported 0 for ever, which reads as a wiring fault.
/// </summary>
[TestFixture]
public class IsrGlobalSurvivesReturnTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;
    private const int D2Bit = 2;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("isr-global-survives-return"));

    /// <summary>Pulses D2 `pulses` times during the firmware's wait, then reads both globals.</summary>
    private static (int Position, byte Last) Run(int pulses)
    {
        var uno = _session.Reset();
        uno.RunToBreak(20_000_000);

        uno.PortD.SetPinValue(D2Bit, true);
        uno.RunCycles(1600);
        for (var i = 0; i < pulses; i++)
        {
            uno.PortD.SetPinValue(D2Bit, false);
            uno.RunCycles(1600);
            uno.PortD.SetPinValue(D2Bit, true);
            uno.RunCycles(1600);
        }

        uno.RunInstructions(1);
        uno.RunToBreak(20_000_000);
        return (uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8), uno.Data[Gpior2Addr]);
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(37)]
    public void ThePositionTheHandlerWroteIsStillThere(int pulses)
    {
        Run(pulses).Position.Should().Be(pulses);
    }

    [Test]
    public void NoEdgesLeavesItAtZero()
    {
        Run(0).Position.Should().Be(0);
    }

    [Test]
    public void AByteWideGlobalSurvivesToo()
    {
        // The handler reads the pin back and stores it; the line is high again by the time the
        // firmware reports, so the value it kept is the one taken inside the handler.
        Run(3).Last.Should().Be(0);
    }
}
