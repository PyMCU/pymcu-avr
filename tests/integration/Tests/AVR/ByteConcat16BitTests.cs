// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Joining a high and a low byte into a 16-bit word must stay 16-bit.
///
/// Arithmetic promotion widened by storage type, so <c>hi * 256</c> with hi:uint8
/// was typed uint32 despite peaking at 65280, and <c>lo + hi * 256</c> ran 32-bit:
/// four bytes spilled to the frame and only the low half reloaded — 36 bytes where
/// the concatenation is two MOVs. The HAL's ADC and Timer1 reads pay it per call.
/// Promotion now stops when the result provably fits the unpromoted type.
/// </summary>
[TestFixture]
public class ByteConcat16BitTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("byte-concat-16bit"));

    [Test]
    public void ByteConcatenation_KeepsItsValue_InAllThreeSpellings()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 2000);
        uno.Serial.Text.Should().Be("4660\n4660\n4660\n11264\n1\nD",
            "0x12/0x34 joins to 4660 whichever way it is written, and big * 256 "
            + "still needs the 32 bits its high half (1) lives in");
    }

    [Test]
    public void ByteConcatenation_DoesNotSpillA32BitTemporary()
    {
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("byte-concat-16bit"), "dist", "debug", "firmware.asm"));

        // A widened 32-bit temporary reaches the frame as four back-to-back STD Y+n
        // stores to ascending offsets. The fixture has exactly one genuine 32-bit
        // value (big * 256); each of the three concatenations added a group of its own
        // before the fix.
        CountFrameSpills(asm, 4).Should().Be(1,
            "only big * 256 needs 32 bits — the byte concatenations must not widen");
    }

    /// <summary>Number of runs of <paramref name="width"/> or more back-to-back
    /// <c>STD Y+n</c> stores to ascending offsets: one multi-byte value spilled.</summary>
    private static int CountFrameSpills(string asm, int width)
    {
        int spills = 0, run = 0, prev = -1;
        foreach (var raw in asm.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;

            var m = System.Text.RegularExpressions.Regex.Match(line, @"^STD\s+Y\+(\d+),");
            if (!m.Success) { run = 0; prev = -1; continue; }

            int offset = int.Parse(m.Groups[1].Value);
            run = offset == prev + 1 ? run + 1 : 1;
            if (run == width) spills++;
            prev = offset;
        }
        return spills;
    }
}
