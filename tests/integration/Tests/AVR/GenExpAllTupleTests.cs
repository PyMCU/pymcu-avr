// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/genexp-all-tuple: <c>all(0 &lt;= component &lt;= 255 for component in val)</c>
/// -- the adafruit_pixelbuf.py:299 colour check, verbatim, over a tuple argument.
///
/// WHAT DISCRIMINATES: prints 1, 0, 1, 0, END. The 0s are the chain comparison
/// failing (300 &gt; 255, 256 &gt; 255); a generator that evaluated eagerly, misread
/// the chain, or never unrolled prints the wrong row.
/// </summary>
[TestFixture]
public class GenExpAllTupleTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("genexp-all-tuple"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("genexp-all-tuple"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AllOverATupleArg_PrintsPerElementAnswer()
    {
        FullRun(_session).Serial.Text.Should().Contain("1\n0\n1\n0\nEND\n",
            because: "(1,2,3) and (0,255,7) pass the range check; (1,2,300) and (256,0,0) do not");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("1\n0\n1\n0\nEND\n",
            because: "generator expressions are an IR feature -- both front ends must agree");
    }
}
