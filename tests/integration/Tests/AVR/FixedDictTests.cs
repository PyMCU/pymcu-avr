// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// pymcu.collections.FixedDict: mutable fixed-capacity dict (open addressing over
/// fixed per-instance arrays, no heap). Insert/overwrite, membership, len, get
/// with default, pop (tombstone), KeyError on a popped key, ValueError inserting
/// into a full dict, clear -- with two instances of different capacities.
/// </summary>
[TestFixture]
public class FixedDictTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("fixeddict"));

    [Test]
    public void InsertOverwriteAndLookup()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "G2:7\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("G:6");
        uno.Serial.Should().ContainLine("G2:7");
    }

    [Test]
    public void MembershipLenAndGetDefault()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "D:99\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("C:1");
        uno.Serial.Should().ContainLine("C:0");
        uno.Serial.Should().ContainLine("L:2");
    }

    [Test]
    public void Pop_Tombstone_ThenKeyErrorCaught()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "E:caught\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("P:7");
        uno.Serial.Should().NotContain("E:missed");
    }

    [Test]
    public void FullDict_RaisesValueError_AndClearResets()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "Z:0\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("F:caught");
        uno.Serial.Should().NotContain("F:missed");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
