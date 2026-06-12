using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-factory — an @inline factory that returns a
/// ZCA instance (`make_adc(ch) -> ADC: return ADC(Pin(ch))`). The factory result's
/// methods must inline; `pot = make_adc(14)` then `pot.read()` used to mangle to an
/// undefined flattened symbol and fail at link. With A0 at ADC count 512 the PWM
/// duty is 512 >> 2 == 128, so OCR0A == 128 and the duty byte 128 is echoed.
/// </summary>
[TestFixture]
public class ZcaFactoryTests
{
    private const int OCR0A = 0x47;
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("zca-factory");

    [Test]
    public void FactoryAdc_ReadsAndDrivesPwm()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 512 / 1024.0 * 5.0;   // A0 == channel 0
        uno.RunUntilSerialBytes(uno.Serial, 4, maxMs: 4000);
        uno.Data[OCR0A].Should().Be(128, "factory ADC read (512) >> 2 = 128 drives OCR0A");
    }
}
