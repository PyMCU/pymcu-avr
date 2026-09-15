// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#421. A method call that returns a class instance -- reduced from
/// adafruit_pcf8574.py's PCF8574.get_pin() / DigitalInOut.switch_to_output() -- left the
/// assignment target untyped, so the next method call on it mangled to an undefined function.
/// </summary>
[TestFixture]
public class MethodFactoryReturnsClassTests
{
    /// <summary>
    /// The discriminating value is 1 (True): the write only reaches o.written if the method
    /// call correctly dispatched on the receiver get_pin returned.
    /// </summary>
    [Test]
    public void Issue421_TheFactoryMethodsReturnedInstanceDispatchesCorrectly()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("method-factory-returns-class")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("MF\n1\nEND\n");
    }
}
