using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pwmio-exact-duty (pymcu-circuitpython#30): a 16-bit duty_cycle
/// lands as round(duty * 256 / 65535) counts high, with OCR one less because fast PWM is
/// high for OCR + 1 counts; 0 counts is off, 256 fully on. Before: every duty was 1/256
/// above what was asked, 50.4 % for 32768 on the Uno.
/// </summary>
[TestFixture]
public class CompatCpPwmioExactDutyTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pwmio-exact-duty"));

    private static (byte Ocr, int Com) Read(ArduinoUnoSimulation uno) =>
        (uno.Data[Gpior1Addr], (uno.Data[Gpior2Addr] >> 6) & 0b11);

    private static ((byte Ocr, int Com) Built, (byte Ocr, int Com) Set) Run(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = seed;
        uno.RunToBreak();
        var built = Read(uno);
        uno.RunInstructions(1);
        uno.RunToBreak();
        return (built, Read(uno));
    }

    [Test]
    public void TheConstructorPuts50PercentAtOcr127()
    {
        var built = Run(0).Built;
        built.Ocr.Should().Be(127, "32768 is 128 counts high, and the pin is high for OCR + 1");
        built.Com.Should().Be(0b10);
    }

    [TestCase((byte)0, (byte)127, 0b10, "32768 is 50.0 %")]
    [TestCase((byte)1, (byte)255, 0b10, "65535 is fully on: 256 of 256 counts")]
    [TestCase((byte)2, (byte)0, 0b00, "127 is below half a count: off, not a 0.39 % pulse")]
    [TestCase((byte)3, (byte)0, 0b10, "128 is the smallest pulse, 1 of 256 counts")]
    [TestCase((byte)4, (byte)63, 0b10, "16384 is 25.0 %")]
    public void TheSetterLandsExactly(byte seed, byte ocr, int com, string because)
    {
        var set = Run(seed).Set;
        set.Ocr.Should().Be(ocr, because);
        set.Com.Should().Be(com, because);
    }
}
