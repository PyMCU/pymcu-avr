// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/quoted-dotted-annotation: <c>obj: "Pack.Dev"</c> is the
/// dotted class <c>Pack.Dev</c>. Adafruit si7021 writes
/// <c>obj: "adafruit_si7021.SI7021"</c>.
///
/// WHAT DISCRIMINATES: prints 17.
/// </summary>
[TestFixture]
public class QuotedDottedAnnotationTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("quoted-dotted-annotation"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("quoted-dotted-annotation"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AQuotedDottedClass_ReadsTheField()
    {
        FullRun(_session).Serial.Text.Should().Contain("17\nEND\n",
            because: "obj: \"Pack.Dev\" is Pack.Dev, so obj.a is the 17 the constructor stored");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("17\nEND\n",
            because: "both front ends must unquote a dotted class annotation");
    }
}
