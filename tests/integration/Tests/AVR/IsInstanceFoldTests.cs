// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#424. `isinstance(x, T)` on a ZCA instance is not a decision, it is a fold --
/// reduced from adafruit_mcp3xxx's own AnalogIn.__init__, which guards its constructor with
/// `if not isinstance(mcp, MCP3xxx): raise ValueError(...)` and is called with an MCP3008 (a
/// subclass of MCP3xxx defined in a third file).
/// </summary>
[TestFixture]
public class IsInstanceFoldTests
{
    /// <summary>
    /// The discriminating value is 3, the pin number the constructor stores. isinstance()
    /// used to be refused outright as an unsupported Python builtin, so this program did not
    /// build at all; a wrong-but-building reading (the guard mistaken for false, raising)
    /// would never reach "END" at all.
    /// </summary>
    [Test]
    public void Issue423_TheSubclassPassesTheBaseClassCheck()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("isinstance-fold")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("IF\n3\nEND\n");
    }
}
