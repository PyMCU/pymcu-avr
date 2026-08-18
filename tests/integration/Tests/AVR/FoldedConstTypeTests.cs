// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Unannotated locals assigned from constant-folded expressions must get the
/// type the equivalent literal would get. The folded Constant carried no type,
/// so `x = -7` (a UnaryExpr, not an IntegerLiteral) fell to the UINT8 default
/// and stored 249, `x = 1 &lt;&lt; 9` stored 0, and `x = -7; x &lt; 0` folded to
/// false. Verified against CPython on real silicon before the fix in
/// IRGenerator/Assign.cs.
/// </summary>
[TestFixture]
public class FoldedConstTypeTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("folded-const-type"));

    [Test]
    public void FoldedConstants_MatchCPython()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 2000);
        uno.Serial.Text.Should().Be(
            "-7\n-300\n-7\n400\n512\n300\n-10\n-7\n256\n333\n-15\nD");
    }
}
