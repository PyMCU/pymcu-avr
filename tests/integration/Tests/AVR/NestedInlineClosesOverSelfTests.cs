using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for fixtures/nested-inline-closes-over-self -- PyMCU#427. A nested
/// function decorated @inline, defined inside a method and reading/writing `self` of the
/// enclosing method, is the documented closure pattern (docs/language/limitations.md:287).
/// Nothing forwarded the enclosing method's own `self` binding into the nested function's own
/// fresh inline frame, so every write inside it was invisible outside the call:
/// Counter(5).bump_twice() printed 5 (bump() a no-op) instead of 7 (5 + 1 + 1).
/// </summary>
[TestFixture]
public class NestedInlineClosesOverSelfTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("nested-inline-closes-over-self"));

    [Test]
    public void NestedInlineFunctionsWriteThroughToTheEnclosingSelf()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("7"), maxMs: 400);
        uno.Serial.Text.Should().Contain("7",
            "bump() must actually increment self.value each call: 5 + 1 + 1 = 7, not a silent no-op");
    }
}
