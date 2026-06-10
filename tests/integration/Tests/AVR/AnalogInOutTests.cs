using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for examples/analog-inout — Arduino "AnalogInOutSerial"
/// on the native PyMCU HAL (UART + AnalogPin + PWM). Reads A0, maps 0-1023 to a
/// 0-255 PWM duty on D6 (OC0A), and echoes the duty byte. With A0 at ADC count
/// 512 the duty is 512 >> 2 == 128 -> OCR0A == 128.
/// </summary>
[TestFixture]
public class AnalogInOutTests
{
    private const int OCR0A = 0x47;
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.Build("analog-inout");

    private static ArduinoUnoSimulation SimWithA0(double adcCount)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = adcCount / 1024.0 * 5.0;   // A0 == channel 0 (PC0)
        return uno;
    }

    [Test]
    public void Pwm_DutyTracksAnalogInput()
    {
        var uno = SimWithA0(512);
        uno.RunUntilSerialBytes(uno.Serial, 20, maxMs: 4000);
        uno.Data[OCR0A].Should().Be(128, "analogWrite duty = sensor (512) >> 2 = 128");
    }
}
