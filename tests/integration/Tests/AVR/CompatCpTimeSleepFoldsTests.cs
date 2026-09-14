using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-time-sleep-folds (pymcu-circuitpython#26): a literal sleep() folds to
/// calibrated constant delays. The first shape of the wider sleep() kept 32-bit arithmetic
/// and the generic delay subroutine in every program (+858 bytes on a servo sketch).
/// </summary>
[TestFixture]
public class CompatCpTimeSleepFoldsTests
{
    private static string[] _lines = null!;

    [OneTimeSetUp]
    public void Build()
    {
        PymcuCompiler.BuildFixture("compat-cp-time-sleep-folds");
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("compat-cp-time-sleep-folds"), "dist", "debug", "firmware.asm"));
        _lines = asm.Split('\n').Select(l => l.Trim()).ToArray();
    }

    [Test]
    public void NoThirtyTwoBitArithmeticIsLinked()
        => _lines.Should().NotContain(l => l.Contains("__mul32") || l.Contains("__div32"),
            "a literal duration folds; the chunk arithmetic must not reach the firmware");

    [Test]
    public void NoGenericDelaySubroutineIsCalled()
        => _lines.Should().NotContain(l => l.StartsWith("CALL") && l.Contains("delay_ms_avr"),
            "delay_ms with a folded constant takes the calibrated loop, not the counted subroutine");

    [Test]
    public void TheCalibratedLoopsAreThere()
        => _lines.Count(l => l.StartsWith("_dly_L") && l.EndsWith(":")).Should().BeGreaterOrEqualTo(3,
            "1 ms, 250 ms and 1 s are three calibrated constant loops in line; 500 us pays in 200 us chunks");
}
