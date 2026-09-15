using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/bytearray-field-init.
///
/// PyMCU#392. `self.data = bytearray(...)` inside __init__ reached only the generic
/// expression visitor, which has no lowering for the bytearray() builtin, and refused with
/// "a Python builtin that PyMCU does not provide" -- true of no import adding it, false of
/// what already worked one binding away (`buf = bytearray(N); self.data = buf`).
///
/// Every expectation is CPython's answer for the same lines against b"abcd".
/// </summary>
[TestFixture]
public class BytearrayFieldInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("bytearray-field-init"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void ASingleByteReadThroughTheFieldBytearray_ReadsTheCorrectByte()
    {
        Boot().Serial.Text.Should().Contain("98\n",
            "b\"abcd\"[1] is 'b' == 98; the field's own storage must answer, not a fabricated 0");
    }

    [Test]
    public void ASecondByteRead_ConfirmsTheWholeBufferIsReal()
    {
        Boot().Serial.Text.Should().Contain("99\n", "b\"abcd\"[2] is 'c' == 99");
    }

    [Test]
    public void ASliceOfTheFieldBytearray_ReprsLikeCPython()
    {
        Boot().Serial.Text.Should().Contain("bytearray(b'bc')\n",
            "b\"abcd\"[1:3] reprs as the two-byte slice, reading through the field's storage");
    }
}
