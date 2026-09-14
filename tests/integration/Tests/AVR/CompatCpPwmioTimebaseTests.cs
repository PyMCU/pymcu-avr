using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pwmio-timebase (PyMCU#295): pwmio.PWMOut at the default frequency
/// and supervisor.ticks_ms() share Timer0 on the ATmega328P, and the clock keeps its rate.
/// </summary>
[TestFixture]
public class CompatCpPwmioTimebaseTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pwmio-timebase"));

    private string Run()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        return uno.Serial.Text;
    }

    [Test]
    public void ADefaultFrequencyPwmOutLeavesTheClockAlone()
    {
        var text = Run();
        var dt = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"dt (\d+)").Groups[1].Value);
        dt.Should().BeInRange(98, 103, "time.sleep(0.1) measured by supervisor.ticks_ms()");
    }
}
