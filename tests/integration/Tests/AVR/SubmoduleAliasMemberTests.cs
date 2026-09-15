// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#422. `import pkg.submodule as alias` mangled a member read through the alias
/// keeping the literal dot in the module name, instead of the underscored form the module's
/// own scan prefix is registered under. Reduced from adafruit_mcp3xxx's own
/// `import adafruit_mcp3xxx.mcp3008 as MCP` / `MCP.P0`.
/// </summary>
[TestFixture]
public class SubmoduleAliasMemberTests
{
    [Test]
    public void Issue422_AConstantReadThroughADottedSubmoduleAliasResolves()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("submodule-alias-member")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("SA\n0\n1\nEND\n");
    }
}
