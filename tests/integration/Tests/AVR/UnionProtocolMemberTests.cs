// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/union-protocol-member (PyMCU#465): a Protocol named in a
/// constructor Union is structural. adafruit_debouncer's
/// <c>Union[ROValueIO, Callable[[], bool]]</c> accepts a Pin that has
/// <c>.value</c> even though Pin is not named ROValueIO.
///
/// WHAT DISCRIMINATES: 5, the Pin constructor argument; 2, one construction
/// per Union arm. A compile that still required the class name would not build.
/// </summary>
[TestFixture]
public class UnionProtocolMemberTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("union-protocol-member"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("union-protocol-member"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void APinSatisfiesTheProtocolMemberOfTheUnion()
    {
        FullRun(_session).Serial.Text.Should().Contain("5\n2\nEND\n");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("5\n2\nEND\n");
    }
}
