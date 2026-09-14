using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// reversed(range()), x in range(), enumerate(range()) over runtime bounds and a comprehension
/// with a step (PyMCU#287, PyMCU#288).
///
/// Three of these were refused outright and the comprehension silently ignored its step. The
/// seed (GPIOR0 = 5) feeds `x` and `lo`, so the membership tests and the runtime ranges are
/// decided on the chip, not by the folder.
/// </summary>
[TestFixture]
public class RangeSurfaceTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("range-surface"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    [Test]
    public void ReversedRange_WalksDown()
        => Transcript().Should().Contain("A 2\nA 1\nA 0\n").And.Contain("B 6\nB 4\nB 2\n");

    [Test]
    public void ReversedRange_OverARuntimeBound()
        => Transcript().Should().Contain("H 4\nH 3\nH 2\nH 1\nH 0\n", "reversed(range(lo)) with lo == 5");

    [Test]
    public void MembershipInARange_IsDecidedAtRunTime()
        => Transcript().Should().Contain("C in\nD notin\nE odd\n", "5 in range(10), not in range(6, 10), in range(1, 10, 2)");

    [Test]
    public void EnumerateOverARuntimeRange_CountsAndWalks()
        => Transcript().Should().Contain("F 0 5\nF 1 6\nF 2 7\n");

    [Test]
    public void AComprehensionOverARange_HonoursTheStep()
        => Transcript().Should().Contain("G 0\nG 2\nG 4\nG 6\nG 8\n");
}
