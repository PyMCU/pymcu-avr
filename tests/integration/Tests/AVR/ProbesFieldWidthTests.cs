using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Pins a silent field-width miscompile (probes-field-width fixture).
///
/// `self.rng = seed` binds a field to an UNANNOTATED `__init__` parameter, whose
/// empty type leaves the field at the uint8 default in DeriveFieldLayout
/// (src/compiler/IR/IRGenerator/Scan.cs). The only other write -- the 31-bit LCG
/// update in `seed()` -- sits inside nested `for` loops, which that scan never
/// visits (it reads top-level method statements only), so neither the widening
/// pass nor the numeric-vs-other field-kind diagnostic ever sees it. The
/// program builds clean, `self.rng` gets a one-byte slot, `rng * 1103515245 +
/// 12345` keeps byte 0 only, and `(rng >> 16) & 3` is always 0: every cell of
/// the "random" world seeds live.
///
/// The assertion is today's BUGGY behaviour, not the correct one: CPython prints
/// 0,0,1,0 for the first four cells of the same world; the firmware prints four
/// 1s. When the field scan learns to look inside loop bodies -- or refuses a
/// param-bound field it cannot type -- this test goes red and the pin moves to
/// the fixed expectation.
/// </summary>
[TestFixture]
public class ProbesFieldWidthTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        // The build succeeding IS part of the pinned behaviour: today nothing
        // warns that a 31-bit value is being stored into a byte.
        _session = new SimSession(PymcuCompiler.BuildFixture("probes-field-width"));

    [Test]
    public void FieldBoundToUnannotatedParam_WrittenInNestedLoop_StaysUint8()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "1\n1\n1\n1\n", maxMs: 800);
        uno.Serial.Text.Should().Be("1\n1\n1\n1\n",
            "the uint8 truncation makes (rng >> 16) & 3 always 0, so every cell " +
            "seeds live; CPython prints 0\\n0\\n1\\n0 for the same world -- " +
            "this equality is the bug's signature, not its absence");
    }

    [Test]
    public void BothFrontEnds_MiscompileIdentically()
    {
        // The C# and Python front ends must agree even on the wrong answer: a
        // divergence here would mean the field layout itself differs by parser.
        PymcuCompiler.BuildFixturePyParser("probes-field-width")
            .Should().Be(PymcuCompiler.BuildFixture("probes-field-width"),
                "both front ends lay the field out the same (wrong) way");
    }
}
