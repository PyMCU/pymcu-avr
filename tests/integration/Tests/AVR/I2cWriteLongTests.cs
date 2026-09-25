using FluentAssertions;
using NUnit.Framework;
using PyMCU.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/i2c-write-long: an I2C write longer than 255 bytes has to reach the
/// wire whole.
///
/// The byte count travelled from machine.I2C.writeto through
/// pymcu.hal.avr.i2c.I2C.write_bytes into i2c_write_bytes in a uint8, so it
/// arrived as len &amp; 0xFF and the loop sent that many bytes: 255 whole, 256
/// nothing at all, 300 forty-four, and the 513 of an SSD1306 frame exactly one.
/// Nothing was said, on the bus or at compile time -- the count is a folded
/// len(), not a literal, so the narrowing refusal never looked at it.
///
/// Both hops matter: widening only i2c_write_bytes leaves the wrapper narrow
/// and the frame still arrives as one byte.
/// </summary>
[TestFixture]
public class I2cWriteLongTests
{
    private const byte Addr = 0x3C;
    private static string _hex = null!;
    private static string _pyHex = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _hex   = PymcuCompiler.BuildFixture("i2c-write-long");
        _pyHex = PymcuCompiler.BuildFixturePyParser("i2c-write-long");
    }

    [Test]
    public void EveryWrite_ReachesTheWireWhole() => AssertLengths(_hex, "C# front end");

    [Test]
    public void EveryWrite_ReachesTheWireWhole_PythonFrontEnd() => AssertLengths(_pyHex, "Python front end");

    private static void AssertLengths(string hex, string what)
    {
        var run = UnoTwiTrace.Record(hex, Addr, 4, maxMs: 4000);
        var writes = run.Transactions.Where(t => t.IsWrite).ToList();

        writes.Should().HaveCount(4, $"{what}: four writes were asked for");
        writes[0].Data.Length.Should().Be(255, $"{what}: 255 fits a byte and always arrived");
        writes[1].Data.Length.Should().Be(256, $"{what}: 256 & 0xFF is 0, which used to send nothing");
        writes[2].Data.Length.Should().Be(300, $"{what}: 300 & 0xFF is 44");
        writes[3].Data.Length.Should().Be(513, $"{what}: an SSD1306 frame, 513 & 0xFF is 1");
        writes[3].Data[0].Should().Be(0x40, $"{what}: the frame's own first byte, not another buffer's");
    }
}
