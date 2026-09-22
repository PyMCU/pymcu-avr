// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/raise-message-deferred-print (PyMCU#435): a non-literal raise message
/// (f-string, a call inside one) compiles as a deferred print. <c>print(e)</c>
/// produces CPython's text.
///
/// WHAT DISCRIMINATES: <c>bad address 200</c>,
/// <c>Expected tuple of length 3, got 0</c>, and <c>code 7 temp 36.5</c> -- the
/// last carries a float piece that arrives as a parameter behind a narrower one.
/// A compile that still refused a call in a raise would not build. A compile
/// that discarded the pieces would print empty lines.
///
/// The expected lines are CPython's, running the same program.
/// </summary>
[TestFixture]
[Property("Issue", "435")]
public class RaiseMessageDeferredPrintTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("raise-message-deferred-print"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("raise-message-deferred-print"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s, int maxMs = 5000)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: maxMs);
        return uno;
    }

    [Test]
    public void AnFStringRaisePrintsTheInterpolatedValue()
    {
        FullRun(_session).Serial.Should().ContainLine("bad address 200",
            because: "print(e) must replay f\"bad address {addr}\" as CPython does");
    }

    [Test]
    public void ACallInsideTheFStringIsEvaluated()
    {
        FullRun(_session).Serial.Should().ContainLine("Expected tuple of length 3, got 0",
            because: "len(xs) in the raise used to be refused; it has to run when the raise fires");
    }

    [Test]
    public void AFloatPieceBehindAnIntParameterPrintsItsValue()
    {
        FullRun(_session).Serial.Should().ContainLine("code 7 temp 36.5",
            because: "the float piece arrives as a parameter behind a narrower one; "
                + "mis-delivered, it stores and prints 0.0");
    }

    [Test]
    public void AnFStringConcatenatedWithALiteralReplaysAsOneMessage()
    {
        // The adafruit_seesaw spelling: f"..." "..." across a line break.
        FullRun(_session).Serial.Should().ContainLine(
            "Seesaw hardware ID returned 0x60 is not correct! Please check your wiring.",
            because: "implicit concat folds into one JoinedStr; the deferred print must "
                + "emit the whole message, not just the f-string piece");
    }

    [Test]
    public void AFloatPieceUnderAnFSpecPrintsWithPrecision()
    {
        FullRun(_session).Serial.Text.Should().Contain(
            "bus voltage -0.1V under range\nbus voltage -2.7V under range\n",
            because: "f\"{v:.1f}\" in a raise must replay through uart_write_float_fmt "
                + "exactly as print(f\"{v:.1f}\") does, round-half-even included");
    }

    [Test]
    public void AnIntPieceUnderAnFSpecConvertsToFloat()
    {
        FullRun(_session).Serial.Should().ContainLine("count 9.00 pins",
            because: "CPython converts the int to float under an f spec (\"9.00\"); "
                + "parking it as an int would print \"9\"");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain(
            "bad address 200\nExpected tuple of length 3, got 0\ncode 7 temp 36.5\n"
                + "Seesaw hardware ID returned 0x60 is not correct! Please check your wiring.\n"
                + "bus voltage -0.1V under range\nbus voltage -2.7V under range\n"
                + "count 9.00 pins\nEND\n",
            because: "both front ends must lower the same deferred-print sequence");
    }
}
