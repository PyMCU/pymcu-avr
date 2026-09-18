using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/return-buffer.
///
/// `data = f()` where f does `return <local bytearray>` is the register-read
/// shape every I2C/SPI driver writes (adafruit_bmp280's `_read_register`). The
/// callee's buffer is a fixed static slot like every local, so the name is
/// what travels back: the receiving variable is bound as another alias of the
/// same storage, and `data[i]`, `data[i] = v`, `len(data)` and `for` answer it.
///
/// The seed (GPIOR0 = 5) fills the buffer on the chip.
/// </summary>
[TestFixture]
public class ReturnBufferTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("return-buffer"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void ReturnedBuffer_IsIndexable()
        => Transcript().Should().Contain("A 5\nB 7\n", "data[i] reads the callee's buffer");

    [Test]
    public void ReturnedBuffer_KeepsItsLength()
        => Transcript().Should().Contain("D 3\n", "len(data) is the buffer's element count");

    [Test]
    public void ReturnedBuffer_IsWritableAndIterable()
        => Transcript().Should().Contain("E 99\nF 99\nF 6\nF 7\n",
            "a store through the alias reaches the same slot the for-in walk reads");
}
