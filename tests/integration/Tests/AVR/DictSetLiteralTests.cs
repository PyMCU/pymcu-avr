// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Closed dict/set literals as compile-time lookup tables: constant-key fold,
/// runtime-key compare chain, KeyError on a missing runtime key (caught by
/// try/except), set membership, len(), and string-keyed constant lookups.
/// </summary>
[TestFixture]
public class DictSetLiteralTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("dict-set-literal"));

    [Test]
    public void ConstAndRuntimeKeyLookups()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "R:20\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("V:30");
        uno.Serial.Should().ContainLine("R:20");
    }

    [Test]
    public void MissingRuntimeKey_RaisesKeyError_Caught()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "E:caught\n", maxMs: 3000);
        uno.Serial.Should().NotContain("E:missed");
    }

    [Test]
    public void SetMembership_RuntimeAndConstFold()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "S:0\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("S:1");
        uno.Serial.Should().NotContain("S:bad");
    }

    [Test]
    public void Len_AndStringKeyedLookup()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "M:2\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("N:3");
        uno.Serial.Should().ContainLine("M:2");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
