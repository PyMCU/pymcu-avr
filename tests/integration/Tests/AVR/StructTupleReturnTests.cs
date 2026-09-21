using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/struct-tuple-return.
///
/// `return struct.unpack_from(fmt, buf)` delivering the multi-field result out of an
/// inline expansion -- adafruit_register i2c_struct's `Struct.__get__` shape -- and the
/// consumers the register_simpletest drives it through: `a, b = ...`, `...[k]`,
/// `print(...)`, `"...".format(*...)`, and the `__set__` write half doing
/// `pack_into(fmt, buf, 1, *value)` for a two-field format.
///
/// The seed (GPIOR0 = 5) fills the buffer on the chip so nothing folds at compile time.
/// The descriptor half is a round trip: `__set__` packs into `_BUF`, `__get__` reads it
/// back, so the printed values prove BOTH directions landed on the same bytes.
/// </summary>
[TestFixture]
public class StructTupleReturnTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;
    private SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("struct-tuple-return"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("struct-tuple-return"));
    }

    private string Transcript(SimSession s)
    {
        var uno = s.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    private string Transcript() => Transcript(_session);

    [Test]
    public void UnpackFrom_ReturnsTheTuple_AndUnpacksIntoTargets()
        => Transcript().Should().Contain("A 1541\nB 2055\n",
            "a, b = read_pair() binds the two u16 fields at full width");

    [Test]
    public void UnpackFrom_ReturnsTheTuple_AndIndexesIt()
        => Transcript().Should().Contain("C 1541\n",
            "read_pair()[0] reads the first field's slot");

    [Test]
    public void UnpackFrom_TuplePrintsTheTupleText()
        => Transcript().Should().Contain("P (1541, 2055)\n",
            "print(read_pair()) writes what CPython prints for the tuple");

    [Test]
    public void Format_SplicesTheTupleReturn()
        => Transcript().Should().Contain("F 1541:2055\n",
            "\"{}:{}\".format(*read_pair()) fills one placeholder per field");

    [Test]
    public void Descriptor_SetThenGet_RoundTrips()
        => Transcript().Should().Contain("S 4660 32768\n",
            "d.pair = (0x1234, 0x8000) packs through __set__; x, y = d.pair reads it back");

    [Test]
    public void Descriptor_GetIndexesTheTuple()
        => Transcript().Should().Contain("D 4660\n",
            "d.pair[0] reads the first field of the descriptor's tuple");

    [Test]
    public void Descriptor_GetSplicesIntoFormat()
        => Transcript().Should().Contain("R register 1: 4660; register 2: 32768\n",
            "the register_simpletest line: .format(*d.pair)");

    [Test]
    public void Descriptor_GetPrintsTheTupleText()
        => Transcript().Should().Contain("T (4660, 32768)\n",
            "print(d.pair) writes the tuple the getter returned");

    [Test]
    public void Descriptor_SecondWrite_RerunsSet()
        => Transcript().Should().Contain("V register 1: 255; register 2: 1\n",
            "d.pair = (0x00FF, 1) calls __set__ again; the read sees the new bytes");

    [Test]
    public void PyParser_Agrees()
        => Transcript(_pySession).Should().Be(Transcript(),
            "the Python front end must lower the same program identically");
}
