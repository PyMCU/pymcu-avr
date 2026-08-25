using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/async-sleep-float (PyMCU#109).
///
/// `await asyncio.sleep(0.5)` never woke. The elapsed microseconds were converted to float
/// through a path that read only their low 16 bits (pymcu-avr#7), so the comparison against
/// the deadline never came true. Two sleeps in a row, because waking once could be luck.
/// </summary>
[TestFixture]
public class AsyncSleepFloatTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("async-sleep-float"));

    [Test]
    public void AFractionalSleepWakes()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        uno.Serial.Text.Should().Contain("one\ntwo\ndone\n");
    }
}
