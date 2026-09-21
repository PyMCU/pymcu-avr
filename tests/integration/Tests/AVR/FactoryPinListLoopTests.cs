// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The list form of PyMCU/PyMCU#421 -- adafruit_pcf8574's
/// `pins = [pcf.get_pin(i) for i in range(8)]` then `for p in pins:
/// p.switch_to_output(...)`. Each comprehension element is a factory call's
/// result; the unrolled loop has to carry the returned class into p or the
/// method call mangles to an undefined `p_switch_to_output`.
/// </summary>
[TestFixture]
public class FactoryPinListLoopTests
{
    /// <summary>
    /// Each pin's own _n (0, 1, 2) plus the owner's call count (3): the output is
    /// only right if every element slot kept the factory's class.
    /// </summary>
    [Test]
    public void AComprehensionOfFactoryCalls_DispatchesPerElement()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("factory-pin-list-loop")).Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("0\n1\n2\n3\ndone\n");
    }
}
