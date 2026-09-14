using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/pwm-stop-keeps-timer (PyMCU#296).
///
/// PWM.stop() wrote TCCR0B = 0. That stops the timer for BOTH channels and for the time
/// base, and leaves this channel's compare output connected, so the pin keeps the level
/// the OC0A latch had when the clock stopped: measured on an Arduino Uno, D6 sits at 5 V
/// after deinit() about half the time.
///
/// Off is the compare output disconnected and the pin driven low (what duty 0 does), with
/// the timer still running. start() reconnects the output unless the duty is 0. deinit()
/// additionally returns the pin to an input.
///
/// Three breaks, read back through the CPU into the GPIORs (see the fixture header).
/// </summary>
[TestFixture]
public class PwmStopKeepsTimerTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private const int Oc0ABit = 6;   // PD6
    private const int Oc0BBit = 5;   // PD5

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-stop-keeps-timer"));

    private readonly record struct Regs(byte G1, byte G2, byte G0);

    private static Regs Read(ArduinoUnoSimulation uno) =>
        new(uno.Data[Gpior1Addr], uno.Data[Gpior2Addr], uno.Data[Gpior0Addr]);

    private static (Regs AfterStop, Regs AfterStart, Regs AfterDeinit) Run(byte duty)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = duty;

        uno.RunToBreak();
        var stopped = Read(uno);
        uno.RunInstructions(1);
        uno.RunToBreak();
        var started = Read(uno);
        uno.RunInstructions(1);
        uno.RunToBreak();
        var released = Read(uno);
        return (stopped, started, released);
    }

    private static int Com0A(byte tccr0a) => (tccr0a >> 6) & 0b11;
    private static int Com0B(byte tccr0a) => (tccr0a >> 4) & 0b11;

    // --- stop() -----------------------------------------------------------------

    [Test]
    public void StopDisconnectsTheCompareOutput()
        => Com0A(Run(128).AfterStop.G1).Should().Be(0b00,
            "stop() must take OC0A off the pin; with it connected the OC0A latch keeps driving " +
            "whatever level it had");

    [Test]
    public void StopDrivesThePinLow()
        => (Run(128).AfterStop.G0 & (1 << Oc0ABit)).Should().Be(0,
            "once the compare output is disconnected PORTD holds the pin, and off is low");

    [Test]
    public void StopKeepsTheTimerRunning()
        => Run(128).AfterStop.G2.Should().Be(0x03,
            "TCCR0B is the clock of both Timer0 channels and of the time base; stop() " +
            "must not touch it");

    [Test]
    public void StopLeavesTheSiblingChannelConnected()
        => Com0B(Run(128).AfterStop.G1).Should().Be(0b10, "OC0B belongs to the other PWM");

    // --- start() ----------------------------------------------------------------

    [Test]
    public void StartReconnectsANonZeroDuty()
    {
        var started = Run(128).AfterStart;
        Com0A(started.G1).Should().Be(0b10, "the wave has to come back after start()");
        started.G2.Should().Be(0x03);
    }

    [Test]
    public void StartLeavesADutyOfZeroOff()
    {
        var started = Run(0).AfterStart;
        Com0A(started.G1).Should().Be(0b00,
            "duty 0 is off; reconnecting it would emit the one-clock pulse OCR0A = 0 gives");
        (started.G0 & (1 << Oc0ABit)).Should().Be(0);
    }

    // --- deinit() ---------------------------------------------------------------

    [Test]
    public void DeinitReturnsThePinToAnInput()
    {
        var released = Run(128).AfterDeinit;
        (released.G1 & (1 << Oc0ABit)).Should().Be(0, "DDRD6 cleared: the pin is an input again");
        (released.G0 & (1 << Oc0ABit)).Should().Be(0, "and without the pull-up");
        Com0A(released.G2).Should().Be(0b00);
    }

    [Test]
    public void DeinitLeavesTheSiblingPinAlone()
    {
        var released = Run(128).AfterDeinit;
        (released.G1 & (1 << Oc0BBit)).Should().NotBe(0, "PD5 is still the other PWM's output");
        Com0B(released.G2).Should().Be(0b10);
    }
}
