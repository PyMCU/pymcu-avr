using System.Collections.Generic;
using System.Linq;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Adafruit's irremote_transmit_simpletest.py, unmodified but for the pin: GenericTransmit
/// walks the header it was handed with `for i, _ in enumerate(self.header)` and sends the NEC
/// frame through pulseio.PulseOut. The carrier gate (COM2B1 in TCCR2A) is watched, as in
/// <see cref="CompatCpPulseioCarrierTests"/>, and the frame is read back from how long it
/// stays open and shut.
/// </summary>
[TestFixture]
public class IrremoteTransmitSimpletestTests
{
    private const int Tccr2aAddr = 0xB0;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("irremote-transmit-simpletest"));

    /// <summary>Open and shut times of the carrier gate in microseconds, from the first frame.</summary>
    private static List<double> Frame()
    {
        var uno = _session.Reset();
        var edges = new List<double>();
        var previous = -1;
        var start = uno.Cpu.Cycles;
        // One frame is about 140 ms of NEC at these timings; the next starts 200 ms later.
        while (uno.Cpu.Cycles - start < 16_000_000L / 10)
        {
            uno.RunCycles(8);
            var gate = (uno.Data[Tccr2aAddr] >> 5) & 1;
            if (gate != previous)
            {
                edges.Add((uno.Cpu.Cycles - start) / 16.0);
                previous = gate;
            }
        }
        var segments = new List<double>();
        for (var i = 1; i + 1 < edges.Count; i++) segments.Add(edges[i + 1] - edges[i]);
        return segments;
    }

    [Test]
    public void TheHeaderIsTheNineAndAHalfThenFourAndAHalfMilliseconds()
    {
        var s = Frame();
        s.Count.Should().BeGreaterThan(4);
        s[0].Should().BeApproximately(9500, 40, "header mark");
        s[1].Should().BeApproximately(4500, 40, "header space");
    }

    [Test]
    public void EachBitIsAMarkThenASpaceTheLengthItsValueSays()
    {
        var s = Frame();
        // 255 = eight ones: 550 on, 550 off. Then 2 = 00000010, MSB first: six zeros
        // (550 on, 1700 off), a one, a zero.
        for (var b = 0; b < 8; b++)
        {
            s[2 + 2 * b].Should().BeApproximately(550, 25, $"bit {b} mark");
            s[3 + 2 * b].Should().BeApproximately(550, 25, $"bit {b} of 255 is a one: a 550 us space");
        }
        for (var b = 0; b < 6; b++)
            s[2 + 2 * (8 + b) + 1].Should().BeApproximately(1700, 25, $"bit {b} of 2 is a zero: a 1700 us space");
        s[2 + 2 * 14 + 1].Should().BeApproximately(550, 25, "the seventh bit of 2 is a one");
        s[2 + 2 * 15 + 1].Should().BeApproximately(1700, 25, "the last bit of 2 is a zero");
    }
}
