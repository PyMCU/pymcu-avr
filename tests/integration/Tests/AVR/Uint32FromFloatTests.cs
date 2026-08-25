using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/uint32-from-float (pymcu-avr#8).
///
/// uint32(f) lowered to __fixsfsi, which reads its result back as a signed int32 and
/// saturates at 0x80000000, so the whole upper half of the uint32 range was unreachable
/// through a float: uint32(3000000000.0) was 2147483648, and so was every larger value.
/// Same signed/unsigned split as pymcu-avr#7, in the other direction.
///
/// The narrower casts are pinned because they must keep the signed helper: the value is
/// truncated from the int32 afterwards, and that is what makes uint8(-3.5) come out as 253.
/// </summary>
[TestFixture]
public class Uint32FromFloatTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("uint32-from-float"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 4000);
        return uno.Serial.Text;
    }

    [Test]
    public void TheTopHalfOfTheRangeIsReachable()
    {
        Boot().Should().StartWith("3000000000\n4294967040\n",
            "both of these used to saturate at 2147483648");
    }

    [Test]
    public void TheUnsignedHelperStillWrapsANegative()
    {
        Boot().Should().Contain("1294967296\n4294867296\n",
            "uint32(-3000000000.0) is int(-3e9) mod 2**32, and uint32(-100000.0) is 4294867296, not 0");
    }

    [Test]
    public void ValuesBelowTwoToThe31AreUnchanged()
    {
        Boot().Should().Contain("100000\n-100000\n");
    }

    [Test]
    public void ANarrowerCastKeepsTheSignedHelper()
    {
        Boot().Should().Contain("253\n-3\n31072\n",
            "uint8(-3.5) is 253 because the int32 is truncated afterwards, not clamped to 0");
    }
}
