using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/instance-array-index (PyMCU#68).
///
/// `pins[i].high()` with a run-time `i`. Both indices are exercised and the OTHER pin is
/// asserted to stay low, because a lowering that ignored the index -- or drove every
/// element -- would pass a test that only checked the selected one.
/// </summary>
[TestFixture]
public class InstanceArrayIndexTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int PortBAddr = 0x25;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("instance-array-index"));

    private ArduinoUnoSimulation RunWithIndex(byte index)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = index;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void IndexZero_DrivesTheFirstPinOnly()
    {
        var uno = RunWithIndex(0);
        (uno.Data[PortBAddr] & 0x01).Should().Be(0x01, "pins[0] is PB0");
        (uno.Data[PortBAddr] & 0x02).Should().Be(0, "pins[1] must not be touched");
    }

    [Test]
    public void IndexOne_DrivesTheSecondPinOnly()
    {
        var uno = RunWithIndex(1);
        (uno.Data[PortBAddr] & 0x02).Should().Be(0x02, "pins[1] is PB1");
        (uno.Data[PortBAddr] & 0x01).Should().Be(0, "pins[0] must not be touched");
    }

    // Both indices, because the first version of this lowering picked the return type from a
    // key that overloads share, got void for Pin.value(), and returned nothing at all -- which
    // an index-1-only assertion happened not to catch.
    [TestCase((byte)0)]
    [TestCase((byte)1)]
    public void ValueReturningMethod_ComesBackThroughTheSameSelection(byte index)
    {
        RunWithIndex(index).Data[Gpior1Addr].Should().Be(1, "the selected pin was just driven high");
    }
}
