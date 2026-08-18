// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Value-returning methods on nested ZCA fields (self.field.method() -> value),
/// the shape the old "nested member access" diagnostic declared unsupported.
/// Pins the constructed-field, parameter-bound-field and outline-eligible
/// variants so the force-inline/class-recovery routing does not regress.
/// </summary>
[TestFixture]
public class NestedZcaValueTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("nested-zca-value"));

    [Test]
    public void NestedFieldMethods_ReturnValues()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be("5\n43\n43\nD");
    }
}
