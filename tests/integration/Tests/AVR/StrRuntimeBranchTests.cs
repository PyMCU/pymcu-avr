using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A str the program rebinds on another path (PyMCU issue #145). The seed comes from
/// GPIOR0, written into the simulation before the run: qemu does not retain a write to
/// that register, so a program that seeds itself reads back zero and one branch is picked
/// for the reader. Every line after the banner is decided by the seed, so the two runs
/// must not agree on any of them.
/// </summary>
[TestFixture]
public class StrRuntimeBranchTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("str-runtime-branch"));

    private static string RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[0x3E] = seed;              // GPIOR0
        uno.RunMilliseconds(200);
        return uno.Serial.Text;
    }

    [Test]
    public void SeedBelowThreshold_ReadsTheValueFromBeforeEachBranch()
        => RunWithSeed(0).Should().Contain("SB\nidle\nne\nstart\nidle\n");

    [Test]
    public void SeedAboveThreshold_ReadsTheValueTheBranchWrote()
        => RunWithSeed(20).Should().Contain("SB\nrunning\neq\nlooped\nrunning\n");

    [Test]
    public void TheTwoSeedsDoNotProduceTheSameOutput()
        => RunWithSeed(0).Should().NotBe(RunWithSeed(20));
}
