using System.Collections.Generic;
using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-attiny85-blink (pymcu-circuitpython#3, #4): the CircuitPython blink
/// that <c>pymcu new --board attiny85 --stdlib circuitpython</c> scaffolds, built for the
/// part it was generated for and run.
///
/// It used to be written <c>board.LED</c>, and the project could not be built at all: a bare
/// ATtiny85 has no LED soldered to it, so the layer defines none, and the build stopped at
/// "Unknown module member: board_LED" after the project was already on disk. Neither
/// <c>board.D0</c> nor a raw integer worked either, so there was no spelling of CircuitPython
/// GPIO that reached a pin on these parts.
///
/// The firmware is run rather than read. An assembly grep would pass on a program that
/// configures PB0 and never drives it, which is the failure this part invites: PB5 is RESET
/// and needs the RSTDISBL fuse, so a wrong pin choice looks identical in the listing and
/// dead on the bench.
/// </summary>
[TestFixture]
public class CompatCpAttiny85BlinkTests
{
    private const int DdrbAddr  = 0x37;
    private const int PortbAddr = 0x38;

    // 8 MHz: what the scaffolder writes for this part, and what the simulation runs at.
    private const double CyclesPerMs = 8_000.0;

    private static string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("compat-cp-attiny85-blink");

    private static ATtiny85Simulation Fresh()
    {
        var tiny = new ATtiny85Simulation();
        tiny.WithHex(_hex);
        return tiny;
    }

    /// <summary>Milliseconds between the level changes of PORTB bit <paramref name="bit"/>.</summary>
    private static List<double> HalfPeriods(int bit, int wanted)
    {
        var tiny = Fresh();
        var edges = new List<double>();
        var previous = -1;
        var start = tiny.Cpu.Cycles;

        // 3 s of simulated time at the sampling rate below, which is far more than the four
        // edges asked for at 500 ms apart and stops a dead pin from running to the timeout.
        for (var i = 0; i < 30_000 && edges.Count <= wanted; i++)
        {
            tiny.RunCycles(800);
            var level = (tiny.Data[PortbAddr] >> bit) & 1;
            if (level != previous)
            {
                edges.Add((tiny.Cpu.Cycles - start) / CyclesPerMs);
                previous = level;
            }
        }

        var gaps = new List<double>();
        // Skip edges[0]: it is the power-on level being seen for the first time, not a change.
        for (var i = 1; i + 1 < edges.Count; i++) gaps.Add(edges[i + 1] - edges[i]);
        return gaps;
    }

    [Test]
    public void TheScaffoldedProgramBuildsForThePartItWasGeneratedFor()
    {
        _hex.Should().NotBeNullOrWhiteSpace("the build is the half of #3 that used to fail");
    }

    [Test]
    public void ThePinIsMadeAnOutputAndNoOtherLegIsTouched()
    {
        var tiny = Fresh();
        tiny.RunCycles(20_000);

        (tiny.Data[DdrbAddr] & 0b0000_0001).Should().Be(1, "board.PB0 is driven");
        (tiny.Data[DdrbAddr] & 0b0000_0010).Should().Be(0b10, "board.D1 is the same leg as PB1");
        (tiny.Data[DdrbAddr] & 0b0010_0000).Should().Be(0,
            "PB5 is RESET on this part and must not be touched by a scaffolded blink");
    }

    [Test]
    public void ThePortNameSpellingTogglesItsPin()
    {
        var gaps = HalfPeriods(bit: 0, wanted: 3);

        gaps.Should().HaveCountGreaterThanOrEqualTo(2, "board.PB0 must actually change level");
        foreach (var gap in gaps)
            gap.Should().BeApproximately(500, 25, "time.sleep(0.5) between the two levels");
    }

    [Test]
    public void TheDnSpellingTogglesTheSamePin()
    {
        // The half of #4 that this fixture measures: board.D1 is PB1 and drives it, rather
        // than being a constant that merely exists.
        var gaps = HalfPeriods(bit: 1, wanted: 3);

        gaps.Should().HaveCountGreaterThanOrEqualTo(2, "board.D1 must actually change level");
        foreach (var gap in gaps)
            gap.Should().BeApproximately(500, 25, "time.sleep(0.5) between the two levels");
    }
}
