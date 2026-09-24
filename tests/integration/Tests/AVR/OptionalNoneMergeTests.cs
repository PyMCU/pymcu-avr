using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0009: a name written a real value AND None under a runtime guard carries a
/// live tag byte -- the arms merge, so which member the storage holds is only
/// decidable at run time. The program reads ONE seed byte over UART RX: seed > 3
/// takes the `x = None` arm. The merged name then exercises `is None` narrowing,
/// a tagged `Optional[int]` parameter call (the caller remaps the tag between the
/// two member lists), `x or default`, and `isinstance` dispatch.
///
/// A sequential `x = None; x = f()` in the same statement list is NOT this shape:
/// nothing can carry the None state into a join, so the name stays compile-time
/// and emits no tag byte at all.
/// </summary>
[TestFixture]
public class OptionalNoneMergeTests
{
    private const string Source =
        "from pymcu.types import uint8, Optional\n" +
        "from pymcu.hal.uart import UART\n" +
        "from pymcu.hal.console import print\n" +
        "\n" +
        "def put(v: Optional[int]) -> int:\n" +
        "    if v is None:\n" +
        "        return -1\n" +
        "    return v + 1\n" +
        "\n" +
        "def main():\n" +
        "    uart = UART(9600)\n" +
        "    uart.println(\"GO\")\n" +
        "    s: uint8 = uart.read_blocking()\n" +
        "    x = 5\n" +
        "    if s > 3:\n" +
        "        x = None\n" +
        "    if x is None:\n" +
        "        print(\"none\")\n" +
        "    else:\n" +
        "        print(x)\n" +
        "    print(put(x))\n" +
        "    print(x or 99)\n" +
        "    if isinstance(x, int):\n" +
        "        print(\"int\")\n" +
        "    else:\n" +
        "        print(\"not-int\")\n" +
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
    public void ValueArm_TagReadsNotNone()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x00);           // s = 0: x stays 5, tag 0
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // x is 5: the else arm prints it, put(5) -> 6, `x or 99` keeps 5,
        // isinstance(x, int) narrows to the int member.
        uno.Serial.Text.Should().Be("GO\n5\n6\n5\nint\nEND\n",
            "the merged name's tag must say has-value so every reader takes the value arm");
    }

    [Test]
    public void NoneArm_TagReadsNone()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(0x09);           // s = 9 > 3: x = None, tag 1
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);

        // x is None: the is-None arm prints, put -> -1, `x or 99` yields 99, and
        // isinstance reads the tag rather than the stale payload (5 still sits in it).
        uno.Serial.Text.Should().Be("GO\nnone\n-1\n99\nnot-int\nEND\n",
            "the merged name's tag must say None -- readers must not see the stale payload");
    }
}
