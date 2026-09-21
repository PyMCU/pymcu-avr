// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the strjoin fixture.
/// `sep.join(<compile-time seq>)` in expression and value position: the bus_device
/// simpletest's `print("".join(f"{x:02x}" for x in result))`.
/// </summary>
[TestFixture]
public class StrJoinTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("strjoin"));

    [Test]
    public void GeneratorOverBytearrayPrintsJoined()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("0aff42");
        uno.Serial.Should().ContainLine("0a, ff, 42");
    }

    [Test]
    public void AssignmentFormAndChrElements()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("0A-FF-42");
        uno.Serial.Should().ContainLine("ABC");
        uno.Serial.Should().ContainLine("a.b.c");
    }

    [Test]
    public void RuntimeFilterCountsProducedElements()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("255");
        uno.Serial.Should().ContainLine("0a,42");
        uno.Serial.Should().ContainLine("7 1025566");
        uno.Serial.Should().ContainLine("01012");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
