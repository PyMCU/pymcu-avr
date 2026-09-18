// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/namedtuple: <c>Name = namedtuple("Name", ("a", "b"))</c> is a
/// compile-time ZCA class, which is how adafruit_irremote writes
/// <c>IRMessage</c>. Construction, field access, <c>isinstance</c>, keyword
/// arguments must all survive both front ends.
///
/// WHAT DISCRIMINATES: <c>3</c>, <c>5</c>, <c>1</c>, <c>7</c>, END.
/// </summary>
[TestFixture]
public class NamedtupleTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("namedtuple"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("namedtuple"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void FieldsAreTheConstructorArguments()
    {
        FullRun(_session).Serial.Text.Should().StartWith("3\n5\n",
            because: "namedtuple fields are the constructor arguments, accessed by name");
    }

    [Test]
    public void IsInstanceAndKeywordSurvive()
    {
        FullRun(_session).Serial.Text.Should().Contain("3\n5\n1\n7\nEND\n",
            because: "isinstance folds and keyword construction binds the named field");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("3\n5\n1\n7\nEND\n",
            because: "both front ends must rewrite namedtuple to the same ZCA class");
    }
}
