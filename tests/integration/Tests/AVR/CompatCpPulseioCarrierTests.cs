using System.Collections.Generic;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pulseio-carrier (pymcu-circuitpython#9): pulseio.PulseOut puts a
/// carrier on the pin and gates it with a pulse list. The module did not exist, so sending
/// an infrared frame from the CircuitPython layer was not possible at all.
///
/// The carrier is read where it is decided -- OCR2A for the period, OCR2B for the duty and
/// COM2B1 for the gate -- and the gate is timed. Reading the pin instead would quantise
/// every measurement to one carrier period, 26 us.
/// </summary>
[TestFixture]
public class CompatCpPulseioCarrierTests
{
    private const int Tccr2aAddr = 0xB0;
    private const int Tccr2bAddr = 0xB1;
    private const int Ocr2aAddr = 0xB3;
    private const int Ocr2bAddr = 0xB4;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pulseio-carrier"));

    /// <summary>How long the carrier gate was open and shut, in microseconds, in order.</summary>
    private static (byte Ocr2a, byte Ocr2b, byte Tccr2a, byte Tccr2b, List<double> Segments) Run()
    {
        var uno = _session.Reset();
        uno.RunToBreak(40_000_000);
        var ocr2a = uno.Data[Ocr2aAddr];
        var ocr2b = uno.Data[Ocr2bAddr];
        var tccr2a = uno.Data[Tccr2aAddr];
        var tccr2b = uno.Data[Tccr2bAddr];

        var edges = new List<double>();
        var previous = -1;
        var start = uno.Cpu.Cycles;
        for (var i = 0; i < 60000; i++)
        {
            uno.RunCycles(2);
            var gate = (uno.Data[Tccr2aAddr] >> 5) & 1;
            if (gate != previous)
            {
                edges.Add((uno.Cpu.Cycles - start) / 16.0);
                previous = gate;
            }
        }
        var segments = new List<double>();
        for (var i = 1; i + 1 < edges.Count; i++) segments.Add(edges[i + 1] - edges[i]);
        return (ocr2a, ocr2b, tccr2a, tccr2b, segments);
    }

    [Test]
    public void TheCarrierPeriodIsTheFrequencyAskedFor()
    {
        var r = Run();
        // Period = (OCR2A + 1) counts of 8 CPU cycles: 52 * 8 / 16 MHz = 26 us = 38462 Hz,
        // 1.2 % above the 38 kHz asked for and well inside any receiver's band-pass.
        r.Ocr2a.Should().Be(51);
        (r.Tccr2b & 0b111).Should().Be(0b010, "prescaler 8");
        ((r.Tccr2b >> 3) & 1).Should().Be(1, "WGM22 set: fast PWM with OCR2A as TOP");
    }

    [Test]
    public void TheCarrierDutyIsHalfThePeriod()
    {
        var r = Run();
        r.Ocr2b.Should().Be(26, "32768 of 65535 of 52 counts");
    }

    [Test]
    public void TheCarrierIsOffUntilSomethingIsSent()
    {
        Run().Tccr2a.Should().Be(0x03, "mode bits only: COM2B1 clear, so OC2B is disconnected");
    }

    [Test]
    public void TheGateFollowsThePulseList()
    {
        var segments = Run().Segments;
        segments.Count.Should().BeGreaterThanOrEqualTo(3);
        segments[0].Should().BeApproximately(560, 12, "the first pulse holds the carrier on");
        segments[1].Should().BeApproximately(560, 12, "the second holds it off");
        segments[2].Should().BeApproximately(1690, 12, "the third holds it on again");
    }
}
