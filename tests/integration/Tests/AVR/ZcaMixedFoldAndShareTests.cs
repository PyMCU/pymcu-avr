// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0006 (docs/rfcs/0006-self-is-this.md, Section 13.2): two <c>SoftUart</c>
/// instances, <c>pin</c> different (2 and 5) and <c>baud</c> the same (9600) in both.
/// Neither escapes, so RFC 0001's existing outlining already shares ONE body across
/// both -- but it shares BOTH fields as runtime parameters, never noticing that
/// <c>baud</c> never actually varies, so <c>1000000 // self.baud</c> compiles to a
/// genuine runtime <c>__div32</c> call despite the divisor being constant. This is the
/// corpus fixture the per-field fold (Section 5) will be measured against once Phase 3
/// lands -- today it is the "before" number.
///
/// The printed values are exactly what the same class prints under CPython: a wrong
/// self/field binding during a future fold (the shape of RFC 0001's own #385, "an
/// outlined body received the field as a number and self did not exist in it") would
/// show up here as a wrong printed number, not only as a byte-count change.
/// </summary>
[TestFixture]
public class ZcaMixedFoldAndShareTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-mixed-fold-and-share"));

    [Test]
    public void FlashSize_Is1032Bytes()
    {
        // 602 B without any observable output (the number RFC 0006's first draft
        // measured); printing the five values so behaviour is checkable, not just
        // size, pulls in UART init and decimal formatting, landing at 1032 B. Recorded
        // here so a change is caught and explained, not silently absorbed.
        FlashBytes(PymcuCompiler.BuildFixture("zca-mixed-fold-and-share")).Should().Be(1032,
            "this fixture's baseline before RFC 0006's per-field fold (Phase 3) lands; " +
            "a change here needs the RFC's baseline JSON updated alongside it, not silently");
    }

    [Test]
    public void PrintsExactlyWhatCPythonPrintsForTheSameClass()
    {
        var uno = _session.Reset();
        // a.bit_period_us()=104, b.bit_period_us()=104 (1_000_000 // 9600, same in both
        // instances since baud never varies); a.send_marker()=3, b.send_marker()=6
        // (pin+1, the field that DOES vary); a.send_marker() == b.send_marker() is False.
        // Verified against `python3` running the identical class body before writing
        // this assertion.
        uno.RunUntilSerial(uno.Serial, "0\n", maxMs: 500);
        uno.Serial.Text.Should().Be("104\n104\n3\n6\n0\n",
            "self.baud (folded-in-every-instance) and self.pin (varying) must resolve " +
            "to the SAME values a shared outlined body reads under CPython semantics");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
}
