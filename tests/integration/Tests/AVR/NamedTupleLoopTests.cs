using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/named-tuple-loop (PyMCU#297, PyMCU#298).
///
/// A tuple of constants bound to a name compiled up to eight elements and was refused at
/// nine -- at the ASSIGNMENT, not at the loop -- while the same elements written as a list
/// compiled at any length and the same tuple written inline at the `for` compiled too.
///
/// Asserted on the values that come out of the UART rather than on the build, because the
/// storage both spellings now share was one byte per element whatever the values were: a
/// 16-bit table built clean and ran on its low byte, which is the first shape a PWM duty
/// cycle takes. The list arms are here because the tuple is only right if it agrees with
/// them.
/// </summary>
[TestFixture]
public class NamedTupleLoopTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("named-tuple-loop"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    // The second column is `(d >> 8) + 1`. It reads 1 all the way down when the loop variable
    // is a byte, which is the column that made the truncation visible on real silicon: every
    // duty in the user's sweep measured the same 4 us pulse.
    [Test]
    public void ATupleOfTenWideConstants_KeepsEveryValue()
        => Transcript().Should().Contain(
            "W 256 2\nW 383 2\nW 512 3\nW 16384 65\nW 16639 65\n"
            + "W 32768 129\nW 32895 129\nW 33024 130\nW 49152 193\nW 65280 256\n");

    [Test]
    public void ATupleOfNineConstants_WalksEveryElement()
        => Transcript().Should().Contain("T 0\nT 7\nT 14\nT 21\nT 28\nT 35\nT 42\nT 49\nT 56\n");

    [Test]
    public void TheSameElementsAsAList_WalkTheSameWay()
        => Transcript().Should().Contain("L 0\nL 7\nL 14\nL 21\nL 28\nL 35\nL 42\nL 49\nL 56\n");

    [Test]
    public void AThreeDigitTupleDoesNotWrapAtAByte()
        => Transcript().Should().Contain("E 100\nE 200\nE 300\nE 400\nE 500\nE 600\nE 700\nE 800\nE 900\n");

    [Test]
    public void ATupleAtTheUnrollLimit_StillWalksEveryElement()
        => Transcript().Should().Contain("S 0\nS 7\nS 14\nS 21\nS 28\nS 35\nS 42\nS 49\n");
}
