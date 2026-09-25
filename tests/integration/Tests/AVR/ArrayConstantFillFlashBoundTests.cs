using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A constant-sized <c>bytearray(N)</c> lowers to N ArrayStore of the same constant into
/// consecutive indices, and each one is a whole instruction: 2 bytes inside the STD Y+q
/// window, 4 bytes (STS) past it. On the unmodified adafruit_ssd1306 simpletest the single
/// line <c>self.buffer = bytearray(513)</c> was 1972 of the 4188 bytes of the image, and
/// the whole simpletest fell to 2230 bytes once the backend wrote that run as a counted loop.
///
/// This fixture isolates the fill: a program with nothing else in it, so a regression cannot
/// hide behind other code. 2080 bytes is what it took with the run unrolled; 166 with the
/// loop. Not simulated -- the semantics of the fill are pinned by the oracle probe
/// <c>tests/oracle/probes/277_bytearray_constant_fill.py</c>, which runs the same program
/// under CPython and on the emulator and compares the two outputs.
/// </summary>
[TestFixture]
public class ArrayConstantFillFlashBoundTests
{
    private static int FlashBytes(string hex)
    {
        int total = 0;
        foreach (var raw in hex.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(":") || line.Length < 9) continue;
            int count = Convert.ToInt32(line.Substring(1, 2), 16);
            int recordType = Convert.ToInt32(line.Substring(7, 2), 16);
            if (recordType == 0) total += count;
        }
        return total;
    }

    [Test]
    public void FillingFiveHundredSlots_StaysUnderTheUnrolledBaseline()
    {
        var hex = PymcuCompiler.BuildFixture("array-constant-fill-flash-bound");
        FlashBytes(hex).Should().BeLessThan(300,
            "a 513-element constant fill must be a counted loop, not 513 unrolled stores " +
            "(2080 bytes unrolled, 166 looped)");
    }
}
