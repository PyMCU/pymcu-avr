using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/adc-two-pins (PyMCU#134).
///
/// Two AnalogPins shared one channel: ADMUX was programmed in __init__ only. The third read
/// goes back to the first pin, because a fix that merely selected on construction in the other
/// order would still pass a test that only read each pin once.
/// </summary>
[TestFixture]
public class AdcTwoPinsTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("adc-two-pins"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void EachPinSelectsItsOwnChannel()
    {
        Boot().Should().StartWith("64\n65\n", "PC0 is ADMUX 0x40 and PC1 is 0x41");
    }

    [Test]
    public void GoingBackToTheFirstPinSelectsItAgain()
    {
        Boot().Should().Contain("64\n65\n64\ndone\n");
    }
}
