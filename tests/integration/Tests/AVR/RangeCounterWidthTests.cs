using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The range() counter is sized from its bounds (PyMCU#284).
///
/// The counter of `for i in range(...)` was always an 8-bit variable: range(300) ran 44
/// times, range(0, 256) never ran, a descending range from 200 to -1 never ran, a uint16
/// stop variable and a uint16 annotation on the loop variable were both ignored, and a step
/// that does not divide the span wrapped the counter around. Every line of the transcript
/// is one of those shapes, with the count CPython gives for the same loop; `L` is the guard
/// that range(100) stays the 8-bit loop it always was, checked on the emitted assembly.
/// </summary>
[TestFixture]
public class RangeCounterWidthTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-counter-width"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 1500);
        return uno.Serial.Text;
    }

    [TestCase("A 300", "range(300)")]
    [TestCase("B 256", "range(0, 256)")]
    [TestCase("C 655", "range(0, 65500, 100)")]
    [TestCase("D 201", "range(200, -1, -1)")]
    [TestCase("E 129", "range(128, -1, -1)")]
    [TestCase("F 300", "range(n) with n: uint16 = 300")]
    [TestCase("G 300", "j: uint16 declared before for j in range(300)")]
    [TestCase("H 10", "range(a, 5) with a: int8 = -5")]
    [TestCase("I 9", "range(0, 250, 30): the last step overshoots 250 and must not wrap")]
    [TestCase("J 9", "range(250, 0, -30)")]
    [TestCase("K 9 1000 143", "runtime stop and step: (250, 30), (1000, 1), (1000, 7)")]
    [TestCase("L 100", "range(100) still counts to 100")]
    public void EachRange_RunsAsManyTimesAsCPython(string line, string shape)
        => Transcript().Should().Contain(line + "\n", shape);

    // The zero-cost gate: a range that fits a byte is still an 8-bit loop. `hundred()` compares
    // its counter against 100 with a single CPI; a widened counter would add a CPC on R25.
    [Test]
    public void ARangeThatFitsAByte_StaysAnEightBitLoop()
    {
        Transcript();   // ensures the fixture is built
        var lines = File.ReadAllLines(Path.Combine(
                PymcuCompiler.FixtureDir("range-counter-width"), "dist", "debug", "firmware.asm"))
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l.Trim(), @"\s+", " ")).ToList();

        // The loop's exit test: an 8-bit compare of the counter with 100, branching straight
        // out on "same or higher". (The decimal printer also compares with 100, 16-bit, so the
        // match is on the compare AND the branch that follows it.)
        bool eightBitExit = lines
            .Where((l, i) => l == "CPI R24, 100" && i + 1 < lines.Count && lines[i + 1].StartsWith("BRSH"))
            .Any();
        eightBitExit.Should().BeTrue("range(100) compares its 8-bit counter with CPI R24, 100 and exits on BRSH, with no CPC on a high byte");
        lines.Should().NotContain("CPI R24, 44", "300 must never be truncated to 44");
    }
}
