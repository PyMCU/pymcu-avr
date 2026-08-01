using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the multi-value return annotation on @inline functions.
/// Exercises:
///   - <c>-&gt; (uint8, uint8)</c> unpacked at the call site
///   - the equivalent <c>-&gt; tuple[uint8, uint8]</c> spelling
///   - <c>-&gt; (uint8, uint16)</c>: the annotated element widths reach the result
///     slots, so the wider value survives instead of truncating to 8 bits
/// </summary>
[TestFixture]
public class TupleReturnAnnotationTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("tuple-return-annotation"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "TA\n", maxMs: 200);
        return uno;
    }

    [Test]
    public void Boot_SendsBanner() =>
        Boot().Serial.Text.Should().Contain("TA");

    [Test]
    public void ParenthesisedAnnotation_DivMod8_Unpacks()
    {
        // divmod8(10, 3) declared `-> (uint8, uint8)` → Q:03 R:01
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("R:01\n"), maxMs: 300);
        uno.Serial.Text.Should().Contain("Q:03", "divmod8(10,3) quotient should be 3");
        uno.Serial.Text.Should().Contain("R:01", "divmod8(10,3) remainder should be 1");
    }

    [Test]
    public void SubscriptedAnnotation_Split16_Unpacks()
    {
        // split16(0x022C) declared `-> tuple[uint8, uint8]` → H:02 L:2C
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("L:2C\n"), maxMs: 400);
        uno.Serial.Text.Should().Contain("H:02", "split16(0x022C) high byte should be 0x02");
        uno.Serial.Text.Should().Contain("L:2C", "split16(0x022C) low byte should be 0x2C");
    }

    [Test]
    public void MixedWidths_Uint16Element_IsNotTruncated()
    {
        // scale(2) is declared `-> (uint8, uint16)`; 2 * 300 = 600 = 0x0258.
        // With both result slots defaulting to uint8 the high byte would be 0x00.
        var uno = Boot();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("T:58\n"), maxMs: 500);
        uno.Serial.Text.Should().Contain("N:02", "scale(2) first element should be 2");
        uno.Serial.Text.Should().Contain("S:02", "2*300 = 600 = 0x0258, high byte 0x02");
        uno.Serial.Text.Should().Contain("T:58", "2*300 = 600 = 0x0258, low byte 0x58");
    }
}
