using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/uint32-compare-max.
///
/// `if x &lt;= C:` is lowered on its false edge to "jump away if x &gt; C". For a 4-byte type
/// the backend treated int.MaxValue as the largest value the type can hold and emitted
/// NOTHING for that threshold, on the grounds that nothing can exceed it. int.MaxValue is
/// the largest SIGNED 32-bit value; a uint32 reaches 4294967295, so for every x above
/// 2147483647 the comparison was simply absent and the then-branch always ran.
///
/// The 8- and 16-bit forms of the same code emit nothing at their own maximum too, and there
/// it is CORRECT, because 0xFF and 0xFFFF are values an int can hold. 32 bits is the width
/// where the type outgrows the int the threshold is kept in, and it is the only one that was
/// wrong.
///
/// WHAT DISCRIMINATES: BoundaryExceeded_TakesTheElseBranch. Against the unfixed backend the
/// firmware writes 1 there instead of 2, because no compare and no branch were emitted at all.
///
/// WHAT IS INVARIANT, and here on purpose: the other three outputs. The `x &gt; C` spelling
/// goes through a different lowering that was already right, the 16-bit case is the same
/// code path being right at its own width, and a threshold below the boundary is the
/// ordinary case a fix must not disturb.
///
/// Data-space addresses (ATmega328P):
///   GPIOR0=0x3E (seed), GPIOR1=0x4A, GPIOR2=0x4B, OCR0A=0x47, OCR0B=0x48
/// </summary>
[TestFixture]
public class Uint32CompareMaxTests
{
    private const int Gpior0 = 0x3E;
    private const int Gpior1 = 0x4A;
    private const int Gpior2 = 0x4B;
    private const int Ocr0A  = 0x47;
    private const int Ocr0B  = 0x48;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("uint32-compare-max"));

    // seed 10 -> x = 1_000_000_000, below the boundary
    // seed 30 -> x = 3_000_000_000, above it and still a uint32
    private ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[Gpior0] = seed;
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void BoundaryExceeded_TakesTheElseBranch() =>
        RunWithSeed(30).Data[Gpior1].Should()
            .Be(2, "3000000000 <= 2147483647 is false, so the else branch must run");

    [Test]
    public void BelowTheBoundary_TakesTheThenBranch() =>
        RunWithSeed(10).Data[Gpior1].Should()
            .Be(1, "1000000000 <= 2147483647 is true");

    [Test]
    public void BothSpellingsOfTheSameQuestionAgree()
    {
        // `x <= C` and `x > C` lower through different paths in the backend. Whatever they
        // answer, they have to answer it the same way.
        foreach (byte seed in new byte[] { 0, 10, 21, 22, 30, 42 })
        {
            var uno = RunWithSeed(seed);
            uno.Data[Gpior2].Should().Be(uno.Data[Gpior1],
                $"seed {seed}: `x > C` must agree with `x <= C`");
        }
    }

    [Test]
    public void SixteenBitsAtItsOwnMaximum_IsUnaffected() =>
        RunWithSeed(30).Data[Ocr0A].Should()
            .Be(1, "a uint16 is always <= 65535, so the then branch must run");

    [Test]
    public void AThresholdBelowTheBoundary_StillCompares()
    {
        RunWithSeed(10).Data[Ocr0B].Should().Be(1, "1000000000 <= 1500000000");
        RunWithSeed(30).Data[Ocr0B].Should().Be(2, "3000000000 <= 1500000000 is false");
    }
}
