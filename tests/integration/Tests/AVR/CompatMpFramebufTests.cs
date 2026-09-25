using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-mp-framebuf: the MicroPython layer's `framebuf` drawing the
/// same picture the interpreter's own C module draws.
///
/// `framebuf` is a builtin of MicroPython, written in C
/// (extmod/modframebuf.c), so there is no upstream Python source to diff the
/// layer's implementation against. What can be compared is what it draws: the
/// fixture's main.py runs unchanged under the real interpreter, and
/// reference/micropython.txt is what it printed there (unix port,
/// v1.29.0-preview at 9f396bba8d) -- every byte of a 32x16 MONO_VLSB buffer
/// after fill, text, hline, vline, rect outlined and filled, fill_rect, line,
/// pixel read and written, the two out-of-bounds reads that answer None, the
/// clipped rectangles and lines, and two scrolls.
///
/// The firmware has to print that file back, line for line, under both front
/// ends. A buffer byte that differs is a drawing bug; a `None` that turns into
/// a number is the out-of-bounds answer diverging from upstream.
/// </summary>
[TestFixture]
public class CompatMpFramebufTests
{
    private const string Fixture = "compat-mp-framebuf";

    private static string[] _reference = null!;
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _reference = File.ReadAllLines(
            Path.Combine(PymcuCompiler.FixtureDir(Fixture), "reference", "micropython.txt"));
        _session   = new SimSession(PymcuCompiler.BuildFixture(Fixture));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser(Fixture));
    }

    [Test]
    public void Draws_WhatMicroPythonDraws() => AssertMatchesReference(_session);

    [Test]
    public void Draws_WhatMicroPythonDraws_PyParser() => AssertMatchesReference(_pySession);

    private static void AssertMatchesReference(SimSession session)
    {
        var uno = session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        var produced = uno.Serial.Text.Replace("\r\n", "\n")
                          .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < Math.Min(produced.Length, _reference.Length); i++)
            produced[i].Should().Be(_reference[i],
                $"line {i + 1} of the framebuf dump must be what MicroPython printed");

        produced.Length.Should().Be(_reference.Length,
            "the firmware must print the whole buffer the interpreter printed");
    }
}
