using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `uart.any()` (PyMCU#59): MicroPython's spelling of `available()`.
///
/// The method existed under the other name and the error said the function was undefined,
/// naming a mangled symbol. Asserted as image equality, so the alias cannot drift into a
/// second implementation with its own behaviour.
/// </summary>
[TestFixture]
public class UartAnyTests
{
    private static string Program(string method) =>
        "from pymcu.hal.uart import UART\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    u = UART(9600)\n" +
        "    while True:\n" +
        "        if u." + method + "():\n" +
        "            c: uint8 = u.read()\n" +
        "            u.write(c)\n";

    [Test]
    public void Any_IsTheSameImageAsAvailable()
    {
        PymcuCompiler.BuildSource(Program("any")).Should().Be(PymcuCompiler.BuildSource(Program("available")),
            "any() is available() under the name MicroPython uses");
    }
}
