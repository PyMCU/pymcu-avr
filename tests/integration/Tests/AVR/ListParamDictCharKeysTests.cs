using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A seven-segment table keyed by CHARACTERS (PyMCU#338).
///
/// A one-character string literal folds to its character code, so these keys are constant
/// integers that are simply not contiguous from zero. Before, the rectangle of rows took only
/// keys 0..N-1, so the table was refused at its first row; and a scalar table of the same
/// glyphs refused a run-time key outright, on the line after an `in` that had just compared the
/// same run-time byte against the same keys.
///
/// Three ways in, all in one program: a constant key, the characters of a constant string, and
/// a byte only known at run time. A code with no glyph is the library's own `raise`, not a
/// compiler refusal.
///
/// Every assertion reads a PORT register. Reading the pin back would fold to the value just
/// written and pass on a firmware that drives nothing.
/// </summary>
[TestFixture]
public class ListParamDictCharKeysTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("list-param-dict-char-keys"));

    private string[] Lines()
    {
        var uno = _session.Reset();
        uno.Data[Gpior0Addr] = (byte)'b';   // the run-time key
        uno.Data[Gpior1Addr] = (byte)'X';   // a code the table has no glyph for
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        return uno.Serial.Text.Split('\n');
    }

    // PORTD & 0xFC is segments a..f (PD2..PD7), PORTB & 0x01 is segment g (PB0). The numbers
    // are the fixture's own glyph rows, computed by CPython.
    [Test]
    public void AConstantCharacterKeyPicksItsGlyph()
        => Lines()[0].Trim().Should().Be("220 1", "'A' is a b c e f g");

    [Test]
    public void TheCharactersOfAConstantStringEachPickTheirGlyph()
    {
        var lines = Lines();
        lines[1].Trim().Should().Be("228 0", "'C'");
        lines[2].Trim().Should().Be("240 1", "'b'");
        lines[3].Trim().Should().Be("0 1", "'-' is the middle segment alone");
    }

    [Test]
    public void AByteKnownOnlyAtRunTimePicksItsGlyph()
        => Lines()[4].Trim().Should().Be("240 1", "GPIOR0 holds 'b'");

    [Test]
    public void ACodeWithNoGlyphIsTheAuthorsRaise()
        => Lines()[5].Trim().Should().Be("X refused");
}
