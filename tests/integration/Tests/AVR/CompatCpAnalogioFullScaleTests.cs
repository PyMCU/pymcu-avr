using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-analogio-full-scale (pymcu-circuitpython#21): AnalogIn.value covers
/// the whole 16-bit range, and a read selects the channel the AnalogIn was built with.
/// Before: the 10-bit reading was multiplied by 64, so full scale on the pin read 65472
/// and a caller dividing by 65535 to get volts was always low.
///
/// The board simulation carries no ADC of its own, so the test attaches one and drives the
/// channel voltages directly.
/// </summary>
[TestFixture]
public class CompatCpAnalogioFullScaleTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-analogio-full-scale"));

    private static (int Value, byte Admux)[] Run(double channel0Volts, double channel3Volts)
    {
        var uno = _session.Reset();
        uno.AddAdc(AvrAdc.AdcConfig, out var adc);
        adc.ChannelValues[0] = channel0Volts;
        adc.ChannelValues[3] = channel3Volts;

        var reads = new (int, byte)[3];
        for (var i = 0; i < reads.Length; i++)
        {
            if (i > 0) uno.RunInstructions(1);
            uno.RunToBreak();
            reads[i] = (uno.Data[Gpior0Addr] | (uno.Data[Gpior1Addr] << 8), uno.Data[Gpior2Addr]);
        }
        return reads;
    }

    [Test]
    public void FullScaleOnThePinReadsFullScaleInTheNumber()
    {
        var reads = Run(5.0, 0.0);
        reads[1].Value.Should().Be(65535, "1023 counts of 1023 is the whole range, not 65472");
    }

    [Test]
    public void ZeroReadsZero()
    {
        var reads = Run(0.0, 0.0);
        reads[1].Value.Should().Be(0);
    }

    [Test]
    public void HalfTheReferenceReadsAboutHalfTheRange()
    {
        var reads = Run(2.5, 0.0);
        reads[1].Value.Should().BeInRange(32700, 32850, "512 of 1023 counts is one count above half");
    }

    [Test]
    public void EachReadSelectsItsOwnChannel()
    {
        // One ADMUX is shared by every AnalogPin, so a read that does not re-select returns
        // whatever channel was constructed last.
        var reads = Run(5.0, 1.25);
        reads[0].Admux.Should().Be(0x43, "A3 was constructed last");
        reads[1].Admux.Should().Be(0x40, "reading A0 re-selects channel 0");
        reads[2].Admux.Should().Be(0x43, "reading A3 re-selects channel 3");
        reads[1].Value.Should().Be(65535);
        reads[2].Value.Should().BeInRange(16300, 16450, "a quarter of the reference");
    }
}
