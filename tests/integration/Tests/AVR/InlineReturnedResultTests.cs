using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/inline-returned-result (PyMCU#302): an @inline helper that returns its own
/// parameter, called in return position from another @inline selector's `match`, from a class
/// constructor storing the result in an annotated local, reaches the register.
///
/// The HAL this shape came from had lost the helper's `return`, and the prescaler arrived as
/// the low byte of RAMEND: TCCR0B = 0x3F instead of 3. That is refused at compile time now;
/// this pins the value on the shape that compiles.
/// </summary>
[TestFixture]
public class InlineReturnedResultTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("inline-returned-result"));

    [Test]
    public void TheReturnedCallsValueReachesTheTimer()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        uno.Serial.Text.Should().Contain("B 3 A 131 O 128",
            "500 Hz is the 976 Hz bucket, prescaler code 3; 0x3F is the stack-pointer byte");
    }
}
