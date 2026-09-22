using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The PyMCU#489 reproducer, kept green (field-from-method-call fixture).
///
/// `self.v = self._read()` where `_read` carries NO return annotation:
/// TypeInference.InferProgram walked only prog.Functions -- module-level
/// functions -- so the method's `return 300` never joined a return type.
/// InferAssignedFieldType saw an empty ReturnType where a declared `-> T` reads
/// as width evidence, the field kept the uint8 default, and the outlined
/// Dev__read emitted `LDI R24,44; RET` -- its own return truncated to a byte.
///
/// The return-join pass now runs over class methods too, so the inferred type
/// lands on the FunctionDef exactly as a declared one and v is uint16. The
/// printed answers are CPython's, running the same program; under the bug the
/// first line is 44 and the `>> 8` read is 0 -- the tells of a byte field.
/// </summary>
[TestFixture]
public class FieldFromMethodCallTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("field-from-method-call"));

    [Test]
    public void UnannotatedMethodReturn_WidensTheFieldToUint16()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "300\n1\n44\n300\n", maxMs: 800);
        uno.Serial.Text.Should().Be("300\n1\n44\n300\n",
            "CPython's answers for the same program; a uint8 field truncates " +
            "the store to 44 and the `>> 8` read is 0, and LoopDev's " +
            "loop-nested write is the same field at #488 depth");
    }

    [Test]
    public void BothFrontEnds_CompileIdentically()
    {
        PymcuCompiler.BuildFixturePyParser("field-from-method-call")
            .Should().Be(PymcuCompiler.BuildFixture("field-from-method-call"),
                "both front ends infer the same method return width");
    }
}
