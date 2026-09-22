using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The PyMCU#488 reproducer, kept green (field-width-nested-write fixture).
///
/// `self.rng = seed` binds a field to an UNANNOTATED `__init__` parameter; the
/// only other write is the 31-bit LCG update inside nested `for` loops in
/// `seed()`. DeriveFieldLayout used to read top-level method statements only,
/// so the nested write was invisible: the field stayed uint8, every store kept
/// byte 0, and the program compiled clean -- silent wrong code.
///
/// The layout scan now walks whole method bodies, so the nested write joins the
/// field's width and `self.rng` is uint32. The printed answers are CPython's,
/// running the same program; under the bug the first line is the low byte and
/// the `>> 16` read is 0.
/// </summary>
[TestFixture]
public class FieldWidthNestedWriteTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("field-width-nested-write"));

    [Test]
    public void NestedLoopWrite_WidensTheFieldToUint32()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "666838957\n10175\n173\n", maxMs: 800);
        uno.Serial.Text.Should().Be("666838957\n10175\n173\n",
            "CPython's answers for the same program; a uint8 field truncates " +
            "every store to byte 0, so it prints 173 / 0 / 173 -- the LCG's " +
            "low byte matches either way, the high reads are the tell");
    }

    [Test]
    public void BothFrontEnds_CompileIdentically()
    {
        PymcuCompiler.BuildFixturePyParser("field-width-nested-write")
            .Should().Be(PymcuCompiler.BuildFixture("field-width-nested-write"),
                "both front ends lay the field out the same way");
    }
}
