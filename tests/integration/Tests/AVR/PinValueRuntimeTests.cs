using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/pin-value-runtime.
///
/// `Pin.value(x)` took a `const` parameter, so a pin could only be driven from a literal
/// and `led.value(state)` was a compile error (PyMCU#57). Reading and toggle() worked, so
/// the hole was specifically writing a computed value -- the shape every blink-with-state,
/// button-follows-LED and shift-out program is written in.
///
/// Both directions are exercised from a volatile seed, because a fixture that only drives
/// 1 would pass with a codegen that ignores the argument and always sets the bit.
/// </summary>
[TestFixture]
public class PinValueRuntimeTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int PortBAddr = 0x25;
    private const int Pb5 = 1 << 5;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pin-value-runtime"));

    private ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void RuntimeOne_DrivesThePinHigh()
    {
        var uno = RunWithSeed(1);
        (uno.Data[PortBAddr] & Pb5).Should().Be(Pb5, "value(v) with v = 1 must set PB5");
        uno.Data[Gpior1Addr].Should().Be(1, "value() must read the pin back as 1");
    }

    [Test]
    public void RuntimeZero_DrivesThePinLow()
    {
        var uno = RunWithSeed(0);
        (uno.Data[PortBAddr] & Pb5).Should().Be(0, "value(v) with v = 0 must clear PB5");
        uno.Data[Gpior1Addr].Should().Be(0, "value() must read the pin back as 0");
    }
}
