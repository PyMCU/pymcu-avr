using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/struct-unpack (PyMCU#361).
///
/// `t = struct.unpack(fmt, buf)` and `coeff = list(struct.unpack(fmt, bytes(buf)))`
/// bind the multi-field result as a compile-time sequence of typed reads -- the
/// coefficient-read shape adafruit_bmp280's `_read_coefficients` writes. Each
/// field keeps its own width and sign, the float comprehension re-reads them,
/// and slices of the rebound name land on `self` fields.
///
/// The seed (GPIOR0 = 5) fills the buffer on the chip; raw[5] is overwritten
/// with 0x80 so the `h` field at offset 4 goes negative.
/// </summary>
[TestFixture]
public class StructUnpackTests
{
    private const int Gpior0Addr = 0x3E;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("struct-unpack"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = 5;
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void Unpack_BoundResultIndexesAndMeasures()
        => Transcript().Should().Contain("A 1541\nB 2055\nC 2\n",
            "a bound unpack result indexes its fields and answers len()");

    [Test]
    public void UnpackFrom_OffsetAndBigEndian()
        => Transcript().Should().Contain("D 1800\nE 2432\n",
            "unpack_from honours its offset and the '>' byte order");

    [Test]
    public void Unpack_SignedFieldReadsNegative()
        => Transcript().Should().Contain("N -32759\n",
            "an h field whose high byte has the sign bit set reads negative");

    [Test]
    public void Unpack_FloatComprehensionAndFieldSlices()
        => Transcript().Should().Contain(
            "F 1541\nG -32759\nH 3083\nI 3\nJ 9\nK 7195\n",
            "the bmp280 chain: list(unpack) -> [float(i) ...] -> self fields = slices");

    [Test]
    public void Unpack_BoundResultIterates()
        => Transcript().Should().Contain("L 11\n",
            "for-in over the bound result visits each field");

    [Test]
    public void UnpackFrom_MemoryviewSliceAddsItsStartToTheOffset()
        => Transcript().Should().Contain("O 1800\n",
            "unpack_from(fmt, memoryview(buf)[2:]) reads buf[2], buf[3] -- the view's "
            + "start is a byte offset, not a copied buffer");

    [Test]
    public void UnpackFrom_NamedMemoryviewSliceUnpacks()
        => Transcript().Should().Contain("P -32759\n",
            "a memoryview slice bound to a name still unpacks the underlying bytes");

    [Test]
    public void UnpackFrom_PlainMemoryviewHonoursTheOffsetArg()
        => Transcript().Should().Contain("Q 1800\n",
            "memoryview(buf) without a slice reads at the unpack_from offset");

    [Test]
    public void Unpack_LongQualifiedNamesShortenToValidSymbols()
        => Transcript().Should().Contain("M 1541\n",
            "unrolled `coeff__N` elements in a long-named method must not shorten to "
            + "a bare digit -- `.equ 0` is not a valid assembler symbol");
}
