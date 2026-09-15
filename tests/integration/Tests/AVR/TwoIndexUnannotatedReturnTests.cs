using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/two-index-unannotated-return.
///
/// PyMCU#397. `def __getitem__(self, key): return ...` has no `-> T`, which is "void" to the
/// parser (return-type inference does not run for class methods), so EmitDunderCall's own
/// result slot stayed null and it fell through to a hardcoded Constant(0) regardless of what
/// the body actually computed. A write through one key followed by a read through a
/// DIFFERENT key both answered 0.
///
/// CPython: m[2, 3] = 4 sets self.value to 2*10+3+4 = 27; m[1, 2] then reads 1*10+2+27 = 39.
/// </summary>
[TestFixture]
public class TwoIndexUnannotatedReturnTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("two-index-unannotated-return"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void AReadThroughADifferentKey_CarriesTheEarlierWritesFieldValue()
    {
        Boot().Serial.Text.Should().Contain("39\n",
            "1*10+2+27 = 39; before the fix this printed 0 -- the write's own result never "
            + "reached EmitDunderCall's caller");
    }
}
