using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `print("text " + str(x))` with a run-time x (PyMCU#78).
///
/// It was refused with "str() argument must be a compile-time constant integer", which names
/// a constraint and not the two spellings that do work. It is how message-building is taught,
/// and what every tutorial written before f-strings shows.
///
/// Asserted as image equality against the f-string, because the concatenation IS that
/// f-string: same output, same streamed lowering, nothing built in RAM.
/// </summary>
[TestFixture]
public class StrConcatTests
{
    private const string Concatenated =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    v: uint8 = p.value()\n" +
        "    print(\"valor: \" + str(v))\n";

    private const string Interpolated =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    v: uint8 = p.value()\n" +
        "    print(f\"valor: {v}\")\n";

    [Test]
    public void ConcatenationWithStr_IsTheSameImageAsTheFString()
    {
        PymcuCompiler.BuildSource(Concatenated).Should().Be(PymcuCompiler.BuildSource(Interpolated));
    }
}
