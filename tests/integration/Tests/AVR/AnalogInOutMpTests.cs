using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for examples/analog-inout-mp — Arduino "AnalogInOutSerial"
/// on the MicroPython compatibility layer (machine.ADC / PWM / UART).
///
/// Constructing ADC(Pin(14)) (int overload) before PWM(Pin("PD6")) (str overload)
/// used to leak the int pin id into the PWM's pin name, mis-resolving the timer
/// and emitting a stray RET. With A0 held at ADC count 512 the duty is
/// 512 >> 2 == 128, so OCR0A == 128 -- proving both Pin overloads resolve
/// independently and the ADC -> PWM chain works on the machine layer.
/// </summary>
[TestFixture]
public class AnalogInOutMpTests
{
    private const int OCR0A = 0x47;
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.Build("analog-inout-mp");

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
