using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/while-condition-call (PyMCU#118).
///
/// `while c.bump() &lt; 4:` built clean and never ended. The condition runs once per
/// iteration, but it was lowered against the constants the loop was entered with, so the
/// call folded to the value of its first evaluation, the comparison disappeared, and the
/// jump back to the top became unconditional. The code after the loop was not in the image
/// at all, which is why the test insists on seeing what comes after it.
/// </summary>
[TestFixture]
public class WhileConditionCallTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("while-condition-call"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno;
    }

    [Test]
    public void TheLoopRunsExactlyThreeTimes()
    {
        Boot().Serial.Text.Should().Contain("tick\ntick\ntick\n99\n",
            "bump() returns 1, 2, 3 and then the 4 that ends the loop");
    }

    [Test]
    public void TheCodeAfterTheLoopIsReached()
    {
        Boot().Serial.Text.Should().Contain("99\ndone\n", "an unconditional jump back left this out of the image");
    }
}
