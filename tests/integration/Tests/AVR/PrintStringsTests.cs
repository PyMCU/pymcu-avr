using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/print-strings (PyMCU#80, PyMCU#82).
///
/// Two silent wrong outputs on a clean build. `msg = "hello"` then `print(msg)` streamed the
/// flash id as a decimal number, printing 256 where the text belonged, because only the
/// ANNOTATED form recorded the name as a string. And `chr(n)` is the byte itself, which is
/// right internally and wrong for print, so print(chr(65)) sent "65" instead of "A".
///
/// Read off the UART, which is the only way to catch this class: both versions compiled.
/// </summary>
[TestFixture]
public class PrintStringsTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("print-strings"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        return uno;
    }

    [Test]
    public void UnannotatedStringVariable_PrintsItsText()
    {
        Boot().Serial.Text.Should().Contain("hello\n", "and must not print the flash id as a number");
    }

    [Test]
    public void ChrOfAConstant_PrintsTheCharacter()
    {
        Boot().Serial.Text.Should().Contain("A\n", "chr(65) is 'A', not 65");
    }

    [Test]
    public void ChrOfARuntimeValue_PrintsTheCharacter()
    {
        Boot().Serial.Text.Should().Contain("B\n", "chr(66 + seed) with seed 0 is 'B'");
    }

    [Test]
    public void NoDecimalLeakedIntoTheOutput()
    {
        Boot().Serial.Text.Should().NotContain("256", "the flash id must never reach the output");
    }
}
