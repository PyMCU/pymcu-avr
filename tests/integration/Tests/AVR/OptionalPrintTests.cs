using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0009 section 8, option A: an unnarrowed read of a runtime-tagged Optional
/// is legal where the use site can represent BOTH outcomes -- print() and
/// f-string interpolation are the first such sites. The tag picks the text
/// CPython writes: the payload through the member's own writer, or the literal
/// "None". The same UART-seed shape as <see cref="OptionalReturnTests"/> keeps
/// the member undecidable at compile time.
/// </summary>
[TestFixture]
public class OptionalPrintTests
{
    private const string Source =
        "from pymcu.types import uint8, Optional, Union\n" +
        "from pymcu.hal.uart import UART\n" +
        "from pymcu.hal.console import print\n" +
        "\n" +
        "def read(k: uint8) -> Optional[uint8]:\n" +
        "    if k == 0:\n" +
        "        return None\n" +
        "    return k + 7\n" +
        "\n" +
        "def readf(k: uint8) -> Union[int, float, None]:\n" +
        "    if k == 0:\n" +
        "        return None\n" +
        "    if k == 1:\n" +
        "        return 23\n" +
        "    return k + 0.5\n" +
        "\n" +
        "def main():\n" +
        "    uart = UART(9600)\n" +
        "    uart.println(\"GO\")\n" +
        "    s: uint8 = uart.read_blocking()\n" +
        "    r = read(s)\n" +
        "    print(r)\n" +
        "    print(f\"r={r}\")\n" +
        "    print(read(s))\n" +
        "    v = readf(s)\n" +
        "    print(v)\n" +
        "    print(f\"v={v}\")\n" +
        "    print(\"END\")\n" +
        "\n" +
        "main()\n";

    private static readonly Lazy<string> Hex = new(() => PymcuCompiler.BuildSource(Source));

    private ArduinoUnoSimulation Sim()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(Hex.Value);
        return uno;
    }

    [Test]
    public void SeedZero_PrintsNoneAtEverySite()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x00);
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // read(0) / readf(0) return None: every unnarrowed print site writes the
        // literal CPython spelling, and the f-string keeps its prefix beside it.
        uno.Serial.Text.Should().Be("GO\nNone\nr=None\nNone\nNone\nv=None\nEND\n",
            "the tag byte must drive \"None\" at print() and f-string sites alike");
    }

    [Test]
    public void SeedNonZero_PrintsThePayloadAtEverySite()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x05);           // read(5) -> 12, readf(5) -> 5.5
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        uno.Serial.Text.Should().Be("GO\n12\nr=12\n12\n5.5\nv=5.5\nEND\n",
            "the has-value tag must print the payload through its own writer");
    }

    [Test]
    public void SeedOne_PrintsTheIntMemberOfTheUnion()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x01);           // read(1) -> 8, readf(1) -> 23 (int member)
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // The int member prints "23", not "23.0" -- each member gets its own repr.
        uno.Serial.Text.Should().Be("GO\n8\nr=8\n8\n23\nv=23\nEND\n",
            "the tag must select the int member's decimal writer, not float's");
    }
}
