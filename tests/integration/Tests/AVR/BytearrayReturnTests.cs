// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/bytearray-return (PyMCU#464): a function that fills a
/// <c>bytearray</c> and returns it. adafruit_bmp280's <c>_read_register</c>.
///
/// WHAT DISCRIMINATES: 208 and 209, the two filled bytes indexed at the
/// call site. A compile that still refused <c>return buf</c> would not build;
/// a scalar return would not index as an array.
/// </summary>
[TestFixture]
public class BytearrayReturnTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("bytearray-return"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("bytearray-return"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void AReturnedBytearrayIsIndexedAtTheCallSite()
    {
        FullRun(_session).Serial.Text.Should().Contain("208\n209\nEND\n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("208\n209\nEND\n");
    }
}
