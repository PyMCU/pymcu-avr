// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/float-pow (PyMCU#463): <c>pow(x, 2.5)</c> with a runtime float base,
/// the sRGB gamma spelling in unmodified adafruit_tcs34725.
///
/// WHAT DISCRIMINATES: 176, <c>int((0.5 ** 2.5) * 1000)</c> in IEEE-754 single.
/// The integer unroll <c>x ** 2</c> is 250 on the same seed, so a lowering that
/// multiplied instead of calling powf would not print 176. GPIOR0 is the volatile
/// seed so the constant folder cannot answer.
///
/// The expected lines are CPython's, running the same arithmetic in float32.
/// </summary>
[TestFixture]
public class FloatPowTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("float-pow"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("float-pow"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void PowOfARuntimeFloatBase_IsTheIeee754SingleOfCPython()
    {
        FullRun(_session).Serial.Text.Should().Contain("176\n176\n250\nEND\n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("176\n176\n250\nEND\n");
    }
}
