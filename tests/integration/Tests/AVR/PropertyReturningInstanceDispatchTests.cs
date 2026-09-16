using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/property-returns-instance.
///
/// PyMCU#445. A @property getter returning a ZCA instance (`return self._q`) lost that
/// instance for further method dispatch: `owner.q.bump()` refused with "its receiver is not
/// a name bound to an object...". The getter is force-inlined at its call site like any other
/// instance method, and its result -- a single-field ZCA instance -- is an alias of the
/// field's own flattened storage rather than a name carrying a class directly; two dispatch
/// call sites checked for a class one hop short of that alias chain.
///
/// Every expectation is CPython's answer for the same lines.
/// </summary>
[TestFixture]
public class PropertyReturningInstanceDispatchTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("property-returns-instance"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno;
    }

    [Test]
    public void EachBumpThroughTheProperty_AccumulatesOnTheSameStorage()
    {
        var text = Boot().Serial.Text;
        text.Should().Contain("1\n", "the first owner.q.bump() must reach the real field");
        text.Should().Contain("2\n", "the second bump must accumulate on the SAME storage");
        text.Should().Contain("3\n", "the third bump must accumulate on the SAME storage");
    }
}
