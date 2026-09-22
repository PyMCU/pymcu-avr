using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Regression coverage for a silent field-width miscompile (probes-field-width
/// fixture, PyMCU#488).
///
/// `self.rng = seed` binds a field to an UNANNOTATED `__init__` parameter, whose
/// empty type leaves the field at the uint8 default in DeriveFieldLayout
/// (src/compiler/IR/IRGenerator/Scan.cs). The only other write -- the 31-bit LCG
/// update in `seed()` -- sits inside nested `for` loops, which that scan used to
/// never visit (it read top-level method statements only), so neither the
/// widening pass nor the numeric-vs-other field-kind diagnostic ever saw it. The
/// program built clean, `self.rng` got a one-byte slot, `rng * 1103515245 +
/// 12345` kept byte 0 only, and `(rng >> 16) & 3` was always 0: every cell of
/// the "random" world seeded live.
///
/// The layout scan now walks whole method bodies with the shared statement
/// visitor, so the nested write joins the field's width and `self.rng` is laid
/// out uint32. The firmware answer is the CPython one: 0,0,1,0 for the first
/// four cells of the same world.
/// </summary>
[TestFixture]
public class ProbesFieldWidthTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("probes-field-width"));

    [Test]
    public void FieldBoundToUnannotatedParam_WrittenInNestedLoop_IsUint32()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "0\n0\n1\n0\n", maxMs: 800);
        uno.Serial.Text.Should().Be("0\n0\n1\n0\n",
            "the nested 31-bit write widens self.rng to uint32, so the firmware " +
            "prints the same four cells CPython prints for this world");
    }

    [Test]
    public void BothFrontEnds_CompileIdentically()
    {
        // The C# and Python front ends must agree on the layout: a divergence
        // here would mean the field width itself differs by parser.
        PymcuCompiler.BuildFixturePyParser("probes-field-width")
            .Should().Be(PymcuCompiler.BuildFixture("probes-field-width"),
                "both front ends lay the field out the same way");
    }
}
