using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outline-on-init (PyMCU#104).
///
/// `@outline` on __init__ failed the build saying the class had no __init__ method. The
/// decorator is ignored on a constructor now, so the fixture only has to prove the class
/// still constructs and answers with the value it was given.
/// </summary>
[TestFixture]
public class OutlineOnInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outline-on-init"));

    [Test]
    public void TheClassConstructsAndAnswers()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("5\n", "GPIOR0 reads 0, so the field holds 4 and g() adds one");
    }
}
