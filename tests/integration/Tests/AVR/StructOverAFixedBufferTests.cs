using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/struct-over-a-fixed-buffer.
///
/// PyMCU#361. Three spellings adafruit_register.i2c_struct writes and struct could not reach:
/// the result bound to a name and indexed through it, `memoryview(buf)[1:]` as the buffer, and
/// a multi-field format read one field per index. The format arrives as a FIELD in all three,
/// which is how a descriptor holds it.
///
/// The same two bytes are read big-endian and little-endian, so a fix that accepted the
/// spellings and assembled the bytes the wrong way round shows here: 4660 against 13330 is a
/// sensor reading against its halves swapped.
///
/// The fixture did not build at all before. Every number is CPython's.
/// </summary>
[TestFixture]
public class StructOverAFixedBufferTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("struct-over-a-fixed-buffer"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 1000);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    [Test]
    public void AMemoryviewSlice_IsTheOffsetSpelledAnotherWay()
    {
        Boot().Should().StartWith("4660\n", "\">H\" over memoryview(buf)[1:] reads 0x1234");
    }

    [Test]
    public void TheByteOrder_IsTheFormatsAndNotTheHosts()
    {
        Boot().Should().StartWith("4660\n13330\n", "the same two bytes, both ways round");
    }

    [Test]
    public void AMultiFieldFormat_IsReadOneFieldPerIndex()
    {
        Boot().Should().Contain("4660\n22136\n", "\">HH\" at offset 1 is 0x1234 then 0x5678");
    }
}
