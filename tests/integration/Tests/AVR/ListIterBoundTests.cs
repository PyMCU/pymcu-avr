// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `for v in &lt;list&gt;` must keep its loop bound intact across iterations.
/// The list length lived in a linear-scan temp (R16) whose interval ended at
/// the loop-head compare, so the body's element-address arithmetic reused R16
/// as scratch and the bound became i+2 after one iteration: the loop ran off
/// the end of the list, summed ~251 bytes of arbitrary RAM, and printed a
/// different sum on every pass. Fixed by extending live intervals across loop
/// back-edges in AvrLinearScan. Caught by a logic-analyser differential run
/// against CPython on real silicon.
/// </summary>
[TestFixture]
public class ListIterBoundTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("list-iter-bound"));

    [Test]
    public void ListSums_AreExact_AndStableAcrossPasses()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 2000);
        uno.Serial.Text.Should().Be("100\n612\n100\n612\nD");
    }
}
