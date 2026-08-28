using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/base-call-returns-class (PyMCU#157).
///
/// A base-class call whose base method returns a multi-field class dropped the base body, and
/// the caller added two slots nobody wrote. `super().split(raw)` did it silently and
/// `Base.split(self, raw)` was refused outright. Both spellings are one construct and both are
/// exercised here, because #157's bar is that they behave alike.
///
/// factory-returns-class next door is the same shape one level down and passed throughout, so
/// it is the neighbour that shows this is about the base call and not about returning a class.
///
/// This is a VALUE claim, so the seed is written HERE, after Reset and before the CPU runs. A
/// literal would fold the whole computation and measure the constant folder instead. qemu does
/// not retain a write to GPIOR0, which is why this lives in the avr8sharp harness.
/// </summary>
[TestFixture]
public class BaseCallReturnsClassTests
{
    private const int Gpior0 = 0x3E;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("base-call-returns-class"));

    private string RunWith(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0] = seed;
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        return uno.Serial.Text;
    }

    // seed, then hi = (raw + 10) + raw and lo = (raw + 100) + raw, where raw is seed + 5.
    // The two offsets differ on purpose: an expansion that bound the argument into the
    // receiver's field computes raw + raw and gives the SAME answer for both.
    private static readonly object[] Seeds =
    {
        new object[] { (byte)0,  20,  110 },
        new object[] { (byte)7,  34,  124 },
        new object[] { (byte)40, 100, 190 },
    };

    // DISCRIMINATING. Before the fix both read 0 for every seed, in both spellings.
    [TestCaseSource(nameof(Seeds))]
    public void SuperSpelling_ReachesTheBaseBody(byte seed, int hi, int _)
    {
        RunWith(seed).Should().Contain($"hi={hi}\n",
            "super().split(raw) must return the Pair the base body builds");
    }

    [TestCaseSource(nameof(Seeds))]
    public void UnboundSpelling_ReachesTheSameBody(byte seed, int _, int lo)
    {
        RunWith(seed).Should().Contain($"lo={lo}\n",
            "Base.split(self, raw) is the same construct and must agree with super()");
    }

    // The two spellings are one construct: whatever they do, they must do the same thing. This
    // is what would have caught the fix landing for one spelling only.
    [TestCaseSource(nameof(Seeds))]
    public void BothSpellingsAgree(byte seed, int hi, int lo)
    {
        var text = RunWith(seed);
        (text.Contains($"hi={hi}\n") == text.Contains($"lo={lo}\n")).Should().BeTrue(
            "one spelling must not be fixed while the other is left wrong");
    }

    /// <summary>
    /// EVERY SUITE THAT SEEDS NEEDS ONE OF THESE. Copy it when you write the next one.
    ///
    /// If the simulator does not retain the write to GPIOR0 the firmware reads zero, every seed
    /// produces the same run, and every assertion above passes VACUOUSLY: green, and measuring
    /// nothing. That failure is invisible, because a suite that measures nothing looks exactly
    /// like a suite that measures something and finds it correct.
    ///
    /// INVARIANT rather than discriminating: it fails against the unfixed compiler too, for the
    /// other reason (every seed read 0), so it does not by itself say the defect is fixed.
    /// </summary>
    [Test]
    public void TheSeedIsRetainedAndChangesTheAnswer()
    {
        RunWith(0).Should().NotBe(RunWith(40),
            "if the simulator dropped the GPIOR write, every seed would produce the same run "
            + "and every assertion in this fixture would pass while measuring nothing");
    }
}
