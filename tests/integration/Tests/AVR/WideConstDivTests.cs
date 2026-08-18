// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A constant operand wider than 16 bits must widen the whole operation.
/// GetValType capped constants at UINT16, so (1100*1024) // avg with a
/// runtime avg divided 12288 (1126400 mod 2^16) instead and returned 53 for
/// 4897. Caught computing Vcc from the 1.1 V bandgap on a real Uno.
/// </summary>
[TestFixture]
public class WideConstDivTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("wide-const-div"));

    [Test]
    public void WideConstantOperand_ForcesThe32BitDivision()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 2000);
        uno.Serial.Text.Should().Be("4897\nD");
    }
}
