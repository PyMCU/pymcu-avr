// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// d.get(key, default) on a dict literal — the compile-time lookup table. A
/// constant key folds; a runtime key lowers to the d[key] compare chain with the
/// miss handed the default instead of raising KeyError.
/// </summary>
[TestFixture]
public class DictLiteralGetTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("dict-literal-get"));

    [Test]
    public void ConstantKeyFoldsAndMissTakesTheDefault()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "d=10 e=99\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("a=20 b=20 c=99");
    }

    [Test]
    public void RuntimeKeyHitsTheCompareChain()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "d=10 e=99\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("d=10 e=99");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
