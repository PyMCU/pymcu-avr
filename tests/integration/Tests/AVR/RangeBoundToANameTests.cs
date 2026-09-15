using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/range-bound-to-a-name.
///
/// PyMCU#363. A range whose bounds fold was refused the moment it was given a name, on a
/// program whose only use of the name is the `for` loop the refusal asks for. `reversed(order)`
/// -- a form the message itself offered -- was refused on the same line.
///
/// The fixture reads the same two bytes in both orders, so a fix that accepted the name but
/// walked it the wrong way round shows here. That is the whole point of the shape in
/// adafruit_register.i2c_bits: one name, two branches, one loop, and the byte order is the
/// difference between a register value and its halves swapped.
///
/// Expected, with buf = 00 12 34 56: 52 18 18 52 18 52 52 18. Every number is CPython's.
/// </summary>
[TestFixture]
public class RangeBoundToANameTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("range-bound-to-a-name"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    [Test]
    public void TheWholeWalk_IsTheOrderCPythonWalks()
    {
        Boot().Should().Be("52\n18\n18\n52\n18\n52\n52\n18\ndone\n",
            "the descending range, its reverse, and both again through a bare name");
    }

    [Test]
    public void ADescendingRangeThroughAField_ReadsHighByteFirst()
    {
        Boot().Should().StartWith("52\n18\n", "range(self.width, 0, -1) visits 2 then 1");
    }

    [Test]
    public void ReversedOfTheSameName_ReadsLowByteFirst()
    {
        Boot().Should().Contain("18\n52\n", "reversed(order) visits 1 then 2");
    }
}
