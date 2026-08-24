using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `import board` without a declared board (PyMCU#63).
///
/// The message blamed the project setup -- install the compat package, set
/// stdlib = ["circuitpython"] -- and a user who had already done both had nowhere to go.
/// `board` is not shipped by any package: the driver generates it from the board the
/// project declares, so the missing piece is the `board = "..."` line.
///
/// The working spelling is covered by the compat-cp-* fixtures, which all declare a board
/// and import it; this pins the message for the case that does not.
/// </summary>
[TestFixture]
public class BoardModuleMessageTests
{
    [Test]
    public void ImportBoardWithoutABoard_PointsAtTheBoardLine()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PymcuCompiler.BuildSource(
            "import board\n\n\ndef main():\n    x: uint8 = 0\n"));

        ex!.Message.Should().Contain("generated from the board this project declares");
        ex.Message.Should().Contain("board = ", "the message must show the line to add");
        ex.Message.Should().NotContain("pip install -r requirements.txt",
            "the compat-package advice is what the reader has already followed");
    }
}
