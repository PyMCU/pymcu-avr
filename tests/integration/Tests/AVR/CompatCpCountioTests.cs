using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-countio (pymcu-circuitpython#11): countio.Counter counts the edges
/// that arrive on a pin. The module was absent, so a flow meter or a tachometer had no way
/// into the CircuitPython layer.
/// </summary>
[TestFixture]
public class CompatCpCountioTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int D2Bit = 2;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-countio"));

    /// <summary>Pulses D2 `pulses` times, then returns the count and the count after reset.</summary>
    private static (int Count, byte AfterReset) Run(int pulses)
    {
        var uno = _session.Reset();
        uno.RunToBreak(20_000_000);

        // The firmware waits 20 ms here. Each pulse is a falling edge and a rising one.
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
        var count = uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8);
        uno.RunInstructions(1);
        uno.RunToBreak(20_000_000);
        return (count, uno.Data[Gpior0Addr]);
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(37)]
    public void EveryFallingEdgeIsCounted(int pulses)
    {
        // The counter is asked for falling edges, and each pulse has exactly one.
        Run(pulses).Count.Should().Be(pulses);
    }

    [Test]
    public void NothingOnThePinLeavesTheCountAtZero()
    {
        Run(0).Count.Should().Be(0);
    }

    [Test]
    public void ResetPutsItBackToZero()
    {
        Run(5).AfterReset.Should().Be(0);
    }
}
