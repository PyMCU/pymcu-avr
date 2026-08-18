// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// for-in over an array slice with runtime bounds rewrites to the equivalent
/// range loop with per-iteration ArrayLoads. Sum over buf[0:n], a window with
/// expression bounds, and break inside the rewritten body. Verified on a real
/// Uno (sum 266 wraps to 10 as uint8 before widening to the formatter).
/// </summary>
[TestFixture]
public class RuntimeSliceIterTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("runtime-slice-iter"));

    [Test]
    public void RuntimeBounds_SumWindowAndBreak()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be("10\n67\n68\n66\nD");
    }
}
