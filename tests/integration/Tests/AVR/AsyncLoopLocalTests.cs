using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/async-loop-local (PyMCU#115).
///
/// A local accumulated in an await-free `for` and read after the await came out 0: the
/// rewriter passed the loop through whole, so the body wrote the plain local while the rest
/// of the coroutine read the promoted field.
/// </summary>
[TestFixture]
public class AsyncLoopLocalTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("async-loop-local"));

    [Test]
    public void TheLoopAccumulatesIntoTheNameTheCoroutineReads()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 2000);
        uno.Serial.Text.Should().Contain("6\n", "0 + 1 + 2 + 3, read back after the await");
    }
}
