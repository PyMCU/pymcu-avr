using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/factory-field-read (half of PyMCU#49).
///
/// Reading a field of an instance returned by a factory gave 0 while calling a method on the
/// same instance gave the right answer. Both are checked, in that order, because the method
/// call was never the broken half and a test that only did that would have passed throughout.
/// </summary>
[TestFixture]
public class FactoryFieldReadTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("factory-field-read"));

    [Test]
    public void TheMethodCallAnswers()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().StartWith("5\n");
    }

    [Test]
    public void TheFieldReadAnswersToo()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("5\n4\ndone\n", "o.a is the handle the factory returned");
    }
}
