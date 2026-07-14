// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// f-strings as VALUES: `s = f"..."` builds the string into a compiler-managed
/// fixed buffer (pymcu.strfmt lowering) -- no heap. Verifies formatting (decimal,
/// signed, hex/width specs), len(s) as the tracked length, s[i] indexing,
/// re-assignment in a loop (buffer reuse) and streaming via print(s)/write_str(s).
/// </summary>
[TestFixture]
public class FStringValueTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("fstring-value"));

    [Test]
    public void Formats_IntSignedAndHexIntoBuffer()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "t=23C reg=beef n=-42\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("t=23C reg=beef n=-42");
    }

    [Test]
    public void Len_ReturnsFormattedLength_NotCapacity()
    {
        var uno = Sim();
        // "t=23C reg=beef n=-42" is 20 chars; the buffer capacity is larger.
        uno.RunUntilSerial(uno.Serial, "L:20\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("L:20");
    }

    [Test]
    public void Indexing_ReadsFormattedByte()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "B:t\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("B:t");
    }

    [Test]
    public void LoopReassignment_ReusesBuffer()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "k=0 k=1 k=2 \n", maxMs: 3000);
        uno.Serial.Should().ContainLine("k=0 k=1 k=2 ");
    }

    [Test]
    public void FormatSpecs_SpaceAndZeroPad()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "pad=[  7]=[007]\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("pad=[  7]=[007]");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
