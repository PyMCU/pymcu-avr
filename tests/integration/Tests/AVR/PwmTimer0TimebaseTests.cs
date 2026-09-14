using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/pwm-timer0-timebase (PyMCU#295): a PWM on a Timer0 pin at the time base's own
/// bucket leaves millis() at its rate, whichever is built first, and millis_init() keeps
/// the PWM mode of a channel built before it (it used to clear TCCR0A).
/// </summary>
[TestFixture]
public class PwmTimer0TimebaseTests
{
    private const int Gpior0Addr = 0x3E;
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("pwm-timer0-timebase"));

    private string Run(byte scenario)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = scenario;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        return uno.Serial.Text;
    }

    private static int Dt(string text) =>
        int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"dt (\d+)").Groups[1].Value);

    [Test]
    public void ThePwmBuiltAfterTheTimeBaseLeavesTheClockAlone()
    {
        var text = Run(0);
        Dt(text).Should().BeInRange(98, 103, "100 ms of delay_ms measured by millis()");
        text.Should().Contain("A 131 B 3", "fast PWM on OC0A, prescaler 64");
    }

    [Test]
    public void MillisInitKeepsThePwmModeOfAChannelBuiltBeforeIt()
    {
        var text = Run(1);
        Dt(text).Should().BeInRange(98, 103);
        text.Should().Contain("A 131 B 3", "TCCR0A = 0x83 must survive millis_init(); it used to be cleared to 0");
    }
}
