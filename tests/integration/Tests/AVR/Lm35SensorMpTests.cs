using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the lm35-sensor-mp fixture.
///
/// MicroPython-style LM35 driver using machine.ADC on channel A0.
/// Conversion: Temp_C = ADC_raw * 0.4882813  (5 V ref, 10-bit)
/// Printed via uart_write_float: MicroPython's ACTUAL float32 print
/// algorithm (py/formatfloat.c's mp_format_float, 6 to 9 significant
/// digits decided by a round-trip check, not a fixed count), e.g.
/// "24.902348" or "99.609392" -- not the fixed two decimals it used to be.
///
/// Boot banner: "LM35 ready\n"
/// Loop output: "T: &lt;temp&gt; C\n"  (sep="", so no extra spaces)
///
/// ADC injection: adc.ChannelValues[0] = voltage_in_volts
///   voltage = raw / 1024.0 * 5.0
/// </summary>
[TestFixture]
public class Lm35SensorMpTests
{
    private string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("lm35-sensor-mp");

    // ── Boot ─────────────────────────────────────────────────────────────────

    [Test]
    public void Boot_SendsBanner()
    {
        var uno = Sim();
        uno.AddAdc(AvrAdc.AdcConfig, out _);
        uno.RunUntilSerial(uno.Serial, "LM35 ready\n", maxMs: 500);
        uno.Serial.Text.Should().Contain("LM35 ready");
    }

    // ── ADC injection ─────────────────────────────────────────────────────────

    [Test]
    public void ZeroVolts_PrintsZeroCelsius()
    {
        // raw = 0 => 0 * 0.4882813 = 0.0 => "T: 0.0 C"
        var uno = Sim();
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 0.0;

        uno.RunUntilSerial(uno.Serial, "LM35 ready\n", maxMs: 500);
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T: 0.0 C"), maxMs: 500);

        uno.Serial.Text.Should().Contain("T: 0.0 C");
    }

    [Test]
    public void Raw51_Prints24Point902348Celsius()
    {
        // raw = 51 => 51 * 0.4882813 = 24.902348... => uart_write_float
        // (MicroPython's float32 print algorithm) => "24.902348"
        var uno = Sim();
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 51.0 / 1024.0 * 5.0;

        uno.RunUntilSerial(uno.Serial, "LM35 ready\n", maxMs: 500);
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T: 24.902348 C"), maxMs: 500);

        uno.Serial.Text.Should().Contain("T: 24.902348 C");
    }

    [Test]
    public void Raw204_Prints99Point609392Celsius()
    {
        // raw = 204 => 204.0f * 0.4882813f = 99.609392... =>
        // uart_write_float => "99.609392"
        var uno = Sim();
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 204.0 / 1024.0 * 5.0;

        uno.RunUntilSerial(uno.Serial, "LM35 ready\n", maxMs: 500);
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T: 99.609392 C"), maxMs: 500);

        uno.Serial.Text.Should().Contain("T: 99.609392 C");
    }

    [Test]
    public void TemperatureChanges_AreReflectedInOutput()
    {
        // Verify the loop keeps emitting readings when the ADC value changes.
        var uno = Sim();
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = 51.0 / 1024.0 * 5.0; // ~25 C

        uno.RunUntilSerial(uno.Serial, "LM35 ready\n", maxMs: 500);
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T: 24.902348 C"), maxMs: 500);

        // Change to ~50 C (raw = 102 => 49.804696...)
        adc.ChannelValues[0] = 102.0 / 1024.0 * 5.0;
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T: 49.804696 C"), maxMs: 1000);

        uno.Serial.Text.Should().Contain("T: 49.804696 C");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private ArduinoUnoSimulation Sim()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        return uno;
    }
}
