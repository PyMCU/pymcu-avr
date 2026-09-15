using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU#411. Growing a fixed-size buffer (PyMCU#362's <c>.extend()</c>) by a large,
/// compile-time constant amount used to unroll into one store per new slot no matter how many.
/// 70 new slots -- what PulseCapture's ring grows by for a 70-pulse NEC frame (PyMCU#406) --
/// cost 432 bytes of flash unrolled on an otherwise 754-byte program.
///
/// Not simulated: this fixture only needs to build. The assertion is on the actual flash the
/// program takes, read as the sum of every data record's byte count in the built firmware's
/// Intel HEX -- the same number the driver reports as "Flash: N / 32768".
/// </summary>
[TestFixture]
public class BufferExtendLoopFlashBoundTests
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
    public void ExtendingSeventySlots_StaysUnderTheUnrolledBaseline()
    {
        // 754 B is compat-cp-pulseio-ir-nec's flash before PyMCU#406 grew its ring at all (the
        // baseline this fix is not allowed to exceed); 432 B unrolled is what #406 alone added
        // for the same 69-slot growth. This fixture isolates the .extend() itself: a small
        // program with nothing else in it, so a regression here cannot hide behind other code.
        var hex = PymcuCompiler.BuildFixture("buffer-extend-loop-flash-bound");
        FlashBytes(hex).Should().BeLessThan(250,
            "growth past the measured AVR crossover (13 slots) must use a counted loop, not " +
            "69 unrolled stores");
    }
}
