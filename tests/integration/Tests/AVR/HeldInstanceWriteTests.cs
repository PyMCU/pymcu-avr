using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for PyMCU#183: a write through a method on a field that HOLDS an instance
/// was discarded, and the reader answered with the constructor's value forever. No diagnostic,
/// no crash, a plausible number.
///
/// Three fixtures rather than one program with three chains, and that is load-bearing. A second
/// construction of the same class invalidates the constant, the read stops folding, and the
/// program answers correctly by accident: the first version of this test held all three chains
/// together and passed against the unfixed compiler. Each shape is measured alone.
///
/// Every value is seeded from GPIOR0 and none is a literal. The issue's own reproducer builds
/// with 0 and writes 77, and folding the constructor's 0 is indistinguishable from loading a
/// stored 0 unless the seed varies, so two seeds are run for each.
/// </summary>
[TestFixture]
public class HeldInstanceWriteTests
{
    private const int GPIOR0_ADDR = 0x3E;
    private const int GPIOR1_ADDR = 0x4A;

    private static SimSession _named = null!;
    private static SimSession _anon = null!;
    private static SimSession _readOnly = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _named = new SimSession(PymcuCompiler.BuildFixture("held-write-named"));
        _anon = new SimSession(PymcuCompiler.BuildFixture("held-write-anon"));
        _readOnly = new SimSession(PymcuCompiler.BuildFixture("held-read-folds"));
    }

    private static byte Run(SimSession session, byte seed)
    {
        var uno = session.Reset();
        uno.Data[GPIOR0_ADDR] = seed;
        uno.RunToBreak();
        return uno.Data[GPIOR1_ADDR];
    }

    // outer holds a module-level instance with a name of its own.
    [TestCase((byte)7, (byte)14)]
    [TestCase((byte)3, (byte)10)]
    public void AWriteThroughAHeldNamedInstanceSurvives(byte seed, byte expected)
        => Run(_named, seed).Should().Be(expected,
            "outer.go() forwards to self.inner.set(), and what it wrote is what the read must see");

    // outer holds an instance built inside the constructor call: nameless until lowering.
    [TestCase((byte)7, (byte)14)]
    [TestCase((byte)3, (byte)10)]
    public void AWriteThroughAHeldAnonymousInstanceSurvives(byte seed, byte expected)
        => Run(_anon, seed).Should().Be(expected,
            "Outer(Inner(...)) holds a nameless temp, and the write through it must survive too");

    // The exact failure mode: answering with what the constructor was given.
    [Test]
    public void NeitherShapeAnswersWithTheConstructorsValue()
    {
        Run(_named, 7).Should().NotBe(7, "7 is the seed the constructor was given, not the value written");
        Run(_anon, 7).Should().NotBe(7, "the anonymous shape must not fold the constructor's value either");
    }

    // The other half of the bargain. Marking every nested field of every held object, rather
    // than only the ones a method writes, takes a held Pin's _bit out of compile time -- and the
    // backend needs that as a constant to build the mask, so it is a miscompile, not a lost
    // optimization. A held instance nobody writes through must still fold.
    [TestCase((byte)7)]
    [TestCase((byte)3)]
    public void AHeldInstanceNobodyWritesThroughStillFoldsItsConstructorValue(byte seed)
        => Run(_readOnly, seed).Should().Be(seed,
            "Quiet.peek() only reads, so inner.v is still the value the constructor was given");
}
