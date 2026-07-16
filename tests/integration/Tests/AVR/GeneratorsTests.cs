// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Generators: `yield` lowers to the coroutine state machine (poll() returns
/// 2 = yielded / 1 = working / 0 = done) and `for x in gen(...)` desugars to a
/// poll loop -- including break abandoning the generator mid-iteration.
/// </summary>
[TestFixture]
public class GeneratorsTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("generators"));

    [Test]
    public void YieldsEachValueExactlyOnce_AndSums()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "S:15\n", maxMs: 3000);
        // 1, 2, 4, 8 each exactly once (the old poll protocol repeated values).
        var text = uno.Serial.Text;
        text.Should().Contain("GEN\n1\n2\n4\n8\nS:15");
    }

    [Test]
    public void Break_AbandonsGenerator()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "F:8\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("F:8");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
