// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#391. A class that declares no <c>__init__</c> is refused at its first
/// construction, though CPython synthesizes a trivial no-op constructor for it and runs it.
///
/// Reduced from the issue's two reproducers (tests/oracle/probes/037_raise_method_caller.py
/// and tests/oracle/probes/079_class_attribute_instance_read.py in PyMCU/PyMCU): a class with
/// only methods and no state, whose method raises and is caught, and a class-level constant
/// read back through an instance.
/// </summary>
[TestFixture]
public class DefaultConstructorTests
{
    /// <summary>
    /// The discriminating values are what CPython itself prints for this program: 42 (the
    /// except handler's constant, reached because 9 > 5), 5 (the class attribute) and 6 (5 + 1
    /// read back through the instance). Asserting only that the firmware builds and runs would
    /// pass even if construction silently produced a garbage instance.
    /// </summary>
    [Test]
    public void Issue391_AClassWithNoInitConstructsAndRunsLikeCPython()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("default-constructor")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("DC\n42\n5\n6\nEND\n");
    }
}
