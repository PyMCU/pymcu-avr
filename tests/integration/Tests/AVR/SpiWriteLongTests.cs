using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using AVR8Sharp.Core.Peripherals;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/spi-write-long: an SPI write longer than 255 bytes has to reach the
/// bus whole.
///
/// The same narrow count the I2C path carried: machine.SPI.write hands len(buf)
/// to pymcu.hal.avr.spi.SPI.write_bytes and on to spi_write_bytes, all declared
/// uint8, so a 256-byte write transferred nothing and a 300-byte one
/// transferred forty-four. This is the SPI side of the MAX7219 and the SD card,
/// where a frame is longer than a byte can count.
/// </summary>
[TestFixture]
public class SpiWriteLongTests
{
    private static string _hex = null!;
    private static string _pyHex = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _hex   = PymcuCompiler.BuildFixture("spi-write-long");
        _pyHex = PymcuCompiler.BuildFixturePyParser("spi-write-long");
    }

    [Test]
    public void EveryWrite_ReachesTheBusWhole() => AssertCount(_hex, "C# front end");

    [Test]
    public void EveryWrite_ReachesTheBusWhole_PythonFrontEnd() => AssertCount(_pyHex, "Python front end");

    private static void AssertCount(string hex, string what)
    {
        var seen = new List<byte>();
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.AddSpi(AvrSpi.SpiConfig, out var spi);
        spi.OnTransfer = b => { seen.Add(b); return 0; };
        uno.RunMilliseconds(400);

        seen.Count.Should().Be(255 + 256 + 300,
            $"{what}: the three writes are 255, 256 and 300 bytes; a byte-wide count " +
            "sends 255, 0 and 44");
        seen[0].Should().Be(0xA1, $"{what}: the first write's own first byte");
        seen[255].Should().Be(0xA2, $"{what}: the second write starts where the first ends");
        seen[255 + 256].Should().Be(0xA3, $"{what}: and the third after that");
    }
}
