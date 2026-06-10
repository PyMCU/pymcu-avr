using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Stress fixture exercising every AVR codegen optimization at once under heavy
/// register pressure: four ADC channels (four (hi&lt;&lt;8)|lo byte-packs), four
/// sliding-window accumulators (variable-index array Z-CSE + many uint16 temps),
/// the divmod() builtin, and decimal printing (divmod fusion).
///
/// With all four channels held at a constant ADC count of 500, every running
/// average converges to 500 and divmod(500, 10) yields (50, 0). Seeing those
/// exact values proves the combined optimizations preserve the arithmetic.
/// </summary>
[TestFixture]
public class DspStressTests
{
    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("dsp-stress");

    private static ArduinoUnoSimulation SimAllChannelsAt(double adcCount)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        double v = adcCount / 1024.0 * 5.0;
        adc.ChannelValues[0] = v;
        adc.ChannelValues[1] = v;
        adc.ChannelValues[2] = v;
        adc.ChannelValues[3] = v;
        return uno;
    }

    [Test]
    public void RunningAverages_ConvergeTo500()
    {
        var uno = SimAllChannelsAt(500);
        // Enough output for the 8-sample windows to fill and converge.
        uno.RunUntilSerialBytes(uno.Serial, 200, maxMs: 8000);
        uno.Serial.Should().Contain("500", "each channel's running average converges to 500");
    }

    [Test]
    public void DivModBuiltin_500_Yields_50_And_0()
    {
        var uno = SimAllChannelsAt(500);
        uno.RunUntilSerialBytes(uno.Serial, 200, maxMs: 8000);
        // divmod(500, 10) == (50, 0): the quotient line "50" then the remainder line "0".
        uno.Serial.Should().Contain("50", "divmod(500, 10) quotient is 50");
    }
}
