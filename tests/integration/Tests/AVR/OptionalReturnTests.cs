using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0009 phase 1: `-> Optional[uint8]` on a real subroutine returns payload +
/// tag byte across an actual CALL. The program reads ONE seed byte over UART RX
/// (read_blocking + InjectByte) so neither the None arm nor the value arm can
/// fold at compile time: seed 0 takes `return None`, any other seed returns
/// seed+7. The caller then exercises `is None` narrowing, the else-arm payload
/// read, and `r or 99`.
/// </summary>
[TestFixture]
public class OptionalReturnTests
{
    private const string Source =
        "from pymcu.types import uint8, Optional\n" +
        "from pymcu.hal.uart import UART\n" +
        "from pymcu.hal.console import print\n" +
        "\n" +
        "def read(k: uint8) -> Optional[uint8]:\n" +
        "    if k == 0:\n" +
        "        return None\n" +
        "    return k + 7\n" +
        "\n" +
        "def main():\n" +
        "    uart = UART(9600)\n" +
        "    uart.println(\"GO\")\n" +
        "    s: uint8 = uart.read_blocking()\n" +
        "    r = read(s)\n" +
        "    if r is None:\n" +
        "        print(\"none\")\n" +
        "    else:\n" +
        "        print(r)\n" +
        "    print(r or 99)\n" +
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
    public void SeedZero_TakesTheNoneArm()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x00);
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // read(0) returns None (tag 1): the is-None arm prints, `r or 99` yields 99.
        uno.Serial.Text.Should().Be("GO\nnone\n99\nEND\n",
            "the tag byte must carry None across the CALL boundary");
    }

    [Test]
    public void SeedNonZero_TakesTheValueArm()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x05);           // read(5) -> 12, tag 0
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // The else arm reads the payload (12), and `r or 99` keeps it.
        uno.Serial.Text.Should().Be("GO\n12\n12\nEND\n",
            "a has-value tag must let the payload through the same CALL boundary");
    }

    [Test]
    public void SeedMaxWrapsPayload()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0xFF);           // read(255) -> (255+7)&0xFF = 6, tag 0
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        uno.Serial.Text.Should().Be("GO\n6\n6\nEND\n",
            "the payload wraps at uint8 and the tag still says has-value");
    }
}
