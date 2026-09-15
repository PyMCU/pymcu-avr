// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#420. A class inherited across a module boundary -- reduced from
/// adafruit_mcp3xxx, where MCP3008(MCP3xxx) declares no __init__ of its own and inherits one
/// from MCP3xxx, defined in a different file.
/// </summary>
[TestFixture]
public class CrossModuleInheritanceTests
{
    /// <summary>
    /// The discriminating value is 10 (4 + 6, the two constructor arguments this call passed).
    /// The earlier bug's synthesized no-op constructor took no arguments at all, so this
    /// program did not build; a wrong-but-building reading (both fields left at 0) would
    /// print 0, not 10.
    /// </summary>
    [Test]
    public void Issue420_TheCrossModuleBaseConstructorRuns()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("cross-module-inheritance")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("XM\n10\nEND\n");
    }
}
