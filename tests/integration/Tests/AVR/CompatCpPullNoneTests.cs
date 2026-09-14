using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-pull-none (PyMCU#306): `pull = None` builds and leaves the pin without
/// a pull, next to a `Pull.UP` on the pin beside it.
///
/// None had no value to compare against, so the layer's `match` stayed a run-time match and
/// every arm was lowered -- including the Pull.DOWN arm, whose compile-time refusal fired for
/// a program that never asked for a pull-down.
/// </summary>
[TestFixture]
public class CompatCpPullNoneTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-pull-none"));

    [Test]
    public void NoneLeavesThePinWithoutAPullAndPullUpStillSetsOne()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        uno.Serial.Text.Should().Contain("DDR 0", "both pins stay inputs");
        uno.Serial.Text.Should().Contain("PORT 8", "D3 has its pull-up (bit 3) and D2 has none");
    }
}
