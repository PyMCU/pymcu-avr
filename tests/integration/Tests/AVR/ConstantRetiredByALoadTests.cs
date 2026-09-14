using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/constant-retired-by-a-load.
///
/// PyMCU#359. PropagateCopies decided which instructions retire a tracked constant from a
/// hand-written list, and every load was outside it: `acc = 0` then `acc = buf[2]` kept the 0
/// and every later read folded to it. The store was correct and nothing was reported, so the
/// only way to see it was to run the program and compare against CPython -- PYMCU_NO_OPT=1
/// printed the right answer, which is what placed it in the optimizer.
///
/// Measured before the fix on this simulator: 0 for the first six, against 52, 52, 52, 52, 54
/// and 52. The last two tests are the shapes that were already right -- `+` lowered through a
/// copy the old list did cover, and a non-zero seed with no identity to fold -- so a fix that
/// traded one for the other shows here.
///
/// Every expectation is CPython's answer for the same lines.
/// </summary>
[TestFixture]
public class ConstantRetiredByALoadTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("constant-retired-by-a-load"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void APlainAssignmentFromTheArray_RetiresTheConstant()
    {
        Boot().Serial.Text.Should().Contain("52\n",
            "acc = 0 then acc = buf[2] is 52; the load must retire the tracked 0");
    }

    [Test]
    public void AnOrWithZero_RetiresTheConstant()
    {
        Boot().Serial.Text.Should().Contain("52\n",
            "0 | x folds to x, and the name it is stored into stops being 0");
    }

    [Test]
    public void TwoOrsOverTwoBytes_AccumulateBoth()
    {
        Boot().Serial.Text.Should().Contain("54\n",
            "0x34 | 0x12 is 54; a stale 0 after the first OR would print 18");
    }

    [Test]
    public void TheValueStoredBackIntoTheArray_IsTheLoadedOne()
    {
        Boot().Serial.Text.Should().Contain("52\n",
            "the stale read reached a store too, so this is a miscompile and not a print defect");
    }

    // The two shapes that were already right, kept so the fix cannot be had by breaking them.
    [Test]
    public void APlusWithZero_IsStillRight()
    {
        Boot().Serial.Text.Should().Contain("52\n", "0 + x was lowered through a copy");
    }

    [Test]
    public void ANonZeroSeed_IsStillRight()
    {
        Boot().Serial.Text.Should().Contain("53\n", "1 | 0x34 is 53; no identity to fold");
    }
}
