using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outline-branchy.
///
/// An @inline expansion that carries its own control flow is outlined too, as long
/// as the region is single-entry / single-exit. UART.write_hex() is the case: two
/// if/else pairs picking a digit per nibble, and inside each uart_write() a
/// "wait until UDRE0" polling loop. Every branch and every back-edge stays inside
/// the expansion, so the repeated copies collapse into shared subroutines that keep
/// the branches and the loop in the outlined body (with freshly renamed labels).
///
/// The four bytes exercise both sides of every branch -- 0x00 low/low, 0xFF
/// high/high, 0x5A and 0xA5 one of each -- so a shared body that selected the wrong
/// digit could not produce the expected text.
/// </summary>
[TestFixture]
public class OutlineBranchyTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outline-branchy"));

    [Test]
    public void OutlinedBranches_StillPrintEveryNibble()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("00FF5AA5"), maxMs: 800);
        uno.Serial.Text.Should().Contain("HX", "boot banner is emitted");
        uno.Serial.Text.Should().Contain("00FF5AA5",
            "the shared write_hex body picks the right digit on both sides of both branches");
    }

    [Test]
    public void RepeatedWriteHex_IsSharedSubroutines_WithTheBranchesInside()
    {
        var lines = File.ReadAllLines(Path.Combine(
            PymcuCompiler.FixtureDir("outline-branchy"), "dist", "debug", "firmware.asm"))
            .Select(l => l.Trim()).ToList();

        var bodies = lines.Where(l => l.StartsWith("__pymcu_outline_") && l.EndsWith(":") &&
                                      !l.Contains(".L")).ToList();
        var branchLabels = lines.Where(l => l.StartsWith("__pymcu_outline_") && l.Contains(".L") &&
                                            l.EndsWith(":")).ToList();
        int calls = lines.Count(l => l.StartsWith("CALL") && l.Contains("__pymcu_outline_"));

        using var _ = new AssertionScope();
        bodies.Should().NotBeEmpty("the repeated write_hex expansions are outlined");
        branchLabels.Should().NotBeEmpty(
            "the outlined bodies keep their own labels -- region-internal control flow " +
            "is moved into the subroutine, not left behind at the call sites");
        calls.Should().BeGreaterThan(bodies.Count,
            "every outlined body is reached from more sites than it costs to define");
    }
}
