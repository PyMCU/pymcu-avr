// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for the except-dotted fixture.
/// `except mod.Exc:` and `except (mod.A, mod.B):` -- the spellings Adafruit libraries
/// use for their own exception classes (irremote simpletest).
/// </summary>
[TestFixture]
public class ExceptDottedTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("except-dotted"));

    [Test]
    public void SingleDottedTypeCatches()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("a:repeat");
        uno.Serial.Should().NotContain("a:missed");
    }

    [Test]
    public void TupleOfDottedTypesSharesOneHandler()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("b: bad pulse train");
        uno.Serial.Should().ContainLine("c: repeat frame");
    }

    [Test]
    public void UnnamedTupleMemberPropagatesPast()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: 4000);
        uno.Serial.Should().ContainLine("d:outer");
        uno.Serial.Should().NotContain("d:wrong handler");
        uno.Serial.Should().NotContain("d:missed");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
