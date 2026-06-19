using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/divmod16 — the divmod() built-in at 16-bit width.
///
/// divmod(3000, 10) must yield (300, 0). A quotient of 300 exceeds 8 bits, so a
/// regression to the old uint8/__div8 lowering would narrow it (300 &amp; 0xFF == 44);
/// seeing the exact "300" proves the wide result keeps its 16 bits.
/// </summary>
[TestFixture]
public class DivMod16Tests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("divmod16"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "DM\n", maxMs: 200);
        return uno;
    }

    [Test]
    public void Boot_SendsBanner() =>
        Boot().Serial.Text.Should().Contain("DM");

    [Test]
    public void WideQuotient_NotNarrowedToEightBits()
    {
        // 3007 // 10 == 300; the old __div8 lowering would print 44.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("300\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("300", "3007 // 10 = 300 at 16-bit width");
    }

    [Test]
    public void Remainder_Correct()
    {
        // 3000 % 10 == 0.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("300\n0\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("300\n0", "3000 % 10 = 0");
    }
}
