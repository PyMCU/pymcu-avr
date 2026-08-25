using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/divmod-same-operands (pymcu-avr#10).
///
/// `//` and `%` over the same operands are fused into one division, and when the allocator
/// gave both destinations the same register the quotient was overwritten by the remainder, so
/// both answered 5 for 75. The two results differ here, which is what makes the failure
/// visible: with a dividend that is a multiple of the divisor both answers would be plausible.
/// </summary>
[TestFixture]
public class DivModSameOperandsTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("divmod-same-operands"));

    [Test]
    public void TheQuotientIsNotTheRemainder()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("7\n5\ndone\n", "75 // 10 is 7 and 75 % 10 is 5");
    }
}
