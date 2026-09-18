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
/// WHAT DISCRIMINATES: <c>bad address 200</c> and
/// <c>Expected tuple of length 3, got 0</c>. A compile that still refused a call
/// in a raise would not build. A compile that discarded the pieces would print
/// empty lines.
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
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain(
            "bad address 200\nExpected tuple of length 3, got 0\nEND\n",
            because: "both front ends must lower the same deferred-print sequence");
    }
}
