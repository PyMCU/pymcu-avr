using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/str-format (PyMCU#56).
///
/// `"val {}".format(x)` used to be refused with a message about nested ZCA member access,
/// describing a program that does not exist. It lowers to the f-string it already is.
///
/// The output is what pins the parts image equality cannot: explicit indices picking the
/// right arguments, and a format spec surviving the rewrite.
/// </summary>
[TestFixture]
public class StrFormatTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("str-format"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        return uno;
    }

    [Test]
    public void ExplicitIndices_PickTheRightArguments()
    {
        Boot().Serial.Text.Should().Contain("2-1\n", "{1}-{0} is b then a");
    }

    [Test]
    public void FormatSpec_SurvivesTheRewrite()
    {
        Boot().Serial.Text.Should().Contain("hex ff\n", "{:02x} formats 255 as ff");
    }

    [Test]
    public void FormatCall_IsTheSameImageAsTheFString()
    {
        const string withFormat =
            "from pymcu.hal.console import print\n" +
            "from pymcu.types import uint8\n" +
            "\n" +
            "\n" +
            "def main():\n    x: uint8 = 5\n    print(\"val {}\".format(x))\n";
        const string withFString =
            "from pymcu.hal.console import print\n" +
            "from pymcu.types import uint8\n" +
            "\n" +
            "\n" +
            "def main():\n    x: uint8 = 5\n    print(f\"val {x}\")\n";

        PymcuCompiler.BuildSource(withFormat).Should().Be(PymcuCompiler.BuildSource(withFString));
    }
}
