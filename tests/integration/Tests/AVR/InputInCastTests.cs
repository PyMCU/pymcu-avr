using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `uint8(input("n: "))` (PyMCU#60).
///
/// input() worked as the whole right-hand side of an annotated assignment, and casting the
/// buffer afterwards worked too, but composing them reported "call to undefined function
/// 'input'" -- which sends the reader hunting for an import while the same call one line up
/// compiles. The one-line form is what a user writes first, because it is what the prompt
/// is for.
///
/// Asserted as image equality against the two-statement spelling: the desugaring must BE
/// those two statements, not a second path with its own behaviour.
/// </summary>
[TestFixture]
public class InputInCastTests
{
    private const string OneLine =
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    n: uint8 = uint8(input(\"n: \"))\n";

    private const string TwoStatements =
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    line: bytearray = input(\"n: \")\n" +
        "    n: uint8 = uint8(line)\n";

    [Test]
    public void InputInsideACast_IsTheSameImageAsTheTwoStatements()
    {
        PymcuCompiler.BuildSource(OneLine).Should().Be(PymcuCompiler.BuildSource(TwoStatements));
    }
}
