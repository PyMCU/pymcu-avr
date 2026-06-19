using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for examples/smoothing — the Arduino "Smoothing" sketch
/// ported to PyMCU: a 10-sample running average of A0, printed over UART.
///
/// With a constant analog input the average converges to that input. Choosing
/// inputs above 255 also exercises the uint16 print path end-to-end: a
/// regression to the old uint8-only print() would truncate the result
/// (500 &amp; 0xFF == 244, 300 &amp; 0xFF == 44), so seeing the exact value proves
/// both the filter math and that wide values are not silently narrowed.
/// </summary>
[TestFixture]
public class SmoothingTests
{
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.Build("smoothing");

    private static ArduinoUnoSimulation SimWithAdc(double adcCount)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = adcCount / 1024.0 * 5.0;   // count -> voltage at 5 V / 1024
        return uno;
    }

    [Test]
    public void RunningAverage_ConvergesToConstantInput_500()
    {
        var uno = SimWithAdc(500);
        // Enough output for the 10-sample window to fill and converge to 500.
        uno.RunUntilSerialBytes(uno.Serial, 60, maxMs: 5000);
        uno.Serial.Should().Contain("500", "the average of a constant 500 input is 500");
    }

    [Test]
    public void WideValue_PrintsWithoutEightBitTruncation()
    {
        var uno = SimWithAdc(300);
        uno.RunUntilSerialBytes(uno.Serial, 60, maxMs: 5000);
        // The converged average is 300; an 8-bit-truncated print would show 44.
        uno.Serial.Should().Contain("300");
    }
}
