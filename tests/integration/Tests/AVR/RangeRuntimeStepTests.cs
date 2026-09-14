using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A signed runtime step picks the direction of its range loop at run time (PyMCU#286).
///
/// The exit test was chosen at compile time from a constant step only, so a step held in a
/// variable always got the ascending test and range(10, 0, step) with step = -2 exited before
/// its first iteration.
/// </summary>
[TestFixture]
public class RangeRuntimeStepTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-runtime-step"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    [Test]
    public void ANegativeRuntimeStep_CountsDown()
        => Transcript().Should().Contain("A 5 10\n", "range(10, 0, -2) is 5 steps and range(10, 0, -1) is 10");

    [Test]
    public void AStepAgainstTheDirection_RunsZeroTimes()
        => Transcript().Should().Contain("B 0 0\n", "range(10, 0, 1) and range(0, 10, -1) are both empty");

    [Test]
    public void APositiveRuntimeStep_StillCountsUp()
        => Transcript().Should().Contain("C 10 2\n", "range(0, 10, 1) is 10 steps and range(0, 10, 7) is 2");
}
