using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A single-field instance mutated inside a method's loop, and the method's value (PyMCU#292).
///
/// An unannotated method's `return self.value` reached the caller as None; the field's
/// constant lived under the instance's own name and survived the loop, so every read folded
/// to the constructor's value; and `.value` took the register-read path, so once the instance
/// had an SRAM slot the writes went there and the reads still came from the scalar. The
/// transcript is what CPython prints for the same file.
/// </summary>
[TestFixture]
public class ZcaMethodLoopReturnTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-method-loop-return"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    [TestCase("A 3", "a plain return of the field")]
    [TestCase("B 13", "the field written in an if, then returned")]
    [TestCase("C 15", "the field written in a while, then returned")]
    [TestCase("D 25", "the field written in a runtime range loop, then returned")]
    [TestCase("E 28", "the same method again, unrolled this time")]
    [TestCase("F 7", "a constant return after a while")]
    [TestCase("G 45", "the Fader that summed 0..9")]
    public void EachMethod_ReturnsWhatCPythonReturns(string line, string shape)
        => Transcript().Should().Contain(line + "\n", shape);
}
